using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using Microsoft.Win32;
using System;
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

namespace BveMapEditor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
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
            // ★ 自動補完が無効な場合は何もしない
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
                // ★ 外部のプロバイダからマスターリストを取得する
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
                // ★ エディタの現在のフォントサイズを自動補完ウィンドウにも反映する
                completionWindow.FontSize = MapTextEditor.FontSize;
                completionWindow.FontFamily = MapTextEditor.FontFamily; // ついでにフォントファミリも統一
                // ★ フォントサイズに応じてポップアップウィンドウの幅を動的に広げ、はみ出しを防ぐ
                // デフォルトのままだと幅が狭いため、フォントサイズ（例: 14〜72）に合わせて幅を計算・調整します
                double estimatedWidth = Math.Max(250, MapTextEditor.FontSize * 18);
                completionWindow.Width = estimatedWidth;

                // 必要に応じてウィンドウ自体の高さの制限やレイアウト調整も可能
                completionWindow.MaxHeight = 300;
                var data = completionWindow.CompletionList.CompletionData;

                foreach (var item in filteredItems)
                {
                    data.Add(item);
                }
                // ★ 候補が1件以上ある場合、1番目の項目オブジェクトを直接指定して選択状態にする
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
        private void FileExplorerTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (FileExplorerTreeView.SelectedItem is TreeViewItem selectedItem && selectedItem.Tag is string path)
            {
                // 選択されたパスがファイルであれば開く
                if (File.Exists(path))
                {
                    // ① 拡張子が .txt 以外の場合は警告を表示して中止する
                    string extension = Path.GetExtension(path);
                    if (!string.Equals(extension, ".txt", StringComparison.OrdinalIgnoreCase))
                    {
                        MessageBox.Show(
                            "テキストファイル（.txt）以外のファイルを開くことはできません。",
                            "対応していないファイル形式",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return;
                    }
                    // 未保存の変更がある場合は確認ダイアログ
                    if (isModified)
                    {
                        var result = MessageBox.Show(
                            "現在の変更が保存されていません。別のファイルを開きますか？",
                            "確認",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning);

                        if (result == MessageBoxResult.No)
                        {
                            return;
                        }
                    }

                    OpenFile(path);
                }
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
            }
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
    }

}