using Microsoft.Win32;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace BveMapEditor
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        // ユーザーの直近の操作履歴を保持するキュー（最大20件）
        private static readonly Queue<string> _operationHistory = new Queue<string>();
        private static readonly object _lockObj = new();

        // 操作履歴を追加するメソッド（MainWindowなどから呼び出す）
        public static void LogOperation(string actionDescription)
        {
            lock (_lockObj)
            {
                string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {actionDescription}";
                if (_operationHistory.Count >= 20)
                {
                    _operationHistory.Dequeue();
                }
                _operationHistory.Enqueue(entry);
            }
        }
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
            // 【グローバル例外ハンドラの登録】（予期せぬクラッシュを確実に捕捉する為に最優先で登録）
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
            // CodePages エンコーディング（Shift-JIS / CP932 等）を利用可能にする
            // NuGet: System.Text.Encoding.CodePages をインストールしておくこと
            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            }
            catch
            {
                // 登録に失敗しても継続（パッケージ未導入の場合は既定のエンコーディングのみ利用可）
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
            else{
                // ★ 通常起動時は、UIの初期化が完了した Loaded イベントのタイミングで自動復元を安全に実行
                mainWindow.Loaded += async (s, ev) =>
                {
                    mainWindow.TryRestoreAutoSession(); // 必要に応じて TryRestoreAutoSession を public / internal にしておく
                };
            }
            mainWindow.Show();
        }
        // --- 例外キャッチ & ログ出力関連のメソッド ---

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            HandleCrash(e.Exception, "UI Thread Unhandled Exception");
            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                HandleCrash(ex, "AppDomain Unhandled Exception");
            }
        }

        private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            HandleCrash(e.Exception, "TaskScheduler Unobserved Task Exception");
            e.SetObserved();
        }

        private void HandleCrash(Exception ex, string source)
        {
            try
            {
                string logContent = GenerateCrashReport(ex, source);

                // ログファイルの保存先（実行ファイルと同じフォルダの "Logs" フォルダ内）
                string logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
                Directory.CreateDirectory(logDir);

                string logFilePath = Path.Combine(logDir, $"crash_{DateTime.Now:yyyyMMdd_HHmmss}.log");
                File.WriteAllText(logFilePath, logContent, Encoding.UTF8);

                // ユーザーへのお知らせと謝罪ダイアログ
                MessageBox.Show(
                    $"予期せぬエラーによりアプリケーションがクラッシュしました。\n\n" +
                    $"エラーログが以下の場所に保存されました:\n{logFilePath}\n\n" +
                    $"開発者へご報告の際はこのファイルをお使いください。",
                    "BVE Map Editor - クラッシュ通知",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception logEx)
            {
                System.Diagnostics.Debug.WriteLine($"クラッシュログの保存に失敗しました: {logEx.Message}");
            }
            finally
            {
                // アプリケーションを安全に終了
                Environment.Exit(1);
            }
        }

        private string GenerateCrashReport(Exception ex, string source)
        {
            var sb = new StringBuilder();
            sb.AppendLine("========================================");
            sb.AppendLine(" BVE Map Editor Crash Report");
            sb.AppendLine("========================================");
            sb.AppendLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Exception Source: {source}");
            sb.AppendLine();

            sb.AppendLine("--- Exception Details ---");
            sb.AppendLine($"Type: {ex.GetType().FullName}");
            sb.AppendLine($"Message: {ex.Message}");
            sb.AppendLine($"TargetSite: {ex.TargetSite}");
            sb.AppendLine("Stack Trace:");
            sb.AppendLine(ex.ToString());
            sb.AppendLine();

            if (ex.InnerException != null)
            {
                sb.AppendLine("--- Inner Exception ---");
                sb.AppendLine($"Type: {ex.InnerException.GetType().FullName}");
                sb.AppendLine($"Message: {ex.InnerException.Message}");
                sb.AppendLine($"Stack Trace:");
                sb.AppendLine(ex.InnerException.ToString());
                sb.AppendLine();
            }
            sb.AppendLine("--- Computer & Environment Info ---");
            sb.AppendLine($"OS Version: {Environment.OSVersion}");
            sb.AppendLine($".NET Runtime: {Environment.Version}");
            //sb.AppendLine($"Machine Name: {Environment.MachineName}");
            //sb.AppendLine($"User Name: {Environment.UserName}");
            sb.AppendLine($"Processor Name: {GetProcessorName()}");
            sb.AppendLine($"Logical Processors: {Environment.ProcessorCount}");
            sb.AppendLine();
            // システム全体の物理メモリ情報の取得
            ulong totalPhys = 0;
            ulong availPhys = 0;
            try
            {
                var memStatus = new MEMORYSTATUSEX();
                memStatus.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
                if (GlobalMemoryStatusEx(ref memStatus))
                {
                    totalPhys = memStatus.ullTotalPhys;
                    availPhys = memStatus.ullAvailPhys;
                }
            }
            catch { 
                //取得が失敗しても何もしない
                    }

            if (totalPhys > 0)
            {
                sb.AppendLine($"System Total Physical Memory: {totalPhys / 1024 / 1024} MB");
                sb.AppendLine($"System Available Physical Memory: {availPhys / 1024 / 1024} MB");
            }
            // 自身のプロセス情報の取得
            try
            {
                using var proc = System.Diagnostics.Process.GetCurrentProcess();
                sb.AppendLine($"Process Name: {proc.ProcessName} (ID: {proc.Id})");
                sb.AppendLine($"Process Working Set Memory (Physical): {proc.WorkingSet64 / 1024 / 1024} MB");
                sb.AppendLine($"Process Private Memory: {proc.PrivateMemorySize64 / 1024 / 1024} MB");
            }
            catch { }
            sb.AppendLine($"Current Directory: {Environment.CurrentDirectory}");
            sb.AppendLine();
            sb.AppendLine("--- Recent User Operations (History) ---");
            lock (_lockObj)
            {
                if (_operationHistory.Count == 0)
                {
                    sb.AppendLine("(No operations recorded)");
                }
                else
                {
                    foreach (var op in _operationHistory)
                    {
                        sb.AppendLine(op);
                    }
                }
            }
            sb.AppendLine("========================================");

            return sb.ToString();
        }
        // --- ハードウェア情報取得用のヘルパー・Win32 API定義 ---

        private static string? GetProcessorName()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
                // パターンマッチングにより null 安全に string 型として取得
                if (key?.GetValue("ProcessorNameString") is string name) return name.Trim();
            }
            catch { }
            return "Unknown Processor";
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);



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


