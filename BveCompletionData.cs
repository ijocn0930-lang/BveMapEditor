using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Media;

namespace BveMapEditor
{
    internal class BveCompletionData : ICompletionData
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
}
