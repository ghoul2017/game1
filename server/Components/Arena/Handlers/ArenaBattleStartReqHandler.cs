using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Game.Components.Player;
using GameplayCore;
using Shared.Common.Protos;
using Shared.Core;
using Shared.Core.Log;
using Shared.Core.Network;

namespace Game.Components.Arena;

public sealed class ArenaBattleStartReqHandler : PlayerRpcMessageHandler<ArenaBattleStartReq, ArenaBattleStartAck>
{
    private const int MaximumRequestIdLength = 64;
    private static readonly ILog Log = LogManager.GetLogger(nameof(ArenaBattleStartReqHandler));

    protected override async Task ExecuteAsync(
        PlayerEntity player,
        ArenaBattleStartReq request,
        ArenaBattleStartAck response)
    {
        string requestId = request.RequestId;
        if (string.IsNullOrWhiteSpace(requestId) || requestId.Length > MaximumRequestIdLength ||
            !string.Equals(requestId, requestId.Trim(), StringComparison.Ordinal))
        {
            response.ErrorMessage = "RequestId 无效";
            return;
        }

        ArenaComponent arena = Context.Server.GetComponent<ArenaComponent>();
        if (arena.TryGetByRequest(player.Id, requestId, out ArenaBattleRecord existing))
        {
            ArenaProtocolMapper.FillStartAck(existing, response);
            return;
        }

        if (!arena.HasCapacity(player.Id))
        {
            response.ErrorMessage = "竞技场当前繁忙，请稍后再试";
            return;
        }

        long battleId = IdGenerator.Instance.NextId();
        uint seed = CreateSeed();
        ArenaBattleRecord record = new ArenaBattleRecord(battleId, player.Id, requestId, seed);
        arena.AddPending(record);
        arena.SimulationRunCount++;

        try
        {
            ArenaSquadSnapshot attacker = ArenaSnapshotMapper.CreateAttacker(player);
            ArenaBattleSetup setup = ArenaSnapshotMapper.CreateBattleSetup(seed, attacker, arena.FixedDefender);
            record.Complete(ArenaBattleRunner.Run(setup));
        }
        catch (Exception exception)
        {
            record.Fail("竞技场战斗模拟失败");
            Log.Error($"[Arena] 战斗模拟失败 RoleId:{player.Id} BattleId:{battleId}", exception);
        }

        ArenaProtocolMapper.FillStartAck(record, response);
        await Task.CompletedTask;
    }

    private static uint CreateSeed()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(sizeof(uint));
        return BitConverter.ToUInt32(bytes, 0);
    }
}
