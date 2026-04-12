using Server.Core.Log;
using Server.Network;
using server.common.protos;

namespace Server.Game.Fight;

/// <summary>
/// 使用技能 Handler
/// </summary>
public class UseSkillHandler : MessageHandler<UseSkillReq>
{
    private static readonly ILog Log = LogManager.GetLogger<UseSkillHandler>();

    protected override Task<IMessage?> ExecuteAsync(TcpSession session, UseSkillReq req)
    {
        if (session.RoleId == 0)
        {
            return Task.FromResult<IMessage?>(new UseSkillAck { Code = FightErrorCode.NotInFight });
        }

        var result = FightManager.Instance.UseSkill(session.RoleId, req.SkillId, req.X, req.Y, req.TargetId);

        // UseSkillAck 已在 FightManager.UseSkill 中通过广播发送
        // 这里返回空响应避免客户端超时（部分客户端框架需要 ack）
        var ack = new UseSkillAck { Code = result.Code };
        return Task.FromResult<IMessage?>(ack);
    }
}

/// <summary>
/// 设置PK模式 Handler
/// </summary>
public class SetPkModeHandler : MessageHandler<SetPkModeReq>
{
    protected override Task<IMessage?> ExecuteAsync(TcpSession session, SetPkModeReq req)
    {
        if (session.RoleId == 0)
            return Task.FromResult<IMessage?>(new PkModeChangedNtf());

        // 简单验证PK模式范围
        if (req.PkMode < PkMode.Peace || req.PkMode > PkMode.All)
            return Task.FromResult<IMessage?>(new PkModeChangedNtf());

        // 通知玩家PK模式变化
        var ntf = new PkModeChangedNtf { PkMode = req.PkMode };

        return Task.FromResult<IMessage?>(ntf);
    }
}
