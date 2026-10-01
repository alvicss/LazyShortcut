// =========================================================
// 檔案：ColorSpaceHelper.cs
// 用途：在 RGB 與 HSV 色彩表示法之間轉換，供調色盤方塊與色相條使用。
// 誰會用到：ColorPickerWindow 讀取既有顏色、拖曳選色及輸入 RGB 時呼叫。
// =========================================================
using System;
using System.Windows.Media;

namespace ProjectShortcutDock;

internal static class ColorSpaceHelper
{
    /// <summary>把 RGB 顏色轉成色相 0–360、飽和度 0–1、明度 0–1。</summary>
    public static void ToHsv(Color color, out double hue, out double saturation, out double value)
    {
        var red = color.R / 255d;
        var green = color.G / 255d;
        var blue = color.B / 255d;
        var maximum = Math.Max(red, Math.Max(green, blue));
        var minimum = Math.Min(red, Math.Min(green, blue));
        var difference = maximum - minimum;

        value = maximum;
        saturation = maximum == 0 ? 0 : difference / maximum;
        hue = 0;

        if (difference == 0)
        {
            return;
        }

        if (maximum == red)
        {
            hue = 60 * ((green - blue) / difference);
        }
        else if (maximum == green)
        {
            hue = 60 * (2 + (blue - red) / difference);
        }
        else
        {
            hue = 60 * (4 + (red - green) / difference);
        }

        if (hue < 0)
        {
            hue += 360;
        }
    }

    /// <summary>依色相、飽和度與明度產生 RGB 顏色，並保留指定透明度。</summary>
    public static Color FromHsv(byte alpha, double hue, double saturation, double value)
    {
        hue = ((hue % 360) + 360) % 360;
        saturation = Math.Clamp(saturation, 0, 1);
        value = Math.Clamp(value, 0, 1);

        var chroma = value * saturation;
        var secondary = chroma * (1 - Math.Abs((hue / 60 % 2) - 1));
        var minimum = value - chroma;
        double red;
        double green;
        double blue;

        if (hue < 60)
        {
            red = chroma;
            green = secondary;
            blue = 0;
        }
        else if (hue < 120)
        {
            red = secondary;
            green = chroma;
            blue = 0;
        }
        else if (hue < 180)
        {
            red = 0;
            green = chroma;
            blue = secondary;
        }
        else if (hue < 240)
        {
            red = 0;
            green = secondary;
            blue = chroma;
        }
        else if (hue < 300)
        {
            red = secondary;
            green = 0;
            blue = chroma;
        }
        else
        {
            red = chroma;
            green = 0;
            blue = secondary;
        }

        return Color.FromArgb(
            alpha,
            ToByte(red + minimum),
            ToByte(green + minimum),
            ToByte(blue + minimum));
    }

    /// <summary>將 0–1 色彩通道換算成 0–255，避免浮點誤差超出 byte 範圍。</summary>
    private static byte ToByte(double channel)
    {
        return (byte)Math.Clamp((int)Math.Round(channel * 255), 0, 255);
    }
}
