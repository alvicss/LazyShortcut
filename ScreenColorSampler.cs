// =========================================================
// 檔案：ScreenColorSampler.cs
// 用途：讀取滑鼠游標所在螢幕像素的 RGB 色彩，供調色盤滴管使用。
// 誰會用到：ColorPickerWindow 進入滴管模式後呼叫。
// =========================================================
using System;
using System.Runtime.InteropServices;
using System.Windows.Media;

namespace ProjectShortcutDock;

internal static class ScreenColorSampler
{
    /// <summary>取得游標下方像素；失敗時回傳 false，透明度由調色盤原值提供。</summary>
    public static bool TryReadCursorColor(byte alpha, out Color color)
    {
        color = default;
        if (!GetCursorPos(out var position))
        {
            return false;
        }

        var screenDeviceContext = GetDC(IntPtr.Zero);
        if (screenDeviceContext == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            var pixel = GetPixel(screenDeviceContext, position.X, position.Y);
            if (pixel == uint.MaxValue)
            {
                return false;
            }

            var red = (byte)(pixel & 0xFF);
            var green = (byte)((pixel >> 8) & 0xFF);
            var blue = (byte)((pixel >> 16) & 0xFF);
            color = Color.FromArgb(alpha, red, green, blue);
            return true;
        }
        finally
        {
            ReleaseDC(IntPtr.Zero, screenDeviceContext);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ScreenPoint
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out ScreenPoint position);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr windowHandle);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr windowHandle, IntPtr deviceContext);

    [DllImport("gdi32.dll")]
    private static extern uint GetPixel(IntPtr deviceContext, int x, int y);
}
