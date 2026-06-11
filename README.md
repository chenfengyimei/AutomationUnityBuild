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

## Unity 工程需要加的脚本

把本项目里的 `UnityBuildScripts/BuildIOS.cs` 复制到你的 Unity 游戏仓库：

```text
Assets/Editor/BuildIOS.cs
```

这个脚本提供 `BuildAutomation.IOSBuilder.Build` 静态方法，控制台工具会通过 Unity 的 `-executeMethod` 调用它。

## 配置

复制模板：

```bash
cp build-ios.sample.json build-ios.json
```

至少改这些字段：

- `repositoryUrl`: Unity 游戏仓库地址，例如 `git@github.com:company/game.git`
- `branch`: 要打包的分支
- `unityVersion` 或 `unityExecutablePath`: Mac 上 Unity 的真实路径
- `teamId`: Apple Developer Team ID
- `bundleIdentifier`: iOS Bundle ID
- `exportMethod`: 本地调试常用 `development`，TestFlight/App Store 按当前 Xcode 支持的导出方式配置

`resetRepository` 默认是 `false`，这样不会清掉 Mac 本地未提交内容。专用打包机想每次强制回到远端分支时再改成 `true`。

## 在 Windows/VS 发布给 Mac 用

Apple Silicon Mac：

```powershell
.\scripts\publish-mac.ps1 -Runtime osx-arm64
```

Intel Mac：

```powershell
.\scripts\publish-mac.ps1 -Runtime osx-x64
```

然后把整个项目目录或 `publish/<runtime>`、`build-ios.json`、`scripts/build-ios.sh` 拷到 Mac。

## 在 Mac 一键打包

首次给脚本执行权限：

```bash
chmod +x scripts/build-ios.sh
```

执行：

```bash
./scripts/build-ios.sh ./build-ios.json
```

也可以直接运行：

```bash
dotnet run --project AutomationUnityBuildIOS.csproj -- run --config build-ios.json
```

打包成功后，产物会在 `artifactsRoot` 下按时间生成，例如：

```text
~/UnityBuildArtifacts/YourUnityGame/20260611-153000/
```

其中包含 Unity 日志、Xcode 日志、`.xcarchive` 和导出的 `.ipa` 目录。

## 常用调试命令

只检查环境：

```bash
./publish/osx-arm64/AutomationUnityBuildIOS doctor --config build-ios.json
```

只打印命令，不执行：

```bash
./publish/osx-arm64/AutomationUnityBuildIOS run --config build-ios.json --dry-run
```

跳过 git，只用当前已拉好的工程：

```bash
./publish/osx-arm64/AutomationUnityBuildIOS run --config build-ios.json --skip-git
```

跳过 Unity，只重新执行 Xcode 打包：

```bash
./publish/osx-arm64/AutomationUnityBuildIOS run --config build-ios.json --skip-unity
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
