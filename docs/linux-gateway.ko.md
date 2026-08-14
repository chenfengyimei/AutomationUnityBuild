# LinuxGateway 다중 노드 진입점

`LinuxGateway`는 옵션 중앙 진입점으로, 공용 도메인을 가진 Linux 서버 배포에 적합합니다. Unity를 실행하지 않고, Unity 프로젝트를 저장하지 않으며, Apple 인증서도 보유하지 않습니다. 웹 로그인, Mac/Windows 빌드 노드 등록, 노드 선택, 노드의 `BuildServer`에 작업을 전달하는 역할만 담당합니다.

LinuxGateway는 두 가지 노드 연결 방식을 지원합니다: 직접 연결(LinuxGateway가 노드에 능동적으로 접근)과 리버스 연결(노드가 LinuxGateway에 능동적으로 연결, NAT/인트라넷 환경에 적합). Gitee/GitHub Releases에서 업데이트 패키지를 다운로드하는 내장 온라인 자가 업데이트 기능도 갖추고 있어, 서버에 .NET SDK가 필요하지 않습니다.

LinuxGateway를 배포하지 않아도 Mac/Windows의 `BuildServer`는 여전히 독립적으로 로그인, 설정, 빌드가 가능합니다.

## 아키텍처

```text
외부 사용자
  -> LinuxGateway Web/API
      -> Mac BuildServer /api/gateway/*    iOS + Android
      -> Windows BuildServer /api/gateway/* Android APK/AAB
```

각 Mac/Windows 노드는 기존 `BuildServer`를 계속 실행하며, LinuxGateway가 호출하기 위한 토큰 보호 API를 추가로 활성화하기만 하면 됩니다.

## Mac/Windows 노드 설정

각 노드에서 `BuildServer` 시작 전 설정:

```bash
export BUILD_SERVER_GATEWAY_TOKEN="이 노드용 강력한 임의 토큰"
export BUILD_SERVER_NODE_PLATFORMS="ios,android"   # Mac에서 일반적
```

Windows Android 노드:

```powershell
$env:BUILD_SERVER_GATEWAY_TOKEN="이 노드용 강력한 임의 토큰"
$env:BUILD_SERVER_NODE_PLATFORMS="android"
```

`BUILD_SERVER_GATEWAY_TOKEN`을 비워두면 노드의 `/api/gateway/*` 엔드포인트가 활성화되지 않습니다.

LinuxGateway는 노드 주소에 접근할 수 있어야 합니다. 예:

```text
https://mac-build.example.com
https://win-build.example.com
```

이는 터널 주소, VPN/인트라넷 주소, 공용 HTTPS 엔드포인트 중 어느 것이든 될 수 있습니다. HTTPS를 권장합니다.

## LinuxGateway 시작

개발 실행:

```bash
./scripts/run-linux-gateway.sh http://127.0.0.1:5090
```

Windows 디버깅:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-linux-gateway.ps1
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

## Linux 게시

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

## 사용 흐름

1. Mac/Windows 노드에서 `BuildServer`를 시작하고 `BUILD_SERVER_GATEWAY_TOKEN`을 설정.
2. Linux에서 `LinuxGateway`를 시작.
3. LinuxGateway 웹 UI에 로그인.
4. 장치 추가:
   - 장치 이름: 예 `Mac Build`
   - BuildServer URL: 예 `https://mac-build.example.com`
   - Gateway Token: 해당 노드의 `BUILD_SERVER_GATEWAY_TOKEN`
   - 플랫폼: Mac: `iOS + Android`, Windows: `Android`
5. 장치를 새로고침하여 노드의 프로젝트와 설정이 표시되는지 확인.
6. 빌드 제출 시 대상 장치, 프로젝트, 설정을 선택.

## 보안 주의사항

- LinuxGateway의 데이터 디렉터리에는 노드의 Gateway Token이 저장되므로 시스템 권한을 제한해야 합니다.
- LinuxGateway는 HTTPS로만 노출해야 하며, 평문 HTTP 직접 노출은 권장하지 않습니다.
- 노드의 `/api/gateway/*`는 `X-Gateway-Token`만 허용합니다. 토큰을 URL에 넣지 마세요.
- 노드의 일반 관리 백엔드를 공용 인터넷에 노출하지 마세요. LinuxGateway만 접근 가능하게 하는 것이 최선입니다.
- iOS 작업은 `ios`를 지원하는 Mac 노드에만 전송할 수 있습니다. Windows 노드는 Android APK/AAB에만 적합합니다.

## 리버스 연결

리버스 연결은 노드가 NAT, 홈 네트워크, 기업 인트라넷 뒤에 있어 LinuxGateway가 노드 주소에 직접 접근할 수 없는 경우에 적합합니다. 이 경우 BuildServer가 LinuxGateway에 자발적으로 연결합니다. 노드 측에서 공용 포트 노출이 필요하지 않습니다.

### 설정 단계

1. LinuxGateway 웹 UI에서 Enrollment Token을 생성.
2. BuildServer 노드에서 환경 변수 설정:

```bash
export BUILD_SERVER_REVERSE_GATEWAY_ENABLED=true
export BUILD_SERVER_REVERSE_GATEWAY_URL="https://build.example.com"
export BUILD_SERVER_REVERSE_GATEWAY_ENROLLMENT_TOKEN="<token>"
export BUILD_SERVER_REVERSE_NODE_NAME="Mac Build"
```

3. BuildServer를 시작하면 LinuxGateway에 자동 연결되어 리버스 연결 노드로 등록됩니다.
4. 연결 성공 후 LinuxGateway 웹 UI에 노드가 표시됩니다.
5. 노드 취소 후 새 Enrollment Token을 생성하여 재등록해야 합니다.

리버스 연결은 `LinuxGateway/Reverse/`와 `BuildServer/Reverse/`에 구현되어 있습니다.

## 온라인 자가 업데이트

LinuxGateway는 `SelfUpdateService`를 내장하여 Gitee 또는 GitHub Releases에서 업데이트 패키지의 확인과 다운로드가 가능합니다. 서버에 .NET SDK가 필요하지 않습니다.

### API 엔드포인트

| 엔드포인트 | 메서드 | 설명 |
|------|------|------|
| `/api/system/version` | GET | 현재 버전 가져오기 |
| `/api/system/update/check` | GET | 최신 버전 확인 |
| `/api/system/update/apply` | POST | 업데이트 적용 (Admin 전용) |

### 업데이트 프로세스

1. Gitee/GitHub Release API에서 병행으로 최신 버전을 쿼리.
2. tar.gz 업데이트 패키지를 다운로드.
3. `apply-update.sh` 스크립트를 생성하여 백업 + 교체 + 재시작을 완료.

### 설정 항목

| 변수 | 설명 |
|------|------|
| `LINUX_GATEWAY_UPDATE_SOURCE` | 업데이트 소스: `gitee` 또는 `github` |
| `LINUX_GATEWAY_UPDATE_REPO_OWNER` | 저장소 소유자 |
| `LINUX_GATEWAY_UPDATE_REPO_NAME` | 저장소 이름 |

## Docker 배포

LinuxGateway는 Docker 배포를 지원합니다. CentOS 7 등 네이티브 `libstdc++` 런타임이 오래되었을 수 있는 환경에 특히 적합합니다. 자세한 내용은 [Docker 배포 가이드](linux-gateway-docker.md)를 참조하세요.
