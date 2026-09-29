// SaveOnShutdown: a `docker stop` saves the world before the shard exits (bug-list D36).
//
// Without this, a stop loses everything since the last autosave (up to five minutes). What
// pinned does on SIGTERM, all at 7c9215d97, with the proof in
// shard-migration/notes/cc-P10-updater-polish-and-d36.md:
//
//   .NET raises AppDomain.ProcessExit, whose handler is HandleClosed (Server/Main.cs:271,
//   :333-348). It waits for a write already in progress and saves nothing, and it runs after
//   the event loop has stopped, so a World.Save() made there could never finish: Save only
//   queues work (World/World.cs:241-252) and the snapshot runs ON the event loop
//   (Main.cs:483-488). EventSink.Shutdown is later still, after ExitSerializationThreads.
//
// So the save happens BEFORE that path: a PosixSignalRegistration for SIGTERM cancels the
// runtime's default handling and asks the event loop to save, then stops the shard the way
// pinned's own "shutdown with save" does (UOContent/Gumps/AdminGump.cs:4015-4034): World.Save
// on the loop, then from the thread pool WaitForWriteCompletion and Core.Kill. Kill ends the
// loop, and the normal exit (Main.cs:519, DoKill -> HandleClosed) follows.
//
// Once only. A second SIGTERM changes nothing. If a save is already under way World.Save does
// nothing (World.cs:243), and the wait below is for that save instead.
//
// Left exactly as before, with the default handling not cancelled:
//   a crash       EventSink.ServerCrashed (Main.cs:252) sets _crashed first. A crashed shard's
//                 loop is gone, so a save could never run.
//   not running   world still loading, or already closing.
// And if the loop has not picked the request up within LoopTimeout (a hung loop), it gives up
// and exits the way the default would have, through ProcessExit, with no save.
//
// THE STOP TIMEOUT MATTERS. Writing a save moves the old save into Backups and the new one
// into Saves file by file (AutoArchive.cs Backup; PathUtility.MoveDirectoryContents). A SIGKILL
// in the middle leaves Saves half-moved. Give the stop at least 60 s: the console does
// (`docker stop -t 60`), but a bare `docker stop` uses the container's StopTimeout, which was
// 1 s on 2026-09-29, and the live world's write took up to 32 s in the captured log.
//
// Logged with Console.WriteLine, not the logger: Serilog's console sink is asynchronous
// (Server/Logger/LogFactory.cs:25) and nothing flushes it on exit, so a line logged this late
// can be lost. "Shutting down" is, on every stop.

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace Server.Misc;

public static class SaveOnShutdown
{
    public static readonly TimeSpan LoopTimeout = TimeSpan.FromSeconds(5);
    public const string RegisteredLine = "listening for SIGTERM: a stop saves the world first (D36)";

    // Kept in a field: a PosixSignalRegistration unregisters itself when it is collected.
    private static PosixSignalRegistration _sigterm;
    private static volatile bool _crashed;
    private static int _requested;      // 0 none, 1 asked the loop to save
    private static volatile bool _started;

    public static void Configure()
    {
        EventSink.ServerCrashed += _ => _crashed = true;

        try
        {
            _sigterm = PosixSignalRegistration.Create(PosixSignal.SIGTERM, OnSigterm);
            // scripts/Shard-Console.ps1 reads this exact line to know that a stop of this
            // container saves. Change both together.
            Say(RegisteredLine);
        }
        catch (Exception e) when (e is PlatformNotSupportedException or IOException)
        {
            Say($"cannot listen for SIGTERM here ({e.GetType().Name}); a stop will not save.");
        }
    }

    private static void OnSigterm(PosixSignalContext context)
    {
        if (_crashed || Core.Closing || !World.Running)
        {
            return;
        }

        context.Cancel = true;

        if (Interlocked.Exchange(ref _requested, 1) != 0)
        {
            return;
        }

        Core.LoopContext.Post(SaveThenStop);

        ThreadPool.QueueUserWorkItem(
            _ =>
            {
                Thread.Sleep(LoopTimeout);
                if (!_started)
                {
                    Say($"the server loop did not answer within {LoopTimeout.TotalSeconds:F0} s; stopping without a save.");
                    Environment.Exit(143);
                }
            }
        );
    }

    // On the event loop.
    private static void SaveThenStop()
    {
        _started = true;
        Say("Saving the world before shutdown...");
        var watch = Stopwatch.StartNew();
        World.Save();

        ThreadPool.QueueUserWorkItem(
            _ =>
            {
                World.WaitForWriteCompletion();
                Say($"...saved in {watch.ElapsedMilliseconds} ms");
                Core.Kill();
            }
        );
    }

    private static void Say(string text)
    {
        Console.WriteLine($"[SaveOnShutdown] {text}");
        Console.Out.Flush();
    }
}
