using DynamicExpresso;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;

namespace BveMapEditor
{
    // エラー情報モデル
    public class ParseError
    {
        public string Level { get; set; } = string.Empty; // 警告やエラーの表示
        public int Line { get; set; } // 列
        public int Column { get; set; } // 行
        public string Message { get; set; } = string.Empty; // メッセージ
    }

    public partial class MapParser
    {
        public string Level { get; set; } = string.Empty;
        public int Line { get; set; }   // 列
        public int Column { get; set; } // 行
        public string Message { get; set; } = string.Empty;

        // 乱数生成用（スレッドセーフ）
        private static readonly Random _bveRandom = new Random();

        public ObservableCollection<ParseError> ErrorLogList { get; set; } = new ObservableCollection<ParseError>();

        /// <summary>
        /// スレッド競合を防ぐため、呼び出しごとに新しいインタプリタインスタンスを生成し、
        /// BVE5公式の数学関数と初期変数を登録して返します。
        /// </summary>
        private static Interpreter CreateAndInitializeInterpreter()
        {
            var interpreter = new Interpreter();

            // BVE5公式の数学関数を登録
            interpreter.SetFunction("abs", (Func<double, double>)Math.Abs);
            interpreter.SetFunction("atan2", (Func<double, double, double>)Math.Atan2);
            interpreter.SetFunction("ceil", (Func<double, double>)Math.Ceiling);
            interpreter.SetFunction("cos", (Func<double, double>)Math.Cos);
            interpreter.SetFunction("exp", (Func<double, double>)Math.Exp);
            interpreter.SetFunction("floor", (Func<double, double>)Math.Floor);
            interpreter.SetFunction("log", (Func<double, double>)Math.Log);
            interpreter.SetFunction("pow", (Func<double, double, double>)Math.Pow);
            interpreter.SetFunction("sin", (Func<double, double>)Math.Sin);
            interpreter.SetFunction("sqrt", (Func<double, double>)Math.Sqrt);
            interpreter.SetFunction("rand", (Func<double>)(() => _bveRandom.NextDouble()));
            interpreter.SetFunction("rand", (Func<double, double>)(value => _bveRandom.NextDouble() * value));

            // BVE公式の暗黙定数・変数
            interpreter.SetVariable("pi", Math.PI);
            interpreter.SetVariable("distance", 0.0);

            return interpreter;
        }

        private static readonly string[] KnownNamespaces = {
            "Sound", "Signal", "Structure", "Background", "Fog", "Light", "Gradient", "Curve", "Beacon", "Section", "Train", "DrawDistance", "CabIlluminance", "Irregularity", "Adhesion", "RollingNoise", "JointNoise", "FlangeNoise", "Legacy"
        };

        private static readonly string[] KeyRequiredNamespaces = {
            "Sound", "Structure", "Signal", "Repeater", "Sound3D", "Track", "Train", "Station"
        };

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

            // スレッドセーフなローカルインタプリタと変数辞書の生成
            Interpreter bveInterpreter = CreateAndInitializeInterpreter();
            Dictionary<string, object> variableValues = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            var allLinesForCheck = new List<(int LineNumber, string RawLine, string TrimmedLine)>();
            var definedVariables = new List<(string Name, int Line)>();
            int lineNumber = 0;
            // 追加: Section.SetSpeedLimit / Signal.SpeedLimit の記述回数をカウントする変数
            int speedLimitCount = 0;
            using (var reader = new StringReader(text))
            {
                string? line;
                bool headerFound = false;
                bool stationLoadFound = false;
                bool soundLoadFound = false;
                bool structureLoadFound = false;
                bool sounddLoadFound = false;
                bool signalLoadFound = false;

                bool hasHitDistance = false;
                int firstStationLoadLine = -1;
                int firstSoundLoadLine = -1;

                var loadedCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                HashSet<string> validCommands = GetValidCommandsSafe();

                while ((line = reader.ReadLine()) != null)
                {
                    lineNumber++;
                    string trimmedLine = line.Trim();

                    if (string.IsNullOrEmpty(trimmedLine)) continue;
                    if (trimmedLine.StartsWith("//") || trimmedLine.StartsWith("#")) continue;

                    allLinesForCheck.Add((lineNumber, line, trimmedLine));

                    string codeWithoutComment = GetCodePartWithoutComment(line).Trim();
                    if (string.IsNullOrEmpty(codeWithoutComment)) continue;

                    // 1行目のヘッダーチェック
                    if (lineNumber == 1)
                    {
                        if (!trimmedLine.StartsWith("BveTs Map", StringComparison.OrdinalIgnoreCase))
                        {
                            errors.Add(new ParseError
                            {
                                Level = "エラー",
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

                    // 変数代入行の処理（インタプリタを引数で渡す）
                    if (TryHandleVariableAssignment(codeWithoutComment, lineNumber, errors, definedVariables, variableValues, bveInterpreter)) continue;

                    string codePart = GetCodePartWithoutComment(line);
                    string trimmedCodePart = codePart.Trim();

                    if (string.IsNullOrEmpty(trimmedCodePart)) continue;

                    // 2行目以降のヘッダー重複検知
                    if (trimmedLine.StartsWith("BveTs Map", StringComparison.OrdinalIgnoreCase))
                    {
                        errors.Add(new ParseError
                        {
                            Level = "エラー",
                            Line = lineNumber,
                            Column = 1,
                            Message = "マップヘッダーが重複しているか、1行目以外の場所に記述されています。"
                        });
                        continue;
                    }

                    // 引数内での変数代入チェック
                    string innerArgs = GetInnerArguments(trimmedCodePart);
                    if (!string.IsNullOrEmpty(innerArgs))
                    {
                        List<string> parsedArgs = SplitArguments(innerArgs);
                        foreach (var arg in parsedArgs)
                        {
                            if (arg.Contains("="))
                            {
                                errors.Add(new ParseError
                                {
                                    Level = "エラー",
                                    Line = lineNumber,
                                    Column = 1,
                                    Message = "マップ要素（関数）の引数の中で変数を代入することはできません。"
                                });
                                break;
                            }
                        }
                    }

                    // 距離程指定のチェック
                    var distanceCheckMatch = Regex.Match(trimmedLine, @"^(\d+|\w+(\.\w+)*)\s*;", RegexOptions.IgnoreCase);
                    if (distanceCheckMatch.Success && distanceCheckMatch.Index == 0)
                    {
                        hasHitDistance = true;
                    }
                    // 追加: Section.SetSpeedLimit または Signal.SpeedLimit の重複・複数記述チェック
                    if (trimmedCodePart.Contains("Section.SetSpeedLimit", StringComparison.OrdinalIgnoreCase) ||
                        trimmedCodePart.Contains("Signal.SpeedLimit", StringComparison.OrdinalIgnoreCase))
                    {
                        speedLimitCount++;
                        if (speedLimitCount >= 2)
                        {
                            errors.Add(new ParseError
                            {
                                Level = "エラー",
                                Line = lineNumber,
                                Column = 1,
                                Message = "Section.SetSpeedLimit または Signal.SpeedLimit が複数記述されています。この構文は信号指示速度のため、マップファイル全体で1つしか記述できません。"
                            });
                        }

                        // もし「0;」の前に書かれていない（距離程がすでにヒットしている等）場合のチェックが必要な場合はここで条件を追加できます
                    }
                    // Load系コマンドの検出
                    if (trimmedCodePart.Contains("Station.Load", StringComparison.OrdinalIgnoreCase))
                    {
                        stationLoadFound = true;
                        if (firstStationLoadLine == -1) firstStationLoadLine = lineNumber;
                    }
                    if (trimmedCodePart.Contains("Sound.Load", StringComparison.OrdinalIgnoreCase))
                    {
                        soundLoadFound = true;
                        if (firstSoundLoadLine == -1) firstSoundLoadLine = lineNumber;
                    }
                    if (trimmedCodePart.Contains("Signal.Load", StringComparison.OrdinalIgnoreCase))
                    {
                        signalLoadFound = true;
                    }
                    if (trimmedCodePart.Contains("Structure.Load", StringComparison.OrdinalIgnoreCase))
                    {
                        structureLoadFound = true;
                    }
                    if (trimmedCodePart.Contains("Sound3D.Load", StringComparison.OrdinalIgnoreCase))
                    {
                        sounddLoadFound = true;
                    }

                    // 距離程より後に Load系コマンドがある場合の警告
                    if (hasHitDistance && Regex.IsMatch(trimmedCodePart, @"\b[A-Za-z0-9]+\.Load\b", RegexOptions.IgnoreCase))
                    {
                        errors.Add(new ParseError
                        {
                            Level = "警告",
                            Line = lineNumber,
                            Column = 1,
                            Message = "Load系コマンドは、距離程よりも前のファイル上部に配置してください。"
                        });
                    }

                    // 各種バリデーション
                    CheckBrackets(codePart, lineNumber, errors);
                    CheckSemicolon(trimmedCodePart, lineNumber, errors);
                    CheckCommandArguments(trimmedCodePart, lineNumber, errors);
                    CheckConsecutiveSemicolons(trimmedCodePart, lineNumber, errors);
                    CheckConsecutiveBrackets(trimmedCodePart, lineNumber, errors);
                    CheckCommandSyntax(trimmedCodePart, lineNumber, baseDirectory, errors, validCommands);
                    CheckDuplicateLoad(trimmedCodePart, lineNumber, loadedCommands, errors);
                    CheckFullWidthSpace(codePart, lineNumber, errors);
                }

                if (!headerFound)
                {
                    errors.Add(new ParseError
                    {
                        Level = "エラー",
                        Line = 1,
                        Column = 1,
                        Message = "マップファイルにヘッダーがありません。"
                    });
                }

                AddMissingLoadWarning(!stationLoadFound, "Station.Load", "必須の 'Station.Load' が見つかりません。", errors);

                if (firstStationLoadLine > 0 && firstSoundLoadLine > 0 && firstStationLoadLine < firstSoundLoadLine)
                {
                    errors.Add(new ParseError
                    {
                        Level = "警告",
                        Line = firstStationLoadLine,
                        Column = 1,
                        Message = "'Station.Load' が 'Sound.Load' よりも前に記述されています。"
                    });
                }

                AddMissingLoadWarning(!soundLoadFound, "Sound.Load", "必須の 'Sound.Load' が見つかりません。", errors);
                AddMissingLoadWarning(!sounddLoadFound, "Sound3D.Load", "必須の 'Sound3D.Load' が見つかりません。", errors);
                AddMissingLoadWarning(!structureLoadFound, "Structure.Load", "必須の 'Structure.Load' が見つかりません。", errors);
                AddMissingLoadWarning(!signalLoadFound, "Signal.Load", "必須の 'Signal.Load' が見つかりません。", errors);
            }

            // 未使用変数チェック
            foreach (var (varName, defLine) in definedVariables)
            {
                int usageCount = 0;
                foreach (var (ln, rawLine, trimmedLine) in allLinesForCheck)
                {
                    if (ln == defLine) continue;
                    if (Regex.IsMatch(rawLine, $@"\b{Regex.Escape(varName)}\b"))
                    {
                        usageCount++;
                    }
                }
                if (usageCount == 0)
                {
                    errors.Add(new ParseError
                    {
                        Level = "警告",
                        Line = defLine,
                        Column = 1,
                        Message = $"この変数は使われていません: {varName}"
                    });
                }
            }

            return errors;
        }

        // 変数代入行の処理ヘルパー（distanceなどの組み込み変数とインスタンス化したインタプリタに対応）
        private static bool TryHandleVariableAssignment(string codeWithoutComment, int lineNumber, List<ParseError> errors, List<(string Name, int Line)> definedVariables, Dictionary<string, object> variableValues, Interpreter interpreter)
        {
            if (!(codeWithoutComment.StartsWith("$") || codeWithoutComment.Contains("="))) return false;

            string contentPart = codeWithoutComment.EndsWith(";") ? codeWithoutComment.Substring(0, codeWithoutComment.Length - 1).Trim() : codeWithoutComment;
            int eqIndex = contentPart.IndexOf('=');
            if (eqIndex < 0)
            {
                errors.Add(new ParseError { Level = "エラー", Line = lineNumber, Column = 1, Message = "変数に代入する文字列または数値がありません。" });
                return true;
            }

            string leftPart = contentPart.Substring(0, eqIndex).Trim();
            string rightPart = contentPart.Substring(eqIndex + 1).Trim();

            // distance などの組み込み変数を許可
            bool isDistance = leftPart.Equals("distance", StringComparison.OrdinalIgnoreCase);
            if (!leftPart.StartsWith("$") && !isDistance)
            {
                errors.Add(new ParseError { Level = "エラー", Line = lineNumber, Column = 1, Message = "右辺と左辺を逆に書くことはできません。" });
                return true;
            }

            if (rightPart.Contains("="))
            {
                errors.Add(new ParseError { Level = "エラー", Line = lineNumber, Column = 1, Message = "一度に複数の変数に値を代入することはできません。" });
                return true;
            }

            if (leftPart.Contains("."))
            {
                errors.Add(new ParseError { Level = "エラー", Line = lineNumber, Column = 1, Message = "変数名に使用できる文字は英数字とアンダースコアのみです。" });
                return true;
            }

            string leftLower = leftPart.ToLowerInvariant();

            bool isQuoted = (rightPart.StartsWith("'") && rightPart.EndsWith("'")) || (rightPart.StartsWith("\"") && rightPart.EndsWith("\""));
            if (isQuoted)
            {
                if (isDistance)
                {
                    errors.Add(new ParseError { Level = "エラー", Line = lineNumber, Column = 1, Message = "distance変数には数値を代入してください。" });
                    return true;
                }
                string strValue = rightPart.Substring(1, rightPart.Length - 2);
                definedVariables.Add((leftPart, lineNumber));
                variableValues[leftLower] = strValue;
                return true;
            }

            try
            {
                foreach (var kvp in variableValues)
                {
                    interpreter.SetVariable(kvp.Key, kvp.Value);
                }

                string cleanRightExpr = rightPart.ToLowerInvariant();
                var evalResult = interpreter.Eval(cleanRightExpr);
                double numResult = Convert.ToDouble(evalResult);

                if (double.IsNaN(numResult))
                {
                    errors.Add(new ParseError { Level = "エラー", Line = lineNumber, Column = 1, Message = "数学関数の計算結果が非数(NaN)です。" });
                    return true;
                }
                if (double.IsInfinity(numResult))
                {
                    errors.Add(new ParseError { Level = "エラー", Line = lineNumber, Column = 1, Message = "計算結果がオーバーフローしたか、ゼロ除算が発生しました。" });
                    return true;
                }

                if (!isDistance)
                {
                    definedVariables.Add((leftPart, lineNumber));
                }

                variableValues[leftLower] = numResult;
                return true;
            }
            catch (DynamicExpresso.Exceptions.UnknownIdentifierException ex)
            {
                errors.Add(new ParseError { Level = "エラー", Line = lineNumber, Column = 1, Message = $"未定義の変数・関数、またはクォーテーション漏れの文字列です: {ex.Identifier}" });
            }
            catch (Exception ex)
            {
                errors.Add(new ParseError { Level = "エラー", Line = lineNumber, Column = 1, Message = $"数式の計算に失敗しました: {ex.Message}" });
            }
            return true;
        }

        private static string GetCodePartWithoutComment(string line)
        {
            int slashCommentIndex = line.IndexOf("//");
            int hashCommentIndex = line.IndexOf('#');
            int commentIndex = -1;

            if (slashCommentIndex >= 0 && hashCommentIndex >= 0)
            {
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

        private static void AddMissingLoadWarning(bool notFound, string commandName, string message, List<ParseError> errors)
        {
            if (notFound)
            {
                errors.Add(new ParseError
                {
                    Level = "警告",
                    Line = 1,
                    Column = 1,
                    Message = message
                });
            }
        }

        public static List<ParseError> QuickParseRegion(string text, int caretLine, int contextRadius, string baseDirectory)
        {
            var errors = new List<ParseError>();
            if (string.IsNullOrEmpty(text)) return errors;

            int startLine = Math.Max(1, caretLine - contextRadius);
            int endLine = caretLine + contextRadius;

            var targetLines = new Dictionary<int, string>();
            using (var rdr = new StringReader(text))
            {
                string? l;
                int currentLine = 1;
                while ((l = rdr.ReadLine()) != null)
                {
                    if (currentLine >= startLine && currentLine <= endLine)
                    {
                        targetLines[currentLine] = l;
                    }
                    if (currentLine > endLine) break;
                    currentLine++;
                }
            }

            HashSet<string> validCommands = GetValidCommandsSafe();

            foreach (var kvp in targetLines)
            {
                int ln = kvp.Key;
                string codePart = kvp.Value;
                string trimmedCodePart = codePart.Trim();
                if (string.IsNullOrEmpty(trimmedCodePart)) continue;
                if (trimmedCodePart.StartsWith("//") || trimmedCodePart.StartsWith("#")) continue;

                CheckBrackets(codePart, ln, errors);
                CheckSemicolon(trimmedCodePart, ln, errors);
                CheckCommandArguments(trimmedCodePart, ln, errors);
                CheckConsecutiveSemicolons(trimmedCodePart, ln, errors);
                CheckConsecutiveBrackets(trimmedCodePart, ln, errors);
                CheckCommandSyntax(trimmedCodePart, ln, baseDirectory, errors, validCommands);
                CheckFullWidthSpace(codePart, ln, errors);
            }
            return errors;
        }

        private static HashSet<string> GetValidCommandsSafe()
        {
            try
            {
                if (Application.Current != null && Application.Current.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
                {
                    return Application.Current.Dispatcher.Invoke(() => BveSyntaxProvider.GetValidCommands());
                }
                return BveSyntaxProvider.GetValidCommands();
            }
            catch
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static void CheckBrackets(string line, int lineNumber, List<ParseError> errors)
        {
            int roundCount = 0;
            int squareCount = 0;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '(') roundCount++;
                else if (c == ')') roundCount--;
                else if (c == '[') squareCount++;
                else if (c == ']') squareCount--;

                if (roundCount < 0)
                {
                    errors.Add(new ParseError { Level = "エラー", Line = lineNumber, Column = i + 1, Message = "対応する '(' がない閉じ括弧 ')' が存在します。" });
                    roundCount = 0;
                }
                if (squareCount < 0)
                {
                    errors.Add(new ParseError { Level = "エラー", Line = lineNumber, Column = i + 1, Message = "対応する '[' がない閉じ括弧 ']' が存在します。" });
                    squareCount = 0;
                }
            }

            if (roundCount > 0) errors.Add(new ParseError { Level = "エラー", Line = lineNumber, Column = line.Length, Message = "閉じ括弧 ')' が不足しています。" });
            if (squareCount > 0) errors.Add(new ParseError { Level = "エラー", Line = lineNumber, Column = line.Length, Message = "閉じ括弧 ']' が不足しています。" });
        }

        private static void CheckSemicolon(string trimmedCodePart, int lineNumber, List<ParseError> errors)
        {
            if (!trimmedCodePart.EndsWith(";") && !trimmedCodePart.EndsWith("{") && !trimmedCodePart.EndsWith("}"))
            {
                if (!trimmedCodePart.StartsWith("#") && !trimmedCodePart.StartsWith("include", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add(new ParseError { Level = "エラー", Line = lineNumber, Column = trimmedCodePart.Length + 1, Message = "文末のセミコロン ';' が不足しています。" });
                }
            }
        }

        private static bool IsNumeric(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            if ((value.StartsWith("'") && value.EndsWith("'")) || (value.StartsWith("\"") && value.EndsWith("\"")))
            {
                return false;
            }
            return double.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out _);
        }

        private static void CheckConsecutiveSemicolons(string trimmedCodePart, int lineNumber, List<ParseError> errors)
        {
            if (Regex.IsMatch(trimmedCodePart, @";\s*;"))
            {
                int index = trimmedCodePart.IndexOf(';');
                errors.Add(new ParseError { Level = "情報", Line = lineNumber, Column = index >= 0 ? index + 1 : 1, Message = "セミコロン ';' が複数連続して記述されています。" });
            }
        }

        private static void CheckConsecutiveBrackets(string trimmedCodePart, int lineNumber, List<ParseError> errors)
        {
            // ユーザー様ご自身で確認された安全なパターンを維持
            if (Regex.IsMatch(trimmedCodePart, @"\(\s*\(|\)\s*\)|\[\s*\[|\]\s*\]|\(\s*\)\s*\(\s*\)"))
            {
                int index = trimmedCodePart.IndexOfAny(new[] { '(', ')', '[', ']' });
                errors.Add(new ParseError { Level = "エラー", Line = lineNumber, Column = index >= 0 ? index + 1 : 1, Message = "括弧が複数連続して記述されています。" });
            }
        }

        private static void CheckDuplicateLoad(string trimmedCodePart, int lineNumber, HashSet<string> loadedCommands, List<ParseError> errors)
        {
            if (Regex.IsMatch(trimmedCodePart, @"\b[A-Za-z0-9]+\.Load\b", RegexOptions.IgnoreCase))
            {
                string loadKey = trimmedCodePart.TrimEnd(';');
                if (loadedCommands.Contains(loadKey))
                {
                    errors.Add(new ParseError { Level = "エラー", Line = lineNumber, Column = 1, Message = $"同じ Load 定義（'{loadKey}'）が重複して記述されています。" });
                }
                else
                {
                    loadedCommands.Add(loadKey);
                }
            }
        }

        private static void CheckFullWidthSpace(string codePart, int lineNumber, List<ParseError> errors)
        {
            for (int i = 0; i < codePart.Length; i++)
            {
                char c = codePart[i];
                if (c > 127)
                {
                    errors.Add(new ParseError { Level = "エラー", Line = lineNumber, Column = i + 1, Message = $"コード内に全角文字（'{c}'）が含まれています。半角文字を使用してください。" });
                }
            }
        }
    }
}