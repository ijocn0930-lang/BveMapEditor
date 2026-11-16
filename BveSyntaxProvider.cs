using System.Windows;
using System.Windows.Controls;


namespace BveMapEditor
{
    // ★ 整理C (2026-11-15): 自動補完の辞書は、ワーカーの BveMapParserWorker/BveSyntaxProvider.cs の1か所だけにした
    //    （以前はこのファイルにも同じ73個の一覧があり、コマンドを足すときは両方直す必要があった）
    //    ここでは、ワーカーの一覧からエディタの自動補完の候補を作るだけ
    //    コマンドを足す・直すときは、ワーカーの BveSyntaxProvider.cs を直す
    internal class BveSyntaxProvider
    {
        public static IEnumerable<BveCompletionData> GetAllSyntaxes()
        {
            return BveMapParserWorker.BveSyntaxProvider.GetSyntaxEntries()
                .Select(entry => new BveCompletionData(entry.Text, MakeDescription(entry.Description)))
                .ToArray();
        }

        // 説明文に改行があれば、以前と同じく折り返して表示する部品（TextBlock）にする
        private static object MakeDescription(string description)
        {
            if (description.Contains('\n'))
            {
                return new TextBlock
                {
                    Text = description,
                    TextWrapping = TextWrapping.Wrap
                };
            }
            return description;
        }
    }
}
