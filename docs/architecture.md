# 架构说明

这个工具按职责拆分为几个目录：

- `Cli/`：命令入口、命令行参数解析、快捷指令映射。
- `ConsoleUi/`：控制台交互界面，包括初始化向导、配置编辑器、输入提示。
- `Configuration/`：配置模型、配置文件读写、配置文件选择、路径解析、示例配置。
- `Workflow/`：自动化打包主流程编排、运行上下文、运行时配置更新、配置快照。
- `Services/`：跨平台共享业务能力，包括 Git 同步、环境检查、目录准备、Unity 工程校验、路径安全校验。
- `Modules/Common/`：平台模块共享能力，包括平台 Pipeline 接口、Unity 命令参数构建、Unity 日志诊断、Unity metadata 读取。
- `Modules/Ios/`：iOS 专属打包能力，包括 Unity 导出 Xcode 工程、Xcode project/workspace 定位、`xcodebuild archive/export`。
- `Modules/Android/`：Android 专属打包能力，包括 Unity 构建 APK/AAB、Google Play Publishing API 上传；`GooglePlay/` 子目录承载 HTTP API、OAuth、Service Account 等细节。
- `Infrastructure/`：通用基础设施，包括日志、进程执行、路径工具、路径安全边界、敏感信息脱敏。
- `UnityBuildScripts/Ios/`：需要复制到 Unity 项目 `Assets/Editor` 的 iOS Unity Editor 构建脚本。
- `UnityBuildScripts/Android/`：需要复制到 Unity 项目 `Assets/Editor` 的 Android Unity Editor 构建脚本。
- `BuildServer/`：Web 打包平台，包含 API、内置前端、后台 Worker、MCP/Agent 入口和 JSON 持久化。
- `deploy/`：生产部署模板，例如 macOS `launchd` plist。

`AutomationWorkflow` 只负责串联步骤，不直接处理 Git、Unity、Xcode 或 Google Play 的细节。新增平台能力时优先放到对应 `Modules/<Platform>/` 中，再由 workflow 调用；跨平台能力才放到 `Services/`。

配置编辑器使用字段描述列表驱动菜单和修改逻辑。新增配置项时，优先在 `ConfigEditor` 的字段列表里补一项，避免菜单显示和 switch 修改逻辑分散在多个地方。

运行 `scripts/verify.ps1` 可以做基础回归验证：编译、帮助入口、dry-run、配置编辑器打开退出。

## 平台化前置能力

后续接 Web 后端、Worker、MCP/Agent 时，所有入口都应该复用当前 CLI 已经落地的前置能力：

- `PathSafetyValidator`：校验 workspace、仓库目录、Unity 工程、产物、日志、Xcode 输出、archive/export 都在允许根目录内。
- `GitRepositoryPolicyValidator`：校验 Git URL 格式和 `allowedRepositoryUrls` 白名单。
- `BuildConfigSnapshotWriter`：每次正式运行生成 `Logs/build-config-snapshot.json`，记录配置快照、解析路径和 CLI 参数。
- `SensitiveText`：统一脱敏日志、命令、stdout/stderr 和配置快照中的常见 Token/密码。

这些能力不应该只放在 Web/API 层。Worker 真正执行打包前也必须再次调用，避免绕过入口直接触发危险配置。
