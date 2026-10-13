using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TextBox;

namespace BveMapEditor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string _copiedFilePath = string.Empty;
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

        // 未保存フラグ
        private bool isModified = false;

        // ファイル読み込み中フラグ（TextChangedの誤発火防止用）
        private bool isLoading = false;
        //エクスプローラペイン自動更新
        private FileSystemWatcher? _fileSystemWatcher;
        public MainWindow()
        {
            InitializeComponent();

            // ★ Undo/Redo の履歴状態が変化したときにメニューの有効/無効を更新するイベントを購読
            if (MapTextEditor.Document != null && MapTextEditor.Document.UndoStack != null)
            {
                MapTextEditor.Document.UndoStack.PropertyChanged += UndoStack_PropertyChanged;
            }
            // AvalonEdit のハイライト初期化
            MapHighlighter.InitializeHighlighting(MapTextEditor);
            // ★1. 起動時に設定をロードしてエディタに適用
            currentSettings = AppSettings.Load();
            ApplySettingsToEditor();
            ApplyWindowSettings(); // ★ ウィンドウ・ペイン設定の適用
            // AvalonEdit のハイライト初期化
            MapHighlighter.InitializeHighlighting(MapTextEditor);
            // ★ 補完用イベントの購読
            MapTextEditor.TextArea.TextEntered += MapTextEditor_TextEntered;
            MapTextEditor.TextArea.TextEntering += MapTextEditor_TextEntering;

            if (MapTextEditor.Document != null && MapTextEditor.Document.UndoStack != null)
            {
                MapTextEditor.Document.UndoStack.PropertyChanged += UndoStack_PropertyChanged;
            }
            UpdateEditMenuState();
            // AvalonEdit は標準コマンド（Cut, Copy, Paste）をサポートしていますが、
            // ウィンドウ全体で確実に機能させるためのバインディング設定例です
            // ★ AvalonEditの機能にコマンドを直接ルーティングする
            // ★ 選択範囲がないときは実行されない（あるいは何もしない）ように確実にガードする
            // ★ エディタの選択範囲が変わったときのイベントを購読
            MapTextEditor.TextArea.SelectionChanged += (s, e) =>
            {
                UpdateEditMenuState();
            };
            // ★ 現在行ハイライトレンダラーの組み込み
            var highlightRenderer = new CurrentLineHighlightRenderer(MapTextEditor.TextArea);
            MapTextEditor.TextArea.TextView.BackgroundRenderers.Add(highlightRenderer);

            // カーソル位置が変わったときに再描画を走らせてハイライトを追従させる
            MapTextEditor.TextArea.Caret.PositionChanged += (s, e) =>
            {
                MapTextEditor.TextArea.TextView.InvalidateVisual();
            };
        }
        // コマンドライン引数から渡された .bveep ファイルを起動時に読み込む用
        public async Task LoadBveepFileFromCommandLineAsync(string filePath)
        {
            // すでに実装した LoadBveepFileAsync を呼び出して復元する
            await LoadBveepFileAsync(filePath);
        }
        // 同じ文字が連続しているかチェックする（例: "ss", "ee" など）
        private bool HasConsecutiveCharacters(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;

            for (int i = 0; i < s.Length - 1; i++)
            {
                if (char.ToLower(s[i]) == char.ToLower(s[i + 1]))
                {
                    return true;
                }
            }
            return false;
        }
        private CompletionWindow? completionWindow;

        private string? GetSelectedPath()
        {
            if (FileExplorerTreeView.SelectedItem is TreeViewItem selectedItem && selectedItem.Tag is string path)
            {
                return path;
            }
            return null;
        }
        // ツリービューの項目を右クリックしたときに、自動でその項目を選択状態にする
        private void TreeViewItem_MouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is TreeViewItem item)
            {
                item.IsSelected = true;
                // フォーカスを当ててコンテキストメニューが正しく対象を認識できるようにする
                item.Focus();
                // イベントが親に伝播して意図しない選択解除が起きるのを防ぐ場合
                e.Handled = true;
            }
        }
        // コンテキストメニューが開かれる直前に、ペインが空かどうかをチェックして制御する
        private void FileExplorerTreeView_ContextMenuOpening(object sender, System.Windows.Controls.ContextMenuEventArgs e)
        {
            // エクスプローラーペインが空（またはルートフォルダが未設定）の場合
            if (FileExplorerTreeView.Items.Count == 0 || string.IsNullOrEmpty(_currentRootFolderPath))
            {
                // メニューの表示自体を完全にキャンセルする（これでメニューが出なくなります）
                e.Handled = true;
            }
            else
            {
                // 項目がある場合は通常通りメニューを許可
                ExplorerContextMenu.IsEnabled = true;
            }
        }
        private void MenuNewTextFile_Click(object sender, RoutedEventArgs e)
        {
            string? path = GetSelectedPath();
            if (string.IsNullOrEmpty(path)) return;

            // 選択されているのがファイルならその親フォルダ、フォルダならその中をターゲットにする
            string targetDir = Directory.Exists(path) ? path : (Path.GetDirectoryName(path) ?? _currentRootFolderPath);
            if (string.IsNullOrEmpty(targetDir)) return;

            // 重複しないファイル名を生成 (例: 新しいテキスト文書.txt, 新しいテキスト文書 (1).txt)
            string baseName = "新しいテキスト文書";
            string ext = ".txt";
            string newPath = Path.Combine(targetDir, baseName + ext);
            int count = 1;

            while (File.Exists(newPath))
            {
                newPath = Path.Combine(targetDir, $"{baseName} ({count}){ext}");
                count++;
            }

            try
            {
                File.WriteAllText(newPath, string.Empty);
                // FileSystemWatcher が有効なため、自動的にツリーが同期・更新されます
            }
            catch (Exception ex)
            {
                MessageBox.Show($"ファイルの作成に失敗しました:\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void ExplorerPaneMenuRename_Click(object sender, RoutedEventArgs e)
        {
            string? path = GetSelectedPath();
            if (string.IsNullOrEmpty(path) || path.Equals(_currentRootFolderPath, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("ルートフォルダや無効な項目は名前を変更できません。", "操作エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            bool isDir = Directory.Exists(path);
            string currentName = Path.GetFileName(path);
            string parentDir = Path.GetDirectoryName(path) ?? "";

            // 入力ボックスを表示
            string newName = Microsoft.VisualBasic.Interaction.InputBox("新しい名前を入力してください:", "名前の変更", currentName);
            if (string.IsNullOrWhiteSpace(newName) || newName == currentName) return;

            string newPath = Path.Combine(parentDir, newName);

            try
            {
                if (isDir)
                {
                    Directory.Move(path, newPath);
                }
                else
                {
                    File.Move(path, newPath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"名前の変更に失敗しました:\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void ExplorerPaneMenuCopy_Click(object sender, RoutedEventArgs e)
        {
            string? path = GetSelectedPath();
            if (!string.IsNullOrEmpty(path) && (File.Exists(path) || Directory.Exists(path)))
            {
                _copiedFilePath = path;
            }
        }

        private void ExplorerPaneMenuPaste_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_copiedFilePath) || (!File.Exists(_copiedFilePath) && !Directory.Exists(_copiedFilePath)))
            {
                MessageBox.Show("コピーされたファイルまたはフォルダが存在しません。", "エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string? targetPath = GetSelectedPath();
            if (string.IsNullOrEmpty(targetPath)) return;

            string targetDir = Directory.Exists(targetPath) ? targetPath : (Path.GetDirectoryName(targetPath) ?? _currentRootFolderPath);
            if (string.IsNullOrEmpty(targetDir)) return;

            try
            {
                string fileName = Path.GetFileName(_copiedFilePath);
                string destPath = Path.Combine(targetDir, fileName);

                // 同名ファイル・フォルダが存在する場合は枝番を付与
                int count = 1;
                string destDirOnly = Path.GetDirectoryName(destPath) ?? targetDir;
                string fileNameWithoutExt = Path.GetFileNameWithoutExtension(destPath);
                string ext = Path.GetExtension(destPath);

                while (File.Exists(destPath) || Directory.Exists(destPath))
                {
                    if (Directory.Exists(_copiedFilePath))
                    {
                        destPath = Path.Combine(destDirOnly, $"{fileName} ({count})");
                    }
                    else
                    {
                        destPath = Path.Combine(destDirOnly, $"{fileNameWithoutExt} ({count}){ext}");
                    }
                    count++;
                }

                if (Directory.Exists(_copiedFilePath))
                {
                    CopyDirectoryRecursive(_copiedFilePath, destPath);
                }
                else
                {
                    File.Copy(_copiedFilePath, destPath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"貼り付けに失敗しました:\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // フォルダを丸ごとコピーするための再帰ヘルパー
        private void CopyDirectoryRecursive(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                string destFile = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, destFile);
            }
            foreach (var subDir in Directory.GetDirectories(sourceDir))
            {
                string destSubDir = Path.Combine(destDir, Path.GetFileName(subDir));
                CopyDirectoryRecursive(subDir, destSubDir);
            }
        }


        // テキストが入力される直前
        private void MapTextEditor_TextEntering(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            if (e.Text.Length > 0 && completionWindow != null)
            {
                // ★ スペースキーが押されたときは、候補を勝手に確定させず、補完ウィンドウを閉じて通常のスペース入力を通す
                if (e.Text == " ")
                {
                    completionWindow.Close();
                    completionWindow = null;
                    return;
                }
                // アルファベット・数字・ピリオド以外の記号で確定させる
                if (!char.IsLetterOrDigit(e.Text[0]) && e.Text != ".")
                {
                    completionWindow.CompletionList.RequestInsertion(e);
                }
            }
        }        // テキストが入力された直後（補完ウィンドウの呼び出し）
                 // テキストが入力された直後
        private void MapTextEditor_TextEntered(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            // 1. 閉じ括弧の自動挿入処理（「AutoCompleteBrackets」設定でのみ制御する）
            if (currentSettings.AutoCompleteBrackets && (e.Text == "(" || e.Text == "["))
            {
                int caretOffset = MapTextEditor.CaretOffset;

                // 【安全装置】直前の文字をチェックし、連続または混合している場合は自動補完しない
                if (caretOffset > 1)
                {
                    char prevChar = MapTextEditor.Document.GetCharAt(caretOffset - 2); // 新しく入力された文字の直前の文字

                    // 直前の文字も括弧（'('、')'、'['、']'）である場合
                    if (prevChar == '(' || prevChar == ')' || prevChar == '[' || prevChar == ']')
                    {
                        goto SkipBracketInsertion; // 連続・混合している場合はスキップ
                    }
                }

                // 対応する閉じ括弧を決める
                char closeChar = (e.Text == "(") ? ')' : ']';

                // 閉じ括弧を自動挿入する
                MapTextEditor.Document.Insert(caretOffset, closeChar.ToString());

                // カーソルを元の位置（開き括弧と閉じ括弧の間）に戻す
                MapTextEditor.CaretOffset = caretOffset;
            }

        SkipBracketInsertion:

            // 2. 全体の自動補完機能（ポップアップ）が無効な場合はここで終了
            if (!currentSettings.EnableAutoCompletion)
            {
                return;
            }

            // ★ 数字、またはピリオドが入力された場合は補完を開かない（開いている場合は閉じる）
            if (e.Text.Length > 0 && (char.IsDigit(e.Text[0]) || e.Text == "."))
            {
                if (completionWindow != null)
                {
                    completionWindow.Close();
                    completionWindow = null;
                }
                return;
            }

            string currentWord = GetCurrentWordUnderCaret();

            // ★ すでに補完ウィンドウが開いている最中に、同じ文字が連続するようになったら閉じる
            if (completionWindow != null)
            {
                if (HasConsecutiveCharacters(currentWord))
                {
                    completionWindow.Close();
                    completionWindow = null;
                }
                return;
            }

            // '.' が入力された、またはアルファベットが入力されたタイミング
            if (e.Text.Length > 0 && char.IsLetter(e.Text[0]))
            {
                if (HasConsecutiveCharacters(currentWord) || HasConsecutiveCharacters(e.Text))
                {
                    return;
                }

                // 外部のプロバイダからマスターリストを取得する
                var allItems = BveSyntaxProvider.GetAllSyntaxes().ToArray();

                // プレフィックスに一致する候補だけに絞り込む（前方一致）
                var filteredItems = allItems;
                if (!string.IsNullOrEmpty(currentWord))
                {
                    filteredItems = allItems
                        .Where(item => item.Text.StartsWith(currentWord, StringComparison.OrdinalIgnoreCase))
                        .ToArray();
                }

                if (filteredItems.Length == 0 && e.Text != ".")
                {
                    return;
                }

                completionWindow = new CompletionWindow(MapTextEditor.TextArea);
                completionWindow.FontSize = MapTextEditor.FontSize;
                completionWindow.FontFamily = MapTextEditor.FontFamily;

                double estimatedWidth = Math.Max(250, MapTextEditor.FontSize * 18);
                completionWindow.Width = estimatedWidth;
                completionWindow.MaxHeight = 300;

                var data = completionWindow.CompletionList.CompletionData;
                foreach (var item in filteredItems)
                {
                    data.Add(item);
                }

                if (filteredItems.Length > 0)
                {
                    completionWindow.CompletionList.SelectedItem = filteredItems[0];
                }

                completionWindow.Show();

                completionWindow.Closed += (o, args) =>
                {
                    completionWindow = null;
                };
            }
        }
        // ★ キャレットの手前にある現在の単語（入力中の文字列）を取得するヘルパーメソッド
        private string GetCurrentWordUnderCaret()
        {
            var document = MapTextEditor.Document;
            int caretOffset = MapTextEditor.CaretOffset;
            int startOffset = caretOffset;

            // 前方に向かってアルファベットやドット、アンダースコアなどの文字が続く間、開始位置を遡る
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

            return document.GetText(startOffset, caretOffset - startOffset);
        }
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
        }
        // ★2. 設定をエディタに反映するヘルパーメソッド
        private void ApplySettingsToEditor()
        {
            if (MapTextEditor == null) return;

            try
            {
                MapTextEditor.FontFamily = new FontFamily(currentSettings.FontFamilyName);
                MapTextEditor.FontSize = currentSettings.FontSize;
                // ★折り返し設定を反映
                MapTextEditor.WordWrap = currentSettings.WordWrap;
                if (MenuWordWrap != null)
                {
                    MenuWordWrap.IsChecked = currentSettings.WordWrap;
                }
            }
            catch
            {
                // フォントが見つからない場合のフォールバック
                MapTextEditor.FontFamily = new FontFamily("Consolas");
                MapTextEditor.FontSize = 14.0;
                MapTextEditor.WordWrap = false;
            }
            // ★ メニューのチェック状態に設定を反映
            if (MenuAutoCompletion != null)
            {
                MenuAutoCompletion.IsChecked = currentSettings.EnableAutoCompletion;
            }
            // ★ テーマの適用
            ApplyTheme(currentSettings.Theme);
        }
        // ★ テーマメニューがクリックされたときの処理
        private void ThemeMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is string themeKey)
            {
                currentSettings.Theme = themeKey;
                ApplyTheme(themeKey);
                currentSettings.Save(); // 設定を保存
            }
        }
        private void MenuAutoCompletion_Click(object sender, RoutedEventArgs e)
        {
            if (MenuAutoCompletion != null)
            {
                currentSettings.EnableAutoCompletion = MenuAutoCompletion.IsChecked;
                currentSettings.Save(); // 設定変更時に即座に保存
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
            if (MapTextEditor != null && MapTextEditor.Document != null)
            {
                MenuUndo.IsEnabled = MapTextEditor.Document.UndoStack.CanUndo;
                MenuRedo.IsEnabled = MapTextEditor.Document.UndoStack.CanRedo;
            }
            else
            {
                MenuUndo.IsEnabled = false;
                MenuRedo.IsEnabled = false;
            }
            bool hasSelection = MapTextEditor?.SelectionLength > 0;

            if (MenuCut != null) MenuCut.IsEnabled = hasSelection;
            if (MenuCopy != null) MenuCopy.IsEnabled = hasSelection;

            // 貼り付けはクリップボードにテキストがある場合のみ有効にする
            if (MenuPaste != null) MenuPaste.IsEnabled = Clipboard.ContainsText();
        }
        // 切り取りメニューがクリックされたとき
        private void MenuCut_Click(object sender, RoutedEventArgs e)
        {
            if (MapTextEditor.SelectionLength > 0)
            {
                MapTextEditor.Cut();
            }
        }

        // コピーメニューがクリックされたとき
        private void MenuCopy_Click(object sender, RoutedEventArgs e)
        {
            if (MapTextEditor.SelectionLength > 0)
            {
                MapTextEditor.Copy();
            }
        }

        // 貼り付けメニューがクリックされたとき
        private void MenuPaste_Click(object sender, RoutedEventArgs e)
        {
            MapTextEditor.Paste();
        }
        // すべて選択メニューがクリックされたとき
        private void MenuSelectAll_Click(object sender, RoutedEventArgs e)
        {
            MapTextEditor.SelectAll();
        }
        private void MenuSearch_Click(object sender, RoutedEventArgs e)
        {
            var searchWindow = new SearchWindow(MapTextEditor, currentSettings)
            {
                Owner = this
            };
            searchWindow.Show();
        }
        // テキスト変更時のイベント
        private void MapTextEditor_TextChanged(object sender, EventArgs e)
        {
            if (isLoading) return;

            RunSyntaxCheck();
            UpdateEditMenuState(); // ★追加
            if (!isModified)
            {
                isModified = true;
                UpdateTitle();
            }
        }
        // 構文解析を実行してDataGridを更新
        private void RunSyntaxCheck()
        {
            if (MapTextEditor == null) return;

            // AvalonEdit から現在のテキストを取得
            string mapText = MapTextEditor.Text;
            string? mapFilePath = currentFilePath;

            // 1. ファイルパスが空、またはファイルが存在するか事前にチェックする
            if (string.IsNullOrEmpty(mapFilePath) || !File.Exists(mapFilePath))
            {
                var missingFileErrors = new List<ParseError>
        {
            new ParseError
            {
                Line = 1,
                Column = 1,
                Message = $"マップファイルが見つかりません: {mapFilePath ?? "(パス未設定)"}"
            }
        };
                ErrorListDataGrid.ItemsSource = missingFileErrors;
                return;
            }

            try
            {
                // 2. ファイルが存在する場合、基準ディレクトリを取得
                string baseDirectory = Path.GetDirectoryName(mapFilePath) ?? string.Empty;

                // 3. 基準ディレクトリを渡してパースを実行（これでファイル存在チェックも正しく行われます）
                List<ParseError> errors = MapParser.Parse(mapText, baseDirectory);

                // 4. DataGrid にエラーリストを反映
                ErrorListDataGrid.ItemsSource = null; // 一度クリアして確実に再描画
                ErrorListDataGrid.ItemsSource = errors;
            }
            catch (Exception ex)
            {
                // 例外発生時のキャッチ
                var exceptionErrors = new List<ParseError>
        {
            new ParseError
            {
                Line = 1,
                Column = 1,
                Message = $"ファイルの読み込み中にエラーが発生しました: {ex.Message}"
            }
        };
                ErrorListDataGrid.ItemsSource = exceptionErrors;
            }
        }
        // 構文エラー一覧（DataGrid）のダブルクリックイベント
        private void ErrorListDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (ErrorListDataGrid.SelectedItem is ParseError selectedError)
            {
                JumpToErrorPosition(selectedError.Line, selectedError.Column);
            }
        }
        // 指定された行・列へカーソルを安全にジャンプさせる処理
        private void JumpToErrorPosition(int lineNumber, int columnNumber)
        {
            try
            {
                if (lineNumber < 1) lineNumber = 1;
                if (columnNumber < 1) columnNumber = 1;

                // ドキュメントの総行数を超えないようにクランプ
                int totalLines = MapTextEditor.Document.LineCount;
                if (lineNumber > totalLines) lineNumber = totalLines;

                // AvalonEdit の行オブジェクトを取得
                DocumentLine line = MapTextEditor.Document.GetLineByNumber(lineNumber);

                // オフセット（文字位置）を計算
                int offset = line.Offset + Math.Max(0, Math.Min(columnNumber - 1, line.Length));

                // キャレットを移動してフォーカスを当て、画面をスクロール
                MapTextEditor.Focus();
                MapTextEditor.CaretOffset = offset;
                MapTextEditor.ScrollToLine(lineNumber);

                // 該当行を選択状態にする（オプション）
                MapTextEditor.Select(line.Offset, line.Length);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ジャンプ処理エラー: {ex.Message}");
            }
        }
        // 開く (Ctrl+O)
        private void MenuOpen_Click(object sender, ExecutedRoutedEventArgs e)
        {
            // 1. 未保存の変更（isModified）がある場合の警告
            if (isModified)
            {
                var result = MessageBox.Show(
                    "編集中の内容に変更が保存されていません。\n" +
                    "保存せずに新しいファイルを開きますか？",
                    "未保存の変更の確認 - BveMapEditor",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.No)
                {
                    return; // 「いいえ」が選ばれた場合は開く処理を中断
                }
            }
            // 2. ErrorListDataGrid にエラーが残っている場合の警告
            bool hasErrors = false;
            if (ErrorListDataGrid.ItemsSource is System.Collections.IEnumerable items)
            {
                foreach (var item in items)
                {
                    hasErrors = true;
                    break;
                }
            }

            if (hasErrors)
            {
                var result = MessageBox.Show(
                    "現在の画面（エラー一覧）にエラーやメッセージが残っています。\n" +
                    "別のファイルを開くと現在の編集内容やエラー情報は破棄されますが、よろしいですか？",
                    "エラー残存の確認 - BveMapEditor",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.No)
                {
                    return; // 「いいえ」が選ばれた場合は開く処理を中断
                }
            }
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Map File (*.txt)|*.txt|All Files (*.*)|*.*",
                Title = "マップファイルを開く"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                // ★ ユーザーが選択した実際のファイルパスを代入する
                string mapFilePath = openFileDialog.FileName;

                // ファイル存在チェック（実際のパスを使用）
                if (!File.Exists(mapFilePath))
                {
                    var errors = new List<ParseError>
        {
            new ParseError
            {
                Line = 1,
                Column = 1,
                Message = $"マップファイルが見つかりません: {mapFilePath}"
            }
        };
                    ErrorListDataGrid.ItemsSource = errors;
                    return;
                }
                try
                {
                    // 実際のファイルのテキストを読み込む
                    string mapText = File.ReadAllText(mapFilePath);

                    // ★ 実際のファイルが置かれているフォルダのパスを基準ディレクトリにする
                    string baseDirectory = Path.GetDirectoryName(mapFilePath) ?? string.Empty;

                    // パーサーに渡して実行
                    List<ParseError> errors = MapParser.Parse(mapText, baseDirectory);
                    ErrorListDataGrid.ItemsSource = errors;
                }
                catch (Exception ex)
                {
                    var errors = new List<ParseError>
        {
            new ParseError
            {
                Line = 1,
                Column = 1,
                Message = $"ファイルの読み込み中にエラーが発生しました: {ex.Message}"
            }
        };
                    ErrorListDataGrid.ItemsSource = errors;
                }
                OpenFile(openFileDialog.FileName);
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
        // ファイルシステムの構造を非同期でスキャンし、データモデルを返すメソッド
        private async Task<FileSystemNode?> ScanDirectoryAsync(string path)
        {
            // Task.Run でバックグラウンドスレッドで実行
            return await Task.Run(() =>
            {
                try
                {
                    var directoryInfo = new DirectoryInfo(path);
                    if (!directoryInfo.Exists) return null;

                    var rootNode = new FileSystemNode(directoryInfo.Name, directoryInfo.FullName, true);
                    PopulateNodeRecursive(rootNode, directoryInfo);
                    return rootNode;
                }
                catch (Exception)
                {
                    // エラー処理（必要に応じて）
                    return null;
                }
            });
        }
        // 再帰的にノード（データ）を構築するメソッド（バックグラウンドで実行）
        private void PopulateNodeRecursive(FileSystemNode parentNode, DirectoryInfo directoryInfo)
        {
            try
            {
                // サブフォルダを追加
                foreach (var directory in directoryInfo.GetDirectories())
                {
                    // 隠しフォルダやシステムフォルダは除外
                    if ((directory.Attributes & FileAttributes.Hidden) != 0) continue;

                    try
                    {
                        var subNode = new FileSystemNode(directory.Name, directory.FullName, true);
                        parentNode.Children.Add(subNode);
                        PopulateNodeRecursive(subNode, directory); // 再帰
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // アクセス拒否された場合はエラーメッセージ付きのノードを追加
                        // UIスレッドではないので、MessageBox.Show は使わない
                        parentNode.Children.Add(new FileSystemNode($"[アクセス拒否] {directory.Name}", directory.FullName, true));
                    }
                }

                // ファイルを追加
                foreach (var file in directoryInfo.GetFiles())
                {
                    if ((file.Attributes & FileAttributes.Hidden) != 0) continue;

                    try
                    {
                        var fileNode = new FileSystemNode(file.Name, file.FullName, false);
                        parentNode.Children.Add(fileNode);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // 個別のファイルへのアクセス権限がない場合
                        parentNode.Children.Add(new FileSystemNode($"[アクセス拒否] {file.Name}", file.FullName, false));
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                // ディレクトリ自体へのアクセス権限がない場合
                parentNode.Children.Add(new FileSystemNode($"[アクセス拒否] {directoryInfo.Name}", directoryInfo.FullName, true));
            }
        }

        // データモデル（FileSystemNode）からTreeViewを構築するメソッド（UIスレッドで実行）
        private void LoadFileSystemToTreeView(FileSystemNode? rootNode)
        {
            FileExplorerTreeView.Items.Clear();

            if (rootNode != null)
            {
                var rootItem = CreateTreeItemFromNode(rootNode);
                if (rootItem != null)
                {
                    FileExplorerTreeView.Items.Add(rootItem);
                    rootItem.IsExpanded = true;
                }
            }
        }

        // TreeViewItemをデータモデルから生成するメソッド（UIスレッドで実行）
        private TreeViewItem CreateTreeItemFromNode(FileSystemNode node)
        {
            var treeItem = new TreeViewItem
            {
                Tag = node.FullPath
            };
            // 1. アイコンとテキストを並べるための StackPanel を作成
            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 2, 0, 2)
            };

            // 2. アイコンを取得して Image コントロールにセット
            var image = new System.Windows.Controls.Image
            {
                Width = 16,
                Height = 16,
                Margin = new Thickness(0, 0, 5, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            // アクセス拒否項目や通常のパスからアイコンを取得
            string actualPath = node.FullPath;
            if (node.Name.StartsWith("[アクセス拒否]"))
            {
                // アクセス拒否の場合は実際のパスからアイコンを取れないことがあるのでプレースホルダー等に
                actualPath = node.FullPath;
            }
            ImageSource? iconSource = GetFileIcon(actualPath, node.IsDirectory);
            if (iconSource != null)
            {
                image.Source = iconSource;
            }
            stackPanel.Children.Add(image);
            // 3. テキスト（ファイル名・フォルダ名）を表示する TextBlock
            var textBlock = new TextBlock
            {
                Text = node.Name,
                VerticalAlignment = VerticalAlignment.Center
            };

            if (node.Name.StartsWith("[アクセス拒否]"))
            {
                textBlock.Foreground = Brushes.Red;
            }
            stackPanel.Children.Add(textBlock);

            // ヘッダーに StackPanel を設定
            treeItem.Header = stackPanel;
            // フォルダの場合は子ノードを再帰的に追加
            if (node.IsDirectory)
            {
                foreach (var childNode in node.Children)
                {
                    var childItem = CreateTreeItemFromNode(childNode);
                    if (childItem != null)
                    {
                        treeItem.Items.Add(childItem);
                    }
                }
            }

            return treeItem;
        }
        // ツリービューに指定したフォルダの内容を読み込む
        // ディレクトリ情報を元に TreeViewItem を再帰的に作成
        private TreeViewItem? CreateTreeItem(DirectoryInfo directoryInfo)
        {
            var directoryItem = new TreeViewItem
            {
                Header = $"📁 {directoryInfo.Name}",
                Tag = directoryInfo.FullName
            };

            try
            {
                // サブフォルダを追加
                foreach (var directory in directoryInfo.GetDirectories())
                {
                    // 隠しフォルダやシステムフォルダは除外
                    if ((directory.Attributes & FileAttributes.Hidden) != 0) continue;

                    try
                    {
                        // サブフォルダのツリーアイテムを作成して追加
                        var subItem = CreateTreeItem(directory);
                        if (subItem != null)
                        {
                            directoryItem.Items.Add(subItem);
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // 個別のサブフォルダへのアクセス権限がない場合
                        MessageBox.Show(
                            $"{directory.Name} へのアクセスが拒否されました。",
                            "アクセス権限エラー",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }
                }
                // ファイルを追加
                foreach (var file in directoryInfo.GetFiles())
                {
                    if ((file.Attributes & FileAttributes.Hidden) != 0) continue;

                    var fileItem = new TreeViewItem
                    {
                        Header = $"📄 {file.Name}",
                        Tag = file.FullName
                    };
                    directoryItem.Items.Add(fileItem);
                }
            }
            catch (UnauthorizedAccessException)
            {
                // ディレクトリ自体へのアクセス権限がない場合
                MessageBox.Show(
                    $"{directoryInfo.Name} へのアクセスが拒否されました。",
                    "アクセス権限エラー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return null;
            }


            return directoryItem;
        }

        // ツリービューの項目選択変更イベント
        private void FileExplorerTreeView_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // ダブルクリックされたアイテムを取得
            if (FileExplorerTreeView.SelectedItem is TreeViewItem selectedItem && selectedItem.Tag is string path)
            {
                if (File.Exists(path))
                {
                    // ファイルの場合はエディタで開く処理を実行
                    OpenFileInEditor(path);

                    // イベントが親にバブリングするのを防ぐ
                    e.Handled = true;
                }
            }
        }
        // ファイルを実際にエディタで開くメソッド（既存のファイルオープン処理をここにまとめる）
        private void OpenFileInEditor(string filePath)
        {
            try
            {
                // すでに実装されているファイル読み込み・エディタ表示処理をここに呼び出します
                // 例: LoadFileToEditor(filePath);
                OpenFile(filePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"ファイルを開けませんでした:\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void FileExplorerTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {

        }

        private void SaveBveepFile(string filePath)
        {
            try
            {
                var lines = new List<string>
        {
            "BVEEDITEXPLORER_PANEDATA_10", // 1行目: ヘッダーとバージョン
            $"CurrentDirectory = {_currentRootFolderPath}" // 2行目: カレントディレクトリ
        };

                // 3行目: 選択中のファイル
                string selectedPath = string.Empty;
                if (FileExplorerTreeView.SelectedItem is TreeViewItem selectedItem && selectedItem.Tag is string path)
                {
                    selectedPath = path;
                }
                lines.Add($"Selected = {selectedPath}");

                // 4行目以降: ツリーの状態（展開されているフォルダ、およびファイル）を再帰的に収集
                foreach (var item in FileExplorerTreeView.Items)
                {
                    if (item is TreeViewItem rootTreeItem)
                    {
                        CollectTreeState(rootTreeItem, _currentRootFolderPath, lines);
                    }
                }

                File.WriteAllLines(filePath, lines, Encoding.UTF8);
                _currentBveepFilePath = filePath;
                MessageBox.Show("エクスプローラーペインの設定を保存しました。", "保存完了", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (UnauthorizedAccessException ex)
            {
                MessageBox.Show($"アクセス拒否されました。\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (IOException ex)
            {
                MessageBox.Show($"保存中にI/Oエラーが発生しました。\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存中に予期しないエラーが発生しました。\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ツリーを再帰的に走査して Folder と File を収集するヘルパー
        private void CollectTreeState(TreeViewItem treeItem, string rootPath, List<string> lines)
        {
            if (treeItem.Tag is not string fullPath) return;
            if (fullPath.Equals(rootPath, StringComparison.OrdinalIgnoreCase))
            {
                // ルート自体の場合は子を走査
                foreach (var subItem in treeItem.Items)
                {
                    if (subItem is TreeViewItem child) CollectTreeState(child, rootPath, lines);
                }
                return;
            }

            // 相対パスを計算
            string relativePath = Path.GetRelativePath(rootPath, fullPath);

            if (Directory.Exists(fullPath))
            {
                // フォルダの場合
                lines.Add($"Folder = {relativePath}");

                // 展開されている場合のみ、子要素も再帰的に記録
                if (treeItem.IsExpanded)
                {
                    foreach (var subItem in treeItem.Items)
                    {
                        if (subItem is TreeViewItem child) CollectTreeState(child, rootPath, lines);
                    }
                }
            }
            else if (File.Exists(fullPath))
            {
                // ファイルの場合
                lines.Add($"File = {relativePath}");
            }
        }


        // メニュー：「名前を付けて保存」
        private void MenuSaveAsBveep_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Title = "エクスプローラーペイン設定を名前を付けて保存",
                Filter = "BVE Explorer Pane (*.bveep)|*.bveep|All Files (*.*)|*.*",
                DefaultExt = "bveep"
            };

            if (dialog.ShowDialog() == true)
            {
                SaveBveepFile(dialog.FileName);
            }
        }

        // メニュー：「開く (.bveep)」
        private async void MenuOpenBveep_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "エクスプローラーペイン設定を開く",
                Filter = "BVE Explorer Pane (*.bveep)|*.bveep|All Files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                await LoadBveepFileAsync(dialog.FileName);
            }
        }

        private async Task LoadBveepFileAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    MessageBox.Show("指定されたファイルが見つかりません。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string[] lines = await File.ReadAllLinesAsync(filePath, Encoding.UTF8);

                // 1. ヘッダーバリデーション (1行目)
                if (lines.Length == 0 || lines[0].Trim() != "BVEEDITEXPLORER_PANEDATA_10")
                {
                    MessageBox.Show("これは有効なエクスプローラーペイン設定ファイルではありません。", "形式エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    FileExplorerTreeView.Items.Clear();
                    return;
                }

                // 2. カレントディレクトリの読み込みとバリデーション (2行目)
                string currentDir = string.Empty;
                if (lines.Length > 1 && lines[1].StartsWith("CurrentDirectory ="))
                {
                    currentDir = lines[1]["CurrentDirectory =".Length..].Trim();
                }

                if (string.IsNullOrEmpty(currentDir) || !Directory.Exists(currentDir))
                {
                    MessageBox.Show("カレントディレクトリが存在しないか、指定されていません。", "アクセス/存在エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    FileExplorerTreeView.Items.Clear();
                    _currentRootFolderPath = string.Empty;
                    // ★ここでファイル監視をスタートする
                    SetupFileSystemWatcher(_currentRootFolderPath);
                    return;
                }

                // アクセス権限チェック
                try
                {
                    Directory.GetDirectories(currentDir);
                }
                catch (UnauthorizedAccessException)
                {
                    MessageBox.Show("カレントディレクトリへのアクセスが拒否されました。", "アクセス拒否", MessageBoxButton.OK, MessageBoxImage.Error);
                    FileExplorerTreeView.Items.Clear();
                    _currentRootFolderPath = string.Empty;
                    return;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"カレントディレクトリの確認中にエラーが発生しました。\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    FileExplorerTreeView.Items.Clear();
                    _currentRootFolderPath = string.Empty;
                    return;
                }
                _currentRootFolderPath = currentDir;
                _currentBveepFilePath = filePath;

                // 3. 選択状態の読み込み (3行目)
                string selectedPath = string.Empty;
                if (lines.Length > 2 && lines[2].StartsWith("Selected ="))
                {
                    selectedPath = lines[2]["Selected =".Length..].Trim();
                }

                // 4. 4行目以降の解析（重複チェック、ソート、存在チェック）
                var rawEntries = new List<string>();
                for (int i = 3; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (!string.IsNullOrEmpty(line))
                    {
                        rawEntries.Add(line);
                    }
                }

                // 4行目以降がない場合のリカバリー
                if (rawEntries.Count == 0)
                {
                    MessageBox.Show("設定ファイルのツリー情報が見つからないため、カレントディレクトリの標準読み込みを行います。", "情報", MessageBoxButton.OK, MessageBoxImage.Information);
                    // 通常のフォルダ読み込みを実行
                    FileSystemNode? defaultRoot = await ScanDirectoryAsync(_currentRootFolderPath);
                    if (defaultRoot != null)
                    {
                        LoadFileSystemToTreeView(defaultRoot);
                    }
                    return;
                }

                // 重複チェック
                if (rawEntries.GroupBy(x => x).Any(g => g.Count() > 1))
                {
                    MessageBox.Show("設定ファイル内に重複したディレクトリまたはファイルのエントリが存在するため、読み込みを中止します。", "重複エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    FileExplorerTreeView.Items.Clear();
                    return;
                }

                // 大量のエントリ（ディレクトリ/ファイル）が存在する場合の警告 (例: 500件以上)
                if (rawEntries.Count > 500)
                {
                    MessageBox.Show("設定ファイルに多数のディレクトリ/ファイルエントリが含まれています。読み込みに時間がかかる場合があります。", "確認", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                // ランダム順の可能性があるためアルファベット順にソート
                rawEntries.Sort(StringComparer.OrdinalIgnoreCase);

                // ツリー構造を再構築するため、データを元にスキャン＆復元
                FileSystemNode? rootNode = await ScanDirectoryAsync(_currentRootFolderPath);
                if (rootNode != null)
                {
                    // 有効なエントリのみを元にフィルタリング・復元する処理
                    LoadFileSystemToTreeView(rootNode);

                    // 選択状態の復元（存在しない場合は警告なしで解除）
                    if (!string.IsNullOrEmpty(selectedPath))
                    {
                        // 必要に応じて絶対パスに補正
                        if (!Path.IsPathRooted(selectedPath))
                        {
                            selectedPath = Path.Combine(_currentRootFolderPath, selectedPath);
                        }

                        if (File.Exists(selectedPath))
                        {
                            // TreeViewItemから該当ファイルを選択状態にする処理をここに実装
                            SelectTreeViewItemByPath(FileExplorerTreeView, selectedPath);
                        }
                    }
                }
            }
            catch (OutOfMemoryException ex)
            {
                MessageBox.Show($"メモリが足りません。アプリケーションを終了します。\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(1677216);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"設定ファイルの読み込み中にエラーが発生しました。\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                FileExplorerTreeView.Items.Clear();
            }
        }

        // パスを指定してTreeViewの該当アイテムを選択状態にするヘルパー
        private bool SelectTreeViewItemByPath(ItemsControl container, string path)
        {
            foreach (var item in container.Items)
            {
                if (item is TreeViewItem treeItem && treeItem.Tag is string tagPath)
                {
                    if (tagPath.Equals(path, StringComparison.OrdinalIgnoreCase))
                    {
                        treeItem.IsSelected = true;
                        treeItem.BringIntoView();
                        return true;
                    }

                    // 子階層も探索
                    if (SelectTreeViewItemByPath(treeItem, path))
                    {
                        treeItem.IsExpanded = true;
                        return true;
                    }
                }
            }
            return false;
        }
        private void SetupFileSystemWatcher(string rootPath)
        {
            // 既存の監視があれば停止・破棄
            if (_fileSystemWatcher != null)
            {
                _fileSystemWatcher.EnableRaisingEvents = false;
                _fileSystemWatcher.Dispose();
                _fileSystemWatcher = null;
            }

            if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath)) return;

            _fileSystemWatcher = new FileSystemWatcher(rootPath)
            {
                IncludeSubdirectories = true, // サブフォルダも含めてすべて監視
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite
            };

            // イベントハンドラの登録
            _fileSystemWatcher.Created += OnFileSystemChanged;
            _fileSystemWatcher.Deleted += OnFileSystemChanged;
            _fileSystemWatcher.Renamed += OnFileSystemChanged;

            // 監視を開始
            _fileSystemWatcher.EnableRaisingEvents = true;
        }

        // ファイル変更検知時の処理（バックグラウンドスレッドから呼ばれるためDispatcherを使用）
        private void OnFileSystemChanged(object sender, FileSystemEventArgs e)
        {
            // 連続した変更イベントが短時間に多発するのを防ぐ簡易的なディレイ（オプション）や
            // UIスレッドへの安全なディスパッチ
            Dispatcher.Invoke(async () =>
            {
                // 変更を検知したらツリーを再読み込みする
                if (!string.IsNullOrEmpty(_currentRootFolderPath) && Directory.Exists(_currentRootFolderPath))
                {
                    // 現在の展開状態や選択状態を完全に保持したい場合は高度なマージが必要ですが、
                    // まずは安全にツリー全体を再スキャンして同期させます
                    FileSystemNode? rootNode = await ScanDirectoryAsync(_currentRootFolderPath);
                    if (rootNode != null)
                    {
                        LoadFileSystemToTreeView(rootNode);
                    }
                }
            });
        }







        // メニュー：「上書き保存」
        private void MenuSaveBveep_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentBveepFilePath))
            {
                MenuSaveAsBveep_Click(sender, e);
            }
            else
            {
                SaveBveepFile(_currentBveepFilePath);
            }
        }
        // 指定されたパスのファイルを開く実体処理
        private void OpenFile(string filePath)
        {
            try
            {
                // ファイルの全テキストを読み込む
                string fileContent = File.ReadAllText(filePath);

                // ★ ヘッダー（例: "BveTs Map"）が含まれているかチェックする
                // 大文字小文字を区別しないように、あるいは先頭部分を重点的に探します
                bool hasValidHeader = fileContent.Contains("BveTs Map", StringComparison.OrdinalIgnoreCase);

                if (!hasValidHeader)
                {
                    var result = MessageBox.Show(
                        "このテキストファイルはMapファイルではありませんが、そのまま続けますか？",
                        "ファイル形式の確認",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result == MessageBoxResult.No)
                    {
                        // 「いいえ」が選ばれた場合は読み込みを中止する
                        return;
                    }
                }
                // 1. ファイルサイズのチェック（例: 1MB = 1,048,576 バイトを超える場合）
                var fileInfo = new FileInfo(filePath);
                long fileSizeInBytes = fileInfo.Length;
                // 1. ファイルサイズが大きすぎる場合（例: 30MB超）の事前警告
                const long warningThreshold = 30 * 1024 * 1024; // 30MB
                if (fileSizeInBytes > warningThreshold)
                {
                    double fileSizeInMb = (double)fileSizeInBytes / (1024 * 1024);
                    var result = MessageBox.Show(
                        $"選択されたファイルは非常に大きいです（約 {fileSizeInMb:F1} MB）。\n" +
                        "読み込みに時間がかかり、お使いのPCのメモリを多く消費する可能性があります。\n\n" +
                        "本当にこのファイルを開きますか？",
                        "メモリ使用量の警告 - BveMapEditor",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result == MessageBoxResult.No)
                    {
                        return; // ユーザーがキャンセルした場合
                    }
                }

                isLoading = true;

                // 2. ファイル読み込み時のメモリ不足（OutOfMemoryException）をキャッチ
                string content;
                try
                {
                    content = File.ReadAllText(filePath);
                }
                catch (OutOfMemoryException)
                {
                    MessageBox.Show(
                        "メモリが不足しているため、ファイルを読み込めませんでした。\n" +
                        "他のアプリケーションを終了するか、よりメモリに余裕のある環境でお試しください。",
                        "メモリ不足エラー",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                // 3. エディタ（AvalonEdit）へのデータ展開時におけるメモリ不足の保護
                try
                {
                    MapHighlighter.SetText(MapTextEditor, content);
                }
                catch (OutOfMemoryException)
                {
                    MessageBox.Show(
                        "テキストをエディタへ展開する際にメモリが不足しました。\n" +
                        "処理を中止します。",
                        "メモリ不足エラー",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    // 安全のためエディタを空にする
                    MapHighlighter.SetText(MapTextEditor, string.Empty);
                    return;
                }
                currentFilePath = filePath;
                isModified = false;
                UpdateTitle();

                // 読み込み完了後の Undo履歴クリア & 構文チェック
                MapTextEditor.Document.UndoStack.ClearAll();
                UpdateEditMenuState();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"ファイルの読み込みに失敗しました:\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                isLoading = false;
                UpdateTitle();
                RunSyntaxCheck();
                UpdateEditMenuState(); // ★追加
            }
        }
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
            MapHighlighter.SetText(MapTextEditor, string.Empty);
            isModified = false;
            currentFilePath = null;
            Title = $"BVE Map Editor";
        }
        // 上書き保存 (Ctrl+S)
        private void MenuSave_Click(object sender, ExecutedRoutedEventArgs e)
        {
            // 1. 保存前に現在のテキストで構文チェックを走らせ、最新のエラー一覧を取得する
            string mapText = MapTextEditor != null ? MapTextEditor.Text : string.Empty;
            var errors = MapParser.Parse(mapText); // ※既存のパーサー呼び出しに合わせて調整してください

            if (ErrorListDataGrid.ItemsSource is System.Collections.ICollection errorCollection && errorCollection.Count > 0)
            {
                var result = MessageBox.Show(
                    $"構文エラーが {errorCollection.Count} 件存在します。このまま保存しますか？",
                    "警告", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result == MessageBoxResult.No) return;
            }

            if (string.IsNullOrEmpty(currentFilePath))
            {
                // 新規ファイルの場合は「名前を付けて保存」へ誘導
                SaveFileAs();
            }
            else
            {
                SaveFile(currentFilePath);
            }
        }
        // 名前を付けて保存 (Ctrl+Shift+S 等)
        private void MenuSaveAs_Click(object sender, ExecutedRoutedEventArgs e)
        {
            SaveFileAs();
        }

        // 名前を付けて保存の実体処理
        private void SaveFileAs()
        {
            // 1. 保存前に現在のテキストで構文チェックを走らせ、最新のエラー一覧を取得する
            string mapText = MapTextEditor != null ? MapTextEditor.Text : string.Empty;
            var errors = MapParser.Parse(mapText); // ※既存のパーサー呼び出しに合わせて調整してください

            if (ErrorListDataGrid.ItemsSource is System.Collections.ICollection errorCollection && errorCollection.Count > 0)
            {
                var result = MessageBox.Show(
                    $"構文エラーが {errorCollection.Count} 件存在します。このまま保存しますか？",
                    "警告", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result == MessageBoxResult.No) return;
            }

            var saveFileDialog = new SaveFileDialog
            {
                Filter = "Map File (*.txt)|*.txt|All Files (*.*)|*.*",
                Title = "名前を付けて保存",
                DefaultExt = ".txt"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                SaveFile(saveFileDialog.FileName);
            }
        }
        // ファイル書き込み処理
        private void SaveFile(string filePath)
        {

            try
            {
                // AvalonEdit からテキストを取得
                string content = MapHighlighter.GetText(MapTextEditor);

                // UTF-8 (BOMなし、または必要に応じてBOM付き) でファイルに書き込み
                File.WriteAllText(filePath, content, new UTF8Encoding(false));

                currentFilePath = filePath;
                isModified = false;
                UpdateTitle();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"ファイルの保存に失敗しました:\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        // 終了(_X)
        private void MenuExit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
        // 「右端で折り返す」メニューのクリック処理
        private void MenuWordWrap_Click(object sender, RoutedEventArgs e)
        {
            if (MapTextEditor == null) return;

            // メニューのチェック状態をエディタに反映
            bool isWrapped = MenuWordWrap.IsChecked;
            MapTextEditor.WordWrap = isWrapped;

            // 設定を更新して保存
            currentSettings.WordWrap = isWrapped;
            currentSettings.Save();
        }
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (MapTextEditor != null)
            {
                currentSettings.FontFamilyName = MapTextEditor.FontFamily.Source;
                currentSettings.FontSize = MapTextEditor.FontSize;
                currentSettings.WordWrap = MapTextEditor.WordWrap; // ★追加
                // ウィンドウが通常状態（Normal）のときのみ幅・高さを保存（最大化中の値を保存しないようにする配慮）
                if (this.WindowState == WindowState.Normal)
                {
                    currentSettings.WindowWidth = this.Width;
                    currentSettings.WindowHeight = this.Height;
                    currentSettings.IsMaximized = false;
                }
                else if (this.WindowState == WindowState.Maximized)
                {
                    currentSettings.IsMaximized = true;
                }

                // エクスプローラーペインの幅を保存
                if (ExplorerColumn != null)
                {
                    currentSettings.ExplorerPaneWidth = ExplorerColumn.ActualWidth;
                }
                currentSettings.Save();
            }
            base.OnClosing(e);
        }
        private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // ★ 追加：開いたままの子ウィンドウ（SearchWindowなど）がないかチェックする
            foreach (Window window in this.OwnedWindows)
            {
                if (window is SearchWindow)
                {
                    MessageBox.Show(this, "別のウィンドウを閉じられていません。別ウィンドウを閉じてからやり直してください。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);

                    // 必要であれば、開きっぱなしのウィンドウを手前にアクティブにする
                    window.Activate();

                    e.Cancel = true; // 終了をキャンセル
                    return;
                }
            }
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
                    // 「キャンセル」が選ばれた場合は終了を中断する
                    e.Cancel = true;
                }
                // 「いいえ」が選ばれた場合はそのまま終了する
                // ★ アプリケーション終了時に検索・置換の保持データを空にする
            }
            _fileSystemWatcher?.Dispose();
        }
        // ウィンドウタイトルと中央ヘッダーの表示更新
        private void UpdateTitle()
        {
            string fileName = string.IsNullOrEmpty(currentFilePath) ? "無題" : Path.GetFileName(currentFilePath);
            Title = $"BVE Map Editor - {fileName}{(isModified ? " *" : "")}";
        }

        // 「変更を戻す」メニューのクリック処理
        private void MenuUndo_Click(object sender, RoutedEventArgs e)
        {
            if (MapTextEditor != null && MapTextEditor.Document.UndoStack.CanUndo)
            {
                MapTextEditor.Undo();
            }
        }

        // 「やり直す」メニューのクリック処理
        private void MenuRedo_Click(object sender, RoutedEventArgs e)
        {
            if (MapTextEditor != null && MapTextEditor.Document.UndoStack.CanRedo)
            {
                MapTextEditor.Redo();
            }
        }
        // 「設定 -> フォント設定...」のクリック処理
        private void MenuFontSettings_Click(object sender, RoutedEventArgs e)
        {
            if (MapTextEditor == null) return;

            var currentFamily = MapTextEditor.FontFamily;
            var currentSize = MapTextEditor.FontSize;

            var dialog = new FontSettingsDialog(currentFamily, currentSize)
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true)
            {
                // ★ null でない場合のみ適用と保存を行う
                if (dialog.SelectedFontFamily != null)
                {
                    MapTextEditor.FontFamily = dialog.SelectedFontFamily;
                    currentSettings.FontFamilyName = dialog.SelectedFontFamily.Source;
                }

                MapTextEditor.FontSize = dialog.SelectedFontSize;
                currentSettings.FontSize = dialog.SelectedFontSize;

                // 設定を保存
                currentSettings.Save();
            }
        }
        // Ctrl + マウスホイールによるフォントサイズの拡大・縮小
        private void MapTextEditor_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            // Ctrlキーが押されているか確認
            if (System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control)
            {
                if (MapTextEditor == null) return;

                double currentSize = MapTextEditor.FontSize;

                if (e.Delta > 0)
                {
                    // 上方向（拡大）: 最大 72 まで
                    if (currentSize < 72)
                    {
                        currentSize += 1;
                    }
                }
                else
                {
                    // 下方向（縮小）: 最小 6 まで
                    if (currentSize > 6)
                    {
                        currentSize -= 1;
                    }
                }

                // エディタのフォントサイズを更新
                MapTextEditor.FontSize = currentSize;

                // 設定オブジェクト側も更新して保存
                currentSettings.FontSize = currentSize;
                currentSettings.Save();

                // イベントの伝播を止めてデフォルトのスクロール動作を抑制する
                e.Handled = true;
            }
        }
        // OSがダークモードかどうかを判定するヘルパー
        private bool IsSystemDarkMode()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                if (key != null)
                {
                    object? value = key.GetValue("AppsUseLightTheme");
                    if (value is int intValue)
                    {
                        return intValue == 0; // 0 ならダークモード
                    }
                }
            }
            catch
            {
                // 取得失敗時はライトとみなす
            }
            return false;
        }

        // テーマを適用するメインメソッド
        private void ApplyTheme(string themeMode)
        {
            bool isDark = themeMode switch
            {
                "Dark" => true,
                "Light" => false,
                _ /* "System" */ => IsSystemDarkMode()
            };

            if (isDark)
            {
                // ダークテーマの色設定
                MapTextEditor.Background = new SolidColorBrush(Color.FromRgb(30, 30, 30));
                MapTextEditor.Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 220));
                // TextArea の文字色なども必要に応じて調整可能
            }
            else
            {
                // ライトテーマの色設定
                MapTextEditor.Background = Brushes.White;
                MapTextEditor.Foreground = Brushes.Black;
            }

            // メニューのチェック状態を同期
            if (MenuThemeSystem != null) MenuThemeSystem.IsChecked = (themeMode == "System");
            if (MenuThemeLight != null) MenuThemeLight.IsChecked = (themeMode == "Light");
            if (MenuThemeDark != null) MenuThemeDark.IsChecked = (themeMode == "Dark");
        }
        private void MenuDetailSettings_Click(object sender, RoutedEventArgs e)
        {
            var detailWindow = new DetailSettingsWindow(currentSettings)
            {
                Owner = this
            };

            if (detailWindow.ShowDialog() == true)
            {
                // 設定が変更されたら必要に応じてファイルを保存
                currentSettings.Save();
            }
        }
        private void MenuAbout_Click(object sender, RoutedEventArgs e)
        {
            var aboutWindow = new AboutWindow
            {
                Owner = this
            };
            aboutWindow.ShowDialog();
        }
        // BVE公式サイトを開く
        private void MenuBveSite_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // BVE Trainsim の公式サイトURL
                string url = "https://bvets.net/jp/";

                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"ブラウザを開けませんでした: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void MenuBveSiteGuide_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // BVE Trainsim の公式サイトURL
                string url = "https://bvets.net/jp/edit/";

                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"ブラウザを開けませんでした: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }

        }

        private void MenuBveformat_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // BVE Trainsim の公式サイトURL
                string url = "https://bvets.net/jp/edit/formats/";

                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"ブラウザを開けませんでした: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }


        }

        private async void MenuRefreshTree_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_currentRootFolderPath) && Directory.Exists(_currentRootFolderPath))
            {
                FileSystemNode? rootNode = await ScanDirectoryAsync(_currentRootFolderPath);
                if (rootNode != null)
                {
                    LoadFileSystemToTreeView(rootNode);
                }
            }
        }

        private void MenuOpenInExplorer_Click(object sender, RoutedEventArgs e)
        {
            if (FileExplorerTreeView.SelectedItem is TreeViewItem selectedItem && selectedItem.Tag is string path)
            {
                try
                {
                    string targetPath = path;
                    // ファイルの場合はその親フォルダ、フォルダの場合はそのフォルダを開く
                    if (File.Exists(path))
                    {
                        targetPath = Path.GetDirectoryName(path) ?? path;
                    }

                    if (Directory.Exists(targetPath))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = targetPath,
                            UseShellExecute = true
                        });
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"エクスプローラーを開けませんでした: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        // 「エクスプローラーペインを空にする」メニュークリック
        private void MenuClearExplorerPane_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 1. ツリービューの中身をすべてクリア
                FileExplorerTreeView.Items.Clear();

                // 2. 保持しているパスや状態をリセット
                _currentRootFolderPath = string.Empty;
                _currentBveepFilePath = string.Empty;

                // 3. 動作中のファイル監視（FileSystemWatcher）を停止・破棄する
                if (_fileSystemWatcher != null)
                {
                    _fileSystemWatcher.EnableRaisingEvents = false;
                    _fileSystemWatcher.Dispose();
                    _fileSystemWatcher = null;
                }
            }
            catch (Exception ex)
            {
                // 失敗したときにメッセージボックスで通知する
                MessageBox.Show($"エクスプローラーペインを空にできませんでした。\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        // デバッグ実行メニューの処理
        private void MenuDebugRun_Click(object sender, RoutedEventArgs e)
        {
            string? bvePath = currentSettings.BveExecutablePath;

            // パスが未設定、またはファイルが存在しない場合
            if (string.IsNullOrWhiteSpace(bvePath) || !File.Exists(bvePath))
            {
                MessageBox.Show(this, "有効なBVE実行ファイルのパスが設定されていません。\n次の画面で実行ファイル（.exe）を指定してください。", "ファイル指定", MessageBoxButton.OK, MessageBoxImage.Information);

                bvePath = PromptForBveExecutable();
                if (string.IsNullOrEmpty(bvePath))
                {
                    // ユーザーが選択ダイアログをキャンセルしたことを通知して処理を中止
                    MessageBox.Show(this, "BVE実行ファイルの選択はキャンセルされました。デバッグ実行を中止します。", "キャンセル", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            // 起動試行
            bool success = TryLaunchBve(bvePath);

            if (success)
            {
                // 起動が成功した場合、もし設定されているパスと異なる（＝新規選択された）なら保存するか尋ねる
                if (currentSettings.BveExecutablePath != bvePath)
                {
                    var result = MessageBox.Show(this,
                        $"BVEの起動に成功しました。\nこの実行ファイルのパスを今後のために設定（保存）しますか？\n\nパス: {bvePath}",
                        "パスの保存",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        currentSettings.BveExecutablePath = bvePath;
                        currentSettings.Save();
                    }
                }
            }
            else
            {
                // 起動に失敗した場合のフォールバック（オープンダイアログで再指定させる）
                MessageBox.Show(this, "BVEの起動に失敗しました。正しい実行ファイルを指定し直してください。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);

                string? newPath = PromptForBveExecutable();
                if (!string.IsNullOrEmpty(newPath))
                {
                    if (TryLaunchBve(newPath))
                    {
                        var result = MessageBox.Show(this, "起動に成功しました。このパスを保存しますか？", "パスの保存", MessageBoxButton.YesNo, MessageBoxImage.Question);
                        if (result == MessageBoxResult.Yes)
                        {
                            currentSettings.BveExecutablePath = newPath;
                            currentSettings.Save();
                        }
                    }
                }
            }
        }

        // オープンダイアログでBVEの実行ファイルを尋ねるヘルパー
        private string? PromptForBveExecutable()
        {
            var openDlg = new OpenFileDialog
            {
                Title = "Bve5 / Bve6 実行ファイルの選択",
                Filter = "Executable Files (*.exe)|*.exe|All Files (*.*)|*.*"
            };

            return openDlg.ShowDialog() == true ? openDlg.FileName : null;
        }

        // 実際にプロセスを起動するヘルパー
        private bool TryLaunchBve(string path)
        {
            try
            {
                var startInfo = new ProcessStartInfo(path)
                {
                    UseShellExecute = true
                };
                Process.Start(startInfo);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void MenuDebugRunQMV_Click(object sender, RoutedEventArgs e)
        {
            string? QMVPath = currentSettings.QuickMapViewerExecutablePath;

            // パスが未設定、またはファイルが存在しない場合
            if (string.IsNullOrWhiteSpace(QMVPath) || !File.Exists(QMVPath))
            {
                MessageBox.Show(this, "有効なQuickMapViewer実行ファイルのパスが設定されていません。\n次の画面で実行ファイル（.exe）を指定してください。", "ファイル指定", MessageBoxButton.OK, MessageBoxImage.Information);

                QMVPath = PromptForQMVExecutable();
                if (string.IsNullOrEmpty(QMVPath))
                {
                    // ユーザーが選択ダイアログをキャンセルしたことを通知して処理を中止
                    MessageBox.Show(this, "QuickMapViewer実行ファイルの選択はキャンセルされました。実行を中止します。", "キャンセル", MessageBoxButton.OK, MessageBoxImage.Error);
                    return; // ユーザーがキャンセルした場合
                }
            }

            // 起動試行
            bool success = TryLaunchQMV(QMVPath);

            if (success)
            {
                // 起動が成功した場合、もし設定されているパスと異なる（＝新規選択された）なら保存するか尋ねる
                if (currentSettings.BveExecutablePath != QMVPath)
                {
                    var result = MessageBox.Show(this,
                        $"QuickMapViewerの起動に成功しました。\nこの実行ファイルのパスを今後のために設定（保存）しますか？\n\nパス: {QMVPath}",
                        "パスの保存",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        currentSettings.QuickMapViewerExecutablePath = QMVPath;
                        currentSettings.Save();
                    }
                }
            }
            else
            {
                // 起動に失敗した場合のフォールバック（オープンダイアログで再指定させる）
                MessageBox.Show(this, "QuickMapViewerの起動に失敗しました。正しい実行ファイルを指定し直してください。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);

                string? newPath = PromptForQMVExecutable();
                if (!string.IsNullOrEmpty(newPath))
                {
                    if (TryLaunchQMV(newPath))
                    {
                        var result = MessageBox.Show(this, "起動に成功しました。このパスを保存しますか？", "パスの保存", MessageBoxButton.YesNo, MessageBoxImage.Question);
                        if (result == MessageBoxResult.Yes)
                        {
                            currentSettings.QuickMapViewerExecutablePath = newPath;
                            currentSettings.Save();
                        }
                    }
                }
            }

        }
        // オープンダイアログでBVEの実行ファイルを尋ねるヘルパー
        private string? PromptForQMVExecutable()
        {
            var openDlg = new OpenFileDialog
            {
                Title = "QuickMapViewer 実行ファイルの選択",
                Filter = "Executable Files (*.exe)|*.exe|All Files (*.*)|*.*"
            };

            return openDlg.ShowDialog() == true ? openDlg.FileName : null;
        }

        // 実際にプロセスを起動するヘルパー
        private bool TryLaunchQMV(string path)
        {
            try
            {
                var startInfo = new ProcessStartInfo(path)
                {
                    UseShellExecute = true
                };
                Process.Start(startInfo);
                return true;
            }
            catch
            {
                return false;
            }
        }

    }
}