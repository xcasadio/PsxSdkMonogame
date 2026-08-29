using System;

namespace PsxSdkMonogame;

// JUSTIFICATION: PSX hardware adaptation only
// RELATION: models the CD controller's XA-ADPCM decoder plus the CD-audio path into the SPU — on
// real hardware the CD drive decodes the XA-ADPCM sectors interleaved in a .STR file and feeds the
// result into the SPU's CD input, which SpuCore.RenderSamples mixes alongside the 24-voice ADPCM
// engine. This class plays that role: the same "hardware model, not a transliteration of one game
// function" status SpuCore itself carries for the voice engine, and LibGpu's rasterizer carries for
// the GPU. Game-agnostic, carries no game-layer state — meant to be lifted into other PSX ports as-is.
//
// Slice S4 (2026-08-29): movies in this port played video-only up to this slice — LibCd.StGetNext's
// ingest classified every raw sector by the CD-XA submode Audio bit and dropped the audio ones with
// a "movie audio out of scope" comment (see LibCd.cs's own updated header note). This class decodes
// those sectors and resamples them to the host's fixed 44100 Hz output rate, buffering into a small
// FIFO that SpuCore.RenderSamples drains once per rendered output frame — mirroring the real
// hardware's CD-audio-to-SPU path, downstream of BOTH volume stages a real PSX applies: the CD
// controller's own ATV (Audio-To-Volume) routing matrix, set via CdMix/DsMix (the FMV driver's own
// fade path — see PeSection70Overlay.cs:1657 / FmvStream.cs:986-1031), and the SPU's own
// RegCdVolL/R registers (set to 0x7FFF, i.e. unity, by the ported AKAO init with cd.mix enabled —
// LibSpu.SpuSetCommonAttr, PsxSystems/Akao.cs:2900-2913).
public static class XaAudio
{
    // ------------------------------------------------------------------------------------------
    // XA-ADPCM decode constants. K0/K1 cross-checked against SpuCore.cs's own ADPCM filter table
    // (AdpcmFilter1/2, SpuCore.cs:251-252) — the first four entries of that 5-entry table are
    // exactly this format's K0={0,60,115,98} / K1={0,0,-52,-55}; CD-XA audio only ever uses filter
    // indices 0-3 (2 bits, filter field width below), so the two decoders agree on the shared part
    // of their tables by construction, not coincidence.
    // ------------------------------------------------------------------------------------------
    private static readonly int[] K0 = { 0, 60, 115, 98 };
    private static readonly int[] K1 = { 0, 0, -52, -55 };

    private const int RawSectorSize = 2336; // 8-byte XA subheader + 2328 usable bytes (LibDs.DsStreamSource)
    private const int SoundGroupSize = 128; // 16-byte header + 112-byte data
    private const int SoundGroupHeaderSize = 16;
    private const int SoundGroupsPerSector = 18; // 18 * 128 = 2304 == the 2328-byte usable area minus the trailing 20 reserved + 4 EDC bytes
    private const int SubBlocksPerGroup = 4; // unit pairs (0,1) (2,3) (4,5) (6,7); even=L, odd=R
    private const int SamplesPerUnit = 28; // 112 data bytes / 4 bytes-per-sample-column
    private const int SamplesPerChannelPerSector = SoundGroupsPerSector * SubBlocksPerGroup * SamplesPerUnit; // 2016

    private static short Clamp16(int v) => (short)(v < -32768 ? -32768 : v > 32767 ? 32767 : v);

    // ADPCM predictor history: ONE running stream per output channel. Real hardware decodes each
    // channel's 4 per-group sub-blocks (and every group across every sector of the stream) as a
    // single continuous ADPCM stream — only a fresh stream arm/flush resets it, never a sector or
    // group boundary; a sub-block changing filter/range mid-stream is normal adaptive coding, not a
    // predictor reset (exactly like SPU-ADPCM's own block-to-block continuity, DecodeNextAdpcmSample
    // in SpuCore.cs).
    private static int s_predL1, s_predL2;
    private static int s_predR1, s_predR2;

    // ---- one-shot diagnostics (KkDiag.Log is a no-op unless PE_KK_DIAG is set) ----
    private static bool s_headerMismatchLogged;

    // JUSTIFICATION: C# language bridge only (test tooling) — total XA audio sectors decoded since
    // the last Flush(), for the offline acceptance harness (_validation/FmvXa.cs) to check against
    // the stream's own total sector count. No effect on the mix.
    public static int SubmittedSectorCountForTest { get; private set; }

    // ------------------------------------------------------------------------------------------
    // ATV routing (libcd's CdMix / libds' DsMix write here — see LibCd.CdMix / LibDs.DsMix).
    // DEFAULT val0=val2=0x80 (unity), val1=val3=0 — straight stereo pass-through (no cross-mix).
    // DECISION: this is the console's hardware RESET default for the CD controller's routing
    // matrix, kept as the default here even though every FMV-open call site in this port's own call
    // graph (Spu_SetVoiceVolume -> CdMix, FmvStream.cs:986-1031 / PeSection70Overlay.cs:1657) already
    // re-poses these before playback starts — so a call site this port hasn't found, or a race before
    // the first pose, gets audible (if un-faded) CD audio rather than silence, matching what real
    // hardware would do coming out of reset.
    // ------------------------------------------------------------------------------------------
    private static byte s_atv0 = 0x80, s_atv1, s_atv2 = 0x80, s_atv3;

    // JUSTIFICATION: PSX hardware adaptation only — called by LibCd.CdMix / LibDs.DsMix.
    public static void SetAtv(byte val0, byte val1, byte val2, byte val3)
    {
        s_atv0 = val0;
        s_atv1 = val1;
        s_atv2 = val2;
        s_atv3 = val3;
    }

    // JUSTIFICATION: PSX hardware adaptation only — read by SpuCore.RenderSamples's CD-input mix.
    public static void GetAtv(out byte val0, out byte val1, out byte val2, out byte val3)
    {
        val0 = s_atv0;
        val1 = s_atv1;
        val2 = s_atv2;
        val3 = s_atv3;
    }

    // ------------------------------------------------------------------------------------------
    // Output FIFO: 44100 Hz stereo shorts, interleaved. Producer = whichever thread calls
    // SubmitSector (the game thread, via LibCd.StGetNext's synchronous ingest); consumer =
    // SpuCore.RenderSamples on the dedicated SpuAudioPump thread (SpuAudioBackend.cs), which wakes
    // roughly every 25 ms to top up its output buffer.
    //
    // THREAD SAFETY: a single lock around a fixed-size circular buffer. This is an SPSC access
    // pattern (one producer thread, one consumer thread) but a full lock-free ring is not warranted
    // here: the producer calls once per ~53 ms sector and the consumer drains one stereo frame at a
    // time from a hot loop that itself already does far more work per frame (voice decode/envelope/
    // mix) than one short lock acquisition costs. A plain lock keeps this correct and simple; revisit
    // only if profiling ever shows contention here, which nothing in this slice's own measurement
    // does.
    // ------------------------------------------------------------------------------------------
    private const int SampleRate = 44100;
    private const int FifoCapacitySeconds = 2;
    private const int FifoCapacityFrames = SampleRate * FifoCapacitySeconds;

    private static readonly short[] s_fifo = new short[FifoCapacityFrames * 2];
    private static int s_fifoHead; // frame index of the oldest buffered frame
    private static int s_fifoCount; // frames currently buffered
    private static readonly object s_fifoLock = new object();
    private static bool s_overrunLogged;

    private static void PushFrame(short l, short r)
    {
        lock (s_fifoLock)
        {
            if (s_fifoCount >= FifoCapacityFrames)
            {
                // Overrun: drop the oldest buffered frame to make room rather than growing
                // unbounded or blocking the producer (repo convention — degrade, never stall/throw
                // on this path; see monogame-disposes-what-your-threads-own / port-crashes-on-
                // benign-overread-style lessons this port has hit before on audio-adjacent code).
                s_fifoHead = (s_fifoHead + 1) % FifoCapacityFrames;
                s_fifoCount--;
                if (!s_overrunLogged)
                {
                    s_overrunLogged = true;
                    KkDiag.Log($"[XaAudio] FIFO overrun -- dropping oldest buffered audio " +
                               $"(capacity {FifoCapacityFrames} frames / {FifoCapacitySeconds}s)");
                }
            }

            int writeIndex = (s_fifoHead + s_fifoCount) % FifoCapacityFrames;
            s_fifo[writeIndex * 2] = l;
            s_fifo[writeIndex * 2 + 1] = r;
            s_fifoCount++;
        }
    }

    // JUSTIFICATION: PSX hardware adaptation only
    // RELATION: pulls one already-decoded, already-resampled stereo frame for SpuCore.RenderSamples'
    // CD-input mix. Underrun policy: emit silence, never block/throw — the audio pump thread must
    // keep advancing regardless (same "degrade, don't stall/exit" convention PushFrame's own note
    // cites).
    public static void TryReadFrame(out short l, out short r)
    {
        lock (s_fifoLock)
        {
            if (s_fifoCount == 0)
            {
                l = 0;
                r = 0;
                return;
            }

            l = s_fifo[s_fifoHead * 2];
            r = s_fifo[s_fifoHead * 2 + 1];
            s_fifoHead = (s_fifoHead + 1) % FifoCapacityFrames;
            s_fifoCount--;
        }
    }

    // JUSTIFICATION: C# language bridge only (test tooling, not part of the ported runtime) — the
    // offline harness (_validation/FmvXa.cs) has no audio pump thread to drain the FIFO through
    // TryReadFrame's normal one-frame-at-a-time cadence, so it needs a bulk drain instead. Same
    // underrun-as-silence contract as TryReadFrame; returns the number of frames actually written
    // (may be less than destInterleaved's capacity if the FIFO ran dry, never more).
    public static int DrainAllForTest(short[] destInterleaved)
    {
        lock (s_fifoLock)
        {
            int frames = Math.Min(s_fifoCount, destInterleaved.Length / 2);
            for (int i = 0; i < frames; i++)
            {
                int idx = (s_fifoHead + i) % FifoCapacityFrames;
                destInterleaved[i * 2] = s_fifo[idx * 2];
                destInterleaved[i * 2 + 1] = s_fifo[idx * 2 + 1];
            }

            s_fifoHead = (s_fifoHead + frames) % FifoCapacityFrames;
            s_fifoCount -= frames;
            return frames;
        }
    }

    // JUSTIFICATION: PSX hardware adaptation only
    // RELATION: fresh stream arm / teardown (LibCd.StUnSetRing calls this). Clears the FIFO and
    // resets ADPCM predictor history. Does NOT reset the ATV routing (LibCd.CdMix / LibDs.DsMix's own
    // state) — the game re-poses it on every FmvStart and zeroes it on stop, per this slice's brief.
    // Resampling (see SubmitSector/ResampleAndPush) is exact-ratio and self-contained per sector —
    // there is no cross-sector resampler phase to reset today, but this method still documents
    // owning it, per this slice's contract, in case that ever changes.
    public static void Flush()
    {
        lock (s_fifoLock)
        {
            s_fifoHead = 0;
            s_fifoCount = 0;
        }

        s_predL1 = 0;
        s_predL2 = 0;
        s_predR1 = 0;
        s_predR2 = 0;
        s_overrunLogged = false;
        SubmittedSectorCountForTest = 0;
    }

    // JUSTIFICATION: PSX hardware adaptation only
    // RELATION: called from LibCd.StGetNext's ingest loop for every raw sector classified as XA
    // audio (CD-XA submode bit 0x04 set — including 0xE4 = Audio+EOF, the movie's last audio sector,
    // which must still decode like any other). `raw2336` is the same 2336-byte raw sector
    // (8-byte XA subheader + 2328 usable bytes) LibDs.DsStreamSource reads and LibCd classifies by.
    public static void SubmitSector(byte[] raw2336)
    {
        if (raw2336 == null || raw2336.Length < RawSectorSize)
        {
            return;
        }

        // Coding-info byte (subheader offset 3): 0x01 = stereo/37800Hz/4-bit-ADPCM, the only format
        // this game's 47 .STR movies ever use. Fail loudly rather than silently mis-decoding a format
        // this class was never built to handle.
        byte codingInfo = raw2336[3];
        if (codingInfo != 0x01)
        {
            KkDiag.Log($"[XaAudio] unexpected XA coding-info 0x{codingInfo:x2} (expected 0x01 " +
                       "stereo/37800Hz/4-bit -- this port's movies never use anything else)");
            throw new InvalidOperationException(
                $"XaAudio.SubmitSector: unsupported XA coding-info 0x{codingInfo:x2} (only 0x01 " +
                "stereo/37800Hz/4-bit is implemented)");
        }

        SubmittedSectorCountForTest++;

        Span<short> rawL = stackalloc short[SamplesPerChannelPerSector];
        Span<short> rawR = stackalloc short[SamplesPerChannelPerSector];

        for (int g = 0; g < SoundGroupsPerSector; g++)
        {
            int groupOffset = 8 /* XA subheader */ + g * SoundGroupSize;
            DecodeGroup(raw2336, groupOffset, rawL, rawR, g * SubBlocksPerGroup * SamplesPerUnit);
        }

        ResampleAndPush(rawL, rawR);
    }

    // Decodes one 128-byte sound group (16-byte header + 112-byte data) into `baseIndex`..
    // `baseIndex`+111 of rawL/rawR. Layout (spec, cross-checked against SpuCore.cs's shared K table):
    //   header[0..3] = unit param bytes for units 0-3, header[8..11] = units 4-7 (header[4..7] and
    //   [12..15] are duplicates of the same bytes, kept for the CD's own error redundancy — verified
    //   optionally below, tolerated on mismatch since this port has no ECC layer to actually fall
    //   back to).
    //   Units pair up (0,1) (2,3) (4,5) (6,7): even unit = LEFT, odd unit = RIGHT, and both members
    //   of a pair share the same 4-byte sample column (byte = data[s*4 + pairIndex]), low nibble =
    //   even unit, high nibble = odd unit — so `pairIndex` below is exactly u>>1 for either unit.
    //   Each unit is 28 4-bit ADPCM samples (SamplesPerUnit); param byte bits 0-3 = range/shift
    //   (13-15 treated as 9), bits 4-5 = filter (0-3, K0/K1 above).
    private static void DecodeGroup(byte[] sector, int groupOffset, Span<short> rawL, Span<short> rawR, int baseIndex)
    {
        if (!s_headerMismatchLogged)
        {
            bool mismatch = false;
            for (int i = 0; i < 4; i++)
            {
                if (sector[groupOffset + 4 + i] != sector[groupOffset + i]) mismatch = true;
                if (sector[groupOffset + 12 + i] != sector[groupOffset + 8 + i]) mismatch = true;
            }

            if (mismatch)
            {
                s_headerMismatchLogged = true;
                KkDiag.Log("[XaAudio] XA sound-parameter duplicate bytes mismatch (tolerated, primary copy used)");
            }
        }

        for (int pairIndex = 0; pairIndex < SubBlocksPerGroup; pairIndex++)
        {
            int unitL = pairIndex * 2;
            int unitR = unitL + 1;
            byte paramL = sector[groupOffset + (unitL < 4 ? unitL : 4 + unitL)];
            byte paramR = sector[groupOffset + (unitR < 4 ? unitR : 4 + unitR)];

            int rangeL = paramL & 0x0F;
            if (rangeL >= 13) rangeL = 9;
            int filterL = (paramL >> 4) & 0x03;

            int rangeR = paramR & 0x0F;
            if (rangeR >= 13) rangeR = 9;
            int filterR = (paramR >> 4) & 0x03;

            int shiftL = 12 - rangeL;
            int shiftR = 12 - rangeR;

            int outBase = baseIndex + pairIndex * SamplesPerUnit;
            int dataStart = groupOffset + SoundGroupHeaderSize;

            for (int s = 0; s < SamplesPerUnit; s++)
            {
                byte raw = sector[dataStart + s * 4 + pairIndex];
                int nibbleL = raw & 0x0F;
                int nibbleR = (raw >> 4) & 0x0F;

                int tL = nibbleL >= 8 ? nibbleL - 16 : nibbleL;
                int predictedL = (K0[filterL] * s_predL1 + K1[filterL] * s_predL2 + 32) >> 6;
                short vL = Clamp16((tL << shiftL) + predictedL);
                s_predL2 = s_predL1;
                s_predL1 = vL;
                rawL[outBase + s] = vL;

                int tR = nibbleR >= 8 ? nibbleR - 16 : nibbleR;
                int predictedR = (K0[filterR] * s_predR1 + K1[filterR] * s_predR2 + 32) >> 6;
                short vR = Clamp16((tR << shiftR) + predictedR);
                s_predR2 = s_predR1;
                s_predR1 = vR;
                rawR[outBase + s] = vR;
            }
        }
    }

    // JUSTIFICATION: PSX hardware adaptation only
    // RELATION: resamples this sector's 2016 decoded 37800 Hz stereo frames to exactly 2352 44100 Hz
    // stereo frames (37800:44100 reduces to the exact ratio 6:7, and 2016 is itself a multiple of 6 —
    // 2016*7/6 = 2352 exactly, so this is exact per-sector, no fractional carry needed). Linear
    // interpolation via a phase-accumulator DDA, same style as SpuCore.InterpolateVoice
    // (SpuCore.cs:365-371) — the established fidelity level for this port's resampling.
    // SELF-CONTAINED PER SECTOR (PROBABLE simplification): the very last output frame of a sector
    // wants a fractional blend one sample PAST this sector's last decoded sample (into the next
    // sector's first sample, not yet decoded); rather than buffer across the call boundary, that one
    // frame in 2352 duplicates the sector's own last sample as its own "next" value. The resulting
    // error is a <1/7 blend weight on a single frame out of 2352 (~21us) once every ~53ms — well
    // under audible/measurable tolerance, and avoids a second piece of persistent cross-call state.
    private static void ResampleAndPush(ReadOnlySpan<short> rawL, ReadOnlySpan<short> rawR)
    {
        const int InLen = SamplesPerChannelPerSector; // 2016
        const int OutLen = InLen * 7 / 6; // 2352, exact since InLen is a multiple of 6

        int idx = 0;
        int fracNum = 0; // numerator over denominator 7

        for (int k = 0; k < OutLen; k++)
        {
            int i0 = idx;
            int i1 = Math.Min(idx + 1, InLen - 1);

            short l0 = rawL[i0], l1 = rawL[i1];
            short r0 = rawR[i0], r1 = rawR[i1];

            short outL = (short)(l0 + ((l1 - l0) * fracNum) / 7);
            short outR = (short)(r0 + ((r1 - r0) * fracNum) / 7);
            PushFrame(outL, outR);

            fracNum += 6;
            if (fracNum >= 7)
            {
                fracNum -= 7;
                idx++;
            }
        }
    }
}
