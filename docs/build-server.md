# BuildServer 打包平台

BuildServer 是当前自动化打包工具的 Web/Agent 化入口，第一版采用单 Mac、单 Worker、串行队列，避免 Unity/Xcode 并发导致缓存和签名状态混乱。

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
admin / admin123
```

生产环境必须通过环境变量修改：

```bash
export BUILD_SERVER_ADMIN_PASSWORD="strong-password"
export BUILD_SERVER_AGENT_TOKEN="strong-agent-token"
```

## Mac 发布

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-build-server-mac.ps1 -Runtime osx-arm64
```

发布后可配合 `deploy/launchd/com.automationunity.buildserver.plist` 作为 `buildbot` 用户启动。证书、描述文件、Unity License、Git SSH Key 都应安装在这个固定 macOS 用户下。

## 必填数据

首次进入后台后：

1. 新增项目：填写项目名、Git 仓库、默认分支、允许分支、工作区、产物目录。
2. 新增配置：选择项目，填写配置名和现有 `build-ios.xxx.json` 路径。
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
- `start_ios_build`
- `get_build_status`
- `tail_build_log`
- `list_build_artifacts`

默认 Agent 只允许 `dryRun=true`。要允许正式打包，需要在数据中把对应 `McpClientRecord.allowFullBuild` 改为 `true`，并建议只给特定项目授权。

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
