param(
[string]$Version = "2.2"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$dotnet = Join-Path $root ".dotnet\dotnet.exe"
if (-not (Test-Path $dotnet)) {
    $dotnet = "dotnet"
}

$artifacts = Join-Path $root "artifacts"
$buildOut = Join-Path $root "bin\Release\net10.0-windows"
$package = Join-Path $artifacts "package"
$app = Join-Path $package "app"
$uninstallExe = Join-Path $app "LazyShortcut.Uninstall.exe"
$setupExe = Join-Path $artifacts "LazyShortcut-Setup-$Version.exe"
$zipPath = Join-Path $artifacts "LazyShortcut-$Version-win-x64.zip"
$appZip = Join-Path $artifacts "app.zip"
$runtimeVersion = "10.0.9"
$runtimeInstallerName = "windowsdesktop-runtime-$runtimeVersion-win-x64.exe"
$runtimeInstallerUrl = "https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/$runtimeVersion/$runtimeInstallerName"
$runtimeInstallerPath = Join-Path $artifacts $runtimeInstallerName

Remove-Item -LiteralPath $artifacts -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $artifacts, $app, (Join-Path $app "image") | Out-Null

& $dotnet build (Join-Path $root "LazyShortcut.csproj") -c Release

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
Invoke-WebRequest -Uri $runtimeInstallerUrl -OutFile $runtimeInstallerPath

$requiredFiles = @(
    "LazyShortcut.exe",
    "LazyShortcut.dll",
    "LazyShortcut.deps.json",
    "LazyShortcut.runtimeconfig.json"
)

foreach ($file in $requiredFiles) {
    Copy-Item -LiteralPath (Join-Path $buildOut $file) -Destination $app -Force
}

Copy-Item -LiteralPath `
    (Join-Path $root "image\lazy-shortcut.ico"), `
    (Join-Path $root "image\codex.ico"), `
    (Join-Path $root "image\claude.ico"), `
    (Join-Path $root "image\agy.ico") `
    -Destination (Join-Path $app "image") -Force

$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) {
    $csc = Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe"
}
if (-not (Test-Path $csc)) {
    throw "Cannot find .NET Framework csc.exe for setup bootstrapper."
}

$setupIcon = Join-Path $root "image\lazy-shortcut.ico"
$appZipResource = "/resource:$appZip,app.zip"
$runtimeResource = "/resource:$runtimeInstallerPath,dotnet-runtime-installer.exe"

& $csc `
    /nologo `
    /target:winexe `
    /out:$uninstallExe `
    /win32icon:$setupIcon `
    /reference:System.Windows.Forms.dll `
    (Join-Path $root "tools\UninstallBootstrapper.cs")

$installScript = @'
$ErrorActionPreference = "Stop"

$installDir = Join-Path $env:LOCALAPPDATA "ProjectShortcutDock"
$startMenuDir = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\Lazy Shortcut"
$legacyStartMenuDir = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\Project Shortcut Dock"
$sourceDir = $PSScriptRoot
$appSource = Join-Path $sourceDir "app"

New-Item -ItemType Directory -Path $installDir -Force | Out-Null
@(
    "ProjectShortcutDock.exe",
    "ProjectShortcutDock.dll",
    "ProjectShortcutDock.deps.json",
    "ProjectShortcutDock.runtimeconfig.json",
    "ProjectShortcutDock.Uninstall.exe"
) | ForEach-Object {
    $obsoletePath = Join-Path $installDir $_
    if (Test-Path $obsoletePath) {
        Remove-Item -LiteralPath $obsoletePath -Force
    }
}
Copy-Item -Path (Join-Path $appSource "*") -Destination $installDir -Recurse -Force

if (Test-Path $legacyStartMenuDir) {
    Remove-Item -LiteralPath $legacyStartMenuDir -Recurse -Force
}
New-Item -ItemType Directory -Path $startMenuDir -Force | Out-Null
$targetPath = Join-Path $installDir "LazyShortcut.exe"
$uninstallPath = Join-Path $installDir "LazyShortcut.Uninstall.exe"
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut((Join-Path $startMenuDir "Lazy Shortcut.lnk"))
$shortcut.TargetPath = $targetPath
$shortcut.WorkingDirectory = $installDir
$shortcut.IconLocation = "$targetPath,0"
$shortcut.Save()

if (Test-Path $uninstallPath) {
    $uninstallShortcut = $shell.CreateShortcut((Join-Path $startMenuDir "Uninstall Lazy Shortcut.lnk"))
    $uninstallShortcut.TargetPath = $uninstallPath
    $uninstallShortcut.WorkingDirectory = $installDir
    $uninstallShortcut.IconLocation = "$targetPath,0"
    $uninstallShortcut.Save()
}

$uninstallKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\ProjectShortcutDock"
New-Item -Path $uninstallKey -Force | Out-Null
Set-ItemProperty -Path $uninstallKey -Name DisplayName -Value "Lazy Shortcut"
Set-ItemProperty -Path $uninstallKey -Name DisplayVersion -Value "{VERSION}"
Set-ItemProperty -Path $uninstallKey -Name Publisher -Value "alvicss"
Set-ItemProperty -Path $uninstallKey -Name InstallLocation -Value $installDir
Set-ItemProperty -Path $uninstallKey -Name DisplayIcon -Value $targetPath
Set-ItemProperty -Path $uninstallKey -Name UninstallString -Value ('"' + $uninstallPath + '"')
Set-ItemProperty -Path $uninstallKey -Name QuietUninstallString -Value ('"' + $uninstallPath + '"')
Set-ItemProperty -Path $uninstallKey -Name InstallDate -Value (Get-Date -Format yyyyMMdd)
Set-ItemProperty -Path $uninstallKey -Name NoModify -Type DWord -Value 1
Set-ItemProperty -Path $uninstallKey -Name NoRepair -Type DWord -Value 1

Start-Process -FilePath $targetPath
'@.Replace("{VERSION}", $Version)

Set-Content -LiteralPath (Join-Path $package "install.ps1") -Value $installScript -Encoding UTF8
Compress-Archive -Path (Join-Path $package "*") -DestinationPath $zipPath -Force
Compress-Archive -Path (Join-Path $app "*") -DestinationPath $appZip -Force

& $csc `
    /nologo `
    /target:winexe `
    /out:$setupExe `
    /win32icon:$setupIcon `
    $appZipResource `
    $runtimeResource `
    /reference:System.Windows.Forms.dll `
    /reference:System.IO.Compression.dll `
    /reference:System.IO.Compression.FileSystem.dll `
    (Join-Path $root "tools\SetupBootstrapper.cs")

Get-Item -LiteralPath $setupExe, $zipPath | Select-Object FullName, Length, LastWriteTime
