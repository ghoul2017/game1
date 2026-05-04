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
        [ProtoMember(1)] public int Code { get; set; }
        [ProtoMember(2)] public string Message { get; set; } = "";
        [ProtoMember(3)] public long PlayerId { get; set; }
        [ProtoMember(4)] public string Token { get; set; } = "";
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

    // --- 角色列表 ---

    [ProtoContract]
    public class RoleListReq : IMessage
    {
        public ushort MsgId => MsgIdConst.RoleListReq;
    }

    [ProtoContract]
    public class RoleListRes : IMessage
    {
        public ushort MsgId => MsgIdConst.RoleListRes;
        [ProtoMember(1)] public int Code { get; set; }
        [ProtoMember(2)] public string Message { get; set; } = "";
        [ProtoMember(3)] public List<RoleInfo> Roles { get; set; } = new List<RoleInfo>();
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
        [ProtoMember(1)] public int Code { get; set; }
        [ProtoMember(2)] public string Message { get; set; } = "";
        [ProtoMember(3)] public RoleInfo Role { get; set; } = new RoleInfo();
    }

    // --- 选角入场 ---

    [ProtoContract]
    public class SelectRoleReq : IMessage
    {
        public ushort MsgId => MsgIdConst.SelectRoleReq;
        [ProtoMember(1)] public long RoleId { get; set; }
    }

    [ProtoContract]
    public class SelectRoleRes : IMessage
    {
        public ushort MsgId => MsgIdConst.SelectRoleRes;
        [ProtoMember(1)] public int Code { get; set; }
        [ProtoMember(2)] public string Message { get; set; } = "";
        [ProtoMember(3)] public long RoleId { get; set; }
        [ProtoMember(4)] public string Name { get; set; } = "";
        [ProtoMember(5)] public int Level { get; set; }
        [ProtoMember(6)] public int Job { get; set; }
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
        [ProtoMember(1)] public int Code { get; set; }
        [ProtoMember(2)] public string Message { get; set; } = "";
        [ProtoMember(3)] public long RoleId { get; set; }
        [ProtoMember(4)] public string Name { get; set; } = "";
        [ProtoMember(5)] public int Level { get; set; }
    }

    // --- 踢人通知 ---

    [ProtoContract]
    public class KickNtf : IMessage
    {
        public ushort MsgId => MsgIdConst.KickNtf;
        [ProtoMember(1)] public int Reason { get; set; }
    }
}
