using System;
using System.Collections.Generic;
using GameplayCore;
using ProductExpedition;
using Xunit;

namespace ProductContent.Tests
{
    public sealed class ProductExpeditionCompletionTests
    {
        [Fact]
        public void DefaultProductSetupCanBeCompletedWithWarningDodges()
        {
            ExpeditionSetup setup = ProductExpeditionFactory.CreateSetup(ProductExpeditionRun.DefaultProductId);
            ExpeditionRun run = ExpeditionRun.Start(setup);
            bool attacked = false;
            bool collected = false;

            for (int tick = 0; tick < 3000 && !run.HasEnded; tick++)
            {
                GetDriveDirection(run, out float directionX, out float directionY);
                List<ExpeditionEvent> events = new List<ExpeditionEvent>();
                ExpeditionSimulation.Advance(run, directionX, directionY, events);
                for (int index = 0; index < events.Count; index++)
                {
                    attacked |= events[index].Kind == ExpeditionEventKind.MemberAttacked;
                    collected |= events[index].Kind == ExpeditionEventKind.ResourceCollected;
                }
            }

            ExpeditionEnemy boss = FindBoss(run);
            Assert.True(
                run.Phase == ExpeditionPhase.Completed,
                $"phase={run.Phase}, tick={run.Tick}, kills={run.Kills}, bossHealth={boss.Health}, " +
                $"memberHealth={run.Members[0].Health}/{run.Members[1].Health}/{run.Members[2].Health}");
            Assert.True(attacked);
            Assert.True(collected);
        }

        private static void GetDriveDirection(ExpeditionRun run, out float directionX, out float directionY)
        {
            ExpeditionEnemy? warningBoss = FindActiveWarningBoss(run);
            if (warningBoss != null)
            {
                NormalizeOrDown(
                    run.SquadX - warningBoss.WarningX,
                    run.SquadY - warningBoss.WarningY,
                    out directionX,
                    out directionY);
                return;
            }

            if (run.TargetKind == ExpeditionTargetKind.Enemy)
            {
                ExpeditionEnemy enemy = FindEnemy(run, run.TargetId);
                MoveToward(run, enemy.X, enemy.Y, 2.5f, out directionX, out directionY);
                return;
            }

            if (run.TargetKind == ExpeditionTargetKind.Resource)
            {
                ExpeditionResource resource = FindResource(run, run.TargetId);
                MoveToward(run, resource.X, resource.Y, 2.5f, out directionX, out directionY);
                return;
            }

            if (run.ExitOpen)
            {
                MoveToward(run, run.ExitX, run.ExitY, 0.0f, out directionX, out directionY);
                return;
            }

            directionX = 0.0f;
            directionY = 1.0f;
        }

        private static void MoveToward(
            ExpeditionRun run,
            float targetX,
            float targetY,
            float stoppingDistance,
            out float directionX,
            out float directionY)
        {
            float deltaX = targetX - run.SquadX;
            float deltaY = targetY - run.SquadY;
            double length = Math.Sqrt((double)deltaX * deltaX + (double)deltaY * deltaY);
            if (length <= stoppingDistance)
            {
                directionX = 0.0f;
                directionY = 0.0f;
                return;
            }

            directionX = (float)(deltaX / length);
            directionY = (float)(deltaY / length);
        }

        private static void NormalizeOrDown(
            float deltaX,
            float deltaY,
            out float directionX,
            out float directionY)
        {
            double lengthSquared = (double)deltaX * deltaX + (double)deltaY * deltaY;
            if (lengthSquared < 0.01d)
            {
                directionX = 0.0f;
                directionY = -1.0f;
                return;
            }

            double length = Math.Sqrt(lengthSquared);
            directionX = (float)(deltaX / length);
            directionY = (float)(deltaY / length);
        }

        private static ExpeditionEnemy FindBoss(ExpeditionRun run)
        {
            for (int index = 0; index < run.Enemies.Count; index++)
            {
                if (run.Enemies[index].Kind == ExpeditionEnemyKind.Boss)
                {
                    return run.Enemies[index];
                }
            }

            throw new InvalidOperationException("默认产品配置缺少 Boss");
        }

        private static ExpeditionEnemy FindEnemy(ExpeditionRun run, int id)
        {
            for (int index = 0; index < run.Enemies.Count; index++)
            {
                if (run.Enemies[index].Id == id)
                {
                    return run.Enemies[index];
                }
            }

            throw new InvalidOperationException($"找不到目标敌人 {id}");
        }

        private static ExpeditionResource FindResource(ExpeditionRun run, int id)
        {
            for (int index = 0; index < run.Resources.Count; index++)
            {
                if (run.Resources[index].Id == id)
                {
                    return run.Resources[index];
                }
            }

            throw new InvalidOperationException($"找不到目标资源 {id}");
        }

        private static ExpeditionEnemy? FindActiveWarningBoss(ExpeditionRun run)
        {
            for (int index = 0; index < run.Enemies.Count; index++)
            {
                ExpeditionEnemy enemy = run.Enemies[index];
                if (enemy.Kind == ExpeditionEnemyKind.Boss && enemy.WarningActive)
                {
                    return enemy;
                }
            }

            return null;
        }
    }
}
