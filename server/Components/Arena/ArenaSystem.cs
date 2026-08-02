namespace Game.Components.Arena;

public static class ArenaSystem
{
    public static bool TryGetByRequest(
        this ArenaComponent self,
        long roleId,
        string requestId,
        out ArenaBattleRecord record)
    {
        PruneExpired(self);
        return self.BattlesByRequest.TryGetValue(new ArenaRequestKey(roleId, requestId), out record!);
    }

    public static bool TryGetByBattleId(
        this ArenaComponent self,
        long battleId,
        out ArenaBattleRecord record)
    {
        PruneExpired(self);
        return self.BattlesById.TryGetValue(battleId, out record!);
    }

    public static bool HasCapacity(this ArenaComponent self, long roleId)
    {
        if (roleId <= 0L)
        {
            throw new ArgumentOutOfRangeException(nameof(roleId));
        }

        PruneExpired(self);
        self.RequestCountsByRole.TryGetValue(roleId, out int roleRequestCount);
        return self.BattlesByRequest.Count < ArenaComponent.MaximumIdempotencyCount &&
            roleRequestCount < ArenaComponent.MaximumIdempotencyCountPerRole &&
            (self.BattlesById.Count < ArenaComponent.MaximumBattleCount || HasFinishedBattle(self));
    }

    public static void AddPending(this ArenaComponent self, ArenaBattleRecord record)
    {
        PruneExpired(self);
        ArenaRequestKey requestKey = new ArenaRequestKey(record.RoleId, record.RequestId);
        if (self.BattlesByRequest.ContainsKey(requestKey) || self.BattlesById.ContainsKey(record.BattleId))
        {
            throw new InvalidOperationException("竞技场 RequestId 或 BattleId 重复");
        }

        self.RequestCountsByRole.TryGetValue(record.RoleId, out int roleRequestCount);
        if (self.BattlesByRequest.Count >= ArenaComponent.MaximumIdempotencyCount ||
            roleRequestCount >= ArenaComponent.MaximumIdempotencyCountPerRole)
        {
            throw new InvalidOperationException("竞技场幂等结果已达到容量上限");
        }

        while (self.BattlesById.Count >= ArenaComponent.MaximumBattleCount)
        {
            if (!TryEvictOldestFinished(self))
            {
                throw new InvalidOperationException("竞技场内存结果已达到容量上限");
            }
        }

        record.MarkRegistered(self.GetNowMilliseconds());
        self.BattlesByRequest.Add(requestKey, record);
        self.BattlesById.Add(record.BattleId, record);
        self.BattleOrder.Enqueue(record.BattleId);
        self.RequestOrder.Enqueue(requestKey);
        self.RequestCountsByRole[record.RoleId] = roleRequestCount + 1;
    }

    private static bool HasFinishedBattle(ArenaComponent self)
    {
        foreach (ArenaBattleRecord record in self.BattlesById.Values)
        {
            if (record.HasFinished)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryEvictOldestFinished(ArenaComponent self)
    {
        int inspectedCount = self.BattleOrder.Count;
        for (int index = 0; index < inspectedCount; index++)
        {
            long battleId = self.BattleOrder.Dequeue();
            if (!self.BattlesById.TryGetValue(battleId, out ArenaBattleRecord? record))
            {
                continue;
            }

            if (!record.HasFinished)
            {
                self.BattleOrder.Enqueue(battleId);
                continue;
            }

            self.BattlesById.Remove(battleId);
            record.ReleaseReplay();
            return true;
        }

        return false;
    }

    private static void PruneExpired(ArenaComponent self)
    {
        long nowMilliseconds = self.GetNowMilliseconds();
        PruneExpiredReplays(self, nowMilliseconds);
        PruneExpiredIdempotency(self, nowMilliseconds);
    }

    private static void PruneExpiredReplays(ArenaComponent self, long nowMilliseconds)
    {
        int inspectedCount = self.BattleOrder.Count;
        for (int index = 0; index < inspectedCount; index++)
        {
            long battleId = self.BattleOrder.Dequeue();
            if (!self.BattlesById.TryGetValue(battleId, out ArenaBattleRecord? record))
            {
                continue;
            }

            if (!record.HasFinished || nowMilliseconds - record.RegisteredAtMilliseconds <
                ArenaComponent.ReplayRetentionMilliseconds)
            {
                self.BattleOrder.Enqueue(battleId);
                continue;
            }

            self.BattlesById.Remove(battleId);
            record.ReleaseReplay();
        }
    }

    private static void PruneExpiredIdempotency(ArenaComponent self, long nowMilliseconds)
    {
        int inspectedCount = self.RequestOrder.Count;
        for (int index = 0; index < inspectedCount; index++)
        {
            ArenaRequestKey requestKey = self.RequestOrder.Dequeue();
            if (!self.BattlesByRequest.TryGetValue(requestKey, out ArenaBattleRecord? record))
            {
                continue;
            }

            if (!record.HasFinished || nowMilliseconds - record.RegisteredAtMilliseconds <
                ArenaComponent.IdempotencyRetentionMilliseconds)
            {
                self.RequestOrder.Enqueue(requestKey);
                continue;
            }

            self.BattlesByRequest.Remove(requestKey);
            if (self.RequestCountsByRole.TryGetValue(record.RoleId, out int roleRequestCount))
            {
                if (roleRequestCount <= 1)
                {
                    self.RequestCountsByRole.Remove(record.RoleId);
                }
                else
                {
                    self.RequestCountsByRole[record.RoleId] = roleRequestCount - 1;
                }
            }

            if (self.BattlesById.Remove(record.BattleId))
            {
                record.ReleaseReplay();
            }
        }
    }
}
