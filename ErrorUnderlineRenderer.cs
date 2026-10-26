using ICSharpCode.AvalonEdit.Rendering;
using System.Windows;
using System.IO;
using System.Windows.Media;

namespace BveMapEditor
{
    internal class ErrorUnderlineRenderer : IBackgroundRenderer
    {
        private readonly ICSharpCode.AvalonEdit.TextEditor? _editor;
        private List<ParseError> _errors = new List<ParseError>();

        // ★ 追加: 現在エディターで開いているファイルのファイル名（またはパス）を設定・保持するプロパティ
        public string CurrentFileName { get; set; } = string.Empty;

        public ErrorUnderlineRenderer(ICSharpCode.AvalonEdit.TextEditor editor)
        {
            _editor = editor;
            if (_editor?.TextArea?.TextView != null)
            {
                _editor.TextArea.TextView.LayoutUpdated += (s, e) =>
                {
                    if (_errors.Count > 0)
                    {
                        _editor.TextArea.TextView.InvalidateVisual();
                    }
                };
            }
        }

        public void SetErrors(IEnumerable<ParseError> errors)
        {
            _errors = [.. errors];
            if (_editor?.Dispatcher != null)
            {
                _editor.Dispatcher.InvokeAsync(() =>
                {
                    var textView = _editor.TextArea?.TextView;
                    if (textView != null)
                    {
                        textView.Redraw();
                    }
                }, System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        public KnownLayer Layer => KnownLayer.Selection;

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            if (_editor?.Document == null || _errors == null || _errors.Count == 0) return;

            // ★ 追加: 現在エディターで開いているファイルのエラーのみに絞り込む
            var filteredErrors = _errors.Where(e =>
            {
                if (string.IsNullOrEmpty(e.FileName))
                {
                    // メインファイルのエラー（FileName が空）は、現在開いているのもメインファイル（CurrentFileName が空または一致）の場合のみ描画
                    return string.IsNullOrEmpty(CurrentFileName);
                }
                // インクルードファイル等のエラーは、FileName と CurrentFileName が一致する場合のみ描画
                return string.Equals(e.FileName, CurrentFileName, Path.GetFileName(CurrentFileName) != CurrentFileName ? StringComparison.OrdinalIgnoreCase : StringComparison.OrdinalIgnoreCase);
            });

            // 行ごとにエラー／警告をグループ化する
            var errorsByLine = filteredErrors
                .Where(e => e.Line >= 1 && e.Line <= _editor.Document.LineCount)
                .GroupBy(e => e.Line);

            foreach (var lineGroup in errorsByLine)
            {
                int line = lineGroup.Key;
                try
                {
                    var lineObj = _editor.Document.GetLineByNumber(line);
                    bool hasError = lineGroup.Any(e => string.Equals(e.Level, "エラー", StringComparison.OrdinalIgnoreCase));
                    Brush waveBrush = hasError ? Brushes.Red : Brushes.Orange;

                    int minColumn = lineGroup.Min(e => e.Column);
                    int startOffset = lineObj.Offset + Math.Max(0, Math.Min(minColumn - 1, lineObj.Length));
                    int endOffset = lineObj.EndOffset;

                    var segment = new ICSharpCode.AvalonEdit.Document.TextSegment
                    {
                        StartOffset = startOffset,
                        EndOffset = endOffset
                    };

                    foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
                    {
                        DrawWave(drawingContext, waveBrush, rect.BottomLeft, rect.BottomRight);
                    }
                }
                catch
                {
                    // 描画例外のガード
                }
            }
        }

        private void DrawWave(DrawingContext dc, Brush brush, Point start, Point end)
        {
            double waveHeight = 3.0;
            double waveWidth = 7.0;
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.BeginFigure(start, false, false);
                for (double x = start.X; x < end.X; x += waveWidth)
                {
                    double nextX = Math.Min(x + waveWidth / 2, end.X);
                    double nextX2 = Math.Min(x + waveWidth, end.X);
                    context.QuadraticBezierTo(new Point((x + nextX) / 2, start.Y + waveHeight), new Point(nextX, start.Y), true, false);
                    if (nextX2 >= end.X) break;
                    context.QuadraticBezierTo(new Point((nextX + nextX2) / 2, start.Y - waveHeight), new Point(nextX2, start.Y), true, false);
                }
            }
            geometry.Freeze();
            dc.DrawGeometry(null, new Pen(brush, 1.0), geometry);
        }
    }
}