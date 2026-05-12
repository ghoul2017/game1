using Game.Components.Map;
using Shared.Core.Event;
using Shared.Core.Log;

namespace Game.Components.Monster;

public static class MonsterHandlers
{
    private static readonly ILog Log = LogManager.GetLogger(nameof(MonsterHandlers));

    [EventSubscriber]
    public static void OnPlayerEnterMap(EventType.PlayerEnterMapEvent e)
    {
        var monsterComp = Context.Monster;
        monsterComp.InitMapMonsters(e.MapCfgId, e.Line);
    }

    [EventSubscriber]
    public static void OnPlayerOffline(EventType.PlayerOfflineEvent e)
    {
        var monsterComp = Context.Monster;
        // 清除以该玩家为追击目标的怪物仇恨
        foreach (var kvp in monsterComp.MapMonsters)
        {
            foreach (var monster in kvp.Value.Values)
            {
                if (monster.Ai.ChaseTarget == e.RoleId)
                {
                    monster.Ai.ChaseTarget = null;
                    monster.Ai.AiState = AiState.Idle;
                }
            }
        }
    }
}
