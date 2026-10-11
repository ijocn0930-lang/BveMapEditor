using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using ICSharpCode.AvalonEdit.Document;
using System;
using System.Xml;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using System.IO;

namespace BveMapEditor
{
    internal class MapHighlighter
    {
        // AvalonEdit 用のカスタムシンタックスハイライト定義を作成・適用する
        public static void InitializeHighlighting(TextEditor editor)
        {
            try
            {
                string xshdPath = "BveMap.xshd";
                if (File.Exists(xshdPath))
                {
                    using (var reader = XmlReader.Create(xshdPath))
                    {
                        editor.SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                    }
                }
                else
                { 
                    // ファイルが見つからない場合
                    MessageBox.Show(
                        "構文ハイライト定義ファイル (BveMap.xshd) が見つかりませんでした。\n標準のハイライト（C#）を代用します。",
                        "警告 - BveMapEditor",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                editor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("C#");
            }
            }
            catch (Exception ex)
            {
                // 読み込みやパースに失敗した場合
                MessageBox.Show(
                    $"構文ハイライト定義ファイル (BveMap.xshd) の読み込みに失敗しました。\n書き換えた可能性がある場合は元に戻してください。\n詳細: {ex.Message}",
                    "警告 - BveMapEditor",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                // フォールバックとして標準ハイライトを適用
                editor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("C#");
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
