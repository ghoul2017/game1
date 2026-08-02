using GameplayCore;

namespace GameplayCore.Tests
{
    internal static class ArenaTestFactory
    {
        internal const uint Seed = 0x9ABCDEF0U;

        internal static ArenaBattleSetup CreateSetup(
            int maximumTicks = 500,
            int maximumEvents = 512,
            int keyFrameIntervalTicks = 10)
        {
            return new ArenaBattleSetup(
                Seed,
                10,
                maximumTicks,
                maximumEvents,
                keyFrameIntervalTicks,
                CreateAttacker(),
                CreateDefender());
        }

        internal static ArenaSquadSnapshot CreateAttacker()
        {
            return new ArenaSquadSnapshot(
                "attacker-v1",
                1,
                1,
                new[]
                {
                    new ArenaUnitSnapshot(1, 1001, 0, 120, 18, 2),
                    new ArenaUnitSnapshot(2, 1002, 1, 95, 22, 3),
                    new ArenaUnitSnapshot(3, 1003, 2, 105, 16, 2)
                });
        }

        internal static ArenaSquadSnapshot CreateDefender()
        {
            return new ArenaSquadSnapshot(
                "defender-v1",
                1,
                1,
                new[]
                {
                    new ArenaUnitSnapshot(101, 2001, 0, 130, 17, 2),
                    new ArenaUnitSnapshot(102, 2002, 1, 100, 20, 3),
                    new ArenaUnitSnapshot(103, 2003, 2, 90, 15, 2)
                });
        }
    }
}
