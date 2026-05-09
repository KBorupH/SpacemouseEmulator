using System.IO;

namespace SpaceMousePilot.Services;

internal static class Logger
{
    private static readonly object _lock = new();

    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SpaceMousePilot", "spacemouse_pilot.log");

    static Logger()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        Info("main", new string('=', 60));
        Info("main", $"SpaceMouse Pilot {AppVersion.Current} starting");
    }

    public static void Info(string src, string msg)  => Write("INFO ", src, msg);
    public static void Debug(string src, string msg) => Write("DEBUG", src, msg);
    public static void Warn(string src, string msg)  => Write("WARN ", src, msg);
    public static void Error(string src, string msg) => Write("ERROR", src, msg);
    public static void Error(string src, Exception ex) => Write("ERROR", src, $"{ex.Message}\n{ex.StackTrace}");

    private static void Write(string level, string src, string msg)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {src} — {msg}";
        lock (_lock)
        {
            try { File.AppendAllText(FilePath, line + Environment.NewLine); }
            catch { }
        }
    }
}
