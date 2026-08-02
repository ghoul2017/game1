using ProductContent;
using Xunit;

namespace ProductContent.Tests
{
    public sealed class ProductIsolationTests
    {
        [Fact]
        public void RepeatedCreationReturnsIndependentObjectGraphs()
        {
            ProductProfile first = ProductCatalog.Create(ProductCatalog.LabyrinthProductId);
            ProductProfile second = ProductCatalog.Create(ProductCatalog.LabyrinthProductId);

            Assert.NotSame(first, second);
            Assert.NotSame(first.Rules, second.Rules);
            Assert.NotSame(first.Theme, second.Theme);
            Assert.NotSame(first.Content, second.Content);
            Assert.NotSame(first.Content.SquadUnits[0], second.Content.SquadUnits[0]);
            Assert.Equal(first.Content.SquadUnits[0].MaxHealth, second.Content.SquadUnits[0].MaxHealth);
        }

        [Fact]
        public void SwitchingFirstSecondFirstDoesNotLeakSecondProductRule()
        {
            ProductProfile firstBefore = ProductCatalog.Create(ProductCatalog.LabyrinthProductId);
            ProductProfile second = ProductCatalog.Create(ProductCatalog.CosmicLegionPlaceholderProductId);
            ProductProfile firstAfter = ProductCatalog.Create(ProductCatalog.LabyrinthProductId);

            Assert.Equal(0, firstBefore.Rules.MaximumRunSeconds);
            Assert.Equal(180, second.Rules.MaximumRunSeconds);
            Assert.Equal(0, firstAfter.Rules.MaximumRunSeconds);
            Assert.False(firstAfter.Rules.HasTimeLimit);
            Assert.Equal(firstBefore.Content.ContentId, firstAfter.Content.ContentId);
        }
    }
}
