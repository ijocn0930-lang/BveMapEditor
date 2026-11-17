using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Rendering;

namespace BveMapEditor
{
    /// <summary>
    /// ★ (2026-11-16) CSV（リストファイル）のタブで、列の始まりの位置を「表示だけ」でそろえる
    /// ・ファイルの中身は変えない（保存・コピーしても空白は入らない）
    /// ・カンマ 1 文字を「カンマ ＋ 見た目だけの余白」に置き換えて表示する（AvalonEdit の VisualLineElementGenerator）
    /// ・列の幅は、その列でいちばん広い所に合わせる。ヘッダー（BveTs …）・空の行・コメント（# / //）の行は幅の計算に入れない
    /// ・とても長い列で右に広がりすぎないよう、1 列の幅に上限（MaxColumnEm）を付ける
    /// ・書き換えたら少し待ってから幅を計算し直す
    /// ・どのタブで効かせるか（CSV のタブか・設定がオンか）は、作るときに渡す isActive で決める
    /// </summary>
    internal sealed class CsvColumnAligner : VisualLineElementGenerator
    {
        // 1 列の幅の上限（文字の大きさの何倍か。半角の文字で約 40 文字分）
        private const double MaxColumnEm = 22.0;
        // 幅をはかるときの文字の大きさ（ここで測って、表示のときに今の文字の大きさに合わせて縮める）
        private const double MeasureEmSize = 100.0;
        // 計算する行数の上限（とても大きいファイルで重くならないように）
        private const int MaxLines = 50000;

        private readonly TextEditor _editor;
        private readonly Func<TextEditor, bool> _isActive;
        private readonly DispatcherTimer _timer;
        // 列ごとの幅（MeasureEmSize で測った値）。空なら何もしない
        private double[] _columnWidths = Array.Empty<double>();

        public CsvColumnAligner(TextEditor editor, Func<TextEditor, bool> isActive)
        {
            _editor = editor;
            _isActive = isActive;
            _timer = new DispatcherTimer(DispatcherPriority.Background, editor.Dispatcher) { Interval = TimeSpan.FromMilliseconds(400) };
            _timer.Tick += (s, e) => { _timer.Stop(); Recalculate(); };
            // 書き換えたら（ファイルを開いたときも）少し待ってから計算し直す
            _editor.TextChanged += (s, e) => ScheduleRecalculate();
        }

        /// <summary>少し待ってから幅を計算し直す</summary>
        public void ScheduleRecalculate()
        {
            _timer.Stop();
            _timer.Start();
        }

        /// <summary>今すぐ幅を計算し直して、表示を描き直す（設定のオン・オフ、フォントの変更のとき）</summary>
        public void Recalculate()
        {
            try
            {
                _columnWidths = _isActive(_editor) ? CalculateColumnWidths() : Array.Empty<double>();
            }
            catch
            {
                _columnWidths = Array.Empty<double>(); // 計算できなくても編集には影響しないようにする
            }
            _editor.TextArea?.TextView?.Redraw();
        }

        // 行が幅の計算に入るか（ヘッダー・空の行・コメントの行は入れない）
        private static bool IsDataLine(string line, int lineNumber)
        {
            string t = line.TrimStart();
            if (t.Length == 0) return false;
            if (t.StartsWith('#') || t.StartsWith("//", StringComparison.Ordinal)) return false;
            if (lineNumber == 1 && t.StartsWith("BveTs", StringComparison.OrdinalIgnoreCase)) return false;
            return true;
        }

        private double[] CalculateColumnWidths()
        {
            var doc = _editor.Document;
            if (doc == null || doc.TextLength == 0) return Array.Empty<double>();
            var widths = new List<double>();
            int count = Math.Min(doc.LineCount, MaxLines);
            for (int n = 1; n <= count; n++)
            {
                var docLine = doc.GetLineByNumber(n);
                string text = doc.GetText(docLine);
                if (!IsDataLine(text, n)) continue;
                var fields = text.Split(',');
                // 最後の列の後ろにはカンマがないので、最後の列は数えない
                for (int i = 0; i < fields.Length - 1; i++)
                {
                    double w = Measure(fields[i]);
                    if (i >= widths.Count) widths.Add(w);
                    else if (w > widths[i]) widths[i] = w;
                }
            }
            double cap = MaxColumnEm * MeasureEmSize;
            return widths.Select(w => Math.Min(w, cap)).ToArray();
        }

        // 文字の幅を MeasureEmSize の大きさで測る（全角の文字も実際の幅で測る）
        private double Measure(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            var typeface = new Typeface(_editor.FontFamily, _editor.FontStyle, _editor.FontWeight, _editor.FontStretch);
            double pixelsPerDip = VisualTreeHelper.GetDpi(_editor).PixelsPerDip;
            var ft = new FormattedText(s.Replace('\t', ' '), CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, MeasureEmSize, Brushes.Black, pixelsPerDip);
            return ft.WidthIncludingTrailingWhitespace;
        }

        // ===== AvalonEdit の表示の部品 =====

        // 次に置き換えたいカンマの位置（なければ -1）
        public override int GetFirstInterestedOffset(int startOffset)
        {
            if (_columnWidths.Length == 0) return -1;
            var doc = CurrentContext.Document;
            var docLine = doc.GetLineByOffset(startOffset);
            if (docLine.LineNumber > MaxLines) return -1;
            string lineText = doc.GetText(docLine);
            if (!IsDataLine(lineText, docLine.LineNumber)) return -1;
            int endOffset = docLine.EndOffset;
            for (int i = startOffset; i < endOffset; i++)
            {
                if (doc.GetCharAt(i) == ',') return i;
            }
            return -1;
        }

        // カンマ 1 文字を「カンマ ＋ 余白」の表示に置き換える
        public override VisualLineElement? ConstructElement(int offset)
        {
            try
            {
                var doc = CurrentContext.Document;
                var docLine = doc.GetLineByOffset(offset);
                // このカンマが何列目の後ろか、その列の文字は何か
                int column = 0;
                int fieldStart = docLine.Offset;
                for (int i = docLine.Offset; i < offset; i++)
                {
                    if (doc.GetCharAt(i) == ',') { column++; fieldStart = i + 1; }
                }
                if (column >= _columnWidths.Length) return null;

                double scale = _editor.FontSize / MeasureEmSize;
                double fieldWidth = Measure(doc.GetText(fieldStart, offset - fieldStart));
                double pad = Math.Max(0, _columnWidths[column] - fieldWidth) * scale;

                var comma = new TextBlock
                {
                    Text = ",",
                    FontFamily = _editor.FontFamily,
                    FontSize = _editor.FontSize,
                    Foreground = _editor.Foreground,
                    Padding = new Thickness(0, 0, pad, 0),
                };
                return new InlineObjectElement(1, comma);
            }
            catch
            {
                return null; // うまく作れなければ、ふつうのカンマのまま表示する
            }
        }
    }
}
