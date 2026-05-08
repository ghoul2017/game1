using Game.Core;
using Shared.Core.Event;
using Shared.Core.Log;

namespace Game.Components.Monster;

public static class MonsterHandlers
{
    private static readonly ILog Log = LogManager.GetLogger(nameof(MonsterHandlers));

    [EventSubscriber]
    public static void OnMonsterKilled(EventType.MonsterKilledEvent e)
    {
        Context.Monster.OnMonsterKilled(e.MonsterId, e.KillerId);
    }

    [EventSubscriber]
    public static void OnPlayerEnterMap(EventType.PlayerEnterMapEvent e)
    {
        Context.Monster.InitMapMonsters(e.MapCfgId, e.Line);
    }

    [EventSubscriber]
    public static void OnPlayerOffline(EventType.PlayerOfflineEvent e)
    {
        // 清除以该玩家为追击目标的怪物仇恨
        foreach (var kvp in Context.Monster.MapMonsters)
        {
            foreach (var monster in kvp.Value.Values)
            {
                if (monster.ChaseTarget == e.RoleId)
                {
                    monster.AiState = AiState.Idle;
                    monster.ChaseTarget = null;
                }
            }
        }
    }
}
