# 架构说明

本项目采用模块化、分层设计，核心打包引擎与平台入口完全解耦。CLI、BuildServer、DesktopApp 和 LinuxGateway 共享同一套核心逻辑，差异仅在入口层和交互方式上。

## 目录职责

这个工具按职责拆分为几个目录：

- `Cli/`：命令入口、命令行参数解析、快捷指令映射（`ShortcutCommands`）。
- `ConsoleUi/`：控制台交互界面，包括初始化向导、配置编辑器、输入提示。
- `Configuration/`：配置模型、配置文件读写、配置文件选择、路径解析、示例配置。支持 `ios`、`android`、`tiktok` 三种平台配置。
- `Workflow/`：自动化打包主流程编排、运行上下文、运行时配置更新、配置快照。
- `Services/`：跨平台共享业务能力，包括 Git 同步、环境检查、目录准备、Unity 工程校验、路径安全校验。
- `Modules/Common/`：平台模块共享能力，包括平台 Pipeline 接口、Unity 命令参数构建、Unity 日志诊断、Unity metadata 读取。
- `Modules/Ios/`：iOS 专属打包能力，包括 Unity 导出 Xcode 工程、Xcode project/workspace 定位、`xcodebuild archive/export`。
- `Modules/Android/`：Android 专属打包能力，包括 Unity 构建 APK/AAB、Google Play Publishing API 上传；`GooglePlay/` 子目录承载 HTTP API、OAuth、Service Account 等细节。
- `Modules/Tiktok/`：TikTok 小游戏专属能力，包括 WebGL 构建流水线（`TiktokBuildPipeline`）、构建服务（`TiktokBuildService`）和 TikTok 开放平台 API 上传（`TiktokUploadService`）。与 iOS/Android 完全独立，不影响已有流程。
- `Infrastructure/`：通用基础设施，包括日志（`BuildLogger`）、进程执行（`ProcessRunner`）、路径工具（`PathTools`）、路径安全边界（`PathSafety`）、敏感信息脱敏。这些能力被 CLI、BuildServer 和 DesktopApp 共同复用。
- `UnityBuildScripts/Ios/`：需要复制到 Unity 项目 `Assets/Editor` 的 iOS Unity Editor 构建脚本。
- `UnityBuildScripts/Android/`：需要复制到 Unity 项目 `Assets/Editor` 的 Android Unity Editor 构建脚本。
- `BuildServer/`：Web 打包平台，包含 API（`ApiRoutes`）、内置前端（`wwwroot/`）、后台 Worker（`BuildWorkerService`）、MCP/Agent 入口（`McpEndpoint`）、Gateway 节点接口（`GatewayEndpoint`）、邮件通知（`EmailNotificationService`）、存储管理（`StorageCleanupService`）、产物扫描（`ArtifactScanner`）、维护清理（`MaintenanceService`）、反向连接（`Reverse/`）和 JSON 持久化（`Persistence/`）。
- `LinuxGateway/`：多设备统一入口，包含 API（`ApiRoutes`）、内置前端（`wwwroot/`）、节点网关客户端（`NodeGatewayClient`）、节点刷新（`NodeRefreshService`）、任务刷新（`JobRefreshService`）、反向连接管理（`Reverse/`）、在线自更新（`SelfUpdateService`）和 JSON 持久化（`Persistence/`）。
- `DesktopApp/`：Avalonia UI 11 桌面客户端，包含 Views（14 个页面）、ViewModels（15 个视图模型）、Services（`BuildRunner` / `ProfileStore` / `ServerSyncService`）、Controls（自定义控件）和 Styles（样式资源）。通过 `InternalsVisibleTo` + `Compile Remove` 引用主项目，复用全部核心逻辑。
- `deploy/`：生产部署模板，例如 macOS `launchd` plist、Docker 部署文件。

## 核心设计原则

### 流程编排与平台能力分离

`AutomationWorkflow` 只负责串联步骤，不直接处理 Git、Unity、Xcode、Google Play 或 TikTok 的细节。新增平台能力时优先放到对应 `Modules/<Platform>/` 中，再由 workflow 调用；跨平台能力才放到 `Services/`。当前已支持三种平台 Pipeline：

- `IosBuildPipeline` — Git → Unity → Xcode archive/export → ASC 上传
- `AndroidBuildPipeline` — Git → Unity → APK/AAB → Google Play 上传
- `TiktokBuildPipeline` — Git → Unity → WebGL → TikTok 开放平台上传

### 配置编辑器字段驱动

配置编辑器使用字段描述列表驱动菜单和修改逻辑。新增配置项时，优先在 `ConfigEditor` 的字段列表里补一项，避免菜单显示和 switch 修改逻辑分散在多个地方。

### 安全边界贯穿全链路

后续接 Web 后端、Worker、MCP/Agent 时，所有入口都应该复用当前 CLI 已经落地的前置能力：

- `PathSafetyValidator`：校验 workspace、仓库目录、Unity 工程、产物、日志、Xcode 输出、archive/export 都在允许根目录内。
- `GitRepositoryPolicyValidator`：校验 Git URL 格式和 `allowedRepositoryUrls` 白名单。
- `BuildConfigSnapshotWriter`：每次正式运行生成 `Logs/build-config-snapshot.json`，记录配置快照、解析路径和 CLI 参数。
- `SensitiveText`：统一脱敏日志、命令、stdout/stderr 和配置快照中的常见 Token/密码。

这些能力不应该只放在 Web/API 层。Worker 真正执行打包前也必须再次调用，避免绕过入口直接触发危险配置。

## BuildServer 架构

BuildServer 是 CLI 的 Web/Agent 化入口，采用以下设计：

### 串行队列

单机、单 Worker、串行队列设计是有意为之：Unity、Xcode、Gradle、签名证书和缓存目录通常不适合在同一台机器上并发抢占。多机器扩展由 LinuxGateway 负责。

### 服务层

| 服务 | 文件 | 职责 |
|------|------|------|
| 任务队列 | `BuildQueueService.cs` | 管理构建任务入队、出队、状态流转 |
| 后台 Worker | `BuildWorkerService.cs` | 串行消费队列，调用 CLI 执行打包 |
| 邮件通知 | `EmailNotificationService.cs` | 构建完成后发送成功/失败邮件通知 |
| 产物扫描 | `ArtifactScanner.cs` | 扫描任务产物目录，生成产物列表 |
| 日志读取 | `LogFileReader.cs` | 读取并 tail 任务日志 |
| 存储清理 | `StorageCleanupService.cs` | 手动和自动清理历史产物 |
| 维护清理 | `MaintenanceService.cs` | 按 RetentionDays/MaxArtifactBytes 自动清理 |
| 自动定位 | `AutomationToolLocator.cs` | 定位 AutomationUnityBuildIOS CLI 可执行文件 |

### 反向连接

`BuildServer/Reverse/` 目录实现 BuildServer 主动连接 LinuxGateway 的能力，用于节点在 NAT/内网环境下无需公网暴露即可被 LinuxGateway 调度。

## LinuxGateway 架构

LinuxGateway 不直接运行 Unity、不保存 Unity 项目、不持有 Apple 证书。它只负责：

1. 网页登录和设备管理。
2. 登记节点（直接连接或反向连接）。
3. 把任务转发给节点上的 BuildServer。
4. 代理日志和产物。

### 服务层

| 服务 | 文件 | 职责 |
|------|------|------|
| 节点网关客户端 | `NodeGatewayClient.cs` | 调用节点 BuildServer 的 `/api/gateway/*` 接口 |
| 节点刷新 | `NodeRefreshService.cs` | 定期刷新节点状态和项目/配置同步 |
| 任务刷新 | `JobRefreshService.cs` | 定期刷新远程任务状态、日志和产物 |
| 在线自更新 | `SelfUpdateService.cs` | 从 Gitee/GitHub Release 检查并下载更新包 |

### 反向连接

`LinuxGateway/Reverse/` 目录管理 BuildServer 主动连接的 Enrollment Token 生成、节点注册和 WebSocket 长连接维护。

### 在线自更新

`SelfUpdateService` 支持：
- 双源检测（Gitee + GitHub 并行查询最新版本）。
- 下载 tar.gz 更新包。
- 生成 `apply-update.sh` 脚本完成备份 + 替换 + 重启。
- 服务器无需 .NET SDK，只下载预编译二进制。

## DesktopApp 架构

DesktopApp 使用 Avalonia UI 11 + .NET 8，通过项目引用复用主项目全部核心逻辑：

- **InternalsVisibleTo** + **Compile Remove**：主项目 csproj 追加声明，让 DesktopApp 访问 internal 成员同时排除 Program.cs 等入口文件。
- **ProfileStore**：统一管理四类配置模板（项目/工程/签名/证书）的持久化，数据存储在 `profiles/` 目录下。
- **ServerSyncService**：通过 HttpClient 连接 BuildServer REST API，实现模板和配置文件的双向同步。
- **BuildRunner**：封装 CLI 调用，提供实时日志输出和打包进度。
- **AvaloniaUseCompiledBindingsByDefault=false**：使用运行时绑定，避免每个 .axaml 文件都需要显式声明 x:DataType。

运行 `scripts/verify.ps1` 可以做基础回归验证：编译、帮助入口、dry-run、配置编辑器打开退出。
