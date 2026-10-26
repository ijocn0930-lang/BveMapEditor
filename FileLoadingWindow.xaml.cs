using System.Windows;
using System.Windows.Controls;

namespace BveMapEditor
{
    /// <summary>
    /// FileLoadingWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class FileLoadingWindow : Window
    {
        public FileLoadingWindow(string fileName)
        {
            InitializeComponent();
            Title = "読み込み中 - BveMapEditor";
            Width = 420;
            Height = 140;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            WindowStyle = WindowStyle.ToolWindow;

            var stackPanel = new StackPanel
            {
                Margin = new Thickness(20),
                VerticalAlignment = VerticalAlignment.Center
            };

            var fileNameTextBlock = new TextBlock
            {
                Text = $"ファイルを読み込んでいます...\n{fileName}",
                Margin = new Thickness(0, 0, 0, 15),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 13
            };

            var progressBar = new ProgressBar
            {
                Height = 20,
                IsIndeterminate = true // 進行状況が不確定なビジー状態のアニメーション
            };

            stackPanel.Children.Add(fileNameTextBlock);
            stackPanel.Children.Add(progressBar);
            Content = stackPanel;
        }
    }
}
