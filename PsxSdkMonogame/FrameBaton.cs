using System;
using System.Threading;

namespace PsxSdkMonogame;

// JUSTIFICATION: PSX hardware adaptation only — see docs/plan-vsync-baton-thread-2026-07-23.md.
//
// Adapts the PSX vertical-retrace wait (VSync) to a desktop host frame loop. On console, VSync(n)
// blocks until the next real vertical blank, which is also how the original game yields control
// back to the display hardware between frames — every blocking loop in the translittered runtime
// (Func_801909b4's retry loop, its 1000-frame main loop, RunPublisherSplashSequence's 480-frame
// loop, the memory-card poll loop, etc.) relies on this to eventually let a new frame of
// input/display state through. Desktop VSync (LibEtc.cs) is a no-op, so without this
// adapter those loops would run to completion inside a single MonoGame Draw() call with no yield
// back to Update().
//
// This class is a strict two-phase baton between exactly two threads (the game-logic thread running
// the translittered SLUS_006_62.Main(), and the MonoGame host thread): only one side ever runs at a
// time, preserving the original's single-threaded, one-frame-of-work-per-VSync semantics. The game
// thread is not a "background simulation" — it is the same blocking call stack as the original,
// just parked on a semaphore instead of a hardware interrupt wait.
//
// Orientation for Blocage 4 (GPU/render backend, NOT addressed by this class): the strict
// non-concurrency this baton guarantees means "publish the display state" and "actually present it"
// can safely be split across the two sides of the handoff — the game thread finishes writing
// whatever framebuffer/command-buffer state a future LoadImage/PutDispEnv/PutDrawEnv backend
// produces BEFORE calling YieldToHost (i.e. before VSync returns), and the host reads/presents it
// inside WaitFrameReadyAndPresent/ReleaseGame, with no risk of the game thread mutating that state
// concurrently. Concretely: the game thread must NEVER call MonoGame's GraphicsDevice (or anything
// touching the swapchain) directly — any real GPU emission belongs on the host thread, driven from
// inside WaitFrameReadyAndPresent's "present" step, reading state the game thread already finished
// publishing. This class doesn't need changes to support that; it's a property of where the
// GPU-backend code gets plugged in later, not of the baton itself.
public static class FrameBaton
{
    private static readonly SemaphoreSlim FrameReady = new(0, 1);
    private static readonly SemaphoreSlim MayContinue = new(0, 1);
    private static volatile Exception _fault;
    private static volatile bool _shutdown;
    private static volatile bool _runtimeCompleted;

    // JUSTIFICATION: backend MonoGame only — an unhandled exception on the game thread must still
    // surface to the host (this is essential for porting: a swallowed exception on a background
    // thread would silently freeze the game instead of showing the crash). The host polls this
    // once per frame and re-throws/exits.
    public static Exception PendingFault => _fault;

    // JUSTIFICATION: backend MonoGame only — lets the host retain the final published frame when
    // an incremental runtime slice reaches its explicit boundary instead of waiting forever.
    public static bool RuntimeCompleted => _runtimeCompleted;

    // JUSTIFICATION: PSX hardware adaptation only — called from the game-thread wrapper's catch
    // block. Records the fault AND releases the host's wait, so a crash occurring before the game
    // thread ever reaches its first VSync doesn't leave the host deadlocked on
    // WaitFrameReadyAndPresent() forever.
    public static void CaptureFault(Exception exception)
    {
        _fault = exception;
        FrameReady.Release();
    }

    // JUSTIFICATION: backend MonoGame only — wakes a host waiting for the next VSync when the
    // translated runtime returns normally at an intentional incremental-port boundary.
    public static void CompleteRuntime()
    {
        _runtimeCompleted = true;
        FrameReady.Release();
    }

    // JUSTIFICATION: PSX hardware adaptation only — called from VSync (LibEtc.cs). Signals
    // the host that a frame of game-thread work is done, then blocks until the host has presented
    // and releases it for the next frame — the desktop equivalent of waiting for the next vertical
    // blank. Throws GameShutdownException if the host requested shutdown while parked, so the game
    // thread unwinds its call stack cleanly (through the translittered runtime's real call frames)
    // instead of being killed mid-instruction.
    public static void YieldToHost()
    {
        FrameReady.Release();
        MayContinue.Wait();

        if (_shutdown)
        {
            throw new GameShutdownException();
        }
    }

    // JUSTIFICATION: backend MonoGame only — called from GameRemaster.OnExiting (host thread).
    // Requests a clean stop: sets the shutdown flag and, if the game thread is currently parked in
    // YieldToHost, releases it so it observes the flag and unwinds via GameShutdownException instead
    // of leaving the host to Join() a thread that's blocked forever.
    public static void RequestShutdown()
    {
        _shutdown = true;
        MayContinue.Release();
    }

    // JUSTIFICATION: backend MonoGame only — called once per host Draw(); blocks until the game
    // thread has reached its next VSync (a full logical frame of game-thread work is ready).
    public static void WaitFrameReadyAndPresent()
    {
        FrameReady.Wait();
    }

    // JUSTIFICATION: backend MonoGame only — called once per host Draw(), after presenting, to let
    // the game thread compute the next frame.
    public static void ReleaseGame()
    {
        MayContinue.Release();
    }
}

// JUSTIFICATION: backend MonoGame only — private-to-the-baton control-flow signal used to unwind
// the game thread's call stack cleanly on shutdown (see FrameBaton.RequestShutdown/YieldToHost).
// Not a fault: RunGameThread (GameRemaster.cs) catches this separately from real exceptions and
// does not report it via FrameBaton.CaptureFault.
public sealed class GameShutdownException : Exception
{
}
