using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace PsxSdkMonogame;

// JUSTIFICATION: backend MonoGame only
// RELATION: diagnostic-only WAV export for the SPU audio pipeline, gated by the PE_AUDIO_DUMP env
// var (a directory path). When set, records (1) the final mixed stereo output SpuAudioBackend
// actually submits to the audio device and (2) per active voice, the raw ADPCM decode BEFORE
// envelope/volume is applied (mono), via SpuCore.OnVoiceRawSampleForDiagnostics. Exists purely to
// let a human (or an offline reference decoder) A/B the port's PCM against independently decoded
// source bytes — it owns no PSX/game semantics, changes no audible behaviour, and is inert unless
// PE_AUDIO_DUMP is set, matching the existing PE_AUDIO_DIAG convention in this same file's sibling.
//
// MM1 (2026-08-21, coordinator): RecordMixedBuffer/RecordVoiceRawSample used to append straight
// into a List<short> under a lock shared with Flush(), and Flush() — called every ~2s from
// SpuAudioBackend so a killed capture loses at most ~2s of tail audio — rewrote the WHOLE
// accumulated WAV file to disk from scratch each time. Both of those ran on the audio pump thread
// (post-LL1) and were flagged (LL3) as a likely cause of the residual silence gaps LL2 still
// measured. Fixed here: recording is now a lock-free ConcurrentQueue enqueue (zero I/O, no lock,
// no blocking) from the audio thread; a dedicated background thread drains those queues into the
// accumulation buffers and performs every WAV write — the audio thread never touches a file or the
// accumulation buffers directly.
public sealed class SpuWavDumpDiagnostics
{
    private const int SampleRate = 44100;

    // JUSTIFICATION: backend MonoGame only
    public static SpuWavDumpDiagnostics CreateFromEnvironment(SpuCore spu)
    {
        string dir = Environment.GetEnvironmentVariable("PE_AUDIO_DUMP");
        if (string.IsNullOrEmpty(dir))
            return null;

        Directory.CreateDirectory(dir);
        return new SpuWavDumpDiagnostics(spu, dir);
    }

    private readonly string _dir;
    private readonly string _timestamp;

    // JUSTIFICATION: backend MonoGame only — audio-thread-facing side: lock-free enqueue only.
    private readonly ConcurrentQueue<short> _mixedQueue = new ConcurrentQueue<short>();
    private readonly ConcurrentDictionary<int, ConcurrentQueue<short>> _voiceQueues =
        new ConcurrentDictionary<int, ConcurrentQueue<short>>();

    // JUSTIFICATION: backend MonoGame only — writer-thread-facing side: only the background thread
    // (and Flush(), guarded by _writeLock below) ever reads or appends to these.
    private readonly List<short> _mixedAccum = new List<short>();
    private readonly Dictionary<int, List<short>> _voiceAccum = new Dictionary<int, List<short>>();
    private readonly object _writeLock = new object();
    private readonly Thread _writerThread;

    private SpuWavDumpDiagnostics(SpuCore spu, string dir)
    {
        _dir = dir;
        _timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        spu.OnVoiceRawSampleForDiagnostics = RecordVoiceRawSample;
        Console.WriteLine($"[PE_AUDIO_DUMP] enabled, writing WAV files to {dir}");

        _writerThread = new Thread(WriterLoop) { IsBackground = true, Name = "SpuWavDumpWriter" };
        _writerThread.Start();
    }

    // JUSTIFICATION: C# language bridge only — called from the audio pump thread; must stay
    // allocation-light and lock-free. ConcurrentQueue<T>.Enqueue is wait-free.
    private void RecordVoiceRawSample(int voiceIndex, short rawSample)
    {
        ConcurrentQueue<short> q = _voiceQueues.GetOrAdd(voiceIndex, _ => new ConcurrentQueue<short>());
        q.Enqueue(rawSample);
    }

    // JUSTIFICATION: backend MonoGame only
    // RELATION: called by SpuAudioBackend right after each mixed buffer is rendered, so the dumped
    // mixed WAV stays in lockstep with what was actually submitted to the audio device (or the
    // silent-fallback path's throwaway buffer). Lock-free — no disk I/O, no lock, on this path.
    public void RecordMixedBuffer(short[] interleavedStereo, int frames)
    {
        for (int i = 0; i < frames * 2; i++)
            _mixedQueue.Enqueue(interleavedStereo[i]);
    }

    // JUSTIFICATION: backend MonoGame only — the background drain-and-write thread. This, and this
    // alone, is where WAV files are written; it never runs on the audio pump thread.
    private void WriterLoop()
    {
        while (true)
        {
            Thread.Sleep(2000);
            DrainAndWrite();
        }
    }

    private void DrainAndWrite()
    {
        lock (_writeLock)
        {
            while (_mixedQueue.TryDequeue(out short s))
                _mixedAccum.Add(s);
            foreach (KeyValuePair<int, ConcurrentQueue<short>> kvp in _voiceQueues)
            {
                if (!_voiceAccum.TryGetValue(kvp.Key, out List<short> list))
                {
                    list = new List<short>();
                    _voiceAccum[kvp.Key] = list;
                }
                while (kvp.Value.TryDequeue(out short s))
                    list.Add(s);
            }

            string mixedPath = Path.Combine(_dir, $"mixed_{_timestamp}.wav");
            WriteWav(mixedPath, _mixedAccum.ToArray(), channels: 2);

            foreach (KeyValuePair<int, List<short>> kvp in _voiceAccum)
            {
                string path = Path.Combine(_dir, $"voice{kvp.Key}_{_timestamp}.wav");
                WriteWav(path, kvp.Value.ToArray(), channels: 1);
            }
        }
    }

    // JUSTIFICATION: backend MonoGame only — explicit final flush, called from SpuAudioBackend.
    // Stop()/Dispose() on the game thread (never from RenderSamples/the audio pump thread), so a
    // synchronous drain-and-write here is exactly the "never from the audio thread" contract MM1
    // asks for, not a regression back to blocking the hot path.
    public void Flush()
    {
        DrainAndWrite();
    }

    // JUSTIFICATION: C# language bridge only — minimal 44-byte PCM16 WAV header, same layout as
    // DataVisualizer/SpuAdpcmDecoder.cs's WrapInWav (not shared cross-project since this is
    // throwaway diagnostic tooling, matching this file's own top-of-file convention note).
    private static void WriteWav(string path, short[] samples, int channels)
    {
        int dataBytes = samples.Length * 2;
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var bw = new BinaryWriter(fs);
        bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        bw.Write(36 + dataBytes);
        bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
        bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        bw.Write(16);
        bw.Write((short)1); // PCM
        bw.Write((short)channels);
        bw.Write(SampleRate);
        bw.Write(SampleRate * channels * 2);
        bw.Write((short)(channels * 2));
        bw.Write((short)16);
        bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        bw.Write(dataBytes);
        foreach (short s in samples)
            bw.Write(s);
    }
}
