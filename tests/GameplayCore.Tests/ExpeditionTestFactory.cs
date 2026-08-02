using System.Collections.Generic;
using GameplayCore;

namespace GameplayCore.Tests
{
    /// <summary>
    /// 为 GameplayCore 行为测试创建可直接调整的最小开局数据。
    /// </summary>
    internal static class ExpeditionTestFactory
    {
        internal static ExpeditionSetup CreateSetup()
        {
            return new ExpeditionSetup
            {
                TicksPerSecond = 1,
                MoveSpeedPerSecond = 1.0f,
                MinimumX = -100.0f,
                MaximumX = 100.0f,
                MinimumY = -100.0f,
                MaximumY = 100.0f,
                InitialSquadX = 0.0f,
                InitialSquadY = 0.0f,
                TargetAcquireRadius = 5.0f,
                TargetReleaseMargin = 2.0f,
                EnemyThreatRadius = 3.0f,
                ExitX = 90.0f,
                ExitY = 0.0f,
                ExitRadius = 1.0f,
                Members = new[]
                {
                    CreateMember(1),
                    CreateMember(2),
                    CreateMember(3)
                },
                Enemies = new[]
                {
                    CreateBoss()
                },
                Resources = new[]
                {
                    CreateResource()
                }
            };
        }

        internal static ExpeditionMemberSetup CreateMember(
            int id,
            int maxHealth = 100,
            int attackDamage = 1,
            float attackRange = 1.0f,
            int attackIntervalTicks = 1)
        {
            return new ExpeditionMemberSetup
            {
                Id = id,
                MaxHealth = maxHealth,
                AttackDamage = attackDamage,
                AttackRange = attackRange,
                AttackIntervalTicks = attackIntervalTicks
            };
        }

        internal static ExpeditionEnemySetup CreateNormalEnemy(
            int id,
            float x,
            float y = 0.0f,
            int maxHealth = 100,
            int attackDamage = 1,
            float attackRange = 1.0f,
            int attackIntervalTicks = 1)
        {
            return new ExpeditionEnemySetup
            {
                Id = id,
                Kind = ExpeditionEnemyKind.Normal,
                X = x,
                Y = y,
                MaxHealth = maxHealth,
                AttackDamage = attackDamage,
                AttackRange = attackRange,
                ActionIntervalTicks = attackIntervalTicks,
                BossWarningDelayTicks = 0,
                BossWarningRadius = 0.0f
            };
        }

        internal static ExpeditionEnemySetup CreateBoss(
            int id = 900,
            float x = 80.0f,
            float y = 0.0f,
            int maxHealth = 1000,
            int attackDamage = 10,
            float attackRange = 1.0f,
            int actionIntervalTicks = 10,
            int warningDelayTicks = 2,
            float warningRadius = 1.0f)
        {
            return new ExpeditionEnemySetup
            {
                Id = id,
                Kind = ExpeditionEnemyKind.Boss,
                X = x,
                Y = y,
                MaxHealth = maxHealth,
                AttackDamage = attackDamage,
                AttackRange = attackRange,
                ActionIntervalTicks = actionIntervalTicks,
                BossWarningDelayTicks = warningDelayTicks,
                BossWarningRadius = warningRadius
            };
        }

        internal static ExpeditionResourceSetup CreateResource(
            int id = 800,
            float x = 70.0f,
            float y = 0.0f,
            int amount = 10,
            int collectionAmount = 1,
            float interactionRange = 1.0f,
            int collectionIntervalTicks = 1)
        {
            return new ExpeditionResourceSetup
            {
                Id = id,
                X = x,
                Y = y,
                Amount = amount,
                CollectionAmount = collectionAmount,
                InteractionRange = interactionRange,
                CollectionIntervalTicks = collectionIntervalTicks
            };
        }

        internal static List<ExpeditionEvent> Advance(
            ExpeditionRun run,
            float directionX = 0.0f,
            float directionY = 0.0f,
            int interactionTargetId = 0)
        {
            List<ExpeditionEvent> events = new List<ExpeditionEvent>();
            ExpeditionSimulation.Advance(run, directionX, directionY, interactionTargetId, events);
            return events;
        }
    }
}
