using ICSharpCode.AvalonEdit;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace BveMapEditor
{
    // ★ (2026-11-17) 変換キーで再変換する
    //    日本語入力（IME）がオンで何も入力していないときに変換キーを押すと、IME がウィンドウに
    //    「カーソルの周りの文字を教えて」と問い合わせる（WM_IME_REQUEST）。AvalonEdit はこれに答えないので、ここで答える
    //    MainWindow.xaml.cs に統合されるファイル
    public partial class MainWindow : Window
    {
        private const int WM_IME_REQUEST = 0x0288;
        private const int IMR_RECONVERTSTRING = 0x0004;        // 再変換する文字を教えて
        private const int IMR_CONFIRMRECONVERTSTRING = 0x0005; // この範囲を再変換してよいか
        private const int ReconvertHeaderSize = 32;            // RECONVERTSTRING の決まった部分（4 バイト × 8 個）
        private const int ReconvertContextChars = 500;         // カーソル（選択）の前後に渡す文字数の上限（長い行のため）

        // 最後に IME に渡した文字（確定の問い合わせのとき、どこを選ぶか求めるため）
        private TextEditor? _reconvertEditor;
        private int _reconvertBaseOffset;
        private string _reconvertText = string.Empty;

        private const int WM_IME_ENDCOMPOSITION = 0x010E;
        private const int WM_IME_COMPOSITION = 0x010F;
        private const int GCS_RESULTSTR = 0x0800;      // 確定した文字がある
        private const int NI_COMPOSITIONSTR = 0x0015;
        private const int CPS_CANCEL = 0x0004;         // 入力中の文字を取り消す

        [DllImport("imm32.dll")]
        private static extern IntPtr ImmGetContext(IntPtr hWnd);
        [DllImport("imm32.dll")]
        private static extern bool ImmReleaseContext(IntPtr hWnd, IntPtr hIMC);
        [DllImport("imm32.dll")]
        private static extern bool ImmNotifyIME(IntPtr hIMC, int dwAction, int dwIndex, int dwValue);
        [DllImport("imm32.dll", CharSet = CharSet.Unicode)]
        private static extern int ImmGetCompositionStringW(IntPtr hIMC, int dwIndex, byte[]? lpBuf, int dwBufLen);

        // ★ 再変換中（IME が決めた範囲を選んでから、確定・取り消しまで）
        //    確定した文字は、ここで覚えた範囲を1回で置き換える（Ctrl+Z 1回で戻せるように。以前は1文字ずつ入っていた）
        //    再変換中にカーソルが動いたら（クリックなど）、再変換を取り消す（以前は動いた先に文字が入り、複製されていた）
        private bool _reconvertActive;
        private TextEditor? _reconvertActiveEditor;
        private int _reconvertRangeStart;
        private int _reconvertRangeLength;
        private string _reconvertRangeText = string.Empty;
        private IntPtr _reconvertHwnd;

        // ウィンドウができたら、ウィンドウに届くメッセージを見る
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            try
            {
                if (PresentationSource.FromVisual(this) is HwndSource source)
                {
                    source.AddHook(ImeReconversionWndProc);
                }
            }
            catch (Exception ex)
            {
                App.LogOperation($"再変換の準備に失敗: {ex.Message}");
            }
        }

        private IntPtr ImeReconversionWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_IME_COMPOSITION)
            {
                // 再変換中に確定したら、覚えた範囲を確定した文字で1回で置き換える
                if (_reconvertActive && (lParam.ToInt64() & GCS_RESULTSTR) != 0)
                {
                    try
                    {
                        if (ApplyReconvertResult(hwnd))
                        {
                            handled = true; // 1文字ずつの入力（WM_IME_CHAR）を作らせない
                            return IntPtr.Zero;
                        }
                    }
                    catch (Exception ex)
                    {
                        App.LogOperation($"再変換の確定でエラー: {ex.Message}");
                        EndReconvert();
                    }
                }
                return IntPtr.Zero;
            }
            if (msg == WM_IME_ENDCOMPOSITION)
            {
                // Esc などで再変換をやめたとき
                if (_reconvertActive) EndReconvert();
                return IntPtr.Zero;
            }
            if (msg != WM_IME_REQUEST) return IntPtr.Zero;
            _reconvertHwnd = hwnd;
            try
            {
                long request = wParam.ToInt64();
                if (request == IMR_RECONVERTSTRING)
                {
                    int size = FillReconvertString(lParam);
                    if (size > 0)
                    {
                        handled = true;
                        return new IntPtr(size);
                    }
                }
                else if (request == IMR_CONFIRMRECONVERTSTRING)
                {
                    if (ConfirmReconvertString(lParam))
                    {
                        handled = true;
                        return new IntPtr(1);
                    }
                }
            }
            catch (Exception ex)
            {
                // 答えられないときは何もしない（IME は再変換しないだけ）
                App.LogOperation($"再変換でエラー: {ex.Message}");
            }
            return IntPtr.Zero;
        }

        // 再変換に使うエディタ（今のタブのエディタに入力先があるときだけ）
        private TextEditor? GetReconvertEditor()
        {
            var editor = ActiveEditor;
            if (editor == null || editor.IsReadOnly) return null;
            if (!editor.TextArea.IsKeyboardFocusWithin) return null;
            return editor;
        }

        /// <summary>
        /// IME に渡す文字を決める。カーソルのある行（長ければ前後 ReconvertContextChars 文字まで）。
        /// 選んでいればその範囲を、選んでいなければカーソルの位置（長さ 0。範囲は IME が決める）を再変換の対象にする
        /// </summary>
        private static bool TryGetReconvertRange(TextEditor editor, out int baseOffset, out string text, out int compStart, out int compLength)
        {
            baseOffset = 0; text = string.Empty; compStart = 0; compLength = 0;
            var doc = editor.Document;
            var area = editor.TextArea;
            if (doc == null) return false;

            int selStart, selLength;
            if (!area.Selection.IsEmpty)
            {
                var seg = area.Selection.SurroundingSegment;
                if (seg == null) return false;
                selStart = seg.Offset;
                selLength = seg.Length;
            }
            else
            {
                selStart = area.Caret.Offset;
                selLength = 0;
            }

            var line = doc.GetLineByOffset(selStart);
            int lineStart = line.Offset;
            int lineEnd = line.EndOffset;
            // 選択が 2 行以上にまたがる・長すぎるときは再変換しない
            if (selStart + selLength > lineEnd) return false;
            if (selLength > ReconvertContextChars * 2) return false;

            int from = Math.Max(lineStart, selStart - ReconvertContextChars);
            int to = Math.Min(lineEnd, selStart + selLength + ReconvertContextChars);
            // サロゲートペア（絵文字など）の途中で切らない
            if (from > lineStart && char.IsLowSurrogate(doc.GetCharAt(from))) from--;
            if (to < lineEnd && char.IsLowSurrogate(doc.GetCharAt(to))) to++;
            if (to <= from) return false; // 空の行

            baseOffset = from;
            text = doc.GetText(from, to - from);
            compStart = selStart - from;
            compLength = selLength;
            return true;
        }

        // IMR_RECONVERTSTRING: lParam が 0 なら必要な大きさ（バイト）を返す。0 でなければ RECONVERTSTRING に書き込む
        private int FillReconvertString(IntPtr lParam)
        {
            var editor = GetReconvertEditor();
            if (editor == null) return 0;
            if (!TryGetReconvertRange(editor, out int baseOffset, out string text, out int compStart, out int compLength)) return 0;

            int size = ReconvertHeaderSize + (text.Length + 1) * sizeof(char);
            if (lParam == IntPtr.Zero) return size;

            int givenSize = Marshal.ReadInt32(lParam, 0);
            if (givenSize < size) return 0;

            Marshal.WriteInt32(lParam, 0, size);                     // dwSize
            Marshal.WriteInt32(lParam, 4, 0);                        // dwVersion
            Marshal.WriteInt32(lParam, 8, text.Length);              // dwStrLen（文字数）
            Marshal.WriteInt32(lParam, 12, ReconvertHeaderSize);     // dwStrOffset（構造体の先頭からのバイト数）
            Marshal.WriteInt32(lParam, 16, compLength);              // dwCompStrLen（文字数）
            Marshal.WriteInt32(lParam, 20, compStart * sizeof(char)); // dwCompStrOffset（文字の先頭からのバイト数）
            Marshal.WriteInt32(lParam, 24, compLength);              // dwTargetStrLen
            Marshal.WriteInt32(lParam, 28, compStart * sizeof(char)); // dwTargetStrOffset
            char[] chars = (text + "\0").ToCharArray();
            Marshal.Copy(chars, 0, lParam + ReconvertHeaderSize, chars.Length);

            _reconvertEditor = editor;
            _reconvertBaseOffset = baseOffset;
            _reconvertText = text;
            return size;
        }

        // IMR_CONFIRMRECONVERTSTRING: IME が決めた範囲をエディタで選ぶ（確定すると、その範囲が変換した文字に置き換わる）
        private bool ConfirmReconvertString(IntPtr lParam)
        {
            if (lParam == IntPtr.Zero) return false;
            var editor = GetReconvertEditor();
            if (editor == null || !ReferenceEquals(editor, _reconvertEditor)) return false;

            int strLen = Marshal.ReadInt32(lParam, 8);
            int strOffset = Marshal.ReadInt32(lParam, 12);
            int compLen = Marshal.ReadInt32(lParam, 16);
            int compOffset = Marshal.ReadInt32(lParam, 20);
            if (strOffset != ReconvertHeaderSize || strLen != _reconvertText.Length) return false;
            if (compOffset < 0 || compOffset % sizeof(char) != 0 || compLen < 0) return false;
            int compStart = compOffset / sizeof(char);
            if (compStart + compLen > strLen) return false;

            // 渡した後に文字が変わっていたら選ばない
            var doc = editor.Document;
            if (_reconvertBaseOffset + strLen > doc.TextLength) return false;
            if (doc.GetText(_reconvertBaseOffset, strLen) != _reconvertText) return false;

            EndReconvert();
            int rangeStart = _reconvertBaseOffset + compStart;
            editor.Select(rangeStart, compLen);

            // 再変換中として覚える（選んだ後につなぐ。選んだときのカーソルの動きで取り消さないように）
            _reconvertActive = true;
            _reconvertActiveEditor = editor;
            _reconvertRangeStart = rangeStart;
            _reconvertRangeLength = compLen;
            _reconvertRangeText = doc.GetText(rangeStart, compLen);
            editor.TextArea.Caret.PositionChanged += ReconvertCaret_Moved;
            editor.TextArea.SelectionChanged += ReconvertCaret_Moved;
            return true;
        }

        // 再変換中にカーソル・選択が範囲から外れたら（クリックなど）、入力中の文字を取り消す
        private void ReconvertCaret_Moved(object? sender, EventArgs e)
        {
            if (!_reconvertActive || _reconvertActiveEditor == null) return;
            var area = _reconvertActiveEditor.TextArea;
            var seg = area.Selection.IsEmpty ? null : area.Selection.SurroundingSegment;
            int selStart = seg?.Offset ?? area.Caret.Offset;
            int selLength = seg?.Length ?? 0;
            if (selStart == _reconvertRangeStart && selLength == _reconvertRangeLength
                && area.Caret.Offset == _reconvertRangeStart + _reconvertRangeLength) return;

            EndReconvert();
            try
            {
                IntPtr hwnd = _reconvertHwnd != IntPtr.Zero ? _reconvertHwnd : new WindowInteropHelper(this).Handle;
                IntPtr himc = ImmGetContext(hwnd);
                if (himc != IntPtr.Zero)
                {
                    ImmNotifyIME(himc, NI_COMPOSITIONSTR, CPS_CANCEL, 0);
                    ImmReleaseContext(hwnd, himc);
                }
            }
            catch (Exception ex)
            {
                App.LogOperation($"再変換の取り消しでエラー: {ex.Message}");
            }
        }

        // 確定した文字で、覚えた範囲を置き換える（範囲の文字が変わっていたら false。そのときは今まで通りの入力にまかせる）
        private bool ApplyReconvertResult(IntPtr hwnd)
        {
            var editor = _reconvertActiveEditor;
            int start = _reconvertRangeStart;
            int length = _reconvertRangeLength;
            string original = _reconvertRangeText;
            EndReconvert(); // 先に外す（置き換えでカーソルが動いても取り消さないように）
            if (editor == null || editor.IsReadOnly) return false;

            string result;
            IntPtr himc = ImmGetContext(hwnd);
            if (himc == IntPtr.Zero) return false;
            try
            {
                int bytes = ImmGetCompositionStringW(himc, GCS_RESULTSTR, null, 0);
                if (bytes <= 0) return false;
                var buf = new byte[bytes];
                if (ImmGetCompositionStringW(himc, GCS_RESULTSTR, buf, bytes) != bytes) return false;
                result = System.Text.Encoding.Unicode.GetString(buf);
            }
            finally
            {
                ImmReleaseContext(hwnd, himc);
            }

            var doc = editor.Document;
            if (start + length > doc.TextLength || doc.GetText(start, length) != original) return false;
            doc.Replace(start, length, result); // 1回の置き換え＝Ctrl+Z 1回で戻る
            editor.TextArea.ClearSelection();
            editor.TextArea.Caret.Offset = start + result.Length;
            editor.TextArea.Caret.BringCaretToView();
            return true;
        }

        // 再変換中の印を消す
        private void EndReconvert()
        {
            if (_reconvertActiveEditor != null)
            {
                _reconvertActiveEditor.TextArea.Caret.PositionChanged -= ReconvertCaret_Moved;
                _reconvertActiveEditor.TextArea.SelectionChanged -= ReconvertCaret_Moved;
            }
            _reconvertActive = false;
            _reconvertActiveEditor = null;
        }
    }
}
