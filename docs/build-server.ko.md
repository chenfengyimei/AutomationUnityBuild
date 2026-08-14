# BuildServer 플랫폼

BuildServer는 자동 빌드 도구의 Web/Agent 진입점으로, iOS, Android APK/AAB, Google Play 업로드를 지원합니다. 초안은 싱글 Mac, 싱글 Worker, 직렬 대기열을 채택하여 Unity, Xcode, Gradle, 서명 환경의 동시 실행으로 인한 캐시와 인증서 상태 혼란을 회피합니다.

## 모듈

- `BuildServer.Api`: ASP.NET Core Minimal API. 로그인, 프로젝트, 설정, 작업, 산출물, 감사를 담당.
- `BuildServer.Worker`: 백그라운드 직렬 Worker. 대기열에서 작업을 꺼내 `AutomationUnityBuildIOS` CLI를 호출.
- `BuildServer.Web`: 내장 정적 프론트엔드. 웹 로그인과 빌드 제출을 제공.
- `BuildServer.Mcp`: `/mcp` JSON-RPC 도구 엔드포인트. Agent/AI용.
- `BuildServer.Reverse`: 리버스 연결 모듈. BuildServer가 LinuxGateway에 자발적으로 연결. NAT/인트라넷 환경에 적합.
- `buildserver-data`: JSON 영속화 디렉터리. 사용자, 프로젝트, 설정, 작업, 산출물, 감사 기록, Worker 노드를 저장.

## 로컬 시작

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-build-server.ps1
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

`BUILD_SERVER_AGENT_TOKEN`이 미설정인 경우, 첫 시작 시 임의 Agent API Key 생성:

```text
<DataRoot>/initial-agent-token.txt
```

프로덕션 권장 설정:

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

보안 관련 기본값:

- 워크스페이스는 기본적으로 `~/UnityBuildWorkspace`로 제한.
- 산출물은 기본적으로 `~/UnityBuildArtifacts`로 제한.
- 설정 파일은 기본적으로 BuildServer 데이터 디렉터리의 `configs`와 프로그램 디렉터리의 `configs`로 제한.
- Git 저장소는 기본적으로 HTTPS/SSH URL을 허용. 프로덕션에서는 `BUILD_SERVER_ALLOWED_REPOSITORY_HOSTS`를 설정할 것을 권장(예: `github.com` 또는 사내 Git 서버 도메인).
- Nginx/Caddy 등의 리버스 프록시를 통해 웹 UI에 접근하는 경우, `BUILD_SERVER_PUBLIC_BASE_URL`과 `BUILD_SERVER_ALLOWED_ORIGINS`를 설정하지 않으면 크로스 사이트 요청 보호가 오리진이 불일치하는 쓰기 작업을 거부합니다.

## Mac 게시

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-build-server-mac.ps1 -Runtime osx-arm64
```

게시 후 `deploy/launchd/com.automationunity.buildserver.plist`로 `buildbot` 사용자로 실행 가능. 인증서, 프로비저닝 프로파일, Unity License, Git SSH 키는 모두 이 고정 macOS 사용자 아래에 설치해야 합니다.

## 필수 데이터

첫 로그인 후:

1. 프로젝트 추가: 프로젝트 이름, Git 저장소, 기본 브랜치, 허용 브랜치, 워크스페이스, 산출물 디렉터리 입력.
2. 설정 추가: iOS 또는 Android 선택. 기존 설정 JSON 파일 경로를 지정하거나, "새 설정 파일 생성"에 체크하고 웹 폼에서 Unity 버전, Bundle ID, 플랫폼별 필드를 입력하면 서버 측에서 JSON을 자동 생성하여 등록.
   - iOS 필드: Team ID, Deployment Target, Export Method, Signing Style, archive의 Organizer 복사 여부, App Store Connect/TestFlight 업로드 여부.
   - Android 필드: APK/AAB/both, SDK 버전, keystore, Google Play Service Account, track, release status, 업로드 산출물.
3. 빌드 시작: 프로젝트와 설정을 선택하고 작업 제출.

BuildServer는 각 작업의 독립적인 설정 스냅샷을 생성하고 Build Number를 예약하며 CLI를 호출:

```text
AutomationUnityBuildIOS run --config <job-config.json>
```

## MCP/Agent

MCP 엔드포인트:

```text
POST /mcp
Header: X-Agent-Token: <BUILD_SERVER_AGENT_TOKEN>
```

도구:

- `list_projects`
- `list_configs`
- `start_build`
- `start_ios_build` (기존 이름, 신규 연동 시 `start_build` 권장)
- `get_build_status`
- `tail_build_log`
- `list_build_artifacts`

기본적으로 Agent는 `dryRun=true`만 허용됩니다. 실제 빌드를 허용하려면 데이터에서 해당 `McpClientRecord.allowFullBuild`를 `true`로 설정하고, 특정 프로젝트만 승인할 것을 권장합니다. MCP는 프로젝트와 설정 ID로만 작업을 제출하며, 임의의 Git 저장소나 경로를 전달할 수 없습니다.

신규 설정은 기본적으로 MCP 사용 불가. 웹 UI에서 명시적으로 "MCP 사용 허용"에 체크해야 합니다.

## 이메일 알림

BuildServer는 내장된 이메일 알림 서비스(`EmailNotificationService`)를 통해 빌드 작업 완료 후 자동으로 이메일을 발송합니다:

- **빌드 성공**: 이메일에 빌드 산출물 경로, 경과 시간, 설정 요약 포함.
- **빌드 실패**: 이메일에 실패 단계, 오류 요약, 로그 경로 포함.

SMTP 465 암시적 SSL, 연락처 목록, 맞춤형 이메일 템플릿을 지원합니다. 웹 백엔드 또는 DesktopApp 이메일 알림 페이지에서 SMTP 서버, 포트, 발신자 자격 증명, 연락처 목록을 설정합니다.

## 스토리지 관리

빌드 작업이 누적되면 산출물이 점차 디스크 공간을 소비합니다. BuildServer는 두 가지 스토리지 관리 메커니즘을 제공합니다:

- **자동 정리**: `MaintenanceService`가 `RetentionDays`와 `MaxArtifactBytes`를 기준으로 완료된 작업과 산출물을 자동 정리.
- **수동 정리**: 웹 백엔드 또는 DesktopApp 스토리지 관리 페이지에서 스토리지 개요를 확인하고 대량 삭제 또는 단일 삭제 가능.

`StorageCleanupService`가 실제 산출물 디렉터리 스캔과 삭제를 담당합니다.

## 리버스 연결

BuildServer 노드가 NAT, 홈 네트워크, 기업 인트라넷 뒤에 있어 LinuxGateway가 노드 주소에 직접 접근할 수 없는 경우, 리버스 연결로 BuildServer가 LinuxGateway에 자발적으로 연결할 수 있습니다.

LinuxGateway 웹 UI에서 Enrollment Token을 생성하고, 환경 변수로 BuildServer를 설정:

```bash
export BUILD_SERVER_REVERSE_GATEWAY_ENABLED=true
export BUILD_SERVER_REVERSE_GATEWAY_URL="https://build.example.com"
export BUILD_SERVER_REVERSE_GATEWAY_ENROLLMENT_TOKEN="<token>"
export BUILD_SERVER_REVERSE_NODE_NAME="Mac Build"
```

연결 성공 후 노드 자격 증명은 BuildServer 데이터 디렉터리에 저장됩니다. `BuildServer/Reverse/` 디렉터리가 리버스 연결 클라이언트 로직을 구현합니다.

## 보안 경계

- Web/MCP는 작업 생성만 수행하며, 임의의 셸 명령을 직접 실행하지 않습니다.
- Worker는 직렬 실행으로 동시에 하나의 작업만 실행.
- 프로젝트는 허용 브랜치를 제한할 수 있음.
- CLI는 내부적으로 Git 화이트리스트와 경로 경계 검증을 계속 수행.
- 작업 산출물 다운로드에는 로그인 인증이 필요.
- 감사 로그는 로그인, 프로젝트 생성, 설정 생성, 작업 제출/취소, Worker 등록을 기록.
- 유지보수 서비스가 `RetentionDays`와 `MaxArtifactBytes`로 완료된 작업과 산출물을 정리.
- 이메일 알림 내 민감 정보(비밀번호, 토큰)는 표시되지 않으며 SMTP 인증에만 사용.

## 다중 Mac 확장

`WorkerNodeRecord`는 이미 영속화되어 있으며, `/api/workers`와 `/api/workers/register`가 제공됩니다. 초안의 내장 Worker는 싱글 Mac에 적합. 다중 Mac 확장 시 권장되는 진화:

```text
중앙 BuildServer.Api + 데이터베이스
Mac Worker A/B/C를 독립 프로세스로 배치
Worker가 자신에 적합한 작업을 풀
Unity/Xcode 버전, 프로젝트 승인, 현재 부하로 스케줄링
```

그 시점에 JSON 영속화는 SQLite/PostgreSQL로 교체하여 머신 간 동시 파일 쓰기를 회피해야 합니다.
