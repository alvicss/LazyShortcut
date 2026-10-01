// =========================================================
// 檔案：UiText.cs
// 用途：集中提供繁體中文、英文與日文介面文字。
// 誰會用到：視窗、右鍵選單及系統匣選單依設定語系讀取文字。
// =========================================================
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectShortcutDock;

public static class UiText
{
    public const string DefaultLanguage = "en";

    private static readonly Dictionary<string, Dictionary<string, string>> Texts = new()
    {
        ["zh-TW"] = new Dictionary<string, string>
        {
            ["AppTitle"] = "Lazy Shortcut",
            ["EmptyHint"] = "將檔案或資料夾拖放到這裡",
            ["AddWindow"] = "新增視窗",
            ["RemoveWindow"] = "刪除視窗",
            ["RenameWindow"] = "重新命名",
            ["RemoveWindowConfirm"] = "要刪除這個群組視窗與其中記憶的捷徑嗎？這不會刪除原本的檔案或資料夾。",
            ["ChangeColor"] = "變更顏色",
            ["ColorDialogTitle"] = "卡片顏色",
            ["PickScreenColor"] = "從螢幕取色；按 Esc 可取消滴管",
            ["Preview"] = "預覽",
            ["Alpha"] = "透明度",
            ["Red"] = "紅",
            ["Green"] = "綠",
            ["Blue"] = "藍",
            ["ColorDialogHint"] = "ARGB 與 #AARRGGBB 會同步更新，透明度越低，卡片顏色越淡。",
            ["Cancel"] = "取消",
            ["Confirm"] = "確定",
            ["GroupDefaultName"] = "卡片",
            ["Settings"] = "設定",
            ["About"] = "關於",
            ["HideToTray"] = "隱藏到系統匣",
            ["Style"] = "樣式",
            ["ThemeMinimal"] = "簡約",
            ["ThemeGlass"] = "玻璃",
            ["ThemeTerminal"] = "終端機",
            ["ThemeNeon"] = "霓虹（僅暗色）",
            ["ColorScheme"] = "色系",
            ["SchemeAuto"] = "跟隨 Windows",
            ["SchemeLight"] = "亮色",
            ["SchemeDark"] = "暗色",
            ["ThemePreview"] = "主題預覽",
            ["ThemePreviewDetail"] = "資料夾名稱 · C:\\Projects\\Sample",
            ["WindowMode"] = "視窗模式",
            ["Language"] = "語系",
            ["TerminalShell"] = "Shell",
            ["StartWithWindows"] = "隨 Windows 啟動",
            ["Show"] = "顯示",
            ["ShowAll"] = "全部顯示",
            ["RestoreWindows"] = "將所有卡片移回主螢幕",
            ["MoveUp"] = "向上移",
            ["MoveDown"] = "向下移",
            ["Hide"] = "隱藏",
            ["HideAll"] = "全部隱藏",
            ["Exit"] = "結束",
            ["Open"] = "開啟",
            ["OpenParent"] = "在檔案總管中顯示",
            ["CopyItem"] = "複製項目",
            ["CopyItemFailed"] = "無法將項目放入剪貼簿。\n{0}",
            ["CopyPath"] = "複製路徑",
            ["OpenTerminal"] = "在終端機中開啟",
            ["OpenCodex"] = "用 Codex 開啟",
            ["OpenClaude"] = "用 Claude 開啟",
            ["OpenAgy"] = "用 Antigravity 開啟",
            ["ChangeIcon"] = "變更圖示...",
            ["ResetIcon"] = "重設圖示",
            ["RemoveShortcut"] = "移除捷徑",
            ["ItemMissing"] = "這個項目已不存在。",
            ["ChooseIcon"] = "選擇圖示",
            ["IconFilter"] = "圖示或圖片檔案|*.ico;*.png;*.jpg;*.jpeg;*.bmp;*.exe;*.dll|所有檔案|*.*"
        },
        ["en"] = new Dictionary<string, string>
        {
            ["AppTitle"] = "Lazy Shortcut",
            ["EmptyHint"] = "Drop files or folders here",
            ["AddWindow"] = "Add window",
            ["RemoveWindow"] = "Remove window",
            ["RenameWindow"] = "Rename",
            ["RemoveWindowConfirm"] = "Delete this group window and its remembered shortcuts? This will not delete the original files or folders.",
            ["ChangeColor"] = "Change color",
            ["ColorDialogTitle"] = "Card color",
            ["PickScreenColor"] = "Pick a screen color; press Esc to cancel",
            ["Preview"] = "Preview",
            ["Alpha"] = "Alpha",
            ["Red"] = "Red",
            ["Green"] = "Green",
            ["Blue"] = "Blue",
            ["ColorDialogHint"] = "ARGB and #AARRGGBB update together. Lower alpha makes the card color lighter.",
            ["Cancel"] = "Cancel",
            ["Confirm"] = "OK",
            ["GroupDefaultName"] = "Card",
            ["Settings"] = "Settings",
            ["About"] = "About",
            ["HideToTray"] = "Hide to tray",
            ["Style"] = "Style",
            ["ThemeMinimal"] = "Minimal",
            ["ThemeGlass"] = "Glass",
            ["ThemeTerminal"] = "Terminal",
            ["ThemeNeon"] = "Neon (dark only)",
            ["ColorScheme"] = "Color scheme",
            ["SchemeAuto"] = "Follow Windows",
            ["SchemeLight"] = "Light",
            ["SchemeDark"] = "Dark",
            ["ThemePreview"] = "Theme preview",
            ["ThemePreviewDetail"] = "Folder name · C:\\Projects\\Sample",
            ["WindowMode"] = "Window mode",
            ["Language"] = "Language",
            ["TerminalShell"] = "Shell",
            ["StartWithWindows"] = "Start with Windows",
            ["Show"] = "Show",
            ["ShowAll"] = "Show all",
            ["RestoreWindows"] = "Move all cards to primary screen",
            ["MoveUp"] = "Move up",
            ["MoveDown"] = "Move down",
            ["Hide"] = "Hide",
            ["HideAll"] = "Hide all",
            ["Exit"] = "Exit",
            ["Open"] = "Open",
            ["OpenParent"] = "Show in File Explorer",
            ["CopyItem"] = "Copy item",
            ["CopyItemFailed"] = "Could not copy the item to the clipboard.\n{0}",
            ["CopyPath"] = "Copy path",
            ["OpenTerminal"] = "Open in Terminal",
            ["OpenCodex"] = "Open with Codex",
            ["OpenClaude"] = "Open with Claude",
            ["OpenAgy"] = "Open with Antigravity",
            ["ChangeIcon"] = "Change icon...",
            ["ResetIcon"] = "Reset icon",
            ["RemoveShortcut"] = "Remove shortcut",
            ["ItemMissing"] = "This item no longer exists.",
            ["ChooseIcon"] = "Choose icon",
            ["IconFilter"] = "Icon or image files|*.ico;*.png;*.jpg;*.jpeg;*.bmp;*.exe;*.dll|All files|*.*"
        },
        ["ja"] = new Dictionary<string, string>
        {
            ["AppTitle"] = "Lazy Shortcut",
            ["EmptyHint"] = "ファイルまたはフォルダーをここにドロップ",
            ["AddWindow"] = "ウィンドウ追加",
            ["RemoveWindow"] = "ウィンドウ削除",
            ["RenameWindow"] = "名前変更",
            ["RemoveWindowConfirm"] = "このグループウィンドウと記憶されたショートカットを削除しますか？元のファイルやフォルダーは削除されません。",
            ["ChangeColor"] = "色を変更",
            ["ColorDialogTitle"] = "カードカラー",
            ["PickScreenColor"] = "画面から色を取得。Esc でスポイトを解除",
            ["Preview"] = "プレビュー",
            ["Alpha"] = "透明度",
            ["Red"] = "赤",
            ["Green"] = "緑",
            ["Blue"] = "青",
            ["ColorDialogHint"] = "ARGB と #AARRGGBB は同期して更新されます。透明度が低いほどカード色は薄くなります。",
            ["Cancel"] = "キャンセル",
            ["Confirm"] = "OK",
            ["GroupDefaultName"] = "カード",
            ["Settings"] = "設定",
            ["About"] = "About",
            ["HideToTray"] = "トレイに隠す",
            ["Style"] = "スタイル",
            ["ThemeMinimal"] = "ミニマル",
            ["ThemeGlass"] = "ガラス",
            ["ThemeTerminal"] = "ターミナル",
            ["ThemeNeon"] = "ネオン（暗色のみ）",
            ["ColorScheme"] = "配色",
            ["SchemeAuto"] = "Windows に合わせる",
            ["SchemeLight"] = "ライト",
            ["SchemeDark"] = "ダーク",
            ["ThemePreview"] = "テーマのプレビュー",
            ["ThemePreviewDetail"] = "フォルダー名 · C:\\Projects\\Sample",
            ["WindowMode"] = "ウィンドウモード",
            ["Language"] = "言語",
            ["TerminalShell"] = "Shell",
            ["StartWithWindows"] = "Windows 起動時に開始",
            ["Show"] = "表示",
            ["ShowAll"] = "すべて表示",
            ["RestoreWindows"] = "すべてのカードをメイン画面に戻す",
            ["MoveUp"] = "上へ移動",
            ["MoveDown"] = "下へ移動",
            ["Hide"] = "非表示",
            ["HideAll"] = "すべて非表示",
            ["Exit"] = "終了",
            ["Open"] = "開く",
            ["OpenParent"] = "エクスプローラーで表示",
            ["CopyItem"] = "項目をコピー",
            ["CopyItemFailed"] = "項目をクリップボードにコピーできませんでした。\n{0}",
            ["CopyPath"] = "パスをコピー",
            ["OpenTerminal"] = "ターミナルで開く",
            ["OpenCodex"] = "Codex で開く",
            ["OpenClaude"] = "Claude で開く",
            ["OpenAgy"] = "Antigravity で開く",
            ["ChangeIcon"] = "アイコンを変更...",
            ["ResetIcon"] = "アイコンをリセット",
            ["RemoveShortcut"] = "ショートカットを削除",
            ["ItemMissing"] = "この項目は存在しません。",
            ["ChooseIcon"] = "アイコンを選択",
            ["IconFilter"] = "アイコンまたは画像ファイル|*.ico;*.png;*.jpg;*.jpeg;*.bmp;*.exe;*.dll|すべてのファイル|*.*"
        }
    };

    private static readonly string[] SupportedLanguages = { "zh-TW", "en", "ja" };

    public static string NormalizeLanguage(string? language)
    {
        return SupportedLanguages.Any(x => string.Equals(x, language, StringComparison.OrdinalIgnoreCase))
            ? language!
            : DefaultLanguage;
    }

    public static string Get(string? language, string key)
    {
        var normalizedLanguage = NormalizeLanguage(language);
        return Texts[normalizedLanguage].TryGetValue(key, out var text)
            ? text
            : Texts[DefaultLanguage][key];
    }
}
