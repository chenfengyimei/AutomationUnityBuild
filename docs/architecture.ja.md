# アーキテクチャ

本プロジェクトはモジュール化された階層設計を採用し、コアビルドエンジンとプラットフォームエントリポイントは完全に分離されています。CLI、BuildServer、DesktopApp、LinuxGateway は同じコアロジックを共有し、違いはエントリ層とインタラクション方法のみです。

## ディレクトリ責務

ツールは責務ごとに以下のディレクトリに分割されています：

- `Cli/`：コマンドエントリ、コマンドライン引数解析、ショートカットコマンドマッピング（`ShortcutCommands`）。
- `ConsoleUi/`：コンソールインタラクティブ UI。初期化ウィザード、設定エディタ、入力プロンプトを含む。
- `Configuration/`：設定モデル、設定ファイルの読み書き、設定ファイル選択、パス解決、サンプル設定。`ios`、`android`、`tiktok` の3種プラットフォーム設定をサポート。
- `Workflow/`：ビルドパイプライン編成、実行コンテキスト、ランタイム設定更新、設定スナップショット。
- `Services/`：クロスプラットフォーム共有ビジネス機能。Git 同期、環境チェック、ディレクトリ準備、Unity プロジェクト検証、パス安全検証を含む。
- `Modules/Common/`：プラットフォームモジュール共有機能。プラットフォーム Pipeline インターフェース、Unity コマンド引数ビルド、Unity ログ診断、Unity メタデータ読み取りを含む。
- `Modules/Ios/`：iOS 専用ビルド機能。Unity Xcode プロジェクトエクスポート、Xcode project/workspace の特定、`xcodebuild archive/export` を含む。
- `Modules/Android/`：Android 専用ビルド機能。Unity APK/AAB ビルド、Google Play Publishing API アップロードを含む。`GooglePlay/` サブディレクトリが HTTP API、OAuth、Service Account の詳細を担当。
- `Modules/Tiktok/`：TikTok ミニゲーム専用機能。WebGL ビルドパイプライン（`TiktokBuildPipeline`）、ビルドサービス（`TiktokBuildService`）、TikTok オープンプラットフォーム API アップロード（`TiktokUploadService`）。iOS/Android から完全に独立しており、既存フローに影響しません。
- `Infrastructure/`：共通インフラストラクチャ。ログ（`BuildLogger`）、プロセス実行（`ProcessRunner`）、パスツール（`PathTools`）、パス安全境界（`PathSafety`）、機密情報マスキング。これらの機能は CLI、BuildServer、DesktopApp で共有されます。
- `UnityBuildScripts/Ios/`：Unity プロジェクトの `Assets/Editor` にコピーする iOS Unity Editor ビルドスクリプト。
- `UnityBuildScripts/Android/`：Unity プロジェクトの `Assets/Editor` にコピーする Android Unity Editor ビルドスクリプト。
- `BuildServer/`：Web ビルドプラットフォーム。API（`ApiRoutes`）、内蔵フロントエンド（`wwwroot/`）、バックグラウンド Worker（`BuildWorkerService`）、MCP/Agent エントリ（`McpEndpoint`）、Gateway ノード API（`GatewayEndpoint`）、メール通知（`EmailNotificationService`）、ストレージ管理（`StorageCleanupService`）、成果物スキャン（`ArtifactScanner`）、メンテナンスクリーンアップ（`MaintenanceService`）、リバース接続（`Reverse/`）、JSON 永続化（`Persistence/`）を含む。
- `LinuxGateway/`：マルチデバイス統合エントリ。API（`ApiRoutes`）、内蔵フロントエンド（`wwwroot/`）、ノードゲートウェイクライアント（`NodeGatewayClient`）、ノードリフレッシュ（`NodeRefreshService`）、ジョブリフレッシュ（`JobRefreshService`）、リバース接続管理（`Reverse/`）、オンライン自己更新（`SelfUpdateService`）、JSON 永続化（`Persistence/`）を含む。
- `DesktopApp/`：Avalonia UI 11 デスクトップクライアント。Views（14ページ）、ViewModels（15ビューモデル）、Services（`BuildRunner` / `ProfileStore` / `ServerSyncService`）、Controls（カスタムコントロール）、Styles（スタイルリソース）を含む。`InternalsVisibleTo` + `Compile Remove` でメインプロジェクトを参照し、全コアロジックを再利用。
- `deploy/`：本番デプロイテンプレート。macOS `launchd` plist、Docker デプロイファイルなど。

## コア設計原則

### パイプライン編成とプラットフォーム機能の分離

`AutomationWorkflow` はステップの編成のみを担当し、Git、Unity、Xcode、Google Play、TikTok の詳細を直接処理しません。プラットフォーム機能を追加する場合は、対応する `Modules/<Platform>/` に配置し、ワークフローから呼び出します。クロスプラットフォーム機能は `Services/` に配置します。現在3種のプラットフォーム Pipeline をサポート：

- `IosBuildPipeline` — Git → Unity → Xcode archive/export → ASC アップロード
- `AndroidBuildPipeline` — Git → Unity → APK/AAB → Google Play アップロード
- `TiktokBuildPipeline` — Git → Unity → WebGL → TikTok オープンプラットフォームアップロード

### 設定エディタのフィールド駆動

設定エディタはフィールド記述子リストでメニューと変更ロジックを駆動します。設定フィールドを追加する場合は、まず `ConfigEditor` のフィールドリストにエントリを追加し、メニュー表示と switch-case 変更ロジックの分散を防ぎます。

### セキュリティ基盤

Web バックエンド、Worker、MCP/Agent に接続する際、すべてのエントリポイントは CLI に既に実装されている事前能力を再利用する必要があります：

- `PathSafetyValidator`：ワークスペース、リポジトリディレクトリ、Unity プロジェクト、成果物、ログ、Xcode 出力、archive/export がすべて許可されたルートディレクトリ内にあることを検証。
- `GitRepositoryPolicyValidator`：Git URL フォーマットと `allowedRepositoryUrls` ホワイトリストを検証。
- `BuildConfigSnapshotWriter`：毎回の実行で `Logs/build-config-snapshot.json` を生成し、設定スナップショット、解決パス、CLI 引数を記録。
- `SensitiveText`：ログ、コマンド、stdout/stderr、設定スナップショット内の一般的なトークン/パスワードを統一的にマスキング。

これらの機能は Web/API 層にのみ配置すべきではありません。Worker がビルドを実行する前にも再呼び出しし、エントリポイントをバイパスして危険な設定を直接トリガーすることを防ぐ必要があります。

## BuildServer アーキテクチャ

BuildServer は CLI の Web/Agent エントリポイントで、以下の設計を採用しています：

### シリアルキュー

シングルマシン、シングル Worker、シリアルキュー設計は意図的なものです。Unity、Xcode、Gradle、署名証明書、キャッシュディレクトリは通常、同じマシンで並行競合に適していません。マルチマシン拡張は LinuxGateway が担当します。

### サービス層

| サービス | ファイル | 責務 |
|------|------|------|
| タスクキュー | `BuildQueueService.cs` | ビルドタスクのエンキュー、デキュー、状態遷移を管理 |
| バックグラウンド Worker | `BuildWorkerService.cs` | キューをシリアル消費し、CLI を呼び出してビルドを実行 |
| メール通知 | `EmailNotificationService.cs` | ビルド完了後に成功/失敗メール通知を送信 |
| 成果物スキャナ | `ArtifactScanner.cs` | タスク成果物ディレクトリをスキャンし、成果物リストを生成 |
| ログリーダー | `LogFileReader.cs` | タスクログの読み取りと tail |
| ストレージクリーンアップ | `StorageCleanupService.cs` | 履歴成果物の手動および自動クリーンアップ |
| メンテナンス | `MaintenanceService.cs` | RetentionDays/MaxArtifactBytes による自動クリーンアップ |
| 自動ロケータ | `AutomationToolLocator.cs` | AutomationUnityBuildIOS CLI 実行ファイルの特定 |

### リバース接続

`BuildServer/Reverse/` ディレクトリは BuildServer が LinuxGateway に自発的に接続する機能を実装し、NAT/イントラネット環境内のノードがパブリック露出なしで LinuxGateway にスケジュールされることを可能にします。

## LinuxGateway アーキテクチャ

LinuxGateway は Unity を実行せず、Unity プロジェクトを保存せず、Apple 証明書も保持しません。以下のみを担当します：

1. Web ログインとデバイス管理。
2. ノード登録（直接接続またはリバース接続）。
3. 各ノードの BuildServer にタスクを転送。
4. ログと成果物のプロキシ。

### サービス層

| サービス | ファイル | 責務 |
|------|------|------|
| ノードゲートウェイクライアント | `NodeGatewayClient.cs` | ノード BuildServer の `/api/gateway/*` エンドポイントを呼び出し |
| ノードリフレッシュ | `NodeRefreshService.cs` | ノードステータスとプロジェクト/設定同期を定期的にリフレッシュ |
| ジョブリフレッシュ | `JobRefreshService.cs` | リモートタスクステータス、ログ、成果物を定期的にリフレッシュ |
| オンライン自己更新 | `SelfUpdateService.cs` | Gitee/GitHub Releases から更新パッケージの確認とダウンロード |

### リバース接続

`LinuxGateway/Reverse/` ディレクトリは BuildServer が自発的に接続する際の Enrollment Token 生成、ノード登録、WebSocket ロングコネクション維持を管理します。

### オンライン自己更新

`SelfUpdateService` がサポート：
- デュアルソース検出（Gitee + GitHub 並行最新バージョンクエリ）。
- tar.gz 更新パッケージのダウンロード。
- `apply-update.sh` スクリプトの生成（バックアップ + 置換 + 再起動を完了）。
- サーバーに .NET SDK 不要。プリコンパイルバイナリのみダウンロード。

## DesktopApp アーキテクチャ

DesktopApp は Avalonia UI 11 + .NET 8 を使用し、プロジェクト参照でメインプロジェクトの全コアロジックを再利用します：

- **InternalsVisibleTo** + **Compile Remove**：メインプロジェクトの csproj に宣言を追加し、DesktopApp が internal メンバーにアクセスできるようにしつつ、Program.cs などのエントリポイントファイルを除外。
- **ProfileStore**：4種類の設定テンプレート（プロジェクト/Unity/署名/証明書）の永続化を統合管理。データは `profiles/` ディレクトリに保存。
- **ServerSyncService**：HttpClient で BuildServer REST API に接続し、テンプレートと設定ファイルの双方向同期を実現。
- **BuildRunner**：CLI 呼び出しをラップし、リアルタイムログ出力とビルド進捗を提供。
- **AvaloniaUseCompiledBindingsByDefault=false**：ランタイムバインディングを使用し、すべての .axaml ファイルで x:DataType を宣言する必要性を回避。

`scripts/verify.ps1` を実行すると基本回帰検証が可能です：コンパイル、ヘルプエントリ、ドライラン、設定エディタの開閉。
