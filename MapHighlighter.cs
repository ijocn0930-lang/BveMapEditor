using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using System;
using System.Xml;
using System.Collections.Generic;
using System.Windows;
using System.IO;

namespace BveMapEditor
{
    internal class MapHighlighter
    {
        // ★ (2026-11-17) 読み込んだ色分けを覚えておく（以前はタブを切り替えるたびに xshd を読み直していた）
        //    ※ xshd を書き換えたときは、アプリを起動し直すと反映される
        private static IHighlightingDefinition? _mapDefinition;
        private static IHighlightingDefinition? _listDefinition;

        // AvalonEdit 用のカスタムシンタックスハイライト定義を作成・適用する（マップ用）
        public static void InitializeHighlighting(TextEditor editor)
        {
            ApplyHighlighting(editor, isList: false);
        }

        /// <summary>
        /// ★ (2026-11-17) マップ用（BveMap.xshd）かリスト用（BveList.xshd）の色分けを付ける
        /// isList: リストファイル（.csv、または 1 行目が BveTs … List）のタブなら true
        /// </summary>
        public static void ApplyHighlighting(TextEditor editor, bool isList)
        {
            if (editor == null) return;
            if (isList)
            {
                _listDefinition ??= LoadDefinition("BveList.xshd");
                // リスト用が読めなければ、マップ用で色分けする
                var def = _listDefinition ?? (_mapDefinition ??= LoadDefinition("BveMap.xshd"));
                if (editor.SyntaxHighlighting != def) editor.SyntaxHighlighting = def;
            }
            else
            {
                _mapDefinition ??= LoadDefinition("BveMap.xshd");
                var def = _mapDefinition ?? HighlightingManager.Instance.GetDefinition("C#"); // 読めなければ以前と同じ標準の色分け
                if (editor.SyntaxHighlighting != def) editor.SyntaxHighlighting = def;
            }
        }

        // xshd を読む（読めなければ知らせて null）
        private static IHighlightingDefinition? LoadDefinition(string fileName)
        {
            try
            {
                // アプリケーションの実行フォルダを基準に XSHD ファイルを探す（カレントディレクトリの影響を避ける）
                string baseDir = AppContext.BaseDirectory ?? AppDomain.CurrentDomain.BaseDirectory;
                string xshdPath = Path.Combine(baseDir, fileName);
                if (!File.Exists(xshdPath)) return null; // ファイルが見つからない場合は静かにフォールバック
                using var stream = File.OpenRead(xshdPath);
                using var reader = XmlReader.Create(stream);
                return HighlightingLoader.Load(reader, HighlightingManager.Instance);
            }
            catch (Exception ex)
            {
                // 読み込みやパースに失敗した場合
                MessageBox.Show(
                    $"構文ハイライト定義ファイル ({fileName}) の読み込みに失敗しました。\n書き換えた可能性がある場合は元に戻してください。\n詳細: {ex.Message}",
                    "警告 - BveMapEditor",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return null;
            }
        }
            public static string GetText(TextEditor editor)
        {
            return editor.Text;
        }

        public static void SetText(TextEditor editor, string text)
        {
            editor.Text = text;
        }
    }
}
