# Architecture

This project uses a modular, layered design with complete decoupling between the core build engine and platform entry points. CLI, BuildServer, DesktopApp, and LinuxGateway share the same core logic — differences lie only in the entry layer and interaction method.

## Directory Responsibilities

The tool is organized into the following directories by responsibility:

- `Cli/`: Command entry, command-line argument parsing, shortcut command mapping (`ShortcutCommands`).
- `ConsoleUi/`: Console interactive UI, including the init wizard, config editor, and input prompts.
- `Configuration/`: Config models, config file read/write, config file selection, path resolution, sample configs. Supports `ios`, `android`, and `tiktok` platform configs.
- `Workflow/`: Build pipeline orchestration, run context, runtime config updates, config snapshots.
- `Services/`: Cross-platform shared business capabilities, including Git sync, environment checks, directory preparation, Unity project validation, and path safety validation.
- `Modules/Common/`: Shared platform module capabilities, including the platform Pipeline interface, Unity command argument building, Unity log diagnostics, and Unity metadata reading.
- `Modules/Ios/`: iOS-specific build capabilities, including Unity Xcode project export, Xcode project/workspace location, `xcodebuild archive/export`.
- `Modules/Android/`: Android-specific build capabilities, including Unity APK/AAB builds, Google Play Publishing API uploads; the `GooglePlay/` subdirectory handles HTTP API, OAuth, and Service Account details.
- `Modules/Tiktok/`: TikTok Mini-Game specific capabilities, including the WebGL build pipeline (`TiktokBuildPipeline`), build service (`TiktokBuildService`), and TikTok Open Platform API upload (`TiktokUploadService`). Completely independent from iOS/Android — does not affect existing flows.
- `Infrastructure/`: Common infrastructure, including logging (`BuildLogger`), process execution (`ProcessRunner`), path tools (`PathTools`), path safety boundaries (`PathSafety`), and sensitive data redaction. These capabilities are shared by CLI, BuildServer, and DesktopApp.
- `UnityBuildScripts/Ios/`: iOS Unity Editor build script to copy into the Unity project's `Assets/Editor`.
- `UnityBuildScripts/Android/`: Android Unity Editor build script to copy into the Unity project's `Assets/Editor`.
- `BuildServer/`: Web build platform, including API (`ApiRoutes`), built-in frontend (`wwwroot/`), background worker (`BuildWorkerService`), MCP/Agent entry (`McpEndpoint`), Gateway node API (`GatewayEndpoint`), email notifications (`EmailNotificationService`), storage management (`StorageCleanupService`), artifact scanning (`ArtifactScanner`), maintenance cleanup (`MaintenanceService`), reverse connection (`Reverse/`), and JSON persistence (`Persistence/`).
- `LinuxGateway/`: Multi-device unified entry, including API (`ApiRoutes`), built-in frontend (`wwwroot/`), node gateway client (`NodeGatewayClient`), node refresh (`NodeRefreshService`), job refresh (`JobRefreshService`), reverse connection management (`Reverse/`), online self-update (`SelfUpdateService`), and JSON persistence (`Persistence/`).
- `DesktopApp/`: Avalonia UI 11 desktop client, including Views (14 pages), ViewModels (15 view models), Services (`BuildRunner` / `ProfileStore` / `ServerSyncService`), Controls (custom controls), and Styles (style resources). References the main project via `InternalsVisibleTo` + `Compile Remove` to reuse all core logic.
- `deploy/`: Production deployment templates, such as macOS `launchd` plist and Docker deployment files.

## Core Design Principles

### Pipeline Orchestration Separated from Platform Capabilities

`AutomationWorkflow` only orchestrates steps — it does not directly handle Git, Unity, Xcode, Google Play, or TikTok details. When adding platform capabilities, they should be placed in the corresponding `Modules/<Platform>/` directory and called by the workflow; cross-platform capabilities go in `Services/`. Three platform pipelines are currently supported:

- `IosBuildPipeline` — Git → Unity → Xcode archive/export → ASC upload
- `AndroidBuildPipeline` — Git → Unity → APK/AAB → Google Play upload
- `TiktokBuildPipeline` — Git → Unity → WebGL → TikTok Open Platform upload

### Config Editor Field-Driven

The config editor uses a field descriptor list to drive the menu and modification logic. When adding config fields, add an entry to the `ConfigEditor` field list first, avoiding scattered menu display and switch-case modification logic.

### Security Boundaries Across the Full Chain

When connecting to web backends, workers, or MCP/Agent, all entry points should reuse the pre-existing capabilities already implemented in the CLI:

- `PathSafetyValidator`: Validates that workspace, repository directories, Unity projects, artifacts, logs, Xcode outputs, and archive/export are all within allowed root directories.
- `GitRepositoryPolicyValidator`: Validates Git URL format and `allowedRepositoryUrls` whitelist.
- `BuildConfigSnapshotWriter`: Generates `Logs/build-config-snapshot.json` on each real run, recording the config snapshot, resolved paths, and CLI arguments.
- `SensitiveText`: Uniformly redacts common tokens/passwords in logs, commands, stdout/stderr, and config snapshots.

These capabilities should not be limited to the Web/API layer. The Worker must also invoke them before executing builds, to prevent bypassing entry points and triggering dangerous configs directly.

## BuildServer Architecture

BuildServer is the Web/Agent entry point for the CLI, with the following design:

### Serial Queue

The single-machine, single-worker, serial-queue design is intentional: Unity, Xcode, Gradle, signing certificates, and cache directories typically do not tolerate concurrent contention on the same machine. Multi-machine scaling is handled by LinuxGateway.

### Service Layer

| Service | File | Responsibility |
|------|------|------|
| Task Queue | `BuildQueueService.cs` | Manages build task enqueue, dequeue, and state transitions |
| Background Worker | `BuildWorkerService.cs` | Serially consumes the queue, invokes CLI for builds |
| Email Notifications | `EmailNotificationService.cs` | Sends success/failure email notifications after builds complete |
| Artifact Scanner | `ArtifactScanner.cs` | Scans task artifact directories, generates artifact lists |
| Log Reader | `LogFileReader.cs` | Reads and tails task logs |
| Storage Cleanup | `StorageCleanupService.cs` | Manual and automatic cleanup of historical artifacts |
| Maintenance | `MaintenanceService.cs` | Auto-cleanup by RetentionDays/MaxArtifactBytes |
| Auto Locator | `AutomationToolLocator.cs` | Locates the AutomationUnityBuildIOS CLI executable |

### Reverse Connection

The `BuildServer/Reverse/` directory implements BuildServer's ability to proactively connect to LinuxGateway, allowing nodes behind NAT/intranet to be scheduled by LinuxGateway without public exposure.

## LinuxGateway Architecture

LinuxGateway does not run Unity, store Unity projects, or hold Apple certificates. It only:

1. Provides web login and device management.
2. Registers nodes (direct or reverse connection).
3. Forwards tasks to the BuildServer on each node.
4. Proxies logs and artifacts.

### Service Layer

| Service | File | Responsibility |
|------|------|------|
| Node Gateway Client | `NodeGatewayClient.cs` | Calls node BuildServer's `/api/gateway/*` endpoints |
| Node Refresh | `NodeRefreshService.cs` | Periodically refreshes node status and project/config sync |
| Job Refresh | `JobRefreshService.cs` | Periodically refreshes remote task status, logs, and artifacts |
| Online Self-Update | `SelfUpdateService.cs` | Checks and downloads update packages from Gitee/GitHub Releases |

### Reverse Connection

The `LinuxGateway/Reverse/` directory manages Enrollment Token generation for BuildServer-initiated connections, node registration, and WebSocket long-connection maintenance.

### Online Self-Update

`SelfUpdateService` supports:
- Dual-source detection (Gitee + GitHub parallel latest version queries).
- Downloading tar.gz update packages.
- Generating an `apply-update.sh` script to complete backup + replacement + restart.
- No .NET SDK required on the server — only pre-compiled binaries are downloaded.

## DesktopApp Architecture

DesktopApp uses Avalonia UI 11 + .NET 8 and reuses all core logic from the main project via project reference:

- **InternalsVisibleTo** + **Compile Remove**: The main project's csproj appends declarations to let DesktopApp access internal members while excluding entry point files like Program.cs.
- **ProfileStore**: Uniformly manages persistence for four config template types (project/Unity/signing/certificate), stored in the `profiles/` directory.
- **ServerSyncService**: Connects to BuildServer REST API via HttpClient for bidirectional sync of templates and config files.
- **BuildRunner**: Wraps CLI invocation, providing real-time log output and build progress.
- **AvaloniaUseCompiledBindingsByDefault=false**: Uses runtime bindings, avoiding the need to declare x:DataType on every .axaml file.

Run `scripts/verify.ps1` for basic regression verification: compilation, help entry, dry-run, config editor open-exit.
