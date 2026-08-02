using System;
using System.Collections.Generic;
using GameplayCore;
using ProductContent;

namespace ProductExpedition
{
    /// <summary>
    /// 在产品中立组合层连接产品规则与真实远征；GameplayCore 不读取产品配置，ProductContent 不依赖核心。
    /// </summary>
    public sealed class ProductExpeditionRun
    {
        public const string DefaultProductId = ProductCatalog.LabyrinthProductId;
        public const string TimedPlaceholderProductId = ProductCatalog.CosmicLegionPlaceholderProductId;

        private readonly ProductRunTimer _timer;

        public ProductExpeditionRun(ExpeditionRun run, string productId)
        {
            Run = run ?? throw new ArgumentNullException(nameof(run));
            ProductProfile profile = ProductCatalog.Create(productId);
            ProductId = profile.ProductId;
            _timer = new ProductRunTimer(profile.Rules, Run.TicksPerSecond);
        }

        public ExpeditionRun Run { get; }

        public string ProductId { get; }

        public bool HasTimeLimit => _timer.HasTimeLimit;

        public bool HasTimeExpired => _timer.HasExpired;

        public long RemainingRuleTicks => _timer.RemainingTicks;

        public long RuleTick => _timer.CurrentTick;

        public static void ValidateProductId(string productId)
        {
            ProductCatalog.Create(productId);
        }

        public bool Advance(
            float directionX,
            float directionY,
            List<ExpeditionEvent> events)
        {
            return Advance(directionX, directionY, 0, events);
        }

        public bool Advance(
            float directionX,
            float directionY,
            int interactionTargetId,
            List<ExpeditionEvent> events)
        {
            if (events == null)
            {
                throw new ArgumentNullException(nameof(events));
            }

            if (Run.HasEnded)
            {
                return false;
            }

            long previousTick = Run.Tick;
            ExpeditionSimulation.Advance(Run, directionX, directionY, interactionTargetId, events);
            long advancedTicks = Run.Tick - previousTick;
            if (advancedTicks != 1L)
            {
                throw new InvalidOperationException("运行中的远征每次规则推进必须恰好增加一个 Tick");
            }

            bool expiredNow = _timer.Advance(1);
            if (!expiredNow || Run.HasEnded)
            {
                return false;
            }

            return ExpeditionSimulation.TryFail(Run, events);
        }
    }
}
