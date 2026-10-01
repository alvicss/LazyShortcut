// =========================================================
// 檔案：AppSettings.cs
// 用途：讀取、儲存並轉換 Lazy Shortcut 的使用者設定。
// 誰會用到：DockManager 啟動時載入設定，並在操作後保存設定。
// =========================================================
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace ProjectShortcutDock;

public sealed class AppSettings
{
    // Keep the existing AppData folder so 2.1 upgrades preserve user settings.
    private const string SettingsFolderName = "ProjectShortcutDock";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public string Theme { get; set; } = ThemePalette.Minimal;
    public string? ColorScheme { get; set; }
    public string WindowMode { get; set; } = "Topmost";
    public string Language { get; set; } = "en";
    public string TerminalShell { get; set; } = "cmd";
    public bool StartWithWindows { get; set; }
    public double? Left { get; set; }
    public double? Top { get; set; }
    public double Width { get; set; } = 360;
    public double Height { get; set; } = 260;
    public List<ShortcutItem> Shortcuts { get; set; } = new();
    public List<ShortcutGroup> Groups { get; set; } = new();

    public static string DirectoryPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), SettingsFolderName);

    public static string FilePath => Path.Combine(DirectoryPath, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return CreateFirstRunDefaults();
            }

            var json = File.ReadAllText(FilePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? CreateFirstRunDefaults();
            settings.MigrateAppearanceSettings();
            return settings;
        }
        catch
        {
            return CreateFirstRunDefaults();
        }
    }

    private static AppSettings CreateFirstRunDefaults()
    {
        return new AppSettings
        {
            Theme = ThemePalette.Minimal,
            ColorScheme = ThemePalette.Auto,
            WindowMode = "Topmost",
            Language = DetectDefaultLanguage()
        };
    }

    /// <summary>
    /// 將舊版把亮暗色當成主題名稱的設定，轉成新版「風格＋色系」欄位。
    /// </summary>
    private void MigrateAppearanceSettings()
    {
        if (string.IsNullOrWhiteSpace(ColorScheme))
        {
            ColorScheme = Theme switch
            {
                ThemePalette.Dark => ThemePalette.DarkScheme,
                ThemePalette.Tech => ThemePalette.DarkScheme,
                _ => ThemePalette.Light
            };
        }

        if (Theme.Equals(ThemePalette.Normal, StringComparison.OrdinalIgnoreCase) ||
            Theme.Equals(ThemePalette.Dark, StringComparison.OrdinalIgnoreCase))
        {
            Theme = ThemePalette.Minimal;
        }
        else if (Theme.Equals(ThemePalette.Tech, StringComparison.OrdinalIgnoreCase))
        {
            Theme = ThemePalette.Terminal;
        }
        else if (Theme.Equals(ThemePalette.Aero, StringComparison.OrdinalIgnoreCase))
        {
            Theme = ThemePalette.Glass;
        }

        Theme = ThemePalette.NormalizeTheme(Theme);
        ColorScheme = ThemePalette.NormalizeColorScheme(ColorScheme);
    }

    private static string DetectDefaultLanguage()
    {
        var cultureName = CultureInfo.CurrentUICulture.Name;
        if (cultureName.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
        {
            return "zh-TW";
        }

        if (cultureName.StartsWith("ja", StringComparison.OrdinalIgnoreCase))
        {
            return "ja";
        }

        return "en";
    }

    public void Save()
    {
        Directory.CreateDirectory(DirectoryPath);
        var json = JsonSerializer.Serialize(this, JsonOptions);
        File.WriteAllText(FilePath, json);
    }
}
