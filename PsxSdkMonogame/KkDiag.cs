using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;

namespace PsxSdkMonogame;

// JUSTIFICATION: backend MonoGame only (diagnostic tooling, not part of the ported runtime)
// RELATION: KK1 (2026-08-21) — a temporary, env-gated, FILE-based logger for the in-game
// silence-correlation harness. The port is a WinExe: a live capture launched without an attached
// console loses plain Console.WriteLine, so this writes to a file under PE_KK_DIAG (a directory
// path) instead — same convention this dossier's own driving-the-monogame-port note already
// established. Inert unless PE_KK_DIAG is set. Every call site this touches is a single added
// line; no behaviour changes. Intended to be removed once KK1's correlation is done, same as this
// dossier's other temporary probes (PE_PITCH_DIAG, etc.).
//
// MM1 (2026-08-21, coordinator): Log() used to call File.AppendAllText synchronously on every call
// — open+write+close a file on disk — from whatever thread called it, which post-LL1 is the audio
// pump thread for every site this class instruments (FUN_8008900c fires hundreds of times/second
// when busy). That blocking I/O on the audio thread was flagged (LL3) as the leading candidate for
// the residual ~2s gaps LL2's witnesses still showed. Fixed here: Log() only enqueues a formatted
// string into a lock-free ConcurrentQueue and returns — zero I/O on the calling (audio) thread. A
// single lazily-started background thread (IsBackground, so it never blocks process exit) drains
// the queue and appends to one FileStream kept open for the process lifetime, flushed periodically
// rather than reopened per line.
public static class KkDiag
{
    private static readonly string s_dir = Environment.GetEnvironmentVariable("PE_KK_DIAG");
    private static readonly ConcurrentQueue<string> s_queue = new ConcurrentQueue<string>();
    private static System.Diagnostics.Stopwatch s_sw;
    private static Thread s_writerThread;
    private static readonly object s_startLock = new object();

    public static bool Enabled => !string.IsNullOrEmpty(s_dir);

    public static void Log(string msg)
    {
        if (!Enabled) return;

        if (s_sw == null)
        {
            lock (s_startLock)
            {
                if (s_sw == null)
                {
                    s_sw = System.Diagnostics.Stopwatch.StartNew();
                    s_writerThread = new Thread(WriterLoop) { IsBackground = true, Name = "KkDiagWriter" };
                    s_writerThread.Start();
                }
            }
        }

        // JUSTIFICATION: C# language bridge only — formatting the timestamped line here (not in the
        // writer thread) keeps the elapsed time accurate to the moment the event actually happened,
        // not the moment the background thread got around to draining it.
        s_queue.Enqueue($"[{s_sw.Elapsed.TotalSeconds:F2}s] {msg}");
    }

    // JUSTIFICATION: backend MonoGame only — the background drain thread. Never touches SpuCore/Akao
    // state, only reads already-formatted strings off the queue; the only I/O here is the file write,
    // and it happens exclusively on this thread, never on the caller's (audio pump) thread.
    private static void WriterLoop()
    {
        Directory.CreateDirectory(s_dir);
        string path = Path.Combine(s_dir, $"kk1_{DateTime.Now:yyyyMMdd_HHmmss}.log");
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        using var sw = new StreamWriter(fs);

        while (true)
        {
            bool wroteAny = false;
            while (s_queue.TryDequeue(out string line))
            {
                sw.WriteLine(line);
                wroteAny = true;
            }
            if (wroteAny)
                sw.Flush();
            Thread.Sleep(50);
        }
    }
}
