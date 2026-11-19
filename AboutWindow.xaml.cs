using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace BveMapEditor
{
    /// <summary>
    /// AboutWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
            // ★ (2026-11-19) バージョンは BveMapEditor.csproj の Version から出す（以前は窓に文字で直接書いていた）
            TxtVersion.Text = $"バージョン: {MainWindow.GetAppVersionText()}";
        }
    
    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            // 外部ブラウザでリンクを開く（.NET Core / .NET 6+ 以降の標準的な書き方）
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }
    }
}