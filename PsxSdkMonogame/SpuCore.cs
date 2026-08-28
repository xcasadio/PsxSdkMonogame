using System;

namespace PsxSdkMonogame;

// JUSTIFICATION: PSX hardware adaptation only
// RELATION: this is not a transliteration of a specific original runtime function — it is a
// hardware model of the PSX SPU chip itself (RAM, the 24-voice register block, ADPCM decode, ADSR,
// mixing), the same role LibGpu's software rasterizer plays for the GPU. LibSpu.cs / LibSnd.cs stay
// untouched "Do nothing PSX SDK" stubs; wiring them to this core is a later task. Nothing here reads
// or writes any game-layer state — this class is meant to be lifted into other PSX ports as-is.
//
// Register block layout (0x1f801c00..0x1f801dff, 0x200 bytes total), verified against psx-spx /
// the hitmen SPU doc:
//   Voice N (N=0..23), base = N*0x10:
//     +0x0 VolL, +0x2 VolR, +0x4 Pitch, +0x6 StartAddr (in 8-byte units), +0x8 ADSR1, +0xA ADSR2,
//     +0xC ADSRvol/ENVX (current envelope level, hardware-updated), +0xE RepeatAddr (8-byte units)
//   Common block from 0x180:
//     0x180/0x182 Main Vol L/R, 0x184/0x186 Reverb Out Vol L/R, 0x188/0x18A KON lo/hi,
//     0x18C/0x18E KOFF lo/hi, 0x190/0x192 PMON lo/hi, 0x194/0x196 NON lo/hi, 0x198/0x19A EON lo/hi,
//     0x19C/0x19E ENDX lo/hi, 0x1A2 Reverb work area start, 0x1A4 IRQ addr, 0x1A6 Transfer addr,
//     0x1A8 Transfer FIFO, 0x1AA SPUCNT, 0x1AC Transfer control, 0x1AE SPUSTAT,
//     0x1B0/0x1B2 CD Vol L/R, 0x1B4/0x1B6 Ext Vol L/R, 0x1C0..0x1FE reverb coefficient registers.
public sealed class SpuCore
{
    // ------------------------------------------------------------------------------------------
    // SPU RAM (512 KiB) and the 0x200-byte register image.
    // ------------------------------------------------------------------------------------------

    public const int SpuRamSizeBytes = 0x80000;
    private const int RegBlockSizeBytes = 0x200;
    private const int VoiceCount = 24;
    private const int VoiceStride = 0x10;

    // Per-voice register offsets (relative to VoiceStride * voiceIndex). Public: LibSpu's
    // AkaoSpuVoice_* register writers (tranche 5a) address these the same way the original does,
    // `voice * 0x10 + offset` into the 0x1f801c00 block.
    public const int RegVoiceVolL = 0x0;
    public const int RegVoiceVolR = 0x2;
    public const int RegVoicePitch = 0x4;
    public const int RegVoiceStartAddr = 0x6;
    public const int RegVoiceAdsr1 = 0x8;
    public const int RegVoiceAdsr2 = 0xA;
    private const int RegVoiceEnvx = 0xC;
    public const int RegVoiceRepeatAddr = 0xE;

    // Common register offsets.
    public const int RegMainVolL = 0x180;
    public const int RegMainVolR = 0x182;
    public const int RegReverbVolL = 0x184;
    public const int RegReverbVolR = 0x186;
    public const int RegKonLo = 0x188;
    public const int RegKonHi = 0x18A;
    public const int RegKoffLo = 0x18C;
    public const int RegKoffHi = 0x18E;
    public const int RegPmonLo = 0x190;
    public const int RegPmonHi = 0x192;
    public const int RegNonLo = 0x194;
    public const int RegNonHi = 0x196;
    public const int RegEonLo = 0x198;
    public const int RegEonHi = 0x19A;
    public const int RegEndxLo = 0x19C;
    public const int RegEndxHi = 0x19E;
    public const int RegReverbWorkAddr = 0x1A2;
    public const int RegIrqAddr = 0x1A4;
    public const int RegTransferAddr = 0x1A6;
    public const int RegTransferFifo = 0x1A8;
    public const int RegSpucnt = 0x1AA;
    public const int RegTransferControl = 0x1AC;
    public const int RegSpustat = 0x1AE;
    public const int RegCdVolL = 0x1B0;
    public const int RegCdVolR = 0x1B2;
    public const int RegExtVolL = 0x1B4;
    public const int RegExtVolR = 0x1B6;
    public const int RegReverbBase = 0x1C0;

    public readonly byte[] SpuRam = new byte[SpuRamSizeBytes];
    private readonly byte[] _regs = new byte[RegBlockSizeBytes];

    private readonly Voice[] _voices = new Voice[VoiceCount];

    // JUSTIFICATION: PSX hardware adaptation only
    public SpuCore()
    {
        for (int i = 0; i < VoiceCount; i++)
            _voices[i] = new Voice(i);
    }

    // ------------------------------------------------------------------------------------------
    // Register / RAM access API.
    // ------------------------------------------------------------------------------------------

    // JUSTIFICATION: C# language bridge only
    // RELATION: stands in for the hardware's `*(ushort *)(0x1f801c00 + offset)` load.
    public ushort ReadReg16(int offset)
    {
        return (ushort)(_regs[offset] | (_regs[offset + 1] << 8));
    }

    // JUSTIFICATION: C# language bridge only (test tooling, not part of the ported runtime — see
    // _validation/SpuCoreValidation.cs's own top-of-file note on this convention).
    // RELATION: exposes Voice.Active, which has no register-mapped equivalent a test could probe
    // through ReadReg16 alone, for _validation/SpuCoreValidation.cs's tranche-5a key-on witness.
    public bool IsVoiceActiveForTest(int voiceIndex) => _voices[voiceIndex].Active;

    // JUSTIFICATION: C# language bridge only (test tooling, not part of the ported runtime).
    // RELATION: exposes Voice.EnvLevel, which — like Active above — has no register-mapped
    // equivalent (ENVX mirrors it but only after UpdateEnvelope runs), for
    // _validation/SpuCoreValidation.cs's tranche-6 D1 sustain-phase step-rate witness.
    public int PeekEnvLevelForTest(int voiceIndex) => _voices[voiceIndex].EnvLevel;

    // JUSTIFICATION: C# language bridge only
    private void SetReg16Raw(int offset, ushort value)
    {
        _regs[offset] = (byte)value;
        _regs[offset + 1] = (byte)(value >> 8);
    }

    // JUSTIFICATION: PSX hardware adaptation only
    // RELATION: KON/KOFF are write-triggered — writing a bit fires SPU key-on/key-off for that
    // voice, it is not just a passive storage cell. Every other offset in the block is plain
    // storage, read back verbatim by ReadReg16, matching hardware for the fields this port needs
    // (volumes, pitch, addresses, ADSR, reverb coefficients).
    public void WriteReg16(int offset, ushort value)
    {
        SetReg16Raw(offset, value);

        if (offset == RegKonLo) TriggerKeyOnMask(value, 0);
        else if (offset == RegKonHi) TriggerKeyOnMask(0, value);
        else if (offset == RegKoffLo) TriggerKeyOffMask(value, 0);
        else if (offset == RegKoffHi) TriggerKeyOffMask(0, value);
        // ENDX (0x19C/0x19E) is normally only ever written with 0 by software to clear flags; a
        // plain store already gives that behaviour, so no extra handling is needed here.
    }

    // JUSTIFICATION: PSX hardware adaptation only
    // RELATION: bulk SPU DMA write, used to upload ADPCM sample data ahead of playback.
    // HARDWARE SEMANTICS (2026-08-20, O2): the real Sound RAM Data Transfer Address register
    // (1F801DA6h) is documented 16-bit ("15-0 Address in sound buffer divided by eight" — psx-spx,
    // https://raw.githubusercontent.com/psx-spx/psx-spx.github.io/master/docs/soundprocessingunitspu.md,
    // fetched 2026-08-20), and writing it "does additionally store the value (multiplied by 8) in
    // another internal 'current address' register (that internal register does increment during
    // transfers...)" (same source) — it is this SEPARATE, incrementing register that actually walks
    // SPU RAM during a transfer, not the static 1F801DA6h value. A 16-bit hardware counter overflows
    // from 0xFFFF back to 0x0000 on further increment — ordinary fixed-width register arithmetic, not
    // an assumption specific to this port — and 0x10000 units x 8 bytes/unit = 0x80000 bytes, exactly
    // this class's own SpuRamSizeBytes: the register's own width already spans the ENTIRE 512K SPU RAM
    // with no gap, so an overrunning transfer wraps back to SPU RAM address 0 and keeps writing/
    // overwriting from there, not an out-of-range fault. The same document's Reverb section describes
    // its own work-area address explicitly wrapping "within mBASE..7FFFEh" — the same wrap convention
    // documented elsewhere in this exact hardware for an address confined to this same 512K space.
    // Before this fix, a single call whose start+length crossed the 0x80000 boundary threw
    // (Array.Copy does not split a destination write across an array boundary) rather than wrapping —
    // reproduced this session by a real AKAO segment delivery exceeding ~480KB from the SPU's own
    // fresh-delivery base address 0x8000 (see _validation/SpuCoreValidation.cs Test 18 and
    // docs/audio-akao/ab-validation-2026-08-20.md, section O1-O3, for the exact threshold and the
    // witness). Fixed here by splitting the copy at the wrap boundary instead of raising.
    public void WriteRam(int spuAddr, byte[] source, int sourceOffset, int length)
    {
        int dest = spuAddr & (SpuRamSizeBytes - 1);
        int remaining = length;
        int srcOffset = sourceOffset;
        while (remaining > 0)
        {
            int chunk = Math.Min(remaining, SpuRamSizeBytes - dest);
            Array.Copy(source, srcOffset, SpuRam, dest, chunk);
            remaining -= chunk;
            srcOffset += chunk;
            dest = 0; // wrapped: the internal current-address register rolled over to SPU RAM address 0
        }
    }

    // JUSTIFICATION: PSX hardware adaptation only
    // RELATION: see WriteRam's own header note — the same 16-bit current-address register drives
    // reads (e.g. Spu_ReadWithPrepare/AkaoWaitSpuTransferDone's callers) as well as writes, so the same
    // wrap-at-512K fix applies symmetrically here.
    public void ReadRam(int spuAddr, byte[] destination, int destinationOffset, int length)
    {
        int src = spuAddr & (SpuRamSizeBytes - 1);
        int remaining = length;
        int destOffset = destinationOffset;
        while (remaining > 0)
        {
            int chunk = Math.Min(remaining, SpuRamSizeBytes - src);
            Array.Copy(SpuRam, src, destination, destOffset, chunk);
            remaining -= chunk;
            destOffset += chunk;
            src = 0;
        }
    }

    // ------------------------------------------------------------------------------------------
    // Voice state.
    // ------------------------------------------------------------------------------------------

    private enum EnvPhase
    {
        Off,
        Attack,
        Decay,
        Sustain,
        Release
    }

    // JUSTIFICATION: PSX hardware adaptation only
    // RELATION: per-voice decode/envelope state that has no register-mapped home — real hardware
    // keeps this inside the SPU's per-voice ADPCM decode and envelope units, which are not
    // software-visible except through ENVX/ENDX.
    private sealed class Voice
    {
        public readonly int Index;

        public bool Active;

        // ADPCM stream position. The Repeat Address is NOT cached per-voice here — the SPU register
        // block (RegVoiceRepeatAddr) is the single source of truth, read/written directly by
        // DecodeNextAdpcmSample and by software (AkaoSpuVoice_SetRepeatAddress), matching hardware
        // (psx-spx: both the Loop-Start auto-latch and a CPU write target the SAME register).
        public int BlockAddr;      // byte offset in SpuRam of the 16-byte block being decoded
        public int NibbleIndex;    // 0..27, next sample to decode within CurBlock
        public int CurShift;       // current block's header, cached at NibbleIndex==0
        public int CurFilter;
        public int CurFlags;

        // ADPCM predictor history (most recent decoded sample, second most recent).
        public int PredS1;
        public int PredS2;

        // Pitch-driven resampling: 12-bit fractional counter plus the two most recent OUTPUT
        // samples used for linear interpolation between ADPCM source samples.
        public uint PitchCounter;
        public int InterpPrev;
        public int InterpCur;

        // Envelope.
        public EnvPhase Phase;
        public int EnvLevel;   // 0..0x7FFF, mirrored into the ENVX register after every tick
        public int EnvCounter; // cycle-gating counter for the current phase's rate

        public Voice(int index)
        {
            Index = index;
        }
    }

    // ------------------------------------------------------------------------------------------
    // ADPCM decode.
    // ------------------------------------------------------------------------------------------

    // Filter coefficients x64, same table as DataVisualizer/SpuAdpcmDecoder.cs (nocash PSX SPU-ADPCM
    // spec). Index >= 5 aliases to filter 4, matching that reference decoder.
    private static readonly int[] AdpcmFilter1 = { 0, 60, 115, 98, 122 };
    private static readonly int[] AdpcmFilter2 = { 0, 0, -52, -55, -60 };

    private static int AdpcmClamp16(int v) => v < -32768 ? -32768 : v > 32767 ? 32767 : v;

    // JUSTIFICATION: PSX hardware adaptation only
    // RELATION: streams the block-based SPU-ADPCM format (see DataVisualizer/SpuAdpcmDecoder.cs for
    // the block-at-a-time reference) one sample at a time, which is what letting the pitch counter
    // drive playback sample-by-sample requires. Same math, same filter table, same flag bits
    // (bit0=loop end, bit1=loop repeat, bit2=loop start); only the block/nibble bookkeeping differs.
    //
    // Loop-flag semantics (psx-spx, "Flag Bits in 2nd Byte of ADPCM Header" / "Possible combinations
    // for Bit0-1", fetched 2026-08-20 — CERTAIN-by-spec, closing the tranche-1 PARTIAL this file
    // used to carry here):
    //   bit0 Loop End    — "1=Set ENDX flag and Jump to [1F801C0Eh+N*10h]" — this jump happens on
    //                       EVERY Loop End block, regardless of bit1. [1F801C0Eh+N*10h] is the voice's
    //                       own Repeat Address REGISTER (RegVoiceRepeatAddr) — the live register, not
    //                       a cached copy, so a software SetRepeatAddress write and the hardware
    //                       auto-latch below are the SAME storage cell, exactly as the spec's own
    //                       "software doesn't need to deal with [it]... but writing may be useful...
    //                       to redirect a one-shot sample" paragraph describes.
    //   bit1 Loop Repeat — "0=Force Release and set ADSR Level to Zero; only if Bit0=1" — i.e. Code 1
    //                       (End+Mute, bit0 set/bit1 clear) jumps to the repeat address AND forces the
    //                       envelope to Release/0 immediately; Code 3 (End+Repeat, both set) jumps and
    //                       keeps decoding normally from there.
    //   bit2 Loop Start  — "1=Copy current address to [1F801C0Eh+N*10h]" — writes the SAME register.
    private int DecodeNextAdpcmSample(Voice v)
    {
        int repeatRegOffset = v.Index * VoiceStride + RegVoiceRepeatAddr;

        if (v.NibbleIndex == 0)
        {
            byte packInfo = SpuRam[v.BlockAddr & (SpuRamSizeBytes - 1)];
            byte flags = SpuRam[(v.BlockAddr + 1) & (SpuRamSizeBytes - 1)];
            v.CurShift = packInfo & 0x0F;
            v.CurFilter = (packInfo >> 4) & 0x0F;
            v.CurFlags = flags;

            // Loop start: "copy current address to [RepeatAddr register]" — the live register, so a
            // prior software SetRepeatAddress write is superseded the moment a Loop-Start block is
            // actually reached, matching hardware.
            if ((flags & 0x04) != 0)
                SetReg16Raw(repeatRegOffset, (ushort)(v.BlockAddr >> 3));
        }

        int byteIndex = v.NibbleIndex >> 1;
        byte raw = SpuRam[(v.BlockAddr + 2 + byteIndex) & (SpuRamSizeBytes - 1)];
        int nibble = (v.NibbleIndex & 1) == 0 ? (raw & 0x0F) : ((raw >> 4) & 0x0F);
        if (nibble >= 8) nibble -= 16;

        int filterIndex = v.CurFilter < AdpcmFilter1.Length ? v.CurFilter : AdpcmFilter1.Length - 1;
        int f1 = AdpcmFilter1[filterIndex];
        int f2 = AdpcmFilter2[filterIndex];

        int shifted = nibble << Math.Max(0, 12 - v.CurShift);
        int predicted = (f1 * v.PredS1 + f2 * v.PredS2 + 32) >> 6;
        int sample = AdpcmClamp16(shifted + predicted);

        v.PredS2 = v.PredS1;
        v.PredS1 = sample;

        v.NibbleIndex++;
        if (v.NibbleIndex >= 28)
        {
            v.NibbleIndex = 0;
            if ((v.CurFlags & 0x01) != 0)
            {
                SetEndx(v.Index);
                // "Jump to [RepeatAddr register]" happens on every Loop End block — reads whatever is
                // CURRENTLY in the register: the hardware auto-latch above, or an explicit software
                // write (AkaoSpuVoice_SetRepeatAddress) if no Loop-Start block was ever reached first.
                v.BlockAddr = (ReadReg16(repeatRegOffset) * 8) & (SpuRamSizeBytes - 1);
                if ((v.CurFlags & 0x02) == 0)
                {
                    // Code 1, End+Mute: "Force Release and set ADSR Level to Zero" — immediate, not a
                    // ramped release; StepEnvelope's own Release case clamps EnvLevel<=0 to Active=false
                    // on its very next step, which is what actually silences and frees the voice.
                    v.Phase = EnvPhase.Release;
                    v.EnvCounter = 0;
                    v.EnvLevel = 0;
                }
                // Code 3, End+Repeat (bit1 set): keep decoding from the jumped-to block, no envelope
                // change — the normal loop case.
            }
            else
            {
                v.BlockAddr += 16;
            }
        }

        return sample;
    }

    // JUSTIFICATION: PSX hardware adaptation only
    // RELATION: the SPU's per-voice sample-rate counter. A voice's own ADPCM stream runs at
    // whatever rate `pitch` encodes (0x1000 = nominal 44100 Hz); this advances that stream by
    // however many source samples fall within one host output sample.
    private void AdvanceVoiceSample(Voice v, ushort pitch)
    {
        // PARTIAL: pitch modulation (PMON) against the previous voice is not applied.
        v.PitchCounter += pitch;
        while (v.PitchCounter >= 0x1000)
        {
            v.PitchCounter -= 0x1000;
            int next = DecodeNextAdpcmSample(v);
            v.InterpPrev = v.InterpCur;
            v.InterpCur = next;
        }
    }

    // PARTIAL: linear interpolation; hardware uses 4-point Gaussian interpolation.
    // At the source sample boundary (PitchCounter == 0, which is every call when pitch == 0x1000)
    // this returns InterpCur directly with no blending, so a voice driven at nominal pitch decodes
    // bit-for-bit identical to DataVisualizer/SpuAdpcmDecoder.cs's block decode.
    private static int InterpolateVoice(Voice v)
    {
        if (v.PitchCounter == 0)
            return v.InterpCur;
        long delta = v.InterpCur - v.InterpPrev;
        return v.InterpPrev + (int)((delta * v.PitchCounter) >> 12);
    }

    // ------------------------------------------------------------------------------------------
    // ADSR envelope.
    //
    // Bit layout verified against the hitmen.c02.at SPU doc (psx-spx.consoledev.net returned 403
    // when fetched directly for this task):
    //   ADSR1 (voice offset +0x8): bit15 Am (attack mode), bits14-8 Ar (7-bit combined attack
    //     rate = shift<<2|step), bits7-4 Dr (decay shift, 0..15, step fixed -8), bits3-0 Sl
    //     (sustain level, level=(Sl+1)*0x800).
    //   ADSR2 (voice offset +0xA): bit15 Sm (sustain mode), bit14 Sd (sustain direction,
    //     0=increase/1=decrease), bit13 unused, bits12-6 Sr (7-bit combined sustain rate), bit5 Rm
    //     (release mode), bits4-0 Rr (release shift, 0..31, step fixed -8).
    // The step tables ({+7,+6,+5,+4} increase / {-8,-7,-6,-5} decrease keyed by the combined rate's
    // low 2 bits) and the cycle/shift/step envelope stepping formula are the standard PSX SPU
    // ADSR algorithm reproduced by essentially every PSX emulator's SPU core; not independently
    // re-derived from a primary doc fetch here, so treat the stepping formula as PROBABLE rather
    // than CERTAIN even though the register bit layout is CERTAIN (fetched).
    // ------------------------------------------------------------------------------------------

    private static readonly int[] AdsrIncreaseSteps = { 7, 6, 5, 4 };
    private static readonly int[] AdsrDecreaseSteps = { -8, -7, -6, -5 };

    private static void StepEnvelope(Voice v, int shift, int step, bool increasing, bool exponential)
    {
        int cycles = 1 << Math.Max(0, shift - 11);
        v.EnvCounter++;
        if (v.EnvCounter < cycles)
            return;
        v.EnvCounter = 0;

        int stepValue = step << Math.Max(0, 11 - shift);
        if (increasing && exponential && v.EnvLevel > 0x6000)
            stepValue >>= 2;
        if (!increasing && exponential)
            stepValue = (int)(((long)stepValue * v.EnvLevel) >> 15);

        int level = v.EnvLevel + stepValue;
        if (level < 0) level = 0;
        if (level > 0x7FFF) level = 0x7FFF;
        v.EnvLevel = level;
    }

    private void UpdateEnvelope(Voice v)
    {
        int adsr1 = ReadReg16(v.Index * VoiceStride + RegVoiceAdsr1);
        int adsr2 = ReadReg16(v.Index * VoiceStride + RegVoiceAdsr2);

        switch (v.Phase)
        {
            case EnvPhase.Attack:
            {
                bool exponential = (adsr1 & 0x8000) != 0;
                int rate = (adsr1 >> 8) & 0x7F;
                int shift = rate >> 2;
                int step = AdsrIncreaseSteps[rate & 3];
                StepEnvelope(v, shift, step, increasing: true, exponential: exponential);
                if (v.EnvLevel >= 0x7FFF)
                {
                    v.Phase = EnvPhase.Decay;
                    v.EnvCounter = 0;
                }
                break;
            }
            case EnvPhase.Decay:
            {
                // The sustain-level check runs BEFORE stepping (unlike Attack, which always has
                // room to step since it starts at 0): Attack clamps at 0x7FFF, so a sustain level
                // at or above that (Sl=15 -> 0x8000) must skip Decay entirely rather than take one
                // large exponential step down and then notice it overshot.
                int sustainLevel = ((adsr1 & 0x0F) + 1) << 11;
                if (v.EnvLevel <= sustainLevel)
                {
                    v.Phase = EnvPhase.Sustain;
                    v.EnvCounter = 0;
                    break;
                }
                int shift = (adsr1 >> 4) & 0x0F;
                StepEnvelope(v, shift, -8, increasing: false, exponential: true);
                break;
            }
            case EnvPhase.Sustain:
            {
                bool exponential = (adsr2 & 0x8000) != 0;
                bool decreasing = (adsr2 & 0x4000) != 0;
                int rate = (adsr2 >> 6) & 0x7F;
                int shift = rate >> 2;
                int step = decreasing ? AdsrDecreaseSteps[rate & 3] : AdsrIncreaseSteps[rate & 3];
                StepEnvelope(v, shift, step, increasing: !decreasing, exponential: exponential);
                break;
            }
            case EnvPhase.Release:
            {
                bool exponential = (adsr2 & 0x0020) != 0;
                int shift = adsr2 & 0x1F;
                StepEnvelope(v, shift, -8, increasing: false, exponential: exponential);
                if (v.EnvLevel <= 0)
                {
                    v.EnvLevel = 0;
                    v.Active = false;
                }
                break;
            }
            case EnvPhase.Off:
            default:
                break;
        }

        SetReg16Raw(v.Index * VoiceStride + RegVoiceEnvx, (ushort)v.EnvLevel);
    }

    // ------------------------------------------------------------------------------------------
    // Key on / key off / ENDX.
    // ------------------------------------------------------------------------------------------

    private void TriggerKeyOnMask(ushort lo, ushort hi)
    {
        uint mask = (uint)lo | ((uint)hi << 16);
        for (int i = 0; i < VoiceCount; i++)
            if ((mask & (1u << i)) != 0)
                KeyOnVoice(i);
    }

    private void TriggerKeyOffMask(ushort lo, ushort hi)
    {
        uint mask = (uint)lo | ((uint)hi << 16);
        for (int i = 0; i < VoiceCount; i++)
            if ((mask & (1u << i)) != 0)
                KeyOffVoice(i);
    }

    // JUSTIFICATION: PSX hardware adaptation only
    // RELATION: SPU key-on — latches the voice's Start Address register (in 8-byte units) as the
    // first ADPCM block, resets decode/interpolation history, clears ENDX and starts the envelope
    // at Attack. Deliberately does NOT touch the Repeat Address register (see DecodeNextAdpcmSample's
    // own header note): psx-spx's own "software doesn't need to deal with [it]... but writing may be
    // useful... to redirect a one-shot sample" wording, and this port's own live measurement (2026-08-20
    // — AkaoSpuVoice_SetRepeatAddress firing BEFORE KeyOn for the same voice, with the register already
    // holding that value the next time it was read), both say KeyOn leaves the register alone; only a
    // software write or a Loop-Start block encountered during decode changes it.
    private void KeyOnVoice(int index)
    {
        Voice v = _voices[index];
        int startAddr = (ReadReg16(index * VoiceStride + RegVoiceStartAddr) * 8) & (SpuRamSizeBytes - 1);

        v.BlockAddr = startAddr;
        v.NibbleIndex = 0;
        v.PredS1 = 0;
        v.PredS2 = 0;
        v.PitchCounter = 0;
        v.InterpPrev = 0;
        v.InterpCur = 0;
        v.Active = true;
        v.Phase = EnvPhase.Attack;
        v.EnvLevel = 0;
        v.EnvCounter = 0;

        ClearEndx(index);
    }

    // JUSTIFICATION: PSX hardware adaptation only
    // RELATION: SPU key-off — moves the envelope straight to Release regardless of its current
    // phase; the voice keeps producing audio (fading out) until Release reaches level 0.
    private void KeyOffVoice(int index)
    {
        Voice v = _voices[index];
        if (!v.Active)
            return;
        v.Phase = EnvPhase.Release;
        v.EnvCounter = 0;
    }

    private void SetEndx(int voiceIndex)
    {
        int offset = voiceIndex < 16 ? RegEndxLo : RegEndxHi;
        int bit = voiceIndex < 16 ? voiceIndex : voiceIndex - 16;
        ushort value = ReadReg16(offset);
        value |= (ushort)(1 << bit);
        SetReg16Raw(offset, value);
    }

    private void ClearEndx(int voiceIndex)
    {
        int offset = voiceIndex < 16 ? RegEndxLo : RegEndxHi;
        int bit = voiceIndex < 16 ? voiceIndex : voiceIndex - 16;
        ushort value = ReadReg16(offset);
        value &= (ushort)~(1 << bit);
        SetReg16Raw(offset, value);
    }

    private bool IsNoiseVoice(int voiceIndex)
    {
        int offset = voiceIndex < 16 ? RegNonLo : RegNonHi;
        int bit = voiceIndex < 16 ? voiceIndex : voiceIndex - 16;
        return (ReadReg16(offset) & (1 << bit)) != 0;
    }

    // ------------------------------------------------------------------------------------------
    // ISR-equivalent tick scheduling.
    // ------------------------------------------------------------------------------------------

    // 44100 Hz * 8 * 0x44E8 / 33868800 Hz = 44100 / 240 = 183.75 samples per tick (240 Hz), the
    // cadence the AKAO sequencer's timer interrupt runs at on real hardware. User decision: this
    // drives AKAO ticking from the audio clock rather than the frame clock.
    private const double SamplesPerTick = 44100.0 * 8.0 * 0x44E8 / 33868800.0;

    private double _tickAccumulator;
    private Action _tickCallback;

    // JUSTIFICATION: PSX hardware adaptation only
    public void SetTickCallback(Action callback)
    {
        _tickCallback = callback;
    }

    // JUSTIFICATION: C# language bridge only — diagnostic-only hook, no effect on the mix. Lets an
    // external listener (SpuWavDumpDiagnostics) observe the raw ADPCM decode of an active voice
    // BEFORE envelope/volume is applied, once per rendered frame, so a WAV dump of "PCM straight off
    // the decoder" can be A/B'd against an independent offline decode of the same source bytes. Null
    // by default (zero cost, no game-layer semantics touched).
    public Action<int, short> OnVoiceRawSampleForDiagnostics;

    // ------------------------------------------------------------------------------------------
    // Mixer.
    // ------------------------------------------------------------------------------------------

    private static short ClampSample(int v) => (short)(v < -32768 ? -32768 : v > 32767 ? 32767 : v);

    // JUSTIFICATION: PSX hardware adaptation only
    // RELATION: renders `frames` stereo samples into `interleavedStereo` (must hold at least
    // frames*2 shorts, L/R interleaved), driving every active voice's pitch counter, ADPCM decode,
    // envelope and volume, then the tick callback on its own accumulated schedule. Reverb: PARTIAL —
    // the reverb registers are stored faithfully (WriteReg16) but no reverb DSP runs, so the mix
    // never includes a reverb contribution. Volume sweep mode (bit15 of the volume registers):
    // PARTIAL — not implemented, the raw register value is used as a fixed linear volume.
    public void RenderSamples(short[] interleavedStereo, int frames)
    {
        for (int f = 0; f < frames; f++)
        {
            _tickAccumulator += 1.0;
            while (_tickAccumulator >= SamplesPerTick)
            {
                _tickAccumulator -= SamplesPerTick;
                _tickCallback?.Invoke();
            }

            int mixL = 0;
            int mixR = 0;

            for (int i = 0; i < VoiceCount; i++)
            {
                Voice v = _voices[i];
                if (!v.Active)
                    continue;

                int sample;
                if (IsNoiseVoice(i))
                {
                    // BLOCKED: noise generator not implemented — silence stands in for it.
                    sample = 0;
                }
                else
                {
                    ushort pitch = ReadReg16(i * VoiceStride + RegVoicePitch);
                    AdvanceVoiceSample(v, pitch);
                    sample = InterpolateVoice(v);
                }

                OnVoiceRawSampleForDiagnostics?.Invoke(i, (short)sample);

                UpdateEnvelope(v);

                long enveloped = ((long)sample * v.EnvLevel) >> 15;

                short volL = (short)ReadReg16(i * VoiceStride + RegVoiceVolL);
                short volR = (short)ReadReg16(i * VoiceStride + RegVoiceVolR);
                mixL += (int)((enveloped * volL) >> 15);
                mixR += (int)((enveloped * volR) >> 15);
            }

            short mainVolL = (short)ReadReg16(RegMainVolL);
            short mainVolR = (short)ReadReg16(RegMainVolR);
            mixL = (int)(((long)mixL * mainVolL) >> 15);
            mixR = (int)(((long)mixR * mainVolR) >> 15);

            interleavedStereo[f * 2] = ClampSample(mixL);
            interleavedStereo[f * 2 + 1] = ClampSample(mixR);
        }
    }
}
