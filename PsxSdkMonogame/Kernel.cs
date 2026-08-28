namespace PsxSdkMonogame;

public static class Kernel
{
    public static void EnterCriticalSection()
    {

    }

    public static void ExitCriticalSection()
    {

    }

    // GHIDRA: memset @ 0x80071a44
    // JUSTIFICATION: PSX hardware adaptation only — a BIOS A0 syscall, so there is no C body to
    // transliterate; the observable contract IS the implementation.
    // IT IS NOT A 561-INSTRUCTION FUNCTION, which is how `overlaycensus floor` ranked it at #1 with
    // 22 scenes behind it. The three words at 0x80071A44 are the standard psyq BIOS stub —
    // `addiu $t2,$zero,0xA0` / `jr $t2` / `addiu $t1,$zero,0x2B` — and A0(0x2Bh) is memset. The
    // census measures a function by scanning to its own `jr $ra`; a stub leaves through `jr $t2`
    // (0x01400008), so the scan ran on into unrelated code. Its three neighbours are the same shape
    // and are already in this port at the right addresses: 0x80071A54 = A0(0x2Fh) = rand and
    // 0x80071A64 = A0(0x30h) = srand, both in Prng.cs, and 0x80071A74 = A0(0x3Fh) = printf below.
    // Every overlay call site read out of PE.IMG is the `memset(&local, 0, 0x10)` that clears a
    // stack VECTOR before its three components are written — the same idiom this port already
    // transliterates inline in HighEffect0..5Particles.cs. Kept `void`: the C contract returns
    // `dst`, and no call site observed uses the result.
    public static void memset(byte[] dst, int offset, int c, int n)
    {
        byte fill = (byte)c;
        for (int i = 0; i < n; i++)
        {
            dst[offset + i] = fill;
        }
    }

    // GHIDRA: printf @ 0x80071a74
    // JUSTIFICATION: PSX hardware adaptation only
    // RELATION: the BIOS/libc printf writes to the dev-kit TTY. The desktop equivalent of that TTY is
    // the debug console, and routing it there is the whole of the adaptation.
    // CHANGED 2026-07-31 from an empty no-op. The no-op was defensible while nothing called this, but
    // the port now transliterates a growing number of the ORIGINAL'S OWN diagnostic strings — the one
    // that motivated this, BindGenericHighEffectVariantThread's "TS No Thread No.%d", is emitted
    // exactly when an effect fails to bind its thread block, which is the single most useful signal
    // this subsystem produces. Swallowing it meant the port could fail the way the original announces
    // and say nothing at all.
    // The C format specifiers are NOT reimplemented: %d/%x/%s are left in the string as the original
    // wrote them and the arguments are appended. Several call sites in this port pass no argument at
    // all for a format that has one (the original's own value is not always recoverable), so a real
    // formatter would throw where a literal echo cannot.
    // THE FILE IS THE ONLY RELIABLE SINK. This project is a WinExe, so there is NO console attached
    // when it runs outside a debugger, and Debug.WriteLine is compiled out of Release builds — a
    // message written only to those two would be invisible in exactly the situation where it matters
    // most, a normal run used to look at the rendering. The log sits next to the executable and is
    // truncated once per process, so each run reads as one session.
    public static void printf(string format, params object[] args)
    {
        string line = format == null ? string.Empty : format.TrimEnd('\r', '\n');
        if (args != null && args.Length != 0)
        {
            line = line + " [" + string.Join(", ", args) + "]";
        }
        System.Diagnostics.Debug.WriteLine(line);
        System.Console.WriteLine(line);
        WriteToLogFile(line);
    }

    // JUSTIFICATION: PSX hardware adaptation only
    // RELATION: the desktop stand-in for the dev-kit TTY that printf above wrote to.
    // Failures are swallowed: a diagnostic channel must never be able to take the game down, and
    // there is nowhere to report a logging failure to anyway.
    private static bool s_logStarted;

    private static void WriteToLogFile(string line)
    {
        try
        {
            string path = System.IO.Path.Combine(System.AppContext.BaseDirectory, "pe-printf.log");
            if (!s_logStarted)
            {
                s_logStarted = true;
                System.IO.File.WriteAllText(path, string.Empty);
            }
            System.IO.File.AppendAllText(path, line + System.Environment.NewLine);
        }
        catch
        {
        }
    }
}
