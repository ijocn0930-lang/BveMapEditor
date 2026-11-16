using ICSharpCode.AvalonEdit;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace BveMapEditor
{
    /// <summary>
    /// SearchWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class SearchWindow : Window
    {
        // ★ ここにフィールドを宣言します
        // ★ 検索ウィンドウを使い回すとき、検索するエディタ（今のタブ）を差し替えられるように readonly を外した
        private TextEditor _editor;
        private readonly AppSettings _settings; // ★ 設定オブジェクトを受け取る
        // ★ ウィンドウを閉じても記憶しておくための静的（static）フィールド
        internal SearchWindow(TextEditor editor, AppSettings settings)
        {
            InitializeComponent();
            // ③ 引数で受け取った editor をフィールドに代入
            _editor = editor;
            _settings = settings;

            // 履歴の読み込みとバインド
            if (_settings.RetainSearchHistory)
            {
                CmbSearchQuery.ItemsSource = _settings.SearchHistory;
                CmbReplaceQuery.ItemsSource = _settings.ReplaceHistory;

                if (_settings.SearchHistory.Count > 0)
                {
                    CmbSearchQuery.Text = _settings.SearchHistory[0];
                }
                if (_settings.ReplaceHistory.Count > 0)
                {
                    CmbReplaceQuery.Text = _settings.ReplaceHistory[0];
                }

                ChkMatchCase.IsChecked = _settings.LastMatchCase;
                ChkLoopSearch.IsChecked = _settings.LastLoopSearch;
                ChkWholeWord.IsChecked = _settings.LastWholeWord; // ★ (2026-11-15) 「単語単位」も戻す（以前は保存だけして読み込んでいなかった）
                ChkWarnLargeReplace.IsChecked = _settings.LastWarnLargeReplace;
            }
            else
            {
                ChkLoopSearch.IsChecked = true;
                ChkWarnLargeReplace.IsChecked = true;
            }

            CmbSearchQuery.Focus();
// ★ ComboBox 内の TextBox を取得して全選択状態にする場合
            if (CmbSearchQuery.Template.FindName("PART_EditableTextBox", CmbSearchQuery) is System.Windows.Controls.TextBox textBox)
            {
                textBox.SelectAll();
            }        }
        /// <summary>
        /// ★ すでに開いている検索ウィンドウを使い回すときに呼ぶ: 検索するエディタを今のタブに差し替え、検索欄の文字を全選択する
        /// </summary>
        internal void ReuseFor(TextEditor editor)
        {
            SetEditor(editor);
            CmbSearchQuery.Focus();
            if (CmbSearchQuery.Template.FindName("PART_EditableTextBox", CmbSearchQuery) is System.Windows.Controls.TextBox textBox)
            {
                textBox.SelectAll();
            }
        }

        /// <summary>
        /// ★ 検索・置換する相手のエディタを差し替える（タブを切り替えたときにメインウィンドウから呼ぶ）。検索欄の入力などはそのまま
        /// </summary>
        internal void SetEditor(TextEditor editor)
        {
            if (editor != null) _editor = editor;
        }

        /// <summary>
        /// ★ 閉じる直前に、持ち主のメインウィンドウを前に出す
        ///    （出さないと、Windows が別のアプリを前に出してしまい、メインウィンドウが後ろに回ることがあった）
        /// </summary>
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);
            if (!e.Cancel)
            {
                try { Owner?.Activate(); } catch { }
            }
        }

        // 検索文字列・置換文字列を履歴リストに追加するヘルパーメソッド
        private void AddToHistory(ComboBox comboBox, List<string> historyList)
        {
            string text = comboBox.Text;
            if (string.IsNullOrWhiteSpace(text)) return;

            // 既存の同じ文字列があれば削除（先頭に移動させるため）
            historyList.Remove(text);

            // 先頭に追加
            historyList.Insert(0, text);

            // 最大件数（例: 10件）を超えたら古いものを削除
            const int MaxHistoryCount = 10;
            if (historyList.Count > MaxHistoryCount)
            {
                historyList.RemoveAt(historyList.Count - 1);
            }
        }
        // ウィンドウが閉じられる直前に、設定の保持フラグに応じて値を AppSettings に保存する
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            if (_settings.RetainSearchHistory)
            {
                // ウィンドウを閉じるときにも現在の入力を履歴に反映
                AddToHistory(CmbSearchQuery, _settings.SearchHistory);
                AddToHistory(CmbReplaceQuery, _settings.ReplaceHistory);

                _settings.LastMatchCase = ChkMatchCase.IsChecked == true;
                _settings.LastLoopSearch = ChkLoopSearch.IsChecked == true;
                _settings.LastWholeWord = ChkWholeWord.IsChecked == true; // ★ 保存
                _settings.LastWarnLargeReplace = ChkWarnLargeReplace.IsChecked == true;
            }
            else
            {
                _settings.SearchHistory.Clear();
                _settings.ReplaceHistory.Clear();
            }
        }
        private void BtnFindNext_Click(object sender, RoutedEventArgs e)
        {
            PerformSearch();
        }

        // ★ 検索欄で Enter を押したら「次を検索」と同じ動き
        //    （以前はこの処理がどこにもつながっていなかったため、Enter を押しても検索されなかった。SearchWindow.xaml の CmbSearchQuery につないだ）
        //    履歴の一覧を開いているときの Enter は、一覧から選ぶ操作なので検索しない
        private void CmbQuery_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !CmbSearchQuery.IsDropDownOpen)
            {
                PerformSearch();
                e.Handled = true;
            }
        }
        private bool IsWholeWordMatch(string text, int index, int length, StringComparison comparison)
        {
            // 前の文字チェック
            if (index > 0)
            {
                char prevChar = text[index - 1];
                if (IsWordChar(prevChar)) return false;
            }

            // 後ろの文字チェック
            int endIndex = index + length;
            if (endIndex < text.Length)
            {
                char nextChar = text[endIndex];
                if (IsWordChar(nextChar)) return false;
            }

            return true;
        }

        private bool IsWordChar(char c)
        {
            // 英数字およびアンダースコアを単語の一部とみなす
            return char.IsLetterOrDigit(c) || c == '_';
        }
        private bool PerformSearch()
        {
            string query = CmbSearchQuery.Text;
            if (string.IsNullOrWhiteSpace(query))
            {
                MessageBox.Show(this, "検索する文字列を入力してください。", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                CmbSearchQuery.Focus();
                return false;
            }

            // 検索実行時に履歴へ追加
            if (_settings.RetainSearchHistory)
            {
                AddToHistory(CmbSearchQuery, _settings.SearchHistory);
                CmbSearchQuery.ItemsSource = null;
                CmbSearchQuery.ItemsSource = _settings.SearchHistory;
                CmbSearchQuery.Text = query; // テキストがリセットされるのを防ぐ
            }

            string text = _editor.Document.Text;
            StringComparison comparison = ChkMatchCase.IsChecked == true
                ? StringComparison.Ordinal
                : StringComparison.OrdinalIgnoreCase;

            int startOffset = _editor.CaretOffset;
            int index = text.IndexOf(query, startOffset, comparison);
            int tempIndex = startOffset;
            bool isWholeWord = ChkWholeWord.IsChecked == true;
            while (true)
            {
                index = text.IndexOf(query, tempIndex, comparison);
                if (index == -1) break;

                if (!isWholeWord || IsWholeWordMatch(text, index, query.Length, comparison))
                {
                    break;
                }

                tempIndex = index + 1;
                if (tempIndex >= text.Length) break;
            }
            // 見つからず、かつループ検索が有効な場合は先頭から再検索
            if (index == -1 && ChkLoopSearch.IsChecked == true)
            {
                tempIndex = 0;
                while (true)
                {
                    index = text.IndexOf(query, tempIndex, comparison);
                    if (index == -1) break;

                    if (!isWholeWord || IsWholeWordMatch(text, index, query.Length, comparison))
                    {
                        break;
                    }

                    tempIndex = index + 1;
                    if (tempIndex >= text.Length) break;
                }
            }
            // 見つからなかった場合の処理
            if (index != -1)
            {
                _editor.Select(index, query.Length);
                _editor.ScrollToLine(_editor.Document.GetLineByOffset(index).LineNumber);
                return true;
            }
            else
            {
                MessageBox.Show(this, "一致する文字列が見つかりませんでした。", "検索", MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }
        }
        // 選択中の一致箇所を置換して次を検索
        private void BtnReplace_Click(object sender, RoutedEventArgs e)
        {
            string query = CmbSearchQuery.Text;
            if (string.IsNullOrWhiteSpace(query))
            {
                MessageBox.Show(this, "検索する文字列を入力してください。", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                CmbSearchQuery.Focus();
                return;
            }
            if (_settings.RetainSearchHistory)
            {
                AddToHistory(CmbSearchQuery, _settings.SearchHistory);
                AddToHistory(CmbReplaceQuery, _settings.ReplaceHistory);
            }
            StringComparison comparison = ChkMatchCase.IsChecked == true ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            string selectedText = _editor.SelectedText;

            if (!string.IsNullOrEmpty(selectedText) && string.Equals(selectedText, query, comparison))
            {
                int offset = _editor.SelectionStart;
                string replaceText = CmbReplaceQuery.Text ?? string.Empty;

                _editor.Document.Replace(offset, query.Length, replaceText);
                _editor.CaretOffset = offset + replaceText.Length;
            }
            // 次の一致箇所へ移動
            PerformSearch();
        }

        // すべて置換
        private void BtnReplaceAll_Click(object sender, RoutedEventArgs e)
        {

            string query = CmbSearchQuery.Text;
            if (string.IsNullOrWhiteSpace(query))
            {
                MessageBox.Show(this, "検索する文字列を入力してください。", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                CmbSearchQuery.Focus();
                return;
            }
            if (_settings.RetainSearchHistory)
            {
                AddToHistory(CmbSearchQuery, _settings.SearchHistory);
                AddToHistory(CmbReplaceQuery, _settings.ReplaceHistory);
            }
            string text = _editor.Document.Text;
            string replaceText = CmbReplaceQuery.Text ?? string.Empty;
            StringComparison comparison = ChkMatchCase.IsChecked == true ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            // ★ 1. まず置換する位置をすべて見つける（件数もここで分かる）
            //    (2026-11-15) 以前は1件置換するたびに本文全体を作り直して探し直していたため、大きいファイルで件数が多いととても遅かった
            var positions = new List<int>();
            int tempIndex = 0;
            bool isWholeWord = ChkWholeWord.IsChecked == true;
            while (true)
            {
                tempIndex = text.IndexOf(query, tempIndex, comparison);
                if (tempIndex == -1) break;

                if (!isWholeWord || IsWholeWordMatch(text, tempIndex, query.Length, comparison))
                {
                    positions.Add(tempIndex);
                }
                tempIndex += query.Length;
            }
            int matchCount = positions.Count;

            if (matchCount == 0)
            {
                MessageBox.Show(this, "一致する文字列が見つかりませんでした。", "検索", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            // ★ 2. 大量置換（例: 100件以上）かつ警告オプションが有効な場合に確認ダイアログを表示
            const int Threshold = 100;
            if (ChkWarnLargeReplace.IsChecked == true && matchCount >= Threshold)
            {
                var result = MessageBox.Show(this,
                    $"{matchCount} 箇所の置換が見つかりました。\n大量の置換処理を実行すると、一時的にパフォーマンスが低下する可能性があります。\n\n続行しますか？",
                    "パフォーマンス警告",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.No)
                {
                    return; // ユーザーが「いいえ」を選んだ場合は中止
                }
            }
            // ★ 2. 見つけた位置を「後ろから」順に置換する（後ろから直せば、前の位置がずれない）
            //    BeginUpdate ～ EndUpdate の間の変更は、Ctrl+Z 1回でまとめて元に戻せる（以前と同じ）
            int count = 0;
            _editor.Document.BeginUpdate();
            try
            {
                for (int i = positions.Count - 1; i >= 0; i--)
                {
                    _editor.Document.Replace(positions[i], query.Length, replaceText);
                    count++;
                }
            }
            finally
            {
                _editor.Document.EndUpdate();
            }

            MessageBox.Show(this, $"{count} 箇所を置換しました。", "すべて置換", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnEnd_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
