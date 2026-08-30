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

    // =======================================================================================
    // CdReady / CD_ready — the drive's response-FIFO poll
    // =======================================================================================
    //
    // CLOSED 2026-08-30 from /SELECT.EXE. CdReady @ 0x80044440 is 32 bytes and does nothing but
    // `return CD_ready(mode, result);`. Its one caller in the image is FUN_80025788 @ 0x800257AC,
    // the per-frame CD-DA service, as `CdReady(1, &DAT_80055AD4)`.
    //
    // WHAT IT REPORTS. CD_ready polls the two interrupt codes libcd's CD interrupt handler keeps
    // and, when one is pending, copies that interrupt's eight-byte response block into the caller's
    // buffer and returns the code. The codes are the libcd CdlIntr set — 0 CdlNoIntr,
    // 1 CdlDataReady, 2 CdlComplete, 3 CdlAcknowledge, 4 CdlDataEnd, 5 CdlDiskError. FUN_80025788's
    // `DAT_80055aac == 1` test is therefore "a CdlDataReady arrived": the once-per-second report
    // packet the drive emits while playing CD-DA with CdlModeRept set, whose result[1] is the BCD
    // track number. FUN_800258f0 @ 0x800258F0 is what turns that on — CdlSetmode with the mode byte
    // at DAT_80055AD0 = 5 = CdlModeDA | CdlModeRept.
    //
    // THE RETURN CONTRACT IS DECODED FROM THE BYTES at 0x800454F0..0x80045597, read out of the
    // image with read-memory, not from the decompiler — Ghidra renders all four exits as calls to
    // the epilogue fragments it names BIOS_OBJ_900 / BIOS_OBJ_A58 / BIOS_OBJ_A68, which hides the
    // value entirely. The four instructions that actually set v0 are:
    //     0x8004553C  00 C0 10 21  addu v0,a2,zero   delay slot of `j 0x80045598`, taken after the
    //                                                sync-result copy: returns DAT_800506E2
    //     0x8004558C  00 C0 10 21  addu v0,a2,zero   the same, after the ready-result copy:
    //                                                returns DAT_800506E1
    //     0x80045594  00 00 10 21  addu v0,zero,zero delay slot of `beq s7,zero,0x8004536C`
    //                                                (s7 = mode): mode != 0 returns 0
    //     0x80045428  24 02 FF FF  addiu v0,zero,-1  delay slot of the timeout's jump: returns -1
    // In both copy paths a2 holds the code read BEFORE the `sb zero` that clears it (0x800454F8 and
    // 0x80045548 respectively), so what comes back is the pending code, not the cleared one. The
    // copy is also skipped, but the return value kept, when the caller passes a NULL buffer — both
    // `beq` instructions branch to the same `addu v0,a2,zero`.
    //
    // PARTIAL, AND THIS IS THE WHOLE ANSWER TO "DOES THE MUSIC LOOP NOW": IT DOES NOT.
    // DAT_800506E1 and DAT_800506E2 are written only by libcd's CD interrupt handler, out of the
    // drive's response FIFO. This port models neither — CD_cw above says so in its own note, and
    // leaves the result buffer untouched for exactly that reason. Both bytes therefore stay at the
    // link-time 0 that read-memory reports at 0x800506E0 (eight zero bytes), so CdReady(1, ...)
    // falls straight through to `if (param_1 != 0) return 0;` on its first loop iteration and
    // answers CdlNoIntr for ever. DAT_80055AAC stays 0, FUN_80025788's `== 1` still fails, and the
    // track-advance branch is still dead. What changed is only that the 0 is now produced by the
    // transliterated control flow instead of asserted by a stub.
    //
    // WHAT WOULD CLOSE IT: a desktop CD-DA playback path that knows which track is playing, plus a
    // response-FIFO model in CD_cw so a CdlModeRept report packet can reach DAT_80056874. Neither
    // exists in this port — CD_cw discards CdlPlay (0x03) outright, and nothing reads data/tracks.

    // GHIDRA: DAT_800506E1 @ 0x800506E1 (SELECT.EXE)
    // The pending "ready" interrupt code. Link-time 0 — read-memory over 0x800506E0 returns eight
    // zero bytes. Only libcd's CD interrupt handler writes it; this port has no interrupt path.
    private static byte DAT_800506e1 = 0;

    // GHIDRA: DAT_800506E2 @ 0x800506E2 (SELECT.EXE)
    // The pending "sync" interrupt code. Same writer, same link-time 0.
    private static byte DAT_800506e2 = 0;

    // GHIDRA: DAT_80056874 @ 0x80056874 (SELECT.EXE)
    // The eight-byte response block belonging to the ready interrupt. .bss — read-memory refuses
    // the address, which is what places it past the end of the loaded image — so it is zero after
    // start's .bss clear, and nothing in this port ever writes it.
    private static readonly byte[] DAT_80056874 = new byte[8];

    // GHIDRA: DAT_8005687C @ 0x8005687C (SELECT.EXE)
    // The same, for the sync interrupt. Also .bss, also never written here.
    private static readonly byte[] DAT_8005687c = new byte[8];

    // GHIDRA: DAT_80056884 @ 0x80056884 (SELECT.EXE)
    // .bss. The V-BLANK count CD_ready gives up at: VSync(-1) + 0x1E0, i.e. 480 fields = 8 seconds.
    private static int DAT_80056884;

    // GHIDRA: DAT_80056888 @ 0x80056888 (SELECT.EXE)
    // .bss. The other half of the same watchdog — a spin counter that trips above 0x1E0000. It is
    // what actually ends the mode == 0 wait in this port, because LibEtc.VSync(-1) only reads the
    // V-BLANK counter and nothing inside this loop yields a frame, so the count never advances.
    private static int DAT_80056888;

    // GHIDRA: DAT_8005688C @ 0x8005688C (SELECT.EXE)
    // .bss. The `char *` naming whichever libcd wait routine is spinning, printed by the timeout
    // diagnostic. Held as a C# string rather than a pointer into the image's literal pool.
    //
    // The compiler flags it CS0414, "assigned but never used", and the flag is true of THIS PORT
    // rather than of the original: its one reader is the second printf of CD_ready's timeout
    // branch, which is left unemitted because its other three operands come from libcd name tables
    // this port does not carry (see the note at that line). The store is kept because the original
    // makes it, so the warning is suppressed for this one field and the build stays as it was.
#pragma warning disable CS0414
    private static string DAT_8005688c;
#pragma warning restore CS0414

    // GHIDRA: CD_ready @ 0x800452F8 (SELECT.EXE)
    // PARTIAL: control flow and return contract closed (see the block above); the interrupt half
    // has no desktop counterpart and is marked where it stood.
    private static int CD_ready(int param_1, byte[] param_2)
    {
        bool bVar3;
        int iVar4;
        uint uVar6;

        iVar4 = LibEtc.VSync(-1);
        DAT_80056884 = iVar4 + 0x1e0;
        DAT_80056888 = 0;
        DAT_8005688c = "CD_ready";
        while (true)
        {
            iVar4 = LibEtc.VSync(-1);

            // The original writes the watchdog as one `||` whose right operand is a comma
            // expression: `(DAT_80056884 < iVar4) || (iVar4 = DAT_80056888 + 1,
            // bVar3 = 0x1e0000 < DAT_80056888, DAT_80056888 = iVar4, bVar3)`. Spelled out here
            // because C# has no comma operator in that position; both properties are kept — the
            // spin counter is incremented ONLY when the V-BLANK test fails, and the comparison
            // reads the value from before that increment.
            bVar3 = DAT_80056884 < iVar4;
            if (!bVar3)
            {
                iVar4 = DAT_80056888 + 1;
                bVar3 = 0x1e0000 < DAT_80056888;
                DAT_80056888 = iVar4;
            }

            if (bVar3)
            {
                Console.WriteLine("CD timeout: ");

                // PARTIAL: the diagnostic's second line is
                //   printf("%s:(%s) Sync=%s, Ready=%s\n", DAT_8005688c,
                //          (&PTR_s_CdlSync_80050428)[DAT_80050425],
                //          (&PTR_s_NoIntr_800504A8)[DAT_800506E0],
                //          (&PTR_s_NoIntr_800504A8)[DAT_800506E1]);
                // Its three indexed operands come out of libcd's command-name and interrupt-name
                // tables at 0x80050428 and 0x800504A8 — the same tables CdComstr and CdIntstr above
                // read, and both of those are still stubs. Left unemitted rather than replaced by a
                // differently shaped diagnostic.

                // BLOCKED: CD_flush @ 0x80045BC8 stands here. It resets the drive's command queue
                // and its pending interrupts; there is no drive and no queue in this port, and it
                // is a different routine from the public CdFlush stub above.

                return -1;
            }

            if (LibEtc.CheckCallback() != 0)
            {
                // BLOCKED: the interrupt drain — `bVar1 = *PTR_CDROM_REG0_800506C8;`, then a
                // `while ((uVar6 = getintr()) != 0)` loop dispatching DAT_80050408 (the
                // CdReadyCallback hook) with DAT_800506E1/&DAT_80056874 and DAT_80050404 (the
                // CdSyncCallback hook) with DAT_800506E0/&DAT_8005686C, then
                // `*PTR_CDROM_REG0_800506C8 = bVar1 & 3;`. All of it is the CD controller's own
                // register interface, which this port does not model. Unreachable here in any
                // case: LibEtc.CheckCallback is itself a "Do nothing" stub returning 0.
            }

            if (DAT_800506e2 != 0)
            {
                break;
            }

            if (DAT_800506e1 != 0)
            {
                uVar6 = DAT_800506e1;
                DAT_800506e1 = 0;
                if (param_2 != null)
                {
                    // The original copies eight bytes unconditionally. C# cannot reproduce the
                    // overrun a shorter buffer would cause on console; every call site in this port
                    // passes the eight-byte libcd result block, so the copy is written as it is.
                    for (iVar4 = 0; iVar4 < 8; iVar4++)
                    {
                        param_2[iVar4] = DAT_80056874[iVar4];
                    }
                }

                return (int)uVar6;
            }

            if (param_1 != 0)
            {
                return 0;
            }
        }

        uVar6 = DAT_800506e2;
        DAT_800506e2 = 0;
        if (param_2 != null)
        {
            for (iVar4 = 0; iVar4 < 8; iVar4++)
            {
                param_2[iVar4] = DAT_8005687c[iVar4];
            }
        }

        return (int)uVar6;
    }

    // GHIDRA: CdReady @ 0x80044440 (SELECT.EXE)
    // The whole body: `iVar1 = CD_ready(); return iVar1;`. Ghidra loses the two arguments because
    // the call is a plain `jal` with a0/a1 already in place; the prototype it carries,
    // `int CdReady(int mode, u_char *result)`, and CD_ready's own use of s7 (mode) and s4 (result)
    // put them back.
    public static int CdReady(int mode, byte[] result)
    {
        int iVar1;

        iVar1 = CD_ready(mode, result);
        return iVar1;
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

    // =======================================================================================
    // CdControl / CdControlB / CdControlF — the libcd command layer
    // =======================================================================================
    //
    // CLOSED 2026-08-30 from /SELECT.EXE, which links the real Sony libcd. Before this tranche
    // the three routines were command-by-command guesses: CdControl answered only 0x02/0x15,
    // CdControlB answered only 0x09, CdControlF answered nothing, and every other command
    // returned 0. SELECT.EXE retries commands in `do { r = ...; } while (r == 0);` loops on
    // main's first-frame path, so "return 0" was an infinite loop:
    //     FUN_80030908 @ 0x80030908 line 21   do { CdControlB(0x0E, {0x80}, 0); } while (r == 0)
    //     FUN_800258F0 @ 0x800258F0 line 19   do { CdControlB(0x0A, 0, result); } while (r == 0)
    //     FUN_800258F0 @ 0x800258F0 line 22   do { CdControlB(0x0E, mode, result); } while (r == 0)
    //     FUN_80025894 @ 0x80025894 lines 8/11 the same two loops for 0x0A and 0x08
    //     FUN_80025788 @ 0x80025788 line 21   do { CdControl(0x03, &toc[n], r); } while (r == 0)
    // The bodies below are transliterated instead, so the loops terminate because the routine
    // says so — not because a command was special-cased.
    //
    // The three share one shape: up to four attempts, an auto-Setloc for the commands that carry
    // a disc position, and a disc-lid recovery branch for command 0x10.

    // GHIDRA: DAT_80050384 @ 0x80050384 (SELECT.EXE)
    // One int per command index: non-zero means "this command carries a disc position, and the
    // wrapper must issue CdlSetloc (command 2) with it before issuing the command itself".
    // Read out of the image with read-memory (128 bytes at 0x80050384). The only non-zero
    // entries are 0x03, 0x06, 0x15, 0x16 and 0x1B. This is what makes FUN_80025788's
    // CdControl(0x03, &TOC[track], ...) actually seek: the position reaches the drive through
    // the implied Setloc, not through the Play command.
    private static readonly int[] DAT_80050384 =
    {
        0, 0, 0, 1, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0,
    };

    // GHIDRA: DAT_80050648 @ 0x80050648 (SELECT.EXE)
    // One int per command index: how many parameter bytes the command takes. Read out of the
    // image the same way. Non-zero entries: 0x02 -> 3, 0x0D -> 2, 0x0E -> 1, 0x12 -> 1,
    // 0x14 -> 1. CD_cw rejects a command with a non-zero count and a NULL param.
    private static readonly int[] DAT_80050648 =
    {
        0, 0, 3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 1, 0,
        0, 0, 1, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
    };

    // JUSTIFICATION: C# language bridge only
    // RELATION: the original indexes both tables with the raw command byte and would read past
    // them for a command above 0x1F. No caller issues one; this bounds the read instead of
    // reproducing an out-of-bounds access that C# cannot express.
    private static int CommandTableEntry(int[] table, uint com) => com < table.Length ? table[com] : 0;

    // GHIDRA: DAT_80050404 @ 0x80050404 (SELECT.EXE)
    // The CdSyncCallback hook. All three wrappers save it, clear it while a non-Nop command is
    // in flight and restore it afterwards; the assignments are reproduced literally below.
    // PARTIAL: nothing consumes it in this port — the desktop CD_cw has no interrupt path, and
    // CdSyncCallback above is still a stub — so it is written and never read.
    private static CdlCB DAT_80050404;

    // GHIDRA: DAT_80050414 @ 0x80050414 (SELECT.EXE)
    // The drive status byte the libcd interrupt path maintains. Link-time .data value, read with
    // read-memory: 0. It has no desktop source and stays 0, which makes the command-0x10 branch
    // below reachable in principle; no call site in SELECT.EXE or in this port issues command
    // 0x10, so it never runs.
    private static byte DAT_80050414 = 0;

    // GHIDRA: DAT_80050424 @ 0x80050424 (SELECT.EXE)
    // The mode byte cached by CD_cw after a completed Setmode (CD_cw line 79-81).
    private static byte DAT_80050424;

    // GHIDRA: DAT_8005041C @ 0x8005041C (SELECT.EXE)
    // Media-change generation counter, maintained by the libcd interrupt path. Link-time .data
    // value, read with read-memory: 0.
    private static int DAT_8005041c = 0;

    // GHIDRA: DAT_8005070C @ 0x8005070C (SELECT.EXE)
    // The generation CD_shell last handled. Link-time .data value: 1.
    private static int DAT_8005070c = 1;

    // GHIDRA: DAT_80050710 @ 0x80050710 (SELECT.EXE)
    // The parameter block CD_shell hands to its CD_cw(0x16, ...) retry. Link-time .data bytes,
    // read with read-memory: 02 00 00 00.
    private static readonly byte[] DAT_80050710 = { 0x02, 0x00, 0x00, 0x00 };

    // GHIDRA: CD_shell @ 0x80045AB8 (SELECT.EXE)
    // The disc-lid open/close recovery every non-Nop command runs first. Transliterated whole,
    // because it costs nothing and it is the guard — not this port — that decides the body never
    // runs: `DAT_8005070C < DAT_8005041C` is 1 < 0 with the link-time values above, and neither
    // counter has a desktop source to change it. Even if the guard did open, both loops exit on
    // the first test here: the lid bit of DAT_80050414 is clear, and the desktop CD_cw returns 0.
    private static void CD_shell()
    {
        bool bVar1;
        CdlCB uVar2;
        int iVar3;
        sbyte cVar4;

        uVar2 = DAT_80050404;
        cVar4 = 0;
        if (DAT_8005070c < DAT_8005041c)
        {
            DAT_80050404 = null;
            while ((DAT_80050414 & 0x10) != 0)
            {
                bVar1 = cVar4 == 0;
                cVar4 = (sbyte)(cVar4 + 1);
                if (bVar1)
                {
                    Console.WriteLine("CD opening...");
                }

                CD_cw(1, null, null, 0);
            }

            while ((iVar3 = CD_cw(0x16, DAT_80050710, null, 0)) != 0)
            {
                CD_cw(1, null, null, 0);
                Console.WriteLine("CD closing...");
            }

            DAT_8005070c = DAT_8005041c;
        }

        DAT_80050404 = uVar2;
    }

    // GHIDRA: CD_cw @ 0x800455C8 (SELECT.EXE)
    // PARTIAL: control flow and return contract closed; the hardware half adapted.
    //
    // What the original does, line by line, and what survives here:
    //   line 15  if (DAT_80050648[com] != 0 && param == NULL) -> -2, the "no param" rejection
    //   line 19  if (com == 2) copy 4 bytes of param into DAT_80050420      -> the seek latch
    //   line 30+ write CDROM_REG0/REG1/REG2 and, when param_4 == 0, spin on the drive interrupt
    //   line 79  if (status == 2 && com == 0x0E) DAT_80050424 = param[0]    -> the mode cache
    //   line 83  if (result != NULL) copy 8 result bytes out of DAT_8005686C
    //   line 93  return -(DAT_800506E0 == 5), i.e. 0 unless the drive reported CdlDiskError
    //   line 43  when param_4 != 0 (the CdControlF path) it never waits and returns 0
    // There is no drive to report CdlDiskError on desktop and no interrupt to wait for, so this
    // returns 0 (accepted) for every command it does not reject on the parameter gate. The
    // result buffer is left alone: the eight bytes it would copy come from the drive's own
    // response FIFO, which this port does not model.
    private static int CD_cw(byte com, byte[] param, byte[] result, int param_4)
    {
        if (CommandTableEntry(DAT_80050648, com) != 0 && param == null)
        {
            return -2;
        }

        if (com == 2 && param.Length >= 3)
        {
            s_lastSeekTarget = new CdlLOC
            {
                minute = param[0],
                second = param[1],
                sector = param[2],
                track = param.Length > 3 ? param[3] : (byte)0,
            };
        }

        if (com == 0x0E && param.Length >= 1)
        {
            DAT_80050424 = param[0];
        }

        if (com == 9)
        {
            // JUSTIFICATION: PSX hardware adaptation only — desktop effect of command 0x09.
            // Previously the whole of CdControlB; kept here because the command means the same
            // thing whichever wrapper issues it. Call sites: MOVIE_EXE/SLPS_003_55 FMV teardown.
            LibDs.CurrentStreamSource?.Dispose();
            LibDs.CurrentStreamSource = null;
        }

        return 0;
    }

    // GHIDRA: CdControl @ 0x800444A8 (SELECT.EXE)
    // Returns 1 as soon as CD_cw accepts the command, 0 after four failed attempts.
    // The command-0x10 branch is transliterated from the bytes rather than from the decompiler:
    // Ghidra renders it as `CD_shell(); return SYS_OBJ_2BC();`, but 0x80044524-0x80044530 read
    //     0C 01 16 AE   jal 0x80045AB8      (CD_shell)
    //     26 10 FF FF   addiu s0, s0, -1    (delay slot: the retry counter)
    //     08 01 11 6A   j 0x800445A8        (the loop test `bne s0, v0`)
    //     24 02 FF FF   addiu v0, zero, -1  (delay slot: the loop-test constant)
    // so it consumes one attempt and goes round again. CdControlB has the identical bytes at
    // 0x800447A0-0x800447AC.
    public static int CdControl(byte com, byte[] param, byte[] result)
    {
        uint uVar1;
        CdlCB uVar2;
        CdlCB uVar3;
        int iVar4;
        int iVar5;

        uVar2 = DAT_80050404;
        iVar5 = 3;
        uVar1 = com;
        do
        {
            if ((uVar1 == 0x10) && ((DAT_80050414 & 0x20) == 0))
            {
                CD_shell();
            }
            else
            {
                uVar3 = DAT_80050404;
                bool issue = uVar1 == 1;
                if (!issue)
                {
                    DAT_80050404 = null;
                    CD_shell();
                    uVar3 = uVar2;
                    issue = (param == null) || (CommandTableEntry(DAT_80050384, uVar1) == 0);
                    if (!issue)
                    {
                        iVar4 = CD_cw(2, param, result, 0);
                        issue = iVar4 == 0;
                    }
                }

                if (issue)
                {
                    DAT_80050404 = uVar3;
                    iVar4 = CD_cw(com, param, result, 0);
                    if (iVar4 == 0)
                    {
                        return 1;
                    }
                }
            }

            iVar5 = iVar5 + -1;
            if (iVar5 == -1)
            {
                DAT_80050404 = uVar2;
                return 0;
            }
        } while (true);
    }

    // JUSTIFICATION: C# language bridge only
    // RELATION: typed form of CdControl(com, CdlLOC*, result) — the original takes a u_char*
    // that the caller happens to point at a CdlLOC.
    public static int CdControl(byte com, CdlLOC param, byte[] result)
    {
        return CdControl(com, CdlLocToBytes(param), result);
    }

    // JUSTIFICATION: C# language bridge only
    // RELATION: the original passes the CdlLOC's four bytes straight through as the command's
    // parameter block; C# needs the conversion spelled out.
    private static byte[] CdlLocToBytes(CdlLOC loc)
    {
        return loc == null
            ? null
            : new[] { loc.minute, loc.second, loc.sector, loc.track };
    }

    // GHIDRA: CdControlB @ 0x8004472C (SELECT.EXE)
    // Same body as CdControl up to the exit, then the blocking tail. The tail is decoded from
    // 0x80044834-0x80044850, which the decompiler renders as `CD_sync(0, result); return
    // SYS_OBJ_568();` — SYS_OBJ_568 @ 0x80044854 is only the register-restore epilogue, so the
    // return value is whatever v0 holds at the jump into it, and the four instructions that set
    // it are:
    //     14 40 00 06   bne v0, zero, 0x80044850   (v0 = the accepted/failed flag)
    //     00 00 20 21   addu a0, zero, zero        (delay slot: CD_sync's mode = 0)
    //     0C 01 14 1D   jal 0x80045074             (CD_sync)
    //     02 60 28 21   addu a1, s3, zero          (delay slot: result)
    //     38 42 00 02   xori v0, v0, 0x2
    //     08 01 12 15   j 0x80044854
    //     2C 42 00 01   sltiu v0, v0, 1            (delay slot)
    //     00 00 10 21   addu v0, zero, zero        (0x80044850: the failure path returns 0)
    // i.e. `return CD_sync(0, result) == 2 ? 1 : 0;` — 2 being CdlComplete. THAT is the whole
    // reason the retry loops terminate; nothing here is keyed to a command number.
    // PARTIAL: the original calls the library-internal CD_sync @ 0x80045074; this port has only
    // the public CdSync, whose desktop body gives the same answer for the same reason (the
    // command has already completed by the time it is asked — see CdlComplete above).
    public static int CdControlB(byte com, byte[] param, byte[] result)
    {
        uint uVar1;
        CdlCB uVar2;
        CdlCB uVar3;
        int iVar4;
        int iVar5;
        int iVar6;

        uVar2 = DAT_80050404;
        iVar6 = 3;
        uVar1 = com;

        // PARTIAL: uVar3 and iVar5 are live registers on entry to the loop's exit path when the
        // very first iteration takes the command-0x10 branch and the retry counter runs out.
        // The original reads whatever those registers happened to hold; C# requires them to be
        // assigned, so they start at the saved callback and at the "not accepted" flag, which is
        // what every other path through the loop leaves behind.
        uVar3 = uVar2;
        iVar5 = -1;
        do
        {
            if ((uVar1 == 0x10) && ((DAT_80050414 & 0x20) == 0))
            {
                CD_shell();
            }
            else
            {
                uVar3 = DAT_80050404;
                bool issue = uVar1 == 1;
                if (!issue)
                {
                    DAT_80050404 = null;
                    CD_shell();
                    uVar3 = uVar2;
                    issue = (param == null) || (CommandTableEntry(DAT_80050384, uVar1) == 0);
                    if (!issue)
                    {
                        iVar4 = CD_cw(2, param, result, 0);
                        issue = iVar4 == 0;
                    }
                }

                if (issue)
                {
                    DAT_80050404 = uVar3;
                    iVar4 = CD_cw(com, param, result, 0);
                    iVar5 = 0;
                    uVar3 = DAT_80050404;
                    if (iVar4 == 0)
                    {
                        break;
                    }
                }
            }

            iVar6 = iVar6 + -1;
            iVar5 = -1;
            uVar3 = uVar2;
        } while (iVar6 != -1);

        DAT_80050404 = uVar3;
        if (iVar5 != 0)
        {
            return 0;
        }

        return CdSync(0, result) == CdlComplete ? 1 : 0;
    }

    // NOTE: there is deliberately no CdControlB(byte, CdlLOC, byte[]) overload to match
    // CdControl's. FUN_800258F0 @ 0x800258F0 passes &DAT_80055CEC + track * 4 — a u_char* into
    // the TOC table CdGetToc filled — and a port renders that as a byte[] slice, not as a CdlLOC
    // object. Adding the typed overload also made every existing `CdControlB(9, null, null)`
    // call site ambiguous; CdlLocToBytes above is the conversion when one is needed.

    // GHIDRA: CdControlF @ 0x800445F0 (SELECT.EXE)
    // The fire-and-forget form: identical to CdControl except that it passes result = NULL and
    // param_4 = 1 to CD_cw for the command itself (line 24, `CD_cw(com, param, 0, 1)`) while the
    // implied Setloc still goes through with param_4 = 0 (line 34, `CD_cw(2, param, 0, 0)`).
    // callerCount is 0 in SELECT.EXE, so nothing here exercises it; it is transliterated because
    // it had the same "return 0 for everything" gap as its two siblings.
    public static int CdControlF(byte com, byte[] param)
    {
        uint uVar1;
        CdlCB uVar2;
        CdlCB uVar3;
        int iVar4;
        int iVar5;

        uVar2 = DAT_80050404;
        iVar5 = 3;
        uVar1 = com;
        do
        {
            if ((uVar1 == 0x10) && ((DAT_80050414 & 0x20) == 0))
            {
                CD_shell();
            }
            else
            {
                uVar3 = DAT_80050404;
                bool issue = uVar1 == 1;
                if (!issue)
                {
                    DAT_80050404 = null;
                    CD_shell();
                    uVar3 = uVar2;
                    issue = (param == null) || (CommandTableEntry(DAT_80050384, uVar1) == 0);
                    if (!issue)
                    {
                        iVar4 = CD_cw(2, param, null, 0);
                        issue = iVar4 == 0;
                    }
                }

                if (issue)
                {
                    DAT_80050404 = uVar3;
                    iVar4 = CD_cw(com, param, null, 1);
                    if (iVar4 == 0)
                    {
                        return 1;
                    }
                }
            }

            iVar5 = iVar5 + -1;
            if (iVar5 == -1)
            {
                DAT_80050404 = uVar2;
                return 0;
            }
        } while (true);
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

    // =======================================================================================
    // CdGetToc / CdGetToc2 — the disc's table of contents
    // =======================================================================================
    //
    // CLOSED 2026-08-30 from /SELECT.EXE. CdGetToc @ 0x80047808 is 36 bytes and does nothing but
    // `return CdGetToc2(1, loc);`. Its one caller in the image is FUN_80025658 @ 0x800256B4, the
    // CD-DA bring-up, as `CdGetToc((CdlLOC *)&DAT_80055CEC)` — the 32-entry TOC array.
    //
    // WHAT CdGetToc2 @ 0x8004782C DOES, in order:
    //   * saves and clears the CdSyncCallback hook, so no callback fires mid-enumeration;
    //   * CdControlB(0x13 = CdlGetTN) -> result[1] is the first track number, result[2] the last,
    //     both packed BCD; the routine decodes them into uVar6/uVar7 straight away;
    //   * CdControlB(0x14 = CdlGetTD) with parameter 0 -> the LEAD-OUT position, stored as entry 0;
    //   * CdControlB(0x14) once per track from first to last, each answer stored as the next entry.
    // Every entry is written as minute = result[1], second = result[2], sector = 0, and the fourth
    // byte — CdlLOC.track — is NEVER written, exactly as CdIntToPos above leaves it alone.
    //
    // THE RETURN VALUE is decoded from 0x800479F0: `08 01 1E 8A  j 0x80047A28` with
    // `02 20 10 21  addu v0,s1,zero` in the delay slot, s1 being the counter the decompiler calls
    // iVar3. It starts at 1 for the lead-out entry and is incremented once per CdlGetTD attempt,
    // INCLUDING the attempt that fails and jumps to the error tail — which discards it and returns
    // 0 instead. So the success value is 1 + (last - first + 1). FUN_80025658 keeps it as
    // `DAT_80055AB0 = CdGetToc(...) - 1`.
    //
    // BLOCKED — THE TOC IS NOT RECOVERABLE IN THIS PORT, AND NOTHING BELOW INVENTS ONE.
    // Every byte this routine stores comes from the drive's response FIFO, which CD_cw above does
    // not model: it accepts commands 0x13 and 0x14 and returns success, but leaves the result
    // buffer alone. So the transliterated body below runs to completion and produces
    // first = last = 0, one loop iteration, a return of 2, and a TOC whose entries are all
    // 00:00:00. The control flow is the original's; the data is absent, and is reported absent.
    //
    // The disc's real TOC cannot be derived from what this port has either:
    //   * the port reads extracted files through LibDs.DiscFileResolver and assigns them synthetic
    //     base LBAs in LibDs.RegisterFile (first = 150, then previous + sectors + 32 guard). That
    //     registry is a read-addressing scheme, not the disc layout;
    //   * data/tracks holds the CD-DA rip as WAV, numbered [2]..[20] — durations, not positions.
    //     Turning those into MSF track starts needs the length of data track 1, which is the ISO's
    //     whole data area and is not present here (data/ holds the extracted files);
    //   * no .cue, .toc or .ccd exists anywhere in the repository.
    // Smallest next step: a real disc image or its cue sheet, at which point the TOC becomes a
    // read rather than a guess.
    //
    // HAZARD WORTH RECORDING, not introduced here and not repaired here: an all-zero TOC entry fed
    // to CdlSetloc/CdlPlay makes CD_cw latch s_lastSeekTarget at 00:00:00, which is LBA -150, which
    // LibDs.FindRegistrationContaining resolves to nothing — a following CdRead would return 0 for
    // ever inside ReadCDData's `while (CdRead(...) != 1)`. FUN_80025658 and FUN_800258F0 are still
    // BLOCKED stubs in SELECT_EXE/SelectScreen.cs, so nothing reaches it today, and the array they
    // would pass was already zero-filled before this change.

    // GHIDRA: DAT_80050410 @ 0x80050410 (SELECT.EXE)
    // libcd's debug level, the value CdSetDebug stores. Link-time 0, read with read-memory, and
    // CdSetDebug is still a stub here — so every diagnostic gated on it below is dead in the image
    // as well as in the port. Transliterated because the gates are part of the control flow.
    private static int DAT_80050410 = 0;

    // GHIDRA: CdGetToc2 @ 0x8004782C (SELECT.EXE)
    // PARTIAL: control flow and return contract closed; the TOC bytes themselves have no desktop
    // source (see the block above).
    //
    // The original walks a `byte *` in strides of four; `param_2` here is the CdlLOC[] the caller
    // already holds, and the two pointer cursors become entry indices — pbVar1 the write cursor,
    // p2 the cursor the trailing diagnostic destroys. param_1 is genuinely unread in the body: the
    // image's only caller passes 1 and no instruction between 0x8004782C and 0x80047A27 touches a0.
    public static int CdGetToc2(int param_1, CdlLOC[] param_2)
    {
        byte bVar2;
        CdlCB func;
        int iVar3;
        int iVar4;
        uint uVar6;
        uint uVar7;
        int pbVar1;
        int p2;
        byte[] local_30 = new byte[8];
        byte[] uStack_28 = new byte[8];

        local_30[0] = 1;
        func = CdSyncCallback(null);
        iVar3 = CdControlB(0x13, null, uStack_28);
        if (iVar3 != 0)
        {
            uVar6 = (uint)(uStack_28[1] >> 4) * 10 + (uint)(uStack_28[1] & 0xf);
            uVar7 = (uint)(uStack_28[2] >> 4) * 10 + (uint)(uStack_28[2] & 0xf);
            if (1 < DAT_80050410)
            {
                Console.WriteLine("track=" + uVar6 + "," + uVar7);
            }

            local_30[0] = 0;
            iVar3 = CdControlB(0x14, local_30, uStack_28);
            if (iVar3 != 0)
            {
                param_2[0].minute = uStack_28[1];
                param_2[0].sector = 0;
                param_2[0].second = uStack_28[2];
                iVar3 = 1;
                pbVar1 = 0;
                if (uVar6 <= uVar7)
                {
                    do
                    {
                        // Binary to packed BCD, the same `v + (v / 10) * 6` identity CdIntToPos
                        // above uses, and in the original it is done in eight bits.
                        local_30[0] = (byte)(uVar6 + (uVar6 / 10) * 6);
                        iVar4 = CdControlB(0x14, local_30, uStack_28);
                        iVar3 = iVar3 + 1;
                        if (iVar4 == 0)
                        {
                            goto TOC_OBJ_1F0;
                        }

                        param_2[pbVar1 + 1].minute = uStack_28[1];
                        uVar6 = uVar6 + 1;
                        param_2[pbVar1 + 1].sector = 0;
                        param_2[pbVar1 + 1].second = uStack_28[2];
                        pbVar1 = pbVar1 + 1;
                    } while ((int)uVar6 <= (int)uVar7);
                }

                if (1 < DAT_80050410)
                {
                    iVar4 = 0;
                    if (-1 < iVar3 + -1)
                    {
                        p2 = 0;
                        do
                        {
                            bVar2 = param_2[p2].minute;
                            iVar4 = iVar4 + 1;
                            Console.WriteLine(
                                "CdGetToc2: " + bVar2.ToString("x2") + ":" +
                                param_2[p2].second.ToString("x2") + ":00");
                            p2 = p2 + 1;
                        } while (iVar4 <= iVar3 + -1);
                    }
                }

                CdSyncCallback(func);
                return iVar3;
            }
        }

    TOC_OBJ_1F0:
        if (DAT_80050410 != 0)
        {
            Console.WriteLine("CdGetToc2: error");
        }

        CdSyncCallback(func);
        return 0;
    }

    // GHIDRA: CdGetToc @ 0x80047808 (SELECT.EXE)
    // The parameter was `CdlLOC loc` before this tranche, a single entry. The original's `CdlLOC *`
    // is the head of the caller's 32-entry array — FUN_80025658 passes &DAT_80055CEC and then walks
    // it with `p = p + 1` — so the port takes the array. Nothing called the old single-entry form.
    public static int CdGetToc(CdlLOC[] loc)
    {
        int iVar1;

        iVar1 = CdGetToc2(1, loc);
        return iVar1;
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