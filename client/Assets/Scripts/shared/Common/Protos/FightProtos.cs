using ProtoBuf;
using Shared.Core.Network;
using System.Collections.Generic;

namespace Shared.Common.Protos
{
    #region 使用技能

    [ProtoContract]
    public class UseSkillReq : IMessage
    {
        public ushort MsgId => MsgIdConst.UseSkillReq;
        [ProtoMember(1)] public int SkillId { get; set; }
        [ProtoMember(2)] public int X { get; set; }
        [ProtoMember(3)] public int Y { get; set; }
        [ProtoMember(4)] public long TargetId { get; set; }
    }

    [ProtoContract]
    public class SkillTargetInfo
    {
        [ProtoMember(1)] public long TargetId { get; set; }
        [ProtoMember(2)] public int DeltaHp { get; set; }
        [ProtoMember(3)] public int Hp { get; set; }
        [ProtoMember(4)] public bool IsCritical { get; set; }
    }

    [ProtoContract]
    public class UseSkillAck : IMessage
    {
        public ushort MsgId => MsgIdConst.UseSkillAck;
        [ProtoMember(1)] public string ErrorMessage { get; set; } = "";
        [ProtoMember(2)] public long AttackerId { get; set; }
        [ProtoMember(3)] public int SkillId { get; set; }
        [ProtoMember(4)] public long MainTargetId { get; set; }
        [ProtoMember(5)] public int X { get; set; }
        [ProtoMember(6)] public int Y { get; set; }
        [ProtoMember(7)] public List<long> MissedIds { get; set; } = new List<long>();
        [ProtoMember(8)] public List<long> IsCriticalIds { get; set; } = new List<long>();
        [ProtoMember(9)] public List<SkillTargetInfo> Targets { get; set; } = new List<SkillTargetInfo>();
    }

    #endregion

    #region HP变化通知

    public static class HpChangeReason
    {
        public const int SkillDamage = 1;
        public const int Heal = 2;
        public const int Killed = 3;
        public const int Revive = 4;
        public const int Buff = 5;
        public const int Recover = 6;  // 物品恢复
    }

    [ProtoContract]
    public class HpChangedNtf : IMessage
    {
        public ushort MsgId => MsgIdConst.HpChangedNtf;
        [ProtoMember(1)] public long ObjectId { get; set; }
        [ProtoMember(2)] public int DeltaHp { get; set; }
        [ProtoMember(3)] public int Hp { get; set; }
        [ProtoMember(4)] public int Reason { get; set; }
    }

    #endregion

    #region 被击杀通知

    [ProtoContract]
    public class KilledByNtf : IMessage
    {
        public ushort MsgId => MsgIdConst.KilledByNtf;
        [ProtoMember(1)] public long KillerId { get; set; }
        [ProtoMember(2)] public string KillerName { get; set; } = "";
        [ProtoMember(3)] public bool IsMonster { get; set; }
    }

    #endregion

    #region PK模式

    public static class PkMode
    {
        public const int Peace = 0;
        public const int Team = 1;
        public const int Guild = 2;
        public const int All = 3;
    }

    [ProtoContract]
    public class SetPkModeReq : IMessage
    {
        public ushort MsgId => MsgIdConst.SetPkModeReq;
        [ProtoMember(1)] public int PkMode { get; set; }
    }

    [ProtoContract]
    public class PkModeChangedNtf : IMessage
    {
        public ushort MsgId => MsgIdConst.PkModeChangedNtf;
        [ProtoMember(1)] public int PkMode { get; set; }
    }

    #endregion

    #region 经验/金币/升级/物品/MP变化通知

    /// <summary>
    /// 经验变化通知
    /// </summary>
    [ProtoContract]
    public class ExpChangedNtf : IMessage
    {
        ushort IMessage.MsgId => MsgIdConst.ExpChangedNtf;
        [ProtoMember(1)] public int Exp { get; set; }

        [ProtoMember(2)] public int Level { get; set; }
    }

    /// <summary>
    /// 金币变化通知
    /// </summary>
    [ProtoContract]
    public class GoldChangedNtf : IMessage
    {
        ushort IMessage.MsgId => MsgIdConst.GoldChangedNtf;
        [ProtoMember(1)] public int Gold { get; set; }
    }

    /// <summary>
    /// 升级通知
    /// </summary>
    [ProtoContract]
    public class LevelUpNtf : IMessage
    {
        ushort IMessage.MsgId => MsgIdConst.LevelUpNtf;
        [ProtoMember(1)] public int Level { get; set; }

        [ProtoMember(2)] public int MaxHp { get; set; }

        [ProtoMember(3)] public int MaxMp { get; set; }
    }

    /// <summary>
    /// 物品获得通知
    /// </summary>
    [ProtoContract]
    public class ItemGainNtf : IMessage
    {
        ushort IMessage.MsgId => MsgIdConst.ItemGainNtf;
        [ProtoMember(1)] public int ConfigId { get; set; }

        [ProtoMember(2)] public int Count { get; set; }
    }

    /// <summary>
    /// MP变化通知
    /// </summary>
    [ProtoContract]
    public class MpChangedNtf : IMessage
    {
        ushort IMessage.MsgId => MsgIdConst.MpChangedNtf;
        [ProtoMember(1)] public long ObjectId { get; set; }

        [ProtoMember(2)] public int DeltaMp { get; set; }

        [ProtoMember(3)] public int Mp { get; set; }

        [ProtoMember(4)] public int Reason { get; set; }
    }

    #endregion
}
