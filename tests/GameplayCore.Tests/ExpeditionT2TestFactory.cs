using System;
using System.Collections.Generic;
using GameplayCore;

namespace GameplayCore.Tests
{
    internal static class ExpeditionT2TestFactory
    {
        internal const int UpgradeCost = 30;

        internal static ExpeditionSetup CreateTemplate(
            int memberHealth = 100,
            int memberDamage = 20,
            float memberRange = 10.0f,
            int normalHealth = 20,
            int normalDamage = 1,
            float normalRange = 0.1f,
            int bossHealth = 20,
            int bossDamage = 1)
        {
            return new ExpeditionSetup
            {
                TicksPerSecond = 1,
                MoveSpeedPerSecond = 2.0f,
                MinimumX = -2.0f,
                MaximumX = 2.0f,
                MinimumY = -2.0f,
                MaximumY = 2.0f,
                InitialSquadX = 0.0f,
                InitialSquadY = 0.0f,
                TargetAcquireRadius = 10.0f,
                TargetReleaseMargin = 1.0f,
                EnemyThreatRadius = 10.0f,
                ExitX = 0.0f,
                ExitY = 1.0f,
                ExitRadius = 0.5f,
                Members = new[]
                {
                    ExpeditionTestFactory.CreateMember(1, memberHealth, memberDamage, memberRange),
                    ExpeditionTestFactory.CreateMember(2, memberHealth, memberDamage, memberRange),
                    ExpeditionTestFactory.CreateMember(3, memberHealth, memberDamage, memberRange)
                },
                Enemies = new[]
                {
                    ExpeditionTestFactory.CreateNormalEnemy(
                        100,
                        0.0f,
                        maxHealth: normalHealth,
                        attackDamage: normalDamage,
                        attackRange: normalRange),
                    ExpeditionTestFactory.CreateBoss(
                        x: 0.0f,
                        y: 0.0f,
                        maxHealth: bossHealth,
                        attackDamage: bossDamage,
                        attackRange: 10.0f,
                        actionIntervalTicks: 1,
                        warningDelayTicks: 1,
                        warningRadius: 10.0f)
                },
                Resources = new[]
                {
                    ExpeditionTestFactory.CreateResource(
                        x: 0.0f,
                        y: 0.0f,
                        amount: 10,
                        collectionAmount: 10,
                        interactionRange: 10.0f)
                }
            };
        }

        internal static ExpeditionCampaignDefinition CreateCampaignDefinition(
            uint campaignSeed = 1234u,
            int roomCount = 8,
            int failureRetentionPercent = 50,
            ExpeditionSetup? template = null)
        {
            return new ExpeditionCampaignDefinition(
                campaignSeed,
                roomCount,
                UpgradeCost,
                failureRetentionPercent,
                20,
                template ?? CreateTemplate());
        }

        internal static ExpeditionRun CompleteRun(ExpeditionRun run)
        {
            List<ExpeditionEvent> events = new List<ExpeditionEvent>();
            for (int tick = 0; tick < 100 && !run.HasEnded; tick++)
            {
                events.Clear();
                ExpeditionSimulation.Advance(run, 0.0f, 1.0f, events);
            }

            if (!run.HasEnded)
            {
                throw new InvalidOperationException("测试远征未在预期 Tick 内结束");
            }

            return run;
        }

        internal static List<ExpeditionEvent> MoveThroughExit(
            ExpeditionRun run,
            ExpeditionDoorDirection direction)
        {
            int originalRoomId = run.CurrentRoomId;
            List<ExpeditionEvent> result = new List<ExpeditionEvent>();
            for (int tick = 0; tick < 10 && run.CurrentRoomId == originalRoomId && !run.HasEnded; tick++)
            {
                (float x, float y) = GetDirection(direction);
                List<ExpeditionEvent> current = new List<ExpeditionEvent>();
                ExpeditionSimulation.Advance(run, x, y, current);
                result.AddRange(current);
            }

            return result;
        }

        internal static ExpeditionVisibleExit FindExit(ExpeditionRun run, ExpeditionDoorDirection direction)
        {
            for (int index = 0; index < run.VisibleExits.Count; index++)
            {
                if (run.VisibleExits[index].Direction == direction)
                {
                    return run.VisibleExits[index];
                }
            }

            throw new InvalidOperationException($"当前房间没有 {direction} 出口");
        }

        internal static ExpeditionSetup CreateRunSetup(
            ExpeditionSetup template,
            ExpeditionRoomGraph graph,
            long runId)
        {
            return new ExpeditionSetup
            {
                RunId = runId,
                Seed = graph.Seed,
                RoomGraph = graph,
                TicksPerSecond = template.TicksPerSecond,
                MoveSpeedPerSecond = template.MoveSpeedPerSecond,
                MinimumX = template.MinimumX,
                MaximumX = template.MaximumX,
                MinimumY = template.MinimumY,
                MaximumY = template.MaximumY,
                InitialSquadX = template.InitialSquadX,
                InitialSquadY = template.InitialSquadY,
                TargetAcquireRadius = template.TargetAcquireRadius,
                TargetReleaseMargin = template.TargetReleaseMargin,
                EnemyThreatRadius = template.EnemyThreatRadius,
                ExitX = template.ExitX,
                ExitY = template.ExitY,
                ExitRadius = template.ExitRadius,
                Members = template.Members,
                Enemies = template.Enemies,
                Resources = template.Resources
            };
        }

        private static (float X, float Y) GetDirection(ExpeditionDoorDirection direction)
        {
            switch (direction)
            {
                case ExpeditionDoorDirection.North:
                    return (0.0f, 1.0f);
                case ExpeditionDoorDirection.East:
                    return (1.0f, 0.0f);
                case ExpeditionDoorDirection.South:
                    return (0.0f, -1.0f);
                case ExpeditionDoorDirection.West:
                    return (-1.0f, 0.0f);
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction));
            }
        }
    }
}
