namespace PsxSdkMonogame;

public static class LibDs
{
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

    public static int DsControl(byte com, DslFILE param, DslFILE result)
    {
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

    public static int DsRead2(DslLOC pos, int mode)
    {
        return 0;
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

    public static DslFILE DsSearchFile(DslFILE fp, char[] name)
    {
        return null;
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