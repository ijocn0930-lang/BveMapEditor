using System;
using System.Collections.Generic;
using System.Text;

namespace BveMapEditor
{
    public partial class MapParser
    {

        // =================================================================
        //  辞書を使ったスマートな引数検証エンジン
        // =================================================================
        private static void CheckCommandArguments(string trimmedCodePart, int lineNumber, List<ParseError> errors)
        {
            string commandName = GetCommandName(trimmedCodePart);
            string argsStr = GetInnerArguments(trimmedCodePart);
            List<string> args = SplitArguments(argsStr);

            // ルール辞書から該当するコマンドを探す
            CommandRule? rule = null;

            if (CommandRules.TryGetValue(commandName, out var exactRule))
            {
                rule = exactRule;
            }
            else if (commandName.EndsWith(".Begin", StringComparison.OrdinalIgnoreCase) && commandName.StartsWith("Repeater", StringComparison.OrdinalIgnoreCase))
            {
                // Repeater[...].Begin のような特殊プレフィックス系をキャッチ
                if (CommandRules.TryGetValue("Repeater.Begin", out var repeaterRule))
                {
                    rule = repeaterRule;
                }
            }

            // 登録されていないコマンドの場合はチェックをスルー
            if (rule == null) return;

            // 0個の許可設定があり、実際に0個の場合はバリデーションをスキップ
            if (rule.AllowZeroArgs && args.Count == 0) return;

            bool isFixedCount = (rule.MinArgs == rule.MaxArgs);
            bool isInvalidCount = isFixedCount
                ? (args.Count != rule.MinArgs)
                : (args.Count < rule.MinArgs || args.Count > rule.MaxArgs);

            if (isInvalidCount)
            {
                string message;
                if (isFixedCount)
                {
                    message = $"{commandName} の引数は {rule.MinArgs} 個である必要があります（現在: {args.Count}個）。本体側：指定されたパラメーターの数が期待された数と一致しませんでした。";
                }
                else
                {
                    string maxStr = (rule.MaxArgs == int.MaxValue) ? "無制限" : rule.MaxArgs.ToString();
                    message = $"{commandName} の引数の数は {rule.MinArgs}〜{maxStr} 個である必要があります（現在: {args.Count}個）。本体側：指定されたパラメーターの数が期待された数と一致しませんでした。";
                }

                errors.Add(new ParseError
                {
                    Level = "エラー",
                    Line = lineNumber,
                    Column = 1,
                    Message = message
                });
                return;
            }
            // 指定されたインデックスの数値チェック
            if (rule.AllArgsNumeric)
            {
                // ★ スイッチがオンのときは、入力されたすべての引数を上から順に数値チェックする
                for (int idx = 0; idx < args.Count; idx++)
                {
                    string arg = args[idx];
                    if (string.IsNullOrWhiteSpace(arg) || string.Equals(arg, "null", StringComparison.OrdinalIgnoreCase)) continue;
                    if (arg.StartsWith("$")) continue; // 変数参照の場合はスキップ
                    if (!IsNumeric(arg))
                    {
                        errors.Add(new ParseError
                        {
                            Level = "エラー",
                            Line = lineNumber,
                            Column = 1,
                            Message = $"{commandName} の第 {idx + 1} 引数には数値を指定してください（値: '{arg}'）。"
                        });
                    }
                }
            }
            else
            {
                // 従来の個別のインデックス指定による数値チェック
                foreach (int idx in rule.NumericArgIndices)
                {
                    if (idx < args.Count)
                    {
                        string arg = args[idx];
                        if (string.IsNullOrWhiteSpace(arg) || string.Equals(arg, "null", StringComparison.OrdinalIgnoreCase)) continue;

                        if (arg.StartsWith("$")) continue; // 変数参照の場合はスキップ
                        if (!IsNumeric(arg))
                        {
                            errors.Add(new ParseError
                            {
                                Level = "エラー",
                                Line = lineNumber,
                                Column = 1,
                                Message = $"{commandName} の第 {idx + 1} 引数には数値を指定してください（値: '{arg}'）。"
                            });
                        }
                    }
                }
            }
            // カスタムバリデーターがあれば実行（Repeaterの特殊ルールなど）
            rule.CustomValidator?.Invoke(args, lineNumber, commandName, errors);
        }
        private static string GetCommandName(string trimmedCodePart)
        {
            int parenIdx = trimmedCodePart.IndexOf('(');
            if (parenIdx > 0)
            {
                string prefix = trimmedCodePart.Substring(0, parenIdx).Trim();

                if (prefix.StartsWith("Repeater", StringComparison.OrdinalIgnoreCase))
                {
                    if (prefix.EndsWith(".Begin0", StringComparison.OrdinalIgnoreCase)) return "Repeater.Begin0";
                    if (prefix.EndsWith(".Begin", StringComparison.OrdinalIgnoreCase)) return "Repeater.Begin";
                }
                else if (prefix.StartsWith("Structure", StringComparison.OrdinalIgnoreCase))
                {
                    if (prefix.EndsWith(".Put0", StringComparison.OrdinalIgnoreCase)) return "Structure.Put0";
                    if (prefix.EndsWith(".Put", StringComparison.OrdinalIgnoreCase)) return "Structure.Put";
                }
                // ★ ここを追加：Track[...].Cant.SetFunction を "Track.Cant.SetFunction" にまとめる
                else if (prefix.StartsWith("Track", StringComparison.OrdinalIgnoreCase) && prefix.EndsWith(".Cant.SetFunction", StringComparison.OrdinalIgnoreCase))
                {
                    return "Track.Cant.SetFunction";
                }
                else if (prefix.StartsWith("Track", StringComparison.OrdinalIgnoreCase) && prefix.EndsWith(".Cant.SetGauge", StringComparison.OrdinalIgnoreCase))
                {
                    return "Track.Cant.SetGauge";
                }
                else if (prefix.StartsWith("Track", StringComparison.OrdinalIgnoreCase) && prefix.EndsWith(".Cant.SetCenter", StringComparison.OrdinalIgnoreCase))
                {
                    return "Track.Cant.SetCenter";
                }
                else if (prefix.StartsWith("Track", StringComparison.OrdinalIgnoreCase) && prefix.EndsWith(".Cant.Begin", StringComparison.OrdinalIgnoreCase))
                {
                    return "Track.Cant.Begin";
                }
                else if (prefix.StartsWith("Track", StringComparison.OrdinalIgnoreCase) && prefix.EndsWith(".Cant.Interpolate", StringComparison.OrdinalIgnoreCase))
                {
                    return "Track.Cant.Interpolate";
                }
                else if (prefix.StartsWith("Track", StringComparison.OrdinalIgnoreCase) && prefix.EndsWith(".X.Interpolate", StringComparison.OrdinalIgnoreCase))
                {
                    return "Track.X.Interpolate";
                }
                else if (prefix.StartsWith("Track", StringComparison.OrdinalIgnoreCase) && prefix.EndsWith(".Y.Interpolate", StringComparison.OrdinalIgnoreCase))
                {
                    return "Track.Y.Interpolate";
                }
                else if (prefix.StartsWith("Track", StringComparison.OrdinalIgnoreCase) && prefix.EndsWith(".Position", StringComparison.OrdinalIgnoreCase))
                {
                    return "Track.Position";
                }
                else if (prefix.StartsWith("Station", StringComparison.OrdinalIgnoreCase) && prefix.EndsWith(".Put", StringComparison.OrdinalIgnoreCase))
                {
                    return "Station.Put";
                }
                else if (prefix.StartsWith("Signal", StringComparison.OrdinalIgnoreCase) && prefix.EndsWith(".Put", StringComparison.OrdinalIgnoreCase))
                {
                    return "Signal.Put";
                }
                else if (prefix.StartsWith("Sound3D", StringComparison.OrdinalIgnoreCase) && prefix.EndsWith(".Put", StringComparison.OrdinalIgnoreCase))
                {
                    return "Sound3D.Put";
                }
                else if (prefix.StartsWith("Train", StringComparison.OrdinalIgnoreCase) && prefix.EndsWith(".Load", StringComparison.OrdinalIgnoreCase))
                {
                    return "Train.Load";
                }
                else if (prefix.StartsWith("Train", StringComparison.OrdinalIgnoreCase) && prefix.EndsWith(".Enable", StringComparison.OrdinalIgnoreCase))
                {
                    return "Train.Enable";
                }
                else if (prefix.StartsWith("Train", StringComparison.OrdinalIgnoreCase) && prefix.EndsWith(".Stop", StringComparison.OrdinalIgnoreCase))
                {
                    return "Train.Stop";
                }
                return prefix;
            }
            return trimmedCodePart;
        }
        private static string GetInnerArguments(string commandLine)
        {
            int openIdx = commandLine.IndexOf('(');
            int closeIdx = commandLine.LastIndexOf(')');
            if (openIdx >= 0 && closeIdx > openIdx)
            {
                return commandLine.Substring(openIdx + 1, closeIdx - openIdx - 1).Trim();
            }

            return string.Empty;

        }

        private static List<string> SplitArguments(string argsStr)
        {
            var list = new List<string>();
            if (string.IsNullOrEmpty(argsStr)) return list;

            var sb = new System.Text.StringBuilder();
            bool inQuote = false;
            int bracketDepth = 0;

            foreach (char c in argsStr)
            {
                if (c == '\'' || c == '"')
                {
                    inQuote = !inQuote;
                    sb.Append(c);
                }
                else if (!inQuote && (c == '[' || c == '('))
                {
                    bracketDepth++;
                    sb.Append(c);
                }
                else if (!inQuote && (c == ']' || c == ')'))
                {
                    bracketDepth--;
                    sb.Append(c);
                }
                else if (c == ',' && !inQuote && bracketDepth == 0)
                {
                    list.Add(sb.ToString().Trim());
                    sb.Clear();
                }
                else
                {
                    sb.Append(c);
                }
            }

            list.Add(sb.ToString().Trim());
            return list;
        }
    }
}
