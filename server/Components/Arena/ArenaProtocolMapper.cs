using GameplayCore;
using Shared.Common.Protos;

namespace Game.Components.Arena;

public static class ArenaProtocolMapper
{
    public static void FillStartAck(ArenaBattleRecord record, ArenaBattleStartAck response)
    {
        response.BattleId = record.BattleId;
        if (!record.HasFinished)
        {
            response.ErrorMessage = "竞技场战斗尚未完成";
            return;
        }

        if (!string.IsNullOrEmpty(record.ErrorMessage))
        {
            response.ErrorMessage = record.ErrorMessage;
            return;
        }

        response.Outcome = (int)record.Outcome;
        response.EndReason = (int)record.EndReason;
        response.FinalTick = record.FinalTick;
        response.EventHash = record.EventHash;
    }

    public static void FillReplayAck(ArenaBattleRecord record, ArenaReplayAck response)
    {
        response.BattleId = record.BattleId;
        if (!record.HasFinished)
        {
            response.ErrorMessage = "竞技场战斗尚未完成";
            return;
        }

        if (!string.IsNullOrEmpty(record.ErrorMessage))
        {
            response.ErrorMessage = record.ErrorMessage;
            return;
        }

        if (record.Execution == null)
        {
            response.ErrorMessage = "竞技场录像已过期";
            return;
        }

        ArenaReplay replay = record.Execution.Replay;
        response.Seed = replay.Seed;
        response.TicksPerSecond = replay.TicksPerSecond;
        response.RulesVersion = replay.RulesVersion;
        response.ContentVersion = replay.ContentVersion;
        response.AttackerSnapshotId = replay.Attacker.SnapshotId;
        response.DefenderSnapshotId = replay.Defender.SnapshotId;
        response.Outcome = (int)replay.Result.Outcome;
        response.EndReason = (int)replay.Result.EndReason;
        response.FinalTick = replay.Result.FinalTick;
        response.EventHash = replay.ComputeEventHash();
        AddUnits(replay.Attacker, response.AttackerUnits);
        AddUnits(replay.Defender, response.DefenderUnits);
        AddEvents(replay, response.Events);
        AddKeyFrames(replay, response.KeyFrames);
    }

    private static void AddUnits(ArenaSquadSnapshot snapshot, List<ArenaUnitSnapshotInfo> output)
    {
        for (int index = 0; index < snapshot.Units.Count; index++)
        {
            ArenaUnitSnapshot unit = snapshot.Units[index];
            output.Add(new ArenaUnitSnapshotInfo
            {
                UnitId = unit.UnitId,
                TemplateId = unit.TemplateId,
                Slot = unit.Slot,
                MaxHealth = unit.MaxHealth,
                AttackDamage = unit.AttackDamage,
                AttackIntervalTicks = unit.AttackIntervalTicks
            });
        }
    }

    private static void AddEvents(ArenaReplay replay, List<ArenaReplayEventInfo> output)
    {
        for (int index = 0; index < replay.Events.Count; index++)
        {
            ArenaEvent current = replay.Events[index];
            output.Add(new ArenaReplayEventInfo
            {
                Tick = current.Tick,
                Kind = (int)current.Kind,
                SourceSide = (int)current.SourceSide,
                SourceUnitId = current.SourceUnitId,
                TargetSide = (int)current.TargetSide,
                TargetUnitId = current.TargetUnitId,
                Value = current.Value,
                RemainingHealth = current.RemainingHealth
            });
        }
    }

    private static void AddKeyFrames(ArenaReplay replay, List<ArenaReplayKeyFrameInfo> output)
    {
        for (int frameIndex = 0; frameIndex < replay.KeyFrames.Count; frameIndex++)
        {
            ArenaKeyFrame keyFrame = replay.KeyFrames[frameIndex];
            ArenaReplayKeyFrameInfo frameInfo = new ArenaReplayKeyFrameInfo { Tick = keyFrame.Tick };
            for (int unitIndex = 0; unitIndex < keyFrame.Units.Count; unitIndex++)
            {
                ArenaUnitFrame unit = keyFrame.Units[unitIndex];
                frameInfo.Units.Add(new ArenaReplayUnitFrameInfo
                {
                    Side = (int)unit.Side,
                    UnitId = unit.UnitId,
                    Health = unit.Health
                });
            }

            output.Add(frameInfo);
        }
    }
}
