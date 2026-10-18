using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace BveMapEditor
{
    public partial class MainWindow : Window
    {
        //エクスプローラーペイン再読み込み
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
        //エクスプローラーペイン設定ファイルオープン
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

    }
}
