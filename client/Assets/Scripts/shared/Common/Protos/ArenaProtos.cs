using System.Collections.Generic;
using ProtoBuf;
using Shared.Core.Network;

namespace Shared.Common.Protos
{
    [ProtoContract]
    [Message("Arena.BattleStartReq")]
    public sealed class ArenaBattleStartReq : IMessage
    {
        [ProtoMember(1)]
        public string RequestId { get; set; } = string.Empty;
    }

    [ProtoContract]
    [Message("Arena.BattleStartAck")]
    public sealed class ArenaBattleStartAck : IMessage
    {
        [ProtoMember(1)]
        public string ErrorMessage { get; set; } = string.Empty;

        [ProtoMember(2)]
        public long BattleId { get; set; }

        [ProtoMember(3)]
        public int Outcome { get; set; }

        [ProtoMember(4)]
        public int EndReason { get; set; }

        [ProtoMember(5)]
        public long FinalTick { get; set; }

        [ProtoMember(6)]
        public ulong EventHash { get; set; }
    }

    [ProtoContract]
    [Message("Arena.ReplayReq")]
    public sealed class ArenaReplayReq : IMessage
    {
        [ProtoMember(1)]
        public long BattleId { get; set; }
    }

    [ProtoContract]
    [Message("Arena.ReplayAck")]
    public sealed class ArenaReplayAck : IMessage
    {
        [ProtoMember(1)]
        public string ErrorMessage { get; set; } = string.Empty;

        [ProtoMember(2)]
        public long BattleId { get; set; }

        [ProtoMember(3)]
        public uint Seed { get; set; }

        [ProtoMember(4)]
        public int TicksPerSecond { get; set; }

        [ProtoMember(5)]
        public int RulesVersion { get; set; }

        [ProtoMember(6)]
        public int ContentVersion { get; set; }

        [ProtoMember(7)]
        public string AttackerSnapshotId { get; set; } = string.Empty;

        [ProtoMember(8)]
        public string DefenderSnapshotId { get; set; } = string.Empty;

        [ProtoMember(9)]
        public List<ArenaUnitSnapshotInfo> AttackerUnits { get; set; } = new List<ArenaUnitSnapshotInfo>();

        [ProtoMember(10)]
        public List<ArenaUnitSnapshotInfo> DefenderUnits { get; set; } = new List<ArenaUnitSnapshotInfo>();

        [ProtoMember(11)]
        public List<ArenaReplayEventInfo> Events { get; set; } = new List<ArenaReplayEventInfo>();

        [ProtoMember(12)]
        public List<ArenaReplayKeyFrameInfo> KeyFrames { get; set; } = new List<ArenaReplayKeyFrameInfo>();

        [ProtoMember(13)]
        public int Outcome { get; set; }

        [ProtoMember(14)]
        public int EndReason { get; set; }

        [ProtoMember(15)]
        public long FinalTick { get; set; }

        [ProtoMember(16)]
        public ulong EventHash { get; set; }
    }

    [ProtoContract]
    public sealed class ArenaUnitSnapshotInfo
    {
        [ProtoMember(1)]
        public int UnitId { get; set; }

        [ProtoMember(2)]
        public int TemplateId { get; set; }

        [ProtoMember(3)]
        public int Slot { get; set; }

        [ProtoMember(4)]
        public int MaxHealth { get; set; }

        [ProtoMember(5)]
        public int AttackDamage { get; set; }

        [ProtoMember(6)]
        public int AttackIntervalTicks { get; set; }
    }

    [ProtoContract]
    public sealed class ArenaReplayEventInfo
    {
        [ProtoMember(1)]
        public long Tick { get; set; }

        [ProtoMember(2)]
        public int Kind { get; set; }

        [ProtoMember(3)]
        public int SourceSide { get; set; }

        [ProtoMember(4)]
        public int SourceUnitId { get; set; }

        [ProtoMember(5)]
        public int TargetSide { get; set; }

        [ProtoMember(6)]
        public int TargetUnitId { get; set; }

        [ProtoMember(7)]
        public int Value { get; set; }

        [ProtoMember(8)]
        public int RemainingHealth { get; set; }
    }

    [ProtoContract]
    public sealed class ArenaReplayKeyFrameInfo
    {
        [ProtoMember(1)]
        public long Tick { get; set; }

        [ProtoMember(2)]
        public List<ArenaReplayUnitFrameInfo> Units { get; set; } = new List<ArenaReplayUnitFrameInfo>();
    }

    [ProtoContract]
    public sealed class ArenaReplayUnitFrameInfo
    {
        [ProtoMember(1)]
        public int Side { get; set; }

        [ProtoMember(2)]
        public int UnitId { get; set; }

        [ProtoMember(3)]
        public int Health { get; set; }
    }
}
