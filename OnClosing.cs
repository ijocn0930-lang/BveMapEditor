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
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            var editor = ActiveEditor ?? MapTextEditor;
            if (editor != null)
            {
                currentSettings.FontFamilyName = editor.FontFamily.Source;
                currentSettings.FontSize = editor.FontSize;
                currentSettings.WordWrap = editor.WordWrap; // ★追加
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
            // タブごとの未保存チェック
            var modifiedTabs = new System.Collections.Generic.List<TabDocumentInfo>();
            if (EditorTabControl != null)
            {
                foreach (var item in EditorTabControl.Items)
                {
                    if (item is TabItem ti)
                    {
                        var info = GetTabInfo(ti);
                        if (info != null && info.IsModified)
                        {
                            modifiedTabs.Add(info);
                        }
                    }
                }
            }

            if (modifiedTabs.Count > 0)
            {
                var result = MessageBox.Show(
                    "保存されていないタブがあります。すべて保存しますか？\n(Yes=すべて保存して終了, No=保存せず終了, Cancel=中止)",
                    "未保存のファイルが存在します",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Cancel)
                {
                    e.Cancel = true;
                    return;
                }

                if (result == MessageBoxResult.Yes)
                {
                    // すべて保存
                    if (!SaveAllModifiedTabs(modifiedTabs))
                    {
                        // 保存の途中でキャンセルされた
                        e.Cancel = true;
                        return;
                    }
                }
                // No の場合は保存せずに終了する
            }
            _fileSystemWatcher?.Dispose();
        }

    }
}
