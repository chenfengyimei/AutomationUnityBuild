# 架构说明

这个工具按职责拆分为几个目录：

- `Cli/`：命令入口、命令行参数解析、快捷指令映射。
- `ConsoleUi/`：控制台交互界面，包括初始化向导、配置编辑器、输入提示。
- `Configuration/`：配置模型、配置文件读写、配置文件选择、路径解析、示例配置。
- `Workflow/`：自动化打包主流程编排、运行上下文、运行时配置更新。
- `Services/`：具体业务能力，包括 Git 同步、环境检查、目录准备、Unity 导出、Xcode archive/export。
- `Infrastructure/`：通用基础设施，包括日志、进程执行、路径工具。
- `UnityBuildScripts/`：需要复制到 Unity 项目 `Assets/Editor` 的 Unity Editor 构建脚本。

`AutomationWorkflow` 只负责串联步骤，不直接处理 Git、Unity、Xcode 的细节。新增功能时优先放到对应服务中，再由 workflow 调用。

配置编辑器使用字段描述列表驱动菜单和修改逻辑。新增配置项时，优先在 `ConfigEditor` 的字段列表里补一项，避免菜单显示和 switch 修改逻辑分散在多个地方。

运行 `scripts/verify.ps1` 可以做基础回归验证：编译、帮助入口、dry-run、配置编辑器打开退出。
