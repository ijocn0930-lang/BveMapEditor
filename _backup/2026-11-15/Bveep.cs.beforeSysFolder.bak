using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace BveMapEditor
{
    public partial class MainWindow : Window
    {
        public async Task LoadBveepFileFromCommandLineAsync(string filePath)
        {
            // すでに実装した LoadBveepFileAsync を呼び出して復元する
            await LoadBveepFileAsync(filePath);
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

                string[] lines = await ReadAllLinesWithAnsiFallbackAsync(filePath);

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
        // メニュー：「上書き保存」 エクスプローラーペイン専用
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

    }
}
