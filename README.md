# Remote Monitor

Windows 원격 데스크톱(RDP) 접속 전에 대상 PC의 연결 상태와 현재 접속자를 확인하고, 필요할 때 `mstsc.exe`로 원격 접속할 수 있는 .NET 8 WinForms 프로그램입니다.

## 주요 기능

- 여러 원격 PC의 연결 상태와 RDP 접속자 상태를 1초 주기로 확인
- 접속자 있음, 연결 불가, 확인 불가 상태를 행 색상으로 구분
- 접속자가 있는 PC에 연결하기 전 경고 표시
- 직접 RDP 연결과 중개 PC 경유 연결 지원
- 원격 PC 이름, 접속 정보, 부가 설명 관리
- 원격 PC 목록 암호화 저장 및 암호화 백업·복원
- Client와 Server의 트레이 실행 및 Windows 자동 시작
- 로그인 전에도 상태 API를 제공하는 Windows Service
- 무토큰 기본판과 별도 토큰판 설치 파일 제공

## 프로젝트 구성

| 프로젝트 | 역할 |
| --- | --- |
| `RemoteMonitor.Client` | 원격 PC 목록, 상태 확인, 접속자 표시, RDP 연결, 백업·복원 |
| `RemoteMonitor.Server` | WTS 세션 조회, HTTP 상태 API, 로그, 트레이 UI, 중개 설정 |
| `RemoteMonitor.Server.Service` | Windows 로그인 전 상태 API를 제공하는 Windows Service |
| `RemoteMonitor.Bridge` | 과거 독립형 중개 프로그램으로, 현재 주요 배포 대상은 아님 |

## 실행 환경

- Windows 10/11 또는 Windows Server x64
- .NET 8 Desktop Runtime
- 원격 접속 대상 PC의 원격 데스크톱 활성화
- 원격 접속 계정의 `Remote Desktop Users` 또는 동등한 RDP 로그온 권한
- 기본 Status Port: `5000`
- 기본 RDP Port: `3389`

## 설치

최신 설치 파일은 [GitHub Releases](https://github.com/kty0910/RDP_Project/releases/latest)에서 받을 수 있습니다.

- `RemoteMonitor_Setup_v1.1.2.exe`: 무토큰 기본판
- `RemoteMonitor_Setup_v1.1.2_Token.exe`: 토큰판
- `SHA256SUMS.txt`: 설치 파일 무결성 확인용 SHA-256 체크섬

설치 프로그램에서 사용할 구성요소를 선택합니다.

1. 원격 접속을 관리할 PC에는 `Client`를 설치합니다.
2. 상태를 제공할 원격 대상 PC에는 `Server`를 설치합니다.
3. 대상 PC가 로그인 전에도 상태를 제공해야 하면 Server의 Windows 자동 시작을 선택합니다.
4. Client에서 대상 PC의 IP, Status Port, RDP Port와 접속 계정을 등록합니다.
5. 상태 확인 결과를 확인한 뒤 `접속` 버튼으로 원격 데스크톱을 실행합니다.

## 상태 확인과 RDP 연결

상태 확인과 RDP 연결은 서로 다른 통신입니다.

- 상태 확인은 Remote Monitor의 HTTP API를 사용합니다.
- 실제 원격 접속은 Windows `mstsc.exe`와 RDP 포트를 사용합니다.
- 상태가 `연결 가능`이어도 등록 계정에 RDP 로그온 권한이 없으면 Windows에서 로그온을 거부할 수 있습니다.

## 데이터 및 보안

- `remote_pc_list.dat`, `bridge_settings.json`, `Logs`, `.rmpbak`, `.rdp` 파일은 Git 추적 및 배포 대상에서 제외됩니다.
- 설치 파일에는 개발·테스트용 원격 PC 목록, 로그, 중개 설정을 포함하지 않습니다.
- Status API와 중개 포트는 신뢰할 수 있는 사내망, VPN 또는 적절한 방화벽 정책 안에서 사용하는 것을 권장합니다.
- 비밀번호, Token, 백업 파일과 로그를 저장소에 커밋하지 마세요.

## 소스 빌드

필요 도구:

- Visual Studio 2022 또는 .NET 8 SDK
- Windows App/데스크톱 빌드 환경
- 설치 파일 생성 시 Inno Setup 6

Release 빌드:

```powershell
dotnet build RemoteSessionMonitor.sln -c Release
```

무토큰 기본판 설치 파일 생성:

```powershell
powershell -ExecutionPolicy Bypass -File .\installer\build-installer.ps1
```

토큰판 설치 파일 생성:

```powershell
powershell -ExecutionPolicy Bypass -File .\installer\build-installer.ps1 -Token
```

기본판은 `publish`와 `installer-output`, 토큰판은 `publish-token`과 `installer-output-token`에 생성됩니다. 생성 결과물은 Git에 커밋하지 않습니다.

## 브랜치 운영

- `main`: 검증 및 릴리스가 완료된 통합 브랜치
- `agent/*`: 기능 추가·수정용 브랜치
- 기능 브랜치를 GitHub에 먼저 push한 뒤 검증하고 `main`에 병합합니다.
- 버전, 태그와 GitHub Release는 main 통합 후 갱신합니다.

변경 내역은 [RELEASE_NOTES.md](RELEASE_NOTES.md)에서 확인할 수 있습니다.

## 라이선스

현재 별도의 오픈 소스 라이선스가 포함되어 있지 않습니다. 저장소가 공개되어 있어도 코드의 복제, 수정, 재배포 권한이 자동으로 부여되는 것은 아닙니다.
