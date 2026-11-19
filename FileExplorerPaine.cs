using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BveMapEditor
{
    public partial class MainWindow : Window
    {
        #region アイコン取得用のWin32API定義
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }
        private const uint SHGFI_ICON = 0x000000100;
        private const uint SHGFI_SMALLICON = 0x000000001;
        private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;

        // ★ (2026-11-17) アイコンを覚えておく（フォルダは 1 つ、ファイルは拡張子ごとに 1 つ）
        //    以前はファイル 1 つごとに Windows に聞いていて、2 万個のフォルダで約 18 秒、画面が止まっていた（応答なし）
        //    .exe .ico など、ファイルごとにアイコンが違うものは今まで通り 1 つずつ聞く
        private static readonly Dictionary<string, ImageSource?> _iconCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> PerFileIconExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".ico", ".lnk", ".url", ".cur", ".ani", ".scr", ".appref-ms",
        };

        private const uint FILE_ATTRIBUTE_DIRECTORY = 0x10;
        private const uint FILE_ATTRIBUTE_NORMAL = 0x80;

        private static ImageSource? GetFileIcon(string filePath, bool isDirectory)
        {
            // ★ (2026-11-17) ドライブの直下（C:\ など）は、ドライブのアイコンなので覚えずにそのまま聞く
            //    （以前は最初に聞いたフォルダのアイコンを全部のフォルダに使っていたため、ドライブの直下を開くと
            //     全部のフォルダがドライブのアイコンになっていた）
            if (isDirectory && IsDriveRoot(filePath)) return GetFileIconCore(filePath, isDirectory, generic: false);

            string? cacheKey = null;
            if (isDirectory)
            {
                cacheKey = "<folder>";
            }
            else
            {
                string ext = Path.GetExtension(filePath);
                if (!PerFileIconExtensions.Contains(ext)) cacheKey = "ext:" + ext;
            }
            if (cacheKey != null && _iconCache.TryGetValue(cacheKey, out var cached)) return cached;

            // 覚えるものは、そのファイル・フォルダのものではなく「ふつうのフォルダ」「その拡張子のふつうのファイル」のアイコンを聞く
            // （最初に聞いたものがたまたま特別なアイコンでも、ほかに広がらないように）
            var icon = GetFileIconCore(filePath, isDirectory, generic: cacheKey != null);
            if (cacheKey != null) _iconCache[cacheKey] = icon;
            return icon;
        }

        // ドライブの直下（C:\ など）なら true
        private static bool IsDriveRoot(string path)
        {
            try
            {
                string full = Path.GetFullPath(path);
                string? root = Path.GetPathRoot(full);
                return !string.IsNullOrEmpty(root)
                    && string.Equals(Path.TrimEndingDirectorySeparator(full), Path.TrimEndingDirectorySeparator(root), StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        /// <param name="generic">true なら、そのパスのファイル・フォルダを見ずに「ふつうのフォルダ」「その拡張子のふつうのファイル」のアイコンを聞く</param>
        private static ImageSource? GetFileIconCore(string filePath, bool isDirectory, bool generic)
        {
            var shfi = new SHFILEINFO();
            uint flags = SHGFI_ICON | SHGFI_SMALLICON;
            uint attributes = 0;
            string queryPath = filePath;
            if (generic)
            {
                flags |= SHGFI_USEFILEATTRIBUTES;
                attributes = isDirectory ? FILE_ATTRIBUTE_DIRECTORY : FILE_ATTRIBUTE_NORMAL;
                queryPath = isDirectory ? "folder" : "file" + Path.GetExtension(filePath);
            }
            else if (!isDirectory && !File.Exists(filePath))
            {
                flags |= SHGFI_USEFILEATTRIBUTES;
            }
            IntPtr result = SHGetFileInfo(queryPath, attributes, ref shfi, (uint)Marshal.SizeOf(shfi), flags);
            if (shfi.hIcon != IntPtr.Zero)
            {
                try
                {
                    ImageSource imageSource = Imaging.CreateBitmapSourceFromHIcon(
                        shfi.hIcon,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    imageSource.Freeze();
                    return imageSource;
                }
                finally
                {
                    DestroyIcon(shfi.hIcon);
                }
            }
            return null;
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr hIcon);
        #endregion

        private string? GetSelectedPath()
        {
            if (FileExplorerTreeView.SelectedItem is TreeViewItem selectedItem && selectedItem.Tag is string path)
            {
                return path;
            }
            return null;
        }
        // ツリービューの項目を右クリックしたときに、自動でその項目を選択状態にする
        private void TreeViewItem_MouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // ★ (2026-11-15) 右クリックした場所にいちばん近い項目を選ぶ
            //    （この処理は MainWindow.xaml の ItemContainerStyle で付けているが、それが効くのは一番上の項目だけ。
            //     以前は sender（＝一番上の項目）を選んでいたため、どの項目を右クリックしても一番上の項目が選ばれていた）
            DependencyObject? d = e.OriginalSource as DependencyObject;
            while (d != null && d is not TreeViewItem)
            {
                d = (d is Visual || d is System.Windows.Media.Media3D.Visual3D)
                    ? VisualTreeHelper.GetParent(d)
                    : LogicalTreeHelper.GetParent(d);
            }
            if ((d as TreeViewItem ?? sender as TreeViewItem) is TreeViewItem item)
            {
                item.IsSelected = true;
                // フォーカスを当ててコンテキストメニューが正しく対象を認識できるようにする
                item.Focus();
                // イベントが親に伝播して意図しない選択解除が起きるのを防ぐ場合
                e.Handled = true;
            }
        }
        // コンテキストメニューが開かれる直前に、ペインが空かどうかをチェックして制御する
        private void FileExplorerTreeView_ContextMenuOpening(object sender, System.Windows.Controls.ContextMenuEventArgs e)
        {
            // エクスプローラーペインが空（またはルートフォルダが未設定）の場合
            if (FileExplorerTreeView.Items.Count == 0 || string.IsNullOrEmpty(_currentRootFolderPath))
            {
                // メニューの表示自体を完全にキャンセルする（これでメニューが出なくなります）
                e.Handled = true;
            }
            else
            {
                // 項目がある場合は通常通りメニューを許可
                ExplorerContextMenu.IsEnabled = true;
            }
        }
        private async void MenuNewTextFile_Click(object sender, RoutedEventArgs e)
        {
            string? path = GetSelectedPath();
            if (string.IsNullOrEmpty(path)) return;

            // 選択されているのがファイルならその親フォルダ、フォルダならその中をターゲットにする
            string targetDir = Directory.Exists(path) ? path : (Path.GetDirectoryName(path) ?? _currentRootFolderPath);
            if (string.IsNullOrEmpty(targetDir)) return;

            // 重複しないファイル名を生成 (例: 新しいテキスト文書.txt, 新しいテキスト文書 (1).txt)
            string baseName = "新しいテキスト文書";
            string ext = ".txt";
            string newPath = Path.Combine(targetDir, baseName + ext);
            int count = 1;

            while (File.Exists(newPath))
            {
                newPath = Path.Combine(targetDir, $"{baseName} ({count}){ext}");
                count++;
            }

            try
            {
                File.WriteAllText(newPath, string.Empty);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"ファイルの作成に失敗しました:\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // ★ (2026-11-17) ファイルの見張り（FileSystemWatcher）を待たずに、すぐツリーを読み込み直して作ったファイルを選ぶ。
            //    続けて名前の変更の窓を出す（Windows のエクスプローラーと同じ。拡張子の前までを選んだ状態。ユーザーの決定）
            try
            {
                await RefreshTreeKeepingStateAsync(newPath);
                RenameItemAtPath(newPath);
            }
            catch (Exception ex)
            {
                App.LogOperation($"新規テキストファイルの後の読み込み直し・名前の変更に失敗: {ex.Message}");
            }
        }

        // ★ (2026-11-18) 新しいフォルダーを作る（新規テキストファイルと同じ流れ。作ったらすぐ表示して、続けて名前の変更の窓を出す）
        private async void MenuNewFolder_Click(object sender, RoutedEventArgs e)
        {
            string? path = GetSelectedPath();
            if (string.IsNullOrEmpty(path)) return;

            // 選択されているのがファイルならその親フォルダ、フォルダならその中に作る
            string targetDir = Directory.Exists(path) ? path : (Path.GetDirectoryName(path) ?? _currentRootFolderPath);
            if (string.IsNullOrEmpty(targetDir)) return;

            // 重複しない名前を決める（例: 新しいフォルダー, 新しいフォルダー (2)。同じ名前のファイルがあっても避ける）
            string baseName = "新しいフォルダー";
            string newPath = Path.Combine(targetDir, baseName);
            int count = 2;
            while (Directory.Exists(newPath) || File.Exists(newPath))
            {
                newPath = Path.Combine(targetDir, $"{baseName} ({count})");
                count++;
            }

            try
            {
                Directory.CreateDirectory(newPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"フォルダーの作成に失敗しました:\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                await RefreshTreeKeepingStateAsync(newPath);
                RenameItemAtPath(newPath);
            }
            catch (Exception ex)
            {
                App.LogOperation($"新しいフォルダーの後の読み込み直し・名前の変更に失敗: {ex.Message}");
            }
        }
        private void ExplorerPaneMenuRename_Click(object sender, RoutedEventArgs e)
        {
            RenameItemAtPath(GetSelectedPath());
        }

        // ★ (2026-11-17) 名前の変更の中身（右クリックの「名前の変更」と、新規テキストファイルを作った直後から使う）
        private void RenameItemAtPath(string? path)
        {
            if (string.IsNullOrEmpty(path) || path.Equals(_currentRootFolderPath, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("ルートフォルダや無効な項目は名前を変更できません。", "操作エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            bool isDir = Directory.Exists(path);
            string currentName = Path.GetFileName(path);
            string parentDir = Path.GetDirectoryName(path) ?? "";

            // ★ (2026-11-15) exe・dll・sys などのシステムにかかわるファイルは名前を変えさせない（ソフトや Windows が動かなくなるのを防ぐ）
            if (!isDir && IsProtectedFile(currentName))
            {
                MessageBox.Show($"このファイルは名前を変えられません。\n（{Path.GetExtension(currentName)} はプログラムやシステムにかかわるファイルのため）\n\nファイル: {currentName}", "名前の変更", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 入力ボックスを表示
            // ★ (2026-11-17) 名前の変更の窓（RenameDialog）にした。ファイルは拡張子の前までを選んだ状態で出し、拡張子を変えたら確認する
            //    （以前の Visual Basic の入力ボックスは全部を選んだ状態で出るため、拡張子まで消してしまうおそれがあった）
            string? entered = RenameDialog.Prompt(this, currentName, isDir);
            if (entered == null) return; // やめた
            string newName = entered.Trim(); // ★ (2026-11-15) 前後の空白は取る（Windows と同じ）
            if (string.IsNullOrWhiteSpace(newName) || newName == currentName) return;

            // ★ (2026-11-15) ファイル名に使えない名前は止める
            //    （以前は「\」や「..」を入れると別のフォルダに移動してしまい、: * ? などでは分かりにくいエラーになっていた）
            string? nameProblem = GetInvalidFileNameReason(newName);
            // ★ (2026-11-15) ふつうのファイルを exe・dll などの名前にするのも止める（実行できるファイルを作ってしまわないように）
            if (nameProblem == null && !isDir && IsProtectedFile(newName))
            {
                nameProblem = $"{Path.GetExtension(newName)} はプログラムやシステムにかかわるファイルの拡張子のため使えません。";
            }
            if (nameProblem != null)
            {
                MessageBox.Show($"この名前は使えません。\n{nameProblem}\n\n入力した名前: {newName}", "名前の変更", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string newPath = Path.Combine(parentDir, newName);

            try
            {
                if (isDir)
                {
                    Directory.Move(path, newPath);
                }
                else
                {
                    File.Move(path, newPath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"名前の変更に失敗しました:\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // ★ (2026-11-15) 開いているタブのパスとタブ名も新しい名前にする
            //    （以前はタブが古い名前のままで、上書き保存すると古い名前のファイルがまたできていた）
            UpdateOpenTabsAfterRename(path, newPath, isDir);
        }

        // ★ (2026-11-15) システムにかかわるファイルの拡張子（system_extensions_list.md の「A. 止める」）
        //    名前の変更・この拡張子への変更・エクスプローラーペインの貼り付け・名前を付けて保存で新しく作ることを止める
        //    ふつうのデータや設定にも使う拡張子（.ini .json .xml .log .bak .dat など）は入れない（BVE のデータや _backup の .bak も止まってしまうため）
        private static readonly HashSet<string> ProtectedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            // 1. 実行ファイル・プログラム本体
            ".exe", ".com", ".msi", ".msp", ".msix", ".appx", ".app", ".dmg", ".pkg", ".deb", ".rpm",
            ".bin", ".elf", ".out", ".scr", ".pif", ".gadget",
            // 2. システムライブラリ・ドライバ・カーネル関連
            ".dll", ".sys", ".drv", ".vxd", ".ocx", ".cpl", ".msc", ".so", ".dylib", ".ko", ".kext", ".efi", ".rom",
            // 3. スクリプト・バッチ・コマンド実行
            ".bat", ".cmd", ".ps1", ".psm1", ".psd1", ".vbs", ".vbe", ".js", ".jse", ".wsf", ".wsh",
            ".sh", ".bash", ".zsh", ".csh", ".fish", ".command", ".scpt", ".applescript", ".awk", ".sed",
            // 4. システム設定（ふつうの設定ファイルに使うものは除く）
            ".reg", ".inf", ".pol", ".adm", ".admx", ".adml", ".service", ".socket", ".timer", ".htaccess",
            // 5. システム管理・ショートカット・ダンプ（一時ファイル・ログ・バックアップなどは除く）
            ".lnk", ".url", ".desktop", ".pf", ".cab", ".msu", ".dmp", ".mdmp", ".evt", ".evtx", ".etl",
            // 6. ディスクイメージ・仮想マシン・ブート関連
            ".iso", ".img", ".vhd", ".vhdx", ".vmdk", ".vdi", ".qcow2", ".ova", ".ovf", ".wim", ".esd", ".swm", ".ost", ".pst",
            // 7. セキュリティ・証明書・暗号化関連
            ".cer", ".crt", ".pem", ".der", ".pfx", ".p12", ".key", ".pub", ".csr", ".crl", ".cat"
        };

        // ★ (2026-11-15) path（ファイル、またはフォルダの中のすべてのファイル）にシステムにかかわるファイルがあれば、最初に見つかったものを返す
        private static string? FindProtectedFile(string path)
        {
            try
            {
                if (File.Exists(path)) return IsProtectedFile(Path.GetFileName(path)) ? path : null;
                if (Directory.Exists(path))
                {
                    foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                    {
                        if (IsProtectedFile(Path.GetFileName(file))) return file;
                    }
                }
            }
            catch
            {
                // 読めないフォルダがあるときは、ここでは止めない（貼り付けの処理で失敗を知らせる）
            }
            return null;
        }

        // ★ (2026-11-15) システムにかかわるファイルか（拡張子で決める）
        private static bool IsProtectedFile(string fileName)
        {
            return ProtectedExtensions.Contains(Path.GetExtension(fileName.TrimEnd()));
        }

        /// <summary>
        /// ★ (2026-11-15) ファイル・フォルダの名前として使えなければ、その理由を返す（使えれば null）
        /// </summary>
        private static string? GetInvalidFileNameReason(string name)
        {
            if (name == "." || name == "..") return "「.」「..」だけの名前は使えません。";
            char[] bad = name.Where(c => Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0).Distinct().ToArray();
            if (bad.Length > 0)
            {
                string shown = string.Join(" ", bad.Select(c => char.IsControl(c) ? "(制御文字)" : c.ToString()));
                return $"使えない文字が入っています: {shown}\n（\\ / : * ? \" < > | は使えません）";
            }
            if (name.EndsWith('.')) return "名前の最後を「.」にはできません。";
            // Windows で予約されている名前（CON, NUL, COM1 など。拡張子が付いていても使えない）
            string stem = name.Split('.')[0].TrimEnd().ToUpperInvariant();
            string[] reserved = { "CON", "PRN", "AUX", "NUL",
                "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
                "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" };
            if (reserved.Contains(stem)) return $"「{stem}」は Windows が使う名前のため使えません。";
            return null;
        }

        /// <summary>
        /// ★ (2026-11-15) 名前を変えたファイル（フォルダなら中のファイルすべて）を開いているタブのパス・タブ名を新しいものにする
        /// </summary>
        private void UpdateOpenTabsAfterRename(string oldPath, string newPath, bool isDir)
        {
            if (EditorTabControl == null) return;
            string oldFull, newFull;
            try
            {
                oldFull = Path.TrimEndingDirectorySeparator(Path.GetFullPath(oldPath));
                newFull = Path.TrimEndingDirectorySeparator(Path.GetFullPath(newPath));
            }
            catch
            {
                return;
            }

            // 古いパスを新しいパスに置き換える（関係ないパスは null）
            string? Replace(string? p)
            {
                if (string.IsNullOrEmpty(p)) return null;
                string full;
                try { full = Path.GetFullPath(p); } catch { return null; }
                if (!isDir)
                {
                    return string.Equals(full, oldFull, StringComparison.OrdinalIgnoreCase) ? newFull : null;
                }
                string prefix = oldFull + Path.DirectorySeparatorChar;
                return full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                    ? newFull + Path.DirectorySeparatorChar + full.Substring(prefix.Length)
                    : null;
            }

            bool changed = false;
            foreach (var item in EditorTabControl.Items)
            {
                if (item is TabItem ti && GetTabInfo(ti) is TabDocumentInfo info)
                {
                    string? replaced = Replace(info.FilePath);
                    if (replaced == null) continue;
                    info.FilePath = replaced;
                    ti.Header = Path.GetFileName(replaced);
                    changed = true;
                }
            }

            // 最後に調べたマップの名前が変わったときも追いかける
            string? lastMap = Replace(_lastMapRootPath);
            if (lastMap != null) _lastMapRootPath = lastMap;

            if (changed)
            {
                UpdateTitle(); // タイトルと currentFilePath も新しい名前にする
                RunSyntaxCheck();
            }
        }
        private void ExplorerPaneMenuCopy_Click(object sender, RoutedEventArgs e)
        {
            string? path = GetSelectedPath();
            if (!string.IsNullOrEmpty(path) && (File.Exists(path) || Directory.Exists(path)))
            {
                _copiedFilePath = path;
            }
        }

        private void ExplorerPaneMenuPaste_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_copiedFilePath) || (!File.Exists(_copiedFilePath) && !Directory.Exists(_copiedFilePath)))
            {
                MessageBox.Show("コピーされたファイルまたはフォルダが存在しません。", "エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string? targetPath = GetSelectedPath();
            if (string.IsNullOrEmpty(targetPath)) return;

            string targetDir = Directory.Exists(targetPath) ? targetPath : (Path.GetDirectoryName(targetPath) ?? _currentRootFolderPath);
            if (string.IsNullOrEmpty(targetDir)) return;

            // ★ (2026-11-15) フォルダを、そのフォルダ自身の中（またはさらに奥のフォルダ）に貼り付けるのは止める
            //    （以前はコピー中に作ったフォルダをまたコピーしてしまい、フォルダが深く入れ子になり続けていた）
            if (Directory.Exists(_copiedFilePath) && IsSameOrInsideFolder(targetDir, _copiedFilePath))
            {
                MessageBox.Show("フォルダを、そのフォルダ自身の中には貼り付けられません。\n別のフォルダを選んでから貼り付けてください。", "貼り付け", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // ★ (2026-11-15) システムにかかわるファイル（exe・dll・bat など）は貼り付け（コピーを作る）できないようにする
            //    フォルダのときは、中に1つでもあれば貼り付けない
            string? protectedFile = FindProtectedFile(_copiedFilePath);
            if (protectedFile != null)
            {
                string shown = string.Equals(protectedFile, _copiedFilePath, StringComparison.OrdinalIgnoreCase)
                    ? Path.GetFileName(protectedFile)
                    : Path.GetRelativePath(_copiedFilePath, protectedFile);
                MessageBox.Show($"プログラムやシステムにかかわるファイルは貼り付けられません。\n\nファイル: {shown}", "貼り付け", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                string fileName = Path.GetFileName(_copiedFilePath);
                string destPath = Path.Combine(targetDir, fileName);

                // 同名ファイル・フォルダが存在する場合は枝番を付与
                int count = 1;
                string destDirOnly = Path.GetDirectoryName(destPath) ?? targetDir;
                string fileNameWithoutExt = Path.GetFileNameWithoutExtension(destPath);
                string ext = Path.GetExtension(destPath);

                while (File.Exists(destPath) || Directory.Exists(destPath))
                {
                    if (Directory.Exists(_copiedFilePath))
                    {
                        destPath = Path.Combine(destDirOnly, $"{fileName} ({count})");
                    }
                    else
                    {
                        destPath = Path.Combine(destDirOnly, $"{fileNameWithoutExt} ({count}){ext}");
                    }
                    count++;
                }

                if (Directory.Exists(_copiedFilePath))
                {
                    CopyDirectoryRecursive(_copiedFilePath, destPath);
                }
                else
                {
                    File.Copy(_copiedFilePath, destPath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"貼り付けに失敗しました:\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ★ (2026-11-15) 開くのを禁止するシステムのフォルダ（Windows・Program Files・Program Files (x86)・ProgramData）
        //    場所は Windows から聞いて決める（C: 以外にインストールされていても効く）
        private static readonly string[] ForbiddenSystemFolders = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        }.Where(p => !string.IsNullOrEmpty(p)).ToArray();

        // ★ (2026-11-15) どのドライブでも直下にあれば禁止するフォルダ
        private static readonly string[] ForbiddenDriveRootFolders = { "$Recycle.Bin", "PerfLogs" };

        /// <summary>
        /// ★ (2026-11-15) 開くのを禁止するシステムのフォルダ（またはその中）なら true
        /// （フォルダを開く・.bveep・プロジェクト・ファイルを開く・エクスプローラーペインのツリーで使う）
        /// </summary>
        private static bool IsForbiddenSystemPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string full;
            try { full = Path.GetFullPath(path); } catch { return false; }

            foreach (var folder in ForbiddenSystemFolders)
            {
                if (IsSameOrInsideFolder(full, folder)) return true;
            }
            string? root = Path.GetPathRoot(full);
            if (!string.IsNullOrEmpty(root))
            {
                foreach (var name in ForbiddenDriveRootFolders)
                {
                    if (IsSameOrInsideFolder(full, Path.Combine(root, name))) return true;
                }
            }
            return false;
        }

        // ★ (2026-11-15) path が folder と同じか、folder の中（奥も含む）にあれば true
        private static bool IsSameOrInsideFolder(string path, string folder)
        {
            try
            {
                string p = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)) + Path.DirectorySeparatorChar;
                string f = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder)) + Path.DirectorySeparatorChar;
                return p.StartsWith(f, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return true; // パスが変で判断できないときは、安全のため貼り付けない
            }
        }

        // フォルダを丸ごとコピーするための再帰ヘルパー
        private void CopyDirectoryRecursive(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                string destFile = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, destFile);
            }
            foreach (var subDir in Directory.GetDirectories(sourceDir))
            {
                string destSubDir = Path.Combine(destDir, Path.GetFileName(subDir));
                CopyDirectoryRecursive(subDir, destSubDir);
            }
        }
        // ファイルシステムの構造を非同期でスキャンし、データモデルを返すメソッド
        private async Task<FileSystemNode?> ScanDirectoryAsync(string path)
        {
            // Task.Run でバックグラウンドスレッドで実行
            return await Task.Run(() =>
            {
                try
                {
                    var directoryInfo = new DirectoryInfo(path);
                    if (!directoryInfo.Exists) return null;

                    var rootNode = new FileSystemNode(directoryInfo.Name, directoryInfo.FullName, true);
                    // ★ (2026-11-17) 以前は下の下まで全部読んでいた（PopulateNodeRecursive）ため、ドライブの直下などで止まっていた
                    //    → いちばん上のフォルダの中（1 段）だけ読む。下のフォルダは、ツリーで開いたときに読む（TreeItem_Expanded）
                    PopulateNodeOneLevel(rootNode, directoryInfo);
                    return rootNode;
                }
                catch (Exception)
                {
                    // エラー処理（必要に応じて）
                    return null;
                }
            });
        }
        /// <summary>
        /// ★ (2026-11-17) フォルダの中を 1 段だけ読む（下のフォルダの中は読まない。IsLoaded = false のまま）
        /// 隠し・禁止するフォルダは出さない。ジャンクション・シンボリックリンクのフォルダは出すが、中は読まない（ぐるぐる回らないように）
        /// </summary>
        private static void PopulateNodeOneLevel(FileSystemNode parentNode, DirectoryInfo directoryInfo)
        {
            parentNode.IsLoaded = true;
            try
            {
                foreach (var directory in directoryInfo.GetDirectories())
                {
                    if ((directory.Attributes & FileAttributes.Hidden) != 0) continue;
                    if (IsForbiddenSystemPath(directory.FullName)) continue;
                    var subNode = new FileSystemNode(directory.Name, directory.FullName, true);
                    // ジャンクション・シンボリックリンクは中を読まない（読み込み済み・中は空として扱う）
                    if ((directory.Attributes & FileAttributes.ReparsePoint) != 0) subNode.IsLoaded = true;
                    parentNode.Children.Add(subNode);
                }
                foreach (var file in directoryInfo.GetFiles())
                {
                    if ((file.Attributes & FileAttributes.Hidden) != 0) continue;
                    parentNode.Children.Add(new FileSystemNode(file.Name, file.FullName, false));
                }
            }
            catch (UnauthorizedAccessException)
            {
                parentNode.Children.Add(new FileSystemNode($"[アクセス拒否] {directoryInfo.Name}", directoryInfo.FullName, true) { IsLoaded = true });
            }
            catch (IOException)
            {
                // 読んでいる途中で消えた・ドライブが外されたなど。読めた所までを出す
            }
        }

        // ★ (2026-11-17) エクスプローラーペインの行間（設定 ExplorerRowSpacing）。1 行の上下の余白: コンパクト 0・標準 2（以前の幅）・広い 5
        private static string NormalizeExplorerSpacing(string? s) => s switch
        {
            "Standard" => "Standard",
            "Wide" => "Wide",
            _ => "Compact", // 知らない値・古い settings.json は初期値のコンパクト
        };

        private Thickness GetExplorerItemMargin()
        {
            double v = NormalizeExplorerSpacing(currentSettings?.ExplorerRowSpacing) switch
            {
                "Standard" => 2,
                "Wide" => 5,
                _ => 0,
            };
            return new Thickness(0, v, 0, v);
        }

        // 行間の設定を、今のツリーの全部の行に当てる（作り直さずに余白だけ変える）
        private void ApplyExplorerSpacingToTree()
        {
            var margin = GetExplorerItemMargin();
            void Apply(ItemsControl parent)
            {
                foreach (var obj in parent.Items)
                {
                    if (obj is not TreeViewItem item) continue;
                    if (item.Header is StackPanel sp) sp.Margin = margin;
                    Apply(item);
                }
            }
            if (FileExplorerTreeView != null) Apply(FileExplorerTreeView);
        }

        // ★ (2026-11-17) 中を読んでいないフォルダに入れておく仮の項目（Tag は null。開いたときに本当の中身に置き換える）
        private const string TreePlaceholderText = "読み込み中…";
        private static bool IsTreePlaceholder(object? item) => item is TreeViewItem t && t.Tag == null && Equals(t.DataContext, TreePlaceholderText);

        // フォルダの項目を開いたとき: まだ中を読んでいなければ、今読んで項目を作る
        private void TreeItem_Expanded(object sender, RoutedEventArgs e)
        {
            if (!ReferenceEquals(e.OriginalSource, sender)) return; // 子の項目が開いたのが伝わってきたときは何もしない
            if (sender is not TreeViewItem item || item.Tag is not string path) return;
            if (item.Items.Count != 1 || !IsTreePlaceholder(item.Items[0])) return; // もう読んである
            try
            {
                var node = new FileSystemNode(Path.GetFileName(path), path, true);
                PopulateNodeOneLevel(node, new DirectoryInfo(path));
                item.Items.Clear();
                foreach (var child in node.Children)
                {
                    item.Items.Add(CreateTreeItemFromNode(child));
                }
            }
            catch (Exception ex)
            {
                App.LogOperation($"フォルダを読めませんでした: {ex.Message}");
                item.Items.Clear();
            }
        }

        // 再帰的にノード（データ）を構築するメソッド（バックグラウンドで実行）
        // ★ (2026-11-17) 今は使っていない（下の下まで全部読むと、ドライブの直下などで止まるため。PopulateNodeOneLevel を使う）
        private void PopulateNodeRecursive(FileSystemNode parentNode, DirectoryInfo directoryInfo)
        {
            try
            {
                // サブフォルダを追加
                foreach (var directory in directoryInfo.GetDirectories())
                {
                    // 隠しフォルダやシステムフォルダは除外
                    if ((directory.Attributes & FileAttributes.Hidden) != 0) continue;
                    // ★ (2026-11-15) Windows・Program Files などの禁止するフォルダは出さない（中も読まない）
                    if (IsForbiddenSystemPath(directory.FullName)) continue;

                    try
                    {
                        var subNode = new FileSystemNode(directory.Name, directory.FullName, true);
                        parentNode.Children.Add(subNode);
                        PopulateNodeRecursive(subNode, directory); // 再帰
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // アクセス拒否された場合はエラーメッセージ付きのノードを追加
                        // UIスレッドではないので、MessageBox.Show は使わない
                        parentNode.Children.Add(new FileSystemNode($"[アクセス拒否] {directory.Name}", directory.FullName, true));
                    }
                }

                // ファイルを追加
                foreach (var file in directoryInfo.GetFiles())
                {
                    if ((file.Attributes & FileAttributes.Hidden) != 0) continue;

                    try
                    {
                        var fileNode = new FileSystemNode(file.Name, file.FullName, false);
                        parentNode.Children.Add(fileNode);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // 個別のファイルへのアクセス権限がない場合
                        parentNode.Children.Add(new FileSystemNode($"[アクセス拒否] {file.Name}", file.FullName, false));
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                // ディレクトリ自体へのアクセス権限がない場合
                parentNode.Children.Add(new FileSystemNode($"[アクセス拒否] {directoryInfo.Name}", directoryInfo.FullName, true));
            }
        }

        // データモデル（FileSystemNode）からTreeViewを構築するメソッド（UIスレッドで実行）
        private void LoadFileSystemToTreeView(FileSystemNode? rootNode)
        {
            FileExplorerTreeView.Items.Clear();

            if (rootNode != null)
            {
                var rootItem = CreateTreeItemFromNode(rootNode);
                if (rootItem != null)
                {
                    FileExplorerTreeView.Items.Add(rootItem);
                    rootItem.IsExpanded = true;
                }
            }
        }

        // TreeViewItemをデータモデルから生成するメソッド（UIスレッドで実行）
        private TreeViewItem CreateTreeItemFromNode(FileSystemNode node)
        {
            var treeItem = new TreeViewItem
            {
                Tag = node.FullPath
            };
            // 1. アイコンとテキストを並べるための StackPanel を作成
            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = GetExplorerItemMargin() // ★ (2026-11-17) 行間の設定（コンパクト・標準・広い）に合わせる。以前はいつも上下 2
            };

            // 2. アイコンを取得して Image コントロールにセット
            var image = new System.Windows.Controls.Image
            {
                Width = 16,
                Height = 16,
                Margin = new Thickness(0, 0, 5, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            // アクセス拒否項目や通常のパスからアイコンを取得
            string actualPath = node.FullPath;
            if (node.Name.StartsWith("[アクセス拒否]"))
            {
                // アクセス拒否の場合は実際のパスからアイコンを取れないことがあるのでプレースホルダー等に
                actualPath = node.FullPath;
            }
            ImageSource? iconSource = GetFileIcon(actualPath, node.IsDirectory);
            if (iconSource != null)
            {
                image.Source = iconSource;
            }
            stackPanel.Children.Add(image);
            // 3. テキスト（ファイル名・フォルダ名）を表示する TextBlock
            var textBlock = new TextBlock
            {
                Text = node.Name,
                VerticalAlignment = VerticalAlignment.Center
            };

            if (node.Name.StartsWith("[アクセス拒否]"))
            {
                textBlock.Foreground = Brushes.Red;
            }
            stackPanel.Children.Add(textBlock);

            // ヘッダーに StackPanel を設定
            treeItem.Header = stackPanel;
            // フォルダの場合は子ノードを再帰的に追加
            if (node.IsDirectory)
            {
                if (!node.IsLoaded)
                {
                    // ★ (2026-11-17) まだ中を読んでいないフォルダ: 仮の項目を入れて ▶ を出し、開いたときに読む
                    treeItem.Items.Add(new TreeViewItem { Header = TreePlaceholderText, DataContext = TreePlaceholderText, Foreground = Brushes.Gray, IsEnabled = false });
                    treeItem.Expanded += TreeItem_Expanded;
                }
                foreach (var childNode in node.Children)
                {
                    var childItem = CreateTreeItemFromNode(childNode);
                    if (childItem != null)
                    {
                        treeItem.Items.Add(childItem);
                    }
                }
            }

            return treeItem;
        }
        // ツリービューに指定したフォルダの内容を読み込む
        // ディレクトリ情報を元に TreeViewItem を再帰的に作成
        private TreeViewItem? CreateTreeItem(DirectoryInfo directoryInfo)
        {
            var directoryItem = new TreeViewItem
            {
                Header = $"📁 {directoryInfo.Name}",
                Tag = directoryInfo.FullName
            };

            try
            {
                // サブフォルダを追加
                foreach (var directory in directoryInfo.GetDirectories())
                {
                    // 隠しフォルダやシステムフォルダは除外
                    if ((directory.Attributes & FileAttributes.Hidden) != 0) continue;

                    try
                    {
                        // サブフォルダのツリーアイテムを作成して追加
                        var subItem = CreateTreeItem(directory);
                        if (subItem != null)
                        {
                            directoryItem.Items.Add(subItem);
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // 個別のサブフォルダへのアクセス権限がない場合
                        MessageBox.Show(
                            $"{directory.Name} へのアクセスが拒否されました。",
                            "アクセス権限エラー",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }
                }
                // ファイルを追加
                foreach (var file in directoryInfo.GetFiles())
                {
                    if ((file.Attributes & FileAttributes.Hidden) != 0) continue;

                    var fileItem = new TreeViewItem
                    {
                        Header = $"📄 {file.Name}",
                        Tag = file.FullName
                    };
                    directoryItem.Items.Add(fileItem);
                }
            }
            catch (UnauthorizedAccessException)
            {
                // ディレクトリ自体へのアクセス権限がない場合
                MessageBox.Show(
                    $"{directoryInfo.Name} へのアクセスが拒否されました。",
                    "アクセス権限エラー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return null;
            }


            return directoryItem;
        }

        // ツリービューの項目選択変更イベント
        private void FileExplorerTreeView_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // ダブルクリックされたアイテムを取得
            if (FileExplorerTreeView.SelectedItem is TreeViewItem selectedItem && selectedItem.Tag is string path)
            {
                if (File.Exists(path))
                {
                    // ファイルの場合はエディタで開く処理を実行
                    OpenFileInEditor(path);

                    // イベントが親にバブリングするのを防ぐ
                    e.Handled = true;
                }
            }
        }
        //ファイルとフォルダが選択されたとき。空実装でよい。削除するとビルドエラーになる
        private void FileExplorerTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            //何も書かない
        }
        // パスを指定してTreeViewの該当アイテムを選択状態にするヘルパー

        private bool SelectTreeViewItemByPath(ItemsControl container, string path)
        {
            foreach (var item in container.Items)
            {
                if (item is TreeViewItem treeItem && treeItem.Tag is string tagPath)
                {
                    if (tagPath.Equals(path, StringComparison.OrdinalIgnoreCase))
                    {
                        treeItem.IsSelected = true;
                        treeItem.BringIntoView();
                        return true;
                    }

                    // 子階層も探索
                    // ★ (2026-11-17) フォルダは開いたときに中を読むので、探すパスがこのフォルダの中のときだけ、開いて（中を読んで）から探す
                    if (path.StartsWith(Path.TrimEndingDirectorySeparator(tagPath) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    {
                        treeItem.IsExpanded = true;
                        if (SelectTreeViewItemByPath(treeItem, path)) return true;
                    }
                }
            }
            return false;
        }
        private void SetupFileSystemWatcher(string rootPath)
        {
            // 既存の監視があれば停止・破棄
            if (_fileSystemWatcher != null)
            {
                _fileSystemWatcher.EnableRaisingEvents = false;
                _fileSystemWatcher.Dispose();
                _fileSystemWatcher = null;
            }

            if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath)) return;

            _fileSystemWatcher = new FileSystemWatcher(rootPath)
            {
                IncludeSubdirectories = true, // サブフォルダも含めてすべて監視
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite
            };

            // イベントハンドラの登録
            _fileSystemWatcher.Created += OnFileSystemChanged;
            _fileSystemWatcher.Deleted += OnFileSystemChanged;
            _fileSystemWatcher.Renamed += OnFileSystemChanged;

            // 監視を開始
            _fileSystemWatcher.EnableRaisingEvents = true;
        }

        // ファイル変更検知時の処理（バックグラウンドスレッドから呼ばれるためDispatcherを使用）
        // ★ (2026-11-15) ツリーの作り直しを少し待ってまとめるタイマー（貼り付けなどで変更が続けて起きても1回にする）
        private System.Windows.Threading.DispatcherTimer? _treeRefreshTimer;
        // ★ (2026-11-15) 待っている間に起きた名前の変更（古いパス → 新しいパス）。開いていたフォルダ・選んでいた項目を新しい名前で戻すため
        private readonly List<(string OldPath, string NewPath)> _pendingTreeRenames = new();
        private readonly object _pendingTreeRenamesLock = new();

        private void OnFileSystemChanged(object sender, FileSystemEventArgs e)
        {
            // ★ (2026-11-15) 以前は変更のたびにすぐツリーを全部作り直していたため、開いていたフォルダが全部閉じていた
            //    （サブフォルダの中で名前を変える・Excel が一時ファイルを作るだけでも閉じた）
            //    → 0.3 秒待ってまとめてから、開いていたフォルダと選んでいた項目を戻して作り直す
            if (e is RenamedEventArgs re)
            {
                lock (_pendingTreeRenamesLock) _pendingTreeRenames.Add((re.OldFullPath, re.FullPath));
            }
            Dispatcher.BeginInvoke(() =>
            {
                if (_treeRefreshTimer == null)
                {
                    _treeRefreshTimer = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Background, Dispatcher)
                    {
                        Interval = TimeSpan.FromMilliseconds(300)
                    };
                    _treeRefreshTimer.Tick += async (s, a) =>
                    {
                        _treeRefreshTimer.Stop();
                        await RefreshTreeKeepingStateAsync();
                    };
                }
                _treeRefreshTimer.Stop();
                _treeRefreshTimer.Start(); // 続けて変更が来たら待ち直す
            });
        }

        /// <summary>
        /// ★ (2026-11-15) ツリーを作り直す。作り直す前に開いていたフォルダ・選んでいた項目・スクロール位置を覚えておき、作り直した後に戻す
        /// （ファイルの変更を見つけたときと、「再読み込み」メニューで使う）
        /// </summary>
        /// <param name="selectPath">★ (2026-11-17) 作り直した後に選ぶ項目（null なら今選んでいる項目）。その親のフォルダは開く</param>
        private async Task RefreshTreeKeepingStateAsync(string? selectPath = null)
        {
            if (string.IsNullOrEmpty(_currentRootFolderPath) || !Directory.Exists(_currentRootFolderPath)) return;

            // 1. 今の状態を覚える
            var expanded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in FileExplorerTreeView.Items.OfType<TreeViewItem>())
            {
                CollectExpandedPaths(item, expanded);
            }
            string? selected = selectPath ?? GetSelectedPath();
            if (selectPath != null)
            {
                // 選ぶ項目が見えるように、ルートからその親までのフォルダを開く
                string? dir = Path.GetDirectoryName(selectPath);
                while (!string.IsNullOrEmpty(dir) && IsSameOrInsideFolder(dir, _currentRootFolderPath))
                {
                    expanded.Add(dir);
                    if (string.Equals(Path.TrimEndingDirectorySeparator(dir), Path.TrimEndingDirectorySeparator(_currentRootFolderPath), StringComparison.OrdinalIgnoreCase)) break;
                    dir = Path.GetDirectoryName(dir);
                }
            }
            var scroller = FindTreeScrollViewer(FileExplorerTreeView);
            double scrollOffset = scroller?.VerticalOffset ?? 0;

            // 2. 待っている間に名前が変わったものは、新しい名前に置き換える
            List<(string OldPath, string NewPath)> renames;
            lock (_pendingTreeRenamesLock)
            {
                renames = new List<(string, string)>(_pendingTreeRenames);
                _pendingTreeRenames.Clear();
            }
            foreach (var (oldPath, newPath) in renames)
            {
                foreach (var p in expanded.ToList())
                {
                    string? moved = ReplacePathPrefix(p, oldPath, newPath);
                    if (moved != null) { expanded.Remove(p); expanded.Add(moved); }
                }
                if (selected != null) selected = ReplacePathPrefix(selected, oldPath, newPath) ?? selected;
            }

            // 3. 作り直す
            FileSystemNode? rootNode = await ScanDirectoryAsync(_currentRootFolderPath);
            if (rootNode == null) return;
            LoadFileSystemToTreeView(rootNode);

            // 4. 開いていたフォルダ・選んでいた項目を戻す
            TreeViewItem? selectedItem = null;
            foreach (var item in FileExplorerTreeView.Items.OfType<TreeViewItem>())
            {
                selectedItem = RestoreTreeState(item, expanded, selected) ?? selectedItem;
            }
            if (selectedItem != null) selectedItem.IsSelected = true;

            // 5. スクロール位置を戻す（画面ができてから）
            if (scroller != null)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    FindTreeScrollViewer(FileExplorerTreeView)?.ScrollToVerticalOffset(scrollOffset);
                }, System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        // 開いているフォルダのパスを集める
        private static void CollectExpandedPaths(TreeViewItem item, HashSet<string> expanded)
        {
            if (item.IsExpanded && item.Tag is string path) expanded.Add(path);
            foreach (var child in item.Items.OfType<TreeViewItem>())
            {
                CollectExpandedPaths(child, expanded);
            }
        }

        // 覚えておいたフォルダを開き直す。選んでいた項目が見つかればそれを返す
        private static TreeViewItem? RestoreTreeState(TreeViewItem item, HashSet<string> expanded, string? selected)
        {
            TreeViewItem? found = null;
            if (item.Tag is string path)
            {
                if (expanded.Contains(path)) item.IsExpanded = true;
                if (selected != null && string.Equals(path, selected, StringComparison.OrdinalIgnoreCase)) found = item;
            }
            foreach (var child in item.Items.OfType<TreeViewItem>())
            {
                found = RestoreTreeState(child, expanded, selected) ?? found;
            }
            return found;
        }

        // path が oldPath そのもの、または oldPath の中なら、newPath に置き換えたパスを返す（関係なければ null）
        private static string? ReplacePathPrefix(string path, string oldPath, string newPath)
        {
            if (string.Equals(path, oldPath, StringComparison.OrdinalIgnoreCase)) return newPath;
            string prefix = Path.TrimEndingDirectorySeparator(oldPath) + Path.DirectorySeparatorChar;
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return Path.TrimEndingDirectorySeparator(newPath) + Path.DirectorySeparatorChar + path.Substring(prefix.Length);
            }
            return null;
        }

        // ツリーの中のスクロールバー（ScrollViewer）を探す
        private static ScrollViewer? FindTreeScrollViewer(DependencyObject parent)
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is ScrollViewer sv) return sv;
                var found = FindTreeScrollViewer(child);
                if (found != null) return found;
            }
            return null;
        }

    }
}
