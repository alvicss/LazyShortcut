// =========================================================
// 檔案：ColorPickerWindow.xaml.cs
// 用途：處理色彩方塊、色相條、螢幕滴管、RGB 輸入及透明度預覽。
// 誰會用到：MainWindow 開啟卡片調色盤時建立此視窗。
// =========================================================
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ProjectShortcutDock;

public partial class ColorPickerWindow : Window
{
    private double _hue;
    private double _saturation;
    private double _value;
    private byte _alpha;
    private bool _isSyncingValues = true;
    private bool _isPickingFromScreen;
    private bool _isExplicitCloseResult;

    public ColorPickerWindow(Window owner, Color initialColor)
    {
        InitializeComponent();

        Owner = owner;
        ApplyThemeFrom(owner);
        ApplyLanguage(owner);
        SetColor(initialColor);
    }

    public Color SelectedColor { get; private set; }

    /// <summary>沿用卡片目前主題的文字、背景與邊框色。</summary>
    private void ApplyThemeFrom(Window owner)
    {
        CopyResource(owner, "WindowBrush", Brushes.White);
        CopyResource(owner, "PanelBrush", Brushes.WhiteSmoke);
        CopyResource(owner, "TextBrush", Brushes.Black);
        CopyResource(owner, "SubtleTextBrush", Brushes.DimGray);
        CopyResource(owner, "BorderBrush", Brushes.LightGray);
    }

    /// <summary>套用目前語系，並保留 R／G／B 通道名稱方便精確輸入。</summary>
    private void ApplyLanguage(Window owner)
    {
        var uiLanguage = owner is MainWindow mainWindow
            ? mainWindow.CurrentLanguage
            : UiText.DefaultLanguage;

        Title = UiText.Get(uiLanguage, "ColorDialogTitle");
        RedLabel.Text = "R";
        GreenLabel.Text = "G";
        BlueLabel.Text = "B";
        AlphaLabel.Text = UiText.Get(uiLanguage, "Alpha");
        EyedropperButton.ToolTip = UiText.Get(uiLanguage, "PickScreenColor");
        CancelButton.Content = UiText.Get(uiLanguage, "Cancel");
        ConfirmButton.Content = UiText.Get(uiLanguage, "Confirm");
    }

    /// <summary>複製主視窗的色彩資源；缺少資源時使用可辨識的預設色。</summary>
    private void CopyResource(Window owner, string key, object fallback)
    {
        Resources[key] = owner.Resources[key] ?? fallback;
    }

    /// <summary>從現有 ARGB 顏色更新 HSV 座標，灰階色保留目前的色相位置。</summary>
    private void SetColor(Color color)
    {
        var previousHue = _hue;
        ColorSpaceHelper.ToHsv(color, out _hue, out _saturation, out _value);
        if (_saturation == 0)
        {
            _hue = previousHue;
        }

        _alpha = color.A;
        SelectedColor = color;
        UpdateControls();
    }

    /// <summary>同步色彩方塊、滑桿、數值輸入與即時預覽。</summary>
    private void UpdateControls()
    {
        _isSyncingValues = true;
        ColorField.Background = new SolidColorBrush(ColorSpaceHelper.FromHsv(255, _hue, 1, 1));
        PreviewSwatch.Fill = new SolidColorBrush(SelectedColor);
        HexValueText.Text = $"#{SelectedColor.A:X2}{SelectedColor.R:X2}{SelectedColor.G:X2}{SelectedColor.B:X2}";
        RedValueBox.Text = SelectedColor.R.ToString();
        GreenValueBox.Text = SelectedColor.G.ToString();
        BlueValueBox.Text = SelectedColor.B.ToString();
        AlphaValueBox.Text = SelectedColor.A.ToString();
        AlphaSlider.Value = _alpha;
        UpdateMarkerPositions();
        _isSyncingValues = false;
    }

    /// <summary>依 HSV 值放置方塊選點與彩虹色相條上的指示標記。</summary>
    private void UpdateMarkerPositions()
    {
        if (ColorField.ActualWidth > 0 && ColorField.ActualHeight > 0)
        {
            Canvas.SetLeft(ColorMarker, _saturation * ColorField.ActualWidth - ColorMarker.Width / 2);
            Canvas.SetTop(ColorMarker, (1 - _value) * ColorField.ActualHeight - ColorMarker.Height / 2);
        }

        if (HueField.ActualWidth > 0)
        {
            Canvas.SetLeft(HueMarker, _hue / 360 * HueField.ActualWidth - HueMarker.Width / 2);
        }
    }

    /// <summary>方塊尺寸改變時重新對齊選色標記。</summary>
    private void ColorField_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateMarkerPositions();
    }

    /// <summary>色相條尺寸改變時重新對齊標記。</summary>
    private void HueField_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateMarkerPositions();
    }

    /// <summary>從目前 HSV 座標計算選中色，透明度維持原值。</summary>
    private void UpdateColorFromHsv()
    {
        SelectedColor = ColorSpaceHelper.FromHsv(_alpha, _hue, _saturation, _value);
        UpdateControls();
    }

    /// <summary>滑鼠在方塊中的水平位置控制飽和度，垂直位置控制明度。</summary>
    private void SelectFromColorField(Point position)
    {
        if (ColorField.ActualWidth <= 0 || ColorField.ActualHeight <= 0)
        {
            return;
        }

        _saturation = Math.Clamp(position.X / ColorField.ActualWidth, 0, 1);
        _value = 1 - Math.Clamp(position.Y / ColorField.ActualHeight, 0, 1);
        UpdateColorFromHsv();
    }

    /// <summary>滑鼠在彩虹條中的位置控制色相。</summary>
    private void SelectFromHueField(Point position)
    {
        if (HueField.ActualWidth <= 0)
        {
            return;
        }

        _hue = Math.Clamp(position.X / HueField.ActualWidth, 0, 1) * 360;
        UpdateColorFromHsv();
    }

    /// <summary>開始拖曳色彩方塊。</summary>
    private void ColorField_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        ColorField.CaptureMouse();
        SelectFromColorField(e.GetPosition(ColorField));
        e.Handled = true;
    }

    /// <summary>拖曳色彩方塊時即時更新預覽。</summary>
    private void ColorField_MouseMove(object sender, MouseEventArgs e)
    {
        if (ColorField.IsMouseCaptured && e.LeftButton == MouseButtonState.Pressed)
        {
            SelectFromColorField(e.GetPosition(ColorField));
        }
    }

    /// <summary>完成色彩方塊拖曳。</summary>
    private void ColorField_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!ColorField.IsMouseCaptured)
        {
            return;
        }

        SelectFromColorField(e.GetPosition(ColorField));
        ColorField.ReleaseMouseCapture();
        e.Handled = true;
    }

    /// <summary>開始拖曳彩虹色相條。</summary>
    private void HueField_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        HueField.CaptureMouse();
        SelectFromHueField(e.GetPosition(HueField));
        e.Handled = true;
    }

    /// <summary>拖曳彩虹條時即時更新色彩方塊。</summary>
    private void HueField_MouseMove(object sender, MouseEventArgs e)
    {
        if (HueField.IsMouseCaptured && e.LeftButton == MouseButtonState.Pressed)
        {
            SelectFromHueField(e.GetPosition(HueField));
        }
    }

    /// <summary>完成彩虹色相條拖曳。</summary>
    private void HueField_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!HueField.IsMouseCaptured)
        {
            return;
        }

        SelectFromHueField(e.GetPosition(HueField));
        HueField.ReleaseMouseCapture();
        e.Handled = true;
    }

    /// <summary>透明度滑桿只改 alpha，不影響 RGB 與色相位置。</summary>
    private void AlphaSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isSyncingValues)
        {
            return;
        }

        _alpha = (byte)AlphaSlider.Value;
        SelectedColor = Color.FromArgb(_alpha, SelectedColor.R, SelectedColor.G, SelectedColor.B);
        UpdateControls();
    }

    /// <summary>按 Enter 時提交目前數值輸入。</summary>
    private void ChannelValueBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        CommitChannelValue(sender as TextBox);
        e.Handled = true;
    }

    /// <summary>離開數值輸入框時提交；無效內容恢復目前值。</summary>
    private void ChannelValueBox_LostFocus(object sender, RoutedEventArgs e)
    {
        CommitChannelValue(sender as TextBox);
    }

    /// <summary>套用 RGB 或透明度的 0–255 數值，並同步方塊與色相標記。</summary>
    private void CommitChannelValue(TextBox? textBox)
    {
        if (_isSyncingValues || textBox is null)
        {
            return;
        }

        if (!byte.TryParse(textBox.Text, out var value))
        {
            UpdateControls();
            return;
        }

        var color = SelectedColor;
        switch (textBox.Name)
        {
            case nameof(RedValueBox):
                color.R = value;
                break;
            case nameof(GreenValueBox):
                color.G = value;
                break;
            case nameof(BlueValueBox):
                color.B = value;
                break;
            case nameof(AlphaValueBox):
                color.A = value;
                break;
        }

        SetColor(color);
    }

    /// <summary>進入螢幕取色模式；下一次滑鼠左鍵會選取游標下方像素。</summary>
    private void EyedropperButton_Click(object sender, RoutedEventArgs e)
    {
        if (!Mouse.Capture(this, CaptureMode.SubTree))
        {
            return;
        }

        _isPickingFromScreen = true;
        Cursor = Cursors.Cross;
    }

    /// <summary>取色模式下點擊任一螢幕位置，讀取該位置的 RGB 並保留透明度。</summary>
    private void Window_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_isPickingFromScreen)
        {
            return;
        }

        TryPickScreenColor();
        EndScreenPicking();
        e.Handled = true;
    }

    /// <summary>取色時按 Esc 僅退出滴管，不關閉整個調色盤。</summary>
    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || !_isPickingFromScreen)
        {
            return;
        }

        EndScreenPicking();
        e.Handled = true;
    }

    /// <summary>從 Windows 桌面畫面取得游標位置的顏色；讀取失敗時保留原色。</summary>
    private void TryPickScreenColor()
    {
        if (ScreenColorSampler.TryReadCursorColor(_alpha, out var color))
        {
            SetColor(color);
        }
    }

    /// <summary>結束滴管模式並恢復一般滑鼠游標。</summary>
    private void EndScreenPicking()
    {
        _isPickingFromScreen = false;
        Cursor = null;
        if (Mouse.Captured == this)
        {
            Mouse.Capture(null);
        }
    }

    /// <summary>切換視窗等情況使滑鼠擷取中斷時，退出滴管模式。</summary>
    private void Window_LostMouseCapture(object sender, MouseEventArgs e)
    {
        if (_isPickingFromScreen && Mouse.Captured != this)
        {
            _isPickingFromScreen = false;
            Cursor = null;
        }
    }

    /// <summary>按確定後將選中色回傳給卡片。</summary>
    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        _isExplicitCloseResult = true;
        DialogResult = true;
    }

    /// <summary>按取消後捨棄對話框中的色彩變更。</summary>
    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _isExplicitCloseResult = true;
        DialogResult = false;
    }

    /// <summary>按視窗關閉鍵時視同取消，避免意外套用尚未確定的顏色。</summary>
    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        EndScreenPicking();
        if (!_isExplicitCloseResult && !DialogResult.HasValue)
        {
            _isExplicitCloseResult = true;
            DialogResult = false;
        }
    }

}
