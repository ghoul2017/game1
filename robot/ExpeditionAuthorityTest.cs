using System;
using System.Threading.Tasks;
using Shared.Common.Protos;

namespace Robot
{
    /// <summary>
    /// 需要独立本地 Server/MongoDB；会创建一次性账号、角色，完整结算第一局并启动第二局。
    /// </summary>
    public sealed class ExpeditionAuthorityTest : TestScenario
    {
        public override string Name => "expedition_authority_test";

        public override async Task Run()
        {
            RobotPlayerIdentity identity = await RobotPlayerBootstrap.CreateIsolatedPlayerAsync(Session, "exp");
            long requestId = DateTime.UtcNow.Ticks;
            ExpeditionStartRes started = await Session.CallAsync<ExpeditionStartRes>(new ExpeditionStartReq
            {
                RequestId = requestId
            });

            Assertions.True(string.IsNullOrEmpty(started.ErrorMessage), $"远征启动应成功: {started.ErrorMessage}");
            Assertions.True(started.Snapshot.RunId > 0L, "服务端应分配 RunId");
            long runId = started.Snapshot.RunId;

            ExpeditionStartRes repeatedStart = await Session.CallAsync<ExpeditionStartRes>(new ExpeditionStartReq
            {
                RequestId = requestId
            });
            Assertions.True(
                string.IsNullOrEmpty(repeatedStart.ErrorMessage),
                $"相同 RequestId 应幂等返回当前局: {repeatedStart.ErrorMessage}");
            Assertions.Equal(runId, repeatedStart.Snapshot.RunId, "相同 RequestId 不应创建第二个 Run");

            ExpeditionStartRes competingStart = await Session.CallAsync<ExpeditionStartRes>(new ExpeditionStartReq
            {
                RequestId = checked(requestId + 1L)
            });
            Assertions.True(!string.IsNullOrEmpty(competingStart.ErrorMessage), "活动局期间的新 RequestId 应被拒绝");

            await Session.SendAsync(new ExpeditionInputNtf
            {
                RunId = runId,
                InputSequence = 1L,
                DirectionX = ExpeditionProtocolConstants.DirectionScale + 1,
                DirectionY = 0
            });
            await Session.SendAsync(new ExpeditionInputNtf
            {
                RunId = runId,
                InputSequence = 2L,
                DirectionX = ExpeditionProtocolConstants.DirectionScale,
                DirectionY = 0
            });
            ExpeditionSnapshotData accepted = await WaitForInputSequenceAsync(runId, 2L);
            Assertions.Equal(2L, accepted.LastProcessedInputSequence, "非法方向不应阻断后续有效序列");

            await Session.SendAsync(new ExpeditionInputNtf
            {
                RunId = runId + 1L,
                InputSequence = 3L,
                DirectionX = 0,
                DirectionY = 0
            });
            await Session.SendAsync(new ExpeditionInputNtf
            {
                RunId = runId,
                InputSequence = 3L,
                DirectionX = 0,
                DirectionY = ExpeditionProtocolConstants.DirectionScale
            });
            ExpeditionSnapshotData ownerChecked = await WaitForInputSequenceAsync(runId, 3L);
            Assertions.Equal(3L, ownerChecked.LastProcessedInputSequence, "伪造 RunId 不应消耗输入序列");

            long tickBeforeDisconnect = ownerChecked.ServerTick;
            string consumedToken = identity.ReconnectToken;
            Session.Disconnect();
            await Task.Delay(350);
            await Session.ReconnectAsync();
            HandshakeRes handshake = await Session.CallAsync<HandshakeRes>(new HandshakeReq());
            Assertions.True(string.IsNullOrEmpty(handshake.ErrorMessage), "重连新会话握手应成功");

            ReconnectRes reconnect = await Session.CallAsync<ReconnectRes>(new ReconnectReq
            {
                RoleId = identity.RoleId,
                Token = consumedToken
            });
            Assertions.True(string.IsNullOrEmpty(reconnect.ErrorMessage), $"同进程重连应成功: {reconnect.ErrorMessage}");
            Assertions.True(!string.IsNullOrEmpty(reconnect.Token), "重连应旋转新 Token");
            Assertions.False(string.Equals(consumedToken, reconnect.Token, StringComparison.Ordinal), "Token 应旋转");

            EnterGameRes reenter = await Session.CallAsync<EnterGameRes>(new EnterGameReq
            {
                RoleId = identity.RoleId
            });
            Assertions.True(string.IsNullOrEmpty(reenter.ErrorMessage), $"重连后重进角色应成功: {reenter.ErrorMessage}");
            Session.UserId = identity.UserId;
            Session.RoleId = identity.RoleId;

            ExpeditionResumeRes resumed = await Session.CallAsync<ExpeditionResumeRes>(new ExpeditionResumeReq
            {
                RunId = runId
            });
            Assertions.True(string.IsNullOrEmpty(resumed.ErrorMessage), $"恢复远征应成功: {resumed.ErrorMessage}");
            Assertions.True(resumed.HasActiveRun, "断线保留期内应存在活动局");
            Assertions.Equal(runId, resumed.Snapshot.RunId, "恢复应返回原 RunId");
            Assertions.True(resumed.Snapshot.ServerTick > tickBeforeDisconnect, "断线期间权威 Tick 应以零输入继续");

            using (RobotSession replaySession = new RobotSession())
            {
                await replaySession.ConnectAsync(Session.Host, Session.Port);
                HandshakeRes replayHandshake = await replaySession.CallAsync<HandshakeRes>(new HandshakeReq());
                Assertions.True(
                    string.IsNullOrEmpty(replayHandshake.ErrorMessage),
                    $"Token 重放连接握手应成功: {replayHandshake.ErrorMessage}");
                ReconnectRes replayedToken = await replaySession.CallAsync<ReconnectRes>(new ReconnectReq
                {
                    RoleId = identity.RoleId,
                    Token = consumedToken
                });
                Assertions.True(
                    !string.IsNullOrEmpty(replayedToken.ErrorMessage),
                    "已消费 Token 不应在新的未认证连接上接管角色");
            }

            ExpeditionSettlementRes prematureSettlement =
                await Session.CallAsync<ExpeditionSettlementRes>(new ExpeditionSettlementReq { RunId = runId });
            Assertions.True(
                !string.IsNullOrEmpty(prematureSettlement.ErrorMessage),
                "客户端不应在权威局终态前触发结算");
            Assertions.Equal(
                resumed.Progress.ResourceBalance,
                prematureSettlement.Progress.ResourceBalance,
                "被拒绝的提前结算不应改变资源");

            ExpeditionSnapshotData terminal = await DriveToTerminalAsync(resumed.Snapshot, 4L);
            Assertions.True(terminal.Phase == 1 || terminal.Phase == 2, "权威局应到达终态");

            ExpeditionSettlementRes settled =
                await Session.CallAsync<ExpeditionSettlementRes>(new ExpeditionSettlementReq { RunId = runId });
            Assertions.True(string.IsNullOrEmpty(settled.ErrorMessage), $"终态结算应成功: {settled.ErrorMessage}");
            Assertions.Equal(runId, settled.Settlement.RunId, "结算回执应属于第一局");
            Assertions.True(settled.Settlement.AwardedResource > 0L, "完整远征应产生可入账资源");
            Assertions.Equal(
                settled.Settlement.ResourceBalance,
                settled.Progress.ResourceBalance,
                "结算回执与永久进度余额应一致");

            ExpeditionSettlementRes repeatedSettlement =
                await Session.CallAsync<ExpeditionSettlementRes>(new ExpeditionSettlementReq { RunId = runId });
            Assertions.True(
                string.IsNullOrEmpty(repeatedSettlement.ErrorMessage),
                $"重复结算应幂等成功: {repeatedSettlement.ErrorMessage}");
            Assertions.Equal(
                settled.Settlement.ResourceBalance,
                repeatedSettlement.Settlement.ResourceBalance,
                "重复结算不应再次增加资源");

            ExpeditionStartRes repeatedCompletedStart =
                await Session.CallAsync<ExpeditionStartRes>(new ExpeditionStartReq { RequestId = requestId });
            Assertions.True(
                !string.IsNullOrEmpty(repeatedCompletedStart.ErrorMessage),
                "保留期内已完成的 Start RequestId 不应创建新局");

            int oldDamage = settled.Progress.RepresentativeDamage;
            ExpeditionUpgradeRes upgraded = await Session.CallAsync<ExpeditionUpgradeRes>(new ExpeditionUpgradeReq
            {
                FacilityId = ExpeditionProtocolConstants.FacilityId
            });
            Assertions.True(string.IsNullOrEmpty(upgraded.ErrorMessage), $"基地升级应成功: {upgraded.ErrorMessage}");
            Assertions.Equal(1, upgraded.Progress.FacilityLevel, "设施应升级到当前测试上限");
            Assertions.Equal(settled.Progress.UpgradeCost, upgraded.SpentResource, "升级扣费应使用服务端规则价格");
            Assertions.True(upgraded.NewRepresentativeDamage > oldDamage, "升级应提高下一局代表伤害");

            long secondRequestId = checked(requestId + 2L);
            ExpeditionStartRes secondRun = await Session.CallAsync<ExpeditionStartRes>(new ExpeditionStartReq
            {
                RequestId = secondRequestId
            });
            Assertions.True(string.IsNullOrEmpty(secondRun.ErrorMessage), $"第二局应成功开始: {secondRun.ErrorMessage}");
            Assertions.True(secondRun.Snapshot.RunId != runId, "第二局必须获得新的 RunId");
            Assertions.Equal(2L, secondRun.Snapshot.RunIndex, "结算升级后应进入第二局");
            Assertions.Equal(1, secondRun.Progress.FacilityLevel, "第二局应继承已升级设施");
        }

        private async Task<ExpeditionSnapshotData> DriveToTerminalAsync(
            ExpeditionSnapshotData initialSnapshot,
            long firstInputSequence)
        {
            ExpeditionSnapshotData snapshot = initialSnapshot;
            long inputSequence = firstInputSequence;
            for (int step = 0; step < 900 && snapshot.Phase == 0; step++)
            {
                (float directionX, float directionY) = ChooseDirection(snapshot);
                ExpeditionProtocolConstants.QuantizeDirection(
                    directionX,
                    directionY,
                    out int quantizedX,
                    out int quantizedY);
                Task<ExpeditionSnapshotNtf> nextSnapshot =
                    Session.WaitForAsync<ExpeditionSnapshotNtf>(1500);
                await Session.SendAsync(new ExpeditionInputNtf
                {
                    RunId = snapshot.RunId,
                    InputSequence = inputSequence++,
                    DirectionX = quantizedX,
                    DirectionY = quantizedY
                });

                ExpeditionSnapshotNtf notification = await nextSnapshot;
                if (notification.Snapshot.RunId == snapshot.RunId)
                {
                    snapshot = notification.Snapshot;
                }

                if (step % 10 == 0)
                {
                    Console.WriteLine(
                        $"  远征推进: step={step} tick={snapshot.ServerTick} " +
                        $"room={snapshot.CurrentRoomId} phase={snapshot.Phase} " +
                        $"input={snapshot.LastProcessedInputSequence} " +
                        $"pos=({snapshot.SquadX:0.00},{snapshot.SquadY:0.00}) " +
                        $"target={snapshot.TargetKind}:{snapshot.TargetId} " +
                        $"kills={snapshot.Kills} resource={snapshot.CollectedResource} " +
                        DescribeCurrentRoom(snapshot));
                }
            }

            if (snapshot.Phase == 0)
            {
                throw new TimeoutException($"远征未在自动驾驶窗口内结束，RunId={snapshot.RunId}");
            }

            return snapshot;
        }

        private static string DescribeCurrentRoom(ExpeditionSnapshotData snapshot)
        {
            for (int roomIndex = 0; roomIndex < snapshot.RevealedRooms.Count; roomIndex++)
            {
                ExpeditionRoomStateData room = snapshot.RevealedRooms[roomIndex];
                if (!room.IsCurrent)
                {
                    continue;
                }

                if (room.VisibleExits.Count == 0)
                {
                    return $"state={room.State} exits=0";
                }

                ExpeditionVisibleExitData exit = room.VisibleExits[0];
                return $"state={room.State} exits={room.VisibleExits.Count} " +
                    $"first={exit.TargetRoomId}/{exit.IsOpen}@({exit.X:0.0},{exit.Y:0.0})";
            }

            return "current-room-missing";
        }

        private static (float X, float Y) ChooseDirection(ExpeditionSnapshotData snapshot)
        {
            for (int index = 0; index < snapshot.Enemies.Count; index++)
            {
                ExpeditionEnemyStateData enemy = snapshot.Enemies[index];
                if (enemy.WarningActive)
                {
                    float awayX = snapshot.SquadX - enemy.WarningX;
                    float awayY = snapshot.SquadY - enemy.WarningY;
                    return Math.Abs(awayX) + Math.Abs(awayY) < 0.01f
                        ? (1.0f, 0.0f)
                        : Normalize(awayX, awayY);
                }
            }

            if (TryFindCurrentTarget(snapshot, out float currentTargetX, out float currentTargetY))
            {
                return Approach(snapshot, currentTargetX, currentTargetY);
            }

            if (TryFindNearestContent(snapshot, out float contentX, out float contentY))
            {
                return Approach(snapshot, contentX, contentY);
            }

            for (int roomIndex = 0; roomIndex < snapshot.RevealedRooms.Count; roomIndex++)
            {
                ExpeditionRoomStateData room = snapshot.RevealedRooms[roomIndex];
                if (!room.IsCurrent)
                {
                    continue;
                }

                for (int exitIndex = 0; exitIndex < room.VisibleExits.Count; exitIndex++)
                {
                    ExpeditionVisibleExitData exit = room.VisibleExits[exitIndex];
                    if (exit.IsOpen && !RoomIsCleared(snapshot, exit.TargetRoomId))
                    {
                        return Normalize(exit.X - snapshot.SquadX, exit.Y - snapshot.SquadY);
                    }
                }

                for (int exitIndex = 0; exitIndex < room.VisibleExits.Count; exitIndex++)
                {
                    ExpeditionVisibleExitData exit = room.VisibleExits[exitIndex];
                    if (exit.IsOpen)
                    {
                        return Normalize(exit.X - snapshot.SquadX, exit.Y - snapshot.SquadY);
                    }
                }
            }

            return (0.0f, 0.0f);
        }

        private static bool RoomIsCleared(ExpeditionSnapshotData snapshot, int roomId)
        {
            for (int index = 0; index < snapshot.RevealedRooms.Count; index++)
            {
                ExpeditionRoomStateData room = snapshot.RevealedRooms[index];
                if (room.RoomId == roomId)
                {
                    return room.State == 2;
                }
            }

            return false;
        }

        private static bool TryFindCurrentTarget(
            ExpeditionSnapshotData snapshot,
            out float targetX,
            out float targetY)
        {
            targetX = 0.0f;
            targetY = 0.0f;
            if (snapshot.TargetKind == 1)
            {
                for (int index = 0; index < snapshot.Enemies.Count; index++)
                {
                    ExpeditionEnemyStateData enemy = snapshot.Enemies[index];
                    if (enemy.Id == snapshot.TargetId && enemy.Health > 0)
                    {
                        targetX = enemy.X;
                        targetY = enemy.Y;
                        return true;
                    }
                }
            }
            else if (snapshot.TargetKind == 2)
            {
                for (int index = 0; index < snapshot.Resources.Count; index++)
                {
                    ExpeditionResourceStateData resource = snapshot.Resources[index];
                    if (resource.Id == snapshot.TargetId && resource.RemainingAmount > 0)
                    {
                        targetX = resource.X;
                        targetY = resource.Y;
                        return true;
                    }
                }
            }

            return false;
        }

        private static (float X, float Y) Approach(
            ExpeditionSnapshotData snapshot,
            float targetX,
            float targetY)
        {
            float deltaX = targetX - snapshot.SquadX;
            float deltaY = targetY - snapshot.SquadY;
            float distanceSquared = deltaX * deltaX + deltaY * deltaY;
            return distanceSquared <= 2.5f * 2.5f ? (0.0f, 0.0f) : Normalize(deltaX, deltaY);
        }

        private static bool TryFindNearestContent(
            ExpeditionSnapshotData snapshot,
            out float targetX,
            out float targetY)
        {
            targetX = 0.0f;
            targetY = 0.0f;
            float nearestDistanceSquared = float.MaxValue;
            for (int index = 0; index < snapshot.Enemies.Count; index++)
            {
                ExpeditionEnemyStateData enemy = snapshot.Enemies[index];
                if (enemy.Health > 0)
                {
                    SelectNearest(snapshot, enemy.X, enemy.Y, ref nearestDistanceSquared, ref targetX, ref targetY);
                }
            }

            for (int index = 0; index < snapshot.Resources.Count; index++)
            {
                ExpeditionResourceStateData resource = snapshot.Resources[index];
                if (resource.RemainingAmount > 0)
                {
                    SelectNearest(
                        snapshot,
                        resource.X,
                        resource.Y,
                        ref nearestDistanceSquared,
                        ref targetX,
                        ref targetY);
                }
            }

            return nearestDistanceSquared < float.MaxValue;
        }

        private static void SelectNearest(
            ExpeditionSnapshotData snapshot,
            float x,
            float y,
            ref float nearestDistanceSquared,
            ref float targetX,
            ref float targetY)
        {
            float deltaX = x - snapshot.SquadX;
            float deltaY = y - snapshot.SquadY;
            float distanceSquared = deltaX * deltaX + deltaY * deltaY;
            if (distanceSquared < nearestDistanceSquared)
            {
                nearestDistanceSquared = distanceSquared;
                targetX = x;
                targetY = y;
            }
        }

        private static (float X, float Y) Normalize(float x, float y)
        {
            double length = Math.Sqrt((double)x * x + (double)y * y);
            return length <= 0.0001d ? (0.0f, 0.0f) : ((float)(x / length), (float)(y / length));
        }

        private async Task<ExpeditionSnapshotData> WaitForInputSequenceAsync(long runId, long inputSequence)
        {
            for (int attempt = 0; attempt < 20; attempt++)
            {
                ExpeditionSnapshotNtf notification = await Session.WaitForAsync<ExpeditionSnapshotNtf>(1000);
                if (notification.Snapshot.RunId == runId &&
                    notification.Snapshot.LastProcessedInputSequence >= inputSequence)
                {
                    return notification.Snapshot;
                }
            }

            throw new TimeoutException($"未收到输入序列 {inputSequence} 的权威快照");
        }
    }
}
