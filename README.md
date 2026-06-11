# Unity iOS 自动化打包工作流

这个控制台项目用于在 Mac 上一键完成：

1. 用 `git` 拉取或更新 Unity 游戏仓库。
2. 用 Unity BatchMode 导出 iOS Xcode 工程。
3. 用 `xcodebuild archive` 生成 `.xcarchive`。
4. 用 `xcodebuild -exportArchive` 导出 `.ipa` 或对应分发产物。

Windows 负责开发和发布这个 C# 工具；真正的 iOS 打包必须在 macOS 上跑，因为 Unity iOS Build Support 和 Xcode 只能在 Mac 侧完成最终构建。

## Mac 准备

- 安装 Xcode，并至少打开一次完成许可与组件安装。
- 安装 Unity Hub、对应 Unity Editor 版本，以及 iOS Build Support 模块。
- Mac 能访问你的 Git 仓库，推荐提前配置 SSH key。
- Apple Developer 账号、证书、描述文件或自动签名权限已在 Xcode 中配置好。
- 如果不发布 self-contained 可执行文件，Mac 还需要安装 .NET 8 SDK。

可先在 Mac 上检查：

```bash
git --version
xcodebuild -version
/Applications/Unity/Hub/Editor/<UnityVersion>/Unity.app/Contents/MacOS/Unity -version
```

## Mac 首次打开被拦截

如果 Mac 提示“无法验证是不是恶意软件”或“不明开发者”，这是因为这个工具是你自己从 Windows 发布出来的，没有 Apple 开发者签名。首次拷到 Mac 后，在工具目录执行：

```bash
chmod +x scripts/fix-mac-gatekeeper.sh
./scripts/fix-mac-gatekeeper.sh
```

如果你已经 `cd` 到了 `publish` 文件夹，直接执行：

```bash
xattr -cr .
chmod +x ./osx-arm64/AutomationUnityBuildIOS
codesign --force --deep --sign - ./osx-arm64/AutomationUnityBuildIOS
./osx-arm64/AutomationUnityBuildIOS 00
```

Intel Mac 把 `osx-arm64` 换成 `osx-x64`。

也可以手动处理：

```bash
chmod +x ./publish/osx-arm64/AutomationUnityBuildIOS
xattr -dr com.apple.quarantine ./publish/osx-arm64/AutomationUnityBuildIOS
codesign --force --deep --sign - ./publish/osx-arm64/AutomationUnityBuildIOS
```

Intel Mac 把 `osx-arm64` 换成 `osx-x64`。

处理完再运行：

```bash
./publish/osx-arm64/AutomationUnityBuildIOS 00
```

## Unity 工程需要加的脚本

把本项目里的 `UnityBuildScripts/BuildIOS.cs` 复制到你的 Unity 游戏仓库：

```text
Assets/Editor/BuildIOS.cs
```

这个脚本提供 `BuildAutomation.IOSBuilder.Build` 静态方法，控制台工具会通过 Unity 的 `-executeMethod` 调用它。

## 初始化配置

现在不需要手动复制模板再编辑 JSON。直接运行：

```bash
./publish/osx-arm64/AutomationUnityBuildIOS init-config
```

Windows/VS 里直接 F5 启动后，也可以在菜单里选择：

```text
1. 初始化新配置
```

向导会依次询问这些信息，并自动生成填好的配置文件：

- 配置名称和保存路径，例如 `configs/build-ios.dev.json`
- Git 仓库地址和分支，推荐填 `https://github.com/company/game.git` 或 `git@github.com:company/game.git`
- Unity 工程相对路径，必须是包含 `Assets` 和 `ProjectSettings` 的目录；仓库根目录就是 Unity 工程时填 `.`
- Unity 版本或 Unity 可执行文件路径
- Product Name、Bundle Identifier、版本号、构建号
- Apple Developer Team ID，必须是 10 位字母数字，不是公司名
- Xcode 导出方式，例如 `development`、`ad-hoc`、`app-store`
- 工作区目录和产物输出目录
- 是否允许 Xcode 自动处理签名
- 是否每次打包前强制重置 Git 仓库

最容易填错的三个地方：

```text
Git 仓库地址: 不要填网页标题，填 clone 地址。
Unity 工程相对路径: 不要填 build、Builds、XcodeProject，通常填 .
Apple Team ID: 不要填公司名，填 10 位 Team ID。
```

路径创建规则：

```text
Mac 工作区目录、产物目录、日志目录、Xcode 输出目录：不用提前创建，工具会自动创建。
Unity 工程目录：不能自动创建，必须是 Git 仓库里真实存在的 Unity 项目目录。
```

Git 新拉下来的 Unity 项目不需要先手动打开。Unity 命令行第一次运行时会自动导入资源并生成 `Library`，只是第一次会比较慢。前提是 Unity 版本正确、iOS Build Support 已安装、Unity License 已激活。

生成一次后，后续只需要选择这个配置文件，不需要重复填写。

如果只是想生成空模板，仍然可以用：

```bash
./publish/osx-arm64/AutomationUnityBuildIOS init-config --config build-ios.json --template
```

## 选择配置打包

如果不传 `--config`，程序会自动列出已有配置文件让你选择：

```bash
./publish/osx-arm64/AutomationUnityBuildIOS run
```

也可以使用数字快捷指令，不用记完整命令：

```bash
./publish/osx-arm64/AutomationUnityBuildIOS 00
./publish/osx-arm64/AutomationUnityBuildIOS 01
./publish/osx-arm64/AutomationUnityBuildIOS 05
./publish/osx-arm64/AutomationUnityBuildIOS 06 --config configs/build-ios.dev.json
```

快捷指令表：

```text
00  显示帮助和快捷指令表
01  初始化配置向导，问答生成可直接使用的配置文件
02  生成空配置模板 build-ios.json
03  查看已有配置文件
04  选择配置并检查环境
05  选择配置并预览完整打包命令 dry-run
06  选择配置并执行完整打包流程
07  选择配置打包，但跳过 Git 同步
08  选择配置打包，但跳过 Unity 导出
09  选择配置打包，但跳过 Xcode 编译导出
```

也可以明确指定：

```bash
./publish/osx-arm64/AutomationUnityBuildIOS run --config configs/build-ios.dev.json
```

查看已有配置：

```bash
./publish/osx-arm64/AutomationUnityBuildIOS list-configs
```

只检查环境：

```bash
./publish/osx-arm64/AutomationUnityBuildIOS doctor
```

只预览命令，不真正执行：

```bash
./publish/osx-arm64/AutomationUnityBuildIOS run --config configs/build-ios.dev.json --dry-run --verbose
```

## 在 Windows/VS 发布给 Mac 用

Apple Silicon Mac：

```powershell
.\scripts\publish-mac.ps1 -Runtime osx-arm64
```

Intel Mac：

```powershell
.\scripts\publish-mac.ps1 -Runtime osx-x64
```

然后把整个项目目录或 `publish/<runtime>`、`configs/`、`scripts/build-ios.sh` 拷到 Mac。

## 在 Mac 一键打包

首次给脚本执行权限：

```bash
chmod +x scripts/build-ios.sh
```

指定配置执行：

```bash
./scripts/build-ios.sh ./configs/build-ios.dev.json
```

也可以直接运行可执行文件并选择配置：

```bash
./publish/osx-arm64/AutomationUnityBuildIOS run
```

打包成功后，产物会在 `artifactsRoot` 下按时间生成，例如：

```text
~/UnityBuildArtifacts/YourUnityGame/20260611-153000/
```

其中包含 Unity 日志、Xcode 日志、`.xcarchive` 和导出的 `.ipa` 目录。

## 常用调试命令

跳过 Git，只用 Mac 本地已有项目：

```bash
./publish/osx-arm64/AutomationUnityBuildIOS run --config configs/build-ios.dev.json --skip-git
```

跳过 Unity，只重新执行 Xcode 打包：

```bash
./publish/osx-arm64/AutomationUnityBuildIOS run --config configs/build-ios.dev.json --skip-unity
```

## 日志系统

每次运行都会在本次产物目录下生成 `Logs/`，例如：

```text
~/UnityBuildArtifacts/YourUnityGame/20260611-153000/Logs/
```

主要日志文件：

- `automation.log`: 总流程日志，包含时间、步骤、命令、工作目录、耗时、失败原因。
- `unity-editor.log`: Unity Editor 自己写出的构建日志，对应 Unity 的 `-logFile`。
- `unity-process.log`: 启动 Unity 进程时捕获到的 stdout/stderr。
- `xcode-archive.log`: `xcodebuild archive` 的完整输出。
- `xcode-export.log`: `xcodebuild -exportArchive` 的完整输出。

排查顺序建议：

1. 先打开 `automation.log`，看最后一个 `FAIL` 或失败命令。
2. 如果失败在 Unity 阶段，再看 `unity-editor.log`。
3. 如果失败在 Xcode 阶段，再看 `xcode-archive.log` 或 `xcode-export.log`。

日志格式类似：

```text
[2026-06-11 16:18:40.087 +08:00] [STEP] START 检查环境
[2026-06-11 16:18:40.110 +08:00] [DRYRUN] git --version
[2026-06-11 16:18:40.111 +08:00] [STEP] DONE 检查环境 (00:00.022)
```
