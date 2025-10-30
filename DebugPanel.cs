// Escape From Duckov Coop Mod - Debug Panel
// 개선 사항: 인게임 디버그 패널 (F9)

namespace EscapeFromDuckovCoopMod;

/// <summary>
/// 멀티플레이어 디버그 패널
/// F9 키로 열고 닫기
/// </summary>
public class MultiplayerDebugPanel : MonoBehaviour
{
    public static MultiplayerDebugPanel Instance;
    
    // UI 표시 여부
    private bool _showPanel = false;
    
    // 패널 위치 및 크기
    private Rect _panelRect = new Rect(10, 10, 500, 600);
    
    // 스크롤 위치
    private Vector2 _scrollPosition = Vector2.zero;
    
    // 정보 새로고침 타이머
    private float _refreshTimer = 0f;
    private const float RefreshInterval = 0.5f; // 0.5초마다 새로고침
    
    // 디버그 정보
    private DebugInfo _debugInfo = new();
    
    private NetService Service => NetService.Instance;
    
    private void Awake()
    {
        Instance = this;
    }
    
    private void Update()
    {
        // F9 키로 패널 토글
        if (Input.GetKeyDown(KeyCode.F9))
        {
            _showPanel = !_showPanel;
        }
        
        // 정보 새로고침
        if (_showPanel)
        {
            _refreshTimer += Time.deltaTime;
            if (_refreshTimer >= RefreshInterval)
            {
                UpdateDebugInfo();
                _refreshTimer = 0f;
            }
        }
    }
    
    private void OnGUI()
    {
        if (!_showPanel) return;
        
        // 패널 창 그리기
        _panelRect = GUILayout.Window(1001, _panelRect, DrawDebugPanel, "멀티플레이어 디버그 패널 (F9)");
    }
    
    /// <summary>
    /// 디버그 패널 그리기
    /// </summary>
    private void DrawDebugPanel(int windowId)
    {
        GUILayout.BeginVertical();
        
        // 닫기 버튼
        if (GUILayout.Button("닫기 (F9)"))
        {
            _showPanel = false;
        }
        
        GUILayout.Space(10);
        
        // 스크롤 영역
        _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);
        
        // 네트워크 상태
        DrawNetworkStatus();
        
        GUILayout.Space(10);
        
        // 플레이어 정보
        DrawPlayerInfo();
        
        GUILayout.Space(10);
        
        // 동기화 정보
        DrawSyncInfo();
        
        GUILayout.Space(10);
        
        // 에러 로그
        DrawErrorLog();
        
        GUILayout.EndScrollView();
        
        GUILayout.EndVertical();
        
        // 창 드래그 가능
        GUI.DragWindow();
    }
    
    /// <summary>
    /// 네트워크 상태 그리기
    /// </summary>
    private void DrawNetworkStatus()
    {
        GUILayout.Label("=== 네트워크 상태 ===", GUI.skin.box);
        
        if (Service == null)
        {
            GUILayout.Label("네트워크 서비스 없음");
            return;
        }
        
        GUILayout.Label($"상태: {Service.status}");
        GUILayout.Label($"네트워크 시작됨: {Service.networkStarted}");
        GUILayout.Label($"호스트: {Service.IsServer}");
        GUILayout.Label($"포트: {Service.port}");
        
        if (Service.connectedPeer != null)
        {
            GUILayout.Label($"연결된 Peer: {Service.connectedPeer.EndPoint}");
            GUILayout.Label($"핑: {Service.connectedPeer.Ping}ms");
            GUILayout.Label($"RTT: {Service.connectedPeer.Ping * 2}ms");
        }
        
        // 패킷 통계 (LiteNetLib 통계 사용)
        if (Service.netManager != null)
        {
            var stats = Service.netManager.Statistics;
            GUILayout.Label($"전송 패킷: {stats.PacketsSent}");
            GUILayout.Label($"수신 패킷: {stats.PacketsReceived}");
            GUILayout.Label($"전송 바이트: {stats.BytesSent}");
            GUILayout.Label($"수신 바이트: {stats.BytesReceived}");
        }
    }
    
    /// <summary>
    /// 플레이어 정보 그리기
    /// </summary>
    private void DrawPlayerInfo()
    {
        GUILayout.Label("=== 플레이어 정보 ===", GUI.skin.box);
        
        if (Service == null) return;
        
        if (Service.IsServer)
        {
            GUILayout.Label($"플레이어 수: {1 + Service.playerStatuses.Count}");
            
            // 로컬 플레이어
            if (Service.localPlayerStatus != null)
            {
                GUILayout.Label($"로컬 플레이어: {Service.localPlayerStatus.PlayerName}");
                GUILayout.Label($"  위치: {Service.localPlayerStatus.Position}");
                GUILayout.Label($"  씬: {Service.localPlayerStatus.SceneId}");
            }
            
            // 원격 플레이어
            foreach (var kvp in Service.playerStatuses)
            {
                var status = kvp.Value;
                if (status == null) continue;
                
                GUILayout.Label($"원격 플레이어: {status.PlayerName}");
                GUILayout.Label($"  핑: {status.Latency}ms");
                GUILayout.Label($"  위치: {status.Position}");
                GUILayout.Label($"  씬: {status.SceneId}");
            }
        }
        else
        {
            GUILayout.Label($"플레이어 수: {1 + Service.clientPlayerStatuses.Count}");
            
            // 로컬 플레이어
            if (Service.localPlayerStatus != null)
            {
                GUILayout.Label($"로컬 플레이어: {Service.localPlayerStatus.PlayerName}");
            }
            
            // 원격 플레이어
            foreach (var kvp in Service.clientPlayerStatuses)
            {
                var status = kvp.Value;
                if (status == null) continue;
                
                GUILayout.Label($"원격 플레이어: {status.PlayerName}");
                GUILayout.Label($"  핑: {status.Latency}ms");
            }
        }
    }
    
    /// <summary>
    /// 동기화 정보 그리기
    /// </summary>
    private void DrawSyncInfo()
    {
        GUILayout.Label("=== 동기화 정보 ===", GUI.skin.box);
        
        var mod = ModBehaviourF.Instance;
        if (mod == null) return;
        
        GUILayout.Label($"동기화 주기: {mod.syncInterval * 1000:F1}ms");
        GUILayout.Label($"브로드캐스트 주기: {mod.broadcastInterval:F1}초");
        
        // AI 동기화
        var aiHandle = COOPManager.AIHandle;
        if (aiHandle != null)
        {
            GUILayout.Label($"AI 수: {AITool.aiById.Count}");
            GUILayout.Label($"AI 동결: {aiHandle.freezeAI}");
        }
        
        // 시간 동기화
        if (Service != null && !Service.IsServer)
        {
            var timeDiff = _debugInfo.ServerTimeDifference;
            GUILayout.Label($"서버 시간 차이: {timeDiff * 1000:F1}ms");
        }
    }
    
    /// <summary>
    /// 에러 로그 그리기
    /// </summary>
    private void DrawErrorLog()
    {
        GUILayout.Label("=== 마지막 에러 로그 ===", GUI.skin.box);
        
        if (string.IsNullOrEmpty(_debugInfo.LastError))
        {
            GUILayout.Label("에러 없음");
        }
        else
        {
            GUILayout.TextArea(_debugInfo.LastError, GUILayout.Height(100));
        }
    }
    
    /// <summary>
    /// 디버그 정보 업데이트
    /// </summary>
    private void UpdateDebugInfo()
    {
        _debugInfo = new DebugInfo();
        
        // 네트워크 정보
        if (Service != null)
        {
            _debugInfo.NetworkStarted = Service.networkStarted;
            _debugInfo.IsServer = Service.IsServer;
            _debugInfo.Port = Service.port;
            
            if (Service.connectedPeer != null)
            {
                _debugInfo.RTT = Service.connectedPeer.Ping * 2;
            }
            
            if (Service.netManager != null)
            {
                var stats = Service.netManager.Statistics;
                _debugInfo.PacketsSent = stats.PacketsSent;
                _debugInfo.PacketsReceived = stats.PacketsReceived;
            }
        }
        
        // 플레이어 수
        _debugInfo.PlayerCount = CoopAPIHelper.GetPlayerCount();
        
        // AI 수
        _debugInfo.AICount = AITool.aiById.Count;
        
        // 동기화 안 된 엔티티 카운트
        _debugInfo.UnsyncedEntityCount = CalculateUnsyncedEntities();
        
        // 마지막 에러 (에러 로그 시스템 필요)
        _debugInfo.LastError = GetLastError();
    }
    
    /// <summary>
    /// 동기화 안 된 엔티티 수 계산
    /// </summary>
    private int CalculateUnsyncedEntities()
    {
        // 실제 구현은 동기화 상태를 추적하는 시스템 필요
        // 여기서는 기본값 반환
        return 0;
    }
    
    /// <summary>
    /// 마지막 에러 가져오기
    /// </summary>
    private string GetLastError()
    {
        // 에러 로그 시스템에서 가져오기
        // 실제 구현은 로그 시스템에 따라 다름
        return "";
    }
    
    /// <summary>
    /// 디버그 정보 구조
    /// </summary>
    private class DebugInfo
    {
        public bool NetworkStarted;
        public bool IsServer;
        public int Port;
        public int RTT;
        public long PacketsSent;
        public long PacketsReceived;
        public int PlayerCount;
        public int AICount;
        public int UnsyncedEntityCount;
        public double ServerTimeDifference;
        public string LastError;
    }
}

/// <summary>
/// 디버그 패널 초기화
/// </summary>
[HarmonyPatch(typeof(ModBehaviour), "Loader")]
internal static class Patch_Mod_InitDebugPanel
{
    private static void Postfix()
    {
        // 디버그 패널 GameObject 생성
        var go = new GameObject("MultiplayerDebugPanel");
        DontDestroyOnLoad(go);
        go.AddComponent<MultiplayerDebugPanel>();
    }
}

