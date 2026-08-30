using System;

namespace PsxSdkMonogame;

public static class LibApi
{
    // JUSTIFICATION: PSX hardware adaptation — the port's model of kernel event delivery for the
    // memory-card events. On console the libapi card functions post kernel events (event class +
    // spec value) and the handlers the game registered through OpenEvent react to them; this port
    // has no kernel event table, so the game installs a sink here and LibApi posts the same
    // (class, spec) pairs the console would deliver. Not installed -> events are dropped.
    public static Action<uint, uint> CardEventSink;

    // CORRECTION (2026-08-30, SELECT.EXE): the sink is no longer the only destination. SELECT.EXE
    // does NOT read the card result through a ported state machine the way TITLE.EXE does — it
    // polls the real kernel events with TestEvent (FUN_800221D0 @ 0x800221D0 and FUN_80022244
    // @ 0x80022244, both in do/while loops on main's pre-loop path). So the same (class, spec)
    // pair now also goes into the event table below, which is what those loops read. The sink is
    // kept unchanged for TITLE.EXE's port.
    private static void DeliverCardEvent(uint eventClass, uint spec)
    {
        CardEventSink?.Invoke(eventClass, spec);
        DeliverEvent(eventClass, spec);
    }

    public static void InitCARD(long val)
    {
        // Do nothing
    }

    public static long StartCARD()
    {
        // Do nothing
        return default;
    }

    public static long StopCARD()
    {
        // Do nothing
        return default;
    }

    public static void _bu_init()
    {
        // Do nothing
    }

    // JUSTIFICATION: PSX hardware adaptation only — chan encodes the port as (port<<4), matching
    // every call site in this port (MemoryCardMenu.UpdateMemoryCardPortStateMachine passes
    // cardIndex<<4). Shared by every _card_* function below.
    private static int DecodeCardPort(long chan) => (int)((chan >> 4) & 1);

    // CORRECTION (2026-07-24): was a no-op stub. Real hardware probes the card asynchronously and
    // signals completion via the three class-0xf4000001 event flags — g_cardEvF4000001Spec2000 /
    // g_cardEvF4000001Spec8000And0100 / g_cardEvF4000001Spec0004 — polled
    // by UpdateMemoryCardPortStateMachine's state 1 (see that function's remarks — re-traced this
    // session to map which flag drives which transition).
    // CORRECTION (2026-07-27): signalled g_cardEvF4000001Spec2000, which is the "card answered but
    // is not a known-good card" path — state 1's spec-0x2000 branch always re-runs _card_clear + _card_load, so
    // the port state machine looped 4 -> 1 -> 2 -> 3 -> 4 forever, re-reading the card every few
    // frames. States 2/3 are exactly what PollMemoryCardActivity (MemoryCardMenu.cs) treats as
    // "busy", so DrawMenuDescriptionBar's id-0x24 case kept flipping to "Checking Memory Card / Do
    // not insert or remove Memory Card" and the slot rows kept dropping out (IsMemoryCardSlotSelectable
    // reads the presence bit, which case 0 clears) instead of settling on "Select Slot".
    // A successful probe must signal g_cardEvF4000001Spec0004: state 1's spec-0x0004 branch is the ONLY one
    // with the `if (statusByte & 1) { state = 4; }` shortcut, which exists precisely so that
    // re-probing an already-initialised card returns straight to state 4 without another
    // clear+load. The first probe still takes the clear+load path (the presence bit is 0 then), so
    // the card is still really initialised exactly once. Presence now comes from the file-backed
    // card itself (LibMcrd.CardIsPresent, whose result this function previously discarded) rather
    // than being assumed, with a missing card reported through the (0xF4000001, 0x8000) card
    // event — the game's sink raises the failure flag its state 1 uses to drop the port back to
    // state 0.
    public static long _card_info(long chan)
    {
        int port = DecodeCardPort(chan);
        if (LibMcrd.CardIsPresent(port))
        {
            DeliverCardEvent(0xF4000001, 0x0004);
        }
        else
        {
            DeliverCardEvent(0xF4000001, 0x8000);
        }
        return 0;
    }

    // CORRECTION (2026-07-24): was a no-op stub. g_cardEvF0000011Spec0004 is the completion signal
    // UpdateMemoryCardPortStateMachine's state 2 waits on before proceeding to _card_load.
    public static long _card_clear(long chan)
    {
        DeliverCardEvent(0xF0000011, 0x0004);
        return 0;
    }

    // CORRECTION (2026-07-24): was a no-op stub. g_cardEvF4000001Spec0004 is what
    // UpdateMemoryCardPortStateMachine's state 3 requires to mark the port's status byte bit 0
    // (IsMemoryCardPresent) and reach state 4 (card active/ready) — this is the actual mechanism
    // that makes "both memory cards always active/present" observable in-game.
    public static long _card_load(long chan)
    {
        DeliverCardEvent(0xF4000001, 0x0004);
        return 0;
    }

    public static long _card_auto(long val)
    {
        // Do nothing
        return default;
    }

    public static void _new_card()
    {
        // Do nothing
    }

    // PROBABLE: not exercised by any currently-ported call site (UpdateMemoryCardSlotStateMachine,
    // the only plausible caller, is itself BLOCKED/unported); 0 = "no error" is the safest minimal
    // choice matching an always-present, never-erroring backend.
    public static long _card_status(long drv)
    {
        return 0;
    }

    public static long _card_wait(long drv)
    {
        // Do nothing
        return default;
    }

    public static ulong _card_chan()
    {
        // Do nothing
        return default;
    }

    // CORRECTION (2026-07-24): was a no-op stub; not yet exercised by any ported call site
    // (UpdateMemoryCardSlotStateMachine is BLOCKED), but wired to the real file-backed block
    // storage now so it behaves correctly once that caller is ported.
    public static long _card_write(long chan, long block, byte[] buf)
    {
        LibMcrd.WriteCardBlock(DecodeCardPort(chan), (int)block, buf, buf.Length);
        return 1;
    }

    // CORRECTION (2026-07-24): was a no-op stub; see _card_write remarks.
    public static long _card_read(long chan, long block, byte[] buf)
    {
        byte[] data = LibMcrd.ReadCardBlock(DecodeCardPort(chan), (int)block);
        System.Array.Copy(data, buf, System.Math.Min(data.Length, buf.Length));
        return 1;
    }

    // CORRECTION (2026-07-24): was a no-op stub; see _card_write remarks.
    public static long _card_format(long chan)
    {
        LibMcrd.FormatCard(DecodeCardPort(chan));
        return 1;
    }



    public static void InitHeap(ulong[] head, ulong size)
    {
        // Do nothing
    }

    // GHIDRA: InitHeap @ 0x80059160 (TITLE.EXE)
    // The game reaches this overload, not the ulong[] one above: main @ 0x800581DC calls
    // InitHeap(0x10000, 0x10000) with a raw PSX address, and FUN_80058a9c repeats it.
    public static void InitHeap(int baseAddress, int size) => PsxHeap.InitHeap(baseAddress, size);

    // GHIDRA: malloc @ 0x800591A0 (TITLE.EXE)
    // Observable contract only; see PsxHeap for why this SDK routine is adapted rather than
    // transliterated.
    public static int malloc(int size) => PsxHeap.Malloc(size);

    // GHIDRA: free @ 0x800593D4 (TITLE.EXE)
    public static void free(int address) => PsxHeap.Free(address);


    public static int open(char[] devname,  int flag)
    {
        // Do nothing
        return default;
    }

    // JUSTIFICATION: PSX hardware adaptation only — the BIOS file API on the memory-card device.
    // UpdateMemoryCardSlotStateMachine drives the whole save browser through it (open/read/write/
    // lseek/close/erase/format/firstfile/nextfile); every one of these was a `return default` stub,
    // so the browser had nothing to enumerate and every open reported success with fd 0. Backed by
    // LibMcrd's card file system (see its own remarks). String overloads because the game builds
    // the path with sprintf into a fixed scratch buffer, which this port models as a string.
    public static int open(string devname, int flag) => LibMcrd.CardFileOpen(devname, flag);

    public static int close(int fd)
    {
        return LibMcrd.CardFileClose(fd);
    }

    public static int read(int fd, byte[] buf, int bufOffset, int n) => LibMcrd.CardFileRead(fd, buf, bufOffset, n);

    public static long read(long fd, object buf, long n)
    {
        // Do nothing
        return default;
    }

    public static int write(int fd, byte[] buf, int bufOffset, int n) => LibMcrd.CardFileWrite(fd, buf, bufOffset, n);

    public static int write(int fd, char[] buf, int n)
    {
        // Do nothing
        return default;
    }

    public static int lseek(int fd, int offset, int whence) => LibMcrd.CardFileSeek(fd, offset, whence);

    public static ulong lseek(int fd, uint offset, int flag)
    {
        // Do nothing
        return default;
    }

    // JUSTIFICATION: PSX hardware adaptation only — see open(string, int) above. The BIOS returns a
    // DIRENTRY pointer (NULL when exhausted); this port returns 1/0 and fills the caller's entry,
    // which is the same observable contract for every call site here.
    public static int firstfile(string pattern, LibMcrd.DIRENTRY dir) => LibMcrd.CardFileFirst(pattern, dir);

    public static int nextfile(LibMcrd.DIRENTRY dir) => LibMcrd.CardFileNext(dir);

    public static int erase(string name) => LibMcrd.CardFileErase(name);

    public static int format(string fs) => LibMcrd.CardFileFormat(fs);

    public static long ioctl(int fd, int com, int arg)
    {
        // Do nothing
        return default;
    }

    public static long format(char[] fs)
    {
        // Do nothing
        return default;
    }

    public static long cd(char[] path)
    {
        // Do nothing
        return default;
    }

    //struct DIRENTRY firstfile(char[] name, struct DIRENTRY dir);

    //struct DIRENTRY nextfile(struct DIRENTRY dir);

    public static long erase(char[] name)
    {
        // Do nothing
        return default;
    }

    public static long undelete(char[] name)
    {
        // Do nothing
        return default;
    }

    public static long rename(char[] src, char[] dest)
    {
        // Do nothing
        return default;
    }

    //public static long Load(char[] name,  struct EXEC exec) {
    //    // Do nothing
    //    return default;
    //}

    //long Exec(struct EXEC exec, long argc, char[] argv);

    public static void LoadExec(char[] name, ulong s_addr, ulong s_size)
    {
        // Do nothing
    }

    //long LoadTest(char[] name, struct EXEC exec);


    // =======================================================================================
    // BIOS PAD DRIVER — B(12h) InitPAD / B(13h) StartPAD / B(14h) StopPAD / B(5Bh) ChangeClearPAD
    // =======================================================================================
    //
    // WHERE THE ORIGINALS LIVE: in ROM. All four are 12-byte jump stubs, read out of /SELECT.EXE:
    //     InitPAD        @ 0x8004EB64 (SELECT.EXE)  B(12h)
    //     StartPAD       @ 0x8004ED54 (SELECT.EXE)  B(13h)
    //     StopPAD        @ 0x8004EB74 (SELECT.EXE)  B(14h)
    //     ChangeClearPAD @ 0x8004ED84 (SELECT.EXE)  B(5Bh)
    //
    // THIS IS NOT THE PATH TITLE.EXE USES. TITLE.EXE goes through libetc: its PadInit
    // @ 0x8006FDA0 calls PAD_init(0x20000001, &DAT_800920D4) and PadRead @ 0x8006FDF0 returns
    // ~DAT_800920D4 — one 32-bit word, both ports. SELECT.EXE calls PadInit(0) too (main line 15)
    // but PadRead has callerCount 0 there; its input comes from the BIOS driver's own 34-byte
    // status buffers instead. Two different drivers, two different layouts; LibEtc's PadRead is
    // untouched by any of this.
    //
    // THE ONE CALL SITE, and the layout contract it fixes:
    //   FUN_800261A4 @ 0x800261A4 is the whole of SELECT.EXE's input bring-up, called from
    //   FUN_80030698 (graphics + CD bring-up), which main calls before its loop:
    //       InitPAD(&DAT_80055D6C, 0x22, &DAT_80055D8E, 0x22);
    //       StartPAD();
    //       ChangeClearPAD(0);
    //   0x80055D8E - 0x80055D6C = 0x22, so the two 34-byte buffers are contiguous, and
    //   FUN_800261E4 @ 0x800261E4 indexes straight across both as one region:
    //       return (&DAT_80055D6C)[param_1 * 0x22];
    //
    // WHICH BYTES THE GAME READS (find-cross-references on 0x80055D6C, plus the two readers):
    //   +0  presence. FUN_800261E4(1) returns it and both consumers read 0 as "pad present":
    //         FUN_80030EF8 line 10:      if (DAT_80055A40 == 0 && FUN_800261E4(1) != 0) ...
    //         FUN_800315C0 lines 96/121: DAT_80055B10 = (FUN_800261E4(1) == 0);
    //       FUN_80026208 line 83 reads pad 2's own +0 the same way (`DAT_80055D8E != '\0'`).
    //   +2, +3  the two controller status bytes, ACTIVE LOW. FUN_80026208 @ 0x80026208 reads
    //       them as ~CONCAT11(DAT_80055D6E, DAT_80055D6F) for pad 1 and
    //       ~CONCAT11(DAT_80055D90, DAT_80055D91) for pad 2, i.e. (buf[+2] << 8) | buf[+3].
    //   +1  never read by SELECT.EXE. BLOCKED: the BIOS writes a pad type/length byte there and
    //       nothing in the image closes its value, so this port leaves it alone.
    //
    // THE BIT ASSIGNMENT IS CLOSED, not assumed. FUN_80033D34 @ 0x80033D34 — the list cursor
    // every menu state drives — consumes FUN_80026208's word:
    //     line 84   (word & 0x4000) advances the cursor        => 0x4000 is Down
    //     line 39   (word & 0x5000) is the auto-repeat pair    => 0x1000|0x4000 = Up|Down
    //     line 38   (word & 0x0060) is the confirm/cancel pair => 0x0020|0x0040 = Circle|Cross
    // which is exactly the mask table PadButton already carries. So buf[+2] is the HIGH byte of
    // that 16-bit mask and buf[+3] the LOW byte, and PadInputBackend.PublishedActiveLow — whose
    // low halfword is port 1 and high halfword port 2 — already holds both in that form.

    // JUSTIFICATION: PSX hardware adaptation only
    // RELATION: the two u_char buffers InitPAD hands to the BIOS driver. Held as (array, offset)
    // pairs because the original passes two raw pointers into ONE contiguous .bss region and a
    // port may model that region as a single byte[] (FUN_800261E4 indexes across both).
    private static byte[] s_biosPadBufA;
    private static int s_biosPadOffA;
    private static int s_biosPadLenA;
    private static byte[] s_biosPadBufB;
    private static int s_biosPadOffB;
    private static int s_biosPadLenB;
    private static bool s_biosPadStarted;

    // JUSTIFICATION: PSX hardware adaptation only
    // RELATION: ChangeClearPAD's argument.
    // PARTIAL: SELECT.EXE only ever passes 0 (FUN_800261A4 and FUN_80021D34), the routine is a
    // BIOS stub with no body in the image, and nothing in this port reads the flag back. It is
    // recorded so the observable state matches, not acted on.
    private static long s_changeClearPad;

    // JUSTIFICATION: C# language bridge only
    // RELATION: shared body of the two InitPAD overloads below.
    private static long InitPADInternal(byte[] bufA, int offsetA, int lenA, byte[] bufB, int offsetB, int lenB)
    {
        s_biosPadBufA = bufA;
        s_biosPadOffA = offsetA;
        s_biosPadLenA = lenA;
        s_biosPadBufB = bufB;
        s_biosPadOffB = offsetB;
        s_biosPadLenB = lenB;
        s_biosPadStarted = false;

        // The buffers are left in the RELEASED state rather than at 0: a zeroed buffer reads
        // through FUN_80026208's ~CONCAT11(...) as EVERY BUTTON HELD. TITLE.EXE's libetc PadInit
        // @ 0x8006FDA0 does the same thing for the other pad path (`DAT_800920D4 = 0xFFFFFFFF;`).
        ReleaseBiosPadBuffer(bufA, offsetA, lenA);
        ReleaseBiosPadBuffer(bufB, offsetB, lenB);
        return BIOS_SUCCESS;
    }

    // GHIDRA: InitPAD @ 0x8004EB64 (SELECT.EXE) — BIOS B(12h) jump stub, body in ROM
    // JUSTIFICATION: PSX hardware adaptation only
    // RELATION: installs the two status buffers; StartPAD is what begins filling them.
    public static long InitPAD(byte[] bufA, int lenA, byte[] bufB, int lenB)
        => InitPADInternal(bufA, 0, lenA, bufB, 0, lenB);

    // JUSTIFICATION: C# language bridge only
    // RELATION: the original passes two raw pointers into one contiguous .bss region
    // (0x80055D6C and 0x80055D8E = +0x22). A port that models that region as a single byte[]
    // names the second buffer by offset instead of by a second array.
    public static long InitPAD(byte[] buf, int offsetA, int lenA, int offsetB, int lenB)
        => InitPADInternal(buf, offsetA, lenA, buf, offsetB, lenB);

    // BLOCKED: this char[] overload predates the byte[] pair above and has no call site. The BIOS
    // buffer is a u_char array whose bytes the game indexes directly, so char[] cannot carry the
    // layout; use InitPAD(byte[], ...) instead. Left in place so no existing signature disappears.
    public static long InitPAD(char[] bufA, long lenA, char[] bufB, long lenB)
    {
        // Do nothing
        return default;
    }

    // GHIDRA: StartPAD @ 0x8004ED54 (SELECT.EXE) — BIOS B(13h) jump stub, body in ROM
    // JUSTIFICATION: PSX hardware adaptation only
    // RELATION: starts the driver, and takes the first sample immediately so the buffer is never
    // read before it has been filled once. No SELECT.EXE call site reads the return value
    // (FUN_800261A4, FUN_80021D34); reported as success on this file's BIOS_SUCCESS convention.
    public static long StartPAD()
    {
        s_biosPadStarted = true;
        RefreshBiosPadBuffers();
        return BIOS_SUCCESS;
    }

    // GHIDRA: StopPAD @ 0x8004EB74 (SELECT.EXE) — BIOS B(14h) jump stub, body in ROM
    // main @ 0x8003045C calls it on the way out of its loop; FUN_80021D34 @ 0x80021D34 calls
    // StartPAD again after the memory-card teardown, so stop/start is a reversible pair.
    // PARTIAL: whether the BIOS also clears the buffers on stop is not closed, so this leaves
    // them holding their last sample.
    public static void StopPAD()
    {
        s_biosPadStarted = false;
    }

    // GHIDRA: ChangeClearPAD @ 0x8004ED84 (SELECT.EXE) — BIOS B(5Bh) jump stub, body in ROM
    public static void ChangeClearPAD(long val)
    {
        s_changeClearPad = val;
    }

    // JUSTIFICATION: PSX hardware adaptation only
    // RELATION: the BIOS driver's own per-vertical-retrace buffer refresh. On console the driver
    // InitPAD/StartPAD install samples the controllers off the V-BLANK interrupt; on desktop the
    // modelled V-BLANK is LibEtc's WaitVBlankInterrupt, which calls this right after the host
    // frame yield, so the sample is the one the host just took. Public only because that call
    // crosses the file boundary.
    public static void RefreshBiosPadBuffers()
    {
        if (!s_biosPadStarted)
        {
            return;
        }

        uint activeLow = PadInputBackend.PublishedActiveLow;
        WriteBiosPadBuffer(s_biosPadBufA, s_biosPadOffA, s_biosPadLenA,
            (ushort)(activeLow & 0xFFFF), PadInputBackend.PublishedPort1Connected);
        WriteBiosPadBuffer(s_biosPadBufB, s_biosPadOffB, s_biosPadLenB,
            (ushort)(activeLow >> 16), PadInputBackend.PublishedPort2Connected);
    }

    // JUSTIFICATION: PSX hardware adaptation only
    // RELATION: writes one 34-byte status buffer in the layout closed above — +0 presence,
    // +2 high status byte, +3 low status byte, both active low. +1 is left untouched (BLOCKED).
    // PARTIAL: only "0 means present" is closed by the consumers; the value the BIOS writes for
    // an absent pad is not, so the all-ones idle value is used for it.
    private static void WriteBiosPadBuffer(byte[] buf, int offset, int len, ushort activeLow, bool connected)
    {
        if (buf == null || len < 4 || offset < 0 || offset + 4 > buf.Length)
        {
            return;
        }

        buf[offset + 0] = connected ? (byte)0x00 : (byte)0xFF;
        buf[offset + 2] = (byte)(activeLow >> 8);
        buf[offset + 3] = (byte)(activeLow & 0xFF);
    }

    // JUSTIFICATION: PSX hardware adaptation only
    // RELATION: the released state of the two status bytes — see InitPADInternal's remark.
    private static void ReleaseBiosPadBuffer(byte[] buf, int offset, int len)
    {
        if (buf == null || len < 4 || offset < 0 || offset + 4 > buf.Length)
        {
            return;
        }

        buf[offset + 2] = 0xFF;
        buf[offset + 3] = 0xFF;
    }

    public static void EnablePAD()
    {
        // Do nothing
    }

    public static void DisablePAD()
    {
        // Do nothing
    }



    //public static long OpenEvent(ulong desc, long spec, long mode, long (* func)()) {
    //    // Do nothing
    //    return default;
    //}

    // JUSTIFICATION: PSX hardware adaptation only
    // RELATION: the BIOS returns 1 on success and 0 on failure for CloseEvent / EnableEvent /
    // DisableEvent / SetRCnt / StartRCnt / StopRCnt / ResetRCnt, and the runtime relies on that: every
    // call site in the Akao driver wraps them in `do { r = Xxx(...); } while (r == 0);` retry loops
    // (ShutdownAkaoSystem @0x80085744, InitSpuAndTimerEvent @0x80085644). Returning `default` (0) made
    // those loops spin forever. There is nothing to fail on desktop, so the adapters report success —
    // the observable contract the original control flow was written against.
    public const long BIOS_SUCCESS = 1;

    // =======================================================================================
    // KERNEL EVENTS — BIOS B-table 07h..0Dh
    // =======================================================================================
    //
    // WHERE THE ORIGINALS LIVE: nowhere in either overlay. Every one of these is a 12-byte jump
    // stub into the BIOS ROM. Read out of /SELECT.EXE with read-memory, the stub is always
    //     24 0A 00 B0   li t2, 0xB0        (the B-function table entry point)
    //     01 40 00 08   jr t2
    //     24 09 00 nn   li t1, nn          (delay slot: the B-function number)
    // and the measured stubs are:
    //     DeliverEvent  @ 0x8004ED24 (SELECT.EXE)  B(07h)
    //     OpenEvent     @ 0x8004EDC4 (SELECT.EXE)  B(08h)
    //     CloseEvent    @ 0x8004EAE4 (SELECT.EXE)  B(09h)
    //     WaitEvent     @ 0x8004EDE4 (SELECT.EXE)  B(0Ah)
    //     TestEvent     @ 0x8004EDF4 (SELECT.EXE)  B(0Bh)
    //     EnableEvent   @ 0x8004EB54 (SELECT.EXE)  B(0Ch)
    //     DisableEvent  @ 0x8004ED14 (SELECT.EXE)  B(0Dh)
    // So there is no body to transliterate. Everything below is a PSX hardware adaptation
    // reconstructed from the CONTRACT SELECT.EXE's own callers depend on, and nothing more.
    //
    // WHAT THE CALLERS CLOSE (all in SELECT.EXE, all on main's pre-loop path):
    //   FUN_80022348 @ 0x80022348 opens eight events and enables all eight:
    //       EnterCriticalSection();
    //       DAT_80055B54 = OpenEvent(0xF4000001, 0x0004, 0x2000, 0);
    //       DAT_80055B58 = OpenEvent(0xF4000001, 0x8000, 0x2000, 0);
    //       DAT_80055B5C = OpenEvent(0xF4000001, 0x0100, 0x2000, 0);
    //       DAT_80055B60 = OpenEvent(0xF4000001, 0x2000, 0x2000, 0);
    //       DAT_80055B70 = OpenEvent(0xF0000011, 0x0004, 0x2000, 0);
    //       DAT_80055B74 = OpenEvent(0xF0000011, 0x8000, 0x2000, 0);
    //       DAT_80055B78 = OpenEvent(0xF0000011, 0x0100, 0x2000, 0);
    //       DAT_80055B7C = OpenEvent(0xF0000011, 0x2000, 0x2000, 0);
    //       ExitCriticalSection();
    //       EnableEvent(each of the eight);
    //     The callback argument is 0 for all eight, so nothing is dispatched: poll-only events.
    //     FUN_80021D34 @ 0x80021D34 is the mirror image on the LoadExec path — eight
    //     DisableEvent, then eight CloseEvent inside the critical section.
    //   FUN_800221D0 @ 0x800221D0 polls the four 0xF4000001 handles in a do/while and maps the
    //     one that fires to a code:  spec 0x0004 -> 0, 0x8000 -> 1, 0x0100 -> 2, 0x2000 -> 4.
    //     FUN_80022244 @ 0x80022244 is the same routine over the four 0xF0000011 handles.
    //   FUN_800222B8 @ 0x800222B8 calls TestEvent on those same four handles and THROWS EVERY
    //     RESULT AWAY, immediately before every _card_info / _card_load. FUN_80022300 @
    //     0x80022300 does the same for the 0xF0000011 four, immediately before _card_clear.
    //
    // THE SIDE EFFECT IS CLOSED FROM THE IMAGE, not from documentation: a side-effect-free
    // TestEvent would make FUN_800222B8 and FUN_80022300 dead code, and would make FUN_800221D0
    // keep reporting the FIRST command's outcome for ever. TestEvent therefore CONSUMES the
    // delivered flag, and those two routines are drains issued before a new card command. That
    // is the "delivered -> return 1 and clear, otherwise return 0" implemented below.
    //
    // WHO DELIVERS, AND WHERE THIS PORT IS STILL SHORT — read this before trusting the loop:
    // On console the 0xF4000001 / 0xF0000011 events are posted asynchronously by the BIOS card
    // driver when the card hardware answers. This port has neither, so TestEvent CANNOT invent a
    // delivery; it only ever reports what something else delivered. The one thing in this port
    // that stands in for the card driver is _card_info / _card_clear / _card_load above, whose
    // desktop bodies already complete the operation synchronously and already post exactly those
    // (class, spec) pairs (closed 2026-07-24/27 against TITLE.EXE's own ported state machine).
    // DeliverCardEvent now posts into this table as well, and that is the whole unblock.
    //
    // SELECT.EXE corroborates that mapping independently: FUN_80021E34 @ 0x80021E34 treats
    // FUN_800221D0's 1 (spec 0x8000) as "retry, up to 5 times", its 4 (spec 0x2000) as "run
    // _card_clear first", and main @ 0x8003045C only reads the save when the result is 0 (spec
    // 0x0004) — the same success/failure/new-card split _card_info already emits.
    //
    // PARTIAL, and it matters: every FUN_800221D0 / FUN_80022244 call site in SELECT.EXE is
    // immediately preceded by a _card_* call (6 and 2 sites, from find-cross-references), so a
    // synchronous delivery is always in flight when the poll runs. A call site that polled
    // WITHOUT first issuing a card command would spin here for ever — and that would be the
    // honest answer, because there would be nothing to deliver.

    // JUSTIFICATION: PSX hardware adaptation only
    // RELATION: stands for the BIOS EventCB table. PARTIAL: the real table's length is whatever
    // SetConf last set, and SetConf is a stub in this port, so the length is fixed here instead.
    // SELECT.EXE's peak usage is the eight handles FUN_80022348 opens.
    private const int EventTableSize = 32;

    // JUSTIFICATION: PSX hardware adaptation only
    // RELATION: one BIOS EventCB — the (class, spec, mode, callback) OpenEvent was handed, plus
    // the two bits of status the callers observe: whether EnableEvent has armed it, and whether
    // a DeliverEvent has landed on it since the last TestEvent.
    private struct EventDescriptor
    {
        public bool open;
        public bool enabled;
        public bool delivered;
        public long evClass;
        public long spec;
        public long mode;
        public Action callback;
    }

    private static readonly EventDescriptor[] s_events = new EventDescriptor[EventTableSize];

    // JUSTIFICATION: C# language bridge only
    // RELATION: the BIOS hands back an opaque descriptor; this port uses table index + 1 so that
    // 0 is never a valid handle. Nothing in the runtime does arithmetic on the value.
    private static int EventIndex(long handle)
    {
        int index = (int)(handle - 1);
        return index >= 0 && index < s_events.Length ? index : -1;
    }

    // GHIDRA: OpenEvent @ 0x8004EDC4 (SELECT.EXE) — BIOS B(08h) jump stub, body in ROM
    // JUSTIFICATION: PSX hardware adaptation only
    // RELATION: registers (class, spec, mode, callback) and returns a handle WITHOUT arming
    // anything — EnableEvent is what arms it, and FUN_80022348 depends on exactly that split
    // (it opens all eight inside the critical section and enables them after leaving it).
    // BLOCKED: the failure return is not closed. The BIOS reports a full event table somehow,
    // but no SELECT.EXE call site tests the handle, so this returns -1 — a value that can never
    // collide with a valid handle — rather than a guessed error code.
    public static long OpenEvent(long ev, long spec, long mode, Action func)
    {
        for (int i = 0; i < s_events.Length; i++)
        {
            if (!s_events[i].open)
            {
                s_events[i].open = true;
                s_events[i].enabled = false;
                s_events[i].delivered = false;
                s_events[i].evClass = ev;
                s_events[i].spec = spec;
                s_events[i].mode = mode;
                s_events[i].callback = func;
                return i + 1;
            }
        }

        return -1;
    }

    // GHIDRA: CloseEvent @ 0x8004EAE4 (SELECT.EXE) — BIOS B(09h) jump stub, body in ROM
    public static long CloseEvent(long @event)
    {
        int index = EventIndex(@event);
        if (index >= 0)
        {
            if (s_events[index].open && s_events[index].evClass == unchecked((long)0xf2000002) &&
                s_events[index].spec == 2)
            {
                LibSpu.Spu.SetTickCallback(null);
                LibSpu.AudioBackend.Stop();
            }

            s_events[index] = default;
        }

        return BIOS_SUCCESS;
    }

    // GHIDRA: DeliverEvent @ 0x8004ED24 (SELECT.EXE) — BIOS B(07h) jump stub, body in ROM
    // JUSTIFICATION: PSX hardware adaptation only
    // RELATION: marks every OPEN and ENABLED descriptor matching (class, spec) as delivered.
    // PARTIAL: the BIOS also dispatches the handler of a callback-mode event on delivery. This
    // port does not, because no event it delivers carries one — the eight card events pass
    // callback 0, and the one callback-carrying event in this port, (0xF2000002, 2), is
    // dispatched by EnableEvent's audio-tick wiring below and is never DeliverEvent'ed.
    public static void DeliverEvent(ulong ev1, ulong ev2)
    {
        long evClass = unchecked((long)(uint)ev1);
        long spec = unchecked((long)(uint)ev2);

        for (int i = 0; i < s_events.Length; i++)
        {
            if (s_events[i].open && s_events[i].enabled &&
                unchecked((long)(uint)s_events[i].evClass) == evClass &&
                unchecked((long)(uint)s_events[i].spec) == spec)
            {
                s_events[i].delivered = true;
            }
        }
    }

    // JUSTIFICATION: PSX hardware adaptation only
    // RELATION: inverse of DeliverEvent above.
    // BLOCKED: no GHIDRA address. SELECT.EXE never calls UnDeliverEvent, so no stub for it was
    // located in the 0x8004EA64-0x8004EF33 thunk block and the annotation cannot be given
    // reliably. Nothing in this port calls it either.
    public static void UnDeliverEvent(ulong ev1, ulong ev2)
    {
        long evClass = unchecked((long)(uint)ev1);
        long spec = unchecked((long)(uint)ev2);

        for (int i = 0; i < s_events.Length; i++)
        {
            if (s_events[i].open &&
                unchecked((long)(uint)s_events[i].evClass) == evClass &&
                unchecked((long)(uint)s_events[i].spec) == spec)
            {
                s_events[i].delivered = false;
            }
        }
    }

    // GHIDRA: TestEvent @ 0x8004EDF4 (SELECT.EXE) — BIOS B(0Bh) jump stub, body in ROM
    // JUSTIFICATION: PSX hardware adaptation only
    // RELATION: 1 when a delivery is pending on this handle, and the delivery is consumed;
    // 0 otherwise. See the block comment above for why the consume is closed from the image.
    public static long TestEvent(long @event)
    {
        int index = EventIndex(@event);
        if (index < 0 || !s_events[index].open || !s_events[index].delivered)
        {
            return 0;
        }

        s_events[index].delivered = false;
        return 1;
    }

    // GHIDRA: WaitEvent @ 0x8004EDE4 (SELECT.EXE) — BIOS B(0Ah) jump stub, body in ROM
    // BLOCKED: WaitEvent blocks until the event is delivered by an interrupt. Nothing in this
    // port delivers from anywhere but the game thread itself, so a blocking WaitEvent would
    // deadlock the only thread that could ever satisfy it — LibSpu.SpuWrite's transfer loop
    // (LibSpu.cs:1291) calls it once per 0x400-byte chunk. It stays a non-blocking 0 and the
    // caller carries on, which is what it already did before this tranche. SELECT.EXE's own two
    // WaitEvent call sites are inside libspu, not on main's path.
    public static long WaitEvent(long @event)
    {
        // Do nothing
        return default;
    }

    // GHIDRA: EnableEvent @ 0x8004EB54 (SELECT.EXE) — BIOS B(0Ch) jump stub, body in ROM
    // JUSTIFICATION: backend MonoGame / PSX hardware adaptation
    // RELATION: arms the descriptor so DeliverEvent can land on it. The Akao special case is
    // unchanged from the previous tranche: InitSpuAndTimerEvent's
    // `OpenEvent(0xf2000002, 2, 0x1000, FUN_8008e23c)` is root counter 2, the Akao tick, and for
    // exactly that (class, spec) pair EnableEvent wires the callback onto PsxSdk.LibSpu.Spu's
    // tick schedule (SpuCore.SetTickCallback) and starts LibSpu.AudioBackend, so the ISR the
    // original ran off a hardware timer now runs off the audio pump instead (the user's own
    // clocking decision for that tranche). DisableEvent/CloseEvent undo it — ShutdownAkaoSystem
    // already calls both.
    public static long EnableEvent(long @event)
    {
        // Do nothing for events OpenEvent was not called on — see BIOS_SUCCESS above.
        int index = EventIndex(@event);
        if (index < 0 || !s_events[index].open)
        {
            return BIOS_SUCCESS;
        }

        s_events[index].enabled = true;

        if (s_events[index].evClass == unchecked((long)0xf2000002) && s_events[index].spec == 2 &&
            s_events[index].callback != null)
        {
            LibSpu.Spu.SetTickCallback(s_events[index].callback);
            LibSpu.AudioBackend.Start();
        }

        return BIOS_SUCCESS;
    }

    // GHIDRA: DisableEvent @ 0x8004ED14 (SELECT.EXE) — BIOS B(0Dh) jump stub, body in ROM
    public static long DisableEvent(long @event)
    {
        // Do nothing for events OpenEvent was not called on — see BIOS_SUCCESS above.
        int index = EventIndex(@event);
        if (index < 0 || !s_events[index].open)
        {
            return BIOS_SUCCESS;
        }

        s_events[index].enabled = false;

        if (s_events[index].evClass == unchecked((long)0xf2000002) && s_events[index].spec == 2)
        {
            LibSpu.Spu.SetTickCallback(null);
            LibSpu.AudioBackend.Stop();
        }

        return BIOS_SUCCESS;
    }



//    ulong OpenTh(ulong (* func)(), ulong sp, ulong gp);

    public static long closeTh(ulong thread)
    {
        // Do nothing
        return default;
    }

    public static long ChangeTh(ulong thread)
    {
        // Do nothing
        return default;
    }



    public static long SetRCnt(long spec, ushort target, long mode)
    {
        // Do nothing — see BIOS_SUCCESS above.
        return BIOS_SUCCESS;
    }

    public static long GetRCnt(long spec)
    {
        // Do nothing. Unlike its siblings this one returns a COUNTER VALUE, not a success flag, so
        // it keeps returning 0 — there is no desktop root counter to read.
        return default;
    }

    public static long StartRCnt(long spec)
    {
        // Do nothing — see BIOS_SUCCESS above.
        return BIOS_SUCCESS;
    }

    public static long StopRCnt(long spec)
    {
        // Do nothing — see BIOS_SUCCESS above.
        return BIOS_SUCCESS;
    }

    public static long ResetRCnt(long spec)
    {
        // Do nothing — see BIOS_SUCCESS above.
        return BIOS_SUCCESS;
    }



    public static long SetConf(ulong ev, ulong tcb, ulong sp)
    {
        // Do nothing
        return default;
    }

    public static void GetConf(ulong[] ev, ulong[] tcb, ulong[] sp)
    {
        // Do nothing
    }

    public static void SetMem(ulong n)
    {
        // Do nothing
    }



    public static ulong GetGp()
    {
        // Do nothing
        return default;
    }

    public static ulong GetSp()
    {
        // Do nothing
        return default;
    }

    public static ulong SetSp(ulong new_sp)
    {
        // Do nothing
        return default;
    }

    public static ulong GetSr()
    {
        // Do nothing
        return default;
    }

    public static ulong GetCr()
    {
        // Do nothing
        return default;
    }

    public static long GetSysSp()
    {
        // Do nothing
        return default;
    }



    public static void FlushCache()
    {
        // Do nothing
    }

    public static void Exception()
    {
        // Do nothing
    }

    public static void ReturnFromException()
    {
        // Do nothing
    }

    public static void SwEnterCriticalSection()
    {
        // Do nothing
    }

    public static void SwExitCriticalSection()
    {
        // Do nothing
    }

    // EnterCriticalSection / ExitCriticalSection ARE NOT HERE. They already exist as
    // Kernel.EnterCriticalSection / Kernel.ExitCriticalSection (Kernel.cs), which TITLE_EXE_exe
    // already calls, and duplicating them in LibApi makes every unqualified call site ambiguous.
    // Recording the evidence at the site the calls come from, since Kernel.cs carries none:
    // unlike their neighbours these two are NOT jump stubs — their bodies are in the SELECT.EXE
    // image, three instructions each, read with read-memory:
    //     EnterCriticalSection @ 0x8004EB04 (SELECT.EXE)
    //         24 04 00 01   li a0, 1
    //         00 00 00 0C   syscall 0
    //         03 E0 00 08   jr ra
    //     ExitCriticalSection  @ 0x8004EE04 (SELECT.EXE)
    //         24 04 00 02   li a0, 2
    //         00 00 00 0C   syscall 0
    //         03 E0 00 08   jr ra
    // i.e. syscall(1) / syscall(2) — mask and unmask interrupts. Call sites in SELECT.EXE:
    // main @ 0x8003045C lines 9/17, FUN_80022348 @ 0x80022348 (bracketing its eight OpenEvent
    // calls) and FUN_80021D34 @ 0x80021D34 (bracketing its eight CloseEvent calls) — that is the
    // kernel event table being protected from the ISR that would otherwise deliver into it.
    // The empty desktop body is correct for this port: there is no interrupt to mask, and every
    // DeliverEvent here is issued synchronously by the game thread from _card_info /
    // _card_clear / _card_load, so the mask has nothing to protect against.



    public static ulong Krom2RawAdd(ushort sjiscode)
    {
        // Do nothing
        return default;
    }

    public static ulong Krom2RawAdd2(ushort sjiscode)
    {
        // Do nothing
        return default;
    }



    public static void SystemError(char c, long n)
    {
        // Do nothing
    }

    public static void _96_init()
    {
        // Do nothing
    }

    public static void _96_remove()
    {
        // Do nothing
    }

    public static void _boot()
    {
        // Do nothing
    }

    public static int _get_errno()
    {
        // Do nothing
        return default;
    }

    public static int _get_error(int fd)
    {
        // Do nothing
        return default;
    }
}