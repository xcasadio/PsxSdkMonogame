using System;
using PsxSdkMonogame;

namespace PsxSdkMonogame;

public static class LibSpu
{
    // JUSTIFICATION: PSX hardware adaptation only
    // RELATION: the virtual SPU this library drives. libspu is a thin register/RAM-access layer over
    // the real chip; SpuCore IS that chip (see PsxSdkMonogame/SpuCore.cs). Every libspu function below that
    // used to write a private g_spuRegisters byte[] now write through here instead, so the reverb
    // chain (and everything ported in this tranche) has a real, ticking SPU underneath it.
    public static readonly SpuCore Spu = new SpuCore();

    // JUSTIFICATION: backend MonoGame only
    // RELATION: the desktop audio pump that keeps Spu's tick callback (see SpuCore.SetTickCallback)
    // actually advancing. Wired up by LibApi.OpenEvent/EnableEvent for the Akao driver's own root-
    // counter-2 event — see LibApi's note on that — and torn down by DisableEvent/CloseEvent.
    public static readonly SpuAudioBackend AudioBackend = new SpuAudioBackend(Spu);

    // ================================================================================
    // libspu globals (0x8009bxxx / 0x8009cxxx BSS), migrated from the game layer's static-globals file.
    // These belong to libspu, not the game: only functions in this file ever read or write them.
    // GHIDRA annotations carried over unchanged from where they were first documented.
    // ================================================================================

    // GHIDRA: g_spuReverbOn @ 0x8009b390
    // CERTAIN: SpuSetReverb writes 0 on the off path and its own on_off argument on the on path,
    // and returns this variable — i.e. it IS the reverb-enabled state libspu reports back.
    public static int g_spuReverbOn;
    // GHIDRA: DAT_8009b394 @ 0x8009b394
    // PARTIAL: read once, by SpuSetReverb, which skips the allocate-area check when it equals 1.
    // Nothing in AkaoStopAllMusic's tree writes it, so its meaning is not closed here.
    public static int DAT_8009b394;
    // GHIDRA: g_spuReverbWorkAreaAddr @ 0x8009b398
    // CERTAIN: SpuSetReverbModeType sets it to g_spuReverbWorkAreaAddrTable[mode], SpuSetReverb
    // passes it to _SpuIsInAllocateArea_, and the mode setter feeds it to _spu_FsetRXX(0xd1, ...) —
    // SPU register 0x1f801da2, the reverb work-area start address.
    public static int g_spuReverbWorkAreaAddr;
    // GHIDRA: g_spuReverbModeType @ 0x8009b3a0
    // CERTAIN: SpuSetReverbModeType stores its (0x100-stripped) mode here and SpuGetReverbModeType
    // @0x8008d7c0 reads it straight back out — which is exactly how AkaoSetReverb tests whether the
    // requested mode is already the current one.
    public static int g_spuReverbModeType;
    // GHIDRA: DAT_8009b3a4 @ 0x8009b3a4 — halfword, zeroed by SpuSetReverbModeType (0x8008d0cc)
    public static ushort DAT_8009b3a4;
    // GHIDRA: DAT_8009b3a6 @ 0x8009b3a6 — halfword, zeroed by SpuSetReverbModeType (0x8008d0d4)
    public static ushort DAT_8009b3a6;
    // GHIDRA: DAT_8009b3a8 @ 0x8009b3a8
    // GHIDRA: DAT_8009b3ac @ 0x8009b3ac
    // PARTIAL: SpuSetReverbModeType sets the pair to (0x7f, 0x7f) for mode 7, (0x7f, 0) for mode 8
    // and (0, 0) for every other mode. They are WRITE-ONLY in SLUS_006.62 — the only other writer is
    // _SpuInit @0x8007d074 and there is no reader anywhere — so which one is the delay and which the
    // feedback cannot be closed from this binary, and both names are left raw rather than guessed
    // from the libspu SpuReverbAttr field order.
    public static int DAT_8009b3a8;
    public static int DAT_8009b3ac;
    // GHIDRA: DAT_8009b418 @ 0x8009b418
    // CERTAIN as SpuSetTransferMode's manual/CPU-poll transfer flag: SpuSetTransferMode(1) sets it to
    // 1 (the manual FIFO-write path, SPU_OBJ_280 @0x8007d454), any other mode sets it to 0 (the DMA
    // path _spu_Fw normally takes). SpuClearReverbWorkArea saves/forces-0/restores it as its own
    // reentrancy guard across the DMA zero-fill loop.
    public static int DAT_8009b418;
    // GHIDRA: DAT_8009b424 @ 0x8009b424
    // CERTAIN as a shift count: every use is `x << (DAT_8009b424 & 0x1f)` or `x >> (...)` converting
    // between SPU work-area units and byte addresses (_SpuIsInAllocateArea_, SpuClearReverbWorkArea,
    // _spu_FsetRXX, _spu_FsetRXXa, SpuInitMalloc). Its VALUE is set outside this tree, by _SpuInit,
    // which is not reached in this port (see SpuStart below) — it stays 0 (identity shift) here.
    public static int DAT_8009b424;
    // GHIDRA: DAT_8009b434 @ 0x8009b434
    // PARTIAL: same save/force-0/restore treatment as DAT_8009b418 inside SpuClearReverbWorkArea; also
    // gates whether FUN_80085e54/SpuWrite clears DAT_8009b430 after a transfer.
    public static int DAT_8009b434;
    // GHIDRA: DAT_8009b464 @ 0x8009b464
    // CERTAIN as the head of libspu's SPU-RAM allocation list, and now REAL: SpuInitMalloc sets it to
    // the caller-provided management table (InitSpuAndTimerEvent's own call site passes `&DAT_800b6958`
    // with 4 blocks — that call site is not itself reached in this port, see SpuStart below, so this
    // stays null in practice), and _SpuIsInAllocateArea_ walks it two words at a time (block flags/
    // addr, then length). Represented as the caller's byte[] rather than a raw PSX pointer: word i's
    // two 4-byte fields sit at byte offsets i*8 and i*8+4 inside it, which is what lets
    // _SpuIsInAllocateArea_ walk it with no PSX address space at all (see below, and the removed
    // LibGpu.RamResolve dependency this replaces).
    public static byte[] DAT_8009b464;
    // GHIDRA: DAT_8009b384 @ 0x8009b384 — the BIOS event SpuClearReverbWorkArea WaitEvent()s on
    public static long DAT_8009b384;
    // GHIDRA: g_spuReverbWorkAreaAddrTable @ 0x8009b46c
    // CERTAIN: indexed by reverb mode (0..9) by both SpuSetReverbModeType and
    // SpuClearReverbWorkArea. Values read out of the running binary at 0x8009b46c.
    public static int[] g_spuReverbWorkAreaAddrTable =
    {
        0x0000fffe, 0x0000fb28, 0x0000fc18, 0x0000f6f8, 0x0000f204,
        0x0000ea44, 0x0000e128, 0x0000cff8, 0x0000cff8, 0x0000f880,
    };
    // GHIDRA: g_spuReverbPresetTable @ 0x8009c8c0
    // CERTAIN as the layout: SpuSetReverbModeType copies 0x44 bytes from
    // g_spuReverbPresetTable + mode * 0x44 into its stack attr block, zeroes the block's first word
    // (the mask, i.e. "apply every field") and hands it to _spu_setReverbAttr — which writes exactly
    // 32 halfwords to SPU registers 0x1c0..0x1fe. So one preset is `uint mask; short regs[32]`.
    // PARTIAL: the initialiser bytes are NOT reproduced here. They are the SPU reverb coefficient
    // presets, and their only consumer is _spu_setReverbAttr writing them into the SPU register
    // block, which no desktop code reads back (see LibSpu.Spu / SpuCore.cs). Copying 680 bytes of
    // coefficients in would be transcription without an observable effect; the copy loop itself is
    // transliterated literally so the shape stays visible.
    public static byte[] g_spuReverbPresetTable = new byte[10 * 0x44];
    // GHIDRA: DAT_8009c4c0 @ 0x8009c4c0
    // CERTAIN as the source block SpuClearReverbWorkArea DMAs into SPU RAM in 0x400-byte chunks to
    // zero the reverb work area. _spu_t below now really deposits it into SpuCore.SpuRam.
    public static byte[] DAT_8009c4c0 = new byte[0x400];

    // --- New globals this tranche's transliterations touch, all previously unmodelled ---

    // GHIDRA: DAT_8009b38c @ 0x8009b38c
    // CERTAIN: SpuSetTransferMode's own mode latch (0 = DMA, 1 = manual/CPU-poll, anything else
    // stored as-is but treated as DMA by DAT_8009b418). Write-only in the reachable tree.
    public static int DAT_8009b38c;
    // JUSTIFICATION: PSX hardware adaptation only
    // RELATION: SpuStart/SpuQuit's own idempotency guard (raw MIPS: DAT_8009b3ec). Real hardware uses
    // it to avoid opening the SPU DMA event twice; kept here so the two functions stay idempotent even
    // though neither is reached by any live call site in this port (see SpuStart below).
    public static int DAT_8009b3ec;
    // GHIDRA: DAT_8009b414 @ 0x8009b414
    // CERTAIN: SpuSetTransferStartAddr's latched current transfer address (address units, not yet
    // shifted to bytes — _spu_Fw does `DAT_8009b414 << (DAT_8009b424 & 0x1f)` when it hands the value
    // to _spu_t). SpuSetTransferStartAddr also returns it.
    public static int DAT_8009b414;
    // GHIDRA: DAT_8009b430 @ 0x8009b430
    // PARTIAL: cleared by FUN_80085e54/SpuWrite after every transfer unless DAT_8009b434 (the
    // reentrancy guard SpuClearReverbWorkArea also uses) is set. No reachable reader.
    public static int DAT_8009b430;
    // GHIDRA: DAT_8009b438 @ 0x8009b438
    // CERTAIN: SpuSetIRQCallback's own callback-pointer latch; it IS the "current callback" the
    // function compares new registrations against and returns as its old value.
    public static int DAT_8009b438;
    // GHIDRA: DAT_8009b45c @ 0x8009b45c, DAT_8009b460 @ 0x8009b460
    // PARTIAL: SpuInitMalloc's own bookkeeping (block-table capacity and in-use count respectively).
    // Write-only in the reachable tree — nothing here calls SpuMalloc to read them back.
    public static int DAT_8009b45c;
    public static int DAT_8009b460;
    public delegate void SpuIRQCallbackProc();

    public delegate void SpuTransferCallbackProc();

    public delegate void SpuStCallbackProc(uint voice_bit, int status);

    public class SpuVolume
    {
        public short left;
        public short right;
    }

    public class SpuVoiceAttr
    {
        public uint voice;
        public uint mask;
        public SpuVolume volume;
        public SpuVolume volmode;
        public SpuVolume volumex;
        public ushort pitch;
        public ushort note;
        public ushort sample_note;
        public short envx;
        public uint addr;
        public uint loop_addr;
        public int a_mode;
        public int s_mode;
        public int r_mode;
        public ushort ar;
        public ushort dr;
        public ushort sr;
        public ushort rr;
        public ushort sl;
        public ushort adsr1;
        public ushort adsr2;
    }

    public class SpuLVoiceAttr
    {
        public short voiceNum;
        public short pad;
        public SpuVoiceAttr attr;
    }

    public class SpuExtAttr
    {
        public SpuVolume volume;
        public int reverb;
        public int mix;
    }

    public class SpuCommonAttr
    {
        public uint mask;
        public SpuVolume mvol;
        public SpuVolume mvolmode;
        public SpuVolume mvolx;
        public SpuExtAttr cd;
        public SpuExtAttr ext;
    }

    public class SpuReverbAttr
    {
        public uint mask;
        public int mode;
        public SpuVolume depth;
        public int delay;
        public int feedback;
    }

    public class SpuEnv
    {
        public uint mask;
        public uint queueing;
    }

    private const int SPU_DECODEDATA_SIZE = 0x200;

    public class SpuDecodeData
    {
        public short[] cd_left = new short[SPU_DECODEDATA_SIZE];
        public short[] cd_right = new short[SPU_DECODEDATA_SIZE];
        public short[] voice1 = new short[SPU_DECODEDATA_SIZE];
        public short[] voice3 = new short[SPU_DECODEDATA_SIZE];
    }

    public class SpuStVoiceAttr
    {
        public byte status;
        public char pad1, pad2, pad3;
        public int last_size;
        public uint buf_addr;
        public uint data_addr;
    }

    public class SpuStEnv
    {
        public int size;
        public int low_priority;
        public SpuStVoiceAttr[] voice = new SpuStVoiceAttr[24];
    }
    // GHIDRA: SpuInit @ 0x8002C1DC in DBZ Legends SLPS_003.55.
    // JUSTIFICATION: PSX hardware adaptation only — the physical SPU runs continuously after
    // initialization; the dedicated desktop audio pump is the corresponding device clock.
    // Parasite Eve SLUS_006.62 does not link this public entry point and starts the same backend
    // through its timer-event adapter, so its existing path is unchanged.
    public static void SpuInit()
    {
        AudioBackend.Start();
    }

    // SpuInitHot is not linked into Parasite Eve SLUS_006.62; no DBZ call is currently mapped to
    // this SDK entry point either.
    public static void SpuInitHot()
    {
        // Do nothing PSX SDK — not linked into SLUS_006.62 (see note above).
    }
    // GHIDRA: SpuStart @ 0x8007d15c
    // CERTAIN as the guard: `if (DAT_8009b3ec == 0) { DAT_8009b3ec = 1; ... }` — idempotent, matching
    // SpuQuit's mirror-image guard below.
    // JUSTIFICATION: PSX hardware adaptation for the guarded body — real hardware installs the SPU DMA
    // completion interrupt handler here (`FUN_8007dd14(_spu_FiDMA)`) and opens/enables the BIOS event
    // (0xf0000009) SpuClearReverbWorkArea already WaitEvent()s on via DAT_8009b384. LibApi has no
    // OpenEvent/EnterCriticalSection adapters yet (OpenEvent is commented out, unimplemented, in
    // PsxSdkMonogame/LibApi.cs) and there is no interrupt controller on desktop, so this port keeps the
    // idempotency contract without inventing those adapters. Not reached by any live call site in
    // this port: its only callers are _SpuInit (itself reached only from SsUtReverbOff, whose C# port
    // in the game layer's SLUS_006_62.cs has `_SpuInit(0)` commented out) and InitSpuAndTimerEvent (whose sole
    // call, in ResetAudioAndWaitForCDReady, is likewise commented out as "No-op on desktop").
    public static void SpuStart()
    {
        if (DAT_8009b3ec == 0)
        {
            DAT_8009b3ec = 1;
        }
    }
    // GHIDRA: SpuQuit @ 0x80085984
    // CERTAIN as the guard, mirroring SpuStart. JUSTIFICATION: PSX hardware adaptation for the body —
    // see SpuStart above; same missing LibApi adapters (CloseEvent/DisableEvent/critical section) and
    // same unreached status (sole caller ShutdownAkaoSystem's SpuQuit() call site IS live, but SpuStart
    // never having run means DAT_8009b3ec is 0 here, so the guarded body never executes in this port
    // either way — see the DAT_8009b3ec field comment on SpuStart).
    public static void SpuQuit()
    {
        if (DAT_8009b3ec == 1)
        {
            DAT_8009b3ec = 0;
        }
    }
    // GHIDRA: SpuKeyOffVoices @ 0x80087728
    // CERTAIN, and now REAL: `SPU_VOICE_KEY_OFF = voiceBitmask;` — Ghidra's SPU_VOICE_KEY_OFF label is
    // 0x1f801d8c, i.e. SpuCore.RegKoffLo (0x18c) as a dword: the store hits both the lo and hi KOFF
    // halfwords at once (KOFF spans two consecutive halfword registers, 0x18c/0x18e), which is why a
    // single dword write reaches all 24 voices with one bitmask. ShutdownAkaoSystem's SpuKeyOffVoices
    // (0xffffff) call is a live call site — voices now genuinely key off (drop into Release) when it
    // fires.
    public static void SpuKeyOffVoices(uint voiceMask)
    {
        KkDiag.Log($"SpuKeyOffVoices(mask=0x{voiceMask:X})");
        Spu.WriteReg16(SpuCore.RegKoffLo, (ushort)voiceMask);
        Spu.WriteReg16(SpuCore.RegKoffHi, (ushort)(voiceMask >> 16));
    }
    public static void SpuSetVoiceAttr(object attr)
    {
        // Do nothing PSX SDK
    }
    public static void SpuGetVoiceAttr(object attr)
    {
        // Do nothing PSX SDK
    }
    public static void SpuNSetVoiceAttr(int voiceNum, object attr)
    {
        // Do nothing PSX SDK
    }
    public static void SpuNGetVoiceAttr(int voiceNum, object attr)
    {
        // Do nothing PSX SDK
    }
    public static int SpuRSetVoiceAttr(int min, int max, object attr)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static void SpuLSetVoiceAttr(int num, object argList)
    {
        // Do nothing PSX SDK
    }
    public static void SpuSetVoiceVolume(int voiceNum, short volumeL, short volumeR)
    {
        // Do nothing PSX SDK
    }
    public static void SpuGetVoiceVolume(int voiceNum, object volumeL, object volumeR)
    {
        // Do nothing PSX SDK
    }
    public static void SpuSetVoiceVolumeAttr(int voiceNum, short volumeL, short volumeR, short volModeL, short volModeR)
    {
        // Do nothing PSX SDK
    }
    public static void SpuGetVoiceVolumeAttr(int voiceNum, object volumeL, object volumeR, object volModeL, object volModeR)
    {
        // Do nothing PSX SDK
    }
    public static void SpuGetVoiceVolumeX(int voiceNum, object volumeXL, object volumeXR)
    {
        // Do nothing PSX SDK
    }
    public static void SpuSetVoicePitch(int voiceNum, ushort pitch)
    {
        // Do nothing PSX SDK
    }
    public static void SpuGetVoicePitch(int voiceNum, object pitch)
    {
        // Do nothing PSX SDK
    }
    public static void SpuSetVoiceNote(int voiceNum, ushort note)
    {
        // Do nothing PSX SDK
    }
    public static void SpuGetVoiceNote(int voiceNum, object note)
    {
        // Do nothing PSX SDK
    }
    public static void SpuSetVoiceSampleNote(int voiceNum, ushort sampleNote)
    {
        // Do nothing PSX SDK
    }
    public static void SpuGetVoiceSampleNote(int voiceNum, object sampleNote)
    {
        // Do nothing PSX SDK
    }
    public static void SpuSetVoiceStartAddr(int voiceNum, uint startAddr)
    {
        // Do nothing PSX SDK
    }
    public static void SpuGetVoiceStartAddr(int voiceNum, object startAddr)
    {
        // Do nothing PSX SDK
    }
    public static void SpuSetVoiceLoopStartAddr(int voiceNum, uint loopStartAddr)
    {
        // Do nothing PSX SDK
    }
    public static void SpuGetVoiceLoopStartAddr(int voiceNum, object loopStartAddr)
    {
        // Do nothing PSX SDK
    }
    public static void SpuSetVoiceADSR(int voiceNum, ushort AR, ushort DR, ushort SR, ushort RR, ushort SL)
    {
        // Do nothing PSX SDK
    }
    public static void SpuGetVoiceADSR(int voiceNum, object AR, object DR, object SR, object RR, object SL)
    {
        // Do nothing PSX SDK
    }
    public static void SpuSetVoiceADSRAttr(int voiceNum, ushort AR, ushort DR, ushort SR, ushort RR, ushort SL, int ARmode, int SRmode, int RRmode)
    {
        // Do nothing PSX SDK
    }
    public static void SpuGetVoiceADSRAttr(int voiceNum, object AR, object DR, object SR, object RR, object SL, object ARmode, object SRmode, object RRmode)
    {
        // Do nothing PSX SDK
    }
    public static void SpuSetVoiceAR(int voiceNum, ushort AR)
    {
        // Do nothing PSX SDK
    }
    public static void SpuGetVoiceAR(int voiceNum, object AR)
    {
        // Do nothing PSX SDK
    }
    public static void SpuSetVoiceARAttr(int voiceNum, ushort AR, int ARmode)
    {
        // Do nothing PSX SDK
    }
    public static void SpuGetVoiceARAttr(int voiceNum, object AR, object ARmode)
    {
        // Do nothing PSX SDK
    }
    public static void SpuSetVoiceDR(int voiceNum, ushort DR)
    {
        // Do nothing PSX SDK
    }
    public static void SpuGetVoiceDR(int voiceNum, object DR)
    {
        // Do nothing PSX SDK
    }
    public static void SpuSetVoiceSR(int voiceNum, ushort SR)
    {
        // Do nothing PSX SDK
    }
    public static void SpuGetVoiceSR(int voiceNum, object SR)
    {
        // Do nothing PSX SDK
    }
    public static void SpuSetVoiceSRAttr(int voiceNum, ushort SR, int SRmode)
    {
        // Do nothing PSX SDK
    }
    public static void SpuGetVoiceSRAttr(int voiceNum, object SR, object SRmode)
    {
        // Do nothing PSX SDK
    }
    public static void SpuSetVoiceSL(int voiceNum, ushort SL)
    {
        // Do nothing PSX SDK
    }
    public static void SpuGetVoiceSL(int voiceNum, object SL)
    {
        // Do nothing PSX SDK
    }
    public static void SpuSetVoiceRR(int voiceNum, ushort RR)
    {
        // Do nothing PSX SDK
    }
    public static void SpuGetVoiceRR(int voiceNum, object RR)
    {
        // Do nothing PSX SDK
    }
    public static void SpuSetVoiceRRAttr(int voiceNum, ushort RR, int RRmode)
    {
        // Do nothing PSX SDK
    }
    public static void SpuGetVoiceRRAttr(int voiceNum, object RR, object RRmode)
    {
        // Do nothing PSX SDK
    }
    // GHIDRA: SpuGetVoiceEnvelope @ 0x80089f08
    // CERTAIN (28 bytes, 1 read): `*param_2 = *(short *)(base + voice*0x10 + 0xc)` — ENVX, the
    // per-voice register SpuCore's ADSR stepper writes every RenderSamples tick (SpuCore.RegVoiceEnvx).
    // Real signal: reads the actual current envelope level, not a stub 0.
    public static void SpuGetVoiceEnvelope(int voiceNum, out short envx)
    {
        envx = (short)Spu.ReadReg16(voiceNum * 0x10 + 0xc);
    }
    public static void SpuGetVoiceEnvelopeAttr(int voiceNum, object keyStat, object envx)
    {
        // Do nothing PSX SDK
    }
    // GHIDRA: SpuSetKey — no symbol in SLUS_006.62 (find-cross-references: invalid address/symbol).
    // Not linked, same as SpuInit/SpuInitHot above — no MIPS to transliterate.
    public static void SpuSetKey(int on_off, uint voice_bit)
    {
        // Do nothing PSX SDK — not linked into SLUS_006.62 (see note above).
    }
    public static void SpuSetKeyOnWithAttr(object attr)
    {
        // Do nothing PSX SDK
    }
    public static int SpuGetKeyStatus(uint voice_bit)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static void SpuGetAllKeysStatus(object status)
    {
        // Do nothing PSX SDK
    }
    public static int SpuRGetAllKeysStatus(int min, int max, object status)
    {
        // Do nothing PSX SDK
        return default;
    }
    // GHIDRA: SpuSetCommonAttr @ 0x80085f74
    // CONFLICT resolved: the decomp hypothesis name for this address was "SPU_WriteVoiceRegs". The
    // MIPS (88 + 84*3 bytes reviewed end to end, including the four "S_SCA_OBJ_*" continuations —
    // proven to be this function's own tail-call-chained fallthrough by their raw disassembly, same
    // intra-function-label pattern documented at the top of this file, not separate functions: each
    // one's own body ends by chaining into the next mask-bit check and the last ends in a bare
    // `jr ra`) writes exactly the fields of libspu's SpuCommonAttr struct — main/CD/ext volumes and
    // the CD/ext reverb+mix enable bits in SPUCNT — never touches a per-voice register. That is
    // SpuSetCommonAttr, not "SPU_WriteVoiceRegs"; the decomp hypothesis was wrong for this address.
    // CERTAIN: mask bit pairs gate each field — bit N "update this field", a second bit "use the
    // mode-curve value instead of the raw one" for the two main-volume fields only (1/4 for left,
    // 2/8 for right); mask==0 means "update everything, mode-curve included". The four reverb/mix
    // enable fields (offsets 0x14/0x18/0x20/0x24 — cd.reverb, cd.mix, ext.reverb, ext.mix) OR their
    // SPUCNT bit in when nonzero and leave it untouched (never clear it) when zero — there is no
    // "disable" path in this function at all, matching the fallthrough chain: a zero flag skips this
    // field's write and falls straight into the next field's check, it does not return early.
    public static void SpuSetCommonAttr(SpuCommonAttr attr)
    {
        bool applyAll = attr.mask == 0;

        if (applyAll || (attr.mask & 1) != 0)
        {
            bool useMode = applyAll || (attr.mask & 4) != 0;
            ushort mode = useMode ? MvolCurve(attr.mvolmode.left) : (ushort)0;
            short raw = attr.mvol.left;
            ushort vol = mode != 0 ? ClampMvol(raw) : (ushort)raw;
            Spu.WriteReg16(SpuCore.RegMainVolL, (ushort)((vol & 0x7fff) | mode));
        }
        if (applyAll || (attr.mask & 2) != 0)
        {
            bool useMode = applyAll || (attr.mask & 8) != 0;
            ushort mode = useMode ? MvolCurve(attr.mvolmode.right) : (ushort)0;
            short raw = attr.mvol.right;
            ushort vol = mode != 0 ? ClampMvol(raw) : (ushort)raw;
            Spu.WriteReg16(SpuCore.RegMainVolR, (ushort)((vol & 0x7fff) | mode));
        }
        if (applyAll || (attr.mask & 0x40) != 0)
        {
            Spu.WriteReg16(SpuCore.RegCdVolL, (ushort)attr.cd.volume.left);
        }
        if (applyAll || (attr.mask & 0x80) != 0)
        {
            Spu.WriteReg16(SpuCore.RegCdVolR, (ushort)attr.cd.volume.right);
        }
        if (applyAll || (attr.mask & 0x400) != 0)
        {
            Spu.WriteReg16(SpuCore.RegExtVolL, (ushort)attr.ext.volume.left);
        }
        if (applyAll || (attr.mask & 0x800) != 0)
        {
            Spu.WriteReg16(SpuCore.RegExtVolR, (ushort)attr.ext.volume.right);
        }
        if ((applyAll || (attr.mask & 0x100) != 0) && attr.cd.reverb != 0)
        {
            Spu.WriteReg16(SpuCore.RegSpucnt, (ushort)(Spu.ReadReg16(SpuCore.RegSpucnt) | 4));
        }
        if ((applyAll || (attr.mask & 0x200) != 0) && attr.cd.mix != 0)
        {
            Spu.WriteReg16(SpuCore.RegSpucnt, (ushort)(Spu.ReadReg16(SpuCore.RegSpucnt) | 1));
        }
        if ((applyAll || (attr.mask & 0x1000) != 0) && attr.ext.reverb != 0)
        {
            Spu.WriteReg16(SpuCore.RegSpucnt, (ushort)(Spu.ReadReg16(SpuCore.RegSpucnt) | 8));
        }
        if ((applyAll || (attr.mask & 0x2000) != 0) && attr.ext.mix != 0)
        {
            Spu.WriteReg16(SpuCore.RegSpucnt, (ushort)(Spu.ReadReg16(SpuCore.RegSpucnt) | 2));
        }
    }

    // JUSTIFICATION: C# language bridge only
    // RELATION: the mvolmode -> SPUCNT-style mode-constant switch SpuSetCommonAttr uses for both main
    // volume channels (identical 7-case switch, duplicated by the original for each channel).
    private static ushort MvolCurve(int mode) => mode switch
    {
        1 => 0x8000, 2 => 0x9000, 3 => 0xa000, 4 => 0xb000,
        5 => 0xc000, 6 => 0xd000, 7 => 0xe000, _ => 0,
    };

    // JUSTIFICATION: C# language bridge only
    // RELATION: the mode-curve path's clamp — `raw < 0x80 && raw >= 0 ? raw : 0x7f`.
    private static ushort ClampMvol(short raw) => raw is >= 0 and < 0x80 ? (ushort)raw : (ushort)0x7f;
    public static void SpuGetCommonAttr(object attr)
    {
        // Do nothing PSX SDK
    }
    public static void SpuSetCommonMasterVolume(short mvolL, short mvolR)
    {
        // Do nothing PSX SDK
    }
    public static void SpuGetCommonMasterVolume(object mvolL, object mvolR)
    {
        // Do nothing PSX SDK
    }
    public static void SpuSetCommonMasterVolumeAttr(short mvolL, short mvolR, short mvolModeL, short mvolModeR)
    {
        // Do nothing PSX SDK
    }
    public static void SpuGetCommonMasterVolumeAttr(object mvolL, object mvolR, object mvolModeL, object mvolModeR)
    {
        // Do nothing PSX SDK
    }
    public static void SpuGetCommonMasterVolumeX(object mvolXL, object mvolXR)
    {
        // Do nothing PSX SDK
    }
    public static void SpuSetCommonCDVolume(short cdvolL, short cdvolR)
    {
        // Do nothing PSX SDK
    }
    public static void SpuGetCommonCDVolume(object cdvolL, object cdvolR)
    {
        // Do nothing PSX SDK
    }
    public static void SpuSetCommonCDMix(int on_off)
    {
        // Do nothing PSX SDK
    }
    public static void SpuGetCommonCDMix(object on_off)
    {
        // Do nothing PSX SDK
    }
    public static void SpuSetCommonCDReverb(int on_off)
    {
        // Do nothing PSX SDK
    }
    public static void SpuGetCommonCDReverb(object on_off)
    {
        // Do nothing PSX SDK
    }
    // SpuSetReverb's "Do nothing PSX SDK" stub was removed 2026-07-28 — the real function is
    // transliterated at the bottom of this file (0x80085a64).
    public static int SpuGetReverb()
    {
        // Do nothing PSX SDK
        return default;
    }
    public static int SpuSetReverbModeParam(object attr)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static void SpuGetReverbModeParam(object attr)
    {
        // Do nothing PSX SDK
    }
    // SpuSetReverbModeType's and SpuGetReverbModeType's "Do nothing PSX SDK" stubs were removed
    // 2026-07-28 — the real functions are transliterated at the bottom of this file (0x8008cf70 and
    // 0x8008d7c0).
    public static int SpuSetReverbDepth(object attr)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static void SpuSetReverbModeDepth(short depthL, short depthR)
    {
        // Do nothing PSX SDK
    }
    public static void SpuGetReverbModeDepth(object depthL, object depthR)
    {
        // Do nothing PSX SDK
    }
    public static void SpuSetReverbModeDelayTime(int delay)
    {
        // Do nothing PSX SDK
    }
    public static void SpuGetReverbModeDelayTime(object delay)
    {
        // Do nothing PSX SDK
    }
    public static void SpuSetReverbModeFeedback(int feedback)
    {
        // Do nothing PSX SDK
    }
    public static void SpuGetReverbModeFeedback(object feedback)
    {
        // Do nothing PSX SDK
    }
    public static uint SpuSetReverbVoice(int on_off, uint voice_bit)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static uint SpuGetReverbVoice()
    {
        // Do nothing PSX SDK
        return default;
    }
    public static int SpuReserveReverbWorkArea(int on_off)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static int SpuIsReverbWorkAreaReserved(int on_off)
    {
        // Do nothing PSX SDK
        return default;
    }
    // SpuClearReverbWorkArea's "Do nothing PSX SDK" stub was removed 2026-07-28 — the real function
    // is transliterated at the bottom of this file (0x8008d610).
    // GHIDRA: SpuSetNoiseClock @ 0x80089eb8
    // CONFLICT resolved: the decomp hypothesis flagged this address as possibly AKAO (spu3/spu4.c),
    // not libspu. The MIPS (72 bytes, reviewed end to end) is a plain SPUCNT bitfield write —
    // `SPUCNT = (SPUCNT & 0xc0ff) | (clamp(n_clock, 0, 0x3f) << 8)`, register bits 13-8, the PSX SPU's
    // documented noise generator clock field — with no AKAO command/queue/channel structure touched
    // anywhere in the function. That is libspu, not AKAO; ported here. Nothing in the reachable tree
    // calls it (its one caller, FUN_80089328, is not itself reached), so it is real but currently dead.
    public static int SpuSetNoiseClock(int n_clock)
    {
        int clamped = 0;
        if (n_clock >= 0)
        {
            clamped = n_clock;
            if (n_clock > 0x3f)
            {
                clamped = 0x3f;
            }
        }
        ushort spucnt = Spu.ReadReg16(SpuCore.RegSpucnt);
        spucnt = (ushort)((spucnt & 0xc0ff) | ((clamped & 0x3f) << 8));
        Spu.WriteReg16(SpuCore.RegSpucnt, spucnt);
        return clamped;
    }
    public static int SpuGetNoiseClock()
    {
        // Do nothing PSX SDK
        return default;
    }
    public static uint SpuSetNoiseVoice(int on_off, uint voice_bit)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static uint SpuGetNoiseVoice()
    {
        // Do nothing PSX SDK
        return default;
    }
    public static uint SpuSetPitchLFOVoice(int on_off, uint voice_bit)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static uint SpuGetPitchLFOVoice()
    {
        // Do nothing PSX SDK
        return default;
    }
    public static int SpuSetMute(int on_off)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static int SpuGetMute()
    {
        // Do nothing PSX SDK
        return default;
    }
    // GHIDRA: SpuSetIRQ @ 0x80085c44
    // CERTAIN for the register logic (244 bytes reviewed): mode 0/3 clears SPUCNT bit 6 (IRQ9
    // enable), mode 1/3 sets it — 3 does both, clear then set, i.e. ends up set. Each half then
    // busy-waits up to 0xf00 iterations for the bit to actually read back the new value before
    // printf-timing-out. Register reads are synchronous in this port (no hardware latency), so the
    // bit already matches on the first check and the wait — and its printf/timeout arm — never
    // executes; BLOCKED as dead rather than invented, same as _SpuInit's other timeout-poll siblings
    // (SPU_OBJ_280, see SpuWrite below).
    public static int SpuSetIRQ(int on_off)
    {
        if (on_off == 0 || on_off == 3)
        {
            Spu.WriteReg16(SpuCore.RegSpucnt, (ushort)(Spu.ReadReg16(SpuCore.RegSpucnt) & 0xffbf));
            // BLOCKED: dead busy-wait for bit6==0 + printf("SPU:T/O [%s]\n","wait (IRQ/OFF)") timeout.
        }
        if (on_off == 1 || on_off == 3)
        {
            Spu.WriteReg16(SpuCore.RegSpucnt, (ushort)(Spu.ReadReg16(SpuCore.RegSpucnt) | 0x40));
            // BLOCKED: dead busy-wait for bit6==1 + printf("SPU:T/O [%s]\n","wait (IRQ/ON)") timeout.
        }
        return on_off;
    }
    public static int SpuGetIRQ()
    {
        // Do nothing PSX SDK
        return default;
    }
    public static uint SpuSetIRQAddr(uint addr)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static uint SpuGetIRQAddr()
    {
        // Do nothing PSX SDK
        return default;
    }
    // GHIDRA: SpuSetIRQCallback @ 0x80085d84
    // CERTAIN for the latch (60 bytes): stores the new callback into DAT_8009b438 and returns the
    // previous value, but only actually does anything when the callback is CHANGING — an unchanged
    // registration is a no-op, callback pointer included.
    // JUSTIFICATION: PSX hardware adaptation for FUN_80085dc4 (called only on change) — real hardware
    // calls `InterruptCallback(9, func)`, the BIOS syscall that installs/removes the IRQ9 (SPU) ISR.
    // There is no interrupt controller on desktop and no LibApi.InterruptCallback adapter; the
    // observable half of the contract that matters to callers — the stored callback value this
    // function reads back — is DAT_8009b438 itself, so no separate ISR needs to exist for that
    // contract to hold. Signature changed from SpuIRQCallbackProc to a raw int (matching the MIPS
    // int/int signature): nothing calls this function, so nothing to break.
    public static int SpuSetIRQCallback(int func)
    {
        int previous = DAT_8009b438;
        if (func != DAT_8009b438)
        {
            DAT_8009b438 = func;
        }
        return previous;
    }
    // GHIDRA: SpuSetTransferMode @ 0x80085f14
    // CERTAIN (28 bytes + the S_STM_OBJ_1C fold-in, another `jr ra`-tail label per the section note):
    // mode 1 selects the manual/CPU-polled write path (DAT_8009b418 = 1, SPU_OBJ_280 in _spu_Fw — see
    // SpuWrite below); any other mode selects DMA (DAT_8009b418 = 0). DAT_8009b38c is the mode as
    // given, unvalidated. InitSpuAndTimerEvent's SpuSetTransferMode(0) call site is not itself reached
    // (see SpuStart above), so this is real but currently dead.
    public static void SpuSetTransferMode(int mode)
    {
        DAT_8009b38c = mode;
        DAT_8009b418 = mode == 1 ? 1 : 0;
    }
    public static int SpuGetTransferMode()
    {
        // Do nothing PSX SDK
        return default;
    }
    // GHIDRA: SpuSetTransferStartAddr @ 0x80085eb4
    // CERTAIN (76 bytes + the S_STSA_OBJ_4C fold-in, a `jr ra`-tail label per the section note):
    // rejects addresses outside [0x1010, 0x7eff9], otherwise rounds/latches the address through
    // _spu_FsetRXXa and returns what got latched (DAT_8009b414). InitSpuAndTimerEvent's
    // SpuSetTransferStartAddr(0x1010) call site is not itself reached (see SpuStart above), so this
    // is real but currently dead; AkaoUploadSamples and its siblings call it too, but those call sites
    // are not rebranched onto this body — see the task boundary note at the top of this file.
    public static uint SpuSetTransferStartAddr(uint addr)
    {
        if (addr - 0x1010u >= 0x7efe9u)
        {
            return 0;
        }
        DAT_8009b414 = (int)_spu_FsetRXXa(-1, addr);
        return (uint)DAT_8009b414;
    }

    // GHIDRA: _spu_FsetRXXa @ 0x8007db24
    // CERTAIN (156 bytes, reg==-1/-2 fold together into "SPU_OBJ_9EC" — an 8-byte `jr ra`, per the
    // section note): reg == -1 or -2 is a "round the address, touch no register" sentinel (the value
    // is returned unchanged past the alignment step below); any other reg writes the SPU register at
    // halfword index `reg` (same convention as _spu_FsetRXX) with the value converted from a byte
    // address to SPU work-area units.
    // BLOCKED: the DAT_8009b420 alignment block is real libspu code, but it is dead in this port —
    // DAT_8009b420 is set only by _SpuInit, which is not reached (see SpuStart above), so it is
    // always 0 here and the branch never runs. Kept out entirely rather than modelled with invented
    // globals for a branch nothing can exercise.
    public static uint _spu_FsetRXXa(int reg, uint value)
    {
        if (reg == -2 || reg == -1)
        {
            return value;
        }
        SpuRegWrite16(reg * 2, (ushort)(value >> (DAT_8009b424 & 0x1f)));
        return value;
    }
    public static uint SpuGetTransferStartAddr()
    {
        // Do nothing PSX SDK
        return default;
    }
    // GHIDRA: FUN_80085e54 @ 0x80085e54 (Ghidra's own comment reads "Possible S_W.OBJ/SpuWrite")
    // CERTAIN (92 bytes): this IS libspu's SpuWrite. Clamps the byte count to 0x7eff0 (SPU RAM's
    // 0x80000 bytes minus the 0x1010-byte area SpuInitMalloc's free list starts after), writes via
    // _spu_Fw, then clears DAT_8009b430 unless a reentrant transfer (DAT_8009b434) is in flight.
    // AkaoWriteSpuMemory (the game layer's PsxSystems/Akao.cs) calls this same MIPS function directly but is
    // NOT rebranched onto this port here — see the task boundary note at the top of this file.
    public static uint SpuWrite(byte[] source, uint size) => SpuWrite(source, 0, size);

    // JUSTIFICATION: C# language bridge — see _spu_t's byte[]+sourceOffset overload above; this is the
    // same offset threading one level up, for AkaoWriteSpuMemory's own offset overload.
    public static uint SpuWrite(byte[] source, int sourceOffset, uint size)
    {
        if (size > 0x7eff0)
        {
            size = 0x7eff0;
        }
        _spu_Fw(source, sourceOffset, size);
        if (DAT_8009b434 == 0)
        {
            DAT_8009b430 = 0;
        }
        return size;
    }

    // GHIDRA: _spu_Fw @ 0x8007d9f8
    // CERTAIN for the DMA path (112 bytes; "SPU_OBJ_894" is this function's own `jr ra` tail per the
    // section note, not a call): DAT_8009b418 == 0 (always true here — SpuSetTransferMode(1)'s
    // manual/CPU-poll path is never selected in this port, see SpuSetTransferMode above) sets the
    // transfer address from DAT_8009b414 (converted to bytes), starts the DMA, then writes the block —
    // now for real, via _spu_t below, straight into SpuCore.SpuRam.
    // BLOCKED: the manual/CPU-polled path (DAT_8009b418 != 0, SPU_OBJ_280 @0x8007d454 — 272 bytes of
    // direct SPU FIFO-register polling with its own timeout/printf) is unreachable in this port and is
    // not transliterated; inventing it would add code no live path can ever exercise.
    public static uint _spu_Fw(byte[] source, uint size) => _spu_Fw(source, 0, size);

    // JUSTIFICATION: C# language bridge — offset threading, see _spu_t's overload above.
    public static uint _spu_Fw(byte[] source, int sourceOffset, uint size)
    {
        if (DAT_8009b418 == 0)
        {
            _spu_t(2, (int)((uint)DAT_8009b414 << (DAT_8009b424 & 0x1f)));
            _spu_t(1);
            _spu_t(3, source, sourceOffset, size);
            return size;
        }
        // BLOCKED: SPU_OBJ_280 manual/CPU-polled path — see above.
        return size;
    }
    public static uint SpuWrite0(uint size)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static uint SpuWritePartly(object addr, uint size)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static uint SpuRead(object addr, uint size)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static int SpuReadDecodedData(object d_data, int flag)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static int SpuIsTransferCompleted(int flag)
    {
        // Do nothing PSX SDK
        return default;
    }
    // GHIDRA: SpuSetTransferCallback @ 0x80085f44 (was FUN_80085f44)
    // CORRECTION 2026-08-19: the note this replaced ("no symbol, not linked") was wrong — Ghidra
    // resolves this address to a named function with two real callers, AkaoBeginSpuTransfer
    // (registers AkaoSpuTransferDoneCallback) and AkaoSpuTransferDoneCallback itself (unregisters with
    // 0). See docs/audio-akao/transliteration-inventory-2026-08-19.md category C.
    // CERTAIN (36 bytes): `if (param_1 != DAT_8009b434) DAT_8009b434 = param_1;` — a redundant check
    // on what is, on hardware, a raw function pointer; kept literally.
    // JUSTIFICATION: C# bridge for the pointer itself — DAT_8009b434 stays the plain int fidelity flag
    // it already was (see its own field note: reentrancy guard elsewhere in this file), storing a
    // nonzero marker rather than a real function pointer. The actual callable is not recovered through
    // it: AkaoWriteSpuMemory calls AkaoSpuTransferDoneCallback directly once its (synchronous, on
    // desktop) write completes, exactly the effect the registered interrupt callback has on hardware.
    public static void SpuSetTransferCallback(int callbackMarker)
    {
        if (callbackMarker != DAT_8009b434)
        {
            DAT_8009b434 = callbackMarker;
        }
    }
    // GHIDRA: SpuInitMalloc @ 0x80085a04
    // CERTAIN (76 bytes + the S_M_INIT_OBJ_4C fold-in, an 8-byte `jr ra` per the section note):
    // numBlocks < 1 is a bare no-op return. Otherwise it seeds the caller-provided management table
    // as ONE terminator entry (word0 = 0x40001010: bit30 set = terminator, address 0x1010 = the first
    // usable byte, matching SpuSetTransferStartAddr's own lower bound) covering the rest of SPU RAM
    // (word1 = 0x10000<<shift - 0x1010), i.e. "nothing allocated yet". DAT_8009b460/DAT_8009b45c are
    // libspu's own bookkeeping (see their field comments); DAT_8009b464 becomes the table.
    // mallocTable is the caller's buffer (InitSpuAndTimerEvent's one call site passes 4 blocks worth,
    // 4*8=32 bytes — the table only needs 8 bytes for the seed entry SpuInitMalloc itself writes, the
    // rest is headroom for a later SpuMalloc, which is not reached and not ported here).
    public static void SpuInitMalloc(int numBlocks, byte[] mallocTable)
    {
        if (numBlocks < 1)
        {
            return;
        }
        BitConverter.GetBytes(0x40001010u).CopyTo(mallocTable, 0);
        DAT_8009b460 = 0;
        DAT_8009b45c = numBlocks;
        DAT_8009b464 = mallocTable;
        uint length = (uint)((0x10000 << (DAT_8009b424 & 0x1f)) - 0x1010);
        BitConverter.GetBytes(length).CopyTo(mallocTable, 4);
    }

    //long* SpuMalloc(long size);

    //long* SpuMallocWithStartAddr(long* addr, long size);

    public static void SpuFree(object addr)
    {
        // Do nothing PSX SDK
    }
    public static void SpuSetEnv(object env)
    {
        // Do nothing PSX SDK
    }
    public static uint SpuFlush(uint ev)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static void SpuSetESA(int revAddr)
    {
        // Do nothing PSX SDK
    }

    public static SpuStEnv SpuStInit(long mode)
    {
        // Do nothing PSX SDK
        return default;
    }

    public static int SpuStQuit()
    {
        // Do nothing PSX SDK
        return default;
    }
    public static int SpuStGetStatus()
    {
        // Do nothing PSX SDK
        return default;
    }
    public static uint SpuStGetVoiceStatus()
    {
        // Do nothing PSX SDK
        return default;
    }
    public static int SpuStTransfer(int flag, uint voice_bit)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static SpuStCallbackProc SpuStSetPreparationFinishedCallback(SpuStCallbackProc callback_proc)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static SpuStCallbackProc SpuStSetTransferFinishedCallback(SpuStCallbackProc callback_proc)
    {
        // Do nothing PSX SDK
        return default;
    }
    public static SpuStCallbackProc SpuStSetStreamFinishedCallback(SpuStCallbackProc callback_proc)
    {
        // Do nothing PSX SDK
        return default;
    }

    // ================================================================================
    // Reverb chain — the libspu half of AkaoStopAllMusic's callee tree, transliterated
    // 2026-07-28. These are NOT stubs: SLUS_006.62 links libspu statically, so every one of
    // them is real code in the binary and is ported instruction-faithfully here.
    //
    // Reached as: AkaoStopAllMusic -> AkaoEnqueueCmd -> AkaoSetReverb -> SpuGetReverbModeType /
    // SpuSetReverb / SpuSetReverbModeType -> _SpuIsInAllocateArea_, _spu_setReverbAttr,
    // SpuClearReverbWorkArea, _spu_FsetRXX.
    //
    // A NOTE ON GHIDRA'S CALL TREE, because it is misleading: get-call-tree lists
    // S_SRMT_OBJ_FC/10C/1B4, S_SR_OBJ_B0/B4, S_CRWA_OBJ_9C/170, SPU_OBJ_948 and
    // S_M_UTIL_OBJ_104 as callees. They are NOT functions. Each sits at a fixed offset inside
    // the function above it (0x8008d06c = SpuSetReverbModeType + 0xFC, 0x8008d07c = +0x10C,
    // 0x8008d124 = +0x1B4, 0x80085b14/0x80085b18 = SpuSetReverb + 0xB0/+0xB4, 0x8008d6ac /
    // 0x8008d780 = SpuClearReverbWorkArea + 0x9C/+0x170) and the raw MIPS shows plain
    // fall-through or a `j` into a shared epilogue, not a call. They are intra-object labels
    // from a symbol file that Ghidra promoted to functions, which is also why the decompiler
    // renders them with `unaff_s2`-style phantom locals. They are folded back into their real
    // function bodies below and are not ported separately.
    // ================================================================================

    // JUSTIFICATION: PSX hardware adaptation only
    // RELATION: the SPU's memory-mapped register block. PTR_VOICE_00_LEFT_RIGHT_8009b3fc holds the
    // constant 0x1f801c00 (read out of the binary), and every register access in this chain is
    // written `*(short *)(that_pointer + offset)`. The offsets used run 0x184..0x1fe, so SpuCore's
    // 0x200-byte register image covers the block exactly (0x1f801c00..0x1f801dff): 0x184/0x186 are
    // the reverb volume L/R, 0x1aa is SPUCNT, 0x1c0..0x1fe are the 32 reverb coefficient registers.
    // Tranche 1 (SpuCore.cs) built the hardware model with no wiring; from this tranche on every
    // register access in this file goes through it for real, so voices this port drives now hear it.
    //
    // JUSTIFICATION: C# language bridge only
    // RELATION: thin renames of Spu.ReadReg16/WriteReg16 kept so the 30+ call sites below (all written
    // before SpuCore existed) didn't need touching one by one.
    private static ushort SpuRegRead16(int offset) => Spu.ReadReg16(offset);

    private static void SpuRegWrite16(int offset, ushort value) => Spu.WriteReg16(offset, value);

    // GHIDRA: SpuGetReverbModeType @ 0x8008d7c0
    // RENAMED 2026-07-28 from FUN_8008d7c0, prototype set to void(int *) (Ghidra updated to match).
    // CERTAIN (16 bytes, 4 instructions, all of them read): `lui v0,0x800a; lw v0,-0x4c60(v0);
    // jr ra; sw v0,0x0(a0)` — i.e. `*outMode = g_spuReverbModeType`, the value SpuSetReverbModeType
    // stores. The out-parameter shape is the original's, not a C# convenience.
    public static void SpuGetReverbModeType(out int outMode)
    {
        outMode = g_spuReverbModeType;
    }

    // GHIDRA: _SpuIsInAllocateArea_ @ 0x80085bb4
    // Prototype set to int(int) 2026-07-28 (Ghidra updated to match; it was undefined(void)).
    // CERTAIN: walks libspu's SPU-RAM allocation list, two words per block — word 0 carries flags in
    // bits 31/30 and the block address in bits 0..27, word 1 the length. Returns 1 when spuAddr
    // falls inside an allocated block, 0 when the terminator (bit 30) is reached first.
    // The `while (true)` is the original's: the loop exits only through the returns, on list data.
    // NOTE: the null-list path returns 0, not garbage — the raw MIPS at 0x80085bd0 is
    // `j 0x80085c38` with `move v0,zero` in the delay slot, jumping to the shared epilogue that
    // Ghidra promoted into the phantom "S_M_UTIL_OBJ_104". That is the path this port always takes
    // in this tranche, because SpuInitMalloc's one call site (InitSpuAndTimerEvent) is not itself
    // reached (see SpuStart below) and DAT_8009b464 stays null.
    // This tranche replaced the walk's mechanism, not its semantics: DAT_8009b464 is now the caller's
    // own byte[] (see the field's comment) instead of a raw PSX pointer resolved through
    // LibGpu.RamResolve, so the list can be walked with an ordinary array index — no PSX address space
    // required, and no dependency on LibGpu left in this file (task D).
    public static int _SpuIsInAllocateArea_(int spuAddr)
    {
        uint uVar2;
        uint uVar3;
        int offset;

        uVar3 = (uint)(spuAddr << (DAT_8009b424 & 0x1f));
        if (DAT_8009b464 == null)
        {
            return 0;
        }

        offset = 0;
        while (true)
        {
            uVar2 = BitConverter.ToUInt32(DAT_8009b464, offset);
            if ((uVar2 & 0x80000000) == 0)
            {
                if ((uVar2 & 0x40000000) != 0)
                {
                    return 0;
                }
                if (uVar3 <= (uVar2 & 0xfffffff))
                {
                    return 1;
                }
                if (uVar3 < (uVar2 & 0xfffffff) + BitConverter.ToUInt32(DAT_8009b464, offset + 4))
                {
                    return 1;
                }
            }
            offset += 8;
        }
    }

    // GHIDRA: SpuSetReverb @ 0x80085a64
    // Prototype set to int(int) 2026-07-28 (Ghidra updated to match; it was undefined(void)).
    // CERTAIN (176 bytes reviewed): off (0) clears g_spuReverbOn and returns it; anything that is
    // neither 0 nor 1 falls to the +0xB4 epilogue and returns whatever g_spuReverbOn already held;
    // on (1) refuses to enable when the work area is not allocated (unless DAT_8009b394 == 1) and
    // otherwise sets g_spuReverbOn and raises SPUCNT bit 7 (reverb master enable).
    // The two "callees" S_SR_OBJ_B0/S_SR_OBJ_B4 are this function's own epilogue labels at +0xB0
    // and +0xB4 — see the note at the top of this section — so they are folded in here.
    public static int SpuSetReverb(int on_off)
    {
        int iVar1;

        if (on_off == 0)
        {
            g_spuReverbOn = 0;
            return g_spuReverbOn;
        }
        if (on_off != 1)
        {
            return g_spuReverbOn;
        }
        // JUSTIFICATION: C# language bridge — the original is
        // `if ((DAT_8009b394 != 1) && (iVar1 = _SpuIsInAllocateArea_(...), iVar1 != 0))`, a C comma
        // operator inside a short-circuit &&. Split into nested ifs, which keeps both the
        // short-circuit (the call only happens when DAT_8009b394 != 1) and the evaluation order.
        if (DAT_8009b394 != 1)
        {
            iVar1 = _SpuIsInAllocateArea_(g_spuReverbWorkAreaAddr);
            if (iVar1 != 0)
            {
                g_spuReverbOn = 0;
                return g_spuReverbOn;
            }
        }
        g_spuReverbOn = on_off;
        // SPUCNT (0x1f801daa) bit 7 = reverb master enable.
        SpuRegWrite16(0x1aa, (ushort)(SpuRegRead16(0x1aa) | 0x80));
        return g_spuReverbOn;
    }

    // GHIDRA: _spu_FsetRXX @ 0x8007dae0
    // CERTAIN (60 bytes): writes one SPU halfword register selected by `reg` (a HALFWORD index, so
    // the byte offset is reg * 2). With mode == 0 the value goes in as-is; otherwise it is first
    // shifted right by DAT_8009b424, i.e. converted from a byte address to SPU work-area units.
    // "SPU_OBJ_948" @0x8007db1c is this function's `jr ra` tail, not a call — see the section note.
    public static void _spu_FsetRXX(int reg, uint value, int mode)
    {
        if (mode == 0)
        {
            SpuRegWrite16(reg * 2, (ushort)value);
            return;
        }
        SpuRegWrite16(reg * 2, (ushort)(value >> (DAT_8009b424 & 0x1f)));
    }

    // GHIDRA: _spu_setReverbAttr @ 0x8008d140
    // CERTAIN (1232 bytes reviewed end to end): 32 mask-gated halfword writes to the SPU reverb
    // register block, registers 0x1c0..0x1fe in order. `attr` is the 0x44-byte block
    // SpuSetReverbModeType builds: word 0 is the mask, then 32 halfwords at byte offsets 4..0x42.
    // A mask of 0 means "write every field" — that is the `bVar1 = (mask == 0)` disjunct the
    // original tests first in every one of the 32 conditions, and it is the only case this port
    // ever takes, because SpuSetReverbModeType zeroes the mask before calling.
    // The 32 conditions are folded into one loop below; that is a mechanical rewrite of a fully
    // regular unrolled sequence, NOT a semantic change: bit i gates the halfword at attr + 4 + i*2
    // going to register offset 0x1c0 + i*2, for i = 0..31, and bit 31 is the original's
    // `(int)mask < 0` sign test.
    public static void _spu_setReverbAttr(byte[] attr, int attrOffset)
    {
        bool bVar1;
        uint uVar2;

        uVar2 = BitConverter.ToUInt32(attr, attrOffset);
        bVar1 = uVar2 == 0;
        for (int i = 0; i < 32; i++)
        {
            if ((bVar1) || ((uVar2 & (1u << i)) != 0))
            {
                SpuRegWrite16(0x1c0 + i * 2, BitConverter.ToUInt16(attr, attrOffset + 4 + i * 2));
            }
        }
    }

    // GHIDRA: SpuClearReverbWorkArea @ 0x8008d610
    // Prototype set to int(uint) 2026-07-28 (Ghidra updated to match; it was undefined(void)).
    // CERTAIN (156 bytes reviewed): zeroes the reverb work area for `mode` by DMAing the 0x400-byte
    // zero block DAT_8009c4c0 into SPU RAM in 0x400-byte chunks, from the mode's work-area address
    // up to the top of SPU RAM (0x10000 units). Mode 0 (SPU_REV_MODE_OFF) has no work area and
    // takes the +0x9C early path; an out-of-range mode, or one whose area is not allocated, falls to
    // the +0x170 epilogue. Both of those "callees" are labels inside this function — section note.
    // DAT_8009b418 and DAT_8009b434 are saved, forced to 0 across the loop and restored: the
    // original's own reentrancy guard, kept.
    // The transfer itself: `_spu_t(2, addr)`, `_spu_t(1)`, `_spu_t(3, &DAT_8009c4c0, size)` then
    // WaitEvent, the SPU DMA engine. This tranche gives _spu_t a real effect (see below) — the zero
    // block genuinely lands in SpuCore.SpuRam now, chunk by chunk, at the addresses this loop compute.
    public static int SpuClearReverbWorkArea(uint mode)
    {
        bool bVar1;
        bool bVar2;
        int iVar3;
        int iVar4;
        uint uVar6;
        uint uVar7;
        int local_28;

        local_28 = 0;
        if (mode < 10)
        {
            iVar4 = _SpuIsInAllocateArea_(g_spuReverbWorkAreaAddrTable[mode]);
            iVar3 = DAT_8009b418;
            if (iVar4 == 0)
            {
                if (mode != 0)
                {
                    iVar4 = g_spuReverbWorkAreaAddrTable[mode];
                    uVar7 = (uint)(0x10000 - iVar4 << (DAT_8009b424 & 0x1f));
                    iVar4 = iVar4 << (DAT_8009b424 & 0x1f);
                    bVar1 = DAT_8009b418 == 1;
                    if (bVar1)
                    {
                        DAT_8009b418 = 0;
                    }
                    bVar2 = true;
                    if (DAT_8009b434 != 0)
                    {
                        local_28 = DAT_8009b434;
                        DAT_8009b434 = 0;
                    }
                    do
                    {
                        uVar6 = 0x400;
                        if (uVar7 < 0x401)
                        {
                            bVar2 = false;
                            uVar6 = uVar7;
                        }
                        _spu_t(2, iVar4);
                        _spu_t(1);
                        _spu_t(3, DAT_8009c4c0, uVar6);
                        uVar7 = uVar7 - 0x400;
                        iVar4 = iVar4 + 0x400;
                        LibApi.WaitEvent(DAT_8009b384);
                    } while (bVar2);
                    if (bVar1)
                    {
                        DAT_8009b418 = iVar3;
                    }
                    if (local_28 != 0)
                    {
                        DAT_8009b434 = local_28;
                    }
                    return 0;
                }
                // SpuClearReverbWorkArea + 0x9C: the mode-0 path.
                return 0;
            }
        }
        // SpuClearReverbWorkArea + 0x170: the shared epilogue.
        return 0;
    }

    // JUSTIFICATION: PSX hardware adaptation only
    // RELATION: libspu's SPU DMA primitive. Real hardware issues op 2 (set the DMA channel's target
    // address), op 1 (start the transfer) and op 3 (hand it a source block + byte count) as three
    // separate register pokes to the DMA controller, then completes asynchronously through an
    // interrupt SpuClearReverbWorkArea/SpuWrite's callers WaitEvent() on. On desktop there is no DMA
    // controller and no latency: op 2 latches the target address, op 1 is a no-op (nothing to
    // "start" — op 3 below applies synchronously), op 3 deposits the source bytes into
    // SpuCore.SpuRam at the latched address and advances it, so a caller doing 2/1/3/2/1/3/... in a
    // chunking loop (SpuClearReverbWorkArea's zero-fill, _spu_Fw's DMA write below) gets the same
    // observable result a real DMA engine would: the whole transfer landed in SPU RAM, in order.
    private static int _spuTransferTargetAddr;

    private static void _spu_t(int op, int addr)
    {
        // op 2: set transfer address.
        _spuTransferTargetAddr = addr;
    }

    private static void _spu_t(int op)
    {
        // op 1: start transfer — nothing to do; op 3 below applies the transfer synchronously.
    }

    private static void _spu_t(int op, byte[] source, uint byteCount) => _spu_t(op, source, 0, byteCount);

    // JUSTIFICATION: C# language bridge — AkaoUploadSamples (Remaster/PsxSystems/Akao.cs) writes from
    // the middle of a larger AKAO block buffer (the sample pool sits after the instrument attribute
    // table), which the original expresses as a raw pointer into the middle of the block. This byte[]
    // + sourceOffset pair is this port's stand-in for that pointer arithmetic (see AkaoWriteSpuMemory's
    // own overload for the same reason at the game layer).
    private static void _spu_t(int op, byte[] source, int sourceOffset, uint byteCount)
    {
        // op 3: write block.
        Spu.WriteRam(_spuTransferTargetAddr, source, sourceOffset, (int)byteCount);
        _spuTransferTargetAddr += (int)byteCount;
    }

    // GHIDRA: SpuSetReverbModeType @ 0x8008cf70
    // Prototype set to int(uint) 2026-07-28 (Ghidra updated to match; it was void(uint) — the raw
    // MIPS returns -1 at 0x8008cfcc `li v0,-1` and 0 at 0x8008d100/0x8008d120).
    // CERTAIN (252 bytes reviewed against the raw MIPS, because Ghidra had split this one function
    // into four): bit 0x100 of the argument is a flag, not part of the mode — it is stripped, and it
    // is what selects whether the reverb work area also gets cleared (s2 at 0x8008cf9c, which the
    // phantom sub-functions render as the uninitialised `unaff_s2`). A mode of 10 or more, or one
    // whose work area is already allocated, returns -1 without touching anything.
    // Otherwise the mode's 0x44-byte preset is copied to a local attr block, its mask word is zeroed
    // ("apply every field"), the mode is recorded, SPUCNT bit 7 is dropped across the update and
    // restored afterwards, and the reverb registers are reprogrammed.
    // Modes 7 and 8 additionally seed DAT_8009b3a8/DAT_8009b3ac before joining the common tail.
    public static int SpuSetReverbModeType(uint mode)
    {
        int iVar2;
        int s2;
        byte[] local_58 = new byte[0x44];

        s2 = 0;
        if ((mode & 0x100) != 0)
        {
            mode = mode & 0xfffffeff;
            s2 = 1;
        }
        if (mode < 10)
        {
            iVar2 = _SpuIsInAllocateArea_(g_spuReverbWorkAreaAddrTable[mode]);
            if (iVar2 == 0)
            {
                g_spuReverbWorkAreaAddr = g_spuReverbWorkAreaAddrTable[mode];
                // The original's byte-by-byte copy of 0x44 bytes (`iVar2 = 0x43; do { ... } while
                // (iVar2 != -1);`), kept as a loop rather than an Array.Copy so the count stays
                // visible as the original wrote it.
                iVar2 = 0x43;
                int src = (int)mode * 0x44;
                int dst = 0;
                do
                {
                    local_58[dst] = g_spuReverbPresetTable[src];
                    src = src + 1;
                    iVar2 = iVar2 + -1;
                    dst = dst + 1;
                } while (iVar2 != -1);
                // mask = 0 -> _spu_setReverbAttr applies every field.
                local_58[0] = 0;
                local_58[1] = 0;
                local_58[2] = 0;
                local_58[3] = 0;
                g_spuReverbModeType = (int)mode;
                if (mode == 7)
                {
                    DAT_8009b3ac = 0x7f;
                    DAT_8009b3a8 = 0x7f;
                }
                else if (mode == 8)
                {
                    DAT_8009b3ac = 0;
                    DAT_8009b3a8 = 0x7f;
                }
                else
                {
                    // SpuSetReverbModeType + 0xFC.
                    DAT_8009b3ac = 0;
                    DAT_8009b3a8 = 0;
                }

                // SpuSetReverbModeType + 0x10C: the tail all three branches fall into.
                ushort uVar1 = SpuRegRead16(0x1aa);
                if ((uVar1 >> 7 & 1) != 0)
                {
                    SpuRegWrite16(0x1aa, (ushort)(SpuRegRead16(0x1aa) & 0xff7f));
                }
                SpuRegWrite16(0x184, 0);
                SpuRegWrite16(0x186, 0);
                DAT_8009b3a4 = 0;
                DAT_8009b3a6 = 0;
                _spu_setReverbAttr(local_58, 0);
                if (s2 != 0)
                {
                    SpuClearReverbWorkArea(mode);
                }
                _spu_FsetRXX(0xd1, (uint)g_spuReverbWorkAreaAddr, 0);
                if ((uVar1 >> 7 & 1) != 0)
                {
                    SpuRegWrite16(0x1aa, (ushort)(SpuRegRead16(0x1aa) | 0x80));
                }
                return 0;
            }
        }
        // SpuSetReverbModeType + 0x1B4: the shared epilogue, reached with v0 = -1.
        return -1;
    }

    // ================================================================================
    // AkaoSpuVoice_* register writers (tranche 5a, LOT 1) — per-voice hardware register
    // stores. Each one verified against the raw MIPS bytes (not just the decompiler's symbol
    // names, which mis-resolve some of these — see AkaoSpuVoice_SetAdsrAttack below for the
    // byte-level proof) and translated to the exact same SpuCore register offsets.
    // ================================================================================

    // GHIDRA: AkaoSpuVoice_SetVolume @ 0x80087798
    // CERTAIN (36 bytes, full decompilation reviewed): `*(ushort*)(&VOICE_00_LEFT_RIGHT +
    // voice*4) = volL & 0x7fff` then `*(ushort*)((int)&VOICE_00_LEFT_RIGHT + voice*0x10 + 2)
    // = volR & 0x7fff` — voice*0x10+0 and voice*0x10+2, i.e. SpuCore.RegVoiceVolL/RegVoiceVolR.
    public static void AkaoSpuVoice_SetVolume(int voiceIndex, ushort volL, ushort volR)
    {
        int baseOff = voiceIndex * 0x10;
        SpuRegWrite16(baseOff + SpuCore.RegVoiceVolL, (ushort)(volL & 0x7fff));
        SpuRegWrite16(baseOff + SpuCore.RegVoiceVolR, (ushort)(volR & 0x7fff));
    }

    // GHIDRA: AkaoSpuVoice_SetPitch @ 0x800877bc
    // CERTAIN (24 bytes, full decompilation reviewed): `(&VOICE_00_ADPCM_SAMPLE_RATE)[voice*8]
    // = pitch`, ushort array index*8 = voice*0x10 bytes, offset 0 relative to that symbol =
    // SpuCore.RegVoicePitch (0x4 relative to the voice's register base).
    public static void AkaoSpuVoice_SetPitch(int voiceIndex, ushort pitch)
    {
        SpuRegWrite16(voiceIndex * 0x10 + SpuCore.RegVoicePitch, pitch);
    }

    // GHIDRA: AkaoSpuVoice_SetStartAddress @ 0x800877d4
    // CERTAIN (28 bytes, full decompilation reviewed): `(&VOICE_00_ADPCM_START_ADDR)[voice*8] =
    // (word)(addr >> 3)` — SpuCore.RegVoiceStartAddr (0x6), addr converted to 8-byte units
    // exactly like SpuCore.KeyOnVoice's own `* 8` decode of the same register.
    public static void AkaoSpuVoice_SetStartAddress(int voiceIndex, uint spuAddr)
    {
        SpuRegWrite16(voiceIndex * 0x10 + SpuCore.RegVoiceStartAddr, (ushort)(spuAddr >> 3));
    }

    // GHIDRA: AkaoSpuVoice_SetRepeatAddress @ 0x800877f0
    // CERTAIN (28 bytes, full decompilation reviewed): identical shape to SetStartAddress but at
    // SpuCore.RegVoiceRepeatAddr (0xE).
    public static void AkaoSpuVoice_SetRepeatAddress(int voiceIndex, uint spuAddr)
    {
        SpuRegWrite16(voiceIndex * 0x10 + SpuCore.RegVoiceRepeatAddr, (ushort)(spuAddr >> 3));
    }

    // GHIDRA: AkaoSpuVoice_SetAdsrAttack @ 0x8008780c
    // CERTAIN — verified byte-for-byte against the raw MIPS (the decompiler's own reconstruction
    // was trusted here only after independently decoding the 12-instruction body):
    //   lui v0,0x1f80; ori v0,v0,0x1c08                  -> v0 = 0x1f801c08 (SpuCore.RegVoiceAdsr1)
    //   sll a0,a0,0x4; addu a0,a0,v0                     -> a0 = adsr1_addr(voice)
    //   srl a2,a2,0x2; sll a2,a2,0xf                     -> a2 = (mode>>2)<<15
    //   sll a1,a1,0x8; or a2,a2,a1                        -> a2 |= rate<<8
    //   lbu v0,0x0(a0); or v0,v0,a2; jr ra; sh v0,0x0(a0) -> reg = existingLowByte | a2
    // i.e. the low byte (Dr/Sl, bits 0-7) is preserved and bits 8-14 (Ar) / bit 15 (Am) are set
    // from rate/mode. Matches SpuCore's documented ADSR1 bit layout exactly.
    public static void AkaoSpuVoice_SetAdsrAttack(int voiceIndex, short rate, uint mode)
    {
        int off = voiceIndex * 0x10 + SpuCore.RegVoiceAdsr1;
        ushort existingLowByte = (ushort)(SpuRegRead16(off) & 0xff);
        ushort newVal = (ushort)(existingLowByte | ((mode >> 2) << 0xf) | (rate << 8));
        SpuRegWrite16(off, newVal);
    }

    // GHIDRA: AkaoSpuVoice_SetAdsrDecayRate @ 0x8008783c
    // CERTAIN (40 bytes, full decompilation reviewed): `reg = reg & 0xff0f | shift << 4` — sets
    // ADSR1 bits 4-7 (Dr), preserves bits 0-3 (Sl) and 8-15 (Am/Ar).
    public static void AkaoSpuVoice_SetAdsrDecayRate(int voiceIndex, short shift)
    {
        int off = voiceIndex * 0x10 + SpuCore.RegVoiceAdsr1;
        SpuRegWrite16(off, (ushort)((SpuRegRead16(off) & 0xff0f) | (shift << 4)));
    }

    // GHIDRA: AkaoSpuVoice_SetAdsrSustainLevel @ 0x80087864
    // CERTAIN (40 bytes, full decompilation reviewed): `reg = reg & 0xfff0 | level` — sets ADSR1
    // bits 0-3 (Sl), preserves bits 4-15 (Dr/Am/Ar).
    public static void AkaoSpuVoice_SetAdsrSustainLevel(int voiceIndex, ushort level)
    {
        int off = voiceIndex * 0x10 + SpuCore.RegVoiceAdsr1;
        SpuRegWrite16(off, (ushort)((SpuRegRead16(off) & 0xfff0) | level));
    }

    // GHIDRA: AkaoSpuVoice_SetAdsrSustainRate @ 0x8008788c
    // CERTAIN (52 bytes, full decompilation reviewed): `(&DAT_1f801c0a)[voice*8] = reg & 0x3f |
    // (mode>>1)<<0xe | rate<<6` — DAT_1f801c0a is SpuCore.RegVoiceAdsr2 (0x1c00+0xA). Preserves
    // bits 0-5 (Rm/Rr), sets bits 6-15 (Sr/Sd/Sm) from rate/mode.
    public static void AkaoSpuVoice_SetAdsrSustainRate(int voiceIndex, short rate, uint mode)
    {
        int off = voiceIndex * 0x10 + SpuCore.RegVoiceAdsr2;
        ushort existing = (ushort)(SpuRegRead16(off) & 0x3f);
        ushort newVal = (ushort)(existing | ((mode >> 1) << 0xe) | (rate << 6));
        SpuRegWrite16(off, newVal);
    }

    // GHIDRA: AkaoSpuVoice_SetAdsrReleaseRate @ 0x800878c0
    // CERTAIN (48 bytes, full decompilation reviewed): `reg = reg & 0xffc0 | (mode>>2)<<5 | rate`
    // — ADSR2, preserves bits 6-15 (Sr/Sd/Sm), sets bits 0-4 (Rr) and bit 5 (Rm).
    public static void AkaoSpuVoice_SetAdsrReleaseRate(int voiceIndex, ushort rate, uint mode)
    {
        int off = voiceIndex * 0x10 + SpuCore.RegVoiceAdsr2;
        ushort existing = (ushort)(SpuRegRead16(off) & 0xffc0);
        ushort newVal = (ushort)(existing | ((mode >> 2) << 5) | rate);
        SpuRegWrite16(off, newVal);
    }

    // GHIDRA: Spu_WriteKeyOn @ 0x8008770c (SPU_VOICE_KEY_ON = 0x1f801d88, SpuCore.RegKonLo/Hi)
    // CERTAIN (28 bytes, full decompilation reviewed): `SPU_VOICE_KEY_ON = mask` split into its
    // two halfword registers — the exact counterpart of SpuKeyOffVoices above (KOFF, +0x18c);
    // this is KON, +0x188.
    public static void SpuKeyOnVoices(uint voiceMask)
    {
        KkDiag.Log($"SpuKeyOnVoices(mask=0x{voiceMask:X})");
        Spu.WriteReg16(SpuCore.RegKonLo, (ushort)voiceMask);
        Spu.WriteReg16(SpuCore.RegKonHi, (ushort)(voiceMask >> 16));
    }

    // GHIDRA: WriteReverbEnable @ 0x80087744 (SPU_VOICE_CHN_REVERB_MODE = 0x1f801d98 ->
    // SpuCore.RegEonLo/Hi, offset 0x198 — the SPU's per-voice Echo/Reverb ON mask; Ghidra's own
    // label name is "REVERB_MODE" but the address resolves to EON, confirmed via get-data).
    // CERTAIN (28 bytes, full decompilation reviewed): same split-dword-store shape as
    // Spu_WriteKeyOn/SpuKeyOffVoices.
    public static void WriteReverbEnable(uint voiceMask)
    {
        Spu.WriteReg16(SpuCore.RegEonLo, (ushort)voiceMask);
        Spu.WriteReg16(SpuCore.RegEonHi, (ushort)(voiceMask >> 16));
    }

    // GHIDRA: WriteNoiseEnable @ 0x80087760 (SPU_VOICE_CHN_NOISE_MODE = 0x1f801d94 ->
    // SpuCore.RegNonLo/Hi, offset 0x194, NON — per-voice noise-generator mask).
    // CERTAIN (28 bytes, full decompilation reviewed): same split-dword-store shape.
    public static void WriteNoiseEnable(uint voiceMask)
    {
        Spu.WriteReg16(SpuCore.RegNonLo, (ushort)voiceMask);
        Spu.WriteReg16(SpuCore.RegNonHi, (ushort)(voiceMask >> 16));
    }

    // GHIDRA: WriteFmEnable @ 0x8008777c (SPU_VOICE_CHN_FM_MODE = 0x1f801d90 ->
    // SpuCore.RegPmonLo/Hi, offset 0x190, PMON — per-voice pitch-modulation-by-previous-voice
    // mask; the PSX SDK itself calls this feature "FM" even though it is amplitude modulation of
    // pitch, not frequency modulation — kept as the original names it).
    // CERTAIN (28 bytes, full decompilation reviewed): same split-dword-store shape.
    public static void WriteFmEnable(uint voiceMask)
    {
        Spu.WriteReg16(SpuCore.RegPmonLo, (ushort)voiceMask);
        Spu.WriteReg16(SpuCore.RegPmonHi, (ushort)(voiceMask >> 16));
    }

    // ================================================================================
    // Voice management layer (tranche 5a, LOT 2).
    // ================================================================================

    // GHIDRA: FUN_80089f28 @ 0x80089f28
    // CERTAIN (40 bytes, full decompilation reviewed): writes SpuCore.RegReverbVolL/RegReverbVolR
    // (0x184/0x186) and mirrors the same two values into DAT_8009b3a4/DAT_8009b3a6 — the same pair
    // SpuSetReverbModeType zeroes. Sole call site (FUN_80089328) passes the SAME value for both
    // params, so this is a single scalar reverb-output-volume setter with a doubled argument, not
    // independent L/R control. NAME LEFT RAW: the decompiler's own guess ("Possible
    // S_SRMD.OBJ/SpuSetReverbModeDepth") and the inventory's "Akao_SetMasterVolume" both conflict
    // with what the body actually touches (reverb output volume, not the main SPUCNT/main-volume
    // registers), so neither is adopted without closer proof.
    public static void FUN_80089f28(short param1, short param2)
    {
        Spu.WriteReg16(SpuCore.RegReverbVolL, (ushort)param1);
        Spu.WriteReg16(SpuCore.RegReverbVolR, (ushort)param2);
        DAT_8009b3a4 = (ushort)param1;
        DAT_8009b3a6 = (ushort)param2;
    }
}
