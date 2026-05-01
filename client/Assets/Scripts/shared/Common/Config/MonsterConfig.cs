using Shared.Core.Config;

namespace Shared.Common.Config
{
    /// <summary>
    /// 怪物配置。对应 Monster.csv。
    /// </summary>
    [ConfigTable(FileName = "Monster.csv", KeyName = "Id")]
    public class MonsterConfig
    {
        /// <summary>怪物配置ID</summary>
        public int Id { get; set; }

        /// <summary>怪物名称</summary>
        public string Name { get; set; } = "";

        /// <summary>怪物类型（1=普通, 2=精英, 3=首领, 4=BOSS）</summary>
        public int Type { get; set; }

        /// <summary>等级</summary>
        public int Level { get; set; }

        /// <summary>所属地图ID</summary>
        public int MapId { get; set; }

        /// <summary>刷新范围X起始</summary>
        public int SpawnX1 { get; set; }

        /// <summary>刷新范围X结束</summary>
        public int SpawnX2 { get; set; }

        /// <summary>刷新范围Y起始</summary>
        public int SpawnY1 { get; set; }

        /// <summary>刷新范围Y结束</summary>
        public int SpawnY2 { get; set; }

        /// <summary>最大数量</summary>
        public int MaxCount { get; set; }

        /// <summary>刷新间隔（秒）</summary>
        public int SpawnInterval { get; set; }

        /// <summary>最大生命值</summary>
        public int MaxHp { get; set; }

        /// <summary>攻击力</summary>
        public int Attack { get; set; }

        /// <summary>防御力</summary>
        public int Defense { get; set; }

        /// <summary>移动速度（格子/秒）</summary>
        public int MoveSpeed { get; set; }

        /// <summary>攻击范围（格子）</summary>
        public int AttackRange { get; set; }

        /// <summary>视野范围（格子）</summary>
        public int SightRange { get; set; }

        /// <summary>击杀经验</summary>
        public int Exp { get; set; }

        /// <summary>掉落金币最小</summary>
        public int GoldMin { get; set; }

        /// <summary>掉落金币最大</summary>
        public int GoldMax { get; set; }

        /// <summary>掉落物品ID列表（逗号分隔）</summary>
        public string DropItems { get; set; } = "";

        /// <summary>掉落概率（万分比）</summary>
        public int DropRate { get; set; }

        /// <summary>AI类型（1=被动, 2=主动, 3=守卫）</summary>
        public int AiType { get; set; }
    }
}