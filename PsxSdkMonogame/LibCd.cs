using System;

namespace PsxSdkMonogame;

public static class LibCd
{
    // JUSTIFICATION: PSX hardware adaptation only
    // RELATION: desktop latch for the CdlSeekL position consumed by the following CdRead2 call.
    private static CdlLOC s_lastSeekTarget;

    public class CdlLOC
    {
        public byte minute;
        public byte second;
        public byte sector;
        public byte track;
    }

    public class CdlATV
    {
        public byte val0;
        public byte val1;
        public byte val2;
        public byte val3;
    }

    public class CdlFILTER
    {
        public byte file;
        public byte chan;
        public ushort pad;
    }

    public class StHEADER
    {
        public ushort id;
        public ushort type;
        public ushort secCount;
        public ushort nSectors;
        public ulong frameCount;
        public ulong frameSize;

        public ushort width;
        public ushort height;
        public ulong dummy1;
        public ulong dummy2;
        public CdlLOC loc;
    }

    public class CdlFILE
    {
        public CdlLOC pos;
        public int size;
        public char[] name = new char[16];
    }

    public delegate void CdlCB(byte arg1, byte[] arg2);

    public static void def_cbsync(byte intr, byte[] result)
    {
        /* Do nothing */
    }

    public static void def_cbready(byte intr, byte[] result)
    {
        /* Do nothing */
    }

    public static void def_cbread(byte intr, byte[] result)
    {
        /* Do nothing */
    }

    public static int CdInit()
    {
        /* Do nothing */
        return default;
    }

    public static int CdStatus()
    {
        /* Do nothing */
        return default;
    }

    public static int CdMode()
    {
        /* Do nothing */
        return default;
    }

    public static int CdLastCom()
    {
        /* Do nothing */
        return default;
    }

    public static CdlLOC CdLastPos()
    {
        /* Do nothing */
        return default;
    }

    public static int CdReset(int mode)
    {
        /* Do nothing */
        return default;
    }

    public static void CdFlush()
    {
        /* Do nothing */
    }

    public static int CdSetDebug(int level)
    {
        /* Do nothing */
        return default;
    }

    public static char CdComstr(byte com)
    {
        /* Do nothing */
        return default;
    }

    public static char CdIntstr(byte intr)
    {
        /* Do nothing */
        return default;
    }

    // JUSTIFICATION: PSX hardware adaptation only
    // RELATION: reports the state of the last command. On console a command completes
    // asynchronously, so callers spin on this until it stops returning CdlNoIntr; here every
    // command has already finished by the time it returns, so the honest answer is CdlComplete.
    //
    // Returning 0 was NOT neutral: ReadCDData @ 0x80057E40 spins on
    // `do { status = CdSync(0, result); } while (status == 0);` and hung forever on the stub.
    // 5 would be the worst answer — that is CdlDiskError, and the enclosing
    // `while (status == 5)` would retry the read for ever.
    private const int CdlComplete = 2;

    public static int CdSync(int mode, byte[] result)
    {
        return CdlComplete;
    }

    public static int CdReady(int mode, byte[] result)
    {
        /* Do nothing */
        return default;
    }

    public static CdlCB CdSyncCallback(CdlCB func)
    {
        /* Do nothing */
        return default;
    }

    public static CdlCB CdReadyCallback(CdlCB func)
    {
        /* Do nothing */
        return default;
    }

    public static int CdControl(byte com, byte[] param, byte[] result)
    {
        // 0x02 is CdlSetloc and 0x15 is CdlSeekL. Both leave the drive pointing at the same place,
        // which is all this port models. ReadCDData @ 0x80057E40 uses Setloc, the FMV players use
        // SeekL, and both then read from the position recorded here.
        if ((com == 0x02 || com == 0x15) && param != null && param.Length >= 3)
        {
            s_lastSeekTarget = new CdlLOC
            {
                minute = param[0],
                second = param[1],
                sector = param[2],
                track = param.Length > 3 ? param[3] : (byte)0,
            };
            return 1;
        }

        return 0;
    }

    // JUSTIFICATION: C# language bridge only
    // RELATION: typed form of CdControl(CdlSeekL, CdlLOC*, result).
    public static int CdControl(byte com, CdlLOC param, byte[] result)
    {
        if ((com != 0x02 && com != 0x15) || param == null)
        {
            return 0;
        }

        s_lastSeekTarget = new CdlLOC
        {
            minute = param.minute,
            second = param.second,
            sector = param.sector,
            track = param.track,
        };
        return 1;
    }

    public static int CdControlB(byte com, byte[] param, byte[] result)
    {
        if (com == 9)
        {
            LibDs.CurrentStreamSource?.Dispose();
            LibDs.CurrentStreamSource = null;
            return 1;
        }

        return 0;
    }

    public static int CdControlF(byte com, byte[] param)
    {
        /* Do nothing */
        return default;
    }

    // JUSTIFICATION: PSX hardware adaptation only (slice S4, XA movie audio)
    // RELATION: real PSX BIOS CD-library call — CdMix(CdlATV*) sets the CD controller's own ATV
    // (Audio-To-Volume) routing matrix, NOT an SPU register; this is the FIRST of the two volume
    // stages the CD-audio-to-SPU path applies (the second is RegCdVolL/R — see SpuCore.RenderSamples'
    // own note on its CD-input mix). The port's own call site is Spu_SetVoiceVolume (FmvStream.cs:
    // 986-1031 / PeSection70Overlay.cs:1657), the FMV driver's fade path. Previously a no-op; now
    // stores the four routing values for XaAudio to read.
    public static int CdMix(CdlATV vol)
    {
        if (vol != null)
        {
            XaAudio.SetAtv(vol.val0, vol.val1, vol.val2, vol.val3);
        }

        return default;
    }

    public static int CdGetSector(object madr, int size)
    {
        /* Do nothing */
        return default;
    }

    public static int CdGetToc(CdlLOC loc)
    {
        /* Do nothing */
        return default;
    }

    public static CdlCB CdDataCallback(CdlCB func)
    {
        /* Do nothing */
        return default;
    }

    // GHIDRA: CdIntToPos @ 0x80069834 (TITLE.EXE)
    // CLOSED 2026-08-30: decoded from the 65 instructions at 0x80069834..0x80069937, read out of
    // the image with read-memory. Not written from general PSX knowledge — every constant below is
    // one that is actually in those bytes.
    //
    // The register form, in order:
    //   addiu a0,a0,0x96                              i + 150
    //   lui/ori 0x1B4E81B5, mult, mfhi, sra 3, subu   a2 = (i + 150) / 0x4b   (magic-number divide)
    //   sll 2 / addu / sll 4 / subu / subu a0         a0 = (i + 150) % 0x4b
    //   lui/ori 0x88888889, mult, mfhi, addu, sra 5, subu
    //                                                 t1 = a2 / 0x3c
    //   sll 4 / subu / sll 2 / subu a2                a2 = a2 % 0x3c
    //   three times: lui/ori 0x66666667, mult, mfhi, sra 2, subu -> v / 10,
    //                then sll 4 on the quotient and add (v - (v / 10) * 10)
    //   sb a0,0x1(v0)   sb a3,0x2(v0)   sb a1,0x0(v0)
    //   jr ra           with v0 = a1 (the incoming p)
    //
    // Four things the bytes decide that lore would only have guessed at:
    //   * the two-second lead-in IS present, it IS 0x96 = 150 sectors (75 sectors per second), and
    //     it is added BEFORE any division, so it carries into the minute as well as the frame;
    //   * the divisors really are 0x4b (75 sectors per second) and 0x3c (60 seconds per minute);
    //   * the packing is BCD — each field is stored as (v / 10) * 16 + (v % 10). The decompiler
    //     spells that identity as `v + (v / 10) * 6`, which is the form written below;
    //   * p->track at +3 is NEVER written. This routine leaves whatever was already there, and so
    //     does the port. The store order is second (+1), sector (+2), minute (+0).
    //
    // All three divisions are SIGNED: the sequences carry the `sra 31` / `subu` sign correction and
    // use `mult`, not `multu`. C#'s truncating `/` and `%` are therefore the exact match, and the
    // arithmetic stays in 32 bits — the `sb` is the only truncation, reproduced by the byte casts.
    //
    // WHY THE STUB WAS NOT HARMLESS: returning `default` and writing nothing left every CdlLOC at
    // whatever it already held, so every seek computed through this routine collapsed onto one
    // sector. LoadFACE_B @ 0x80052D68 walks a twelve-entry table and calls
    // `CdIntToPos(base + (n - 1) * 2, &cdlFile2.pos)` once per portrait; all twelve reads landed on
    // the same sector, so the portraits would have been WRONG rather than missing. FUN_800583fc
    // @ 0x800583FC (ported in TITLE_EXE/LoadingScreen.cs) does
    // `CdIntToPos(base + DAT_1f80012c * 10, ...)` to pick one of three loading pictures, and its
    // PARTIAL note records that same lost seek.
    public static CdlLOC CdIntToPos(int i, CdlLOC p)
    {
        int iVar1;
        int iVar2;
        int iVar3;

        iVar3 = (i + 0x96) / 0x4b;
        iVar2 = (i + 0x96) % 0x4b;
        iVar1 = iVar3 / 0x3c;
        iVar3 = iVar3 % 0x3c;
        p.second = (byte)(iVar3 + (iVar3 / 10) * 6);
        p.sector = (byte)(iVar2 + (iVar2 / 10) * 6);
        p.minute = (byte)(iVar1 + (iVar1 / 10) * 6);
        return p;
    }

    // GHIDRA: CdPosToInt @ 0x80069938 (TITLE.EXE)
    // CLOSED 2026-08-30: decoded from the 32 instructions at 0x80069938..0x800699B7.
    //   lbu v1,0x0(a0)  lbu a2,0x1(a0)  lbu a1,0x2(a0)   minute, second, sector. track is NOT read.
    //   srl 4 / sll 2 / addu / sll 1 / andi 0xF / addu   BCD -> binary, once per byte
    //   sll 4 / subu / sll 2                             * 0x3c
    //   sll 2 / addu / sll 4 / subu                      * 0x4b
    //   addiu v0,v0,-0x96                                the same 150-sector lead-in, in the delay
    //                                                    slot of the `jr ra`
    // The loads are lbu, so the fields are unsigned; the decompiler's `(uint)` casts say the same,
    // and C#'s `byte` reproduces it directly. Exact inverse of CdIntToPos above; every call site
    // uses the two as a pair.
    //
    // EQUIVALENCE: LibDs.LbaFromPosition (LibDs.cs:283) computes this same value for the desktop
    // read path. It is deliberately NOT called from here — rule 3 forbids folding an original
    // routine into a neighbouring API, and this one has to exist under its own name because the
    // game calls it directly (LoadFACE_B @ 0x80052D68, FUN_800583fc @ 0x800583FC and six others).
    public static int CdPosToInt(CdlLOC p)
    {
        return ((((p.minute >> 4) * 10 + (p.minute & 0xf)) * 0x3c +
                 (p.second >> 4) * 10 + (p.second & 0xf)) * 0x4b +
                (p.sector >> 4) * 10 + (p.sector & 0xf)) + -0x96;
    }

    public static CdlFILE CdSearchFile(CdlFILE fp, char name)
    {
        return CdSearchFile(fp, new[] { name });
    }

    // JUSTIFICATION: C# language bridge only
    // RELATION: array-backed representation of the original null-terminated char* filename.
    public static CdlFILE CdSearchFile(CdlFILE fp, char[] name)
    {
        if (fp == null || name == null)
        {
            return null;
        }

        var dsFile = new LibDs.DslFILE();
        if (LibDs.DsSearchFile(dsFile, name) == null)
        {
            return null;
        }

        fp.pos ??= new CdlLOC();
        fp.pos.minute = dsFile.pos.minute;
        fp.pos.second = dsFile.pos.second;
        fp.pos.sector = dsFile.pos.sector;
        fp.pos.track = dsFile.pos.track;
        fp.size = checked((int)dsFile.size);

        int copyLength = Math.Min(name.Length, fp.name.Length);
        Array.Copy(name, fp.name, copyLength);
        for (int i = copyLength; i < fp.name.Length; i++)
        {
            fp.name[i] = '\0';
        }

        return fp;
    }

    // JUSTIFICATION: PSX hardware adaptation only
    // RELATION: restores the disc-load latency the desktop adapter otherwise skips entirely.
    //
    // Measured on the real game in PCSX-Redux, off the cycle counter, between the call site of
    // ShutdownAndLoadExecutable and the loaded overlay's main:
    //
    //   MOVIE.EXE  133 120 bytes   34 496 533 cycles  =  1018.5 ms
    //   TITLE.EXE  942 080 bytes  123 154 369 cycles  =  3636.2 ms
    //
    // Solving those two gives 309 037 bytes/s over a 587.8 ms fixed cost. That transfer rate is
    // the drive's real 2x speed (2 x 75 sectors/s x 2048 bytes = 307 200 B/s), so the model lands
    // on the hardware specification instead of being fitted to taste.
    //
    // This is not cosmetic. With LoadExec instantaneous, one Start press skipped BOTH startup
    // movies: the next overlay's first pad test ran 66 ms after the previous one's, far inside a
    // human keypress. On console that gap is about 1.6 s. Holding Start throughout was checked on
    // the console and skips both there too, so the transliterated code was already faithful — only
    // this latency was missing.
    private const double DiscBytesPerSecond = 309036.5;
    private const double DiscSeekMilliseconds = 587.8;

    public static void WaitDiscLoad(string isoPath)
    {
        long size = LibDs.DiscFileSize(isoPath);
        if (size < 0)
        {
            return;
        }

        double milliseconds = DiscSeekMilliseconds + (size / DiscBytesPerSecond * 1000.0);
        int frames = (int)Math.Round(milliseconds / (1000.0 / 60.0));
        for (int i = 0; i < frames; i++)
        {
            LibEtc.VSync(0);
        }
    }

    public static int CdRead(int sectors, ulong[] buf, int mode)
    {
        /* Do nothing */
        return default;
    }

    // GHIDRA: CdRead @ 0x800697B4 (TITLE.EXE)
    // JUSTIFICATION: C# language bridge only
    // RELATION: the original takes a u_long* into PSX RAM; ReadCDData @ 0x80057E40 hands it a raw
    // address, so this overload takes the address and writes through PsxRam. Reads from the
    // position the last CdlSetloc/CdlSeekL recorded.
    //
    // The desktop read is synchronous: by the time this returns, the sectors are already in memory
    // and CdReadSync has nothing left to wait for, which is why it reports completion immediately.
    // Returns 1 on success, matching the `while (CdRead(...) != 1)` retry every call site uses.
    public static int CdRead(int sectors, int psxAddress, int mode)
    {
        if (s_lastSeekTarget == null || sectors <= 0)
        {
            return 0;
        }

        int lba = LibDs.LbaFromPosition(
            s_lastSeekTarget.minute,
            s_lastSeekTarget.second,
            s_lastSeekTarget.sector);

        int delivered = LibDs.ReadDataSectors(lba, sectors, psxAddress);
        return delivered == sectors ? 1 : 0;
    }

    public static int CdReadSync(int mode, byte[] result)
    {
        /* Do nothing */
        return default;
    }

    public static CdlCB CdReadCallback(CdlCB func)
    {
        /* Do nothing */
        return default;
    }

    public static int CdRead2(long mode)
    {
        if (s_lastSeekTarget == null)
        {
            return 0;
        }

        var position = new LibDs.DslLOC
        {
            minute = s_lastSeekTarget.minute,
            second = s_lastSeekTarget.second,
            sector = s_lastSeekTarget.sector,
            track = s_lastSeekTarget.track,
        };
        return LibDs.DsRead2(position, checked((int)mode));
    }

    // ---------------------------------------------------------------------------------------
    // St* streaming ring API (slice S1: SDK CD streaming — ring + disc file source).
    //
    // Binary layout (from StGetNext@0x8007C484 / StFreeRing@0x8007C394 disassembly, discovery
    // already done): the ring at ringAddr holds `ringSize` (64 in practice) 32-byte slot HEADERS
    // packed first, followed by `ringSize` 0x7E0=2016-byte PAYLOADS; payload(i) =
    // ringAddr + ringSize*32 + i*0x7E0. A frame occupies secCnt CONTIGUOUS slots. Slot lifecycle
    // is tracked in the slot header's own u16 at offset +0: 0=free, 1=WRAP MARKER (the reader
    // resets its index to 0 on seeing it), 2=filled/ready, 4=handed out. StFreeRing requires
    // status 4 and frees `count` slots, where count is read from the slot header's u16 at +6.
    //
    // Calibration (data/disk-1/FMV1/FMV000.STR, first 40 sectors, measured 2026-08-29): a raw
    // sector is 8-byte XA subheader + 2048 bytes of data + 280 EDC/ECC. For a VIDEO sector, the
    // first 32 bytes of the data area are the STR header, laid out exactly as this ring's slot
    // header: u16 id(non-zero, e.g. 0x0160 in the file; overwritten with the ring's own 0/1/2/4
    // status once ingested — see IngestOneSector), u16 type(==0x8001), u16 secNum(0-based within
    // frame), u16 secCnt AT OFFSET +6 (matches the pinned ring contract exactly — confirmed by
    // this calibration, no divergence), u32 frameNumber, u32 demuxSize, u16 width(320),
    // u16 height(240), 12 bytes reserved/unused by this port. The remaining 2016 bytes are the
    // slot payload. XA AUDIO sectors are interleaved (measured ~1 per 7-9 video sectors) and are
    // distinguished by the XA subheader's submode byte (raw sector offset +2): bit 0x04 (Audio)
    // is set for audio sectors and clear for video sectors in this dump — video sectors instead
    // carry RT|Data (0x48); this is the standard CD-XA submode Audio bit, so both the submode bit
    // and the payload type==0x8001 are checked at ingest (IngestOneSector) and either one failing
    // drops the sector without occupying a ring slot.
    //
    // Desktop ingest is synchronous on demand (no CD-IRQ side ported — see St_CdReadyHandler@
    // 0x8007C564 / data_ready_callback@0x8007C214 in the original): StGetNext pulls raw sectors
    // from the stream source LibDs.CurrentStreamSource (armed by LibDs.DsRead2 in streaming mode)
    // until the next frame's secCnt slots are all filled or the ring has no room left.
    private const int SlotHeaderSize = 32;
    private const int SlotPayloadSize = 0x7E0; // 2016

    private static int s_ringAddr;
    private static int s_ringSize;
    private static int s_writeSlot;

    private static bool s_streamArmed;
    private static int s_streamMode;
    private static int s_streamStartFrame;
    private static int s_streamEndFrame;
    private static Action s_streamFunc1;
    private static Action s_streamFunc2;

    private static int s_maskValue;
    private static int s_maskStart;
    private static int s_maskEnd;

    // Resume-point bookkeeping for StGetBackloc: the absolute raw-sector LBA of the last sector
    // consumed into the last frame actually handed out by StGetNext, and that frame's own
    // frameNumber field.
    private static int s_lastDeliveredAbsSector = -1;
    private static int s_lastDeliveredFrameNumber = -1;

    private static int HeaderAddr(int slot) => s_ringAddr + slot * SlotHeaderSize;
    private static int PayloadAddr(int slot) => s_ringAddr + s_ringSize * SlotHeaderSize + slot * SlotPayloadSize;

    private static int ReadSlotStatus(int slot) => PsxRam.ReadU16(HeaderAddr(slot));
    private static void WriteSlotStatus(int slot, int status) => PsxRam.WriteU16(HeaderAddr(slot), (ushort)status);

    private static int ReadSlotSecCount(int slot)
    {
        byte[] b = PsxRam.ReadBytes(HeaderAddr(slot) + 6, 2);
        return b == null ? 0 : b[0] | (b[1] << 8);
    }

    // GHIDRA: StSetRing @ 0x8007A214
    // PROOF: CERTAIN interface (ring base address + slot count, matches disassembly) — ADAPTED
    // internals: records the ring location and clears every slot, rather than the original's
    // register-store-only body, because this desktop ring is populated by StGetNext's synchronous
    // ingest instead of a CD-IRQ handler filling it in the background.
    public static void StSetRing(int ringAddr, int ringSize)
    {
        s_ringAddr = ringAddr;
        s_ringSize = ringSize;
        StClearRing();
    }

    // GHIDRA: StClearRing @ 0x8007A244
    // PROOF: CERTAIN interface — resets every slot to free (status 0) and rewinds the write
    // cursor, matching the original's ring-wide re-initialisation.
    public static void StClearRing()
    {
        s_writeSlot = 0;
        if (s_ringSize <= 0 || s_ringAddr == 0)
        {
            return;
        }

        for (int i = 0; i < s_ringSize; i++)
        {
            WriteSlotStatus(i, 0);
        }
    }

    // GHIDRA: StSetStream @ 0x8007C304
    // PROOF: CERTAIN interface — arms streaming with the requested mode/frame range and optional
    // callbacks (both null in every call site this port has). endFrame=-1 means "play to end".
    public static void StSetStream(int mode, int startFrame, int endFrame, Action func1, Action func2)
    {
        s_streamMode = mode;
        s_streamStartFrame = startFrame;
        s_streamEndFrame = endFrame;
        s_streamFunc1 = func1;
        s_streamFunc2 = func2;
        s_streamArmed = true;
    }

    // GHIDRA: StSetMask @ 0x8007C544
    // PROOF: CERTAIN interface — store-only, as in the original; nothing in this port's ported
    // call sites reads the mask back yet.
    public static void StSetMask(int mask, int start, int end)
    {
        s_maskValue = mask;
        s_maskStart = start;
        s_maskEnd = end;
    }

    // JUSTIFICATION: PSX hardware adaptation only
    // RELATION: deferred St CD interrupt work is unnecessary because desktop sector ingest runs
    // synchronously inside StGetNext. The game callback retains the original conditional call.
    public static void StCdInterrupt()
    {
    }

    // JUSTIFICATION: desktop adaptation — copies one already-classified VIDEO sector's 32-byte
    // STR header and 2016-byte payload into ring slot `slot`, then overwrites the header's u16
    // status field (offset +0) with the ring's own lifecycle value (2=filled). The file's on-disk
    // id tag at that same offset is intentionally clobbered — the ring's status lifecycle owns
    // that field once a sector is ingested, exactly as StGetNext/StFreeRing's disassembly
    // establishes it does on console.
    private static void IngestOneSector(int slot, byte[] sectorData2048)
    {
        byte[] header = new byte[SlotHeaderSize];
        Array.Copy(sectorData2048, 0, header, 0, SlotHeaderSize);
        PsxRam.WriteBytes(HeaderAddr(slot), header);

        byte[] payload = new byte[SlotPayloadSize];
        Array.Copy(sectorData2048, SlotHeaderSize, payload, 0, SlotPayloadSize);
        PsxRam.WriteBytes(PayloadAddr(slot), payload);

        WriteSlotStatus(slot, 2);
    }

    // GHIDRA: StGetNext @ 0x8007C484
    // PROOF: CERTAIN interface (out addr/header PSX addresses, 0=frame ready/1=not ready, matches
    // disassembly) — ADAPTED internals: on console this only reads ring state the CD-IRQ handler
    // already filled; here it also DRIVES the synchronous ingest (see the ring contract note
    // above the field block), since no IRQ side is ported.
    // S3 (2026-08-29): the S1 "TRANSITIONAL GATING" branch (no-source -> return 0 with
    // addr=header=0) is removed — every caller now arms a real source before polling, so
    // no-source is simply "not ready" (1), like any other empty-ring state.
    public static int StGetNext(out int addr, out int header)
    {
        addr = 0;
        header = 0;

        if (!s_streamArmed || s_ringAddr == 0 || s_ringSize <= 0)
        {
            return 1;
        }

        LibDs.DsStreamSource source = LibDs.CurrentStreamSource;
        if (source == null)
        {
            return 1;
        }

        int frameStartSlot = s_writeSlot;
        int filled = 0;
        int secCnt = -1;
        int frameNumber = -1;
        int lastRawSector = -1;

        while (true)
        {
            if (!source.TryReadNextSector(out byte[] sector, out int absSector))
            {
                // Source exhausted before completing the next frame.
                return 1;
            }

            byte submode = sector[2];
            bool isAudio = (submode & 0x04) != 0;

            if (isAudio)
            {
                // Slice S4: interleaved XA-ADPCM movie audio sectors (submode bit 0x04, including
                // the 0xE4 = Audio+EOF last-sector marker) flow to XaAudio's decoder/resampler/FIFO
                // instead of being dropped — they never occupy a ring slot (video-only ring, as
                // before), so the ring's own contract is unchanged.
                XaAudio.SubmitSector(sector);
                continue;
            }

            byte[] data = new byte[2048];
            Array.Copy(sector, 8, data, 0, 2048);
            ushort type = (ushort)(data[2] | (data[3] << 8));

            if (type != 0x8001)
            {
                // Anything else (padding/non-video, non-audio) is dropped at ingest.
                continue;
            }

            ushort thisSecCnt = (ushort)(data[6] | (data[7] << 8));
            uint thisFrameNumber = (uint)(data[8] | (data[9] << 8) | (data[10] << 16) | (data[11] << 24));

            if (secCnt < 0)
            {
                secCnt = thisSecCnt;
                frameNumber = unchecked((int)thisFrameNumber);

                if (frameStartSlot + secCnt > s_ringSize)
                {
                    // Frame doesn't fit contiguously before the end of the ring: mark the current
                    // write slot as a wrap marker (if there's room to write one) and restart the
                    // frame at slot 0, per the pinned wrap policy.
                    if (frameStartSlot < s_ringSize)
                    {
                        WriteSlotStatus(frameStartSlot, 1);
                    }

                    frameStartSlot = 0;
                }
            }

            int slotIndex = frameStartSlot + filled;
            if (slotIndex >= s_ringSize)
            {
                // Ring is full (no free slot for the next sector of this frame) with no complete
                // frame available yet.
                return 1;
            }

            int existingStatus = ReadSlotStatus(slotIndex);
            if (existingStatus != 0 && existingStatus != 1)
            {
                // Free (0) and wrap-marker (1) slots are both available for the writer to reuse —
                // a marker never holds real payload data, it only tells a reader walking the ring
                // to reset its index to 0. Anything else (2=filled, 4=handed out) means the ring
                // genuinely has no room for this frame yet.
                return 1;
            }

            IngestOneSector(slotIndex, data);
            lastRawSector = absSector;
            filled++;

            if (filled == secCnt)
            {
                break;
            }
        }

        for (int i = 0; i < secCnt; i++)
        {
            WriteSlotStatus(frameStartSlot + i, 4);
        }

        s_writeSlot = frameStartSlot + secCnt;
        if (s_writeSlot >= s_ringSize)
        {
            s_writeSlot = 0;
        }

        addr = PayloadAddr(frameStartSlot);
        header = HeaderAddr(frameStartSlot);

        s_lastDeliveredAbsSector = lastRawSector;
        s_lastDeliveredFrameNumber = frameNumber;

        return 0;
    }

    // GHIDRA: StFreeRing @ 0x8007C394
    // PROOF: CERTAIN interface — maps a payload address back to its slot index, requires status 4
    // (handed out), frees exactly the `count` slots read from the slot header's u16 at +6 (its
    // own secCnt field), and returns 0 on success. Frame slots are always contiguous without
    // wrapping mid-frame (StGetNext's wrap policy only starts a NEW frame at slot 0, never splits
    // one), so no modulo wraparound is needed when freeing.
    public static int StFreeRing(int frameAddr)
    {
        int payloadBase = s_ringAddr + s_ringSize * SlotHeaderSize;
        int rel = frameAddr - payloadBase;
        if (s_ringSize <= 0 || rel < 0 || rel % SlotPayloadSize != 0)
        {
            return 1;
        }

        int slotIndex = rel / SlotPayloadSize;
        if (slotIndex < 0 || slotIndex >= s_ringSize || ReadSlotStatus(slotIndex) != 4)
        {
            return 1;
        }

        int count = ReadSlotSecCount(slotIndex);
        if (count <= 0 || slotIndex + count > s_ringSize)
        {
            return 1;
        }

        for (int i = 0; i < count; i++)
        {
            WriteSlotStatus(slotIndex + i, 0);
        }

        return 0;
    }

    private static byte ToBcd(int v) => (byte)((((v / 10) % 10) << 4) | (v % 10));
    private static int FromBcd(byte b) => ((b >> 4) & 0xF) * 10 + (b & 0xF);

    // GHIDRA: StGetBackloc @ 0x8007C2A0
    // PROOF: CERTAIN interface — fills `loc` with the BCD MSF resume position (one raw sector
    // past the last one consumed into the last frame StGetNext actually handed out) and returns
    // that frame's frame number, matching the pinned CdlLOC convention (LBA = ((min*60)+sec)*75 +
    // frame - 150).
    public static int StGetBackloc(CdlLOC loc)
    {
        if (loc != null && s_lastDeliveredAbsSector >= 0)
        {
            int resumeLba = s_lastDeliveredAbsSector + 1;
            int v = resumeLba + 150;
            int frame = v % 75;
            v /= 75;
            int sec = v % 60;
            int min = v / 60;
            loc.minute = ToBcd(min);
            loc.second = ToBcd(sec);
            loc.sector = ToBcd(frame);
            loc.track = 0;
        }

        return s_lastDeliveredFrameNumber;
    }

    // JUSTIFICATION: no distinct binary function was found for a ring teardown call — StSetRing/
    // StClearRing only (re)arm the ring. The ported game (PeSection70Overlay.cs:1338/:1601) calls
    // StUnSetRing as FMV teardown alongside DsControlB(9,...), so on desktop it owns full
    // teardown: closing the streaming source, clearing the ring, and disarming streaming.
    public static void StUnSetRing()
    {
        LibDs.CurrentStreamSource?.Dispose();
        LibDs.CurrentStreamSource = null;

        StClearRing();
        s_streamArmed = false;
        s_ringAddr = 0;
        s_ringSize = 0;
        s_lastDeliveredAbsSector = -1;
        s_lastDeliveredFrameNumber = -1;

        // Slice S4: teardown also owns clearing XaAudio's FIFO/predictor state, matching every other
        // piece of stream state this method resets — a mid-movie stall-recovery re-seek goes through
        // DsRead2 instead (never this method), so audio continuity across THAT path is preserved.
        XaAudio.Flush();
    }
}