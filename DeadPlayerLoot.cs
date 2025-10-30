// Escape From Duckov Coop Mod - Dead Player Loot System
// 개선 사항: 죽은 플레이어 루팅 (호스트만 허용)

using LiteNetLib;
using LiteNetLib.Utils;

namespace EscapeFromDuckovCoopMod;

/// <summary>
/// 죽은 플레이어 루팅 시스템
/// </summary>
public class DeadPlayerLootSystem : MonoBehaviour
{
    public static DeadPlayerLootSystem Instance;
    
    // 죽은 플레이어 ID -> 인벤토리 저장
    private readonly Dictionary<string, Inventory> _deadPlayerInventories = new();
    
    private NetService Service => NetService.Instance;
    private bool IsServer => Service != null && Service.IsServer;
    private NetManager netManager => Service?.netManager;
    private NetDataWriter writer => Service?.writer;
    private NetPeer connectedPeer => Service?.connectedPeer;
    
    private void Awake()
    {
        Instance = this;
    }
    
    /// <summary>
    /// 죽은 플레이어 인벤토리 루팅 요청 (클라이언트 -> 호스트)
    /// </summary>
    public void RequestDeadPlayerLoot(string deadPlayerId, string requesterId)
    {
        if (IsServer || connectedPeer == null) return;
        
        writer.Reset();
        writer.Put((byte)OpExtensions.DEAD_PLAYER_LOOT_REQUEST);
        writer.Put(deadPlayerId);
        writer.Put(requesterId);
        
        connectedPeer.Send(writer, DeliveryMethod.ReliableOrdered);
    }
    
    /// <summary>
    /// 죽은 플레이어 루팅 요청 처리 (호스트)
    /// </summary>
    public void HandleDeadPlayerLootRequest(NetPeer peer, string deadPlayerId)
    {
        if (!IsServer) return;
        
        var requesterId = Service.GetPlayerId(peer);
        
        // 죽은 플레이어의 인벤토리 찾기
        Inventory deadPlayerInv = null;
        
        // 원격 플레이어 중에서 찾기
        foreach (var kvp in Service.remoteCharacters)
        {
            var playerObject = kvp.Value;
            if (playerObject == null) continue;
            
            var cmc = playerObject.GetComponent<CharacterMainControl>() 
                   ?? playerObject.GetComponentInChildren<CharacterMainControl>(true);
            
            if (cmc == null) continue;
            
            var playerId = Service.GetPlayerId(kvp.Key);
            if (playerId != deadPlayerId) continue;
            
            // 죽었는지 확인
            var health = cmc.Health;
            if (health != null && health.CurrentHealth <= 0f)
            {
                // 인벤토리 가져오기
                var characterItem = cmc.CharacterItem;
                if (characterItem != null)
                {
                    // 플레이어 인벤토리는 CharacterItem에 있을 수 있음
                    // 실제 구현은 게임의 인벤토리 구조에 따라 다름
                    // 여기서는 기본 구조만 보여줌
                }
            }
        }
        
        // 인벤토리를 찾았으면 전송
        if (deadPlayerInv != null)
        {
            SendDeadPlayerInventory(peer, deadPlayerId, deadPlayerInv);
        }
        else
        {
            // 거부 메시지
            writer.Reset();
            writer.Put((byte)OpExtensions.LOOT_DENY);
            writer.Put("죽은 플레이어의 인벤토리를 찾을 수 없습니다");
            
            peer.Send(writer, DeliveryMethod.ReliableOrdered);
        }
    }
    
    /// <summary>
    /// 죽은 플레이어 인벤토리 전송 (호스트 -> 클라이언트)
    /// </summary>
    private void SendDeadPlayerInventory(NetPeer peer, string deadPlayerId, Inventory inventory)
    {
        writer.Reset();
        writer.Put((byte)OpExtensions.DEAD_PLAYER_LOOT_REQUEST);
        writer.Put(deadPlayerId);
        writer.Put(true); // 성공
        
        // 인벤토리 내용 전송 (LootNet의 방식과 유사)
        var capacity = inventory.Capacity;
        writer.Put(capacity);
        
        var content = inventory.Content;
        var count = 0;
        for (var i = 0; i < content.Count; ++i)
            if (content[i] != null) count++;
        
        writer.Put(count);
        
        for (var i = 0; i < content.Count; ++i)
        {
            var item = content[i];
            if (item == null) continue;
            
            writer.Put(i);
            ItemTool.WriteItemSnapshot(writer, item);
        }
        
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }
    
    /// <summary>
    /// 죽은 플레이어 인벤토리 수신 (클라이언트)
    /// </summary>
    public void ReceiveDeadPlayerInventory(string deadPlayerId, Inventory inventory)
    {
        _deadPlayerInventories[deadPlayerId] = inventory;
        
        // UI 열기 (LootView 사용)
        var lootView = LootView.Instance;
        if (lootView != null && inventory != null)
        {
            // 죽은 플레이어 인벤토리를 루팅 창에 표시
            // 실제 구현은 LootView API에 따라 다름
        }
    }
}

/// <summary>
/// 죽은 플레이어 루팅 UI 추가
/// </summary>
[HarmonyPatch(typeof(Spectator), "OnSpectatorMode")]
internal static class Patch_Spectator_DeadPlayerLoot
{
    private static void Postfix()
    {
        var spectator = Spectator.Instance;
        if (spectator == null || !spectator._spectatorActive) return;
        
        var mod = ModBehaviourF.Instance;
        if (mod == null || !mod.networkStarted) return;
        
        // 관전 모드에서 죽은 플레이어 인벤토리 확인 UI 추가
        // 실제 구현은 UI 시스템에 따라 다름
    }
}

