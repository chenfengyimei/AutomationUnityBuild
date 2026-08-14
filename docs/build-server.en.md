# BuildServer Platform

BuildServer is the Web/Agent entry point for the automated build tool, supporting iOS, Android APK/AAB, and Google Play uploads. The first version uses a single Mac, single Worker, and serial queue to avoid concurrent contention between Unity, Xcode, Gradle, signing environments, and cache/credential state.

## Modules

- `BuildServer.Api`: ASP.NET Core Minimal API for login, projects, configs, tasks, artifacts, and audit.
- `BuildServer.Worker`: Background serial Worker that dequeues tasks and invokes the `AutomationUnityBuildIOS` CLI.
- `BuildServer.Web`: Built-in static frontend for web login and build submission.
- `BuildServer.Mcp`: `/mcp` JSON-RPC tool endpoint for Agent/AI use.
- `BuildServer.Reverse`: Reverse connection module that lets BuildServer proactively connect to LinuxGateway, suitable for NAT/intranet environments.
- `buildserver-data`: JSON persistence directory, storing users, projects, configs, tasks, artifacts, audit records, and Worker nodes.

## Local Startup

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-build-server.ps1
```

Default address:

```text
http://127.0.0.1:5088
```

Default account:

```text
admin
```

If `BUILD_SERVER_ADMIN_PASSWORD` is not set, a random password is generated on first start:

```text
<DataRoot>/initial-admin.txt
```

If `BUILD_SERVER_AGENT_TOKEN` is not set, a random Agent API Key is generated on first start:

```text
<DataRoot>/initial-agent-token.txt
```

Recommended for production:

```bash
export BUILD_SERVER_ADMIN_PASSWORD="strong-password"
export BUILD_SERVER_AGENT_TOKEN="strong-agent-token"
export BUILD_SERVER_PUBLIC_BASE_URL="https://build.example.com"
export BUILD_SERVER_ALLOWED_ORIGINS="https://build.example.com"
export BUILD_SERVER_ALLOWED_WORKSPACE_ROOTS="/Users/build/UnityBuildWorkspace"
export BUILD_SERVER_ALLOWED_ARTIFACTS_ROOTS="/Users/build/UnityBuildArtifacts"
export BUILD_SERVER_ALLOWED_CONFIG_ROOTS="/Users/build/BuildServerData/configs"
export BUILD_SERVER_ALLOWED_REPOSITORY_HOSTS="github.com"
```

Security defaults:

- Workspace is restricted to `~/UnityBuildWorkspace` by default.
- Artifacts are restricted to `~/UnityBuildArtifacts` by default.
- Config files are restricted to the `configs` subdirectory under BuildServer's data directory and the program's `configs` directory.
- Git repositories allow HTTPS/SSH URLs by default; in production, set `BUILD_SERVER_ALLOWED_REPOSITORY_HOSTS`, e.g. `github.com` or your company's Git server domain.
- If accessing the web UI through Nginx/Caddy or other reverse proxies, set `BUILD_SERVER_PUBLIC_BASE_URL` and `BUILD_SERVER_ALLOWED_ORIGINS`, otherwise cross-site request protection will reject writes with mismatched origins.

## Mac Publishing

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-build-server-mac.ps1 -Runtime osx-arm64
```

After publishing, use `deploy/launchd/com.automationunity.buildserver.plist` to run as a `buildbot` user. Certificates, provisioning profiles, Unity License, and Git SSH keys should all be installed under this dedicated macOS user.

## Required Data

After logging in for the first time:

1. Add a project: fill in project name, Git repo, default branch, allowed branches, workspace, and artifact directory.
2. Add a config: select iOS or Android. You can reference an existing config JSON file, or check "Generate new config file" to fill in Unity version, Bundle ID, platform-specific fields in the web form, and the server will generate the JSON and register it.
   - iOS fields include Team ID, Deployment Target, Export Method, Signing Style, whether to copy archive to Organizer, whether to upload to App Store Connect/TestFlight.
   - Android fields include APK/AAB/both, SDK versions, keystore, Google Play Service Account, track, release status, upload artifact.
3. Start a build: select project and config, submit the task.

BuildServer generates an independent config snapshot for each task, reserves the Build Number, and invokes the CLI:

```text
AutomationUnityBuildIOS run --config <job-config.json>
```

## MCP/Agent

MCP endpoint:

```text
POST /mcp
Header: X-Agent-Token: <BUILD_SERVER_AGENT_TOKEN>
```

Tools:

- `list_projects`
- `list_configs`
- `start_build`
- `start_ios_build` (legacy name, new integrations should use `start_build`)
- `get_build_status`
- `tail_build_log`
- `list_build_artifacts`

By default, Agents are only allowed `dryRun=true`. To allow real builds, set the corresponding `McpClientRecord.allowFullBuild` to `true` in the data, and recommend authorizing only specific projects. MCP only submits tasks by project and config ID — it does not accept arbitrary Git repos or paths.

New configs are not MCP-enabled by default; you must explicitly check "Allow MCP" in the web UI.

## Email Notifications

BuildServer includes a built-in email notification service (`EmailNotificationService`) that automatically sends emails after build tasks complete:

- **Build success**: Email includes build artifact paths, elapsed time, and config summary.
- **Build failure**: Email includes the failed step, error summary, and log path.

Supports SMTP 465 implicit SSL, contact lists, and personalized email templates. Configure SMTP server, port, sender credentials, and contact list in the web backend or DesktopApp email notifications page.

## Storage Management

As build tasks accumulate, artifacts gradually consume disk space. BuildServer provides two storage management mechanisms:

- **Automatic cleanup**: `MaintenanceService` auto-cleans completed tasks and artifacts based on `RetentionDays` and `MaxArtifactBytes`.
- **Manual cleanup**: View storage overview in the web backend or DesktopApp storage management page, bulk-delete or single-delete historical artifacts.

`StorageCleanupService` handles the actual artifact directory scanning and deletion.

## Reverse Connection

If the BuildServer node is behind NAT, a home network, or corporate intranet where LinuxGateway cannot directly access it, you can use reverse connection to have BuildServer proactively connect to LinuxGateway.

Generate an Enrollment Token in the LinuxGateway web UI, then configure BuildServer via environment variables:

```bash
export BUILD_SERVER_REVERSE_GATEWAY_ENABLED=true
export BUILD_SERVER_REVERSE_GATEWAY_URL="https://build.example.com"
export BUILD_SERVER_REVERSE_GATEWAY_ENROLLMENT_TOKEN="<token>"
export BUILD_SERVER_REVERSE_NODE_NAME="Mac Build"
```

After connecting, node credentials are saved in the BuildServer data directory. The `BuildServer/Reverse/` directory implements the reverse connection client logic.

## Security Boundaries

- Web/MCP only create tasks — they do not directly execute arbitrary shell commands.
- Worker executes serially — only one task runs at a time.
- Projects can restrict allowed branches.
- CLI internally continues to validate Git whitelists and path boundaries.
- Task artifact downloads require login authentication.
- Audit logs record logins, project creation, config creation, task submission/cancellation, and Worker registration.
- Maintenance service cleans up completed tasks and artifacts by `RetentionDays` and `MaxArtifactBytes`.
- Sensitive information (passwords, tokens) in email notifications is not echoed — used only for SMTP authentication.

## Multi-Mac Scaling

`WorkerNodeRecord` is already persisted, and `/api/workers` and `/api/workers/register` are provided. The first version's built-in Worker is suited for a single Mac; when scaling to multiple Macs, the recommended evolution is:

```text
Central BuildServer.Api + Database
Mac Worker A/B/C as independent processes
Workers pull tasks suited to them
Scheduling by Unity/Xcode version, project authorization, current load
```

At that point, JSON persistence should be replaced with SQLite/PostgreSQL to avoid concurrent file writes across machines.
