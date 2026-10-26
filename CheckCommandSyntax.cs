using System.IO;
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
            // 2. 各種構文チェックの実行
            CheckNamespaceDot(trimmedCodePart, lineNumber, errors);
            CheckKeyRequiredBrackets(trimmedCodePart, lineNumber, errors);
            CheckBracketContent(trimmedCodePart, lineNumber, errors);
            CheckLoadCommandFile(trimmedCodePart, lineNumber, baseDirectory, lineNumber, errors);
            CheckRequiredArguments(trimmedCodePart, lineNumber, errors);
            CheckInvalidParentheses(trimmedCodePart, lineNumber, errors);
            CheckNoArgMethods(trimmedCodePart, lineNumber, errors);

            // 8. include 構文のチェック
            if (CheckIncludeSyntax(trimmedCodePart, baseDirectory, lineNumber, errors)) return;

            // 9-1. BveEx 構文のチェック
            if (CheckBveExSyntax(trimmedCodePart, lineNumber, errors)) return;

            // 9. 未知のコマンドチェック
            CheckUnknownCommand(trimmedCodePart, validCommands, lineNumber, errors);
        }
        // ==========================================
        // 個別のチェック処理（プライベートメソッド）
        // ==========================================

        private static void CheckNamespaceDot(string code, int line, List<ParseError> errors)
        {
            foreach (var ns in KnownNamespaces)
            {
                if (Regex.IsMatch(code, $@"\b{ns}[A-Za-z]+\s*\(", RegexOptions.IgnoreCase))
                {
                    if (!Regex.IsMatch(code, $@"\b{ns}\.[A-Za-z]+\s*\(", RegexOptions.IgnoreCase) &&
                        !Regex.IsMatch(code, $@"\b{ns}\s*\(.*?\)", RegexOptions.IgnoreCase))
                    {
                        int index = code.IndexOf(ns, StringComparison.OrdinalIgnoreCase);
                        errors.Add(CreateError("エラー", line, index, $"'{ns}' の後にドット '.' が抜けているか、コマンドの記述形式が不正です（例: '{ns}.Load' のように記述してください）。"));
                    }
                }
            }
        }

        private static void CheckKeyRequiredBrackets(string code, int line, List<ParseError> errors)
        {
            foreach (var ns in KeyRequiredNamespaces)
            {
                if (Regex.IsMatch(code, $@"\b{ns}\.[A-Za-z0-9]+", RegexOptions.IgnoreCase) ||
                    Regex.IsMatch(code, $@"\b{ns}\s*\[", RegexOptions.IgnoreCase))
                {
                    if (Regex.IsMatch(code, $@"\b{ns}\.(Load|Set)\b", RegexOptions.IgnoreCase) ||
                        Regex.IsMatch(code, $@"\bSignal\.SpeedLimit\b", RegexOptions.IgnoreCase) ||
                        Regex.IsMatch(code, $@"\bTrain\.Add\b", RegexOptions.IgnoreCase))
                    {
                        continue;
                    }

                    if (!Regex.IsMatch(code, $@"\b{ns}\s*\[", RegexOptions.IgnoreCase))
                    {
                        int index = code.IndexOf(ns, StringComparison.OrdinalIgnoreCase);
                        errors.Add(CreateError("エラー", line, index, $"'{ns}' の指定に角括弧 '[]'（キーまたはインデックス）が不足している可能性があります。"));
                    }
                }
            }
        }
        private static void CheckBracketContent(string code, int line, List<ParseError> errors)
        {
            var matches = Regex.Matches(code, $@"\b(Sound|Structure|Signal|Track|Station|Repeater|Sound3D|Train)\s*\[(.*?)\]", RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                string innerContent = match.Groups[2].Value.Trim();
                int index = match.Index;

                if (string.IsNullOrEmpty(innerContent) || innerContent == "''" || innerContent == "\"\"")
                {
                    errors.Add(CreateError("エラー", line, index, "角括弧 '[]' の中にキー（またはインデックス）が指定されていません。"));
                    continue;
                }

                bool startsWithQuote = innerContent.StartsWith("'") || innerContent.StartsWith("\"");
                bool endsWithQuote = innerContent.EndsWith("'") || innerContent.EndsWith("\"");

                if (!int.TryParse(innerContent, out _))
                {
                    if (startsWithQuote != endsWithQuote)
                    {
                        errors.Add(CreateError("エラー", line, index, "角括弧 '[]' 内の文字列を囲むアポストロフィ（' または \"）が片方しかありません。"));
                    }
                    else if (!startsWithQuote && !endsWithQuote)
                    {
                        errors.Add(CreateError("エラー", line, index, "角括弧 '[]' 内のキーはアポストロフィ等で囲むか、有効な数値を指定してください。"));
                    }
                }
            }
        }
        private static void CheckLoadCommandFile(string code, int line, string baseDirectory, int lineNumber, List<ParseError> errors)
        {
            if (!Regex.IsMatch(code, $@"\b[A-Za-z0-9]+\.Load\b", RegexOptions.IgnoreCase)) return;

            int openParen = code.IndexOf('(');
            int closeParen = code.LastIndexOf(')');
            if (openParen < 0 || closeParen <= openParen) return;

            string innerContent = code.Substring(openParen + 1, closeParen - openParen - 1).Trim();
            if (string.IsNullOrEmpty(innerContent) || innerContent == "''" || innerContent == "\"\"")
            {
                errors.Add(CreateError("エラー", line, openParen, "Load系コマンドの引数（丸括弧内）にファイルパスが指定されていません。"));
                return;
            }

            bool startsWithQuote = innerContent.StartsWith("'") || innerContent.StartsWith("\"");
            bool endsWithQuote = innerContent.EndsWith("'") || innerContent.EndsWith("\"");

            if (startsWithQuote != endsWithQuote)
            {
                errors.Add(CreateError("エラー", line, openParen, "Load系コマンドの引数を囲むアポストロフィ（' または \"）が片方しかありません。"));
            }
            else if (!startsWithQuote && !endsWithQuote)
            {
                errors.Add(CreateError("エラー", line, openParen, "Load系コマンドの引数（ファイルパス等）はアポストロフィ（' または \"）で囲む必要があります。"));
            }
            else if (!string.IsNullOrEmpty(baseDirectory))
            {
                string filePath = innerContent.Trim('\'', '"');
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    errors.Add(CreateError("エラー", line, openParen, "ファイルパスが空です。"));
                    return;
                }

                try
                {
                    string fullPath = Path.GetFullPath(Path.Combine(baseDirectory, filePath));
                    if (!File.Exists(fullPath))
                    {
                        errors.Add(CreateError("エラー", line, openParen, $"指定されたファイルが見つかりません: '{filePath}'"));
                    }
                }
                catch
                {
                    errors.Add(CreateError("エラー", line, openParen, $"ファイルパスの形式が不正です: '{filePath}'"));
                }
            }
        }


        private static void CheckRequiredArguments(string code, int line, List<ParseError> errors)
        {
            // ★ 今後追加したい「引数が必須なコマンド」をここにどんどん登録できます！
            var requiredArgCommands = new Dictionary<string, string>
            {
                { "Background.Change", "Background.Change の引数に背景キーが指定されていません。" },
                { "Curve.Gauge",       "Curve.Gauge の引数にゲージ幅（数値）が指定されていません。" },
                { "Begin0",            "Begin0 の引数にパラメータが指定されていません。" },
                { "Begin",            "Begin の引数にパラメータが指定されていません。" },
                { "Put",            "Put の引数にパラメータが指定されていません。" },
                { "Put0",            "Put0 の引数にパラメータが指定されていません。" }, 
                { "PutBetween",            "PutBetween の引数にパラメータが指定されていません。" }, 
                { "SetGauge",            "SetGauge の引数にパラメータが指定されていません。" }, 
                { "Gauge",            "Gauge の引数にパラメータが指定されていません。" }, 
                { "SetFunction",            "SetFunction の引数にパラメータが指定されていません。" },
                { "Curve.Change",            "Curve.Change の引数にパラメータが指定されていません。" },
                { "Adhesion.Change",            "Adhesion.Change の引数にパラメータが指定されていません。" },
                { "Fog.Interpolate",            "Fog.Interpolate の引数にパラメータが指定されていません。" },
                { "Light.Ambient",            "Light.Ambient の引数にパラメータが指定されていません。" },
                { "Light.Diffuse",            "Light.Diffuse の引数にパラメータが指定されていません。" }, 
                { "Light.Direction",            "Light.Direction の引数にパラメータが指定されていません。" },
                { "Stop",            "Stop の引数にパラメータが指定されていません。" }, 
                { "Pass",            "Pass の引数にパラメータが指定されていません。" }, 
                { "Enable",            "Enable の引数にパラメータが指定されていません。" }, 
                { "Center",            "Center の引数にパラメータが指定されていません。" }, 
                // 例：ここに新しいコマンドとメッセージをどんどん追加できます
            };

            foreach (var pair in requiredArgCommands)
            {
                if (Regex.IsMatch(code, $@"\b{Regex.Escape(pair.Key)}\b", RegexOptions.IgnoreCase))
                {
                    ValidateCommandHasArguments(code, line, pair.Value, "警告", errors);
                }
            }
        }
        private static void CheckInvalidParentheses(string code, int line, List<ParseError> errors)
        {
            foreach (var ns in KeyRequiredNamespaces)
            {
                string pattern = $@"\b{ns}\s*\((.*?)\)";
                if (Regex.IsMatch(code, pattern, RegexOptions.IgnoreCase))
                {
                    if (!Regex.IsMatch(code, $@"\b{ns}\.(Load|Set)\b", RegexOptions.IgnoreCase))
                    {
                        int index = code.IndexOf(ns, StringComparison.OrdinalIgnoreCase);
                        errors.Add(CreateError("エラー", line, index, $"'{ns}' の指定には丸括弧 '()' ではなく、角括弧 '[]' を使用してください。"));
                    }
                }
            }
        }

        private static void CheckNoArgMethods(string code, int line, List<ParseError> errors)
        {
            var noArgMethods = new Dictionary<string, string>
            {
                { "Play", ".Play() の引数には何も指定できません。" },
                { "BeginTransition", ".BeginTransition() の引数には何も指定できません。" },
                { "End", ".End() の引数には何も指定できません。" },
            };

            foreach (var pair in noArgMethods)
            {
                string pattern = $@"\.{pair.Key}\s*\((.*?)\)";
                foreach (Match match in Regex.Matches(code, pattern, RegexOptions.IgnoreCase))
                {
                    string innerContent = match.Groups[1].Value.Trim();
                    if (!string.IsNullOrEmpty(innerContent) && innerContent != "''" && innerContent != "\"\"")
                    {
                        errors.Add(CreateError("エラー", line, match.Index, pair.Value));
                    }
                }
            }
        }
        private static bool CheckIncludeSyntax(string code, string baseDirectory, int line, List<ParseError> errors)
        {
            if (!Regex.IsMatch(code, @"\binclude\b", RegexOptions.IgnoreCase)) return false;

            if (Regex.IsMatch(code, @"\binclude\s*=", RegexOptions.IgnoreCase))
            {
                int index = code.IndexOf("include", StringComparison.OrdinalIgnoreCase);
                errors.Add(CreateError("エラー", line, index, "'include' の後に不要な '=' が含まれています。"));
                return true;
            }

            var match = Regex.Match(code, @"\binclude\s+(['""])(.*?)\1", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                int index = code.IndexOf("include", StringComparison.OrdinalIgnoreCase);
                errors.Add(CreateError("エラー", line, index, "'include' の引数はアポストロフィ等で囲む必要があります。"));
                return true;
            }

            string filePath = match.Groups[2].Value.Trim();
            int matchIndex = match.Index;

            if (string.IsNullOrEmpty(filePath))
            {
                errors.Add(CreateError("エラー", line, matchIndex, "'include' の引数にファイルパスが指定されていません。"));
                return true;
            }

            if (!filePath.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(CreateError("警告", line, matchIndex, "'include' で指定できるファイルは拡張子が '.txt' のファイルのみです。"));
                return true;
            }
            if (!string.IsNullOrEmpty(baseDirectory))
            {
                try
                {
                    string fullPath = Path.GetFullPath(Path.Combine(baseDirectory, filePath));
                    if (!File.Exists(fullPath))
                    {
                        errors.Add(CreateError("エラー", line, matchIndex, $"インクルード先のファイルが見つかりません: '{filePath}'"));
                    }
                }
                catch
                {
                    errors.Add(CreateError("警告", line, matchIndex, $"ファイルパスの形式が不正です: '{filePath}'"));
                }
            }
            return true;
        }
        private static bool CheckBveExSyntax(string code, int line, List<ParseError> errors)
        {
            if (Regex.IsMatch(code, $@"\bBveEx(\.User)?\b", RegexOptions.IgnoreCase))
            {
                int index = code.IndexOf("BveEx", StringComparison.OrdinalIgnoreCase);
                errors.Add(CreateError("注意", line, index, "BveEx（拡張構文）が使用されています。実行環境をご確認ください。"));
                return true;
            }
            return false;
        }

        private static void CheckUnknownCommand(string code, HashSet<string> validCommands, int line, List<ParseError> errors)
        {
            string codeToCheck = code;
            var distanceMatch = Regex.Match(code, @"^([\d\.\+\-\*\/\s\(\)]+|distance[\s\+\-\*\/\s\d\.\(\+\-\*\/]*)?\s*;", RegexOptions.IgnoreCase);
            if (distanceMatch.Success && distanceMatch.Index == 0)
            {
                codeToCheck = code.Substring(distanceMatch.Length).Trim();
            }

            if (string.IsNullOrEmpty(codeToCheck)) return;

            if (codeToCheck.Contains("$") || codeToCheck.Contains("=")) return;

            var matchCmd = Regex.Match(codeToCheck, @"\b([A-Za-z0-9]+\.[A-Za-z0-9]+|[A-Za-z0-9]+)\b");
            if (matchCmd.Success)
            {
                string commandName = matchCmd.Groups[1].Value;
                if (validCommands != null && validCommands.Count > 0 && !validCommands.Contains(commandName))
                {
                    int index = code.IndexOf(commandName, StringComparison.OrdinalIgnoreCase);
                    errors.Add(CreateError("エラー", line, index, $"未知のコマンドまたは存在しない単語です: '{commandName}'"));
                }
            }
        }
        // 補助ヘルパーメソッド
        private static ParseError CreateError(string level, int line, int index, string message)
        {
            return new ParseError
            {
                Level = level,
                Line = line,
                Column = index >= 0 ? index + 1 : 1,
                Message = message
            };
        }

        private static void ValidateCommandHasArguments(string code, int line, string errorMessage, string level, List<ParseError> errors)
        {
            int openParen = code.IndexOf('(');
            int closeParen = code.LastIndexOf(')');
            if (openParen >= 0 && closeParen > openParen)
            {
                string innerContent = code.Substring(openParen + 1, closeParen - openParen - 1).Trim();
                if (string.IsNullOrEmpty(innerContent) || innerContent == "''" || innerContent == "\"\"")
                {
                    errors.Add(CreateError(level, line, openParen, errorMessage));
                }
            }
        }
    }
}
