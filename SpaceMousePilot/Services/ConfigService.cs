using System.IO;
using System.Text.Json;
using SpaceMousePilot.Models;

namespace SpaceMousePilot.Services;

internal static class ConfigService
{
    private static readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SpaceMousePilot", "config.json");

    private static readonly JsonSerializerOptions _opts = new() { WriteIndented = true };

    public static string FolderPath => Path.GetDirectoryName(_path)!;

    public static AppConfig Load()
    {
        if (!File.Exists(_path)) return new AppConfig();
        try
        {
            var saved = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(_path), _opts);
            if (saved is null) return new AppConfig();

            var defaults = new AppConfig();
            foreach (var (key, def) in defaults.Axes)
            {
                if (!saved.Axes.TryGetValue(key, out var ax))
                    saved.Axes[key] = def;
                else if (ax.Scale <= 0)
                    ax.Scale = def.Scale;
            }
            return saved;
        }
        catch (Exception ex)
        {
            Logger.Warn("config", $"Load failed, using defaults: {ex.Message}");
            return new AppConfig();
        }
    }

    public static void Save(AppConfig config)
    {
        try
        {
            Directory.CreateDirectory(FolderPath);
            File.WriteAllText(_path, JsonSerializer.Serialize(config, _opts));
        }
        catch (Exception ex) { Logger.Error("config", ex); }
    }
}
