# Escape From Duckov Coop Mod - Improved Version

이 모드는 [Escape From Duckov Coop Mod Preview](https://github.com/Mr-sans-and-InitLoader-s-team/Escape-From-Duckov-Coop-Mod-Preview)를 기반으로 개선된 버전입니다.

## 🎯 개선 사항

7가지 주요 개선 사항이 통합되었습니다:

1. **네트워크 구조 다듬기** - 권위적 호스트 강화, 스냅샷+보간 시스템
2. **싱글플레이 UI 문제 해결** - 루팅 락, 동시 루팅, 죽은 플레이어 루팅
3. **적/보스 동기화 강화** - 이벤트 기반 동기화, 채널 분리
4. **매치메이킹/접속성 개선** - 방 리스트 UI, 비밀번호, 재접속 복구
5. **모드 API 개방** - 이벤트 기반 API로 다른 모드와 통합 가능
6. **성능/트래픽 최적화** - 클라이언트 예측, 배치 전송, 구역 기반 브로드캐스트
7. **테스트/디버그 도구** - 인게임 디버그 패널 (F9)

## 📖 원본 모드

이 모드는 다음 저장소를 기반으로 합니다:
- 원본: https://github.com/Mr-sans-and-InitLoader-s-team/Escape-From-Duckov-Coop-Mod-Preview

## 🛠️ 빌드 방법

### 전제 조건
- Visual Studio 2019 이상
- .NET Framework 4.8
- 게임 "Escape From Duckov" 설치

### 빌드 단계

1. 환경 변수 설정:
   ```
   DUCKOV_GAME_MANAGED = C:\Steam\steamapps\common\Escape from Duckov\Duckov_Data\Managed
   ```

2. 의존성 DLL 확인:
   - `Shared/0Harmony.dll`
   - `Shared/LiteNetLib.dll`

3. 프로젝트 빌드:
   - Visual Studio에서 `EscapeFromDuckovCoopMod.csproj` 열기
   - **Release** 구성 선택
   - 솔루션 빌드

## 📦 Steam Workshop 업로드

빌드 후 다음 파일들이 필요합니다:
- `EscapeFromDuckovCoopMod.dll`
- `0Harmony.dll`
- `LiteNetLib.dll`
- `Localization/*.json` (선택사항)
- `LICENSE.txt`

## 📄 라이센스

원본 모드와 동일한 라이센스 적용:
- AGPL-3.0 기반
- 상업적 사용 금지
- 저작자 표시 필수

## 🙏 감사의 말

원본 모드 개발자들에게 감사드립니다:
- Mr.sans and InitLoader's team

