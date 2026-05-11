namespace Shared.Core.Aoi;

/// <summary>
/// AOI 对象接口，所有需要进入 AOI 系统的实体需实现此接口
/// </summary>
public interface IAoiObject
{
    /// <summary>对象唯一ID</summary>
    long ObjectId { get; }

    /// <summary>对象类型（用于按类型查询）</summary>
    int ObjectType { get; }

    /// <summary>X 坐标（格子单位）</summary>
    int X { get; set; }

    /// <summary>Y 坐标（格子单位）</summary>
    int Y { get; set; }
}
