using ProductContent;
using Xunit;

namespace ProductContent.Tests
{
    public sealed class ProductRunTimerTests
    {
        [Fact]
        public void LabyrinthProfileNeverExpires()
        {
            ProductProfile profile = ProductCatalog.Create(ProductCatalog.LabyrinthProductId);
            ProductRunTimer timer = new ProductRunTimer(profile.Rules, 10);

            bool expired = timer.Advance(100_000);

            Assert.False(timer.HasTimeLimit);
            Assert.False(expired);
            Assert.False(timer.HasExpired);
            Assert.Equal(-1L, timer.RemainingTicks);
            Assert.Equal(100_000L, timer.CurrentTick);
        }

        [Fact]
        public void CosmicPlaceholderExpiresExactlyAtOneHundredEightySeconds()
        {
            ProductProfile profile = ProductCatalog.Create(ProductCatalog.CosmicLegionPlaceholderProductId);
            ProductRunTimer timer = new ProductRunTimer(profile.Rules, 10);

            Assert.False(timer.Advance(1_799));
            Assert.False(timer.HasExpired);
            Assert.Equal(1L, timer.RemainingTicks);

            Assert.True(timer.Advance(1));
            Assert.True(timer.HasExpired);
            Assert.Equal(1_800L, timer.CurrentTick);
            Assert.Equal(0L, timer.RemainingTicks);
            Assert.False(timer.Advance(1));
            Assert.Equal(1_800L, timer.CurrentTick);
        }

        [Fact]
        public void TimerRejectsInvalidTickInputs()
        {
            ProductRuleProfile rules = new ProductRuleProfile(180);

            Assert.Throws<System.ArgumentOutOfRangeException>(() => new ProductRunTimer(rules, 0));
            ProductRunTimer timer = new ProductRunTimer(rules, 10);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => timer.Advance(0));
        }
    }
}
