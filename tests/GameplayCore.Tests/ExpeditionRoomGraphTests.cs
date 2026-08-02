using System.Collections.Generic;
using GameplayCore;
using Xunit;

namespace GameplayCore.Tests
{
    public sealed class ExpeditionRoomGraphTests
    {
        [Theory]
        [InlineData(1u, 6, 13457797925067893868UL)]
        [InlineData(20260730u, 8, 10143469041349558715UL)]
        [InlineData(uint.MaxValue, 10, 10933509969111741512UL)]
        public void GoldenSeedsProduceFrozenCompleteGraphHashes(
            uint seed,
            int roomCount,
            ulong expectedHash)
        {
            ExpeditionSetup template = ExpeditionT2TestFactory.CreateTemplate();
            ExpeditionRoomGraph first = ExpeditionRoomGraphGenerator.Generate(
                seed,
                roomCount,
                ExpeditionT2TestFactory.UpgradeCost,
                template);
            ExpeditionRoomGraph second = ExpeditionRoomGraphGenerator.Generate(
                seed,
                roomCount,
                ExpeditionT2TestFactory.UpgradeCost,
                template);

            Assert.Equal(expectedHash, first.GetSignatureHash());
            Assert.Equal(first.GetSignature(), second.GetSignature());
        }

        [Fact]
        public void FixedSeedCorpusPreservesGraphAndIdInvariants()
        {
            ExpeditionSetup template = ExpeditionT2TestFactory.CreateTemplate();
            for (uint seed = 0u; seed < 256u; seed++)
            {
                int roomCount = 6 + (int)(seed % 5u);
                ExpeditionRoomGraph graph = ExpeditionRoomGraphGenerator.Generate(
                    seed,
                    roomCount,
                    ExpeditionT2TestFactory.UpgradeCost,
                    template);
                HashSet<int> ids = new HashSet<int>();
                int optionalCount = 0;
                int bossCount = 0;
                int exitCount = 0;

                Assert.Equal(roomCount, graph.Rooms.Count);
                for (int index = 0; index < graph.Rooms.Count; index++)
                {
                    ExpeditionRoomDefinition room = graph.Rooms[index];
                    Assert.True(ids.Add(room.Id));
                    optionalCount += room.IsOptional ? 1 : 0;
                    bossCount += room.Kind == ExpeditionRoomKind.Boss ? 1 : 0;
                    exitCount += room.Kind == ExpeditionRoomKind.Exit ? 1 : 0;
                    Assert.DoesNotContain(room.Exits, exit => exit.TargetRoomId == room.Id);
                }

                Assert.InRange(optionalCount, 1, 2);
                Assert.Equal(1, bossCount);
                Assert.Equal(1, exitCount);
                Assert.NotEqual(0UL, graph.GetSignatureHash());
            }
        }

        [Fact]
        public void GeneratedGraphDoesNotRetainMutableTemplateValues()
        {
            ExpeditionSetup template = ExpeditionT2TestFactory.CreateTemplate();
            ExpeditionRoomGraph graph = ExpeditionRoomGraphGenerator.Generate(
                88u,
                8,
                ExpeditionT2TestFactory.UpgradeCost,
                template);
            string signature = graph.GetSignature();

            template.Enemies[0].MaxHealth = 999999;
            template.Resources[0].Amount = 999999;

            Assert.Equal(signature, graph.GetSignature());
        }
    }
}
