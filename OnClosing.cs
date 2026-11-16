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
            App.LogOperation("ウィンドウクローズ");
            // ★ (2026-11-15) 以前は検索ウィンドウが開いていると「別のウィンドウを閉じられていません」と出して終了を止めていた
            //    → 止めずに未保存の確認へ進み、終了するときに検索ウィンドウも閉じる（一番下）
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
                        App.LogOperation("クローズキャンセル");
                        return;
                    }
                }
                // No の場合は保存せずに終了する
            }

            // ★ (2026-11-15) 終了が決まったので、開いている検索ウィンドウを閉じる（検索の履歴は検索ウィンドウを閉じるときに保存される）
            foreach (Window window in this.OwnedWindows.Cast<Window>().ToList())
            {
                if (window is SearchWindow)
                {
                    try { window.Close(); } catch { }
                }
            }
            _fileSystemWatcher?.Dispose();
        }
    }
}
