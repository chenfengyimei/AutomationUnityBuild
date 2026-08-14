# LinuxGateway マルチノードエントリ

`LinuxGateway` はオプションの中央エントリポイントで、パブリックドメインを持つ Linux サーバーへのデプロイに適しています。Unity を実行せず、Unity プロジェクトを保存せず、Apple 証明書も保持しません。Web ログイン、Mac/Windows ビルドノードの登録、ノード選択、ノードの `BuildServer` へのタスク転送のみを担当します。

LinuxGateway は2種類のノード接続方式をサポートします：直接接続（LinuxGateway がノードにアクセス）とリバース接続（ノードが LinuxGateway に接続、NAT/イントラネット環境向け）。Gitee/GitHub Releases から更新パッケージをダウンロードする内蔵のオンライン自己更新機能も備えており、サーバーに .NET SDK は不要です。

LinuxGateway をデプロイしない場合、Mac/Windows の `BuildServer` は引き続き独立してログイン、設定、ビルドが可能です。

## アーキテクチャ

```text
外部ユーザー
  -> LinuxGateway Web/API
      -> Mac BuildServer /api/gateway/*    iOS + Android
      -> Windows BuildServer /api/gateway/* Android APK/AAB
```

各 Mac/Windows ノードは既存の `BuildServer` を引き続き実行し、LinuxGateway が呼び出すためのトークン保護された API を追加で有効化するのみです。

## Mac/Windows ノード設定

各ノードで `BuildServer` 起動前に設定：

```bash
export BUILD_SERVER_GATEWAY_TOKEN="このノード用の強力なランダムトークン"
export BUILD_SERVER_NODE_PLATFORMS="ios,android"   # Mac で一般的
```

Windows Android ノード：

```powershell
$env:BUILD_SERVER_GATEWAY_TOKEN="このノード用の強力なランダムトークン"
$env:BUILD_SERVER_NODE_PLATFORMS="android"
```

`BUILD_SERVER_GATEWAY_TOKEN` を空のままにすると、ノードの `/api/gateway/*` エンドポイントは有効になりません。

LinuxGateway がノードアドレスにアクセスできる必要があります。例：

```text
https://mac-build.example.com
https://win-build.example.com
```

これらはトンネルアドレス、VPN/イントラネットアドレス、パブリック HTTPS エンドポイントのいずれでも可能です。HTTPS を推奨します。

## LinuxGateway の起動

開発実行：

```bash
./scripts/run-linux-gateway.sh http://127.0.0.1:5090
```

Windows デバッグ：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-linux-gateway.ps1
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

## Linux へのパブリッシュ

Windows から Linux x64 をパブリッシュ：

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

## 利用フロー

1. Mac/Windows ノードで `BuildServer` を起動し、`BUILD_SERVER_GATEWAY_TOKEN` を設定。
2. Linux で `LinuxGateway` を起動。
3. LinuxGateway Web UI にログイン。
4. デバイス追加：
   - デバイス名：例 `Mac Build`
   - BuildServer URL：例 `https://mac-build.example.com`
   - Gateway Token：そのノードの `BUILD_SERVER_GATEWAY_TOKEN`
   - プラットフォーム：Mac：`iOS + Android`、Windows：`Android`
5. デバイスをリフレッシュし、ノードのプロジェクトと設定が表示されることを確認。
6. ビルド送信時にターゲットデバイス、プロジェクト、設定を選択。

## セキュリティ注意事項

- LinuxGateway のデータディレクトリにはノードの Gateway Token が保存されるため、システム権限を制限してください。
- LinuxGateway は HTTPS 経由でのみ公開すべきです。平文 HTTP の直接公開は推奨されません。
- ノードの `/api/gateway/*` は `X-Gateway-Token` のみ受け付けます。トークンを URL に含めないでください。
- ノードの通常の管理バックエンドをパブリックインターネットに公開しないでください。LinuxGateway のみアクセス可能にすることが最善です。
- iOS タスクは `ios` をサポートする Mac ノードにのみ送信可能。Windows ノードは Android APK/AAB のみ適しています。

## リバース接続

リバース接続は、ノードが NAT、ホームネットワーク、企業イントラネットの背後にあり、LinuxGateway がノードアドレスに直接アクセスできない場合に適しています。この場合、BuildServer が LinuxGateway に自発的に接続します。ノード側でパブリックポートの公開は不要です。

### 設定手順

1. LinuxGateway Web UI で Enrollment Token を生成。
2. BuildServer ノードで環境変数を設定：

```bash
export BUILD_SERVER_REVERSE_GATEWAY_ENABLED=true
export BUILD_SERVER_REVERSE_GATEWAY_URL="https://build.example.com"
export BUILD_SERVER_REVERSE_GATEWAY_ENROLLMENT_TOKEN="<token>"
export BUILD_SERVER_REVERSE_NODE_NAME="Mac Build"
```

3. BuildServer を起動すると、LinuxGateway に自動接続し、リバース接続ノードとして登録されます。
4. 接続成功後、LinuxGateway Web UI にノードが表示されます。
5. ノード取り消し後、新しい Enrollment Token を生成して再登録する必要があります。

リバース接続は `LinuxGateway/Reverse/` と `BuildServer/Reverse/` に実装されています。

## オンライン自己更新

LinuxGateway は `SelfUpdateService` を内蔵し、Gitea または GitHub Releases から更新パッケージの確認とダウンロードが可能です。サーバーに .NET SDK は不要です。

### API エンドポイント

| エンドポイント | メソッド | 説明 |
|------|------|------|
| `/api/system/version` | GET | 現在のバージョンを取得 |
| `/api/system/update/check` | GET | 最新バージョンを確認 |
| `/api/system/update/apply` | POST | 更新を適用（Admin のみ） |

### 更新プロセス

1. Gitee/GitHub Release API から並行で最新バージョンをクエリ。
2. tar.gz 更新パッケージをダウンロード。
3. `apply-update.sh` スクリプトを生成し、バックアップ + 置換 + 再起動を完了。

### 設定項目

| 変数 | 説明 |
|------|------|
| `LINUX_GATEWAY_UPDATE_SOURCE` | 更新ソース：`gitee` または `github` |
| `LINUX_GATEWAY_UPDATE_REPO_OWNER` | リポジトリオーナー |
| `LINUX_GATEWAY_UPDATE_REPO_NAME` | リポジトリ名 |

## Docker デプロイ

LinuxGateway は Docker デプロイをサポートしています。CentOS 7 などネイティブ `libstdc++` ランタイムが古い可能性のある環境に特に適しています。詳細は [Docker デプロイガイド](linux-gateway-docker.md) を参照してください。
