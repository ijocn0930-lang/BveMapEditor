using ICSharpCode.AvalonEdit.CodeCompletion;
using System.IO;
using System.Windows;

namespace BveMapEditor
{
    public partial class MainWindow : Window
    {
        // 同じ文字が連続しているかチェックする（例: "ss", "ee" など）
        private bool HasConsecutiveCharacters(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            for (int i = 0; i < s.Length - 1; i++)
            {
                if (char.ToLower(s[i]) == char.ToLower(s[i + 1])) return true;
            }
            return false;
        }

        // ★ キャレットの手前にある現在の単語（入力中の文字列）を取得するヘルパーメソッド
        private string GetCurrentWordUnderCaret()
        {
            var editor = ActiveEditor;
            if (editor == null || editor.Document == null) return string.Empty;
            var document = editor.Document;
            int caretOffset = editor.CaretOffset;
            int startOffset = caretOffset;

            // 前方に向かってアルファベットやドット、アンダースコアなどの文字が続く間、開始位置を遡る
            while (startOffset > 0)
            {
                char c = document.GetCharAt(startOffset - 1);
                if (char.IsLetterOrDigit(c) || c == '.' || c == '_')
                {
                    startOffset--;
                }
                else
                {
                    break;
                }
            }
            return document.GetText(startOffset, caretOffset - startOffset);
        }

        // テキストが入力される直前
        private void MapTextEditor_TextEntering(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            try
            {
                var editor = ActiveEditor;
                if (_isOverwriteMode && !string.IsNullOrEmpty(e.Text) && editor != null && editor.Document != null)
                {
                    if (editor.SelectionLength == 0)
                    {
                        int offset = editor.CaretOffset;
                        int remaining = editor.Document.TextLength - offset;
                        int deleteCount = Math.Min(e.Text.Length, Math.Max(0, remaining));
                        if (deleteCount > 0)
                        {
                            editor.Document.Remove(offset, deleteCount);
                        }
                    }
                }
            }
            catch
            {
                // ignore
            }

            if (e.Text.Length > 0 && completionWindow != null)
            {
                if (e.Text == " ")
                {
                    completionWindow.Close();
                    completionWindow = null;
                    return;
                }

                if (!char.IsLetterOrDigit(e.Text[0]) && e.Text != ".")
                {
                    completionWindow.CompletionList.RequestInsertion(e);
                }
            }
        }

        // テキストが入力された直後（補完ウィンドウの呼び出し）
        private void MapTextEditor_TextEntered(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            var editor = ActiveEditor;
            if (currentSettings.AutoCompleteBrackets && (e.Text == "(" || e.Text == "["))
            {
                if (editor == null) return;
                int caretOffset = editor.CaretOffset;
                if (caretOffset > 1)
                {
                    char prevChar = editor.Document.GetCharAt(caretOffset - 1);
                    if (prevChar == '(' || prevChar == ')' || prevChar == '[' || prevChar == ']')
                    {
                        goto SkipBracketInsertion;
                    }
                }
                char closeChar = (e.Text == "(") ? ')' : ']';
                editor.Document.Insert(caretOffset, closeChar.ToString());
                editor.CaretOffset = caretOffset;
            }
        SkipBracketInsertion:
            if (!currentSettings.EnableAutoCompletion) return;
            if (e.Text.Length > 0 && (char.IsDigit(e.Text[0]) || e.Text == "."))
            {
                completionWindow?.Close();
                completionWindow = null;
                return;
            }

            string currentWord = GetCurrentWordUnderCaret();

            // すでに補完ウィンドウが開いている場合の処理を修正
            if (completionWindow != null)
            {
                var filteredItems = string.IsNullOrEmpty(currentWord)
                    ? cachedSyntaxes
                    : cachedSyntaxes.Where(item => item.Text.StartsWith(currentWord, StringComparison.OrdinalIgnoreCase)).ToArray();

                // 候補が0件になった場合、または連続文字が含まれる場合はウィンドウを閉じる
                if (filteredItems.Length == 0 || HasConsecutiveCharacters(currentWord))
                {
                    completionWindow.Close();
                    completionWindow = null;
                }
                else
                {
                    // 候補が存在する場合はリストを動的に更新する
                    var data = completionWindow.CompletionList.CompletionData;
                    data.Clear();
                    foreach (var item in filteredItems)
                    {
                        data.Add(item);
                    }
                    if (filteredItems.Length > 0)
                    {
                        completionWindow.CompletionList.SelectedItem = filteredItems[0];
                    }
                }
                return;
            }

            if (e.Text.Length > 0 && char.IsLetter(e.Text[0]))
            {
                if (HasConsecutiveCharacters(currentWord) || HasConsecutiveCharacters(e.Text)) return;
                var filteredItems = string.IsNullOrEmpty(currentWord)
                    ? cachedSyntaxes
                    : cachedSyntaxes.Where(item => item.Text.StartsWith(currentWord, StringComparison.OrdinalIgnoreCase)).ToArray();
                if (filteredItems.Length == 0 && e.Text != ".") return;
                var ed = editor;
                if (ed == null) return;
                completionWindow = new CompletionWindow(ed.TextArea)
                {
                    FontSize = ed.FontSize,
                    FontFamily = ed.FontFamily
                };
                double estimatedWidth = Math.Max(250, ed.FontSize * 18);
                completionWindow.Width = estimatedWidth;
                completionWindow.MaxHeight = 300;
                var data = completionWindow.CompletionList.CompletionData;
                foreach (var item in filteredItems)
                {
                    data.Add(item);
                }
                if (filteredItems.Length > 0)
                {
                    completionWindow.CompletionList.SelectedItem = filteredItems[0];
                }
                completionWindow.Show();
                completionWindow.Closed += (o, args) =>
                {
                    completionWindow = null;
                };
            }
        }
        // テキスト変更時のイベント
        private void MapTextEditor_TextChanged(object? sender, EventArgs e)
        {
            if (isLoading || isInitializing) return;

            var editor = sender as ICSharpCode.AvalonEdit.TextEditor ?? GetActiveEditor();
            var tabInfo = FindTabByEditor(editor);
            if (tabInfo != null)
            {
                if (!tabInfo.IsModified)
                {
                    tabInfo.IsModified = true;
                    if (editor == GetActiveEditor()) UpdateTitle();
                }
            }
            else
            {
                if (!isModified)
                {
                    isModified = true;
                    UpdateTitle();
                }
            }

            RunSyntaxCheck();
            UpdateEditMenuState();
        }

        // 構文解析を実行してDataGridおよび波線を更新（パイプ通信版）
        private void RunSyntaxCheck()
        {
            if (isLoading || isInitializing) return;

            _syntaxCts?.Cancel();
            _syntaxCts = new CancellationTokenSource();
            var token = _syntaxCts.Token;

            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(SyntaxDebounceDelay, token).ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    return;
                }

                if (token.IsCancellationRequested) return;

                string textSnapshot = string.Empty;
                string? mapFilePathSnapshot = null;

                await Dispatcher.InvokeAsync(() =>
                {
                    var editor = ActiveEditor ?? MapTextEditor;
                    textSnapshot = editor?.Text ?? string.Empty;
                    mapFilePathSnapshot = currentFilePath;
                });

                if (string.IsNullOrEmpty(mapFilePathSnapshot) || !File.Exists(mapFilePathSnapshot))
                {
                    var missingFileErrors = new List<ParseError>
                    {
                        new ParseError { Line = 1, Column = 1, Message = $"マップファイルが見つかりません: {mapFilePathSnapshot ?? "(パス未設定)"}" }
                    };
                    await Dispatcher.InvokeAsync(() =>
                    {
                        ErrorListDataGrid.ItemsSource = missingFileErrors;
                    });
                    return;
                }

                try
                {
                    string baseDirectory = Path.GetDirectoryName(mapFilePathSnapshot) ?? string.Empty;

                    // パイプ通信経由で別プロセスのパーサーに処理を委託
                    List<ParseError> errors = await ParseViaPipeAsync(textSnapshot, baseDirectory);

                    if (token.IsCancellationRequested) return;

                    await Dispatcher.InvokeAsync(() =>
                    {
                        ErrorListDataGrid.ItemsSource = null;
                        ErrorListDataGrid.ItemsSource = errors;

                        // アクティブなエディタの波線レンダラーを更新
                        var editor = ActiveEditor ?? MapTextEditor;
                        if (editor != null)
                        {
                            var renderer = GetOrCreateRenderer(editor);
                            renderer.SetErrors(errors);
                        }
                    });
                }
                catch (Exception ex)
                {
                    var exceptionErrors = new List<ParseError>
                    {
                        new ParseError { Line = 1, Column = 1, Message = $"構文チェック中にエラーが発生しました: {ex.Message}" }
                    };
                    await Dispatcher.InvokeAsync(() => ErrorListDataGrid.ItemsSource = exceptionErrors);
                }
            }, token);
        }

        // 即時フルパースを実行するメソッド（パイプ通信版）
        private async void RunSyntaxCheckImmediate()
        {
            _syntaxCts?.Cancel();
            _syntaxCts = new CancellationTokenSource();
            var token = _syntaxCts.Token;

            string textSnapshot = string.Empty;
            string? mapFilePathSnapshot = currentFilePath;

            await Dispatcher.InvokeAsync(() =>
            {
                var editor = ActiveEditor ?? MapTextEditor;
                textSnapshot = editor?.Text ?? string.Empty;
            });

            if (string.IsNullOrEmpty(mapFilePathSnapshot) || !File.Exists(mapFilePathSnapshot))
            {
                var missingFileErrors = new List<ParseError>
                {
                    new ParseError { Line = 1, Column = 1, Message = $"マップファイルが見つかりません: {mapFilePathSnapshot ?? "(パス未設定)"}" }
                };
                ErrorListDataGrid.ItemsSource = missingFileErrors;
                return;
            }

            try
            {
                string baseDirectory = Path.GetDirectoryName(mapFilePathSnapshot) ?? string.Empty;

                // パイプ通信経由で即時パース
                List<ParseError> errors = await ParseViaPipeAsync(textSnapshot, baseDirectory);

                if (token.IsCancellationRequested) return;

                await Dispatcher.InvokeAsync(() =>
                {
                    ErrorListDataGrid.ItemsSource = null;
                    ErrorListDataGrid.ItemsSource = errors;

                    var editor = ActiveEditor ?? MapTextEditor;
                    if (editor != null)
                    {
                        var renderer = GetOrCreateRenderer(editor);
                        renderer.SetErrors(errors);
                    }
                });
            }
            catch (OperationCanceledException)
            {
                // キャンセル時
            }
            catch (Exception ex)
            {
                var exceptionErrors = new List<ParseError>
                {
                    new ParseError { Line = 1, Column = 1, Message = $"構文チェック中にエラーが発生しました: {ex.Message}" }
                };
                await Dispatcher.InvokeAsync(() => ErrorListDataGrid.ItemsSource = exceptionErrors);
            }
        }
    }
}