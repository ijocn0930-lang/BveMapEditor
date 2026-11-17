using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace BveMapEditor
{
    //MainWindow.xaml.csが冗長になるため、分離した。内容はMainwindow.xaml.csに統合される。
    public partial class MainWindow : Window
    {
        // アプリ終了時にワーカープロセスも確実に終了させる このメソッドはMainwindow.xaml.csにあるため、このメソッド自体をコメントアウト解除しない
        //private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)ともう一つはOnClosing.csにある
        /*protected override void OnClosed(EventArgs e)
        {
            App.LogOperation("パーサーワーカーを終了 & セッション自動保存");
            try
            {
                // 自動復元が有効な場合、現在の状態を自動保存
                if (currentSettings != null && currentSettings.EnableAutoRestore)
                {
                    SaveAutoSession();
                }
            }
            catch { }

            try
            {
                if (_parserWorkerProcess != null && !_parserWorkerProcess.HasExited)
                {
                    _parserWorkerProcess.Kill();
                    _parserWorkerProcess.Dispose();
                }
            }
            catch { }
            base.OnClosed(e);
        }*/


        // 自動セッション保存のヘルパー
        private void SaveAutoSession()
        {
            var projectData = CollectCurrentProjectData();
            string autoSavePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "autosave.bveproj");

            // ★ 日本語パスが \uXXXX にエスケープされないように設定
            string json = JsonSerializer.Serialize(projectData, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
            File.WriteAllText(autoSavePath, json, Encoding.UTF8);
        }

        // 現在のタブとエクスプローラーの状態を収集する
        private BveProjectData CollectCurrentProjectData()
        {
            var data = new BveProjectData
            {
                RootFolderPath = _currentRootFolderPath ?? string.Empty
            };

            if (EditorTabControl != null)
            {
                foreach (var item in EditorTabControl.Items)
                {
                    if (item is TabItem ti && GetTabInfo(ti) is TabDocumentInfo info)
                    {
                        var session = new TabSessionItem
                        {
                            FilePath = info.FilePath, // 完全パス（無題の場合はnull）
                            IsActive = (EditorTabControl.SelectedItem == ti)
                        };
                        // ★ (2026-11-16) 「終了時のカーソル位置を記録する」がオンなら、カーソルの行・列も残す
                        if (currentSettings.RememberCaretPosition && info.Editor?.TextArea?.Caret != null)
                        {
                            session.CaretLine = info.Editor.TextArea.Caret.Line;
                            session.CaretColumn = info.Editor.TextArea.Caret.Column;
                        }
                        data.Tabs.Add(session);
                    }
                }
            }
            return data;
        }
        // ★ (2026-11-15) async void → async Task にした（起動時に渡されたファイルを、復元が終わってから開くため。App.xaml.cs で await する）
        public async Task TryRestoreAutoSessionAsync()
        {
            try
            {
                string autoSavePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "autosave.bveproj");
                if (currentSettings.EnableAutoRestore && File.Exists(autoSavePath))
                {
                    // ★ エンコーディングを考慮して安全に読み込む
                    string json = ReadTextWithEncodingFallback(autoSavePath);
                    var projectData = JsonSerializer.Deserialize<BveProjectData>(json);
                    if (projectData != null)
                    {
                        await LoadProjectDataAsync(projectData);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"自動復元に失敗しました: {ex.Message}");
            }
        }
        // メニュー：プロジェクトを開く (.bveproj)
        private async void MenuOpenProject_Click(object sender, RoutedEventArgs e)
        {
            App.LogOperation("プロジェクトファイルを開く");
            var openDlg = new OpenFileDialog
            {
                Title = "BVE プロジェクトを開く",
                Filter = "BVE Project File (*.bveproj)|*.bveproj|All Files (*.*)|*.*"
            };

            if (openDlg.ShowDialog() == true)
            {
                try
                {
                    // ★ エンコーディングを考慮して安全に読み込む
                    string json = ReadTextWithEncodingFallback(openDlg.FileName);
                    var projectData = JsonSerializer.Deserialize<BveProjectData>(json);
                    if (projectData != null)
                    {
                        // ★ (2026-11-15) プロジェクトを開くと今のタブは全部閉じるので、未保存のタブがあれば先に「保存しますか?」を聞く
                        //    （以前は確認なしで閉じていたため、書いた内容が消えていた）。キャンセルなら開かない
                        if (projectData.Tabs.Count > 0 && !PromptSaveModifiedTabs())
                        {
                            App.LogOperation("プロジェクトを開くのをキャンセル");
                            return;
                        }
                        await LoadProjectDataAsync(projectData);
                        _currentProjectPath = openDlg.FileName; // ★ 開いたプロジェクトパスを記憶
                        App.LogOperation($"プロジェクトを読み込みました: {openDlg.FileName}");
                    }

                }
                catch (Exception ex)
                {
                    MessageBox.Show($"プロジェクトの読み込みに失敗しました:\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        // メニュー：「プロジェクトの上書き保存」 (Ctrl+S や メニューの保存)
        private void MenuSaveProject_Click(object sender, RoutedEventArgs e)
        {
            App.LogOperation("プロジェクトの上書き保存");

            // すでにプロジェクトファイルパスが存在する場合は、ダイアログを出さずに上書き保存
            if (!string.IsNullOrEmpty(_currentProjectPath))
            {
                SaveProjectToFile(_currentProjectPath);
            }
            else
            {
                // まだパスが決まっていない（新規）場合は「名前を付けて保存」へ誘導
                MenuSaveProjectAs_Click(sender, e);
            }
        }

        // メニュー：「名前を付けてプロジェクトを保存」
        private void MenuSaveProjectAs_Click(object sender, RoutedEventArgs e)
        {
            App.LogOperation("名前を付けてプロジェクトを保存");
            var saveDlg = new SaveFileDialog
            {
                Title = "名前を付けて BVE プロジェクトを保存",
                Filter = "BVE Project File (*.bveproj)|*.bveproj|All Files (*.*)|*.*",
                DefaultExt = ".bveproj"
            };

            if (saveDlg.ShowDialog() == true)
            {
                _currentProjectPath = saveDlg.FileName; // パスを更新
                SaveProjectToFile(_currentProjectPath);
            }
        }
        // 実際のファイル書き込み処理（共通ヘルパーメソッド）
        private void SaveProjectToFile(string filePath)
        {
            try
            {
                var projectData = CollectCurrentProjectData();

                // ★ 日本語パスがエスケープされないように設定し、UTF-8で保存
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                string json = JsonSerializer.Serialize(projectData, options);
                File.WriteAllText(filePath, json, Encoding.UTF8);

                App.LogOperation($"プロジェクトを保存しました: {filePath}");
                MessageBox.Show("プロジェクトを保存しました。", "完了", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"プロジェクトの保存に失敗しました:\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        // プロジェクトファイル等を安全に読み込むためのエンコーディングフォールバックヘルパー
        private string ReadTextWithEncodingFallback(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            Encoding enc = DetectEncodingFromSample(bytes); // ★ ファイル全体で判定
            string text;
            try
            {
                text = enc.GetString(bytes);
            }
            catch
            {
                try { text = Encoding.UTF8.GetString(bytes); }
                catch { text = Encoding.Default.GetString(bytes); }
            }

            // ★ 先頭に残ったBOM（\uFEFF）を確実に除去する
            if (!string.IsNullOrEmpty(text) && text[0] == '\uFEFF')
            {
                text = text.Substring(1);
            }

            return text;
        }
        public async Task LoadProjectDataAsync(BveProjectData projectData)
        {
            // 1. エクスプローラーペインのルートフォルダ復元
            // ★ (2026-11-15) Windows・Program Files などのシステムのフォルダ（とその中）は開かない（知らせてエクスプローラーペインは空のまま）
            if (!string.IsNullOrEmpty(projectData.RootFolderPath) && IsForbiddenSystemPath(projectData.RootFolderPath))
            {
                MessageBox.Show(this, $"Windows や Program Files などのシステムのフォルダは開けません。エクスプローラーペインは読み込みません。\n\nフォルダ: {projectData.RootFolderPath}", "プロジェクトを開く", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else if (!string.IsNullOrEmpty(projectData.RootFolderPath) && Directory.Exists(projectData.RootFolderPath))
            {
                _currentRootFolderPath = projectData.RootFolderPath;
                SetupFileSystemWatcher(_currentRootFolderPath);
                try
                {
                    FileSystemNode? rootNode = await ScanDirectoryAsync(_currentRootFolderPath);
                    LoadFileSystemToTreeView(rootNode);
                }
                catch { }
            }

            // 2. タブの復元（ファイルパスからテキストを読み込み）
            if (EditorTabControl != null && projectData.Tabs.Count > 0)
            {
                ForgetAllTabs(); // ★ (2026-11-15) 閉じるタブのエディタをメモリから消せるようにする
                EditorTabControl.Items.Clear();
                TabItem? activeTabToSelect = null;

                foreach (var tabInfo in projectData.Tabs)
                {
                    // ★ 共通初期化を適用
                    var editor = new ICSharpCode.AvalonEdit.TextEditor();
                    InitializeEditor(editor);
                    string header = "無題";
                    string? filePath = tabInfo.FilePath;
                    Encoding? fileEncoding = null; // ★ 読み込んだエンコーディングを保持する変数
                    // ★ (2026-11-15) システムのフォルダの中のファイル・システムにかかわるファイル（vhd・pif など）は読み込まない（空の「無題」タブにする）
                    if (!string.IsNullOrEmpty(filePath) && (IsForbiddenSystemPath(filePath) || IsProtectedFile(Path.GetFileName(filePath))))
                    {
                        filePath = null;
                    }
                    // ファイルが存在する場合はディスクから読み込む
                    if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                    {
                        try
                        {
                            // ★ バイト配列として読み込み、エンコーディングを判定して文字列化する
                            byte[] bytes = await File.ReadAllBytesAsync(filePath);
                            fileEncoding = DetectEncodingFromSample(bytes); // ★ ファイル全体で判定
                            string? text;
                            try
                            {
                                text = fileEncoding.GetString(bytes);
                            }
                            catch
                            {
                                fileEncoding = Encoding.GetEncoding(932);
                                text = fileEncoding.GetString(bytes);
                            }
                            text = RemoveLeadingBom(text); // ★ 先頭の BOM 文字を取り除く

                            editor.Text = text;
                            header = Path.GetFileName(filePath);
                        }
                        catch
                        {
                            editor.Text = string.Empty;
                            filePath = null;
                            fileEncoding = null;
                        }
                    }
                    else
                    {
                        editor.Text = string.Empty;
                        filePath = null;
                        fileEncoding = null;
                    }

                    var tab = new TabItem { Header = header, Content = editor };
                    var docInfo = new TabDocumentInfo
                    {
                        Editor = editor,
                        FilePath = filePath,
                        IsModified = false,
                        FileEncoding = fileEncoding, // ★ 判定したエンコーディングを TabDocumentInfo に設定
                        TabItem = tab
                    };
                    tab.Tag = docInfo;
                    EditorTabControl.Items.Add(tab);
                    // ★ (2026-11-16) 記録したカーソルの位置に戻す（「終了時のカーソル位置を記録する」がオンで、記録があるとき）
                    if (currentSettings.RememberCaretPosition && tabInfo.CaretLine > 0 && !string.IsNullOrEmpty(filePath))
                    {
                        RestoreCaretPosition(editor, tabInfo.CaretLine, tabInfo.CaretColumn);
                    }

                    if (tabInfo.IsActive)
                    {
                        activeTabToSelect = tab;
                    }
                }

                if (activeTabToSelect != null)
                {
                    EditorTabControl.SelectedItem = activeTabToSelect;
                }
                else if (EditorTabControl.Items.Count > 0)
                {
                    EditorTabControl.SelectedIndex = 0;
                }
                // ★ 追加：プロジェクト読み込み・復元完了後にタイトルバーを更新
                UpdateTitle();
                UpdateEncodingStatus();
            }
        }
        /// <summary>
        /// ★ (2026-11-16) カーソルを記録した行・列に置き、画面に出たときにその行が見えるようにスクロールする
        /// ファイルが短くなっていたら、ある所まで（最後の行・行末）に合わせる
        /// </summary>
        private static void RestoreCaretPosition(ICSharpCode.AvalonEdit.TextEditor editor, int line, int column)
        {
            try
            {
                var doc = editor.Document;
                if (doc == null || doc.LineCount == 0) return;
                int l = Math.Clamp(line, 1, doc.LineCount);
                var docLine = doc.GetLineByNumber(l);
                int c = Math.Clamp(column, 1, docLine.Length + 1);
                editor.TextArea.Caret.Position = new ICSharpCode.AvalonEdit.TextViewPosition(l, c);

                // まだ画面に出ていない（タブを選んでいない）エディタはスクロールできないので、画面に出たときに1回だけスクロールする
                void ScrollOnce(object? s, RoutedEventArgs e)
                {
                    editor.Loaded -= ScrollOnce;
                    editor.Dispatcher.BeginInvoke(() =>
                    {
                        try { editor.ScrollTo(l, c); } catch { }
                    }, System.Windows.Threading.DispatcherPriority.Loaded);
                }
                if (editor.IsLoaded)
                {
                    editor.Dispatcher.BeginInvoke(() =>
                    {
                        try { editor.ScrollTo(l, c); } catch { }
                    }, System.Windows.Threading.DispatcherPriority.Loaded);
                }
                else
                {
                    editor.Loaded += ScrollOnce;
                }
            }
            catch
            {
                // 戻せなくてもファイルは開けているので何もしない
            }
        }

        // テキストエディタに対する共通の初期化処理
        private void InitializeEditor(ICSharpCode.AvalonEdit.TextEditor editor)
        {
            if (editor == null) return;
            editor.MinHeight = 120;
            // ★ (2026-11-15) フォント・右端で折り返す・テーマは設定から当てる（以前は最初のエディタのフォントを写すだけで、折り返し・テーマは写していなかった）
            ApplyViewSettingsToEditor(editor);
            editor.ShowLineNumbers = true;

            var options = editor.Options;
            options.EnableEmailHyperlinks = false;
            options.EnableHyperlinks = false;
            options.EnableRectangularSelection = false;
            options.AllowScrollBelowDocument = true;
            options.ShowBoxForControlCharacters = false;
            options.ShowSpaces = false;
            options.ShowEndOfLine = false;

            MapHighlighter.InitializeHighlighting(editor);

            editor.TextChanged += MapTextEditor_TextChanged;
            editor.TextArea.TextEntered += MapTextEditor_TextEntered;
            editor.TextArea.TextEntering += MapTextEditor_TextEntering;
            // ★ キーが空の [''] にカーソルが来たらキーの候補を出す
            editor.TextArea.Caret.PositionChanged += (s, ea) => OnCaretMovedForKeyCompletion(editor);

            // ★ 追加：Ctrl + マウスホイールによるフォントサイズ変更イベントを登録
            editor.PreviewMouseWheel += MapTextEditor_PreviewMouseWheel;
            // ★ (2026-11-16) 変数にマウスを乗せたら宣言を吹き出しで出す（AvalonEditClass.cs）
            editor.MouseHover += Editor_MouseHover;
            editor.MouseHoverStopped += Editor_MouseHoverStopped;
            // ★ (2026-11-16) CSV のタブで列を表示だけでそろえる（CsvColumnAligner.cs。設定と、マップでないタブかどうかで効く）
            editor.TextArea.TextView.ElementGenerators.Add(new CsvColumnAligner(editor, IsCsvAlignActive));

            var cm = new ContextMenu();
            cm.Items.Add(new MenuItem() { Header = "元に戻す(_U)", Command = ApplicationCommands.Undo });
            cm.Items.Add(new MenuItem() { Header = "やり直す(_R)", Command = ApplicationCommands.Redo });
            cm.Items.Add(new Separator());
            cm.Items.Add(new MenuItem() { Header = "切り取り(_T)", Command = ApplicationCommands.Cut });
            cm.Items.Add(new MenuItem() { Header = "コピー(_C)", Command = ApplicationCommands.Copy });
            cm.Items.Add(new MenuItem() { Header = "貼り付け(_P)", Command = ApplicationCommands.Paste });
            cm.Items.Add(new Separator());
            cm.Items.Add(new MenuItem() { Header = "すべて選択(_A)", Command = ApplicationCommands.SelectAll });
            ContextMenuService.SetContextMenu(editor, cm);

            editor.TextArea.KeyDown += (s, e) =>
            {
                try
                {
                    if (e.Key == Key.Insert)
                    {
                        if (currentSettings == null || currentSettings.AllowOverwriteToggle)
                        {
                            _isOverwriteMode = !_isOverwriteMode;
                            UpdateEditModeStatus();
                        }
                        e.Handled = true;
                    }
                }
                catch { }
            };

            editor.PreviewKeyDown += (s, e) =>
            {
                try
                {
                    if (e.Key == Key.Insert)
                    {
                        if (currentSettings == null || currentSettings.AllowOverwriteToggle)
                        {
                            _isOverwriteMode = !_isOverwriteMode;
                            UpdateEditModeStatus();
                        }
                        e.Handled = true;
                    }
                }
                catch { }
            };

            if (editor.Document != null && editor.Document.UndoStack != null)
            {
                editor.Document.UndoStack.PropertyChanged += UndoStack_PropertyChanged;
            }

            editor.TextArea.SelectionChanged += (s, e) =>
            {
                UpdateEditMenuState();
                UpdateCaretPositionStatus();
            };

            editor.TextArea.Caret.PositionChanged += (s, ea) =>
            {
                editor.TextArea.TextView.InvalidateVisual();
                UpdateCaretPositionStatus();
            };

            UpdateTitle();
            RunSyntaxCheck();
            GetOrCreateRenderer(editor);
            GetOrCreateCurrentLineRenderer(editor);
        }
    }
}
