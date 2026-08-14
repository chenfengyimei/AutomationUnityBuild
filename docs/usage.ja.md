# 利用ガイド

このドキュメントは AutomationUnityBuildIOS の全利用パスをカバーします：ローカル CLI、iOS ビルド、Android ビルド、TikTok ミニゲームビルド、ストアアップロード、DesktopApp デスクトップクライアント、BuildServer Web プラットフォーム、メール通知、ストレージ管理、テンプレート管理、MCP/Agent エントリ、および LinuxGateway マルチノードスケジューリング。

初めての方は、以下の順序で進めることをお勧めします：

1. Mac/Windows ビルド環境を準備する。
2. Unity ビルドスクリプトを Unity プロジェクトにコピーする。
3. Mac で CLI を使って設定を生成し、ドライランを完了する。
4. 実ビルドを行う。
5. チームが Web エントリを必要とする場合、BuildServer をデプロイする。
6. 複数のビルドマシンを統合する場合、LinuxGateway をデプロイする。

---

## モード選択

| シナリオ | 推奨モード | 備考 |
|------|----------|------|
| 自分の Mac で iOS パッケージをビルド | CLI | 最小構成、`./AutomationUnityBuildIOS 06` を直接実行 |
| iOS + Android 両方を自動化 | CLI または BuildServer | CLI は個人向け、BuildServer はチーム向け |
| TikTok ミニゲーム WebGL ビルド＆アップロード | CLI | ショートカット `12` で TikTok 設定を生成、WebGL ビルド後 API アップロードをサポート |
| Windows でオフライン設定管理とビルド | DesktopApp | ネイティブデスクトップクライアント、フル機能設定編集、ビルド実行、成果物ブラウザ |
| QA/運用がボタンクリックでビルド | BuildServer | ブラウザログイン、タスク送信、ログ閲覧、成果物ダウンロード |
| 複数の Mac/Windows ビルドマシン | LinuxGateway + BuildServer | LinuxGateway は統合エントリのみ、実際のビルドは各ノードの BuildServer で実行 |
| ノードが NAT/イントラネットで外部からアクセス不可 | LinuxGateway リバース接続 | ノードが LinuxGateway にアウトバウンド接続、パブリック IP やポートマッピング不要 |
| AI Agent にビルドプロセスに参加させる | BuildServer MCP | Agent はデフォルトでドライラン、実ビルドには承認が必要 |

---

## 環境セットアップ

### 開発機

このツールのビルドとパブリッシュに必要なもの：

- .NET 8 SDK。
- Windows、macOS、Linux のいずれでもコンパイル可能。
- Visual Studio を使用する場合は VS 2022 以降を推奨。

基本検証：

```powershell
dotnet --version
dotnet build .\AutomationUnityBuildIOS.sln
```

### iOS ビルドマシン

iOS の最終ビルドは macOS で実行する必要があります。Unity iOS Build Support と Xcode は Mac でのみ利用可能です。

Mac の必要要件：

- Xcode（ライセンス同意とコンポーネントインストールのため、最低1回は起動）。
- Unity Hub、対応する Unity Editor バージョン、iOS Build Support モジュール。
- Git CLI。Mac から Unity リポジトリにアクセス可能であること。SSH キーの設定を推奨。
- Apple Developer アカウント、証明書、プロビジョニングプロファイル、または Xcode 自動署名。
- セルフコンテナパブリッシュパッケージを使用しない場合、.NET 8 SDK も必要。

検証コマンド：

```bash
git --version
xcodebuild -version
/Applications/Unity/Hub/Editor/<UnityVersion>/Unity.app/Contents/MacOS/Unity -version
```

### Android ビルドマシン

Android ビルドは macOS または Windows で実行可能です。

必要要件：

- Unity Hub、対応する Unity Editor バージョン、Android Build Support。
- Unity にバンドルされた Android SDK、NDK、OpenJDK、または独自の Android ツールチェーン。
- リリースパッケージ署名用の Android keystore。
- Google Play アップロード用の Google Play Console Service Account JSON（対象アプリの公開権限を付与）。

---

## Unity プロジェクトの準備

このツールは Unity の `-executeMethod` で Unity Editor スクリプトを呼び出すため、Unity ゲームリポジトリに本プロジェクト提供のビルドスクリプトを追加する必要があります。

iOS：

```text
UnityBuildScripts/Ios/BuildIOS.cs
```

Unity プロジェクトにコピー：

```text
Assets/Editor/BuildIOS.cs
```

提供されるメソッド：

```text
BuildAutomation.IOSBuilder.Build
```

Android：

```text
UnityBuildScripts/Android/BuildAndroid.cs
```

Unity プロジェクトにコピー：

```text
Assets/Editor/BuildAndroid.cs
```

提供されるメソッド：

```text
BuildAutomation.AndroidBuilder.Build
```

AutomationUnityBuildIOS の更新後、これらのスクリプトに変更があった場合は、Unity ゲームリポジトリにも同期してください。

---

## ローカルCLIクイックスタート

### 開発機から Mac CLI をパブリッシュ

Apple Silicon Mac：

```powershell
.\scripts\publish-mac.ps1 -Runtime osx-arm64
```

Intel Mac：

```powershell
.\scripts\publish-mac.ps1 -Runtime osx-x64
```

パブリッシュ成果物の出力先：

```text
publish/osx-arm64
publish/osx-x64
```

ディレクトリ全体を Mac にコピーします。例：

```text
~/Downloads/publish_m1
```

### Mac での初回実行

macOS で「未確認のデベロッパ」や「悪意のあるソフトウェアか検証できない」という警告が出た場合、パブリッシュディレクトリで以下を実行：

```bash
cd ~/Downloads/publish_m1
xattr -cr .
chmod +x ./AutomationUnityBuildIOS
codesign --force --deep --sign - ./AutomationUnityBuildIOS
./AutomationUnityBuildIOS 00
```

`00` でヘルプとショートカットコマンド表が表示されます。

### 設定の作成

iOS インタラクティブ設定ウィザード：

```bash
./AutomationUnityBuildIOS 01
```

同等のフルコマンド：

```bash
./AutomationUnityBuildIOS init-config
```

空の iOS テンプレートを生成：

```bash
./AutomationUnityBuildIOS init-config --config build-ios.json --template
```

空の Android テンプレートを生成：

```bash
./AutomationUnityBuildIOS 11
```

同等のフルコマンド：

```bash
./AutomationUnityBuildIOS init-config --config build-android.json --template --platform android
```

本番設定は `configs/` に配置することを推奨：

```text
configs/build-ios.dev.json
configs/build-ios.testflight.json
configs/build-android.internal.json
```

### 環境チェック

設定を選択して環境をチェック：

```bash
./AutomationUnityBuildIOS 04
```

設定を指定：

```bash
./AutomationUnityBuildIOS doctor --config configs/build-ios.dev.json
```

Windows で設定のデバッグやドライランを行う場合：

```bash
--allow-non-mac
```

iOS の本番ビルドは引き続き macOS で実行する必要があります。

### コマンドのプレビュー

実行せずにパイプラインをプレビュー：

```bash
./AutomationUnityBuildIOS 05 --config configs/build-ios.dev.json
```

同等のフルコマンド：

```bash
./AutomationUnityBuildIOS run --config configs/build-ios.dev.json --dry-run --verbose --allow-non-mac
```

### 実ビルド

既存の設定を選択してフルパイプラインを実行：

```bash
./AutomationUnityBuildIOS 06
```

設定を指定：

```bash
./AutomationUnityBuildIOS 06 --config configs/build-ios.dev.json
```

フルコマンド：

```bash
./AutomationUnityBuildIOS run --config configs/build-ios.dev.json
```

### 一般的なスキップフラグ

| フラグ | 効果 |
|------|------|
| `--skip-git` | Git のプル/リセットをスキップ、ワークスペースの既存プロジェクトを使用 |
| `--skip-unity` | Unity エクスポートまたは Android ビルドをスキップ |
| `--skip-xcode` | Xcode archive/export をスキップ（iOS のみ、Android では無視） |
| `--dry-run` | コマンドを印刷するのみ、ビルドやアップロードを実行しない |
| `--verbose` | より詳細なパスとコマンドを出力 |
| `--allow-non-mac` | 非 macOS で iOS ドライランや設定デバッグを許可 |

### ショートカットコマンド表

| コード | 説明 |
|------|------|
| `00` | ヘルプとショートカットコマンド表を表示 |
| `01` | インタラクティブ設定ウィザード、すぐ使える設定ファイルを生成 |
| `02` | 空の iOS 設定テンプレート `build-ios.json` を生成 |
| `03` | 既存の設定ファイル一覧を表示 |
| `04` | 設定を選択して環境チェック |
| `05` | 設定を選択してフルビルドコマンドをプレビュー（ドライラン） |
| `06` | 設定を選択してフルビルドパイプラインを実行 |
| `07` | 設定を選択してビルド、Git 同期をスキップ |
| `08` | 設定を選択してビルド、Unity エクスポートをスキップ |
| `09` | 設定を選択してビルド、Xcode コンパイル/エクスポートをスキップ |
| `10` | 設定を選択して内容を編集 |
| `11` | Android APK/AAB 設定テンプレート `build-android.json` を生成 |
| `12` | TikTok ミニゲーム設定テンプレート `build-tiktok.json` を生成 |

ショートカットには追加引数を付けられます：

```bash
./AutomationUnityBuildIOS 05 --config configs/build-ios.dev.json
./AutomationUnityBuildIOS 06 --config configs/build-ios.release.json
./AutomationUnityBuildIOS 10 --config configs/build-android.internal.json
```

---

## 設定ファイルリファレンス

設定ファイルは JSON です。iOS の例は `build-ios.sample.json`、Android は `build-android.sample.json`、TikTok は `build-tiktok.sample.json` を参照してください。

### 共通フィールド

| フィールド | 説明 |
|------|------|
| `configName` | 設定の表示名、選択リストに表示 |
| `buildPlatform` | `ios`、`android`、または `tiktok` |
| `repositoryUrl` | Unity ゲームリポジトリの Git クローン URL、HTTPS/SSH 対応 |
| `allowedRepositoryUrls` | リポジトリホワイトリスト、本番環境では推奨 |
| `branch` | ビルドブランチ |
| `workspaceRoot` | Git ワークスペースのルートディレクトリ |
| `allowedWorkspaceRoots` | 許可されたワークスペースルートディレクトリ、パスエスケープを防止 |
| `projectDirectoryName` | リポジトリクローン後のディレクトリ名 |
| `unityProjectRelativePath` | リポジトリルートからの Unity プロジェクトの相対パス。リポジトリルートが Unity プロジェクトの場合は `.` |
| `unityVersion` | Unity Hub インストールバージョン、Unity 実行ファイルパスの推導に使用 |
| `unityExecutablePath` | Unity 実行ファイルのフルパス。`unityVersion` より優先 |
| `unityBuildMethod` | Unity Editor 静的メソッド名 |
| `artifactsRoot` | ビルド成果物のルートディレクトリ |
| `allowedArtifactsRoots` | 許可された成果物ルートディレクトリ |
| `productName` | Unity Product Name |
| `bundleIdentifier` | iOS Bundle ID または Android Package Name |
| `bundleVersion` | バージョン番号 |
| `syncBundleVersionFromUnity` | Unity PlayerSettings からバージョンを同期するか |
| `buildNumber` | iOS Build Number または Android versionCode |
| `autoIncrementBuildNumber` | ビルド成功後にビルド番号を自動インクリメントするか |
| `saveConfigSnapshot` | ログディレクトリに設定スナップショットを保存するか |

最も間違いやすい3つの値：

```text
repositoryUrl: git clone URL を使用。Web ページのタイトルではない。
unityProjectRelativePath: 通常は .。build、Builds、XcodeProject ではない。
teamId: iOS は10文字の Apple Developer Team ID。会社名ではない。
```

### iOS フィールド

| フィールド | 説明 |
|------|------|
| `scheme` | デフォルト `Unity-iPhone` |
| `configuration` | デフォルト `Release` |
| `exportMethod` | `development`、`ad-hoc`、`app-store` など（Xcode エクスポート方式） |
| `teamId` | Apple Developer Team ID、10文字の英数字である必要あり |
| `signingStyle` | `automatic` または `manual` |
| `iosDeploymentTarget` | iOS 最低バージョン（例：`13.0`） |
| `allowProvisioningUpdates` | Xcode が署名更新を自動処理することを許可するか |
| `generateExportOptionsPlist` | `ExportOptions.plist` を自動生成するか |
| `copyArchiveToOrganizer` | `.xcarchive` を Xcode Organizer にコピーするか |
| `appStoreConnectUploadEnabled` | App Store Connect/TestFlight に自動アップロードするか |

### Android フィールド

| フィールド | 説明 |
|------|------|
| `androidBuildFormat` | `apk`、`aab`、または `both` |
| `androidOutputDirectory` | Android 出力ディレクトリ、空の場合は自動生成 |
| `apkOutputPath` | APK 出力パス、空の場合は自動生成 |
| `aabOutputPath` | AAB 出力パス、空の場合は自動生成 |
| `androidMinSdkVersion` | 任意、Min SDK を上書き |
| `androidTargetSdkVersion` | 任意、Target SDK を上書き |
| `androidKeystoreName` | keystore パスまたは名前 |
| `androidKeystorePass` | keystore パスワード |
| `androidKeyaliasName` | key alias |
| `androidKeyaliasPass` | key alias パスワード |
| `googlePlayUploadEnabled` | Google Play にアップロードするか |
| `googlePlayTrack` | `internal`、`alpha`、`beta`、`production` |
| `googlePlayReleaseStatus` | `draft`、`inProgress`、`halted`、`completed` |
| `googlePlayUploadArtifact` | `apk`、`aab`、または `both` をアップロード |

証明書、秘密鍵、長期有効トークンをリポジトリにコミットしないでください。設定でシークレットを参照する必要がある場合は、ビルドマシン上のローカルパスを優先し、ファイル権限を保護してください。

### TikTok フィールド

| フィールド | 説明 |
|------|------|
| `tiktokAppId` | TikTok オープンプラットフォーム App ID |
| `tiktokAccessToken` | TikTok オープンプラットフォーム Access Token |
| `tiktokGameName` | TikTok ミニゲーム名 |
| `tiktokWebglOutputDirectory` | WebGL 出力ディレクトリ、空の場合は自動生成 |
| `tiktokUploadEnabled` | TikTok オープンプラットフォームに自動アップロードするか |
| `tiktokApiEndpoint` | TikTok オープンプラットフォーム API URL、デフォルト `https://open-api.tiktokglobalshop.com` |

---

## iOSビルド

### 基本パイプライン

完全な iOS パイプライン：

1. 設定の安全境界と Git リポジトリポリシーを検証。
2. `git`、Unity、`xcodebuild` をチェック。
3. 実行ディレクトリとログディレクトリを作成。
4. `build-config-snapshot.json` を書き込み。
5. Unity リポジトリをプルまたは更新。
6. Unity BatchMode で iOS Xcode プロジェクトをエクスポート。
7. `xcodebuild archive` を実行。
8. `xcodebuild -exportArchive` を実行。
9. オプションで `.xcarchive` を Xcode Organizer にコピー。
10. オプションで App Store Connect/TestFlight にアップロード。

### App Store Connect / TestFlightアップロード

自動アップロードを有効にするには `exportMethod` を `app-store` に設定し、App Store Connect API Key を設定します。

例：

```json
{
  "exportMethod": "app-store",
  "appStoreConnectUploadEnabled": true,
  "appStoreConnectApiKeyPath": "~/Secrets/AuthKey_XXXXXXXXXX.p8",
  "appStoreConnectApiKeyId": "XXXXXXXXXX",
  "appStoreConnectApiIssuerId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
}
```

注意：

- `.p8` ファイルは Mac ビルドマシン上にローカルに存在する必要があります。
- Key ID と Issuer ID は App Store Connect API Key ページから取得します。
- アップロード成功後、ビルドは App Store Connect/TestFlight の処理キューに入ります。
- 審査に提出するか、本番にリリースするかは、App Store Connect のバージョンポリシーに従います。

### 一般的なiOSデバッグ方法

Git と Unity のみ同期、Xcode をスキップ：

```bash
./AutomationUnityBuildIOS run --config configs/build-ios.dev.json --skip-xcode
```

Unity をスキップ、既存の Xcode プロジェクトを再利用して archive/export：

```bash
./AutomationUnityBuildIOS run --config configs/build-ios.dev.json --skip-unity
```

設定と環境のチェックのみ：

```bash
./AutomationUnityBuildIOS doctor --config configs/build-ios.dev.json
```

---

## Androidビルド

### 基本パイプライン

完全な Android パイプライン：

1. 設定の安全境界と Git リポジトリポリシーを検証。
2. `git` と Unity をチェック。
3. 実行ディレクトリとログディレクトリを作成。
4. `build-config-snapshot.json` を書き込み。
5. Unity リポジトリをプルまたは更新。
6. Unity BatchMode で APK/AAB をビルド。
7. オプションで Google Play にアップロード。

Android は Xcode を必要としません。`--skip-xcode` は無視されます。

### APK/AABビルド

設定：

```json
{
  "buildPlatform": "android",
  "unityBuildMethod": "BuildAutomation.AndroidBuilder.Build",
  "androidBuildFormat": "both"
}
```

`androidBuildFormat` の選択肢：

| 値 | 結果 |
|-------|--------|
| `apk` | APK のみ生成 |
| `aab` | AAB のみ生成 |
| `both` | APK と AAB の両方を生成 |

### Google Playアップロード

Google Play Console で Service Account を作成し、対象アプリの公開権限を付与する必要があります。

例：

```json
{
  "googlePlayUploadEnabled": true,
  "googlePlayPackageName": "com.company.game",
  "googlePlayServiceAccountJsonPath": "~/Secrets/google-play-service-account.json",
  "googlePlayTrack": "internal",
  "googlePlayReleaseStatus": "draft",
  "googlePlayUploadArtifact": "aab",
  "googlePlayChangesNotSentForReview": false,
  "googlePlayUserFraction": null
}
```

推奨：まずドライラン：

```bash
./AutomationUnityBuildIOS run --config configs/build-android.internal.json --dry-run --verbose
```

パス、パッケージ名、バージョン、アップロード成果物を確認してから本番実行してください。

---

## TikTokミニゲームビルド

### 基本パイプライン

TikTok ミニゲームビルドパイプライン：

1. 設定の安全境界と Git リポジトリポリシーを検証。
2. `git` と Unity をチェック。
3. 実行ディレクトリとログディレクトリを作成。
4. `build-config-snapshot.json` を書き込み。
5. Unity リポジトリをプルまたは更新。
6. Unity BatchMode で WebGL をビルド。
7. オプションで TikTok オープンプラットフォームにアップロード。

TikTok ビルドは Xcode を必要としません。`--skip-xcode` は無視されます。

### 設定の生成

```bash
./AutomationUnityBuildIOS 12
```

同等のフルコマンド：

```bash
./AutomationUnityBuildIOS init-config --config build-tiktok.json --template --platform tiktok
```

### 設定例

```json
{
  "buildPlatform": "tiktok",
  "unityBuildMethod": "BuildAutomation.TiktokBuilder.Build",
  "tiktokAppId": "your-app-id",
  "tiktokAccessToken": "your-access-token",
  "tiktokGameName": "Your Game",
  "tiktokUploadEnabled": true
}
```

### 実ビルド

```bash
./AutomationUnityBuildIOS run --config configs/build-tiktok.release.json
```

TikTok 関連コードは `Modules/Tiktok/` にあり、iOS/Android から完全に独立しており、既存のビルドフローに影響しません。

---

## デスクトップクライアント

DesktopApp は Avalonia UI 11 + .NET 8 ベースのネイティブ Windows デスクトップクライアントで、メインプロジェクトの全コアロジック（AutomationWorkflow / BuildConfig / ConfigFileSelector / SampleFiles）を再利用します。CLI、BuildServer、テンプレート管理の機能を一つのデスクトップアプリに統合し、全操作がオフラインで利用可能です。

### 機能ページ

| ページ | 機能 |
|------|----------|
| **設定管理** | iOS/Android/TikTok の全フィールド編集、設定ファイル名の自動同期、テンプレートセレクタでワンクリック入力 |
| **ビルドタスク** | リアルタイムログ tail、経過タイマー、ログクリア、自動スクロール |
| **環境チェック** | Unity、Git、Xcode などの環境依存関係を検証 |
| **成果物ブラウザ** | ファイルリスト、選択、ダブルクリックで開く、ファイルプレビュー |
| **ストレージ管理** | チェックボックスで一括削除、単一削除、全選択、ストレージ概要 |
| **メール通知** | SMTP 設定（465 暗黙 SSL 含む）、連絡先リスト、メールテンプレート |
| **プロジェクト管理** | ProjectProfile テンプレート、リポジトリ/ワークスペースディレクトリ等を管理 |
| **Unity 管理** | UnityProfile テンプレート、Unity バージョン/パス/BuildMethod/ProductName/BundleID を管理 |
| **署名管理** | SigningProfile テンプレート、iOS TeamID/ExportMethod/SigningStyle/Android Keystore を管理 |
| **証明書管理** | CertificateProfile テンプレート、ASC API Key/Google Play/TikTok Token を管理 |
| **サーバー同期** | BuildServer REST API に接続、テンプレートと設定ファイルの双方向同期 |
| **BuildServer 管理** | BuildServer.exe パスの自動検出または手動選択、ワンクリック起動/停止、ヘルスチェック |
| **データ管理** | 各データタイプを JSON でエクスポート、JSON インポートで ID ベースの重複排除マージ |
| **ヘルプ** | 利用ガイドとショートカットコマンドリファレンス |

### DesktopAppのパブリッシュ

```powershell
dotnet publish DesktopApp/DesktopApp.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true -o DesktopApp/bin/publish-vN
```

前回の exe が実行中の場合、`UnauthorizedAccessException` が発生します。先に停止してください：

```powershell
Stop-Process -Name DesktopApp -Force
```

その後、新しいディレクトリにパブリッシュします。単一ファイル出力は約 89 MB です。

パブリッシュスクリプトも使用可能：

```powershell
.\scripts\publish-desktop.ps1
```

### テンプレート管理

DesktopApp は4種類の設定テンプレートを提供し、データは `profiles/` ディレクトリに保存されます：

| テンプレート | ファイル | 用途 |
|------|------|------|
| プロジェクト管理 | `projects.json` | リポジトリ URL、ワークスペースディレクトリ、成果物ディレクトリ等 |
| Unity 管理 | `unity-profiles.json` | Unity バージョン、パス、BuildMethod、ProductName、BundleID |
| 署名管理 | `signing-profiles.json` | iOS TeamID、ExportMethod、SigningStyle、Android Keystore |
| 証明書管理 | `certificates.json` | ASC API Key、Google Play Service Account、TikTok Token |

設定管理ページの編集フォーム上部に4つのテンプレートセレクタがあります。各々から1つを選び「適用」をクリックすると、対応するフィールドがワンクリックで入力されます。テンプレート適用後、入力されたフィールドセクションは自動的に非表示になり、画面の煩雑さを軽減します。

### サーバー同期

DesktopApp は BuildServer REST API に接続して双方向同期が可能：

- **プロジェクトテンプレート**: プル/プッシュ
- **証明書テンプレート**: プル/プッシュ
- **設定ファイル**: サーバー設定リストの閲覧 + ローカル `configs/` ディレクトリへのダウンロード

接続情報は `profiles/server-settings.json` に永続化されます。

設定管理ページには「設定ファイルのインポート」ボタンもあり、ローカルの任意の場所から JSON を `configs/` にインポートできます。

---

## メール通知

BuildServer はビルドタスク完了後に自動でメール通知を送信します。成功と失敗の両方をカバーします。

### 設定

BuildServer の Web バックエンドまたは DesktopApp のメール通知ページで設定：

| フィールド | 説明 |
|------|------|
| SMTP サーバー | 例：`smtp.gmail.com`、`smtp.qq.com` |
| SMTP ポート | 一般：25（平文）、465（暗黙 SSL）、587（STARTTLS） |
| 送信元メールアドレス | 通知を送信するメールアドレス |
| 送信元パスワード | メール認証コードまたはパスワード |
| SSL 有効化 | ポート 465 は暗黙 SSL を使用 |
| 通知先連絡先 | 受信者メールリスト、カンマまたは改行で区切り |
| メールテンプレート | カスタマイズされたメール件名と本文テンプレート |

### 通知トリガー

- **ビルド成功**: メールにビルド成果物パス、経過時間、設定サマリーを含む。
- **ビルド失敗**: メールに失敗ステップ、エラーサマリー、ログパスを含む。迅速なトラブルシューティングに便利。

メール通知サービスは `BuildServer/Services/EmailNotificationService.cs` に実装されています。

---

## ストレージ管理

ビルドタスクが蓄積すると、成果物がディスク容量を徐々に消費します。BuildServer は2つのストレージ管理メカニズムを提供します：

### 自動クリーンアップ

`MaintenanceService` が設定された `RetentionDays` と `MaxArtifactBytes` に基づいて完了タスクと成果物を自動クリーンアップします。

### 手動クリーンアップ

Web バックエンドまたは DesktopApp のストレージ管理ページで：

- ストレージ概要の表示（総容量、使用量、タスク数、成果物サイズ分布）。
- 複数の履歴タスクを選択して一括削除。
- 単一タスクの成果物を削除。
- 全選択で全履歴成果物をクリア。

ストレージクリーンアップサービスは `BuildServer/Services/StorageCleanupService.cs` に実装されています。

---

## ログと成果物

毎回の実行で `artifactsRoot` の下に独立したディレクトリが作成されます。例：

```text
~/UnityBuildArtifacts/YourUnityGame/20260625-153000/
```

一般的な内容：

| ファイルまたはディレクトリ | 説明 |
|------------|------|
| `Logs/automation.log` | マスターパイプラインログ。ステップ、コマンド、経過時間、エラーを含む |
| `Logs/unity-editor.log` | Unity Editor 自身のビルドログ |
| `Logs/unity-process.log` | Unity プロセスからキャプチャした stdout/stderr |
| `Logs/build-config-snapshot.json` | 今回の設定スナップショット。基本マスキング済み |
| `Logs/xcode-archive.log` | iOS archive ログ |
| `Logs/xcode-export.log` | iOS export ログ |
| `Logs/xcode-upload.log` | App Store Connect アップロードログ |
| `.xcarchive` | iOS アーカイブ成果物 |
| `.ipa` エクスポートディレクトリ | iOS エクスポート成果物 |
| `.apk` / `.aab` | Android ビルド成果物 |

トラブルシューティングの順序：

1. まず `automation.log` の末尾で失敗ステップを確認。
2. Unity ステージの失敗場合は `unity-editor.log` を確認。
3. iOS Xcode ステージの失敗場合は `xcode-archive.log` または `xcode-export.log` を確認。
4. ストアアップロードの失敗場合は `xcode-upload.log` またはマスターログの Google Play アップロードエラーを確認。

ログシステムは一般的な機密情報（URL 内の認証情報/トークン、`Bearer` トークン、`password/token/secret/apiKey` などのキーの値）に基本マスキングを適用します。

---

## BuildServer Webプラットフォーム

BuildServer は CLI の Web/Agent エントリポイントです。以下を提供します：

- Web ログイン。
- プロジェクト管理。
- 設定管理。
- ビルドタスクキュー。
- リアルタイムログ。
- 成果物ダウンロード。
- ユーザー権限。
- 監査ログ。
- MCP/Agent ツール。
- LinuxGateway ノード API。

初版はシングルマシン、シングル Worker、シリアルキューを採用し、Unity、Xcode、Gradle、署名環境、キャッシュディレクトリの並行競合を回避します。

### ローカル起動

Windows デバッグ：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-build-server.ps1
```

macOS/Linux デバッグ：

```bash
./scripts/run-build-server.sh
```

デフォルトアドレス：

```text
http://127.0.0.1:5088
```

デフォルトアカウント：

```text
admin
```

`BUILD_SERVER_ADMIN_PASSWORD` が未設定の場合、初回起動時にランダムパスワードを生成：

```text
<DataRoot>/initial-admin.txt
```

`BUILD_SERVER_AGENT_TOKEN` が未設定の場合、初回起動時にデフォルト MCP Agent Token を生成：

```text
<DataRoot>/initial-agent-token.txt
```

### 本番環境変数

本番環境での推奨設定：

```bash
export BUILD_SERVER_ADMIN_PASSWORD="strong-password"
export BUILD_SERVER_AGENT_TOKEN="strong-agent-token"
export BUILD_SERVER_PUBLIC_BASE_URL="https://mac-build.example.com"
export BUILD_SERVER_ALLOWED_ORIGINS="https://mac-build.example.com"
export BUILD_SERVER_ALLOWED_WORKSPACE_ROOTS="/Users/build/UnityBuildWorkspace"
export BUILD_SERVER_ALLOWED_ARTIFACTS_ROOTS="/Users/build/UnityBuildArtifacts"
export BUILD_SERVER_ALLOWED_CONFIG_ROOTS="/Users/build/BuildServerData/configs"
export BUILD_SERVER_ALLOWED_REPOSITORY_HOSTS="gitee.com,github.com"
export BUILD_SERVER_NODE_PLATFORMS="ios,android"
```

主な変数：

| 変数 | 説明 |
|------|------|
| `BUILD_SERVER_DATA_ROOT` | データディレクトリ。ユーザー、プロジェクト、設定、タスク、監査 JSON を保存 |
| `BUILD_SERVER_ADMIN_PASSWORD` | 管理者パスワード |
| `BUILD_SERVER_AGENT_TOKEN` | MCP Agent Token |
| `BUILD_SERVER_PUBLIC_BASE_URL` | 外部アクセス URL |
| `BUILD_SERVER_ALLOWED_ORIGINS` | 許可する Web Origin。リバースプロキシ使用時に推奨 |
| `BUILD_SERVER_ALLOWED_WORKSPACE_ROOTS` | 許可するワークスペースルートディレクトリ |
| `BUILD_SERVER_ALLOWED_ARTIFACTS_ROOTS` | 許可する成果物ルートディレクトリ |
| `BUILD_SERVER_ALLOWED_CONFIG_ROOTS` | 許可する設定ファイルルートディレクトリ |
| `BUILD_SERVER_ALLOWED_REPOSITORY_HOSTS` | 登録可能な Git ホスト |
| `BUILD_SERVER_GATEWAY_TOKEN` | ノード API トークン。空の場合は初回起動時に `initial-gateway-token.txt` を自動生成 |
| `BUILD_SERVER_NODE_PLATFORMS` | 現在のノード能力。例：`ios,android` または `android` |

### Web 利用フロー

バックエンドに初回ログイン後：

1. プロジェクト追加：プロジェクト名、Git リポジトリ、デフォルトブランチ、許可ブランチ、ワークスペース、成果物ディレクトリを入力。
2. 設定追加：iOS または Android を選択。
3. 設定は既存 JSON ファイルを参照するか、Web フォームから新規生成可能。
4. ビルド開始：プロジェクト、設定、ブランチ、オプションパラメータを選択。
5. タスクリストでステータス、リアルタイムログ、成果物を確認。

BuildServer は各タスクの独立した設定スナップショットを生成し、CLI を呼び出します：

```text
AutomationUnityBuildIOS run --config <job-config.json>
```

### BuildServer の Mac へのパブリッシュ

Apple Silicon Mac：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-build-server-mac.ps1 -Runtime osx-arm64
```

Intel Mac：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-build-server-mac.ps1 -Runtime osx-x64
```

パブリッシュディレクトリには BuildServer と AutomationUnityBuildIOS CLI の両方が含まれます。本番環境では以下と組み合わせて使用：

```text
deploy/launchd/com.automationunity.buildserver.plist
```

BuildServer を実行する専用 macOS ユーザーを固定し、Unity License、Xcode 署名、証明書、プロビジョニングプロファイル、Git SSH キーをすべてそのユーザーの下に設定することを推奨します。

### MCP / Agent

MCP エンドポイント：

```text
POST /mcp
Header: X-Agent-Token: <BUILD_SERVER_AGENT_TOKEN>
```

サポートツール：

| ツール | 説明 |
|------|------|
| `list_projects` | 利用可能なプロジェクト一覧 |
| `list_configs` | プロジェクト配下のビルド設定一覧 |
| `start_build` | iOS または Android ビルドタスクを送信 |
| `start_ios_build` | 旧名称、新規統合では `start_build` を推奨 |
| `get_build_status` | ビルドタスクステータスを照会 |
| `tail_build_log` | 最新ログを読み取り |
| `list_build_artifacts` | タスク成果物一覧 |

デフォルトでは Agent は `dryRun=true` のみ許可されます。実ビルドを許可するには、対応する MCP Client の `allowFullBuild` を有効にし、特定のプロジェクトのみを承認することを推奨します。

Agent Token を URL クエリパラメータに含めないでください。`X-Agent-Token` または `Authorization: Bearer` を使用してください。

---

## LinuxGateway マルチノードエントリ

LinuxGateway はパブリックドメインを持つ Linux サーバーへのデプロイに適しています。Unity を実行せず、Unity プロジェクトを保存せず、Apple 証明書も保持しません。ログイン、ノード登録、ノード選択、タスク転送、ログ/成果物のプロキシのみを担当します。

典型的なアーキテクチャ：

```text
外部ユーザー
  -> LinuxGateway Web/API
      -> Mac BuildServer       iOS + Android
      -> Windows BuildServer   Android
```

LinuxGateway をデプロイしない場合、各 Mac/Windows の BuildServer は引き続き独立して使用可能です。

### LinuxGateway の起動

開発実行：

```bash
./scripts/run-linux-gateway.sh http://127.0.0.1:5090
```

Windows デバッグ：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-linux-gateway.ps1
```

デフォルトアドレス：

```text
http://127.0.0.1:5090
```

`LINUX_GATEWAY_ADMIN_PASSWORD` が未設定の場合、初回起動時に初期パスワードを生成：

```text
linuxgateway-data/initial-admin.txt
```

本番環境での推奨設定：

```bash
export LINUX_GATEWAY_ADMIN_PASSWORD="strong-password"
export LINUX_GATEWAY_PUBLIC_BASE_URL="https://build.example.com"
export LINUX_GATEWAY_ALLOWED_ORIGINS="https://build.example.com"
export LINUX_GATEWAY_DATA_ROOT="/opt/unity-build-gateway/data"
```

### LinuxGateway の Linux へのパブリッシュ

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-linux-gateway-linux.ps1
```

デフォルト出力：

```text
publish/linux-gateway
```

Linux にコピー後、実行：

```bash
chmod +x ./LinuxGateway
./LinuxGateway --urls http://127.0.0.1:5090
```

外部アクセスには Nginx/Caddy で HTTPS を提供し、`127.0.0.1:5090` にリバースプロキシすることを推奨します。

### モード1：直接ノード接続

直接接続は LinuxGateway が Mac/Windows BuildServer にアクセス可能な場合に適しています。VPN、イントラネット、トンネル、パブリック HTTPS など。

各 BuildServer ノードの起動前に設定：

```bash
export BUILD_SERVER_GATEWAY_TOKEN="strong-random-token"
export BUILD_SERVER_NODE_PLATFORMS="ios,android"
```

Windows Android ノード：

```powershell
$env:BUILD_SERVER_GATEWAY_TOKEN="strong-random-token"
$env:BUILD_SERVER_NODE_PLATFORMS="android"
```

`BUILD_SERVER_GATEWAY_TOKEN` を手動設定しなくても、BuildServer が初回起動時に自動生成し、以下に保存：

```text
<DataRoot>/initial-gateway-token.txt
```

BuildServer は以下を有効化：

```text
/api/gateway/*
```

LinuxGateway はノード呼び出し時に以下を使用：

```text
Header: X-Gateway-Token: <BUILD_SERVER_GATEWAY_TOKEN>
```

LinuxGateway Web UI でデバイスを追加：

| フィールド | 例 |
|------|------|
| デバイス名 | `Mac Build` |
| BuildServer URL | `https://mac-build.example.com` |
| Gateway Token | そのノードの `BUILD_SERVER_GATEWAY_TOKEN` |
| プラットフォーム | Mac：`iOS + Android`、Windows：`Android` |

保存後、デバイスをリフレッシュし、ノードのプロジェクトと設定が表示されることを確認します。

### モード2：リバースノード接続

リバース接続は、ノードが NAT、ホームネットワーク、企業イントラネットの背後にあり、LinuxGateway がノードアドレスに直接アクセスできない場合に適しています。この場合、BuildServer が LinuxGateway にアウトバウンド接続します。

LinuxGateway Web UI で Enrollment Token を生成し、BuildServer の Gateway 接続ページに入力：

```text
Gateway URL: https://build.example.com
Enrollment Token: <token>
```

環境変数で BuildServer 起動時に自動接続させることも可能：

```bash
export BUILD_SERVER_REVERSE_GATEWAY_ENABLED=true
export BUILD_SERVER_REVERSE_GATEWAY_URL="https://build.example.com"
export BUILD_SERVER_REVERSE_GATEWAY_ENROLLMENT_TOKEN="<token>"
export BUILD_SERVER_REVERSE_NODE_NAME="Mac Build"
```

接続成功後、LinuxGateway にリバース接続ノードが表示されます。ノード認証情報は BuildServer データディレクトリに保存されます。ノード取り消し後、新しい Enrollment Token を生成して再登録する必要があります。

リバース接続は `LinuxGateway/Reverse/` と `BuildServer/Reverse/` に実装されています。

### LinuxGateway オンライン自己更新

LinuxGateway は `SelfUpdateService` を内蔵し、Gitea または GitHub Releases から更新パッケージの確認とダウンロードが可能です。サーバーに .NET SDK は不要です。

更新確認：

```text
GET /api/system/version
GET /api/system/update/check
```

更新実行（Admin のみ）：

```text
POST /api/system/update/apply
```

更新プロセスは現在バージョンのバックアップ、tar.gz 更新パッケージのダウンロード、`apply-update.sh` スクリプトの生成（置換と再起動を完了）を自動的に行います。

設定：

| 変数 | 説明 |
|------|------|
| `LINUX_GATEWAY_UPDATE_SOURCE` | 更新ソース：`gitee` または `github` |
| `LINUX_GATEWAY_UPDATE_REPO_OWNER` | リポジトリオーナー |
| `LINUX_GATEWAY_UPDATE_REPO_NAME` | リポジトリ名 |

### LinuxGateway 経由でビルドを送信

1. LinuxGateway にログイン。
2. デバイスページでノードがオンラインであることを確認。
3. ノードをリフレッシュし、プロジェクトと設定が同期されていることを確認。
4. ビルドタスクページでデバイス、プロジェクト、設定、ブランチを選択。
5. タスクを送信。
6. リモートノードから返されたステータス、ログ、成果物を確認。

iOS タスクは `ios` をサポートする Mac ノードにのみ送信できます。Windows ノードは通常 Android APK/AAB のみ適しています。

---

## セキュリティ推奨事項

- 本番環境では必ず強力なパスワードを設定し、初期パスワードファイルへの長期依存を避けてください。
- `BUILD_SERVER_AGENT_TOKEN`、`BUILD_SERVER_GATEWAY_TOKEN`、Enrollment Token を URL に含めないでください。ヘッダーまたはサーバーサイドフォームで保存してください。
- LinuxGateway と BuildServer のデータディレクトリにはユーザー、タスク、ノード認証情報、トークンが保存されるため、システム権限を制限してください。
- BuildServer に `BUILD_SERVER_ALLOWED_WORKSPACE_ROOTS`、`BUILD_SERVER_ALLOWED_ARTIFACTS_ROOTS`、`BUILD_SERVER_ALLOWED_CONFIG_ROOTS`、`BUILD_SERVER_ALLOWED_REPOSITORY_HOSTS` を設定することを推奨します。
- ノードバックエンドが LinuxGateway 専用の場合、通常の管理バックエンドをパブリックインターネットに公開しないでください。
- iOS 証明書、プロビジョニングプロファイル、App Store Connect `.p8`、Android keystore、Google Play Service Account JSON はすべてビルドマシン上のセキュアなローカルディレクトリにのみ配置してください。
- 証明書、秘密鍵、長期有効トークンを Git にコミットしないでください。
- リバースプロキシ経由で Web UI にアクセスする場合、`PUBLIC_BASE_URL` と `ALLOWED_ORIGINS` を設定し、クロスオリジンリクエストの拒否やオリジン検証の失敗を回避してください。

---

## FAQ

| 問題 | 解決策 |
|------|------|
| Windows で iOS ビルドが macOS 必要とエラー | iOS 本番ビルドは Mac で実行する必要があります。Windows は `--dry-run --allow-non-mac` での設定デバッグのみ対応 |
| Unity 実行ファイルが見つからない | `unityExecutablePath` を設定、または `unityVersion` が Unity Hub インストールパスに一致することを確認 |
| Git プル失敗 | ビルドマシンで手動 `git clone` を実行し SSH キーまたは HTTPS 認証情報を検証 |
| Team ID 検証失敗 | `teamId` は10文字の Apple Developer Team ID である必要があります。会社名ではありません |
| App Store Connect アップロード失敗 | `exportMethod=app-store`、`.p8` パスの存在、Key ID と Issuer ID が正しいことを確認 |
| Android versionCode エラー | `buildNumber` は正の整数である必要があります |
| Google Play アップロード失敗 | Service Account JSON パス、アプリ権限、packageName、track、アップロード成果物フォーマットを確認 |
| BuildServer ログイン失敗 | アカウントは `admin`。`initial-admin.txt` の `admin password:` 以降の値のみコピー |
| Web 書き込み操作が拒否される | `BUILD_SERVER_ALLOWED_ORIGINS` または `LINUX_GATEWAY_ALLOWED_ORIGINS` がアクセスドメインと一致するか確認 |
| LinuxGateway ノード 401 | Gateway Token が間違っている、またはノードが `BUILD_SERVER_GATEWAY_TOKEN` を有効にしていない |
| LinuxGateway ノードタイムアウト | ノードアドレス、ポート、ファイアウォール、トンネル、リバースプロキシを確認 |
| 成果物ダウンロード失敗 | 成果物パスが BuildServer の許可された artifacts roots 内にあることを確認 |

---

## 回帰テスト

開発者は以下を実行できます：

```powershell
.\scripts\verify.ps1
```

実行内容：

- ソリューションコンパイル。
- CLI プロジェクトコンパイル。
- BuildServer コンパイル。
- LinuxGateway コンパイル。
- ヘルプエントリ `00`。
- iOS サンプルドライラン。
- Android サンプルドライラン。
- 設定エディタの開閉。

テストスイートは 256 以上のテストケースをカバーし、CLI 引数解析、設定モデル、パス安全、Git ポリシー、Unity コマンドビルド、Google Play API、TikTok 設定、BuildServer API ルート、LinuxGateway ノード通信、リバース接続、メール通知など、全モジュールを網羅しています。

完全テストスイートの実行：

```powershell
dotnet test .\AutomationUnityBuildIOS.Tests\AutomationUnityBuildIOS.Tests.csproj
```

コンパイルへの影響を素早く確認したい場合：

```powershell
dotnet build .\AutomationUnityBuildIOS.sln
```
