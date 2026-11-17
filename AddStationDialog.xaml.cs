using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace BveMapEditor
{
    /// <summary>
    /// ★ (2026-11-17) ステーションリストに駅の 1 行（13 列）を足すダイアログ
    ///    列: キー,駅名,到着時刻,発車時刻,停車時間,既定の時刻,信号,降車時間,乗車率,到着の音のキー,発車の音のキー,ドアの再開閉,駆け込み
    ///    「詳しく」の 7 列と停車時間は、リストの最後の駅の行の値を初めに入れておく（同じ路線の書き方にそろう）
    ///    ここでは入力の形だけを確かめる（キーの重複は、リストの中身を見て呼び出し側が確かめる）
    /// </summary>
    public partial class AddStationDialog : Window
    {
        public const int ColumnCount = 13;
        private static readonly Regex TimeRegex = new(@"^\d{1,2}:[0-5]\d(:[0-5]\d)?$", RegexOptions.CultureInvariant);

        /// <summary>入力したキー</summary>
        public string Key { get; private set; } = string.Empty;
        /// <summary>リストに書く 1 行（13 列をカンマでつないだもの）</summary>
        public string Line { get; private set; } = string.Empty;

        /// <param name="listText">リストファイルの本文（最後の駅の行を初めの値に使う）</param>
        /// <param name="soundKeys">サウンドのキーの一覧（音のキーの欄の候補。なければ null）</param>
        /// <param name="initialKey">★ (2026-11-17) 初めにキーの欄に入れておくキー（修正の候補から開いたときのエラーのキー）</param>
        public AddStationDialog(string listFilePath, string listText, IEnumerable<string>? soundKeys, string? initialKey = null)
        {
            InitializeComponent();
            if (!string.IsNullOrEmpty(initialKey)) TxtKey.Text = initialKey;
            TxtListFile.Text = $"追加するリストファイル: {listFilePath}";
            if (soundKeys != null)
            {
                foreach (var k in soundKeys)
                {
                    CmbArrivalSound.Items.Add(k);
                    CmbDepartureSound.Items.Add(k);
                }
            }

            var template = FindTemplateRow(listText);
            if (template != null)
            {
                TxtStop.Text = template[4];
                CmbSignal.SelectedIndex = template[6] == "1" ? 1 : 0; // 0 / 1 以外（空など）は 0（進行）
                TxtAlight.Text = template[7];
                TxtPassengers.Text = template[8];
                CmbArrivalSound.Text = template[9];
                CmbDepartureSound.Text = template[10];
                TxtReopen.Text = template[11];
                TxtStuck.Text = template[12];
                TxtTemplateNote.Text = $"停車時間と「詳しく」の値は、リストの最後の駅（{template[0]}）の行から入れています。";
            }
            else
            {
                TxtStop.Text = "30";
                CmbSignal.SelectedIndex = 0;
                TxtAlight.Text = "0";
                TxtPassengers.Text = "0";
                TxtReopen.Text = "0";
                TxtStuck.Text = "0";
                TxtTemplateNote.Text = "リストに駅の行がないため、0 などの値を入れています。";
            }
            // キーが入っていれば駅名の欄から（修正の候補から開いたとき）
            Loaded += (s, e) => { if (TxtKey.Text.Length > 0) TxtName.Focus(); else TxtKey.Focus(); };
        }

        /// <summary>
        /// リストの本文から、最後の駅の行（1 行目のヘッダー・空の行・コメントを除く）を 13 列に分けて返す。なければ null
        /// 足りない列は空、前後の空白・タブは取る
        /// </summary>
        public static string[]? FindTemplateRow(string listText)
        {
            if (string.IsNullOrEmpty(listText)) return null;
            var lines = listText.Split('\n');
            for (int i = lines.Length - 1; i >= 1; i--) // 0 はヘッダー
            {
                string l = lines[i].TrimEnd('\r').Trim();
                if (l.Length == 0 || l.StartsWith("#", StringComparison.Ordinal) || l.StartsWith("//", StringComparison.Ordinal)) continue;
                var cols = l.Split(',');
                if (cols.Length < 2 || cols[0].Trim().Length == 0) continue;
                var result = new string[ColumnCount];
                for (int j = 0; j < ColumnCount; j++) result[j] = j < cols.Length ? cols[j].Trim() : string.Empty;
                return result;
            }
            return null;
        }

        /// <summary>時刻の形（H:MM:SS / HH:MM:SS。秒なしも可）なら true</summary>
        public static bool IsValidTime(string text) => TimeRegex.IsMatch(text);

        /// <summary>0 以上の数（空も可）なら true。integerOnly なら整数だけ</summary>
        public static bool IsValidNumber(string text, bool integerOnly = false)
        {
            if (text.Length == 0) return true;
            if (integerOnly)
                return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out _);
            return double.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double v) && v >= 0 && !double.IsInfinity(v);
        }

        // ★ (2026-11-17) 乗車率の上限（1 = 100%。4 = 400% まで。ユーザーより）
        public const double MaxPassengers = 4.0;

        /// <summary>乗車率として使える（空、または 0〜4 の数）なら true</summary>
        public static bool IsValidPassengers(string text)
        {
            if (text.Length == 0) return true;
            return IsValidNumber(text)
                && double.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double v)
                && v <= MaxPassengers;
        }

        // 乗車率を入れたら % でも出す（例: 0.5 → 50%）
        private void TxtPassengers_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (TxtPassengersPercent == null) return;
            string t = TxtPassengers.Text.Trim();
            if (t.Length > 0 && IsValidPassengers(t)
                && double.TryParse(t, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double v))
                TxtPassengersPercent.Text = $"= {v * 100:0.##}%（1 = 100%。0〜4）";
            else
                TxtPassengersPercent.Text = "（1 = 100%。0〜4）";
        }

        // ★ (2026-11-17) ドアの再開閉も % で出す（乗車率と同じ書き方。1 = 100%。上限なし）
        private void TxtReopen_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (TxtReopenPercent == null) return;
            string t = TxtReopen.Text.Trim();
            if (t.Length > 0 && IsValidNumber(t)
                && double.TryParse(t, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double v))
                TxtReopenPercent.Text = $"= {v * 100:0.##}%（1 = 100%）";
            else
                TxtReopenPercent.Text = "（1 = 100%）";
        }

        // 駅名・音のキーなど、文字の欄に使えない文字（, は列の区切り。改行・タブも不可）
        private static bool HasBadTextChar(string text) => text.IndexOfAny(new[] { ',', '\r', '\n', '\t', '"' }) >= 0;

        private void CmbArrival_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TxtArrival != null) TxtArrival.IsEnabled = CmbArrival.SelectedIndex == 0;
            UpdatePassTerminalItems();
        }

        private void CmbDeparture_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TxtDeparture != null) TxtDeparture.IsEnabled = CmbDeparture.SelectedIndex == 0;
            UpdatePassTerminalItems();
        }

        // ★ (2026-11-17) 通過（p）と終点（t）は同時に選べない（ユーザーより）。片方を選んだら、もう片方の選択肢を押せなくする
        private void UpdatePassTerminalItems()
        {
            if (CmbArrival == null || CmbDeparture == null) return;
            if (CmbDeparture.Items.Count > 1 && CmbDeparture.Items[1] is ComboBoxItem terminalItem)
                terminalItem.IsEnabled = CmbArrival.SelectedIndex != 1;
            if (CmbArrival.Items.Count > 1 && CmbArrival.Items[1] is ComboBoxItem passItem)
                passItem.IsEnabled = CmbDeparture.SelectedIndex != 1;
        }

        // 到着時刻を入れたとき、既定の時刻が空なら同じ時刻を入れる
        private void TxtArrival_LostFocus(object sender, RoutedEventArgs e)
        {
            string t = TxtArrival.Text.Trim();
            if (TxtDefault.Text.Trim().Length == 0 && IsValidTime(t)) TxtDefault.Text = t;
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            string key = TxtKey.Text.Trim();
            string? keyProblem = AddListItemDialog.GetKeyProblem(key);
            if (keyProblem != null) { Warn(keyProblem, TxtKey); return; }

            string name = TxtName.Text.Trim();
            if (HasBadTextChar(name)) { Warn("駅名に使えない文字（, \" やタブ・改行）があります。", TxtName); return; }

            // 到着時刻: 時刻 / p / 空
            string arrival = CmbArrival.SelectedIndex switch { 1 => "p", 2 => string.Empty, _ => TxtArrival.Text.Trim() };
            if (CmbArrival.SelectedIndex == 0 && arrival.Length > 0 && !IsValidTime(arrival))
            { Warn("到着時刻は 13:21:00 のような形で入力してください（秒は省けます。通過なら「通過（p）」を選びます）。", TxtArrival); return; }

            // 発車時刻: 時刻 / t / 空
            string departure = CmbDeparture.SelectedIndex switch { 1 => "t", 2 => string.Empty, _ => TxtDeparture.Text.Trim() };
            if (CmbDeparture.SelectedIndex == 0 && departure.Length > 0 && !IsValidTime(departure))
            { Warn("発車時刻は 13:21:20 のような形で入力してください（秒は省けます。終点なら「終点（t）」を選びます）。", TxtDeparture); return; }

            // ★ (2026-11-17) 通過（p）と終点（t）は同時に入れられない
            if (arrival == "p" && departure == "t")
            { Warn("通過（p）と終点（t）は同時に選べません。到着時刻か発車時刻のどちらかを変えてください。", CmbDeparture); return; }

            string stop = TxtStop.Text.Trim();
            if (!IsValidNumber(stop)) { Warn("停車時間は 0 以上の数で入力してください（秒）。", TxtStop); return; }

            string def = TxtDefault.Text.Trim();
            if (def.Length > 0 && !IsValidTime(def)) { Warn("既定の時刻は 13:21:00 のような形で入力してください（空でもかまいません）。", TxtDefault); return; }

            // ★ (2026-11-17) 出発信号は 0（進行）か 1（停止）だけ。選ぶ形なのでほかの値は入らない
            string signal = CmbSignal.SelectedIndex == 1 ? "1" : "0";
            string alight = TxtAlight.Text.Trim();
            if (!IsValidNumber(alight)) { Warn("降車時間は 0 以上の数（秒）で入力してください（「詳しく」の中）。", TxtAlight); return; }
            string passengers = TxtPassengers.Text.Trim();
            if (!IsValidPassengers(passengers)) { Warn("乗車率は 0〜4 の数で入力してください（1 = 100%、4 = 400%。「詳しく」の中）。", TxtPassengers); return; }
            string arrSound = CmbArrivalSound.Text.Trim();
            string depSound = CmbDepartureSound.Text.Trim();
            if (HasBadTextChar(arrSound) || HasBadTextChar(depSound)) { Warn("音のキーに使えない文字（, \" やタブ・改行）があります（「詳しく」の中）。", null); return; }
            string reopen = TxtReopen.Text.Trim();
            if (!IsValidNumber(reopen)) { Warn("ドアの再開閉は 0 以上の数で入力してください（1 = 100%。「詳しく」の中）。", TxtReopen); return; }
            string stuck = TxtStuck.Text.Trim();
            if (!IsValidNumber(stuck)) { Warn("駆け込みは 0 以上の数（秒）で入力してください（「詳しく」の中）。", TxtStuck); return; }

            Key = key;
            Line = string.Join(",", key, name, arrival, departure, stop, def, signal, alight, passengers, arrSound, depSound, reopen, stuck);
            DialogResult = true;
        }

        private void Warn(string message, Control? focus)
        {
            MessageBox.Show(this, message, Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            focus?.Focus();
        }
    }
}
