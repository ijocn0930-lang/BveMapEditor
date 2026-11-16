using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace BveMapEditor
{
    public partial class MainWindow : Window
    {
        // 「設定 -> フォント設定...」のクリック処理
        private void MenuFontSettings_Click(object sender, RoutedEventArgs e)
        {
            var editor = ActiveEditor ?? MapTextEditor;
            if (editor == null) return;

            var currentFamily = editor.FontFamily;
            var currentSize = editor.FontSize;

            var dialog = new FontSettingsDialog(currentFamily, currentSize)
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true)
            {
                // ★ null でない場合のみ適用と保存を行う
                if (dialog.SelectedFontFamily != null)
                {
                    editor.FontFamily = dialog.SelectedFontFamily;
                    currentSettings.FontFamilyName = dialog.SelectedFontFamily.Source;
                }

                editor.FontSize = dialog.SelectedFontSize;
                currentSettings.FontSize = dialog.SelectedFontSize;

                // 設定を保存
                currentSettings.Save();
            }
        }
        // Ctrl + マウスホイールによるフォントサイズの拡大・縮小（すべてのタブのエディタに一括反映）
        private void MapTextEditor_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            // Ctrlキーが押されているか確認
            if (System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control)
            {
                // 基準となる現在サイズ（アクティブエディタ、なければ MapTextEditor）
                var activeEditor = ActiveEditor ?? MapTextEditor;
                if (activeEditor == null) return;

                double currentSize = activeEditor.FontSize;
                if (e.Delta > 0)
                {
                    // 上方向（拡大）: 最大 72 まで
                    if (currentSize < 72)
                    {
                        currentSize += 1;
                    }
                }
                else
                {
                    // 下方向（縮小）: 最小 6 まで
                    if (currentSize > 6)
                    {
                        currentSize -= 1;
                    }
                }

                // ★ 開いているすべてのタブのエディタ、および初期エディタのフォントサイズを一括で更新する
                if (EditorTabControl != null)
                {
                    foreach (var item in EditorTabControl.Items)
                    {
                        if (item is TabItem ti && ti.Content is ICSharpCode.AvalonEdit.TextEditor tabEditor)
                        {
                            tabEditor.FontSize = currentSize;
                        }
                    }
                }

                // 万が一初期の MapTextEditor がタブ外にある場合のフォールバック
                MapTextEditor?.FontSize = currentSize;

                // 設定オブジェクト側も更新して保存
                currentSettings.FontSize = currentSize;
                try
                {
                    currentSettings.FontFamilyName = activeEditor.FontFamily.Source;
                }
                catch
                {
                    // ignore
                }
                currentSettings.Save();

                // イベントの伝播を止めてデフォルトのスクロール動作を抑制する
                e.Handled = true;
            }
        }
            // OSがダークモードかどうかを判定するヘルパー
        private bool IsSystemDarkMode()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                if (key != null)
                {
                    object? value = key.GetValue("AppsUseLightTheme");
                    if (value is int intValue)
                    {
                        return intValue == 0; // 0 ならダークモード
                    }
                }
            }
            catch
            {
                // 取得失敗時はライトとみなす
            }
            return false;
        }

        // テーマを適用するメインメソッド
        private void ApplyTheme(string themeMode)
        {
            bool isDark = themeMode switch
            {
                "Dark" => true,
                "Light" => false,
                _ /* "System" */ => IsSystemDarkMode()
            };

            var editor = ActiveEditor ?? MapTextEditor;
            if (editor != null)
            {
                if (isDark)
                {
                    // ダークテーマの色設定
                    editor.Background = new SolidColorBrush(Color.FromRgb(30, 30, 30));
                    editor.Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 220));
                }
                else
                {
                    // ライトテーマの色設定
                    editor.Background = Brushes.White;
                    editor.Foreground = Brushes.Black;
                }
            }

            // メニューのチェック状態を同期
            if (MenuThemeSystem != null) MenuThemeSystem.IsChecked = (themeMode == "System");
            if (MenuThemeLight != null) MenuThemeLight.IsChecked = (themeMode == "Light");
            if (MenuThemeDark != null) MenuThemeDark.IsChecked = (themeMode == "Dark");
        }
        //詳細設定
        private void MenuDetailSettings_Click(object sender, RoutedEventArgs e)
        {
            var detailWindow = new DetailSettingsWindow(currentSettings)
            {
                Owner = this
            };

            if (detailWindow.ShowDialog() == true)
            {
                // 設定が変更されたら必要に応じてファイルを保存
                currentSettings.Save();
            }
        }
        // ★2. 設定をエディタに反映するヘルパーメソッド
        private void ApplySettingsToEditor()
        {
            var editor = ActiveEditor ?? MapTextEditor;
            if (editor == null) return;

            try
            {
                editor.FontFamily = new FontFamily(currentSettings.FontFamilyName);
                editor.FontSize = currentSettings.FontSize;
                editor.WordWrap = currentSettings.WordWrap;
                MenuWordWrap?.IsChecked = currentSettings.WordWrap;
            }
            catch
            {
                // フォントが見つからない場合のフォールバック
                editor.FontFamily = new FontFamily("Consolas");
                editor.FontSize = 14.0;
                editor.WordWrap = false;
            }
            // ★ メニューのチェック状態に設定を反映
            MenuAutoCompletion?.IsChecked = currentSettings.EnableAutoCompletion;
            MenuAllowOverwriteToggle?.IsChecked = currentSettings.AllowOverwriteToggle;
            // ★ テーマの適用
            ApplyTheme(currentSettings.Theme);
        }
        // ★ テーマメニューがクリックされたときの処理
        private void ThemeMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is string themeKey)
            {
                currentSettings.Theme = themeKey;
                ApplyTheme(themeKey);
                currentSettings.Save(); // 設定を保存
            }
        }
        private void MenuAutoCompletion_Click(object sender, RoutedEventArgs e)
        {
            if (MenuAutoCompletion != null)
            {
                currentSettings.EnableAutoCompletion = MenuAutoCompletion.IsChecked;
                currentSettings.Save(); // 設定変更時に即座に保存
            }
        }

    }
}
