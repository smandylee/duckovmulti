// Escape From Duckov Coop Mod - Room System & Matchmaking
// 개선 사항: 방 리스트 UI, 비밀번호, 재접속 복구

using LiteNetLib;
using LiteNetLib.Utils;

namespace EscapeFromDuckovCoopMod;

/// <summary>
/// 방 정보 구조
/// </summary>
public class RoomInfo
{
    public string RoomId { get; set; }
    public string RoomName { get; set; }
    public string HostName { get; set; }
    public string HostIP { get; set; }
    public int Port { get; set; }
    public int CurrentPlayers { get; set; }
    public int MaxPlayers { get; set; }
    public bool HasPassword { get; set; }
    public bool IsPrivate { get; set; }
    public string MapName { get; set; }
    public double LastUpdateTime { get; set; }
}

/// <summary>
/// 방 시스템 관리자
/// </summary>
public class RoomSystem : MonoBehaviour
{
    public static RoomSystem Instance;
    
    // 방 리스트 (클라이언트)
    private readonly List<RoomInfo> _roomList = new();
    
    // 현재 방 정보
    public RoomInfo CurrentRoom { get; private set; }
    
    // 방 비밀번호 (호스트 설정)
    public string RoomPassword { get; set; }
    
    // 브로드캐스트 타이머
    private float _broadcastTimer = 0f;
    private const float BroadcastInterval = 2f; // 2초마다 브로드캐스트
    
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
        
        // 호스트: 방 정보 브로드캐스트
        if (IsServer)
        {
            _broadcastTimer += Time.deltaTime;
            if (_broadcastTimer >= BroadcastInterval)
            {
                BroadcastRoomInfo();
                _broadcastTimer = 0f;
            }
        }
        
        // 클라이언트: 오래된 방 정리
        else
        {
            CleanupOldRooms();
        }
    }
    
    /// <summary>
    /// 방 정보 브로드캐스트 (호스트)
    /// </summary>
    private void BroadcastRoomInfo()
    {
        if (writer == null || netManager == null) return;
        
        var roomInfo = CreateCurrentRoomInfo();
        CurrentRoom = roomInfo;
        
        // 브로드캐스트 메시지 전송
        writer.Reset();
        writer.Put((byte)OpExtensions.ROOM_CREATE);
        
        writer.Put(roomInfo.RoomId);
        writer.Put(roomInfo.RoomName);
        writer.Put(roomInfo.HostName);
        writer.Put(roomInfo.HostIP);
        writer.Put(roomInfo.Port);
        writer.Put(roomInfo.CurrentPlayers);
        writer.Put(roomInfo.MaxPlayers);
        writer.Put(roomInfo.HasPassword);
        writer.Put(roomInfo.IsPrivate);
        writer.Put(roomInfo.MapName ?? "");
        writer.Put(Time.unscaledTimeAsDouble);
        
        // 브로드캐스트 전송
        netManager.SendBroadcast(writer, Service.port);
    }
    
    /// <summary>
    /// 현재 방 정보 생성
    /// </summary>
    private RoomInfo CreateCurrentRoomInfo()
    {
        var scene = SceneManager.GetActiveScene();
        var mapName = scene.name;
        
        var playerCount = 1; // 호스트 포함
        if (Service != null)
        {
            playerCount += Service.playerStatuses.Count;
        }
        
        return new RoomInfo
        {
            RoomId = Service?.localPlayerStatus?.EndPoint ?? "unknown",
            RoomName = $"방: {mapName}",
            HostName = CharacterMainControl.Main?.name ?? "Host",
            HostIP = GetLocalIP(),
            Port = Service?.port ?? 9050,
            CurrentPlayers = playerCount,
            MaxPlayers = 8, // 최대 인원 설정 가능
            HasPassword = !string.IsNullOrEmpty(RoomPassword),
            IsPrivate = false,
            MapName = mapName,
            LastUpdateTime = Time.unscaledTimeAsDouble
        };
    }
    
    /// <summary>
    /// 방 정보 수신 (클라이언트)
    /// </summary>
    public void ReceiveRoomInfo(string roomId, string roomName, string hostName, string hostIP, 
        int port, int currentPlayers, int maxPlayers, bool hasPassword, bool isPrivate, 
        string mapName, double timestamp)
    {
        // 이미 있는 방인지 확인
        var existingRoom = _roomList.FirstOrDefault(r => r.RoomId == roomId);
        
        if (existingRoom != null)
        {
            // 업데이트
            existingRoom.RoomName = roomName;
            existingRoom.HostName = hostName;
            existingRoom.CurrentPlayers = currentPlayers;
            existingRoom.HasPassword = hasPassword;
            existingRoom.IsPrivate = isPrivate;
            existingRoom.LastUpdateTime = timestamp;
        }
        else
        {
            // 새 방 추가
            var roomInfo = new RoomInfo
            {
                RoomId = roomId,
                RoomName = roomName,
                HostName = hostName,
                HostIP = hostIP,
                Port = port,
                CurrentPlayers = currentPlayers,
                MaxPlayers = maxPlayers,
                HasPassword = hasPassword,
                IsPrivate = isPrivate,
                MapName = mapName,
                LastUpdateTime = timestamp
            };
            
            _roomList.Add(roomInfo);
        }
    }
    
    /// <summary>
    /// 방 참가 요청 (클라이언트)
    /// </summary>
    public void JoinRoom(RoomInfo room, string password = null)
    {
        if (room == null) return;
        
        // 비밀번호 확인
        if (room.HasPassword && password != room.RoomPassword)
        {
            PopText.Pop("비밀번호가 올바르지 않습니다", Vector3.zero);
            return;
        }
        
        // 방 참가
        Service?.ConnectToHost(room.HostIP, room.Port);
        
        if (!string.IsNullOrEmpty(password))
        {
            // 비밀번호 전송
            SendPassword(password);
        }
    }
    
    /// <summary>
    /// 비밀번호 전송
    /// </summary>
    private void SendPassword(string password)
    {
        if (Service?.connectedPeer == null || writer == null) return;
        
        writer.Reset();
        writer.Put((byte)OpExtensions.ROOM_PASSWORD);
        writer.Put(password);
        
        Service.connectedPeer.Send(writer, DeliveryMethod.ReliableOrdered);
    }
    
    /// <summary>
    /// 비밀번호 검증 (호스트)
    /// </summary>
    public bool ValidatePassword(NetPeer peer, string password)
    {
        if (!IsServer) return false;
        
        if (string.IsNullOrEmpty(RoomPassword))
            return true; // 비밀번호 없으면 통과
        
        return password == RoomPassword;
    }
    
    /// <summary>
    /// 방 리스트 가져오기
    /// </summary>
    public List<RoomInfo> GetRoomList()
    {
        return _roomList.ToList();
    }
    
    /// <summary>
    /// 오래된 방 정리
    /// </summary>
    private void CleanupOldRooms()
    {
        var currentTime = Time.unscaledTimeAsDouble;
        var timeout = 5.0; // 5초 동안 응답 없으면 제거
        
        _roomList.RemoveAll(room => currentTime - room.LastUpdateTime > timeout);
    }
    
    /// <summary>
    /// 로컬 IP 주소 가져오기
    /// </summary>
    private string GetLocalIP()
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
        }
        catch
        {
        }
        
        return "127.0.0.1";
    }
}

/// <summary>
/// 재접속 상태 복구 시스템
/// </summary>
public class ReconnectStateRecovery : MonoBehaviour
{
    public static ReconnectStateRecovery Instance;
    
    // 플레이어 상태 스냅샷 (주기적으로 저장)
    private PlayerStateSnapshot _lastSnapshot;
    private float _snapshotTimer = 0f;
    private const float SnapshotInterval = 5f; // 5초마다 저장
    
    private NetService Service => NetService.Instance;
    private bool IsClient => Service != null && Service.networkStarted && !Service.IsServer;
    
    private void Awake()
    {
        Instance = this;
    }
    
    private void Update()
    {
        if (!IsClient) return;
        
        _snapshotTimer += Time.deltaTime;
        if (_snapshotTimer >= SnapshotInterval)
        {
            SaveSnapshot();
            _snapshotTimer = 0f;
        }
    }
    
    /// <summary>
    /// 상태 스냅샷 저장
    /// </summary>
    private void SaveSnapshot()
    {
        var main = CharacterMainControl.Main;
        if (main == null) return;
        
        _lastSnapshot = new PlayerStateSnapshot
        {
            Position = main.transform.position,
            Rotation = main.transform.rotation,
            Health = main.Health?.CurrentHealth ?? 0f,
            MaxHealth = main.Health?.MaxHealth ?? 0f,
            SceneId = SceneManager.GetActiveScene().name,
            Timestamp = Time.unscaledTimeAsDouble
        };
        
        // 인벤토리 상태도 저장 가능 (추가 구현)
    }
    
    /// <summary>
    /// 재접속 시 상태 복구 요청
    /// </summary>
    public void RequestStateRecovery()
    {
        if (Service?.connectedPeer == null || Service.writer == null) return;
        
        Service.writer.Reset();
        Service.writer.Put((byte)OpExtensions.RECONNECT_STATE);
        Service.writer.Put(Service.localPlayerStatus?.EndPoint ?? "");
        
        // 마지막 스냅샷 전송
        if (_lastSnapshot != null)
        {
            Service.writer.PutV3cm(_lastSnapshot.Position);
            Service.writer.PutQuaternion(_lastSnapshot.Rotation);
            Service.writer.Put(_lastSnapshot.Health);
            Service.writer.Put(_lastSnapshot.MaxHealth);
            Service.writer.Put(_lastSnapshot.SceneId ?? "");
        }
        
        Service.connectedPeer.Send(Service.writer, DeliveryMethod.ReliableOrdered);
    }
    
    /// <summary>
    /// 재접속 상태 수신 (호스트)
    /// </summary>
    public void HandleReconnectState(NetPeer peer, PlayerStateSnapshot snapshot)
    {
        if (!Service.IsServer) return;
        
        // 클라이언트의 마지막 상태 확인 및 복구
        // 실제 구현은 게임 상태 복구 로직에 따라 다름
    }
    
    /// <summary>
    /// 플레이어 상태 스냅샷
    /// </summary>
    public class PlayerStateSnapshot
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public float Health;
        public float MaxHealth;
        public string SceneId;
        public double Timestamp;
    }
}

