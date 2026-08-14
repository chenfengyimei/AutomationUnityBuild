# LinuxGateway Multi-Node Entry

`LinuxGateway` is an optional central entry point, suitable for deployment on a Linux server with a public domain. It does not run Unity, store Unity projects, or hold Apple certificates; it only handles web login, Mac/Windows build node registration, node selection, and task forwarding to each node's `BuildServer`.

LinuxGateway supports two node connection modes: direct connection (LinuxGateway proactively accesses the node) and reverse connection (the node proactively connects to LinuxGateway, suitable for NAT/intranet environments). It includes a built-in online self-update feature that downloads update packages from Gitee/GitHub Releases, requiring no .NET SDK on the server.

Without LinuxGateway, Mac/Windows `BuildServer` instances can still be used independently for login, configuration, and builds.

## Architecture

```text
External users
  -> LinuxGateway Web/API
      -> Mac BuildServer /api/gateway/*    iOS + Android
      -> Windows BuildServer /api/gateway/* Android APK/AAB
```

Each Mac/Windows node continues running the existing `BuildServer`, with an additional token-protected API for LinuxGateway to call.

## Mac/Windows Node Configuration

Set before starting `BuildServer` on each node:

```bash
export BUILD_SERVER_GATEWAY_TOKEN="strong-random-token for this node"
export BUILD_SERVER_NODE_PLATFORMS="ios,android"   # Common for Mac
```

Windows Android node:

```powershell
$env:BUILD_SERVER_GATEWAY_TOKEN="strong-random-token for this node"
$env:BUILD_SERVER_NODE_PLATFORMS="android"
```

If `BUILD_SERVER_GATEWAY_TOKEN` is left empty, the node's `/api/gateway/*` endpoints will not be enabled.

LinuxGateway must be able to reach the node address, e.g.:

```text
https://mac-build.example.com
https://win-build.example.com
```

These can be tunnel addresses, VPN/intranet addresses, or public HTTPS endpoints. HTTPS is recommended.

## Starting LinuxGateway

Development:

```bash
./scripts/run-linux-gateway.sh http://127.0.0.1:5090
```

Windows debugging:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-linux-gateway.ps1
```

If `LINUX_GATEWAY_ADMIN_PASSWORD` is not set, an initial password is generated on first start:

```text
linuxgateway-data/initial-admin.txt
```

Recommended for production:

```bash
export LINUX_GATEWAY_ADMIN_PASSWORD="strong-password"
export LINUX_GATEWAY_PUBLIC_BASE_URL="https://build.example.com"
export LINUX_GATEWAY_ALLOWED_ORIGINS="https://build.example.com"
export LINUX_GATEWAY_DATA_ROOT="/opt/unity-build-gateway/data"
```

## Publishing to Linux

Publish Linux x64 from Windows:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-linux-gateway-linux.ps1
```

Default output:

```text
publish/linux-gateway
```

Copy to Linux and run:

```bash
chmod +x ./LinuxGateway
./LinuxGateway --urls http://127.0.0.1:5090
```

For public access, use Nginx/Caddy for HTTPS and reverse proxy to `127.0.0.1:5090`.

## Usage Workflow

1. Start `BuildServer` on Mac/Windows nodes and set `BUILD_SERVER_GATEWAY_TOKEN`.
2. Start `LinuxGateway` on Linux.
3. Log in to the LinuxGateway web UI.
4. Add a device:
   - Device Name: e.g. `Mac Build`
   - BuildServer URL: e.g. `https://mac-build.example.com`
   - Gateway Token: the node's `BUILD_SERVER_GATEWAY_TOKEN`
   - Platforms: Mac: `iOS + Android`, Windows: `Android`
5. Refresh the device to confirm that node projects and configs are visible.
6. When submitting a build, select the target device, project, and config.

## Security Notes

- LinuxGateway's data directory stores node Gateway Tokens — restrict system permissions.
- LinuxGateway should only be exposed via HTTPS; plain HTTP is not recommended.
- Node `/api/gateway/*` only accepts `X-Gateway-Token` — do not put tokens in URLs.
- Nodes should not expose the regular admin backend to the public internet; restrict access to LinuxGateway only.
- iOS tasks can only be sent to Mac nodes that support `ios`; Windows nodes are only suitable for Android APK/AAB.

## Reverse Connection

Reverse connection is suitable when nodes are behind NAT, home networks, or corporate intranets where LinuxGateway cannot directly access the node address. In this case, BuildServer proactively connects to LinuxGateway — no public port exposure needed on the node.

### Configuration Steps

1. Generate an Enrollment Token in the LinuxGateway web UI.
2. Set environment variables on the BuildServer node:

```bash
export BUILD_SERVER_REVERSE_GATEWAY_ENABLED=true
export BUILD_SERVER_REVERSE_GATEWAY_URL="https://build.example.com"
export BUILD_SERVER_REVERSE_GATEWAY_ENROLLMENT_TOKEN="<token>"
export BUILD_SERVER_REVERSE_NODE_NAME="Mac Build"
```

3. Start BuildServer — it will automatically connect to LinuxGateway and register as a reverse-connected node.
4. After connecting, the node appears in the LinuxGateway web UI.
5. After revoking a node, a new Enrollment Token must be generated to re-register.

The reverse connection is implemented in `LinuxGateway/Reverse/` and `BuildServer/Reverse/`.

## Online Self-Update

LinuxGateway includes `SelfUpdateService`, which can check and download update packages from Gitee or GitHub Releases without requiring .NET SDK on the server.

### API Endpoints

| Endpoint | Method | Description |
|------|------|------|
| `/api/system/version` | GET | Get current version |
| `/api/system/update/check` | GET | Check for latest version |
| `/api/system/update/apply` | POST | Apply update (Admin only) |

### Update Process

1. Query the latest version from Gitee/GitHub Release API in parallel.
2. Download the tar.gz update package.
3. Generate an `apply-update.sh` script to complete backup + replacement + restart.

### Configuration

| Variable | Description |
|------|------|
| `LINUX_GATEWAY_UPDATE_SOURCE` | Update source: `gitee` or `github` |
| `LINUX_GATEWAY_UPDATE_REPO_OWNER` | Repository owner |
| `LINUX_GATEWAY_UPDATE_REPO_NAME` | Repository name |

## Docker Deployment

LinuxGateway supports Docker deployment, particularly suitable for older systems like CentOS 7 where the native `libstdc++` runtime may be too old. See [Docker Deployment Guide](linux-gateway-docker.md).
