using System.Collections.Generic;

namespace Game.Game.Aoi;

/// <summary>
/// 单个灯塔，管理一定范围内的 AOI 对象和观察者
/// </summary>
public class Tower
{
    /// <summary>灯塔内所有对象</summary>
    private readonly Dictionary<long, IAoiObject> _objects = new();

    /// <summary>灯塔内所有观察者</summary>
    private readonly Dictionary<long, IAoiObject> _watchers = new();

    /// <summary>按类型分类的对象索引</summary>
    private readonly Dictionary<int, Dictionary<long, IAoiObject>> _objectsByType = new();

    /// <summary>灯塔内所有对象（只读）</summary>
    public IReadOnlyDictionary<long, IAoiObject> Objects => _objects;

    /// <summary>灯塔内所有观察者（只读）</summary>
    public IReadOnlyDictionary<long, IAoiObject> Watchers => _watchers;

    /// <summary>添加对象到灯塔</summary>
    public void AddObject(IAoiObject obj)
    {
        _objects[obj.ObjectId] = obj;

        if (!_objectsByType.TryGetValue(obj.ObjectType, out var typeMap))
        {
            typeMap = new Dictionary<long, IAoiObject>();
            _objectsByType[obj.ObjectType] = typeMap;
        }
        typeMap[obj.ObjectId] = obj;
    }

    /// <summary>从灯塔移除对象</summary>
    public bool RemoveObject(IAoiObject obj)
    {
        if (!_objects.Remove(obj.ObjectId))
            return false;

        if (_objectsByType.TryGetValue(obj.ObjectType, out var typeMap))
            typeMap.Remove(obj.ObjectId);

        return true;
    }

    /// <summary>添加观察者</summary>
    public void AddWatcher(IAoiObject watcher)
    {
        _watchers[watcher.ObjectId] = watcher;
    }

    /// <summary>移除观察者</summary>
    public bool RemoveWatcher(IAoiObject watcher)
    {
        return _watchers.Remove(watcher.ObjectId);
    }

    /// <summary>获取灯塔内所有对象（副本）</summary>
    public List<IAoiObject> GetAllObjects()
    {
        return new List<IAoiObject>(_objects.Values);
    }

    /// <summary>按类型获取对象列表</summary>
    public List<IAoiObject> GetObjectsByType(int type)
    {
        if (!_objectsByType.TryGetValue(type, out var typeMap))
            return new List<IAoiObject>();
        return new List<IAoiObject>(typeMap.Values);
    }

    /// <summary>灯塔是否为空（无对象且无观察者）</summary>
    public bool IsEmpty => _objects.Count == 0 && _watchers.Count == 0;
}
