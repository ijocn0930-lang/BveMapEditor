using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;

namespace BveMapEditor
{
    // エラー情報モデル
    public class ParseError
    {
        //ErrorListGridに出力
        public string Level { get; set; } = string.Empty; //警告やエラーの表示
        public int Line { get; set; } //列
        public int Column { get; set; } //行
        public string Message { get; set; } = string.Empty; //メッセージ
    }
    // コマンドのバリデーションルール定義用クラス
    public class CommandRule
    {
        //最小引数
        public int MinArgs { get; set; } = 0;
        //最高引数
        public int MaxArgs { get; set; } = int.MaxValue;
        //0引数
        public bool AllowZeroArgs { get; set; } = false;
        // 数値チェックを行うインデックスのリスト（例: 0, 1番目の引数など）
        public int[] NumericArgIndices { get; set; } = Array.Empty<int>();
// ★ 追加：すべての引数を数値としてチェックするスイッチ（標準は false）
        public bool AllArgsNumeric { get; set; } = false;
        // 特別なバリデーション関数（必要に応じてカスタム処理を挟むため）
        public Action<List<string>, int, string, List<ParseError>>? CustomValidator { get; set; }
    }
    public partial class MapParser
    {

        // =================================================================
        //  辞書によるコマンドルールの定義
        // =================================================================
        private static readonly Dictionary<string, CommandRule> CommandRules = new(StringComparer.OrdinalIgnoreCase)
        {
            {
                "Curve.SetCenter",
                new CommandRule
                {
                    MinArgs = 1,
                    MaxArgs = 1,
                    NumericArgIndices = new[] { 0 }
                }
            },
            {
                "Curve.SetGauge",
                new CommandRule
                {
                    MinArgs = 1,
                    MaxArgs = 1,
                    NumericArgIndices = new[] { 0}
                }
            },
            {
                "Track.Cant.SetGauge",
                new CommandRule
                {
                    MinArgs = 1,
                    MaxArgs = 1,
                    NumericArgIndices = new[] { 0 }
                }
            },
            {
                "Track.Cant.SetCenter",
                new CommandRule
                {
                    MinArgs = 1,
                    MaxArgs = 1,
                    NumericArgIndices = new[] { 0 }
                }
            },
            {
                "Track.Cant.Begin",
                new CommandRule
                {
                    MinArgs = 1,
                    MaxArgs = 1,
                    NumericArgIndices = new[] { 0 }
                }
            },
            {
                "Track.Cant.Interpolate",
                new CommandRule
                {
                    MinArgs = 0,
                    MaxArgs = 1,
                    AllowZeroArgs = true,
                    NumericArgIndices = new[] { 0 }
                }
            },

            {
                "Curve.Begin",
                new CommandRule
                {
                    MinArgs = 1,
                    MaxArgs = 2,
                    NumericArgIndices = new[] { 0, 1 }
                }
            },
            {
                "Curve.Interpolate",
                new CommandRule
                {
                    MinArgs = 0,
                    MaxArgs = 2,
                    AllowZeroArgs = true,
                    NumericArgIndices = new[] { 0, 1 }
                }
            },
            {
                "Curve.Change",
                new CommandRule
                {
                    MinArgs = 1,
                    MaxArgs = 1,
                    NumericArgIndices = new[] { 0 }
                }
            },
            {
                "Gradient.Begin",
                new CommandRule
                {
                    MinArgs = 1,
                    MaxArgs = 1,
                    NumericArgIndices = new[] { 0 }
                }
            },
            {
                "Gradient.BeginCost",
                new CommandRule
                {
                    MinArgs = 1,
                    MaxArgs = 1,
                    NumericArgIndices = new[] { 0 }
                }
            },
            {
                "Gradient.Interpolate",
                new CommandRule
                {
                    MinArgs = 0,
                    MaxArgs = 1,
                    AllowZeroArgs = true,
                    NumericArgIndices = new[] { 0 }
                }
            },








            {
                "Repeater.Begin",
                new CommandRule
                {
                    MinArgs = 11,
                    MaxArgs = int.MaxValue,
                    CustomValidator = (args, lineNumber, cmdName, errors) =>
                    {
                        // 2〜10番目の引数（インデックス 1〜9）の数値チェック
                        for (int i = 1; i <= 9 && i < args.Count; i++)
                        {
                            string arg = args[i];
                            if (string.IsNullOrWhiteSpace(arg) || string.Equals(arg, "null", StringComparison.OrdinalIgnoreCase)) continue;
                            if (arg.StartsWith("$")) return; // 変数参照はスキップ
                            if (!IsNumeric(arg))
                            {
                                errors.Add(new ParseError { Line = lineNumber, Column = 1, Message = $"{cmdName} の第 {i + 1} 引数には数値を指定してください。" });
                            }
                        }

                        // ★ Repeater.Begin の第8引数（インデックス 7）は「傾斜オプション (0〜3)」
                        if (args.Count > 7)
                        {
                            ValidateTiltOption(args[7], lineNumber, $"{cmdName} (Begin)");
                        }

                    }
                }
            },
            {
                "Repeater.Begin0",
                new CommandRule
                {
                    MinArgs = 5, // プレビュー用などの Begin0 の引数構成に合わせて調整
                    MaxArgs = int.MaxValue,
                    CustomValidator = (args, lineNumber, cmdName, errors) =>
                    {
                        // 2〜5目の引数（インデックス 1〜5）の数値チェック
                        for (int i = 1; i <= 3 && i < args.Count; i++)
                        {
                            string arg = args[i];
                            if (string.IsNullOrWhiteSpace(arg) || string.Equals(arg, "null", StringComparison.OrdinalIgnoreCase)) continue;
                            if (arg.StartsWith("$")) return; // 変数参照はスキップ
                            if (!IsNumeric(arg))
                            {
                                errors.Add(new ParseError { Line = lineNumber, Column = 1, Message = $"{cmdName} の第 {i + 1} 引数には数値を指定してください。" });
                            }
                        }
                        // ★ Repeater.Begin0 の第2引数（インデックス 1）は「傾斜オプション (0〜3)」
                        if (args.Count > 1)
                        {
                            ValidateTiltOption(args[1], lineNumber, $"{cmdName} (Begin0)");
                        }
                    }
                }
            },
{
                "Curve.SetFunction",
                new CommandRule
                {
                    MinArgs = 1,
                    MaxArgs = 1, // 1個固定
                    CustomValidator = (args, lineNumber, cmdName, errors) =>
{
                        if (args.Count > 0 && !string.IsNullOrWhiteSpace(args[0]))
                        {
                            string arg = args[0];
                            if (arg.StartsWith("$")) return; // 変数参照はスキップ

                            if (int.TryParse(arg, out int val) && (val < 0 || val > 1))
                            {
                                errors.Add(new ParseError
                                {
                                    Level = "エラー",
                                    Line = lineNumber,
                                    Column = 1,
                                    Message = $"{cmdName} の引数には 0 または 1 を指定してください（現在: {val}）。"
                                });
                            }
                        }
                    }
}
            },
            {
                "Track.Cant.SetFunction",
                new CommandRule
                {
                    MinArgs = 1,
                    MaxArgs = 1, // 1個固定
                    CustomValidator = (args, lineNumber, cmdName, errors) =>
                    {
                    if (args.Count > 0 && !string.IsNullOrWhiteSpace(args[0]))
                        {
                            string arg = args[0];
                            if (arg.StartsWith("$")) return; // 変数参照はスキップ

                            if (int.TryParse(arg, out int val) && (val < 0 || val > 1))
                            {
                                errors.Add(new ParseError
                                {
                                    Level = "エラー",
                                    Line = lineNumber,
                                    Column = 1,
                                    Message = $"{cmdName} の引数には 0 または 1 を指定してください（現在: {val}）。"
                                });
                            }
                        }
                    }
                }
                },
            {
                "Structure.Put",
                new CommandRule
                {
                    MinArgs = 9, // 仕様に応じた最小数
                    MaxArgs = 9,
                    CustomValidator = (args, lineNumber, cmdName, errors) =>
{
                        if (args.Count > 7 && !string.IsNullOrWhiteSpace(args[7]))
                        {
                            string arg = args[7];
                            if (arg.StartsWith("$")) return; // 変数参照はスキップ

                            if (int.TryParse(arg, out int tilt) && (tilt < 0 || tilt > 3))
                            {
                                errors.Add(new ParseError
                                {
                                    Level = "エラー",
                                    Line = lineNumber,
                                    Column = 1,
                                    Message = $"{cmdName} の第 8 引数（傾斜オプション）には 0〜3 の値を指定してください（現在: {tilt}）。"
                                });
                            }
                        }
                    }
                } },
            {
                "Structure.Put0",
                new CommandRule
                {
                    MinArgs = 3,
                    MaxArgs = 3,
                    CustomValidator = (args, lineNumber, cmdName, errors) =>
                    {
                        if (args.Count > 1 && !string.IsNullOrWhiteSpace(args[1]))
                        {
                            string arg = args[1];
                            if (arg.StartsWith("$")) return; // 変数参照はスキップ

                            if (int.TryParse(arg, out int tilt) && (tilt < 0 || tilt > 3))
                            {
                                errors.Add(new ParseError
                                {
                                    Level = "エラー",
                                    Line = lineNumber,
                                    Column = 1,
                                    Message = $"{cmdName} の第 2 引数（傾斜オプション）には 0〜3 の値を指定してください（現在: {tilt}）。"
                                });
                            }
                        }
                    }
                }
},
            {
                "Structure.PutBetween",
                new CommandRule
                {
                    MinArgs = 3,
                    MaxArgs = 3,
                    NumericArgIndices = new[] { 2 }
                }
            },
            {
                "Background.Change",
                new CommandRule
                {
                    MinArgs = 1,
                    MaxArgs = 1,
                    //NumericArgIndices = new[] { 0, 1 }
                }
            },
            {
                "Station.Put",
                new CommandRule
                {
                    MinArgs = 3,
                    MaxArgs = 3,
                    NumericArgIndices = new[] { 0, 1, 2 }
                }
            },
            {
                "Track.Position",
                new CommandRule
                {
                    MinArgs = 0,
                    MaxArgs = 4,
                    AllowZeroArgs = true,
                    NumericArgIndices = new[] { 0, 1, 2, 3 }
                }
            },

            {
                "Track.X.Interpolate",
                new CommandRule
                {
                    MinArgs = 0,
                    MaxArgs = 2,
                    AllowZeroArgs = true,
                    NumericArgIndices = new[] { 0, 1 }
                }
            },
                        {
                "Track.Y.Interpolate",
                new CommandRule
                {
                    MinArgs = 0,
                    MaxArgs = 2,
                    AllowZeroArgs = true,
                    NumericArgIndices = new[] { 0, 1 }
                }
            },
            {
                "Section.Begin",
                new CommandRule
                {
                    MinArgs = 0,
                    MaxArgs = 255, //閉塞を指定する引数。255まで扱える
                    AllowZeroArgs = true,
                    AllArgsNumeric = true // ★ これだけで「すべての引数を数値チェック」になります！
                }
            },
            {
                "Section.BeginNew",
                new CommandRule
                {
                    MinArgs = 0,
                    MaxArgs = 255, //閉塞を指定する引数。255まで扱える
                    AllowZeroArgs = true,
                    AllArgsNumeric = true // ★ これだけで「すべての引数を数値チェック」になります！
                }
            },
            {
                "Section.SetSpeedLimit",
                new CommandRule
                {
                    MinArgs = 0,
                    MaxArgs = 255, //閉塞を指定する引数。255まで扱える
                    AllowZeroArgs = true,
                    AllArgsNumeric = true // ★ これだけで「すべての引数を数値チェック」になります！
                }
            },
            {
                "Signal.SpeedLimit",
                new CommandRule
                {
                    MinArgs = 0,
                    MaxArgs = 255, //閉塞を指定する引数。255まで扱える
                    AllowZeroArgs = true,
                    AllArgsNumeric = true // ★ これだけで「すべての引数を数値チェック」になります！
                }
            },
            {
                "Signal.put",
                new CommandRule
                {
                    MinArgs = 4,
                    MaxArgs = 10,
                    NumericArgIndices = new[] { 0, 2, 3, 4, 5, 6, 7, 8, 9, 10 }
                }
            },
            {
                "Beacon.put",
                new CommandRule
                {
                    MinArgs = 3,
                    MaxArgs = 3,
                    NumericArgIndices = new[] { 0, 1, 2 }
                }
            },
            {
                "SpeedLimit.Begin",
                new CommandRule
                {
                    MinArgs = 1,
                    MaxArgs = 1,
                    NumericArgIndices = new[] { 0 }
                }
            },
            {
                "PreTrain.Pass",
                new CommandRule
                {
                    MinArgs = 1,
                    MaxArgs = 1,
                    //NumericArgIndices = new[] { 0 }
                }
            },
            {
                "Light.Ambient",
                new CommandRule
                {
                    MinArgs = 3,
                    MaxArgs = 3,
                    NumericArgIndices = new[] { 0, 1, 2 }
                }
            },
            {
                "Light.Diffuse",
                new CommandRule
                {
                    MinArgs = 3,
                    MaxArgs = 3,
                    NumericArgIndices = new[] { 0, 1, 2 }
                }
            },            {
                "Light.Direction",
                new CommandRule
                {
                    MinArgs = 2,
                    MaxArgs = 2,
                    NumericArgIndices = new[] { 0, 1 }
                }
            },
            {
                "Fog.Interpolate",
                new CommandRule
                {
                    MinArgs = 0,
                    MaxArgs = 4,
                    AllowZeroArgs = true,
                    NumericArgIndices = new[] { 0, 1, 2, 3 }
                }
            },
            {
                "Fog.Set",
                new CommandRule
                {
                    MinArgs = 0,
                    MaxArgs = 4,
                    AllowZeroArgs = true,
                    NumericArgIndices = new[] { 0, 1, 2, 3 }
                }
            },
            {
                "DrawDistance.Change",
                new CommandRule
                {
                    MinArgs = 1,
                    MaxArgs = 1,
                    NumericArgIndices = new[] { 0 }
                }
            },
            {
                "CabIlluminance.Interpolate",
                new CommandRule
                {
                    MinArgs = 0,
                    MaxArgs = 1,
                    AllowZeroArgs = true,
                    NumericArgIndices = new[] { 0 }
                }
            },
            {
                "Irregularity.Change",
                new CommandRule
                {
                    MinArgs = 6,
                    MaxArgs = 6,
                    NumericArgIndices = new[] { 0, 1, 2, 3, 4, 5 }
                }
            },
            {
                "Adhesion.Change",
                new CommandRule
                {
                    MinArgs = 1,
                    MaxArgs = 3,
                    NumericArgIndices = new[] { 0, 1, 2 }
                }
            },
            {
                "Sound3D.Put",
                new CommandRule
                {
                    MinArgs = 2,
                    MaxArgs = 2,
                    NumericArgIndices = new[] { 0, 1, }
                }
            },
            {
                "RollingNoise.Change",
                new CommandRule
                {
                    MinArgs = 1,
                    MaxArgs = 1,
                    NumericArgIndices = new[] { 0 }
                }
            },
            {
                "FlangeNoise.Change",
                new CommandRule
                {
                    MinArgs = 1,
                    MaxArgs = 1,
                    NumericArgIndices = new[] { 0 }
                }
            },            {
                "JointNoise.Change",
                new CommandRule
                {
                    MinArgs = 1,
                    MaxArgs = 1,
                    NumericArgIndices = new[] { 0 }
                }
            },
           {
                "Train.Load",
                new CommandRule
                {
                    MinArgs = 3,
                    MaxArgs = 3,
                    NumericArgIndices = new[] { 3 }
                }
            },
           {
                "Train.Enable",
                new CommandRule
                {
                    MinArgs = 1,
                    MaxArgs = 1,
                   // NumericArgIndices = new[] { 3 }
                }
            },
           {
                "Train.Stop",
                new CommandRule
                {
                    MinArgs = 4,
                    MaxArgs = 4,
                    NumericArgIndices = new[] { 0, 1, 2, 3 }
                }
            },




            // 今後コマンドを追加したい時はここへ簡単に追加できます！
            // 例: { "Gradient.Begin", new CommandRule { MinArgs = 1, MaxArgs = 1, NumericArgIndices = new[] { 0 } } },
        };

        // 傾斜オプション（0, 1, 2, 3）が正しいか検証するヘルパー
        private static void ValidateTiltOption(string argVal, int lineNumber, string commandName) //lineNumberとcommandNameは消さない。ビルドエラーになるで。
        {
            if (string.IsNullOrWhiteSpace(argVal) || string.Equals(argVal, "null", StringComparison.OrdinalIgnoreCase))
            {
                return; // 省略やnullの場合は別の必須チェックに任せるかスキップ
            }
            
            if (int.TryParse(argVal, out int tilt))
            {
                if (tilt < 0 || tilt > 3)
                {
                    // 0〜3の範囲外ならエラー
                    // （ここに直接 errors を渡せないため、ParseErrorのリストに追加する形にするか、
                    //   あるいは CustomValidator の中で直接処理する形に書き換えてもOKです）
                }
            }
        }
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
            // Parse: prepare
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


            // 未使用変数チェック用に、行ごとのテキストを保持しておくリスト
            var allLinesForCheck = new List<(int LineNumber, string RawLine, string TrimmedLine)>();
            // 検出された変数定義を記録するリスト: (VariableName, LineNumber)
            var definedVariables = new List<(string Name, int Line)>();

            int lineNumber = 0;
            using (var reader = new StringReader(text))
            {
                string? line;
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
                // 事前に有効コマンドセットを一度だけ取得して使い回す（UI スレッド呼び出しを頻繁に行わない）
                HashSet<string> validCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    if (Application.Current != null && Application.Current.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
                    {
                        validCommands = Application.Current.Dispatcher.Invoke(() => BveSyntaxProvider.GetValidCommands());
                    }
                    else
                    {
                        validCommands = BveSyntaxProvider.GetValidCommands();
                    }
                }
                catch
                {
                    try { validCommands = BveSyntaxProvider.GetValidCommands(); } catch { validCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase); }
                }

                while ((line = reader.ReadLine()) != null)
                    {
                    // 進捗ロギングは削除（デバッグ終了）
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
                    // 後続の未使用変数チェック用に保持（元の生行）
                    allLinesForCheck.Add((lineNumber, line, trimmedLine));

                    // ★ インラインコメント ( // ... ) を除外したクリーンなコード文字列を取得
                    string codeWithoutComment = GetCodePartWithoutComment(line).Trim();
                    if (string.IsNullOrEmpty(codeWithoutComment))
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
                    // =================================================================
                    //  $ から始まる行（変数定義・代入文など）の高度なバリデーション
                    // =================================================================
                    if (codeWithoutComment.StartsWith("$") || codeWithoutComment.Contains("="))
                    {
                        string contentPart = codeWithoutComment.EndsWith(";") ? codeWithoutComment.Substring(0, codeWithoutComment.Length - 1).Trim() : codeWithoutComment;

                        // 1. 代入演算子 = が含まれているかチェック（例: $foo; の対策）
                        int eqIndex = contentPart.IndexOf('=');
                        if (eqIndex < 0)
                        {
                            errors.Add(new ParseError
                            {
                                Level = "エラー",
                                Line = lineNumber,
                                Column = 1,
                                Message = "変数に代入する文字列または数値がありません。"
                            });
                            continue;
                        }
                        // 2. 左辺と右辺の分離
                        string leftPart = contentPart.Substring(0, eqIndex).Trim();
                        string rightPart = contentPart.Substring(eqIndex + 1).Trim(); // ★ 右辺
                        // 3. 左辺が変数（$で始まる）かどうかチェック（例: 1 = $foo; 対策）
                        if (!leftPart.StartsWith("$"))
                        {
                            errors.Add(new ParseError
                            {
                                Level = "エラー",
                                Line = lineNumber,
                                Column = 1,
                                Message = "左辺と右辺を入れ替えることはできません。"
                            });
                            continue;

                        }
                        // 4. ★ 追加：右辺にさらに = が含まれていないかチェック（例: $foo = $bar = 1; 対策）
                        if (rightPart.Contains("="))
                        {
                            errors.Add(new ParseError
                            {
                                Level = "エラー",
                                Line = lineNumber,
                                Column = 1,
                                Message = "変数に変数を代入することはできません。"
                            });
                            continue;
                        }
                        // 5. 変数名にピリオドが含まれていないかチェック（例: $foo.bar = 1; 対策）
                        if (leftPart.Contains("."))
                        {
                            errors.Add(new ParseError
                            {
                                Level = "エラー",
                                Line = lineNumber,
                                Column = 1,
                                Message = "変数名に使用できる文字は英数字とアンダースコアのみです（ピリオドは使用できません）。"
                            });
                            continue;
                        }
                        // 6. ★ 追加：右辺が数値でもクォート文字列でもない場合のチェック（アポストロフィ囲み忘れなど）
                        bool isNumeric = double.TryParse(rightPart, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out _);
                        bool isQuoted = (rightPart.StartsWith("'") && rightPart.EndsWith("'")) || (rightPart.StartsWith("\"") && rightPart.EndsWith("\""));
                        bool isVariableRef = rightPart.StartsWith("$");
                        if (!isNumeric && !isQuoted)
                        {
                            if (isVariableRef)
                            {
                                errors.Add(new ParseError
                                {
                                    Level = "エラー",
                                    Line = lineNumber,
                                    Column = 1,
                                    Message = "変数に変数を代入することはできません。"
                                });
                            }
                            else
                            {
                                errors.Add(new ParseError
                                {
                                    Level = "エラー",
                                    Line = lineNumber,
                                    Column = 1,
                                    Message = "文字列を代入する場合は、アポストロフィ（またはダブルクォート）で囲んでください。"
                                });
                            }
                            continue;
                        }


                        // 正常な変数定義として記録
                        definedVariables.Add((leftPart, lineNumber));
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
                            Level = "エラー",
                            Line = lineNumber,
                            Column = 1,
                            Message = "マップヘッダーが重複しているか、1行目以外の場所に記述されています（ヘッダーは必ず1行目に記述してください）。"
                        });
                        continue;
                    }
                    // ★ 追加：マップ要素（関数）の引数内での変数代入チェック（例: Curve.Gauge($foo = 1);）
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
                                Level = "警告",
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
                    // ★ 3. ここで引数の個数・型チェックを呼び出す！
                    CheckCommandArguments(trimmedCodePart, lineNumber, errors);
                    // 4. ★ 追加：セミコロンの連続（2個以上）チェック
                    CheckConsecutiveSemicolons(trimmedCodePart, lineNumber, errors);
                    // 5. ★ 追加：括弧の連続（2個以上）チェック
                    CheckConsecutiveBrackets(trimmedCodePart, lineNumber, errors);
                    // 6. 構文・コマンド形式チェック（ドット抜けなどのミスを検出）
                    CheckCommandSyntax(trimmedCodePart, lineNumber, baseDirectory, errors, validCommands);
                    // 7. ★ 追加：Load系コマンドの重複チェック
                    CheckDuplicateLoad(trimmedCodePart, lineNumber, loadedCommands, errors);
                    // 8. ★ 追加：全角スペースのチェック（コメント部分を除いたコード内を対象）
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
                // ★ ファイル全体を通して Station.Load が一度も出現しなかった場合のエラー追加
                if (!stationLoadFound)
                {
                    errors.Add(new ParseError
                    {
                        Level = "警告",
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
                        Level = "警告",
                        Line = firstStationLoadLine,
                        Column = 1,
                        Message = "'Station.Load' が 'Sound.Load' よりも前に記述されています。'Sound.Load' を先に記述してください。"
                    });
                }
                if (!soundLoadFound)
                {
                    errors.Add(new ParseError
                    {
                        Level = "警告",
                        Line = 1,
                        Column = 1,
                        Message = "必須の 'Sound.Load' が見つかりません。サウンドリストの読み込み定義を追加してください。"
                    });
                }
                if (!sounddLoadFound)
                {
                    errors.Add(new ParseError
                    {
                        Level = "警告",
                        Line = 1,
                        Column = 1,
                        Message = "必須の 'Sound3D.Load' が見つかりません。サウンド3Dリストの読み込み定義を追加してください。"
                    });
                }
                if (!structureLoadFound)
                {
                    errors.Add(new ParseError
                    {
                        Level = "警告",
                        Line = 1,
                        Column = 1,
                        Message = "必須の 'Structure.Load' が見つかりません。ストラクチャリストの読み込み定義を追加してください。"
                    });
                }
                if (!signalLoadFound)
                {
                    errors.Add(new ParseError
                    {
                        Level = "警告",
                        Line = 1,
                        Column = 1,
                        Message = "必須の 'Signal.Load' が見つかりません。信号リストの読み込み定義を追加してください。"
                    });
                }
            }
            // =================================================================
            //  未使用変数チェック（どこにも使われていない場合は警告を表示）
            // =================================================================
            foreach (var (varName, defLine) in definedVariables)
            {
                int usageCount = 0;
                foreach (var (ln, rawLine, trimmedLine) in allLinesForCheck)
                {
                    // 定義行自体はカウントから除外
                    if (ln == defLine) continue;

                    // ファイル内の他所でこの変数名が使われているか確認
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
            // Parse finished

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
        // 行分割ユーティリティ: テキストを行リストにして返す
        public static List<ParseError> QuickParseRegion(string text, int caretLine, int contextRadius, string baseDirectory)
        {
            var errors = new List<ParseError>();
            if (string.IsNullOrEmpty(text)) return errors;

            // 1. キャレット周辺の行範囲を計算
            int startLine = Math.Max(1, caretLine - contextRadius);
            int endLine = caretLine + contextRadius;

            // 2. 15万行すべてをリスト化せず、必要な行（startLine ~ endLine）だけをピンポイントで取り出す
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
                    if (currentLine > endLine)
                    {
                        break; // 必要な範囲を超えたら即座に読み込みを終了（15万行最後まで読まない！）
                    }
                    currentLine++;
                }
            }

            // 有効コマンドセットの取得（ここは従来通り）
            HashSet<string> validCommands = GetValidCommandsSafe();

            // 3. 抽出した行だけをチェック
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
        // ヘルパー: スレッドセーフに有効コマンドを取得
        private static HashSet<string> GetValidCommandsSafe()
        {
            try
            {
                if (System.Windows.Application.Current != null && System.Windows.Application.Current.Dispatcher != null && !System.Windows.Application.Current.Dispatcher.CheckAccess())
                {
                    return System.Windows.Application.Current.Dispatcher.Invoke(() => BveSyntaxProvider.GetValidCommands());
                }
                return BveSyntaxProvider.GetValidCommands();
            }
            catch
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
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
                        Level = "エラー",
                        Line = lineNumber,
                        Column = i + 1,
                        Message = "対応する '(' がない閉じ括弧 ')' が存在します。本体側:Syntax error, expected: dot, [, semi, +, -, *, /, %, <, >, <=, >=, ==, !=, &&, ||, ), comma, ]"
                    });
                    roundCount = 0;
                }

                if (squareCount < 0)
                {
                    errors.Add(new ParseError
                    {
                        Level = "エラー",
                        Line = lineNumber,
                        Column = i + 1,
                        Message = "対応する '[' がない閉じ括弧 ']' が存在します。本体側:Syntax error, expected: dot, [, semi, +, -, *, /, %, <, >, <=, >=, ==, !=, &&, ||, ), comma, ]"
                    });
                    squareCount = 0;
                }
            }

            if (roundCount > 0)
            {
                errors.Add(new ParseError
                {
                    Level = "エラー",
                    Line = lineNumber,
                    Column = line.Length,
                    Message = "閉じ括弧 ')' が不足しています。本体側:Syntax error, expected: dot, [, semi, +, -, *, /, %, <, >, <=, >=, ==, !=, &&, ||, ), comma, ]"
                });
            }

            if (squareCount > 0)
            {
                errors.Add(new ParseError
                {
                    Level = "エラー",
                    Line = lineNumber,
                    Column = line.Length,
                    Message = "閉じ括弧 ']' が不足しています。本体側:Syntax error, expected: dot, [, semi, +, -, *, /, %, <, >, <=, >=, ==, !=, &&, ||, ), comma, ]"
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
                    Level = "エラー",
                        Line = lineNumber,
                        Column = trimmedCodePart.Length + 1,
                        Message = "文末のセミコロン ';' が不足しています。本体側:Syntax error, expected: dot, [, semi, +, -, *, /, %, <, >, <=, >=, ==, !=, &&, ||, ), comma, ]"
                    });
                }
            }
        }
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
                    if (string.IsNullOrWhiteSpace(arg) || string.Equals(arg, "null", StringComparison.OrdinalIgnoreCase))
                    {
                        continue; // 空文字やnullはスキップ
                    }
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
                        if (string.IsNullOrWhiteSpace(arg) || string.Equals(arg, "null", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
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

        private static bool IsNumeric(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            if ((value.StartsWith("'") && value.EndsWith("'")) || (value.StartsWith("\"") && value.EndsWith("\"")))
            {
                return false;
            }
            return double.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out _);

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
                    Level = "警告",
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
                int index = trimmedCodePart.IndexOfAny(['(', ')', '[', ']']);
                errors.Add(new ParseError
                {
                    Level = "エラー",
                    Line = lineNumber,
                    Column = index >= 0 ? index + 1 : 1,
                    Message = "括弧（'('、')'、'['、']'）が複数連続して記述されています。"
                });
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
                        Level = "エラー",
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
                        Level = "エラー",
                        Line = lineNumber,
                        Column = i + 1,
                        Message = $"コード内に全角文字（またはマルチバイト文字: '{c}'）が含まれています。半角文字を使用してください。"
                    });
                }
            }
        }

    }
}
