using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using System.Windows.Controls;
using System.Windows.Media;

namespace BveMapEditor
{
public partial class MainWindow : Window
        {
        //初期化処理を書かないこと

        private void MenuOpen_Click(object sender, ExecutedRoutedEventArgs e)
        {
            // 1. 未保存の変更（isModified）がある場合の警告
            if (isModified)
            {
                var result = MessageBox.Show(
                    "編集中の内容に変更が保存されていません。\n" +
                    "保存せずに新しいファイルを開きますか？",
                    "未保存の変更の確認 - BveMapEditor",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.No)
                {
                    return; // 「いいえ」が選ばれた場合は開く処理を中断
                }
            }
            // 2. ErrorListDataGrid にエラーが残っている場合の警告
            bool hasErrors = false;
            if (ErrorListDataGrid.ItemsSource is System.Collections.IEnumerable items)
            {
                foreach (var item in items)
                {
                    hasErrors = true;
                    break;
                }
            }

            if (hasErrors)
            {
                var result = MessageBox.Show(
                    "現在の画面（エラー一覧）にエラーやメッセージが残っています。\n" +
                    "別のファイルを開くと現在の編集内容やエラー情報は破棄されますが、よろしいですか？",
                    "エラー残存の確認 - BveMapEditor",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.No)
                {
                    return; // 「いいえ」が選ばれた場合は開く処理を中断
                }
            }
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Map File (*.txt)|*.txt|All Files (*.*)|*.*",
                Title = "マップファイルを開く"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                // ★ ユーザーが選択した実際のファイルパスを代入する
                string mapFilePath = openFileDialog.FileName;

                // ファイル存在チェック（実際のパスを使用）
                if (!File.Exists(mapFilePath))
                {
                    var errors = new List<ParseError>
        {
            new ParseError
            {
                Line = 1,
                Column = 1,
                Message = $"マップファイルが見つかりません: {mapFilePath}"
            }
        };
                    ErrorListDataGrid.ItemsSource = errors;
                    return;
                }
                try
                {
                    // 実際のファイルのテキストを読み込む (UTF-8 を試し、失敗時は ANSI にフォールバック)
                    string mapText = ReadAllTextWithAnsiFallback(mapFilePath);

                    // ★ 実際のファイルが置かれているフォルダのパスを基準ディレクトリにする
                    string baseDirectory = Path.GetDirectoryName(mapFilePath) ?? string.Empty;

                    // パーサーに渡して実行
                    List<ParseError> errors = MapParser.Parse(mapText, baseDirectory);
                    ErrorListDataGrid.ItemsSource = errors;
                }
                catch (Exception ex)
                {
                    var errors = new List<ParseError>
        {
            new ParseError
            {
                Line = 1,
                Column = 1,
                Message = $"ファイルの読み込み中にエラーが発生しました: {ex.Message}"
            }
        };
                    ErrorListDataGrid.ItemsSource = errors; return;
                }
                OpenFile(openFileDialog.FileName);
            }
        }
        private void OpenFile(string filePath)
        {
            try
            {
// ★ 変更：StreamReaderでBOMの有無を正確に取得しつつテキストを読み込む
                string fileContent;
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    // 第二引数の true により、BOMの有無を自動判別して CurrentEncoding に反映させます
                    using (var sr = new StreamReader(fs, true))
                    {
                        fileContent = sr.ReadToEnd();
                        currentFileEncoding = sr.CurrentEncoding; // ここでBOM有無を正確に保持
                                          }
                }

                // ★ ヘッダー（例: "BveTs Map"）が含まれているかチェックする
                // 大文字小文字を区別しないように、あるいは先頭部分を重点的に探します
                bool hasValidHeader = fileContent.Contains("BveTs Map", StringComparison.OrdinalIgnoreCase);

                if (!hasValidHeader)
                {
                    var result = MessageBox.Show(
                        "このテキストファイルはMapファイルではありませんが、そのまま続けますか？",
                        "ファイル形式の確認",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result == MessageBoxResult.No)
                    {
                        // 「いいえ」が選ばれた場合は読み込みを中止する
                        return;
                    }
                }
                // 1. ファイルサイズのチェック（例: 1MB = 1,048,576 バイトを超える場合）
                var fileInfo = new FileInfo(filePath);
                long fileSizeInBytes = fileInfo.Length;
                // 1. ファイルサイズが大きすぎる場合（例: 30MB超）の事前警告
                const long warningThreshold = 30 * 1024 * 1024; // 30MB
                if (fileSizeInBytes > warningThreshold)
                {
                    double fileSizeInMb = (double)fileSizeInBytes / (1024 * 1024);
                    var result = MessageBox.Show(
                        $"選択されたファイルは非常に大きいです（約 {fileSizeInMb:F1} MB）。\n" +
                        "読み込みに時間がかかり、お使いのPCのメモリを多く消費する可能性があります。\n\n" +
                        "本当にこのファイルを開きますか？",
                        "メモリ使用量の警告 - BveMapEditor",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result == MessageBoxResult.No)
                    {
                        return; // ユーザーがキャンセルした場合
                    }
                }

                isLoading = true;

// エディタ（AvalonEdit）へのデータ展開
                string content = fileContent;

                // 3. エディタ（AvalonEdit）へのデータ展開時におけるメモリ不足の保護
                try
                {
                    // 既にファイルが開かれている場合は新しいタブを追加してそちらに読み込む
                    // 判定は currentFilePath ではなくアクティブタブのメタ情報を参照して行う
                    var activeTabInfo = (EditorTabControl != null && EditorTabControl.SelectedItem is TabItem sti) ? GetTabInfo(sti) : null;
                    bool shouldCreateNewTab = activeTabInfo != null && !string.IsNullOrEmpty(activeTabInfo.FilePath) && EditorTabControl != null;
                    if (shouldCreateNewTab)
                    {
                        var newEditor = new ICSharpCode.AvalonEdit.TextEditor()
                        {
                            FontFamily = ActiveEditor?.FontFamily ?? MapTextEditor?.FontFamily ?? new FontFamily("Consolas"),
                            FontSize = ActiveEditor?.FontSize ?? MapTextEditor?.FontSize ?? 14.0,
                            ShowLineNumbers = true,
                            MinHeight = 120
                        };

                        // TextChanged イベントを購読してタブごとの変更フラグを管理
                        newEditor.TextChanged += MapTextEditor_TextChanged;


                        // 簡易的なコンテキストメニューを設定
                        var cm = new ContextMenu();
                        cm.Items.Add(new MenuItem() { Header = "元に戻す(_U)", Command = ApplicationCommands.Undo });
                        cm.Items.Add(new MenuItem() { Header = "やり直す(_R)", Command = ApplicationCommands.Redo });
                        cm.Items.Add(new Separator());
                        cm.Items.Add(new MenuItem() { Header = "切り取り(_T)", Command = ApplicationCommands.Cut });
                        cm.Items.Add(new MenuItem() { Header = "コピー(_C)", Command = ApplicationCommands.Copy });
                        cm.Items.Add(new MenuItem() { Header = "貼り付け(_P)", Command = ApplicationCommands.Paste });
                        cm.Items.Add(new Separator());
                        cm.Items.Add(new MenuItem() { Header = "すべて選択(_A)", Command = ApplicationCommands.SelectAll });
                        ContextMenuService.SetContextMenu(newEditor, cm);

                        // AvalonEdit のハイライト初期化と補完イベントの購読
                        try
                        {
                            MapHighlighter.InitializeHighlighting(newEditor);
                            newEditor.TextArea.TextEntered += MapTextEditor_TextEntered;
                            newEditor.TextArea.TextEntering += MapTextEditor_TextEntering;
                            if (newEditor.Document?.UndoStack != null)
                            {
                                newEditor.Document.UndoStack.PropertyChanged += UndoStack_PropertyChanged;
                            }
                        }
                        catch
                        {
                            // 初期化失敗は無視して続行
                        }

                        // テキストを設定
                        MapHighlighter.SetText(newEditor, content);

                        // タブメタ情報を作成して設定
                        var tab = new TabItem()
                        {
                            Header = Path.GetFileName(filePath),
                            Content = newEditor
                        };

                        var info = new TabDocumentInfo { Editor = newEditor, FilePath = filePath, IsModified = false, FileEncoding = currentFileEncoding, TabItem = tab };
                        tab.Tag = info;

                        // タブ作成して追加、選択
                        EditorTabControl!.Items.Add(tab);
                        EditorTabControl!.SelectedItem = tab;
                    }
                    else
                    {
                        // アクティブエディタへ読み込む場合はそのタブのメタ情報を更新
                        var targetEditor = ActiveEditor ?? MapTextEditor;
                        MapHighlighter.SetText(targetEditor, content);
                        var active = (EditorTabControl != null && EditorTabControl.SelectedItem is TabItem at) ? GetTabInfo(at) : null;
                        if (active != null)
                        {
                            active.FilePath = filePath;
                            active.FileEncoding = currentFileEncoding;
                            active.IsModified = false;
                            // タブヘッダを更新
                            if (active.TabItem != null) active.TabItem.Header = Path.GetFileName(filePath);
                        }
                    }
                }
                catch (OutOfMemoryException)
                {
                    MessageBox.Show(
                        "テキストをエディタへ展開する際にメモリが不足しました。\n" +
                        "処理を中止します。",
                        "メモリ不足エラー",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    // 安全のためエディタを空にする
                    MapHighlighter.SetText(ActiveEditor ?? MapTextEditor, string.Empty);
                    return;
                }
                currentFilePath = filePath;
                isModified = false;
                UpdateTitle();

                // 読み込み完了後の Undo履歴クリア & 構文チェック
                (ActiveEditor ?? MapTextEditor)?.Document?.UndoStack?.ClearAll();
                UpdateEditMenuState();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"ファイルの読み込みに失敗しました:\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                isLoading = false;
                UpdateTitle();
                RunSyntaxCheck();
                UpdateEditMenuState(); // ★追加
                                       // ★ ロード完了直後に、デバウンスをスキップして即時フルパースを1回強制実行する！
                RunSyntaxCheckImmediate();
            }
        }
        //名前を付けて保存
        private void SaveFileAs()
        {
            // 1. 保存前に現在のテキストで構文チェックを走らせ、最新のエラー一覧を取得する
            var editorForSaveAs = ActiveEditor ?? MapTextEditor;
            string mapText = editorForSaveAs != null ? editorForSaveAs.Text : string.Empty;
            var errors = MapParser.Parse(mapText); // ※既存のパーサー呼び出しに合わせて調整してください

            if (ErrorListDataGrid.ItemsSource is System.Collections.ICollection errorCollection && errorCollection.Count > 0)
            {
                var result = MessageBox.Show(
                    $"構文エラーが {errorCollection.Count} 件存在します。このまま保存しますか？",
                    "警告", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result == MessageBoxResult.No) return;
            }

            var saveFileDialog = new SaveFileDialog
            {
                Filter = "Map File (*.txt)|*.txt|All Files (*.*)|*.*",
                Title = "名前を付けて保存",
                DefaultExt = ".txt"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                SaveFile(saveFileDialog.FileName);
            }
        }
        // ファイル書き込み処理
        private void SaveFile(string filePath, Encoding? forcedEncoding = null, bool useDialogIfNull = true)
        {
            try
            {
                // AvalonEdit からテキストを取得
                var editorForWrite = ActiveEditor ?? MapTextEditor;
                string content = MapHighlighter.GetText(editorForWrite);

                // 優先順位:
                // 1. forcedEncoding が指定されていればそれを使う
                // 2. useDialogIfNull == true の場合はダイアログで選ばせる（Keep を選べば currentFileEncoding を使う）
                // 3. useDialogIfNull == false の場合は currentFileEncoding を使い、無ければ UTF-8 にフォールバック
                Encoding? encodingToUse = forcedEncoding;

                if (encodingToUse == null)
                {
                    if (useDialogIfNull)
                    {
                        var dialogResult = SaveEncodingDialog.PromptEncoding(this, currentFileEncoding);
                        // ダイアログで Cancel された場合は保存を中止する
                        if (!dialogResult.Confirmed)
                        {
                            return;
                        }
                        // SelectedEncoding が null なら "Keep" が選ばれた扱い -> currentFileEncoding を使う（それも null なら UTF-8）
                        encodingToUse = dialogResult.SelectedEncoding ?? currentFileEncoding ?? new UTF8Encoding(false);
                    }
                    else
                    {
                        encodingToUse = currentFileEncoding ?? new UTF8Encoding(false);
                    }
                }

                bool wasOverwrite = File.Exists(filePath);
                try
                {
                    File.WriteAllText(filePath, content, encodingToUse);
                }
                catch (EncoderFallbackException)
                {
                    // 選択したエンコーディングでエンコードできない文字がある場合は UTF-8 にフォールバックして保存
                    var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
                    File.WriteAllText(filePath, content, utf8NoBom);
                }

                // 保存に成功したら現在のファイルパス・エンコーディング情報を更新
                currentFilePath = filePath;
                currentFileEncoding = encodingToUse;
                // 上書き保存だった場合は、特定ファイルのエンコーディング設定として記憶して次回以降の確認を省略できるようにする
                try
                {
                    if (wasOverwrite && currentSettings != null)
                    {
                        try
                        {
                            string webName = encodingToUse.WebName;
                            currentSettings.PerFileEncodingMap[filePath] = webName;
                            currentSettings.Save();
                        }
                        catch
                        {
                            // 記憶に失敗しても保存自体は成功しているので無視
                        }
                    }
                }
                catch
                {
                    // ignore
                }
                isModified = false;
                UpdateTitle();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"ファイルの保存に失敗しました:\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void MenuSave_Click(object sender, ExecutedRoutedEventArgs e)
        {
            // 1. 保存前に現在のテキストで構文チェックを走らせ、最新のエラー一覧を取得する
            var editorForSave = ActiveEditor ?? MapTextEditor;
            string mapText = editorForSave != null ? editorForSave.Text : string.Empty;
            var errors = MapParser.Parse(mapText); // ※既存のパーサー呼び出しに合わせて調整してください

            if (ErrorListDataGrid.ItemsSource is System.Collections.ICollection errorCollection && errorCollection.Count > 0)
            {
                var result = MessageBox.Show(
                    $"構文エラーが {errorCollection.Count} 件存在します。このまま保存しますか？",
                    "警告", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result == MessageBoxResult.No) return;
            }

            if (string.IsNullOrEmpty(currentFilePath))
            {
                // 新規ファイルの場合は名前を付けて保存フローへ
                MenuSaveAs_Click(sender, e);
                return;
            }

            // 上書き保存: まず既にこのファイルに対して記憶されたエンコーディングがないか確認
            if (!string.IsNullOrEmpty(currentFilePath) && currentSettings.PerFileEncodingMap.TryGetValue(currentFilePath, out var savedWebName))
            {
                try
                {
                    var enc = Encoding.GetEncoding(savedWebName);
                    SaveFile(currentFilePath, enc, useDialogIfNull: false);
                    return;
                }
                catch
                {
                    // 無効な指定なら続行して通常フローへ
                }
            }

            // グローバル設定で確認がオフの場合は元のエンコーディングを保持して保存
            if (!currentSettings.ConfirmEncodingOnSave)
            {
                SaveFile(currentFilePath, currentFileEncoding, useDialogIfNull: false);
                return;
            }

            // それ以外は確認ダイアログを表示して選択
            var dialogResult = SaveEncodingDialog.PromptEncoding(this, currentFileEncoding);
            if (!dialogResult.Confirmed)
            {
                // ユーザーがキャンセルした場合は保存中止
                return;
            }

            Encoding? encodingToUse = dialogResult.SelectedEncoding ?? currentFileEncoding;
            SaveFile(currentFilePath, encodingToUse, useDialogIfNull: false);
        }
        // 名前を付けて保存 (Ctrl+Shift+S 等)
        private void MenuSaveAs_Click(object sender, ExecutedRoutedEventArgs e)
        {
            // まずエンコーディングを選択
            var dialogResult = SaveEncodingDialog.PromptEncoding(this, currentFileEncoding);
            if (!dialogResult.Confirmed)
            {
                // ユーザーがキャンセルした場合は保存中止
                return;
            }

            // 次に保存先を選ばせる
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "Map File (*.txt)|*.txt|All Files (*.*)|*.*",
                Title = "名前を付けて保存",
                DefaultExt = ".txt"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                Encoding? encodingToUse = dialogResult.SelectedEncoding ?? currentFileEncoding;
                SaveFile(saveFileDialog.FileName, encodingToUse, useDialogIfNull: false);
            }
        }
        // ファイルを実際にエディタで開くメソッド（既存のファイルオープン処理をここにまとめる） エクスプローラーペインからの操作です。メニューからの操作ではありません。

        private void OpenFileInEditor(string filePath)
        {
            try
            {
                // すでに実装されているファイル読み込み・エディタ表示処理をここに呼び出します
                // 例: LoadFileToEditor(filePath);
                OpenFile(filePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"ファイルを開けませんでした:\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }



    }
}
