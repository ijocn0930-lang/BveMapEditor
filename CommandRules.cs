using System;
using System.Collections.Generic;
using System.Text;

namespace BveMapEditor
{
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
                    //NumericArgIndices = new[] { 0, 1 } <-キーを指定するのでコメントアウト。キーは文字列になるため。
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
                   // NumericArgIndices = new[] { 3 } <--'00:00:00'を使うことがあるので不具合回避のためコメントアウト
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

    }
}
