using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Game.Database;
using MongoDB.Bson;
using Shared.Core;
using Shared.Core.Database;
using Shared.Core.ECS;
using Shared.Core.Log;

namespace Game.Components.Player;

/// <summary>
/// 玩家聚合持久化。业务只修改主线程内存并标脏；本系统按批次生成快照并分集合写入。
/// </summary>
public static class PlayerPersistenceSystem
{
    private static readonly ILog Log = LogManager.GetLogger(nameof(PlayerPersistenceSystem));
    private const int SaveIntervalMs = 60_000;

    public static void MarkDirty(this Component component)
    {
        if (component is not IEntity)
            throw new InvalidOperationException($"组件 {component.GetType().Name} 不是持久化实体");
        if (component.Parent is not PlayerEntity player)
            throw new InvalidOperationException($"组件 {component.GetType().Name} 未挂载到 PlayerEntity");

        player.MarkDirty(component.GetType());
    }

    public static void StartBatchSave(this PlayerComponent self)
    {
        if (self.SaveTimerStarted)
            return;

        self.SaveTimer = GameTimer.Start(SaveIntervalMs, self.QueueOnlineBatchSave);
        self.SaveTimerStarted = true;
        Log.Info($"玩家批量保存定时器已启动 IntervalMs:{SaveIntervalMs}");
    }

    public static void StopBatchSave(this PlayerComponent self)
    {
        if (!self.SaveTimerStarted)
            return;

        GameTimer.Stop(self.SaveTimer);
        self.SaveTimerStarted = false;
        Log.Info("玩家批量保存定时器已停止");
    }

    public static void QueueOnlineBatchSave(this PlayerComponent self)
    {
        var players = self.Players.Values
            .Concat(self.PendingSavePlayers)
            .Distinct()
            .ToList();
        _ = self.EnqueueSave(players);
    }

    public static void QueueDetachedPlayerSave(this PlayerComponent self, PlayerEntity player)
    {
        self.PendingSavePlayers.Add(player);
        _ = self.EnqueueSave([player]);
    }

    public static Task FlushAllPlayersAsync(this PlayerComponent self)
    {
        var players = self.Players.Values
            .Concat(self.PendingSavePlayers)
            .Distinct()
            .ToList();
        return self.EnqueueSave(players);
    }

    private static Task EnqueueSave(this PlayerComponent self, List<PlayerEntity> players)
    {
        self.SaveQueue = SaveAfterAsync(self, self.SaveQueue, players);
        return self.SaveQueue;
    }

    private static async Task SaveAfterAsync(
        PlayerComponent self, Task previousSave, List<PlayerEntity> players)
    {
        try
        {
            await previousSave;
        }
        catch (Exception ex)
        {
            Log.Error("等待上一批玩家保存时发生异常，继续处理当前批次", ex);
        }

        var snapshots = CaptureSnapshots(players);
        if (snapshots.Count == 0)
        {
            RemoveCleanPendingPlayers(self, players);
            return;
        }

        var savedCount = 0;
        foreach (var group in snapshots.GroupBy(snapshot => snapshot.EntityType))
        {
            var groupSnapshots = group.ToList();
            try
            {
                await MongoDb.Instance.UpdateBatchAsync(
                    group.Key, groupSnapshots.Select(snapshot => snapshot.Document).ToList());
                foreach (var snapshot in groupSnapshots)
                {
                    snapshot.Player.ConfirmSaved(snapshot.EntityType, snapshot.Version);
                    savedCount++;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"玩家批量保存失败 Component:{group.Key.Name} Count:{groupSnapshots.Count}", ex);
            }
        }

        RemoveCleanPendingPlayers(self, players);
        Log.Info($"玩家批量保存完成 PlayerCount:{players.Count} ComponentCount:{savedCount}");
    }

    private static List<PlayerSaveSnapshot> CaptureSnapshots(List<PlayerEntity> players)
    {
        var snapshots = new List<PlayerSaveSnapshot>();
        foreach (var player in players)
        {
            foreach (var dirtyVersion in player.GetDirtyVersions())
            {
                var component = player.GetComponent(dirtyVersion.Key);
                if (component is not IEntity entity)
                {
                    Log.Error($"玩家脏组件无法持久化 RoleId:{player.Id} Component:{dirtyVersion.Key.Name}");
                    continue;
                }

                try
                {
                    var document = entity.ToBsonDocument(dirtyVersion.Key);
                    document["_id"] = entity.Id;
                    snapshots.Add(new PlayerSaveSnapshot(
                        player, dirtyVersion.Key, dirtyVersion.Value, document));
                }
                catch (Exception ex)
                {
                    Log.Error($"生成玩家保存快照失败 RoleId:{player.Id} Component:{dirtyVersion.Key.Name}", ex);
                }
            }
        }
        return snapshots;
    }

    private static void RemoveCleanPendingPlayers(PlayerComponent self, List<PlayerEntity> players)
    {
        foreach (var player in players)
        {
            if (!player.HasDirtyData)
                self.PendingSavePlayers.Remove(player);
        }
    }

    private sealed record PlayerSaveSnapshot(
        PlayerEntity Player, Type EntityType, long Version, BsonDocument Document);
}
