// Escape From Duckov Coop Mod - Remote Player Controller with Snapshot & Interpolation
// 개선 사항: 100-150ms 단위 스냅샷 + 보간 시스템

using LiteNetLib;
using LiteNetLib.Utils;

namespace EscapeFromDuckovCoopMod;

/// <summary>
/// 원격 플레이어 컨트롤러 - 스냅샷 기반 보간 시스템
/// 100-150ms 단위 스냅샷을 받아서 부드럽게 보간
/// </summary>
public class RemoteDuckController : MonoBehaviour
{
    [Header("보간 설정")]
    [Tooltip("스냅샷 간격 (밀리초)")]
    public float snapshotInterval = 100f; // 100ms = 10Hz
    
    [Tooltip("보간 백타임 (초) - 큰 값일수록 더 부드럽지만 지연이 증가")]
    public float interpolationBackTime = 0.15f; // 150ms
    
    [Tooltip("보정 임계값 (미터) - 이 거리 이상 차이나면 즉시 보정")]
    public float snapDistance = 2f;
    
    [Tooltip("보간 속도 (Lerp 계수)")]
    [Range(0f, 1f)]
    public float lerpSpeed = 0.9f;
    
    // 스냅샷 버퍼
    private readonly Queue<DuckovSnapshot> _snapshotBuffer = new(64);
    
    // 현재 보간 대상
    private Transform _targetTransform;
    private Transform _modelRoot;
    private CharacterMainControl _characterControl;
    private Animator _animator;
    
    // 마지막 스냅샷 정보
    private double _lastSnapshotTime = -1;
    private uint _lastSequence = 0;
    private float _timer = 0f;
    
    // 네트워크 ID
    public string PlayerId { get; set; }
    
    private NetService Service => NetService.Instance;
    private bool IsClient => Service != null && Service.networkStarted && !Service.IsServer;
    
    private void Awake()
    {
        // 컴포넌트 찾기
        _characterControl = GetComponent<CharacterMainControl>() ?? GetComponentInChildren<CharacterMainControl>(true);
        if (_characterControl != null)
        {
            _targetTransform = _characterControl.transform;
            _modelRoot = _characterControl.modelRoot ? _characterControl.modelRoot.transform : _targetTransform;
            
            // 애니메이터 찾기
            _animator = _characterControl.GetComponent<Animator>() 
                     ?? _characterControl.GetComponentInChildren<Animator>(true);
        }
        else
        {
            _targetTransform = transform;
            _modelRoot = transform;
        }
    }
    
    private void Update()
    {
        if (!IsClient || _targetTransform == null) return;
        
        // 스냅샷 버퍼가 없으면 리턴
        if (_snapshotBuffer.Count == 0) return;
        
        // 보간 시간 계산
        var renderTime = Time.unscaledTimeAsDouble - interpolationBackTime;
        
        // 보간할 스냅샷 찾기
        DuckovSnapshot? before = null;
        DuckovSnapshot? after = null;
        
        foreach (var snapshot in _snapshotBuffer)
        {
            if (snapshot.serverTime <= renderTime)
            {
                before = snapshot;
            }
            else if (snapshot.serverTime > renderTime)
            {
                after = snapshot;
                break;
            }
        }
        
        // 스냅샷이 너무 오래되었으면 최신 것 사용 (예측)
        if (before == null && _snapshotBuffer.Count > 0)
        {
            var latest = _snapshotBuffer.ToArray().Last();
            if (renderTime - latest.serverTime < 0.5) // 0.5초 이내면 예측
            {
                var delta = renderTime - latest.serverTime;
                var velocity = latest.vel;
                ApplyPositionAndAnimation(latest.pos + velocity * (float)delta, latest.rot, latest);
                return;
            }
        }
        
        // 두 스냅샷 사이 보간
        if (before.HasValue && after.HasValue)
        {
            var beforeSnap = before.Value;
            var afterSnap = after.Value;
            
            var totalTime = afterSnap.serverTime - beforeSnap.serverTime;
            if (totalTime > 0.0001)
            {
                var t = (float)((renderTime - beforeSnap.serverTime) / totalTime);
                
                // Lerp로 보간
                var position = Vector3.LerpUnclamped(beforeSnap.pos, afterSnap.pos, t);
                var rotation = Quaternion.SlerpUnclamped(beforeSnap.rot, afterSnap.rot, t);
                
                ApplyPositionAndAnimation(position, rotation, afterSnap);
            }
            else
            {
                // 시간 차이가 거의 없으면 즉시 적용
                ApplyPositionAndAnimation(afterSnap.pos, afterSnap.rot, afterSnap);
            }
            
            // 오래된 스냅샷 정리
            CleanupOldSnapshots(renderTime);
        }
        else if (before.HasValue)
        {
            // after가 없으면 before 사용
            ApplyPositionAndAnimation(before.Value.pos, before.Value.rot, before.Value);
        }
    }
    
    /// <summary>
    /// 스냅샷 수신 및 버퍼에 추가
    /// </summary>
    public void ReceiveSnapshot(Vector3 position, Quaternion rotation, Vector3 velocity, uint sequence, double serverTime)
    {
        if (!IsClient) return;
        
        // 시퀀스 체크 - 뒤늦게 온 패킷 무시
        if (sequence <= _lastSequence && _lastSequence > 0)
        {
            Debug.LogWarning($"[RemoteDuck] Player {PlayerId} received stale snapshot: sequence {sequence} <= {_lastSequence}");
            return;
        }
        
        _lastSequence = sequence;
        
        // 위치가 너무 멀리 떨어져 있으면 스냅 (순간이동 감지)
        if (_snapshotBuffer.Count > 0)
        {
            var last = _snapshotBuffer.ToArray().Last();
            var distance = Vector3.Distance(position, last.pos);
            
            if (distance > snapDistance)
            {
                // 순간이동 감지 - 버퍼 초기화하고 즉시 적용
                _snapshotBuffer.Clear();
                _targetTransform?.SetPositionAndRotation(position, rotation);
                Debug.Log($"[RemoteDuck] Player {PlayerId} snap detected: {distance:F2}m");
            }
        }
        
        // 스냅샷 추가
        _snapshotBuffer.Enqueue(new DuckovSnapshot
        {
            sequence = sequence,
            serverTime = serverTime,
            pos = position,
            rot = rotation,
            vel = velocity
        });
        
        // 버퍼 크기 제한
        while (_snapshotBuffer.Count > 64)
        {
            _snapshotBuffer.Dequeue();
        }
        
        _lastSnapshotTime = serverTime;
    }
    
    /// <summary>
    /// 레거시 호환용 (시퀀스 없이)
    /// </summary>
    public void ReceiveSnapshot(Vector3 position, Quaternion rotation, Vector3 velocity, double timestamp)
    {
        if (!IsClient) return;
        
        // 시퀀스 번호 자동 생성 (타임스탬프 기반)
        var sequence = (uint)(timestamp * 1000); // 밀리초 기반
        
        ReceiveSnapshot(position, rotation, velocity, sequence, timestamp);
    }
    
    /// <summary>
    /// 위치 및 애니메이션 적용 (보간 또는 즉시)
    /// </summary>
    private void ApplyPositionAndAnimation(Vector3 position, Quaternion rotation, DuckovSnapshot snapshot)
    {
        if (_targetTransform == null) return;
        
        // 거리 차이 확인
        var distance = Vector3.Distance(_targetTransform.position, position);
        
        if (distance > snapDistance)
        {
            // 거리가 너무 멀면 즉시 적용
            _targetTransform.SetPositionAndRotation(position, rotation);
        }
        else
        {
            // 부드럽게 보간
            _targetTransform.position = Vector3.Lerp(_targetTransform.position, position, lerpSpeed);
            
            if (_modelRoot != null && _modelRoot != _targetTransform)
            {
                _modelRoot.rotation = Quaternion.Slerp(_modelRoot.rotation, rotation, lerpSpeed);
            }
            else
            {
                _targetTransform.rotation = Quaternion.Slerp(_targetTransform.rotation, rotation, lerpSpeed);
            }
        }
        
        // 애니메이션 동기화 (추가 구현 가능)
        // 여기서 스냅샷의 애니메이션 파라미터를 적용할 수 있음
    }
    
    /// <summary>
    /// 레거시 호환용
    /// </summary>
    private void ApplyPosition(Vector3 position, Quaternion rotation)
    {
        ApplyPositionAndAnimation(position, rotation, default);
    }
    
    /// <summary>
    /// 오래된 스냅샷 정리
    /// </summary>
    private void CleanupOldSnapshots(double renderTime)
    {
        while (_snapshotBuffer.Count > 1)
        {
            var oldest = _snapshotBuffer.Peek();
            if (oldest.serverTime < renderTime - interpolationBackTime)
            {
                _snapshotBuffer.Dequeue();
            }
            else
            {
                break;
            }
        }
    }
}

/// <summary>
/// 스냅샷 데이터 구조 - 버전/시퀀스 포함
/// </summary>
public struct DuckovSnapshot
{
    public uint sequence;      // 증가하는 번호
    public double serverTime;  // 호스트 기준 시간
    public Vector3 pos;
    public Vector3 vel;
    public Quaternion rot;
}

/// <summary>
/// 스냅샷 기반 위치 동기화 관리자
/// </summary>
public static class SnapshotSyncManager
{
    // 플레이어별 RemoteDuckController 저장
    private static readonly Dictionary<string, RemoteDuckController> _controllers = new();
    
    /// <summary>
    /// 원격 플레이어 GameObject에 RemoteDuckController 추가/가져오기
    /// </summary>
    public static RemoteDuckController GetOrAddController(GameObject playerObject, string playerId)
    {
        if (playerObject == null) return null;
        
        if (!_controllers.TryGetValue(playerId, out var controller) || controller == null)
        {
            controller = playerObject.GetComponent<RemoteDuckController>();
            if (controller == null)
            {
                controller = playerObject.AddComponent<RemoteDuckController>();
            }
            controller.PlayerId = playerId;
            _controllers[playerId] = controller;
        }
        
        return controller;
    }
    
    /// <summary>
    /// 스냅샷 수신 처리 (시퀀스 포함)
    /// </summary>
    public static void ReceiveSnapshot(string playerId, Vector3 position, Quaternion rotation, Vector3 velocity, uint sequence, double serverTime)
    {
        if (!_controllers.TryGetValue(playerId, out var controller) || controller == null)
        {
            // GameObject 찾기
            var service = NetService.Instance;
            if (service == null) return;
            
            GameObject playerObject = null;
            if (service.clientRemoteCharacters.TryGetValue(playerId, out playerObject))
            {
                controller = GetOrAddController(playerObject, playerId);
            }
            
            if (controller == null) return;
        }
        
        controller.ReceiveSnapshot(position, rotation, velocity, sequence, serverTime);
    }
    
    /// <summary>
    /// 레거시 호환용 (시퀀스 없이)
    /// </summary>
    public static void ReceiveSnapshot(string playerId, Vector3 position, Quaternion rotation, Vector3 velocity)
    {
        var timestamp = Time.unscaledTimeAsDouble;
        var sequence = (uint)(timestamp * 1000); // 밀리초 기반
        
        ReceiveSnapshot(playerId, position, rotation, velocity, sequence, timestamp);
    }
}

