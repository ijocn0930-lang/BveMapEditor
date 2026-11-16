# システム関連拡張子一覧

OS（Windows / macOS / Linux・Unix系）のシステム動作、設定、実行、ドライバ、ディスク管理などに直接関わる**主要なシステム関連拡張子**を用途別に網羅した一覧です。

---

## 1. 実行ファイル・プログラム本体
プログラムの起動や処理の実行そのものを担うファイルです。

* **.exe** (Executable) Windowsの標準的な実行可能ファイル。
* **.com** (Command) MS-DOS時代から使われる小規模な実行ファイル。
* **.msi** (Microsoft Installer) Windowsインストーラー用のパッケージファイル。
* **.msix / .appx** Windows 10/11以降のモダンアプリ用パッケージ。
* **.app** macOSのアプリケーション本体（実体はディレクトリ構造のバンドル）。
* **.dmg** (Apple Disk Image) macOSでアプリ配布などに使われるディスクイメージ。
* **.pkg** macOSやUnix系OSで使われるインストーラーパッケージ。
* **.deb** Debian / Ubuntu系Linuxのインストールパッケージ。
* **.rpm** Red Hat / CentOS / Fedora系Linuxのインストールパッケージ。
* **.bin** (Binary) バイナリ形式の実行ファイルやファームウェアデータ。
* **.elf** (Executable and Linkable Format) LinuxなどのUnix系OSにおける標準実行ファイル形式。
* **.out** Unix系OSのコンパイラが出力するデフォルトの実行ファイル（a.outなど）。
* **.scr** (Screen Saver) Windowsのスクリーンセーバー（実体は.exeと同じ実行形式）。
* **.pif** (Program Information File) MS-DOSプログラムの実行環境設定ファイル（Windowsでは実行ファイル扱い）。
* **.gadget** Windowsデスクトップガジェットファイル。

---

## 2. システムライブラリ・ドライバ・カーネル関連
OSの根幹機能、ハードウェア制御、プログラム間の共通処理を提供するファイルです。

* **.dll** (Dynamic Link Library) Windowsで複数のプログラムから共有される機能ライブラリ。
* **.sys** (System) Windowsのデバイスドライバやカーネルモードの重要システムファイル。
* **.drv** (Driver) ハードウェアを制御するデバイスドライバファイル。
* **.vxd** (Virtual Device Driver) 旧世代Windows（9x系）の仮想デバイスドライバ。
* **.ocx** (OLE Control Extension) ActiveXコントロール用のライブラリファイル。
* **.cpl** (Control Panel) Windowsのコントロールパネルの各項目を構成するファイル。
* **.msc** (Microsoft Management Console) デバイスマネージャーやディスク管理などの管理ツール用ファイル。
* **.so** (Shared Object) Linux / Unix系OSにおける共有ライブラリ（Windowsの.dllに相当）。
* **.dylib** (Dynamic Library) macOSにおける動的共有ライブラリ。
* **.ko** (Kernel Object) Linuxのカーネルモジュール（ドライバなど）ファイル。
* **.kext** (Kernel Extension) macOSのカーネル機能拡張ファイル。
* **.efi** (Extensible Firmware Interface) UEFIブートローダーやファームウェア関連の実行ファイル。
* **.rom** チップセットやBIOS/UEFIなどの読み取り専用メモリイメージ。

---

## 3. スクリプト・バッチ・コマンド実行
テキスト形式で記述され、OSのシェルやインタープリタによって順次実行されるファイルです。

* **.bat** (Batch) Windowsコマンドプロンプト用のバッチファイル。
* **.cmd** Windows NT系コマンドスクリプトファイル。
* **.ps1** (PowerShell) Windows PowerShell用のスクリプトファイル。
* **.psm1 / .psd1** PowerShellのモジュールファイル（.psm1）とマニフェストファイル（.psd1）。
* **.vbs** (VBScript) Windows Script Hostで実行されるスクリプト。
* **.vbe** 暗号化されたVBScriptファイル。
* **.js / .jse** Windows Script HostやNode.js等で実行されるJavaScriptファイル（.jseは暗号化版）。
* **.wsf** (Windows Script File) 複数のスクリプト言語を混在できるXML形式の実行ファイル。
* **.wsh** Windows Script Hostの実行設定ファイル。
* **.sh** (Shell Script) Linux / macOSのsh / bashなどで実行されるシェルスクリプト。
* **.bash / .zsh / .csh / .fish** 各特定のシェル環境向けのコマンドスクリプト。
* **.command** macOSでダブルクリックからターミナル経由で実行できるシェルスクリプト。
* **.scpt / .applescript** macOSの自動操作（AppleScript）ファイル。
* **.awk / .sed** Unix系テキスト処理コマンド用のスクリプトファイル。

---

## 4. システム設定・レジストリ・構成ファイル
OSやサービスの挙動、環境設定、認証情報などを定義するファイルです。

* **.ini** (Initialization) アプリケーションやWindowsの初期設定を記述するテキストファイル。
* **.cfg / .conf** (Configuration) OS、サーバー、各種ソフトウェアの汎用設定ファイル。
* **.config** .NET Frameworkや各種システムで使われるXMLベースの設定ファイル。
* **.inf** (Information) Windowsのドライバインストール手順や構成情報を記述したテキストファイル。
* **.reg** (Registry) Windowsレジストリのキーや値をインポート・エクスポートするファイル。
* **.pol** (Policy) Windowsのグループポリシー設定ファイル。
* **.adm / .admx / .adml** グループポリシーの管理用テンプレートファイル。
* **.plist** (Property List) macOS / iOSでシステムやアプリの設定を保存するXML/バイナリファイル。
* **.env** 環境変数を定義する設定ファイル。
* **.yaml / .yml** システム構成管理（Docker, Kubernetes, Ansible等）で多用される設定ファイル。
* **.json** システム設定やAPI通信、マニフェスト等で広く使われるデータ形式。
* **.toml** 設定ファイル向けに設計された可読性の高い構成ファイル。
* **.xml** 各種システム構成やマニフェスト定義に使われるマークアップファイル。
* **.properties** Java系システムやサーバーで用いられるキー・バリュー形式の設定ファイル。
* **.service / .socket / .timer** Linuxのsystemdでサービスやプロセスを管理するユニットファイル。
* **.htaccess** Apache Webサーバーのディレクトリ単位の設定ファイル。

---

## 5. システム管理・ログ・一時・仮想メモリ・ダンプファイル
OSの稼働記録、メモリ退避、エラー解析、作業領域としてシステムが自動生成・管理するファイルです。

* **.log** OSやアプリケーションの動作履歴・エラー記録ファイル。
* **.evtx / .evt** Windowsイベントビューアーのシステムログファイル（.evtxは現行形式）。
* **.etl** (Event Trace Log) Windowsのカーネルやドライバの詳細な診断トレースログ。
* **.dmp / .mdmp** (Dump / Minidump) システムクラッシュ（ブルースクリーン等）時のメモリダンプファイル。
* **.core** Unix系OSでプロセス異常終了時に出力されるコアダンプファイル。
* **.tmp / .temp** システムやアプリが処理中に一時的に作成する作業用ファイル。
* **.bak** (Backup) システムファイルや設定変更時に自動生成されるバックアップファイル。
* **.old** Windowsアップデート後などに旧バージョンのファイルに付与される拡張子。
* **.swp / .swo** テキストエディタ（Vim等）やシステムが作成するスワップ（一時退避）ファイル。
* **.lock / .lck** ファイルやプロセスの二重起動・同時書き込みを防ぐロックファイル。
* **.pid** (Process ID) 稼働中プロセスのID番号を記録するUnix系ファイル。
* **.lnk** (Link) Windowsのショートカットファイル。
* **.url** インターネットショートカットファイル。
* **.desktop** Linuxデスクトップ環境のアプリケーションランチャー（ショートカット）ファイル。
* **.pf** (Prefetch) Windowsのアプリ起動を高速化するためのプリフェッチキャッシュファイル。
* **.db** (Database) Windowsのサムネイルキャッシュ（Thumbs.db）やシステム管理用データベース。
* **.dat** (Data) ユーザーレジストリ（NTUSER.DAT）など、システムやアプリが格納する汎用データファイル。
* **.cab** (Cabinet) Windowsのシステムファイルや更新プログラムを格納する圧縮アーカイブ。
* **.msu** (Microsoft Update) Windows Updateのスタンドアロンインストーラーパッケージ。

---

## 6. ディスクイメージ・仮想マシン・ブート関連
ストレージ全体の複製、仮想環境、OS起動プロセスに関わるファイルです。

* **.iso** CD/DVD/BDやOSインストールメディアの標準ディスクイメージ。
* **.img** フロッピーディスクやSDカード、生ディスク領域のバイナリイメージ。
* **.vhd / .vhdx** Microsoft Hyper-VやWindowsの仮想ハードディスクファイル。
* **.vmdk** VMwareの仮想マシンディスクファイル。
* **.vdi** VirtualBoxの仮想ディスクイメージファイル。
* **.qcow2** QEMU / KVM（Linux仮想化環境）の仮想ディスクイメージ。
* **.ova / .ovf** 仮想マシン環境を丸ごと配布・移行するためのパッケージ形式。
* **.wim** (Windows Imaging Format) Windowsのインストールやシステム展開に使われるファイルベースイメージ。
* **.esd** (Electronic Software Download) 高圧縮された暗号化Windowsイメージファイル。
* **.swm** 分割されたWindowsイメージ（.wim）ファイル。
* **.ost / .pst**（参考：システム管理上よく扱われるデータファイル）Outlookのオフライン/個人用データファイル。

---

## 7. セキュリティ・証明書・暗号化関連
OSの通信保護、プログラムの署名検証、ユーザー認証に使われるファイルです。

* **.cer / .crt** デジタル証明書（公開鍵証明書）ファイル。
* **.pem** Base64エンコードされた証明書や秘密鍵のテキスト形式ファイル。
* **.der** バイナリ形式のデジタル証明書ファイル。
* **.pfx / .p12** 秘密鍵と証明書をセットにしてパスワード保護したファイル（PKCS#12）。
* **.key** 暗号化やSSH認証などに使われる秘密鍵・公開鍵ファイル。
* **.pub** SSHなどの公開鍵ファイル。
* **.csr** (Certificate Signing Request) 認証局へ提出する証明書署名要求ファイル。
* **.crl** (Certificate Revocation List) 失効した証明書の一覧ファイル。
* **.cat** (Security Catalog) Windowsドライバやシステムファイルのデジタル署名カタログ。

---

## 補足：拡張子がないシステムファイル（Windows / Unix系）
ファイル名に拡張子を持たない重要なシステムファイルも多数存在します。
* **Windows:** `pagefile.sys`（仮想メモリ・拡張子あり）、`hiberfil.sys`（休止状態）、`swapfile.sys` などのほか、`hosts`、`lmhosts`、`services`、`protocol`、`networks`（`C:\Windows\System32\drivers\etc` 配下）は拡張子なし。
* **Linux / macOS:** `/etc/fstab`、`/etc/passwd`、`/etc/shadow`、`/etc/hosts`、`/etc/sudoers` や、`Makefile`、`Dockerfile`、`Vagrantfile` などのシステム構成ファイルは基本的に拡張子を持ちません。
