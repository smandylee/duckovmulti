// Escape From Duckov Coop Mod - Traffic Optimization
// 개선 사항: 클라이언트 예측, 배치 전송, 구역 기반 브로드캐스트

using LiteNetLib;
using LiteNetLib.Utils;

namespace EscapeFromDuckovCoopMod;

/// <summary>
/// 트래픽 최적화 메인 클래스
/// 디버그 패널에서 옵션 토글 가능
/// </summary>
public class TrafficOptimization : MonoBehaviour
{
    public static TrafficOptimization Instance;
    
    [Header("트래픽 최적화 옵션")]
    [Tooltip("애니메이션 파라미터 전송 여부")]
    public bool sendAnimParams = true;
    
    [Tooltip("루팅 이벤트 전송 여부")]
    public bool sendLootEvents = true;
    
    private void Awake()
    {
        Instance = this;
    }
}

/// <summary>
/// 배치 업데이트 시스템 - 정적 오브젝트 변경 묶어서 전송
/// </summary>
public class BatchUpdateSystem : MonoBehaviour
{
    public static BatchUpdateSystem Instance;
    
    // 배치 업데이트 큐
    private readonly Queue<BatchUpdateItem> _updateQueue = new(256);
    
    // 배치 전송 타이머
    private float _batchTimer = 0f;
    private const float BatchInterval = 0.1f; // 100ms마다 배치 전송
    
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
        
        if (!IsServer) return; // 호스트만 배치 전송
        
        _batchTimer += Time.deltaTime;
        if (_batchTimer >= BatchInterval)
        {
            SendBatchUpdates();
            _batchTimer = 0f;
        }
    }
    
    /// <summary>
    /// 배치 업데이트 추가 (문, 파괴물 등)
    /// </summary>
    public void AddBatchUpdate(BatchUpdateType type, int objectId, bool state)
    {
        if (!IsServer) return;
        
        var item = new BatchUpdateItem
        {
            Type = type,
            ObjectId = objectId,
            State = state,
            Timestamp = Time.unscaledTimeAsDouble
        };
        
        _updateQueue.Enqueue(item);
        
        // 큐 크기 제한
        while (_updateQueue.Count > 256)
        {
            _updateQueue.Dequeue();
        }
    }
    
    /// <summary>
    /// 배치 업데이트 전송
    /// </summary>
    private void SendBatchUpdates()
    {
        if (writer == null || _updateQueue.Count == 0) return;
        
        // 같은 타입별로 그룹화
        var grouped = _updateQueue.GroupBy(item => item.Type).ToList();
        
        // 각 타입별로 배치 전송
        foreach (var group in grouped)
        {
            var items = group.ToList();
            
            writer.Reset();
            writer.Put((byte)Op.BATCH_UPDATE);
            writer.Put((byte)group.Key);
            writer.Put(items.Count);
            
            foreach (var item in items)
            {
                writer.Put(item.ObjectId);
                writer.Put(item.State);
            }
            
            // 신뢰성 있는 채널로 전송
            netManager.SendToAll(writer, DeliveryMethod.ReliableSequenced);
        }
        
        _updateQueue.Clear();
    }
    
    /// <summary>
    /// 배치 업데이트 수신 (클라이언트)
    /// </summary>
    public void ReceiveBatchUpdate(BatchUpdateType type, List<(int objectId, bool state)> updates)
    {
        switch (type)
        {
            case BatchUpdateType.Door:
                foreach (var (objectId, state) in updates)
                {
                    COOPManager.Door.Client_ApplyDoorState(objectId, state);
                }
                break;
                
            case BatchUpdateType.Destructible:
                foreach (var (objectId, state) in updates)
                {
                    // 파괴물 상태 적용
                    // 실제 구현은 Destructible 시스템에 따라 다름
                }
                break;
        }
    }
    
    /// <summary>
    /// 배치 업데이트 타입
    /// </summary>
    public enum BatchUpdateType : byte
    {
        Door = 1,
        Destructible = 2,
        Lootbox = 3
    }
    
    /// <summary>
    /// 배치 업데이트 아이템
    /// </summary>
    private struct BatchUpdateItem
    {
        public BatchUpdateType Type;
        public int ObjectId;
        public bool State;
        public double Timestamp;
    }
}

/// <summary>
/// 구역 기반 브로드캐스트 시스템
/// AI 없는 구역은 브로드캐스트 제외
/// </summary>
public class ZoneBasedBroadcast : MonoBehaviour
{
    public static ZoneBasedBroadcast Instance;
    
    // 구역 정보
    private readonly Dictionary<int, ZoneInfo> _zones = new();
    
    // 플레이어가 있는 구역
    private readonly Dictionary<string, int> _playerZones = new();
    
    // 구역 크기 (미터)
    private const float ZoneSize = 50f;
    
    private NetService Service => NetService.Instance;
    private bool IsServer => Service != null && Service.IsServer;
    
    private void Awake()
    {
        Instance = this;
    }
    
    private void Update()
    {
        if (!Service?.networkStarted ?? true) return;
        
        if (!IsServer) return; // 호스트만 구역 관리
        
        UpdatePlayerZones();
        UpdateZoneAICounts();
    }
    
    /// <summary>
    /// 플레이어 구역 업데이트
    /// </summary>
    private void UpdatePlayerZones()
    {
        // 로컬 플레이어
        var main = CharacterMainControl.Main;
        if (main != null)
        {
            var zoneId = GetZoneId(main.transform.position);
            var playerId = Service.GetPlayerId(null);
            _playerZones[playerId] = zoneId;
        }
        
        // 원격 플레이어
        foreach (var kvp in Service.remoteCharacters)
        {
            var playerObject = kvp.Value;
            if (playerObject == null) continue;
            
            var cmc = playerObject.GetComponent<CharacterMainControl>() 
                   ?? playerObject.GetComponentInChildren<CharacterMainControl>(true);
            
            if (cmc == null) continue;
            
            var zoneId = GetZoneId(cmc.transform.position);
            var playerId = Service.GetPlayerId(kvp.Key);
            _playerZones[playerId] = zoneId;
        }
    }
    
    /// <summary>
    /// 구역 AI 수 업데이트
    /// </summary>
    private void UpdateZoneAICounts()
    {
        _zones.Clear();
        
        // 모든 AI 순회
        foreach (var kvp in AITool.aiById)
        {
            var cmc = kvp.Value;
            if (cmc == null) continue;
            
            var zoneId = GetZoneId(cmc.transform.position);
            
            if (!_zones.TryGetValue(zoneId, out var zone))
            {
                zone = new ZoneInfo { ZoneId = zoneId, AICount = 0 };
                _zones[zoneId] = zone;
            }
            
            zone.AICount++;
        }
    }
    
    /// <summary>
    /// 위치에서 구역 ID 계산
    /// </summary>
    private int GetZoneId(Vector3 position)
    {
        var x = Mathf.FloorToInt(position.x / ZoneSize);
        var z = Mathf.FloorToInt(position.z / ZoneSize);
        return x * 1000 + z; // 간단한 해시
    }
    
    /// <summary>
    /// 구역에 플레이어가 있는지 확인
    /// </summary>
    public bool HasPlayersInZone(int zoneId)
    {
        return _playerZones.ContainsValue(zoneId);
    }
    
    /// <summary>
    /// 구역에 AI가 있는지 확인
    /// </summary>
    public bool HasAIInZone(int zoneId)
    {
        return _zones.TryGetValue(zoneId, out var zone) && zone.AICount > 0;
    }
    
    /// <summary>
    /// 브로드캐스트 필요 여부 확인
    /// </summary>
    public bool ShouldBroadcastToZone(int zoneId)
    {
        // 플레이어가 있거나 AI가 있으면 브로드캐스트
        return HasPlayersInZone(zoneId) || HasAIInZone(zoneId);
    }
    
    /// <summary>
    /// 구역 정보
    /// </summary>
    private class ZoneInfo
    {
        public int ZoneId;
        public int AICount;
    }
}

/// <summary>
/// 클라이언트 예측 총기 시스템
/// 탄환은 서버 판정, 궤적은 클라이언트 예측
/// </summary>
public class ClientPredictedWeaponSystem : MonoBehaviour
{
    public static ClientPredictedWeaponSystem Instance;
    
    // 예측 발사 히스토리
    private readonly Queue<PendingShot> _pendingShots = new(128);
    
    private NetService Service => NetService.Instance;
    private bool IsClient => Service != null && Service.networkStarted && !Service.IsServer;
    
    private void Awake()
    {
        Instance = this;
    }
    
    /// <summary>
    /// 예측 발사 처리 (클라이언트)
    /// </summary>
    public void PredictShot(ItemAgent_Gun gun, Vector3 muzzle, Vector3 direction)
    {
        if (!IsClient || Service?.connectedPeer == null) return;
        
        // 즉시 로컬에서 궤적 재생 (예측)
        PlayPredictedTrajectory(gun, muzzle, direction);
        
        // 서버에 요청
        var shotId = Time.frameCount;
        var pendingShot = new PendingShot
        {
            ShotId = shotId,
            MuzzlePosition = muzzle,
            Direction = direction,
            WeaponTypeId = gun.Item?.TypeID ?? 0,
            Timestamp = Time.unscaledTimeAsDouble
        };
        
        _pendingShots.Enqueue(pendingShot);
        
        // 서버에 요청 전송
        COOPManager.WeaponRequest.Net_OnClientShoot(gun, muzzle, direction, muzzle);
    }
    
    /// <summary>
    /// 서버 응답 처리 (클라이언트)
    /// </summary>
    public void OnServerShotResponse(int shotId, bool hit, Vector3 hitPoint)
    {
        if (!IsClient) return;
        
        // 예측된 발사 찾기
        PendingShot? found = null;
        foreach (var shot in _pendingShots)
        {
            if (shot.ShotId == shotId)
            {
                found = shot;
                break;
            }
        }
        
        if (found.HasValue)
        {
            var shot = found.Value;
            
            if (hit)
            {
                // 맞았다면 피격 이펙트 재생
                PlayHitEffect(hitPoint);
            }
            
            // 큐에서 제거
            // 실제로는 큐를 정리해야 함
        }
    }
    
    /// <summary>
    /// 예측 궤적 재생
    /// </summary>
    private void PlayPredictedTrajectory(ItemAgent_Gun gun, Vector3 muzzle, Vector3 direction)
    {
        // 즉시 로컬에서 발사 이펙트 재생
        if (gun && gun.muzzle)
        {
            var weaponType = gun.Item?.TypeID ?? 0;
            FxManager.Client_PlayLocalShotFx(gun, gun.muzzle, weaponType);
        }
    }
    
    /// <summary>
    /// 피격 이펙트 재생
    /// </summary>
    private void PlayHitEffect(Vector3 hitPoint)
    {
        // 피격 이펙트 재생 로직
    }
    
    /// <summary>
    /// 예측 발사 데이터
    /// </summary>
    private struct PendingShot
    {
        public int ShotId;
        public Vector3 MuzzlePosition;
        public Vector3 Direction;
        public int WeaponTypeId;
        public double Timestamp;
    }
}
