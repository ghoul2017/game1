using System;
using GameplayCore;

namespace Client.Expedition
{
    /// <summary>
    /// 集中创建 T1 灰盒唯一一套可调开局数据。
    /// </summary>
    public static class T1ExpeditionSetupFactory
    {
        public const int DefaultTicksPerSecond = 15;

        public static ExpeditionSetup Create(int ticksPerSecond)
        {
            return new ExpeditionSetup
            {
                TicksPerSecond = ticksPerSecond,
                MoveSpeedPerSecond = 4.5f,
                MinimumX = -10.0f,
                MaximumX = 10.0f,
                MinimumY = -25.0f,
                MaximumY = 28.0f,
                InitialSquadX = 0.0f,
                InitialSquadY = -22.0f,
                TargetAcquireRadius = 4.0f,
                TargetReleaseMargin = 1.5f,
                EnemyThreatRadius = 3.5f,
                ExitX = 0.0f,
                ExitY = 24.0f,
                ExitRadius = 2.0f,
                Members = CreateMembers(ticksPerSecond),
                Enemies = CreateEnemies(ticksPerSecond),
                Resources = CreateResources(ticksPerSecond)
            };
        }

        private static ExpeditionMemberSetup[] CreateMembers(int ticksPerSecond)
        {
            return new[]
            {
                new ExpeditionMemberSetup
                {
                    Id = 1,
                    MaxHealth = 100,
                    AttackDamage = 8,
                    AttackRange = 3.0f,
                    AttackIntervalTicks = Math.Max(1, ticksPerSecond * 4 / 5)
                },
                new ExpeditionMemberSetup
                {
                    Id = 2,
                    MaxHealth = 80,
                    AttackDamage = 6,
                    AttackRange = 3.0f,
                    AttackIntervalTicks = Math.Max(1, ticksPerSecond * 4 / 5)
                },
                new ExpeditionMemberSetup
                {
                    Id = 3,
                    MaxHealth = 80,
                    AttackDamage = 6,
                    AttackRange = 3.0f,
                    AttackIntervalTicks = Math.Max(1, ticksPerSecond * 4 / 5)
                }
            };
        }

        private static ExpeditionEnemySetup[] CreateEnemies(int ticksPerSecond)
        {
            return new[]
            {
                CreateNormalEnemy(101, -1.5f, -14.0f, ticksPerSecond),
                CreateNormalEnemy(102, 1.5f, -12.5f, ticksPerSecond),
                CreateNormalEnemy(103, 0.0f, -10.5f, ticksPerSecond),
                new ExpeditionEnemySetup
                {
                    Id = 900,
                    Kind = ExpeditionEnemyKind.Boss,
                    X = 0.0f,
                    Y = 13.0f,
                    MaxHealth = 1500,
                    AttackDamage = 20,
                    AttackRange = 5.0f,
                    ActionIntervalTicks = ticksPerSecond * 3,
                    BossWarningDelayTicks = ticksPerSecond,
                    BossWarningRadius = 2.2f
                }
            };
        }

        private static ExpeditionEnemySetup CreateNormalEnemy(int id, float x, float y, int ticksPerSecond)
        {
            return new ExpeditionEnemySetup
            {
                Id = id,
                Kind = ExpeditionEnemyKind.Normal,
                X = x,
                Y = y,
                MaxHealth = 50,
                AttackDamage = 4,
                AttackRange = 2.5f,
                ActionIntervalTicks = Math.Max(1, ticksPerSecond * 6 / 5),
                BossWarningDelayTicks = 0,
                BossWarningRadius = 0.0f
            };
        }

        private static ExpeditionResourceSetup[] CreateResources(int ticksPerSecond)
        {
            return new[]
            {
                new ExpeditionResourceSetup
                {
                    Id = 201,
                    X = 0.0f,
                    Y = -2.0f,
                    Amount = 30,
                    CollectionAmount = 5,
                    InteractionRange = 3.0f,
                    CollectionIntervalTicks = Math.Max(1, ticksPerSecond * 3 / 5)
                }
            };
        }
    }
}
