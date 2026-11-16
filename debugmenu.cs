using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;

namespace BveMapEditor
{
    public partial class MainWindow : Window
    {
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
        //QMVStart
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
                // ★ (2026-11-15) 以前は BVE のパス（BveExecutablePath）と比べていたため、保存済みでも毎回「保存しますか？」と聞いていた
                if (currentSettings.QuickMapViewerExecutablePath != QMVPath)
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
