using Shared.Core.Config;

namespace Shared.Common.Config
{
    /// <summary>
    /// 地图配置。对应 Map.csv。
    /// </summary>
    [ConfigTable("Map.csv")]
    public class MapConfig
    {
        /// <summary>地图配置ID</summary>
        public int Id { get; set; }

        /// <summary>地图名称</summary>
        public string Name { get; set; } = "";

        /// <summary>地图宽度（格子）</summary>
        public int Width { get; set; }

        /// <summary>地图高度（格子）</summary>
        public int Height { get; set; }

        /// <summary>出生点X</summary>
        public int BirthX { get; set; }

        /// <summary>出生点Y</summary>
        public int BirthY { get; set; }
    }
}
