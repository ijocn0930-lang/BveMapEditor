using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BveMapEditor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // タブごとのファイル情報を保持する小クラス
        private class TabDocumentInfo
        {
            public ICSharpCode.AvalonEdit.TextEditor? Editor { get; set; }
            public string? FilePath { get; set; }
            public bool IsModified { get; set; }
            public Encoding? FileEncoding { get; set; }
            public TabItem? TabItem { get; set; }
// 現在開いているファイルのパスとエンコーディングを保持
        }


        // 構文チェックのデバウンスとキャンセル用
        private CancellationTokenSource? _syntaxCts;
        // デフォルトデバウンスを少し長めにして入力遅延を緩和
        private static readonly TimeSpan SyntaxDebounceDelay = TimeSpan.FromMilliseconds(700);

        // メニューから現在のタブを閉じる
        private void MenuCloseTab_Click(object sender, RoutedEventArgs e)
        {
            CloseTab(null, promptToSave: true);
        }

        // メニューからすべてのタブを閉じる
        private void MenuCloseAllTabs_Click(object sender, RoutedEventArgs e)
        {
            CloseAllTabs(promptToSave: true);
        }

        // タブを閉じる（指定タブまたはアクティブタブ）
        private bool CloseTab(TabItem? tab, bool promptToSave = true)
        {
            if (EditorTabControl == null) return true;

            // 既定でアクティブタブを閉じる
            tab ??= EditorTabControl.SelectedItem as TabItem;

            if (tab == null) return true;

            var info = GetTabInfo(tab);

            // 変更がある場合は保存確認
            if (promptToSave && info != null && info.IsModified)
            {
                var result = MessageBox.Show(this, $"'{tab.Header}' は未保存です。保存しますか?", "保存確認", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (result == MessageBoxResult.Cancel) return false;
                if (result == MessageBoxResult.Yes)
                {
                    if (!SaveTabDocument(info)) return false;
                }
            }

            // タブを削除
            EditorTabControl.Items.Remove(tab);

            // 最低でも1つのタブが必要な場合は、新しい無題タブを作成
            if (EditorTabControl.Items.Count == 0)
            {
                CreateNewUntitledTab();
            }

            return true;
        }

        // すべてのタブを閉じる
        private bool CloseAllTabs(bool promptToSave = true)
        {
            if (EditorTabControl == null) return true;

            // 変更のあるタブを集める
            var modified = new System.Collections.Generic.List<TabDocumentInfo>();
            foreach (var item in EditorTabControl.Items)
            {
                if (item is TabItem ti)
                {
                    var info = GetTabInfo(ti);
                    if (info != null && info.IsModified) modified.Add(info);
                }
            }

            if (promptToSave && modified.Count > 0)
            {
                var res = MessageBox.Show(this, "変更されたファイルをすべて保存しますか?", "保存確認", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (res == MessageBoxResult.Cancel) return false;
                if (res == MessageBoxResult.Yes)
                {
                    if (!SaveAllModifiedTabs(modified)) return false;
                }
            }

            EditorTabControl.Items.Clear();

            // 最低でも1つのタブを残す
            CreateNewUntitledTab();

            return true;
        }

        // 新しい無題タブを作成するヘルパー
        private void CreateNewUntitledTab()
        {
            if (EditorTabControl == null) return;

            var editor = new ICSharpCode.AvalonEdit.TextEditor
            {
                MinHeight = 120,
                FontFamily = MapTextEditor?.FontFamily ?? new FontFamily("Consolas"),
                FontSize = MapTextEditor?.FontSize ?? 14.0,
                ShowLineNumbers = true
            };
            editor.TextChanged += MapTextEditor_TextChanged;

            var tab = new TabItem { Header = "無題", Content = editor };
            var info = new TabDocumentInfo { Editor = editor, FilePath = null, IsModified = false, FileEncoding = null, TabItem = tab };
            tab.Tag = info;
            EditorTabControl.Items.Add(tab);
            EditorTabControl.SelectedItem = tab;
        }

        // Tab 操作用のユーティリティ
        private static TabDocumentInfo? GetTabInfo(TabItem? tab)
        {
            return tab?.Tag as TabDocumentInfo;
        }

        private TabDocumentInfo? FindTabByEditor(ICSharpCode.AvalonEdit.TextEditor? editor)
        {
            if (editor == null || EditorTabControl == null) return null;
            foreach (var item in EditorTabControl.Items)
            {
                if (item is TabItem ti && ti.Content is ICSharpCode.AvalonEdit.TextEditor te)
                {
                    if (te == editor) return ti.Tag as TabDocumentInfo;
                }
            }
            return null;
        }

        // すべての未保存タブを保存します。途中でユーザーがキャンセルした場合は false を返します。
        private bool SaveAllModifiedTabs(System.Collections.Generic.List<TabDocumentInfo> modifiedTabs)
        {
            foreach (var info in modifiedTabs)
            {
                if (!SaveTabDocument(info))
                {
                    return false;
                }
            }
            return true;
        }

        // 単一タブの保存処理。ユーザーが保存ダイアログをキャンセルした場合は false を返す。
        private bool SaveTabDocument(TabDocumentInfo info)
        {
            if (info == null || info.Editor == null || info.TabItem == null) return true;

            // 一時的にそのタブを選択して ActiveEditor を合わせる
            var prev = EditorTabControl?.SelectedItem;
            try
            {
                if (EditorTabControl != null)
                {
                    EditorTabControl.SelectedItem = info.TabItem;
                }

                // 既にパスがある場合は上書き保存
                if (!string.IsNullOrEmpty(info.FilePath))
                {
                    // currentFileEncoding は編集時に更新されている可能性があるため、そのまま渡す
                    currentFilePath = info.FilePath;
                    SaveFile(info.FilePath, info.FileEncoding, useDialogIfNull: false);
                    info.IsModified = false;
                    return true;
                }

                // パスが無い場合は保存ダイアログを表示
                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "Map File (*.txt)|*.txt|All Files (*.*)|*.*",
                    Title = "名前を付けて保存",
                    DefaultExt = ".txt"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    string path = saveFileDialog.FileName;
                    currentFilePath = path;
                    SaveFile(path);
                    info.FilePath = path;
                    info.IsModified = false;
                    return true;
                }

                // ユーザーがキャンセル
                return false;
            }
            finally
            {
                if (EditorTabControl != null)
                {
                    EditorTabControl.SelectedItem = prev;
                }
            }
        }
        private string _copiedFilePath = string.Empty;
        // 上書きモードフラグ (false = 挿入, true = 上書き)
        private bool _isOverwriteMode = false;
        // MainWindow クラス内、または適当なヘルパー領域に追加してください
        #region アイコン取得用のWin32API定義
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }
        private void UpdateEditModeStatus()
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    if (EditModeText != null)
                    {
                        EditModeText.Text = _isOverwriteMode ? "上書き" : "挿入";
                    }
                }
                catch
                {
                    // ignore
                }
            });
        }
        private void MenuStatusBar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is MenuItem mi && MainStatusBar != null)
                {
                    MainStatusBar.Visibility = mi.IsChecked ? Visibility.Visible : Visibility.Collapsed;
                    // 設定に反映して保存
                    currentSettings.ShowStatusBar = mi.IsChecked;
                    currentSettings.Save();
                }
            }
            catch
            {
                // ignore
            }
        }

        private void MenuAllowOverwriteToggle_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is MenuItem mi)
                {
                    currentSettings.AllowOverwriteToggle = mi.IsChecked;
                    currentSettings.Save();
                }
            }
            catch
            {
                // ignore
            }
        }
        /// <summary>
        /// ステータスバーに現在のキャレット位置 (行, 列) を表示します。
        /// </summary>
        private void UpdateCaretPositionStatus()
        {
            // UIスレッドで安全に更新する
            Dispatcher.Invoke(() =>
            {
                if (CaretPositionText == null)
                {
                    return;
                }

                try
                {
                    var editor = ActiveEditor;
                    if (editor?.TextArea?.Caret != null && editor.Document != null)
                    {
                        int line = editor.TextArea.Caret.Line;
                        int column = editor.TextArea.Caret.Column;
                        CaretPositionText.Text = $"行 {line} 列 {column}";
                        return;
                    }
                }
                catch
                {
                    // フォールバックでドキュメントから算出を試みる
                }

                // フォールバック: Document と Caret が利用できる場合はオフセットから取得
                try
                {
                    var editor = ActiveEditor;
                    if (editor?.Document != null && editor.TextArea?.Caret != null)
                    {
                        var loc = editor.Document.GetLocation(editor.TextArea.Caret.Offset);
                        CaretPositionText.Text = $"行 {loc.Line} 列 {loc.Column}";
                        return;
                    }
                }
                catch
                {
                    // 最終的に空文字にする
                }

                CaretPositionText.Text = string.Empty;
            });
        }

        // 現在アクティブな AvalonEdit テキストエディタを返します（タブ対応）。存在しない場合は null を返す。
        private ICSharpCode.AvalonEdit.TextEditor? GetActiveEditor()
        {
            // タブコントロールが存在し、選択タブの内容が TextEditor の場合はそれを返す
            if (EditorTabControl?.SelectedItem is TabItem ti && ti.Content is ICSharpCode.AvalonEdit.TextEditor te)
            {
                return te;
            }

            // それ以外は既存の MapTextEditor を返す（XAML により通常は存在する）
            if (MapTextEditor != null) return MapTextEditor;

            // 見つからない場合は null を返す
            return null;
        }

        // 簡易アクセサ: 現在アクティブなエディタ（null許容）
        private ICSharpCode.AvalonEdit.TextEditor? ActiveEditor => GetActiveEditor();

        private const uint SHGFI_ICON = 0x000000100;     // アイコンを取得
        private const uint SHGFI_SMALLICON = 0x000000001; // 小さいアイコン（16x16）
        private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010; // ファイルが存在しない場合でも拡張子から推測

        private string _currentRootFolderPath = string.Empty;
        private string _currentBveepFilePath = string.Empty; // 現在開いている .bveep ファイルのパス（上書き保
        // ファイルパスから WPF で使える ImageSource（アイコン）を取得するメソッド
        private static ImageSource? GetFileIcon(string filePath, bool isDirectory)
        {
            var shfi = new SHFILEINFO();
            uint flags = SHGFI_ICON | SHGFI_SMALLICON;

            // フォルダかファイルかでフラグを調整
            if (!isDirectory && !File.Exists(filePath))
            {
                flags |= SHGFI_USEFILEATTRIBUTES;
            }

            IntPtr hIcon = SHGetFileInfo(filePath, (uint)(isDirectory ? 128 : 0), ref shfi, (uint)Marshal.SizeOf(shfi), flags);

            if (shfi.hIcon != IntPtr.Zero)
            {
                try
                {
                    // Win32のHICONをWPFのImageSourceに変換
                    ImageSource imageSource = Imaging.CreateBitmapSourceFromHIcon(
                        shfi.hIcon,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());

                    imageSource.Freeze(); // パフォーマンス向上のためフリーズ
                    return imageSource;
                }
                finally
                {
                    // メモリリークを防ぐためにアイコンハンドルを破棄
                    DestroyIcon(shfi.hIcon);
                }
            }

            return null;
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr hIcon);
        #endregion
        private AppSettings currentSettings; // ★設定保持用の変数

        // 現在開いているファイルのパスを保持（未保存の場合は null）
        private string? currentFilePath = null;
        // 現在開いているファイルのエンコーディング（読み込み時に検出）。未設定時は null
        private Encoding? currentFileEncoding = null;
        // クラスのフィールド等で保持しておく
        private static readonly ICompletionData[] cachedSyntaxes = BveSyntaxProvider.GetAllSyntaxes().ToArray();

        // 未保存フラグ
        private bool isModified = false;
        //初期化完了フラグ
        private bool isInitializing = false;

        // ファイル読み込み中フラグ（TextChangedの誤発火防止用）
        private bool isLoading = false;

        // サンプル先頭バイト列からエンコーディングを推定します（BOM、UTF-8厳密判定、UTF-16のnullバイト分布、Shift_JIS試行など）
        private static Encoding DetectEncodingFromSample(byte[] sample)
        {
            if (sample == null || sample.Length == 0) return Encoding.Default;

            // BOM 判定
            if (sample.Length >= 3 && sample[0] == 0xEF && sample[1] == 0xBB && sample[2] == 0xBF)
            {
                return new UTF8Encoding(false);
            }
            if (sample.Length >= 2 && sample[0] == 0xFF && sample[1] == 0xFE)
            {
                return Encoding.Unicode; // UTF-16 LE
            }
            if (sample.Length >= 2 && sample[0] == 0xFE && sample[1] == 0xFF)
            {
                return Encoding.BigEndianUnicode; // UTF-16 BE
            }
            if (sample.Length >= 4 && sample[0] == 0x00 && sample[1] == 0x00 && sample[2] == 0xFE && sample[3] == 0xFF)
            {
                return Encoding.UTF32;
            }

            // 厳密な UTF-8 判定（例外を投げる設定で試す）
            try
            {
                var utf8Strict = new UTF8Encoding(false, true);
                utf8Strict.GetString(sample);
                return Encoding.UTF8;
            }
            catch (DecoderFallbackException)
            {
                // 続行して他候補を試す
            }

            // UTF-16 の可能性を null バイト分布で判定
            int zeroCount = sample.Count(b => b == 0);
            if (zeroCount > sample.Length / 10) // 10% を閾値に heuristics
            {
                int zerosAtEven = 0, zerosAtOdd = 0;
                for (int i = 0; i < sample.Length; i++)
                {
                    if (sample[i] == 0)
                    {
                        if ((i & 1) == 0) zerosAtEven++; else zerosAtOdd++;
                    }
                }
                // UTF-16 LE は奇数インデックスに 0 が出やすい（ASCII の低バイトが先に来るため）
                if (zerosAtOdd > zerosAtEven) return Encoding.Unicode;
                return Encoding.BigEndianUnicode;
            }

            // Shift_JIS (CP932) を例外フェールバックで試す
            try
            {
                var sjis = Encoding.GetEncoding(932, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
                sjis.GetString(sample);
                return sjis;
            }
            catch
            {
                // 無視して最後のフォールバックへ
            }

            // 最終フォールバックはシステム既定（ANSI）
            return Encoding.Default;
        }



        // ファイル読み込みヘルパー: 先頭の一部を解析してエンコーディングを決定して読み込みます
        private static string ReadAllTextWithAnsiFallback(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            int sampleSize = Math.Min(bytes.Length, 4096);
            byte[] sample = bytes.Take(sampleSize).ToArray();
            Encoding enc = DetectEncodingFromSample(sample);
            try
            {
                return enc.GetString(bytes);
            }
            catch
            {
                // 万が一デコードで失敗したら最終フォールバック
                return Encoding.Default.GetString(bytes);
            }
        }

        private static async Task<string[]> ReadAllLinesWithAnsiFallbackAsync(string path)
        {
            byte[] bytes = await File.ReadAllBytesAsync(path);
            int sampleSize = Math.Min(bytes.Length, 4096);
            byte[] sample = bytes.Take(sampleSize).ToArray();
            Encoding enc = DetectEncodingFromSample(sample);
            string text;
            try
            {
                text = enc.GetString(bytes);
            }
            catch
            {
                text = Encoding.Default.GetString(bytes);
            }
            return text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        }
        //エクスプローラペイン自動更新
        private FileSystemWatcher? _fileSystemWatcher;
        public MainWindow()
        {
            isInitializing = true;
            InitializeComponent();
            // ステータスバーのメニュー状態を初期化
            try
            {
                if (MenuStatusBar != null && MainStatusBar != null)
                {
                    MenuStatusBar.IsChecked = MainStatusBar.Visibility == Visibility.Visible;
                }
            }
            catch
            {
                // 無視
            }

            // ★ Undo/Redo の履歴状態が変化したときにメニューの有効/無効を更新するイベントを購読
            // エディタ参照を取得（タブ対応）
            var editor = ActiveEditor ?? MapTextEditor;

            // ★ Undo/Redo の履歴状態が変化したときにメニューの有効/無効を更新するイベントを購読
            if (editor.Document != null && editor.Document.UndoStack != null)
            {
                editor.Document.UndoStack.PropertyChanged += UndoStack_PropertyChanged;
            }
            // AvalonEdit のハイライト初期化
            MapHighlighter.InitializeHighlighting(editor);
            // ★1. 起動時に設定をロードしてエディタに適用
            currentSettings = AppSettings.Load();
            ApplySettingsToEditor();
            ApplyWindowSettings(); // ★ ウィンドウ・ペイン設定の適用
            // AvalonEdit のハイライト初期化（再度）
            MapHighlighter.InitializeHighlighting(editor);
            // ★ 補完用イベントの購読
            if (editor != null)
            {
                editor.TextArea.TextEntered += MapTextEditor_TextEntered;
                editor.TextArea.TextEntering += MapTextEditor_TextEntering;
            }

            // 初期タブ（XAMLで定義されている無題タブ）に TabDocumentInfo を設定
            try
            {
                if (EditorTabControl != null && EditorTabControl.Items.Count > 0 && EditorTabControl.Items[0] is TabItem firstTab)
                {
                    var info = new TabDocumentInfo { Editor = MapTextEditor, FilePath = null, IsModified = false, FileEncoding = null, TabItem = firstTab };
                    firstTab.Tag = info;
                }
            }
            catch
            {
                // ignore
            }

            // Insert キーで挿入/上書きモードを切り替え
            editor?.TextArea.KeyDown += (s, e) =>
            {
                try
                {
                    if (e.Key == Key.Insert)
                    {
                        // 設定で許可されている場合のみ切替
                        if (currentSettings == null || currentSettings.AllowOverwriteToggle)
                        {
                            _isOverwriteMode = !_isOverwriteMode;
                            UpdateEditModeStatus();
                        }
                        e.Handled = true;
                    }
                }
                catch
                {
                    // ignore
                }
            };
            // PreviewKeyDown でも捕捉する（環境により KeyDown が来ないことがあるため）
            editor?.PreviewKeyDown += (s, e) =>
            {
                try
                {
                    if (e.Key == Key.Insert)
                    {
                        if (currentSettings == null || currentSettings.AllowOverwriteToggle)
                        {
                            _isOverwriteMode = !_isOverwriteMode;
                            UpdateEditModeStatus();
                        }
                        e.Handled = true;
                    }
                }
                catch
                {
                    // ignore
                }
            };

            if (editor?.Document != null && editor.Document.UndoStack != null)
            {
                editor.Document.UndoStack.PropertyChanged += UndoStack_PropertyChanged;
            }
            UpdateEditMenuState();
            // AvalonEdit は標準コマンド（Cut, Copy, Paste）をサポートしていますが、
            // ウィンドウ全体で確実に機能させるためのバインディング設定例です
            // ★ AvalonEditの機能にコマンドを直接ルーティングする
            // ★ 選択範囲がないときは実行されない（あるいは何もしない）ように確実にガードする
            // ★ エディタの選択範囲が変わったときのイベントを購読
            editor?.TextArea.SelectionChanged += (s, e) =>
            {
                UpdateEditMenuState();
                UpdateCaretPositionStatus();
            };
            // ★ 現在行ハイライトレンダラーの組み込み（editor が存在する場合のみ）
            if (editor?.TextArea != null)
            {
                var highlightRenderer = new CurrentLineHighlightRenderer(editor.TextArea);
                editor.TextArea.TextView.BackgroundRenderers.Add(highlightRenderer);

                // カーソル位置が変わったときに再描画を走らせてハイライトを追従させる
                editor.TextArea.Caret.PositionChanged += (s, ea) =>
                {
                    // editor はクロージャでキャプチャされているが null ではないことを保証している
                    editor.TextArea.TextView.InvalidateVisual();
                    // ステータスバーにカーソル位置を表示
                    UpdateCaretPositionStatus();
                };
            }
            // 初期表示用に一度更新
            UpdateCaretPositionStatus();
            // 初期の編集モード表示
            UpdateEditModeStatus();
            isInitializing = false;

        }
        private CompletionWindow? completionWindow;


        private void ApplyWindowSettings()
        {
            // ウィンドウサイズと最大化状態の復元
            if (currentSettings.WindowWidth > 0 && currentSettings.WindowHeight > 0)
            {
                this.Width = currentSettings.WindowWidth;
                this.Height = currentSettings.WindowHeight;
            }

            if (currentSettings.IsMaximized)
            {
                this.WindowState = WindowState.Maximized;
            }

            // エクスプローラーペインの幅の復元
            if (ExplorerColumn != null && currentSettings.ExplorerPaneWidth > 0)
            {
                ExplorerColumn.Width = new GridLength(currentSettings.ExplorerPaneWidth);
            }
            // ステータスバーの表示状態を復元
            try
            {
                if (MainStatusBar != null)
                {
                    MainStatusBar.Visibility = currentSettings.ShowStatusBar ? Visibility.Visible : Visibility.Collapsed;
                }
                if (MenuStatusBar != null)
                {
                    MenuStatusBar.IsChecked = currentSettings.ShowStatusBar;
                }
            }
            catch
            {
                // ignore
            }
        }
        // UndoStack の状態変更イベント
        private void UndoStack_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            UpdateEditMenuState();
        }

        // メニューの IsEnabled を更新するメソッド
        private void UpdateEditMenuState()
        {
            var editor = ActiveEditor;
            if (editor != null)
            {
                MenuUndo.IsEnabled = editor.Document?.UndoStack?.CanUndo ?? false;
                MenuRedo.IsEnabled = editor.Document?.UndoStack?.CanRedo ?? false;
            }
            else
            {
                MenuUndo.IsEnabled = false;
                MenuRedo.IsEnabled = false;
            }
            // reuse existing 'editor' variable declared above
            bool hasSelection = editor?.SelectionLength > 0;

            if (MenuCut != null) MenuCut.IsEnabled = hasSelection;
            if (MenuCopy != null) MenuCopy.IsEnabled = hasSelection;

            // 貼り付けはクリップボードにテキストがある場合のみ有効にする
            if (MenuPaste != null) MenuPaste.IsEnabled = Clipboard.ContainsText();
        }
        // 構文エラー一覧（DataGrid）のダブルクリックイベント
        private void ErrorListDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (ErrorListDataGrid.SelectedItem is ParseError selectedError)
            {
                JumpToErrorPosition(selectedError.Line, selectedError.Column);
            }
        }

        // タブ切り替え時に ErrorList を更新
        private void EditorTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 選択タブに合わせてハイライトと補完イベントを再初期化し、構文チェックを再実行する
            try
            {
                var editor = ActiveEditor;
                if (editor != null)
                {
                    isInitializing = true;
                    // ハイライトを再適用（ファイル切替やロードの影響で外れている可能性を補正）
                    MapHighlighter.InitializeHighlighting(editor);

                    // 補完イベントの重複登録を防ぐため、一度解除してから登録する
                    try { editor.TextArea.TextEntered -= MapTextEditor_TextEntered; } catch { }
                    try { editor.TextArea.TextEntering -= MapTextEditor_TextEntering; } catch { }
                    editor.TextArea.TextEntered += MapTextEditor_TextEntered;
                    editor.TextArea.TextEntering += MapTextEditor_TextEntering;

                    // UndoStack の PropertyChanged ハンドラも確実に登録
                    try
                    {
                        if (editor.Document?.UndoStack != null)
                        {
                            editor.Document.UndoStack.PropertyChanged -= UndoStack_PropertyChanged;
                            editor.Document.UndoStack.PropertyChanged += UndoStack_PropertyChanged;
                        }
                        isInitializing = false;
                    }
                    catch
                    {
                        // ignore
                        isInitializing = false;

                    }
                }
            }
            catch
            {
                // ignore
                isInitializing = false;

            }

            // 選択タブに合わせて構文チェックを再実行し、ErrorList を更新する
            RunSyntaxCheck();
        }
        // 指定された行・列へカーソルを安全にジャンプさせる処理
        private void JumpToErrorPosition(int lineNumber, int columnNumber)
        {
            try
            {
                if (lineNumber < 1) lineNumber = 1;
                if (columnNumber < 1) columnNumber = 1;

                // ドキュメントの総行数を超えないようにクランプ
                var editor = ActiveEditor ?? MapTextEditor;
                if (editor == null) return;

                int totalLines = editor.Document.LineCount;
                if (lineNumber > totalLines) lineNumber = totalLines;

                // AvalonEdit の行オブジェクトを取得
                DocumentLine line = editor.Document.GetLineByNumber(lineNumber);

                // オフセット（文字位置）を計算
                int offset = line.Offset + Math.Max(0, Math.Min(columnNumber - 1, line.Length));

                // キャレットを移動してフォーカスを当て、画面をスクロール
                editor.Focus();
                editor.CaretOffset = offset;
                editor.ScrollToLine(lineNumber);

                // 該当行を選択状態にする（オプション）
                editor.Select(line.Offset, line.Length);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ジャンプ処理エラー: {ex.Message}");
            }
        }

        // フォルダを開く
        private async void MenuOpenFolder_Click(object sender, RoutedEventArgs e)
        {

            var openFolderDialog = new OpenFolderDialog
            {
                Title = "プロジェクトフォルダを開く"
            };

            if (openFolderDialog.ShowDialog() == true)
            {
                string folderPath = openFolderDialog.FolderName;
                _currentRootFolderPath = openFolderDialog.FolderName;
                _currentBveepFilePath = string.Empty; // 新規フォルダを開いたときはパスをリセット
                                                      // ★ここでファイル監視をスタートする
                SetupFileSystemWatcher(folderPath);
                // 1. 読み込み中ウィンドウを表示
                var loadingWindow = new LoadingWindow
                {
                    Owner = this // 親ウィンドウを指定して中央に表示させる
                };
                loadingWindow.Show();

                // 2. 親ウィンドウを操作不可（応答なし状態）にし、カーソルを待機状態にする
                this.IsEnabled = false;
                Mouse.OverrideCursor = Cursors.Wait;

                try
                {
                    // 3. 【重要】重い処理（ファイルスキャン）をバックグラウンドで実行
                    // これにより、UIスレッドが解放され、LoadingWindowが正常に描画されます。
                    FileSystemNode? rootNode = await ScanDirectoryAsync(folderPath);

                    // 4. UIの更新（TreeViewItemの生成）はUIスレッドで行う
                    LoadFileSystemToTreeView(rootNode);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"フォルダの読み込み中にエラーが発生しました。\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    // 5. 処理完了後（例外発生時含む）、必ず元に戻す
                    this.IsEnabled = true;
                    Mouse.OverrideCursor = null;
                    loadingWindow.Close();
                }
            }
        }

        // 新規作成
        private void MenuNew_Click(object sender, ExecutedRoutedEventArgs e)
        {
            if (isModified)
            {
                var result = MessageBox.Show(
                    "ファイルに変更が加えられています。保存しますか？",
                    "確認",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    // TODO: ここに既存のファイル保存処理を呼び出すコードを記述する
                    if (string.IsNullOrEmpty(currentFilePath))
                    {
                        // 新規ファイルの場合は「名前を付けて保存」へ誘導
                        SaveFileAs();
                    }
                    else
                    {
                        SaveFile(currentFilePath);
                    }

                    // 保存処理に失敗した、あるいは保存がキャンセルされた場合は終了を中止したいときはここで `e.Cancel = true;` にする
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    // 「キャンセル」が選ばれた場合は中断する
                    return;
                }
            }
            MapHighlighter.SetText(ActiveEditor ?? MapTextEditor, string.Empty);
            isModified = false;
            currentFilePath = null;
            Title = $"BVE Map Editor";
        }
        // 終了(_X)
        private void MenuExit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
        // ウィンドウタイトルと中央ヘッダーの表示更新
        private void UpdateTitle()
        {
            // アクティブタブの情報を優先してタイトルを構築
            var active = EditorTabControl != null && EditorTabControl.SelectedItem is TabItem ti ? global::BveMapEditor.MainWindow.GetTabInfo(ti) : null;
            string fileName;
            bool modified = false;
            if (active != null)
            {
                fileName = string.IsNullOrEmpty(active.FilePath) ? "無題" : Path.GetFileName(active.FilePath);
                modified = active.IsModified;
                // グローバル currentFilePath は互換性のため更新
                currentFilePath = active.FilePath;
            }
            else
            {
                fileName = string.IsNullOrEmpty(currentFilePath) ? "無題" : Path.GetFileName(currentFilePath);
                modified = isModified;
            }

            Title = $"BVE Map Editor - {fileName}{(modified ? " *" : "")}";
        }

        //情報
        private void MenuAbout_Click(object sender, RoutedEventArgs e)
        {
            var aboutWindow = new AboutWindow
            {
                Owner = this
            };
            aboutWindow.ShowDialog();
        }
    }
}