using GameplayCore;
using Xunit;

namespace GameplayCore.Tests
{
    /// <summary>
    /// 验证目标获取、保持、威胁打断和稳定编号裁决规则。
    /// </summary>
    public sealed class ExpeditionTargetingTests
    {
        [Fact]
        public void CurrentEnemyRemainsLockedInsideReleaseRadius()
        {
            ExpeditionSetup setup = ExpeditionTestFactory.CreateSetup();
            setup.MoveSpeedPerSecond = 3.0f;
            setup.Members = new[]
            {
                ExpeditionTestFactory.CreateMember(1, attackRange: 0.1f),
                ExpeditionTestFactory.CreateMember(2, attackRange: 0.1f),
                ExpeditionTestFactory.CreateMember(3, attackRange: 0.1f)
            };
            setup.Enemies = new[]
            {
                ExpeditionTestFactory.CreateNormalEnemy(10, 1.0f, attackRange: 0.1f),
                ExpeditionTestFactory.CreateBoss(x: 4.0f, attackRange: 0.1f)
            };
            ExpeditionRun run = ExpeditionRun.Start(setup);

            ExpeditionTestFactory.Advance(run);
            Assert.Equal(10, run.TargetId);

            ExpeditionTestFactory.Advance(run, 1.0f, 0.0f);

            Assert.Equal(ExpeditionTargetKind.Enemy, run.TargetKind);
            Assert.Equal(10, run.TargetId);
        }

        [Fact]
        public void EnemyThreatInterruptsResourceAndResourceIsReacquiredAfterKill()
        {
            ExpeditionSetup setup = ExpeditionTestFactory.CreateSetup();
            setup.MoveSpeedPerSecond = 3.0f;
            setup.TargetAcquireRadius = 2.0f;
            setup.TargetReleaseMargin = 2.0f;
            setup.EnemyThreatRadius = 1.0f;
            setup.Enemies = new[]
            {
                ExpeditionTestFactory.CreateNormalEnemy(10, 3.0f, maxHealth: 3, attackRange: 0.1f),
                ExpeditionTestFactory.CreateBoss()
            };
            setup.Resources = new[]
            {
                ExpeditionTestFactory.CreateResource(x: 1.0f, interactionRange: 0.1f)
            };
            ExpeditionRun run = ExpeditionRun.Start(setup);

            ExpeditionTestFactory.Advance(run);
            Assert.Equal(ExpeditionTargetKind.Resource, run.TargetKind);

            ExpeditionTestFactory.Advance(run, 1.0f, 0.0f);
            Assert.Equal(1, run.Kills);
            Assert.Equal(ExpeditionTargetKind.None, run.TargetKind);

            ExpeditionTestFactory.Advance(run);

            Assert.Equal(ExpeditionTargetKind.Resource, run.TargetKind);
            Assert.Equal(800, run.TargetId);
        }

        [Fact]
        public void EqualDistanceUsesLowestStableEnemyId()
        {
            ExpeditionSetup setup = ExpeditionTestFactory.CreateSetup();
            setup.Members = new[]
            {
                ExpeditionTestFactory.CreateMember(1, attackRange: 0.1f)
            };
            setup.Enemies = new[]
            {
                ExpeditionTestFactory.CreateNormalEnemy(20, 1.0f, attackRange: 0.1f),
                ExpeditionTestFactory.CreateBoss(),
                ExpeditionTestFactory.CreateNormalEnemy(10, -1.0f, attackRange: 0.1f)
            };
            ExpeditionRun run = ExpeditionRun.Start(setup);

            ExpeditionTestFactory.Advance(run);

            Assert.Equal(ExpeditionTargetKind.Enemy, run.TargetKind);
            Assert.Equal(10, run.TargetId);
        }

        [Fact]
        public void TargetIsReleasedOutsideReleaseRadius()
        {
            ExpeditionSetup setup = ExpeditionTestFactory.CreateSetup();
            setup.MoveSpeedPerSecond = 5.0f;
            setup.TargetAcquireRadius = 2.0f;
            setup.TargetReleaseMargin = 1.0f;
            setup.Members = new[]
            {
                ExpeditionTestFactory.CreateMember(1, attackRange: 0.1f)
            };
            setup.Enemies = new[]
            {
                ExpeditionTestFactory.CreateNormalEnemy(10, 1.0f, attackRange: 0.1f),
                ExpeditionTestFactory.CreateBoss()
            };
            ExpeditionRun run = ExpeditionRun.Start(setup);

            ExpeditionTestFactory.Advance(run);
            Assert.Equal(10, run.TargetId);

            ExpeditionTestFactory.Advance(run, 1.0f, 0.0f);

            Assert.Equal(ExpeditionTargetKind.None, run.TargetKind);
            Assert.Equal(0, run.TargetId);
        }
    }
}
