using Microsoft.Win32;
using System;
using System.Configuration;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;

namespace BveMapEditor
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // コマンドライン引数によるファイル関連付けの登録・解除モードの処理
            if (e.Args.Length > 0)
            {
                if (e.Args[0] == "--assoc-register")
                {
                    ExecuteRegistryRegister();
                    Shutdown();
                    return;
                }
                else if (e.Args[0] == "--assoc-unregister")
                {
                    ExecuteRegistryUnregister();
                    Shutdown();
                    return;
                }
            }
            base.OnStartup(e);
            // 1. 【最優先】ウィンドウを作る前に、カレントディレクトリをアプリのある場所に強制固定する
            // これにより、AvalonEditなどのハイライトファイルが常に正しく見つかるようになります。
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            Directory.SetCurrentDirectory(appDir);

            // 2. ダブルクリック起動などで渡された引数（.bveepファイル）の絶対パスを保持しておく
            string? targetBveepPath = null;
            if (e.Args.Length > 0)
            {
                // OSによってカレントディレクトリが変更されている状態を利用して絶対パスに変換
                targetBveepPath = Path.GetFullPath(e.Args[0]);
            }

            // 3. メインウィンドウを手動で生成・表示する
            var mainWindow = new MainWindow();

            // 引数がある場合は、ウィンドウが完全に読み込まれてから .bveep を復元する
            if (!string.IsNullOrEmpty(targetBveepPath))
            {
                mainWindow.Loaded += async (s, ev) =>
                {
                    await mainWindow.LoadBveepFileFromCommandLineAsync(targetBveepPath);
                };
            }
            mainWindow.Show();
        }
        // レジストリ登録の実際処理
        private static void ExecuteRegistryRegister()
        {
            try
            {
                string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";

                // .bveep 拡張子の紐付け
                using (var key = Registry.ClassesRoot.CreateSubKey(".bveep"))
                {
                    key.SetValue("", "BveEdit.ExplorerPane");
                }

                // ProgID の定義
                using (var key = Registry.ClassesRoot.CreateSubKey("BveEdit.ExplorerPane"))
                {
                    key.SetValue("", "BVE Explorer Pane File");
                }

                // アイコンの設定
                using (var key = Registry.ClassesRoot.CreateSubKey(@"BveEdit.ExplorerPane\DefaultIcon"))
                {
                    key.SetValue("", $"{exePath},0");
                }

                // 起動コマンドの設定
                using (var key = Registry.ClassesRoot.CreateSubKey(@"BveEdit.ExplorerPane\shell\open\command"))
                {
                    key.SetValue("", $"\"{exePath}\" \"%1\"");
                }

                // シェルにアイコンキャッシュの更新を通知
                SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"関連付けの登録に失敗しました:\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // レジストリ解除の実際処理
        private static void ExecuteRegistryUnregister()
        {
            try
            {
                Registry.ClassesRoot.DeleteSubKeyTree(".bveep", false);
                Registry.ClassesRoot.DeleteSubKeyTree("BveEdit.ExplorerPane", false);

                SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"関連付けの解除に失敗しました:\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
    }
}


