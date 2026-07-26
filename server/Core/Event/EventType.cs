using Game.Components.Player;
using Shared.Common.Enums;

namespace Shared.Core.Event;

public static class EventType
{
    /// <summary>服务器启动完成</summary>
    public struct ServerInitEvent : IEvent
    {
    }

    /// <summary>玩家上线（选角/重连成功后）</summary>
    public struct PlayerOnlineEvent : IEvent
    {
        public PlayerEntity Player;
    }

    /// <summary>玩家下线（断线/被踢）</summary>
    public struct PlayerOfflineEvent : IEvent
    {
        public long RoleId;
        public LogoutReason Reason;
    }

    /// <summary>玩家进入地图</summary>
    public struct PlayerEnterMapEvent : IEvent
    {
        public long RoleId;
        public int MapCfgId;
        public int Line;
    }

    /// <summary>玩家离开地图</summary>
    public struct PlayerLeaveMapEvent : IEvent
    {
        public long RoleId;
        public int MapCfgId;
        public int Line;
    }

    /// <summary>怪物被击杀</summary>
    public struct MonsterKilledEvent : IEvent
    {
        public long MonsterId;
        public long KillerId;
        public int MonsterCfgId;
        public int MapId;
        public int Line;
    }

    /// <summary>玩家升级</summary>
    public struct PlayerLevelUpEvent : IEvent
    {
        public long RoleId;
        public int NewLevel;
    }

    /// <summary>玩家经验变化</summary>
    public struct PlayerExpChangedEvent : IEvent
    {
        public long RoleId;
        public int Exp;
        public int Level;
    }

    /// <summary>玩家金币变化</summary>
    public struct PlayerGoldChangedEvent : IEvent
    {
        public long RoleId;
        public int Gold;
    }

    /// <summary>玩家获得物品</summary>
    public struct PlayerItemGainedEvent : IEvent
    {
        public long RoleId;
        public int ConfigId;
        public long Count;
    }

    /// <summary>玩家战斗死亡</summary>
    public struct PlayerDeathEvent : IEvent
    {
        public long RoleId;
        public long KillerId;
    }

}
