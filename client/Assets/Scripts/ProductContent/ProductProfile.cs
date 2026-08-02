using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ProductContent
{
    public sealed class ProductRuleProfile
    {
        public ProductRuleProfile(int maximumRunSeconds)
        {
            if (maximumRunSeconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumRunSeconds));
            }

            MaximumRunSeconds = maximumRunSeconds;
        }

        public int MaximumRunSeconds { get; }

        public bool HasTimeLimit => MaximumRunSeconds > 0;
    }

    public sealed class ProductThemeProfile
    {
        public ProductThemeProfile(string primaryColor, string accentColor, string environmentToken)
        {
            PrimaryColor = RequireText(primaryColor, nameof(primaryColor));
            AccentColor = RequireText(accentColor, nameof(accentColor));
            EnvironmentToken = RequireText(environmentToken, nameof(environmentToken));
        }

        public string PrimaryColor { get; }

        public string AccentColor { get; }

        public string EnvironmentToken { get; }

        private static string RequireText(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("主题字段不能为空", parameterName);
            }

            return value;
        }
    }

    public sealed class ProductUnitContent
    {
        public ProductUnitContent(
            int templateId,
            string displayName,
            int maxHealth,
            int attackDamage,
            int attackIntervalTicks)
        {
            if (templateId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(templateId));
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException("单位占位名称不能为空", nameof(displayName));
            }

            if (maxHealth <= 0 || attackDamage <= 0 || attackIntervalTicks <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxHealth), "单位战斗参数必须是正数");
            }

            TemplateId = templateId;
            DisplayName = displayName;
            MaxHealth = maxHealth;
            AttackDamage = attackDamage;
            AttackIntervalTicks = attackIntervalTicks;
        }

        public int TemplateId { get; }

        public string DisplayName { get; }

        public int MaxHealth { get; }

        public int AttackDamage { get; }

        public int AttackIntervalTicks { get; }

        internal ProductUnitContent Copy()
        {
            return new ProductUnitContent(
                TemplateId,
                DisplayName,
                MaxHealth,
                AttackDamage,
                AttackIntervalTicks);
        }
    }

    public sealed class ProductContentPack
    {
        private readonly ProductUnitContent[] _enemyUnits;
        private readonly ReadOnlyCollection<ProductUnitContent> _enemyView;
        private readonly ProductUnitContent[] _squadUnits;
        private readonly ReadOnlyCollection<ProductUnitContent> _squadView;

        public ProductContentPack(
            string contentId,
            IReadOnlyList<ProductUnitContent> squadUnits,
            IReadOnlyList<ProductUnitContent> enemyUnits,
            int resourceNodeCount)
        {
            if (string.IsNullOrWhiteSpace(contentId))
            {
                throw new ArgumentException("内容包 ID 不能为空", nameof(contentId));
            }

            if (resourceNodeCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(resourceNodeCount));
            }

            ContentId = contentId;
            ResourceNodeCount = resourceNodeCount;
            _squadUnits = CopyUnits(squadUnits, nameof(squadUnits));
            _enemyUnits = CopyUnits(enemyUnits, nameof(enemyUnits));
            _squadView = Array.AsReadOnly(_squadUnits);
            _enemyView = Array.AsReadOnly(_enemyUnits);
        }

        public string ContentId { get; }

        public IReadOnlyList<ProductUnitContent> SquadUnits => _squadView;

        public IReadOnlyList<ProductUnitContent> EnemyUnits => _enemyView;

        public int ResourceNodeCount { get; }

        internal ProductContentPack Copy()
        {
            return new ProductContentPack(ContentId, _squadUnits, _enemyUnits, ResourceNodeCount);
        }

        private static ProductUnitContent[] CopyUnits(
            IReadOnlyList<ProductUnitContent> units,
            string parameterName)
        {
            if (units == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (units.Count == 0 || units.Count > 5)
            {
                throw new ArgumentException("占位阵容人数必须在 1 到 5 之间", parameterName);
            }

            ProductUnitContent[] result = new ProductUnitContent[units.Count];
            for (int index = 0; index < units.Count; index++)
            {
                ProductUnitContent unit = units[index];
                if (unit == null)
                {
                    throw new ArgumentException("占位阵容不能包含空单位", parameterName);
                }

                result[index] = unit.Copy();
                for (int previous = 0; previous < index; previous++)
                {
                    if (result[previous].TemplateId == result[index].TemplateId)
                    {
                        throw new ArgumentException("占位阵容模板 ID 不能重复", parameterName);
                    }
                }
            }

            return result;
        }
    }

    public sealed class ProductProfile
    {
        public ProductProfile(
            string productId,
            string displayName,
            ProductRuleProfile rules,
            ProductThemeProfile theme,
            ProductContentPack content)
        {
            if (string.IsNullOrWhiteSpace(productId))
            {
                throw new ArgumentException("产品 ID 不能为空", nameof(productId));
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException("产品占位名称不能为空", nameof(displayName));
            }

            ProductId = productId;
            DisplayName = displayName;
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            Theme = theme ?? throw new ArgumentNullException(nameof(theme));
            Content = content?.Copy() ?? throw new ArgumentNullException(nameof(content));
        }

        public string ProductId { get; }

        public string DisplayName { get; }

        public ProductRuleProfile Rules { get; }

        public ProductThemeProfile Theme { get; }

        public ProductContentPack Content { get; }
    }
}
