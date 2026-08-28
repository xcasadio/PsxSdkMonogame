using System;
using System.Collections.Concurrent;
using System.Threading;

namespace PsxSdkMonogame;

// JUSTIFICATION: backend MonoGame only (diagnostic tooling, not part of the ported runtime)
// RELATION: MM1 (2026-08-21, coordinator) — SpuAudioBackend's throttled PE_AUDIO_DIAG stdout line
// used to call Console.WriteLine directly from the audio pump thread. Console.WriteLine can block
// (e.g. a full/slow pipe when stdout is redirected, as this dossier's own harnesses do), which is
// exactly the kind of hot-path stall LL1's fix depends on not having. This is a minimal fire-and-
// forget async console writer: WriteLine enqueues (lock-free) and returns immediately; a single
// lazily-started background thread (IsBackground, dies with the process) drains the queue to the
// real Console.Out. Used only by diagnostic call sites, never by ported game logic.
public static class AsyncConsole
{
    private static readonly ConcurrentQueue<string> s_queue = new ConcurrentQueue<string>();
    private static Thread s_thread;
    private static readonly object s_startLock = new object();

    public static void WriteLine(string line)
    {
        if (s_thread == null)
        {
            lock (s_startLock)
            {
                if (s_thread == null)
                {
                    s_thread = new Thread(DrainLoop) { IsBackground = true, Name = "AsyncConsoleWriter" };
                    s_thread.Start();
                }
            }
        }
        s_queue.Enqueue(line);
    }

    private static void DrainLoop()
    {
        while (true)
        {
            bool wroteAny = false;
            while (s_queue.TryDequeue(out string line))
            {
                Console.WriteLine(line);
                wroteAny = true;
            }
            if (!wroteAny)
                Thread.Sleep(20);
        }
    }
}
