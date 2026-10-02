using System.IO;
using System.Text.Json;

namespace Jotter;

public sealed class JotterConfig
{
    public string Hotkey { get; set; } = "Ctrl+Alt+N";
    public string? NotesFolder { get; set; }
    public double WindowWidth { get; set; } = 480;
    public double WindowHeight { get; set; } = 240;

    public string EffectiveNotesFolder =>
        string.IsNullOrWhiteSpace(NotesFolder)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Jotter")
            : Environment.ExpandEnvironmentVariables(NotesFolder.Trim());

    private static string ConfigPath()
    {
        var dir = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
        return Path.Combine(dir, "settings.json");
    }

    public static JotterConfig LoadOrDefault()
    {
        var cfg = new JotterConfig();
        try
        {
            var path = ConfigPath();
            if (!File.Exists(path))
            {
                File.WriteAllText(path, JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true }));
                return cfg;
            }
            cfg = JsonSerializer.Deserialize<JotterConfig>(File.ReadAllText(path)) ?? cfg;
        }
        catch
        {
            // 配置坏了就用默认值，不挡启动
        }
        return cfg;
    }
}
