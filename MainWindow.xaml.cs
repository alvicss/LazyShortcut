// =========================================================
// 檔案：MainWindow.xaml.cs
// 用途：處理捷徑卡片視窗的操作、外觀、排序及 Windows 整合。
// 誰會用到：MainWindow.xaml 的事件由此檔案執行。
// =========================================================
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Forms = System.Windows.Forms;

namespace ProjectShortcutDock;

public partial class MainWindow : Window
{
    private const string ShortcutDragFormat = "ProjectShortcutDock.ShortcutItem";
    private const string DesktopWindowMode = "Desktop";
    private const string TopmostWindowMode = "Topmost";
    private const string DefaultTerminalShell = "cmd";
    private const int WmNcHitTest = 0x0084;
    private const int WmStyleChanging = 0x007C;
    private const int GwlStyle = -16;
    private const int GwlExStyle = -20;
    private const int WsMaximizeBox = 0x00010000;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExAppWindow = 0x00040000;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpFrameChanged = 0x0020;
    private const int HitTestLeft = 10;
    private const int HitTestRight = 11;
    private const int HitTestTop = 12;
    private const int HitTestTopLeft = 13;
    private const int HitTestTopRight = 14;
    private const int HitTestBottom = 15;
    private const int HitTestBottomLeft = 16;
    private const int HitTestBottomRight = 17;
    private const double ResizeEdgeSize = 8;
    private static readonly LanguageOption[] LanguageOptions =
    {
        new("zh-TW", "繁體中文"),
        new("en", "English"),
        new("ja", "日本語")
    };

    private readonly DockManager _manager;
    private readonly AppSettings _settings;
    private readonly ShortcutGroup _group;
    private readonly List<ShellOption> _terminalShellOptions = new();
    private bool _allowClose;
    private bool _isLoading = true;
    private IntPtr _windowHandle;
    private ShortcutItem? _draggedShortcut;
    private Point _shortcutDragStartPoint;

    public MainWindow(DockManager manager, ShortcutGroup group)
    {
        InitializeComponent();

        _manager = manager;
        _settings = manager.Settings;
        _group = group;
        _terminalShellOptions = DetectTerminalShellOptions();
        _settings.TerminalShell = NormalizeTerminalShell(_settings.TerminalShell);

        DataContext = this;

        foreach (var item in _group.Shortcuts)
        {
            item.RefreshIcon();
        }

        _group.PropertyChanged += Group_PropertyChanged;

        ConfigureSettingsControls();
        ApplyLanguage();
        ApplySavedWindowPosition();
        ApplyTheme(_settings.Theme);
        ApplyWindowMode();
        ApplyGroupAccent();
        UpdateTitle();
        UpdateSubtitleVisibility();
        SetTitleEditing(false);
        Opacity = GetIdleOpacity();

        _isLoading = false;
    }

    public ShortcutGroup Group => _group;

    public ObservableCollection<ShortcutItem> Shortcuts => _group.Shortcuts;

    internal string GetText(string key) => UiText.Get(_settings.Language, key);

    internal string CurrentLanguage => _settings.Language;

    public void PrepareForClose()
    {
        _allowClose = true;
    }

    public void ShowFromManager()
    {
        Show();
        WindowState = WindowState.Normal;
        EnsureWindowIsOnScreen();
        if (Topmost)
        {
            Activate();
        }
        else
        {
            PlaceBehindForegroundWindow();
        }
    }

    public void FocusTitle()
    {
        SetTitleEditing(true);
        GroupNameBox.Focus();
        GroupNameBox.SelectAll();
    }

    public void RefreshFromSharedSettings()
    {
        _isLoading = true;
        ConfigureSettingsControls();
        ApplyLanguage();
        ApplyTheme(_settings.Theme);
        ApplyWindowMode();
        ApplyGroupAccent();
        UpdateTitle();
        UpdateSubtitleVisibility();
        _isLoading = false;
    }

    private void ConfigureSettingsControls()
    {
        ThemeBox.ItemsSource = new[]
        {
            new AppearanceOption(ThemePalette.Minimal, GetText("ThemeMinimal")),
            new AppearanceOption(ThemePalette.Glass, GetText("ThemeGlass")),
            new AppearanceOption(ThemePalette.Terminal, GetText("ThemeTerminal")),
            new AppearanceOption(ThemePalette.Neon, GetText("ThemeNeon"))
        };
        ThemeBox.DisplayMemberPath = nameof(AppearanceOption.DisplayName);
        ThemeBox.SelectedValuePath = nameof(AppearanceOption.Code);
        ThemeBox.SelectedValue = ThemePalette.NormalizeTheme(_settings.Theme);
        ColorSchemeBox.ItemsSource = new[]
        {
            new AppearanceOption(ThemePalette.Auto, GetText("SchemeAuto")),
            new AppearanceOption(ThemePalette.Light, GetText("SchemeLight")),
            new AppearanceOption(ThemePalette.DarkScheme, GetText("SchemeDark"))
        };
        ColorSchemeBox.DisplayMemberPath = nameof(AppearanceOption.DisplayName);
        ColorSchemeBox.SelectedValuePath = nameof(AppearanceOption.Code);
        ColorSchemeBox.SelectedValue = ThemePalette.NormalizeColorScheme(_settings.ColorScheme);
        ColorSchemeBox.IsEnabled = _settings.Theme != ThemePalette.Neon;
        WindowModeBox.ItemsSource = new[] { DesktopWindowMode, TopmostWindowMode };
        WindowModeBox.SelectedItem = NormalizeWindowMode(_settings.WindowMode);
        LanguageBox.ItemsSource = LanguageOptions;
        LanguageBox.DisplayMemberPath = nameof(LanguageOption.DisplayName);
        LanguageBox.SelectedValuePath = nameof(LanguageOption.Code);
        LanguageBox.SelectedValue = UiText.NormalizeLanguage(_settings.Language);
        TerminalShellBox.ItemsSource = _terminalShellOptions;
        TerminalShellBox.DisplayMemberPath = nameof(ShellOption.DisplayName);
        TerminalShellBox.SelectedValuePath = nameof(ShellOption.Code);
        TerminalShellBox.SelectedValue = _settings.TerminalShell;
        StartWithWindowsBox.IsChecked = _settings.StartWithWindows;
    }

    private void ApplySavedWindowPosition()
    {
        Width = _group.Width > 0 ? _group.Width : Width;
        Height = _group.Height > 0 ? _group.Height : Height;

        if (_group.Left.HasValue && _group.Top.HasValue)
        {
            Left = _group.Left.Value;
            Top = _group.Top.Value;
            return;
        }

        MoveToDefaultPosition(0);
    }

    /// <summary>
    /// 把卡片放到主螢幕右上角（程式的預設位置）。
    /// cascadeIndex：錯開的格數，0 = 不錯開；每多 1 格就往左下多偏移一點，避免多張卡片完全疊在一起。
    /// </summary>
    private void MoveToDefaultPosition(int cascadeIndex)
    {
        var workArea = SystemParameters.WorkArea;
        var offset = DockManager.CascadeOffset * cascadeIndex;

        // 往左下錯開；卡片很多或螢幕較小時，仍限制在主螢幕工作區內。
        var maximumLeft = Math.Max(workArea.Left, workArea.Right - Width);
        var maximumTop = Math.Max(workArea.Top, workArea.Bottom - Height);
        Left = Math.Clamp(workArea.Right - Width - 18 - offset, workArea.Left, maximumLeft);
        Top = Math.Clamp(workArea.Top + 18 + offset, workArea.Top, maximumTop);
    }

    /// <summary>
    /// 檢查卡片是否還在某個螢幕上看得到；看不到就移回主螢幕右上角，並把新位置存進設定檔。
    /// 會發生的情境：換了螢幕、拔掉外接螢幕、或螢幕排列被改掉（例如顯示卡驅動更新後重置），
    /// 設定檔裡存的舊座標剛好落在「沒有任何螢幕」的區域，卡片就會開著但看不到。
    /// </summary>
    private void EnsureWindowIsOnScreen()
    {
        if (IsTitleBarOnAnyScreen())
        {
            return;
        }

        // 依群組順序錯開，多張卡片同時被拉回來時才不會疊在同一個位置
        var cascadeIndex = Math.Max(0, _settings.Groups.IndexOf(_group));
        MoveToDefaultPosition(cascadeIndex);

        // 確保新位置寫回設定檔，下次開啟就直接用新位置
        PersistWindowBounds();
    }

    /// <summary>
    /// 螢幕排列改變後檢查卡片；若標題列已離開所有螢幕，就按群組順序移回主螢幕。
    /// </summary>
    internal void EnsureVisibleAfterDisplayChange(int groupIndex)
    {
        if (!IsVisible || IsTitleBarOnAnyScreen())
        {
            return;
        }

        MoveToDefaultPosition(groupIndex);
        PersistWindowBounds();
    }

    /// <summary>
    /// 從系統匣救援單張卡片：顯示視窗並移到主螢幕可操作範圍。
    /// </summary>
    internal void RestoreToPrimaryScreen(int groupIndex)
    {
        MoveToDefaultPosition(groupIndex);
        Show();
        WindowState = WindowState.Normal;
        PersistWindowBounds();
    }

    /// <summary>
    /// 判斷「標題列正中間」那一點，有沒有落在任何一個螢幕的工作區（扣掉工作列的範圍）內。
    /// 這一點看得到，代表標題列至少有一半在畫面上，使用者抓得到、拖得動。
    /// 回傳：true = 看得到；false = 不在任何螢幕上。
    /// </summary>
    private bool IsTitleBarOnAnyScreen()
    {
        // 取標題列正中間的點，換算成螢幕像素座標。
        // 不直接用 WPF 的 Left/Top：多螢幕縮放比例不同時，WPF 的單位和螢幕像素會對不起來；
        // PointToScreen 和 Screen 都是向 Windows 查的像素，單位一定一致。
        var titleBarCenter = new Point(TitleBar.ActualWidth / 2, TitleBar.ActualHeight / 2);
        var pointOnScreen = TitleBar.PointToScreen(titleBarCenter);
        var pixelX = (int)Math.Round(pointOnScreen.X);
        var pixelY = (int)Math.Round(pointOnScreen.Y);

        // 逐一比對每個螢幕。不能只看「所有螢幕合起來的外框」，
        // 因為兩個螢幕中間可能有空白區，落在那裡一樣看不到。
        foreach (var screen in Forms.Screen.AllScreens)
        {
            if (screen.WorkingArea.Contains(pixelX, pixelY))
            {
                return true;
            }
        }

        return false;
    }

    private void ApplyLanguage()
    {
        SubtitleText.Text = GetText("EmptyHint");
        AddWindowButton.ToolTip = GetText("AddWindow");
        RemoveWindowButton.ToolTip = GetText("RemoveWindow");
        RenameButton.ToolTip = GetText("RenameWindow");
        ColorButton.ToolTip = GetText("ChangeColor");
        SettingsButton.ToolTip = GetText("Settings");
        AboutButton.ToolTip = GetText("About");
        HideButton.ToolTip = GetText("HideToTray");
        StyleLabel.Text = GetText("Style");
        WindowModeLabel.Text = GetText("WindowMode");
        LanguageLabel.Text = GetText("Language");
        TerminalShellLabel.Text = GetText("TerminalShell");
        ColorSchemeLabel.Text = GetText("ColorScheme");
        StartWithWindowsBox.Content = GetText("StartWithWindows");
        ThemePreviewTitle.Text = GetText("ThemePreview");
        ThemePreviewDetail.Text = GetText("ThemePreviewDetail");
    }

    private void ApplyTheme(string themeName)
    {
        var palette = ThemePalette.For(themeName, _settings.ColorScheme);
        var normalizedTheme = ThemePalette.NormalizeTheme(themeName);
        Resources["WindowBrush"] = CreateBrush(palette.Window);
        Resources["PanelBrush"] = CreateBrush(palette.Panel);
        Resources["TextBrush"] = CreateBrush(palette.Text);
        Resources["SubtleTextBrush"] = CreateBrush(palette.SubtleText);
        Resources["BorderBrush"] = CreateBrush(palette.Border);
        Resources["HoverBrush"] = CreateBrush(palette.Hover);
        Resources["ShortcutRowBorderThickness"] = normalizedTheme == ThemePalette.Terminal
            ? new Thickness(0, 0, 0, 1)
            : new Thickness(0);
        ShellShadow.Color = palette.Shadow;
        ShellShadow.Opacity = palette.ShadowOpacity;
        ShellShadow.BlurRadius = normalizedTheme == ThemePalette.Glass ? 32 : 24;
        ShellShadow.ShadowDepth = normalizedTheme == ThemePalette.Glass ? 6 : 3;
        ShellBorder.CornerRadius = new CornerRadius(palette.CornerRadius);
        SettingsPanel.CornerRadius = new CornerRadius(Math.Max(3, palette.CornerRadius - 3));
        ThemePreviewBorder.CornerRadius = new CornerRadius(Math.Max(3, palette.CornerRadius - 3));
        FontFamily = new FontFamily(palette.FontFamily);
        TerminalPromptText.Visibility = normalizedTheme == ThemePalette.Terminal ? Visibility.Visible : Visibility.Collapsed;
        UpdateTitleEditorVisibility();
        TerminalScanlines.Visibility = normalizedTheme == ThemePalette.Terminal ? Visibility.Visible : Visibility.Collapsed;
        TerminalScanlines.Fill = CreateScanlineBrush(palette);
        Opacity = IsMouseOver ? 1 : GetIdleOpacity();
        ApplyGroupAccent();
    }

    /// <summary>依主題設定滑鼠離開後的透明度；玻璃使用自身半透明底，簡約維持實色。</summary>
    private double GetIdleOpacity()
    {
        var theme = ThemePalette.NormalizeTheme(_settings.Theme);
        return theme == ThemePalette.Terminal || theme == ThemePalette.Neon ? 0.94 : 1;
    }

    /// <summary>混合群組色時保留原本的透明度，避免玻璃背景意外變成實色。</summary>
    private static Color BlendPreservingAlpha(Color baseColor, Color accentColor, double weight)
    {
        var blended = CardColorPalette.Blend(baseColor, accentColor, weight);
        return Color.FromArgb(baseColor.A, blended.R, blended.G, blended.B);
    }

    /// <summary>為玻璃加入柔和反光，為終端機加入低對比漸層。</summary>
    private static Brush CreateCardBackground(string themeName, Color baseColor, Color panelColor)
    {
        var theme = ThemePalette.NormalizeTheme(themeName);
        if (theme != ThemePalette.Glass && theme != ThemePalette.Terminal)
        {
            return CreateBrush(baseColor);
        }

        var highlightWeight = theme == ThemePalette.Glass ? 0.14 : 0.32;
        var highlightColor = theme == ThemePalette.Glass ? Colors.White : panelColor;
        var topColor = BlendPreservingAlpha(baseColor, highlightColor, highlightWeight);
        var gradient = new LinearGradientBrush(topColor, baseColor, new Point(0, 0), new Point(1, 1));
        gradient.Freeze();
        return gradient;
    }

    /// <summary>建立終端機背景的細掃描線；線條不接收滑鼠事件。</summary>
    private static Brush CreateScanlineBrush(ThemePalette palette)
    {
        var isDark = palette.Window.R < 100;
        var lineColor = isDark ? Color.FromArgb(8, 255, 255, 255) : Color.FromArgb(7, 0, 0, 0);
        var line = new GeometryDrawing(CreateBrush(lineColor), null, new RectangleGeometry(new Rect(0, 0, 4, 1)));
        var brush = new DrawingBrush(line)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, 4, 4),
            ViewportUnits = BrushMappingMode.Absolute,
            Viewbox = new Rect(0, 0, 4, 4),
            ViewboxUnits = BrushMappingMode.Absolute,
            Stretch = Stretch.Fill
        };
        brush.Freeze();
        return brush;
    }

    private void ApplyGroupAccent()
    {
        var theme = ThemePalette.For(_settings.Theme, _settings.ColorScheme);
        var accent = _group.GetAccentColor();
        var accentWeight = CardColorPalette.GetAlphaWeight(accent);
        var visibleAccent = CardColorPalette.WithoutAlpha(accent);
        var themeName = ThemePalette.NormalizeTheme(_settings.Theme);
        var borderWeight = themeName == ThemePalette.Neon ? 0.7 : 0.14;
        var borderColor = BlendPreservingAlpha(theme.Border, visibleAccent, borderWeight * accentWeight);
        var backgroundColor = BlendPreservingAlpha(theme.Window, visibleAccent, 0.10 * accentWeight);
        var settingsBorder = BlendPreservingAlpha(theme.Border, visibleAccent, 0.30 * accentWeight);
        var settingsBackground = BlendPreservingAlpha(theme.Panel, visibleAccent, 0.06 * accentWeight);
        ShellBorder.BorderBrush = CreateBrush(borderColor);
        ShellBorder.Background = CreateCardBackground(themeName, backgroundColor, theme.Panel);
        SettingsPanel.BorderBrush = CreateBrush(settingsBorder);
        SettingsPanel.Background = CreateBrush(settingsBackground);
        ColorIndicatorFill.Fill = CreateBrush(accent);
        ColorIndicatorFill.Stroke = CreateBrush(visibleAccent);
        ColorIndicatorOutline.Stroke = CreateBrush(visibleAccent);
        ColorIndicatorOutline.Visibility = accent.A < 30 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ApplyWindowMode()
    {
        _settings.WindowMode = NormalizeWindowMode(_settings.WindowMode);
        Topmost = _settings.WindowMode == TopmostWindowMode;
        if (!Topmost)
        {
            PlaceBehindForegroundWindow();
        }
    }

    private void UpdateTitle()
    {
        Title = string.IsNullOrWhiteSpace(_group.Name)
            ? GetDefaultGroupName()
            : _group.Name;
        TerminalPromptText.Text = $"~/shortcut-dock $ ls \"{Title}\"";
    }

    private void UpdateSubtitleVisibility()
    {
        SubtitleText.Visibility = _group.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
    }

    private string GetDefaultGroupName()
    {
        var index = Math.Max(1, _settings.Groups.IndexOf(_group) + 1);
        return $"{GetText("GroupDefaultName")} {index}";
    }

    private void PersistWindowBounds()
    {
        if (_isLoading || WindowState != WindowState.Normal)
        {
            return;
        }

        _group.Left = Left;
        _group.Top = Top;
        _group.Width = Width;
        _group.Height = Height;
        _manager.SaveSettings();
    }

    private void BeginWindowDrag(MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        DragMove();
        PersistWindowBounds();
        e.Handled = true;
    }

    private void SetTitleEditing(bool isEditing)
    {
        GroupNameBox.IsReadOnly = !isEditing;
        GroupNameBox.Cursor = isEditing ? Cursors.IBeam : Cursors.SizeAll;
        UpdateTitleEditorVisibility();
    }

    /// <summary>Terminal 平時只顯示命令提示列；按鉛筆改名時才顯示群組名稱輸入框。</summary>
    private void UpdateTitleEditorVisibility()
    {
        var isTerminal = ThemePalette.NormalizeTheme(_settings.Theme) == ThemePalette.Terminal;
        GroupNameBox.Visibility = isTerminal && GroupNameBox.IsReadOnly
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void AddShortcuts(string[] paths)
    {
        var added = false;
        foreach (var path in paths.Where(ShortcutPathExists))
        {
            var normalizedPath = NormalizeShortcutPath(path);
            if (_group.Shortcuts.Any(x => string.Equals(x.Path, normalizedPath, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var item = new ShortcutItem
            {
                Path = normalizedPath,
                Name = GetShortcutName(normalizedPath)
            };
            item.RefreshIcon();
            _group.Shortcuts.Add(item);
            added = true;
        }

        if (added)
        {
            UpdateSubtitleVisibility();
            _manager.SaveSettings();
        }
    }

    private void RemoveShortcut(ShortcutItem item)
    {
        _group.Shortcuts.Remove(item);
        UpdateSubtitleVisibility();
        _manager.SaveSettings();
    }

    /// <summary>
    /// 將捷徑在目前卡片內移動指定格數，並立即保存新的顯示順序。
    /// </summary>
    private void MoveShortcut(ShortcutItem item, int offset)
    {
        var currentIndex = _group.Shortcuts.IndexOf(item);
        var targetIndex = currentIndex + offset;
        if (currentIndex < 0 || targetIndex < 0 || targetIndex >= _group.Shortcuts.Count)
        {
            return;
        }

        _group.Shortcuts.Move(currentIndex, targetIndex);
        ShortcutList.SelectedItem = item;
        _manager.SaveSettings();
    }

    /// <summary>
    /// 依滑鼠放下位置，把拖曳中的捷徑插入目標項目之前或之後。
    /// </summary>
    private void ReorderShortcutAt(Point dropPoint, ShortcutItem draggedItem)
    {
        var targetContainer = ItemsControl.ContainerFromElement(
            ShortcutList,
            ShortcutList.InputHitTest(dropPoint) as DependencyObject) as ListBoxItem;
        var originalIndex = _group.Shortcuts.IndexOf(draggedItem);
        if (originalIndex < 0)
        {
            return;
        }

        var targetIndex = _group.Shortcuts.Count;
        if (targetContainer?.DataContext is ShortcutItem targetItem)
        {
            targetIndex = _group.Shortcuts.IndexOf(targetItem);
            var itemPoint = dropPoint;
            var itemTopLeft = targetContainer.TranslatePoint(new Point(0, 0), ShortcutList);
            if (itemPoint.Y > itemTopLeft.Y + targetContainer.ActualHeight / 2)
            {
                targetIndex++;
            }
        }

        if (targetIndex > originalIndex)
        {
            targetIndex--;
        }

        targetIndex = Math.Clamp(targetIndex, 0, _group.Shortcuts.Count - 1);
        if (targetIndex == originalIndex)
        {
            return;
        }

        _group.Shortcuts.Move(originalIndex, targetIndex);
        ShortcutList.SelectedItem = draggedItem;
        _manager.SaveSettings();
    }

    private void OpenPath(string path)
    {
        if (!ShortcutPathExists(path))
        {
            MessageBox.Show(GetText("ItemMissing"), GetText("AppTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }

    private void OpenTerminal(string path, string? command = null)
    {
        if (!ShortcutPathExists(path))
        {
            MessageBox.Show(GetText("ItemMissing"), GetText("AppTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var workingDirectory = GetWorkingDirectory(path);
        if (string.IsNullOrWhiteSpace(workingDirectory) || !Directory.Exists(workingDirectory))
        {
            MessageBox.Show(GetText("ItemMissing"), GetText("AppTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var shell = _terminalShellOptions.FirstOrDefault(x => x.Code == NormalizeTerminalShell(_settings.TerminalShell))
            ?? _terminalShellOptions.First(x => x.Code == DefaultTerminalShell);
        Process.Start(CreateTerminalStartInfo(shell, workingDirectory, command));
    }

    private void SetStartWithWindows(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
        if (key is null)
        {
            return;
        }

        const string valueName = "ProjectShortcutDock";
        if (enabled)
        {
            key.SetValue(valueName, $"\"{Environment.ProcessPath}\"");
        }
        else
        {
            key.DeleteValue(valueName, false);
        }
    }

    private static bool ShortcutPathExists(string path)
    {
        return Directory.Exists(path) || File.Exists(path);
    }

    private static string NormalizeShortcutPath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var rootPath = Path.GetPathRoot(fullPath);
        return Directory.Exists(fullPath) &&
               Path.EndsInDirectorySeparator(fullPath) &&
               !string.Equals(fullPath, rootPath, StringComparison.OrdinalIgnoreCase)
            ? fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            : fullPath;
    }

    private static string GetShortcutName(string path)
    {
        var name = Path.GetFileName(path);
        return string.IsNullOrWhiteSpace(name) ? path : name;
    }

    private static string? GetWorkingDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            return path;
        }

        return File.Exists(path) ? Path.GetDirectoryName(path) : null;
    }

    private static string NormalizeWindowMode(string? mode)
    {
        return string.Equals(mode, TopmostWindowMode, StringComparison.OrdinalIgnoreCase)
            ? TopmostWindowMode
            : DesktopWindowMode;
    }

    private string NormalizeTerminalShell(string? shell)
    {
        return _terminalShellOptions.Any(x => string.Equals(x.Code, shell, StringComparison.OrdinalIgnoreCase))
            ? shell!
            : DefaultTerminalShell;
    }

    private static Brush CreateBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static ProcessStartInfo CreateTerminalStartInfo(ShellOption shell, string path, string? command)
    {
        return shell.Code switch
        {
            "pwsh" or "powershell" => new ProcessStartInfo
            {
                FileName = shell.ExecutablePath,
                Arguments = BuildPowerShellArguments(path, command),
                UseShellExecute = false
            },
            "git-bash" => new ProcessStartInfo
            {
                FileName = shell.ExecutablePath,
                Arguments = BuildGitBashArguments(path, command),
                UseShellExecute = false
            },
            "wsl" => new ProcessStartInfo
            {
                FileName = shell.ExecutablePath,
                Arguments = BuildWslArguments(path, command),
                UseShellExecute = false
            },
            _ => new ProcessStartInfo
            {
                FileName = shell.ExecutablePath,
                Arguments = BuildCmdArguments(path, command),
                UseShellExecute = false
            }
        };
    }

    private static string BuildCmdArguments(string path, string? command)
    {
        return string.IsNullOrWhiteSpace(command)
            ? $"/k cd /d \"{path}\""
            : $"/k cd /d \"{path}\" && {command}";
    }

    private static string BuildPowerShellArguments(string path, string? command)
    {
        var escapedPath = path.Replace("'", "''");
        return string.IsNullOrWhiteSpace(command)
            ? $"-NoExit -Command \"Set-Location -LiteralPath '{escapedPath}'\""
            : $"-NoExit -Command \"Set-Location -LiteralPath '{escapedPath}'; {command}\"";
    }

    private static string BuildGitBashArguments(string path, string? command)
    {
        var bashPath = path.Replace('\\', '/').Replace("\"", "\\\"");
        var bashCommand = string.IsNullOrWhiteSpace(command)
            ? $"cd \"{bashPath}\"; exec bash"
            : $"cd \"{bashPath}\" && {command}; exec bash";
        return $"--login -i -c \"{bashCommand.Replace("\"", "\\\"")}\"";
    }

    private static string BuildWslArguments(string path, string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return $"--cd \"{path}\"";
        }

        return $"--cd \"{path}\" --exec bash -lc \"{command}; exec bash\"";
    }

    private static List<ShellOption> DetectTerminalShellOptions()
    {
        var options = new List<ShellOption>
        {
            new(DefaultTerminalShell, "cmd.exe", "cmd.exe")
        };

        if (FindExecutable("powershell.exe") is string windowsPowerShell)
        {
            options.Add(new("powershell", "Windows PowerShell", windowsPowerShell));
        }

        if (FindExecutable("pwsh.exe") is string powerShell7)
        {
            options.Add(new("pwsh", "PowerShell 7", powerShell7));
        }

        if (FindGitBash() is string gitBash)
        {
            options.Add(new("git-bash", "Git Bash", gitBash));
        }

        if (FindExecutable("wsl.exe") is string wsl)
        {
            options.Add(new("wsl", "WSL", wsl));
        }

        return options;
    }

    private static string? FindGitBash()
    {
        var candidates = new[]
        {
            @"C:\Program Files\Git\bin\bash.exe",
            @"C:\Program Files (x86)\Git\bin\bash.exe"
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private static string? FindExecutable(string fileName)
    {
        if (File.Exists(fileName))
        {
            return fileName;
        }

        var paths = (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        foreach (var path in paths)
        {
            try
            {
                var candidate = Path.Combine(path.Trim(), fileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch
            {
                // Ignore malformed PATH entries.
            }
        }

        return null;
    }

    private ContextMenu CreateShortcutContextMenu()
    {
        var menu = new ContextMenu();
        var selected = ShortcutList.SelectedItem as ShortcutItem;
        var selectedIndex = selected is null ? -1 : _group.Shortcuts.IndexOf(selected);
        var moveUpItem = CreateMenuItem(GetText("MoveUp"), "\uE74A", MoveShortcutUpMenuItem_Click);
        moveUpItem.IsEnabled = selectedIndex > 0;
        menu.Items.Add(moveUpItem);
        var moveDownItem = CreateMenuItem(GetText("MoveDown"), "\uE74B", MoveShortcutDownMenuItem_Click);
        moveDownItem.IsEnabled = selectedIndex >= 0 && selectedIndex < _group.Shortcuts.Count - 1;
        menu.Items.Add(moveDownItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(CreateMenuItem(GetText("Open"), "\uE8E5", OpenMenuItem_Click));
        menu.Items.Add(CreateMenuItem(GetText("OpenParent"), "\uE838", OpenParentMenuItem_Click));
        menu.Items.Add(CreateMenuItem(GetText("CopyItem"), "\uE8C8", CopyItemMenuItem_Click));
        menu.Items.Add(CreateMenuItem(GetText("CopyPath"), "\uE8C8", CopyPathMenuItem_Click));
        menu.Items.Add(new Separator());
        menu.Items.Add(CreateMenuItem(GetText("OpenTerminal"), "\uE756", OpenTerminalMenuItem_Click));
        menu.Items.Add(CreateMenuItem(GetText("OpenCodex"), LoadMenuIcon("codex.ico"), OpenCodexMenuItem_Click));
        menu.Items.Add(CreateMenuItem(GetText("OpenClaude"), LoadMenuIcon("claude.ico"), OpenClaudeMenuItem_Click));
        menu.Items.Add(CreateMenuItem(GetText("OpenAgy"), LoadMenuIcon("agy.ico"), OpenAgyMenuItem_Click));
        menu.Items.Add(new Separator());
        menu.Items.Add(CreateMenuItem(GetText("ChangeIcon"), "\uE8B9", ChangeIconMenuItem_Click));
        menu.Items.Add(CreateMenuItem(GetText("ResetIcon"), "\uE72C", ResetIconMenuItem_Click));
        menu.Items.Add(new Separator());
        menu.Items.Add(CreateMenuItem(GetText("RemoveShortcut"), "\uE74D", RemoveMenuItem_Click));
        return menu;
    }

    private static MenuItem CreateMenuItem(string header, object? icon, RoutedEventHandler clickHandler)
    {
        var item = new MenuItem
        {
            Header = header,
            Icon = icon is string glyph ? CreateMenuIcon(glyph) : icon
        };
        item.Click += clickHandler;
        return item;
    }

    private static TextBlock CreateMenuIcon(string glyph)
    {
        return new TextBlock
        {
            Text = glyph,
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 15,
            Width = 18,
            Height = 18,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };
    }

    private static Image LoadMenuIcon(string fileName)
    {
        var image = new Image
        {
            Width = 18,
            Height = 18,
            Stretch = Stretch.Uniform
        };
        var path = Path.Combine(AppContext.BaseDirectory, "image", fileName);
        if (File.Exists(path))
        {
            image.Source = new BitmapImage(new Uri(path, UriKind.Absolute));
        }

        return image;
    }

    private ShortcutItem? ItemFromSender(object sender)
    {
        if (sender is FrameworkElement element && element.DataContext is ShortcutItem item)
        {
            return item;
        }

        if (sender is MenuItem menuItem &&
            menuItem.Parent is ContextMenu contextMenu &&
            contextMenu.PlacementTarget is FrameworkElement placementTarget)
        {
            if (placementTarget.DataContext is ShortcutItem contextItem)
            {
                return contextItem;
            }

            if (placementTarget is ListBox listBox && listBox.SelectedItem is ShortcutItem selectedItem)
            {
                return selectedItem;
            }
        }

        return ShortcutList.SelectedItem as ShortcutItem;
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private void Group_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ShortcutGroup.Name))
        {
            UpdateTitle();
            if (!_isLoading)
            {
                _manager.SaveSettings();
            }
        }

        if (e.PropertyName is nameof(ShortcutGroup.ColorA) or nameof(ShortcutGroup.ColorR) or nameof(ShortcutGroup.ColorG) or nameof(ShortcutGroup.ColorB))
        {
            ApplyGroupAccent();
            if (!_isLoading)
            {
                _manager.SaveSettings();
            }
        }

        if (e.PropertyName == nameof(ShortcutGroup.IsEmpty))
        {
            UpdateSubtitleVisibility();
        }
    }

    private void AddWindowButton_Click(object sender, RoutedEventArgs e)
    {
        _manager.AddWindow(this);
    }

    private void RemoveWindowButton_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            GetText("RemoveWindowConfirm"),
            GetText("RemoveWindow"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes)
        {
            _manager.RemoveWindow(this);
        }
    }

    private void ColorButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ColorPickerWindow(this, _group.GetAccentColor());
        if (dialog.ShowDialog() == true)
        {
            _group.SetAccentColor(dialog.SelectedColor);
        }
    }

    private void GroupNameBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isLoading)
        {
            return;
        }

        UpdateTitle();
        _manager.SaveSettings();
    }

    private void GroupNameBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (GroupNameBox.IsReadOnly)
        {
            GroupNameBox.Focusable = false;
            try
            {
                BeginWindowDrag(e);
            }
            finally
            {
                GroupNameBox.Focusable = true;
            }
        }
    }

    private void RenameButton_Click(object sender, RoutedEventArgs e)
    {
        SetTitleEditing(true);
        GroupNameBox.Focus();
        GroupNameBox.SelectAll();
    }

    private void GroupNameBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            SetTitleEditing(false);
            Keyboard.ClearFocus();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            GroupNameBox.Text = string.IsNullOrWhiteSpace(_group.Name) ? GetDefaultGroupName() : _group.Name;
            SetTitleEditing(false);
            Keyboard.ClearFocus();
            e.Handled = true;
        }
    }

    private void GroupNameBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_group.Name))
        {
            _group.Name = GetDefaultGroupName();
        }

        SetTitleEditing(false);
    }

    private void ToggleSettings_Click(object sender, RoutedEventArgs e)
    {
        SettingsPanel.Visibility = SettingsPanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        var aboutWindow = new AboutWindow(this);
        aboutWindow.ShowDialog();
    }

    private void Hide_Click(object sender, RoutedEventArgs e)
    {
        _manager.HideWindow(this);
    }

    private void Window_MouseEnter(object sender, MouseEventArgs e) => Opacity = 1;

    private void Window_MouseLeave(object sender, MouseEventArgs e) => Opacity = GetIdleOpacity();

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        _windowHandle = new WindowInteropHelper(this).Handle;
        if (HwndSource.FromHwnd(_windowHandle) is HwndSource source)
        {
            source.AddHook(Window_HwndSourceHook);
        }

        ApplyToolWindowStyle();
        ApplyWindowMode();
    }

    /// <summary>
    /// 把卡片視窗設成「工具視窗」，讓它不出現在 Alt+Tab 清單，並拿掉最大化能力。
    /// 為什麼：ShowInTaskbar=False 只拿掉工作列按鈕，Alt+Tab 仍看得到；
    /// 而一般程式視窗被拖到螢幕邊緣時，Windows 會套用靠左／靠右（Snap）把它放大。
    /// 工具視窗＋不可最大化，Windows 就不會對它做這些處理（與 ai_usage-board 同一做法）。
    /// </summary>
    private void ApplyToolWindowStyle()
    {
        if (_windowHandle == IntPtr.Zero)
        {
            return;
        }

        var oldStyle = GetWindowLong(_windowHandle, GwlStyle);
        var oldExStyle = GetWindowLong(_windowHandle, GwlExStyle);
        var newStyle = RemoveMaximizeBox(oldStyle);
        var newExStyle = AddToolWindowExStyle(oldExStyle);
        if (newStyle == oldStyle && newExStyle == oldExStyle)
        {
            return;
        }

        SetWindowLong(_windowHandle, GwlStyle, newStyle);
        SetWindowLong(_windowHandle, GwlExStyle, newExStyle);

        // 樣式改變後要通知 Windows 重新套用（不移動、不改大小、不改前後順序、不搶焦點）
        SetWindowPos(
            _windowHandle,
            IntPtr.Zero,
            0,
            0,
            0,
            0,
            SwpNoMove | SwpNoSize | SwpNoZOrder | SwpNoActivate | SwpFrameChanged);
    }

    /// <summary>拿掉「可最大化」旗標，避免拖到螢幕上緣時被最大化。</summary>
    private static int RemoveMaximizeBox(int style) => style & ~WsMaximizeBox;

    /// <summary>加上「工具視窗」、拿掉「應用程式視窗」，讓視窗不出現在 Alt+Tab。</summary>
    private static int AddToolWindowExStyle(int exStyle) => (exStyle | WsExToolWindow) & ~WsExAppWindow;

    /// <summary>
    /// WPF 之後若因其他屬性變動而重寫視窗樣式，會把工具視窗旗標蓋掉；
    /// 在 Windows 套用新樣式之前（WM_STYLECHANGING）把旗標補回去，確保一直維持。
    /// </summary>
    private static void KeepToolWindowStyle(IntPtr wParam, IntPtr lParam)
    {
        var styleKind = wParam.ToInt32();
        var styleChange = Marshal.PtrToStructure<StyleStruct>(lParam);
        var correctedStyle = styleChange.StyleNew;

        if (styleKind == GwlStyle)
        {
            correctedStyle = RemoveMaximizeBox(styleChange.StyleNew);
        }
        else if (styleKind == GwlExStyle)
        {
            correctedStyle = AddToolWindowExStyle(styleChange.StyleNew);
        }

        if (correctedStyle != styleChange.StyleNew)
        {
            styleChange.StyleNew = correctedStyle;
            Marshal.StructureToPtr(styleChange, lParam, false);
        }
    }

    /// <summary>
    /// 視窗訊息 hook：
    /// 1. WM_STYLECHANGING：維持工具視窗樣式（不上 Alt+Tab、不被 Snap 放大）。
    /// 2. WM_NCHITTEST：將視窗外緣與四角的滑鼠命中位置交給 Windows，讓無框視窗使用原生尺寸調整行為。
    /// </summary>
    private IntPtr Window_HwndSourceHook(
        IntPtr hwnd,
        int message,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled)
    {
        if (message == WmStyleChanging)
        {
            // 只修正即將套用的值，不設 handled，讓 Windows 照常完成樣式變更
            KeepToolWindowStyle(wParam, lParam);
            return IntPtr.Zero;
        }

        if (message != WmNcHitTest || WindowState != WindowState.Normal || ResizeMode == ResizeMode.NoResize)
        {
            return IntPtr.Zero;
        }

        if (!GetWindowRect(hwnd, out var windowRect))
        {
            return IntPtr.Zero;
        }

        var screenPoint = lParam.ToInt64();
        var pointerX = unchecked((short)(screenPoint & 0xFFFF));
        var pointerY = unchecked((short)((screenPoint >> 16) & 0xFFFF));
        var resizeEdge = ResizeEdgeSize * VisualTreeHelper.GetDpi(this).DpiScaleX;
        var nearLeft = pointerX < windowRect.Left + resizeEdge;
        var nearRight = pointerX >= windowRect.Right - resizeEdge;
        var nearTop = pointerY < windowRect.Top + resizeEdge;
        var nearBottom = pointerY >= windowRect.Bottom - resizeEdge;

        var hitTest = 0;
        if (nearTop && nearLeft)
        {
            hitTest = HitTestTopLeft;
        }
        else if (nearTop && nearRight)
        {
            hitTest = HitTestTopRight;
        }
        else if (nearBottom && nearLeft)
        {
            hitTest = HitTestBottomLeft;
        }
        else if (nearBottom && nearRight)
        {
            hitTest = HitTestBottomRight;
        }
        else if (nearLeft)
        {
            hitTest = HitTestLeft;
        }
        else if (nearRight)
        {
            hitTest = HitTestRight;
        }
        else if (nearTop)
        {
            hitTest = HitTestTop;
        }
        else if (nearBottom)
        {
            hitTest = HitTestBottom;
        }

        if (hitTest == 0)
        {
            return IntPtr.Zero;
        }

        handled = true;
        return new IntPtr(hitTest);
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Loaded 時版面已排好，才量得到標題列的位置
        EnsureWindowIsOnScreen();
        PlaceBehindForegroundWindow();
    }

    /// <summary>
    /// Desktop 模式放在目前一般程式視窗後方、桌面圖層前方，避免卡片遮住工作內容或被桌面蓋住。
    /// </summary>
    private void PlaceBehindForegroundWindow()
    {
        if (Topmost || _windowHandle == IntPtr.Zero)
        {
            return;
        }

        var foregroundWindow = GetForegroundWindow();
        if (foregroundWindow == IntPtr.Zero || foregroundWindow == _windowHandle)
        {
            return;
        }

        var className = new StringBuilder(128);
        GetClassName(foregroundWindow, className, className.Capacity);
        var foregroundClass = className.ToString();
        if (foregroundClass is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd")
        {
            // 目前前景是桌面或工作列，卡片已在桌面上方，不要再壓到它們後面。
            return;
        }

        SetWindowPos(
            _windowHandle,
            foregroundWindow,
            0,
            0,
            0,
            0,
            SwpNoMove | SwpNoSize | SwpNoActivate);
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        PersistWindowBounds();
    }

    private void Window_LocationChanged(object? sender, EventArgs e)
    {
        PersistWindowBounds();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_allowClose)
        {
            return;
        }

        e.Cancel = true;
        _manager.HideWindow(this);
    }

    private void Window_DragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(ShortcutDragFormat))
        {
            e.Effects = DragDropEffects.Move;
        }
        else
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        }

        e.Handled = true;
    }

    /// <summary>
    /// Desktop 模式失去焦點後，將卡片放到新前景程式後方，仍保留在桌面圖層上方。
    /// </summary>
    private void Window_Deactivated(object? sender, EventArgs e)
    {
        PlaceBehindForegroundWindow();
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            AddShortcuts(paths);
            e.Handled = true;
        }
    }

    private void ShortcutList_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (ShortcutList.SelectedItem is not ShortcutItem)
        {
            e.Handled = true;
            return;
        }

        ShortcutList.ContextMenu = CreateShortcutContextMenu();
    }

    private void ShortcutList_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete && ShortcutList.SelectedItem is ShortcutItem item)
        {
            RemoveShortcut(item);
            e.Handled = true;
        }
    }

    private void ShortcutList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ShortcutList.SelectedItem is ShortcutItem item)
        {
            OpenPath(item.Path);
        }
    }

    private void ShortcutList_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var item = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);
        if (item is null)
        {
            ShortcutList.SelectedItem = null;
            return;
        }

        item.IsSelected = true;
    }

    /// <summary>
    /// 記錄清單內拖曳的起點與來源捷徑。
    /// </summary>
    private void ShortcutList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var item = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);
        _draggedShortcut = item?.DataContext as ShortcutItem;
        _shortcutDragStartPoint = e.GetPosition(ShortcutList);
    }

    /// <summary>
    /// 滑鼠移動超過 Windows 拖曳門檻後，開始同一清單內的捷徑排序拖曳。
    /// </summary>
    private void ShortcutList_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _draggedShortcut is null)
        {
            return;
        }

        var currentPoint = e.GetPosition(ShortcutList);
        var movedEnough = Math.Abs(currentPoint.X - _shortcutDragStartPoint.X) >= SystemParameters.MinimumHorizontalDragDistance ||
                          Math.Abs(currentPoint.Y - _shortcutDragStartPoint.Y) >= SystemParameters.MinimumVerticalDragDistance;
        if (!movedEnough)
        {
            return;
        }

        var data = new DataObject(ShortcutDragFormat, _draggedShortcut);
        DragDrop.DoDragDrop(ShortcutList, data, DragDropEffects.Move);
        _draggedShortcut = null;
    }

    /// <summary>
    /// 根據拖放內容顯示排序或新增捷徑的正確游標提示。
    /// </summary>
    private void ShortcutList_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(ShortcutDragFormat))
        {
            e.Effects = DragDropEffects.Move;
        }
        else if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }

        e.Handled = true;
    }

    /// <summary>
    /// 處理清單內排序，或把檔案總管拖入的檔案與資料夾加入清單。
    /// </summary>
    private void ShortcutList_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(ShortcutDragFormat) is ShortcutItem draggedItem)
        {
            ReorderShortcutAt(e.GetPosition(ShortcutList), draggedItem);
            e.Handled = true;
            return;
        }

        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            AddShortcuts(paths);
            e.Handled = true;
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            SetTitleEditing(false);
            SettingsPanel.Visibility = SettingsPanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            return;
        }

        SetTitleEditing(false);
        BeginWindowDrag(e);
    }

    private void WindowModeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading)
        {
            return;
        }

        var mode = WindowModeBox.SelectedItem?.ToString();
        if (!string.IsNullOrWhiteSpace(mode))
        {
            _settings.WindowMode = NormalizeWindowMode(mode);
            _manager.RefreshAllWindows();
            _manager.SaveSettings();
        }
    }

    /// <summary>保存新的主題風格並立即套用到所有卡片。</summary>
    private void ThemeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading)
        {
            return;
        }

        var themeName = ThemeBox.SelectedValue?.ToString();
        if (!string.IsNullOrWhiteSpace(themeName))
        {
            _settings.Theme = ThemePalette.NormalizeTheme(themeName);
            ColorSchemeBox.IsEnabled = _settings.Theme != ThemePalette.Neon;
            _manager.RefreshAllWindows();
            _manager.SaveSettings();
        }
    }

    /// <summary>保存亮暗色系選擇並立即套用到所有卡片。</summary>
    private void ColorSchemeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading)
        {
            return;
        }

        var colorScheme = ColorSchemeBox.SelectedValue?.ToString();
        if (!string.IsNullOrWhiteSpace(colorScheme))
        {
            _settings.ColorScheme = ThemePalette.NormalizeColorScheme(colorScheme);
            _manager.RefreshAllWindows();
            _manager.SaveSettings();
        }
    }

    private void LanguageBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading)
        {
            return;
        }

        _settings.Language = UiText.NormalizeLanguage(LanguageBox.SelectedValue?.ToString());
        _manager.RefreshAllWindows();
        _manager.SaveSettings();
    }

    private void TerminalShellBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading)
        {
            return;
        }

        _settings.TerminalShell = NormalizeTerminalShell(TerminalShellBox.SelectedValue?.ToString());
        _manager.SaveSettings();
    }

    private void StartWithWindowsBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading)
        {
            return;
        }

        var enabled = StartWithWindowsBox.IsChecked == true;
        _settings.StartWithWindows = enabled;
        SetStartWithWindows(enabled);
        _manager.SaveSettings();
    }

    private void OpenMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (ItemFromSender(sender) is ShortcutItem item)
        {
            OpenPath(item.Path);
        }
    }

    /// <summary>將目前捷徑往清單前方移一格。</summary>
    private void MoveShortcutUpMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (ItemFromSender(sender) is ShortcutItem item)
        {
            MoveShortcut(item, -1);
        }
    }

    /// <summary>將目前捷徑往清單後方移一格。</summary>
    private void MoveShortcutDownMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (ItemFromSender(sender) is ShortcutItem item)
        {
            MoveShortcut(item, 1);
        }
    }

    private void OpenParentMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (ItemFromSender(sender) is ShortcutItem item)
        {
            RevealPathInExplorer(item.Path);
        }
    }

    /// <summary>把右鍵選中的實體檔案或資料夾放進 Windows 剪貼簿。</summary>
    private void CopyItemMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (ItemFromSender(sender) is ShortcutItem item)
        {
            CopyItemToClipboard(item.Path);
        }
    }

    /// <summary>
    /// 將實體檔案或資料夾放進 Windows 剪貼簿，讓檔案總管負責後續完整複製。
    /// </summary>
    private void CopyItemToClipboard(string path)
    {
        if (!ShortcutPathExists(path))
        {
            MessageBox.Show(GetText("ItemMissing"), GetText("AppTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var fileDropList = new System.Collections.Specialized.StringCollection();
            fileDropList.Add(path);

            var clipboardData = new DataObject();
            clipboardData.SetFileDropList(fileDropList);

            // Windows 檔案總管會依這個旗標在貼上時複製，而不是移動來源項目。
            var copyEffect = new MemoryStream(BitConverter.GetBytes(1));
            clipboardData.SetData("Preferred DropEffect", copyEffect);
            Clipboard.SetDataObject(clipboardData, true);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                string.Format(GetText("CopyItemFailed"), exception.Message),
                GetText("AppTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    /// <summary>
    /// 開啟檔案總管並反白選中指定檔案或資料夾。
    /// </summary>
    private void RevealPathInExplorer(string path)
    {
        if (!ShortcutPathExists(path))
        {
            MessageBox.Show(GetText("ItemMissing"), GetText("AppTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"/select,\"{path}\"",
            UseShellExecute = true
        });
    }

    private void CopyPathMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (ItemFromSender(sender) is ShortcutItem item)
        {
            Clipboard.SetText(item.Path);
        }
    }

    private void OpenTerminalMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (ItemFromSender(sender) is ShortcutItem item)
        {
            OpenTerminal(item.Path);
        }
    }

    private void OpenCodexMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (ItemFromSender(sender) is ShortcutItem item)
        {
            OpenTerminal(item.Path, "codex");
        }
    }

    private void OpenClaudeMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (ItemFromSender(sender) is ShortcutItem item)
        {
            OpenTerminal(item.Path, "CLAUDE");
        }
    }

    private void OpenAgyMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (ItemFromSender(sender) is ShortcutItem item)
        {
            OpenTerminal(item.Path, "agy");
        }
    }

    private void ChangeIconMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (ItemFromSender(sender) is not ShortcutItem item)
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = GetText("ChooseIcon"),
            Filter = GetText("IconFilter"),
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) == true)
        {
            item.IconPath = dialog.FileName;
            item.RefreshIcon();
            _manager.SaveSettings();
        }
    }

    private void ResetIconMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (ItemFromSender(sender) is ShortcutItem item)
        {
            item.IconPath = null;
            item.RefreshIcon();
            _manager.SaveSettings();
        }
    }

    private void RemoveMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (ItemFromSender(sender) is ShortcutItem item)
        {
            RemoveShortcut(item);
        }
    }

    private sealed record LanguageOption(string Code, string DisplayName);

    private sealed record AppearanceOption(string Code, string DisplayName);

    private sealed record ShellOption(string Code, string DisplayName, string ExecutablePath);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    /// <summary>WM_STYLECHANGING 帶來的樣式資料：變更前與即將套用的樣式值。</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct StyleStruct
    {
        public int StyleOld;
        public int StyleNew;
    }

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out NativeRect rectangle);

    // 視窗樣式只用到低 32 位元，32／64 位元程式都可用 GetWindowLong／SetWindowLong
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr hWnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong(IntPtr hWnd, int index, int newLong);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder className, int maxCount);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint flags);
}
