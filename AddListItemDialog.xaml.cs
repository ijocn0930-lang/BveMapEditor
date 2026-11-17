using Microsoft.Win32;
using System.IO;
using System.Windows;

namespace BveMapEditor
{
    /// <summary>
    /// ★ (2026-11-17) リストファイル（ストラクチャーなど）に「キー,ファイル」の 1 行を足すダイアログ
    ///    ここでは入力の形だけを確かめる（キーの重複などは、リストファイルの中身を見て呼び出し側が確かめる）
    /// </summary>
    public partial class AddListItemDialog : Window
    {
        private readonly string _listFilePath;
        private readonly string _fileFilter;

        /// <summary>入力したキー（前後の空白は取る）</summary>
        public string Key { get; private set; } = string.Empty;
        /// <summary>リストファイルのフォルダから見たファイルの場所（リストに書く形）</summary>
        public string RelativeFilePath { get; private set; } = string.Empty;
        /// <summary>★ (2026-11-17) 同時に鳴らせる数（サウンド3D のときだけ。空なら書かない）</summary>
        public string Count { get; private set; } = string.Empty;

        private readonly bool _showCount;
        private readonly string? _requiredExtension;

        /// <param name="showCount">true なら「同時に鳴らせる数」の欄を出す（サウンド3D）</param>
        /// <param name="requiredExtension">★ (2026-11-17) この拡張子以外のファイルなら確認する（サウンドは ".wav"。BVE5 / BVE6 は .wav だけ）。null なら確かめない</param>
        /// <param name="initialKey">★ (2026-11-17) 初めにキーの欄に入れておくキー（修正の候補から開いたときのエラーのキー）</param>
        public AddListItemDialog(string title, string listFilePath, string fileFilter, bool showCount = false, string? requiredExtension = null, string? initialKey = null)
        {
            InitializeComponent();
            if (!string.IsNullOrEmpty(initialKey)) TxtKey.Text = initialKey;
            Title = title;
            _listFilePath = listFilePath;
            _fileFilter = fileFilter;
            _showCount = showCount;
            _requiredExtension = requiredExtension;
            TxtListFile.Text = $"追加するリストファイル: {listFilePath}";
            if (showCount)
            {
                LblCount.Visibility = Visibility.Visible;
                TxtCount.Visibility = Visibility.Visible;
            }
            // キーが入っていればファイルの欄から（修正の候補から開いたとき）
            Loaded += (s, e) => { if (TxtKey.Text.Length > 0) TxtFile.Focus(); else TxtKey.Focus(); };
        }

        /// <summary>
        /// ★ (2026-11-17) 同時に鳴らせる数の形を確かめる（空か、1〜999 の整数なら null。だめなら理由）
        /// </summary>
        public static string? GetCountProblem(string count)
        {
            if (count.Length == 0) return null;
            if (!int.TryParse(count, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int n) || n < 1 || n > 999)
                return "同時に鳴らせる数は、1〜999 の整数で入力してください（空にすると書きません）。";
            return null;
        }

        private string ListFolder => Path.GetDirectoryName(_listFilePath) ?? string.Empty;

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "ファイルを選ぶ",
                Filter = _fileFilter,
                InitialDirectory = Directory.Exists(ListFolder) ? ListFolder : string.Empty,
                CheckFileExists = true,
            };
            if (dlg.ShowDialog(this) != true) return;
            TxtFile.Text = ToListRelativePath(dlg.FileName);
            // キーが空なら、ファイル名（拡張子なし）を入れておく
            if (string.IsNullOrWhiteSpace(TxtKey.Text)) TxtKey.Text = Path.GetFileNameWithoutExtension(dlg.FileName);
        }

        // リストファイルのフォルダから見た場所にする（別のドライブなどで直せなければそのまま）
        private string ToListRelativePath(string fullPath)
        {
            try
            {
                if (string.IsNullOrEmpty(ListFolder)) return fullPath;
                string rel = Path.GetRelativePath(ListFolder, fullPath);
                return Path.IsPathRooted(rel) ? fullPath : rel;
            }
            catch
            {
                return fullPath;
            }
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            string key = TxtKey.Text.Trim();
            string file = TxtFile.Text.Trim();

            string? keyProblem = GetKeyProblem(key);
            if (keyProblem != null)
            {
                MessageBox.Show(this, keyProblem, Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtKey.Focus();
                return;
            }
            string count = _showCount ? TxtCount.Text.Trim() : string.Empty;
            string? countProblem = GetCountProblem(count);
            if (countProblem != null)
            {
                MessageBox.Show(this, countProblem, Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtCount.Focus();
                return;
            }
            Count = count;
            if (file.Length == 0)
            {
                // ★ (2026-11-17) BVE5 / BVE6 ではファイルが空でもエラーにならないので、止めずに尋ねる（ユーザーの指示）
                var r = MessageBox.Show(this, "ファイルが空です。\n（BVE5 / BVE6 ではエラーになりませんが、何も表示されません）\n\nこのまま追加しますか？", Title, MessageBoxButton.OKCancel, MessageBoxImage.Question);
                if (r != MessageBoxResult.OK)
                {
                    TxtFile.Focus();
                    return;
                }
                Key = key;
                RelativeFilePath = string.Empty;
                DialogResult = true;
                return;
            }
            if (file.IndexOfAny(new[] { ',', '\r', '\n', '\t', '"' }) >= 0 || file.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            {
                MessageBox.Show(this, "ファイルの場所に使えない文字（, \" やタブなど）があります。", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtFile.Focus();
                return;
            }

            // 手で入力した絶対パスも、リストファイルのフォルダから見た場所に直す
            if (Path.IsPathRooted(file)) file = ToListRelativePath(file);
            if (Path.IsPathRooted(file))
            {
                MessageBox.Show(this, "リストファイルと別のドライブにあるファイルは追加できません。\nリストファイルと同じドライブ（できれば同じフォルダの中）のファイルを選んでください。", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string full;
            try { full = Path.GetFullPath(Path.Combine(ListFolder, file)); }
            catch
            {
                MessageBox.Show(this, "ファイルの場所が正しくありません。", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (_requiredExtension != null && !Path.GetExtension(file).Equals(_requiredExtension, StringComparison.OrdinalIgnoreCase))
            {
                var r = MessageBox.Show(this, $"BVE5 / BVE6 で使えるのは {_requiredExtension} のファイルだけです。\n（{Path.GetFileName(file)} は読み込まれません）\n\nこのまま追加しますか？", Title, MessageBoxButton.OKCancel, MessageBoxImage.Warning);
                if (r != MessageBoxResult.OK)
                {
                    TxtFile.Focus();
                    return;
                }
            }
            if (!File.Exists(full))
            {
                var r = MessageBox.Show(this, $"ファイルが見つかりません。\n{full}\n\nこのまま追加しますか？", Title, MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (r != MessageBoxResult.Yes) return;
            }
            if (file.StartsWith("..", StringComparison.Ordinal))
            {
                var r = MessageBox.Show(this, "リストファイルのフォルダの外のファイルです。\nシナリオを配るときに、このファイルを入れ忘れないよう注意してください。\n\nこのまま追加しますか？", Title, MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (r != MessageBoxResult.Yes) return;
            }

            Key = key;
            RelativeFilePath = file;
            DialogResult = true;
        }

        /// <summary>
        /// キーに使えない形なら理由を返す（使えれば null）。, はリストの区切り、' " [ ] はマップの Structure['キー'] で困るため
        /// </summary>
        public static string? GetKeyProblem(string key)
        {
            if (key.Length == 0) return "キーを入力してください。";
            if (key.IndexOfAny(new[] { ',', '\'', '"', '[', ']', '\t', '\r', '\n', ';' }) >= 0)
                return "キーに使えない文字（, ' \" [ ] ; やタブ）があります。";
            if (key.StartsWith("#", StringComparison.Ordinal) || key.StartsWith("//", StringComparison.Ordinal))
                return "キーを # や // で始めると、コメントの行になってしまいます。";
            foreach (char c in key)
            {
                if (char.IsControl(c)) return "キーに制御文字があります。";
            }
            return null;
        }
    }
}
