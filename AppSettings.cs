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
        public string FontFamilyName { get; set; } = "Consolas";
        public double FontSize { get; set; } = 14.0;
        public bool WordWrap { get; set; } = false; // ★追加
        // ★ 追加：ウィンドウサイズ・状態とペインサイズ
        public double WindowWidth { get; set; } = 1050;
        public double WindowHeight { get; set; } = 600;
        public bool IsMaximized { get; set; } = false;
        public double ExplorerPaneWidth { get; set; } = 230;
        // ★ 追加：自動補完の有効/無効（デフォルトは有効）
        public bool EnableAutoCompletion { get; set; } = true;
        // ★ 追加：テーマ設定 ("System", "Light", "Dark")
        public string Theme { get; set; } = "System";
        private static readonly string SettingsFilePath = "settings.json";

        // 設定をファイルから読み込む
        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    string json = File.ReadAllText(SettingsFilePath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    if (settings != null)
                    {
                        return settings;
                    }
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
