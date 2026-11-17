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
                    currentSettings.FontFamilyName = dialog.SelectedFontFamily.Source;
                }
                currentSettings.FontSize = dialog.SelectedFontSize;

                // ★ (2026-11-15) 全部のタブに当てる（以前は今のタブだけ変えていた）
                ApplyViewSettingsToAllEditors();

                // 設定を保存
                currentSettings.Save();
            }
        }

        // ===== ★ (2026-11-15) 表示の設定（フォント・右端で折り返す・テーマ）を全部のタブに当てる =====
        // 以前は今のタブ（ActiveEditor）だけを変えていたため、ほかのタブや新しいタブは前の設定のままだった

        // テーマがダークか（"System" のときは Windows の設定に合わせる）
        private bool IsDarkTheme()
        {
            return currentSettings.Theme switch
            {
                "Dark" => true,
                "Light" => false,
                _ /* "System" */ => IsSystemDarkMode()
            };
        }

        // 1つのエディタに表示の設定を当てる（新しいタブを作るときにも使う）
        private void ApplyViewSettingsToEditor(ICSharpCode.AvalonEdit.TextEditor? editor)
        {
            if (editor == null) return;
            try
            {
                editor.FontFamily = new FontFamily(currentSettings.FontFamilyName);
            }
            catch
            {
                editor.FontFamily = new FontFamily("Consolas"); // フォントが見つからない場合
            }
            editor.FontSize = currentSettings.FontSize > 0 ? currentSettings.FontSize : 14.0;
            editor.WordWrap = currentSettings.WordWrap;
            if (IsDarkTheme())
            {
                editor.Background = new SolidColorBrush(Color.FromRgb(30, 30, 30));
                editor.Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 220));
            }
            else
            {
                editor.Background = Brushes.White;
                editor.Foreground = Brushes.Black;
            }
            // ★ (2026-11-16) フォントが変わったら、CSV の列の幅も計算し直す（CsvColumnAligner）
            foreach (var aligner in editor.TextArea.TextView.ElementGenerators.OfType<CsvColumnAligner>().ToList())
            {
                aligner.ScheduleRecalculate();
            }
        }

        // 開いている全部のタブ（と最初のエディタ）に表示の設定を当てる
        private void ApplyViewSettingsToAllEditors()
        {
            if (EditorTabControl != null)
            {
                foreach (var item in EditorTabControl.Items)
                {
                    if (item is TabItem ti && ti.Content is ICSharpCode.AvalonEdit.TextEditor tabEditor)
                    {
                        ApplyViewSettingsToEditor(tabEditor);
                    }
                }
            }
            ApplyViewSettingsToEditor(MapTextEditor);
            MenuWordWrap?.IsChecked = currentSettings.WordWrap;
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

                // ★ マウスの下にある文字を中心に拡大・縮小するため、変更前の位置を覚えておく
                //    （以前は表示位置を合わせ直していなかったので、一番上を基準に拡大・縮小されていた）
                var wheelEditor = sender as ICSharpCode.AvalonEdit.TextEditor ?? activeEditor;
                var textView = wheelEditor.TextArea?.TextView;
                double oldSize = wheelEditor.FontSize;
                int anchorLine = 0;          // マウスの下の行番号
                double offsetInLine = 0;     // その行の上端からマウスまでの距離
                double mouseY = 0, mouseX = 0, oldHorizontal = 0;
                if (textView != null && textView.Document != null)
                {
                    var mouse = e.GetPosition(textView);
                    mouseY = Math.Max(0, Math.Min(mouse.Y, textView.ActualHeight));
                    mouseX = Math.Max(0, Math.Min(mouse.X, textView.ActualWidth));
                    double visualY = textView.VerticalOffset + mouseY;
                    var line = textView.GetDocumentLineByVisualTop(visualY);
                    if (line != null)
                    {
                        anchorLine = line.LineNumber;
                        offsetInLine = visualY - textView.GetVisualTopByDocumentLine(anchorLine);
                    }
                    oldHorizontal = textView.HorizontalOffset;
                }

                double currentSize = oldSize;
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

                // ★ (2026-11-16) 変数の吹き出しが出ていれば、その文字の大きさも合わせる（AvalonEditClass.cs の Editor_MouseHover）
                if (_variableToolTip?.IsOpen == true && _variableToolTip.Content is TextBlock tipText)
                {
                    tipText.FontSize = currentSize;
                }

                // ★ 文字の大きさを変えたあと、覚えておいた行がマウスの下に来るようにスクロールし直す
                if (textView != null && anchorLine > 0 && oldSize > 0 && currentSize != oldSize)
                {
                    try
                    {
                        wheelEditor.UpdateLayout(); // 新しい大きさで行の高さを計算し直す
                        double ratio = currentSize / oldSize;
                        int lineCount = textView.Document?.LineCount ?? 0;
                        if (anchorLine <= lineCount)
                        {
                            double newTop = textView.GetVisualTopByDocumentLine(anchorLine) + offsetInLine * ratio;
                            wheelEditor.ScrollToVerticalOffset(Math.Max(0, newTop - mouseY));
                        }
                        // 横方向も、マウスの下の文字がずれないように合わせる（折り返し表示のときは横スクロールなし）
                        if (!wheelEditor.WordWrap)
                        {
                            double newHorizontal = (oldHorizontal + mouseX) * ratio - mouseX;
                            wheelEditor.ScrollToHorizontalOffset(Math.Max(0, newHorizontal));
                        }
                    }
                    catch
                    {
                        // 位置合わせに失敗しても拡大・縮小自体はそのまま
                    }
                }

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
            // ★ (2026-11-15) 全部のタブに当てる（色は ApplyViewSettingsToEditor。以前は今のタブだけ変えていた）
            currentSettings.Theme = themeMode;
            ApplyViewSettingsToAllEditors();

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
            // ★ (2026-11-15) フォント・右端で折り返す・テーマは全部のタブに当てる（ApplyTheme の中で ApplyViewSettingsToAllEditors を呼ぶ）
            // ★ メニューのチェック状態に設定を反映
            MenuAutoCompletion?.IsChecked = currentSettings.EnableAutoCompletion;
            MenuKeyCompletion?.IsChecked = currentSettings.EnableKeyCompletion; // ★ キーの自動補完
            MenuAllowOverwriteToggle?.IsChecked = currentSettings.AllowOverwriteToggle;
            MenuRememberCaret?.IsChecked = currentSettings.RememberCaretPosition; // ★ (2026-11-16) 終了時のカーソル位置を記録する
            MenuVariableHover?.IsChecked = currentSettings.EnableVariableHover; // ★ (2026-11-16) 変数の宣言を吹き出しで表示する
            MenuAlignCsv?.IsChecked = currentSettings.AlignCsvColumns; // ★ (2026-11-16) CSV の列をそろえて表示する
            UpdateExplorerSpacingMenu(); // ★ (2026-11-17) エクスプローラーペインの行間
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
        // ★ 「キーの自動補完を有効にする」のクリック処理（すぐに設定ファイルへ保存）
        private void MenuKeyCompletion_Click(object sender, RoutedEventArgs e)
        {
            if (MenuKeyCompletion != null)
            {
                currentSettings.EnableKeyCompletion = MenuKeyCompletion.IsChecked;
                currentSettings.Save();
            }
        }
        // ★ (2026-11-16) 「終了時のカーソル位置を記録する」のクリック処理（すぐに設定ファイルへ保存）
        private void MenuRememberCaret_Click(object sender, RoutedEventArgs e)
        {
            if (MenuRememberCaret != null)
            {
                currentSettings.RememberCaretPosition = MenuRememberCaret.IsChecked;
                currentSettings.Save();
            }
        }
        // ★ (2026-11-16) 「変数の宣言を吹き出しで表示する」のクリック処理（すぐに設定ファイルへ保存。オフにしたら出ている吹き出しも消す）
        private void MenuVariableHover_Click(object sender, RoutedEventArgs e)
        {
            if (MenuVariableHover != null)
            {
                currentSettings.EnableVariableHover = MenuVariableHover.IsChecked;
                currentSettings.Save();
                if (!currentSettings.EnableVariableHover && _variableToolTip != null) _variableToolTip.IsOpen = false;
            }
        }
        // ★ (2026-11-17) 「エクスプローラーペインの行間」（コンパクト・標準・広い）のクリック処理（すぐに保存し、ツリーの全部の行に当てる）
        private void ExplorerSpacingMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem item || item.Tag is not string spacing) return;
            currentSettings.ExplorerRowSpacing = spacing;
            currentSettings.Save();
            UpdateExplorerSpacingMenu();
            ApplyExplorerSpacingToTree();
        }

        // 行間のメニューのチェックを、今の設定の 1 つだけに付ける
        private void UpdateExplorerSpacingMenu()
        {
            string s = NormalizeExplorerSpacing(currentSettings?.ExplorerRowSpacing);
            MenuExplorerSpacingCompact?.IsChecked = s == "Compact";
            MenuExplorerSpacingStandard?.IsChecked = s == "Standard";
            MenuExplorerSpacingWide?.IsChecked = s == "Wide";
        }

        // ★ (2026-11-16) 「CSV の列をそろえて表示する」のクリック処理（すぐに設定ファイルへ保存し、開いているタブの表示をすぐ変える）
        private void MenuAlignCsv_Click(object sender, RoutedEventArgs e)
        {
            if (MenuAlignCsv == null) return;
            currentSettings.AlignCsvColumns = MenuAlignCsv.IsChecked;
            currentSettings.Save();
            foreach (var editor in AllTabEditors())
            {
                foreach (var aligner in editor.TextArea.TextView.ElementGenerators.OfType<CsvColumnAligner>().ToList())
                {
                    aligner.Recalculate();
                }
            }
        }

        // ★ (2026-11-16) CSV の列をそろえるかどうか（設定がオンで、そのタブがリストファイルのとき）。CsvColumnAligner から呼ぶ
        //    (2026-11-17) 以前は「マップでないファイル」全部（他列車・車両・シナリオの .txt も）だったのを、
        //    リストファイル（.csv、または 1 行目が BveTs … List の .txt）だけにしぼった（IsListText）
        private bool IsCsvAlignActive(ICSharpCode.AvalonEdit.TextEditor editor)
        {
            if (!currentSettings.AlignCsvColumns) return false;
            string? path = FindTabByEditor(editor)?.FilePath;
            return IsListText(path, editor.Text);
        }

        // ★ (2026-11-16) 開いている全部のタブのエディタ
        private IEnumerable<ICSharpCode.AvalonEdit.TextEditor> AllTabEditors()
        {
            if (EditorTabControl == null) yield break;
            foreach (var item in EditorTabControl.Items)
            {
                if (item is TabItem ti && ti.Content is ICSharpCode.AvalonEdit.TextEditor te) yield return te;
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
