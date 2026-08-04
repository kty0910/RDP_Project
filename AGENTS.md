# RDP Project Codex 운영 지침

## 배포판 정책

- 앞으로 업데이트·검증·배포하는 공식 배포판은 기본판만 사용한다.
- 기본판은 `EnableBridgeToken=false`, `publish`, `installer-output`, `RemoteMonitor_Setup_vX.Y.Z.exe` 기준이다.
- 토큰판(`EnableBridgeToken=true`, `publish-token`, `installer-output-token`, `RemoteMonitor_Setup_vX.Y.Z_Token.exe`)은 빌드, publish, 설치 파일 재생성, 테스트 및 GitHub Release 자산 갱신 대상에서 제외한다.
- 소스에 남아 있는 토큰 기능을 임의로 제거하지 않는다. 이 정책은 토큰판 산출물을 더 이상 업데이트하거나 배포하지 않는다는 의미다.
- 기존 토큰판 로컬 파일과 과거 GitHub Release 자산은 사용자의 별도 삭제 요청이 없으면 그대로 보존한다.

## GitHub 반영

- Git push 또는 배포 요청에 따라 GitHub Release를 게시·갱신할 때는 기본판 설치 파일만 첨부한다.
- `SHA256SUMS.txt`에는 실제로 첨부하는 기본판 설치 파일의 SHA-256만 기록하며 토큰판 항목은 넣지 않는다.
- 기능 브랜치에서 Main으로 인수인계할 때도 토큰판 갱신이 필요하다고 요청하지 않는다.
