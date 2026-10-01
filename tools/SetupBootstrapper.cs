using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Windows.Forms;

internal static class SetupBootstrapper
{
    private const string AppName = "Lazy Shortcut";
    private const string ExeName = "LazyShortcut.exe";
    private const string LegacyExeName = "ProjectShortcutDock.exe";
    private const string UninstallExeName = "LazyShortcut.Uninstall.exe";
    private const string CurrentVersion = "2.2";
    private const string PublisherName = "alvicss";
    private const string StartMenuFolderName = "Lazy Shortcut";
    private const string LegacyStartMenuFolderName = "Project Shortcut Dock";
    private const string StartWithWindowsRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string UninstallRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\ProjectShortcutDock";
    private const string RuntimeInstallerResourceName = "dotnet-runtime-installer.exe";
    private const string RuntimeInstallerFileName = "windowsdesktop-runtime-10.0.9-win-x64.exe";
    private const string RuntimeManualInstallUrl = "https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-desktop-10.0.9-windows-x64-installer";
    private static readonly string[] ObsoleteInstalledFiles =
    {
        "ProjectShortcutDock.exe",
        "ProjectShortcutDock.dll",
        "ProjectShortcutDock.deps.json",
        "ProjectShortcutDock.runtimeconfig.json",
        "ProjectShortcutDock.Uninstall.exe"
    };

    [STAThread]
    private static int Main()
    {
        Application.EnableVisualStyles();

        try
        {
            EnsureDesktopRuntime();

            string installDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ProjectShortcutDock");
            Directory.CreateDirectory(installDir);

            EnsureInstalledAppIsClosed(installDir);
            ExtractApp(installDir);
            CreateStartMenuShortcuts(installDir);
            RegisterUninstallInfo(installDir);

            Process.Start(Path.Combine(installDir, ExeName));
            MessageBox.Show(
                AppName + " 已安裝完成。",
                AppName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                BuildFailureMessage(ex),
                AppName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return 1;
        }
    }

    private static void EnsureDesktopRuntime()
    {
        if (HasDesktopRuntime10())
        {
            return;
        }

        DialogResult result = MessageBox.Show(
            "此程式需要 .NET 10 Desktop Runtime。現在要安裝內嵌的 Microsoft 官方離線安裝包嗎？",
            AppName,
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result != DialogResult.Yes)
        {
            throw new InvalidOperationException("未安裝 .NET 10 Desktop Runtime。");
        }

        string installerPath = Path.Combine(Path.GetTempPath(), RuntimeInstallerFileName);
        try
        {
            ExtractEmbeddedFile(RuntimeInstallerResourceName, installerPath);
        }
        catch (Exception ex)
        {
            throw new RuntimeInstallException(
                "無法準備 .NET 10 Desktop Runtime 安裝檔。" + Environment.NewLine +
                "原因：" + ex.Message,
                ex);
        }

        Process process;
        try
        {
            process = Process.Start(new ProcessStartInfo
            {
                FileName = installerPath,
                Arguments = "/install /quiet /norestart",
                UseShellExecute = true,
                Verb = "runas"
            });
        }
        catch (Exception ex)
        {
            throw new RuntimeInstallException(
                "無法啟動 .NET 10 Desktop Runtime 安裝程式。" + Environment.NewLine +
                "原因：" + ex.Message,
                ex);
        }

        if (process == null)
        {
            throw new RuntimeInstallException("無法啟動 .NET 10 Desktop Runtime 安裝程式。");
        }

        process.WaitForExit();
        if (process.ExitCode != 0 && process.ExitCode != 3010)
        {
            throw new RuntimeInstallException(".NET 10 Desktop Runtime 安裝失敗，ExitCode=" + process.ExitCode + "。");
        }

        if (!HasDesktopRuntime10())
        {
            throw new RuntimeInstallException("找不到 .NET 10 Desktop Runtime。請重新啟動電腦或手動安裝後再試。");
        }
    }

    private static bool HasDesktopRuntime10()
    {
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string runtimeDir = Path.Combine(programFiles, "dotnet", "shared", "Microsoft.WindowsDesktop.App");
        if (Directory.Exists(runtimeDir))
        {
            foreach (string dir in Directory.GetDirectories(runtimeDir, "10.*"))
            {
                if (Directory.Exists(dir))
                {
                    return true;
                }
            }
        }

        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = "--list-runtimes",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(3000);
            return output.IndexOf("Microsoft.WindowsDesktop.App 10.", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        catch
        {
            return false;
        }
    }

    private static string BuildFailureMessage(Exception ex)
    {
        string message = "安裝失敗：" + Environment.NewLine + ex.Message;
        if (ex is RuntimeInstallException)
        {
            message += Environment.NewLine + Environment.NewLine +
                "請先手動安裝 .NET 10 Desktop Runtime (x64) 後，再重新執行安裝程式：" +
                Environment.NewLine + RuntimeManualInstallUrl;
        }
        else if (ex is IOException || ex is UnauthorizedAccessException)
        {
            message += Environment.NewLine + Environment.NewLine +
                "可能原因：舊版本仍在執行，或先前解除安裝尚未完全清理。" + Environment.NewLine +
                "請先確認 Lazy Shortcut 已完全結束，必要時刪除 %LOCALAPPDATA%\\ProjectShortcutDock 後再重新安裝。";
        }

        return message;
    }

    private static void ExtractEmbeddedFile(string resourceName, string destinationPath)
    {
        using (Stream resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
        {
            if (resource == null)
            {
                throw new InvalidOperationException("找不到內嵌資源：" + resourceName + "。");
            }

            using (var output = File.Create(destinationPath))
            {
                resource.CopyTo(output);
            }
        }
    }

    private static void ExtractApp(string installDir)
    {
        string tempZip = Path.Combine(Path.GetTempPath(), "LazyShortcut-app.zip");
        string stagingDir = Path.Combine(Path.GetTempPath(), "LazyShortcut-staging-" + Guid.NewGuid().ToString("N"));
        ExtractEmbeddedFile("app.zip", tempZip);

        try
        {
            if (Directory.Exists(stagingDir))
            {
                Directory.Delete(stagingDir, true);
            }

            Directory.CreateDirectory(stagingDir);
            ZipFile.ExtractToDirectory(tempZip, stagingDir);
            PrepareInstallDirectory(installDir);
            CopyDirectory(stagingDir, installDir);
        }
        finally
        {
            TryDeleteDirectory(stagingDir);
            TryDeleteFile(tempZip);
        }
    }

    private static void EnsureInstalledAppIsClosed(string installDir)
    {
        EnsureInstalledProcessIsClosed(installDir, ExeName);
        EnsureInstalledProcessIsClosed(installDir, LegacyExeName);
    }

    private static void PrepareInstallDirectory(string installDir)
    {
        if (!Directory.Exists(installDir))
        {
            Directory.CreateDirectory(installDir);
            return;
        }

        foreach (string file in Directory.GetFiles(installDir, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        foreach (string obsoleteFile in ObsoleteInstalledFiles)
        {
            string obsoletePath = Path.Combine(installDir, obsoleteFile);
            if (File.Exists(obsoletePath))
            {
                File.SetAttributes(obsoletePath, FileAttributes.Normal);
                File.Delete(obsoletePath);
            }
        }
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);

        foreach (string directory in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
        {
            string relativePath = directory.Substring(sourceDir.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            Directory.CreateDirectory(Path.Combine(destinationDir, relativePath));
        }

        foreach (string file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            string relativePath = file.Substring(sourceDir.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string destinationPath = Path.Combine(destinationDir, relativePath);
            string destinationDirectory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            if (File.Exists(destinationPath))
            {
                File.SetAttributes(destinationPath, FileAttributes.Normal);
            }

            File.Copy(file, destinationPath, true);
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (!Directory.Exists(path))
            {
                return;
            }

            foreach (string file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(path, true);
        }
        catch
        {
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    private static string TryGetProcessPath(Process process)
    {
        try
        {
            return process.MainModule != null ? process.MainModule.FileName : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static void CreateStartMenuShortcuts(string installDir)
    {
        RemoveLegacyStartMenuFolder();

        string startMenuDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft\\Windows\\Start Menu\\Programs",
            StartMenuFolderName);
        Directory.CreateDirectory(startMenuDir);

        string targetPath = Path.Combine(installDir, ExeName);
        string uninstallPath = Path.Combine(installDir, UninstallExeName);

        Type shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType == null)
        {
            return;
        }

        dynamic shell = Activator.CreateInstance(shellType);
        CreateShortcut(shell, Path.Combine(startMenuDir, "Lazy Shortcut.lnk"), targetPath, installDir, targetPath + ",0");
        if (File.Exists(uninstallPath))
        {
            CreateShortcut(shell, Path.Combine(startMenuDir, "Uninstall Lazy Shortcut.lnk"), uninstallPath, installDir, targetPath + ",0");
        }
    }

    private static void CreateShortcut(dynamic shell, string shortcutPath, string targetPath, string workingDirectory, string iconLocation)
    {
        dynamic shortcut = shell.CreateShortcut(shortcutPath);
        shortcut.TargetPath = targetPath;
        shortcut.WorkingDirectory = workingDirectory;
        shortcut.IconLocation = iconLocation;
        shortcut.Save();
    }

    private static void RegisterUninstallInfo(string installDir)
    {
        string targetPath = Path.Combine(installDir, ExeName);
        string uninstallPath = Path.Combine(installDir, UninstallExeName);
        string displayVersion = FileVersionInfo.GetVersionInfo(targetPath).ProductVersion ?? CurrentVersion;

        using (RegistryKey key = Registry.CurrentUser.CreateSubKey(UninstallRegistryPath))
        {
            if (key == null)
            {
                throw new InvalidOperationException("無法建立解除安裝登錄資訊。");
            }

            key.SetValue("DisplayName", AppName);
            key.SetValue("DisplayVersion", displayVersion);
            key.SetValue("Publisher", PublisherName);
            key.SetValue("InstallLocation", installDir);
            key.SetValue("DisplayIcon", targetPath);
            key.SetValue("UninstallString", "\"" + uninstallPath + "\"");
            key.SetValue("QuietUninstallString", "\"" + uninstallPath + "\"");
            key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
            key.SetValue("NoModify", 1, RegistryValueKind.DWord);
            key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        }

        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(StartWithWindowsRegistryPath, true))
        {
            if (key == null)
            {
                return;
            }

            object existingValue = key.GetValue("ProjectShortcutDock");
            if (existingValue == null)
            {
                return;
            }

            key.SetValue("ProjectShortcutDock", "\"" + targetPath + "\"");
        }
    }

    private static void EnsureInstalledProcessIsClosed(string installDir, string exeName)
    {
        string installedExePath = Path.Combine(installDir, exeName);
        string processName = Path.GetFileNameWithoutExtension(exeName);

        foreach (Process process in Process.GetProcessesByName(processName))
        {
            try
            {
                string processPath = TryGetProcessPath(process);
                if (!string.Equals(processPath, installedExePath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!process.HasExited)
                {
                    process.CloseMainWindow();
                    if (!process.WaitForExit(5000))
                    {
                        throw new InvalidOperationException(
                            "偵測到舊版 Lazy Shortcut 仍在執行。" + Environment.NewLine +
                            "請先從系統匣結束程式，或在工作管理員中關閉 " + exeName + " 後再重新安裝。");
                    }
                }
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    private static void RemoveLegacyStartMenuFolder()
    {
        string legacyDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft\\Windows\\Start Menu\\Programs",
            LegacyStartMenuFolderName);

        if (!Directory.Exists(legacyDir))
        {
            return;
        }

        foreach (string file in Directory.GetFiles(legacyDir, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(legacyDir, true);
    }

    private sealed class RuntimeInstallException : Exception
    {
        public RuntimeInstallException(string message)
            : base(message)
        {
        }

        public RuntimeInstallException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
