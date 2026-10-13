using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace BveMapEditor
{
    // エラー情報モデル
    public class ParseError
    {
        public int Line { get; set; }
        public int Column { get; set; }
        public string Message { get; set; } = string.Empty;
    }
    internal class MapParser
    {
        // よく使われるBVEマップの名前空間一覧（ドット抜けチェック用）
        private static readonly string[] KnownNamespaces = {
            "Sound", "Signal", "Structure","Background", "Fog", "Light", "Gradient", "Curve", "Beacon", "Section","Train","DrawDistance","CabIlluminance","Irregularity","Adhesion","RollingNoise","JointNoise","FlangeNoise"
        };
        // 配列・キー指定（[]）が本来必要な名前空間
        private static readonly string[] KeyRequiredNamespaces = {
            "Sound", "Structure", "Signal","Repeater","Sound3D","Track","Train","Station"
        };
        // ★ baseDirectory（マップファイルのあるフォルダのパス）を受け取れるようにオーバーロードを追加
        public static List<ParseError> Parse(string text)
        {
            return Parse(text, string.Empty);
        }
        public static List<ParseError> Parse(string text, string baseDirectory)
        {
            var errors = new List<ParseError>();

            if (string.IsNullOrWhiteSpace(text))
            {
                errors.Add(new ParseError
                {
                    Line = 1,
                    Column = 1,
                    Message = "ファイルが空です。"
                });
                return errors;
            }

            using (var reader = new StringReader(text))
            {
                string? line;
                int lineNumber = 0;
                bool headerFound = false;
                bool stationLoadFound = false; // Station.Load が見つかったかどうかのフラグ
                bool soundLoadFound = false; // Sound.Load が見つかったかどうかのフラグ
                bool structureLoadFound = false; // Sound.Load が見つかったかどうかのフラグ
                bool sounddLoadFound = false; // Sound3D.Load が見つかったかどうかのフラグ
                bool signalLoadFound = false; // Signal.Load が見つかったかどうかのフラグ
                // ★ 追加：これまでに距離程（0; など）が登場したかどうかを記録するフラグ
                bool hasHitDistance = false;
                // ★ 追加：最初に登場した行番号を記録する変数
                int firstStationLoadLine = -1;
                int firstSoundLoadLine = -1;
                // ★ 重複チェック用：これまでに登場したLoad系コマンド（または完全一致のLoad文）を記録するセット
                var loadedCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                while ((line = reader.ReadLine()) != null)
                {
                    lineNumber++;
                    string trimmedLine = line.Trim();

                    // 空行はスキップ
                    if (string.IsNullOrEmpty(trimmedLine))
                    {
                        continue;
                    }
                    // # または // で始まる行（コメント行・ディレクティブ行）はスキップ
                    if (trimmedLine.StartsWith("//") || trimmedLine.StartsWith("#"))
                    {
                        continue;
                    }
                    // ★ 1. 1行目のヘッダーチェック
                    if (lineNumber == 1)
                    {
                        if (string.IsNullOrEmpty(trimmedLine) || !trimmedLine.StartsWith("BveTs Map", StringComparison.OrdinalIgnoreCase))
                        {
                            errors.Add(new ParseError
                            {
                                Line = 1,
                                Column = 1,
                                Message = "マップファイルの1行目に有効なヘッダー（'BveTs Map 2.02' など）がありません。"
                            });
                        }
                        else
                        {
                            headerFound = true;
                        }
                        continue;
                    }

                    // 行末のインラインコメント ( // ... ) を除外した有効なコード部分を抽出
                    string codePart = GetCodePartWithoutComment(line);
                    string trimmedCodePart = codePart.Trim();

                    // コメントを除去した結果、空行になった場合はチェック不要
                    if (string.IsNullOrEmpty(trimmedCodePart))
                    {
                        continue;
                    }
                    // ★ 2. 2行目以降にヘッダーが記述されている場合のエラー検知
                    if (trimmedLine.StartsWith("BveTs Map", StringComparison.OrdinalIgnoreCase))
                    {
                        errors.Add(new ParseError
                        {
                            Line = lineNumber,
                            Column = 1,
                            Message = "マップヘッダーが重複しているか、1行目以外の場所に記述されています（ヘッダーは必ず1行目に記述してください）。"
                        });
                        continue;
                    }
                    // ★ 追加：この行が「距離程の指定（0;, 100;, distance + 10; など）」から始まっているかチェック
                    var distanceCheckMatch = Regex.Match(trimmedLine, @"^([\d\.\+\-\*\/\s\(\)]+|distance[\s\+\-\*\/\s\d\.\(\+\-\*\/]*)?\s*;", RegexOptions.IgnoreCase);
                    if (distanceCheckMatch.Success && distanceCheckMatch.Index == 0)
                    {
                        hasHitDistance = true;
                    }
                    // ★ Station.Load の検出チェック（大文字小文字を区別しない）
                    if (trimmedCodePart.Contains("Station.Load", StringComparison.OrdinalIgnoreCase))
                    {
                        stationLoadFound = true;
                    }
                    // ★ Sound.Load の検出チェック（大文字小文字を区別しない）
                    if (trimmedCodePart.Contains("Sound.Load", StringComparison.OrdinalIgnoreCase))
                    {
                        soundLoadFound = true;
                    }
                    // ★ Sound.Load の検出チェック（大文字小文字を区別しない）
                    if (trimmedCodePart.Contains("Signal.Load", StringComparison.OrdinalIgnoreCase))
                    {
                        signalLoadFound = true;
                    }
                    // ★ Sound.Load の検出チェック（大文字小文字を区別しない）
                    if (trimmedCodePart.Contains("Structure.Load", StringComparison.OrdinalIgnoreCase))
                    {
                        structureLoadFound = true;
                    }

                    // ★ Sound.Load の検出チェック（大文字小文字を区別しない）
                    if (trimmedCodePart.Contains("Sound3D.Load", StringComparison.OrdinalIgnoreCase))
                    {
                        sounddLoadFound = true;
                    }
                    // Station.Load の検出チェック
                    if (trimmedCodePart.Contains("Station.Load", StringComparison.OrdinalIgnoreCase))
                    {
                        stationLoadFound = true;
                        if (firstStationLoadLine == -1)
                        {
                            firstStationLoadLine = lineNumber;
                        }
                    }

                    // ★ 追加：Sound.Load の初出行を記録
                    if (Regex.IsMatch(trimmedCodePart, $@"\bSound\.Load\b", RegexOptions.IgnoreCase))
                    {
                        if (firstSoundLoadLine == -1)
                        {
                            firstSoundLoadLine = lineNumber;
                        }
                    }
                    // ★ 追加：すでに距離程（0;など）が登場した後に Load系コマンドが置かれている場合の警告
                    if (Regex.IsMatch(trimmedCodePart, $@"\b[A-Za-z0-9]+\.Load\b", RegexOptions.IgnoreCase))
                    {
                        if (hasHitDistance)
                        {
                            errors.Add(new ParseError
                            {
                                Line = lineNumber,
                                Column = 1,
                                Message = "Load系コマンドは、距離程（0;など）よりも前のファイル上部に配置してください。"
                            });
                        }
                    }
                    // 1. 括弧の対チェック（コメント除外済みの文字列で実施）
                    CheckBrackets(codePart, lineNumber, errors);

                    // 2. セミコロン（;）チェック（コメント除外済みの文字列で実施）
                    CheckSemicolon(trimmedCodePart, lineNumber, errors);
                    // 3. ★ 追加：セミコロンの連続（2個以上）チェック
                    CheckConsecutiveSemicolons(trimmedCodePart, lineNumber, errors);
                    // 4. ★ 追加：括弧の連続（2個以上）チェック
                    CheckConsecutiveBrackets(trimmedCodePart, lineNumber, errors);
                    // 5. 構文・コマンド形式チェック（ドット抜けなどのミスを検出）
                    CheckCommandSyntax(trimmedCodePart, lineNumber, baseDirectory, errors);
                    // 6. ★ 追加：Load系コマンドの重複チェック
                    CheckDuplicateLoad(trimmedCodePart, lineNumber, loadedCommands, errors);
                    // 7. ★ 追加：全角スペースのチェック（コメント部分を除いたコード内を対象）
                    CheckFullWidthSpace(codePart, lineNumber, errors);
                }
                if (!headerFound)
                {
                    errors.Add(new ParseError
                    {
                        Line = 1,
                        Column = 1,
                        Message = "マップファイルにヘッダーがありません。"
                    });
                }
                // ★ ファイル全体を通して Station.Load が一度も出現しなかった場合のエラー追加
                if (!stationLoadFound)
                {
                    errors.Add(new ParseError
                    {
                        Line = 1,
                        Column = 1,
                        Message = "必須の 'Station.Load' が見つかりません。停車場リストの読み込み定義を追加してください。"
                    });
                }
                // ★ 追加：Station.Load と Sound.Load の記述順序チェック
                // （Station.Load が Sound.Load よりも上の行にある場合に警告）
                if (firstStationLoadLine > 0 && firstSoundLoadLine > 0 && firstStationLoadLine < firstSoundLoadLine)
                {
                    errors.Add(new ParseError
                    {
                        Line = firstStationLoadLine,
                        Column = 1,
                        Message = "'Station.Load' が 'Sound.Load' よりも前に記述されています。'Sound.Load' を先に記述してください。"
                    });
                }
                if (!soundLoadFound)
                {
                    errors.Add(new ParseError
                    {
                        Line = 1,
                        Column = 1,
                        Message = "必須の 'Sound.Load' が見つかりません。サウンドリストの読み込み定義を追加してください。"
                    });
                }
                if (!sounddLoadFound)
                {
                    errors.Add(new ParseError
                    {
                        Line = 1,
                        Column = 1,
                        Message = "必須の 'Sound3D.Load' が見つかりません。サウンド3Dリストの読み込み定義を追加してください。"
                    });
                }
                if (!structureLoadFound)
                {
                    errors.Add(new ParseError
                    {
                        Line = 1,
                        Column = 1,
                        Message = "必須の 'Structure.Load' が見つかりません。ストラクチャリストの読み込み定義を追加してください。"
                    });
                }
                if (!signalLoadFound)
                {
                    errors.Add(new ParseError
                    {
                        Line = 1,
                        Column = 1,
                        Message = "必須の 'Signal.Load' が見つかりません。信号リストの読み込み定義を追加してください。"
                    });
                }

            }

            return errors;
        }
        // 行から '//' 以降のインラインコメントを除去して有効コードのみを取得
        private static string GetCodePartWithoutComment(string line)
        {
            int slashCommentIndex = line.IndexOf("//");
            int hashCommentIndex = line.IndexOf("#");
            int commentIndex = -1;

            if (slashCommentIndex >= 0 && hashCommentIndex >= 0)
            {
                // 先に現れた方をコメント開始位置とする
                commentIndex = Math.Min(slashCommentIndex, hashCommentIndex);
            }
            else if (slashCommentIndex >= 0)
            {
                commentIndex = slashCommentIndex;
            }
            else if (hashCommentIndex >= 0)
            {
                commentIndex = hashCommentIndex;
            }

            if (commentIndex >= 0)
            {
                return line.Substring(0, commentIndex);
            }
            return line;
        }
        // 括弧の対（丸括弧 (), 角括弧 []）が正しく閉じられているか確認
        private static void CheckBrackets(string line, int lineNumber, List<ParseError> errors)
        {
            int roundCount = 0; // ()
            int squareCount = 0; // []

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '(') roundCount++;
                else if (c == ')') roundCount--;
                else if (c == '[') squareCount++;
                else if (c == ']') squareCount--;

                if (roundCount < 0)
                {
                    errors.Add(new ParseError
                    {
                        Line = lineNumber,
                        Column = i + 1,
                        Message = "対応する '(' がない閉じ括弧 ')' が存在します。"
                    });
                    roundCount = 0;
                }

                if (squareCount < 0)
                {
                    errors.Add(new ParseError
                    {
                        Line = lineNumber,
                        Column = i + 1,
                        Message = "対応する '[' がない閉じ括弧 ']' が存在します。"
                    });
                    squareCount = 0;
                }
            }

            if (roundCount > 0)
            {
                errors.Add(new ParseError
                {
                    Line = lineNumber,
                    Column = line.Length,
                    Message = "閉じ括弧 ')' が不足しています。"
                });
            }

            if (squareCount > 0)
            {
                errors.Add(new ParseError
                {
                    Line = lineNumber,
                    Column = line.Length,
                    Message = "閉じ括弧 ']' が不足しています。"
                });
            }
        }

        // 行末のセミコロン忘れ簡易チェック
        private static void CheckSemicolon(string trimmedCodePart, int lineNumber, List<ParseError> errors)
        {
            // ブロック区切り '{' や '}' 以外で、行末が ';' で終わっていないか判定
            if (!trimmedCodePart.EndsWith(";") && !trimmedCodePart.EndsWith("{") && !trimmedCodePart.EndsWith("}"))
            {
                // include や # などのディレクティブを除外
                if (!trimmedCodePart.StartsWith("#") && !trimmedCodePart.StartsWith("include", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add(new ParseError
                    {
                        Line = lineNumber,
                        Column = trimmedCodePart.Length + 1,
                        Message = "文末のセミコロン ';' が不足しています。"
                    });
                }
            }
        }
        // ★ 追加：セミコロン（;）が2個以上連続しているかをチェックするメソッド
        private static void CheckConsecutiveSemicolons(string trimmedCodePart, int lineNumber, List<ParseError> errors)
        {
            // セミコロンの間にスペースやタブが含まれていても、連続して出現している場合を検出する (例: ';;', '; ;' など)
            if (Regex.IsMatch(trimmedCodePart, @";\s*;"))
            {
                int index = trimmedCodePart.IndexOf(';');
                errors.Add(new ParseError
                {
                    Line = lineNumber,
                    Column = index >= 0 ? index + 1 : 1,
                    Message = "セミコロン ';' が複数連続して記述されています。"
                });
            }
        }
        // ★ 追加：括弧（() や []）が2個以上連続しているかをチェックするメソッド
        private static void CheckConsecutiveBrackets(string trimmedCodePart, int lineNumber, List<ParseError> errors)
        {
            // 丸括弧や角括弧が連続している場合を検出 (例: '((', '))', '[[', ']]', またはペアの連続)
            if (Regex.IsMatch(trimmedCodePart, @"\(\s*\(\vert{}\)\s*\)|\[\s*\[\vert{}\]\s*\]|\(\s*\)\s*\(\s*\)"))
            {
                int index = trimmedCodePart.IndexOfAny(new char[] { '(', ')', '[', ']' });
                errors.Add(new ParseError
                {
                    Line = lineNumber,
                    Column = index >= 0 ? index + 1 : 1,
                    Message = "括弧（'('、')'、'['、']'）が複数連続して記述されています。"
                });
            }
        }
        // 構文・コマンド形式チェック（ドット抜け、および角括弧抜けを検出）
        private static void CheckCommandSyntax(string trimmedCodePart, int lineNumber, string baseDirectory, List<ParseError> errors)
        {
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
                            Line = lineNumber,
                            Column = index + 1,
                            Message = "角括弧 '[]' 内の文字列を囲むアポストロフィ（' または \"）が片方しかありません。"
                        });
                    }
                    else if (!startsWithQuote && !endsWithQuote)
                    {
                        errors.Add(new ParseError
                        {
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
                            Line = lineNumber,
                            Column = openParen + 1,
                            Message = "Load系コマンドの引数を囲むアポストロフィ（' または \"）が片方しかありません。"
                        });
                    }
                    else if (!startsWithQuote && !endsWithQuote)
                    {
                        errors.Add(new ParseError
                        {
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
                                Line = lineNumber,
                                Column = openParen + 1,
                                Message = errorMessage
                            });
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
                var matchCmd = Regex.Match(codeToCheck, @"\b([A-Za-z0-9]+\.[A-Za-z0-9]+|[A-Za-z0-9]+)\b");
                if (matchCmd.Success)
                {
                    string commandName = matchCmd.Groups[1].Value;

                    var validCommands = BveSyntaxProvider.GetValidCommands();

                    if (validCommands.Count > 0 && !validCommands.Contains(commandName))
                    {
                        int index = trimmedCodePart.IndexOf(commandName, StringComparison.OrdinalIgnoreCase);
                        errors.Add(new ParseError
                        {
                            Line = lineNumber,
                            Column = index >= 0 ? index + 1 : 1,
                            Message = $"未知のコマンドまたは存在しない単語です: '{commandName}'"
                        });
                    }
                }
            }
        }
        // ★ 追加：同じ Load コマンドが重複して記述されているかを検出するメソッド
        private static void CheckDuplicateLoad(string trimmedCodePart, int lineNumber, HashSet<string> loadedCommands, List<ParseError> errors)
        {
            // .Load を含む行を対象とする（例: Station.Load, Sound.Load など）
            if (Regex.IsMatch(trimmedCodePart, $@"\b[A-Za-z0-9]+\.Load\b", RegexOptions.IgnoreCase))
            {
                // 同じ Load コマンドの記述（または同一の行全体の記述）が既に存在するかチェック
                // ここでは安全のため、完全一致したLoad文全体、またはコマンド名をキーにします
                // 例として「行全体（またはセミコロンまでの文）」を重複キーとして判定します
                string loadKey = trimmedCodePart.TrimEnd(';'); // 末尾のセミコロンを除いて比較

                if (loadedCommands.Contains(loadKey))
                {
                    errors.Add(new ParseError
                    {
                        Line = lineNumber,
                        Column = 1,
                        Message = $"同じ Load 定義（'{loadKey}'）が重複して記述されています。"
                    });
                }
                else
                {
                    loadedCommands.Add(loadKey);
                }
            }
        }
        // ★ 追加：コード内に全角スペースが含まれていないかチェックするメソッド
        private static void CheckFullWidthSpace(string codePart, int lineNumber, List<ParseError> errors)
        {
            for (int i = 0; i < codePart.Length; i++)
            {
                char c = codePart[i];

                // 文字コードが 127 を超える文字（ASCII範囲外＝全角・マルチバイト文字）を検出
                // ※もしファイルパスや文字列リテラル内の全角（日本語ファイル名など）を許容したい場合は、
                //   必要に応じてクォート内を除外する調整が可能です。
                if (c > 127)
                {
                    errors.Add(new ParseError
                    {
                        Line = lineNumber,
                        Column = i + 1,
                        Message = $"コード内に全角文字（またはマルチバイト文字: '{c}'）が含まれています。半角文字を使用してください。"
                    });
                }
            }
        }
    }
}
