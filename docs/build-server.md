# BuildServer 打包平台

BuildServer 是当前自动化打包工具的 Web/Agent 化入口，支持 iOS、Android APK/AAB，以及 Android AAB/APK 上传 Google Play。第一版采用单 Mac、单 Worker、串行队列，避免 Unity、Xcode、Gradle、签名环境并发导致缓存和证书状态混乱。

## 模块

- `BuildServer.Api`: ASP.NET Core Minimal API，负责登录、项目、配置、任务、产物、审计。
- `BuildServer.Worker`: 后台串行 Worker，从队列取任务并调用 `AutomationUnityBuildIOS` CLI。
- `BuildServer.Web`: 内置静态前端，负责人可网页登录发起打包。
- `BuildServer.Mcp`: `/mcp` JSON-RPC 工具入口，给 Agent/AI 使用。
- `buildserver-data`: JSON 持久化目录，保存用户、项目、配置、任务、产物、审计、Worker 节点。

## 本地启动

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-build-server.ps1
```

默认地址：

```text
http://127.0.0.1:5088
```

默认账号：

```text
admin
```

如果没有设置 `BUILD_SERVER_ADMIN_PASSWORD`，服务首次启动会生成随机密码并写入：

```text
<DataRoot>/initial-admin.txt
```

如果没有设置 `BUILD_SERVER_AGENT_TOKEN`，服务首次启动会生成随机 Agent API Key 并写入：

```text
<DataRoot>/initial-agent-token.txt
```

生产环境建议显式设置：

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

安全相关默认值：

- 工作区默认限制在 `~/UnityBuildWorkspace` 下面。
- 产物默认限制在 `~/UnityBuildArtifacts` 下面。
- 配置文件默认限制在 BuildServer 数据目录的 `configs` 和程序目录的 `configs` 下面。
- Git 仓库默认允许 HTTPS/SSH 地址；生产环境建议设置 `BUILD_SERVER_ALLOWED_REPOSITORY_HOSTS`，例如 `github.com` 或公司 Git 服务器域名。
- 如果经过 Nginx/Caddy 等反向代理访问网页，设置 `BUILD_SERVER_PUBLIC_BASE_URL` 和 `BUILD_SERVER_ALLOWED_ORIGINS`，否则跨站请求防护会拒绝来源不一致的写操作。

## Mac 发布

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-build-server-mac.ps1 -Runtime osx-arm64
```

发布后可配合 `deploy/launchd/com.automationunity.buildserver.plist` 作为 `buildbot` 用户启动。证书、描述文件、Unity License、Git SSH Key 都应安装在这个固定 macOS 用户下。

## 必填数据

首次进入后台后：

1. 新增项目：填写项目名、Git 仓库、默认分支、允许分支、工作区、产物目录。
2. 新增配置：选择 iOS 或 Android。可以填写现有配置 JSON 路径，也可以勾选“生成新的配置文件”，在网页里填写 Unity 版本、Bundle ID、平台专属字段后由服务端自动生成 JSON 并登记。
   - iOS 字段包括 Team ID、Deployment Target、Export Method、Signing Style、是否复制 archive 到 Organizer、是否上传 App Store Connect/TestFlight。
   - Android 字段包括 APK/AAB/both、SDK 版本、keystore、Google Play Service Account、track、release status、上传产物。
3. 发起打包：选择项目和配置，提交任务。

BuildServer 会为每个任务生成独立配置快照，预留 Build Number，并调用 CLI：

```text
AutomationUnityBuildIOS run --config <job-config.json>
```

## MCP/Agent

MCP 入口：

```text
POST /mcp
Header: X-Agent-Token: <BUILD_SERVER_AGENT_TOKEN>
```

工具：

- `list_projects`
- `list_configs`
- `start_build`
- `start_ios_build`（兼容旧名称，建议新接入使用 `start_build`）
- `get_build_status`
- `tail_build_log`
- `list_build_artifacts`

默认 Agent 只允许 `dryRun=true`。要允许正式打包，需要在数据中把对应 `McpClientRecord.allowFullBuild` 改为 `true`，并建议只给特定项目授权。MCP 只按项目和配置 ID 发起任务，不允许传任意 Git 仓库或任意路径。

新建配置默认不允许 MCP 使用，需要在网页里显式勾选“允许 MCP 使用”。

## LinuxGateway 节点接口

如果要把这台 Mac/Windows BuildServer 接入 LinuxGateway，可以手动设置：

```bash
export BUILD_SERVER_GATEWAY_TOKEN="强随机 token"
export BUILD_SERVER_NODE_PLATFORMS="ios,android"
```

Windows Android 节点可以使用：

```powershell
$env:BUILD_SERVER_GATEWAY_TOKEN="强随机 token"
$env:BUILD_SERVER_NODE_PLATFORMS="android"
```

也可以不手动设置 `BUILD_SERVER_GATEWAY_TOKEN`。服务启动时会自动生成 `initial-gateway-token.txt` 并在控制台打印可复制的 Gateway Token；之后重启会复用同一个文件，不会重复生成新值。

设置或自动生成后会启用 `/api/gateway/*`，LinuxGateway 用 `X-Gateway-Token` 调用它来读取节点、提交任务、拉日志和产物。

## 安全边界

- Web/MCP 都只创建任务，不直接执行任意 shell。
- Worker 串行执行，同一时间只跑一个任务。
- 项目可限制允许分支。
- CLI 内部继续校验 Git 白名单和路径边界。
- 任务产物下载必须经过登录权限。
- 审计日志记录登录、创建项目、创建配置、提交/取消任务、注册 Worker。
- 维护服务按 `RetentionDays` 和 `MaxArtifactBytes` 清理已完成任务和产物。

## 多 Mac 扩展

当前已落库 `WorkerNodeRecord`，并提供 `/api/workers` 和 `/api/workers/register`。第一版内置 Worker 适合单 Mac；扩展多 Mac 时建议演进为：

```text
中央 BuildServer.Api + 数据库
Mac Worker A/B/C 独立进程
Worker 拉取适合自己的任务
按 Unity/Xcode 版本、项目授权、当前负载调度
```

届时 JSON 持久化应替换为 SQLite/PostgreSQL，避免多机器同时写文件。
