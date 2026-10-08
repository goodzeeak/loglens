using System.IO;
using System.Text.Json;

namespace LogLens.App;

public sealed record AppSettings(int Days = 7, string Theme = "Dark")
{
    private static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Goodwin Labs", "LogLens", "settings.json");
    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath) || new FileInfo(FilePath).Length > 8192) return new();
            var value = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath));
            return value is { Days: 1 or 7 or 30, Theme: "Dark" or "Light" } ? value : new();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return new(); }
    }
    public bool Save()
    {
        try { Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!); File.WriteAllText(FilePath, JsonSerializer.Serialize(this)); return true; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }
}
