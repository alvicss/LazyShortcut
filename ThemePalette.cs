// =========================================================
// 檔案：ThemePalette.cs
// 用途：集中定義各種卡片風格與亮暗色系的顏色、圓角及字體。
// 誰會用到：MainWindow 套用外觀時讀取對應的 ThemePalette。
// =========================================================
using System;
using System.Collections.Generic;
using Microsoft.Win32;
using System.Windows.Media;

namespace ProjectShortcutDock;

public sealed record ThemePalette(
    Color Window,
    Color Panel,
    Color Text,
    Color SubtleText,
    Color Border,
    Color Hover,
    Color Shadow,
    double ShadowOpacity,
    double CornerRadius,
    string FontFamily)
{
    // 舊版設定名稱仍保留，供 AppSettings 載入時進行相容轉換。
    public const string Normal = "Normal";
    public const string Dark = "Dark";
    public const string Tech = "Tech";
    public const string Aero = "Aero";

    public const string Minimal = "Minimal";
    public const string Glass = "Glass";
    public const string Terminal = "Terminal";
    public const string Neon = "Neon";
    public const string Auto = "Auto";
    public const string Light = "Light";
    public const string DarkScheme = "Dark";

    public static IReadOnlyList<string> Names { get; } = new[] { Minimal, Glass, Terminal, Neon };

    /// <summary>將未知或舊版以外的主題名稱轉成目前支援的風格 ID。</summary>
    public static string NormalizeTheme(string? theme)
    {
        foreach (var name in Names)
        {
            if (string.Equals(name, theme, StringComparison.OrdinalIgnoreCase))
            {
                return name;
            }
        }

        return Minimal;
    }

    /// <summary>將色系設定限制為 Auto、Light 或 Dark。</summary>
    public static string NormalizeColorScheme(string? colorScheme)
    {
        if (string.Equals(colorScheme, Light, StringComparison.OrdinalIgnoreCase))
        {
            return Light;
        }

        if (string.Equals(colorScheme, DarkScheme, StringComparison.OrdinalIgnoreCase))
        {
            return DarkScheme;
        }

        return Auto;
    }

    /// <summary>
    /// 依風格與亮暗設定回傳色盤；Auto 讀取 Windows 應用程式色彩偏好，Neon 固定暗色。
    /// </summary>
    public static ThemePalette For(string? themeName, string? colorScheme)
    {
        var theme = NormalizeTheme(themeName);
        var scheme = NormalizeColorScheme(colorScheme);
        var isDark = theme == Neon || (scheme == Auto ? IsWindowsDarkMode() : scheme == DarkScheme);

        if (theme == Glass)
        {
            if (isDark)
            {
                return new ThemePalette(
                    Color.FromArgb(185, 20, 26, 34),
                    Color.FromArgb(150, 31, 39, 49),
                    Color.FromRgb(241, 245, 249),
                    Color.FromRgb(176, 190, 205),
                    Color.FromArgb(160, 220, 233, 247),
                    Color.FromArgb(80, 220, 233, 247),
                    Color.FromRgb(0, 0, 0),
                    0.34,
                    16,
                    "Segoe UI");
            }

            return new ThemePalette(
                Color.FromArgb(205, 245, 248, 252),
                Color.FromArgb(150, 255, 255, 255),
                Color.FromRgb(31, 41, 55),
                Color.FromRgb(90, 105, 123),
                Color.FromArgb(215, 255, 255, 255),
                Color.FromArgb(90, 175, 202, 230),
                Color.FromRgb(30, 55, 80),
                0.24,
                16,
                "Segoe UI");
        }

        if (theme == Terminal)
        {
            if (isDark)
            {
                return new ThemePalette(
                    Color.FromRgb(12, 15, 10),
                    Color.FromRgb(18, 23, 16),
                    Color.FromRgb(207, 239, 203),
                    Color.FromRgb(111, 138, 107),
                    Color.FromRgb(31, 42, 28),
                    Color.FromArgb(35, 207, 239, 203),
                    Color.FromRgb(0, 0, 0),
                    0.08,
                    6,
                    "Consolas");
            }

            return new ThemePalette(
                Color.FromRgb(243, 245, 238),
                Color.FromRgb(250, 251, 246),
                Color.FromRgb(31, 58, 29),
                Color.FromRgb(107, 122, 98),
                Color.FromRgb(213, 220, 203),
                Color.FromArgb(28, 31, 58, 29),
                Color.FromRgb(31, 58, 29),
                0.05,
                6,
                "Consolas");
        }

        if (theme == Neon)
        {
            return new ThemePalette(
                Color.FromRgb(22, 15, 42),
                Color.FromRgb(35, 24, 63),
                Color.FromRgb(244, 238, 255),
                Color.FromRgb(174, 158, 213),
                Color.FromRgb(228, 61, 202),
                Color.FromRgb(64, 41, 100),
                Color.FromRgb(109, 63, 255),
                0.42,
                14,
                "Segoe UI");
        }

        if (isDark)
        {
            return new ThemePalette(
                Color.FromRgb(22, 25, 29),
                Color.FromRgb(29, 33, 38),
                Color.FromRgb(236, 239, 243),
                Color.FromRgb(140, 149, 161),
                Color.FromRgb(42, 47, 54),
                Color.FromRgb(38, 44, 50),
                Color.FromRgb(0, 0, 0),
                0,
                10,
                "Segoe UI");
        }

        return new ThemePalette(
            Color.FromRgb(245, 247, 250),
            Color.FromRgb(255, 255, 255),
            Color.FromRgb(21, 25, 30),
            Color.FromRgb(107, 116, 128),
            Color.FromRgb(225, 229, 234),
            Color.FromRgb(230, 234, 239),
            Color.FromRgb(15, 23, 42),
            0,
            10,
            "Segoe UI");
    }

    /// <summary>
    /// 讀取 Windows「應用程式模式」設定；設定不存在時使用亮色作為安全預設。
    /// </summary>
    private static bool IsWindowsDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int lightTheme && lightTheme == 0;
        }
        catch
        {
            return false;
        }
    }
}
