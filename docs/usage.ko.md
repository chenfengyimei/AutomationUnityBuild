# 사용 가이드

이 문서는 AutomationUnityBuildIOS의 전체 사용 경로를 다룹니다: 로컬 CLI, iOS 빌드, Android 빌드, TikTok 미니게임 빌드, 스토어 업로드, DesktopApp 데스크톱 클라이언트, BuildServer 웹 플랫폼, 이메일 알림, 스토리지 관리, 템플릿 관리, MCP/Agent 진입점, LinuxGateway 다중 노드 스케줄링.

처음 사용하시는 분은 다음 순서를 권장합니다:

1. Mac/Windows 빌드 환경을 준비합니다.
2. Unity 빌드 스크립트를 Unity 프로젝트에 복사합니다.
3. Mac에서 CLI로 설정을 생성하고 드라이런을 완료합니다.
4. 실제 빌드를 수행합니다.
5. 팀이 웹 진입점이 필요할 때 BuildServer를 배포합니다.
6. 여러 빌드 머신을 통합 진입점이 필요할 때 LinuxGateway를 배포합니다.

---

## 모드 선택

| 시나리오 | 권장 모드 | 비고 |
|------|----------|------|
| Mac에서 iOS 패키지 빌드 | CLI | 최소 구성, `./AutomationUnityBuildIOS 06` 직접 실행 |
| iOS + Android 모두 자동화 | CLI 또는 BuildServer | CLI는 개인용, BuildServer는 팀용 |
| TikTok 미니게임 WebGL 빌드 및 업로드 | CLI | 단축키 `12`로 TikTok 설정 생성, WebGL 빌드 후 API 업로드 지원 |
| Windows에서 오프라인 설정 관리 및 빌드 | DesktopApp | 네이티브 데스크톱 클라이언트, 전체 기능 설정 편집, 빌드 실행, 산출물 탐색 |
| QA/운영이 버튼 클릭으로 빌드 | BuildServer | 브라우저 로그인, 작업 제출, 로그 조회, 산출물 다운로드 |
| 여러 Mac/Windows 빌드 머신 | LinuxGateway + BuildServer | LinuxGateway는 통합 진입점만 담당, 실제 빌드는 각 노드의 BuildServer에서 실행 |
| 노드가 NAT/인트라넷 뒤에 있어 외부 접근 불가 | LinuxGateway 리버스 연결 | 노드가 LinuxGateway에 아웃바운드 연결, 공용 IP나 포트 매핑 불필요 |
| AI Agent가 빌드 프로세스에 참여 | BuildServer MCP | Agent는 기본적으로 드라이런, 실제 빌드에는 승인 필요 |

---

## 환경 설정

### 개발 머신

이 도구의 빌드와 게시에 필요한 것:

- .NET 8 SDK.
- Windows, macOS, Linux 어디서나 컴파일 가능.
- Visual Studio를 사용하는 경우 VS 2022 이상 권장.

기본 검증:

```powershell
dotnet --version
dotnet build .\AutomationUnityBuildIOS.sln
```

### iOS 빌드 머신

iOS 최종 빌드는 macOS에서 실행해야 합니다. Unity iOS Build Support와 Xcode는 Mac에서만 사용 가능합니다.

Mac 필수 요구사항:

- Xcode (라이선스 동의 및 컴포넌트 설치를 위해 최소 1회 실행).
- Unity Hub, 해당 Unity Editor 버전, iOS Build Support 모듈.
- Git CLI. Mac에서 Unity 저장소에 접근 가능해야 함. SSH 키 설정 권장.
- Apple Developer 계정, 인증서, 프로비저닝 프로파일, 또는 Xcode 자동 서명.
- Self-contained 게시 패키지를 사용하지 않는 경우 .NET 8 SDK도 필요.

검증 명령:

```bash
git --version
xcodebuild -version
/Applications/Unity/Hub/Editor/<UnityVersion>/Unity.app/Contents/MacOS/Unity -version
```

### Android 빌드 머신

Android 빌드는 macOS 또는 Windows에서 실행 가능합니다.

필수 요구사항:

- Unity Hub, 해당 Unity Editor 버전, Android Build Support.
- Unity에 번들된 Android SDK, NDK, OpenJDK, 또는 자체 Android 툴체인.
- 릴리스 패키지 서명용 Android keystore.
- Google Play 업로드용 Google Play Console Service Account JSON (대상 앱의 게시 권한 부여).

---

## Unity 프로젝트 준비

이 도구는 Unity의 `-executeMethod`로 Unity Editor 스크립트를 호출하므로, Unity 게임 저장소에 이 프로젝트에서 제공하는 빌드 스크립트를 추가해야 합니다.

iOS:

```text
UnityBuildScripts/Ios/BuildIOS.cs
```

Unity 프로젝트에 복사:

```text
Assets/Editor/BuildIOS.cs
```

제공하는 메서드:

```text
BuildAutomation.IOSBuilder.Build
```

Android:

```text
UnityBuildScripts/Android/BuildAndroid.cs
```

Unity 프로젝트에 복사:

```text
Assets/Editor/BuildAndroid.cs
```

제공하는 메서드:

```text
BuildAutomation.AndroidBuilder.Build
```

AutomationUnityBuildIOS 업데이트 후 이 스크립트에 변경이 있으면 Unity 게임 저장소에도 동기화하세요.

---

## 로컬 CLI 빠른 시작

### 개발 머신에서 Mac CLI 게시

Apple Silicon Mac:

```powershell
.\scripts\publish-mac.ps1 -Runtime osx-arm64
```

Intel Mac:

```powershell
.\scripts\publish-mac.ps1 -Runtime osx-x64
```

게시 산출물 출력 위치:

```text
publish/osx-arm64
publish/osx-x64
```

전체 디렉터리를 Mac에 복사합니다. 예:

```text
~/Downloads/publish_m1
```

### Mac 첫 실행

macOS에서 "확인되지 않은 개발자" 또는 "악성 소프트웨어인지 확인할 수 없음" 경고가 나오면, 게시 디렉터리에서 다음을 실행:

```bash
cd ~/Downloads/publish_m1
xattr -cr .
chmod +x ./AutomationUnityBuildIOS
codesign --force --deep --sign - ./AutomationUnityBuildIOS
./AutomationUnityBuildIOS 00
```

`00`은 도움말과 단축 명령 표를 표시합니다.

### 설정 생성

iOS 대화형 설정 마법사:

```bash
./AutomationUnityBuildIOS 01
```

동등한 전체 명령:

```bash
./AutomationUnityBuildIOS init-config
```

빈 iOS 템플릿 생성:

```bash
./AutomationUnityBuildIOS init-config --config build-ios.json --template
```

빈 Android 템플릿 생성:

```bash
./AutomationUnityBuildIOS 11
```

동등한 전체 명령:

```bash
./AutomationUnityBuildIOS init-config --config build-android.json --template --platform android
```

프로덕션 설정은 `configs/` 아래에 배치를 권장:

```text
configs/build-ios.dev.json
configs/build-ios.testflight.json
configs/build-android.internal.json
```

### 환경 검사

설정을 선택하고 환경 검사:

```bash
./AutomationUnityBuildIOS 04
```

설정 지정:

```bash
./AutomationUnityBuildIOS doctor --config configs/build-ios.dev.json
```

Windows에서 설정 디버깅이나 드라이런 시:

```bash
--allow-non-mac
```

iOS 프로덕션 빌드는 여전히 macOS에서 실행해야 합니다.

### 명령 미리보기

실행하지 않고 파이프라인 미리보기:

```bash
./AutomationUnityBuildIOS 05 --config configs/build-ios.dev.json
```

동등한 전체 명령:

```bash
./AutomationUnityBuildIOS run --config configs/build-ios.dev.json --dry-run --verbose --allow-non-mac
```

### 실제 빌드

기존 설정을 선택하고 전체 파이프라인 실행:

```bash
./AutomationUnityBuildIOS 06
```

설정 지정:

```bash
./AutomationUnityBuildIOS 06 --config configs/build-ios.dev.json
```

전체 명령:

```bash
./AutomationUnityBuildIOS run --config configs/build-ios.dev.json
```

### 일반 스킵 플래그

| 플래그 | 효과 |
|------|------|
| `--skip-git` | Git 풀/리셋을 건너뛰고 워크스페이스의 기존 프로젝트 사용 |
| `--skip-unity` | Unity 내보내기 또는 Android 빌드를 건너뜀 |
| `--skip-xcode` | Xcode archive/export를 건너뜀 (iOS 전용, Android에서는 무시됨) |
| `--dry-run` | 명령어만 출력하고 빌드나 업로드를 실행하지 않음 |
| `--verbose` | 더 상세한 경로와 명령 출력 |
| `--allow-non-mac` | 비 macOS에서 iOS 드라이런이나 설정 디버깅 허용 |

### 단축 명령 표

| 코드 | 설명 |
|------|------|
| `00` | 도움말 및 단축 명령 표 표시 |
| `01` | 대화형 설정 마법사, 바로 사용 가능한 설정 파일 생성 |
| `02` | 빈 iOS 설정 템플릿 `build-ios.json` 생성 |
| `03` | 기존 설정 파일 목록 표시 |
| `04` | 설정을 선택하고 환경 검사 |
| `05` | 설정을 선택하고 전체 빌드 명령 미리보기 (드라이런) |
| `06` | 설정을 선택하고 전체 빌드 파이프라인 실행 |
| `07` | 설정을 선택하고 빌드, Git 동기화 건너뜀 |
| `08` | 설정을 선택하고 빌드, Unity 내보내기 건너뜀 |
| `09` | 설정을 선택하고 빌드, Xcode 컴파일/내보내기 건너뜀 |
| `10` | 설정을 선택하고 내용 편집 |
| `11` | Android APK/AAB 설정 템플릿 `build-android.json` 생성 |
| `12` | TikTok 미니게임 설정 템플릿 `build-tiktok.json` 생성 |

단축 명령에는 추가 인수를 붙일 수 있습니다:

```bash
./AutomationUnityBuildIOS 05 --config configs/build-ios.dev.json
./AutomationUnityBuildIOS 06 --config configs/build-ios.release.json
./AutomationUnityBuildIOS 10 --config configs/build-android.internal.json
```

---

## 설정 파일 참조

설정 파일은 JSON입니다. iOS 예는 `build-ios.sample.json`, Android는 `build-android.sample.json`, TikTok는 `build-tiktok.sample.json`을 참조하세요.

### 공통 필드

| 필드 | 설명 |
|------|------|
| `configName` | 설정 표시 이름, 선택 목록에 표시 |
| `buildPlatform` | `ios`, `android`, 또는 `tiktok` |
| `repositoryUrl` | Unity 게임 저장소의 Git 클론 URL, HTTPS/SSH 지원 |
| `allowedRepositoryUrls` | 저장소 화이트리스트, 프로덕션 권장 |
| `branch` | 빌드 브랜치 |
| `workspaceRoot` | Git 워크스페이스 루트 디렉터리 |
| `allowedWorkspaceRoots` | 허용된 워크스페이스 루트 디렉터리, 경로 이스케이프 방지 |
| `projectDirectoryName` | 저장소 클론 후 디렉터리 이름 |
| `unityProjectRelativePath` | 저장소 루트에서 Unity 프로젝트까지의 상대 경로. 저장소 루트가 Unity 프로젝트인 경우 `.` |
| `unityVersion` | Unity Hub 설치 버전, Unity 실행 파일 경로 추론에 사용 |
| `unityExecutablePath` | Unity 실행 파일의 전체 경로. `unityVersion`보다 우선 |
| `unityBuildMethod` | Unity Editor 정적 메서드 이름 |
| `artifactsRoot` | 빌드 산출물 루트 디렉터리 |
| `allowedArtifactsRoots` | 허용된 산출물 루트 디렉터리 |
| `productName` | Unity Product Name |
| `bundleIdentifier` | iOS Bundle ID 또는 Android Package Name |
| `bundleVersion` | 버전 번호 |
| `syncBundleVersionFromUnity` | Unity PlayerSettings에서 버전을 동기화할지 여부 |
| `buildNumber` | iOS Build Number 또는 Android versionCode |
| `autoIncrementBuildNumber` | 빌드 성공 후 빌드 번호를 자동 증가시킬지 여부 |
| `saveConfigSnapshot` | 로그 디렉터리에 설정 스냅샷을 저장할지 여부 |

가장 자주 틀리는 3 가지 값:

```text
repositoryUrl: git clone URL을 사용. 웹 페이지 제목이 아님.
unityProjectRelativePath: 보통 .. build, Builds, XcodeProject가 아님.
teamId: iOS는 10자 Apple Developer Team ID. 회사명이 아님.
```

### iOS 필드

| 필드 | 설명 |
|------|------|
| `scheme` | 기본값 `Unity-iPhone` |
| `configuration` | 기본값 `Release` |
| `exportMethod` | `development`, `ad-hoc`, `app-store` 등 (Xcode 내보내기 방식) |
| `teamId` | Apple Developer Team ID, 10자 영숫자여야 함 |
| `signingStyle` | `automatic` 또는 `manual` |
| `iosDeploymentTarget` | iOS 최소 버전, 예: `13.0` |
| `allowProvisioningUpdates` | Xcode가 서명 업데이트를 자동으로 처리하도록 허용할지 여부 |
| `generateExportOptionsPlist` | `ExportOptions.plist`를 자동 생성할지 여부 |
| `copyArchiveToOrganizer` | `.xcarchive`를 Xcode Organizer에 복사할지 여부 |
| `appStoreConnectUploadEnabled` | App Store Connect/TestFlight에 자동 업로드할지 여부 |

### Android 필드

| 필드 | 설명 |
|------|------|
| `androidBuildFormat` | `apk`, `aab`, 또는 `both` |
| `androidOutputDirectory` | Android 출력 디렉터리, 비어 있으면 자동 생성 |
| `apkOutputPath` | APK 출력 경로, 비어 있으면 자동 생성 |
| `aabOutputPath` | AAB 출력 경로, 비어 있으면 자동 생성 |
| `androidMinSdkVersion` | 선택 사항, Min SDK 덮어쓰기 |
| `androidTargetSdkVersion` | 선택 사항, Target SDK 덮어쓰기 |
| `androidKeystoreName` | keystore 경로 또는 이름 |
| `androidKeystorePass` | keystore 비밀번호 |
| `androidKeyaliasName` | key alias |
| `androidKeyaliasPass` | key alias 비밀번호 |
| `googlePlayUploadEnabled` | Google Play에 업로드할지 여부 |
| `googlePlayTrack` | `internal`, `alpha`, `beta`, `production` |
| `googlePlayReleaseStatus` | `draft`, `inProgress`, `halted`, `completed` |
| `googlePlayUploadArtifact` | `apk`, `aab`, 또는 `both` 업로드 |

인증서, 개인 키, 장기 토큰을 저장소에 커밋하지 마세요. 설정에서 시크릿을 참조해야 하는 경우, 빌드 머신의 로컬 경로를 우선하고 파일 권한을 보호하세요.

### TikTok 필드

| 필드 | 설명 |
|------|------|
| `tiktokAppId` | TikTok 오픈 플랫폼 App ID |
| `tiktokAccessToken` | TikTok 오픈 플랫폼 Access Token |
| `tiktokGameName` | TikTok 미니게임 이름 |
| `tiktokWebglOutputDirectory` | WebGL 출력 디렉터리, 비어 있으면 자동 생성 |
| `tiktokUploadEnabled` | TikTok 오픈 플랫폼에 자동 업로드할지 여부 |
| `tiktokApiEndpoint` | TikTok 오픈 플랫폼 API URL, 기본값 `https://open-api.tiktokglobalshop.com` |

---

## iOS 빌드

### 기본 파이프라인

전체 iOS 파이프라인:

1. 설정 보안 경계 및 Git 저장소 정책 검증.
2. `git`, Unity, `xcodebuild` 확인.
3. 실행 디렉터리와 로그 디렉터리 생성.
4. `build-config-snapshot.json` 작성.
5. Unity 저장소를 풀 또는 업데이트.
6. Unity BatchMode로 iOS Xcode 프로젝트 내보내기.
7. `xcodebuild archive` 실행.
8. `xcodebuild -exportArchive` 실행.
9. 선택적으로 `.xcarchive`를 Xcode Organizer에 복사.
10. 선택적으로 App Store Connect/TestFlight에 업로드.

### App Store Connect / TestFlight 업로드

자동 업로드를 활성화하려면 `exportMethod`를 `app-store`로 설정하고 App Store Connect API Key를 구성해야 합니다.

예:

```json
{
  "exportMethod": "app-store",
  "appStoreConnectUploadEnabled": true,
  "appStoreConnectApiKeyPath": "~/Secrets/AuthKey_XXXXXXXXXX.p8",
  "appStoreConnectApiKeyId": "XXXXXXXXXX",
  "appStoreConnectApiIssuerId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
}
```

참고:

- `.p8` 파일은 Mac 빌드 머신에 로컬로 존재해야 합니다.
- Key ID와 Issuer ID는 App Store Connect API Key 페이지에서 가져옵니다.
- 업로드 성공 후, 빌드는 App Store Connect/TestFlight 처리 대기열에 들어갑니다.
- 심사 제출 여부나 프로덕션 릴리스 여부는 App Store Connect의 버전 정책에 따릅니다.

### 일반적인 iOS 디버깅 방법

Git과 Unity만 동기화, Xcode 건너뜀:

```bash
./AutomationUnityBuildIOS run --config configs/build-ios.dev.json --skip-xcode
```

Unity 건너뛰고 기존 Xcode 프로젝트를 재사용하여 archive/export:

```bash
./AutomationUnityBuildIOS run --config configs/build-ios.dev.json --skip-unity
```

설정과 환경만 검사:

```bash
./AutomationUnityBuildIOS doctor --config configs/build-ios.dev.json
```

---

## Android 빌드

### 기본 파이프라인

전체 Android 파이프라인:

1. 설정 보안 경계 및 Git 저장소 정책 검증.
2. `git`과 Unity 확인.
3. 실행 디렉터리와 로그 디렉터리 생성.
4. `build-config-snapshot.json` 작성.
5. Unity 저장소를 풀 또는 업데이트.
6. Unity BatchMode로 APK/AAB 빌드.
7. 선택적으로 Google Play에 업로드.

Android는 Xcode가 필요하지 않습니다. `--skip-xcode`는 무시됩니다.

### APK/AAB 빌드

설정:

```json
{
  "buildPlatform": "android",
  "unityBuildMethod": "BuildAutomation.AndroidBuilder.Build",
  "androidBuildFormat": "both"
}
```

`androidBuildFormat` 옵션:

| 값 | 결과 |
|-------|--------|
| `apk` | APK만 생성 |
| `aab` | AAB만 생성 |
| `both` | APK와 AAB 모두 생성 |

### Google Play 업로드

Google Play Console에서 Service Account를 생성하고 대상 앱의 게시 권한을 부여해야 합니다.

예:

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

권장: 먼저 드라이런:

```bash
./AutomationUnityBuildIOS run --config configs/build-android.internal.json --dry-run --verbose
```

경로, 패키지 이름, 버전, 업로드 산출물을 확인한 후 실제 빌드를 실행하세요.

---

## TikTok 미니게임 빌드

### 기본 파이프라인

TikTok 미니게임 빌드 파이프라인:

1. 설정 보안 경계 및 Git 저장소 정책 검증.
2. `git`과 Unity 확인.
3. 실행 디렉터리와 로그 디렉터리 생성.
4. `build-config-snapshot.json` 작성.
5. Unity 저장소를 풀 또는 업데이트.
6. Unity BatchMode로 WebGL 빌드.
7. 선택적으로 TikTok 오픈 플랫폼에 업로드.

TikTok 빌드는 Xcode가 필요하지 않습니다. `--skip-xcode`는 무시됩니다.

### 설정 생성

```bash
./AutomationUnityBuildIOS 12
```

동등한 전체 명령:

```bash
./AutomationUnityBuildIOS init-config --config build-tiktok.json --template --platform tiktok
```

### 설정 예

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

### 실제 빌드

```bash
./AutomationUnityBuildIOS run --config configs/build-tiktok.release.json
```

TikTok 관련 코드는 `Modules/Tiktok/`에 있으며 iOS/Android와 완전히 독립적이고 기존 빌드 흐름에 영향을 주지 않습니다.

---

## 데스크톱 클라이언트

DesktopApp은 Avalonia UI 11 + .NET 8 기반의 네이티브 Windows 데스크톱 클라이언트로, 메인 프로젝트의 모든 핵심 로직(AutomationWorkflow / BuildConfig / ConfigFileSelector / SampleFiles)을 재사용합니다. CLI, BuildServer, 템플릿 관리 기능을 하나의 데스크톱 앱으로 통합하며, 모든 작업을 오프라인으로 사용할 수 있습니다.

### 기능 페이지

| 페이지 | 기능 |
|------|----------|
| **설정 관리** | iOS/Android/TikTok 전체 필드 편집, 설정 파일 이름 자동 동기화, 템플릿 선택기 원클릭 입력 |
| **빌드 작업** | 실시간 로그 tail, 경과 타이머, 로그 지우기, 자동 스크롤 |
| **환경 검사** | Unity, Git, Xcode 등 환경 종속성 검증 |
| **산출물 탐색기** | 파일 목록, 선택, 더블클릭으로 열기, 파일 미리보기 |
| **스토리지 관리** | 체크박스 대량 삭제, 단일 삭제, 전체 선택, 스토리지 개요 |
| **이메일 알림** | SMTP 설정(465 암시적 SSL 포함), 연락처 목록, 이메일 템플릿 |
| **프로젝트 관리** | ProjectProfile 템플릿, 저장소/워크스페이스 디렉터리 등 관리 |
| **Unity 관리** | UnityProfile 템플릿, Unity 버전/경로/BuildMethod/ProductName/BundleID 관리 |
| **서명 관리** | SigningProfile 템플릿, iOS TeamID/ExportMethod/SigningStyle/Android Keystore 관리 |
| **인증서 관리** | CertificateProfile 템플릿, ASC API Key/Google Play/TikTok Token 관리 |
| **서버 동기화** | BuildServer REST API에 연결, 템플릿과 설정 파일의 양방향 동기화 |
| **BuildServer 관리** | BuildServer.exe 경로 자동 감지 또는 수동 선택, 원클릭 시작/중지, 헬스 체크 |
| **데이터 관리** | 각 데이터 유형을 JSON으로 내보내기, JSON 가져오기 시 ID 기반 중복 제거 병합 |
| **도움말** | 사용 가이드 및 단축 명령 참조 |

### DesktopApp 게시

```powershell
dotnet publish DesktopApp/DesktopApp.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true -o DesktopApp/bin/publish-vN
```

이전 exe가 실행 중인 경우 `UnauthorizedAccessException`이 발생합니다. 먼저 중지하세요:

```powershell
Stop-Process -Name DesktopApp -Force
```

그 후 새 디렉터리에 게시합니다. 단일 파일 출력은 약 89 MB입니다.

게시 스크립트도 사용 가능:

```powershell
.\scripts\publish-desktop.ps1
```

### 템플릿 관리

DesktopApp은 4종류의 설정 템플릿을 제공하며, 데이터는 `profiles/` 디렉터리에 저장됩니다:

| 템플릿 | 파일 | 용도 |
|------|------|------|
| 프로젝트 관리 | `projects.json` | 저장소 URL, 워크스페이스 디렉터리, 산출물 디렉터리 등 |
| Unity 관리 | `unity-profiles.json` | Unity 버전, 경로, BuildMethod, ProductName, BundleID |
| 서명 관리 | `signing-profiles.json` | iOS TeamID, ExportMethod, SigningStyle, Android Keystore |
| 인증서 관리 | `certificates.json` | ASC API Key, Google Play Service Account, TikTok Token |

설정 관리 페이지의 편집 폼 상단에 4개의 템플릿 선택기가 있습니다. 각각에서 하나를 선택하고 "적용"을 클릭하면 해당 필드가 원클릭으로 채워집니다. 템플릿 적용 후 채워진 필드 섹션은 자동으로 숨겨져 화면의 복잡성을 줄입니다.

### 서버 동기화

DesktopApp은 BuildServer REST API에 연결하여 양방향 동기화가 가능:

- **프로젝트 템플릿**: 풀/푸시
- **인증서 템플릿**: 풀/푸시
- **설정 파일**: 서버 설정 목록 탐색 + 로컬 `configs/` 디렉터리로 다운로드

연결 정보는 `profiles/server-settings.json`에 영속화됩니다.

설정 관리 페이지에는 "설정 파일 가져오기" 버튼도 있어, 로컬의 임의 위치에서 JSON을 `configs/`로 가져올 수 있습니다.

---

## 이메일 알림

BuildServer는 빌드 작업 완료 후 자동으로 이메일 알림을 발송합니다. 성공과 실패 모두를 커버합니다.

### 설정

BuildServer 웹 백엔드 또는 DesktopApp 이메일 알림 페이지에서 설정:

| 필드 | 설명 |
|------|------|
| SMTP 서버 | 예: `smtp.gmail.com`, `smtp.qq.com` |
| SMTP 포트 | 일반: 25(평문), 465(암시적 SSL), 587(STARTTLS) |
| 발신자 이메일 | 알림을 발송하는 이메일 주소 |
| 발신자 비밀번호 | 이메일 인증 코드 또는 비밀번호 |
| SSL 활성화 | 포트 465는 암시적 SSL 사용 |
| 알림 수신자 | 수신자 이메일 목록, 쉼표 또는 줄바꿈으로 구분 |
| 이메일 템플릿 | 맞춤형 이메일 제목과 본문 템플릿 |

### 알림 트리거

- **빌드 성공**: 이메일에 빌드 산출물 경로, 경과 시간, 설정 요약 포함.
- **빌드 실패**: 이메일에 실패 단계, 오류 요약, 로그 경로 포함. 빠른 문제 해결에 편리.

이메일 알림 서비스는 `BuildServer/Services/EmailNotificationService.cs`에 구현되어 있습니다.

---

## 스토리지 관리

빌드 작업이 누적되면 산출물이 점차 디스크 공간을 소비합니다. BuildServer는 두 가지 스토리지 관리 메커니즘을 제공합니다:

### 자동 정리

`MaintenanceService`가 구성된 `RetentionDays`와 `MaxArtifactBytes`를 기준으로 완료된 작업과 산출물을 자동 정리합니다.

### 수동 정리

웹 백엔드 또는 DesktopApp 스토리지 관리 페이지에서:

- 스토리지 개요 확인 (전체 공간, 사용량, 작업 수, 산출물 크기 분포).
- 여러 기록 작업을 선택하여 대량 삭제.
- 단일 작업의 산출물 삭제.
- 전체 선택으로 모든 기록 산출물 삭제.

스토리지 정리 서비스는 `BuildServer/Services/StorageCleanupService.cs`에 구현되어 있습니다.

---

## 로그와 산출물

매 실행마다 `artifactsRoot` 아래에 독립적인 디렉터리가 생성됩니다. 예:

```text
~/UnityBuildArtifacts/YourUnityGame/20260625-153000/
```

일반적인 내용:

| 파일 또는 디렉터리 | 설명 |
|------------|------|
| `Logs/automation.log` | 마스터 파이프라인 로그. 단계, 명령, 경과 시간, 오류 포함 |
| `Logs/unity-editor.log` | Unity Editor 자체 빌드 로그 |
| `Logs/unity-process.log` | Unity 프로세스에서 캡처한 stdout/stderr |
| `Logs/build-config-snapshot.json` | 이번 실행의 설정 스냅샷. 기본 마스킹 적용 |
| `Logs/xcode-archive.log` | iOS archive 로그 |
| `Logs/xcode-export.log` | iOS export 로그 |
| `Logs/xcode-upload.log` | App Store Connect 업로드 로그 |
| `.xcarchive` | iOS 아카이브 산출물 |
| `.ipa` 내보내기 디렉터리 | iOS 내보내기 산출물 |
| `.apk` / `.aab` | Android 빌드 산출물 |

문제 해결 순서:

1. 먼저 `automation.log` 끝부분에서 실패 단계 확인.
2. Unity 단계 실패 시 `unity-editor.log` 확인.
3. iOS Xcode 단계 실패 시 `xcode-archive.log` 또는 `xcode-export.log` 확인.
4. 스토어 업로드 실패 시 `xcode-upload.log` 또는 마스터 로그의 Google Play 업로드 오류 확인.

로그 시스템은 URL 내 자격 증명/토큰, `Bearer` 토큰, `password/token/secret/apiKey` 등의 키 값에 대해 기본 마스킹을 적용합니다.

---

## BuildServer 웹 플랫폼

BuildServer는 CLI의 Web/Agent 진입점입니다. 다음을 제공합니다:

- 웹 로그인.
- 프로젝트 관리.
- 설정 관리.
- 빌드 작업 대기열.
- 실시간 로그.
- 산출물 다운로드.
- 사용자 권한.
- 감사 로그.
- MCP/Agent 도구.
- LinuxGateway 노드 API.

초안은 싱글 머신, 싱글 Worker, 직렬 대기열을 채택하여 Unity, Xcode, Gradle, 서명 환경, 캐시 디렉터리의 동시 경쟁을 회피합니다.

### 로컬 시작

Windows 디버깅:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-build-server.ps1
```

macOS/Linux 디버깅:

```bash
./scripts/run-build-server.sh
```

기본 주소:

```text
http://127.0.0.1:5088
```

기본 계정:

```text
admin
```

`BUILD_SERVER_ADMIN_PASSWORD`가 미설정인 경우, 첫 시작 시 임의 비밀번호 생성:

```text
<DataRoot>/initial-admin.txt
```

`BUILD_SERVER_AGENT_TOKEN`이 미설정인 경우, 첫 시작 시 기본 MCP Agent Token 생성:

```text
<DataRoot>/initial-agent-token.txt
```

### 프로덕션 환경 변수

프로덕션 권장 설정:

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

주요 변수:

| 변수 | 설명 |
|------|------|
| `BUILD_SERVER_DATA_ROOT` | 데이터 디렉터리. 사용자, 프로젝트, 설정, 작업, 감사 JSON 저장 |
| `BUILD_SERVER_ADMIN_PASSWORD` | 관리자 비밀번호 |
| `BUILD_SERVER_AGENT_TOKEN` | MCP Agent Token |
| `BUILD_SERVER_PUBLIC_BASE_URL` | 외부 접근 URL |
| `BUILD_SERVER_ALLOWED_ORIGINS` | 허용된 Web Origin. 리버스 프록시 사용 시 권장 |
| `BUILD_SERVER_ALLOWED_WORKSPACE_ROOTS` | 허용된 워크스페이스 루트 디렉터리 |
| `BUILD_SERVER_ALLOWED_ARTIFACTS_ROOTS` | 허용된 산출물 루트 디렉터리 |
| `BUILD_SERVER_ALLOWED_CONFIG_ROOTS` | 허용된 설정 파일 루트 디렉터리 |
| `BUILD_SERVER_ALLOWED_REPOSITORY_HOSTS` | 등록 가능한 Git 호스트 |
| `BUILD_SERVER_GATEWAY_TOKEN` | 노드 API 토큰. 비어 있으면 첫 시작 시 `initial-gateway-token.txt` 자동 생성 |
| `BUILD_SERVER_NODE_PLATFORMS` | 현재 노드 능력. 예: `ios,android` 또는 `android` |

### 웹 사용 흐름

백엔드에 첫 로그인 후:

1. 프로젝트 추가: 프로젝트 이름, Git 저장소, 기본 브랜치, 허용 브랜치, 워크스페이스, 산출물 디렉터리 입력.
2. 설정 추가: iOS 또는 Android 선택.
3. 설정은 기존 JSON 파일을 참조하거나 웹 폼에서 새로 생성 가능.
4. 빌드 시작: 프로젝트, 설정, 브랜치, 선택적 파라미터 선택.
5. 작업 목록에서 상태, 실시간 로그, 산출물 확인.

BuildServer는 각 작업의 독립적인 설정 스냅샷을 생성하고 CLI를 호출합니다:

```text
AutomationUnityBuildIOS run --config <job-config.json>
```

### BuildServer Mac 게시

Apple Silicon Mac:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-build-server-mac.ps1 -Runtime osx-arm64
```

Intel Mac:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-build-server-mac.ps1 -Runtime osx-x64
```

게시 디렉터리에는 BuildServer와 AutomationUnityBuildIOS CLI가 모두 포함됩니다. 프로덕션 환경에서는 다음과 함께 사용:

```text
deploy/launchd/com.automationunity.buildserver.plist
```

BuildServer를 실행할 전용 macOS 사용자를 지정하고, Unity License, Xcode 서명, 인증서, 프로비저닝 프로파일, Git SSH 키를 모두 해당 사용자 아래에 설정할 것을 권장합니다.

### MCP / Agent

MCP 엔드포인트:

```text
POST /mcp
Header: X-Agent-Token: <BUILD_SERVER_AGENT_TOKEN>
```

지원 도구:

| 도구 | 설명 |
|------|------|
| `list_projects` | 사용 가능한 프로젝트 목록 |
| `list_configs` | 프로젝트 내 빌드 설정 목록 |
| `start_build` | iOS 또는 Android 빌드 작업 제출 |
| `start_ios_build` | 기존 이름, 신규 연동 시 `start_build` 권장 |
| `get_build_status` | 빌드 작업 상태 조회 |
| `tail_build_log` | 최신 로그 읽기 |
| `list_build_artifacts` | 작업 산출물 목록 |

기본적으로 Agent는 `dryRun=true`만 허용됩니다. 실제 빌드를 허용하려면 해당 MCP Client의 `allowFullBuild`를 활성화하고, 특정 프로젝트만 승인할 것을 권장합니다.

Agent Token을 URL 쿼리 파라미터에 포함하지 마세요. `X-Agent-Token` 또는 `Authorization: Bearer`를 사용하세요.

---

## LinuxGateway 다중 노드 진입점

LinuxGateway는 공용 도메인을 가진 Linux 서버 배포에 적합합니다. Unity를 실행하지 않고, Unity 프로젝트를 저장하지 않으며, Apple 인증서도 보유하지 않습니다. 로그인, 노드 등록, 노드 선택, 작업 전달, 로그/산출물 프록시만 담당합니다.

전형적인 아키텍처:

```text
외부 사용자
  -> LinuxGateway Web/API
      -> Mac BuildServer       iOS + Android
      -> Windows BuildServer   Android
```

LinuxGateway를 배포하지 않아도 각 Mac/Windows BuildServer는 독립적으로 사용할 수 있습니다.

### LinuxGateway 시작

개발 실행:

```bash
./scripts/run-linux-gateway.sh http://127.0.0.1:5090
```

Windows 디버깅:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-linux-gateway.ps1
```

기본 주소:

```text
http://127.0.0.1:5090
```

`LINUX_GATEWAY_ADMIN_PASSWORD`가 미설정인 경우, 첫 시작 시 초기 비밀번호 생성:

```text
linuxgateway-data/initial-admin.txt
```

프로덕션 권장 설정:

```bash
export LINUX_GATEWAY_ADMIN_PASSWORD="strong-password"
export LINUX_GATEWAY_PUBLIC_BASE_URL="https://build.example.com"
export LINUX_GATEWAY_ALLOWED_ORIGINS="https://build.example.com"
export LINUX_GATEWAY_DATA_ROOT="/opt/unity-build-gateway/data"
```

### LinuxGateway Linux 게시

Windows에서 Linux x64 게시:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-linux-gateway-linux.ps1
```

기본 출력:

```text
publish/linux-gateway
```

Linux에 복사 후 실행:

```bash
chmod +x ./LinuxGateway
./LinuxGateway --urls http://127.0.0.1:5090
```

외부 액세스에는 Nginx/Caddy로 HTTPS를 제공하고 `127.0.0.1:5090`에 리버스 프록시할 것을 권장합니다.

### 모드 1: 직접 노드 연결

직접 연결은 LinuxGateway가 Mac/Windows BuildServer에 접근 가능한 경우에 적합합니다. VPN, 인트라넷, 터널, 공용 HTTPS 등.

각 BuildServer 노드 시작 전 설정:

```bash
export BUILD_SERVER_GATEWAY_TOKEN="strong-random-token"
export BUILD_SERVER_NODE_PLATFORMS="ios,android"
```

Windows Android 노드:

```powershell
$env:BUILD_SERVER_GATEWAY_TOKEN="strong-random-token"
$env:BUILD_SERVER_NODE_PLATFORMS="android"
```

`BUILD_SERVER_GATEWAY_TOKEN`을 수동으로 설정하지 않아도, BuildServer가 첫 시작 시 자동 생성하고 다음에 저장:

```text
<DataRoot>/initial-gateway-token.txt
```

BuildServer는 다음을 활성화:

```text
/api/gateway/*
```

LinuxGateway는 노드 호출 시 다음을 사용:

```text
Header: X-Gateway-Token: <BUILD_SERVER_GATEWAY_TOKEN>
```

LinuxGateway 웹 UI에서 장치 추가:

| 필드 | 예 |
|------|------|
| 장치 이름 | `Mac Build` |
| BuildServer URL | `https://mac-build.example.com` |
| Gateway Token | 해당 노드의 `BUILD_SERVER_GATEWAY_TOKEN` |
| 플랫폼 | Mac: `iOS + Android`, Windows: `Android` |

저장 후 장치를 새로고침하여 노드 프로젝트와 설정이 표시되는지 확인합니다.

### 모드 2: 리버스 노드 연결

리버스 연결은 노드가 NAT, 홈 네트워크, 기업 인트라넷 뒤에 있어 LinuxGateway가 노드 주소에 직접 접근할 수 없는 경우에 적합합니다. 이 경우 BuildServer가 LinuxGateway에 아웃바운드 연결을 시작합니다.

LinuxGateway 웹 UI에서 Enrollment Token을 생성하고, BuildServer의 Gateway 연결 페이지에 입력:

```text
Gateway URL: https://build.example.com
Enrollment Token: <token>
```

환경 변수로 BuildServer 시작 시 자동 연결을 설정할 수도 있습니다:

```bash
export BUILD_SERVER_REVERSE_GATEWAY_ENABLED=true
export BUILD_SERVER_REVERSE_GATEWAY_URL="https://build.example.com"
export BUILD_SERVER_REVERSE_GATEWAY_ENROLLMENT_TOKEN="<token>"
export BUILD_SERVER_REVERSE_NODE_NAME="Mac Build"
```

연결 성공 후 LinuxGateway에 리버스 연결 노드가 표시됩니다. 노드 자격 증명은 BuildServer 데이터 디렉터리에 저장됩니다. 노드 취소 후 새 Enrollment Token을 생성하여 재등록해야 합니다.

리버스 연결은 `LinuxGateway/Reverse/`와 `BuildServer/Reverse/`에 구현되어 있습니다.

### LinuxGateway 온라인 자가 업데이트

LinuxGateway는 `SelfUpdateService`를 내장하여 Gitee 또는 GitHub Releases에서 업데이트 패키지의 확인과 다운로드가 가능합니다. 서버에 .NET SDK가 필요하지 않습니다.

업데이트 확인:

```text
GET /api/system/version
GET /api/system/update/check
```

업데이트 실행 (Admin 전용):

```text
POST /api/system/update/apply
```

업데이트 프로세스는 현재 버전의 백업, tar.gz 업데이트 패키지 다운로드, `apply-update.sh` 스크립트 생성(교체 및 재시작 완료)을 자동으로 수행합니다.

설정:

| 변수 | 설명 |
|------|------|
| `LINUX_GATEWAY_UPDATE_SOURCE` | 업데이트 소스: `gitee` 또는 `github` |
| `LINUX_GATEWAY_UPDATE_REPO_OWNER` | 저장소 소유자 |
| `LINUX_GATEWAY_UPDATE_REPO_NAME` | 저장소 이름 |

### LinuxGateway를 통한 빌드 제출

1. LinuxGateway에 로그인.
2. 장치 페이지에서 노드가 온라인인지 확인.
3. 노드를 새로고침하여 프로젝트와 설정이 동기화되었는지 확인.
4. 빌드 작업 페이지에서 장치, 프로젝트, 설정, 브랜치 선택.
5. 작업 제출.
6. 원격 노드에서 반환된 상태, 로그, 산출물 확인.

iOS 작업은 `ios`를 지원하는 Mac 노드에만 전송할 수 있습니다. Windows 노드는 일반적으로 Android APK/AAB에만 적합합니다.

---

## 보안 권장 사항

- 프로덕션 환경에서는 반드시 강력한 비밀번호를 설정하고, 초기 비밀번호 파일에 장기 의존하지 마세요.
- `BUILD_SERVER_AGENT_TOKEN`, `BUILD_SERVER_GATEWAY_TOKEN`, Enrollment Token을 URL에 포함하지 마세요. 헤더 또는 서버 측 폼으로 저장하세요.
- LinuxGateway와 BuildServer의 데이터 디렉터리에는 사용자, 작업, 노드 자격 증명, 토큰이 저장되므로 시스템 권한을 제한하세요.
- BuildServer에 `BUILD_SERVER_ALLOWED_WORKSPACE_ROOTS`, `BUILD_SERVER_ALLOWED_ARTIFACTS_ROOTS`, `BUILD_SERVER_ALLOWED_CONFIG_ROOTS`, `BUILD_SERVER_ALLOWED_REPOSITORY_HOSTS`를 설정할 것을 권장합니다.
- 노드 백엔드가 LinuxGateway 전용인 경우, 일반 관리 백엔드를 공용 인터넷에 노출하지 마세요.
- iOS 인증서, 프로비저닝 프로파일, App Store Connect `.p8`, Android keystore, Google Play Service Account JSON은 모두 빌드 머신의 보안 로컬 디렉터리에만 배치하세요.
- 인증서, 개인 키, 장기 토큰을 Git에 커밋하지 마세요.
- 리버스 프록시를 통해 웹 UI에 접근하는 경우, `PUBLIC_BASE_URL`과 `ALLOWED_ORIGINS`을 설정하여 크로스 오리진 요청 거부나 오리진 검증 실패를 회피하세요.

---

## FAQ

| 문제 | 해결책 |
|------|------|
| Windows에서 iOS 빌드가 macOS 필요 오류 | iOS 프로덕션 빌드는 Mac에서 실행해야 합니다. Windows는 `--dry-run --allow-non-mac`으로 설정 디버깅만 지원 |
| Unity 실행 파일을 찾을 수 없음 | `unityExecutablePath`를 설정하거나 `unityVersion`이 Unity Hub 설치 경로와 일치하는지 확인 |
| Git 풀 실패 | 빌드 머신에서 수동 `git clone`을 실행하여 SSH 키 또는 HTTPS 자격 증명 검증 |
| Team ID 검증 실패 | `teamId`는 10자 Apple Developer Team ID여야 합니다. 회사명이 아님 |
| App Store Connect 업로드 실패 | `exportMethod=app-store`, `.p8` 경로 존재, Key ID와 Issuer ID가 올바른지 확인 |
| Android versionCode 오류 | `buildNumber`는 양의 정수여야 합니다 |
| Google Play 업로드 실패 | Service Account JSON 경로, 앱 권한, packageName, track, 업로드 산출물 형식 확인 |
| BuildServer 로그인 실패 | 계정은 `admin`. `initial-admin.txt`에서 `admin password:` 이후의 값만 복사 |
| 웹 쓰기 작업 거부됨 | `BUILD_SERVER_ALLOWED_ORIGINS` 또는 `LINUX_GATEWAY_ALLOWED_ORIGINS`가 접근 도메인과 일치하는지 확인 |
| LinuxGateway 노드 401 | Gateway Token이 잘못되었거나 노드가 `BUILD_SERVER_GATEWAY_TOKEN`을 활성화하지 않음 |
| LinuxGateway 노드 시간 초과 | 노드 주소, 포트, 방화벽, 터널, 리버스 프록시 확인 |
| 산출물 다운로드 실패 | 산출물 경로가 BuildServer의 허용된 artifacts roots 내에 있는지 확인 |

---

## 회귀 테스트

개발자는 다음을 실행할 수 있습니다:

```powershell
.\scripts\verify.ps1
```

실행 내용:

- 솔루션 컴파일.
- CLI 프로젝트 컴파일.
- BuildServer 컴파일.
- LinuxGateway 컴파일.
- 도움말 진입점 `00`.
- iOS 샘플 드라이런.
- Android 샘플 드라이런.
- 설정 편집기 열기-닫기.

테스트 스위트는 256개 이상의 테스트 케이스를 커버하며, CLI 인수 파싱, 설정 모델, 경로 보안, Git 정책, Unity 명령 빌드, Google Play API, TikTok 설정, BuildServer API 라우트, LinuxGateway 노드 통신, 리버스 연결, 이메일 알림 등 모든 모듈을 포괄합니다.

전체 테스트 스위트 실행:

```powershell
dotnet test .\AutomationUnityBuildIOS.Tests\AutomationUnityBuildIOS.Tests.csproj
```

컴파일에 미치는 영향을 빠르게 확인하려면:

```powershell
dotnet build .\AutomationUnityBuildIOS.sln
```
