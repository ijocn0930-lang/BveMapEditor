using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Media;

namespace BveMapEditor
{
    public class BveCompletionData : ICompletionData
    {
        // ★ description を string から object に変更し、UI要素も受け取れるようにする
        public BveCompletionData(string text, object? description = null)
        {
            Text = text;
            Description = description ?? string.Empty;
        }

        public ImageSource? Image => null;

        public string Text { get; }

        public object Content => Text;

        // object 型なので string も TextBlock などのUI要素も格納できる
        public object Description { get; }

        public double Priority => 0;

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            var document = textArea.Document;
            int caretOffset = textArea.Caret.Offset;
            int startOffset = caretOffset;

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

            int lengthToReplace = caretOffset - startOffset;
            if (lengthToReplace < 0) lengthToReplace = 0;

            document.Replace(startOffset, lengthToReplace, Text);
        }
    }

    /// <summary>
    /// ★ キーの自動補完の候補1つ分（Structure['ここ'] などに入るキー）
    ///    選ぶと、引用符の後ろから入力中の文字までを、キーで置き換える
    ///    （コマンド用の BveCompletionData は英数字・. ・_ しか置き換えないため、- などを含むキーでは使えない）
    /// </summary>
    public class BveKeyCompletionData : ICompletionData
    {
        // ★ closeBracket: true なら閉じる引用符と ] を入れる（Structure['キー'] など）
        //                 false なら閉じる引用符だけ入れる（Repeater の引数のキー）
        public BveKeyCompletionData(string key, object? description = null, bool closeBracket = true)
        {
            Text = key;
            Description = description ?? string.Empty;
            CloseBracket = closeBracket;
        }

        public bool CloseBracket { get; }

        // ★ キーと '] を入れ終わった後に呼ぶ処理（続けて関数の候補を出すために使う）
        public Action<TextArea>? AfterClose { get; set; }

        public ImageSource? Image => null;

        public string Text { get; }

        public object Content => Text;

        public object Description { get; }

        public double Priority => 0;

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            var document = textArea.Document;
            // ★ キーの入力・閉じる記号の追加を、元に戻す（Ctrl+Z）1回で戻せるようにまとめる
            using (document.RunUpdate())
            {
                // completionSegment は「引用符の直後」から「今のカーソル位置」まで
                int start = completionSegment.Offset;
                document.Replace(completionSegment, Text);
                int pos = start + Text.Length;

                // ★ 開いた引用符と同じ文字で閉じる（' か "）
                char quote = start > 0 ? document.GetCharAt(start - 1) : '\'';
                if (quote != '\'' && quote != '"') quote = '\'';

                // ★ カーソルの後ろを見て、足りない分だけ閉じる記号を入れる
                //    何もない → ' と ]、] だけある → '、すでに '] がある → 何も入れない
                //    後ろにキーの続きの文字が残っているとき（キーの途中を書き換えた）は何も入れない
                char next = pos < document.TextLength ? document.GetCharAt(pos) : '\0';
                bool hasQuote = next == quote;
                if (!hasQuote && next != '\0' && next != ']' && next != ')' && next != ',' && next != ';' && !char.IsWhiteSpace(next))
                {
                    textArea.Caret.Offset = pos;
                    return;
                }
                if (!hasQuote)
                {
                    document.Insert(pos, quote.ToString());
                }
                int afterQuote = pos + 1;
                int caret = afterQuote;
                if (CloseBracket)
                {
                    bool hasBracket = afterQuote < document.TextLength && document.GetCharAt(afterQuote) == ']';
                    if (!hasBracket) document.Insert(afterQuote, "]");
                    caret = afterQuote + 1;
                }
                textArea.Caret.Offset = caret;
            }
            // ★ '] まで入ったら、続けて関数の候補を出す（Repeater の引数のキーでは出さない）
            if (CloseBracket) AfterClose?.Invoke(textArea);
        }
    }

    /// <summary>
    /// ★ キーの後ろに続ける関数の候補1つ分（Structure['キー'] の後の .Put(); など）
    ///    選ぶと「.Put();」を入れて、カーソルを ( と ) の間に置く
    /// </summary>
    public class BveMethodCompletionData : ICompletionData
    {
        public BveMethodCompletionData(string text, object? description = null)
        {
            Text = text;
            Description = description ?? string.Empty;
        }

        public ImageSource? Image => null;

        public string Text { get; }

        public object Content => Text;

        public object Description { get; }

        public double Priority => 0;

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            int start = completionSegment.Offset;
            textArea.Document.Replace(completionSegment, Text);
            int paren = Text.IndexOf('(');
            textArea.Caret.Offset = paren >= 0 ? start + paren + 1 : start + Text.Length;
        }
    }
}
