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
            var currentLine = _textArea.Document.GetLineByOffset(_textArea.Caret.Offset);

            // BackgroundGeometryBuilder を使って行の描画ジオメトリを安全に取得
            BackgroundGeometryBuilder geoBuilder = new()
            {
                AlignToWholePixels = true
            };
            geoBuilder.AddSegment(textView, new ICSharpCode.AvalonEdit.Document.TextSegment { StartOffset = currentLine.Offset, Length = currentLine.Length });

            var geometry = geoBuilder.CreateGeometry();
            if (geometry != null)
            {
                // ジオメトリの境界を使って行の矩形を取得し、エディタ幅いっぱいにハイライトを描画する
                var bounds = geometry.Bounds;
                if (!bounds.IsEmpty)
                {
                    var fullWidthRect = new Rect(0, bounds.Top, textView.ActualWidth, bounds.Height);
                    drawingContext.DrawRectangle(HighlightBrush, BorderPen, fullWidthRect);
                }
            }
        }
    }
}
