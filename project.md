
# Remote Session Monitor - Codex Project Prompt

## 목적
- A PC 현재 RDP 접속자 확인
- B PC에서 접속 전 현재 접속자 확인 후 중복 접속 방지

## 개발 환경
- C#
- .NET
- WinForms
- Visual Studio 2022
- Windows 전용

## 프로젝트 구성

### RemoteMonitor.Server
- quser + qwinsta 기반 RDP 상태 감지
- HTTP API 제공
- TXT 로그 저장
- Tray 상시 실행
- Windows 로그인 시 자동 실행
- Single Instance 적용

### RemoteMonitor.Client
- 원격 PC 상태 조회
- 현재 접속자 표시
- mstsc.exe 기반 RDP 접속 실행
- 원격 PC 목록 관리

## 통신
- HTTP + JSON
- Port: 5000
- 1초 Polling

## 로그 정책
- 날짜 기준 TXT 파일 생성
- Append 방식 저장

## 원격 PC 저장
- JSON 구조
- AES 암호화 저장
- remote_pc_list.dat 사용

## Client 정책
- 상태 확인 버튼 클릭 시만 Polling 시작
- 접속자 존재 시 경고 후 접속 허용

## Server 정책
- X 버튼 클릭 시 Tray 최소화
- 실제 종료는 Tray 메뉴에서만 가능

## 프로젝트 구조

```text
/Forms
/Services
/Models
/Networking
/Logging
/Config
/Utilities
```

## Codex 규칙
- 단계별 구현
- 빌드 가능한 상태 유지
- Service 클래스 분리
- WPF 금지
- Electron 금지
- 외부 DB 금지

## 최초 구현 순서
1. Solution 생성
2. Server 프로젝트 생성
3. Client 프로젝트 생성
4. Tray 기능
5. quser/qwinsta 구현
6. HTTP API 구현
7. Client 상태 조회
8. Polling 구현
9. mstsc.exe 실행
10. TXT 로그 저장

## Codex 최초 프롬프트

project.md 내용을 기준으로 Visual Studio 2022용 C# WinForms 솔루션을 생성해줘.

조건:
- Server/Client 프로젝트 분리
- 단계별 구현
- WinForms 사용
- HTTP + JSON 사용
- quser + qwinsta 기반 구현
- Service 클래스 분리
