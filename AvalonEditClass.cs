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

            // ★ 括弧の自動補完で入った ] の直前で ] を打ったら、二重にせず ] の後ろへ進む（Structure['abc'|] → Structure['abc']|）
            //    そのあと関数の候補を出す（Repeater なら .Begin(); / .Begin0(); / .End(); など）
            if (TryStepOverClosingBracket(e)) return;

            if (e.Text.Length > 0 && completionWindow != null)
            {
                // ★ キーの候補を出しているとき: 閉じる引用符・括弧・空白で候補を閉じる（選択中の候補は入れない）
                //    それ以外の文字（- など）はそのまま入力して、候補の絞り込みに使う
                if (_isKeyCompletion)
                {
                    if (e.Text == "'" || e.Text == "\"" || e.Text == "]" || e.Text == ")" || e.Text == " " || e.Text == ";")
                    {
                        completionWindow.Close();
                    }
                    return;
                }

                if (e.Text == " ")
                {
                    completionWindow.Close();
                    completionWindow = null;
                    return;
                }

                // ★ 記号（[ ( ' ; など）を打ったら、候補を確定せずに閉じる
                //    （以前は選択中の候補を確定していたため、Structure[ と打つと「Structure.Load();」などが勝手に入っていた）
                //    候補を確定するのは Enter / Tab（候補ウィンドウ自身が処理する）
                if (!char.IsLetterOrDigit(e.Text[0]) && e.Text != ".")
                {
                    completionWindow.Close();
                    completionWindow = null;
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
            // ★ Repeater['abc'] のように ] を打ってキーを閉じたら、関数の候補を出す
            if (e.Text == "]" && editor != null && TryShowMethodAfterBracket(editor)) return;

            // ★ Structure['…'] などの引用符の中では、コマンドではなくキーの候補を出す
            //    「キーの自動補完」がオフなら候補は出さない（引用符の中ではコマンドの候補も出さない）
            //    （キーの自動補完は「自動補完」のオン・オフとは別に決める）
            if (TryShowKeyCompletion(editor, currentSettings.EnableKeyCompletion)) return;

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
        #region キーの自動補完
        // ★ 今出している候補がキーの候補かどうか（コマンドの候補と動きを分けるため）
        private bool _isKeyCompletion;

        // ★ カーソルの手前が「Structure['」「Sound["abc」のように、キーの引用符の中かを調べる
        //    1: 種類（Structure など） / 3: 引用符の後ろに入力済みの文字
        private static readonly System.Text.RegularExpressions.Regex KeyContextRegex = new(
            @"\b(Structure|Sound3D|Sound|Station|Signal|Train)\s*\[\s*(['""])([^'""\[\]]*)$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

        /// <summary>
        /// キーの引用符の中なら、キーの候補を出して true を返す（コマンドの候補は出さない）。
        /// キーの一覧がまだない（構文チェックが終わっていない）ときも、引用符の中なら true（何も出さない）。
        /// </summary>
        private bool TryShowKeyCompletion(ICSharpCode.AvalonEdit.TextEditor? editor, bool showKeys, bool allowRepeater = true)
        {
            if (editor?.Document == null) return false;

            // すでにキーの候補が出ていれば、絞り込みは候補ウィンドウ自身が行う
            if (completionWindow != null && _isKeyCompletion) return true;

            int caret = editor.CaretOffset;
            var line = editor.Document.GetLineByOffset(caret);
            string before = editor.Document.GetText(line.Offset, caret - line.Offset);
            string kind, typed;
            bool closeBracket; // ★ キーを選んだ後に ] も入れるか（Repeater の引数では閉じる引用符だけ）
            var m = KeyContextRegex.Match(before);
            if (m.Success)
            {
                kind = m.Groups[1].Value;
                typed = m.Groups[3].Value;
                closeBracket = true;
            }
            else if (allowRepeater && TryGetRepeaterKeyContext(editor, caret, out typed))
            {
                // ★ Repeater の Begin / Begin0 のストラクチャーキーの位置
                kind = "Structure";
                closeBracket = false;
            }
            else
            {
                return false;
            }

            // コマンドの候補が出ていれば閉じる
            completionWindow?.Close();
            completionWindow = null;

            if (!showKeys) return true; // ★ キーの自動補完がオフ

            var keys = GetKeyList(editor, kind);
            if (keys == null || keys.Count == 0) return true;

            var win = new CompletionWindow(editor.TextArea)
            {
                FontSize = editor.FontSize,
                FontFamily = editor.FontFamily,
                Width = Math.Max(250, editor.FontSize * 18),
                MaxHeight = 300,
                StartOffset = caret - typed.Length // 引用符の直後から（入力済みの文字も置き換える）
            };
            var allItems = new List<ICompletionData>(keys.Count);
            string description = $"{kind} のキー（リストファイルから）";
            foreach (var key in keys)
            {
                allItems.Add(new BveKeyCompletionData(key, description, closeBracket)
                {
                    // ★ '] まで入ったら、候補ウィンドウが閉じた後に関数の候補を出す
                    AfterClose = ta => Dispatcher.BeginInvoke(new Action(() => ShowMethodCompletion(editor, kind)),
                        System.Windows.Threading.DispatcherPriority.Input)
                });
            }
            _allKeyItems.AddOrUpdate(win, allItems);
            // ★ 入力した文字で「始まる」キーだけに絞り込む（候補ウィンドウ標準の絞り込みは「含む」ものも出すため使わない）
            win.CompletionList.IsFiltering = false;
            if (!FilterKeyCompletion(win, typed)) return true; // 一致するキーがなければ出さない

            // カーソルが動くたび（文字の入力・Backspace）に絞り込み直す
            EventHandler refilter = (o, args) =>
            {
                int pos = editor.CaretOffset;
                if (pos < win.StartOffset) return; // 引用符より前に戻った（候補ウィンドウ自身が閉じる）
                string now = editor.Document.GetText(win.StartOffset, pos - win.StartOffset);
                if (!FilterKeyCompletion(win, now)) win.Close();
            };
            editor.TextArea.Caret.PositionChanged += refilter;

            _isKeyCompletion = true;
            completionWindow = win;
            win.Closed += (o, args) =>
            {
                editor.TextArea.Caret.PositionChanged -= refilter;
                if (completionWindow == win) completionWindow = null;
                _isKeyCompletion = false;
            };
            win.Show();
            return true;
        }

        /// <summary>
        /// ★ キーの候補を、入力した文字で始まるもの（大文字小文字は区別しない）だけにする。1つもなければ false
        ///    全部のキーは、最初に作った候補（_allKeyItems）から取り出す
        /// </summary>
        private bool FilterKeyCompletion(CompletionWindow win, string typed)
        {
            if (!_allKeyItems.TryGetValue(win, out var all)) return false;
            var matched = string.IsNullOrEmpty(typed)
                ? all
                : all.Where(item => item.Text.StartsWith(typed, StringComparison.OrdinalIgnoreCase)).ToList();
            if (matched.Count == 0) return false;
            var data = win.CompletionList.CompletionData;
            data.Clear();
            foreach (var item in matched) data.Add(item);
            win.CompletionList.SelectedItem = matched[0];
            return true;
        }

        // ★ 候補ウィンドウごとの「全部のキーの候補」（絞り込みに使う）
        private readonly System.Runtime.CompilerServices.ConditionalWeakTable<CompletionWindow, List<ICompletionData>> _allKeyItems = new();

        /// <summary>
        /// ★ 括弧の自動補完で入った ] の直前で ] を打ったとき、二重にせずカーソルを ] の後ろへ進め、関数の候補を出す
        /// </summary>
        private bool TryStepOverClosingBracket(System.Windows.Input.TextCompositionEventArgs e)
        {
            if (e.Text != "]" || _isOverwriteMode || !currentSettings.AutoCompleteBrackets) return false;
            var editor = ActiveEditor;
            if (editor?.Document == null || editor.SelectionLength > 0) return false;
            int caret = editor.CaretOffset;
            if (caret >= editor.Document.TextLength || editor.Document.GetCharAt(caret) != ']') return false;

            if (completionWindow != null) completionWindow.Close();
            completionWindow = null;
            editor.CaretOffset = caret + 1;
            e.Handled = true;
            TryShowMethodAfterBracket(editor);
            return true;
        }

        // ★ カーソルの手前が「Repeater['abc']」のようにキーを閉じた形かを調べる（1: 種類）
        private static readonly System.Text.RegularExpressions.Regex ClosedKeyRegex = new(
            @"\b(Structure|Sound3D|Sound|Station|Signal|Train|Repeater|Track)\s*\[\s*(['""])[^'""\r\n]*\2\s*\]$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

        /// <summary>
        /// ★ カーソルの手前が Repeater['abc'] などなら、関数の候補を出して true を返す（「キーの自動補完」がオフなら出さない）
        /// </summary>
        private bool TryShowMethodAfterBracket(ICSharpCode.AvalonEdit.TextEditor editor)
        {
            if (!currentSettings.EnableKeyCompletion || editor.Document == null) return false;
            int caret = editor.CaretOffset;
            var line = editor.Document.GetLineByOffset(caret);
            string before = editor.Document.GetText(line.Offset, caret - line.Offset);
            var m = ClosedKeyRegex.Match(before);
            if (!m.Success) return false;
            completionWindow?.Close();
            completionWindow = null;
            ShowMethodCompletion(editor, m.Groups[1].Value);
            return true;
        }

        /// <summary>
        /// ★ カーソルが動いたとき: Structure[''] のようにキーが空の引用符の間にカーソルが来たら、キーの候補を出す。
        ///    Repeater は空のキーでもエラーにならないので対象外。
        ///    エディタに入力の焦点があり、選択していなくて、ほかの候補が出ていないときだけ。
        /// </summary>
        private void OnCaretMovedForKeyCompletion(ICSharpCode.AvalonEdit.TextEditor? editor)
        {
            if (editor?.Document == null || isLoading || isInitializing) return;
            if (!currentSettings.EnableKeyCompletion || completionWindow != null) return;
            if (!editor.TextArea.IsKeyboardFocusWithin || editor.SelectionLength > 0) return;

            int caret = editor.CaretOffset;
            var doc = editor.Document;
            if (caret <= 0 || caret >= doc.TextLength) return;
            char prev = doc.GetCharAt(caret - 1), next = doc.GetCharAt(caret);
            if (!((prev == '\'' && next == '\'') || (prev == '"' && next == '"'))) return;

            // 入力の途中で動いた場合と重ならないよう、入力の処理が終わってから出す
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (editor.CaretOffset != caret || completionWindow != null) return;
                TryShowKeyCompletion(editor, true, allowRepeater: false);
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        /// <summary>
        /// ★ Structure['キー'] の後ろに続ける関数（.Put(); など）の候補を出す。
        ///    候補は自動補完の辞書（BveSyntaxProvider の「Structure[''].Put();」など）から取り出す。
        ///    後ろにすでに .Put(…) などが書いてあるとき（キーだけ直したとき）は出さない。
        /// </summary>
        private void ShowMethodCompletion(ICSharpCode.AvalonEdit.TextEditor editor, string kind)
        {
            if (editor?.Document == null || completionWindow != null) return;
            if (!editor.TextArea.IsKeyboardFocusWithin) return;
            int caret = editor.CaretOffset;
            if (caret < editor.Document.TextLength && editor.Document.GetCharAt(caret) == '.') return;

            string prefix = kind + "['']";
            var methods = cachedSyntaxes
                .Where(item => item.Text.StartsWith(prefix + ".", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (methods.Count == 0) return;

            var win = new CompletionWindow(editor.TextArea)
            {
                FontSize = editor.FontSize,
                FontFamily = editor.FontFamily,
                Width = Math.Max(250, editor.FontSize * 18),
                MaxHeight = 300,
                StartOffset = caret
            };
            foreach (var item in methods)
            {
                // 「Structure[''].Put();」→「.Put();」
                win.CompletionList.CompletionData.Add(new BveMethodCompletionData(item.Text.Substring(prefix.Length), item.Description));
            }
            win.CompletionList.SelectedItem = win.CompletionList.CompletionData[0];

            // キーの候補と同じく、絞り込みは候補ウィンドウ自身が行う（コマンドの候補の作り直しをさせない）
            _isKeyCompletion = true;
            completionWindow = win;
            win.Closed += (o, args) =>
            {
                if (completionWindow == win) completionWindow = null;
                _isKeyCompletion = false;
            };
            win.Show();
        }

        // ★ Repeater['…'].Begin( / Begin0( を探す（2: Begin か Begin0）
        private static readonly System.Text.RegularExpressions.Regex RepeaterBeginRegex = new(
            @"\bRepeater\s*\[\s*(['""]).*?\1\s*\]\s*\.\s*(Begin0|Begin)\s*\(",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

        /// <summary>
        /// ★ カーソルが Repeater の Begin / Begin0 の「ストラクチャーキーを書く位置」の引用符の中かを調べる。
        ///    Begin は丸括弧の中の11番目以降、Begin0 は5番目以降（ワーカーのチェックと同じ位置）。
        ///    複数行にまたがってもよいように、手前の ; より後ろの文字を見る。
        ///    typed: 引用符の後ろに入力済みの文字
        /// </summary>
        private static bool TryGetRepeaterKeyContext(ICSharpCode.AvalonEdit.TextEditor editor, int caret, out string typed)
        {
            typed = string.Empty;
            const int MaxLookBack = 4000; // 長すぎる文は見ない（大きいファイルでも軽くするため）
            int from = Math.Max(0, caret - MaxLookBack);
            string text = editor.Document.GetText(from, caret - from);
            int semicolon = text.LastIndexOf(';');
            if (semicolon >= 0) text = text.Substring(semicolon + 1);

            System.Text.RegularExpressions.Match? last = null;
            foreach (System.Text.RegularExpressions.Match mm in RepeaterBeginRegex.Matches(text)) last = mm;
            if (last == null) return false;
            bool isBegin0 = last.Groups[2].Value.Equals("Begin0", StringComparison.OrdinalIgnoreCase);

            // 丸括弧の中を前から読み、引数の区切りのカンマを数える（引用符の中・内側の括弧の中のカンマは数えない）
            int commas = 0, depth = 0, quoteStart = -1;
            char quote = '\0';
            string args = text.Substring(last.Index + last.Length);
            for (int i = 0; i < args.Length; i++)
            {
                char c = args[i];
                if (quote != '\0')
                {
                    if (c == quote) quote = '\0';
                    continue;
                }
                if (c == '\'' || c == '"') { quote = c; quoteStart = i; }
                else if (c == '(') depth++;
                else if (c == ')')
                {
                    if (depth == 0) return false; // Begin の丸括弧が閉じている
                    depth--;
                }
                else if (c == ',' && depth == 0) commas++;
            }
            if (quote == '\0') return false; // 引用符の中ではない

            int firstKeyIndex = isBegin0 ? 4 : 10; // 0から数えた引数の番号
            if (commas < firstKeyIndex) return false;
            typed = args.Substring(quoteStart + 1);
            return true;
        }

        /// <summary>
        /// エディタのファイルからルートファイルをたどり、構文チェックで受け取ったキーの一覧を返す（なければ null）
        /// </summary>
        // ===== ★ (2026-11-16) 変数にマウスを乗せたら宣言を吹き出しで出す =====
        // 宣言の一覧は、構文チェックのたびにワーカーがキーの一覧と一緒に返す（キーの種類は BveSemanticAnalyzer.VariableListKey = "$Variables"）
        // 1件は「名前<TAB>ファイル（ルートは空）<TAB>行<TAB>右辺」
        private System.Windows.Controls.ToolTip? _variableToolTip;

        private static bool IsVariableNameChar(char c) => char.IsAsciiLetterOrDigit(c) || c == '_';

        private void Editor_MouseHover(object sender, System.Windows.Input.MouseEventArgs e)
        {
            try
            {
                if (sender is not ICSharpCode.AvalonEdit.TextEditor editor || editor.Document == null) return;
                if (!currentSettings.EnableVariableHover) return; // ★ (2026-11-16) 「設定」でオフにしていれば出さない
                var pos = editor.GetPositionFromPoint(e.GetPosition(editor));
                if (pos == null) return;
                var doc = editor.Document;
                int offset = doc.GetOffset(pos.Value.Location);
                if (offset < 0 || offset > doc.TextLength) return;

                // マウスの下の「$名前」を探す（マウスが $ の上でも名前の上でもよい）
                int start = offset, end = offset;
                if (start < doc.TextLength && doc.GetCharAt(start) == '$') { start++; end++; }
                while (start > 0 && IsVariableNameChar(doc.GetCharAt(start - 1))) start--;
                while (end < doc.TextLength && IsVariableNameChar(doc.GetCharAt(end))) end++;
                if (start == 0 || doc.GetCharAt(start - 1) != '$' || end <= start) return;
                string name = "$" + doc.GetText(start, end - start);

                // 宣言の一覧から探す
                var list = GetKeyList(editor, BveMapParserWorker.BveSemanticAnalyzer.VariableListKey);
                string rootPath = GetRootFilePath(FindTabByEditor(editor)?.FilePath);
                string rootName = string.IsNullOrEmpty(rootPath) ? "無題" : Path.GetFileName(rootPath);
                var lines = new List<string>();
                if (list == null)
                {
                    lines.Add($"{name}: 構文チェックが終わると、宣言を表示できます。");
                }
                else
                {
                    foreach (var row in list)
                    {
                        var parts = row.Split('\t');
                        if (parts.Length < 4 || !string.Equals(parts[0], name, StringComparison.OrdinalIgnoreCase)) continue;
                        string file = string.IsNullOrEmpty(parts[1]) ? rootName : Path.GetFileName(parts[1]);
                        lines.Add($"{parts[0]} = {parts[3]}　（{file} の {parts[2]} 行目）");
                    }
                    if (lines.Count == 0) lines.Add($"{name}: 宣言が見つかりません（{name} = 値; と書くと宣言できます）。");
                    else if (lines.Count > 1) lines.Add("※ 何回か宣言されています。BVE では後の宣言の値が使われます。");
                }

                _variableToolTip ??= new System.Windows.Controls.ToolTip { Placement = System.Windows.Controls.Primitives.PlacementMode.Mouse };
                // ★ (2026-11-16) 文字の大きさもエディタと同じにする（Ctrl＋ホイールで変えた大きさに合わせる）
                _variableToolTip.Content = new System.Windows.Controls.TextBlock { Text = string.Join("\n", lines), FontFamily = editor.FontFamily, FontSize = editor.FontSize };
                _variableToolTip.PlacementTarget = editor;
                _variableToolTip.IsOpen = true;
                e.Handled = true;
            }
            catch
            {
                // 吹き出しが出せなくても編集には影響しない
            }
        }

        private void Editor_MouseHoverStopped(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_variableToolTip != null) _variableToolTip.IsOpen = false;
        }

        private List<string>? GetKeyList(ICSharpCode.AvalonEdit.TextEditor editor, string kind)
        {
            string path = GetRootFilePath(FindTabByEditor(editor)?.FilePath);
            if (_keyListsByRoute.TryGetValue(path, out var lists) && lists.TryGetValue(kind, out var keys)) return keys;
            return null;
        }
        #endregion

        // テキスト変更時のイベント
        private void MapTextEditor_TextChanged(object? sender, EventArgs e)
        {
            if (isLoading || isInitializing) return;

            var editor = sender as ICSharpCode.AvalonEdit.TextEditor ?? GetActiveEditor();
            var tabInfo = FindTabByEditor(editor);
            if (tabInfo != null)
            {
                // ★ 「変更あり」かどうかを、保存した時点（または開いた時点）の本文と比べて決める
                //    元に戻す（Ctrl+Z）などで同じ本文に戻ったら「変更なし」に戻す
                //    （以前は一度でも書き換えたら「変更あり」のままで、元に戻しても閉じるときに警告が出ていた）
                //    ・文字数が違えばすぐ「変更あり」。同じときだけ本文全体を比べる（大きいファイルでも軽くするため）
                //    ・エディタの「元に戻す」の記録は、続けて入力した文字をまとめて後で確定するため、入力中の判定には使えなかった
                bool modifiedNow = true;
                var doc = editor?.Document;
                string? original = tabInfo.OriginalText;
                if (doc != null && original != null && doc.TextLength == original.Length)
                {
                    modifiedNow = !string.Equals(doc.Text, original, StringComparison.Ordinal);
                }
                if (tabInfo.IsModified != modifiedNow)
                {
                    tabInfo.IsModified = modifiedNow;
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
    }
}