using System;
using System.Threading.Tasks;
using Shared.Common.Protos;

namespace Client.Logic
{
    public sealed class ArenaController
    {
        public static ArenaController Instance { get; } = new ArenaController();

        private ArenaController()
        {
        }

        public async Task<ArenaBattleStartAck> StartBattleAsync(string requestId)
        {
            if (string.IsNullOrWhiteSpace(requestId))
            {
                throw new ArgumentException("RequestId 不能为空", nameof(requestId));
            }

            ArenaBattleStartReq request = new ArenaBattleStartReq { RequestId = requestId };
            return await NetClient.Instance.CallAsync<ArenaBattleStartAck>(request);
        }

        public async Task<ArenaReplayAck> GetReplayAsync(long battleId)
        {
            if (battleId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(battleId));
            }

            ArenaReplayReq request = new ArenaReplayReq { BattleId = battleId };
            return await NetClient.Instance.CallAsync<ArenaReplayAck>(request);
        }
    }
}
