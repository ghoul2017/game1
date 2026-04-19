using Shared.Core.Config;

namespace Shared.Common.Config
{
    /// <summary>
    /// 任务配置。对应 Task.csv。
    /// </summary>
    [ConfigTable("Task.csv", KeyName = "Id")]
    public class TaskConfig
    {
        /// <summary>任务ID</summary>
        public int Id { get; set; }

        /// <summary>任务名称</summary>
        public string Name { get; set; } = "";

        /// <summary>任务类型（1=主线, 2=支线, 3=日常, 4=活动）</summary>
        public int Type { get; set; }

        /// <summary>任务描述</summary>
        public string Description { get; set; } = "";

        /// <summary>接取NPC ID</summary>
        public int AcceptNpcId { get; set; }

        /// <summary>完成NPC ID</summary>
        public int SubmitNpcId { get; set; }

        /// <summary>任务目标数量</summary>
        public int GoalCount { get; set; }

        /// <summary>目标1类型（1=击杀怪物, 2=收集物品, 3=对话, 4=到达位置）</summary>
        public int GoalType1 { get; set; }

        /// <summary>目标1参数（怪物ID/物品ID/NPCID/坐标等）</summary>
        public int GoalParam1 { get; set; }

        /// <summary>目标1数量</summary>
        public int GoalTarget1 { get; set; }

        /// <summary>目标2类型</summary>
        public int GoalType2 { get; set; }

        /// <summary>目标2参数</summary>
        public int GoalParam2 { get; set; }

        /// <summary>目标2数量</summary>
        public int GoalTarget2 { get; set; }

        /// <summary>目标3类型</summary>
        public int GoalType3 { get; set; }

        /// <summary>目标3参数</summary>
        public int GoalParam3 { get; set; }

        /// <summary>目标3数量</summary>
        public int GoalTarget3 { get; set; }

        /// <summary>奖励经验</summary>
        public int RewardExp { get; set; }

        /// <summary>奖励金币</summary>
        public int RewardGold { get; set; }

        /// <summary>奖励物品列表（格式：物品ID:数量,物品ID:数量）</summary>
        public string RewardItems { get; set; } = "";

        /// <summary>前置任务ID（0表示无前置）</summary>
        public int PreTaskId { get; set; }

        /// <summary>最低等级要求</summary>
        public int MinLevel { get; set; }
    }
}
