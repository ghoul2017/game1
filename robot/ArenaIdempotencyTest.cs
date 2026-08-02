using System;
using System.Threading.Tasks;
using Shared.Common.Protos;

namespace Robot
{
    /// <summary>
    /// 需要独立本地 Server/MongoDB；会创建一次性账号、角色和一条内存竞技场结果。
    /// </summary>
    public sealed class ArenaIdempotencyTest : TestScenario
    {
        public override string Name => "arena_idempotency_test";

        public override async Task Run()
        {
            await RobotPlayerBootstrap.CreateIsolatedPlayerAsync(Session, "arena");
            string requestId = Guid.NewGuid().ToString("N");
            ArenaBattleStartAck first = await Session.CallAsync<ArenaBattleStartAck>(new ArenaBattleStartReq
            {
                RequestId = requestId
            });
            Assertions.True(string.IsNullOrEmpty(first.ErrorMessage), $"竞技场战斗应成功: {first.ErrorMessage}");
            Assertions.True(first.BattleId > 0L, "服务端应分配 BattleId");
            Assertions.True(first.FinalTick > 0L, "服务端应返回有界终态 Tick");

            ArenaBattleStartAck duplicate = await Session.CallAsync<ArenaBattleStartAck>(new ArenaBattleStartReq
            {
                RequestId = requestId
            });
            Assertions.True(string.IsNullOrEmpty(duplicate.ErrorMessage), "重复 RequestId 应返回原结果");
            Assertions.Equal(first.BattleId, duplicate.BattleId, "重复 RequestId 只能生成一个 BattleId");
            Assertions.Equal(first.EventHash, duplicate.EventHash, "重复 RequestId 的回放哈希应一致");
            Assertions.Equal(first.Outcome, duplicate.Outcome, "重复 RequestId 的胜负应一致");

            ArenaReplayAck replay = await Session.CallAsync<ArenaReplayAck>(new ArenaReplayReq
            {
                BattleId = first.BattleId
            });
            Assertions.True(string.IsNullOrEmpty(replay.ErrorMessage), $"回放读取应成功: {replay.ErrorMessage}");
            Assertions.Equal(first.BattleId, replay.BattleId, "回放应属于原战斗");
            Assertions.Equal(first.EventHash, replay.EventHash, "回放语义事件应与结果哈希一致");
            Assertions.Equal(first.Outcome, replay.Outcome, "播放数据不应改变胜负");
            Assertions.True(replay.KeyFrames.Count >= 2, "回放应包含初始和终态关键帧");

            ArenaReplayAck missing = await Session.CallAsync<ArenaReplayAck>(new ArenaReplayReq
            {
                BattleId = first.BattleId + 1L
            });
            Assertions.True(!string.IsNullOrEmpty(missing.ErrorMessage), "不存在的 BattleId 应被拒绝");
        }
    }
}
