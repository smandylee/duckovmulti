// Escape From Duckov Coop Mod - Mod API Events
// 개선 사항: 이벤트 기반 API로 다른 모드와 통합 가능하게

namespace EscapeFromDuckovCoopMod;

/// <summary>
/// Duckov Coop Mod 이벤트 API
/// 다른 모드가 멀티플레이어 상태를 감지하고 통합할 수 있도록 하는 공개 API
/// </summary>
public static class DuckovCoopEvents
{
    /// <summary>
    /// 원격 플레이어가 참가했을 때
    /// </summary>
    public static event Action<PlayerJoinInfo> OnRemotePlayerJoined;
    
    /// <summary>
    /// 플레이어가 떠났을 때
    /// </summary>
    public static event Action<string> OnRemotePlayerLeft; // playerId
    
    /// <summary>
    /// 전리품이 생성되었을 때
    /// </summary>
    public static event Action<LootSpawnInfo> OnLootSpawned;
    
    /// <summary>
    /// 세션이 시작되었을 때 (호스트가 네트워크 시작)
    /// </summary>
    public static event Action<string> OnSessionStarted; // sessionId
    
    /// <summary>
    /// 세션이 종료되었을 때
    /// </summary>
    public static event Action OnSessionEnded;
    
    /// <summary>
    /// 플레이어가 장비를 변경했을 때
    /// </summary>
    public static event Action<PlayerEquipmentInfo> OnPlayerEquipmentChanged;
    
    /// <summary>
    /// 플레이어가 총기를 발사했을 때
    /// </summary>
    public static event Action<PlayerFireInfo> OnPlayerFire;
    
    /// <summary>
    /// AI가 사망했을 때
    /// </summary>
    public static event Action<AIDeathInfo> OnAIDeath;
    
    /// <summary>
    /// 씬이 변경되었을 때
    /// </summary>
    public static event Action<SceneChangeInfo> OnSceneChanged;
    
    /// <summary>
    /// 이벤트 트리거 (내부용)
    /// </summary>
    internal static void TriggerPlayerJoined(PlayerJoinInfo info)
    {
        OnRemotePlayerJoined?.Invoke(info);
    }
    
    internal static void TriggerPlayerLeft(string playerId)
    {
        OnRemotePlayerLeft?.Invoke(playerId);
    }
    
    internal static void TriggerLootSpawned(LootSpawnInfo info)
    {
        OnLootSpawned?.Invoke(info);
    }
    
    internal static void TriggerSessionStarted(string sessionId)
    {
        OnSessionStarted?.Invoke(sessionId);
    }
    
    internal static void TriggerSessionEnded()
    {
        OnSessionEnded?.Invoke();
    }
    
    internal static void TriggerPlayerEquipmentChanged(PlayerEquipmentInfo info)
    {
        OnPlayerEquipmentChanged?.Invoke(info);
    }
    
    internal static void TriggerPlayerFire(PlayerFireInfo info)
    {
        OnPlayerFire?.Invoke(info);
    }
    
    internal static void TriggerAIDeath(AIDeathInfo info)
    {
        OnAIDeath?.Invoke(info);
    }
    
    internal static void TriggerSceneChanged(SceneChangeInfo info)
    {
        OnSceneChanged?.Invoke(info);
    }
}

/// <summary>
/// 플레이어 참가 정보
/// </summary>
public class PlayerJoinInfo
{
    public string PlayerId { get; set; }
    public string PlayerName { get; set; }
    public Vector3 Position { get; set; }
    public Quaternion Rotation { get; set; }
    public string SceneId { get; set; }
    public double Timestamp { get; set; }
}

/// <summary>
/// 전리품 생성 정보
/// </summary>
public class LootSpawnInfo
{
    public int LootUid { get; set; }
    public Vector3 Position { get; set; }
    public Quaternion Rotation { get; set; }
    public string SceneId { get; set; }
    public int LootType { get; set; } // 0: 일반, 1: AI 사망, 2: 플레이어 사망
    public double Timestamp { get; set; }
}

/// <summary>
/// 플레이어 장비 정보
/// </summary>
public class PlayerEquipmentInfo
{
    public string PlayerId { get; set; }
    public int SlotHash { get; set; }
    public string ItemId { get; set; }
    public double Timestamp { get; set; }
}

/// <summary>
/// 플레이어 발사 정보
/// </summary>
public class PlayerFireInfo
{
    public string PlayerId { get; set; }
    public Vector3 MuzzlePosition { get; set; }
    public Vector3 Direction { get; set; }
    public int WeaponTypeId { get; set; }
    public double Timestamp { get; set; }
}

/// <summary>
/// AI 사망 정보
/// </summary>
public class AIDeathInfo
{
    public int AIId { get; set; }
    public Vector3 Position { get; set; }
    public string KillerId { get; set; } // 플레이어 ID 또는 null
    public double Timestamp { get; set; }
}

/// <summary>
/// 씬 변경 정보
/// </summary>
public class SceneChangeInfo
{
    public string OldSceneId { get; set; }
    public string NewSceneId { get; set; }
    public List<string> ParticipantIds { get; set; }
    public double Timestamp { get; set; }
}

/// <summary>
/// API 통합 헬퍼 클래스
/// </summary>
public static class CoopAPIHelper
{
    /// <summary>
    /// 현재 멀티플레이어 세션인지 확인
    /// </summary>
    public static bool IsInMultiplayerSession()
    {
        var service = NetService.Instance;
        return service != null && service.networkStarted;
    }
    
    /// <summary>
    /// 호스트인지 확인
    /// </summary>
    public static bool IsHost()
    {
        var service = NetService.Instance;
        return service != null && service.IsServer;
    }
    
    /// <summary>
    /// 클라이언트인지 확인
    /// </summary>
    public static bool IsClient()
    {
        var service = NetService.Instance;
        return service != null && service.networkStarted && !service.IsServer;
    }
    
    /// <summary>
    /// 현재 플레이어 수 가져오기
    /// </summary>
    public static int GetPlayerCount()
    {
        var service = NetService.Instance;
        if (service == null || !service.networkStarted) return 1;
        
        return service.IsServer 
            ? 1 + service.playerStatuses.Count 
            : 1 + service.clientPlayerStatuses.Count;
    }
    
    /// <summary>
    /// 플레이어 ID 목록 가져오기
    /// </summary>
    public static List<string> GetPlayerIds()
    {
        var service = NetService.Instance;
        if (service == null || !service.networkStarted) return new List<string>();
        
        var list = new List<string>();
        
        if (service.localPlayerStatus != null)
        {
            list.Add(service.localPlayerStatus.EndPoint);
        }
        
        if (service.IsServer)
        {
            foreach (var kvp in service.playerStatuses)
            {
                if (kvp.Value?.EndPoint != null)
                    list.Add(kvp.Value.EndPoint);
            }
        }
        else
        {
            foreach (var kvp in service.clientPlayerStatuses)
            {
                if (!string.IsNullOrEmpty(kvp.Key))
                    list.Add(kvp.Key);
            }
        }
        
        return list;
    }
}

/// <summary>
/// 이벤트 트리거 통합 패치
/// </summary>
[HarmonyPatch(typeof(NetService), "OnPeerConnected")]
internal static class Patch_NetService_OnPeerConnected_Event
{
    private static void Postfix(NetPeer peer)
    {
        var service = NetService.Instance;
        if (service == null || !service.IsServer) return;
        
        var playerId = service.GetPlayerId(peer);
        if (string.IsNullOrEmpty(playerId)) return;
        
        var status = service.playerStatuses.TryGetValue(peer, out var st) ? st : null;
        
        var info = new PlayerJoinInfo
        {
            PlayerId = playerId,
            PlayerName = status?.PlayerName ?? $"Player_{peer.Id}",
            Position = status?.Position ?? Vector3.zero,
            Rotation = status?.Rotation ?? Quaternion.identity,
            SceneId = status?.SceneId,
            Timestamp = Time.unscaledTimeAsDouble
        };
        
        DuckovCoopEvents.TriggerPlayerJoined(info);
    }
}

[HarmonyPatch(typeof(NetService), "OnPeerDisconnected")]
internal static class Patch_NetService_OnPeerDisconnected_Event
{
    private static void Postfix(NetPeer peer)
    {
        var service = NetService.Instance;
        if (service == null) return;
        
        var playerId = service.GetPlayerId(peer);
        if (!string.IsNullOrEmpty(playerId))
        {
            DuckovCoopEvents.TriggerPlayerLeft(playerId);
        }
    }
}

