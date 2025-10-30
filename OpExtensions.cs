// Escape From Duckov Coop Mod - Op Enum Extensions
// 개선 사항: 새로운 Opcode 추가

namespace EscapeFromDuckovCoopMod;

/// <summary>
/// Op enum에 추가할 새로운 Opcode들
/// </summary>
public static class OpExtensions
{
    // Op enum에 직접 추가할 수 없으므로 별도 정의
    // 실제로는 src/Main/Op.cs에 직접 추가해야 함
    
    // 새로운 Opcode 상수들
    public const byte PLAYER_SNAPSHOT = 26; // 스냅샷 전송 (100ms 간격)
    public const byte LOOT_LOCK_REQUEST = 27; // 루팅 락 요청
    public const byte LOOT_LOCK_STATE = 28; // 루팅 락 상태
    public const byte LOOT_UNLOCK = 29; // 루팅 락 해제
    public const byte DEAD_PLAYER_LOOT_REQUEST = 30; // 죽은 플레이어 루팅 요청
    public const byte AI_EVENT_BROADCAST = 31; // AI 이벤트 브로드캐스트 (시간 기준)
    public const byte ROOM_LIST_REQUEST = 32; // 방 리스트 요청
    public const byte ROOM_LIST_RESPONSE = 33; // 방 리스트 응답
    public const byte ROOM_CREATE = 34; // 방 생성
    public const byte ROOM_JOIN = 35; // 방 참가
    public const byte ROOM_PASSWORD = 36; // 방 비밀번호
    public const byte RECONNECT_STATE = 37; // 재접속 상태 복구
    public const byte MOD_EVENT = 38; // 모드 이벤트 (API용)
    public const byte BATCH_UPDATE = 39; // 배치 업데이트 (문, 파괴물 등)
    public const byte ZONE_BROADCAST = 40; // 구역 기반 브로드캐스트
    public const byte DEBUG_INFO_REQUEST = 41; // 디버그 정보 요청
    public const byte DEBUG_INFO_RESPONSE = 42; // 디버그 정보 응답
}

