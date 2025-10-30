// Escape From Duckov Coop Mod - AI Event-based Synchronization
// 개선 사항: 이벤트 기반 AI 동기화 (시간 기준 재생)

using LiteNetLib;
using LiteNetLib.Utils;

namespace EscapeFromDuckovCoopMod;

/// <summary>
/// AI 이벤트 동기화 시스템
/// 호스트가 AI 이벤트를 시간 기준으로 브로드캐스트하고,
/// 클라이언트가 그 시각으로 재생
/// </summary>
public class AIEventSync : MonoBehaviour
{
    public static AIEventSync Instance;
    
    // AI 이벤트 큐 (시간 기준 정렬)
    private readonly PriorityQueue<AIEvent, double> _eventQueue = new();
    
    // 지연 보정 임계값 (밀리초)
    private const double TimeDifferenceThreshold = 80.0; // 80ms 이상 차이나면 보정
    
    private NetService Service => NetService.Instance;
    private bool IsServer => Service != null && Service.IsServer;
    private NetManager netManager => Service?.netManager;
    private NetDataWriter writer => Service?.writer;
    
    private void Awake()
    {
        Instance = this;
    }
    
    private void Update()
    {
        if (!Service?.networkStarted ?? true) return;
        
        if (IsServer)
        {
            // 호스트: 이벤트 처리 및 브로드캐스트
            ProcessServerEvents();
        }
        else
        {
            // 클라이언트: 큐에서 이벤트 재생
            ProcessClientEvents();
        }
    }
    
    /// <summary>
    /// 호스트: AI 이벤트 브로드캐스트
    /// </summary>
    private void ProcessServerEvents()
    {
        // AI 이벤트 발생 시 이 메서드가 호출됨
        // 실제 구현은 AI 행동에 따라 다름
    }
    
    /// <summary>
    /// 클라이언트: 큐에서 이벤트 재생
    /// </summary>
    private void ProcessClientEvents()
    {
        var currentTime = Time.unscaledTimeAsDouble;
        
        // 큐에서 시간이 된 이벤트 재생
        while (_eventQueue.Count > 0)
        {
            var eventItem = _eventQueue.Peek();
            
            // 이벤트 시간 확인
            var timeDiff = currentTime - eventItem.Priority;
            
            if (timeDiff >= 0)
            {
                // 시간이 되었으면 재생
                var aiEvent = _eventQueue.Dequeue();
                PlayAIEvent(aiEvent);
            }
            else
            {
                // 아직 시간이 안 됨
                break;
            }
            
            // 시간 차이가 너무 크면 보정
            if (Math.Abs(timeDiff) > TimeDifferenceThreshold / 1000.0)
            {
                // 약한 보정 적용 (너무 급격한 보정 방지)
                Debug.LogWarning($"[AIEvent] Time difference too large: {timeDiff * 1000:F1}ms");
            }
        }
    }
    
    /// <summary>
    /// AI 이벤트 브로드캐스트 (호스트)
    /// </summary>
    public void BroadcastAIEvent(int aiId, AIEventType eventType, Vector3 position, float delay = 0f)
    {
        if (!IsServer || writer == null) return;
        
        var timestamp = Time.unscaledTimeAsDouble + delay;
        
        writer.Reset();
        writer.Put((byte)OpExtensions.AI_EVENT_BROADCAST);
        writer.Put(aiId);
        writer.Put((byte)eventType);
        writer.PutV3cm(position);
        writer.Put(timestamp);
        writer.Put(delay);
        
        // 신뢰성 있는 채널로 전송 (행동 시작/사망/드롭)
        var deliveryMethod = eventType switch
        {
            AIEventType.AttackStart => DeliveryMethod.ReliableOrdered,
            AIEventType.Death => DeliveryMethod.ReliableOrdered,
            AIEventType.ItemDrop => DeliveryMethod.ReliableOrdered,
            _ => DeliveryMethod.ReliableSequenced
        };
        
        netManager.SendToAll(writer, deliveryMethod);
    }
    
    /// <summary>
    /// AI 이벤트 수신 (클라이언트)
    /// </summary>
    public void ReceiveAIEvent(int aiId, AIEventType eventType, Vector3 position, double timestamp, float delay)
    {
        var aiEvent = new AIEvent
        {
            AIId = aiId,
            EventType = eventType,
            Position = position,
            Timestamp = timestamp,
            Delay = delay
        };
        
        // 큐에 추가 (시간 기준 정렬)
        _eventQueue.Enqueue(aiEvent, timestamp);
    }
    
    /// <summary>
    /// AI 이벤트 재생 (클라이언트)
    /// </summary>
    private void PlayAIEvent(AIEvent aiEvent)
    {
        if (!AITool.aiById.TryGetValue(aiEvent.AIId, out var cmc) || cmc == null)
            return;
        
        switch (aiEvent.EventType)
        {
            case AIEventType.AttackStart:
                // 공격 시작 애니메이션
                var anim = cmc.GetComponent<CharacterAnimationControl_MagicBlend>();
                if (anim != null) anim.OnAttack();
                break;
                
            case AIEventType.SkillCast:
                // 스킬 시전
                // 실제 구현은 게임의 스킬 시스템에 따라 다름
                break;
                
            case AIEventType.Death:
                // 사망 처리
                var health = cmc.Health;
                if (health != null)
                {
                    // 사망 처리 (Health 시스템 사용)
                }
                break;
                
            case AIEventType.ItemDrop:
                // 아이템 드롭
                // 실제 구현은 아이템 드롭 시스템에 따라 다름
                break;
        }
    }
    
    /// <summary>
    /// AI 이벤트 타입
    /// </summary>
    public enum AIEventType : byte
    {
        AttackStart = 1,
        SkillCast = 2,
        Death = 3,
        ItemDrop = 4,
        SoundPlay = 5,
        EffectSpawn = 6
    }
    
    /// <summary>
    /// AI 이벤트 데이터
    /// </summary>
    private struct AIEvent
    {
        public int AIId;
        public AIEventType EventType;
        public Vector3 Position;
        public double Timestamp;
        public float Delay;
    }
}

/// <summary>
/// AI 공격 이벤트 패치
/// </summary>
[HarmonyPatch(typeof(AICharacterController), "OnAttack")]
internal static class Patch_AI_Attack_Event
{
    private static void Postfix(AICharacterController __instance)
    {
        var mod = ModBehaviourF.Instance;
        if (mod == null || !mod.networkStarted || !mod.IsServer) return;
        
        var aiCmc = __instance?.CharacterMainControl;
        if (aiCmc == null) return;
        
        var tag = aiCmc.GetComponent<NetAiTag>();
        if (tag == null) return;
        
        var aiId = tag.aiId;
        if (aiId == 0) return;
        
        // 이벤트 브로드캐스트
        var position = aiCmc.transform.position;
        AIEventSync.Instance?.BroadcastAIEvent(aiId, AIEventSync.AIEventType.AttackStart, position);
    }
}

/// <summary>
/// 채널 분리 시스템
/// </summary>
public static class DeliveryChannelManager
{
    /// <summary>
    /// 중요도에 따른 전송 방법 선택
    /// </summary>
    public static DeliveryMethod GetDeliveryMethod(MessagePriority priority)
    {
        return priority switch
        {
            MessagePriority.Critical => DeliveryMethod.ReliableOrdered,    // 발사, 체력, 사망
            MessagePriority.High => DeliveryMethod.ReliableSequenced,     // 행동 시작, 드롭
            MessagePriority.Normal => DeliveryMethod.Sequenced,             // 위치, 애니메이션
            MessagePriority.Low => DeliveryMethod.Unreliable,              // AI 위치, 환경
            _ => DeliveryMethod.Unreliable
        };
    }
}

public enum MessagePriority
{
    Critical,  // 발사, 체력, 사망
    High,     // 행동 시작, 아이템 드롭
    Normal,   // 위치, 애니메이션
    Low       // AI 위치, 환경 상태
}

