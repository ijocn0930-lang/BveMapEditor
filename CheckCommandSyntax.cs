using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace BveMapEditor
{
    public partial class MapParser
    {
        private static void CheckCommandSyntax(string trimmedCodePart, int lineNumber, string baseDirectory, List<ParseError> errors, HashSet<string> validCommands)
        {
            // 1. 括弧の中身を取り出す
            string argsContent = GetInnerArguments(trimmedCodePart);

            // 2. 引数を安全に分割する
            List<string> args = SplitArguments(argsContent);
            // 1. 名前空間のドット抜けチェック (例: StationLoad など)
            foreach (var ns in KnownNamespaces)
            {
                if (Regex.IsMatch(trimmedCodePart, $@"\b{ns}[A-Za-z]+\s*\(", RegexOptions.IgnoreCase))
                {
                    if (!Regex.IsMatch(trimmedCodePart, $@"\b{ns}\.[A-Za-z]+\s*\(", RegexOptions.IgnoreCase) &&
                        !Regex.IsMatch(trimmedCodePart, $@"\b{ns}\[.*?\]", RegexOptions.IgnoreCase))
                    {
                        int index = trimmedCodePart.IndexOf(ns, StringComparison.OrdinalIgnoreCase);
                        errors.Add(new ParseError
                        {
                            Level = "エラー",
                            Line = lineNumber,
                            Column = index >= 0 ? index + 1 : 1,
                            Message = $"'{ns}' の後にドット '.' が抜けているか、コマンドの記述形式が不正です（例: '{ns}.Load' のように記述してください）。"
                        });
                    }
                }
            }
            // 2. Sound や Structure などの角括弧 `[...]` 抜けチェック（例外コマンド除外対応版）
            foreach (var ns in KeyRequiredNamespaces)
            {
                if (Regex.IsMatch(trimmedCodePart, $@"\b{ns}\.[A-Za-z0-9]+", RegexOptions.IgnoreCase) ||
                    Regex.IsMatch(trimmedCodePart, $@"\b{ns}\s*\[", RegexOptions.IgnoreCase))
                {
                    // .Load や .Set などのほか、Signal.SpeedLimit や Train.Add は角括弧が不要なので除外
                    if (Regex.IsMatch(trimmedCodePart, $@"\b{ns}\.(Load|Set)\b", RegexOptions.IgnoreCase))
                    {
                        continue;
                    }
                    if (Regex.IsMatch(trimmedCodePart, $@"\bSignal\.SpeedLimit\b", RegexOptions.IgnoreCase) ||
                        Regex.IsMatch(trimmedCodePart, $@"\bTrain\.Add\b", RegexOptions.IgnoreCase))
                    {
                        continue;
                    }

                    // もし `ns[...]` の形式が含まれていない場合、角括弧が抜けている
                    if (!Regex.IsMatch(trimmedCodePart, $@"\b{ns}\s*\[", RegexOptions.IgnoreCase))
                    {
                        int index = trimmedCodePart.IndexOf(ns, StringComparison.OrdinalIgnoreCase);
                        errors.Add(new ParseError
                        {
                            Level = "エラー",
                            Line = lineNumber,
                            Column = index >= 0 ? index + 1 : 1,
                            Message = $"'{ns}' の指定に角括弧 '[]'（キーまたはインデックス）が不足している可能性があります（例: '{ns}[\"key\"].Command(...)'）。"
                        });
                    }
                }
            }
            // 3. Sound / Structure / Signal などの角括弧の中身の検証
            var bracketMatches = Regex.Matches(trimmedCodePart, $@"\b(Sound|Structure|Signal|Track|Station|Repeater|Sound3D|Train)\s*\[(.*?)\]", RegexOptions.IgnoreCase);
            foreach (Match match in bracketMatches)
            {
                string innerContent = match.Groups[2].Value.Trim();
                int index = match.Index;

                if (string.IsNullOrEmpty(innerContent) || innerContent == "''" || innerContent == "\"\"")
                {
                    errors.Add(new ParseError
                    {
                        Level = "エラー",
                        Line = lineNumber,
                        Column = index + 1,
                        Message = "角括弧 '[]' の中にキー（またはインデックス）が指定されていません。"
                    });
                    continue;
                }

                bool startsWithQuote = innerContent.StartsWith("'") || innerContent.StartsWith("\"");
                bool endsWithQuote = innerContent.EndsWith("'") || innerContent.EndsWith("\"");

                if (!int.TryParse(innerContent, out _))
                {
                    if (startsWithQuote != endsWithQuote)
                    {
                        errors.Add(new ParseError
                        {
                            Level = "エラー",
                            Line = lineNumber,
                            Column = index + 1,
                            Message = "角括弧 '[]' 内の文字列を囲むアポストロフィ（' または \"）が片方しかありません。"
                        });
                    }
                    else if (!startsWithQuote && !endsWithQuote)
                    {
                        errors.Add(new ParseError
                        {
                            Level = "エラー",
                            Line = lineNumber,
                            Column = index + 1,
                            Message = "角括弧 '[]' 内のキーはアポストロフィ等で囲むか、有効な数値を指定してください。"
                        });
                    }
                }
            }
            // 4. ★ 修正：Station.Load, Sound.Load などの Load系コマンドの引数・ファイル存在チェック（文字列位置ベース）
            if (Regex.IsMatch(trimmedCodePart, $@"\b[A-Za-z0-9]+\.Load\b", RegexOptions.IgnoreCase))
            {
                int openParen = trimmedCodePart.IndexOf('(');
                int closeParen = trimmedCodePart.LastIndexOf(')');

                if (openParen >= 0 && closeParen > openParen)
                {
                    string innerContent = trimmedCodePart.Substring(openParen + 1, closeParen - openParen - 1).Trim();

                    if (string.IsNullOrEmpty(innerContent) || innerContent == "''" || innerContent == "\"\"")
                    {
                        errors.Add(new ParseError
                        {
                            Level = "エラー",
                            Line = lineNumber,
                            Column = openParen + 1,
                            Message = "Load系コマンドの引数（丸括弧内）にファイルパスが指定されていません。"
                        });
                        return;
                    }
                    // ② 引数を囲むアポストロフィ・引用符のチェック
                    bool startsWithQuote = innerContent.StartsWith("'") || innerContent.StartsWith("\"");
                    bool endsWithQuote = innerContent.EndsWith("'") || innerContent.EndsWith("\"");

                    if (startsWithQuote != endsWithQuote)
                    {
                        errors.Add(new ParseError
                        {
                            Level = "エラー",
                            Line = lineNumber,
                            Column = openParen + 1,
                            Message = "Load系コマンドの引数を囲むアポストロフィ（' または \"）が片方しかありません。"
                        });
                    }
                    else if (!startsWithQuote && !endsWithQuote)
                    {
                        errors.Add(new ParseError
                        {
                            Level = "エラー",
                            Line = lineNumber,
                            Column = openParen + 1,
                            Message = "Load系コマンドの引数（ファイルパス等）はアポストロフィ（' または \"）で囲む必要があります。"
                        });
                    }
                    else
                    {
                        // ★ 5. Trim('\'', '"') を使って安全にクォートを除去する
                        string filePath = innerContent.Trim('\'', '"');

                        if (!string.IsNullOrEmpty(baseDirectory))
                        {
                            if (string.IsNullOrWhiteSpace(filePath))
                            {
                                errors.Add(new ParseError
                                {
                                    Line = lineNumber,
                                    Column = openParen + 1,
                                    Message = "ファイルパスが空です。"
                                });
                            }
                            else
                            {
                                try
                                {
                                    string fullPath = Path.GetFullPath(Path.Combine(baseDirectory, filePath));

                                    // デバッグ用（必要に応じて出力ウィンドウで確認できます）
                                    //System.Diagnostics.Debug.WriteLine($"[FileCheck] Checking: {fullPath} (Exists: {File.Exists(fullPath)})");

                                    if (!File.Exists(fullPath))
                                    {
                                        errors.Add(new ParseError
                                        {
                                            Line = lineNumber,
                                            Column = openParen + 1,
                                            Message = $"指定されたファイルが見つかりません: '{filePath}'"
                                        });
                                    }
                                }
                                catch
                                {
                                    errors.Add(new ParseError
                                    {
                                        Line = lineNumber,
                                        Column = openParen + 1,
                                        Message = $"ファイルパスの形式が不正です: '{filePath}'"
                                    });
                                }
                            }
                        }
                        else
                        {
                            // baseDirectory が空の場合の警告（必要に応じて）
                            // System.Diagnostics.Debug.WriteLine("[FileCheck] baseDirectory is empty. File existence check skipped.");
                        }
                    }
                }
            }

            // ★ 今後追加したい「引数が必須なコマンド」をここにどんどん登録できます！
            var requiredArgCommands = new Dictionary<string, string>
            {
                { "Background.Change", "Background.Change の引数に背景キーが指定されていません。" },
                { "Curve.Gauge",       "Curve.Gauge の引数にゲージ幅（数値）が指定されていません。" },
                { "Begin0",            "Begin0 の引数にパラメータが指定されていません。" },
                                { "Begin",            "Begin の引数にパラメータが指定されていません。" },
                { "Put",            "Put の引数にパラメータが指定されていません。" }, // 例として追加
                { "Put0",            "Put0 の引数にパラメータが指定されていません。" }, // 例として追加
                { "PutBetween",            "PutBetween の引数にパラメータが指定されていません。" }, // 例として追加
                { "SetGauge",            "SetGauge の引数にパラメータが指定されていません。" }, // 例として追加
                { "Gauge",            "Gauge の引数にパラメータが指定されていません。" }, // 例として追加
                { "SetFunction",            "SetFunction の引数にパラメータが指定されていません。" }, // 例として追加
                { "Curve.Change",            "Curve.Change の引数にパラメータが指定されていません。" }, // 例として追加
                { "Adhesion.Change",            "Adhesion.Change の引数にパラメータが指定されていません。" }, // 例として追加
                { "Fog.Interpolate",            "Fog.Interpolate の引数にパラメータが指定されていません。" }, // 例として追加
                { "Light.Ambient",            "Light.Ambient の引数にパラメータが指定されていません。" }, // 例として追加
                { "Light.Diffuse",            "Light.Diffuse の引数にパラメータが指定されていません。" }, // 例として追加
                { "Light.Direction",            "Light.Direction の引数にパラメータが指定されていません。" }, // 例として追加
                { "Stop",            "Stop の引数にパラメータが指定されていません。" }, // 例として追加
                { "Pass",            "Pass の引数にパラメータが指定されていません。" }, // 例として追加
                { "Enable",            "Enable の引数にパラメータが指定されていません。" }, // 例として追加
                { "Center",            "Center の引数にパラメータが指定されていません。" }, // 例として追加



                // 例：ここに新しいコマンドとメッセージをどんどん追加できます
            };

            foreach (var pair in requiredArgCommands)
            {
                string cmdName = pair.Key;
                string errorMessage = pair.Value;

                if (Regex.IsMatch(trimmedCodePart, $@"\b{Regex.Escape(cmdName)}\b", RegexOptions.IgnoreCase))
                {
                    int openParen = trimmedCodePart.IndexOf('(');
                    int closeParen = trimmedCodePart.LastIndexOf(')');

                    if (openParen >= 0 && closeParen > openParen)
                    {
                        string innerContent = trimmedCodePart.Substring(openParen + 1, closeParen - openParen - 1).Trim();

                        if (string.IsNullOrEmpty(innerContent) || innerContent == "''" || innerContent == "\"\"")
                        {
                            errors.Add(new ParseError
                            {
                                Level = "警告",
                                Line = lineNumber,
                                Column = openParen + 1,
                                Message = errorMessage
                            });
                            continue;
                        }

                    }
                }

            }
            // ★ 追加：KeyRequiredNamespaces（Sound, Structure, Signal, Train）の要素指定に誤って丸括弧 '()' を使っている場合の検出
            foreach (var ns in KeyRequiredNamespaces)
            {
                // 例: Sound(...) や Train(...) のように、名前に続いてすぐ丸括弧が来ているケースを検知
                // （ただし Sound.Load などドットが続くものは除外する）
                string pattern = $@"\b{ns}\s*\([^)]+\)";
                if (Regex.IsMatch(trimmedCodePart, pattern, RegexOptions.IgnoreCase))
                {
                    // .Load や .Set などの例外や、もし丸括弧が許される特殊なケースがあればここで除外可能
                    if (!Regex.IsMatch(trimmedCodePart, $@"\b{ns}\.(Load|Set)\b", RegexOptions.IgnoreCase))
                    {
                        int index = trimmedCodePart.IndexOf(ns, StringComparison.OrdinalIgnoreCase);
                        errors.Add(new ParseError
                        {
                            Level = "エラー",
                            Line = lineNumber,
                            Column = index >= 0 ? index + 1 : 1,
                            Message = $"'{ns}' の指定には丸括弧 '()' ではなく、角括弧 '[]'（キーまたはインデックス）を使用してください（例: '{ns}[\"key\"].Play()'）。"
                        });
                    }
                }
            }
            // ★ 追加：引数を取ってはいけない（カッコ内が空であるべき）メソッドの一括管理
            var noArgMethods = new Dictionary<string, string>
            {
                { "Play", ".Play() の引数には何も指定できません（例: 'Sound[\"key\"].Play();' のように記述してください）。" },
                { "BeginTransition", ".BeginTransition() の引数には何も指定できません（例: 'Curve.BeginTransition();' のように記述してください）。" },
                { "End", ".End() の引数には何も指定できません（例: 'Repetar[\"key\"].End();' のように記述してください）。" },

                // 例：今後もし「引数不可」のメソッドを増やしたくなったらここに追記できます
                // { "Show", ".Show() の引数には何も指定できません。" },
            };

            foreach (var pair in noArgMethods)
            {
                string methodName = pair.Key;
                string errorMessage = pair.Value;

                // .Play(...) のような形式を検出する正規表現
                string pattern = $@"\.{methodName}\s*\((.*?)\)";
                var matches = Regex.Matches(trimmedCodePart, pattern, RegexOptions.IgnoreCase);

                foreach (Match match in matches)
                {
                    string innerContent = match.Groups[1].Value.Trim();

                    // カッコの中身が空ではない（＝何か余計な文字や数値が入っている）場合に警告
                    if (!string.IsNullOrEmpty(innerContent) && innerContent != "''" && innerContent != "\"\"")
                    {
                        int index = match.Index;
                        errors.Add(new ParseError
                        {
                            Level = "エラー",
                            Line = lineNumber,
                            Column = index + 1,
                            Message = errorMessage
                        });
                    }
                }
            }
            // 8. ★ 追加：include 構文のチェック（= の禁止、クォート、拡張子 .txt、ファイル存在チェック）
            if (Regex.IsMatch(trimmedCodePart, $@"\binclude\b", RegexOptions.IgnoreCase))
            {
                // A. 不要な '=' が含まれていないかチェック
                if (Regex.IsMatch(trimmedCodePart, $@"\binclude\s*=", RegexOptions.IgnoreCase))
                {
                    int index = trimmedCodePart.IndexOf("include", StringComparison.OrdinalIgnoreCase);
                    errors.Add(new ParseError
                    {
                        Level = "エラー",
                        Line = lineNumber,
                        Column = index >= 0 ? index + 1 : 1,
                        Message = "'include' の後に不要な '=' が含まれています（例: 'include \"submap.txt\";' のように記述してください）。"
                    });
                }
                else
                {
                    // B. クォートで正しく囲まれたファイルパスの抽出
                    var match = Regex.Match(trimmedCodePart, $@"\binclude\s+(['""])(.*?)\1", RegexOptions.IgnoreCase);
                    if (!match.Success)
                    {
                        int index = trimmedCodePart.IndexOf("include", StringComparison.OrdinalIgnoreCase);
                        errors.Add(new ParseError
                        {
                            Level = "エラー",
                            Line = lineNumber,
                            Column = index >= 0 ? index + 1 : 1,
                            Message = "'include' の引数はアポストロフィ（' または \"）で囲む必要があります（例: 'include \"submap.txt\";'）。"
                        });
                    }
                    else
                    {
                        string filePath = match.Groups[2].Value.Trim();
                        int index = match.Index;

                        if (string.IsNullOrEmpty(filePath))
                        {
                            errors.Add(new ParseError
                            {
                                Level = "エラー",
                                Line = lineNumber,
                                Column = index + 1,
                                Message = "'include' の引数にファイルパスが指定されていません。"
                            });
                        }
                        else
                        {
                            // C. 拡張子 .txt のチェック
                            if (!filePath.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                            {
                                errors.Add(new ParseError
                                {
                                    Level = "警告",
                                    Line = lineNumber,
                                    Column = index + 1,
                                    Message = "'include' で指定できるファイルは拡張子が '.txt' のファイルのみです。"
                                });
                            }
                            else
                            {
                                // D. ファイルの物理存在チェック
                                if (!string.IsNullOrEmpty(baseDirectory))
                                {
                                    try
                                    {
                                        string fullPath = Path.GetFullPath(Path.Combine(baseDirectory, filePath));

                                        if (!File.Exists(fullPath))
                                        {
                                            errors.Add(new ParseError
                                            {
                                                Level = "エラー",
                                                Line = lineNumber,
                                                Column = index + 1,
                                                Message = $"インクルード先のファイルが見つかりません: '{filePath}'"
                                            });
                                        }
                                    }
                                    catch
                                    {
                                        errors.Add(new ParseError
                                        {
                                            Level = "警告",
                                            Line = lineNumber,
                                            Column = index + 1,
                                            Message = $"ファイルパスの形式が不正です: '{filePath}'"
                                        });
                                    }
                                }
                            }
                        }
                    }
                }
            }
            // 9-1. ★ 追加：BveEx / BveEx.User に続くカスタム構文の検出
            if (Regex.IsMatch(trimmedCodePart, $@"\bBveEx(\.User)?\b", RegexOptions.IgnoreCase))
            {
                // BveEx が使われている場合は、標準のBVEコマンドではないため未知コマンドエラーから除外し、
                // 代わりに「BveExのインストール先が特定できないため注意」という警告を出す
                int index = trimmedCodePart.IndexOf("BveEx", StringComparison.OrdinalIgnoreCase);
                errors.Add(new ParseError
                {
                    Level = "注意",
                    Line = lineNumber,
                    Column = index >= 0 ? index + 1 : 1,
                    Message = "BveEx（拡張構文）が使用されています。実行環境にBveExがインストールされているか、またはインストール先が特定できない場合があります。"
                });

                // BveEx の行は標準の未知コマンドチェックへ進まないようにする（あるいはスキップする）
                // ここで return するか、以降のチェックをスキップするフラグを立てます
                return;
            }
// 9. 修正：未知のコマンド（登録されていないキーワード）のチェック
            string codeToCheck = trimmedCodePart;

            // 行頭から最初のセミコロン（;）までが「距離程（数値、演算子、distance等）」であるかを判定して除外する
            // 例: "0;", "10+20;", "distance + 100;" などを正確にキャッチして取り除く
            var distanceMatch = Regex.Match(trimmedCodePart, @"^([\d\.\+\-\*\/\s\(\)]+|distance[\s\+\-\*\/\s\d\.\(\+\-\*\/]*)?\s*;", RegexOptions.IgnoreCase);
            if (distanceMatch.Success && distanceMatch.Index == 0)
            {
                // 距離程部分を切り捨てる（残った後ろのコード部分だけでコマンドチェックを行う）
                codeToCheck = trimmedCodePart.Substring(distanceMatch.Length).Trim();
            }

            // 距離程を取り除いた結果、行が空でない場合のみコマンドチェックを実行
            if (!string.IsNullOrEmpty(codeToCheck))
            {
                // ★ 追加：'100 = $too;' や '$foo = 1;' のように '=' や '$' が含まれる行は
                // 変数の代入・定義・参照に関する記述であるため、未知のコマンドチェックから除外する
                if (codeToCheck.Contains("$") || codeToCheck.Contains("="))
                {
                    // 変数関連の記述のためチェックをスキップ
                }
                else
                {
                    var matchCmd = Regex.Match(codeToCheck, @"\b([A-Za-z0-9]+\.[A-Za-z0-9]+|[A-Za-z0-9]+)\b");
                    if (matchCmd.Success)
                    {
                        string commandName = matchCmd.Groups[1].Value;

                        // validCommands は Parse 開始時に取得済みでここへ渡される
                        if (validCommands != null && validCommands.Count > 0 && !validCommands.Contains(commandName))
                        {
                            int index = trimmedCodePart.IndexOf(commandName, StringComparison.OrdinalIgnoreCase);
                            errors.Add(new ParseError
                            {
                                Level = "エラー",
                                Line = lineNumber,
                                Column = index >= 0 ? index + 1 : 1,
                                Message = $"未知のコマンドまたは存在しない単語です: '{commandName}' 本体側：{commandName} doesn't exist"
                            });
                        }
                    }
                }
                        }
        }

    }
}
