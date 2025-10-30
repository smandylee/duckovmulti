// Escape From Duckov Coop Mod - Snapshot Synchronization System
// 개선 사항: 100-150ms 단위 스냅샷 전송 시스템

using LiteNetLib;
using LiteNetLib.Utils;

namespace EscapeFromDuckovCoopMod;

/// <summary>
/// 스냅샷 동기화 시스템 - 호스트 측
/// </summary>
public class SnapshotSync : MonoBehaviour
{
    public static SnapshotSync Instance;
    
    [Header("스냅샷 설정")]
    [Tooltip("스냅샷 전송 간격 (밀리초)")]
    public float snapshotInterval = 100f; // 100ms = 10Hz
    
    [Tooltip("위치 변화 임계값 (미터) - 이보다 작으면 전송 안 함")]
    public float positionThreshold = 0.05f;
    
    [Tooltip("회전 변화 임계값 (도) - 이보다 작으면 전송 안 함")]
    public float rotationThreshold = 2f;
    
    // 전송 타이머
    private float _sendTimer = 0f;
    
    // 이전 상태 저장 (델타 압축용)
    private readonly Dictionary<string, PlayerSnapshotState> _lastStates = new();
    
    // 시퀀스 번호 (플레이어별로 관리)
    private readonly Dictionary<string, uint> _sequences = new();
    
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
        if (!IsServer || netManager == null) return;
        
        _sendTimer += Time.deltaTime * 1000f; // 밀리초 단위
        
        if (_sendTimer >= snapshotInterval)
        {
            SendSnapshots();
            _sendTimer = 0f;
        }
    }
    
    /// <summary>
    /// 모든 플레이어의 스냅샷 전송
    /// </summary>
    private void SendSnapshots()
    {
        if (Service == null || writer == null) return;
        
        // 로컬 플레이어 스냅샷
        SendLocalPlayerSnapshot();
        
        // 원격 플레이어 스냅샷
        foreach (var kvp in Service.remoteCharacters)
        {
            var peer = kvp.Key;
            var playerObject = kvp.Value;
            
            if (playerObject == null) continue;
            
            var cmc = playerObject.GetComponent<CharacterMainControl>() 
                   ?? playerObject.GetComponentInChildren<CharacterMainControl>(true);
            
            if (cmc == null) continue;
            
            SendRemotePlayerSnapshot(peer, cmc);
        }
    }
    
    /// <summary>
    /// 로컬 플레이어 스냅샷 전송
    /// </summary>
    private void SendLocalPlayerSnapshot()
    {
        var main = CharacterMainControl.Main;
        if (main == null) return;
        
        var playerId = Service.GetPlayerId(null);
        var position = main.transform.position;
        var rotation = main.modelRoot ? main.modelRoot.rotation : main.transform.rotation;
        
        // 속도 계산
        var velocity = Vector3.zero;
        var rb = main.GetComponent<Rigidbody>();
        if (rb != null) velocity = rb.velocity;
        
        // 델타 체크
        if (!HasStateChanged(playerId, position, rotation))
        {
            return; // 변화 없으면 전송 안 함
        }
        
        // 시퀀스 번호 증가
        if (!_sequences.TryGetValue(playerId, out var sequence))
        {
            sequence = 0;
        }
        sequence++;
        _sequences[playerId] = sequence;
        
        // 스냅샷 패킷 구성
        writer.Reset();
        writer.Put((byte)Op.PLAYER_SNAPSHOT);
        
        writer.Put(playerId);
        writer.Put(sequence); // 시퀀스 번호 추가
        writer.Put((double)Time.unscaledTimeAsDouble); // 서버 시간 추가
        writer.PutV3cm(position);
        writer.PutQuaternion(rotation);
        writer.PutV3cm(velocity);
        
        // 모든 클라이언트에 브로드캐스트
        netManager.SendToAll(writer, DeliveryMethod.Unreliable);
        
        // 상태 저장
        UpdateLastState(playerId, position, rotation);
    }
    
    /// <summary>
    /// 원격 플레이어 스냅샷 전송
    /// </summary>
    private void SendRemotePlayerSnapshot(NetPeer peer, CharacterMainControl cmc)
    {
        var playerId = Service.GetPlayerId(peer);
        var position = cmc.transform.position;
        var rotation = cmc.modelRoot ? cmc.modelRoot.rotation : cmc.transform.rotation;
        
        // 속도 계산
        var velocity = Vector3.zero;
        var rb = cmc.GetComponent<Rigidbody>();
        if (rb != null) velocity = rb.velocity;
        
        // 델타 체크
        if (!HasStateChanged(playerId, position, rotation))
        {
            return; // 변화 없으면 전송 안 함
        }
        
        // 시퀀스 번호 증가
        if (!_sequences.TryGetValue(playerId, out var sequence))
        {
            sequence = 0;
        }
        sequence++;
        _sequences[playerId] = sequence;
        
        // 스냅샷 패킷 구성
        writer.Reset();
        writer.Put((byte)Op.PLAYER_SNAPSHOT);
        
        writer.Put(playerId);
        writer.Put(sequence); // 시퀀스 번호 추가
        writer.Put((double)Time.unscaledTimeAsDouble); // 서버 시간 추가
        writer.PutV3cm(position);
        writer.PutQuaternion(rotation);
        writer.PutV3cm(velocity);
        
        // 해당 클라이언트에게 전송
        peer.Send(writer, DeliveryMethod.Unreliable);
        
        // 상태 저장
        UpdateLastState(playerId, position, rotation);
    }
    
    /// <summary>
    /// 상태 변화 체크 (델타 압축)
    /// </summary>
    private bool HasStateChanged(string playerId, Vector3 position, Quaternion rotation)
    {
        if (!_lastStates.TryGetValue(playerId, out var lastState))
        {
            return true; // 첫 전송
        }
        
        var posDelta = Vector3.Distance(position, lastState.position);
        var rotDelta = Quaternion.Angle(rotation, lastState.rotation);
        
        return posDelta > positionThreshold || rotDelta > rotationThreshold;
    }
    
    /// <summary>
    /// 마지막 상태 업데이트
    /// </summary>
    private void UpdateLastState(string playerId, Vector3 position, Quaternion rotation)
    {
        _lastStates[playerId] = new PlayerSnapshotState
        {
            position = position,
            rotation = rotation,
            timestamp = Time.unscaledTimeAsDouble
        };
    }
    
    /// <summary>
    /// 플레이어 상태 구조
    /// </summary>
    private struct PlayerSnapshotState
    {
        public Vector3 position;
        public Quaternion rotation;
        public double timestamp;
    }
}

/// <summary>
/// Op enum에 새 Opcode 추가 필요
/// </summary>
/*
public enum Op : byte
{
    // ... 기존 코드들 ...
    PLAYER_SNAPSHOT = 26, // 스냅샷 전송 (100ms 간격)
}
*/

