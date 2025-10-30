// Escape From Duckov Coop Mod - Loot Lock System
// 개선 사항: 루팅 락 시스템으로 중복 생성 및 싱크 깨짐 방지

using LiteNetLib;
using LiteNetLib.Utils;

namespace EscapeFromDuckovCoopMod;

/// <summary>
/// 루팅 락 시스템 - 상자 열기 락 관리
/// </summary>
public class LootLockSystem : MonoBehaviour
{
    public static LootLockSystem Instance;
    
    // 상자 ID -> 락 정보 (누가 언제 열었는지)
    private readonly Dictionary<int, LootLock> _lockedLootboxes = new();
    
    // 플레이어 ID -> 상자 ID (어떤 상자를 열고 있는지)
    private readonly Dictionary<string, int> _playerLootbox = new();
    
    [Header("시간제 락 설정")]
    [Tooltip("락 타임아웃 시간 (초) - 이 시간 후 자동 해제")]
    public float lockTimeout = 15f;
    
    private NetService Service => NetService.Instance;
    private bool IsServer => Service != null && Service.IsServer;
    private NetManager netManager => Service?.netManager;
    private NetDataWriter writer => Service?.writer;
    private NetPeer connectedPeer => Service?.connectedPeer;
    
    private void Awake()
    {
        Instance = this;
    }
    
    private void Update()
    {
        if (!IsServer) return;
        
        // 시간제 락 자동 해제 체크
        var currentTime = Time.unscaledTime;
        var locksToUnlock = new List<int>();
        
        foreach (var kvp in _lockedLootboxes)
        {
            var lootboxUid = kvp.Key;
            var lockInfo = kvp.Value;
            
            // 타임아웃 체크
            if (currentTime - lockInfo.lockedAt > lockTimeout)
            {
                locksToUnlock.Add(lootboxUid);
            }
            // 플레이어가 연결 해제되었는지 체크
            else if (!IsPlayerConnected(lockInfo.lockerPlayerId))
            {
                locksToUnlock.Add(lootboxUid);
            }
        }
        
        // 타임아웃된 락 해제
        foreach (var lootboxUid in locksToUnlock)
        {
            var lockInfo = _lockedLootboxes[lootboxUid];
            UnlockLootbox(lootboxUid, lockInfo.lockerPlayerId);
            Debug.Log($"[LootLock] Auto-unlocked lootbox {lootboxUid} (timeout or disconnected)");
        }
    }
    
    /// <summary>
    /// 플레이어가 연결되어 있는지 확인
    /// </summary>
    private bool IsPlayerConnected(string playerId)
    {
        if (Service == null) return false;
        
        // 호스트의 플레이어 상태 확인
        foreach (var kvp in Service.playerStatuses)
        {
            var status = kvp.Value;
            if (status != null && status.EndPoint == playerId)
            {
                return true;
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// 루팅 락 요청 (클라이언트 -> 호스트)
    /// </summary>
    public void RequestLootLock(int lootboxUid, string playerId)
    {
        if (!IsServer || connectedPeer == null) return;
        
        writer.Reset();
        writer.Put((byte)OpExtensions.LOOT_LOCK_REQUEST);
        writer.Put(lootboxUid);
        writer.Put(playerId);
        
        connectedPeer.Send(writer, DeliveryMethod.ReliableOrdered);
    }
    
    /// <summary>
    /// 루팅 락 처리 (호스트)
    /// </summary>
    public void HandleLootLockRequest(NetPeer peer, int lootboxUid)
    {
        if (!IsServer) return;
        
        var playerId = Service.GetPlayerId(peer);
        
        // 이미 다른 플레이어가 열고 있으면 거부
        if (_lockedLootboxes.TryGetValue(lootboxUid, out var existingLock))
        {
            // 타임아웃 체크
            if (Time.unscaledTime - existingLock.lockedAt > lockTimeout)
            {
                // 타임아웃된 락은 해제하고 새로 락
                _lockedLootboxes.Remove(lootboxUid);
                _playerLootbox.Remove(existingLock.lockerPlayerId);
            }
            else if (existingLock.lockerPlayerId != playerId)
            {
                // 다른 플레이어가 열고 있으면 거부
                writer.Reset();
                writer.Put((byte)OpExtensions.LOOT_LOCK_STATE);
                writer.Put(lootboxUid);
                writer.Put(false); // 락 실패
                writer.Put($"이미 {existingLock.lockerPlayerId}가 열고 있습니다");
                
                peer.Send(writer, DeliveryMethod.ReliableOrdered);
                return;
            }
            else
            {
                // 같은 플레이어가 다시 요청하면 성공 (이미 락됨)
                // 시간만 갱신
                existingLock.lockedAt = Time.unscaledTime;
                _lockedLootboxes[lootboxUid] = existingLock;
                return;
            }
        }
        
        // 락 설정
        _lockedLootboxes[lootboxUid] = new LootLock
        {
            lockerPlayerId = playerId,
            lockedAt = Time.unscaledTime
        };
        _playerLootbox[playerId] = lootboxUid;
        
        // 성공 메시지 전송 (모든 클라이언트에 브로드캐스트)
        writer.Reset();
        writer.Put((byte)OpExtensions.LOOT_LOCK_STATE);
        writer.Put(lootboxUid);
        writer.Put(true); // 락 성공
        writer.Put(playerId);
        
        netManager.SendToAll(writer, DeliveryMethod.ReliableOrdered);
    }
    
    /// <summary>
    /// 루팅 락 해제 (호스트)
    /// </summary>
    public void UnlockLootbox(int lootboxUid, string playerId)
    {
        if (!IsServer) return;
        
        // 락 확인
        if (!_lockedLootboxes.TryGetValue(lootboxUid, out var lockedBy))
            return;
        
        if (lockedBy != playerId)
            return; // 다른 플레이어가 락했으면 해제 불가
        
        // 락 해제
        _lockedLootboxes.Remove(lootboxUid);
        _playerLootbox.Remove(playerId);
        
        // 모든 클라이언트에 브로드캐스트
        writer.Reset();
        writer.Put((byte)OpExtensions.LOOT_UNLOCK);
        writer.Put(lootboxUid);
        
        netManager.SendToAll(writer, DeliveryMethod.ReliableOrdered);
    }
    
    /// <summary>
    /// 락 상태 확인 (클라이언트)
    /// </summary>
    public bool IsLootboxLocked(int lootboxUid)
    {
        return _lockedLootboxes.ContainsKey(lootboxUid);
    }
    
    /// <summary>
    /// 락한 플레이어 ID 가져오기
    /// </summary>
    public string GetLockedBy(int lootboxUid)
    {
        return _lockedLootboxes.TryGetValue(lootboxUid, out var lockInfo) ? lockInfo.lockerPlayerId : null;
    }
    
    /// <summary>
    /// 락 상태 수신 (클라이언트)
    /// </summary>
    public void ReceiveLockState(int lootboxUid, bool locked, string playerId)
    {
        if (locked)
        {
            _lockedLootboxes[lootboxUid] = new LootLock
            {
                lockerPlayerId = playerId,
                lockedAt = Time.unscaledTime // 클라이언트는 대략적인 시간
            };
            _playerLootbox[playerId] = lootboxUid;
        }
        else
        {
            _lockedLootboxes.Remove(lootboxUid);
        }
    }
    
    /// <summary>
    /// 락 해제 수신 (클라이언트)
    /// </summary>
    public void ReceiveUnlock(int lootboxUid)
    {
        _lockedLootboxes.Remove(lootboxUid);
        
        // 해당 상자를 열고 있던 플레이어 찾아서 제거
        var playerToRemove = _playerLootbox.FirstOrDefault(kvp => kvp.Value == lootboxUid).Key;
        if (playerToRemove != null)
        {
            _playerLootbox.Remove(playerToRemove);
        }
    }
}

/// <summary>
/// 루팅 락 UI 패치
/// </summary>
[HarmonyPatch(typeof(LootView), "OnStartLoot")]
internal static class Patch_LootView_LockSystem
{
    private static void Prefix(LootView __instance, InteractableLootbox lootbox)
    {
        var mod = ModBehaviourF.Instance;
        if (mod == null || !mod.networkStarted) return;
        
        if (mod.IsServer) return; // 호스트는 자동으로 락
        
        var lockSystem = LootLockSystem.Instance;
        if (lockSystem == null) return;
        
        var lootManager = LootManager.Instance;
        if (lootManager == null || lootbox == null) return;
        
        var inv = lootbox.Inventory;
        if (inv == null) return;
        
        // 루팅 박스 UID 찾기
        var lootboxUid = -1;
        foreach (var kvp in lootManager._cliLootByUid)
        {
            if (kvp.Value == inv)
            {
                lootboxUid = kvp.Key;
                break;
            }
        }
        
        if (lootboxUid < 0) return;
        
        // 이미 락되어 있는지 확인
        if (lockSystem.IsLootboxLocked(lootboxUid))
        {
            var lockedBy = lockSystem.GetLockedBy(lootboxUid);
            var playerName = Service?.clientPlayerStatuses.TryGetValue(lockedBy, out var status) == true 
                ? status.PlayerName 
                : lockedBy;
            
            // UI 메시지 표시
            PopText.Pop($"이미 {playerName}가 열고 있습니다", __instance.transform.position + Vector3.up * 2f);
            
            // 루팅 차단
            return;
        }
        
        // 락 요청
        var playerId = Service?.localPlayerStatus?.EndPoint;
        if (!string.IsNullOrEmpty(playerId))
        {
            lockSystem.RequestLootLock(lootboxUid, playerId);
        }
    }
    
    private static NetService Service => NetService.Instance;
}

/// <summary>
/// 루팅 종료 시 락 해제 패치
/// </summary>
[HarmonyPatch(typeof(LootView), "OnEndLoot")]
internal static class Patch_LootView_UnlockOnEnd
{
    private static void Postfix(LootView __instance)
    {
        var mod = ModBehaviourF.Instance;
        if (mod == null || !mod.networkStarted) return;
        
        if (mod.IsServer)
        {
            // 호스트: 자동 해제
            var lockSystem = LootLockSystem.Instance;
            if (lockSystem == null) return;
            
            var inv = __instance?.TargetInventory;
            if (inv == null) return;
            
            var lootManager = LootManager.Instance;
            if (lootManager == null) return;
            
            // 루팅 박스 UID 찾기
            var lootboxUid = -1;
            foreach (var kvp in lootManager._srvLootByUid)
            {
                if (kvp.Value == inv)
                {
                    lootboxUid = kvp.Key;
                    break;
                }
            }
            
            if (lootboxUid >= 0)
            {
                var playerId = Service?.localPlayerStatus?.EndPoint;
                if (!string.IsNullOrEmpty(playerId))
                {
                    lockSystem.UnlockLootbox(lootboxUid, playerId);
                }
            }
        }
        
        // 클라이언트도 서버로 해제 요청 필요 (추가 구현)
    }
    
    private static NetService Service => NetService.Instance;
}

/// <summary>
/// 락 정보 구조
/// </summary>
public class LootLock
{
    public string lockerPlayerId;
    public float lockedAt;
    public float timeout = 15f; // 기본 타임아웃 (초)
}

