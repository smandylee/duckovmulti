// Escape From Duckov Coop Mod - AI Authority Enforcement
// 개선 사항: AI Tick은 호스트만 실행하도록 강화

namespace EscapeFromDuckovCoopMod;

/// <summary>
/// AI 업데이트 권한 강화 패치
/// 클라이언트에서는 AI 업데이트를 완전히 차단
/// </summary>
[HarmonyPatch(typeof(AICharacterController), "Update")]
internal static class Patch_AIUpdate_HostOnly
{
    private static bool Prefix(AICharacterController __instance)
    {
        var mod = ModBehaviourF.Instance;
        if (mod == null || !mod.networkStarted) return true; // 오프라인 모드면 원래대로
        
        // 클라이언트에서는 AI Update를 완전히 차단
        if (!mod.IsServer)
        {
            // AI 컴포넌트 비활성화 (성능 최적화)
            if (__instance != null)
            {
                __instance.enabled = false;
                
                // NavMeshAgent 같은 컴포넌트도 비활성화
                var agent = __instance.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null) agent.enabled = false;
            }
            
            return false; // AI 업데이트 차단
        }
        
        // 호스트에서는 AI 컴포넌트 활성화 보장
        if (__instance != null && !__instance.enabled)
        {
            __instance.enabled = true;
            
            var agent = __instance.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null && !agent.enabled) agent.enabled = true;
        }
        
        return true; // 호스트만 AI 업데이트 실행
    }
}

/// <summary>
/// AI FSM 업데이트 권한 강화 패치
/// </summary>
[HarmonyPatch(typeof(FSM), "OnGraphUpdate")]
internal static class Patch_FSMUpdate_HostOnly
{
    private static bool Prefix(FSM __instance)
    {
        var mod = ModBehaviourF.Instance;
        if (mod == null || !mod.networkStarted) return true;
        
        // AI FSM인지 확인
        Component agent = null;
        try
        {
            agent = (Component)AccessTools.Property(typeof(Graph), "agent").GetValue(__instance, null);
        }
        catch
        {
            return true; // 확인 실패 시 원래대로
        }
        
        if (!agent) return true;
        
        var aiCmc = agent.GetComponentInParent<CharacterMainControl>();
        if (!aiCmc) return true;
        if (!AITool.IsRealAI(aiCmc)) return true; // 플레이어는 원래대로
        
        // AI FSM은 호스트만 실행
        if (!mod.IsServer)
        {
            return false; // 클라이언트에서는 차단
        }
        
        return true;
    }
}

/// <summary>
/// AI 행동 트리 업데이트 권한 강화 패치
/// </summary>
[HarmonyPatch]
internal static class Patch_BehaviourTreeUpdate_HostOnly
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        // NodeCanvas BehaviourTree의 Update 메서드들
        var btType = typeof(BehaviourTree);
        if (btType != null)
        {
            yield return AccessTools.Method(btType, "UpdateGraph");
        }
        
        // 다른 AI 행동 관련 Update 메서드들도 추가 가능
    }
    
    private static bool Prefix()
    {
        var mod = ModBehaviourF.Instance;
        if (mod == null || !mod.networkStarted) return true;
        
        // 호스트만 실행
        return mod.IsServer;
    }
}

/// <summary>
/// AI 컴포넌트의 모든 Update/FixedUpdate/LateUpdate 메서드 차단
/// </summary>
[HarmonyPatch]
internal static class Patch_AIComponentUpdates_HostOnly
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        // AICharacterController의 모든 업데이트 메서드
        var aiType = typeof(AICharacterController);
        if (aiType != null)
        {
            var updateMethods = new[] { "FixedUpdate", "LateUpdate" };
            foreach (var methodName in updateMethods)
            {
                var method = AccessTools.Method(aiType, methodName);
                if (method != null) yield return method;
            }
        }
    }
    
    private static bool Prefix(MonoBehaviour __instance)
    {
        var mod = ModBehaviourF.Instance;
        if (mod == null || !mod.networkStarted) return true;
        
        // AI 컴포넌트인지 확인
        var aiCmc = __instance.GetComponentInParent<CharacterMainControl>();
        if (aiCmc == null) return true;
        if (!AITool.IsRealAI(aiCmc)) return true;
        
        // 호스트만 실행
        return mod.IsServer;
    }
}

