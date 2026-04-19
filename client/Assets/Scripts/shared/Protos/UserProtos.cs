using ProtoBuf;
using GameShared.Network;
using System.Collections.Generic;

namespace GameShared.Protos
{
    // --- 登录 ---

    [ProtoContract]
    public class LoginReq : IMessage
    {
        public ushort MsgId => GameShared.Protos.MsgId.LoginReq;
        [ProtoMember(1)] public string Username { get; set; } = "";
        [ProtoMember(2)] public string Password { get; set; } = "";
    }

    [ProtoContract]
    public class LoginResp : IMessage
    {
        public ushort MsgId => GameShared.Protos.MsgId.LoginResp;
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
        public ushort MsgId => GameShared.Protos.MsgId.RoleListReq;
    }

    [ProtoContract]
    public class RoleListResp : IMessage
    {
        public ushort MsgId => GameShared.Protos.MsgId.RoleListResp;
        [ProtoMember(1)] public int Code { get; set; }
        [ProtoMember(2)] public string Message { get; set; } = "";
        [ProtoMember(3)] public List<RoleInfo> Roles { get; set; } = new List<RoleInfo>();
    }

    // --- 创建角色 ---

    [ProtoContract]
    public class CreateRoleReq : IMessage
    {
        public ushort MsgId => GameShared.Protos.MsgId.CreateRoleReq;
        [ProtoMember(1)] public string Name { get; set; } = "";
        [ProtoMember(2)] public int Job { get; set; }
    }

    [ProtoContract]
    public class CreateRoleResp : IMessage
    {
        public ushort MsgId => GameShared.Protos.MsgId.CreateRoleResp;
        [ProtoMember(1)] public int Code { get; set; }
        [ProtoMember(2)] public string Message { get; set; } = "";
        [ProtoMember(3)] public RoleInfo Role { get; set; } = new RoleInfo();
    }

    // --- 选角入场 ---

    [ProtoContract]
    public class SelectRoleReq : IMessage
    {
        public ushort MsgId => GameShared.Protos.MsgId.SelectRoleReq;
        [ProtoMember(1)] public long RoleId { get; set; }
    }

    [ProtoContract]
    public class SelectRoleResp : IMessage
    {
        public ushort MsgId => GameShared.Protos.MsgId.SelectRoleResp;
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
        public ushort MsgId => GameShared.Protos.MsgId.ReconnectReq;
        [ProtoMember(1)] public string Token { get; set; } = "";
        [ProtoMember(2)] public long RoleId { get; set; }
    }

    [ProtoContract]
    public class ReconnectResp : IMessage
    {
        public ushort MsgId => GameShared.Protos.MsgId.ReconnectResp;
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
        public ushort MsgId => GameShared.Protos.MsgId.KickNtf;
        [ProtoMember(1)] public int Reason { get; set; }
    }
}
