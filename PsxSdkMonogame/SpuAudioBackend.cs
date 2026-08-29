using System;
using System.Threading;
using Microsoft.Xna.Framework.Audio;
using PsxSdkMonogame;

namespace PsxSdkMonogame;

// JUSTIFICATION: backend MonoGame only
// RELATION: pumps SpuCore.RenderSamples into a MonoGame DynamicSoundEffectInstance. This is pure
// desktop audio plumbing — it owns no PSX/game semantics, only the buffer scheduling needed to keep
// a continuous 44100 Hz stereo stream fed from the virtual SPU. Independent of the game layer.
//
// LL1 (2026-08-21, coordinator finding from KK1's correlation): this used to submit buffers from
// DynamicSoundEffectInstance.BufferNeeded, on the belief (this class's own prior header note, now
// corrected) that MonoGame raises that event from "a dedicated audio thread". It does not — WindowsDX
// raises BufferNeeded from FrameworkDispatcher.Update(), called by Game.Update on the GAME thread,
// once per frame. That made SpuCore.RenderSamples — and with it AkaoTick, the track/voice walks,
// FUN_8008900c — advance only when the game loop pumped, with ~2-3 buffers (~50-75ms) of slack: any
// frame hitch past that slack is a buffer underrun, i.e. silence, and KK1 measured the game thread
// stalling ~2s every ~3.5s, matching the observed silence bursts exactly. The offline harness (II1)
// never showed this because it calls RenderSamples in a tight loop with no game thread in the path.
// On real PSX hardware the SPU and its RCnt2 ISR run independently of the CPU — a dedicated desktop
// audio thread, decoupled from the game/render loop, is the faithful desktop equivalent (also the
// user's own stated design decision that AkaoTick is clocked off the audio clock, not the frame
// loop). This class now owns that thread; nothing above this file (Akao.cs, StaticVariables.cs, the
// tick callback contract) changes.
//
// MM1 (2026-08-21, coordinator): the periodic (every ~2s) call into SpuWavDumpDiagnostics.Flush()
// used to run directly on this audio pump thread and rewrite the whole accumulated WAV file to
// disk each time -- flagged (LL3) as a likely cause of residual silence gaps even after LL1.
// SpuWavDumpDiagnostics now drains and writes on its own background thread (see its own MM1 note),
// so this class no longer calls Flush() from the hot path at all -- only once, at Stop()/Dispose(),
// which already runs on the game thread, never RenderSamples. The throttled PE_AUDIO_DIAG line
// below also no longer calls Console.WriteLine directly (it can block on a redirected pipe); it
// goes through AsyncConsole instead, for the same reason.
//
// SD1 (2026-08-29): shutdown lifecycle. MonoGame owns the DynamicSoundEffectInstance as well as
// this class does -- Game.Dispose() shuts the audio subsystem down and disposes every live
// instance, ours included. Nothing used to stop this pump thread at application exit (Dispose()
// had no caller at all, and Stop() only ran on the Akao DisableEvent path), so the thread went on
// calling PendingBufferCount on an instance MonoGame had just disposed, and the resulting
// ObjectDisposedException on a thread with no handler took the process down on the way out. Two
// changes close it: the host now disposes the backend before MonoGame tears audio down (see
// GameRemaster.OnExiting), and every instance touch below is guarded so this thread survives the
// device disappearing under it in any ordering this class does not control.
public sealed class SpuAudioBackend : IDisposable
{
    private const int SampleRate = 44100;
    private const AudioChannels Channels = AudioChannels.Stereo;

    // ~25 ms per buffer. QueuedBuffers is the target PendingBufferCount the dedicated thread keeps
    // the device topped up to — 4 buffers (~100ms) rather than the old 3 (~75ms), a little extra
    // slack against exactly the kind of scheduling jitter LL1 exists to survive; raise further if a
    // capture still shows crackle/underrun.
    private const int BufferMilliseconds = 25;
    private const int QueuedBuffers = 4;

    private readonly SpuCore _spu;
    private readonly int _framesPerBuffer;
    private readonly short[] _stagingSamples;
    private readonly byte[] _stagingBytes;

    // JUSTIFICATION: backend MonoGame only — see SpuWavDumpDiagnostics's own header note. Null
    // unless PE_AUDIO_DUMP is set; CreateFromEnvironment is a no-op then.
    private readonly SpuWavDumpDiagnostics _wavDump;

    private DynamicSoundEffectInstance _instance;

    // JUSTIFICATION: backend MonoGame only — LL1's dedicated pump thread and its stop flag/handle.
    // volatile because _running is read by the pump thread and written by Stop()/Dispose() from
    // whichever thread calls them (the game thread, on the existing LibApi.DisableEvent path).
    private Thread _audioThread;
    private volatile bool _running;

    // JUSTIFICATION: backend MonoGame only — guards _instance access from the dedicated pump thread.
    // XAudio2 (WindowsDX's underlying API) documents its own calls as thread-safe, but this port has
    // not independently verified MonoGame.Framework.WindowsDX's DynamicSoundEffectInstance wrapper
    // itself carries no additional non-thread-safe state (source not vendored in this repo to check
    // directly) — so Start/Stop/Dispose/SubmitBuffer/PendingBufferCount from the pump thread all take
    // this same lock, the conservative option the coordinator's brief allows for exactly this case.
    private readonly object _instanceLock = new object();

    // JUSTIFICATION: C# language bridge only — an observability hook, not business logic. Tracks the
    // peak |sample| (0-32767) over the most recently rendered buffer, refreshed every ~25 ms whether
    // the real audio device or the silent fallback path is driving RenderSamples. Exists so a smoke
    // test / diagnostic can answer "is real, non-silent PCM actually reaching the output" (or the
    // silent-fallback path) without adding any probe to the ported AKAO/SPU driver itself.
    public static int LastBufferPeakAmplitude { get; private set; }

    // JUSTIFICATION: C# language bridge only — diagnostic only, zero business logic. LastBufferPeak
    // Amplitude only ever reflects the single ~25 ms buffer most recently rendered; a poller that
    // samples it once every ~2 s (PE_AUDIO_DIAG's own cadence, or an external smoke-test harness) is
    // therefore looking at ~1.25% of the actual audio timeline and can read "sparse" music as silent
    // purely from under-sampling. This tracks the max |sample| across EVERY buffer rendered since the
    // last read, so a 2 s poll can answer "was there ANY audible output in the last 2 s" instead of
    // "was the one buffer I happened to catch audible". Guarded by a lock since RenderSamples now
    // always runs on the dedicated pump thread (or the silent-fallback path within it), which need
    // not be the thread that calls ReadAndResetWindowMaxPeakAmplitude.
    private static readonly object s_windowPeakLock = new object();
    private static int s_windowMaxPeakAmplitude;

    // JUSTIFICATION: C# language bridge only — see the field's own remarks above. Read-and-reset so
    // consecutive polls report disjoint windows instead of a running maximum since process start.
    public static int ReadAndResetWindowMaxPeakAmplitude()
    {
        lock (s_windowPeakLock)
        {
            int value = s_windowMaxPeakAmplitude;
            s_windowMaxPeakAmplitude = 0;
            return value;
        }
    }

    // JUSTIFICATION: backend MonoGame only
    public SpuAudioBackend(SpuCore spu)
    {
        _spu = spu ?? throw new ArgumentNullException(nameof(spu));
        _framesPerBuffer = SampleRate * BufferMilliseconds / 1000;
        _stagingSamples = new short[_framesPerBuffer * 2];
        _stagingBytes = new byte[_stagingSamples.Length * 2];
        _wavDump = SpuWavDumpDiagnostics.CreateFromEnvironment(_spu);
    }

    // JUSTIFICATION: backend MonoGame only
    // RELATION: the tick callback registered on SpuCore fires from whichever thread calls
    // RenderSamples — now always the dedicated pump thread this method starts (LL1), never the game
    // thread. Any consumer of the tick callback must treat it as running off the game thread.
    public void Start()
    {
        if (_audioThread != null)
            return;

        bool haveDevice;
        _deviceLossLogged = false;
        lock (_instanceLock)
        {
            try
            {
                // SD1: Stop() used to leave the previous instance alive (stopped, but neither
                // disposed nor released), so every Stop()/Start() cycle -- one per scene
                // transition, via ShutdownAkaoSystem/InitSpuAndTimerEvent -> DisableEvent/
                // EnableEvent -- leaked one DynamicSoundEffectInstance into MonoGame's instance
                // pool, which is capped. Release it here before taking a new one.
                DisposeInstanceLocked();
                _instance = new DynamicSoundEffectInstance(SampleRate, Channels);
                for (int i = 0; i < QueuedBuffers; i++)
                    SubmitOneBufferLocked();
                _instance.Play();
                haveDevice = true;
            }
            catch (NoAudioHardwareException)
            {
                _instance = null;
                haveDevice = false;
            }
        }

        _running = true;
        _audioThread = new Thread(() => AudioThreadLoop(haveDevice))
        {
            IsBackground = true,
            Priority = ThreadPriority.Highest,
            Name = "SpuAudioPump",
        };
        _audioThread.Start();
    }

    // JUSTIFICATION: backend MonoGame only — the dedicated pump thread's own loop. Real-device path:
    // keeps DynamicSoundEffectInstance.PendingBufferCount topped up to QueuedBuffers, sleeping 1ms
    // between checks when already full (cheap, and MonoGame/XAudio2 has no wait-for-buffer-consumed
    // primitive this port can block on instead). No-device path: there is no PSX hardware equivalent
    // for "no sound card present" — this just paces RenderSamples by wall-clock sleep so the tick
    // callback (and with it AkaoTick's own sequencing) keeps advancing on a machine with no audio
    // output, exactly like the old silent-fallback timer did, just on this thread instead.
    //
    // SD1 (2026-08-29): haveDevice is a mutable LOCAL, not a fixed one, because the device can go
    // away under this thread (see the class note). On loss the loop DEGRADES to the no-device path
    // instead of returning: the tick callback this pump drives is the Akao ISR (FUN_8008e23c), and
    // stopping it stops AKAO's own sequencing, so the clock must keep advancing for as long as
    // _running says the backend is meant to be live. Stop()/Dispose() clear _running first, so a
    // deliberate teardown still ends the loop rather than spinning on the silent path.
    private void AudioThreadLoop(bool haveDevice)
    {
        while (_running)
        {
            if (haveDevice)
            {
                try
                {
                    lock (_instanceLock)
                    {
                        if (_instance == null || _instance.IsDisposed)
                        {
                            haveDevice = false;
                            MaybeLogDeviceLoss();
                            continue;
                        }

                        if (_instance.PendingBufferCount < QueuedBuffers)
                        {
                            SubmitOneBufferLocked();
                            continue;
                        }
                    }
                    Thread.Sleep(1);
                }
                catch (ObjectDisposedException)
                {
                    // Disposed between the IsDisposed check above and the call itself -- MonoGame's
                    // shutdown does not take _instanceLock, so no guard can make that window empty.
                    haveDevice = false;
                    MaybeLogDeviceLoss();
                }
            }
            else
            {
                _spu.RenderSamples(_stagingSamples, _framesPerBuffer);
                UpdatePeakAmplitude();
                _wavDump?.RecordMixedBuffer(_stagingSamples, _framesPerBuffer);
                Thread.Sleep(BufferMilliseconds);
            }
        }
    }

    // JUSTIFICATION: backend MonoGame only
    // SD1: releases the device outright rather than parking a stopped-but-live instance, so a later
    // Start() cannot pile a second one on top of it -- see Start()'s own note.
    public void Stop()
    {
        _running = false;
        _audioThread?.Join(TimeSpan.FromSeconds(2));
        _audioThread = null;

        lock (_instanceLock)
        {
            DisposeInstanceLocked();
        }
        _wavDump?.Flush();
    }

    // JUSTIFICATION: backend MonoGame only
    // RELATION: SD1 -- the host calls this on its exit path (GameRemaster.OnExiting), BEFORE
    // MonoGame's own Game.Dispose() shuts the audio subsystem down, so the pump thread is already
    // joined and the instance already released by the time MonoGame disposes what is left.
    // Idempotent: safe after Stop(), and safe to call twice.
    public void Dispose()
    {
        _running = false;
        _audioThread?.Join(TimeSpan.FromSeconds(2));
        _audioThread = null;

        lock (_instanceLock)
        {
            DisposeInstanceLocked();
        }
        _wavDump?.Flush();
    }

    // JUSTIFICATION: backend MonoGame only -- must be called with _instanceLock already held.
    // SD1: Stop() and Dispose() on a DynamicSoundEffectInstance both assert not-disposed and throw
    // ObjectDisposedException, and MonoGame may already have disposed it from its own shutdown, so
    // neither call may be made unguarded. IsDisposed is checked first for the ordinary case; the
    // catch covers the race, since MonoGame's shutdown does not take this lock.
    private void DisposeInstanceLocked()
    {
        DynamicSoundEffectInstance instance = _instance;
        _instance = null;
        if (instance == null)
            return;

        try
        {
            if (!instance.IsDisposed)
                instance.Stop();
            instance.Dispose();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    // JUSTIFICATION: backend MonoGame only — must be called with _instanceLock already held.
    private void SubmitOneBufferLocked()
    {
        _spu.RenderSamples(_stagingSamples, _framesPerBuffer);
        UpdatePeakAmplitude();
        _wavDump?.RecordMixedBuffer(_stagingSamples, _framesPerBuffer);
        Buffer.BlockCopy(_stagingSamples, 0, _stagingBytes, 0, _stagingBytes.Length);
        _instance.SubmitBuffer(_stagingBytes);
    }

    // JUSTIFICATION: C# language bridge only — see LastBufferPeakAmplitude's own remarks above.
    private void UpdatePeakAmplitude()
    {
        int peak = 0;
        for (int i = 0; i < _stagingSamples.Length; i++)
        {
            int abs = Math.Abs((int)_stagingSamples[i]);
            if (abs > peak)
                peak = abs;
        }
        LastBufferPeakAmplitude = peak;
        lock (s_windowPeakLock)
        {
            if (peak > s_windowMaxPeakAmplitude)
                s_windowMaxPeakAmplitude = peak;
        }
        MaybeLogPeakAmplitude(peak);
    }

    // JUSTIFICATION: backend MonoGame only — throttled (once every ~2 s) stdout line so a smoke test
    // driving the real .exe from outside the process can observe LastBufferPeakAmplitude without
    // attaching a debugger. Opt-in via the PE_AUDIO_DIAG env var so normal play never prints this.
    // RELATION: also publishes the window-max peak (see ReadAndResetWindowMaxPeakAmplitude's own
    // remarks) alongside the single-buffer peak, since the two answer different questions — one
    // sample of "instant" versus a read-and-reset max of "anything audible since last read".
    private static readonly bool s_diagEnabled =
        Environment.GetEnvironmentVariable("PE_AUDIO_DIAG") == "1";
    private System.Diagnostics.Stopwatch _diagStopwatch;
    private void MaybeLogPeakAmplitude(int peak)
    {
        if (!s_diagEnabled)
            return;
        _diagStopwatch ??= System.Diagnostics.Stopwatch.StartNew();
        if (_diagStopwatch.ElapsedMilliseconds < 2000)
            return;
        _diagStopwatch.Restart();
        int windowMax = ReadAndResetWindowMaxPeakAmplitude();
        AsyncConsole.WriteLine($"[PE_AUDIO_DIAG] peak={peak} windowMax={windowMax}");
    }

    // JUSTIFICATION: backend MonoGame only -- one-shot diagnostic for SD1's device-loss path, gated
    // behind PE_AUDIO_DIAG like MaybeLogPeakAmplitude so ordinary play prints nothing. Written and
    // read only from the pump thread (reset by Start(), which cannot run while that thread lives).
    private bool _deviceLossLogged;
    private void MaybeLogDeviceLoss()
    {
        if (!s_diagEnabled || _deviceLossLogged)
            return;
        _deviceLossLogged = true;
        AsyncConsole.WriteLine("[PE_AUDIO_DIAG] audio device disposed under the pump thread; degraded to silent pacing");
    }
}
