using System;

namespace PsxSdkMonogame;

public static class LibEtc
{
    // GHIDRA: DAT_8009568c @ 0x8009568c
    // The libetc V-BLANK callback registered through VSyncCallback @0x80073d24. The V-BLANK
    // interrupt handler @0x8007440c increments DAT_800956ac and then invokes every non-null entry
    // of the callback table based at 0x8009568c (8 slots, 0x8007442c-0x8007445c).
    // PARTIAL: only one callback is modelled, not the 8-slot table — SLUS_006.62 registers exactly
    // one (Sound.VSyncCallbackHandler, SLUS_006_62.cs:448) and VSyncCallbacks() is never called.
    public static Action g_vsyncCallback;

    // GHIDRA: DAT_80094580 @ 0x80094580 — DAT_800956ac's value as of the last blocking VSync()
    // return (written at 0x80073b7c). VSync(0) and VSync(n>=2) compute their wait target relative
    // to this, not to "now": VSync(n) waits until n retraces have elapsed since the previous
    // blocking VSync call.
    // COMMENT CORRECTED 2026-07-28 (was "VSync callback counter" — that is DAT_800956ac).
    public static int g_lastBlockingVsyncCount;

    // GHIDRA: DAT_800956ac @ 0x800956ac — the V-BLANK interrupt counter. Zeroed by startIntrVSync
    // @0x800743b4 (0x800743d8 `sw zero,0x56ac(at)`) and incremented once per vertical-retrace
    // interrupt by the libetc V-BLANK handler @0x8007440c (0x80074430/0x80074438). This is the
    // value VSync(-1) returns.
    public static int g_vblankCounter;

    // GHIDRA: VSync @ 0x80073a44
    // JUSTIFICATION: PSX hardware adaptation only
    // RELATION: adapter for the libetc VSync observable contract. The console function reads the
    // H-retrace timer and the V-BLANK interrupt counter DAT_800956ac; desktop has neither, so a
    // vertical retrace is modelled by FrameBaton.YieldToHost() — one real host frame, the single
    // yield point that lets the translittered runtime's blocking loops (Func_801909b4 and friends)
    // run one frame of work per host Draw() call instead of running to completion inside one
    // Draw(). See docs/plan-vsync-baton-thread-2026-07-23.md and FrameBaton's own remarks.
    //
    // The mode dispatch below is read off 0x80073a44 instruction by instruction:
    //   mode <  0  -> return DAT_800956ac, NO wait        (0x80073a9c bgez / 0x80073aa8 lw)
    //   mode == 1  -> return the elapsed count, NO wait   (0x80073ab8 beq -> 0x80073ba4)
    //   mode == 0  -> target = DAT_80094580               (0x80073ae0)
    //   mode >= 2  -> target = DAT_80094580 + mode - 1    (0x80073ac8-0x80073adc)
    //   both waiting cases then: wait while DAT_800956ac < target (0x80073bbc), then ONE more
    //   unconditional retrace (0x80073b18 waits for DAT_800956ac + 1), then
    //   DAT_80094580 = DAT_800956ac (0x80073b7c).
    // That "wait, then one more" is why VSync(0) is exactly one retrace and VSync(n) is n retraces
    // since the previous blocking VSync call. The GPU-idle spin (0x80073b20-0x80073b60) and the
    // watchdog/timeout argument of 0x80073bbc are hardware recovery with no desktop equivalent.
    public static int VSync(int mode)
    {
        if (mode < 0)
        {
            return g_vblankCounter;
        }

        if (mode == 1)
        {
            // PARTIAL: the console returns (H-retrace timer - DAT_8009457c) & 0xffff, i.e. the time
            // elapsed inside the current field. There is no desktop equivalent of the H-retrace
            // timer, and no caller in the ported runtime reads this value — every VSync call site
            // whose result is used passes -1. Left at 0 rather than invented.
            return 0;
        }

        int target = 0 < mode ? g_lastBlockingVsyncCount + mode + -1 : g_lastBlockingVsyncCount;
        while (g_vblankCounter < target)
        {
            WaitVBlankInterrupt();
        }

        WaitVBlankInterrupt();
        g_lastBlockingVsyncCount = g_vblankCounter;
        return 0;
    }

    // JUSTIFICATION: PSX hardware adaptation only
    // RELATION: adapter for the libetc V-BLANK interrupt handler @0x8007440c, which increments
    // DAT_800956ac and then calls the registered callback(s). On console that handler is driven by
    // the interrupt and runs whether or not the game is inside VSync(); on desktop the game thread
    // only ever gives up control inside VSync, and FrameBaton guarantees exactly one YieldToHost
    // per host Draw(), so one host frame IS one vertical retrace. Increment-then-callback order and
    // the position after the present both match the console handler.
    private static void WaitVBlankInterrupt()
    {
        FrameBaton.YieldToHost();

        // The BIOS pad driver (LibApi InitPAD/StartPAD — SELECT.EXE's input path, see the block
        // comment there) samples the controllers off this same interrupt and refreshes the status
        // buffers it was handed. Placed immediately after the yield so the buffers hold the
        // sample the host just took, and before the V-BLANK callback, which on console also runs
        // after the driver's own handler. It is a no-op until StartPAD has been called, so
        // TITLE.EXE's libetc PadRead path below is untouched.
        LibApi.RefreshBiosPadBuffers();

        g_vblankCounter++;
        g_vsyncCallback?.Invoke();
    }

    public static int VSyncCallback(Action callback)
    {
        // PARTIAL: PSX SDK function @0x80073d24. Sets/clears the V-BLANK interrupt callback; the
        // console dispatches through a vtable (PTR_PTR_8009566c + 0x14) into the 0x8009568c table.
        g_vsyncCallback = callback;
        return 0;
    }

    public static int VSyncCallbacks(int ch, Action callback)
    {
        // Do nothing
        return default;
    }

    // GHIDRA: DMACallback @ 0x80073CF4
    // PROOF: CERTAIN interface (channel index + callback, returns the previous callback) — real
    // per-channel registry, replacing the prior no-op. 7 channels (0=MDEC-in, 1=MDEC-out, 2=GPU,
    // 3=CDROM, 4=SPU, 5=PIO, 6=OTC — the standard PSX DMA channel assignment), matching libetc's own
    // DMACallback table size. Added for slice S2 (LibPress.DecDCTin/DecDCTout register/invoke
    // channel 0/1 through this — see LibPress.cs's DecDCTinCallback/DecDCToutCallback). No call site
    // in the ported game exists yet (grepped clean at slice S2 time), so this change is behaviourally
    // inert for every currently-live path; it only starts mattering once LibPress or a future FMV
    // driver slice calls it.
    private static readonly Action[] s_dmaCallbacks = new Action[7];

    public static object DMACallback(int dma, Action callback)
    {
        if (dma < 0 || dma >= s_dmaCallbacks.Length)
        {
            return null;
        }

        Action previous = s_dmaCallbacks[dma];
        s_dmaCallbacks[dma] = callback;
        return previous;
    }

    // JUSTIFICATION: desktop adaptation helper — lets LibPress invoke the DMA channel-1 (MDEC-out)
    // callback registered above without duplicating the registry. Not part of the original libetc
    // API surface (the original only exposes registration; invocation is the IRQ handler's job,
    // which has no desktop equivalent — see LibPress.DecDCTout for the synchronous adaptation).
    public static Action GetDmaCallback(int dma)
    {
        return dma >= 0 && dma < s_dmaCallbacks.Length ? s_dmaCallbacks[dma] : null;
    }

    public static int ResetCallback()
    {
        // Do nothing
        return default;
    }

    public static int StopCallback()
    {
        // Do nothing
        return default;
    }

    public static int RestartCallback()
    {
        // Do nothing
        return default;
    }

    public static int CheckCallback()
    {
        // Do nothing
        return default;
    }

    public static long GetVideoMode()
    {
        // Do nothing
        return default;
    }

    public static long SetVideoMode(long mode)
    {
        // Do nothing
        return default;
    }

    // JUSTIFICATION: PSX hardware adaptation only
    // RELATION: mirrors the u_long buffer that PadInit hands to the BIOS PAD_init vector and that
    // PAD_dr refreshes. Active-low exactly like the hardware, so a set bit means released, and
    // port 1 occupies the low halfword.
    //
    // Both facts are closed, not assumed. Verified live in PCSX-Redux while TITLE.EXE was the
    // resident overlay: its buffer at 0x800920D4 reads 0xFFFFFFFF at rest and 0xFFFFF7FF with
    // Start held, which puts Start at 0x0800 in the low halfword. That is also what makes the FMV
    // players' `PadRead(1) & 0x800` test read as Start.
    private static uint s_padBuffer = 0xFFFFFFFF;

    // JUSTIFICATION: PSX hardware adaptation only
    // RELATION: PadInit stores its mode argument next to the buffer; kept so the observable state
    // matches even though no desktop path reads it back yet.
    private static int s_padMode;

    // JUSTIFICATION: PSX hardware adaptation only
    // RELATION: stands for the BIOS pad driver being installed. PadStop tears it down, and the
    // next overlay reinstalls it through PadInit, which is the sequence every
    // ShutdownAndLoadExecutable performs.
    private static bool s_padActive;

    // GHIDRA: PadInit @ 0x8002B850 (SLPS_003.55), @ 0x8006FDA0 (TITLE.EXE)
    public static void PadInit(int mode)
    {
        s_padBuffer = 0xFFFFFFFF;
        s_padMode = mode;
        ResetCallback();
        s_padActive = true;
    }

    // JUSTIFICATION: PSX hardware adaptation only
    // RELATION: desktop stand-in for the BIOS PAD_dr vector, which refreshes the installed buffer
    // from the controller. It consumes the snapshot the host sampled this frame instead of
    // touching MonoGame from the runtime thread.
    private static void PAD_dr()
    {
        if (s_padActive)
        {
            s_padBuffer = PadInputBackend.PublishedActiveLow;
        }
    }

    // GHIDRA: PadRead @ 0x8002B8A0 (SLPS_003.55), @ 0x8006FDF0 (TITLE.EXE)
    // The original ignores its id argument: it refreshes the shared buffer and returns the whole
    // one's complement, both ports at once.
    public static uint PadRead(int id)
    {
        PAD_dr();
        return ~s_padBuffer;
    }

    // GHIDRA: PadStop @ 0x8002B8D0 (SLPS_003.55), @ 0x8006FE20 (TITLE.EXE)
    public static void PadStop()
    {
        s_padActive = false;
        s_padBuffer = 0xFFFFFFFF;
    }

    public static void PadInitDirect(byte pad1, byte pad2)
    {
        // Do nothing
    }

    public static void PadInitMtap(byte pad1, byte pad2)
    {
        // Do nothing
    }

    public static void PadInitGun(byte buff, int size)
    {
        // Do nothing
    }

    public static void PadStartCom()
    {
        // Do nothing
    }

    public static void PadStopCom()
    {
        // Do nothing
    }

    public static byte PadEnableCom(byte mode)
    {
        // Do nothing
        return default;
    }

    public static int PadChkVsync()
    {
        // Do nothing
        return default;
    }

    // JUSTIFICATION: PSX hardware adaptation — this port has no real pad-driver state machine, but
    // returning 0 here is NOT neutral: 0 is PadStateDiscon, and the game reads it as "the controller
    // was unplugged". ProcessPadInput's disconnect arm (Input.cs) then INJECTS a Start press
    // (g_buttonsPressed = 4, the pad-table entry for raw bit 0x8) so the main loop pauses the game,
    // which is what the console does when you pull the controller mid-game. With the stub at 0 that
    // arm was permanently armed, held off only by g_padStatus bit 0x4000: any code clearing that bit
    // while unpaused produced a phantom pause on the very next frame — DeserializeSaveData does
    // exactly that (its `g_padStatus &= 0xffff2679` drops 0x4000), so loading a save paused the game,
    // and script opcode 0x53 can do the same.
    // PadStateStable (6) is the truthful answer for this port: the desktop bridge in
    // Remaster/PsxSystems/Input.cs always presents a connected digital pad through s_gamePad1.
    // Checked against all three arms that consume this value (Input.cs, the g_padStatus & 0x4000
    // block): 6 leaves 0x4000 set and falls through PadInfoMode (itself a stub returning 0), so the
    // analog-mode negotiation is skipped, which is correct for a digital pad. Note 2
    // (PadStateFindCTP1) would be the worst possible answer — it CLEARS 0x4000 and so re-arms the
    // very injection this fixes.
    private const int PadStateStable = 6;

    public static int PadGetState(int port)
    {
        return PadStateStable;
    }

    public static int PadInfoAct(int port, int actno, int term)
    {
        // Do nothing
        return default;
    }

    public static int PadInfoComb(int port, int listno, int offs)
    {
        // Do nothing
        return default;
    }

    public static int PadInfoMode(int port, int term, int offs)
    {
        // Do nothing
        return default;
    }

    public static void PadSetAct(int port, byte data, int len)
    {
        // Do nothing
    }

    public static int PadSetActAlign(int port, char data)
    {
        // Do nothing
        return default;
    }

    public static int PadSetMainMode(int port, int offs, int @lock)
    {
        // Do nothing
        return default;
    }

    public static void PadEnableGun(byte mask)
    {
        // Do nothing
    }

    public static void PadRemoveGun()
    {
        // Do nothing
    }

    public static void InitTAP(char bufA, long lenA, char bufB, long lenB)
    {
        // Do nothing
    }

    public static void StartTAP()
    {
        // Do nothing
    }

    public static void StopTAP()
    {
        // Do nothing
    }

    public static void EnableTAP()
    {
        // Do nothing
    }

    public static void DisableTAP()
    {
        // Do nothing
    }


    public static void InitGUN(char bufA, long lenA, char bufB, long lenB, char buf0,
        char buf1, long len)
    {
        // Do nothing
    }

    public static long StartGUN()
    {
        // Do nothing
        return default;
    }

    public static void StopGUN()
    {
        // Do nothing
    }

    public static void SelectGUN(int ch, byte mask)
    {
        // Do nothing
    }

    public static void RemoveGUN()
    {
        // Do nothing
    }
}