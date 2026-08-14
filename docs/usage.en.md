# Usage Guide

This document covers the complete usage paths for AutomationUnityBuildIOS: local CLI, iOS builds, Android builds, TikTok Mini-Game builds, store uploads, DesktopApp desktop client, BuildServer web platform, email notifications, storage management, template management, MCP/Agent entry, and LinuxGateway multi-node scheduling.

If you are new, we recommend following this order:

1. Prepare your Mac/Windows build environment.
2. Copy the Unity build scripts into your Unity project.
3. Generate a config and run a dry-run on Mac using the CLI.
4. Do a real build.
5. Deploy BuildServer when your team needs a web entry point.
6. Deploy LinuxGateway when multiple build machines need a unified entry point.

---

## Choosing a Mode

| Scenario | Recommended Mode | Notes |
|------|----------|------|
| Building iOS packages on your own Mac | CLI | Minimal components, just run `./AutomationUnityBuildIOS 06` |
| Both iOS + Android automation | CLI or BuildServer | CLI for solo, BuildServer for teams |
| TikTok Mini-Game WebGL build & upload | CLI | Use shortcut `12` to generate a TikTok config, supports WebGL build with API upload |
| Offline config management and builds on Windows | DesktopApp | Native desktop client, full-featured config editing, build execution, artifact browsing |
| QA/ops need a button-click build workflow | BuildServer | Browser login, submit tasks, view logs, download artifacts |
| Multiple Mac/Windows build machines | LinuxGateway + BuildServer | LinuxGateway is the unified entry; actual builds run on each node's BuildServer |
| Nodes behind NAT/intranet, unreachable externally | LinuxGateway Reverse Connection | Nodes connect outward to LinuxGateway, no public IP or port mapping needed |
| Let AI Agents participate in the build process | BuildServer MCP | Agents default to dry-run; full builds require authorization |

---

## Environment Setup

### Dev Machine

Building and publishing this tool requires:

- .NET 8 SDK.
- Windows, macOS, or Linux can all compile this project.
- If using Visual Studio, VS 2022 or later is recommended.

Basic verification:

```powershell
dotnet --version
dotnet build .\AutomationUnityBuildIOS.sln
```

### iOS Build Machine

The final iOS build must run on macOS, because Unity iOS Build Support and Xcode are only available on Mac.

Mac prerequisites:

- Xcode, opened at least once to accept the license and install components.
- Unity Hub, the corresponding Unity Editor version, and the iOS Build Support module.
- Git CLI, with the Mac able to access your Unity repository. SSH key recommended.
- Apple Developer account, certificates, provisioning profiles, or Xcode automatic signing.
- If not using a self-contained publish package, .NET 8 SDK must also be installed on the Mac.

Verification commands:

```bash
git --version
xcodebuild -version
/Applications/Unity/Hub/Editor/<UnityVersion>/Unity.app/Contents/MacOS/Unity -version
```

### Android Build Machine

Android builds can run on macOS or Windows.

Prerequisites:

- Unity Hub, the corresponding Unity Editor version, and Android Build Support.
- Android SDK, NDK, OpenJDK bundled with Unity, or your own Android toolchain.
- An Android keystore if signing release packages.
- A Google Play Console Service Account JSON with publish permissions for the target app, if uploading to Google Play.

---

## Unity Project Preparation

This tool invokes Unity Editor scripts via `-executeMethod`, so your Unity game repository needs the build scripts provided by this project.

iOS:

```text
UnityBuildScripts/Ios/BuildIOS.cs
```

Copy to your Unity project:

```text
Assets/Editor/BuildIOS.cs
```

The method it provides:

```text
BuildAutomation.IOSBuilder.Build
```

Android:

```text
UnityBuildScripts/Android/BuildAndroid.cs
```

Copy to your Unity project:

```text
Assets/Editor/BuildAndroid.cs
```

The method it provides:

```text
BuildAutomation.AndroidBuilder.Build
```

After updating AutomationUnityBuildIOS, if these scripts have changed, sync them to your Unity game repository as well.

---

## Local CLI Quick Start

### Publishing the Mac CLI from a Dev Machine

Apple Silicon Mac:

```powershell
.\scripts\publish-mac.ps1 -Runtime osx-arm64
```

Intel Mac:

```powershell
.\scripts\publish-mac.ps1 -Runtime osx-x64
```

The published output will be at:

```text
publish/osx-arm64
publish/osx-x64
```

Copy the entire directory to your Mac, e.g.:

```text
~/Downloads/publish_m1
```

### First Run on Mac

If macOS warns about an unidentified developer or unverified software, run the following in the publish directory:

```bash
cd ~/Downloads/publish_m1
xattr -cr .
chmod +x ./AutomationUnityBuildIOS
codesign --force --deep --sign - ./AutomationUnityBuildIOS
./AutomationUnityBuildIOS 00
```

`00` displays help and the shortcut command table.

### Creating a Config

iOS interactive config wizard:

```bash
./AutomationUnityBuildIOS 01
```

Equivalent full command:

```bash
./AutomationUnityBuildIOS init-config
```

Generate a blank iOS template:

```bash
./AutomationUnityBuildIOS init-config --config build-ios.json --template
```

Generate a blank Android template:

```bash
./AutomationUnityBuildIOS 11
```

Equivalent full command:

```bash
./AutomationUnityBuildIOS init-config --config build-android.json --template --platform android
```

It is recommended to store production configs under `configs/`, e.g.:

```text
configs/build-ios.dev.json
configs/build-ios.testflight.json
configs/build-android.internal.json
```

### Environment Check

Select a config and check the environment:

```bash
./AutomationUnityBuildIOS 04
```

Specify a config:

```bash
./AutomationUnityBuildIOS doctor --config configs/build-ios.dev.json
```

When debugging configs or running dry-runs on Windows, add:

```bash
--allow-non-mac
```

Actual iOS production builds must still run on macOS.

### Previewing Commands

Preview the pipeline without executing:

```bash
./AutomationUnityBuildIOS 05 --config configs/build-ios.dev.json
```

Equivalent full command:

```bash
./AutomationUnityBuildIOS run --config configs/build-ios.dev.json --dry-run --verbose --allow-non-mac
```

### Real Build

Select an existing config and run the full pipeline:

```bash
./AutomationUnityBuildIOS 06
```

Specify a config:

```bash
./AutomationUnityBuildIOS 06 --config configs/build-ios.dev.json
```

Full command:

```bash
./AutomationUnityBuildIOS run --config configs/build-ios.dev.json
```

### Common Skip Flags

| Flag | Effect |
|------|------|
| `--skip-git` | Skip Git pull/reset, use the existing project in the workspace |
| `--skip-unity` | Skip Unity export or Android build |
| `--skip-xcode` | Skip Xcode archive/export (iOS only; ignored for Android) |
| `--dry-run` | Print commands without executing builds or uploads |
| `--verbose` | Output more detailed paths and commands |
| `--allow-non-mac` | Allow iOS dry-run or config debugging on non-macOS |

### Shortcut Command Table

| Code | Description |
|------|------|
| `00` | Display help and shortcut command table |
| `01` | Interactive config wizard, generates a ready-to-use config file |
| `02` | Generate a blank iOS config template `build-ios.json` |
| `03` | List existing config files |
| `04` | Select a config and check the environment |
| `05` | Select a config and preview the full build command (dry-run) |
| `06` | Select a config and run the full build pipeline |
| `07` | Select a config and build, skipping Git sync |
| `08` | Select a config and build, skipping Unity export |
| `09` | Select a config and build, skipping Xcode compile/export |
| `10` | Select a config and edit its contents |
| `11` | Generate an Android APK/AAB config template `build-android.json` |
| `12` | Generate a TikTok Mini-Game config template `build-tiktok.json` |

Shortcuts can be followed by additional arguments:

```bash
./AutomationUnityBuildIOS 05 --config configs/build-ios.dev.json
./AutomationUnityBuildIOS 06 --config configs/build-ios.release.json
./AutomationUnityBuildIOS 10 --config configs/build-android.internal.json
```

---

## Config File Reference

Config files are JSON. See `build-ios.sample.json` for iOS, `build-android.sample.json` for Android, and `build-tiktok.sample.json` for TikTok.

### Common Fields

| Field | Description |
|------|------|
| `configName` | Display name for the config, shown in selection lists |
| `buildPlatform` | `ios`, `android`, or `tiktok` |
| `repositoryUrl` | Git clone URL for the Unity game repo, supports HTTPS/SSH |
| `allowedRepositoryUrls` | Repository whitelist, recommended for production |
| `branch` | Build branch |
| `workspaceRoot` | Git workspace root directory |
| `allowedWorkspaceRoots` | Allowed workspace root directories, prevents path escape |
| `projectDirectoryName` | Directory name after cloning the repo |
| `unityProjectRelativePath` | Path to the Unity project relative to the repo root; use `.` if the repo root is the Unity project |
| `unityVersion` | Unity Hub installed version, used to derive the Unity executable path |
| `unityExecutablePath` | Full path to the Unity executable; takes priority over `unityVersion` |
| `unityBuildMethod` | Unity Editor static method name |
| `artifactsRoot` | Build artifacts root directory |
| `allowedArtifactsRoots` | Allowed artifact root directories |
| `productName` | Unity Product Name |
| `bundleIdentifier` | iOS Bundle ID or Android Package Name |
| `bundleVersion` | Version number |
| `syncBundleVersionFromUnity` | Whether to sync the version from Unity PlayerSettings |
| `buildNumber` | iOS Build Number or Android versionCode |
| `autoIncrementBuildNumber` | Whether to auto-increment the build number after a successful build |
| `saveConfigSnapshot` | Whether to save a config snapshot in the log directory |

The three most commonly misconfigured values:

```text
repositoryUrl: Use the git clone URL, not the web page title.
unityProjectRelativePath: Usually ".", not build, Builds, or XcodeProject.
teamId: iOS uses the 10-character Apple Developer Team ID, not the company name.
```

### iOS Fields

| Field | Description |
|------|------|
| `scheme` | Default `Unity-iPhone` |
| `configuration` | Default `Release` |
| `exportMethod` | `development`, `ad-hoc`, `app-store`, etc. (Xcode export method) |
| `teamId` | Apple Developer Team ID, must be 10 alphanumeric characters |
| `signingStyle` | `automatic` or `manual` |
| `iosDeploymentTarget` | Minimum iOS version, e.g. `13.0` |
| `allowProvisioningUpdates` | Whether to allow Xcode to handle signing updates automatically |
| `generateExportOptionsPlist` | Whether to auto-generate `ExportOptions.plist` |
| `copyArchiveToOrganizer` | Whether to copy `.xcarchive` to Xcode Organizer |
| `appStoreConnectUploadEnabled` | Whether to auto-upload to App Store Connect/TestFlight |

### Android Fields

| Field | Description |
|------|------|
| `androidBuildFormat` | `apk`, `aab`, or `both` |
| `androidOutputDirectory` | Android output directory, auto-generated if left empty |
| `apkOutputPath` | APK output path, auto-generated if left empty |
| `aabOutputPath` | AAB output path, auto-generated if left empty |
| `androidMinSdkVersion` | Optional, override Min SDK |
| `androidTargetSdkVersion` | Optional, override Target SDK |
| `androidKeystoreName` | Keystore path or name |
| `androidKeystorePass` | Keystore password |
| `androidKeyaliasName` | Key alias |
| `androidKeyaliasPass` | Key alias password |
| `googlePlayUploadEnabled` | Whether to upload to Google Play |
| `googlePlayTrack` | `internal`, `alpha`, `beta`, `production` |
| `googlePlayReleaseStatus` | `draft`, `inProgress`, `halted`, `completed` |
| `googlePlayUploadArtifact` | Upload `apk`, `aab`, or `both` |

Never commit certificates, private keys, or long-lived tokens to the repository. When configs need to reference secrets, prefer local paths on the build machine and protect file permissions.

### TikTok Fields

| Field | Description |
|------|------|
| `tiktokAppId` | TikTok Open Platform App ID |
| `tiktokAccessToken` | TikTok Open Platform Access Token |
| `tiktokGameName` | TikTok Mini-Game name |
| `tiktokWebglOutputDirectory` | WebGL output directory, auto-generated if left empty |
| `tiktokUploadEnabled` | Whether to auto-upload to TikTok Open Platform |
| `tiktokApiEndpoint` | TikTok Open Platform API URL, defaults to `https://open-api.tiktokglobalshop.com` |

---

## iOS Build

### Basic Pipeline

The complete iOS pipeline:

1. Validate config safety boundaries and Git repository policy.
2. Check `git`, Unity, `xcodebuild`.
3. Create run directory and log directory.
4. Write `build-config-snapshot.json`.
5. Pull or update the Unity repository.
6. Invoke Unity BatchMode to export the iOS Xcode project.
7. Run `xcodebuild archive`.
8. Run `xcodebuild -exportArchive`.
9. Optionally copy `.xcarchive` to Xcode Organizer.
10. Optionally upload to App Store Connect/TestFlight.

### App Store Connect / TestFlight Upload

Enabling auto-upload requires `exportMethod` set to `app-store` and a configured App Store Connect API Key.

Example:

```json
{
  "exportMethod": "app-store",
  "appStoreConnectUploadEnabled": true,
  "appStoreConnectApiKeyPath": "~/Secrets/AuthKey_XXXXXXXXXX.p8",
  "appStoreConnectApiKeyId": "XXXXXXXXXX",
  "appStoreConnectApiIssuerId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
}
```

Notes:

- The `.p8` file must exist on the Mac build machine locally.
- Key ID and Issuer ID come from the App Store Connect API Key page.
- After a successful upload, the build enters the App Store Connect/TestFlight processing queue.
- Whether to submit for review or release to production follows App Store Connect's version policies.

### Common iOS Debug Methods

Sync Git and Unity only, skip Xcode:

```bash
./AutomationUnityBuildIOS run --config configs/build-ios.dev.json --skip-xcode
```

Skip Unity, reuse the existing Xcode project for archive/export:

```bash
./AutomationUnityBuildIOS run --config configs/build-ios.dev.json --skip-unity
```

Check config and environment only:

```bash
./AutomationUnityBuildIOS doctor --config configs/build-ios.dev.json
```

---

## Android Build

### Basic Pipeline

The complete Android pipeline:

1. Validate config safety boundaries and Git repository policy.
2. Check `git` and Unity.
3. Create run directory and log directory.
4. Write `build-config-snapshot.json`.
5. Pull or update the Unity repository.
6. Invoke Unity BatchMode to build APK/AAB.
7. Optionally upload to Google Play.

Android does not require Xcode; `--skip-xcode` is ignored.

### Building APK/AAB

Config:

```json
{
  "buildPlatform": "android",
  "unityBuildMethod": "BuildAutomation.AndroidBuilder.Build",
  "androidBuildFormat": "both"
}
```

`androidBuildFormat` options:

| Value | Result |
|-------|--------|
| `apk` | Generate APK only |
| `aab` | Generate AAB only |
| `both` | Generate both APK and AAB |

### Google Play Upload

You need to create a Service Account in Google Play Console and grant publish permissions for the target app.

Example:

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

Recommended: dry-run first:

```bash
./AutomationUnityBuildIOS run --config configs/build-android.internal.json --dry-run --verbose
```

Verify paths, package name, version, and upload artifact before running the real build.

---

## TikTok Mini-Game Build

### Basic Pipeline

The TikTok Mini-Game build pipeline:

1. Validate config safety boundaries and Git repository policy.
2. Check `git` and Unity.
3. Create run directory and log directory.
4. Write `build-config-snapshot.json`.
5. Pull or update the Unity repository.
6. Invoke Unity BatchMode to build WebGL.
7. Optionally upload to TikTok Open Platform.

TikTok builds do not require Xcode; `--skip-xcode` is ignored.

### Generate Config

```bash
./AutomationUnityBuildIOS 12
```

Equivalent full command:

```bash
./AutomationUnityBuildIOS init-config --config build-tiktok.json --template --platform tiktok
```

### Config Example

```json
{
  "buildPlatform": "tiktok",
  "unityBuildMethod": "BuildAutomation.TiktokBuilder.Build",
  "tiktokAppId": "your-app-id",
  "tiktokAccessToken": "your-access-token",
  "tiktokGameName": "Your Game",
  "tiktokUploadEnabled": true
}
```

### Real Build

```bash
./AutomationUnityBuildIOS run --config configs/build-tiktok.release.json
```

TikTok-related code lives in `Modules/Tiktok/`, completely independent from iOS/Android and does not affect existing build flows.

---

## Desktop Client

DesktopApp is a native Windows desktop client built on Avalonia UI 11 + .NET 8, reusing all core logic from the main project (AutomationWorkflow / BuildConfig / ConfigFileSelector / SampleFiles). It integrates CLI, BuildServer, and template management capabilities into a single desktop application with full offline support.

### Feature Pages

| Page | Features |
|------|----------|
| **Config Management** | Full-field editing for iOS/Android/TikTok, auto-sync config file names, one-click template population |
| **Build Task** | Real-time log tail, elapsed timer, clear logs, auto-scroll |
| **Environment Check** | Verify Unity, Git, Xcode, and other environment dependencies |
| **Artifact Browser** | File list, selection, double-click to open, file preview |
| **Storage Management** | Bulk delete with checkboxes, single delete, select-all, storage overview |
| **Email Notifications** | SMTP config (including 465 implicit SSL), contact list, email templates |
| **Project Profile** | ProjectProfile template, manages repo/workspace directories |
| **Unity Profile** | UnityProfile template, manages Unity version/path/BuildMethod/ProductName/BundleID |
| **Signing Profile** | SigningProfile template, manages iOS TeamID/ExportMethod/SigningStyle/Android Keystore |
| **Certificate Profile** | CertificateProfile template, manages ASC API Key/Google Play/TikTok Token |
| **Server Sync** | Connect to BuildServer REST API, bidirectional sync of templates and config files |
| **BuildServer Manager** | Auto-detect or manually select BuildServer.exe path, one-click start/stop, health check |
| **Data Management** | Export data types as JSON, import JSON with ID-based deduplication merge |
| **Help** | Usage guide and shortcut command reference |

### Publishing DesktopApp

```powershell
dotnet publish DesktopApp/DesktopApp.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true -o DesktopApp/bin/publish-vN
```

If the previous exe is still running, you will get an `UnauthorizedAccessException`. Stop it first:

```powershell
Stop-Process -Name DesktopApp -Force
```

Then publish to a new directory. Single-file output is approximately 89 MB.

Alternatively, use the publish script:

```powershell
.\scripts\publish-desktop.ps1
```

### Template Management

DesktopApp provides four types of configuration templates, stored in the `profiles/` directory:

| Template | File | Purpose |
|------|------|------|
| Project Profile | `projects.json` | Repository URL, workspace dir, artifacts dir, etc. |
| Unity Profile | `unity-profiles.json` | Unity version, path, BuildMethod, ProductName, BundleID |
| Signing Profile | `signing-profiles.json` | iOS TeamID, ExportMethod, SigningStyle, Android Keystore |
| Certificate Profile | `certificates.json` | ASC API Key, Google Play Service Account, TikTok Token |

At the top of the config management edit form, there are four template selectors. Pick one from each and click "Apply" to populate the corresponding fields in one click. After applying a template, the populated field sections are automatically hidden to reduce clutter.

### Server Sync

DesktopApp can connect to the BuildServer REST API for bidirectional sync:

- **Project templates**: Pull / push
- **Certificate templates**: Pull / push
- **Config files**: Browse server config list + download to local `configs/` directory

Connection info is persisted to `profiles/server-settings.json`.

The config management page also provides an "Import Config File" button to import JSON from any local path into `configs/`.

---

## Email Notifications

BuildServer supports automatic email notifications after build tasks complete, covering both success and failure outcomes.

### Configuration

Configure in the BuildServer web backend or DesktopApp email notifications page:

| Field | Description |
|------|------|
| SMTP Server | e.g. `smtp.gmail.com`, `smtp.qq.com` |
| SMTP Port | Common: 25 (plaintext), 465 (implicit SSL), 587 (STARTTLS) |
| Sender Email | Email address sending the notifications |
| Sender Password | Email authorization code or password |
| Enable SSL | Port 465 uses implicit SSL |
| Notification Contacts | Recipient email list, separated by commas or newlines |
| Email Template | Personalized email subject and body template |

### Notification Triggers

- **Build success**: Email includes build artifact paths, elapsed time, and config summary.
- **Build failure**: Email includes the failed step, error summary, and log path for quick troubleshooting.

The email notification service is implemented in `BuildServer/Services/EmailNotificationService.cs`.

---

## Storage Management

As build tasks accumulate, artifacts gradually consume disk space. BuildServer provides two storage management mechanisms:

### Automatic Cleanup

`MaintenanceService` automatically cleans up completed tasks and artifacts based on configured `RetentionDays` and `MaxArtifactBytes`.

### Manual Cleanup

In the web backend or DesktopApp storage management page, you can:

- View storage overview (total space, used space, task count, artifact size distribution).
- Select multiple historical tasks for bulk deletion.
- Delete artifacts for a single task.
- Select all to clear all historical artifacts.

The storage cleanup service is implemented in `BuildServer/Services/StorageCleanupService.cs`.

---

## Logs and Artifacts

Each run creates an independent directory under `artifactsRoot`, e.g.:

```text
~/UnityBuildArtifacts/YourUnityGame/20260625-153000/
```

Common contents:

| File or Directory | Description |
|------------|------|
| `Logs/automation.log` | Master pipeline log, includes steps, commands, elapsed time, and errors |
| `Logs/unity-editor.log` | Unity Editor's own build log |
| `Logs/unity-process.log` | stdout/stderr captured from the Unity process |
| `Logs/build-config-snapshot.json` | Config snapshot for this run, with basic redaction |
| `Logs/xcode-archive.log` | iOS archive log |
| `Logs/xcode-export.log` | iOS export log |
| `Logs/xcode-upload.log` | App Store Connect upload log |
| `.xcarchive` | iOS archive artifact |
| `.ipa` export directory | iOS export artifact |
| `.apk` / `.aab` | Android build artifacts |

Troubleshooting order:

1. Check the end of `automation.log` for the failed step.
2. If the Unity stage failed, check `unity-editor.log`.
3. If the iOS Xcode stage failed, check `xcode-archive.log` or `xcode-export.log`.
4. If the store upload failed, check `xcode-upload.log` or the Google Play upload error in the master log.

The logging system applies basic redaction to common sensitive information, such as credentials/tokens in URLs, `Bearer` tokens, and values for keys like `password/token/secret/apiKey`.

---

## BuildServer Web Platform

BuildServer is the Web/Agent entry point for the CLI. It provides:

- Web login.
- Project management.
- Config management.
- Build task queue.
- Real-time logs.
- Artifact download.
- User permissions.
- Audit logs.
- MCP/Agent tools.
- LinuxGateway node API.

The first version uses a single-machine, single-worker, serial queue to avoid concurrent contention between Unity, Xcode, Gradle, signing environments, and cache directories.

### Local Startup

Windows debugging:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-build-server.ps1
```

macOS/Linux debugging:

```bash
./scripts/run-build-server.sh
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

If `BUILD_SERVER_AGENT_TOKEN` is not set, a default MCP Agent Token is generated on first start:

```text
<DataRoot>/initial-agent-token.txt
```

### Production Environment Variables

Recommended for production:

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

Common variables:

| Variable | Description |
|------|------|
| `BUILD_SERVER_DATA_ROOT` | Data directory, stores users, projects, configs, tasks, audit JSON |
| `BUILD_SERVER_ADMIN_PASSWORD` | Admin password |
| `BUILD_SERVER_AGENT_TOKEN` | MCP Agent Token |
| `BUILD_SERVER_PUBLIC_BASE_URL` | Public-facing URL |
| `BUILD_SERVER_ALLOWED_ORIGINS` | Allowed web Origins; recommended when behind a reverse proxy |
| `BUILD_SERVER_ALLOWED_WORKSPACE_ROOTS` | Allowed workspace root directories |
| `BUILD_SERVER_ALLOWED_ARTIFACTS_ROOTS` | Allowed artifact root directories |
| `BUILD_SERVER_ALLOWED_CONFIG_ROOTS` | Allowed config file root directories |
| `BUILD_SERVER_ALLOWED_REPOSITORY_HOSTS` | Allowed Git hosts for registration |
| `BUILD_SERVER_GATEWAY_TOKEN` | Node API token; auto-generates `initial-gateway-token.txt` on first start if left empty |
| `BUILD_SERVER_NODE_PLATFORMS` | Current node capabilities, e.g. `ios,android` or `android` |

### Web Workflow

After logging in to the backend for the first time:

1. Add a project: fill in project name, Git repo, default branch, allowed branches, workspace, and artifact directory.
2. Add a config: select iOS or Android.
3. Configs can point to an existing JSON file or be generated from the web form.
4. Start a build: select project, config, branch, and optional parameters.
5. View status, real-time logs, and artifacts in the task list.

BuildServer generates an independent config snapshot for each task and invokes the CLI:

```text
AutomationUnityBuildIOS run --config <job-config.json>
```

### Publishing BuildServer to Mac

Apple Silicon Mac:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-build-server-mac.ps1 -Runtime osx-arm64
```

Intel Mac:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-build-server-mac.ps1 -Runtime osx-x64
```

The publish directory includes both BuildServer and the AutomationUnityBuildIOS CLI. For production, use:

```text
deploy/launchd/com.automationunity.buildserver.plist
```

It is recommended to designate a dedicated macOS user for running BuildServer, with Unity License, Xcode signing, certificates, provisioning profiles, and Git SSH keys all configured under that user.

### MCP / Agent

MCP endpoint:

```text
POST /mcp
Header: X-Agent-Token: <BUILD_SERVER_AGENT_TOKEN>
```

Supported tools:

| Tool | Description |
|------|------|
| `list_projects` | List available projects |
| `list_configs` | List build configs under a project |
| `start_build` | Submit an iOS or Android build task |
| `start_ios_build` | Legacy name, new integrations should use `start_build` |
| `get_build_status` | Query build task status |
| `tail_build_log` | Read recent log lines |
| `list_build_artifacts` | List task artifacts |

By default, Agents are only allowed `dryRun=true`. To allow real builds, enable `allowFullBuild` for the corresponding MCP Client, and recommend authorizing only specific projects.

Do not put Agent Tokens in URL query parameters. Use `X-Agent-Token` or `Authorization: Bearer`.

---

## LinuxGateway Multi-Node Entry

LinuxGateway is suitable for deployment on a Linux server with a public domain. It does not run Unity, store Unity projects, or hold Apple certificates; it only handles login, node registration, node selection, task forwarding, and log/artifact proxying.

Typical architecture:

```text
External users
  -> LinuxGateway Web/API
      -> Mac BuildServer       iOS + Android
      -> Windows BuildServer   Android
```

Without LinuxGateway, each Mac/Windows BuildServer can still be used independently.

### Starting LinuxGateway

Development:

```bash
./scripts/run-linux-gateway.sh http://127.0.0.1:5090
```

Windows debugging:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-linux-gateway.ps1
```

Default address:

```text
http://127.0.0.1:5090
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

### Publishing LinuxGateway to Linux

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

### Mode 1: Direct Node Connection

Direct connection is suitable when LinuxGateway can reach the Mac/Windows BuildServer, e.g. via VPN, intranet, tunnel, or public HTTPS.

Set before starting each BuildServer node:

```bash
export BUILD_SERVER_GATEWAY_TOKEN="strong-random-token"
export BUILD_SERVER_NODE_PLATFORMS="ios,android"
```

Windows Android node:

```powershell
$env:BUILD_SERVER_GATEWAY_TOKEN="strong-random-token"
$env:BUILD_SERVER_NODE_PLATFORMS="android"
```

You can also skip manually setting `BUILD_SERVER_GATEWAY_TOKEN`. BuildServer will auto-generate a Gateway Token on first start and save it to:

```text
<DataRoot>/initial-gateway-token.txt
```

BuildServer will enable:

```text
/api/gateway/*
```

LinuxGateway calls the node using:

```text
Header: X-Gateway-Token: <BUILD_SERVER_GATEWAY_TOKEN>
```

Add a device in the LinuxGateway web UI:

| Field | Example |
|------|------|
| Device Name | `Mac Build` |
| BuildServer URL | `https://mac-build.example.com` |
| Gateway Token | The node's `BUILD_SERVER_GATEWAY_TOKEN` |
| Platforms | Mac: `iOS + Android`, Windows: `Android` |

After saving, refresh the device to confirm that node projects and configs are visible.

### Mode 2: Reverse Node Connection

Reverse connection is suitable when nodes are behind NAT, home networks, or corporate intranets where LinuxGateway cannot directly access the node address. In this case, BuildServer initiates the connection to LinuxGateway.

Generate an Enrollment Token in the LinuxGateway web UI, then fill in the BuildServer Gateway connection page:

```text
Gateway URL: https://build.example.com
Enrollment Token: <token>
```

Alternatively, configure via environment variables so BuildServer auto-connects on startup:

```bash
export BUILD_SERVER_REVERSE_GATEWAY_ENABLED=true
export BUILD_SERVER_REVERSE_GATEWAY_URL="https://build.example.com"
export BUILD_SERVER_REVERSE_GATEWAY_ENROLLMENT_TOKEN="<token>"
export BUILD_SERVER_REVERSE_NODE_NAME="Mac Build"
```

Once connected, LinuxGateway displays the reverse-connected node. Node credentials are saved in the BuildServer data directory; after revoking a node, you must generate a new Enrollment Token to re-register.

The reverse connection is implemented in `LinuxGateway/Reverse/` and `BuildServer/Reverse/`.

### LinuxGateway Online Self-Update

LinuxGateway includes `SelfUpdateService`, which can check and download update packages from Gitee or GitHub Releases without requiring .NET SDK on the server.

Check for updates:

```text
GET /api/system/version
GET /api/system/update/check
```

Apply update (Admin only):

```text
POST /api/system/update/apply
```

The update process automatically backs up the current version, downloads a tar.gz update package, and generates an `apply-update.sh` script to complete the replacement and restart.

Configuration:

| Variable | Description |
|------|------|
| `LINUX_GATEWAY_UPDATE_SOURCE` | Update source: `gitee` or `github` |
| `LINUX_GATEWAY_UPDATE_REPO_OWNER` | Repository owner |
| `LINUX_GATEWAY_UPDATE_REPO_NAME` | Repository name |

### Submitting Builds via LinuxGateway

1. Log in to LinuxGateway.
2. Confirm the node is online on the devices page.
3. Refresh the node to ensure projects and configs are synced.
4. On the build task page, select device, project, config, and branch.
5. Submit the task.
6. View the status, logs, and artifacts returned by the remote node.

iOS tasks can only be sent to Mac nodes that support `ios`; Windows nodes are typically only suitable for Android APK/AAB.

---

## Security Recommendations

- Always set strong passwords in production; do not rely on initial password files long-term.
- Do not put `BUILD_SERVER_AGENT_TOKEN`, `BUILD_SERVER_GATEWAY_TOKEN`, or Enrollment Tokens in URLs. Use headers or server-side form storage.
- LinuxGateway and BuildServer data directories store users, tasks, node credentials, or tokens — restrict system permissions.
- Configure `BUILD_SERVER_ALLOWED_WORKSPACE_ROOTS`, `BUILD_SERVER_ALLOWED_ARTIFACTS_ROOTS`, `BUILD_SERVER_ALLOWED_CONFIG_ROOTS`, and `BUILD_SERVER_ALLOWED_REPOSITORY_HOSTS` for BuildServer.
- If a node backend is only used by LinuxGateway, avoid exposing the regular admin backend to the public internet.
- iOS certificates, provisioning profiles, App Store Connect `.p8` files, Android keystores, and Google Play Service Account JSON files should only be stored in secure local directories on the build machine.
- Never commit certificates, private keys, or long-lived tokens to Git.
- When accessing the web UI through a reverse proxy, configure `PUBLIC_BASE_URL` and `ALLOWED_ORIGINS` to avoid cross-origin request rejection or origin validation failure.

---

## FAQ

| Problem | Resolution |
|------|------|
| iOS build on Windows reports macOS required | iOS production builds must run on Mac; Windows only supports `--dry-run --allow-non-mac` for config debugging |
| Unity executable not found | Set `unityExecutablePath`, or verify that `unityVersion` matches an installed Unity Hub path |
| Git pull failed | Manually `git clone` on the build machine to verify SSH key or HTTPS credentials |
| Team ID validation failed | `teamId` must be a 10-character Apple Developer Team ID, not a company name |
| App Store Connect upload failed | Verify `exportMethod=app-store`, `.p8` path exists, Key ID and Issuer ID are correct |
| Android versionCode error | `buildNumber` must be a positive integer |
| Google Play upload failed | Check Service Account JSON path, app permissions, packageName, track, and upload artifact format |
| BuildServer login failed | Account is `admin`; copy only the value after `admin password:` in `initial-admin.txt` |
| Web write operations rejected | Check that `BUILD_SERVER_ALLOWED_ORIGINS` or `LINUX_GATEWAY_ALLOWED_ORIGINS` matches the access domain |
| LinuxGateway node 401 | Gateway Token is wrong or the node has not enabled `BUILD_SERVER_GATEWAY_TOKEN` |
| LinuxGateway node timeout | Check node address, port, firewall, tunnel, or reverse proxy |
| Artifact download failed | Confirm the artifact path is within BuildServer's allowed artifacts roots |

---

## Regression Testing

Developers can run:

```powershell
.\scripts\verify.ps1
```

It performs:

- Solution compilation.
- CLI project compilation.
- BuildServer compilation.
- LinuxGateway compilation.
- Help entry `00`.
- iOS sample dry-run.
- Android sample dry-run.
- Config editor open-exit.

The test suite covers 256+ test cases, spanning CLI argument parsing, config models, path safety, Git policies, Unity command building, Google Play API, TikTok configs, BuildServer API routes, LinuxGateway node communication, reverse connection, email notifications, and all other modules.

Run the full test suite:

```powershell
dotnet test .\AutomationUnityBuildIOS.Tests\AutomationUnityBuildIOS.Tests.csproj
```

If you only want to quickly check whether current changes affect compilation:

```powershell
dotnet build .\AutomationUnityBuildIOS.sln
```
