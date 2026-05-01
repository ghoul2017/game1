using Shared.Core.Config;

namespace Shared.Common.Config
{
    /// <summary>
    /// NPC配置。对应 Npc.csv。
    /// </summary>
    [ConfigTable(FileName = "Npc.csv", KeyName = "Id")]
    public class NpcConfig
    {
        /// <summary>NPC配置ID</summary>
        public int Id { get; set; }

        /// <summary>NPC名称</summary>
        public string Name { get; set; } = "";

        /// <summary>NPC类型（1=普通NPC, 2=任务NPC, 3=商店NPC, 4=传送NPC）</summary>
        public int Type { get; set; }

        /// <summary>所属地图ID</summary>
        public int MapId { get; set; }

        /// <summary>地图X坐标</summary>
        public int X { get; set; }

        /// <summary>地图Y坐标</summary>
        public int Y { get; set; }

        /// <summary>对话文本</summary>
        public string Dialog { get; set; } = "";

        /// <summary>是否可攻击（0=否, 1=是）</summary>
        public int CanAttack { get; set; }

        /// <summary>头顶显示名字（0=不显示, 1=显示）</summary>
        public int ShowName { get; set; }

        /// <summary>头顶显示血条（0=不显示, 1=显示）</summary>
        public int ShowHpBar { get; set; }
    }
}