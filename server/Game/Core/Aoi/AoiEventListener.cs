using System.Collections.Generic;

namespace Game.Core.Aoi;

/// <summary>
/// AOI 事件监听器接口
/// </summary>
public interface IAoiEventListener
{
    /// <summary>对象进入灯塔视野</summary>
    void OnObjectEnter(IAoiObject obj, IReadOnlyDictionary<long, IAoiObject> watchers);

    /// <summary>对象离开灯塔视野</summary>
    void OnObjectLeave(IAoiObject obj, IReadOnlyDictionary<long, IAoiObject> watchers);

    /// <summary>对象在同一灯塔内移动</summary>
    void OnObjectMove(IAoiObject obj, IReadOnlyDictionary<long, IAoiObject> oldWatchers,
        IReadOnlyDictionary<long, IAoiObject> updateWatchers, IReadOnlyDictionary<long, IAoiObject> newWatchers);

    /// <summary>观察者自身视野更新（通知自己看到了什么新对象/失去了什么对象）</summary>
    void OnWatcherUpdate(IAoiObject watcher, IReadOnlyList<IAoiObject> addedObjects, IReadOnlyList<IAoiObject> removedObjects);
}
