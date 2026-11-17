using BveMapParserWorker;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace BveMapEditor
{
    // ★ (2026-11-17) ④ Load 系・include のないファイルを、修正の候補から作る
    //    「指定されたファイルが見つかりません: '…'」「インクルード先のファイルが見つかりません: '…'」のエラーで
    //    「「○○」を作る…」を出し、確認してからリストのヘッダーだけ（include は空）のファイルを作ってタブで開く
    //    作る場所はルートのマップのフォルダの中だけ（ユーザーの決定）。すでにあるファイルは上書きしない
    //    MainWindow.xaml.cs に統合されるファイル
    public partial class MainWindow : Window
    {
        private static readonly Regex MissingFileErrorRegex =
            new(@"^(指定されたファイルが見つかりません|インクルード先のファイルが見つかりません): '(?<p>[^']+)'", RegexOptions.CultureInvariant);

        // 作るファイルの中身（Load の種類 → 1 行目のヘッダー）。駅は 2 行目に列の名前のコメントも入れる（ユーザーの決定）
        private static readonly Dictionary<string, string[]> NewListFileContents = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Structure"] = new[] { "BveTs Structure List 2.00" },
            ["Sound"] = new[] { "BveTs Sound List 2.00" },
            ["Sound3D"] = new[] { "BveTs Sound List 2.00" },
            ["Station"] = new[] { "BveTs Station List 2.00", "#stationKey,stationText,arrivalTime,depertureTime,stoppageTime,defaultTime,signalFlag,alightingTime,passengers,arrivalSoundKey,depertureSoundKey,doorReopen,stuckInDoor" },
            ["Signal"] = new[] { "BveTs Signal Aspects List 2.00" },
        };

        /// <summary>
        /// 修正の候補に「「○○」を作る…」を足す（作れる形のときだけ）
        /// </summary>
        private void AddCreateMissingFileSuggestion(ParseError err, List<ErrorFixSuggestion> list)
        {
            var m = MissingFileErrorRegex.Match(err.Message ?? string.Empty);
            if (!m.Success) return;
            string rel = m.Groups["p"].Value;
            if (rel.Contains('$')) return; // 変数を使ったパスは作らない
            bool isInclude = m.Groups[1].Value.StartsWith("インクルード", StringComparison.Ordinal);

            string? kind = null;
            if (!isInclude)
            {
                // どの Load かは、エラーの行の文字から決める（Structure / Sound / Sound3D / Station / Signal だけ）
                string? lineText = GetErrorLineText(err);
                if (lineText == null) return;
                kind = FindLoadKindForPath(lineText, rel);
                if (kind == null) return;
            }
            string k = kind ?? string.Empty;
            list.Add(new ErrorFixSuggestion($"「{Path.GetFileName(rel)}」を作る…", null)
            {
                Action = () => CreateMissingFileAsync(err, rel, isInclude ? null : k),
            });
        }

        /// <summary>
        /// 行の文字から、rel を読み込んでいる Load の種類を返す（Structure / Sound / Sound3D / Station / Signal。ほかは null）
        /// </summary>
        public static string? FindLoadKindForPath(string lineText, string rel)
        {
            var m = Regex.Match(lineText,
                @"\b(?<k>Structure|Sound3D|Sound|Station|Signal)\s*\.\s*Load\s*\(\s*['""]" + Regex.Escape(rel) + @"['""]",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            if (!m.Success) return null;
            string k = m.Groups["k"].Value;
            return NewListFileContents.Keys.FirstOrDefault(x => x.Equals(k, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 作ってよいかを確かめる。だめなら理由、よければ null（fullPath に作る場所）
        /// rootMapFolder: ルートのマップのフォルダ（この中だけ作れる） / baseFolder: その行が書かれているファイルのフォルダ / rel: マップに書かれたパス
        /// </summary>
        public static string? GetNewFileProblem(string rootMapFolder, string baseFolder, string rel, out string fullPath)
        {
            fullPath = string.Empty;
            if (string.IsNullOrWhiteSpace(rel)) return "ファイルのパスが空です。";
            if (rel.Contains('$')) return "変数（$）を使ったパスのファイルは作れません。";
            if (Path.IsPathRooted(rel)) return "ドライブから始まるパス（絶対パス）のファイルは作れません。マップのフォルダから見た場所で書いてください。";
            string ext = Path.GetExtension(rel).ToLowerInvariant();
            if (ext != ".csv" && ext != ".txt") return "作れるのは .csv と .txt のファイルだけです。";
            if (string.IsNullOrEmpty(rootMapFolder) || string.IsNullOrEmpty(baseFolder)) return "マップを保存してから作ってください（マップのフォルダが分かりません）。";
            try
            {
                fullPath = Path.GetFullPath(Path.Combine(baseFolder, rel));
            }
            catch
            {
                return "ファイルのパスの形が正しくありません。";
            }
            if (fullPath.Length >= 250) return "パスが長すぎます。";
            string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootMapFolder));
            if (!fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return $"ルートのマップのフォルダの外には作れません。\n（作れる場所: {root} の中）";
            if (IsForbiddenSystemPath(fullPath)) return "Windows や Program Files などのシステムのフォルダの中には作れません。";
            if (IsProtectedFile(Path.GetFileName(fullPath))) return "この拡張子のファイルは作れません。";
            // ルートのマップのフォルダから下の、フォルダ名・ファイル名を 1 つずつ確かめる（使えない文字・予約名など）
            string relFromRoot = fullPath.Substring(root.Length + 1);
            foreach (string part in relFromRoot.Split(Path.DirectorySeparatorChar))
            {
                string? reason = GetInvalidFileNameReason(part);
                if (reason != null) return $"「{part}」は名前に使えません。\n{reason}";
            }
            return null;
        }

        // 修正の候補「「○○」を作る…」を選んだとき
        private async Task CreateMissingFileAsync(ParseError err, string rel, string? kind)
        {
            const string title = "ファイルを作る";
            try
            {
                App.LogOperation("ないファイルを作る");
                string? activePath = GetTabInfo(EditorTabControl?.SelectedItem as TabItem)?.FilePath ?? currentFilePath;
                string rootPath = GetRootFilePath(activePath);
                string? sourcePath = err.FileName;
                if (!string.IsNullOrEmpty(sourcePath) && !Path.IsPathRooted(sourcePath) && !string.IsNullOrEmpty(activePath))
                    sourcePath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(activePath) ?? string.Empty, sourcePath));
                if (string.IsNullOrEmpty(sourcePath)) sourcePath = rootPath; // ファイル名が空のエラーはルートファイルのもの

                string rootFolder = string.IsNullOrEmpty(rootPath) ? string.Empty : Path.GetDirectoryName(rootPath) ?? string.Empty;
                string baseFolder = string.IsNullOrEmpty(sourcePath) ? string.Empty : Path.GetDirectoryName(sourcePath) ?? string.Empty;
                string? problem = GetNewFileProblem(rootFolder, baseFolder, rel, out string fullPath);
                if (problem != null)
                {
                    MessageBox.Show(this, problem, title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // すでにあるときは上書きしない（開くか聞く）
                if (File.Exists(fullPath))
                {
                    var r = MessageBox.Show(this, $"このファイルはすでにあります。\n{fullPath}\n\n開きますか？", title, MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (r == MessageBoxResult.Yes) await OpenFileAsync(fullPath);
                    return;
                }
                if (Directory.Exists(fullPath))
                {
                    MessageBox.Show(this, $"同じ名前のフォルダがあるため作れません。\n{fullPath}", title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 途中のフォルダがなければ、作るか聞く
                string folder = Path.GetDirectoryName(fullPath) ?? string.Empty;
                bool needFolder = !Directory.Exists(folder);
                if (needFolder)
                {
                    var r = MessageBox.Show(this, $"フォルダがありません。\n{folder}\n\nフォルダも作りますか？", title, MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (r != MessageBoxResult.Yes) return;
                }

                // 作る前に、場所と中身を見せて確認する
                string[] lines = kind != null && NewListFileContents.TryGetValue(kind, out var c) ? c : Array.Empty<string>();
                string preview = lines.Length == 0 ? "（空のファイル）" : string.Join("\n", lines);
                var ok = MessageBox.Show(this,
                    $"次のファイルを作ります。\n{fullPath}\n\n中身:\n{preview}\n\n（文字コード UTF-8（BOM なし）。作った後はタブで開きます）",
                    title, MessageBoxButton.OKCancel, MessageBoxImage.Question);
                if (ok != MessageBoxResult.OK) return;

                if (needFolder) Directory.CreateDirectory(folder);
                string content = lines.Length == 0 ? string.Empty : string.Join("\r\n", lines) + "\r\n";
                // CreateNew: 確認している間に同じ名前のファイルができていたら、上書きせずに失敗させる
                using (var fs = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write))
                using (var sw = new StreamWriter(fs, new UTF8Encoding(false)))
                {
                    sw.Write(content);
                }
                App.LogOperation("ないファイルを作った");
                await OpenFileAsync(fullPath);
                StatusText.Text = $"作りました: {Path.GetFileName(fullPath)}";
                RunSyntaxCheck(true);
            }
            catch (IOException ex) when (ex.HResult == unchecked((int)0x80070050)) // ファイルがすでにある
            {
                MessageBox.Show(this, "同じ名前のファイルができていたため、作りませんでした（上書きはしません）。", title, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"ファイルを作れませんでした。\n{ex.Message}", title, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
