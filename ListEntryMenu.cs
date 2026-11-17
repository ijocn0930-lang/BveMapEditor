using ICSharpCode.AvalonEdit;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace BveMapEditor
{
    // ★ (2026-11-17) マップのタブの右クリックメニューから、リストファイル（ストラクチャーなど）に 1 行足す
    //    リストファイルをタブで開いて（開いていればそのタブ）、最後に 1 行足すだけ。保存はしない（Ctrl+Z で戻せる）
    //    どのリストファイルかは、構文チェックのときにワーカーが返す "$ListFiles"（BveSemanticAnalyzer.ListFilesKey）で決める
    //    MainWindow.xaml.cs に統合されるファイル
    public partial class MainWindow : Window
    {
        private const string AddListMenuTag = "AddListMenu";

        /// <summary>
        /// エディタの右クリックメニューの最後に「ストラクチャーを追加…」「サウンドを追加…」「サウンド3Dを追加…」を出す（マップのタブだけ）
        /// </summary>
        private void AddListEntryMenuItems(ContextMenu menu, TextEditor editor)
        {
            foreach (var old in menu.Items.OfType<FrameworkElement>().Where(x => AddListMenuTag.Equals(x.Tag)).ToList())
            {
                menu.Items.Remove(old);
            }
            var info = FindTabByEditor(editor);
            if (IsNonMapText(info?.FilePath, editor.Text)) return; // リストファイルなど、マップでないタブには出さない

            menu.Items.Add(new Separator { Tag = AddListMenuTag });
            // ★ (2026-11-17) ② サウンド・サウンド3D も足した（同じダイアログ。サウンド3D は「同時に鳴らせる数」の欄を出す）
            foreach (var kind in AddListKinds)
            {
                string? listPath = GetListFilePath(editor, kind.Kind);
                var item = new MenuItem { Header = kind.Title + "…", Tag = AddListMenuTag };
                if (listPath == null)
                {
                    item.IsEnabled = false;
                    item.Header = $"{kind.Title}…（{kind.Kind}.Load がないか、構文チェックがまだです）";
                }
                else
                {
                    item.ToolTip = listPath;
                    var captured = kind;
                    item.Click += async (s, e) => await AddItemToListAsync(captured, listPath);
                }
                menu.Items.Add(item);
            }

            // ★ (2026-11-17) ③ 駅を追加（専用のダイアログ AddStationDialog。13 列）
            string? stationList = GetListFilePath(editor, "Station");
            var stationItem = new MenuItem { Header = "駅を追加…", Tag = AddListMenuTag };
            if (stationList == null)
            {
                stationItem.IsEnabled = false;
                stationItem.Header = "駅を追加…（Station.Load がないか、構文チェックがまだです）";
            }
            else
            {
                stationItem.ToolTip = stationList;
                var soundKeys = GetKeyList(editor, "Sound");
                stationItem.Click += async (s, e) => await AddStationToListAsync(stationList, soundKeys);
            }
            menu.Items.Add(stationItem);
        }

        // ステーションリストに駅の 1 行を足す
        private async Task AddStationToListAsync(string listPath, List<string>? soundKeys, string? initialKey = null)
        {
            const string title = "駅を追加";
            try
            {
                App.LogOperation(title);
                // 最後の駅の行を初めの値に使うため、リストの本文を読む（タブで開いていればその中身）
                string listText = FindTabByFilePath(listPath)?.Editor?.Text ?? ReadListFileText(listPath);
                var dlg = new AddStationDialog(listPath, listText, soundKeys, initialKey) { Owner = this };
                if (dlg.ShowDialog() != true) return;

                await AppendLineToListAsync(listPath, dlg.Key, string.Empty, dlg.Line);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"リストに追加できませんでした。\n{ex.Message}", title, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // リストファイルを、開くときと同じ文字コードの判定で読む（読めない・大きすぎるときは空）
        private static string ReadListFileText(string path)
        {
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists || info.Length > 20L * 1024 * 1024) return string.Empty;
                byte[] bytes = File.ReadAllBytes(path);
                return RemoveLeadingBom(DetectEncodingFromSample(bytes).GetString(bytes));
            }
            catch
            {
                return string.Empty;
            }
        }

        // 右クリックに出す「○○を追加」の種類（Kind: Load の名前空間 / Title: メニューと窓の題名 / Filter: 「参照…」のファイルの種類 / ShowCount: 3 列目「同時に鳴らせる数」）
        private sealed record AddListKind(string Kind, string Title, string Filter, bool ShowCount);

        // ★ (2026-11-17) BVE5 / BVE6 の音は .wav だけ（.ogg .mp3 は読めない。ユーザーより）
        private const string SoundFileFilter = "WAV ファイル (*.wav)|*.wav";

        private static readonly AddListKind[] AddListKinds =
        {
            new("Structure", "ストラクチャーを追加", "X ファイル (*.x)|*.x|すべてのファイル (*.*)|*.*", false),
            new("Sound", "サウンドを追加", SoundFileFilter, false),
            new("Sound3D", "サウンド3Dを追加", SoundFileFilter, true),
        };

        /// <summary>
        /// 構文チェックでワーカーが返したリストファイルの場所（kind: Structure / Sound / Sound3D / Station / Signal）。
        /// 分からない・ファイルがないときは null
        /// </summary>
        private string? GetListFilePath(TextEditor editor, string kind)
            => FindListFilePath(GetKeyList(editor, BveMapParserWorker.BveSemanticAnalyzer.ListFilesKey), kind);

        // "$ListFiles" の一覧（種類<TAB>場所）から、その種類のリストファイルの場所を探す（なければ null）
        private static string? FindListFilePath(List<string>? entries, string kind)
        {
            if (entries == null) return null;
            foreach (var entry in entries)
            {
                int tab = entry.IndexOf('\t');
                if (tab <= 0) continue;
                if (!entry.Substring(0, tab).Equals(kind, StringComparison.OrdinalIgnoreCase)) continue;
                string path = entry.Substring(tab + 1);
                if (Path.IsPathRooted(path) && File.Exists(path)) return path;
            }
            return null;
        }

        // リストに「キー,ファイル」（サウンド3D で数を入れたら「キー,ファイル,数」）を足す
        private async Task AddItemToListAsync(AddListKind kind, string listPath, string? initialKey = null)
        {
            try
            {
                App.LogOperation(kind.Title);
                var dlg = new AddListItemDialog(kind.Title, listPath, kind.Filter, kind.ShowCount,
                    kind.Kind.StartsWith("Sound", StringComparison.OrdinalIgnoreCase) ? ".wav" : null, initialKey) { Owner = this };
                if (dlg.ShowDialog() != true) return;

                string line = $"{dlg.Key},{dlg.RelativeFilePath}";
                if (dlg.Count.Length > 0) line += "," + dlg.Count;
                await AppendLineToListAsync(listPath, dlg.Key, dlg.RelativeFilePath, line);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"リストに追加できませんでした。\n{ex.Message}", kind.Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// ★ (2026-11-17) 修正の候補「○○リストに追加…」から: エラーのキーを入れた状態で、その種類の追加のダイアログを開く
        /// kind: Structure / Sound / Sound3D / Station。listPath はエラーのマップが読み込んでいるリストファイル
        /// </summary>
        private async Task OpenAddListDialogForKeyAsync(string kind, string listPath, string key)
        {
            if (kind.Equals("Station", StringComparison.OrdinalIgnoreCase))
            {
                await AddStationToListAsync(listPath, GetKeyListForErrorFix("Sound"), key);
                return;
            }
            var addKind = AddListKinds.FirstOrDefault(k => k.Kind.Equals(kind, StringComparison.OrdinalIgnoreCase));
            if (addKind != null) await AddItemToListAsync(addKind, listPath, key);
        }

        // 修正の候補のメニューに出す、リストの種類の呼び名
        private static string ListKindName(string kind) => kind switch
        {
            "Structure" => "ストラクチャーリスト",
            "Sound" => "サウンドリスト",
            "Sound3D" => "サウンド3D リスト",
            "Station" => "ステーションリスト",
            _ => kind + " リスト",
        };

        /// <summary>
        /// リストファイルをタブで開き（開いていればそのタブ）、すでにあるキー・ファイルなら確認してから、最後に line を 1 行足す
        /// </summary>
        private async Task AppendLineToListAsync(string listPath, string key, string relativeFile, string line)
        {
            var tabInfo = FindTabByFilePath(listPath);
            if (tabInfo == null)
            {
                await OpenFileAsync(listPath);
                tabInfo = FindTabByFilePath(listPath);
            }
            if (tabInfo?.Editor == null || tabInfo.TabItem == null)
            {
                MessageBox.Show(this, $"リストファイルを開けませんでした。\n{listPath}", "リストに追加", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            EditorTabControl.SelectedItem = tabInfo.TabItem;
            var editor = tabInfo.Editor;
            var doc = editor.Document;

            // ★ すでにあるキー・ファイルなら確認する（ユーザーの指示: 重複しているときは確認）
            var (sameKeyLine, sameFileLine) = FindDuplicateInList(doc.Text, key, relativeFile);
            if (sameKeyLine > 0 || sameFileLine > 0)
            {
                string msg = sameKeyLine > 0
                    ? $"キー '{key}' は、すでに {sameKeyLine} 行目にあります。\n（同じキーが 2 つあると、どちらが使われるか分かりにくくなります）"
                    : $"ファイル '{relativeFile}' は、すでに {sameFileLine} 行目で別のキーに登録されています。";
                var r = MessageBox.Show(this, msg + "\n\nそれでも追加しますか？", "リストに追加", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (r != MessageBoxResult.Yes) return;
            }

            // ファイルの改行の書き方に合わせて、最後に 1 行足す（1 回の挿入 = Ctrl+Z 1 回で戻る）
            string text = doc.Text;
            string nl = text.Contains("\r\n") ? "\r\n" : (text.Contains('\n') ? "\n" : "\r\n");
            bool endsWithNewLine = text.Length == 0 || text.EndsWith('\n');
            string insert = endsWithNewLine ? line + nl : nl + line;
            doc.Insert(doc.TextLength, insert);

            int addedLine = endsWithNewLine ? doc.LineCount - 1 : doc.LineCount;
            if (addedLine < 1) addedLine = 1;
            var docLine = doc.GetLineByNumber(addedLine);
            editor.TextArea.Caret.Offset = docLine.EndOffset;
            editor.Select(docLine.Offset, docLine.Length);
            editor.ScrollToLine(addedLine);
            editor.Focus();
            StatusText.Text = $"{Path.GetFileName(listPath)} の {addedLine} 行目に追加しました（まだ保存していません。元に戻すには Ctrl+Z）";
        }

        // 開いているタブから、そのファイルのタブを探す
        private TabDocumentInfo? FindTabByFilePath(string path)
        {
            if (EditorTabControl == null) return null;
            foreach (var item in EditorTabControl.Items)
            {
                if (item is TabItem ti && GetTabInfo(ti) is TabDocumentInfo info
                    && !string.IsNullOrEmpty(info.FilePath)
                    && string.Equals(info.FilePath, path, StringComparison.OrdinalIgnoreCase))
                {
                    return info;
                }
            }
            return null;
        }

        /// <summary>
        /// リストの本文で、同じキー（大文字小文字は区別しない）・同じファイル（2 列目）がある行を探す。なければ 0。
        /// 1 行目（ヘッダー）・空の行・コメント（# / //）は見ない
        /// </summary>
        private static (int SameKeyLine, int SameFileLine) FindDuplicateInList(string text, string key, string relativeFile)
        {
            int sameKey = 0, sameFile = 0;
            string normFile = relativeFile.Replace('/', '\\').Trim();
            var lines = text.Split('\n');
            for (int i = 1; i < lines.Length; i++) // 0 はヘッダー
            {
                string l = lines[i].TrimEnd('\r').Trim();
                if (l.Length == 0 || l.StartsWith("#", StringComparison.Ordinal) || l.StartsWith("//", StringComparison.Ordinal)) continue;
                var cols = l.Split(',');
                if (sameKey == 0 && cols[0].Trim().Equals(key, StringComparison.OrdinalIgnoreCase)) sameKey = i + 1;
                if (sameFile == 0 && normFile.Length > 0 && cols.Length >= 2 && cols[1].Trim().Replace('/', '\\').Equals(normFile, StringComparison.OrdinalIgnoreCase)) sameFile = i + 1;
            }
            return (sameKey, sameFile);
        }
    }
}
