# BuildServer プラットフォーム

BuildServer は自動ビルドツールの Web/Agent エントリポイントで、iOS、Android APK/AAB、および Google Play アップロードをサポートします。初版はシングル Mac、シングル Worker、シリアルキューを採用し、Unity、Xcode、Gradle、署名環境の並行によるキャッシュと証明書状態の混乱を回避します。

## モジュール

- `BuildServer.Api`：ASP.NET Core Minimal API。ログイン、プロジェクト、設定、タスク、成果物、監査を担当。
- `BuildServer.Worker`：バックグラウンドシリアル Worker。キューからタスクを取り出し `AutomationUnityBuildIOS` CLI を呼び出す。
- `BuildServer.Web`：内蔵静的フロントエンド。Web ログインとビルド送信を提供。
- `BuildServer.Mcp`：`/mcp` JSON-RPC ツールエンドポイント。Agent/AI 向け。
- `BuildServer.Reverse`：リバース接続モジュール。BuildServer が LinuxGateway に自発的に接続し、NAT/イントラネット環境に対応。
- `buildserver-data`：JSON 永続化ディレクトリ。ユーザー、プロジェクト、設定、タスク、成果物、監査、Worker ノードを保存。

## ローカル起動

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-build-server.ps1
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

`BUILD_SERVER_AGENT_TOKEN` が未設定の場合、初回起動時にランダム Agent API Key を生成：

```text
<DataRoot>/initial-agent-token.txt
```

本番環境での推奨設定：

```bash
export BUILD_SERVER_ADMIN_PASSWORD="strong-password"
export BUILD_SERVER_AGENT_TOKEN="strong-agent-token"
export BUILD_SERVER_PUBLIC_BASE_URL="https://build.example.com"
export BUILD_SERVER_ALLOWED_ORIGINS="https://build.example.com"
export BUILD_SERVER_ALLOWED_WORKSPACE_ROOTS="/Users/build/UnityBuildWorkspace"
export BUILD_SERVER_ALLOWED_ARTIFACTS_ROOTS="/Users/build/UnityBuildArtifacts"
export BUILD_SERVER_ALLOWED_CONFIG_ROOTS="/Users/build/BuildServerData/configs"
export BUILD_SERVER_ALLOWED_REPOSITORY_HOSTS="github.com"
```

セキュリティ関連のデフォルト値：

- ワークスペースはデフォルトで `~/UnityBuildWorkspace` に制限。
- 成果物はデフォルトで `~/UnityBuildArtifacts` に制限。
- 設定ファイルはデフォルトで BuildServer データディレクトリの `configs` とプログラムディレクトリの `configs` に制限。
- Git リポジトリはデフォルトで HTTPS/SSH URL を許可。本番環境では `BUILD_SERVER_ALLOWED_REPOSITORY_HOSTS` を設定することを推奨（例：`github.com` や社内 Git サーバードメイン）。
- Nginx/Caddy などのリバースプロキシ経由で Web UI にアクセスする場合、`BUILD_SERVER_PUBLIC_BASE_URL` と `BUILD_SERVER_ALLOWED_ORIGINS` を設定しないと、オリジン不一致の書き込み操作がクロスサイトリクエスト保護により拒否されます。

## Mac へのパブリッシュ

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-build-server-mac.ps1 -Runtime osx-arm64
```

パブリッシュ後、`deploy/launchd/com.automationunity.buildserver.plist` で `buildbot` ユーザーとして起動可能。証明書、プロビジョニングプロファイル、Unity License、Git SSH キーはすべてこの固定 macOS ユーザーの下にインストールしてください。

## 必須データ

初回ログイン後：

1. プロジェクト追加：プロジェクト名、Git リポジトリ、デフォルトブランチ、許可ブランチ、ワークスペース、成果物ディレクトリを入力。
2. 設定追加：iOS または Android を選択。既存の設定 JSON ファイルパスを指定するか、「新しい設定ファイルを生成」にチェックを入れ、Web フォームで Unity バージョン、Bundle ID、プラットフォーム固有フィールドを入力し、サーバー側で JSON を自動生成して登録できます。
   - iOS フィールド：Team ID、Deployment Target、Export Method、Signing Style、archive の Organizer へコピーの有無、App Store Connect/TestFlight アップロードの有無。
   - Android フィールド：APK/AAB/both、SDK バージョン、keystore、Google Play Service Account、track、release status、アップロード成果物。
3. ビルド開始：プロジェクトと設定を選択し、タスクを送信。

BuildServer は各タスクの独立した設定スナップショットを生成し、Build Number を予約し、CLI を呼び出します：

```text
AutomationUnityBuildIOS run --config <job-config.json>
```

## MCP/Agent

MCP エンドポイント：

```text
POST /mcp
Header: X-Agent-Token: <BUILD_SERVER_AGENT_TOKEN>
```

ツール：

- `list_projects`
- `list_configs`
- `start_build`
- `start_ios_build`（旧名称、新規統合では `start_build` を推奨）
- `get_build_status`
- `tail_build_log`
- `list_build_artifacts`

デフォルトでは Agent は `dryRun=true` のみ許可されます。実ビルドを許可するには、データ内の対応する `McpClientRecord.allowFullBuild` を `true` に設定し、特定のプロジェクトのみを承認することを推奨します。MCP はプロジェクトと設定 ID でのみタスクを送信し、任意の Git リポジトリやパスを渡すことはできません。

新規設定はデフォルトで MCP 使用不可。Web UI で明示的に「MCP 使用を許可」にチェックを入れる必要があります。

## メール通知

BuildServer は内蔵のメール通知サービス（`EmailNotificationService`）を備え、ビルドタスク完了後に自動でメールを送信します：

- **ビルド成功**：メールにビルド成果物パス、経過時間、設定サマリーを含む。
- **ビルド失敗**：メールに失敗ステップ、エラーサマリー、ログパスを含む。

SMTP 465 暗黙 SSL、連絡先リスト、カスタマイズされたメールテンプレートをサポート。Web バックエンドまたは DesktopApp のメール通知ページで SMTP サーバー、ポート、送信元認証情報、連絡先リストを設定します。

## ストレージ管理

ビルドタスクが蓄積すると、成果物が徐々にディスク容量を消費します。BuildServer は2つのストレージ管理メカニズムを提供します：

- **自動クリーンアップ**：`MaintenanceService` が `RetentionDays` と `MaxArtifactBytes` に基づいて完了タスクと成果物を自動クリーンアップ。
- **手動クリーンアップ**：Web バックエンドまたは DesktopApp のストレージ管理ページでストレージ概要を確認し、一括削除または単一削除が可能。

`StorageCleanupService` が実際の成果物ディレクトリのスキャンと削除を担当します。

## リバース接続

BuildServer ノードが NAT、ホームネットワーク、企業イントラネットの背後にあり、LinuxGateway がノードアドレスに直接アクセスできない場合、リバース接続で BuildServer が LinuxGateway に自発的に接続できます。

LinuxGateway Web UI で Enrollment Token を生成し、環境変数で BuildServer を設定：

```bash
export BUILD_SERVER_REVERSE_GATEWAY_ENABLED=true
export BUILD_SERVER_REVERSE_GATEWAY_URL="https://build.example.com"
export BUILD_SERVER_REVERSE_GATEWAY_ENROLLMENT_TOKEN="<token>"
export BUILD_SERVER_REVERSE_NODE_NAME="Mac Build"
```

接続成功後、ノード認証情報は BuildServer データディレクトリに保存されます。`BuildServer/Reverse/` ディレクトリがリバース接続のクライアントロジックを実装しています。

## セキュリティ境界

- Web/MCP はタスクの作成のみを行い、任意のシェルコマンドを直接実行しません。
- Worker はシリアル実行で、同時に1つのタスクのみ実行。
- プロジェクトは許可ブランチを制限可能。
- CLI は内部で Git ホワイトリストとパス境界の検証を継続。
- タスク成果物のダウンロードにはログイン認証が必要。
- 監査ログはログイン、プロジェクト作成、設定作成、タスク送信/キャンセル、Worker 登録を記録。
- メンテナンスサービスが `RetentionDays` と `MaxArtifactBytes` で完了タスクと成果物をクリーンアップ。
- メール通知内の機密情報（パスワード、トークン）はエコーされず、SMTP 認証にのみ使用。

## マルチ Mac 拡張

`WorkerNodeRecord` は既に永続化されており、`/api/workers` と `/api/workers/register` が提供されています。初版の内蔵 Worker はシングル Mac 向け。マルチ Mac 拡張時の推奨される進化：

```text
中央 BuildServer.Api + データベース
Mac Worker A/B/C を独立プロセスとして配置
Worker が自身に適したタスクをプル
Unity/Xcode バージョン、プロジェクト承認、現在の負荷でスケジューリング
```

その際、JSON 永続化は SQLite/PostgreSQL に置き換え、マシン間での同時ファイル書き込みを回避すべきです。
