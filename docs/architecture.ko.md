# 아키텍처

이 프로젝트는 모듈화된 계층 설계를 채택하고 있으며, 코어 빌드 엔진과 플랫폼 진입점이 완전히 분리되어 있습니다. CLI, BuildServer, DesktopApp, LinuxGateway는 동일한 코어 로직을 공유하며, 차이점은 진입점 계층과 상호작용 방식에만 있습니다.

## 디렉터리 책임

이 도구는 책임별로 다음 디렉터리로 분할됩니다:

- `Cli/`: 명령 진입점, 명령줄 인수 파싱, 단축 명령 매핑(`ShortcutCommands`).
- `ConsoleUi/`: 콘솔 대화형 UI. 초기화 마법사, 설정 편집기, 입력 프롬프트 포함.
- `Configuration/`: 설정 모델, 설정 파일 읽기/쓰기, 설정 파일 선택, 경로 해석, 샘플 설정. `ios`, `android`, `tiktok` 세 가지 플랫폼 설정을 지원.
- `Workflow/`: 빌드 파이프라인 오케스트레이션, 실행 컨텍스트, 런타임 설정 업데이트, 설정 스냅샷.
- `Services/`: 크로스 플랫폼 공유 비즈니스 기능. Git 동기화, 환경 검사, 디렉터리 준비, Unity 프로젝트 검증, 경로 보안 검증 포함.
- `Modules/Common/`: 플랫폼 모듈 공유 기능. 플랫폼 Pipeline 인터페이스, Unity 명령 인수 빌드, Unity 로그 진단, Unity 메타데이터 읽기 포함.
- `Modules/Ios/`: iOS 전용 빌드 기능. Unity Xcode 프로젝트 내보내기, Xcode project/workspace 위치 파악, `xcodebuild archive/export` 포함.
- `Modules/Android/`: Android 전용 빌드 기능. Unity APK/AAB 빌드, Google Play Publishing API 업로드 포함. `GooglePlay/` 하위 디렉터리가 HTTP API, OAuth, Service Account 세부 사항을 담당.
- `Modules/Tiktok/`: TikTok 미니게임 전용 기능. WebGL 빌드 파이프라인(`TiktokBuildPipeline`), 빌드 서비스(`TiktokBuildService`), TikTok 오픈 플랫폼 API 업로드(`TiktokUploadService`). iOS/Android와 완전히 독립적이며 기존 흐름에 영향을 주지 않음.
- `Infrastructure/`: 공통 인프라. 로깅(`BuildLogger`), 프로세스 실행(`ProcessRunner`), 경로 도구(`PathTools`), 경로 보안 경계(`PathSafety`), 민감 정보 마스킹. 이러한 기능은 CLI, BuildServer, DesktopApp에서 공유됩니다.
- `UnityBuildScripts/Ios/`: Unity 프로젝트의 `Assets/Editor`에 복사할 iOS Unity Editor 빌드 스크립트.
- `UnityBuildScripts/Android/`: Unity 프로젝트의 `Assets/Editor`에 복사할 Android Unity Editor 빌드 스크립트.
- `BuildServer/`: 웹 빌드 플랫폼. API(`ApiRoutes`), 내장 프론트엔드(`wwwroot/`), 백그라운드 Worker(`BuildWorkerService`), MCP/Agent 진입점(`McpEndpoint`), Gateway 노드 API(`GatewayEndpoint`), 이메일 알림(`EmailNotificationService`), 스토리지 관리(`StorageCleanupService`), 산출물 스캔(`ArtifactScanner`), 유지보수 정리(`MaintenanceService`), 리버스 연결(`Reverse/`), JSON 영속화(`Persistence/`) 포함.
- `LinuxGateway/`: 다중 장치 통합 진입점. API(`ApiRoutes`), 내장 프론트엔드(`wwwroot/`), 노드 게이트웨이 클라이언트(`NodeGatewayClient`), 노드 새로고침(`NodeRefreshService`), 작업 새로고침(`JobRefreshService`), 리버스 연결 관리(`Reverse/`), 온라인 자가 업데이트(`SelfUpdateService`), JSON 영속화(`Persistence/`) 포함.
- `DesktopApp/`: Avalonia UI 11 데스크톱 클라이언트. Views(14 페이지), ViewModels(15 뷰 모델), Services(`BuildRunner` / `ProfileStore` / `ServerSyncService`), Controls(커스텀 컨트롤), Styles(스타일 리소스) 포함. `InternalsVisibleTo` + `Compile Remove`로 메인 프로젝트를 참조하여 모든 코어 로직을 재사용.
- `deploy/`: 프로덕션 배포 템플릿. macOS `launchd` plist, Docker 배포 파일 등.

## 핵심 설계 원칙

### 파이프라인 오케스트레이션과 플랫폼 기능의 분리

`AutomationWorkflow`는 단계의 오케스트레이션만 담당하며, Git, Unity, Xcode, Google Play, TikTok의 세부 사항을 직접 처리하지 않습니다. 플랫폼 기능을 추가할 때는 해당 `Modules/<Platform>/` 디렉터리에 배치하고 워크플로에서 호출합니다. 크로스 플랫폼 기능은 `Services/`에 배치합니다. 현재 3종의 플랫폼 Pipeline을 지원:

- `IosBuildPipeline` — Git → Unity → Xcode archive/export → ASC 업로드
- `AndroidBuildPipeline` — Git → Unity → APK/AAB → Google Play 업로드
- `TiktokBuildPipeline` — Git → Unity → WebGL → TikTok 오픈 플랫폼 업로드

### 설정 편집기 필드 기반

설정 편집기는 필드 설명자 목록으로 메뉴와 수정 로직을 구동합니다. 설정 필드를 추가할 때는 먼저 `ConfigEditor`의 필드 목록에 항목을 추가하여, 메뉴 표시와 switch-case 수정 로직의 분산을 방지합니다.

### 보안 기반

웹 백엔드, Worker, MCP/Agent에 연결할 때, 모든 진입점은 CLI에 이미 구현된 기존 능력을 재사용해야 합니다:

- `PathSafetyValidator`: 워크스페이스, 저장소 디렉터리, Unity 프로젝트, 산출물, 로그, Xcode 출력, archive/export가 모두 허용된 루트 디렉터리 내에 있는지 검증.
- `GitRepositoryPolicyValidator`: Git URL 형식과 `allowedRepositoryUrls` 화이트리스트를 검증.
- `BuildConfigSnapshotWriter`: 매 실행마다 `Logs/build-config-snapshot.json`을 생성하여 설정 스냅샷, 해석된 경로, CLI 인수를 기록.
- `SensitiveText`: 로그, 명령, stdout/stderr, 설정 스냅샷의 일반적인 토큰/비밀번호를 통일적으로 마스킹.

이러한 기능은 Web/API 계층에만 배치되어서는 안 됩니다. Worker가 빌드를 실행하기 전에도 재호출하여, 진입점을 우회하여 위험한 설정을 직접 트리거하는 것을 방지해야 합니다.

## BuildServer 아키텍처

BuildServer는 CLI의 Web/Agent 진입점으로, 다음 설계를 채택하고 있습니다:

### 직렬 대기열

싱글 머신, 싱글 Worker, 직렬 대기열 설계는 의도적인 것입니다. Unity, Xcode, Gradle, 서명 인증서, 캐시 디렉터리는 일반적으로 같은 머신에서 동시 경쟁에 적합하지 않습니다. 다중 머신 확장은 LinuxGateway가 담당합니다.

### 서비스 계층

| 서비스 | 파일 | 책임 |
|------|------|------|
| 작업 대기열 | `BuildQueueService.cs` | 빌드 작업 인큐, 디큐, 상태 전환 관리 |
| 백그라운드 Worker | `BuildWorkerService.cs` | 대기열을 직렬로 소비하고 CLI를 호출하여 빌드 실행 |
| 이메일 알림 | `EmailNotificationService.cs` | 빌드 완료 후 성공/실패 이메일 알림 발송 |
| 산출물 스캐너 | `ArtifactScanner.cs` | 작업 산출물 디렉터리를 스캔하여 산출물 목록 생성 |
| 로그 리더 | `LogFileReader.cs` | 작업 로그 읽기 및 tail |
| 스토리지 정리 | `StorageCleanupService.cs` | 기록 산출물의 수동 및 자동 정리 |
| 유지보수 | `MaintenanceService.cs` | RetentionDays/MaxArtifactBytes 기반 자동 정리 |
| 자동 로케이터 | `AutomationToolLocator.cs` | AutomationUnityBuildIOS CLI 실행 파일 위치 파악 |

### 리버스 연결

`BuildServer/Reverse/` 디렉터리는 BuildServer가 LinuxGateway에 자발적으로 연결하는 기능을 구현하여, NAT/인트라넷 환경의 노드가 공용 노출 없이 LinuxGateway에 의해 스케줄될 수 있도록 합니다.

## LinuxGateway 아키텍처

LinuxGateway는 Unity를 실행하지 않고, Unity 프로젝트를 저장하지 않으며, Apple 인증서도 보유하지 않습니다. 다음만 담당합니다:

1. 웹 로그인 및 장치 관리.
2. 노드 등록(직접 또는 리버스 연결).
3. 각 노드의 BuildServer에 작업을 전달.
4. 로그와 산출물을 프록시.

### 서비스 계층

| 서비스 | 파일 | 책임 |
|------|------|------|
| 노드 게이트웨이 클라이언트 | `NodeGatewayClient.cs` | 노드 BuildServer의 `/api/gateway/*` 엔드포인트 호출 |
| 노드 새로고침 | `NodeRefreshService.cs` | 노드 상태와 프로젝트/설정 동기화를 주기적으로 새로고침 |
| 작업 새로고침 | `JobRefreshService.cs` | 원격 작업 상태, 로그, 산출물을 주기적으로 새로고침 |
| 온라인 자가 업데이트 | `SelfUpdateService.cs` | Gitee/GitHub Releases에서 업데이트 패키지 확인 및 다운로드 |

### 리버스 연결

`LinuxGateway/Reverse/` 디렉터리는 BuildServer가 자발적으로 연결할 때의 Enrollment Token 생성, 노드 등록, WebSocket 롱 커넥션 유지를 관리합니다.

### 온라인 자가 업데이트

`SelfUpdateService`가 지원:
- 듀얼 소스 감지(Gitee + GitHub 병행 최신 버전 쿼리).
- tar.gz 업데이트 패키지 다운로드.
- `apply-update.sh` 스크립트 생성(백업 + 교체 + 재시작 완료).
- 서버에 .NET SDK 불필요. 사전 컴파일된 바이너리만 다운로드.

## DesktopApp 아키텍처

DesktopApp은 Avalonia UI 11 + .NET 8을 사용하며, 프로젝트 참조로 메인 프로젝트의 모든 코어 로직을 재사용합니다:

- **InternalsVisibleTo** + **Compile Remove**: 메인 프로젝트의 csproj에 선언을 추가하여 DesktopApp이 internal 멤버에 접근할 수 있도록 하면서 Program.cs 등의 진입점 파일을 제외.
- **ProfileStore**: 4종류 설정 템플릿(프로젝트/Unity/서명/인증서)의 영속화를 통합 관리. 데이터는 `profiles/` 디렉터리에 저장.
- **ServerSyncService**: HttpClient로 BuildServer REST API에 연결하여 템플릿과 설정 파일의 양방향 동기화를 구현.
- **BuildRunner**: CLI 호출을 래핑하여 실시간 로그 출력과 빌드 진행률을 제공.
- **AvaloniaUseCompiledBindingsByDefault=false**: 런타임 바인딩을 사용하여 모든 .axaml 파일에 x:DataType을 선언할 필요를 회피.

`scripts/verify.ps1`을 실행하면 기본 회귀 검증이 가능합니다: 컴파일, 도움말 진입점, 드라이런, 설정 편집기 열기-닫기.
