using System.Collections.ObjectModel;
using Avalonia.Threading;

namespace Zenkei.Services;

public record LogEntry(DateTime Time, string Level, string Message);

/// <summary>
/// Application-wide event log. Thread-safe: posts to the UI thread if called off it.
/// </summary>
public static class AppLog
{
    public static ObservableCollection<LogEntry> Entries { get; } = [];

    public static void Info(string message) => Add("INFO", message);
    public static void Warn(string message) => Add("WARN", message);
    public static void Error(string message) => Add("ERROR", message);

    private static void Add(string level, string message)
    {
        var entry = new LogEntry(DateTime.Now, level, message);
        if (Dispatcher.UIThread.CheckAccess())
            Entries.Add(entry);
        else
            Dispatcher.UIThread.Post(() => Entries.Add(entry));
    }
}
