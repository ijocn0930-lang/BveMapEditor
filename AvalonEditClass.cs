using ICSharpCode.AvalonEdit.CodeCompletion;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;


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
                if (char.ToLower(s[i]) == char.ToLower(s[i + 1]))
                {
                    return true;
                }
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
            // 上書きモード時は、入力される直前に既存の文字を削除しておく
            try
            {
                var editor = ActiveEditor;
                if (_isOverwriteMode && !string.IsNullOrEmpty(e.Text) && editor != null && editor.Document != null)
                {
                    // 選択範囲がある場合はそのまま置換されるので削除不要
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
                // ignore any issue and allow normal insertion
            }

            if (e.Text.Length > 0 && completionWindow != null)
            {
                // ★ スペースキーが押されたときは、候補を勝手に確定させず、補完ウィンドウを閉じて通常のスペース入力を通す
                if (e.Text == " ")
                {
                    completionWindow.Close();
                    completionWindow = null;
                    return;
                }
                // アルファベット・数字・ピリオド以外の記号で確定させる
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
            // 1. 閉じ括弧の自動挿入処理（「AutoCompleteBrackets」設定でのみ制御する）
            if (currentSettings.AutoCompleteBrackets && (e.Text == "(" || e.Text == "["))
            {
                if (editor == null) return;
                int caretOffset = editor.CaretOffset;

                // 【安全装置】直前の文字をチェックし、連続または混合している場合は自動補完しない
                if (caretOffset > 1)
                {
                    char prevChar = editor.Document.GetCharAt(caretOffset - 1); // 新しく入力された文字の直前の文字

                    // 直前の文字も括弧（'('、')'、'['、']'）である場合
                    if (prevChar == '(' || prevChar == ')' || prevChar == '[' || prevChar == ']')
                    {
                        goto SkipBracketInsertion; // 連続・混合している場合はスキップ
                    }
                }

                // 対応する閉じ括弧を決める
                char closeChar = (e.Text == "(") ? ')' : ']';

                // 閉じ括弧を自動挿入する
                editor.Document.Insert(caretOffset, closeChar.ToString());

                // カーソルを元の位置（開き括弧と閉じ括弧の間）に戻す
                editor.CaretOffset = caretOffset;
            }

        SkipBracketInsertion:

            // 2. 全体の自動補完機能（ポップアップ）が無効な場合はここで終了
            if (!currentSettings.EnableAutoCompletion)
            {
                return;
            }

            // ★ 数字、またはピリオドが入力された場合は補完を開かない（開いている場合は閉じる）
            if (e.Text.Length > 0 && (char.IsDigit(e.Text[0]) || e.Text == "."))
            {
                if (completionWindow != null)
                {
                    completionWindow.Close();
                    completionWindow = null;
                }
                return;
            }

            string currentWord = GetCurrentWordUnderCaret();

            // ★ すでに補完ウィンドウが開いている最中に、同じ文字が連続するようになったら閉じる
            if (completionWindow != null)
            {
                if (HasConsecutiveCharacters(currentWord))
                {
                    completionWindow.Close();
                    completionWindow = null;
                }
                return;
            }

            // '.' が入力された、またはアルファベットが入力されたタイミング
            if (e.Text.Length > 0 && char.IsLetter(e.Text[0]))
            {
                if (HasConsecutiveCharacters(currentWord) || HasConsecutiveCharacters(e.Text))
                {
                    return;
                }

                // 外部のプロバイダからマスターリストを取得する
                var filteredItems = string.IsNullOrEmpty(currentWord)
                ? cachedSyntaxes
                : cachedSyntaxes.Where(item => item.Text.StartsWith(currentWord, StringComparison.OrdinalIgnoreCase)).ToArray();
                if (filteredItems.Length == 0 && e.Text != ".")
                {
                    return;
                }

                var ed = editor;
                if (ed == null) return;

                completionWindow = new CompletionWindow(ed.TextArea);
                completionWindow.FontSize = ed.FontSize;
                completionWindow.FontFamily = ed.FontFamily;

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

            // どのエディタが変更されたかを判定してタブ情報を更新
            var editor = sender as ICSharpCode.AvalonEdit.TextEditor ?? GetActiveEditor();
            var tabInfo = FindTabByEditor(editor);
            if (tabInfo != null)
            {
                if (!tabInfo.IsModified)
                {
                    tabInfo.IsModified = true;
                    // アクティブなタブが変更された場合はタイトルを更新
                    if (editor == GetActiveEditor()) UpdateTitle();
                }
            }
            else
            {
                // フォールバック: 既存のグローバルフラグを使う
                if (!isModified)
                {
                    isModified = true;
                    UpdateTitle();
                }
            }

            RunSyntaxCheck();
            UpdateEditMenuState(); // ★追加
        }
        // 構文解析を実行してDataGridを更新
        private void RunSyntaxCheck()
        {
            if (isLoading || isInitializing) return;
            // デバウンス: 直前のチェックをキャンセルして少し待ってから実行
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

                // Capture editor and text on UI thread to avoid cross-thread access
                string textSnapshot = string.Empty;
                string? mapFilePathSnapshot = null;
                int caretLine = 1;
                int totalLines = 0; // ★ ここで事前に宣言しておく
                await Dispatcher.InvokeAsync(() =>
                {
                    var editor = ActiveEditor ?? MapTextEditor;
                    textSnapshot = editor?.Text ?? string.Empty;
                    mapFilePathSnapshot = currentFilePath;
                    try
                    {
                        if (editor != null)
                        {
                            caretLine = editor.TextArea.Caret.Line;
                            totalLines = editor.Document.LineCount; // ★ UIスレッド上で LineCount を取得する
                        }
                    }
                    catch
                    {
                        // ignore
                    }
                });

                if (string.IsNullOrEmpty(mapFilePathSnapshot) || !File.Exists(mapFilePathSnapshot))
                {
                    var missingFileErrors = new List<ParseError>
                    {
                        new ParseError { Line = 1, Column = 1, Message = $"マップファイルが見つかりません: {mapFilePathSnapshot ?? "(パス未設定)"}" }
                    };
                    await Dispatcher.InvokeAsync(() => ErrorListDataGrid.ItemsSource = missingFileErrors);
                    return;
                }

                try
                {
                    string baseDirectory = Path.GetDirectoryName(mapFilePathSnapshot) ?? string.Empty;

                    // 大きいファイルではまずクイック領域パースを行い、即時フィードバックを出す
                    using (var rdr = new StringReader(textSnapshot))
                    {
                        while (rdr.ReadLine() != null) totalLines++;
                    }

                    // 閾値は今は 5000 行（変更可）
                    if (totalLines > 5000)
                    {
                        // クイック: キャレット周辺 ±3 行を先に解析
                        var quickErrors = await Task.Run(() => MapParser.QuickParseRegion(textSnapshot, caretLine, 3, baseDirectory));
                        await Dispatcher.InvokeAsync(() =>
                        {
                            // 部分的結果を即時反映
                            ErrorListDataGrid.ItemsSource = null;
                            ErrorListDataGrid.ItemsSource = quickErrors;
                        });

                        // さらに長めの遅延でフルパースをスケジュール（既存 token でキャンセル可能）
                        var localToken = token;
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await Task.Delay(2000, localToken).ConfigureAwait(false);
                            }
                            catch (TaskCanceledException) { return; }
                            if (localToken.IsCancellationRequested) return;

                            var fullErrors = await Task.Run(() => MapParser.Parse(textSnapshot, baseDirectory));
                            await Dispatcher.InvokeAsync(() =>
                            {
                                ErrorListDataGrid.ItemsSource = null;
                                ErrorListDataGrid.ItemsSource = fullErrors;
                            });
                        }, localToken);
                    }
                    else
                    {
                        // 小さいファイルは従来のフルパース
                        List<ParseError> errors = await Task.Run(() => MapParser.Parse(textSnapshot, baseDirectory));

                        await Dispatcher.InvokeAsync(() =>
                        {
                            ErrorListDataGrid.ItemsSource = null;
                            ErrorListDataGrid.ItemsSource = errors;
                        });
                    }
                }
                catch (Exception ex)
                {
                    var exceptionErrors = new List<ParseError>
                    {
                        new ParseError { Line = 1, Column = 1, Message = $"ファイルの読み込み中にエラーが発生しました: {ex.Message}" }
                    };
                    await Dispatcher.InvokeAsync(() => ErrorListDataGrid.ItemsSource = exceptionErrors);
                }
            }, token);
        }
        // デバウンスをスキップして即座にフルパースを実行するメソッド
        private async void RunSyntaxCheckImmediate()
        {
            // 既存の非同期チェックがあればキャンセルしておく
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

                // 15万行であっても、ファイルオープン時は最初からフルパースで正確なエラーを出す
                List<ParseError> errors = await Task.Run(() => MapParser.Parse(textSnapshot, baseDirectory), token);

                if (token.IsCancellationRequested) return;

                await Dispatcher.InvokeAsync(() =>
                {
                    ErrorListDataGrid.ItemsSource = null;
                    ErrorListDataGrid.ItemsSource = errors;
                });
            }
            catch (OperationCanceledException)
            {
                // キャンセル時は何もしない
            }
            catch (Exception ex)
            {
                var exceptionErrors = new List<ParseError>
        {
            new ParseError { Line = 1, Column = 1, Message = $"ファイルの読み込み中にエラーが発生しました: {ex.Message}" }
        };
                await Dispatcher.InvokeAsync(() => ErrorListDataGrid.ItemsSource = exceptionErrors);
            }
        }


    }
}
