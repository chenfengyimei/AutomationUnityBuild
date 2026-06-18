# Unity iOS / Android 自动化打包工作流

这个控制台项目用于在 Mac 上一键完成：

1. 用 `git` 拉取或更新 Unity 游戏仓库。
2. 用 Unity BatchMode 导出 iOS Xcode 工程。
3. 用 `xcodebuild archive` 生成 `.xcarchive`。
4. 用 `xcodebuild -exportArchive` 导出 `.ipa` 或对应分发产物。
5. 可选：用 App Store Connect API Key 自动上传到 App Store Connect/TestFlight。

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

如果 Mac 提示“无法验证是不是恶意软件”或“不明开发者”，这是因为这个工具是你自己从 Windows 发布出来的，没有 Apple 开发者签名。

推荐做法是：把 Windows 发布出来的 `publish/osx-arm64` 整个文件夹拷到 Mac，例如放到 `~/Downloads/publish_m1`，然后进入这个发布目录执行：

```bash
cd ~/Downloads/publish_m1
xattr -cr .
chmod +x ./AutomationUnityBuildIOS
codesign --force --deep --sign - ./AutomationUnityBuildIOS
./AutomationUnityBuildIOS 00
```

`00` 会打开帮助和快捷指令菜单。Apple Silicon Mac 使用 `osx-arm64` 发布包；Intel Mac 使用 `osx-x64` 发布包，但进入发布目录后的命令一样。

## Unity 工程需要加的脚本

把本项目里的 `UnityBuildScripts/Ios/BuildIOS.cs` 复制到你的 Unity 游戏仓库：

```text
Assets/Editor/BuildIOS.cs
```

这个脚本提供 `BuildAutomation.IOSBuilder.Build` 静态方法，控制台工具会通过 Unity 的 `-executeMethod` 调用它。脚本还会在导出成功后写出 `unity-build-metadata.json`，用于把 Unity 项目里的实际版本号同步回配置文件；更新本工具后，也要同步更新 Unity 仓库里的这个脚本。

## Android APK/AAB 打包和 Google Play 上传

如果要打 Android APK/AAB，把本项目里的 `UnityBuildScripts/Android/BuildAndroid.cs` 复制到 Unity 游戏仓库：

```text
Assets/Editor/BuildAndroid.cs
```

这个脚本提供 `BuildAutomation.AndroidBuilder.Build`，支持 `androidBuildFormat` 为 `apk`、`aab`、`both`。Android 配置模板可以这样生成：

```bash
./AutomationUnityBuildIOS init-config --template --platform android
# 或快捷指令
./AutomationUnityBuildIOS 11
```

Android 配置使用 `buildPlatform: "android"`。常用字段：

```json
{
  "buildPlatform": "android",
  "unityBuildMethod": "BuildAutomation.AndroidBuilder.Build",
  "androidBuildFormat": "both",
  "googlePlayUploadEnabled": false,
  "googlePlayTrack": "internal",
  "googlePlayUploadArtifact": "aab"
}
```

开启 Google Play 上传时，需要在 Google Play Console 配好 Service Account 权限，并在配置里填写 `googlePlayServiceAccountJsonPath`。工具会按 `edits.insert -> bundles/apks.upload -> tracks.update -> edits.commit` 的流程上传；建议先用：

```bash
./AutomationUnityBuildIOS run --config configs/build-android.release.json --dry-run --verbose
```

确认流程和路径无误后再正式执行。

## 初始化配置

现在不需要手动复制模板再编辑 JSON。直接运行：

```bash
./AutomationUnityBuildIOS 01
```

也可以使用完整命令：

```bash
./AutomationUnityBuildIOS init-config
```

向导会依次询问这些信息，并自动生成填好的配置文件：

- 配置名称和保存路径，例如配置名 `dev`、路径 `configs/build-ios.dev.json`
- Git 仓库地址和分支，推荐填 `https://github.com/company/game.git` 或 `git@github.com:company/game.git`
- Unity 工程相对路径，必须是包含 `Assets` 和 `ProjectSettings` 的目录；仓库根目录就是 Unity 工程时填 `.`
- Unity 版本或 Unity 可执行文件路径
- Product Name、Bundle Identifier、版本号是否同步 Unity 项目、构建号、是否自动增加 Build Number、iOS Deployment Target
- Apple Developer Team ID，必须是 10 位字母数字，不是公司名
- Xcode 导出方式，例如 `development`、`ad-hoc`、`app-store`
- 工作区目录和产物输出目录
- 是否允许 Xcode 自动处理签名
- 是否复制 `.xcarchive` 到 Xcode Organizer
- 如果 `exportMethod=app-store`，可选择是否自动上传到 App Store Connect/TestFlight
- 是否每次打包前强制重置 Git 仓库
- 强制重置 Git 时是否保留 Unity `Library` 缓存，避免每次重新导入资源

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

## iOS 自动上传 App Store Connect / TestFlight

默认流程会生成 `.xcarchive`、导出 `.ipa`，并可复制到 Xcode Organizer。Xcode Organizer 里的上传按钮本身仍然是手动操作；如果要无人值守上传，需要在 iOS 配置里开启：

```json
{
  "exportMethod": "app-store",
  "appStoreConnectUploadEnabled": true,
  "appStoreConnectApiKeyPath": "~/Secrets/AuthKey_XXXXXXXXXX.p8",
  "appStoreConnectApiKeyId": "XXXXXXXXXX",
  "appStoreConnectApiIssuerId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
}
```

说明：

- `appStoreConnectApiKeyPath` 是 Mac 打包机本地的 `.p8` 文件路径。
- `appStoreConnectApiKeyId` 是 App Store Connect API Key 的 Key ID。
- `appStoreConnectApiIssuerId` 是 App Store Connect API 页面显示的 Issuer ID。
- 开启上传时 `exportMethod` 必须是 `app-store`。
- 上传成功后，构建会进入 App Store Connect/TestFlight 的处理队列；是否提交审核、是否发布生产环境，仍需要按 App Store Connect 的版本和测试策略处理。

如果只是想生成空模板，仍然可以用：

```bash
./AutomationUnityBuildIOS init-config --config build-ios.json --template
```

## 选择配置打包

如果不传 `--config`，程序会自动列出已有配置文件让你选择：

```bash
./AutomationUnityBuildIOS run
```

也可以使用数字快捷指令，不用记完整命令：

```bash
./AutomationUnityBuildIOS 00
./AutomationUnityBuildIOS 01
./AutomationUnityBuildIOS 05
./AutomationUnityBuildIOS 06 --config configs/build-ios.dev.json
./AutomationUnityBuildIOS 10 --config configs/build-ios.dev.json
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
10  选择配置并修改配置内容
```

选择配置或查看已有配置时，会优先显示初始化时填写的配置名称，并在括号里显示路径，例如：

```text
1. dev (configs/build-ios.dev.json)
2. testflight (configs/build-ios.testflight.json)
```

也可以明确指定：

```bash
./AutomationUnityBuildIOS run --config configs/build-ios.dev.json
```

## 修改已有配置

如果只是要改版本号同步方式、固定版本号、构建号、是否自动增加 Build Number、Bundle ID、导出方式、签名、Git 分支等配置，不需要重新初始化，也不需要手动编辑 JSON。直接运行：

```bash
./AutomationUnityBuildIOS 10
```

也可以指定配置文件：

```bash
./AutomationUnityBuildIOS edit-config --config configs/build-ios.dev.json
```

进入后选择要修改的字段编号，输入新值后会立即保存到配置文件。输入 `s` 可以查看当前摘要，输入 `0` 或直接回车退出。

`autoIncrementBuildNumber` 默认开启。正式打包时会在 Unity 导出前把 `buildNumber` 自动 +1，本次打包使用加一后的值；完整流程成功后会把新的 `buildNumber` 保存回配置文件。`--dry-run` 或 `--skip-unity` 不会修改配置文件。

`syncBundleVersionFromUnity` 默认开启。开启时，工具不会把 JSON 里的 `bundleVersion` 强制写入 Unity，而是使用 Unity 项目 `PlayerSettings.bundleVersion` 里的版本号；Unity 导出成功并且完整流程成功后，会把实际版本号记录回配置文件。需要强制指定版本号时，在配置编辑器里把 `Sync Bundle Version From Unity` 改成 `false`，再填写 `Bundle Version`。

查看已有配置：

```bash
./AutomationUnityBuildIOS list-configs
```

只检查环境：

```bash
./AutomationUnityBuildIOS doctor
```

只预览命令，不真正执行：

```bash
./AutomationUnityBuildIOS run --config configs/build-ios.dev.json --dry-run --verbose
```

## Web 打包平台 BuildServer

除了控制台 CLI，本仓库还包含一个可部署在 Mac 上的 Web 打包平台。它提供网页登录、项目/配置管理、任务队列、实时日志、产物下载、审计日志和 MCP/Agent 入口。

网页端的“新增配置”既可以登记已有 `build-ios.xxx.json` 路径，也可以直接生成新的配置文件；生成时会自动写入服务端允许的配置目录，并登记到配置列表里。

Windows 调试：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-build-server.ps1
```

Mac 启动：

```bash
./scripts/run-build-server.sh
```

默认访问地址：

```text
http://127.0.0.1:5088
```

默认账号是 `admin`。如果没有设置 `BUILD_SERVER_ADMIN_PASSWORD`，首次启动会在数据目录生成 `initial-admin.txt`；如果没有设置 `BUILD_SERVER_AGENT_TOKEN`，会生成 `initial-agent-token.txt`。生产环境建议显式设置这两个环境变量，详细说明见 [docs/build-server.md](docs/build-server.md)。

生产环境还建议同时设置这些安全边界：

```bash
export BUILD_SERVER_PUBLIC_BASE_URL="https://build.example.com"
export BUILD_SERVER_ALLOWED_ORIGINS="https://build.example.com"
export BUILD_SERVER_ALLOWED_WORKSPACE_ROOTS="/Users/build/UnityBuildWorkspace"
export BUILD_SERVER_ALLOWED_ARTIFACTS_ROOTS="/Users/build/UnityBuildArtifacts"
export BUILD_SERVER_ALLOWED_CONFIG_ROOTS="/Users/build/BuildServerData/configs"
export BUILD_SERVER_ALLOWED_REPOSITORY_HOSTS="github.com"
```

这些限制会让网页和 MCP 只能在允许目录内创建任务、读取配置和产物，并限制可登记的 Git 仓库 Host。

## 在 Windows/VS 发布给 Mac 用

Apple Silicon Mac：

```powershell
.\scripts\publish-mac.ps1 -Runtime osx-arm64
```

Intel Mac：

```powershell
.\scripts\publish-mac.ps1 -Runtime osx-x64
```

然后把 `publish/<runtime>` 这个发布文件夹整个拷到 Mac。建议在 Mac 上改一个容易区分的目录名，例如：

```text
~/Downloads/publish_m1
~/Downloads/publish_release
```

后续所有命令都在这个发布目录里执行。

## 在 Mac 一键打包

进入发布目录并完成首次启动处理：

```bash
cd ~/Downloads/publish_m1
xattr -cr .
chmod +x ./AutomationUnityBuildIOS
codesign --force --deep --sign - ./AutomationUnityBuildIOS
./AutomationUnityBuildIOS 00
```

第一次使用先初始化配置：

```bash
./AutomationUnityBuildIOS 01
```

后续打包直接选择已有配置并执行完整流程：

```bash
./AutomationUnityBuildIOS 06
```

也可以指定配置文件：

```bash
./AutomationUnityBuildIOS 06 --config configs/build-ios.dev.json
```

打包成功后，产物会在 `artifactsRoot` 下按时间生成，例如：

```text
~/UnityBuildArtifacts/YourUnityGame/20260611-153000/
```

其中包含 Unity 日志、Xcode 日志、`.xcarchive` 和导出的 `.ipa` 目录。

## 常用调试命令

跳过 Git，只用 Mac 本地已有项目：

```bash
./AutomationUnityBuildIOS run --config configs/build-ios.dev.json --skip-git
```

跳过 Unity，只重新执行 Xcode 打包：

```bash
./AutomationUnityBuildIOS run --config configs/build-ios.dev.json --skip-unity
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
- `build-config-snapshot.json`: 本次运行使用的配置快照和解析后的路径，已做基础脱敏。
- `xcode-archive.log`: `xcodebuild archive` 的完整输出。
- `xcode-export.log`: `xcodebuild -exportArchive` 的完整输出。
- `xcode-upload.log`: 开启 App Store Connect 自动上传时，记录上传命令的完整输出。

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

## 安全与审计配置

为了后续接 Web 后端、Worker、MCP/Agent，配置文件里已经预留了第一批安全字段：

```json
{
  "allowedRepositoryUrls": ["git@github.com:your-org/your-unity-game.git"],
  "allowedWorkspaceRoots": ["~/UnityBuildWorkspace"],
  "allowedArtifactsRoots": ["~/UnityBuildArtifacts/YourUnityGame"],
  "saveConfigSnapshot": true
}
```

- `allowedRepositoryUrls`: Git 仓库白名单。配置里的 `repositoryUrl` 必须在白名单里，避免误打到陌生仓库。
- `allowedWorkspaceRoots`: Git 工作区允许根目录。仓库目录和 Unity 工程不能逃出这个范围。
- `allowedArtifactsRoots`: 产物允许根目录。日志、Xcode 工程、archive、ipa 导出目录都必须在这个范围内。
- `saveConfigSnapshot`: 正式运行时生成配置快照，方便之后追溯“谁用什么配置打出了这个包”。

日志系统会对常见敏感内容做基础脱敏，例如 URL 里的账号/Token、GitHub/GitLab token、`Bearer` token、`password/token/secret/apiKey` 这类键值。不要主动把证书、私钥、长期 Token 写进配置文件或命令参数。
