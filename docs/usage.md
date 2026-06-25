# 使用说明

这份文档覆盖 AutomationUnityBuildIOS 的完整使用路径：本地 CLI、iOS 打包、Android 打包、商店上传、BuildServer 网页平台、MCP/Agent 入口，以及 LinuxGateway 多节点调度。

如果你第一次使用，建议先按这个顺序走：

1. 准备 Mac/Windows 打包环境。
2. 把 Unity 构建脚本复制到 Unity 项目。
3. 在 Mac 上用 CLI 生成配置并完成一次 dry-run。
4. 正式打包。
5. 团队需要网页入口时再部署 BuildServer。
6. 多台打包机需要统一入口时再部署 LinuxGateway。

---

## 选择使用模式

| 场景 | 推荐方式 | 说明 |
|------|----------|------|
| 自己在 Mac 上发 iOS 包 | CLI | 最少组件，直接运行 `./AutomationUnityBuildIOS 06` |
| iOS + Android 都要自动化 | CLI 或 BuildServer | CLI 适合单人，BuildServer 适合团队 |
| 测试/运营需要点按钮打包 | BuildServer | 浏览器登录、提交任务、看日志、下载产物 |
| 多台 Mac/Windows 打包机 | LinuxGateway + BuildServer | LinuxGateway 只做统一入口，真正构建仍在各节点 BuildServer |
| 让 AI Agent 参与构建流程 | BuildServer MCP | Agent 默认建议 dry-run，需要授权后才能正式打包 |

---

## 环境准备

### 开发机

开发和发布这个工具需要：

- .NET 8 SDK。
- Windows、macOS 或 Linux 均可编译本项目。
- 如果用 Visual Studio，建议使用 VS 2022 或更新版本。

基础验证：

```powershell
dotnet --version
dotnet build .\AutomationUnityBuildIOS.sln
```

### iOS 打包机

iOS 最终构建必须在 macOS 上执行，因为 Unity iOS Build Support 和 Xcode 只在 Mac 侧完成最终链路。

Mac 需要准备：

- Xcode，并至少打开一次完成许可和组件安装。
- Unity Hub、对应 Unity Editor 版本，以及 iOS Build Support 模块。
- Git 命令行，并确保 Mac 能访问你的 Unity 仓库。推荐提前配置 SSH key。
- Apple Developer 账号、证书、描述文件，或 Xcode 自动签名权限。
- 如果不是使用 self-contained 发布包，Mac 还需要安装 .NET 8 SDK。

检查命令：

```bash
git --version
xcodebuild -version
/Applications/Unity/Hub/Editor/<UnityVersion>/Unity.app/Contents/MacOS/Unity -version
```

### Android 打包机

Android 可以在 macOS 或 Windows 节点上执行。

需要准备：

- Unity Hub、对应 Unity Editor 版本，以及 Android Build Support。
- Unity 安装的 Android SDK、NDK、OpenJDK，或你自己配置的 Android 工具链。
- 如果要签名 release 包，准备 Android keystore。
- 如果要上传 Google Play，准备 Google Play Console Service Account JSON，并给它授予对应应用权限。

---

## Unity 项目准备

本工具通过 Unity 的 `-executeMethod` 调用 Unity Editor 脚本，所以你的 Unity 游戏仓库需要加入本项目提供的构建脚本。

iOS：

```text
UnityBuildScripts/Ios/BuildIOS.cs
```

复制到 Unity 项目：

```text
Assets/Editor/BuildIOS.cs
```

它提供的方法是：

```text
BuildAutomation.IOSBuilder.Build
```

Android：

```text
UnityBuildScripts/Android/BuildAndroid.cs
```

复制到 Unity 项目：

```text
Assets/Editor/BuildAndroid.cs
```

它提供的方法是：

```text
BuildAutomation.AndroidBuilder.Build
```

更新 AutomationUnityBuildIOS 后，如果这两个脚本有变化，也要同步更新到 Unity 游戏仓库中。

---

## 本地 CLI 快速开始

### 在开发机发布 Mac CLI

Apple Silicon Mac：

```powershell
.\scripts\publish-mac.ps1 -Runtime osx-arm64
```

Intel Mac：

```powershell
.\scripts\publish-mac.ps1 -Runtime osx-x64
```

发布产物会生成在：

```text
publish/osx-arm64
publish/osx-x64
```

把对应目录整个复制到 Mac，例如：

```text
~/Downloads/publish_m1
```

### Mac 首次运行

如果 macOS 提示“不明开发者”或“无法验证是否恶意软件”，进入发布目录执行：

```bash
cd ~/Downloads/publish_m1
xattr -cr .
chmod +x ./AutomationUnityBuildIOS
codesign --force --deep --sign - ./AutomationUnityBuildIOS
./AutomationUnityBuildIOS 00
```

`00` 会显示帮助和快捷指令表。

### 创建配置

iOS 交互式配置向导：

```bash
./AutomationUnityBuildIOS 01
```

等价完整命令：

```bash
./AutomationUnityBuildIOS init-config
```

只生成 iOS 空模板：

```bash
./AutomationUnityBuildIOS init-config --config build-ios.json --template
```

只生成 Android 空模板：

```bash
./AutomationUnityBuildIOS 11
```

等价完整命令：

```bash
./AutomationUnityBuildIOS init-config --config build-android.json --template --platform android
```

建议把正式配置放在 `configs/` 下，例如：

```text
configs/build-ios.dev.json
configs/build-ios.testflight.json
configs/build-android.internal.json
```

### 检查环境

选择配置并检查环境：

```bash
./AutomationUnityBuildIOS 04
```

指定配置：

```bash
./AutomationUnityBuildIOS doctor --config configs/build-ios.dev.json
```

在 Windows 上调试配置或 dry-run 时可以加：

```bash
--allow-non-mac
```

iOS 正式打包仍然必须在 macOS 上执行。

### 预览命令

只预览流程，不真正执行构建：

```bash
./AutomationUnityBuildIOS 05 --config configs/build-ios.dev.json
```

等价完整命令：

```bash
./AutomationUnityBuildIOS run --config configs/build-ios.dev.json --dry-run --verbose --allow-non-mac
```

### 正式打包

选择已有配置并执行完整流程：

```bash
./AutomationUnityBuildIOS 06
```

指定配置：

```bash
./AutomationUnityBuildIOS 06 --config configs/build-ios.dev.json
```

完整命令：

```bash
./AutomationUnityBuildIOS run --config configs/build-ios.dev.json
```

### 常用跳过参数

| 参数 | 作用 |
|------|------|
| `--skip-git` | 不拉取或重置 Git，直接使用工作区里已有项目 |
| `--skip-unity` | 跳过 Unity 导出或 Android 构建 |
| `--skip-xcode` | iOS 跳过 Xcode archive/export；Android 会忽略此参数 |
| `--dry-run` | 只打印将要执行的命令，不真正构建或上传 |
| `--verbose` | 输出更详细的路径和命令 |
| `--allow-non-mac` | 允许在非 macOS 上做 iOS dry-run 或调试配置 |

### 快捷指令表

| 指令 | 说明 |
|------|------|
| `00` | 显示帮助和快捷指令表 |
| `01` | 初始化配置向导，问答生成可直接使用的配置文件 |
| `02` | 生成空 iOS 配置模板 `build-ios.json` |
| `03` | 查看已有配置文件 |
| `04` | 选择配置并检查环境 |
| `05` | 选择配置并预览完整打包命令 dry-run |
| `06` | 选择配置并执行完整打包流程 |
| `07` | 选择配置打包，但跳过 Git 同步 |
| `08` | 选择配置打包，但跳过 Unity 导出 |
| `09` | 选择配置打包，但跳过 Xcode 编译导出 |
| `10` | 选择配置并修改配置内容 |
| `11` | 生成 Android APK/AAB 配置模板 `build-android.json` |

快捷指令可以追加参数：

```bash
./AutomationUnityBuildIOS 05 --config configs/build-ios.dev.json
./AutomationUnityBuildIOS 06 --config configs/build-ios.release.json
./AutomationUnityBuildIOS 10 --config configs/build-android.internal.json
```

---

## 配置文件说明

配置文件是 JSON，iOS 示例见 `build-ios.sample.json`，Android 示例见 `build-android.sample.json`。

### 通用字段

| 字段 | 说明 |
|------|------|
| `configName` | 配置显示名，用于选择列表 |
| `buildPlatform` | `ios` 或 `android` |
| `repositoryUrl` | Unity 游戏仓库 clone 地址，支持 HTTPS/SSH |
| `allowedRepositoryUrls` | 仓库白名单，建议生产环境填写 |
| `branch` | 构建分支 |
| `workspaceRoot` | Git 工作区根目录 |
| `allowedWorkspaceRoots` | 工作区允许根目录，防止配置逃逸到危险路径 |
| `projectDirectoryName` | 仓库克隆后的目录名 |
| `unityProjectRelativePath` | Unity 工程相对仓库根目录的路径，仓库根就是 Unity 工程时填 `.` |
| `unityVersion` | Unity Hub 安装的版本号，用于推导 Unity 可执行文件路径 |
| `unityExecutablePath` | Unity 可执行文件完整路径，优先级高于 `unityVersion` |
| `unityBuildMethod` | Unity Editor 静态方法名 |
| `artifactsRoot` | 构建产物根目录 |
| `allowedArtifactsRoots` | 产物允许根目录 |
| `productName` | Unity Product Name |
| `bundleIdentifier` | iOS Bundle ID 或 Android Package Name |
| `bundleVersion` | 版本号 |
| `syncBundleVersionFromUnity` | 是否从 Unity PlayerSettings 同步版本号 |
| `buildNumber` | iOS Build Number 或 Android versionCode |
| `autoIncrementBuildNumber` | 正式构建成功后是否自动递增构建号 |
| `saveConfigSnapshot` | 是否在日志目录保存本次配置快照 |

最容易填错的三个值：

```text
repositoryUrl: 填 git clone 地址，不要填网页标题。
unityProjectRelativePath: 通常是 .，不要填 build、Builds、XcodeProject。
teamId: iOS 填 10 位 Apple Developer Team ID，不是公司名。
```

### iOS 字段

| 字段 | 说明 |
|------|------|
| `scheme` | 默认 `Unity-iPhone` |
| `configuration` | 默认 `Release` |
| `exportMethod` | `development`、`ad-hoc`、`app-store` 等 Xcode 导出方式 |
| `teamId` | Apple Developer Team ID，必须是 10 位字母数字 |
| `signingStyle` | `automatic` 或 `manual` |
| `iosDeploymentTarget` | iOS 最低版本，例如 `13.0` |
| `allowProvisioningUpdates` | 是否允许 Xcode 自动处理签名更新 |
| `generateExportOptionsPlist` | 是否自动生成 `ExportOptions.plist` |
| `copyArchiveToOrganizer` | 是否复制 `.xcarchive` 到 Xcode Organizer |
| `appStoreConnectUploadEnabled` | 是否自动上传 App Store Connect/TestFlight |

### Android 字段

| 字段 | 说明 |
|------|------|
| `androidBuildFormat` | `apk`、`aab` 或 `both` |
| `androidOutputDirectory` | Android 输出目录，留空时自动生成 |
| `apkOutputPath` | APK 输出路径，留空时自动生成 |
| `aabOutputPath` | AAB 输出路径，留空时自动生成 |
| `androidMinSdkVersion` | 可选，覆盖 Min SDK |
| `androidTargetSdkVersion` | 可选，覆盖 Target SDK |
| `androidKeystoreName` | keystore 路径或名称 |
| `androidKeystorePass` | keystore 密码 |
| `androidKeyaliasName` | key alias |
| `androidKeyaliasPass` | key alias 密码 |
| `googlePlayUploadEnabled` | 是否上传 Google Play |
| `googlePlayTrack` | `internal`、`alpha`、`beta`、`production` |
| `googlePlayReleaseStatus` | `draft`、`inProgress`、`halted`、`completed` |
| `googlePlayUploadArtifact` | 上传 `apk`、`aab` 或 `both` |

不要把证书、私钥、长期 Token 放进仓库。配置里确实需要引用密钥时，优先填写打包机本地路径，并保护好文件权限。

---

## iOS 打包

### 基础流程

iOS 完整流程如下：

1. 校验配置安全边界和 Git 仓库策略。
2. 检查 `git`、Unity、`xcodebuild`。
3. 创建本次运行目录和日志目录。
4. 写入 `build-config-snapshot.json`。
5. 拉取或更新 Unity 仓库。
6. 调用 Unity BatchMode 导出 iOS Xcode 工程。
7. 执行 `xcodebuild archive`。
8. 执行 `xcodebuild -exportArchive`。
9. 可选复制 `.xcarchive` 到 Xcode Organizer。
10. 可选上传 App Store Connect/TestFlight。

### App Store Connect / TestFlight 上传

开启自动上传需要 `exportMethod` 为 `app-store`，并配置 App Store Connect API Key。

示例：

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

- `.p8` 文件必须存在于 Mac 打包机本地。
- Key ID 和 Issuer ID 来自 App Store Connect API Key 页面。
- 上传成功后，构建会进入 App Store Connect/TestFlight 的处理队列。
- 是否提交审核、是否发布生产环境，仍按 App Store Connect 的版本策略处理。

### 常用 iOS 调试方式

只同步 Git 和 Unity，不执行 Xcode：

```bash
./AutomationUnityBuildIOS run --config configs/build-ios.dev.json --skip-xcode
```

跳过 Unity，只复用已有 Xcode 工程重新 archive/export：

```bash
./AutomationUnityBuildIOS run --config configs/build-ios.dev.json --skip-unity
```

只检查配置和环境：

```bash
./AutomationUnityBuildIOS doctor --config configs/build-ios.dev.json
```

---

## Android 打包

### 基础流程

Android 完整流程如下：

1. 校验配置安全边界和 Git 仓库策略。
2. 检查 `git` 和 Unity。
3. 创建本次运行目录和日志目录。
4. 写入 `build-config-snapshot.json`。
5. 拉取或更新 Unity 仓库。
6. 调用 Unity BatchMode 构建 APK/AAB。
7. 可选上传 Google Play。

Android 不需要 Xcode，`--skip-xcode` 会被忽略。

### 构建 APK/AAB

配置：

```json
{
  "buildPlatform": "android",
  "unityBuildMethod": "BuildAutomation.AndroidBuilder.Build",
  "androidBuildFormat": "both"
}
```

`androidBuildFormat` 可选：

| 值 | 结果 |
|----|------|
| `apk` | 只生成 APK |
| `aab` | 只生成 AAB |
| `both` | 同时生成 APK 和 AAB |

### Google Play 上传

需要在 Google Play Console 中创建 Service Account，并授予对应应用的发布权限。

示例：

```json
{
  "googlePlayUploadEnabled": true,
  "googlePlayPackageName": "com.company.game",
  "googlePlayServiceAccountJsonPath": "~/Secrets/google-play-service-account.json",
  "googlePlayTrack": "internal",
  "googlePlayReleaseStatus": "draft",
  "googlePlayUploadArtifact": "aab",
  "googlePlayChangesNotSentForReview": false,
  "googlePlayUserFraction": null
}
```

建议先 dry-run：

```bash
./AutomationUnityBuildIOS run --config configs/build-android.internal.json --dry-run --verbose
```

确认路径、包名、版本号和上传产物无误后再正式执行。

---

## 日志和产物

每次运行都会在 `artifactsRoot` 下创建独立运行目录，例如：

```text
~/UnityBuildArtifacts/YourUnityGame/20260625-153000/
```

常见内容：

| 文件或目录 | 说明 |
|------------|------|
| `Logs/automation.log` | 总流程日志，包含步骤、命令、耗时和错误 |
| `Logs/unity-editor.log` | Unity Editor 自己写出的构建日志 |
| `Logs/unity-process.log` | 启动 Unity 进程捕获到的 stdout/stderr |
| `Logs/build-config-snapshot.json` | 本次使用的配置快照，已做基础脱敏 |
| `Logs/xcode-archive.log` | iOS archive 日志 |
| `Logs/xcode-export.log` | iOS export 日志 |
| `Logs/xcode-upload.log` | App Store Connect 上传日志 |
| `.xcarchive` | iOS 归档产物 |
| `.ipa` 导出目录 | iOS 导出产物 |
| `.apk` / `.aab` | Android 构建产物 |

排查顺序：

1. 先看 `automation.log` 末尾的失败步骤。
2. Unity 阶段失败，看 `unity-editor.log`。
3. iOS Xcode 阶段失败，看 `xcode-archive.log` 或 `xcode-export.log`。
4. 商店上传失败，看 `xcode-upload.log` 或总日志中的 Google Play 上传错误。

日志系统会对常见敏感信息做基础脱敏，例如 URL 中的账号/Token、`Bearer` token、`password/token/secret/apiKey` 等键值。

---

## BuildServer 网页平台

BuildServer 是 CLI 的 Web/Agent 化入口。它提供：

- Web 登录。
- 项目管理。
- 配置管理。
- 打包任务队列。
- 实时日志。
- 产物下载。
- 用户权限。
- 审计日志。
- MCP/Agent 工具。
- LinuxGateway 节点接口。

第一版采用单机、单 Worker、串行队列，避免 Unity、Xcode、Gradle、签名环境和缓存目录并发互相影响。

### 本地启动

Windows 调试：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-build-server.ps1
```

macOS/Linux 调试：

```bash
./scripts/run-build-server.sh
```

默认地址：

```text
http://127.0.0.1:5088
```

默认账号：

```text
admin
```

如果没有设置 `BUILD_SERVER_ADMIN_PASSWORD`，首次启动会生成随机密码：

```text
<DataRoot>/initial-admin.txt
```

如果没有设置 `BUILD_SERVER_AGENT_TOKEN`，首次启动会生成默认 MCP Agent Token：

```text
<DataRoot>/initial-agent-token.txt
```

### 生产环境变量

建议生产环境显式设置：

```bash
export BUILD_SERVER_ADMIN_PASSWORD="strong-password"
export BUILD_SERVER_AGENT_TOKEN="strong-agent-token"
export BUILD_SERVER_PUBLIC_BASE_URL="https://mac-build.example.com"
export BUILD_SERVER_ALLOWED_ORIGINS="https://mac-build.example.com"
export BUILD_SERVER_ALLOWED_WORKSPACE_ROOTS="/Users/build/UnityBuildWorkspace"
export BUILD_SERVER_ALLOWED_ARTIFACTS_ROOTS="/Users/build/UnityBuildArtifacts"
export BUILD_SERVER_ALLOWED_CONFIG_ROOTS="/Users/build/BuildServerData/configs"
export BUILD_SERVER_ALLOWED_REPOSITORY_HOSTS="gitee.com,github.com"
export BUILD_SERVER_NODE_PLATFORMS="ios,android"
```

常用变量：

| 变量 | 说明 |
|------|------|
| `BUILD_SERVER_DATA_ROOT` | 数据目录，保存用户、项目、配置、任务、审计等 JSON |
| `BUILD_SERVER_ADMIN_PASSWORD` | 管理员密码 |
| `BUILD_SERVER_AGENT_TOKEN` | MCP Agent Token |
| `BUILD_SERVER_PUBLIC_BASE_URL` | 对外访问地址 |
| `BUILD_SERVER_ALLOWED_ORIGINS` | 允许的 Web Origin，经过反向代理时建议设置 |
| `BUILD_SERVER_ALLOWED_WORKSPACE_ROOTS` | 工作区允许根目录 |
| `BUILD_SERVER_ALLOWED_ARTIFACTS_ROOTS` | 产物允许根目录 |
| `BUILD_SERVER_ALLOWED_CONFIG_ROOTS` | 配置文件允许根目录 |
| `BUILD_SERVER_ALLOWED_REPOSITORY_HOSTS` | 允许登记的 Git Host |
| `BUILD_SERVER_GATEWAY_TOKEN` | 节点接口 Token；留空时首次启动会自动生成 `initial-gateway-token.txt` |
| `BUILD_SERVER_NODE_PLATFORMS` | 当前节点能力，例如 `ios,android` 或 `android` |

### Web 使用流程

首次进入后台后：

1. 新增项目，填写项目名、Git 仓库、默认分支、允许分支、工作区和产物目录。
2. 新增配置，选择 iOS 或 Android。
3. 配置可以指向已有 JSON，也可以由网页表单生成新的 JSON。
4. 发起打包，选择项目、配置、分支和可选参数。
5. 在任务列表查看状态、实时日志和产物。

BuildServer 会为每个任务生成独立配置快照，并调用 CLI：

```text
AutomationUnityBuildIOS run --config <job-config.json>
```

### 发布 BuildServer 到 Mac

Apple Silicon Mac：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-build-server-mac.ps1 -Runtime osx-arm64
```

Intel Mac：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-build-server-mac.ps1 -Runtime osx-x64
```

发布目录会同时包含 BuildServer 和 AutomationUnityBuildIOS CLI。生产环境可配合：

```text
deploy/launchd/com.automationunity.buildserver.plist
```

建议固定一个 macOS 用户运行 BuildServer，并把 Unity License、Xcode 签名、证书、描述文件、Git SSH Key 都配置在这个用户下。

### MCP / Agent

MCP 入口：

```text
POST /mcp
Header: X-Agent-Token: <BUILD_SERVER_AGENT_TOKEN>
```

支持工具：

| 工具 | 说明 |
|------|------|
| `list_projects` | 列出可用项目 |
| `list_configs` | 列出项目下的打包配置 |
| `start_build` | 提交 iOS 或 Android 打包任务 |
| `start_ios_build` | 兼容旧名称，建议新接入使用 `start_build` |
| `get_build_status` | 查询打包任务状态 |
| `tail_build_log` | 读取最近日志 |
| `list_build_artifacts` | 列出任务产物 |

默认 Agent 只允许 `dryRun=true`。要允许正式打包，需要给对应 MCP Client 开启 `allowFullBuild`，并建议只授权特定项目。

不要把 Agent Token 放在 URL 查询参数里，使用 `X-Agent-Token` 或 `Authorization: Bearer`。

---

## LinuxGateway 多节点入口

LinuxGateway 适合部署在有公网域名的 Linux 服务器上。它不直接运行 Unity，不保存 Unity 项目，也不持有 Apple 证书；它只负责登录、登记节点、选择节点、转发任务、代理日志和产物。

典型架构：

```text
外网用户
  -> LinuxGateway Web/API
      -> Mac BuildServer       iOS + Android
      -> Windows BuildServer   Android
```

不部署 LinuxGateway 时，每台 Mac/Windows 上的 BuildServer 仍然可以独立使用。

### 启动 LinuxGateway

开发运行：

```bash
./scripts/run-linux-gateway.sh http://127.0.0.1:5090
```

Windows 调试：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-linux-gateway.ps1
```

默认地址：

```text
http://127.0.0.1:5090
```

首次启动后，如果没有设置 `LINUX_GATEWAY_ADMIN_PASSWORD`，会生成初始密码：

```text
linuxgateway-data/initial-admin.txt
```

生产环境建议设置：

```bash
export LINUX_GATEWAY_ADMIN_PASSWORD="strong-password"
export LINUX_GATEWAY_PUBLIC_BASE_URL="https://build.example.com"
export LINUX_GATEWAY_ALLOWED_ORIGINS="https://build.example.com"
export LINUX_GATEWAY_DATA_ROOT="/opt/unity-build-gateway/data"
```

### 发布 LinuxGateway 到 Linux

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-linux-gateway-linux.ps1
```

默认输出：

```text
publish/linux-gateway
```

复制到 Linux 后运行：

```bash
chmod +x ./LinuxGateway
./LinuxGateway --urls http://127.0.0.1:5090
```

外网建议使用 Nginx/Caddy 提供 HTTPS，再反向代理到 `127.0.0.1:5090`。

### 方式一：直接连接节点

直接连接适合 LinuxGateway 能访问到 Mac/Windows BuildServer 的场景，例如 VPN、内网、内网穿透或公网 HTTPS。

在每台 BuildServer 节点启动前设置：

```bash
export BUILD_SERVER_GATEWAY_TOKEN="strong-random-token"
export BUILD_SERVER_NODE_PLATFORMS="ios,android"
```

Windows Android 节点：

```powershell
$env:BUILD_SERVER_GATEWAY_TOKEN="strong-random-token"
$env:BUILD_SERVER_NODE_PLATFORMS="android"
```

也可以不手动设置 `BUILD_SERVER_GATEWAY_TOKEN`。BuildServer 首次启动会自动生成 Gateway Token，并保存到：

```text
<DataRoot>/initial-gateway-token.txt
```

BuildServer 会启用：

```text
/api/gateway/*
```

LinuxGateway 调用节点时使用：

```text
Header: X-Gateway-Token: <BUILD_SERVER_GATEWAY_TOKEN>
```

在 LinuxGateway 网页中新增设备：

| 字段 | 示例 |
|------|------|
| 设备名称 | `Mac Build` |
| BuildServer 地址 | `https://mac-build.example.com` |
| Gateway Token | 该节点的 `BUILD_SERVER_GATEWAY_TOKEN` |
| 平台 | Mac 选 `iOS + Android`，Windows 选 `Android` |

保存后刷新设备，确认能看到节点项目和配置。

### 方式二：反向连接节点

反向连接适合节点在 NAT、家庭网络或公司内网中，LinuxGateway 无法直接访问节点地址的场景。此时由 BuildServer 主动连接 LinuxGateway。

在 LinuxGateway 网页中生成 Enrollment Token，然后在 BuildServer 的 Gateway 连接页填入：

```text
Gateway 地址: https://build.example.com
Enrollment Token: <token>
```

也可以通过环境变量让 BuildServer 启动后自动连接：

```bash
export BUILD_SERVER_REVERSE_GATEWAY_ENABLED=true
export BUILD_SERVER_REVERSE_GATEWAY_URL="https://build.example.com"
export BUILD_SERVER_REVERSE_GATEWAY_ENROLLMENT_TOKEN="<token>"
export BUILD_SERVER_REVERSE_NODE_NAME="Mac Build"
```

连接成功后，LinuxGateway 会显示反向连接节点。节点凭据会保存在 BuildServer 数据目录中；吊销节点后，需要重新生成 Enrollment Token 再注册。

### 通过 LinuxGateway 发起构建

1. 登录 LinuxGateway。
2. 在设备节点页确认节点在线。
3. 刷新节点，确保项目和配置同步成功。
4. 在打包任务页选择设备、项目、配置和分支。
5. 提交任务。
6. 查看远程节点返回的状态、日志和产物。

iOS 任务只能发到支持 `ios` 的 Mac 节点；Windows 节点通常只适合 Android APK/AAB。

---

## 安全建议

- 生产环境必须设置强密码，不要依赖初始密码文件长期使用。
- `BUILD_SERVER_AGENT_TOKEN`、`BUILD_SERVER_GATEWAY_TOKEN`、Enrollment Token 不要放 URL，使用 Header 或服务端表单保存。
- LinuxGateway 和 BuildServer 的数据目录会保存用户、任务、节点凭据或 Token，必须限制系统权限。
- BuildServer 建议配置 `BUILD_SERVER_ALLOWED_WORKSPACE_ROOTS`、`BUILD_SERVER_ALLOWED_ARTIFACTS_ROOTS`、`BUILD_SERVER_ALLOWED_CONFIG_ROOTS` 和 `BUILD_SERVER_ALLOWED_REPOSITORY_HOSTS`。
- 节点后台如果只给 LinuxGateway 使用，尽量不要把普通管理后台直接暴露到公网。
- iOS 证书、描述文件、App Store Connect `.p8`、Android keystore、Google Play Service Account JSON 都应只放在打包机本地安全目录。
- 不要把证书、私钥、长期 Token 提交到 Git。
- 经过反向代理访问网页时，配置 `PUBLIC_BASE_URL` 和 `ALLOWED_ORIGINS`，避免跨站请求被拒或来源校验失效。

---

## 常见问题

| 问题 | 处理 |
|------|------|
| iOS 在 Windows 上报必须 macOS | iOS 正式构建必须在 Mac；Windows 只适合 `--dry-run --allow-non-mac` 调试配置 |
| 找不到 Unity 可执行文件 | 填 `unityExecutablePath`，或确认 `unityVersion` 对应 Unity Hub 安装路径存在 |
| Git 拉取失败 | 先在打包机手动 `git clone` 验证 SSH key 或 HTTPS 凭据 |
| Team ID 校验失败 | `teamId` 必须是 10 位 Apple Developer Team ID，不是公司名 |
| App Store Connect 上传失败 | 确认 `exportMethod=app-store`，`.p8` 路径存在，Key ID 和 Issuer ID 正确 |
| Android versionCode 报错 | `buildNumber` 必须是大于 0 的整数 |
| Google Play 上传失败 | 检查 Service Account JSON 路径、应用权限、packageName、track 和上传产物格式 |
| BuildServer 登录失败 | 账号是 `admin`，密码只复制 `initial-admin.txt` 中 `admin password:` 后面的值 |
| Web 写操作被拒 | 检查 `BUILD_SERVER_ALLOWED_ORIGINS` 或 `LINUX_GATEWAY_ALLOWED_ORIGINS` 是否与访问域名一致 |
| LinuxGateway 节点 401 | Gateway Token 错误或节点没有启用 `BUILD_SERVER_GATEWAY_TOKEN` |
| LinuxGateway 节点超时 | 检查节点地址、端口、防火墙、内网穿透或反向代理 |
| 产物下载失败 | 确认产物路径在 BuildServer 允许的 artifacts roots 内 |

---

## 回归验证

开发者可以运行：

```powershell
.\scripts\verify.ps1
```

它会执行：

- 解决方案编译。
- CLI 项目编译。
- BuildServer 编译。
- LinuxGateway 编译。
- 帮助入口 `00`。
- iOS sample dry-run。
- Android sample dry-run。
- 配置编辑器打开并退出。

如果只想快速检查当前文档改动是否影响编译，可直接运行：

```powershell
dotnet build .\AutomationUnityBuildIOS.sln
```
