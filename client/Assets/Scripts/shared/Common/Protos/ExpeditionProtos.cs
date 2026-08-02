using System;
using System.Collections.Generic;
using ProtoBuf;
using Shared.Core.Network;

namespace Shared.Common.Protos
{
    public static class ExpeditionProtocolConstants
    {
        public const int DirectionScale = 1000;
        public const int FacilityId = 1;

        /// <summary>将方向编码到协议圆形边界内，避免独立四舍五入把单位向量推到圆外。</summary>
        public static void QuantizeDirection(
            float directionX,
            float directionY,
            out int quantizedX,
            out int quantizedY)
        {
            if (float.IsNaN(directionX) || float.IsInfinity(directionX) ||
                float.IsNaN(directionY) || float.IsInfinity(directionY))
            {
                throw new ArgumentOutOfRangeException(nameof(directionX), "移动方向必须是有限数值");
            }

            double lengthSquared = ((double)directionX * directionX) + ((double)directionY * directionY);
            if (lengthSquared > 1.0d)
            {
                float reciprocalLength = (float)(1.0d / Math.Sqrt(lengthSquared));
                directionX *= reciprocalLength;
                directionY *= reciprocalLength;
            }

            quantizedX = (int)Math.Round(directionX * DirectionScale, MidpointRounding.AwayFromZero);
            quantizedY = (int)Math.Round(directionY * DirectionScale, MidpointRounding.AwayFromZero);
            long maximumLengthSquared = (long)DirectionScale * DirectionScale;
            while (((long)quantizedX * quantizedX) + ((long)quantizedY * quantizedY) > maximumLengthSquared)
            {
                if (Math.Abs(quantizedX) >= Math.Abs(quantizedY))
                {
                    quantizedX -= Math.Sign(quantizedX);
                }
                else
                {
                    quantizedY -= Math.Sign(quantizedY);
                }
            }
        }
    }

    [ProtoContract]
    [Message("Expedition.StartReq")]
    public sealed class ExpeditionStartReq : IMessage
    {
        [ProtoMember(1)] public long RequestId { get; set; }
    }

    [ProtoContract]
    [Message("Expedition.StartRes")]
    public sealed class ExpeditionStartRes : IMessage
    {
        [ProtoMember(1)] public string ErrorMessage { get; set; } = "";
        [ProtoMember(2)] public ExpeditionSnapshotData Snapshot { get; set; } = new ExpeditionSnapshotData();
        [ProtoMember(3)] public ExpeditionProgressData Progress { get; set; } = new ExpeditionProgressData();
    }

    /// <summary>
    /// 客户端唯一可提交的局内命令。协议不包含位置、伤害、种子、掉落、终态或收益。
    /// </summary>
    [ProtoContract]
    [Message("Expedition.InputNtf")]
    public sealed class ExpeditionInputNtf : IMessage
    {
        [ProtoMember(1)] public long RunId { get; set; }
        [ProtoMember(2)] public long InputSequence { get; set; }
        [ProtoMember(3)] public int DirectionX { get; set; }
        [ProtoMember(4)] public int DirectionY { get; set; }
        [ProtoMember(5)] public int InteractionTargetId { get; set; }
    }

    [ProtoContract]
    [Message("Expedition.ResumeReq")]
    public sealed class ExpeditionResumeReq : IMessage
    {
        [ProtoMember(1)] public long RunId { get; set; }
    }

    [ProtoContract]
    [Message("Expedition.ResumeRes")]
    public sealed class ExpeditionResumeRes : IMessage
    {
        [ProtoMember(1)] public string ErrorMessage { get; set; } = "";
        [ProtoMember(2)] public bool HasActiveRun { get; set; }
        [ProtoMember(3)] public ExpeditionSnapshotData Snapshot { get; set; } = new ExpeditionSnapshotData();
        [ProtoMember(4)] public ExpeditionSettlementData Settlement { get; set; } = new ExpeditionSettlementData();
        [ProtoMember(5)] public ExpeditionProgressData Progress { get; set; } = new ExpeditionProgressData();
    }

    [ProtoContract]
    [Message("Expedition.SettlementReq")]
    public sealed class ExpeditionSettlementReq : IMessage
    {
        [ProtoMember(1)] public long RunId { get; set; }
    }

    [ProtoContract]
    [Message("Expedition.SettlementRes")]
    public sealed class ExpeditionSettlementRes : IMessage
    {
        [ProtoMember(1)] public string ErrorMessage { get; set; } = "";
        [ProtoMember(2)] public ExpeditionSettlementData Settlement { get; set; } = new ExpeditionSettlementData();
        [ProtoMember(3)] public ExpeditionProgressData Progress { get; set; } = new ExpeditionProgressData();
    }

    [ProtoContract]
    [Message("Expedition.QueryProgressReq")]
    public sealed class ExpeditionQueryProgressReq : IMessage
    {
    }

    [ProtoContract]
    [Message("Expedition.QueryProgressRes")]
    public sealed class ExpeditionQueryProgressRes : IMessage
    {
        [ProtoMember(1)] public string ErrorMessage { get; set; } = "";
        [ProtoMember(2)] public ExpeditionProgressData Progress { get; set; } = new ExpeditionProgressData();
    }

    [ProtoContract]
    [Message("Expedition.UpgradeReq")]
    public sealed class ExpeditionUpgradeReq : IMessage
    {
        [ProtoMember(1)] public int FacilityId { get; set; }
    }

    [ProtoContract]
    [Message("Expedition.UpgradeRes")]
    public sealed class ExpeditionUpgradeRes : IMessage
    {
        [ProtoMember(1)] public string ErrorMessage { get; set; } = "";
        [ProtoMember(2)] public ExpeditionProgressData Progress { get; set; } = new ExpeditionProgressData();
        [ProtoMember(3)] public long SpentResource { get; set; }
        [ProtoMember(4)] public int OldFacilityLevel { get; set; }
        [ProtoMember(5)] public int NewFacilityLevel { get; set; }
        [ProtoMember(6)] public int OldRepresentativeDamage { get; set; }
        [ProtoMember(7)] public int NewRepresentativeDamage { get; set; }
    }

    [ProtoContract]
    [Message("Expedition.SnapshotNtf")]
    public sealed class ExpeditionSnapshotNtf : IMessage
    {
        [ProtoMember(1)] public ExpeditionSnapshotData Snapshot { get; set; } = new ExpeditionSnapshotData();
    }

    /// <summary>
    /// 不可被普通状态快照覆盖的局内表现事件批次。同一局内 EventSequence 严格单调递增。
    /// </summary>
    [ProtoContract]
    [Message("Expedition.EventBatchNtf")]
    public sealed class ExpeditionEventBatchNtf : IMessage
    {
        [ProtoMember(1)] public long RunId { get; set; }
        [ProtoMember(2)] public long EventSequence { get; set; }
        [ProtoMember(3)] public List<ExpeditionEventData> Events { get; set; } =
            new List<ExpeditionEventData>();
    }

    [ProtoContract]
    [Message("Expedition.ResultNtf")]
    public sealed class ExpeditionResultNtf : IMessage
    {
        [ProtoMember(1)] public long RunId { get; set; }
        [ProtoMember(2)] public int Phase { get; set; }
        [ProtoMember(3)] public long ServerTick { get; set; }
        [ProtoMember(4)] public bool SettlementReady { get; set; }
    }

    [ProtoContract]
    public sealed class ExpeditionSnapshotData
    {
        [ProtoMember(1)] public long RunId { get; set; }
        [ProtoMember(2)] public long RunIndex { get; set; }
        [ProtoMember(3)] public long ServerTick { get; set; }
        [ProtoMember(4)] public long LastProcessedInputSequence { get; set; }
        [ProtoMember(5)] public int Phase { get; set; }
        [ProtoMember(6)] public float SquadX { get; set; }
        [ProtoMember(7)] public float SquadY { get; set; }
        [ProtoMember(8)] public int CurrentRoomId { get; set; }
        [ProtoMember(9)] public int TargetKind { get; set; }
        [ProtoMember(10)] public int TargetId { get; set; }
        [ProtoMember(11)] public int Kills { get; set; }
        [ProtoMember(12)] public long CollectedResource { get; set; }
        [ProtoMember(13)] public bool ExitOpen { get; set; }
        [ProtoMember(14)] public List<ExpeditionMemberStateData> Members { get; set; } =
            new List<ExpeditionMemberStateData>();
        [ProtoMember(15)] public List<ExpeditionEnemyStateData> Enemies { get; set; } =
            new List<ExpeditionEnemyStateData>();
        [ProtoMember(16)] public List<ExpeditionResourceStateData> Resources { get; set; } =
            new List<ExpeditionResourceStateData>();
        [ProtoMember(17)] public List<ExpeditionRoomStateData> RevealedRooms { get; set; } =
            new List<ExpeditionRoomStateData>();
        [ProtoMember(18)] public List<ExpeditionEventData> Events { get; set; } =
            new List<ExpeditionEventData>();
        [ProtoMember(19)] public int TicksPerSecond { get; set; }
        [ProtoMember(20)] public float MoveSpeedPerSecond { get; set; }
        [ProtoMember(21)] public float MinimumX { get; set; }
        [ProtoMember(22)] public float MaximumX { get; set; }
        [ProtoMember(23)] public float MinimumY { get; set; }
        [ProtoMember(24)] public float MaximumY { get; set; }
    }

    [ProtoContract]
    public sealed class ExpeditionMemberStateData
    {
        [ProtoMember(1)] public int Id { get; set; }
        [ProtoMember(2)] public int Health { get; set; }
        [ProtoMember(3)] public int MaxHealth { get; set; }
        [ProtoMember(4)] public int AttackCooldownTicksRemaining { get; set; }
    }

    [ProtoContract]
    public sealed class ExpeditionEnemyStateData
    {
        [ProtoMember(1)] public int Id { get; set; }
        [ProtoMember(2)] public int Kind { get; set; }
        [ProtoMember(3)] public float X { get; set; }
        [ProtoMember(4)] public float Y { get; set; }
        [ProtoMember(5)] public int Health { get; set; }
        [ProtoMember(6)] public int MaxHealth { get; set; }
        [ProtoMember(7)] public int ActionCooldownTicksRemaining { get; set; }
        [ProtoMember(8)] public bool WarningActive { get; set; }
        [ProtoMember(9)] public long WarningResolveTick { get; set; }
        [ProtoMember(10)] public float WarningX { get; set; }
        [ProtoMember(11)] public float WarningY { get; set; }
        [ProtoMember(12)] public float WarningRadius { get; set; }
    }

    [ProtoContract]
    public sealed class ExpeditionResourceStateData
    {
        [ProtoMember(1)] public int Id { get; set; }
        [ProtoMember(2)] public float X { get; set; }
        [ProtoMember(3)] public float Y { get; set; }
        [ProtoMember(4)] public int RemainingAmount { get; set; }
        [ProtoMember(5)] public int InitialAmount { get; set; }
        [ProtoMember(6)] public int CollectionCooldownTicksRemaining { get; set; }
    }

    /// <summary>
    /// 只描述服务端已经揭示的房间和出口；协议不包含完整房间图或 Seed。
    /// </summary>
    [ProtoContract]
    public sealed class ExpeditionRoomStateData
    {
        [ProtoMember(1)] public int RoomId { get; set; }
        [ProtoMember(2)] public int Kind { get; set; }
        [ProtoMember(3)] public int State { get; set; }
        [ProtoMember(4)] public bool IsCurrent { get; set; }
        [ProtoMember(5)] public List<int> VisibleConnectedRoomIds { get; set; } = new List<int>();
        [ProtoMember(6)] public List<ExpeditionVisibleExitData> VisibleExits { get; set; } =
            new List<ExpeditionVisibleExitData>();
    }

    [ProtoContract]
    public sealed class ExpeditionVisibleExitData
    {
        [ProtoMember(1)] public int Direction { get; set; }
        [ProtoMember(2)] public int TargetRoomId { get; set; }
        [ProtoMember(3)] public bool IsOpen { get; set; }
        [ProtoMember(4)] public bool HasDangerHint { get; set; }
        [ProtoMember(5)] public float X { get; set; }
        [ProtoMember(6)] public float Y { get; set; }
    }

    [ProtoContract]
    public sealed class ExpeditionEventData
    {
        [ProtoMember(1)] public long Tick { get; set; }
        [ProtoMember(2)] public int Kind { get; set; }
        [ProtoMember(3)] public int SourceKind { get; set; }
        [ProtoMember(4)] public int SourceId { get; set; }
        [ProtoMember(5)] public int TargetKind { get; set; }
        [ProtoMember(6)] public int TargetId { get; set; }
        [ProtoMember(7)] public int Value { get; set; }
        [ProtoMember(8)] public float X { get; set; }
        [ProtoMember(9)] public float Y { get; set; }
    }

    [ProtoContract]
    public sealed class ExpeditionProgressData
    {
        [ProtoMember(1)] public long ResourceBalance { get; set; }
        [ProtoMember(2)] public int FacilityLevel { get; set; }
        [ProtoMember(3)] public long LastStartedRunIndex { get; set; }
        [ProtoMember(4)] public long LastSettledRunId { get; set; }
        [ProtoMember(5)] public long UpgradeCost { get; set; }
        [ProtoMember(6)] public int MaximumFacilityLevel { get; set; }
        [ProtoMember(7)] public int RepresentativeDamage { get; set; }
    }

    [ProtoContract]
    public sealed class ExpeditionSettlementData
    {
        [ProtoMember(1)] public long RunId { get; set; }
        [ProtoMember(2)] public int Phase { get; set; }
        [ProtoMember(3)] public long GrossResource { get; set; }
        [ProtoMember(4)] public long AwardedResource { get; set; }
        [ProtoMember(5)] public long ResourceBalance { get; set; }
        [ProtoMember(6)] public int FacilityLevel { get; set; }
        [ProtoMember(7)] public long EndTick { get; set; }
    }
}
