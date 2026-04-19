using System.Text.Json;
using Microsoft.Win32;

namespace ClipboardCompressor;

public enum CompressionFormat { Png, Jpeg }

public enum CompressorMode
{
    Auto,   // Replace clipboard image with compressed version immediately on copy
    Manual  // Show choice popup on Ctrl+V; show overlay button on right-click
}

public class AppSettings
{
    public bool Enabled { get; set; } = true;
    public CompressorMode Mode { get; set; } = CompressorMode.Auto;
    public CompressionFormat Format { get; set; } = CompressionFormat.Png;
    public int JpegQuality { get; set; } = 85;
    public bool PreserveTransparencyAsPng { get; set; } = true;
    public bool ShowTrayNotifications { get; set; } = true;
    public bool StartWithWindows { get; set; } = false;

    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ClipboardCompressor");

    private static string SettingsPath => Path.Combine(SettingsDir, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch { }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
            ApplyStartWithWindows();
        }
        catch { }
    }

    private void ApplyStartWithWindows()
    {
        const string keyName = "ClipboardCompressor";
        using var key = Registry.CurrentUser.OpenSubKey(
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", writable: true);
        if (key == null) return;
        if (StartWithWindows)
            key.SetValue(keyName, $"\"{Application.ExecutablePath}\"");
        else
            key.DeleteValue(keyName, throwOnMissingValue: false);
    }
}
