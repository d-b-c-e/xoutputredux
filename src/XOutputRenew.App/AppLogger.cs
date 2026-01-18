using System.Collections.Concurrent;
using System.IO;
using System.Runtime.CompilerServices;

namespace XOutputRenew.App;

/// <summary>
/// Async buffered file logger for debugging.
/// Uses background thread to batch writes for performance.
/// </summary>
public static class AppLogger
{
    private static readonly string LogPath = Path.Combine(
        AppPaths.Logs,
        $"xoutputrenew-{DateTime.Now:yyyy-MM-dd}.log");

    private static readonly ConcurrentQueue<string> LogQueue = new();
    private static readonly AutoResetEvent LogSignal = new(false);
    private static Thread? _writerThread;
    private static volatile bool _initialized;
    private static volatile bool _shutdown;

    private const int FlushIntervalMs = 100; // Batch writes every 100ms
    private const int MaxBatchSize = 500;    // Max lines per batch

    public static void Initialize()
    {
        if (_initialized) return;

        try
        {
            var dir = Path.GetDirectoryName(LogPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            _initialized = true;
            _shutdown = false;

            // Start background writer thread
            _writerThread = new Thread(WriterLoop)
            {
                Name = "LogWriter",
                IsBackground = true,
                Priority = ThreadPriority.BelowNormal
            };
            _writerThread.Start();

            Log("INFO", "Application started (async logging)");
        }
        catch
        {
            // Ignore initialization errors
        }
    }

    public static void Shutdown()
    {
        if (!_initialized) return;

        _shutdown = true;
        LogSignal.Set(); // Wake up writer thread

        // Wait for writer to finish (with timeout)
        _writerThread?.Join(1000);

        // Flush any remaining messages synchronously
        FlushRemaining();
    }

    public static void Info(string message, [CallerMemberName] string? caller = null)
    {
        Log("INFO", message, caller);
    }

    public static void Warning(string message, [CallerMemberName] string? caller = null)
    {
        Log("WARN", message, caller);
    }

    public static void Error(string message, Exception? ex = null, [CallerMemberName] string? caller = null)
    {
        if (ex != null)
        {
            Log("ERROR", $"{message}\n  Exception: {ex.GetType().Name}: {ex.Message}\n  StackTrace: {ex.StackTrace}", caller);
        }
        else
        {
            Log("ERROR", message, caller);
        }
    }

    private static void Log(string level, string message, string? caller = null)
    {
        if (!_initialized) return;

        try
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var callerInfo = caller != null ? $"[{caller}] " : "";
            var line = $"{timestamp} [{level}] {callerInfo}{message}";

            LogQueue.Enqueue(line);

            // Signal writer thread if queue is getting large
            if (LogQueue.Count > MaxBatchSize)
            {
                LogSignal.Set();
            }
        }
        catch
        {
            // Ignore logging errors
        }
    }

    private static void WriterLoop()
    {
        try
        {
            // Keep file open for better performance
            using var writer = new StreamWriter(LogPath, append: true)
            {
                AutoFlush = false
            };

            while (!_shutdown)
            {
                // Wait for signal or timeout
                LogSignal.WaitOne(FlushIntervalMs);

                // Write batch of messages
                int written = 0;
                while (written < MaxBatchSize && LogQueue.TryDequeue(out var line))
                {
                    writer.WriteLine(line);
                    written++;
                }

                if (written > 0)
                {
                    writer.Flush();
                }
            }

            // Final flush on shutdown
            while (LogQueue.TryDequeue(out var line))
            {
                writer.WriteLine(line);
            }
            writer.Flush();
        }
        catch
        {
            // Ignore writer errors
        }
    }

    private static void FlushRemaining()
    {
        try
        {
            if (LogQueue.IsEmpty) return;

            using var writer = new StreamWriter(LogPath, append: true);
            while (LogQueue.TryDequeue(out var line))
            {
                writer.WriteLine(line);
            }
        }
        catch
        {
            // Ignore flush errors
        }
    }

    /// <summary>
    /// Gets the path to the current log file.
    /// </summary>
    public static string GetLogPath() => LogPath;
}
