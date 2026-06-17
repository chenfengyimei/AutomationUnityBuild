# LinuxGateway 多设备入口

`LinuxGateway` 是新增的可选中央入口，适合部署在有公网域名的 Linux 服务器上。它不直接运行 Unity、不保存 Unity 项目、不持有 Apple 证书；它只负责网页登录、登记 Mac/Windows 打包节点、选择节点并把任务转发给节点上的 `BuildServer`。

不部署 LinuxGateway 时，Mac/Windows 上原来的 `BuildServer` 仍然可以独立登录、配置和打包。

## 架构

```text
外网用户
  -> LinuxGateway Web/API
      -> Mac BuildServer /api/gateway/*    iOS + Android
      -> Windows BuildServer /api/gateway/* Android APK/AAB
```

每台 Mac/Windows 节点仍然运行现有 `BuildServer`，只是额外开启一个给 LinuxGateway 调用的 token 接口。

## Mac/Windows 节点配置

在每台节点启动 `BuildServer` 前设置：

```bash
export BUILD_SERVER_GATEWAY_TOKEN="为这台节点生成一个强随机 token"
export BUILD_SERVER_NODE_PLATFORMS="ios,android"   # Mac 常用
```

Windows Android 节点可设置：

```powershell
$env:BUILD_SERVER_GATEWAY_TOKEN="为这台节点生成一个强随机 token"
$env:BUILD_SERVER_NODE_PLATFORMS="android"
```

留空 `BUILD_SERVER_GATEWAY_TOKEN` 时，节点的 `/api/gateway/*` 接口不会启用。

LinuxGateway 需要能访问节点地址。例如：

```text
https://mac-build.example.com
https://win-build.example.com
```

这些地址可以是内网穿透地址，也可以是 VPN/内网地址。建议走 HTTPS。

## LinuxGateway 启动

开发运行：

```bash
./scripts/run-linux-gateway.sh http://127.0.0.1:5090
```

Windows 调试：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-linux-gateway.ps1
```

首次启动后，如果没有设置 `LINUX_GATEWAY_ADMIN_PASSWORD`，会生成初始密码：

```text
linuxgateway-data/initial-admin.txt
```

生产环境建议设置：

```bash
export LINUX_GATEWAY_ADMIN_PASSWORD="强密码"
export LINUX_GATEWAY_PUBLIC_BASE_URL="https://build.example.com"
export LINUX_GATEWAY_ALLOWED_ORIGINS="https://build.example.com"
export LINUX_GATEWAY_DATA_ROOT="/opt/unity-build-gateway/data"
```

## Linux 发布

从 Windows 发布 Linux x64：

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

## 使用流程

1. 在 Mac/Windows 节点启动原 `BuildServer`，并设置 `BUILD_SERVER_GATEWAY_TOKEN`。
2. 在 Linux 启动 `LinuxGateway`。
3. 登录 LinuxGateway 网页。
4. 新增设备：
   - 设备名称：例如 `Mac Build`
   - BuildServer 地址：例如 `https://mac-build.example.com`
   - Gateway Token：填写该节点的 `BUILD_SERVER_GATEWAY_TOKEN`
   - 平台：Mac 选 `iOS + Android`，Windows 选 `Android`
5. 刷新设备，确认能看到节点项目和配置。
6. 发起打包时选择目标设备、项目、配置。

## 安全注意

- LinuxGateway 的数据目录会保存节点 Gateway Token，必须限制系统权限。
- LinuxGateway 只应该暴露 HTTPS，不建议直接暴露明文 HTTP。
- 节点的 `/api/gateway/*` 只接受 `X-Gateway-Token`，不要把 token 放 URL。
- 节点不要对公网开放普通管理后台，能只给 LinuxGateway 访问最好。
- iOS 任务只能发到支持 `ios` 的 Mac 节点；Windows 节点只适合 Android APK/AAB。
