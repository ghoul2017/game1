using System;
using System.Collections.Generic;

namespace GameplayCore
{
    public static class ArenaSimulation
    {
        private const int MaximumEventsPerAttack = 4;

        public static void Advance(ArenaRun run, List<ArenaEvent> events)
        {
            if (run == null)
            {
                throw new ArgumentNullException(nameof(run));
            }

            if (events == null)
            {
                throw new ArgumentNullException(nameof(events));
            }

            if (events.Count > run.MaximumEvents)
            {
                throw new ArgumentException("现有事件数已超过本局上限", nameof(events));
            }

            if (run.HasEnded)
            {
                return;
            }

            run.BeginTick();
            bool attackerFirst = run.NextBoolean();
            if (attackerFirst)
            {
                AdvanceSide(run, ArenaSide.Attacker, events);
                AdvanceSide(run, ArenaSide.Defender, events);
            }
            else
            {
                AdvanceSide(run, ArenaSide.Defender, events);
                AdvanceSide(run, ArenaSide.Attacker, events);
            }

            if (!run.HasEnded && run.Tick >= run.MaximumTicks)
            {
                Finish(run, ArenaBattleOutcome.Draw, ArenaBattleEndReason.TickLimitReached, events);
            }
        }

        private static void AdvanceSide(ArenaRun run, ArenaSide side, List<ArenaEvent> events)
        {
            if (run.HasEnded)
            {
                return;
            }

            ArenaUnitState[] actingUnits = side == ArenaSide.Attacker
                ? run.MutableAttackerUnits
                : run.MutableDefenderUnits;
            ArenaUnitState[] targetUnits = side == ArenaSide.Attacker
                ? run.MutableDefenderUnits
                : run.MutableAttackerUnits;
            ArenaSide targetSide = side == ArenaSide.Attacker ? ArenaSide.Defender : ArenaSide.Attacker;

            for (int index = 0; index < actingUnits.Length; index++)
            {
                ArenaUnitState actingUnit = actingUnits[index];
                if (!actingUnit.CanAttack)
                {
                    continue;
                }

                ArenaUnitState target = FindTarget(targetUnits);
                if (target == null)
                {
                    FinishForElimination(run, targetSide, events);
                    return;
                }

                if (events.Count > run.MaximumEvents - MaximumEventsPerAttack)
                {
                    Finish(run, ArenaBattleOutcome.Draw, ArenaBattleEndReason.EventLimitReached, events);
                    return;
                }

                actingUnit.StartAttack();
                events.Add(new ArenaEvent(
                    run.Tick,
                    ArenaEventKind.UnitAttacked,
                    side,
                    actingUnit.UnitId,
                    targetSide,
                    target.UnitId,
                    actingUnit.AttackDamage,
                    target.Health));

                int damagePercent = run.NextDamagePercent();
                int requestedDamage = Math.Max(1, actingUnit.AttackDamage * damagePercent / 100);
                int actualDamage = target.TakeDamage(requestedDamage);
                events.Add(new ArenaEvent(
                    run.Tick,
                    ArenaEventKind.UnitDamaged,
                    side,
                    actingUnit.UnitId,
                    targetSide,
                    target.UnitId,
                    actualDamage,
                    target.Health));

                if (!target.IsAlive)
                {
                    events.Add(new ArenaEvent(
                        run.Tick,
                        ArenaEventKind.UnitDefeated,
                        side,
                        actingUnit.UnitId,
                        targetSide,
                        target.UnitId,
                        0,
                        0));
                }

                if (!run.HasLivingUnit(targetSide))
                {
                    FinishForElimination(run, targetSide, events);
                    return;
                }
            }
        }

        private static ArenaUnitState FindTarget(ArenaUnitState[] units)
        {
            for (int index = 0; index < units.Length; index++)
            {
                if (units[index].IsAlive)
                {
                    return units[index];
                }
            }

            return null;
        }

        private static void FinishForElimination(
            ArenaRun run,
            ArenaSide eliminatedSide,
            List<ArenaEvent> events)
        {
            if (eliminatedSide == ArenaSide.Defender)
            {
                Finish(
                    run,
                    ArenaBattleOutcome.AttackerVictory,
                    ArenaBattleEndReason.DefenderEliminated,
                    events);
                return;
            }

            Finish(
                run,
                ArenaBattleOutcome.DefenderVictory,
                ArenaBattleEndReason.AttackerEliminated,
                events);
        }

        private static void Finish(
            ArenaRun run,
            ArenaBattleOutcome outcome,
            ArenaBattleEndReason endReason,
            List<ArenaEvent> events)
        {
            run.Finish(outcome, endReason);
            if (events.Count >= run.MaximumEvents)
            {
                throw new InvalidOperationException("竞技场为终态事件预留的容量已耗尽");
            }

            events.Add(new ArenaEvent(
                run.Tick,
                ArenaEventKind.BattleEnded,
                ArenaSide.None,
                0,
                ArenaSide.None,
                0,
                (int)outcome,
                (int)endReason));
        }
    }
}
