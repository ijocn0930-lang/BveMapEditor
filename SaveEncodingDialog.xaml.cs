using System.Text;
using System.Windows;

namespace BveMapEditor
{
    /// <summary>
    /// Interaction logic for SaveEncodingDialog.xaml
    /// </summary>
    public partial class SaveEncodingDialog : Window
    {
        public SaveEncodingDialog(Encoding? currentEncoding)
        {
            InitializeComponent();

            // 初期選択
            if (currentEncoding != null)
            {
                RbKeep.IsEnabled = true;
                RbKeep.IsChecked = true;
            }
            else
            {
                RbKeep.IsEnabled = false;
                RbUtf8NoBom.IsChecked = true;
            }
        }

        public Encoding? SelectedEncoding { get; private set; }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            if (RbKeep.IsChecked == true)
            {
                // 値は呼び出し側で currentFileEncoding から再取得することを想定しているため
                // ここでは null を意味する特殊値ではなくそのまま保持しておく。
                SelectedEncoding = null;
            }
            else if (RbUtf8NoBom.IsChecked == true)
            {
                SelectedEncoding = new UTF8Encoding(false); //<-BOMとして保存されるのを修正したい
            }
            else if (RbShiftJis.IsChecked == true)
            {
                SelectedEncoding = Encoding.GetEncoding(932);
            }
            else if (RbUtf8Bom.IsChecked == true)
            {
                SelectedEncoding = new UTF8Encoding(true);
            }
            else
            {
                SelectedEncoding = new UTF8Encoding(false);
            }

            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        public static (bool Confirmed, Encoding? SelectedEncoding) PromptEncoding(Window owner, Encoding? currentEncoding)
        {
            var dlg = new SaveEncodingDialog(currentEncoding)
            {
                Owner = owner
            };

            bool? res = dlg.ShowDialog();
            if (res == true)
            {
                // Confirmed == true: user clicked OK; SelectedEncoding may be null to mean "Keep original"
                return (true, dlg.SelectedEncoding);
            }
            // Confirmed == false: user cancelled
            return (false, null);
        }
    }
}
