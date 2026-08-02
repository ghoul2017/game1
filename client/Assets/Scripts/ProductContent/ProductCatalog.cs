using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ProductContent
{
    public static class ProductCatalog
    {
        public const int AuthoredTicksPerSecond = 15;
        public const string LabyrinthProductId = "labyrinth";
        public const string CosmicLegionPlaceholderProductId = "cosmic_legion_placeholder";

        public static ProductProfile Create(string productId)
        {
            if (string.Equals(productId, LabyrinthProductId, StringComparison.Ordinal))
            {
                return CreateLabyrinth();
            }

            if (string.Equals(productId, CosmicLegionPlaceholderProductId, StringComparison.Ordinal))
            {
                return CreateCosmicLegionPlaceholder();
            }

            throw new ArgumentOutOfRangeException(nameof(productId), $"未知产品：{productId}");
        }

        public static IReadOnlyList<ProductProfile> CreateAll()
        {
            return new ReadOnlyCollection<ProductProfile>(new[]
            {
                CreateLabyrinth(),
                CreateCosmicLegionPlaceholder()
            });
        }

        private static ProductProfile CreateLabyrinth()
        {
            return new ProductProfile(
                LabyrinthProductId,
                "异界迷宫（占位）",
                new ProductRuleProfile(0),
                new ProductThemeProfile("#233044", "#59D4C7", "labyrinth_placeholder"),
                new ProductContentPack(
                    "labyrinth_content_v1",
                    new[]
                    {
                        new ProductUnitContent(1001, "探索员 A", 120, 18, 8),
                        new ProductUnitContent(1002, "探索员 B", 95, 22, 8),
                        new ProductUnitContent(1003, "探索员 C", 105, 16, 8)
                    },
                    new[]
                    {
                        new ProductUnitContent(2001, "异界生物 A", 70, 10, 15),
                        new ProductUnitContent(2002, "异界生物 B", 85, 12, 45)
                    },
                    3));
        }

        private static ProductProfile CreateCosmicLegionPlaceholder()
        {
            return new ProductProfile(
                CosmicLegionPlaceholderProductId,
                "宇宙军团（占位）",
                new ProductRuleProfile(180),
                new ProductThemeProfile("#172A56", "#F3C94E", "cosmic_legion_placeholder"),
                new ProductContentPack(
                    "cosmic_legion_content_v1",
                    new[]
                    {
                        new ProductUnitContent(3001, "宇宙战士 A", 125, 17, 8),
                        new ProductUnitContent(3002, "宇宙战士 B", 100, 21, 8),
                        new ProductUnitContent(3003, "防卫机体 A", 115, 15, 8)
                    },
                    new[]
                    {
                        new ProductUnitContent(4001, "暗域生物 A", 75, 11, 15),
                        new ProductUnitContent(4002, "暗域生物 B", 90, 13, 45)
                    },
                    2));
        }
    }
}
