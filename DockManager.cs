// =========================================================
// 檔案：DockManager.cs
// 用途：管理捷徑群組視窗、系統匣選單與共用設定保存。
// 誰會用到：App 啟動時建立，負責協調所有 MainWindow。
// =========================================================
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace ProjectShortcutDock;

public sealed class DockManager : IDisposable
{
    private const double DefaultWidth = 360;
    private const double DefaultHeight = 260;
    // 多張卡片錯開排列時，每張往旁邊偏移的距離；MainWindow 把看不到的卡片拉回來時也會用到
    internal const double CascadeOffset = 26;

    private readonly Application _application;
    private readonly List<MainWindow> _windows = new();
    private readonly AppSettings _settings;
    private Forms.NotifyIcon? _trayIcon;
    private bool _isExiting;

    public DockManager(Application application)
    {
        _application = application;
        _settings = AppSettings.Load();
        _settings.Language = UiText.NormalizeLanguage(_settings.Language);
        SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;
        SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;
    }

    public AppSettings Settings => _settings;

    public void Initialize()
    {
        EnsureGroups();
        CreateTrayIcon();

        foreach (var group in _settings.Groups.ToList())
        {
            CreateWindow(group, activate: false);
        }

        ShowAllWindows();
        SaveSettings();
    }

    public void AddWindow(MainWindow sourceWindow)
    {
        var group = CreateNewGroup(sourceWindow.Group);
        _settings.Groups.Add(group);
        var window = CreateWindow(group, activate: true);
        window.FocusTitle();
        SaveSettings();
    }

    public void RemoveWindow(MainWindow window)
    {
        if (_settings.Groups.Count == 1)
        {
            var replacement = CreateNewGroup(window.Group);
            _settings.Groups.Add(replacement);
            CreateWindow(replacement, activate: true);
        }

        _settings.Groups.Remove(window.Group);
        CloseWindow(window);
        SaveSettings();
    }

    public void SaveSettings()
    {
        _settings.Shortcuts = new List<ShortcutItem>();
        _settings.Save();
    }

    public void RefreshAllWindows()
    {
        foreach (var window in _windows.ToList())
        {
            window.RefreshFromSharedSettings();
        }

        RefreshTrayMenu();
    }

    public void HideAllWindows()
    {
        foreach (var window in _windows.ToList())
        {
            window.Hide();
        }
    }

    public void HideWindow(MainWindow window)
    {
        if (_windows.Contains(window))
        {
            window.Hide();
        }
    }

    public void ShowAllWindows()
    {
        foreach (var window in _windows.ToList())
        {
            window.ShowFromManager();
        }
    }

    /// <summary>
    /// 將全部卡片移回主螢幕可操作範圍，供螢幕配置改變後手動救援。
    /// </summary>
    public void RestoreAllWindowsToPrimary()
    {
        for (var index = 0; index < _windows.Count; index++)
        {
            _windows[index].RestoreToPrimaryScreen(index);
        }

        SaveSettings();
    }

    public void ExitApplication()
    {
        _isExiting = true;
        Dispose();

        foreach (var window in _windows.ToList())
        {
            window.PrepareForClose();
            window.Close();
        }

        _application.Shutdown();
    }

    private void EnsureGroups()
    {
        var groups = _settings.Groups.Count > 0 ? _settings.Groups : CreateLegacyGroups();
        if (groups.Count == 0)
        {
            groups.Add(CreateNewGroup(null));
        }

        var workArea = SystemParameters.WorkArea;
        var baseLeft = _settings.Left ?? (workArea.Right - DefaultWidth - 18);
        var baseTop = _settings.Top ?? (workArea.Top + 18);

        for (var index = 0; index < groups.Count; index++)
        {
            var group = groups[index];
            group.Name = string.IsNullOrWhiteSpace(group.Name)
                ? $"{UiText.Get(_settings.Language, "GroupDefaultName")} {index + 1}"
                : group.Name;
            group.MigrateLegacyColorIfNeeded();
            group.Shortcuts = new ObservableCollection<ShortcutItem>(
                group.Shortcuts.Where(x => DirectoryExistsOrFileExists(x.Path)));
            group.Width = group.Width > 0 ? group.Width : (_settings.Width > 0 ? _settings.Width : DefaultWidth);
            group.Height = group.Height > 0 ? group.Height : (_settings.Height > 0 ? _settings.Height : DefaultHeight);
            group.Left ??= baseLeft + (CascadeOffset * index);
            group.Top ??= baseTop + (CascadeOffset * index);
        }

        _settings.Groups = groups;
    }

    private List<ShortcutGroup> CreateLegacyGroups()
    {
        var groups = new List<ShortcutGroup>();

        if (_settings.Groups.Count > 0)
        {
            groups.AddRange(_settings.Groups);
        }
        else if (_settings.Shortcuts.Count > 0)
        {
            groups.Add(new ShortcutGroup
            {
                Name = $"{UiText.Get(_settings.Language, "GroupDefaultName")} 1",
                Left = _settings.Left,
                Top = _settings.Top,
                Width = _settings.Width > 0 ? _settings.Width : DefaultWidth,
                Height = _settings.Height > 0 ? _settings.Height : DefaultHeight,
                Shortcuts = new ObservableCollection<ShortcutItem>(_settings.Shortcuts)
            });
        }

        return groups;
    }

    private ShortcutGroup CreateNewGroup(ShortcutGroup? sourceGroup)
    {
        var groupCount = _settings.Groups.Count + 1;
        return new ShortcutGroup
        {
            Name = $"{UiText.Get(_settings.Language, "GroupDefaultName")} {groupCount}",
            Left = (sourceGroup?.Left ?? _settings.Left ?? SystemParameters.WorkArea.Right - DefaultWidth - 18) + CascadeOffset,
            Top = (sourceGroup?.Top ?? _settings.Top ?? SystemParameters.WorkArea.Top + 18) + CascadeOffset,
            Width = sourceGroup?.Width > 0 ? sourceGroup.Width : DefaultWidth,
            Height = sourceGroup?.Height > 0 ? sourceGroup.Height : DefaultHeight
        };
    }

    private MainWindow CreateWindow(ShortcutGroup group, bool activate)
    {
        var window = new MainWindow(this, group);
        window.Closed += Window_Closed;
        _windows.Add(window);
        window.Show();
        if (activate)
        {
            window.Activate();
        }

        return window;
    }

    private void CloseWindow(MainWindow window)
    {
        window.PrepareForClose();
        window.Close();
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        if (sender is MainWindow window)
        {
            _windows.Remove(window);
        }

        if (!_isExiting && _windows.Count == 0)
        {
            ExitApplication();
        }
    }

    private void CreateTrayIcon()
    {
        var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "image", "lazy-shortcut.ico");
        _trayIcon = new Forms.NotifyIcon
        {
            Icon = System.IO.File.Exists(iconPath)
                ? new System.Drawing.Icon(iconPath)
                : System.Drawing.SystemIcons.Application,
            Text = UiText.Get(_settings.Language, "AppTitle"),
            Visible = true
        };
        _trayIcon.DoubleClick += (_, _) => ShowAllWindows();
        RefreshTrayMenu();
    }

    private void RefreshTrayMenu()
    {
        if (_trayIcon is null)
        {
            return;
        }

        _trayIcon.ContextMenuStrip?.Dispose();
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(UiText.Get(_settings.Language, "ShowAll"), null, (_, _) => ShowAllWindows());
        menu.Items.Add(UiText.Get(_settings.Language, "RestoreWindows"), null, (_, _) => RestoreAllWindowsToPrimary());
        menu.Items.Add(UiText.Get(_settings.Language, "HideAll"), null, (_, _) => HideAllWindows());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(UiText.Get(_settings.Language, "Exit"), null, (_, _) => ExitApplication());
        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.Text = UiText.Get(_settings.Language, "AppTitle");
    }

    private static bool DirectoryExistsOrFileExists(string path)
    {
        return System.IO.Directory.Exists(path) || System.IO.File.Exists(path);
    }

    public void Dispose()
    {
        SystemEvents.DisplaySettingsChanged -= SystemEvents_DisplaySettingsChanged;
        SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;
        if (_trayIcon is null)
        {
            return;
        }

        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _trayIcon = null;
    }

    /// <summary>
    /// 螢幕排列改變後，重新檢查卡片是否仍在可操作的工作區。
    /// </summary>
    private void SystemEvents_DisplaySettingsChanged(object? sender, EventArgs e)
    {
        _application.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
        {
            for (var index = 0; index < _windows.Count; index++)
            {
                _windows[index].EnsureVisibleAfterDisplayChange(index);
            }
        }));
    }

    /// <summary>
    /// Windows 外觀偏好改變時，更新選擇「跟隨 Windows」的卡片主題。
    /// </summary>
    private void SystemEvents_UserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        _application.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(RefreshAllWindows));
    }
}
