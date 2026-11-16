using Microsoft.Win32;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BveMapEditor
{
    /// <summary>
    /// DetailSettingsWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class DetailSettingsWindow : Window
    {
        // --- UACアイコン取得用 Win32 API 定義 ---
        [DllImport("shell32.dll", SetLastError = true)]
        private static extern int SHGetStockIconInfo(uint siid, uint uFlags, ref SHSTOCKICONINFO psii);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHSTOCKICONINFO
        {
            public uint cbSize;
            public IntPtr hIcon;
            public int iSysImageIndex;
            public int iIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szPath;
        }
        private const uint SIID_SHIELD = 77;          // UACシールドアイコンのID
        private const uint SHGSI_ICON = 0x000000100;    // アイコンを取得するフラグ
        private const uint SHGSI_SMALLICON = 0x000000001; // 小さいアイコン (16x16)

        private static ImageSource? GetUacShieldIcon()
        {
            var sii = new SHSTOCKICONINFO();
            sii.cbSize = (uint)Marshal.SizeOf(typeof(SHSTOCKICONINFO));

            int result = SHGetStockIconInfo(SIID_SHIELD, SHGSI_ICON | SHGSI_SMALLICON, ref sii);
            if (result == 0 && sii.hIcon != IntPtr.Zero)
            {
                try
                {
                    ImageSource imageSource = Imaging.CreateBitmapSourceFromHIcon(
                        sii.hIcon,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());

                    imageSource.Freeze();
                    return imageSource;
                }
                finally
                {
                    DestroyIcon(sii.hIcon);
                }
            }
            return null;
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr hIcon);


        private readonly AppSettings _settings;
        private readonly string? _initialBvePath = string.Empty; // ★ 初期パス
        private readonly string? _initialQuickMapPath = string.Empty; // ★ QuickMapViewer 初期パス
        // ★ 開いた時点の初期状態を保持するフィールド
        private readonly bool _initialRetainSearchHistory;
        private readonly bool _initialEnableAutoRestore; // ★ 追加
        internal DetailSettingsWindow(AppSettings settings)
        {
            InitializeComponent();
            _settings = settings;
            // ★ 初期値を保存
            _initialRetainSearchHistory = _settings.RetainSearchHistory;
            _initialEnableAutoRestore = _settings.EnableAutoRestore; // ★ 追加
            // 現在の設定値をUIに反映
            ChkRetainSearchHistory.IsChecked = _settings.RetainSearchHistory;
            // ウィンドウを開いたときに現在の設定状態をチェックボックスに反映
            chkAutoCompleteBrackets.IsChecked = _settings.AutoCompleteBrackets;
            ChkEnableAutoRestore.IsChecked = _settings.EnableAutoRestore; // ★ 追加
            // 保存時のエンコーディング確認設定を反映
            ChkConfirmEncodingOnSave.IsChecked = _settings.ConfirmEncodingOnSave;
            //パスを保持
            _initialBvePath = _settings.BveExecutablePath ?? string.Empty;
            _initialQuickMapPath = _settings.QuickMapViewerExecutablePath ?? string.Empty;
            // BVE実行ファイルパスの初期値をテキストボックスに反映
            TxtBvePath.Text = _initialBvePath;
            // QuickMapViewer 初期値をテキストボックスに反映
            TxtQuickMapPath.Text = _initialQuickMapPath;
            UpdateAssociationButtonStates();
            // ボタンにUACアイコンとラベルを設定
            SetupButtonWithUacIcon(BtnRegister, "関連付けを登録");
            SetupButtonWithUacIcon(BtnUnregister, "関連付けを解除");

            // 状態を反映
            UpdateAssociationButtonStates();
        }
        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            // チェックボックスの状態を設定モデルに反映
            _settings.AutoCompleteBrackets = chkAutoCompleteBrackets.IsChecked ?? true;
            // UIの設定値をAppsettingsインスタンスに反映
            _settings.RetainSearchHistory = ChkRetainSearchHistory.IsChecked == true;
            _settings.EnableAutoRestore = ChkEnableAutoRestore.IsChecked == true; // ★ 追加
            _settings.BveExecutablePath = TxtBvePath.Text.Trim();
            _settings.QuickMapViewerExecutablePath = TxtQuickMapPath.Text.Trim();
            // ConfirmEncodingOnSave を反映
            _settings.ConfirmEncodingOnSave = ChkConfirmEncodingOnSave.IsChecked == true;

            // もし「履歴を保持する」がオフになった場合は、即座に保持データをクリアする
            if (!_settings.RetainSearchHistory)
            {
                _settings.SearchHistory.Clear();
                _settings.ReplaceHistory.Clear();
                _settings.LastMatchCase = false;
                _settings.LastLoopSearch = true;
                _settings.LastWarnLargeReplace = true;
            }

            DialogResult = true;
            Close();
        }
        // ボタンにUACアイコンとテキストを設定するヘルパー
        private void SetupButtonWithUacIcon(Button button, string text)
        {
            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            // UACシールドアイコン
            var shieldIconSource = GetUacShieldIcon();
            if (shieldIconSource != null)
            {
                var iconImage = new System.Windows.Controls.Image
                {
                    Source = shieldIconSource,
                    Width = 16,
                    Height = 16,
                    Margin = new Thickness(0, 0, 5, 0), // テキストとの間隔
                    VerticalAlignment = VerticalAlignment.Center
                };
                stackPanel.Children.Add(iconImage);
            }

            // ボタンのテキスト
            var textBlock = new TextBlock
            {
                Text = text,
                VerticalAlignment = VerticalAlignment.Center
            };
            stackPanel.Children.Add(textBlock);

            button.Content = stackPanel;
        }
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnResetPerFileEncoding_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(this,
                "保存時に特定ファイルのエンコーディングをスキップする設定をすべて削除しますか？",
                "確認",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                _settings.PerFileEncodingMap.Clear();
                MessageBox.Show(this, "特定ファイルのエンコーディング設定をリセットしました。", "完了", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        // ★ ウィンドウが閉じられる直前の共通イベント（「×」ボタンやキャンセル時の共通ガード）
        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);

            // OKボタンが押されて正常終了した場合は警告を出さない
            if (DialogResult == true) return;
            // 変更チェック（履歴設定、Bveパス、QuickMapViewerパスのいずれかが変更されているか）
            bool isChanged = (ChkRetainSearchHistory.IsChecked != _initialRetainSearchHistory) ||
                             (ChkEnableAutoRestore.IsChecked != _initialEnableAutoRestore) || // ★ 追加
                             (TxtBvePath.Text.Trim() != _initialBvePath) ||
                             (TxtQuickMapPath.Text.Trim() != _initialQuickMapPath);

            if (isChanged)
            {
                var result = MessageBox.Show(this,
                    "変更内容が破棄されます。よろしいですか？",
                    "確認",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.No)
                {
                    // 「いいえ」を選んだ場合はウィンドウを閉じるのをキャンセルする
                    e.Cancel = true;
                }
            }
        }
        private void UpdateAssociationButtonStates()
        {
            bool isAssociated = CheckFileAssociation();

            // 関連付けされているときは登録ボタンをグレーアウト(IsEnabled = false)
            // 関連付けされていないときは解除ボタンをグレーアウト(IsEnabled = false)
            BtnRegister.IsEnabled = !isAssociated;
            BtnUnregister.IsEnabled = isAssociated;
        }

        // 関連付けの有無を判定
        private bool CheckFileAssociation()
        {
            try
            {
                using var key = Registry.ClassesRoot.OpenSubKey(".bveep");
                if (key != null)
                {
                    var value = key.GetValue("");
                    return value as string == "BveEdit.ExplorerPane";
                }
            }
            catch
            {
                // 権限がない場合などは false とみなす
            }
            return false;
        }

        // 「登録」ボタンクリック
        private void BtnRegister_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "関連付けを変更しようとしています。レジストリ操作はシステムが不安定になる可能性があります。ユーザーによって操作されたものですか？",
                "確認",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                ExecuteElevatedCommand("--assoc-register");
                UpdateAssociationButtonStates();
            }
        }

        // 「解除」ボタンクリック
        private void BtnUnregister_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "関連付けを変更しようとしています。レジストリ操作はシステムが不安定になる可能性があります。ユーザーによって操作されたものですか？",
                "確認",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                ExecuteElevatedCommand("--assoc-unregister");
                UpdateAssociationButtonStates();
            }
        }

        // 管理者権限（UAC）で自分自身のプロセスを引数付きで再起動する
        private void ExecuteElevatedCommand(string argument)
        {
            try
            {
                string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = argument,
                    UseShellExecute = true,
                    Verb = "runas" // ★これで強制的にUAC（管理者昇格プロンプト）が表示される
                };

                var process = Process.Start(psi);
                process?.WaitForExit(); // 処理が完了するまで待機
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // ユーザーがUACダイアログで「いいえ」を押した場合はここに飛びます（キャンセル扱い）
            }
            catch (Exception ex)
            {
                MessageBox.Show($"エラーが発生しました:\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        //削除しない
        private void chkAutoCompleteBrackets_Checked(object sender, RoutedEventArgs e)
        {
        }
        // 参照ボタンで実行ファイル(.exe)を選ぶ
        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Bve5 / Bve6 実行ファイルの選択",
                Filter = "Executable Files (*.exe)|*.exe|All Files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                TxtBvePath.Text = openFileDialog.FileName;
            }
        }

        // QuickMapViewer 用の参照ボタン
        private void BtnBrowseQuickMap_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "QuickMapViewer 実行ファイルの選択",
                Filter = "Executable Files (*.exe)|*.exe|All Files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                TxtQuickMapPath.Text = openFileDialog.FileName;
            }
        }
    }
}
