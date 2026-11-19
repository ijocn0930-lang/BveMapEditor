using BveMapParserWorker;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace BveMapEditor
{
    // ★ (2026-11-19) MainWindow.xaml.cs に統合されるファイル
    //    エラー一覧の右クリック「この種類を表示しない」と、「設定」➔「表示しない警告」（元に戻す）
    //    隠すのはエディタの表示だけ（ワーカーは今まで通り出す）。エラー一覧と波線の両方から消える
    //    BVE で止まるものを隠さないよう、隠せるのは警告・情報・注意だけ。種類は HideableMessageKinds に足していく
    public partial class MainWindow
    {
        // 隠せる種類（Id は settings.json に書く名前。Name はメニューに出す名前。Pattern はエラーの文で見分ける）
        //    文の頭に「[ファイル名] 」が付いていても見分けられるように、文の途中でも探す
        private static readonly (string Id, string Name, Regex Pattern)[] HideableMessageKinds =
        {
            ("BackgroundChangeEmptyKey", "Background.Change の引数に背景キーが指定されていません",
                new Regex(@"Background\.Change の引数に背景キーが指定されていません", RegexOptions.Compiled)),
            ("ConsecutiveSemicolons", "セミコロン ';' が複数連続して記述されています",
                new Regex(@"セミコロン '.*' が複数連続して記述されています", RegexOptions.Compiled)),
        };

        // 隠してよいレベルか（エラーは隠さない）
        private static bool IsHideableLevel(string? level) =>
            level == "警告" || level == "情報" || level == "注意";

        // このエラーが隠せる種類なら、その種類を返す（なければ null）
        private static (string Id, string Name, Regex Pattern)? FindHideableKind(ParseError? error)
        {
            if (error == null || !IsHideableLevel(error.Level) || string.IsNullOrEmpty(error.Message)) return null;
            foreach (var kind in HideableMessageKinds)
            {
                if (kind.Pattern.IsMatch(error.Message)) return kind;
            }
            return null;
        }

        // 「表示しない」にした種類のエラーを除いた一覧を返す（何も隠していなければ、そのまま返す）
        // ★ (2026-11-19) BveEx が入っているパソコンでは、「注意: BveEx（拡張構文）が使用されています」も出さない
        private List<ParseError> FilterHiddenMessages(List<ParseError> errors)
        {
            var hidden = currentSettings.HiddenMessageKinds;
            bool hideBveExNotice = IsBveExInstalled();
            if ((hidden == null || hidden.Count == 0) && !hideBveExNotice) return errors;
            return errors.Where(e =>
            {
                if (hideBveExNotice && IsBveExNotice(e)) return false;
                if (hidden == null || hidden.Count == 0) return true;
                var kind = FindHideableKind(e);
                return kind == null || !hidden.Contains(kind.Value.Id, StringComparer.Ordinal);
            }).ToList();
        }

        // ===== ★ (2026-11-19) BveEx が入っているか =====
        // 入っているとみなす（ユーザーの決定: 全部そろったら）:
        //   「公開のドキュメント\BveEx\2.0」に BveEx.dll と BveTypes.dll の両方がある
        //   かつ、次のどれかの「Input Devices」に BveEx.Caller.InputDevice.dll がある:
        //     Program Files (x86)\mackoy\BveTs5・Program Files\mackoy\BveTs6・詳細設定の BVE の実行ファイルのフォルダ（BVE を別の場所に入れている人のため）
        //   AtsEx は見ない（BveEx になる前の古いもの）。7-zip 版（ポータブル）は考えない
        // 調べた結果は、詳細設定の BVE の場所が変わるまで覚えておく（構文チェックのたびにファイルを探さない）
        private bool? _bveExInstalled;
        private string? _bveExCheckedBvePath;

        private bool IsBveExInstalled()
        {
            string bvePath = currentSettings.BveExecutablePath ?? string.Empty;
            if (_bveExInstalled.HasValue && string.Equals(_bveExCheckedBvePath, bvePath, StringComparison.OrdinalIgnoreCase))
                return _bveExInstalled.Value;
            bool installed = false;
            try
            {
                string publicDocs = Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments);
                string bveExDir = Path.Combine(publicDocs, "BveEx", "2.0");
                bool coreFiles = !string.IsNullOrEmpty(publicDocs) &&
                                 File.Exists(Path.Combine(bveExDir, "BveEx.dll")) &&
                                 File.Exists(Path.Combine(bveExDir, "BveTypes.dll"));
                if (coreFiles)
                {
                    var bveDirs = new List<string>();
                    string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                    string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                    if (!string.IsNullOrEmpty(pf86)) bveDirs.Add(Path.Combine(pf86, "mackoy", "BveTs5"));
                    if (!string.IsNullOrEmpty(pf)) bveDirs.Add(Path.Combine(pf, "mackoy", "BveTs6"));
                    if (!string.IsNullOrWhiteSpace(bvePath))
                    {
                        string? dir = Path.GetDirectoryName(bvePath.Trim().Trim('"'));
                        if (!string.IsNullOrEmpty(dir)) bveDirs.Add(dir);
                    }
                    installed = bveDirs.Any(d => File.Exists(Path.Combine(d, "Input Devices", "BveEx.Caller.InputDevice.dll")));
                }
            }
            catch
            {
                installed = false; // 調べられないときは、今まで通り注意を出す
            }
            _bveExInstalled = installed;
            _bveExCheckedBvePath = bvePath;
            return installed;
        }

        // 「注意: BveEx（拡張構文）が使用されています」か（文の頭に「[ファイル名] 」が付いていてもよい）
        private static bool IsBveExNotice(ParseError e) =>
            e.Level == "注意" && (e.Message ?? string.Empty).Contains("BveEx（拡張構文）が使用されています", StringComparison.Ordinal);

        // 右クリックメニューを開くときに、「この種類を表示しない」を押せるかどうかと名前を決める
        private void UpdateHideKindMenu(ParseError? item)
        {
            var kind = FindHideableKind(item);
            MenuErrorHideKind.IsEnabled = kind != null;
            MenuErrorHideKind.Header = kind != null
                ? $"この種類を表示しない（{kind.Value.Name}）"
                : "この種類を表示しない（この行の種類は隠せません）";
        }

        // エラー一覧の右クリック「この種類を表示しない」
        private void ErrorMenuHideKind_Click(object sender, RoutedEventArgs e)
        {
            var kind = FindHideableKind(ErrorListDataGrid.SelectedItem as ParseError);
            if (kind == null) return;
            var r = MessageBox.Show(this,
                $"「{kind.Value.Name}」を、エラー一覧と波線に表示しないようにしますか？\n\n元に戻すときは「設定」➔「表示しない警告」から戻せます。",
                "この種類を表示しない", MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (r != MessageBoxResult.OK) return;
            currentSettings.HiddenMessageKinds ??= new List<string>();
            if (!currentSettings.HiddenMessageKinds.Contains(kind.Value.Id, StringComparer.Ordinal))
            {
                currentSettings.HiddenMessageKinds.Add(kind.Value.Id);
                currentSettings.Save();
            }
            RunSyntaxCheck(isImmediate: true); // 表示を作り直す
        }

        // 「設定」➔「表示しない警告」を開いたときに、今隠している種類を並べる（押すと表示に戻す）
        private void MenuHiddenMessages_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem parent || !ReferenceEquals(e.OriginalSource, parent)) return;
            parent.Items.Clear();
            var hidden = currentSettings.HiddenMessageKinds ?? new List<string>();
            var shown = HideableMessageKinds.Where(k => hidden.Contains(k.Id, StringComparer.Ordinal)).ToList();
            if (shown.Count == 0)
            {
                parent.Items.Add(new MenuItem { Header = "（表示しないものはありません）", IsEnabled = false });
                return;
            }
            foreach (var kind in shown)
            {
                var item = new MenuItem { Header = $"「{kind.Name}」を表示に戻す", Tag = kind.Id };
                item.Click += (_, _) => ShowHiddenKindAgain(kind.Id);
                parent.Items.Add(item);
            }
            if (shown.Count > 1)
            {
                parent.Items.Add(new Separator());
                var all = new MenuItem { Header = "すべて表示に戻す" };
                all.Click += (_, _) => ShowHiddenKindAgain(null);
                parent.Items.Add(all);
            }
        }

        // id の種類を表示に戻す（null なら全部）
        private void ShowHiddenKindAgain(string? id)
        {
            if (currentSettings.HiddenMessageKinds == null) return;
            if (id == null) currentSettings.HiddenMessageKinds.Clear();
            else currentSettings.HiddenMessageKinds.RemoveAll(x => string.Equals(x, id, StringComparison.Ordinal));
            currentSettings.Save();
            RunSyntaxCheck(isImmediate: true); // 表示を作り直す
        }
    }
}
