using System;
using System.Collections.Generic;

namespace PsxSdkMonogame;

public static class LibApi
{
    // JUSTIFICATION: PSX hardware adaptation — the port's model of kernel event delivery for the
    // memory-card events. On console the libapi card functions post kernel events (event class +
    // spec value) and the handlers the game registered through OpenEvent react to them; this port
    // has no kernel event table, so the game installs a sink here and LibApi posts the same
    // (class, spec) pairs the console would deliver. Not installed -> events are dropped.
    public static Action<uint, uint> CardEventSink;

    private static void DeliverCardEvent(uint eventClass, uint spec)
    {
        CardEventSink?.Invoke(eventClass, spec);
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


    public static long InitPAD(char[] bufA, long lenA, char[] bufB, long lenB)
    {
        // Do nothing
        return default;
    }

    public static long StartPAD()
    {
        // Do nothing
        return default;
    }

    public static void StopPAD()
    {
        // Do nothing
    }

    public static void ChangeClearPAD(long val)
    {
        // Do nothing
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

    // JUSTIFICATION: backend MonoGame / PSX hardware adaptation
    // RELATION: OpenEvent's real BIOS registers (class, spec, mode, callback) without arming
    // anything; EnableEvent is what arms it. The one caller that matters here is
    // InitSpuAndTimerEvent's `OpenEvent(0xf2000002, 2, 0x1000, FUN_8008e23c)` — root counter 2, the
    // Akao tick. For exactly that (class, spec) pair, EnableEvent below wires the callback onto
    // PsxSdk.LibSpu.Spu's tick schedule (SpuCore.SetTickCallback) and starts LibSpu.AudioBackend, so
    // the ISR the original ran off a hardware timer now runs off the audio pump instead (the user's
    // own clocking decision for this tranche). DisableEvent/CloseEvent undo it — ShutdownAkaoSystem
    // already calls both. Every other (class, spec) pair is unaffected; this table just remembers
    // what OpenEvent was called with so Enable/DisableEvent can look it up by handle.
    private static readonly Dictionary<long, (long evClass, long spec, System.Action callback)> s_openEvents = new();
    private static long s_nextEventHandle = 1;

    public static long OpenEvent(long ev, long spec, long mode, System.Action func)
    {
        long handle = s_nextEventHandle++;
        s_openEvents[handle] = (ev, spec, func);
        return handle;
    }

    public static long CloseEvent(long @event) {
        s_openEvents.Remove(@event);
        return BIOS_SUCCESS;
    }

    public static void DeliverEvent(ulong ev1, ulong ev2)
    {
        // Do nothing
    }

    public static void UnDeliverEvent(ulong ev1, ulong ev2)
    {
        // Do nothing
    }

    public static long TestEvent(long @event) {
        // Do nothing
        return default;
    }

    public static long WaitEvent(long @event) {
        // Do nothing
        return default;
    }

    public static long EnableEvent(long @event) {
        // Do nothing for events OpenEvent was not called on — see BIOS_SUCCESS above.
        if (s_openEvents.TryGetValue(@event, out var e) && e.evClass == unchecked((long)0xf2000002) &&
            e.spec == 2 && e.callback != null)
        {
            LibSpu.Spu.SetTickCallback(e.callback);
            LibSpu.AudioBackend.Start();
        }
        return BIOS_SUCCESS;
    }

    public static long DisableEvent(long @event) {
        // Do nothing for events OpenEvent was not called on — see BIOS_SUCCESS above.
        if (s_openEvents.TryGetValue(@event, out var e) && e.evClass == unchecked((long)0xf2000002) &&
            e.spec == 2)
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