using System.IO;
using System.Windows;

namespace BveMapEditor
{
    /// <summary>
    /// ★ (2026-11-17) エクスプローラーペインの名前の変更の窓
    ///    以前の入力ボックス（Visual Basic の InputBox）は文字を全部選んだ状態で出るため、拡張子まで消してしまうおそれがあった
    ///    → Windows のエクスプローラーと同じく、ファイルは拡張子の前までを選んだ状態で出す（フォルダは全部）
    ///    拡張子を変えた・消したときは確認する。使えない文字などの確かめは呼び出し側（今まで通り）
    /// </summary>
    public partial class RenameDialog : Window
    {
        private readonly string _currentName;
        private readonly bool _isDirectory;

        /// <summary>入力した新しい名前（前後の空白は取る）</summary>
        public string NewName { get; private set; } = string.Empty;

        public RenameDialog(string currentName, bool isDirectory)
        {
            InitializeComponent();
            _currentName = currentName;
            _isDirectory = isDirectory;
            TxtName.Text = currentName;
            Loaded += (s, e) =>
            {
                TxtName.Focus();
                int length = GetInitialSelectionLength(currentName, isDirectory);
                TxtName.Select(0, length);
            };
        }

        /// <summary>
        /// 初めに選んでおく文字数。ファイルは拡張子の前まで（.gitignore のように名前が . で始まるだけのものは全部）、フォルダは全部
        /// </summary>
        public static int GetInitialSelectionLength(string name, bool isDirectory)
        {
            if (isDirectory) return name.Length;
            int dot = name.LastIndexOf('.');
            return dot > 0 ? dot : name.Length;
        }

        /// <summary>拡張子が変わったか（大文字小文字の違いだけなら変わっていない）</summary>
        public static bool IsExtensionChanged(string oldName, string newName)
            => !string.Equals(Path.GetExtension(oldName), Path.GetExtension(newName), StringComparison.OrdinalIgnoreCase);

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            string name = TxtName.Text.Trim();
            if (!_isDirectory && name.Length > 0 && name != _currentName && IsExtensionChanged(_currentName, name))
            {
                string oldExt = Path.GetExtension(_currentName);
                string newExt = Path.GetExtension(name);
                string what = newExt.Length == 0 ? $"拡張子（{oldExt}）を消すと" : $"拡張子を {(oldExt.Length == 0 ? "（なし）" : oldExt)} から {newExt} に変えると";
                var r = MessageBox.Show(this, $"{what}、ファイルが使えなくなることがあります。\n\n変えますか？", Title, MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (r != MessageBoxResult.Yes)
                {
                    TxtName.Focus();
                    return;
                }
            }
            NewName = name;
            DialogResult = true;
        }

        /// <summary>窓を出して新しい名前を聞く。やめたら null</summary>
        public static string? Prompt(Window owner, string currentName, bool isDirectory)
        {
            var dlg = new RenameDialog(currentName, isDirectory) { Owner = owner };
            return dlg.ShowDialog() == true ? dlg.NewName : null;
        }
    }
}
