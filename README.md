# AutomationUnityBuildIOS — Unity 全平台自动化打包发布系统

> 一套经过生产验证的 Unity 移动端自动化构建与发布工具链。从 Git 同步、Unity BatchMode、Xcode/Android 构建到 App Store Connect / TestFlight、Google Play、TikTok 小游戏上传，再到 Web 打包平台、桌面客户端、多节点网关调度和 AI Agent 接入——它把整个发布流水线串成一条可落地、可追溯、可扩展的完整链路。

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Unity](https://img.shields.io/badge/Unity-iOS%20%7C%20Android%20%7C%20TikTok-black?logo=unity&logoColor=white)](https://unity.com/)
[![BuildServer](https://img.shields.io/badge/BuildServer-Web%20Queue-2563EB)](docs/build-server.md)
[![DesktopApp](https://img.shields.io/badge/DesktopApp-Avalonia%2011-7C3AED)](docs/usage.md#桌面客户端)
[![Gateway](https://img.shields.io/badge/LinuxGateway-Multi--Node-16A34A)](docs/linux-gateway.md)
[![Tests](https://img.shields.io/badge/tests-256%2B%20passing-brightgreen)](docs/usage.md#回归验证)
[![License](https://img.shields.io/badge/license-Apache--2.0-blue.svg)](LICENSE)

中文 | [English](README.en.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | [Français](README.fr.md) | [Русский](README.ru.md) | [Español](README.es.md) | [Português](README.pt.md) | [完整使用文档](docs/usage.md) | [架构说明](docs/architecture.md)

---

## 项目仓库

- **Gitee**: https://gitee.com/chenfengloveyuri/automation-unity-build-ios
- **GitHub**: https://github.com/chenfengyimei/-AutomationUnityBuild

---

## 这是什么

AutomationUnityBuildIOS 是一套为 Unity 移动端项目打造的端到端自动化打包发布系统。

它不是简单的脚本封装，而是覆盖了从代码仓库到应用商店全链路的工程化平台。最小形态下，它是一个拷到 Mac 上就能用的 .NET 8 命令行工具：选择配置，自动拉取 Unity 仓库、执行 Unity Editor 构建脚本、导出 iOS Xcode 工程或 Android APK/AAB、生成日志与产物。团队形态下，它变成网页打包平台：负责人在 Web 后台维护项目和配置，构建员点击发起任务，所有人通过浏览器查看队列、日志、产物和审计记录。桌面形态下，它提供原生 Windows 桌面客户端，所有操作离线可用，配置模板一键填充。多设备形态下，它通过 LinuxGateway 把多台 Mac/Windows 打包机统一接入一个公网入口，支持直接连接和反向穿透两种组网方式。

它还覆盖了 TikTok 小游戏的 WebGL 构建与开放平台 API 上传、邮件通知（成功/失败/SMTP 465 隐式 SSL）、存储管理（产物清理/存储概览/批量删除）、四类配置模板（项目/工程/签名/证书）以及 AI Agent 通过 MCP 工具参与构建流程的能力。

它解决的是一个很具体但很疼的问题：Unity 移动端发包不应该每次都靠人记命令、翻路径、找证书、手工看日志。

---

## 适合谁

- **Unity 手游/应用团队**：需要稳定生成 iOS `.ipa`、`.xcarchive`、Android `.apk` / `.aab`，并自动上传到 App Store Connect / TestFlight / Google Play。
- **TikTok 小游戏团队**：需要 WebGL 构建后直接上传到 TikTok 开放平台。
- **独立开发者**：想把 Mac 打包步骤固化成一套可复用配置，减少每次发包前的手工操作。
- **测试/运营/发行团队**：希望通过网页或桌面客户端发起构建、下载产物、追踪历史，而不是远程登录打包机。
- **多平台构建团队**：Mac 负责 iOS 和 Android，Windows 节点负责 Android，通过 LinuxGateway 统一调度。
- **AI/Agent 工作流用户**：希望通过 MCP 工具让 Agent 查询项目、提交 dry-run、查看状态、读取日志和产物。

---

## 核心能力

| 能力 | 说明 | 文档 |
|------|------|------|
| **本地 CLI 自动打包** | 数字快捷指令、交互式配置向导、配置选择器、配置编辑器、dry-run 和环境检查 | [使用文档](docs/usage.md#本地-cli-快速开始) |
| **iOS 完整链路** | Git 同步、Unity 导出 Xcode 工程、`xcodebuild archive/export`、复制 `.xcarchive` 到 Organizer | [iOS 打包](docs/usage.md#ios-打包) |
| **App Store Connect 上传** | 通过 API Key 自动上传到 App Store Connect/TestFlight，适合无人值守流水线 | [商店上传](docs/usage.md#app-store-connect--testflight-上传) |
| **Android APK/AAB** | 支持 `apk`、`aab`、`both` 三种构建格式，兼容 Android keystore 和版本号管理 | [Android 打包](docs/usage.md#android-打包) |
| **Google Play 发布** | 使用 Service Account 调用 Google Play Publishing API，支持 track、release status 和灰度比例 | [Google Play](docs/usage.md#google-play-上传) |
| **TikTok 小游戏** | WebGL 构建后通过 TikTok 开放平台 API 自动上传，独立 `Modules/Tiktok/` 模块 | [TikTok 打包](docs/usage.md#tiktok-小游戏打包) |
| **BuildServer 网页平台** | 登录、项目/配置管理、任务队列、实时日志、产物下载、用户权限、审计日志、邮件通知、存储管理 | [BuildServer](docs/build-server.md) |
| **DesktopApp 桌面客户端** | Avalonia UI 11 原生 Windows 桌面应用，全功能离线配置管理、打包执行、产物浏览、模板管理、服务器同步 | [桌面客户端](docs/usage.md#桌面客户端) |
| **MCP / Agent 入口** | 提供 `list_projects`、`start_build`、`get_build_status`、`tail_build_log` 等工具 | [MCP/Agent](docs/build-server.md#mcpagent) |
| **LinuxGateway 多节点入口** | 在 Linux 公网服务器上统一调度多台 Mac/Windows BuildServer 节点，支持直接连接和反向穿透 | [LinuxGateway](docs/linux-gateway.md) |
| **邮件通知** | 构建成功/失败自动发送邮件通知，支持 SMTP 465 隐式 SSL、通知联系人列表、个性化模板 | [邮件通知](docs/usage.md#邮件通知) |
| **存储管理** | 手动清理历史产物、存储概览、批量删除，防止打包机磁盘膨胀 | [存储管理](docs/usage.md#存储管理) |
| **配置模板** | 四类模板（项目管理/工程管理/签名管理/证书管理），一键填充配置字段，支持服务端双向同步 | [模板管理](docs/usage.md#模板管理) |
| **安全边界** | Git 仓库白名单、路径根目录限制、配置快照、敏感信息脱敏、登录与审计 | [架构说明](docs/architecture.md#平台化前置能力) |
| **日志与产物追溯** | 每次运行生成独立目录，保存总日志、Unity 日志、Xcode/Android 日志和配置快照 | [日志排查](docs/usage.md#日志和产物) |

---

## 快速体验

在开发机上可以先跑帮助和 dry-run，确认命令入口正常：

```powershell
dotnet build .\AutomationUnityBuildIOS.sln
dotnet run --project .\AutomationUnityBuildIOS.csproj -- 00
dotnet run --project .\AutomationUnityBuildIOS.csproj -- run --config .\build-ios.sample.json --dry-run --allow-non-mac --skip-git --skip-xcode
dotnet run --project .\AutomationUnityBuildIOS.csproj -- run --config .\build-android.sample.json --dry-run --allow-non-mac --skip-git
```

真正的 iOS 构建必须在 macOS 上执行。常见发布方式是先在 Windows/VS 或任意 .NET 环境发布 Mac 可执行文件：

```powershell
.\scripts\publish-mac.ps1 -Runtime osx-arm64
```

把 `publish/osx-arm64` 拷到 Mac 后：

```bash
cd ~/Downloads/publish_m1
xattr -cr .
chmod +x ./AutomationUnityBuildIOS
codesign --force --deep --sign - ./AutomationUnityBuildIOS
./AutomationUnityBuildIOS 00
./AutomationUnityBuildIOS 01
./AutomationUnityBuildIOS 06
```

完整准备、配置字段、iOS/Android/TikTok 商店上传、Web 平台、桌面客户端和多节点部署请看 [docs/usage.md](docs/usage.md)。

---

## 运行方式

| 方式 | 适用场景 | 入口 |
|------|----------|------|
| **CLI 单机模式** | 单人或小团队，直接在 Mac 打包机上操作 | `./AutomationUnityBuildIOS 06` |
| **BuildServer 网页模式** | 团队通过浏览器管理项目、配置、队列、日志和产物 | `http://127.0.0.1:5088` |
| **DesktopApp 桌面模式** | Windows 原生桌面客户端，离线配置管理、打包执行、模板管理和服务器同步 | `DesktopApp.exe` |
| **MCP/Agent 模式** | 让 AI Agent 通过受控工具提交 dry-run、查询状态和读取日志 | `POST /mcp` |
| **LinuxGateway 多节点模式** | 多台 Mac/Windows 打包机接入一个公网调度入口，支持直连和反向穿透 | `http://127.0.0.1:5090` |

---

## 整体架构

```mermaid
graph TB
    Dev["开发机 / Windows / VS"] --> Publish["发布 CLI / BuildServer / DesktopApp"]
    Publish --> Mac["Mac 打包机"]
    Publish --> Win["Windows Android 节点"]

    subgraph CLI["AutomationUnityBuildIOS CLI"]
        Config["配置选择 / 配置编辑 / dry-run"]
        Git["Git 同步"]
        Unity["Unity BatchMode"]
        Ios["iOS: Xcode archive/export"]
        Android["Android: APK/AAB"]
        Tiktok["TikTok: WebGL 构建"]
        Logs["日志 / 配置快照 / 产物目录"]
    end

    Mac --> CLI
    Win --> CLI
    Config --> Git --> Unity
    Unity --> Ios --> Logs
    Unity --> Android --> Logs
    Unity --> Tiktok --> Logs
    Ios --> ASC["App Store Connect / TestFlight"]
    Android --> GP["Google Play"]
    Tiktok --> TT["TikTok 开放平台"]

    subgraph Web["BuildServer"]
        UI["Web 控制台"]
        Queue["串行任务队列"]
        Audit["用户 / 权限 / 审计"]
        Email["邮件通知"]
        Storage["存储管理"]
        MCP["MCP / Agent 工具"]
    end

    UI --> Queue --> CLI
    MCP --> Queue
    Audit --> Queue
    Email --> Queue
    Storage --> Audit

    subgraph Desktop["DesktopApp"]
        DConfig["配置管理 / 模板填充"]
        DBuild["打包执行 / 实时日志"]
        DArtifacts["产物浏览"]
        DSync["服务器同步"]
    end

    DConfig --> DSync
    DSync --> Web

    subgraph Gateway["LinuxGateway"]
        PublicUI["公网入口"]
        Nodes["Mac / Windows 节点"]
        Forward["任务转发 / 日志产物代理"]
        Reverse["反向连接通道"]
        Update["在线自更新"]
    end

    PublicUI --> Forward --> Nodes --> Web
    Reverse --> Nodes
    Update --> Gateway
```

第一版 BuildServer 采用单机、单 Worker、串行队列设计，这是有意为之：Unity、Xcode、Gradle、签名证书和缓存目录通常不适合在同一台机器上并发抢占。多机器扩展由 LinuxGateway 负责，把并发调度分散到不同节点，同时支持直接连接和 NAT 穿透两种组网方式。

---

## 项目结构

```text
AutomationUnityBuildIOS/
├── Cli/                         # 命令入口、参数解析、数字快捷指令
├── ConsoleUi/                   # 交互式菜单、配置向导、配置编辑器
├── Configuration/               # 配置模型、模板、路径解析、配置文件选择
├── Workflow/                    # 自动化打包主流程、运行上下文、配置快照
├── Services/                    # Git、环境检查、目录准备、安全边界校验
├── Modules/
│   ├── Common/                  # 平台 Pipeline、Unity 命令、日志诊断
│   ├── Ios/                     # Unity 导出 iOS、Xcode archive/export、ASC 上传
│   ├── Android/                 # Android APK/AAB、Google Play Publishing API
│   └── Tiktok/                  # TikTok 小游戏 WebGL 构建与开放平台上传
├── Infrastructure/              # 日志、进程执行、路径工具、路径安全、敏感信息脱敏
├── UnityBuildScripts/
│   ├── Ios/BuildIOS.cs          # 复制到 Unity 项目的 Assets/Editor
│   └── Android/BuildAndroid.cs  # 复制到 Unity 项目的 Assets/Editor
├── BuildServer/                 # Web 打包平台、队列 Worker、MCP、节点接口、邮件通知、存储管理
├── LinuxGateway/                # 多设备统一入口、反向连接通道、在线自更新
├── DesktopApp/                  # Avalonia UI 11 桌面客户端、模板管理、服务器同步
├── deploy/                      # launchd、Docker 等部署模板
├── docs/                        # 使用、架构和部署文档
├── scripts/                     # 发布脚本（CLI/BuildServer/LinuxGateway/DesktopApp）
└── AutomationUnityBuildIOS.Tests/
```

---

## 文档导航

| 文档 | 内容 |
|------|------|
| [docs/usage.md](docs/usage.md) | 从零开始使用 CLI、DesktopApp、BuildServer、LinuxGateway 和 MCP |
| [docs/architecture.md](docs/architecture.md) | 目录职责、核心模块、平台化安全能力 |
| [docs/build-server.md](docs/build-server.md) | BuildServer 的启动、数据、MCP、Gateway 接口和扩展方向 |
| [docs/linux-gateway.md](docs/linux-gateway.md) | LinuxGateway 的节点接入、反向连接、自更新和部署说明 |
| [docs/linux-gateway-docker.md](docs/linux-gateway-docker.md) | LinuxGateway 的 Docker 部署方案 |

---

## 开发与验证

```powershell
.\scripts\verify.ps1
```

这个脚本会执行解决方案编译、CLI 帮助入口、iOS/Android dry-run、配置编辑器打开退出，以及 BuildServer/LinuxGateway 基础编译验证。

测试套件覆盖 256+ 个用例，涵盖 CLI 参数解析、配置模型、路径安全、Git 策略、Unity 命令构建、Google Play API、BuildServer API 路由、LinuxGateway 节点通信、反向连接、TikTok 配置等全部模块。

---

## 当前状态

| 模块 | 状态 |
|------|------|
| CLI iOS 自动打包 | ✅ 生产级 |
| CLI Android APK/AAB 打包 | ✅ 生产级 |
| CLI TikTok 小游戏打包 | ✅ 可用 |
| App Store Connect / TestFlight 上传 | ✅ 生产级 |
| Google Play 上传 | ✅ 生产级 |
| BuildServer Web 平台 | ✅ 可用 |
| DesktopApp 桌面客户端 | ✅ 可用 |
| MCP/Agent 工具入口 | ✅ 可用 |
| LinuxGateway 多节点入口 | ✅ 可用 |
| LinuxGateway 反向连接 | ✅ 可用 |
| LinuxGateway 在线自更新 | ✅ 可用 |
| 邮件通知 | ✅ 可用 |
| 存储管理 | ✅ 可用 |
| 配置模板管理 | ✅ 可用 |
| 多 Worker 数据库化调度 | 后续可演进 |

---

## 许可证

本项目基于 [Apache License 2.0](LICENSE) 开源。
