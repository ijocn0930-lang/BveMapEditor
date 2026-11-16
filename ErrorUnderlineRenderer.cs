using ICSharpCode.AvalonEdit.Rendering;
using System.Windows;
using System.IO;
using System.Windows.Media;
using BveMapParserWorker;

namespace BveMapEditor
{
    internal class ErrorUnderlineRenderer : IBackgroundRenderer
    {
        private readonly ICSharpCode.AvalonEdit.TextEditor? _editor;
        private List<ParseError> _errors = new List<ParseError>();

        // ★ 現在は使っていない（絞り込みは MainWindow.xaml.cs の RunSyntaxCheck で行う）
        public string CurrentFileName { get; set; } = string.Empty;

        public ErrorUnderlineRenderer(ICSharpCode.AvalonEdit.TextEditor editor)
        {
            _editor = editor;
            // ★ 以前はここで「画面の配置が更新されるたび（LayoutUpdated）に描き直し（InvalidateVisual）を指示」していた。
            //    描き直すと配置がまた更新されるため、エラーがある間ずっと描き直しを繰り返し、
            //    何もしていなくてもエディタの CPU を 8% 前後使っていた。
            //    波線はエラーが変わったとき（SetErrors）に描き直しを指示しており、スクロール等ではエディタが自動で描き直すので、この処理は不要。削除した。
        }

        public void SetErrors(IEnumerable<ParseError> errors)
        {
            _errors = [.. errors];
            if (_editor?.Dispatcher != null)
            {
                _editor.Dispatcher.InvokeAsync(() =>
                {
                    var textView = _editor.TextArea?.TextView;
                    textView?.Redraw();
                }, System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        public KnownLayer Layer => KnownLayer.Selection;

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            if (_editor?.Document == null || _errors == null || _errors.Count == 0) return;

            // ★ 「このタブのエラーだけ」への絞り込みは、呼び出し側（MainWindow.xaml.cs の RunSyntaxCheck）で済ませている。
            //    ここでも絞り込むと、CurrentFileName が設定されていないため、インクルード先のタブの波線が消えてしまう。
            //    そのため、渡されたエラーをそのまま描画する。
            var filteredErrors = _errors;

            // ★ 行ごとに、エラー（赤）と警告（オレンジ）の範囲を求めて描く
            //    ・同じ色で重なる範囲は1本にまとめる（重ねて描くと線が濃くなるため）
            //    ・エラーの範囲には警告を描かない（エラーを優先）
            static bool IsError(ParseError e) => string.Equals(e.Level, "エラー", StringComparison.OrdinalIgnoreCase);
            var errorsByLine = filteredErrors
                .Where(e => e.Line >= 1 && e.Line <= _editor.Document.LineCount)
                .GroupBy(e => e.Line);

            foreach (var lineGroup in errorsByLine)
            {
                try
                {
                    var lineObj = _editor.Document.GetLineByNumber(lineGroup.Key);
                    var errorRanges = MergeRanges(lineGroup.Where(IsError).Select(e => GetRange(e, lineObj)));
                    var warningRanges = SubtractRanges(
                        MergeRanges(lineGroup.Where(e => !IsError(e)).Select(e => GetRange(e, lineObj))),
                        errorRanges);

                    foreach (var (start, end) in warningRanges) DrawRange(textView, drawingContext, Brushes.Orange, start, end);
                    foreach (var (start, end) in errorRanges) DrawRange(textView, drawingContext, Brushes.Red, start, end);
                }
                catch
                {
                    // 描画例外のガード
                }
            }
        }

        // ★ 1件のエラーが指す範囲（ドキュメント内の開始・終了位置）を求める
        //    Length が 0 のときは今まで通り行末まで。指定があればその長さだけ（行末を超えない）
        private static (int Start, int End) GetRange(ParseError err, ICSharpCode.AvalonEdit.Document.DocumentLine lineObj)
        {
            int startOffset = lineObj.Offset + Math.Max(0, Math.Min(err.Column - 1, lineObj.Length));
            // ★ エラーの列が行末より後ろ（例: 「閉じ括弧 ')' が不足」）のときは、行の最後の1文字に波線を引く
            //    （以前はここで範囲が0文字になり、波線が消えていた）
            if (startOffset >= lineObj.EndOffset && lineObj.Length > 0)
            {
                return (lineObj.EndOffset - 1, lineObj.EndOffset);
            }
            int endOffset = err.Length > 0
                ? Math.Min(startOffset + err.Length, lineObj.EndOffset)
                : lineObj.EndOffset;
            return (startOffset, endOffset);
        }

        // ★ 重なる・つながる範囲を1つにまとめる
        private static List<(int Start, int End)> MergeRanges(IEnumerable<(int Start, int End)> ranges)
        {
            var result = new List<(int Start, int End)>();
            foreach (var r in ranges.Where(r => r.End > r.Start).OrderBy(r => r.Start))
            {
                if (result.Count > 0 && r.Start <= result[^1].End)
                {
                    var last = result[^1];
                    result[^1] = (last.Start, Math.Max(last.End, r.End));
                }
                else
                {
                    result.Add(r);
                }
            }
            return result;
        }

        // ★ ranges から remove の部分を取り除く（警告の範囲からエラーの範囲を除くのに使う）
        private static List<(int Start, int End)> SubtractRanges(List<(int Start, int End)> ranges, List<(int Start, int End)> remove)
        {
            var result = new List<(int Start, int End)>();
            foreach (var r in ranges)
            {
                int cursor = r.Start;
                foreach (var cut in remove)
                {
                    if (cut.End <= cursor || cut.Start >= r.End) continue;
                    if (cut.Start > cursor) result.Add((cursor, cut.Start));
                    cursor = Math.Max(cursor, cut.End);
                }
                if (cursor < r.End) result.Add((cursor, r.End));
            }
            return result;
        }

        private static void DrawRange(TextView textView, DrawingContext drawingContext, Brush brush, int startOffset, int endOffset)
        {
            if (endOffset <= startOffset) return;
            var segment = new ICSharpCode.AvalonEdit.Document.TextSegment
            {
                StartOffset = startOffset,
                EndOffset = endOffset
            };
            foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
            {
                DrawWave(drawingContext, brush, rect.BottomLeft, rect.BottomRight);
            }
        }

        private static void DrawWave(DrawingContext dc, Brush brush, Point start, Point end)
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