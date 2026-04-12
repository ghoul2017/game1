using System.Collections.Generic;
using ProtoBuf;

namespace Client.Shared.Messages
{
    // ─── 使用技能 ───

    [ProtoContract]
    public class UseSkillReq : IMessage
    {
        public ushort MsgId => MsgId.UseSkillReq;
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
        public ushort MsgId => MsgId.UseSkillAck;
        [ProtoMember(1)] public int Code { get; set; }
        [ProtoMember(2)] public long AttackerId { get; set; }
        [ProtoMember(3)] public int SkillId { get; set; }
        [ProtoMember(4)] public long MainTargetId { get; set; }
        [ProtoMember(5)] public int X { get; set; }
        [ProtoMember(6)] public int Y { get; set; }
        [ProtoMember(7)] public List<long> MissedIds { get; set; } = new List<long>();
        [ProtoMember(8)] public List<long> IsCriticalIds { get; set; } = new List<long>();
        [ProtoMember(9)] public List<SkillTargetInfo> Targets { get; set; } = new List<SkillTargetInfo>();
    }

    // ─── HP变化通知 ───

    public static class HpChangeReason
    {
        public const int SkillDamage = 1;
        public const int Heal = 2;
        public const int Killed = 3;
        public const int Revive = 4;
        public const int Buff = 5;
    }

    [ProtoContract]
    public class HpChangedNtf : IMessage
    {
        public ushort MsgId => MsgId.HpChangedNtf;
        [ProtoMember(1)] public long ObjectId { get; set; }
        [ProtoMember(2)] public int DeltaHp { get; set; }
        [ProtoMember(3)] public int Hp { get; set; }
        [ProtoMember(4)] public int Reason { get; set; }
    }

    // ─── 被击杀通知 ───

    [ProtoContract]
    public class KilledByNtf : IMessage
    {
        public ushort MsgId => MsgId.KilledByNtf;
        [ProtoMember(1)] public long KillerId { get; set; }
        [ProtoMember(2)] public string KillerName { get; set; } = "";
        [ProtoMember(3)] public bool IsMonster { get; set; }
    }

    // ─── PK模式 ───

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
        public ushort MsgId => MsgId.SetPkModeReq;
        [ProtoMember(1)] public int PkMode { get; set; }
    }

    [ProtoContract]
    public class PkModeChangedNtf : IMessage
    {
        public ushort MsgId => MsgId.PkModeChangedNtf;
        [ProtoMember(1)] public int PkMode { get; set; }
    }
}
