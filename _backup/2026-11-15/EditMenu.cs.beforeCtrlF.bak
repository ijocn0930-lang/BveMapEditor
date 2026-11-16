using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;

namespace BveMapEditor
{
    public partial class MainWindow : Window
    {
        // 切り取りメニューがクリックされたとき
        private void MenuCut_Click(object sender, RoutedEventArgs e)
        {
            var editor = ActiveEditor;
            if (editor != null && editor.SelectionLength > 0)
            {
                editor.Cut();
            }
        }

        // コピーメニューがクリックされたとき
        private void MenuCopy_Click(object sender, RoutedEventArgs e)
        {
            var editor = ActiveEditor;
            if (editor != null && editor.SelectionLength > 0)
            {
                editor.Copy();
            }
        }

        // 貼り付けメニューがクリックされたとき
        private void MenuPaste_Click(object sender, RoutedEventArgs e)
        {
            var editor = ActiveEditor;
            editor?.Paste();
        }
        // すべて選択メニューがクリックされたとき
        private void MenuSelectAll_Click(object sender, RoutedEventArgs e)
        {
            var editor = ActiveEditor;
            editor?.SelectAll();
        }
        private void MenuSearch_Click(object sender, RoutedEventArgs e)
        {
            var searchWindow = new SearchWindow(ActiveEditor ?? MapTextEditor, currentSettings)
            {
                Owner = this
            };
            searchWindow.Show();
        }
        // 「右端で折り返す」メニューのクリック処理
        private void MenuWordWrap_Click(object sender, RoutedEventArgs e)
        {
            var editor = ActiveEditor ?? MapTextEditor;
            if (editor == null) return;

            // メニューのチェック状態をエディタに反映
            bool isWrapped = MenuWordWrap.IsChecked;
            editor.WordWrap = isWrapped;

            // 設定を更新して保存
            currentSettings.WordWrap = isWrapped;
            currentSettings.Save();
        }
        // 「変更を戻す」メニューのクリック処理
        private void MenuUndo_Click(object sender, RoutedEventArgs e)
        {
            var editor = ActiveEditor ?? MapTextEditor;
            if (editor?.Document?.UndoStack?.CanUndo == true)
            {
                editor.Undo();
            }
        }

        // 「やり直す」メニューのクリック処理
        private void MenuRedo_Click(object sender, RoutedEventArgs e)
        {
            var editor = ActiveEditor ?? MapTextEditor;
            if (editor?.Document?.UndoStack?.CanRedo == true)
            {
                editor.Redo();
            }
        }

    }
}
