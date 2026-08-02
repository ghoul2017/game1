using System;
using ProductContent;
using Xunit;

namespace ProductContent.Tests
{
    public sealed class ProductProfileTests
    {
        [Fact]
        public void CatalogContainsExactlyTwoValidPlaceholderProfiles()
        {
            ProductProfile labyrinth = ProductCatalog.Create(ProductCatalog.LabyrinthProductId);
            ProductProfile cosmic = ProductCatalog.Create(ProductCatalog.CosmicLegionPlaceholderProductId);

            Assert.Equal(ProductCatalog.LabyrinthProductId, labyrinth.ProductId);
            Assert.False(labyrinth.Rules.HasTimeLimit);
            Assert.Equal(0, labyrinth.Rules.MaximumRunSeconds);
            Assert.Equal(ProductCatalog.CosmicLegionPlaceholderProductId, cosmic.ProductId);
            Assert.True(cosmic.Rules.HasTimeLimit);
            Assert.Equal(180, cosmic.Rules.MaximumRunSeconds);
            Assert.Equal(2, ProductCatalog.CreateAll().Count);
        }

        [Fact]
        public void InvalidProductAndNegativeTimeLimitAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ProductCatalog.Create("missing"));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ProductRuleProfile(-1));
        }

        [Fact]
        public void ProfilesUseDifferentContentAndThemeTokens()
        {
            ProductProfile labyrinth = ProductCatalog.Create(ProductCatalog.LabyrinthProductId);
            ProductProfile cosmic = ProductCatalog.Create(ProductCatalog.CosmicLegionPlaceholderProductId);

            Assert.NotEqual(labyrinth.Content.ContentId, cosmic.Content.ContentId);
            Assert.NotEqual(labyrinth.Theme.EnvironmentToken, cosmic.Theme.EnvironmentToken);
            Assert.NotEqual(
                labyrinth.Content.SquadUnits[0].TemplateId,
                cosmic.Content.SquadUnits[0].TemplateId);
        }
    }
}
