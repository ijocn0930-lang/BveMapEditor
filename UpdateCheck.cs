using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;

namespace BveMapEditor
{
    // ★ (2026-11-19) MainWindow.xaml.cs に統合されるファイル
    //    「ヘルプ」➔「アップデートを確認…」: 押したときだけ GitHub の Releases の最新版を聞き、今のバージョンと比べる
    //    ダウンロード・入れ替えは自動ではしない（新しい版があれば、はいでブラウザで Releases のページを開くだけ）
    //    安全のため: 聞く先は api.github.com だけ（https）。5 秒で打ち切る。返ってきた中からタグとページの場所だけを使う。
    //    開くページは ReleasePageUrlPrefix で始まるときだけ（違えば決まった Releases のページを開く）。送るのはアプリの名前だけ
    public partial class MainWindow
    {
        private const string LatestReleaseApiUrl = "https://api.github.com/repos/ijocn0930-lang/BveMapEditor/releases/latest";
        private const string ReleasePageUrlPrefix = "https://github.com/ijocn0930-lang/BveMapEditor/";
        private const string ReleasesPageUrl = "https://github.com/ijocn0930-lang/BveMapEditor/releases";

        // 通信は 1 つを使い回す（.NET のおすすめ）。5 秒で打ち切る
        private static readonly HttpClient UpdateHttpClient = CreateUpdateHttpClient();

        private static HttpClient CreateUpdateHttpClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            // GitHub の決まりで、アプリの名前（User-Agent）が要る。個人の情報は入れない
            client.DefaultRequestHeaders.UserAgent.ParseAdd("BveMapEditor");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            return client;
        }

        // 今のバージョン（BveMapEditor.csproj の Version。例: 1.1.0）
        internal static Version GetAppVersion()
        {
            var v = typeof(MainWindow).Assembly.GetName().Version ?? new Version(0, 0, 0);
            return new Version(v.Major, v.Minor, Math.Max(0, v.Build));
        }

        internal static string GetAppVersionText() => GetAppVersion().ToString(3);

        // GitHub のタグ（例: "1.2.0" / "v1.2.0"）をバージョンにする。だめなら false
        internal static bool TryParseReleaseVersion(string? tag, out Version version)
        {
            version = new Version(0, 0, 0);
            if (string.IsNullOrWhiteSpace(tag)) return false;
            string t = tag.Trim();
            if (t.StartsWith("v", StringComparison.OrdinalIgnoreCase)) t = t.Substring(1);
            int cut = t.IndexOfAny(new[] { '-', '+', ' ' }); // 1.2.0-beta などの後ろは見ない
            if (cut >= 0) t = t.Substring(0, cut);
            if (!Version.TryParse(t.Contains('.') ? t : t + ".0", out var parsed)) return false;
            version = new Version(parsed.Major, parsed.Minor, Math.Max(0, parsed.Build));
            return true;
        }

        // GitHub の返事（JSON）からタグとページの場所だけを取り出す
        internal static (string? Tag, string? Url) ParseLatestReleaseJson(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            string? tag = root.TryGetProperty("tag_name", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null;
            string? url = root.TryGetProperty("html_url", out var u) && u.ValueKind == JsonValueKind.String ? u.GetString() : null;
            return (tag, url);
        }

        // 開いてよいページか（このアプリの GitHub のページだけ）
        internal static string SafeReleasePageUrl(string? url) =>
            !string.IsNullOrEmpty(url) && url.StartsWith(ReleasePageUrlPrefix, StringComparison.OrdinalIgnoreCase) ? url : ReleasesPageUrl;

        private bool _isCheckingUpdate;

        private async void MenuCheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (_isCheckingUpdate) return;
            _isCheckingUpdate = true;
            App.LogOperation("アップデートの確認がクリックされました。");
            var oldCursor = Mouse.OverrideCursor;
            Mouse.OverrideCursor = Cursors.Wait;
            string current = GetAppVersionText();
            string? message = null;
            MessageBoxImage icon = MessageBoxImage.Information;
            string? newVersionTag = null, pageUrl = null;
            try
            {
                using var response = await UpdateHttpClient.GetAsync(LatestReleaseApiUrl);
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    message = $"GitHub にまだリリースがありません。\n\n今のバージョン: {current}";
                }
                else if (!response.IsSuccessStatusCode)
                {
                    message = $"アップデートを確認できませんでした（GitHub の返事: {(int)response.StatusCode}）。\nしばらくしてからもう一度お試しください。";
                    icon = MessageBoxImage.Warning;
                }
                else
                {
                    // 大きすぎる返事は読まない（ふつうは数 KB）
                    if (response.Content.Headers.ContentLength is long len && len > 1024 * 1024) throw new InvalidDataException("返事が大きすぎます");
                    string json = await response.Content.ReadAsStringAsync();
                    var (tag, url) = ParseLatestReleaseJson(json);
                    if (!TryParseReleaseVersion(tag, out var latest))
                    {
                        message = $"GitHub の最新版のバージョン（{tag}）を読めませんでした。\n\n今のバージョン: {current}";
                        icon = MessageBoxImage.Warning;
                    }
                    else if (latest > GetAppVersion())
                    {
                        newVersionTag = latest.ToString(3);
                        pageUrl = SafeReleasePageUrl(url);
                    }
                    else
                    {
                        message = $"最新のバージョンです（{current}）。";
                    }
                }
            }
            catch (TaskCanceledException)
            {
                message = "アップデートを確認できませんでした（5 秒たっても GitHub から返事がありません）。\nインターネットにつながっているか確かめてください。";
                icon = MessageBoxImage.Warning;
            }
            catch (HttpRequestException)
            {
                message = "アップデートを確認できませんでした（GitHub につながりません）。\nインターネットにつながっているか確かめてください。";
                icon = MessageBoxImage.Warning;
            }
            catch (Exception ex)
            {
                message = $"アップデートを確認できませんでした（{ex.GetType().Name}）。";
                icon = MessageBoxImage.Warning;
                App.LogOperation($"アップデートの確認に失敗: {ex.Message}");
            }
            finally
            {
                Mouse.OverrideCursor = oldCursor;
                _isCheckingUpdate = false;
            }

            if (newVersionTag != null && pageUrl != null)
            {
                var r = MessageBox.Show(this,
                    $"新しいバージョン {newVersionTag} があります（今は {current}）。\nダウンロードのページを開きますか？",
                    "アップデートを確認", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (r == MessageBoxResult.Yes)
                {
                    try { Process.Start(new ProcessStartInfo(pageUrl) { UseShellExecute = true }); }
                    catch (Exception ex) { MessageBox.Show(this, $"ページを開けませんでした: {ex.Message}", "アップデートを確認", MessageBoxButton.OK, MessageBoxImage.Warning); }
                }
                return;
            }
            MessageBox.Show(this, message ?? "アップデートを確認できませんでした。", "アップデートを確認", MessageBoxButton.OK, icon);
        }
    }
}
