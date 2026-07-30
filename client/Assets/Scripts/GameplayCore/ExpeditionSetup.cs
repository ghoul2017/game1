using System;

namespace GameplayCore
{
    /// <summary>
    /// 区分普通敌人与使用地面警告攻击的 Boss。
    /// </summary>
    public enum ExpeditionEnemyKind
    {
        Normal = 0,
        Boss = 1
    }

    /// <summary>
    /// 保存创建一局远征所需的固定规则和初始对象数据。
    /// </summary>
    public sealed class ExpeditionSetup
    {
        public const int MaximumMemberCount = 5;

        public int TicksPerSecond { get; set; }

        public float MoveSpeedPerSecond { get; set; }

        public float MinimumX { get; set; }

        public float MaximumX { get; set; }

        public float MinimumY { get; set; }

        public float MaximumY { get; set; }

        public float InitialSquadX { get; set; }

        public float InitialSquadY { get; set; }

        public float TargetAcquireRadius { get; set; }

        public float TargetReleaseMargin { get; set; }

        public float EnemyThreatRadius { get; set; }

        public float ExitX { get; set; }

        public float ExitY { get; set; }

        public float ExitRadius { get; set; }

        public ExpeditionMemberSetup[] Members { get; set; }

        public ExpeditionEnemySetup[] Enemies { get; set; }

        public ExpeditionResourceSetup[] Resources { get; set; }
    }

    /// <summary>
    /// 保存一名队员开局时的生命和自动攻击参数。
    /// </summary>
    public sealed class ExpeditionMemberSetup
    {
        public int Id { get; set; }

        public int MaxHealth { get; set; }

        public int AttackDamage { get; set; }

        public float AttackRange { get; set; }

        public int AttackIntervalTicks { get; set; }
    }

    /// <summary>
    /// 保存一个敌人开局时的位置、生命和行动参数。
    /// </summary>
    public sealed class ExpeditionEnemySetup
    {
        public int Id { get; set; }

        public ExpeditionEnemyKind Kind { get; set; }

        public float X { get; set; }

        public float Y { get; set; }

        public int MaxHealth { get; set; }

        public int AttackDamage { get; set; }

        public float AttackRange { get; set; }

        public int ActionIntervalTicks { get; set; }

        public int BossWarningDelayTicks { get; set; }

        public float BossWarningRadius { get; set; }
    }

    /// <summary>
    /// 保存一个资源点开局时的位置、数量和采集参数。
    /// </summary>
    public sealed class ExpeditionResourceSetup
    {
        public int Id { get; set; }

        public float X { get; set; }

        public float Y { get; set; }

        public int Amount { get; set; }

        public int CollectionAmount { get; set; }

        public float InteractionRange { get; set; }

        public int CollectionIntervalTicks { get; set; }
    }

    /// <summary>
    /// 在创建权威局内状态前集中校验外部提供的开局数据。
    /// </summary>
    internal static class ExpeditionSetupValidator
    {
        internal static void Validate(ExpeditionSetup setup)
        {
            if (setup == null)
            {
                throw new ArgumentNullException(nameof(setup));
            }

            ValidateWorld(setup);
            ValidateMembers(setup.Members);
            ValidateEnemies(setup, setup.Enemies);
            ValidateResources(setup, setup.Resources);
        }

        private static void ValidateWorld(ExpeditionSetup setup)
        {
            if (setup.TicksPerSecond <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(setup.TicksPerSecond), "Tick 率必须是正数");
            }

            RequirePositive(setup.MoveSpeedPerSecond, nameof(setup.MoveSpeedPerSecond));
            RequireFinite(setup.MinimumX, nameof(setup.MinimumX));
            RequireFinite(setup.MaximumX, nameof(setup.MaximumX));
            RequireFinite(setup.MinimumY, nameof(setup.MinimumY));
            RequireFinite(setup.MaximumY, nameof(setup.MaximumY));

            if (setup.MinimumX >= setup.MaximumX || setup.MinimumY >= setup.MaximumY)
            {
                throw new ArgumentException("活动边界最小值必须小于最大值", nameof(setup));
            }

            RequireFinite(setup.InitialSquadX, nameof(setup.InitialSquadX));
            RequireFinite(setup.InitialSquadY, nameof(setup.InitialSquadY));
            RequireInside(setup, setup.InitialSquadX, setup.InitialSquadY, "队伍初始位置");

            RequirePositive(setup.TargetAcquireRadius, nameof(setup.TargetAcquireRadius));
            RequireNonNegative(setup.TargetReleaseMargin, nameof(setup.TargetReleaseMargin));
            RequirePositive(setup.EnemyThreatRadius, nameof(setup.EnemyThreatRadius));
            RequirePositive(
                setup.TargetAcquireRadius + setup.TargetReleaseMargin,
                nameof(setup.TargetReleaseMargin));

            RequireFinite(setup.ExitX, nameof(setup.ExitX));
            RequireFinite(setup.ExitY, nameof(setup.ExitY));
            RequirePositive(setup.ExitRadius, nameof(setup.ExitRadius));
            RequireInside(setup, setup.ExitX, setup.ExitY, "出口");

            RequirePositive(setup.MoveSpeedPerSecond / setup.TicksPerSecond, nameof(setup.MoveSpeedPerSecond));
        }

        private static void ValidateMembers(ExpeditionMemberSetup[] members)
        {
            if (members == null)
            {
                throw new ArgumentNullException(nameof(members));
            }

            if (members.Length == 0 || members.Length > ExpeditionSetup.MaximumMemberCount)
            {
                throw new ArgumentException(
                    $"队伍人数必须在 1 到 {ExpeditionSetup.MaximumMemberCount} 之间",
                    nameof(members));
            }

            for (int index = 0; index < members.Length; index++)
            {
                ExpeditionMemberSetup member = members[index];
                if (member == null)
                {
                    throw new ArgumentException("队员数据不能包含空值", nameof(members));
                }

                RequireId(member.Id, nameof(member.Id));
                RequirePositive(member.MaxHealth, nameof(member.MaxHealth));
                RequirePositive(member.AttackDamage, nameof(member.AttackDamage));
                RequirePositive(member.AttackRange, nameof(member.AttackRange));
                RequirePositive(member.AttackIntervalTicks, nameof(member.AttackIntervalTicks));

                for (int previous = 0; previous < index; previous++)
                {
                    if (members[previous].Id == member.Id)
                    {
                        throw new ArgumentException("队员 ID 不能重复", nameof(members));
                    }
                }
            }
        }

        private static void ValidateEnemies(ExpeditionSetup setup, ExpeditionEnemySetup[] enemies)
        {
            if (enemies == null)
            {
                throw new ArgumentNullException(nameof(enemies));
            }

            if (enemies.Length == 0)
            {
                throw new ArgumentException("远征至少需要一个敌人", nameof(enemies));
            }

            int bossCount = 0;
            for (int index = 0; index < enemies.Length; index++)
            {
                ExpeditionEnemySetup enemy = enemies[index];
                if (enemy == null)
                {
                    throw new ArgumentException("敌人数据不能包含空值", nameof(enemies));
                }

                ValidateEnemy(setup, enemy);
                if (enemy.Kind == ExpeditionEnemyKind.Boss)
                {
                    bossCount++;
                }

                for (int previous = 0; previous < index; previous++)
                {
                    if (enemies[previous].Id == enemy.Id)
                    {
                        throw new ArgumentException("敌人 ID 不能重复", nameof(enemies));
                    }
                }
            }

            if (bossCount != 1)
            {
                throw new ArgumentException("远征必须且只能配置一个 Boss", nameof(enemies));
            }
        }

        private static void ValidateEnemy(ExpeditionSetup setup, ExpeditionEnemySetup enemy)
        {
            RequireId(enemy.Id, nameof(enemy.Id));
            RequireFinite(enemy.X, nameof(enemy.X));
            RequireFinite(enemy.Y, nameof(enemy.Y));
            RequireInside(setup, enemy.X, enemy.Y, "敌人");
            RequirePositive(enemy.MaxHealth, nameof(enemy.MaxHealth));
            RequirePositive(enemy.AttackDamage, nameof(enemy.AttackDamage));
            RequirePositive(enemy.AttackRange, nameof(enemy.AttackRange));
            RequirePositive(enemy.ActionIntervalTicks, nameof(enemy.ActionIntervalTicks));

            if (enemy.Kind == ExpeditionEnemyKind.Boss)
            {
                RequirePositive(enemy.BossWarningDelayTicks, nameof(enemy.BossWarningDelayTicks));
                RequirePositive(enemy.BossWarningRadius, nameof(enemy.BossWarningRadius));
                return;
            }

            if (enemy.Kind != ExpeditionEnemyKind.Normal)
            {
                throw new ArgumentOutOfRangeException(nameof(enemy.Kind), "敌人种类无效");
            }

            if (enemy.BossWarningDelayTicks != 0 || enemy.BossWarningRadius != 0.0f)
            {
                throw new ArgumentException("普通敌人不能包含 Boss 警告参数", nameof(enemy));
            }
        }

        private static void ValidateResources(ExpeditionSetup setup, ExpeditionResourceSetup[] resources)
        {
            if (resources == null)
            {
                throw new ArgumentNullException(nameof(resources));
            }

            if (resources.Length == 0)
            {
                throw new ArgumentException("远征至少需要一个资源点", nameof(resources));
            }

            for (int index = 0; index < resources.Length; index++)
            {
                ExpeditionResourceSetup resource = resources[index];
                if (resource == null)
                {
                    throw new ArgumentException("资源点数据不能包含空值", nameof(resources));
                }

                RequireId(resource.Id, nameof(resource.Id));
                RequireFinite(resource.X, nameof(resource.X));
                RequireFinite(resource.Y, nameof(resource.Y));
                RequireInside(setup, resource.X, resource.Y, "资源点");
                RequirePositive(resource.Amount, nameof(resource.Amount));
                RequirePositive(resource.CollectionAmount, nameof(resource.CollectionAmount));
                RequirePositive(resource.InteractionRange, nameof(resource.InteractionRange));
                RequirePositive(resource.CollectionIntervalTicks, nameof(resource.CollectionIntervalTicks));

                for (int previous = 0; previous < index; previous++)
                {
                    if (resources[previous].Id == resource.Id)
                    {
                        throw new ArgumentException("资源点 ID 不能重复", nameof(resources));
                    }
                }
            }
        }

        private static void RequireId(int value, string parameterName)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(parameterName, "ID 必须是正数");
            }
        }

        private static void RequirePositive(int value, string parameterName)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(parameterName, "数值必须是正数");
            }
        }

        private static void RequirePositive(float value, string parameterName)
        {
            if (!IsFinite(value) || value <= 0.0f)
            {
                throw new ArgumentOutOfRangeException(parameterName, "数值必须是有限正数");
            }
        }

        private static void RequireNonNegative(float value, string parameterName)
        {
            if (!IsFinite(value) || value < 0.0f)
            {
                throw new ArgumentOutOfRangeException(parameterName, "数值必须是有限非负数");
            }
        }

        private static void RequireFinite(float value, string parameterName)
        {
            if (!IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(parameterName, "数值必须有限");
            }
        }

        private static void RequireInside(ExpeditionSetup setup, float x, float y, string objectName)
        {
            if (x < setup.MinimumX || x > setup.MaximumX || y < setup.MinimumY || y > setup.MaximumY)
            {
                throw new ArgumentOutOfRangeException(nameof(setup), $"{objectName}必须位于活动边界内");
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
