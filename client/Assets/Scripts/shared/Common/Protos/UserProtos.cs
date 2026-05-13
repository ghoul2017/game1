using ProtoBuf;
using Shared.Core.Network;
using System.Collections.Generic;

namespace Shared.Common.Protos
{
    // --- 登录 ---

    [ProtoContract]
    public class LoginReq : IMessage
    {
        public ushort MsgId => MsgIdConst.LoginReq;
        [ProtoMember(1)] public string Username { get; set; } = "";
        [ProtoMember(2)] public string Password { get; set; } = "";
    }

    [ProtoContract]
    public class LoginRes : IMessage
    {
        public ushort MsgId => MsgIdConst.LoginRes;
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
    public class CreateRoleReq : IMessage
    {
        public ushort MsgId => MsgIdConst.CreateRoleReq;
        [ProtoMember(1)] public string Name { get; set; } = "";
        [ProtoMember(2)] public int Job { get; set; }
    }

    [ProtoContract]
    public class CreateRoleRes : IMessage
    {
        public ushort MsgId => MsgIdConst.CreateRoleRes;
        [ProtoMember(1)] public string ErrorMessage { get; set; } = "";
        [ProtoMember(2)] public RoleInfo Role { get; set; } = new RoleInfo();
    }

    // --- 进入游戏 ---

    [ProtoContract]
    public class EnterGameReq : IMessage
    {
        public ushort MsgId => MsgIdConst.EnterGameReq;
        [ProtoMember(1)] public long RoleId { get; set; }
    }

    [ProtoContract]
    public class EnterGameRes : IMessage
    {
        public ushort MsgId => MsgIdConst.EnterGameRes;
        [ProtoMember(1)] public string ErrorMessage { get; set; } = "";
        [ProtoMember(2)] public string Token { get; set; } = "";
    }

    // --- 断线重连 ---

    [ProtoContract]
    public class ReconnectReq : IMessage
    {
        public ushort MsgId => MsgIdConst.ReconnectReq;
        [ProtoMember(1)] public string Token { get; set; } = "";
        [ProtoMember(2)] public long RoleId { get; set; }
    }

    [ProtoContract]
    public class ReconnectRes : IMessage
    {
        public ushort MsgId => MsgIdConst.ReconnectRes;
        [ProtoMember(1)] public string ErrorMessage { get; set; } = "";
        [ProtoMember(2)] public string Token { get; set; } = "";
    }

    // --- 踢人通知 ---

    [ProtoContract]
    public class KickNtf : IMessage
    {
        public ushort MsgId => MsgIdConst.KickNtf;
        [ProtoMember(1)] public int Reason { get; set; }
    }

    // --- 进入游戏通知 ---

    [ProtoContract]
    public class EnterGameNtf : IMessage
    {
        public ushort MsgId => MsgIdConst.EnterGameNtf;
        [ProtoMember(1)] public long RoleId { get; set; }
        [ProtoMember(2)] public string Name { get; set; } = "";
        [ProtoMember(3)] public int Level { get; set; }
        [ProtoMember(4)] public int Job { get; set; }
    }
}
