using ProtoBuf;
using Shared.Core.Network;
using System.Collections.Generic;

namespace Shared.Common.Protos
{
    // --- 登录 ---

    [ProtoContract]
    [Message("User.LoginReq")]
    public class LoginReq : IMessage
    {
        [ProtoMember(1)] public string Username { get; set; } = "";
        [ProtoMember(2)] public string Password { get; set; } = "";
    }

    [ProtoContract]
    [Message("User.LoginRes")]
    public class LoginRes : IMessage
    {
        [ProtoMember(1)] public string ErrorMessage { get; set; } = "";
        [ProtoMember(2)] public long PlayerId { get; set; }
        [ProtoMember(3)] public string Token { get; set; } = "";
        [ProtoMember(4)] public List<RoleInfo> Roles { get; set; } = new List<RoleInfo>();
    }

    // --- 角色信息（复用于列表/创建/选角） ---

    [ProtoContract]
    public class RoleInfo
    {
        [ProtoMember(1)] public long RoleId { get; set; }
        [ProtoMember(2)] public string Name { get; set; } = "";
        [ProtoMember(3)] public int Level { get; set; }
        [ProtoMember(4)] public int Job { get; set; }
    }

    // --- 创建角色 ---

    [ProtoContract]
    [Message("User.CreateRoleReq")]
    public class CreateRoleReq : IMessage
    {
        [ProtoMember(1)] public string Name { get; set; } = "";
        [ProtoMember(2)] public int Job { get; set; }
    }

    [ProtoContract]
    [Message("User.CreateRoleRes")]
    public class CreateRoleRes : IMessage
    {
        [ProtoMember(1)] public string ErrorMessage { get; set; } = "";
        [ProtoMember(2)] public RoleInfo Role { get; set; } = new RoleInfo();
    }

    // --- 进入游戏 ---

    [ProtoContract]
    [Message("User.EnterGameReq")]
    public class EnterGameReq : IMessage
    {
        [ProtoMember(1)] public long RoleId { get; set; }
    }

    [ProtoContract]
    [Message("User.EnterGameRes")]
    public class EnterGameRes : IMessage
    {
        [ProtoMember(1)] public string ErrorMessage { get; set; } = "";
        [ProtoMember(2)] public string Token { get; set; } = "";
    }

    // --- 断线重连 ---

    [ProtoContract]
    [Message("User.ReconnectReq")]
    public class ReconnectReq : IMessage
    {
        [ProtoMember(1)] public string Token { get; set; } = "";
        [ProtoMember(2)] public long RoleId { get; set; }
    }

    [ProtoContract]
    [Message("User.ReconnectRes")]
    public class ReconnectRes : IMessage
    {
        [ProtoMember(1)] public string ErrorMessage { get; set; } = "";
        [ProtoMember(2)] public string Token { get; set; } = "";
    }

    // --- 踢人通知 ---

    [ProtoContract]
    [Message("User.KickNtf")]
    public class KickNtf : IMessage
    {
        [ProtoMember(1)] public int Reason { get; set; }
    }

    // --- 进入游戏通知 ---

    [ProtoContract]
    [Message("User.EnterGameNtf")]
    public class EnterGameNtf : IMessage
    {
        [ProtoMember(1)] public long RoleId { get; set; }
        [ProtoMember(2)] public string Name { get; set; } = "";
        [ProtoMember(3)] public int Level { get; set; }
        [ProtoMember(4)] public int Job { get; set; }
    }
}
