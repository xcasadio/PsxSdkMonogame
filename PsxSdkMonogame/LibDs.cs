using System;
using System.Collections.Generic;
using System.IO;

namespace PsxSdkMonogame;

public static class LibDs
{
    // JUSTIFICATION: desktop adaptation — resolves an ISO disc path (e.g. "\FMV1\FMV000.STR;1",
    // as DsSearchFile receives it) to a host filesystem path, or null when no such file exists on
    // the (virtual) disc. Game-specific — the game installs this at startup (see PsxSdkBridges),
    // deriving the disc root from wherever it keeps the rest of the disc image.
    public static Func<string, string> DiscFileResolver;

    // JUSTIFICATION: desktop adaptation — a raw-sector reader positioned on an already-open disc
    // file, wired up by DsRead2 when it's asked for streaming mode (mode & 0x100) and consumed by
    // LibCd.StGetNext's synchronous ingest. One raw sector is 2336 bytes (8-byte XA subheader +
    // 2048 data + 280 EDC/ECC), matching the RAW dumps this repo's data/disk-*/FMV*/*.STR files
    // are stored as (calibrated against data/disk-1/FMV1/FMV000.STR, 2026-08-29).
    public sealed class DsStreamSource : IDisposable
    {
        private readonly FileStream _stream;
        private int _nextAbsoluteSector;

        public DsStreamSource(FileStream stream, int startAbsoluteSector)
        {
            _stream = stream;
            _nextAbsoluteSector = startAbsoluteSector;
        }

        public bool TryReadNextSector(out byte[] sector, out int absoluteSector)
        {
            sector = new byte[2336];
            absoluteSector = _nextAbsoluteSector;

            int totalRead = 0;
            while (totalRead < 2336)
            {
                int n = _stream.Read(sector, totalRead, 2336 - totalRead);
                if (n <= 0)
                {
                    sector = null;
                    absoluteSector = -1;
                    return false;
                }

                totalRead += n;
            }

            _nextAbsoluteSector++;
            return true;
        }

        public void Dispose() => _stream.Dispose();
    }

    // JUSTIFICATION: desktop adaptation — the stream source most recently armed by DsRead2's
    // streaming mode, read by LibCd.StGetNext and torn down by LibCd.StUnSetRing. There is only
    // ever one active FMV stream at a time in this port (as on console).
    public static DsStreamSource CurrentStreamSource;

    // JUSTIFICATION: desktop adaptation — per-file CD LBA registry backing the pinned CdlLOC
    // convention: each resolved disc file gets a cumulative base LBA on first resolve
    // (first=150, matching the standard CD lead-in; next = previous base + previous file's sector
    // count + 32 guard sectors), so BCD-MSF round-trips and DsRead2 can map a position back to the
    // host file + byte offset that produced it. Keyed case-insensitively on the ISO path exactly
    // as passed to DsSearchFile.
    private sealed class FileRegistration
    {
        public string HostPath;
        public int BaseLba;
        public int SectorCount;
    }

    private static readonly Dictionary<string, FileRegistration> s_registry =
        new(StringComparer.OrdinalIgnoreCase);
    private static int s_nextBaseLba = 150;

    // Max LBA that still fits in BCD MSF (99:59:74 -> ((99*60)+59)*75+74-150 = 449849), per the
    // pinned CdlLOC convention.
    private const int MaxBcdLba = 449849;

    private static byte ToBcd(int v) => (byte)((((v / 10) % 10) << 4) | (v % 10));
    private static int FromBcd(byte b) => ((b >> 4) & 0xF) * 10 + (b & 0xF);

    private static int LbaFromMsf(DslLOC loc) =>
        ((FromBcd(loc.minute) * 60) + FromBcd(loc.second)) * 75 + FromBcd(loc.sector) - 150;

    private static void MsfFromLba(int lba, DslLOC loc)
    {
        int v = lba + 150;
        int frame = v % 75;
        v /= 75;
        int sec = v % 60;
        int min = v / 60;
        loc.minute = ToBcd(min);
        loc.second = ToBcd(sec);
        loc.sector = ToBcd(frame);
        loc.track = 0;
    }

    private static FileRegistration RegisterFile(string isoPath, string hostPath)
    {
        if (s_registry.TryGetValue(isoPath, out FileRegistration existing))
        {
            return existing;
        }

        long lengthBytes = new FileInfo(hostPath).Length;
        int sectorCount = (int)(lengthBytes / 2336);

        int baseLba = s_nextBaseLba;
        int endLba = baseLba + sectorCount - 1;
        if (endLba > MaxBcdLba)
        {
            throw new InvalidOperationException(
                $"LibDs: disc file '{isoPath}' ('{hostPath}') would end at LBA {endLba}, past the " +
                $"max BCD-MSF-representable LBA {MaxBcdLba}. Registered base LBA {baseLba}, " +
                $"{sectorCount} sectors.");
        }

        var reg = new FileRegistration { HostPath = hostPath, BaseLba = baseLba, SectorCount = sectorCount };
        s_registry[isoPath] = reg;
        s_nextBaseLba = baseLba + sectorCount + 32;
        return reg;
    }

    private static FileRegistration FindRegistrationContaining(int lba)
    {
        foreach (FileRegistration reg in s_registry.Values)
        {
            if (lba >= reg.BaseLba && lba < reg.BaseLba + reg.SectorCount)
            {
                return reg;
            }
        }

        return null;
    }

    public delegate void DslCB(byte status, byte[] data);
    public delegate void DslRCB(byte status, byte[] data);

    public class DslATV
    {
        public byte val0;
        public byte val1;
        public byte val2;
        public byte val3;
    }

    public class DslLOC
    {
        public byte minute;
        public byte second;
        public byte sector;
        public byte track;
    }

    public class DslFILE
    {
        public DslLOC pos = new();
        public ulong size;
        public char[] name = new char[16];
    }

    public class DslFILTER
    {
        public byte file;
        public byte chan;
        public ushort pad;
    }


    public static void DsClose()
    {
    }

    public static int DsCommand(byte com, byte param, DslCB cbsync, int count)
    {
        return 0;
    }

    public static char DsComstr(byte com)
    {
        return (char)0;
    }

    // GHIDRA: DsControl (com=2) — no separate address found for this discovery slice; grouped
    // with the rest of the Ds* CD-command surface. ADAPTED internals for com=2 only: stores the
    // seek target from `param.pos` (the FmvStream.cs / PeSection70Overlay.cs call sites pass a
    // DslFILE whose `pos` DsSearchFile already filled with a BCD-MSF position) so it's available
    // alongside the position DsRead2 is given directly. Other com values keep the existing
    // no-op/stub behaviour — unexercised by this slice's call sites.
    private static DslLOC s_lastSeekTarget;

    public static int DsControl(byte com, DslFILE param, DslFILE result)
    {
        if (com == 2 && param != null)
        {
            s_lastSeekTarget = new DslLOC
            {
                minute = param.pos.minute,
                second = param.pos.second,
                sector = param.pos.sector,
                track = param.pos.track,
            };
        }

        return 0;
    }

    // GHIDRA: DsQueue_SubmitCommand @ 0x8007ee84
    // DECOMP: name from parasite-eve-decomp sym.main.txt (PROBABLE, not re-verified here)
    // JUSTIFICATION: PSX hardware adaptation — CD command queue submission.
    // Original allocates a CD command entry in a circular buffer and triggers the drive.
    // On desktop, CD reads are synchronous; returns a dummy command handle (1) so the
    // caller's state machine advances.
    public static int DsQueue_SubmitCommand(byte param_1, int param_2, int param_3, int param_4)
    {
        return 1;
    }

    // GHIDRA: DsQueue_FindEntryById @ 0x8007f418
    // DECOMP: name from parasite-eve-decomp sym.main.txt (PROBABLE, not re-verified here)
    // JUSTIFICATION: PSX hardware adaptation — CD command status check.
    // Original searches the CD command circular buffer for param_1 and returns
    // its status byte (2=complete, 6=not found, 0=not ready).
    // On desktop, CD reads complete synchronously; always return 2 (complete).
    public static byte DsQueue_FindEntryById(int param_1, int param_2)
    {
        return 2;
    }

    // GHIDRA: DSSYS_4_OBJ_E4 @ 0x80080e18
    // CERTAIN: real body is a bare `return;` — a genuine no-op in the original too, reached from
    // DsControlB only when DsQueue_SubmitCommand fails to allocate a command slot (never happens with the
    // always-succeeds adaptation above).
    public static int DSSYS_4_OBJ_E4()
    {
        return 0;
    }

    // GHIDRA: DsControlB @ 0x80080dc4
    // PROOF: CERTAIN — full control flow re-traced 2026-07-24. Submits a CD command (com/param)
    // via DsQueue_SubmitCommand, then polls DsQueue_FindEntryById for its completion status (2=complete) into
    // `result`, retrying while not-yet-complete. Both callees are already-established
    // PSX-hardware-adaptation no-ops above (CD commands complete synchronously on desktop), so
    // this always takes the "already complete" path on the very first poll.
    public static int DsControlB(byte com, byte[] param, byte[] result)
    {
        int commandHandle = DsQueue_SubmitCommand(com, param != null ? 1 : 0, 0, 0);
        if (commandHandle == 0)
        {
            return DSSYS_4_OBJ_E4();
        }

        byte status;
        do
        {
            status = DsQueue_FindEntryById(commandHandle, result != null ? 1 : 0);
        } while (status == 0);

        return status == 2 ? 1 : 0;
    }

    public static int DsControlF(byte com, byte param)
    {
        return 0;
    }

    //void (DsDataCallback(void (* func) ()))() { }

    public static int DsDataSync(int mode)
    {
        return 0;
    }

    public static void DsEndReadySystem()
    {
    }

    public static void DsFlush()
    {
    }

    public static int DsGetDiskType()
    {
        return 0;
    }

    public static int DsGetSector(object madr, int size)
    {
        return 0;
    }

    public static int DsGetSector2(object madr, int size)
    {
        return 0;
    }

    public static int DsGetToc(DslLOC loc)
    {
        return 0;
    }

    public static int DsInit()
    {
        return 0;
    }

    public static char DsInstr(byte intr)
    {
        return (char)0;
    }

    public static DslLOC DsIntToPos(int i, DslLOC p)
    {
        return null;
    }

    public static byte DsLastCom()
    {
        return 0;
    }

    public static DslLOC DsLastPos(DslLOC p)
    {
        return null;
    }

    public static int DsMix(DslATV vol)
    {
        return 0;
    }

    public static int DsPacket(byte mode, DslLOC pos, byte com, DslCB cbsync, int count)
    {
        return 0;
    }

    public static int DsPlay(int mode, int tracks, int offset)
    {
        return 0;
    }

    public static int DsPosToInt(DslLOC p)
    {
        return 0;
    }

    public static int DsQueueLen()
    {
        return 0;
    }

    public static int DsRead(DslLOC pos, int sectors, ulong buf, int mode)
    {
        return 0;
    }

    // GHIDRA: DsRead2 @ 0x80081314
    // PROOF: interface CERTAIN for the streaming path (mode & 0x100) — ADAPTED internals: opens
    // the registered disc file containing `pos`'s absolute LBA, positions it there, and arms it
    // as LibCd's St ring source (LibCd.StGetNext consumes it). mode byte 0xE0 (2x-speed|XA-RT, the
    // console's other DsRead2 mode literal) has no desktop equivalent — XA audio playback during
    // FMV is out of scope for this port — and keeps the original stub's return value (0).
    public static int DsRead2(DslLOC pos, int mode)
    {
        if ((mode & 0x100) == 0)
        {
            return 0;
        }

        int lba = LbaFromMsf(pos);
        FileRegistration reg = FindRegistrationContaining(lba);
        if (reg == null)
        {
            // No registered file covers this position (e.g. DsSearchFile was never actually
            // called with a real path, or the position doesn't resolve) — explicitly clear any
            // previous stream source so StGetNext takes its no-source-armed path rather than
            // silently continuing a stale stream.
            CurrentStreamSource?.Dispose();
            CurrentStreamSource = null;
            return 0;
        }

        FileStream stream;
        try
        {
            stream = new FileStream(reg.HostPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
        catch (IOException)
        {
            CurrentStreamSource?.Dispose();
            CurrentStreamSource = null;
            return 0;
        }

        long byteOffset = (long)(lba - reg.BaseLba) * 2336;
        stream.Seek(byteOffset, SeekOrigin.Begin);

        CurrentStreamSource?.Dispose();
        CurrentStreamSource = new DsStreamSource(stream, lba);
        return 1;
    }

    public static void DsReadBreak()
    {
    }

    public static DslCB DsReadCallback(DslCB func)
    {
        return null;
    }

    //struct EXEC DsReadExec(char file)
    //{
    //}

    public static int DsReadFile(char file, ulong addr, int nbyte)
    {
        return 0;
    }

    public static int DsReadSync(byte[] result)
    {
        return 0;
    }

    public static int DsReady(byte result)
    {
        return 0;
    }

    public static DslCB DsReadyCallback(DslCB func)
    {
        return null;
    }

    public static int DsReadySystemMode(int mode)
    {
        return 0;
    }

    public static int DsReset()
    {
        return 0;
    }

    // ADAPTED internals: resolves `name` (an ISO disc path, e.g. "\FMV1\FMV000.STR;1") through the
    // game-installed DiscFileResolver hook. On a miss, returns null (current stub behaviour,
    // unchanged). On a hit, registers the file in the CD LBA registry (first resolve of a given
    // path reuses its registration) and fills fp.pos with the BCD-MSF of its base LBA, matching
    // the pinned CdlLOC convention.
    public static DslFILE DsSearchFile(DslFILE fp, char[] name)
    {
        if (fp == null || name == null)
        {
            return null;
        }

        int len = 0;
        while (len < name.Length && name[len] != '\0')
        {
            len++;
        }

        string isoPath = new string(name, 0, len);
        string hostPath = DiscFileResolver?.Invoke(isoPath);
        if (hostPath == null)
        {
            return null;
        }

        FileRegistration reg = RegisterFile(isoPath, hostPath);

        fp.pos ??= new DslLOC();
        MsfFromLba(reg.BaseLba, fp.pos);
        fp.size = (ulong)reg.SectorCount * 2048UL;

        int copyLen = Math.Min(len, fp.name.Length);
        Array.Copy(name, fp.name, copyLen);
        for (int i = copyLen; i < fp.name.Length; i++)
        {
            fp.name[i] = '\0';
        }

        return fp;
    }

    public static int DsSetDebug(int level)
    {
        return 0;
    }

    public static int DsShellOpen()
    {
        return 0;
    }

    public static int DsStartReadySystem(DslRCB func, int count)
    {
        return 0;
    }

    public static byte DsStatus()
    {
        return 0;
    }

    public static int DsSync(int id, byte result)
    {
        return 0;
    }

    public static DslCB DsSyncCallback(DslCB func)
    {
        return null;
    }

    public static int DsSystemStatus()
    {
        return 0;
    }
}