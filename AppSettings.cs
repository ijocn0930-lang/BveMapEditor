using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.IO;
using System.Windows.Media;

namespace BveMapEditor
{
    class AppSettings
    {
        //フォントファミリー
        public string FontFamilyName { get; set; } = "Consolas";
        //fontSize
        public double FontSize { get; set; } = 14.0;
        public bool WordWrap { get; set; } = false; // 右端で折り返す
        // ★ 追加：ウィンドウサイズ・状態とペインサイズ
        public double WindowWidth { get; set; } = 1050;
        public double WindowHeight { get; set; } = 600;
        public bool IsMaximized { get; set; } = false;
        //エクスプローラーペインサイズ
        public double ExplorerPaneWidth { get; set; } = 230;
        // ★ 追加：自動補完の有効/無効（デフォルトは有効）
        public bool EnableAutoCompletion { get; set; } = true;
        // ★ 追加：テーマ設定 ("System", "Light", "Dark")
        public string Theme { get; set; } = "System";
        // ★ 追加：検索・置換関連の設定と履歴
        public bool RetainSearchHistory { get; set; } = true;         // 検索履歴を保持するフラグ
        // ★ 単一の文字列から、履歴を保存するリスト（List<string>）に変更
        public List<string> SearchHistory { get; set; } = new List<string>();
        public List<string> ReplaceHistory { get; set; } = new List<string>();
        public bool LastMatchCase { get; set; } = false;             // 大文字小文字の区別
        public bool LastLoopSearch { get; set; } = true;             // ループ検索
        public bool LastWarnLargeReplace { get; set; } = true;       // 大量置換時の警告
        // ★ 追加：単語単位で検索するフラグ
        public bool LastWholeWord { get; set; } = false;
        // 閉じ括弧の自動補完を有効にするかどうか（デフォルトは true）
        public bool AutoCompleteBrackets { get; set; } = true;
        // ★ 追加：BVE5/6の実行ファイルパス
        public string BveExecutablePath { get; set; } = string.Empty;
        // ★ 追加：QuickMapViewer の実行ファイルパス
        public string QuickMapViewerExecutablePath { get; set; } = string.Empty;
        // ステータスバーの表示状態
        public bool ShowStatusBar { get; set; } = true;
        // Insertキーで上書きモード切替を許可するか
        public bool AllowOverwriteToggle { get; set; } = true;
        // 保存時にエンコーディングを確認するか（true = 確認ダイアログを表示、false = 元のエンコーディングを保持）
        public bool ConfirmEncodingOnSave { get; set; } = true;
        // 特定ファイルごとに上書き保存時のエンコーディングをスキップするためのマップ
        // キー: 絶対ファイルパス, 値: Encoding.WebName（例: "utf-8", "shift_jis"）
        // ★ 追加：自動復元機能の有効/無効（デフォルトは有効）
        public bool EnableAutoRestore { get; set; } = true;
        public Dictionary<string, string> PerFileEncodingMap { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly string SettingsFilePath = "settings.json"; //jsonの場所

        // 設定をファイルから読み込む
        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    string json = File.ReadAllText(SettingsFilePath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    if (settings != null) return settings;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"設定ファイルの読み込みに失敗しました: {ex.Message}");
            }

            // 読み込めなかった場合はデフォルト値を返す
            return new AppSettings();
        }

        // 設定をファイルに保存する
        public void Save()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(this, options);
                File.WriteAllText(SettingsFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"設定ファイルの保存に失敗しました: {ex.Message}");
            }
        }
    }
}
