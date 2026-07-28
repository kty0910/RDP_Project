# Direct status push prototype

이 프로토타입은 중앙 서버 없이 Client가 각 원격 PC의 Server에 직접 상태 스트림을 구독하는 실험용 빌드입니다.

## 동작 범위

- 직접 연결 PC: `/status/stream`을 통한 snapshot, 상태 변경 이벤트, 15초 heartbeat
- 중개 연결 PC: 기존 1초 상태 polling 유지
- 일반 빌드: 기존 동작 유지
- 프로토타입 빌드: `EnableStatusPushPrototype=true`

Server는 Client가 보낸 임시 `X-Client-Id`와 현재 열린 연결을 구독 정보로 사용합니다. Client 주소와 포트를 디스크에 저장하거나 Client로 역접속하지 않습니다.

## 빌드

```powershell
.\prototype\status-push\build.ps1
```

무토큰 변형:

```powershell
.\prototype\status-push\build.ps1 -NoBridgeToken
```

산출물은 Git에서 제외되는 `publish-status-push\client`, `publish-status-push\server`, `publish-status-push\server-service` 폴더에 생성됩니다.

## 자동 smoke test

```powershell
dotnet run --project .\prototype\status-push\StatusPushSmokeTest\StatusPushSmokeTest.csproj -c Release
```

테스트는 임시 로컬 포트에서 Server와 Client 구독기를 함께 실행하여 최초 `snapshot`과 후속 `statusChanged` 이벤트 수신을 확인합니다.
