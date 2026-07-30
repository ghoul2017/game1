using System;
using System.Collections.Generic;

namespace GameplayCore
{
    /// <summary>
    /// 以固定顺序推进一局远征的移动、目标、行动、Boss 警告和终态。
    /// </summary>
    public static class ExpeditionSimulation
    {
        public static void Advance(
            ExpeditionRun run,
            float directionX,
            float directionY,
            List<ExpeditionEvent> events)
        {
            if (run == null)
            {
                throw new ArgumentNullException(nameof(run));
            }

            if (events == null)
            {
                throw new ArgumentNullException(nameof(events));
            }

            if (!IsFinite(directionX) || !IsFinite(directionY))
            {
                throw new ArgumentOutOfRangeException(nameof(directionX), "移动方向必须是有限数值");
            }

            if (run.HasEnded)
            {
                return;
            }

            NormalizeDirection(ref directionX, ref directionY);
            run.Move(directionX * run.MovePerTick, directionY * run.MovePerTick);
            AdvanceCooldowns(run);
            AdvanceBossWarnings(run, events);
            RefreshTarget(run, events);
            AdvanceSquadAction(run, events);
            AdvanceNormalEnemies(run, events);
            OpenExit(run, events);
            FinishRun(run, events);
        }

        private static void NormalizeDirection(ref float directionX, ref float directionY)
        {
            double lengthSquared = (double)directionX * directionX + (double)directionY * directionY;
            if (lengthSquared <= 1.0d)
            {
                return;
            }

            double inverseLength = 1.0d / Math.Sqrt(lengthSquared);
            directionX = (float)(directionX * inverseLength);
            directionY = (float)(directionY * inverseLength);
        }

        private static void AdvanceCooldowns(ExpeditionRun run)
        {
            ExpeditionMember[] members = run.MutableMembers;
            for (int index = 0; index < members.Length; index++)
            {
                members[index].AdvanceCooldown();
            }

            ExpeditionEnemy[] enemies = run.MutableEnemies;
            for (int index = 0; index < enemies.Length; index++)
            {
                enemies[index].AdvanceCooldown();
            }

            ExpeditionResource[] resources = run.MutableResources;
            for (int index = 0; index < resources.Length; index++)
            {
                resources[index].AdvanceCooldown();
            }
        }

        private static void AdvanceBossWarnings(ExpeditionRun run, List<ExpeditionEvent> events)
        {
            ExpeditionEnemy[] enemies = run.MutableEnemies;
            for (int index = 0; index < enemies.Length; index++)
            {
                ExpeditionEnemy boss = enemies[index];
                if (boss.Kind != ExpeditionEnemyKind.Boss || !boss.IsAlive)
                {
                    continue;
                }

                bool resolved = false;
                if (boss.WarningActive && run.Tick == boss.WarningResolveTick)
                {
                    ResolveBossWarning(run, boss, events);
                    resolved = true;
                }

                if (resolved || !boss.CanAct || !run.HasLivingMember())
                {
                    continue;
                }

                if (!IsWithin(run.SquadX, run.SquadY, boss.X, boss.Y, boss.AttackRange))
                {
                    continue;
                }

                boss.StartWarning(run.Tick + boss.BossWarningDelayTicks, run.SquadX, run.SquadY);
                events.Add(new ExpeditionEvent(
                    run.Tick, ExpeditionEventKind.BossWarningStarted,
                    ExpeditionObjectKind.Enemy, boss.Id,
                    ExpeditionObjectKind.None, 0,
                    boss.BossWarningDelayTicks, boss.WarningX, boss.WarningY));
            }
        }

        private static void ResolveBossWarning(
            ExpeditionRun run,
            ExpeditionEnemy boss,
            List<ExpeditionEvent> events)
        {
            float warningX = boss.WarningX;
            float warningY = boss.WarningY;
            bool hit = IsWithin(run.SquadX, run.SquadY, warningX, warningY, boss.BossWarningRadius);
            boss.ResolveWarning();

            if (!hit)
            {
                events.Add(new ExpeditionEvent(
                    run.Tick, ExpeditionEventKind.BossWarningDodged,
                    ExpeditionObjectKind.Enemy, boss.Id,
                    ExpeditionObjectKind.None, 0,
                    0, warningX, warningY));
                return;
            }

            events.Add(new ExpeditionEvent(
                run.Tick, ExpeditionEventKind.BossWarningHit,
                ExpeditionObjectKind.Enemy, boss.Id,
                ExpeditionObjectKind.None, 0,
                boss.AttackDamage, warningX, warningY));

            ExpeditionMember[] members = run.MutableMembers;
            for (int index = 0; index < members.Length; index++)
            {
                DamageMember(run, boss, members[index], events);
            }
        }

        private static void RefreshTarget(ExpeditionRun run, List<ExpeditionEvent> events)
        {
            ExpeditionTargetKind nextKind = run.TargetKind;
            int nextId = run.TargetId;

            if (!CurrentTargetIsValid(run))
            {
                nextKind = ExpeditionTargetKind.None;
                nextId = 0;
            }

            if (nextKind == ExpeditionTargetKind.Resource)
            {
                ExpeditionEnemy threat = FindBestEnemy(run, run.EnemyThreatRadius);
                if (threat != null)
                {
                    nextKind = ExpeditionTargetKind.Enemy;
                    nextId = threat.Id;
                }
            }

            if (nextKind == ExpeditionTargetKind.None)
            {
                ExpeditionEnemy enemy = FindBestEnemy(run, run.TargetAcquireRadius);
                if (enemy != null)
                {
                    nextKind = ExpeditionTargetKind.Enemy;
                    nextId = enemy.Id;
                }
                else
                {
                    ExpeditionResource resource = FindBestResource(run, run.TargetAcquireRadius);
                    if (resource != null)
                    {
                        nextKind = ExpeditionTargetKind.Resource;
                        nextId = resource.Id;
                    }
                }
            }

            ChangeTarget(run, nextKind, nextId, events);
        }

        private static bool CurrentTargetIsValid(ExpeditionRun run)
        {
            if (run.TargetKind == ExpeditionTargetKind.Enemy)
            {
                ExpeditionEnemy enemy = run.FindEnemy(run.TargetId);
                return enemy.IsAlive &&
                    IsWithin(run.SquadX, run.SquadY, enemy.X, enemy.Y, run.TargetReleaseRadius);
            }

            if (run.TargetKind == ExpeditionTargetKind.Resource)
            {
                ExpeditionResource resource = run.FindResource(run.TargetId);
                return !resource.IsDepleted &&
                    IsWithin(run.SquadX, run.SquadY, resource.X, resource.Y, run.TargetReleaseRadius);
            }

            return false;
        }

        private static ExpeditionEnemy FindBestEnemy(ExpeditionRun run, float radius)
        {
            ExpeditionEnemy best = null;
            double bestDistance = double.MaxValue;
            double maximumDistance = (double)radius * radius;
            ExpeditionEnemy[] enemies = run.MutableEnemies;

            for (int index = 0; index < enemies.Length; index++)
            {
                ExpeditionEnemy candidate = enemies[index];
                if (!candidate.IsAlive)
                {
                    continue;
                }

                double distance = DistanceSquared(run.SquadX, run.SquadY, candidate.X, candidate.Y);
                if (distance > maximumDistance)
                {
                    continue;
                }

                if (best == null || distance < bestDistance || distance == bestDistance && candidate.Id < best.Id)
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private static ExpeditionResource FindBestResource(ExpeditionRun run, float radius)
        {
            ExpeditionResource best = null;
            double bestDistance = double.MaxValue;
            double maximumDistance = (double)radius * radius;
            ExpeditionResource[] resources = run.MutableResources;

            for (int index = 0; index < resources.Length; index++)
            {
                ExpeditionResource candidate = resources[index];
                if (candidate.IsDepleted)
                {
                    continue;
                }

                double distance = DistanceSquared(run.SquadX, run.SquadY, candidate.X, candidate.Y);
                if (distance > maximumDistance)
                {
                    continue;
                }

                if (best == null || distance < bestDistance || distance == bestDistance && candidate.Id < best.Id)
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private static void AdvanceSquadAction(ExpeditionRun run, List<ExpeditionEvent> events)
        {
            if (run.TargetKind == ExpeditionTargetKind.Enemy)
            {
                AdvanceSquadAttacks(run, run.FindEnemy(run.TargetId), events);
            }
            else if (run.TargetKind == ExpeditionTargetKind.Resource)
            {
                AdvanceCollection(run, run.FindResource(run.TargetId), events);
            }
        }

        private static void AdvanceSquadAttacks(
            ExpeditionRun run,
            ExpeditionEnemy enemy,
            List<ExpeditionEvent> events)
        {
            ExpeditionMember[] members = run.MutableMembers;
            for (int index = 0; index < members.Length && enemy.IsAlive; index++)
            {
                ExpeditionMember member = members[index];
                if (!member.CanAttack ||
                    !IsWithin(run.SquadX, run.SquadY, enemy.X, enemy.Y, member.AttackRange))
                {
                    continue;
                }

                member.StartAttack();
                events.Add(new ExpeditionEvent(
                    run.Tick, ExpeditionEventKind.MemberAttacked,
                    ExpeditionObjectKind.Member, member.Id,
                    ExpeditionObjectKind.Enemy, enemy.Id,
                    member.AttackDamage, enemy.X, enemy.Y));

                int damage = enemy.TakeDamage(member.AttackDamage);
                events.Add(new ExpeditionEvent(
                    run.Tick, ExpeditionEventKind.EnemyDamaged,
                    ExpeditionObjectKind.Member, member.Id,
                    ExpeditionObjectKind.Enemy, enemy.Id,
                    damage, enemy.X, enemy.Y));

                if (!enemy.IsAlive)
                {
                    run.AddKill();
                    events.Add(new ExpeditionEvent(
                        run.Tick, ExpeditionEventKind.EnemyDied,
                        ExpeditionObjectKind.Member, member.Id,
                        ExpeditionObjectKind.Enemy, enemy.Id,
                        0, enemy.X, enemy.Y));
                    ChangeTarget(run, ExpeditionTargetKind.None, 0, events);
                }
            }
        }

        private static void AdvanceCollection(
            ExpeditionRun run,
            ExpeditionResource resource,
            List<ExpeditionEvent> events)
        {
            if (!resource.CanCollect ||
                !IsWithin(run.SquadX, run.SquadY, resource.X, resource.Y, resource.InteractionRange))
            {
                return;
            }

            int amount = resource.Collect();
            run.AddResource(amount);
            events.Add(new ExpeditionEvent(
                run.Tick, ExpeditionEventKind.ResourceCollected,
                ExpeditionObjectKind.None, 0,
                ExpeditionObjectKind.Resource, resource.Id,
                amount, resource.X, resource.Y));

            if (resource.IsDepleted)
            {
                events.Add(new ExpeditionEvent(
                    run.Tick, ExpeditionEventKind.ResourceDepleted,
                    ExpeditionObjectKind.None, 0,
                    ExpeditionObjectKind.Resource, resource.Id,
                    0, resource.X, resource.Y));
                ChangeTarget(run, ExpeditionTargetKind.None, 0, events);
            }
        }

        private static void AdvanceNormalEnemies(ExpeditionRun run, List<ExpeditionEvent> events)
        {
            ExpeditionEnemy[] enemies = run.MutableEnemies;
            for (int index = 0; index < enemies.Length; index++)
            {
                ExpeditionEnemy enemy = enemies[index];
                if (enemy.Kind != ExpeditionEnemyKind.Normal || !enemy.CanAct ||
                    !IsWithin(run.SquadX, run.SquadY, enemy.X, enemy.Y, enemy.AttackRange))
                {
                    continue;
                }

                ExpeditionMember member = SelectMember(run, enemy);
                if (member == null)
                {
                    break;
                }

                enemy.StartNormalAttack();
                events.Add(new ExpeditionEvent(
                    run.Tick, ExpeditionEventKind.EnemyAttacked,
                    ExpeditionObjectKind.Enemy, enemy.Id,
                    ExpeditionObjectKind.Member, member.Id,
                    enemy.AttackDamage, run.SquadX, run.SquadY));
                DamageMember(run, enemy, member, events);
            }
        }

        private static ExpeditionMember SelectMember(ExpeditionRun run, ExpeditionEnemy enemy)
        {
            ExpeditionMember[] members = run.MutableMembers;
            int livingCount = 0;
            for (int index = 0; index < members.Length; index++)
            {
                if (members[index].IsAlive)
                {
                    livingCount++;
                }
            }

            if (livingCount == 0)
            {
                return null;
            }

            long targetOffset = (enemy.Id - 1L + enemy.NormalAttackCount) % livingCount;
            for (int index = 0; index < members.Length; index++)
            {
                if (!members[index].IsAlive)
                {
                    continue;
                }

                if (targetOffset == 0L)
                {
                    return members[index];
                }

                targetOffset--;
            }

            return null;
        }

        private static void DamageMember(
            ExpeditionRun run,
            ExpeditionEnemy enemy,
            ExpeditionMember member,
            List<ExpeditionEvent> events)
        {
            if (!member.IsAlive)
            {
                return;
            }

            int damage = member.TakeDamage(enemy.AttackDamage);
            events.Add(new ExpeditionEvent(
                run.Tick, ExpeditionEventKind.MemberDamaged,
                ExpeditionObjectKind.Enemy, enemy.Id,
                ExpeditionObjectKind.Member, member.Id,
                damage, run.SquadX, run.SquadY));

            if (!member.IsAlive)
            {
                events.Add(new ExpeditionEvent(
                    run.Tick, ExpeditionEventKind.MemberDied,
                    ExpeditionObjectKind.Enemy, enemy.Id,
                    ExpeditionObjectKind.Member, member.Id,
                    0, run.SquadX, run.SquadY));
            }
        }

        private static void OpenExit(ExpeditionRun run, List<ExpeditionEvent> events)
        {
            if (run.ExitOpen || run.HasLivingBoss())
            {
                return;
            }

            run.OpenExit();
            events.Add(new ExpeditionEvent(
                run.Tick, ExpeditionEventKind.ExitOpened,
                ExpeditionObjectKind.None, 0,
                ExpeditionObjectKind.None, 0,
                0, run.ExitX, run.ExitY));
        }

        private static void FinishRun(ExpeditionRun run, List<ExpeditionEvent> events)
        {
            if (!run.HasLivingMember())
            {
                run.Finish(ExpeditionPhase.Failed);
                events.Add(new ExpeditionEvent(
                    run.Tick, ExpeditionEventKind.Failed,
                    ExpeditionObjectKind.None, 0,
                    ExpeditionObjectKind.None, 0,
                    0, run.SquadX, run.SquadY));
                return;
            }

            if (!run.ExitOpen || !IsWithin(run.SquadX, run.SquadY, run.ExitX, run.ExitY, run.ExitRadius))
            {
                return;
            }

            run.Finish(ExpeditionPhase.Completed);
            events.Add(new ExpeditionEvent(
                run.Tick, ExpeditionEventKind.Completed,
                ExpeditionObjectKind.None, 0,
                ExpeditionObjectKind.None, 0,
                0, run.ExitX, run.ExitY));
        }

        private static void ChangeTarget(
            ExpeditionRun run,
            ExpeditionTargetKind kind,
            int id,
            List<ExpeditionEvent> events)
        {
            if (run.TargetKind == kind && run.TargetId == id)
            {
                return;
            }

            run.SetTarget(kind, id);
            float x = run.SquadX;
            float y = run.SquadY;
            ExpeditionObjectKind objectKind = ExpeditionObjectKind.None;

            if (kind == ExpeditionTargetKind.Enemy)
            {
                ExpeditionEnemy enemy = run.FindEnemy(id);
                objectKind = ExpeditionObjectKind.Enemy;
                x = enemy.X;
                y = enemy.Y;
            }
            else if (kind == ExpeditionTargetKind.Resource)
            {
                ExpeditionResource resource = run.FindResource(id);
                objectKind = ExpeditionObjectKind.Resource;
                x = resource.X;
                y = resource.Y;
            }

            events.Add(new ExpeditionEvent(
                run.Tick, ExpeditionEventKind.TargetChanged,
                ExpeditionObjectKind.None, 0,
                objectKind, id,
                0, x, y));
        }

        private static bool IsWithin(float firstX, float firstY, float secondX, float secondY, float radius)
        {
            return DistanceSquared(firstX, firstY, secondX, secondY) <= (double)radius * radius;
        }

        private static double DistanceSquared(float firstX, float firstY, float secondX, float secondY)
        {
            double deltaX = (double)firstX - secondX;
            double deltaY = (double)firstY - secondY;
            return deltaX * deltaX + deltaY * deltaY;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
