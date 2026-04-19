using System;
using System.Collections.Generic;
using Shared.Core.Log;

namespace Game.Game.Aoi;

/// <summary>
/// 灯塔 AOI 系统
/// 将地图划分为灯塔网格，通过灯塔管理视野和对象
/// </summary>
public class TowerAoi
{
    private static readonly ILog Log = LogManager.GetLogger<TowerAoi>();

    /// <summary>灯塔网格：key = (towerX, towerY)</summary>
    private readonly Dictionary<(int, int), Tower> _towers = new();

    /// <summary>对象所在的灯塔坐标</summary>
    private readonly Dictionary<long, (int tx, int ty)> _objectTowerMap = new();

    /// <summary>观察者所在的灯塔坐标和视野范围</summary>
    private readonly Dictionary<long, WatcherInfo> _watcherInfoMap = new();

    /// <summary>事件监听器列表</summary>
    private readonly List<IAoiEventListener> _listeners = new();

    /// <summary>每个灯塔覆盖的格子数（X轴）</summary>
    private readonly int _gridPerTowerX;

    /// <summary>每个灯塔覆盖的格子数（Y轴）</summary>
    private readonly int _gridPerTowerY;

    /// <summary>最大视野范围（灯塔数）</summary>
    private readonly int _rangeLimit;

    /// <summary>观察者信息</summary>
    private record WatcherInfo(int Tx, int Ty, int Range);

    /// <summary>
    /// 创建灯塔 AOI 系统
    /// </summary>
    /// <param name="gridPerTowerX">每个灯塔覆盖的格子数（X轴），默认5</param>
    /// <param name="gridPerTowerY">每个灯塔覆盖的格子数（Y轴），默认6</param>
    /// <param name="rangeLimit">最大视野范围（灯塔数），默认4</param>
    public TowerAoi(int gridPerTowerX = 5, int gridPerTowerY = 6, int rangeLimit = 4)
    {
        _gridPerTowerX = gridPerTowerX;
        _gridPerTowerY = gridPerTowerY;
        _rangeLimit = rangeLimit;
    }

    /// <summary>添加事件监听器</summary>
    public void AddListener(IAoiEventListener listener)
    {
        _listeners.Add(listener);
    }

    /// <summary>移除事件监听器</summary>
    public void RemoveListener(IAoiEventListener listener)
    {
        _listeners.Remove(listener);
    }

    #region 对象管理

    /// <summary>
    /// 添加对象到 AOI 系统
    /// </summary>
    public bool AddObject(IAoiObject obj)
    {
        var (tx, ty) = ToTowerCoord(obj.X, obj.Y);
        var tower = GetOrCreateTower(tx, ty);
        tower.AddObject(obj);
        _objectTowerMap[obj.ObjectId] = (tx, ty);

        // 通知该灯塔中的观察者：有新对象进入视野
        foreach (var listener in _listeners)
            listener.OnObjectEnter(obj, tower.Watchers);

        return true;
    }

    /// <summary>
    /// 从 AOI 系统移除对象
    /// </summary>
    public bool RemoveObject(IAoiObject obj)
    {
        if (!_objectTowerMap.Remove(obj.ObjectId, out var coord))
            return false;

        var tower = GetTower(coord.tx, coord.ty);
        if (tower == null) return false;

        tower.RemoveObject(obj);

        // 通知该灯塔中的观察者：有对象离开视野
        foreach (var listener in _listeners)
            listener.OnObjectLeave(obj, tower.Watchers);

        return true;
    }

    /// <summary>
    /// 更新对象位置
    /// </summary>
    public bool UpdateObject(IAoiObject obj, int oldX, int oldY, int newX, int newY)
    {
        var oldCoord = ToTowerCoord(oldX, oldY);
        var newCoord = ToTowerCoord(newX, newY);

        obj.X = newX;
        obj.Y = newY;

        // 同一个灯塔内移动
        if (oldCoord == newCoord)
        {
            var tower = GetTower(oldCoord.tx, oldCoord.ty);
            if (tower != null)
            {
                foreach (var listener in _listeners)
                    listener.OnObjectMove(obj, tower.Watchers, tower.Watchers, tower.Watchers);
            }
            return true;
        }

        // 跨灯塔移动
        var oldTower = GetTower(oldCoord.tx, oldCoord.ty);
        var newTower = GetOrCreateTower(newCoord.tx, newCoord.ty);

        if (oldTower == null) return false;

        oldTower.RemoveObject(obj);
        newTower.AddObject(obj);
        _objectTowerMap[obj.ObjectId] = newCoord;

        // 计算灯塔变更
        var change = GetTowerChange(oldCoord, newCoord);

        // 通知离开的灯塔中的观察者
        foreach (var pos in change.Removed)
        {
            var t = GetTower(pos.tx, pos.ty);
            if (t != null)
                foreach (var listener in _listeners)
                    listener.OnObjectLeave(obj, t.Watchers);
        }

        // 通知新增的灯塔中的观察者
        foreach (var pos in change.Added)
        {
            var t = GetTower(pos.tx, pos.ty);
            if (t != null)
                foreach (var listener in _listeners)
                    listener.OnObjectEnter(obj, t.Watchers);
        }

        return true;
    }

    #endregion

    #region 观察者管理

    /// <summary>
    /// 添加观察者到 AOI 系统
    /// </summary>
    public bool AddWatcher(IAoiObject watcher, int range)
    {
        range = Math.Min(range, _rangeLimit);
        var center = ToTowerCoord(watcher.X, watcher.Y);
        _watcherInfoMap[watcher.ObjectId] = new WatcherInfo(center.tx, center.ty, range);

        var addedObjects = new List<IAoiObject>();

        // 将观察者添加到视野范围内的所有灯塔
        ForEachTowerInRange(center.tx, center.ty, range, (tower) =>
        {
            tower.AddWatcher(watcher);
            addedObjects.AddRange(tower.GetAllObjects());
        });

        // 通知观察者：看到了哪些对象
        if (addedObjects.Count > 0)
        {
            foreach (var listener in _listeners)
                listener.OnWatcherUpdate(watcher, addedObjects, Array.Empty<IAoiObject>());
        }

        return true;
    }

    /// <summary>
    /// 从 AOI 系统移除观察者
    /// </summary>
    public bool RemoveWatcher(IAoiObject watcher)
    {
        if (!_watcherInfoMap.Remove(watcher.ObjectId, out var info))
            return false;

        var removedObjects = new List<IAoiObject>();

        ForEachTowerInRange(info.Tx, info.Ty, info.Range, (tower) =>
        {
            tower.RemoveWatcher(watcher);
            removedObjects.AddRange(tower.GetAllObjects());
        });

        // 通知观察者：失去了哪些对象
        if (removedObjects.Count > 0)
        {
            foreach (var listener in _listeners)
                listener.OnWatcherUpdate(watcher, Array.Empty<IAoiObject>(), removedObjects);
        }

        return true;
    }

    /// <summary>
    /// 更新观察者位置（视野跟随移动）
    /// </summary>
    public bool UpdateWatcher(IAoiObject watcher, int oldX, int oldY, int newX, int newY)
    {
        if (!_watcherInfoMap.TryGetValue(watcher.ObjectId, out var info))
            return false;

        var oldCoord = ToTowerCoord(oldX, oldY);
        var newCoord = ToTowerCoord(newX, newY);

        watcher.X = newX;
        watcher.Y = newY;

        // 同一个灯塔内移动，无需变更观察者
        if (oldCoord == newCoord) return true;

        var range = info.Range;
        _watcherInfoMap[watcher.ObjectId] = new WatcherInfo(newCoord.tx, newCoord.ty, range);

        // 计算灯塔变更
        var change = GetTowerChange(oldCoord, newCoord);

        // 从离开的灯塔中移除观察者
        foreach (var pos in change.Removed)
        {
            var tower = GetTower(pos.tx, pos.ty);
            tower?.RemoveWatcher(watcher);
        }

        // 向新增的灯塔中添加观察者
        foreach (var pos in change.Added)
        {
            var tower = GetOrCreateTower(pos.tx, pos.ty);
            tower.AddWatcher(watcher);
        }

        // 计算观察者视野内新增和失去的对象
        var addedObjects = new List<IAoiObject>();
        var removedObjects = new List<IAoiObject>();

        foreach (var pos in change.Added)
        {
            var tower = GetTower(pos.tx, pos.ty);
            if (tower != null)
                addedObjects.AddRange(tower.GetAllObjects());
        }

        foreach (var pos in change.Removed)
        {
            var tower = GetTower(pos.tx, pos.ty);
            if (tower != null)
                removedObjects.AddRange(tower.GetAllObjects());
        }

        // 通知观察者视野变化
        if (addedObjects.Count > 0 || removedObjects.Count > 0)
        {
            foreach (var listener in _listeners)
                listener.OnWatcherUpdate(watcher, addedObjects, removedObjects);
        }

        return true;
    }

    #endregion

    #region 查询

    /// <summary>
    /// 获取指定坐标范围内的所有对象
    /// </summary>
    public List<IAoiObject> GetObjectsByRange(int x, int y, int range)
    {
        range = Math.Min(range, _rangeLimit);
        var center = ToTowerCoord(x, y);
        var result = new List<IAoiObject>();

        ForEachTowerInRange(center.tx, center.ty, range, (tower) =>
        {
            result.AddRange(tower.GetAllObjects());
        });

        return result;
    }

    /// <summary>
    /// 获取指定坐标和范围内的指定类型对象
    /// </summary>
    public List<IAoiObject> GetObjectsByRangeAndType(int x, int y, int range, int type)
    {
        range = Math.Min(range, _rangeLimit);
        var center = ToTowerCoord(x, y);
        var result = new List<IAoiObject>();

        ForEachTowerInRange(center.tx, center.ty, range, (tower) =>
        {
            result.AddRange(tower.GetObjectsByType(type));
        });

        return result;
    }

    /// <summary>
    /// 获取对象所在的灯塔坐标
    /// </summary>
    public (int tx, int ty)? GetObjectTowerCoord(long objectId)
    {
        if (_objectTowerMap.TryGetValue(objectId, out var coord))
            return coord;
        return null;
    }

    #endregion

    #region 内部方法

    /// <summary>格子坐标转灯塔坐标</summary>
    private (int tx, int ty) ToTowerCoord(int x, int y)
    {
        int tx = x < 0 ? (x + 1) / _gridPerTowerX - 1 : x / _gridPerTowerX;
        int ty = y < 0 ? (y + 1) / _gridPerTowerY - 1 : y / _gridPerTowerY;
        return (tx, ty);
    }

    /// <summary>获取灯塔，不存在则返回 null</summary>
    private Tower? GetTower(int tx, int ty)
    {
        _towers.TryGetValue((tx, ty), out var tower);
        return tower;
    }

    /// <summary>获取或创建灯塔</summary>
    private Tower GetOrCreateTower(int tx, int ty)
    {
        if (!_towers.TryGetValue((tx, ty), out var tower))
        {
            tower = new Tower();
            _towers[(tx, ty)] = tower;
        }
        return tower;
    }

    /// <summary>遍历视野范围内的所有灯塔</summary>
    private void ForEachTowerInRange(int centerX, int centerY, int range, Action<Tower> action)
    {
        for (int dx = -range; dx <= range; dx++)
        {
            for (int dy = -range; dy <= range; dy++)
            {
                var tower = GetTower(centerX + dx, centerY + dy);
                if (tower != null)
                    action(tower);
            }
        }
    }

    /// <summary>计算灯塔变更（移动时哪些灯塔需要添加/移除）</summary>
    private TowerChangeResult GetTowerChange((int tx, int ty) oldCoord, (int tx, int ty) newCoord)
    {
        var added = new List<(int tx, int ty)>();
        var removed = new List<(int tx, int ty)>();

        // 旧位置视野有但新位置视野没有的灯塔 → 移除
        // 新位置视野有但旧位置视野没有的灯塔 → 添加
        // 对于对象移动，range=0 表示只看所在灯塔
        if (!oldCoord.Equals(newCoord))
        {
            // 对象从 oldCoord 移到 newCoord
            // 离开 oldCoord 的灯塔
            removed.Add(oldCoord);
            // 进入 newCoord 的灯塔
            added.Add(newCoord);
        }

        return new TowerChangeResult(added, removed);
    }

    private record TowerChangeResult(List<(int tx, int ty)> Added, List<(int tx, int ty)> Removed);

    #endregion
}
