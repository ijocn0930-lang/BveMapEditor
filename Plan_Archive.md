# BVEMapEditer 作成プラン

## 実装済み機能
- パイプ通信
  - 脆弱性対策(現在ユーザーかどうか、バッファオーバーラン対策)
  - パイプ通信再試行
  - 入力を止めるまでタスクを実行しない

- AvalonEditを採用
- Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
- タブ機能
  - タブを切り替えた時に描画更新
  - 
- エラーの波線を追加
- 現在行の協調
- ステータスバーに行と列と文字コード
- ファイル保存と開く
- プロジェクトファイルの保存と開く
- エクスプローラーペイン
- エクスプローラーペイン設定ファイル保存と開く
- クラッシュログ作成
- フォント設定
- 右端で折り返す
- BVEとQuickMapViewerを開く
- ヘルプメニュー
  - 対処法のページ 
- 公式サイトへのリンクメニュー
## 実装済み機能（BveMapParserWorker）
- 自動補完のカスタマイズ(xhsd)
- 字句解析　トークン処理(BveMapParserWorker)
  - 不明な単語の処理
  - ヘッダーの処理
  - 構文辞書
  - 引数の数辞書
  - セミコロン漏れと連続
  - カッコ忘れと連続
  - キーを読む
  - Load系の確認
  - ファイルパスの読み取り
  - インクルード先のエラーチェック（2026-11-14 修正: 未保存タブ・親子対応表に対応）
  - 変数


## 完了した作業の記録 (2026-11-14 動作確認済み)

### 新しい機能
- 修正提案 まとまり4: 同じファイルの同じ種類のエラー（; 抜け・全角文字など8種類）を「まとめて直す」。Ctrl+Z 1回で元に戻せる (2026-11-15, ErrorFix.cs)
- 修正提案 まとまり4: エディタのエラーのある行を右クリックしても、メニューの先頭に「修正の候補」を出す (2026-11-15, ErrorFix.cs / MainWindow.xaml)
- 修正提案 まとまり3: 'BeginTransition' is required before 'Begin' / 'End' のとき、ダイアログで距離程を選び、その行のすぐ下に Curve / Gradient / Track['x'].Cant の BeginTransition の行を足す (2026-11-15, ErrorFix.cs)
- 修正提案 まとまり3: 引数の数が合わないとき、足りない引数を後ろに足す（数値は 0、文字列は ''）・多い引数を後ろから消す候補（Repeater は除く） (2026-11-15, ErrorFix.cs / BveMapParserWorker/BveSemanticCommandRules.cs)
- 修正提案: 引数の数が合わないとき、数が合う兄弟のコマンドに変える候補（Put ↔ Put0、Begin ↔ Begin0） (2026-11-15, ErrorFix.cs)
- 修正提案 まとまり2: . のない単語にも似た名前を候補に出す（includ → include / Curvee → Curve）。閉じる ' の足し忘れを直す候補（include 'a.txt; / Begin0('RoadR, 1, ...）。行に閉じていない ' があるときの「; 抜け」は ' を足す候補にする (2026-11-15, ErrorFix.cs)
- 修正提案: 似たコマンド名の候補は、( ) の中の引数の数が合うものだけを出す（Put0 と Put など）。Structure['a']Put( / Structure['a'] Put0( のような . 抜けも直せる (2026-11-15, ErrorFix.cs / BveMapParserWorker/BveSemanticCommandRules.cs)
- 修正提案 段階3: 必須の Load がないときは Load の行（Sound.Load は Station.Load より前）、Cant.SetGauge / Curve.SetGauge がないときは SetGauge の行（1.067 / 1.435）を足す候補を出す。ファイル名が空のエラーは、インクルード先のタブからでもルートファイルを開く。開いているタブへは確認なしで切り替える (2026-11-15, ErrorFix.cs / MainWindow.xaml.cs)
- 修正提案 段階2: 未知のコマンド・リストにないキーで、つづりの近い名前を最大3つ「修正の候補」に出す (2026-11-15, ErrorFix.cs)
- 修正提案 段階1: エラー一覧の右クリック「修正の候補」で、; 抜け・; の連続・() 抜け・. 抜け・全角英数記号をその場で直す（Ctrl+Z で戻せる） (2026-11-15, ErrorFix.cs / MainWindow.xaml / MainWindow.xaml.cs)
- キーの自動補完: Repeater['abc'] など ] まで打つと関数の候補（.Begin(); / .Begin0(); / .End(); など）を出す。自動で入った ] の直前で ] を打つと二重にならずに進む。キーの候補は入力した文字で「始まる」ものだけに絞り込む (2026-11-15, AvalonEditClass.cs)
- 不具合修正: 候補が出ているときに [ などの記号を打つと候補が勝手に確定していたのを、確定せずに閉じるようにした (2026-11-15, AvalonEditClass.cs)
- キーの自動補完: Structure[''] のようにキーが空の引用符の間にカーソルを置くと、キーの候補を出す（Repeater は除く） (2026-11-15, AvalonEditClass.cs / ProjectFile.cs / MainWindow.xaml.cs)
- キーの自動補完: キーを選んで '] が入ったら、続けて .Put(); などの関数を候補に出す（選ぶとカーソルが ( と ) の間へ） (2026-11-15, AvalonEditClass.cs / BveCompletionData.cs)
- キーの自動補完: キーを選ぶと閉じる '] を足りない分だけ自動で入れる（Repeater の引数では ' だけ） (2026-11-15, BveCompletionData.cs / AvalonEditClass.cs)
- キーの自動補完 2-3: 「設定」メニューに「キーの自動補完を有効にする」を追加（「自動補完」とは別にオン・オフ、設定ファイルに保存） (2026-11-15, AppSettings.cs / MainWindow.xaml / SettingsMenu.cs / AvalonEditClass.cs)
- キーの自動補完 2-2: Repeater の Begin（11番目以降）/ Begin0（5番目以降）の引数で ' を打つと、ストラクチャーのキーを候補に出す (2026-11-15, AvalonEditClass.cs)
- キーの自動補完 2-1: Structure[' / Sound[' / Sound3D[' / Station[' / Signal[' / Train[' と打つと、読み込んだリストのキーを候補に出す (2026-11-15, AvalonEditClass.cs / BveCompletionData.cs / MainWindow.xaml.cs)
- キーの自動補完 段階1: ワーカーが構文チェックの結果と一緒に、読み込んだリストのキーの一覧を返し、エディタが覚えておく（画面はまだ変わらない） (2026-11-15, BveMapParserWorker/BveSemanticAnalyzer.cs / MapParser.cs / Program.cs / MainWindow.xaml.cs)

### ソースの整理
- 整理C: 自動補完の辞書をワーカーの BveSyntaxProvider.cs の1か所にまとめた。エディタはそこから候補を作るだけ。コマンドを足すときはワーカー側だけ直せばよい（候補の中身は変わらない） (2026-11-15, BveMapParserWorker/BveSyntaxProvider.cs / BveSyntaxProvider.cs)
- 整理A: 使われていない処理（CheckIncludeSyntaxWithUnsaved / 古い ParseRequest / QuickParseRegion / SaveFileAs）を削除、ルートファイルをたどる処理を GetRootFilePath にまとめた、重なった行とビルドの警告2件を修正（動きは変わらない） (2026-11-15, BveMapParserWorker/BveSemanticAnalyzer.cs / Program.cs / MapParser.cs / TextFileManager.cs / MainWindow.xaml.cs / AvalonEditClass.cs / ErrorFix.cs)

### 不具合の修正
- 文字列の引数の ' ' 忘れ（Put(xyz, …) / Put0(distance, …)）がエラーにならなかった不具合を修正し、'xyz' に直す候補を追加。閉じる ' が1つもない Put('0, 0, …) も直せる。distance + のように演算子で終わる距離程をエラーにし、演算子を消す候補を追加。閉じる ' がない行でコメントの日本語が1文字ずつ全角文字のエラーになるのを出さないようにした (2026-11-15, BveMapParserWorker/BveSemanticAnalyzer.cs / MapParser.cs / ErrorFix.cs)
- 1行に2つの文があり間の ; が抜けているとき、1つの文として読んで誤ったエラー（.End() の引数…）を出していた不具合を修正（; 抜けを出し、修正の候補で ) の後ろに ; を入れる）。SpeedLimit の複数記述の波線をコマンド名だけに。全角文字のエラーを続いている文字ごとに1件にまとめ、修正の候補でまとめて半角にする (2026-11-15, BveMapParserWorker/BveParser.cs / MapParser.cs / ErrorFix.cs)
- 検索ウィンドウを閉じるとメインウィンドウが別のアプリの後ろに回る不具合、開いたままタブを切り替えると前のタブを検索・置換していた不具合を修正 (2026-11-15, SearchWindow.xaml.cs / MainWindow.xaml.cs)
- 検索ウィンドウで Enter を押しても検索しない不具合、Ctrl+F・Ctrl+Shift+S が効かない不具合を修正。検索ウィンドウは1つだけ開き、開いていれば前に出して今のタブを検索する (2026-11-15, SearchWindow.xaml / .cs / MainWindow.xaml / EditMenu.cs)
- Station.Load が Structure.Load より前にあると、すべての Structure キーがエラーになる不具合を修正。書く順番に関係なくストラクチャーリストを読む (2026-11-15, BveMapParserWorker/BveSemanticAnalyzer.cs)
- 上書き保存すると文字コードが自動で記憶され、確認ダイアログが二度と出なくなる不具合を修正。ダイアログの「このファイルは次回から確認しない」にチェックしたときだけ記憶する (2026-11-15, SaveEncodingDialog.xaml / .cs / TextFileManager.cs)
- Ctrl＋マウスホイールの拡大・縮小が一番上を基準にしていた不具合を修正。マウスの下の文字を中心に拡大・縮小する (2026-11-15, SettingsMenu.cs)
- 「BOMなしUTF-8」で上書き保存すると、次回から「BOM付き」で保存される不具合を修正 (TextFileManager.cs)
- 文字コードがタブごとに管理されていなかった不具合を修正 (MainWindow.xaml.cs / TextFileManager.cs)
- BOM付きファイルを開くと BOM 文字が本文に残る不具合を修正。BOM付きのまま保存できるようにした (MainWindow.xaml.cs / TextFileManager.cs / ProjectFile.cs)
- 文字コードをファイル先頭 4KB だけで判定していた不具合を修正。ファイル全体で判定する (MainWindow.xaml.cs / TextFileManager.cs / ProjectFile.cs)
- Shift-JIS で保存できない文字が黙って「?」になる不具合を修正。保存前に確認する。保存キャンセル時に閉じる処理を止める (TextFileManager.cs / MainWindow.xaml.cs)
- 新規作成で開いていたファイルが閉じられる不具合を修正。新規作成は「無題」のタブを追加する (MainWindow.xaml.cs)
- 構文チェックのパイプ通信が毎回失敗する不具合を修正。ワーカーのやり取りの形をエディタに合わせた (BveMapParserWorker/Program.cs)
- 構文チェックの結果が波線に表示されない不具合を修正 (MainWindow.xaml.cs / ErrorUnderlineRenderer.cs)
- include のファイル名を読み取れず、インクルード先の中身がチェックされていなかった不具合を修正 (BveMapParserWorker/BveSemanticAnalyzer.cs)
- BOM付きのインクルード先の1行目が誤ってエラーになる不具合を修正 (BveMapParserWorker/BveSemanticAnalyzer.cs)
- include のチェックが同じ行に2回呼ばれていた無駄を削除 (BveMapParserWorker/BveSemanticAnalyzer.cs)
- インクルード先の全角文字・; の連続などのエラーが親ファイルのエラーとして表示される不具合を修正 (BveMapParserWorker/BveSemanticAnalyzer.cs)
- パーサー内で想定外のエラーが起きてもワーカーを強制終了せず、エラー内容をエラー一覧に表示する (BveMapParserWorker/Program.cs)
- タブのファイル名の「_」が表示されない不具合を修正 (MainWindow.xaml)
- 「; 抜け」の検出を新しく作った（カッコが閉じている文が対象、レベルはエラー） (2026-11-15, BveMapParserWorker/BveParser.cs)
- 書き換えて元に戻したときも未保存の警告が出ていた不具合を修正。開いた・保存した時点の本文と比べて「変更あり」を決める (2026-11-15, MainWindow.xaml.cs / AvalonEditClass.cs / TextFileManager.cs)
- 脆弱性対策 V3・V4: 計算式での「.名前」（関数の呼び出しなど）を禁止し、細工されたマップでメモリを大量に使わせられないようにした。通信のデータは読みながら上限を確認 (2026-11-15, BveMapParserWorker/BveSemanticAnalyzer.cs / Program.cs)
- 上下にスクロールすると水平スクロールバーが出たり消えたりして画面がガタガタする不具合を修正（常に表示。折り返し時は非表示） (2026-11-15, MainWindow.xaml)
- 何もしていないときもエディタが CPU を 8% 前後使っていた不具合を修正（波線の描き直しの繰り返しを削除） (2026-11-15, ErrorUnderlineRenderer.cs)
- 脆弱性対策 V1・V2: パイプの名前をエディタごとに変え、接続相手が同じユーザー・自分が起動したワーカーかを確認。2つ目のワーカーが CPU を使い続ける問題も解消 (2026-11-15, MainWindow.xaml.cs / BveMapParserWorker/Program.cs)
- 波線を間違っている部分だけに引くようにした（キー・ファイルパス・コマンド名・引数の値・変数名など。約 87% のエラーが対象） (2026-11-15, BveMapParserWorker/MapParser.cs / BveParser.cs)
- 文を元の空白どおりに組み立てるようにし、止まっていたチェック（[] 抜け・. 抜け・Load のファイル確認など）が動くようにした。エラーの列も正しくなった。() 抜けの検出を追加。行末より後ろを指すエラー（閉じ括弧不足など）も波線を表示 (2026-11-15, BveMapParserWorker/BveParser.cs / ErrorUnderlineRenderer.cs)
- 波線をエラー・警告ごとに描き、重なりは1本にまとめ、エラー（赤）を警告（オレンジ）より優先。ParseError に長さ Length を追加（段階2の準備） (2026-11-15, ErrorUnderlineRenderer.cs / BveMapParserWorker/MapParser.cs)
- ファイルを開くときの余計な確認（エラー残存・未保存の変更）をやめた。空の無題タブ以外は新しいタブで開き、無題タブの内容が消えないようにした (2026-11-15, TextFileManager.cs)
- 複数行にまたがる文（次の行が + で始まる等）が「; 抜け」と誤検出される不具合を修正。1つの文として読む (2026-11-15, BveMapParserWorker/BveParser.cs)
- 解析をさらに高速化（distance の式・正規表現の作り直し削減・変数代入）。作業用フォルダの計測で 3793F_P.txt 約 0.8 秒 → 約 0.43 秒、include 4 倍のマップで約 33% 短縮。174 ファイルでエラー内容が一致 (2026-11-15, BveMapParserWorker/BveSemanticAnalyzer.cs / BveParser.cs)
- include が多いマップの解析を高速化（3793F_P.txt で約 7 秒 → 約 1.5 秒）。「数字 ± 変数」の距離程を重い Eval なしで計算、正規表現を覚えておく数を増やした (2026-11-15, BveMapParserWorker/BveSemanticAnalyzer.cs / Program.cs)
- 大きいマップで「ワーカーとの通信に失敗しました」になる不具合を修正。問い合わせを1つずつにし、接続待ちを5秒に、時間切れでワーカーを強制終了しないようにした。失敗時は詳しい理由を表示 (2026-11-15, MainWindow.xaml.cs)
- Track[''].Gauge(); が「未知のコマンド」になる不具合を修正。自動補完にも追加 (2026-11-15, BveMapParserWorker/BveSyntaxProvider.cs / BveSemanticCommandRules.cs / BveSemanticAnalyzer.cs / BveSyntaxProvider.cs)
- インクルード先の未保存の内容でチェックするようにした。インクルード先のタブでは親ファイルからたどってチェックする（親子対応表）(BveMapParserWorker/BveSemanticAnalyzer.cs / MapParser.cs / Program.cs)

## 実装がまだ
- [x] プロジェクト内のソースコードを整理する / バグつぶし ← 整理A・B・C すべて完了・動作確認OK (2026-11-15)
	- [x] 小さく進める
	- 調査 (2026-11-15)。作業用フォルダでビルドした警告は2件だけ（ErrorFix.cs / Program.cs の null の可能性）
		- [x] 整理A ← A1〜A4 まとめて作成済み・動作確認OK (2026-11-15)
			- A1: CheckIncludeSyntaxWithUnsaved（BveSemanticAnalyzer.cs）・古い ParseRequest（Program.cs）・QuickParseRegion（MapParser.cs）・SaveFileAs（TextFileManager.cs）を削除（削除した所に「整理」のコメント）
			- A2: MainWindow.xaml.cs に GetRootFilePath を追加し、RunSyntaxCheck / OpenErrorFileAsync / GetKeyList（AvalonEditClass.cs）から使う
			- A3: SaveFile の2回続いていた isModified = false を1つに（UpdateTitle は間に別の処理があるので2回のまま）
			- A4: 警告2件を直した（ErrorFix.cs: 辞書がなければ空として扱う / Program.cs: jsonRequest ?? string.Empty）
			- 作業用フォルダで確認: エディタのコピーをビルドして警告 0 件。ワーカーは 175 ファイルでエラー内容が整理前と完全に一致
			- ユーザーが古いファイル（BveCommandRules.cs / CheckCommandArguments.cs / CheckCommandSyntax.cs / CommandRules.cs）を削除済みなのを確認 → CLAUDE.md の構成から外した
			- 修正前のファイル: _backup/2026-11-15/*.beforeCleanA.bak（8ファイル: Worker_BveSemanticAnalyzer.cs / Worker_Program.cs / Worker_MapParser.cs / TextFileManager.cs / MainWindow.xaml.cs / AvalonEditClass.cs / ErrorFix.cs / CLAUDE.md）
		- 整理A（動きは変わらない）
			- A1. どこからも呼ばれていない処理: BveSemanticAnalyzer.cs の CheckIncludeSyntaxWithUnsaved（約110行）、Program.cs の古い ParseRequest、MapParser.cs の QuickParseRegion、TextFileManager.cs の SaveFileAs
			- A2. 「ルートファイルをたどる」同じ処理が3か所（MainWindow.xaml.cs の RunSyntaxCheck と OpenErrorFileAsync、AvalonEditClass.cs の GetKeyList）→ 1つにまとめる
			- A3. TextFileManager.cs の SaveFile に同じ行が2回ずつ（isModified = false / UpdateTitle）
			- A4. ビルドの警告2件
		- 整理B（見つけた不具合）: 検索ウィンドウで Enter を押しても検索しない（SearchWindow.xaml.cs の CmbQuery_KeyDown がどこにもつながっていない）
			- [x] ← 修正済み・動作確認OK (2026-11-15)
			- SearchWindow.xaml: 検索欄 CmbSearchQuery の PreviewKeyDown に CmbQuery_KeyDown をつないだ
			- SearchWindow.xaml.cs: Enter で「次を検索」と同じ動き。履歴の一覧を開いているときは検索しない（一覧から選ぶ操作のため）
			- 作業用フォルダでエディタのコピーをビルドできることを確認（警告 0 件）
			- 修正前のファイル: _backup/2026-11-15/SearchWindow.xaml(.cs).beforeEnter.bak（2ファイル）
			- ユーザー確認 (2026-11-15): Enter は動いた。しかし Ctrl+F で検索ウィンドウが開かない
				- 原因: メニューの「Ctrl+F」は表示だけ（InputGestureText）で、キーにつながっていなかった。同じ理由で Ctrl+Shift+S（名前を付けて保存）も動かない見込み（WPF の SaveAs には決まったキーがない）
				- 修正（動作確認OK）: MainWindow.xaml に ApplicationCommands.Find（Ctrl+F）のつなぎを追加し、EditMenu.cs の MenuSearchCommand_Executed から検索ウィンドウを開く。Ctrl+Shift+S も Window.InputBindings でつないだ
				- 作業用フォルダでエディタのコピーをビルドできることを確認（警告 0 件）
				- 修正前のファイル: _backup/2026-11-15/*.beforeCtrlF.bak（MainWindow.xaml / EditMenu.cs）
			- [x] ユーザー要望 (2026-11-15): 検索ウィンドウがすでにあるときに Ctrl+F で新たに開かないようにする ← 修正済み・動作確認OK
				- EditMenu.cs: 開いている検索ウィンドウを _searchWindow に覚える。開いていれば新しく作らず、最小化を戻して前に出す（閉じたら忘れる）
				- SearchWindow.xaml.cs: ReuseFor を追加。検索するエディタを今のタブに差し替え（タブを切り替えた後でも今のタブを検索する）、検索欄の文字を全選択する
				- 作業用フォルダでエディタのコピーをビルドできることを確認（警告 0 件）
				- 修正前のファイル: _backup/2026-11-15/*.beforeSingle.bak（SearchWindow.xaml.cs / EditMenu.cs）
		- 整理C（大きめ・後で）: 自動補完の辞書（BveSyntaxProvider.cs）がエディタとワーカーに2つある（中身の73個は同じ）。片方だけ直すとずれる（Track.Gauge のときは両方直した）→ 1つにまとめる
			- 調査 (2026-11-15): 作業用フォルダで2つの辞書の中身を比べた。73個すべて、並び順・コマンド・説明文が完全に同じ。違いは、エディタ側は改行のある説明文2つ（SetFunction）を折り返し表示の部品（TextBlock）にしていることだけ
			- エディタはワーカーのプロジェクトを参照している（BveMapEditor.csproj）ので、ワーカーの辞書をそのまま読める
			- まとめ方の案: ワーカーの辞書（BveMapParserWorker/BveSyntaxProvider.cs）を「元」にする
				1. ワーカー側: 辞書をエディタから読めるようにし（public）、「コマンドと説明文の一覧」を返す GetSyntaxEntries を追加。チェック（GetValidCommands）は今まで通り
				2. エディタ側（BveSyntaxProvider.cs）: 73個の一覧を消し、ワーカーの一覧から候補を作るだけにする。改行のある説明文は今まで通り折り返し表示にする
				- これからコマンドを足すときは、ワーカーの BveSyntaxProvider.cs の1か所だけ直せばよい
				- 確かめ方: 作業用フォルダで、まとめる前と後のエディタの候補（73個のコマンド・説明文）が同じこと、ワーカーの 175 ファイルの結果が同じことを比べる
			- [x] ← 案のとおり作成済み・動作確認OK (2026-11-15)
				- BveMapParserWorker/BveSyntaxProvider.cs: クラスを public にし、GetSyntaxEntries（コマンドと説明文の一覧）を追加。GetAllSyntaxes はワーカーの中だけで使う（internal）
				- BveMapEditor/BveSyntaxProvider.cs: 73個の一覧を消し、ワーカーの GetSyntaxEntries から候補を作るだけにした。改行のある説明文は折り返し表示（TextBlock）
				- 作業用フォルダで確認: まとめる前と後で、エディタの候補 73 個（コマンド・説明文・表示の種類）が完全に同じ。ワーカーは 175 ファイルでエラー内容が完全に一致。ビルドの警告 0 件
				- 修正前のファイル: _backup/2026-11-15/*.beforeOneDict.bak（BveSyntaxProvider.cs / Worker_BveSyntaxProvider.cs / CLAUDE.md）
		- 参考: エディタ・ワーカーの古いファイル（CheckCommandArguments.cs / CheckCommandSyntax.cs / CommandRules.cs / BveCommandRules.cs）はユーザーが削除済み (2026-11-15)
- [x] インクルード先のエラーチェックを直す ← (A)〜(E) すべて修正・動作確認OK (2026-11-14)
	- 調査結果 (2026-11-14)。場所はすべて BveMapParserWorker/BveSemanticAnalyzer.cs
	- [x] (A) include のファイル名を読み取る正規表現 IncludePathRegex が壊れている（いちばん大きい原因） ← 修正済み・動作確認OK (2026-11-14)
		- 確認: インクルード先に存在しないコマンドを書くと、親ファイルのエラー一覧に出た
		- 修正: `(['"])` に直した。PowerShell で include 'a.txt' / include "a.txt" が一致することを確認済み
		- 修正前のファイル: _backup/2026-11-14/Worker_BveSemanticAnalyzer.cs.beforeIncA.bak
		- 今の形 `(\['"\])` は「'」や「"」ではなく「['"]」という4文字そのものを探してしまう
		- そのため `include 'a.txt';` が一致せず、すべての include 行が「アポストロフィ等で囲む必要があります」エラーになり、インクルード先の中身は一度もチェックされていない
		- 直し方: `(['"])` に直す（1行）
	- [x] (B) インクルード先のファイルを読むとき、BOM（見えない文字）を取り除いていない（ReadAllTextWithEncodingFallback） ← 修正済み・動作確認OK (2026-11-14)
		- 修正: 読み込んだ文字列の先頭の U+FEFF を取り除く
		- 修正前のファイル: _backup/2026-11-14/Worker_BveSemanticAnalyzer.cs.beforeIncB.bak
		- BOM付きのインクルード先は 1 行目が誤ってエラーになる。エディタ側の (3) と同じ問題
	- [x] (C) インクルード処理が2つある
		- 使われている CheckIncludeSyntaxInternal: 書き方のチェックはあるが、未保存のタブの内容を使わない（保存済みのファイルを読む）、親子対応表（ParentMap）を作らない
		- 使われていない CheckIncludeSyntaxWithUnsaved: 未保存タブ・親子対応表に対応しているが、書き方のチェックがない
		- ParentMap が空のため、エディタでインクルード先のファイルのタブを開くと、そのファイルだけで単独のマップとして解析される（ヘッダーなし・Load なしなどの誤ったエラーが出る）
		- 直し方: 使われている方に「未保存タブ」「親子対応表」を足し、MapParser.Parse → Program.cs まで受け渡す。使われていない方は使用しない印をつける
		- 直し方の案（詳細, 2026-11-14）。変更はワーカー側の3ファイルだけ。エディタ側は変更なし
			1. BveSemanticAnalyzer.cs: CheckIncludeSyntaxInternal に「親ファイルのパス」「未保存タブの内容」「親子対応表」を追加。未保存の内容があればそれを使い、親子対応表に「インクルード先 → 親」を記録する。Analyze → CheckIncludeSyntax にも同じものを追加（省略可能な引数にして、ほかの呼び出しに影響させない）
			2. MapParser.cs: 上の3つを受け取る Parse を追加（今の Parse はそのまま残す）
			3. Program.cs: リクエストの RouteFilePath / UnsavedTexts を渡し、できた親子対応表を ParentMap に入れて返す。UnsavedTexts は大文字小文字を区別しない辞書に入れ直す（JSON から読むと区別する辞書になるため）
			4. CheckIncludeSyntaxWithUnsaved は「使用しない」とコメントする（消さない）
		- 制限: 親ファイルを一度もチェックしていない状態でインクルード先だけを開くと、親子対応表がないので今まで通り単独で解析される
		- ← 案のとおり修正済み・動作確認OK (2026-11-14)
			- BveSemanticAnalyzer.cs: Analyze / CheckIncludeSyntax / CheckIncludeSyntaxInternal に rootFilePath・unsavedTexts・parentMap（省略可）を追加
			- MapParser.cs: Parse(text, baseDirectory, rootFilePath, unsavedTexts, parentMap) を追加。今の Parse(text, baseDirectory) はこれを呼ぶだけにした
			- Program.cs: RouteFilePath・UnsavedTexts を渡し、ParentMap を返す
			- 修正前のファイル: _backup/2026-11-14/Worker_*.beforeIncC.bak（3ファイル）
	- [x] (E) インクルード先の字句解析・構文解析のエラー（BveLexer / BveParser）が FileName 空のまま追加され、親ファイルのエラーとして扱われる ← 修正済み・動作確認OK (2026-11-14)
		- 修正: CheckIncludeSyntaxInternal で、字句解析・構文解析のエラーを別のリストに集め、インクルード先のファイル名を付けてから追加する
		- 修正前のファイル: _backup/2026-11-14/Worker_BveSemanticAnalyzer.cs.beforeIncE.bak
		- 経緯: 「; 抜け」がエラー一覧に出ない報告を調べて見つけた。「; 抜け」の検出処理自体がないことは、別作業「; 抜けを検出する処理を作り直す」に移した
	- [x] (D) Analyze の中で CheckIncludeSyntax が同じ行に対して2回呼ばれている（237行目と304行目。2回目は意味がない） ← 修正済み・動作確認OK (2026-11-14)
		- 修正: 2回目の呼び出しを削除（チェック結果は変わらない）
		- 修正前のファイル: _backup/2026-11-14/Worker_BveSemanticAnalyzer.cs.beforeIncD.bak
- [x] ワーカーがパーサー内のエラーで強制終了する（Environment.FailFast）← 修正済み・動作確認OK (2026-11-14)
	- 修正: BveMapParserWorker/Program.cs で FailFast をやめ、エラー内容（例外の種類とメッセージ）をエディタのエラー一覧に返す
	- 修正前のファイル: _backup/2026-11-14/Worker_Program.cs.beforeFailFast.bak
- [x] 「; 抜け」を検出する処理を作り直す（今のパーサーにはない。親ファイル・インクルード先の両方で必要）
	- 調査結果 (2026-11-15): 以前のパーサーのファイル（CheckCommandSyntax.cs など）にも ; 抜けの検出は残っていない。新しく作る
	- 作り方の案: BveMapParserWorker/BveParser.cs の ParseTokens だけを変更
		- 今のパーサーは「行が変わったのに ; が来ていない文」を、その行で区切って続けている（51〜60行目、90〜98行目）
		- その2か所で「文の最後が ; ではない」ことを見つけたら、「文の終わりにセミコロン ';' がありません。」を追加する
		- 位置は文の最後の文字の直後（行・列）。ヘッダー行（BveTs Map …）とコメントだけの行は対象外
		- インクルード先も同じパーサーを通るので、(E) の修正によりインクルード先のファイル名付きで出る
	- 決めること: レベル（エラー / 警告）、括弧が閉じていない行（複数行にまたがる文の可能性）も対象にするか
		- 決定 (2026-11-15): レベルは「エラー」。カッコ・角括弧が閉じている文だけを対象にする
	- ← 修正済み・動作確認OK (2026-11-15)
		- 修正: BveParser.cs に CheckMissingSemicolon を追加し、ParseTokens の2か所（行が変わったとき・ファイルの最後）で呼ぶ
		- 位置は「文の最後の単語・記号」（行末より後ろだと波線が描けないため）
		- 修正前のファイル: _backup/2026-11-14/Worker_BveParser.cs.beforeSemicolon.bak
- [ ] 全作業が終わったら RELEASE.md にリリース内容を書く 
- [x] 脆弱性対策が必要かどうかを監査する。 ← V1〜V4 すべて修正・動作確認OK (2026-11-15)
	- 監査結果 (2026-11-15)。ファイルは変更せず、ソースの確認と作業用フォルダでの試験で調べた
	- 対策が必要なもの（危険の大きさ順）
		- [x] V1.（中）パイプの「なりすまし」: パイプの名前が固定（BveMapParserPipe）。別のユーザーや別のプログラムが先に同じ名前のパイプを作ると、エディタはそれに接続してしまい、マップの本文を渡したり、偽の結果を受け取ったりする。エディタは相手のプロセスIDを取得しているが、自分が起動したワーカーか比べていない（MainWindow.xaml.cs の ParseViaPipeAsync）
			- 対策案: パイプの名前をエディタごとに変える（エディタのプロセスIDを付けてワーカーに渡す）。接続時に「相手が自分と同じユーザー」（PipeOptions.CurrentUserOnly）と「相手が自分の起動したワーカー」を確認する
		- [x] V2.（中・推定）エディタを2つ起動すると、2つ目のワーカーがパイプを作れず、待ち時間なしで作り直しを繰り返して CPU を使い続ける（Program.cs の while ループ。作成に失敗すると catch してすぐ次へ）
			- 対策案: V1 の「名前をエディタごとに変える」で同時に解決。加えて、作成に失敗したら少し待つ
		- V1・V2 ← 修正済み・動作確認OK (2026-11-15)
			- MainWindow.xaml.cs: パイプの名前を BveMapParserPipe_<エディタのプロセスID> にし、ワーカーに「--worker --pipe 名前」で渡す。接続時に PipeOptions.CurrentUserOnly を付け、相手のプロセスIDが起動したワーカーと同じか確かめる（違えば送らない）
			- BveMapParserWorker/Program.cs: 「--pipe 名前」を受け取る（BveMapParserPipe_数字 の形だけ受け付ける。無ければ以前の名前）。パイプを作れなかったら 1 秒待ってやり直す
			- 作業用フォルダでワーカーのコピーをビルドして試験: 渡した名前のパイプで返事が来ること、同じ名前で2つ目を起動しても CPU を使わない（3 秒で 0 ms）ことを確認。エディタ側はアプリでの確認が必要
			- 修正前のファイル: _backup/2026-11-14/*.beforeV1.bak（2ファイル）
		- [x] V3.（低〜中）細工されたマップで大量のメモリを使わせられる: 計算式（Eval）で文字列の関数が使える（例: "x".PadLeft(50000000) を 4 回つなぐと約 400MB を確保できた）。BVE の構文には文字列の関数呼び出しはない
			- 対策案: 計算式に「.名前(」の形（関数の呼び出し）がある場合は計算しない
			- ← 修正済み・動作確認OK (2026-11-15): BveSemanticAnalyzer.cs の NormalizeVariableExpression（全ての Eval が通る所）で、'…' "…" の外に「.名前」があれば計算しない（エラー「計算式に「.名前」の形（関数の呼び出しなど）は使えません。」）
				- 試験: 細工したマップ（"x".PadLeft(50000000) など）が 1094 ms → 117 ms・エラー表示に。普通の式（floor・'…'+'.txt'・1.5+0.25）は今まで通り。174 ファイルでエラー内容は全く同じ
		- [x] V4.（低）20MB の上限チェックが、1行を全部読み込んだ後に行われている（Program.cs）。同じユーザーのプログラムからしか送れないため影響は小さい
			- 対策案: 読み込みながら上限を超えたら打ち切る
			- ← 修正済み・動作確認OK (2026-11-15): Program.cs に ReadLineWithLimit を追加。読みながら 20MB 相当を超えたら、それ以上はためずに読み捨てて「大きすぎる」と返す（読み捨ても 200MB 相当で打ち切り）
				- 試験（ワーカーのコピーを起動）: 普通のリクエストは今まで通り。2,200 万文字のリクエストで、ワーカーの最大メモリ 193MB → 87MB。返事は今まで通り「大きすぎる」
			- V3・V4 の修正前のファイル: _backup/2026-11-14/Worker_*.beforeV3V4.bak（2ファイル）
	- 問題なかったもの
		- 計算式から System.IO・Environment などのクラスやリフレクション（GetType など）は使えない（試験で確認。DynamicExpresso が既定で禁止）
		- パイプは「今のユーザーだけ」がワーカーに接続できる（Program.cs の PipeSecurity）
		- エラーのヘルプ（HTML）はメッセージを HTML エンコードしてから埋め込んでいる。Google 検索の URL もエスケープしている
		- 関連付けのレジストリ登録はパスを " で囲んでいる。管理者権限は登録・解除のときだけ UAC で求める。管理者で起動したときは警告を出す
		- 通信・設定・プロジェクトの JSON は決まった型にだけ読み込む
	- [ ] 小さく進める
- [x] リストからキーを抽出して['']の中に自動補完候補 ← 段階1・2-1〜2-3、ユーザー追加の6項目すべて完了・動作確認OK (2026-11-15)
    - Repeater構文の場合はBegin 12引数以降 Begin 6引数以降 
	- 設定を追加する
	- 調査 (2026-11-15): キーの一覧はワーカーが構文チェックのときに読み込んでいる（BveSemanticAnalyzer.cs の LoadKeysFromCsv）が、エディタには渡していなかった
		- Repeater のキーの位置: Begin は丸括弧の11番目以降、Begin0 は5番目以降（Repeater['キー'] を1つ目と数えると12番目・6番目）
		- 調査中に見つけた不具合（Station.Load が先だと Structure キーがすべてエラー）は先に修正済み（バグの欄を参照）
	- [x] 段階1（ワーカー側）: 構文チェックの結果と一緒にキーの一覧を返す ← 修正済み・動作確認OK (2026-11-15)
		- BveSemanticAnalyzer.cs: ParseResponse に KeyLists を追加。Analyze が Structure / Sound / Sound3D / Station / Signal / Train のキーを名前順で書き込む
		- MapParser.cs: keyLists を受け取る Parse を追加 / Program.cs: KeyLists をエディタに返す
		- MainWindow.xaml.cs: 受け取ったキーの一覧をルートファイルごとに覚える（_keyListsByRoute）。画面はまだ変わらない
		- 作業用フォルダで確認: 3793F_P.txt で Structure 730 / Sound 15 / Sound3D 19 / Station 12 / Signal 4 個のキーが返る
		- 制限: インクルード先に書いた .Load のキーは入らない（今のキーのチェックと同じ）
		- 修正前のファイル: _backup/2026-11-15/*.beforeKeyLists.bak（4ファイル）
	- [x] 段階2（エディタ側）: Structure[' などで候補を出す。Repeater の Begin / Begin0 のキーの位置でも出す。「設定」メニューにオン・オフを追加
		- [x] 2-1: ['' の中でキーの候補を出す ← 作成済み・動作確認OK (2026-11-15)
			- AvalonEditClass.cs: TryShowKeyCompletion を追加。カーソルの手前が Structure[' / Sound[" などなら、そのリストのキーを候補に出す（Structure / Sound / Sound3D / Station / Signal / Train）。入力した文字で絞り込み。引用符の中ではコマンドの候補を出さない
			- AvalonEditClass.cs（TextEntering）: キーの候補中は ' " ] ) 空白で候補を閉じる（選択中の候補は入れない）。- などはそのまま入力
			- BveCompletionData.cs: キー用の候補 BveKeyCompletionData を追加（引用符の直後から入力中の文字までを置き換える）
			- MainWindow.xaml.cs: 受け取ったキーの一覧を大文字小文字を区別しない辞書に入れ直す
			- 今は「自動補完」のオン・オフに従う（キー専用のオン・オフは 2-3）。構文チェックが一度終わるまでは候補なし
			- 作業用フォルダでエディタのコピーをビルドできることを確認
			- 修正前のファイル: _backup/2026-11-15/*.beforeKeyComp.bak（2ファイル）
		- [x] 2-2: Repeater の Begin / Begin0 で候補を出す ← 作成済み・動作確認OK (2026-11-15)
			- AvalonEditClass.cs: TryGetRepeaterKeyContext を追加。手前の ; より後ろから Repeater['…'].Begin( / Begin0( を探し、カンマを数えて（引用符・内側の括弧の中は数えない）、Begin は11番目以降・Begin0 は5番目以降の引用符の中ならストラクチャーのキーを候補に出す
			- 複数行にまたがる Repeater にも対応（手前 4000 文字まで）
			- 作業用フォルダでエディタのコピーをビルドできることを確認
			- 修正前のファイル: _backup/2026-11-15/AvalonEditClass.cs.beforeRepeaterKey.bak
		- [x] 2-3: 「設定」メニューにオン・オフを追加 ← 作成済み・動作確認OK (2026-11-15)
			- AppSettings.cs: EnableKeyCompletion（最初はオン）を追加
			- MainWindow.xaml: 「設定」メニューの「自動補完を有効にする」の下に「キーの自動補完を有効にする(_K)」を追加
			- SettingsMenu.cs: MenuKeyCompletion_Click（すぐ保存）、起動時にチェック状態を反映
			- AvalonEditClass.cs: キーの自動補完は「自動補完」とは別にオン・オフを決める。オフのときは候補を出さない（引用符の中ではコマンドの候補も出さない）
			- 作業用フォルダでエディタのコピーをビルドできることを確認
			- 修正前のファイル: _backup/2026-11-15/*.beforeKeySetting.bak（4ファイル）
	- [x] ['']のようにキーが空の時にカーソルを持ってくると自動補完を表示する (Repeater構文は空でもエラーにならないのでRepeater構文を除く)
		- ← 作成済み・動作確認OK (2026-11-15)
		- AvalonEditClass.cs: OnCaretMovedForKeyCompletion を追加。カーソルの両側が '' か "" で、手前が Structure[ などなら、キーの候補を出す（Repeater は対象外: TryShowKeyCompletion に allowRepeater を追加）。入力の焦点があり、選択なし、ほかの候補が出ていないときだけ。「キーの自動補完」がオフなら出さない
		- ProjectFile.cs（InitializeEditor）/ MainWindow.xaml.cs（最初の「無題」タブ）: カーソルが動いたときに上の処理を呼ぶ
		- 作業用フォルダでエディタのコピーをビルドできることを確認
		- 修正前のファイル: _backup/2026-11-15/*.beforeEmptyKey.bak（3ファイル）
	- [x] 入力後に自動的に']を挿入する　（オプションの設定にある自動補完はキーの挿入後は動かないので注意）
		- ← 作成済み・動作確認OK (2026-11-15)
		- BveCompletionData.cs（BveKeyCompletionData.Complete）: キーを選んだ後、カーソルの後ろを見て足りない分だけ入れる。何もない → ']、] だけ → '、'] がある → 入れない。カーソルは ] の後ろへ。開いた引用符と同じ文字（' か "）で閉じる。キーの途中を書き換えたとき（後ろにキーの続きがある）は何も入れない。Ctrl+Z 1回で戻せる
		- Repeater の引数のキーでは閉じる引用符だけ入れる（AvalonEditClass.cs から closeBracket を渡す）
		- 作業用フォルダで確認: 上の6通りの入力で期待どおりの結果。エディタのコピーもビルドできる
		- 修正前のファイル: _backup/2026-11-15/*.beforeAutoClose.bak（2ファイル）
	- [x] 挿入後の残りの関数を続けて挿入する。(Structure['abc -> この先はどうするかを候補を出す Structure['abc'].put...)
		- ← 作成済み・動作確認OK (2026-11-15)
		- AvalonEditClass.cs: ShowMethodCompletion を追加。キーを選んで '] まで入ったら、その種類の関数（Structure なら .Put(); / .Put0(); / .PutBetween();）を候補に出す。候補は自動補完の辞書（BveSyntaxProvider の Structure[''].Put(); など）から取り出す。後ろにすでに . があるとき（キーだけ直したとき）は出さない。Repeater の引数のキーでは出さない。候補中は ; でも閉じる
		- BveCompletionData.cs: BveKeyCompletionData に AfterClose（'] を入れた後の処理）を追加。関数用の候補 BveMethodCompletionData を追加（.Put(); を入れてカーソルを ( と ) の間へ）
		- 作業用フォルダで確認: キーを選ぶと AfterClose が呼ばれる（キーの途中・Repeater では呼ばれない）。.Put(); を選ぶと Structure['KEY'].Put(|); になる。エディタのコピーもビルドできる
		- 修正前のファイル: _backup/2026-11-15/*.beforeMethodComp.bak（2ファイル）
	- [x] Repeater構文も']まで入力したら自動補完候補を出す(End, Begin, Begin0)
		- ← 作成済み・動作確認OK (2026-11-15)
		- AvalonEditClass.cs: TryShowMethodAfterBracket を追加。] を打ってカーソルの手前が Repeater['abc'] などになったら関数の候補を出す（Repeater は .Begin(); / .Begin0(); / .End();。Track・Structure なども同じ仕組みで出る）
		- AvalonEditClass.cs: TryStepOverClosingBracket を追加。括弧の自動補完で入った ] の直前で ] を打ったら、]] にせず ] の後ろへ進む（その後に関数の候補）
		- 「キーの自動補完」がオフなら出さない
	- [x] バグ Structure[ など、[を入力すると自動的に自動補完候補が確定されてしまう
		- ← 修正済み・動作確認OK (2026-11-15)
		- 原因: AvalonEditClass.cs の TextEntering で、コマンドの候補が出ているときに記号（[ ( ' ; など）を打つと、選択中の候補を確定していた
		- 修正: 記号を打ったら候補を確定せずに閉じる。確定は Enter / Tab のときだけ
	- [x] 入力文字を見て候補を絞り込む（ユーザー回答: キーの候補のこと）
		- ← 作成済み・動作確認OK (2026-11-15)
		- 原因: 候補ウィンドウ標準の絞り込みは、入力した文字を「含む」キーも出していた
		- AvalonEditClass.cs: FilterKeyCompletion を追加。入力した文字で「始まる」キーだけにする（大文字小文字は区別しない）。文字の入力・Backspace のたびに絞り込み直す。1つもなければ候補を閉じる
	- 3項目まとめて: 作業用フォルダでエディタのコピーをビルドできることを確認 / 修正前のファイル: _backup/2026-11-15/AvalonEditClass.cs.beforeKeyFilter.bak

	- [x] 修正提案の時、Structure...Put0 / Put の提案がある場合は()内の引数の数を見る（間違って引数が合わない方を選択してエラーを増やすだけです）
		- 調査 (2026-11-15): 引数の数の決まりはワーカーの BveSemanticCommandRules.cs にある（例: Structure.Put は 9 個、Structure.Put0 は 3 個）が、エディタからは読めない。今の「似たコマンド名」の候補は、つづりだけで選んでいる
		- 直し方の案:
			1. ワーカー（BveSemanticCommandRules.cs）: 「コマンドの引数の数の範囲」を返す GetArgumentCountRange を追加（エディタから読めるように public）
			2. エディタ（ErrorFix.cs）: エラーの行の ( ) の中の引数を数え（引用符の中・内側の括弧の中のカンマは数えない）、数が合わない候補は出さない。決まりのないコマンドや、( ) が次の行に続いていて数えられないときは今まで通り
	- [x] Structure['CrossSig']Put('',...をStructure['CrossSig'].Put('', ... のように直せるようにする
		- 調査 (2026-11-15): この書き方は「未知のコマンドまたは存在しない単語です: 'StructurePut'」になる（キーの部分を除いて名前を作るため . のない1つの名前になる）。今の候補は「Structure.Put」が出るが、. の数が違うので「自動で直せませんでした」になる
		- 直し方の案（ErrorFix.cs）: 候補の名前から . を除くとエラーの名前と同じとき（StructurePut と Structure.Put）は、行の中の「Structure['CrossSig']Put」を探して、抜けている所に . を入れる（Structure['CrossSig'].Put）。1 の引数の数の確認もかかる（Put と Put0 のうち、数が合う方だけ出す）
	- 2項目まとめて ← 作成済み・動作確認OK (2026-11-15)
		- BveMapParserWorker/BveSemanticCommandRules.cs: GetArgumentCountRange（コマンドの引数の数の範囲。決まりがなければ false）を追加
		- ErrorFix.cs: GetErrorLineText（エラーの行を、開いているタブ・なければファイルから読む）、CountArguments（( ) の中の引数を数える）を追加。似たコマンド名の候補は、引数の数が合わないもの・直せない形のものを出さない
		- ErrorFix.cs: InsertMissingDots / CanReplaceCommandName を追加。. が抜けているだけなら抜けている所に . を入れる（間の空白も . にする）
		- 作業用フォルダで確認: Structre.Put0('0',0,1) → Structure.Put0 だけ / Structre.Put(9個) → Structure.Put だけ / Structure['CrossSig']Put(9個) → Structure['CrossSig'].Put / Structure['CrossSig'] Put0(3個) → Structure['CrossSig'].Put0 / ) が次の行に続くときは今まで通り。ワーカーは 175 ファイルで結果が同じ。ビルドの警告 0 件
		- 修正前のファイル: _backup/2026-11-15/*.beforeArgCount.bak（ErrorFix.cs / Worker_BveSemanticCommandRules.cs）
		- ユーザー報告 (2026-11-15): Structure['CrossSig'] Put0('0', 0, 1);（] と Put0 の間に空白）で修正の候補が出ない
			- 原因: 空白があると、エラーの名前が「Structure」だけになる（「未知のコマンドまたは存在しない単語です: 'Structure'」）。Structure は正しい名前なので、似た名前の候補がなかった
			- 修正（動作確認OK）: ErrorFix.cs に JoinFollowingWord を追加。エラーの名前の後ろに、キーと空白をはさんで名前と ( が続いていれば、つなげた名前（StructurePut0）で候補を探す → 「Structure.Put0」が出て、選ぶと Structure['CrossSig'].Put0( になる
			- 作業用フォルダで確認: 空白あり Put0(3個) → Structure.Put0 / 空白あり Put(9個) → Structure.Put / キーなし「Structure  Put0(」→ Structure.Put0。前の試験も同じ結果。ビルドの警告 0 件
			- 修正前のファイル: _backup/2026-11-15/ErrorFix.cs.beforeSpaceDot.bak
	- [x] エラーがいくつかある場合はまとめて修正できるか ← 作成済み・動作確認OK (2026-11-15)
		- ← 作成済み・動作確認OK (2026-11-15)
			- ErrorFix.cs: まとめて直せる候補に種類の印（BatchKind）を付けた: ; 抜け / ; の連続 / () 抜け / . 抜け / 全角文字 / 閉じる ' 抜け / 演算子で終わる距離程 / ' ' のない文字列（行の中だけで決まる直し方だけ。似た名前・引数の足し引き・行を足すものは1件ずつ選ぶ）
			- 「修正の候補」の中（エラー一覧の右クリック・エディタの右クリックの両方）に「このファイルの「; 抜け」をまとめて直す（N 件）」を出す（同じファイルの同じ種類が2件以上のとき）
			- ApplyBatchFix: 先に全部の直し方を求めてから、ファイルの後ろから順に入れる（位置がずれないように）。重なるものと直せないものは飛ばして、件数をステータスバーに出す。Ctrl+Z 1回でまとめて元に戻せる
			- 作業用フォルダでエディタのコピーをビルドできることを確認（警告 0 件）。動きは画面で確認が必要
			- 修正前のファイル: _backup/2026-11-15/ErrorFix.cs.beforeBatch.bak（MainWindow.xaml は変更なし）
	- [x] エラー行を右クリックして修正提案 ← 作成済み・動作確認OK (2026-11-15)
		- ← 作成済み・動作確認OK (2026-11-15)
			- MainWindow.xaml: タブ（EditorTabControl）に ContextMenuOpening をつないだ（右クリックメニューを開くときの処理。どのタブのエディタにも効く）
			- ErrorFix.cs: EditorTabControl_ContextMenuOpening を追加。右クリックした行（マウスの位置。取れなければカーソルの行）のエラーを、エラー一覧から波線と同じ絞り込みで探し（GetErrorsOfEditor）、修正の候補をエディタの右クリックメニューの先頭に「修正の候補」として出す。エラーのない行では何も足さない。候補がなければ「修正の候補はありません」（灰色）。各候補にマウスを乗せるとエラーの文が出る
			- 作業用フォルダでエディタのコピーをビルドできることを確認（警告 0 件）。動きは画面で確認が必要
			- 修正前のファイル: _backup/2026-11-15/*.beforeEditorFix.bak（MainWindow.xaml / ErrorFix.cs）
	- [x] includ の時、include に直せるようにする ← まとまり2で作成・動作確認OK (2026-11-15)
	- [x] include 	'Map_RunIndexDefine_E217r.txt; を include 	'Map_RunIndexDefine_E217r.txt';に直せるようにする（'の中は例です） ← まとまり2で作成・動作確認OK (2026-11-15)
	- [x] 'BeginTransition' is required before 'Begin'. のエラーの時の修正提案はどうするか。 ← 作成済み・動作確認OK (2026-11-15)
		- ダイアログを出して挿入位置を決めるか　エラーの距離程の位置から0までの距離程
		- ユーザー回答 (2026-11-15): ダイアログで距離程を選ぶ
		- ← 作成済み・動作確認OK (2026-11-15)
			- ErrorFix.cs: 修正の候補「Curve.BeginTransition(); を足す（距離程を選ぶ…）」。エラーの行から Curve / Gradient / Track['A'].Cant を取り出し（FindTransitionHead）、エラーの行より上の距離程の行を近い順に一覧にしたダイアログ（ChooseDistanceLine）で選ぶと、その距離程の行のすぐ下にエラーの行と同じ字下げで足す（FindDistanceLinesAbove / InsertLineBelow）。キャンセルしたら何もしない
			- 距離程の行の見分け方: 数字・$変数・( ・- ・distance で始まり、= を含まず ; で終わる行（コメント・コマンドの行は除く）
			- ダイアログは新しいファイルを作らず、ErrorFix.cs の中で組み立てた
			- 作業用フォルダで確認: 距離程の行の一覧（distance + 25; / 1500; / 1320 + $WK; / 0;。コメントの # 1500; と変数の代入は入らない）、選んだ行の下に字下げ付きで足されること。エディタのコピーをビルドできる（警告 0 件）。ダイアログの見た目は画面で確認が必要
			- 修正前のファイル: _backup/2026-11-15/ErrorFix.cs.beforeTransition.bak
	- [x] Repeater['RoadR_Road'].Begin0('RoadR, 1 ... 文字列となるところの囲み忘れの修正提案 ← まとまり2で作成・動作確認OK (2026-11-15)
		- [x] ほかの構文（Structure）の引数に文字列があるときにも適用する ← 作成済み・動作確認OK (2026-11-15)（下の「Structure['abc'].Put(xyz, …」の項目に記録）
		- [x] バグがあります。下を見てください ← 下の「Structure['abc'].Put(xyz, …」の項目。作成済み・動作確認OK (2026-11-15)
	- [x] 存在しない単語があるとき、酷似している、近い単語を提案する ← まとまり2で作成・動作確認OK (2026-11-15)（コマンド名・include・名前空間・キーと比べる）
	- [x] バグ Repeater['FenceR'].End() Structure['CrossSig'].Put0('0', 0, 1); のようにEnd();のセミコロンが抜けてる時の修正提案が働かない ← まとまり1で修正・動作確認OK (2026-11-15)
		- [x] .End() の引数には何も指定できません。と表示されるバグ ()の中に何も書かれていないのに誤ったエラー
	- [x] 引数が合わないときは増やすか減らすかを選択する(Repeater構文を除く) ← 作成済み・動作確認OK (2026-11-15)
		- ユーザー回答 (2026-11-15): 足す値は「数値の位置は 0、文字列の位置は ''」。BeginTransition の行は「ダイアログで距離程を選ぶ」（次の作業）
		- ← 作成済み・動作確認OK (2026-11-15)
			- BveMapParserWorker/BveSemanticCommandRules.cs: IsNumericArgument（その位置の引数が数値か）を追加
			- ErrorFix.cs: 引数の数のエラーの候補に「足りない引数 N 個を後ろに足す（'', 0, 0）」「多い引数 N 個を後ろから消す」を追加（AddArguments / RemoveLastArguments / FindArgumentSpans）。Repeater は除く。) が次の行に続くときは候補なし
			- 作業用フォルダで確認: Put(引数7個) → 0, 0 を足して9個 / Put0() → '', 0, 0 / Put0(5個) → 後ろの2個を消す / 引数の中の floor(3, 4) のカンマは数えない / コメントはそのまま。ビルドの警告 0 件
			- 修正前のファイル: _backup/2026-11-15/*.beforeArgAdjust.bak（ErrorFix.cs / Worker_BveSemanticCommandRules.cs）
	- 調査 (2026-11-15)。ユーザー追加の項目（上の 316〜326 行と、下の SpeedLimit の波線・全角文字）を、作業用フォルダのワーカーのコピーで試した
		- いま出るエラー
			- Repeater['FenceR'].End() Structure['CrossSig'].Put0(...); → 「.End() の引数には何も指定できません」だけ（誤り）。1行に2つの文があり ; が抜けているのに、パーサーが1つの文としてつないでしまい、後ろの文が End の引数扱いになっている。; 抜けは出ない
			- includ 'a.txt'; → 「未知のコマンド: 'includ'」。似た名前の候補は . のあるコマンド名だけから探すため include が候補にない
			- include 'Map_….txt; → 「'include' の引数はアポストロフィ等で囲む必要があります」と「; 抜け」（閉じる ' がないので ; まで文字列の中と読まれる）
			- Repeater['RoadR_Road'].Begin0('RoadR, 1, 25, 25, 'x'); → 「Repeater.Begin0 の引数の数は 5〜無制限 個（現在: 1個）」（閉じる ' がないので後ろが全部1つの文字列になる）
			- Section.SetSpeedLimit が2つ → エラーの列・長さがないため、波線が行末まで
			- Curve.Begn（３００）; → 全角文字ごとにエラーが1件ずつ（「（」「３」「０」「０」「）」で5件。「（」「）」は字句解析と全角チェックの両方から出ている）
			- Curvee(300); → 「'Curve' の後にドット」と「未知のコマンド: 'Curvee'」
			- 'BeginTransition' is required before 'Begin' / 'End' → 修正の候補なし
			- Structure.Put の引数は 9 個である必要があります（現在: 7個）→ 修正の候補なし
		- 進め方の案（小さい順に4つのまとまり）
			- まとまり1（不具合・ワーカー側）
				1. 1行に2つの文があって間の ; が抜けているとき（「) の後に名前が続く」）は「文の終わりにセミコロン ';' がありません」を出し、2つの文に分けて読む → .End() の誤ったエラーも消える。修正の候補（; を付ける）も ) の後ろに入れる
				2. SpeedLimit の複数記述のエラーに、コマンド名の位置と長さを付ける（波線をコマンド名だけに）
				3. 全角文字のエラーを、続いている全角文字ごとに1件にまとめる（「（３００）」で1件）。字句解析と全角チェックの重なりもなくす。修正の候補は続いている全角文字をまとめて半角にする
			- [x] まとまり1 ← 1〜3 まとめて作成済み・動作確認OK (2026-11-15)
				- 1. BveMapParserWorker/BveParser.cs: IsNewStatementOnSameLine を追加。コマンドの文が ) で終わりカッコがすべて閉じていて、同じ行に次の名前が続くときは文を区切り、前の文の「; 抜け」を出す（位置は ) ）→ .End() の誤ったエラーが消え、後ろの文も正しくチェックされる
				- 1. ErrorFix.cs: AddSemicolon がエラーの列と長さを使い、) の後ろに ; を入れる（Repeater['FenceR'].End(); Structure[...]）。分からないときは今まで通り行のコードの最後
				- 2. BveMapParserWorker/MapParser.cs: SpeedLimit の複数記述のエラーに、行の中の Section.SetSpeedLimit / Signal.SpeedLimit の位置と長さを入れる（波線がコマンド名だけ）
				- 3. MapParser.cs: MergeFullWidthErrors / FindFullWidthRuns を追加。全角文字のエラーを、行の中で続いている全角文字ごとに1件にまとめ、列と長さを入れる（引用符の中・コメントは除く）。字句解析と全角チェックの重なりも消える。行を読む処理を CreateLineReader にまとめた
				- 3. ErrorFix.cs: 続いている全角文字をまとめて半角にする候補（「（３００）」→「(300)」）。1文字でも半角にできない文字があれば候補なし
				- 作業用フォルダで確認: 試験用マップで、End() の行は「; 抜け」（) の位置）と後ろの文のキーのエラーになり .End() の誤ったエラーは消えた。全角「（３００）」は5件 → 1件。SpeedLimit は列1・長さ21。175 ファイルの比較で変わったのは全角文字の多いファイル1つだけ（32件 → 6件にまとまった）。ほかは完全に同じ。ビルドの警告 0 件
				- 修正前のファイル: _backup/2026-11-15/*.beforeGroup1.bak（Worker_BveParser.cs / Worker_MapParser.cs / ErrorFix.cs）
			- [x] まとまり2 ← 4・5 まとめて作成済み・動作確認OK (2026-11-15)
				- 4. ErrorFix.cs: KnownWords（include と名前空間 Curve / Structure / Track など）を追加。. のない未知の単語は、コマンド名と KnownWords の両方から似た名前を探す（includ → include / Curvee → Curve / Strucure → Structure / CurveBegin → Curve.Begin）
				- 5. ErrorFix.cs: 閉じる ' の足し忘れの候補を追加
					- 「'include' の引数はアポストロフィ等で囲む必要があります」→ CloseUnclosedQuote（閉じていない引用符の後ろで最初の , ) ; の前に ' を足す。include 'a.txt; → include 'a.txt';）
					- 「; 抜け」で、行に閉じていない引用符があるとき → ; ではなく ' を足す候補を出す
					- 引数の数のエラーで、カンマを含む文字列があるとき → CloseQuoteBeforeComma（文字列の中の最初の , の前に ' を足す。Begin0('RoadR, 1, 25, 25, 'x') → Begin0('RoadR', 1, 25, 25, 'x')）
				- 作業用フォルダで確認: 上の例がすべて期待どおり。Structure.Load('a.csv); → Structure.Load('a.csv'); も直る。エディタのコピーをビルドできる（警告 0 件）
				- 修正前のファイル: _backup/2026-11-15/ErrorFix.cs.beforeGroup2.bak
			- まとまり2（修正の候補を増やす・小さい）
				4. 未知の単語の似た名前の候補に、include や名前空間（Curve / Structure など）も入れる（includ → include、Curvee → Curve）
				5. 閉じる ' が抜けている文字列（include 'a.txt; / Begin0('RoadR, 1, ...）→ 閉じる ' を足す候補（; の前、またはカンマの前）
			- まとまり3（修正の候補・考えることが多い）
				6. 引数の数が合わない → 「足りない分を 0 で足す」「多い分を後ろから消す」を選べる（Repeater は除く）
				7. 'BeginTransition' is required before 'Begin' → BeginTransition の行を足す。足す位置（距離程）は、ダイアログでエラーの距離程から 0 までの間を選ぶか、まずは「エラーの行のすぐ上」にするか決める
			- まとまり4（使い方を広げる）
				8. エディタの行を右クリックしても「修正の候補」を出す（その行のエラーの候補）
				9. 同じ種類のエラーをまとめて直す（例: このファイルの ; 抜けをすべて直す）







- [x] 修正提案の方法（エラー一覧から選択して修正提案） ← AIを使わない簡易な方で段階1〜3すべて完了・動作確認OK (2026-11-15)
    - 簡易な方　AIを使用しないので単純で軽いが、予測ができない
    - AIを扱う　予測して修正提案する。コストがかかるため非推奨
	- 調査 (2026-11-15): エラー一覧の右クリックメニューは「行のコピー / ファイルツリーで選択 / エクスプローラーで表示 / 検索・ヘルプ / Geminiに修正を依頼」（MainWindow.xaml）。修正を直接入れる機能はない
		- サンプル174ファイルで多いエラー: キーの検証（リスト未読込）、必須 Load なし、Cant.SetGauge 前のカント、未知のコマンド、距離程の値 など。; 抜けが多いのは他列車ファイル（マップではない）を含むため
	- 案（AIを使わない簡易な方）: 右クリックメニューの先頭に「修正の候補」を出し、選ぶとエディタに直接入れる（元に戻す Ctrl+Z 可・保存はしない）
		- 段階1: 仕組み + 確実に直せるもの（; 抜け → 行末に ; / 全角文字 → 半角に / ; の連続 → 1つに / () 抜け → () を付ける / . 抜け → . を入れる）
			- [x] ← 作成済み・動作確認OK (2026-11-15)
			- ErrorFix.cs（新しいファイル）: エラーのメッセージから候補を作る GetErrorFixSuggestions、メニューを作る FillErrorFixMenu、直しを入れる ApplyErrorFix。行の文字列だけを見て直し方を求める ErrorFix クラス（引用符の中・# や // のコメントは見ない）
			- 直せる内容が行に見つからないとき（行が変わった等）は「自動で直せませんでした」と知らせて何もしない
			- 全角文字は、全角英数・記号と全角スペースだけ半角に直せる（ひらがな等は候補なし）
			- MainWindow.xaml: エラー一覧の右クリックメニューの先頭に「修正の候補」を追加
			- MainWindow.xaml.cs: ダブルクリックのファイルを開く部分を OpenErrorFileAsync に分けた（動きは同じ）。右クリックメニューを開くときに FillErrorFixMenu を呼ぶ
			- 作業用フォルダで確認: 13通りの行で期待どおりの直し方になる。エディタのコピーもビルドできる
			- 修正前のファイル: _backup/2026-11-15/*.beforeErrorFix.bak（MainWindow.xaml / MainWindow.xaml.cs / CLAUDE.md）
		- 段階2: 似た名前の候補（未知のコマンド → 似たコマンド名、リストにないキー → 似たキー。編集距離で近いものを最大3つ）
			- [x] ← 作成済み・動作確認OK (2026-11-15)
			- ErrorFix.cs: 「未知のコマンド」は自動補完の辞書のコマンド名（73個）から、「リストにないキー」（Structure / Sound / Sound3D / Station / Signal、Repeater のストラクチャキー、トレインキー）は構文チェックで受け取ったキーの一覧から、つづりの近いものを最大3つ出す
			- 違ってよい文字数: 名前が 4 文字以下なら 1、10 文字以下なら 2、それより長ければ 3（隣どうしの入れ替えも 1 と数える。大文字小文字は区別しない）
			- コマンド名は . で区切った部分ごとに比べ、違う部分だけ書き換える（Structre['a'].Put → Structure['a'].Put。キーはそのまま）
			- 作業用フォルダで確認: Structre.Put → Structure.Put / Curve.Begn → Curve.Begin / Track.Cant.SetGuage → Track.Cant.SetGauge / Bg_Fnie → Bg_Fine など。似ていない名前（Xyz.Abc）は候補なし。エディタのコピーもビルドできる
			- 修正前のファイル: _backup/2026-11-15/ErrorFix.cs.beforeSimilar.bak
		- 段階3: 行を足すもの（必須 Load なし → Load の行、Cant.SetGauge なし → SetGauge の行 など）
			- [x] ← 作成済み・動作確認OK (2026-11-15)
			- ErrorFix.cs: 必須の X.Load なし → 「X.Load('');」の行を足す（最後の Load の行の下・同じ字下げ。Load がなければヘッダーの次の行。カーソルは '' の間）。すでにあれば直さない
			- ErrorFix.cs: Track['X'].Cant.SetGauge なし / Curve.SetGauge なし → エラーの行の上に同じ字下げで SetGauge の行を足す。軌間は 1.067（狭軌）/ 1.435（標準軌）の2つの候補
			- ファイルの改行の書き方（\r\n / \n）に合わせる
			- ユーザー報告 (2026-11-15): Sound.Load を Station.Load の後に足すとエラーになる → Sound.Load だけは、Station.Load があればその行の上に足すように修正（動作確認OK）。作業用フォルダで確認済み。修正前: _backup/2026-11-15/ErrorFix.cs.beforeSoundOrder.bak
			- MainWindow.xaml.cs（OpenErrorFileAsync）: ファイル名が空のエラー（ルートファイルのもの）は、インクルード先のタブを見ていても親をたどってルートファイルを開く。すでに別のタブで開いていれば「既に開かれています」の確認を出さずに切り替える（ダブルクリックも同じ動きになる）
			- 作業用フォルダで確認: 5通りで期待どおり。エディタのコピーもビルドできる
			- 修正前のファイル: _backup/2026-11-15/*.beforeAddLine.bak（ErrorFix.cs / MainWindow.xaml.cs）
- [ ] ErrorMenuSearchHelp_Clickで不足しているエラー一覧のヘルプhtmlを追加する RELEASE.mdの前に行う
	- 調査 (2026-11-15)
		- Help フォルダにあるのは default.html / Put.html / Structure.html の3つだけで、3つとも中身は同じ（「不明なエラーが発生しました…」）
		- MainWindow.xaml.cs の ErrorMenuSearchHelp_Click は、メッセージの言葉で structure / sound / station / file / Put / SignalLimitSection の HTML を選ぶが、sound・station・file・SignalLimitSection の HTML はない（default.html になる）。また最初の条件に「キー」が入っているため、キーのエラーはすべて structure.html になる
		- ワーカーのエラーの文は約 80 種類。似たものをまとめると 15 くらいの種類になる
		- csproj は HTML を1つずつ「出力フォルダにコピー」と書いているため、新しい HTML を足すたびに書き足す必要がある
	- 作り方の案
		1. エラーの種類ごとの HTML を作る（それぞれ「原因」「直し方」「例（間違い → 正しい）」「修正の候補で直せるか」を書く）
			- semicolon（; 抜け・連続）/ brackets（括弧の不足・対応なし・() 抜け・[] と () の使い分け）/ key（キー: [] 不足・未指定・囲み・リストにない・リスト未読込・Repeater・Train・Station 重複）/ load（Load 系: 必須なし・位置・囲み・重複・リストファイル）/ include / unknown（未知のコマンド・. 抜け・Invalid character・その名前は存在しない）/ arguments（引数の数・数値でない・文字列の囲み・0か1・傾斜）/ fullwidth（全角文字）/ distance（距離程: 0 以上・カンマ・演算子で終わる・Int32 など）/ transition（BeginTransition・SetGauge 前のカント）/ speedlimit / cabilluminance / train（他列車ファイル）/ bveex / internal（パーサー内部のエラー・ファイルが空・大きすぎる）、default は「一般的な調べ方」に書き直す
		2. MainWindow.xaml.cs: HTML を選ぶ部分を、上から順に「メッセージの形 → HTML」を調べる表にする（キーのエラーがすべて structure.html になる問題も直る）
		3. BveMapEditor.csproj: Help\*.html をまとめて出力フォルダにコピーする書き方にする（これから HTML を足しても csproj を直さなくてよい）
		- 既存の Put.html / Structure.html は使わなくなる（中身は default と同じ。削除はユーザーにお願いする）
	- ← 案のとおり作成済み・ユーザーの動作確認待ち (2026-11-15)
		- Help フォルダに 15 個の HTML を追加: semicolon / brackets / key / load / include / unknown / arguments / fullwidth / distance / transition / speedlimit / cabilluminance / train / bveex / internal。default.html は「調べ方」に書き直した（ファイル・行・メッセージの表示は今まで通り）
		- MainWindow.xaml.cs: HelpPageRules（メッセージの形 → HTML の対応表。上から順に調べる）と GetHelpPageName を追加し、ErrorMenuSearchHelp_Click から使う
		- BveMapEditor.csproj: Help\*.html をまとめて出力フォルダにコピーする書き方にした
		- 作業用フォルダで確認: エディタのコピーをビルドして、Help の HTML が出力フォルダにすべてコピーされる（警告 0 件）。サンプル・試験用マップで出るエラーの文（約 50 種類）がすべて意図した HTML になる（「引数」の値に distance がある文、「() 抜け」の例に SetGauge がある文は、順番を直して正しい HTML に）
		- Put.html / Structure.html はもう使われない（削除はユーザーにお願いする。残っていても動きには影響しない）
		- 修正前のファイル: _backup/2026-11-15/*.beforeHelp.bak（MainWindow.xaml.cs / BveMapEditor.csproj / Help_default.html / CLAUDE.md）

## バグ
- [ ] おそらくバグがいくつかあるかもしれない
	- [ ] Claude が確認したバグをここに追記しますー
	- [x] Station.Load が Structure.Load より前にあると、すべての Structure キーが「リストファイルに存在しません」になる（キーの自動補完の調査中に発見 2026-11-15）
		- 原因: BveSemanticAnalyzer.cs で、Station.Load より後の Structure.Load ではキーを読み込まないのに「読み込み済み」にしていた
		- 修正（2026-11-15・動作確認OK）: 他のリストと同じ ProcessListLoadCommand で読み込むようにした
		- 作業用フォルダで確認: 174 ファイルでエラー内容が修正前と一致。Station.Load を先に書いた試験用マップで、正しいキーのエラーが消え、存在しないキーだけがエラーになる
		- 修正前のファイル: _backup/2026-11-15/Worker_BveSemanticAnalyzer.cs.beforeStructLoad.bak
- [x] 開く、保存の文字コードあたりがおかしい気がします ← (1)〜(6) すべて修正・動作確認OK (2026-11-14)
	- [x] (1) 「BOMなしUTF-8」で上書き保存すると、次回から「BOM付き」で保存される ← 修正済み・動作確認OK (2026-11-14)
		- TextFileManager.cs の SaveFile / MenuSave_Click。記憶する名前 "utf-8" から作り直すと BOM付きになってしまう
		- 修正: 記憶する名前を BOMなし "utf-8" / BOM付き "utf-8-bom" に分けた（EncodingToSettingKey / SettingKeyToEncoding を追加）
		- 修正前に記憶された "utf-8" は「BOMなし」として扱う
		- 修正前のファイル: _backup/2026-11-14/TextFileManager.cs.bak
		- [x] 別件: 一度記憶すると確認ダイアログが二度と出ない（詳細設定でクリアするまで）← 修正済み・動作確認OK (2026-11-15)
			- 原因 (2026-11-15): 上書き保存するたびに、ファイルごとの文字コードが自動で記憶されていた
			- 修正（2026-11-15・動作確認OK）: 保存時のダイアログに「このファイルは次回から確認しない」チェックを追加（最初はオフ）。チェックしたときだけ記憶する
				- SaveEncodingDialog.xaml / .cs: チェックボックス ChkRemember と RememberForFile を追加。PromptEncoding が Remember も返す
				- TextFileManager.cs: SaveFile に rememberEncoding を追加。上書き保存での自動記憶をやめた
				- これまで自動で記憶されたものは、詳細設定の「特定ファイルの設定をリセット」で消す
				- 修正前のファイル: _backup/2026-11-15/*.beforeRememberEnc.bak（3ファイル）
	- [x] (2) 文字コードの判定がファイル先頭 4KB だけで行われる ← 修正済み・動作確認OK (2026-11-14)
		- 修正: 4か所すべてでファイル全体を渡して判定するようにした
		- 修正: UTF-8 の確認を Utf8.IsValid に変更（文字列を作らないので大きいファイルでもメモリを余分に使わない）
		- 修正前のファイル: _backup/2026-11-14/*.before2.bak
		- MainWindow.xaml.cs の DetectEncodingFromSample 呼び出し（TextFileManager.cs / ProjectFile.cs / MainWindow.xaml.cs）
		- 先頭が英数字だけの Shift-JIS ファイルを UTF-8 と誤判定 → 後半の日本語が文字化け
		- 4KB の境目で UTF-8 の日本語が切れると Shift-JIS と誤判定 → 全体が文字化け
	- [x] (3) BOM付きファイルを開くと、BOM（見えない文字 U+FEFF）が本文の先頭に残る ← 修正済み・動作確認OK (2026-11-14)
		- 1行目のエラー判定: パイプ通信修正後に確認OK (2026-11-14)
		- 保存のたびに BOM が残る/二重になる原因。1行目のエラー判定にも影響する可能性
		- 修正: 読み込んだ本文の先頭の BOM 文字を取り除く RemoveLeadingBom を追加（MainWindow.xaml.cs）
			- 使用箇所: TextFileManager.cs の OpenFileAsync / ProjectFile.cs の LoadProjectDataAsync / MainWindow.xaml.cs の ReadAllLinesWithAnsiFallbackAsync
		- 修正: BOM付き UTF-8 を「UTF-8 (BOM付)」と判定するようにした（DetectEncodingFromSample）。「元の文字コードを保持」で保存すると BOM付きのまま
		- 修正前のファイル: _backup/2026-11-14/*.before3.bak
	- [x] (4) Shift-JIS で保存すると、Shift-JIS にない文字（一部の記号・絵文字など）が黙って「?」に置き換わる ← 修正済み・動作確認OK (2026-11-14)
		- 例外で UTF-8 に切り替える処理があるが、例外が出ない設定のため動いていない
		- 修正: 保存前に FindUnencodableChar で調べ、見つかったら確認する（はい=UTF-8で保存 / いいえ=そのまま / キャンセル=保存しない）（TextFileManager.cs）
		- 修正: 動いていなかった UTF-8 切り替えの処理を削除
		- 修正: SaveFile が「保存できたか」を返すようにした。タブを閉じる・アプリを閉じる・新規作成のときに保存がキャンセル/失敗したら、閉じない（内容が消えるのを防ぐ）（MainWindow.xaml.cs）
		- 修正前のファイル: _backup/2026-11-14/*.before4.bak
	- [x] (6) 新規作成で「保存する」を選び、名前を付けて保存をキャンセルしても、内容が消えてしまう（MainWindow.xaml.cs の MenuNew_Click → SaveFileAs） ← 修正済み・動作確認OK (2026-11-14)
		- 修正: SaveFileAs が「保存できたか」を返すようにし、キャンセル・失敗なら新規作成しない（TextFileManager.cs）
		- 修正(2回目): ファイルを開いた状態で新規作成すると、そのファイルが閉じられていた（今のタブの中身を消していたため）
			- 新規作成は「無題」のタブを追加するように変更。今のタブはそのまま残るので、保存の確認も不要になった（MainWindow.xaml.cs の MenuNew_Click）
			- 1回目の修正（今のタブを「無題」に戻す・保存の確認）はこの変更で不要になったので置き換えた
		- 修正前のファイル: _backup/2026-11-14/*.before6.bak（1回目の前）、MainWindow.xaml.cs.before6b.bak（2回目の前）
	- [x] (5) 文字コードがタブごとに管理されていない ← 修正済み・動作確認OK (2026-11-14)
		- 修正: タブ切替時にそのタブの文字コードに合わせる（MainWindow.xaml.cs の EditorTabControl_SelectionChanged）
		- 修正: 保存後にタブの情報（パス・文字コード・タブ名）を更新し、ステータスバーも更新（TextFileManager.cs の SaveFile）
		- 修正前のファイル: _backup/2026-11-14/*.before5.bak
		- 上書き保存は「最後に開いたファイルの文字コード」を使う。タブを切り替えても変わらない
		- 保存後、タブの文字コード情報が更新されず、ステータスバー表示が古いまま
	- [ ] ほかにバグがあるか調べる
- [x] 起動時にパイプ通信アプリが落ちるバグ ← 修正・動作確認OK (2026-11-14)
	- [x] 再試行が失敗 
	- 原因（2026-11-14 調査）: エディタとワーカーで「やり取りするデータの形」が食い違っている
		- エディタ側（MainWindow.xaml.cs の ParseViaPipeAsync）が送るもの: RouteFilePath / RouteText / UnsavedTexts（BveSemanticAnalyzer.ParseRequest）
		- ワーカー側（BveMapParserWorker/Program.cs）が読むもの: Text / BaseDirectory（Program.cs 内の古い ParseRequest）→ 本文が空として扱われる
		- ワーカーが返すもの: エラーの「一覧」だけ / エディタが期待するもの: Errors と ParentMap を持つ ParseResponse
		- エディタが返事を読み取れずに例外 → 再起動して再試行 → 同じ理由で失敗 → 「通信に失敗しました」。ワーカーは落ちていないので、プロセスは残る
		- Program.cs だけが新しい形（ParseRequest / ParseResponse）に更新されていなかったのが原因
	- 直し方の案: Program.cs を BveSemanticAnalyzer.ParseRequest で受け取り、ParseResponse で返すように変える（Program.cs だけの変更）
	- ← 修正済み・動作確認OK (2026-11-14)
		- 修正: Program.cs で BveSemanticAnalyzer.ParseRequest を受け取り、RouteFilePath のフォルダを基準に MapParser.Parse を実行、ParseResponse で返す
		- 修正: 「データが大きすぎる」場合の返事も ParseResponse の形にした
		- ParentMap は今は空で返す（インクルード作業で対応）
		- 修正前のファイル: _backup/2026-11-14/Worker_Program.cs.beforePipe.bak
	- [x] 新しく見つけたバグ: 構文チェックの結果が波線に反映されない ← 修正済み・動作確認OK (2026-11-14)
		- MainWindow.xaml.cs の RunSyntaxCheck で、今のタブのエラー（targetErrors）を作っているが、波線の描画（ErrorUnderlineRenderer.SetErrors）に渡していない
		- 修正: RunSyntaxCheck で targetErrors を SetErrors に渡す（MainWindow.xaml.cs）
		- 修正: ErrorUnderlineRenderer.cs の Draw にあった二重の絞り込みを外した（CurrentFileName が未設定のため、インクルード先タブの波線が消える原因になる）
		- 修正前のファイル: _backup/2026-11-14/*.beforeWave.bak
	- 関連して見つけたこと（別作業）
		- Program.cs: パーサー内で想定外のエラーが起きると Environment.FailFast でワーカーごと強制終了する（エディタが再起動するが、エラー内容が失われる）
		- BveSemanticAnalyzer.cs の CheckIncludeSyntaxWithUnsaved（未保存タブ対応・親子対応表を作るインクルード処理）がどこからも呼ばれていない → 「インクルード先のエラーチェックを直す」で対応
		- 脆弱性監査で確認: 20MB 上限チェックは全部読み込んだ後に行われるため効果が薄い / エディタがパイプ相手のプロセスIDを取得しているが、起動したワーカーと一致するか比べていない

- [x] Track[].Gauge();がエラー判定になるバグ ← 修正・動作確認OK (2026-11-15)
	- おそらく辞書に書き忘れた可能性あり
	- 調査結果 (2026-11-14): 予想どおり辞書の書き忘れ。エラーは「未知のコマンドまたは存在しない単語です: 'Track.Gauge'」
		- 正しいコマンドの一覧は BveMapParserWorker/BveSyntaxProvider.cs（自動補完の辞書）から作られる。Track[''].Cant.SetGauge はあるが Track[''].Gauge がない（Curve は Curve.Gauge / Curve.SetGauge の両方がある）
		- 引数の数の辞書 BveSemanticCommandRules.cs にも Track.Gauge がない
		- 関連: BveSemanticAnalyzer.cs のカントのチェック（ValidateTrackCant）は Cant.SetGauge しか「軌間を設定した」と扱わないため、Track[].Gauge で設定しても、その後の Track[].Cant.〜 が「SetGauge が設定される前」と誤ってエラーになる
		- 直し方の案: (1) ワーカーの BveSyntaxProvider.cs に Track[''].Gauge(); を追加 (2) BveSemanticCommandRules.cs に Track.Gauge（引数1つ・数値）を追加 (3) ValidateTrackCant で Gauge も「軌間を設定した」と扱う (4) エディタの BveSyntaxProvider.cs（自動補完）にも追加
		- ← 案のとおり (1)〜(4) 修正済み・動作確認OK (2026-11-15)
			- 修正前のファイル: _backup/2026-11-14/*.beforeGauge.bak（4ファイル）
- [x] タブのファイル名にアンダースコアが表示されない ← 修正済み・動作確認OK (2026-11-14)
	- 原因: WPF のタブは、文字列の「_」をキーボードショートカットの印として扱い、表示から消す
	- 修正: MainWindow.xaml の EditorTabControl に、タブ名を TextBlock で表示するスタイルを追加（コードで追加するタブにも効く）
	- 修正前のファイル: _backup/2026-11-14/MainWindow.xaml.beforeUnderscore.bak
- [ ] まれにパーサーが落ちる。再現が難しいため追加で調査が必要かもしれない。(2026/11/15 15:56 現象未確認)
	- [x] 一部対処済み 
	- 2026-11-14: FailFast をやめたので、パーサー内のエラーなら「パーサー内部でエラーが発生しました（種類）: 内容」がエラー一覧に出るようになった。出たらその文面で調べる
	- 2026-11-15 ユーザー報告: 「マップパーサープロセス（ワーカー）との通信に失敗しました。構文チェックを実行できません。」が出た。詳細は不明
		- これはパーサー内のエラーではなく、エディタ側の通信（MainWindow.xaml.cs の ParseViaPipeAsync）が2回とも失敗したときの表示
		- 詳しい理由は App.LogOperation に書かれるが、操作履歴はメモリ上だけで、アプリが落ちたときしかファイルに出ないため見られない
		- 疑っている原因: ワーカーが前の解析で忙しいと、エディタの接続待ち（1秒）が時間切れになり、エディタがワーカーを強制終了→再起動してしまう（include が多いマップで起きやすい。「解析が遅い」問題とつながっている可能性）
		- 対応1: 失敗したときに、詳しい理由をエラー一覧のメッセージに付ける（原因を確かめるため） ← 修正済み・動作確認OK (2026-11-15)
			- MainWindow.xaml.cs の ParseViaPipeAsync。各試行の失敗理由（例外の種類と内容 / 空の返事）を「（詳細: …）」として付ける
			- 修正前のファイル: _backup/2026-11-14/MainWindow.xaml.cs.beforePipeDetail.bak
			- 結果 (2026-11-15): 「試行1: TimeoutException / 試行2: TimeoutException」→ 接続待ち（1秒）の時間切れが原因と確定
		- 原因の詳しい説明
			- ワーカーは1度に1つの接続しか受け付けない。解析中は次の接続を受け付けられない
			- エディタは入力が止まって 0.7 秒たつと構文チェックを始めるが、前のチェックが終わるのを待たない → 解析に時間がかかるマップだと、前の解析中に次の接続が来て 1 秒で時間切れ
			- 時間切れになると、エディタは解析中のワーカーを強制終了して再起動する → 前のチェックも失敗。再起動直後は準備ができていないので、2回目も時間切れ
			- アプリ起動直後（ワーカーの起動が終わる前）にチェックが走った場合も同じことが起きる
		- 対応2の案（MainWindow.xaml.cs だけ）
			1. エディタからワーカーへの問い合わせを1度に1つにする（前のチェックが終わるまで次は待つ。待っている間に新しい入力があれば古いチェックは取りやめる）
			2. 接続待ちを 1 秒 → 5 秒にする
			3. 時間切れのときはワーカーを強制終了しない。ワーカーのプロセスが本当に終わっているときだけ再起動する
		- ← 対応2 修正済み・動作確認OK (2026-11-15)
			- MainWindow.xaml.cs: _pipeLock（SemaphoreSlim）で問い合わせを1つずつにした。ParseViaPipeAsync に token を追加し、待っている間に取り消されたら何もせず終わる（RunSyntaxCheck で OperationCanceledException を受け止める）
			- 接続待ちを PipeConnectTimeoutMs = 5000 に変更
			- 再起動は IsParserWorkerExited() が true のときだけ
			- 残る制限: ワーカーが固まって（終了せずに）応答しない場合は、自動では再起動しない
			- 修正前のファイル: _backup/2026-11-14/MainWindow.xaml.cs.beforePipeLock.bak

- [x] 	$KTK_13A	=	'AddTrain_Formation\KTK200_1_'								#	小湊鐵道線：下り  
					+ ( Ceil(Rand(1.05)) )+'.txt';	  
					のような書き方にしている場合はエラー(文の終わりにセミコロンがありません)とみなしてしまうバグがあります。
					$KTK_13A	=	'AddTrain_Formation\KTK200_1_'+ ( Ceil(Rand(1.05)) )+'.txt';	と読むようにしてエラーを消す
	- ← 修正済み・動作確認OK (2026-11-15)
		- 原因: パーサー（BveParser.cs の ParseTokens）が行が変わると文を区切っていた。BVE 本体は改行を空白として扱い ; まで1つの文として読む
		- 修正: IsStatementContinuation を追加。前の行の最後が + = , ( [ . * / % - か、次の行の最初が + , ) ] . * / % - なら、区切らずにつなげる。行の比較も「文の最後の記号の行」と比べるようにした
		- 作業用フォルダで試験: 例の書き方のエラーが消え、本当の ; 抜けは引き続き検出。174 ファイルで比べ、変わったのは 5 ファイルの「; 抜け」誤検出 2 件ずつが消えたことだけ（増えたエラーなし）
		- 修正前のファイル: _backup/2026-11-14/Worker_BveParser.cs.beforeMultiline.bak


- [x] エラーと警告の波線が行全体に描画していた　キーのエラー、ファイルパスの問題、単語の間違いなどでも最後まで描画してしまう
    - キーのエラーならキーの部分だけ、ファイルパスのエラーならファイルパスの部分だけなどにする
	- 警告の波線よりエラー波線を優先させる
	- 調査結果 (2026-11-15)
		- エラーの情報は「行・列」だけで「長さ」がない（MapParser.cs の ParseError）。エディタ（ErrorUnderlineRenderer.cs の Draw）は、行ごとに「いちばん左の列から行末まで」1本の波線を引き、その行にエラーが1つでもあれば赤にしている
		- 列の値がチェックによってばらばら（試験用マップで確認）
			- 正しいもの: 「; 抜け」、カントのチェックなど（単語の位置を使っている）
			- 1列目・行の先頭になるもの: 未知のコマンド、リストファイルが見つからない、引数が数値でない など
			- ずれるもの: キーのチェックなど。文を「単語をスペースでつないだ文字列」にしてから位置を数えているため、ファイル上の位置と合わない（例: Sound['x'] のエラーが「ound」から始まる）
		- 直し方の案（2段階）
			- 段階1（エディタ側＋エラーの形）: ParseError に「長さ」Length を追加（0 なら今まで通り行末まで）。波線をエラーごとに引き、警告を先・エラーを後に描いてエラーの赤を上に重ねる。チェック結果は変わらない
			- ← 段階1 修正済み・動作確認OK (2026-11-15)
				- BveMapParserWorker/MapParser.cs: ParseError に Length を追加
				- ErrorUnderlineRenderer.cs の Draw: エラーごとに波線を引く。警告→エラーの順に描く。Length が 0 なら行末まで（今は全部 0 なので範囲は前と同じ）
				- 修正前のファイル: _backup/2026-11-14/*.beforeWaveRange1.bak
				- 動作確認で見つかった問題: エラーが2つ以上あると同じ場所に重ねて描かれ、線が濃くなった
				- ← 追加修正・動作確認OK (2026-11-15): 行ごとに、同じ色の範囲を1本にまとめ（MergeRanges）、警告の範囲からエラーの範囲を除いて（SubtractRanges）から描く
					- 修正前のファイル: _backup/2026-11-14/ErrorUnderlineRenderer.cs.beforeWaveMerge.bak
			- ← 段階2 修正済み・動作確認OK (2026-11-15)
				- BveMapParserWorker/MapParser.cs: AssignErrorRanges を追加。解析の最後に、メッセージから「指している文字」（キー・ファイルパス・コマンド名・引数の値・変数名・名前空間）を取り出し、その行で探して列と長さを入れる。見つからなければ今まで通り行末まで
				- BveMapParserWorker/BveParser.cs: 「; 抜け」のエラーに長さ（文の最後の単語・記号の長さ）を入れる
				- 作業用フォルダで試験: 174 ファイルで「レベル・行・ファイル・メッセージ」は全て同じ（変わったのは列と長さだけ）。エラー 12,456 件のうち 10,781 件に範囲が付いた。波線が指す文字をエラーの種類ごとに確認し、範囲外はなし
				- 修正前のファイル: _backup/2026-11-14/Worker_*.beforeWaveRange2.bak（2ファイル）
			- 段階2（ワーカー側）: エラーに「指している文字」（未知のコマンド名・ファイルパス・キー・引数の値など）を持たせ、最後にファイルの行からその文字を探して、正しい列と長さを入れる。位置がずれる問題もまとめて直る

- [x] 開くときの余計な警告ダイアログを直す
	- 調査 (2026-11-15): メニューの「ファイルを開く」（TextFileManager.cs の MenuOpen_Click）で、ファイルを選ぶ前に出る確認が2つある
		- 「エラー残存の確認」: エラー一覧に1件でもあれば出る。警告（必須の Station.Load がない等）も数えるので、ほぼ毎回出る。実際は新しいタブで開くので、何も破棄されない
		- 「未保存の変更の確認」: isModified を見ているが、タブがあると isModified は更新されないため、ほとんど出ない（出ても内容と合っていない）
		- 本当に内容が消える場合: 今のタブが「無題」のときは、新しいタブを作らずに今のタブへ読み込むため、無題タブに書いた内容が消える
	- 直し方の案（TextFileManager.cs だけ）
		1. 「エラー残存の確認」と「未保存の変更の確認」をやめる
		2. 今のタブが「無題」で何も書かれていないときだけ今のタブを使い、それ以外は新しいタブで開く（無題タブの内容が消えないようにする）
		- 残す確認: Map ファイルではない（ヘッダーがない）・すでに開いている・30MB を超える
	- ← 案のとおり修正済み・動作確認OK (2026-11-15)
		- MenuOpen_Click: 2つの確認を削除
		- OpenFileAsync: 今のタブが「無題・未変更・空」のときだけ今のタブに読み込み、それ以外は新しいタブで開く
		- 修正前のファイル: _backup/2026-11-14/TextFileManager.cs.beforeOpenDialog.bak
- [x] 閉じ括弧が不足しているときに波線が消えるバグ
- [x] Track[''].Cant.SetGauge; のように()が抜けてるのにエラー検知しない
- [x] Track.Cant.SetGauge(1);のように、[]が抜けてるのにエラー検知しない
	- 調査結果 (2026-11-15)。作業用フォルダの計測用プログラムで再現
		- [] 抜けの原因（いちばん大きい）: パーサー（BveParser.cs の BuildStatementNode）が文を「単語をスペース1つでつないだ文字列」（例: Track . Cant . SetGauge ( 1 ) ;）にしてから各チェックに渡している。「Track.」のように続けて書かれていることを前提にした正規表現が一致せず、[] 抜け・. 抜け・Load のファイル確認などのチェックが動いていなかった
			- 同じ理由で、エラーの列（位置）もずれていた（文の先頭の位置を足していない・空白が増えている）
		- 波線が消える原因: 「閉じ括弧 ')' が不足」のエラーの列が行の長さより後ろ（例: 25 文字の行で 35 列目）になり、描く場所がなかった
		- () 抜けの原因: () のないコマンドを調べるチェックがない
	- 直し方の案（作業用フォルダで試作・試験済み）
		- A. BveParser.cs: 文を「元のファイルどおりの空白」で組み立て、先頭に元の位置ぶんの空白を入れる（文字列の位置 = 行の中の位置）→ [] 抜けを検出。止まっていたチェックも動き、列も正しくなる
		- B. ErrorUnderlineRenderer.cs: エラーの列が行末より後ろのときは、行の最後の1文字に波線を引く → 閉じ括弧不足の波線が出る
		- C. BveParser.cs: 「名前空間[キー].メソッド」の形なのに () がない文をエラーにする（「コマンドの後に丸括弧 '()' がありません」）
		- 174 ファイルでの比較（今のプロジェクト ↔ A+C）: 変わったのは 6 ファイルだけ
			- 3793F_P.txt / 3793F_P3.txt / 3793F_P_wet.txt: 「指定されたファイルが見つかりません: '..\Structures.csv'」が 1 件増えた（実際にそのファイルは Maps の 1 つ上にない。今まで Load のファイル確認が動いていなかった）
			- map.txt: 同じく Load のファイルが見つからないエラーが 5 件増えた（実際にない）
			- AddTrain_KTK_0/1.txt: エラーの件数は同じで、メッセージ内のキーの表示から余分な空白が消えた
			- C のエラーはサンプルでは 0 件（誤検出なし）
	- ← A・B・C まとめて反映済み・動作確認OK (2026-11-15)
		- BveParser.cs: RebuildStatementText（A）と MissingParenCommandRegex のチェック（C）を追加（試験したファイルをそのまま反映）
		- ErrorUnderlineRenderer.cs の GetRange: 列が行末より後ろなら行の最後の1文字に波線（B）
		- 反映後のソースを作業用フォルダでビルドし、3 つのバグが検出されること・3793F_P.txt の結果が試験版と同じことを確認
		- 修正前のファイル: _backup/2026-11-14/*.beforeABC.bak（2ファイル）

- [x] エディタ側のCPU使用率が入力などをしていない状態で8%前後使われている 自環境はi7-12700なので影響はないが、ノートパソコンなどの省電力モデルでは高負荷になる可能性がある
	- ワーカー側は解析中に10%前後使うが、アイドル状態では0%だった。
	- 原因（2026-11-15）: ErrorUnderlineRenderer.cs で、画面の配置が更新されるたび（LayoutUpdated）に、エラーがあれば描き直し（InvalidateVisual）を指示していた。描き直すと配置がまた更新されるため、描き直しを繰り返していた
	- ← 修正済み・動作確認OK (2026-11-15): この処理を削除（波線は SetErrors で描き直しを指示済み、スクロール等はエディタが自動で描き直す）
		- 修正前のファイル: _backup/2026-11-14/ErrorUnderlineRenderer.cs.beforeCpu.bak
- [x] 水平スクロールバーが垂直スクロールするとガタガタしてしまう
	- 原因の見込み (2026-11-15): エディタ（AvalonEdit）は「画面に見えている行」だけで横幅を計算する。水平スクロールバーの表示が「自動」（Auto）のため、上下にスクロールして見えている行の長さが変わるたびに、スクロールバーが出たり消えたりし、そのたびに表示部分の高さが変わってガタガタする
	- ← 修正済み・動作確認OK (2026-11-15): MainWindow.xaml の EditorTabControl に、エディタの水平スクロールバーを常に表示するスタイルを追加（コードで作るタブのエディタにも効く）。「右端で折り返す」が有効なときは表示しない
		- 残る動き: つまみの大きさは、見えている行の長さに合わせて変わる（エディタ部品の仕組み）
		- 修正前のファイル: _backup/2026-11-14/MainWindow.xaml.beforeHScroll.bak

- [x] 書き換えてからすべて元に戻した状態でも未保存の警告ダイアログが出てしまう (Windowsメモ帳などでは戻したら警告ダイアログなしで閉じられます)
	- 原因 (2026-11-15): AvalonEditClass.cs の MapTextEditor_TextChanged が、一度でも書き換えたら「変更あり」にするだけで、元に戻したかを見ていなかった
	- ← 1回目の修正 (2026-11-15)。問題があったため下の2回目の修正で置き換え・動作確認OK
		- AvalonEditClass.cs: 「変更あり」を、エディタの元に戻す記録（UndoStack.IsOriginalFile = 保存・読み込み時点の内容と同じか）で決める。戻ったら「変更なし」に戻し、タイトルの * も消える
		- TextFileManager.cs の SaveFile: 保存した時点を「元の内容」として覚える（UndoStack.MarkAsOriginalFile）
		- 修正前のファイル: _backup/2026-11-14/*.beforeUndoModified.bak（2ファイル）
		- [x] 無題で a などを入れた時は * がつかず、元に戻すとなぜか * がついてしまう
			- 原因 (2026-11-15): エディタの「元に戻す」の記録は、続けて入力した文字をまとめておき、後で（カーソル移動や元に戻すのとき）確定する。そのため入力した直後は「元の内容のまま」と判定され、元に戻すと逆に「変更あり」と判定されていた
			- ← 2回目の修正・動作確認OK (2026-11-15): 「元に戻す」の記録を使うのをやめ、「変更なし」にした時点（開いた・保存した・新規作成した時点）の本文を TabDocumentInfo.OriginalText に覚え、書き換えるたびに比べる（文字数が違えばすぐ「変更あり」、同じときだけ全体を比べる）
				- MainWindow.xaml.cs: TabDocumentInfo の IsModified を false にしたときに本文を覚える
				- AvalonEditClass.cs: MapTextEditor_TextChanged で本文と比べる
				- TextFileManager.cs: 1回目に入れた MarkAsOriginalFile を削除
				- 修正前のファイル: _backup/2026-11-14/*.beforeUndoModified2.bak（3ファイル）

- [x] Ctrl + マウスホイール で拡大と縮小する際にマウスカーソル中心/ 現在行ではなく一番上から拡大縮小してしまう
	- 原因: 文字の大きさを変えたあと、表示位置を合わせ直していなかった（スクロール位置が一番上基準のまま）
	- 修正（2026-11-15・動作確認OK）: SettingsMenu.cs の MapTextEditor_PreviewMouseWheel
		- 変更前に「マウスの下の行」と「その行の上端からマウスまでの距離」を覚えておく
		- 変更後にその行が同じ高さに来るように縦スクロールし直す。横スクロールも文字の大きさの比率で合わせる（折り返し表示のときは縦だけ）
		- 修正前のファイル: _backup/2026-11-15/SettingsMenu.cs.beforeZoom.bak
- [x] 検索ウィンドウを閉じるとメインウィンドウが別のアプリケーションの後ろに回ってしまう ← 修正済み・動作確認OK (2026-11-15)
	- 調査 (2026-11-15): 検索ウィンドウはメインウィンドウを「持ち主」（Owner）にして開いているが、閉じるときにメインウィンドウを前に出していない。WPF では、持ち主のある小さなウィンドウを閉じると、Windows が別のアプリを前に出してしまうことがある（よく知られた動き）
	- 直し方の案: SearchWindow.xaml.cs で、閉じる直前（OnClosing）に持ち主のメインウィンドウを前に出す（Owner.Activate）
	- ← 案のとおり修正済み・動作確認OK (2026-11-15)。作業用フォルダでエディタのコピーをビルドできることを確認（警告 0 件）。修正前のファイル: _backup/2026-11-15/SearchWindow.xaml.cs.beforeSearchTab.bak
- [x] 検索ウィンドウを開いた状態でタブを切り替えると正常に動作しない ← 修正済み・動作確認OK (2026-11-15)
	- 調査 (2026-11-15): 検索ウィンドウは、開いたとき（または Ctrl+F を押したとき）のタブのエディタを覚えて、そこだけを検索・置換している。タブを切り替えても覚えているエディタは変わらないため、見えていない前のタブを検索・置換してしまう（前のタブを閉じた後は、閉じたタブの中を検索する）
	- 直し方の案: MainWindow.xaml.cs のタブ切り替え（EditorTabControl_SelectionChanged）で、検索ウィンドウが開いていれば、検索するエディタを新しいタブに差し替える（SearchWindow.xaml.cs に差し替え用の SetEditor を追加。検索欄の入力や選択はそのまま）
	- ← 案のとおり修正済み・動作確認OK (2026-11-15)。ReuseFor（Ctrl+F で前に出すとき）も SetEditor を使うようにした。作業用フォルダでエディタのコピーをビルドできることを確認（警告 0 件）。修正前のファイル: _backup/2026-11-15/*.beforeSearchTab.bak（SearchWindow.xaml.cs / MainWindow.xaml.cs）

- [x] Structure['CrossSig'] Put(''... の時に未知のコマンドまたは存在しない単語です: 'Structure' 修正提案なし ← 修正済み・動作確認OK (2026-11-15)。修正提案の「ユーザー報告: ] と Put0 の間に空白」と同じ（JoinFollowingWord）
- [x] Section.SetSpeedLimit または Signal.SpeedLimit が複数記述されています。 波線が行の最後まで引いてしまう ← まとまり1で修正・動作確認OK (2026-11-15)
- [x] 全角文字を1つずつ分けてエラー一覧に生成してしまう ← まとまり1で修正・動作確認OK (2026-11-15)
- [x] Structure['abc'].Put(xyz, 0, ... のように文字列に''を忘れた時にエラーにならないバグ ← 修正済み・動作確認OK (2026-11-15)
	- 調査 (2026-11-15): 引数のチェック（BveSemanticAnalyzer.cs の CheckCommandArguments）は、数値の引数だけを調べていて、文字列の引数（軌道名・ストラクチャーキーなど）は何も調べていなかった
	- 修正（動作確認OK）:
		- BveMapParserWorker/BveSemanticAnalyzer.cs: 数値ではない引数に、' ' で囲まれていない名前だけ（xyz など）が書かれていたら「○○ の第 N 引数は文字列のため ' ' で囲む必要があります（値: 'xyz'）。」を出す。distance と null は除く。波線は xyz だけ
		- ErrorFix.cs: 修正の候補「「xyz」を 'xyz' にする」（WrapWithQuotes）
		- 上の「ほかの構文（Structure）の引数に文字列があるときにも適用する」もあわせて対応: 引数の数のエラーで、閉じる ' が1つもないとき（Structure['a'].Put('0, 0, ...);）は、閉じていない ' を最初の , の前で閉じる候補を出す（Put('0', 0, ...)）
		- 作業用フォルダで確認: Structure.Put(xyz, …) / Repeater.Begin0(abc, …) がエラーになり、候補で 'xyz' / 'abc' に直る。Put('0, 0, …) / Put0('0, 1, 25) は Put('0', 0, …) / Put0('0', 1, 25) に直る。distance は今まで通りエラーにならない。175 ファイルの比較で結果は完全に同じ（誤検出なし）。ビルドの警告 0 件
		- 修正前のファイル: _backup/2026-11-15/*.beforeBareString.bak（Worker_BveSemanticAnalyzer.cs / ErrorFix.cs）
		- ユーザー指摘 (2026-11-15): distance は距離程の位置（distance + 100; など）以外ではただの文字列として扱われる
			- 修正（動作確認OK）: BveSemanticAnalyzer.cs で distance を除くのをやめ、ほかの名前と同じく「' ' で囲む必要があります」にする（null だけは除く）
			- 作業用フォルダで確認: Structure['abc'].Put0(distance, 1, 25); がエラーになり、波線は distance だけ。175 ファイルの比較で結果は完全に同じ

- [x] distance + （演算子）のような書き方でもエラーを検知しないバグ ← 修正済み・動作確認OK (2026-11-15)
	- 原因 (2026-11-15): 距離程の式の計算（BveSemanticAnalyzer.cs の CheckDistanceValue）が失敗しても、何もエラーを出していなかった
	- 修正（動作確認OK）: 式が演算子（+ - * / %）で終わっていたら「距離程の式が演算子 '+' で終わっています（式: 'distance +'）。…」を出す。波線は式だけ（MapParser.cs に「（式: '…'）」の位置探しを追加）。修正の候補「最後の演算子 + を消す」（ErrorFix.cs の RemoveTrailingOperator）
	- 作業用フォルダで確認: distance +; / distance + ; / 100 +; / distance + 100 +; / distance *; / 50 -; がエラーになり、候補で distance; などに直る。175 ファイルの比較で結果は完全に同じ
	- 残る制限: 「distance +」の次の行にコマンドが続くと、2行が1つの文としてつながり（+ で終わる行は次の行に続くと読むため）、エラーにならない
	- 修正前のファイル: _backup/2026-11-15/*.beforeDangling.bak（Worker_BveSemanticAnalyzer.cs / Worker_MapParser.cs / ErrorFix.cs）
- [x] エラー内容と一致しない 1文字ずつ分けてるのを直ってない ← 修正済み・動作確認OK (2026-11-15)
	- 原因 (2026-11-15、Editor01.png): include 'Map_RunIndexDefine_E217r.txt;    # Run インデックス… のように閉じる ' がないと、後ろのコメントまで文字列の中と読まれ、コメントの日本語が1文字ずつ「全角文字」のエラーになっていた。まとめる処理（MergeFullWidthErrors）は、文字列の中なのでまとめる相手が見つからず、1文字ずつのまま残していた
	- 修正（動作確認OK）: MapParser.cs の MergeFullWidthErrors で、行は読めたのに文字列・コメントの外に全角文字がないときは、そのエラーを出さない（本当の原因の「閉じる ' 抜け」は別のエラーで出て、修正の候補で直せる）
	- 作業用フォルダで確認: 例の行で全角文字のエラーが 8 件 → 0 件。「'include' の引数はアポストロフィ等で囲む必要があります」と「; 抜け」は出る。175 ファイルの比較で結果は完全に同じ
![Editor01](Editor01.png)

- [x] Structure['abc'].Put('', 1, 25);のように引数を見てput0にしなかった場合のエラーに修正提案する その逆でも同様のようにする ← 作成済み・動作確認OK (2026-11-15)
	- 修正（動作確認OK, 2026-11-15）: ErrorFix.cs の引数の数のエラーの候補に、「兄弟のコマンド」（同じ名前空間で、最後の数字を除くと同じ名前: Put と Put0、Begin と Begin0）のうち、( ) の中の引数の数が合うものに変える候補を追加（FindSiblingCommands）。例: 「Put を Put0 にする（引数 3 個に合う）」
	- 作業用フォルダで確認: Structure['abc'].Put('', 1, 25); → Put0 に / Structure['abc'].Put0(引数9個) → Put に。兄弟のない Curve.Begin は候補なし。ビルドの警告 0 件
	- 修正前のファイル: _backup/2026-11-15/ErrorFix.cs.beforeSwapPut.bak







## パフォーマンスの問題
- [x] includeを多用しているマップファイルで解析パフォーマンスが低下する
	- サンプルシナリオ(内房線製作チーム スタッフが制作した内房線のシナリオです)としてMapsフォルダを用意、Load系が含まれるメインファイルは3793F_P.txt
	- testフォルダは無視してください。include先にこのフォルダを使用していません
	- 調査結果 (2026-11-15)
		- 3793F_P.txt は約 40 個のファイルを include。Maps（test 以外）は .txt が 173 個・約 3 万 4 千行・距離程らしい行が約 6 千 4 百行。量としては多くないので、1行あたりの処理が重いと考えられる
		- いちばん怪しいもの: 計算式の評価（DynamicExpresso の Interpreter.Eval）を、ただの数字にも毎回使っている（BveSemanticAnalyzer.cs）
			- Eval は式を読み取って「プログラムを組み立てる」ため 1 回が重い
			- 距離程 1 行ごとに EvaluateDistance と CheckDistanceValue で 2 回 Eval。そのたびに ConfigureInterpreter（関数12個の登録）と SetInterpreterVariables（全変数の登録）もやり直す
			- 数値の引数でも CheckInt32Range / IsValidNumericOrExpression で Eval（IsValidNumericOrExpression は毎回 Interpreter を新しく作る）
		- そのほか: CheckUnknownCommand などで、毎回作り直す正規表現が多い（影響は小さいと思われる）
	- 直し方の案（小さい順。1つずつ確認しながら）
		1. ただの数字（例: 1234.5）のときは Eval を使わず、そのまま数値にする（EvaluateDistance / CheckDistanceValue / CheckInt32Range）。いちばん効果が大きい見込み ← 修正済み・動作確認OK (2026-11-15)
			- TryParsePlainNumber を追加し、EvaluateDistance と CheckDistanceValue の先頭で使う（CheckInt32Range と IsValidNumericOrExpression は元から数字なら Eval しない）
			- 数字でない式（変数・計算式・カンマ入り）は今まで通り Eval。チェック結果は変わらない
			- 修正前のファイル: _backup/2026-11-14/Worker_BveSemanticAnalyzer.cs.beforePerf1.bak
			- 結果: 3793F_P.txt で約 7 秒。あまり効かなかった
			- 理由: このマップの距離程はほとんど「1320 + $WK02;」のような変数入りの式（約 6,150 行）で、ただの数字は約 20 行だけだった
		1b. 「数字や変数を + / - でつないだだけの式」も Eval を使わずに計算する ← 修正済み・動作確認OK (2026-11-15)。約 7 秒 → 約 3 秒
			- TryEvaluateSimpleSum を追加し、EvaluateDistance と CheckDistanceValue で使う（Int32 の範囲チェックも同じ内容で行う）
			- 掛け算・割り算・関数・カッコ・未定義の変数・文字列の変数は今まで通り Eval。チェック結果は変わらない
			- 式の読み取りは PowerShell で確認済み（1320 + $WK02 / $KM - 15.5 / 100 + $A + $B は対象、1320 * $WK02 / floor($A) は対象外）
			- 修正前のファイル: _backup/2026-11-14/Worker_BveSemanticAnalyzer.cs.beforePerf2.bak
		1c. 次の候補（残り約 3 秒の原因）: 正規表現の作り直し
			- CheckNamespaceDot（19 種類 × 最大 3 個）、CheckKeyRequiredBrackets、CheckRequiredArguments などが、コマンド 1 行ごとに、名前を埋め込んだ正規表現を数十〜百個使っている。コマンドは約 1 万 2 千行
			- .NET は使った正規表現を 15 個までしか覚えておかない（Regex.CacheSize の初期値）。種類がそれより多いので、毎回「正規表現の組み立て」からやり直している
			- 直し方: ワーカー起動時に Regex.CacheSize を大きく（例: 500）する。1 行の追加（BveMapParserWorker/Program.cs）。チェック結果は変わらない
			- ← 修正済み・動作確認OK (2026-11-15)。Main の先頭（--worker の確認の後）で Regex.CacheSize = 500。約 3 秒 → 約 1.5 秒
			- 修正前のファイル: _backup/2026-11-14/Worker_Program.cs.beforeRegexCache.bak
	- 計測 (2026-11-15)。作業用フォルダ（プロジェクト外）でワーカーのソースのコピーを使い、計測用プログラムで測った（プロジェクトのファイルは変更していない）
		- 3793F_P.txt の解析 1 回: 約 0.8 秒（エディタで見える 1.5 秒は、入力後の待ち 0.7 秒を含む）
		- include の量を増やした試験（本文を繰り返して作成）: 1倍 0.5 秒 / 2倍 0.76 秒 / 4倍 1.37 秒 / 8倍 2.48 秒 → 量にほぼ比例。急に遅くなる処理はない
		- 最適化ビルド（Release・Optimize=true）でもほとんど変わらない
		- 時間のかかっている処理（4倍のとき、計測の上乗せ込み）:
			- 文の組み立て BuildStatementNode 225 ms / 字句解析 Tokenize 126 ms（毎回作る正規表現を使っている）
			- CheckRequiredArguments 201 ms（呼ばれるたびに辞書を作り直し、キーごとに正規表現を作る）
			- 変数の代入 TryHandleVariableAssignmentAst 178 ms（Eval を使う）
			- CheckCommandArguments 114 / CheckUnknownCommand 110 / CheckKeyRequiredBrackets 104 / CheckNamespaceDot 104 ms（名前を埋め込んだ正規表現を毎回使う）
		- 「Distance + 25」のような、組み込み変数 distance を使う距離程が Eval に回っていた（1 倍で 372 行 × 3 回）
	- 次の直し方（どれもチェック結果は変えない。1 つずつ）
		- [x] P1. 足し算・引き算の高速計算に distance（直前の距離程）を追加 ← 作業用フォルダで試験済み: 約 0.8 秒 → 約 0.5 秒、エラー内容は同じ
			- ← プロジェクトに反映済み・動作確認OK (2026-11-15)。反映後のソースを作業用フォルダでビルドして、エラー内容が同じ・平均約 0.48 秒を確認
			- EvaluateDistance / CheckDistanceValue / TryEvaluateSimpleSum に distanceVar（省略可）を追加。距離程の 3 か所で「直前の距離程」を渡す
			- 修正前のファイル: _backup/2026-11-14/Worker_BveSemanticAnalyzer.cs.beforeP1.bak
		- 作業用フォルダでの試験結果 (2026-11-15)。4倍のマップの中央値: P1 後 1048 ms → P2 後 約 930 → P3 後 802 → P4 後 733 → P5 後 697 ms（約 33% 短縮）。元の 3793F_P.txt: 360 → 279 ms（約 22% 短縮）
			- エラー内容の比較: Maps の .txt 173 個と map.txt をそれぞれルートとして解析し、P1 後と P2〜P5 後で全 174 ファイル・エラー 12,458 行が完全に一致
			- 注意: 計測用プログラムが 0 回計測のときに例外で落ち、Windows のデバッガー画面が出た → 計測用プログラムを修正済み（プロジェクトには関係なし）
		- P2〜P5 はまとめてプロジェクトに反映済み・動作確認OK (2026-11-15)。試験したファイルをそのまま反映（BveSemanticAnalyzer.cs / BveParser.cs）
			- 修正前のファイル: _backup/2026-11-14/Worker_BveSemanticAnalyzer.cs.beforeP2-5.bak / Worker_BveParser.cs.beforeP2-5.bak
		- [x] P2. CheckRequiredArguments の辞書と正規表現を最初に1回だけ作る
		- [x] P3. CheckNamespaceDot / CheckKeyRequiredBrackets の正規表現を最初に1回だけ作り、名前が含まれない行は正規表現を使わずに飛ばす
		- [x] P4. BuildStatementNode / CheckUnknownCommand の正規表現を最初に1回だけ作る
		- [x] P5. 変数の代入でも、足し算・引き算だけの式は Eval を使わない
		- 将来の案（大きい変更）: 変更のないインクルード先ファイルは、前回の字句解析・構文解析の結果を使い回す
		2. CheckDistanceValue は EvaluateDistance で計算済みの値を使い、2 回目の Eval をやめる
		3. 共通の Interpreter には関数を最初に1回だけ登録し、毎回の ConfigureInterpreter をやめる
	- 注意: 時間を測るにはビルドが必要。まずは体感で確認し、必要なら時間を測る仕組みを追加する







