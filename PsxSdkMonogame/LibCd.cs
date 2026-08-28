using System;

namespace PsxSdkMonogame;

public static class LibCd
{
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

    public static int CdSync(int mode, byte[] result)
    {
        /* Do nothing */
        return default;
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
        /* Do nothing */
        return default;
    }

    public static int CdControlB(byte com, byte[] param, byte[] result)
    {
        /* Do nothing */
        return default;
    }

    public static int CdControlF(byte com, byte[] param)
    {
        /* Do nothing */
        return default;
    }

    public static int CdMix(CdlATV vol)
    {
        /* Do nothing */
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

    public static CdlLOC CdIntToPos(int i, CdlLOC p)
    {
        /* Do nothing */
        return default;
    }

    public static int CdPosToInt(CdlLOC p)
    {
        /* Do nothing */
        return default;
    }

    public static CdlFILE CdSearchFile(CdlFILE fp, char name)
    {
        /* Do nothing */
        return default;
    }

    public static int CdRead(int sectors, ulong[] buf, int mode)
    {
        /* Do nothing */
        return default;
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
        /* Do nothing */
        return default;
    }

    public static void StClearRing()
    {
        /* Do nothing */
    }

    public static void StSetStream(ulong mode, ulong start_frame, ulong end_frame, Action func1, Action func2)
    {
        /* Do nothing */
    }

    public static void StSetMask(ulong mask, ulong start, ulong end)
    {
        /* Do nothing */
    }

    public static ulong StGetNext(ulong[][] addr, ulong[][] header)
    {
        /* Do nothing */
        return default;
    }

    public static ulong StFreeRing(ulong[] @base)
    {
        /* Do nothing */
        return default;
    }

    public static int StGetBackloc(CdlLOC loc)
    {
        /* Do nothing */
        return default;
    }

    public static void StSetRing(ulong[] ring_addr, ulong ring_size1)
    {
        /* Do nothing */
    }

    public static void StUnSetRing()
    {
        /* Do nothing */
    }

}