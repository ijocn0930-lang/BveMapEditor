using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Rendering;
using System.Windows;
using System.Windows.Media;

namespace BveMapEditor
{
    // 現在行協調クラス
    internal class CurrentLineHighlightRenderer : IBackgroundRenderer
    {
        private readonly TextArea _textArea;
        // ハイライトしたい背景色（ここでは薄い黄色やグレー系など、お好みで調整してください）
        private static readonly Brush HighlightBrush = new SolidColorBrush(Color.FromArgb(30, 0, 120, 215)); // 薄い青系
        private static readonly Pen BorderPen = new (new SolidColorBrush(Color.FromArgb(40, 0, 120, 215)), 1);


        public CurrentLineHighlightRenderer(TextArea textArea)
        {
            _textArea = textArea;

            // キャレット位置が変更されたときに行ハイライトが追随して再描画されるようにする（コンストラクタで1回のみ購読）
            _textArea.Caret.PositionChanged += (s, e) =>
            {
                _textArea.TextView?.InvalidateVisual();
            };
        }
        public KnownLayer Layer => KnownLayer.Background;

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            if (_textArea.Document == null) return;

            // 現在のキャレット（カーソル）がある行を取得
            int lineNumber = _textArea.Caret.Line;
            if (lineNumber < 1 || lineNumber > _textArea.Document.LineCount) return;

            // ★ (2026-11-16) 行の上端と高さを、画面に並んでいる行（VisualLine）から取る
            //    （以前は行の文字の範囲から四角を作っていたため、文字のない空の行では四角ができず、強調が出ないことがあった）
            //    画面に見えていない行なら何も描かない。折り返し表示で1行が何段かになっても、全部の段を強調する
            foreach (var visualLine in textView.VisualLines)
            {
                if (visualLine.FirstDocumentLine.LineNumber <= lineNumber && lineNumber <= visualLine.LastDocumentLine.LineNumber)
                {
                    double top = visualLine.VisualTop - textView.VerticalOffset;
                    var fullWidthRect = new Rect(0, top, textView.ActualWidth, visualLine.Height);
                    drawingContext.DrawRectangle(HighlightBrush, BorderPen, fullWidthRect);
                    break;
                }
            }
        }
    }
}
