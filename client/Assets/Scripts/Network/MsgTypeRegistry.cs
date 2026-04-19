using System;
using System.Collections.Generic;
using Shared.Common;
using Shared.Core.Network;
using Shared.Common.Protos;

namespace Client.Network
{
    /// <summary>
    /// MsgId → 消息类型映射，用于服务端推送消息的反序列化
    /// </summary>
    public static class MsgTypeRegistry
    {
        private static readonly Dictionary<ushort, Type> _types = new Dictionary<ushort, Type>();

        public static void Register<T>(ushort msgId) where T : IMessage
        {
            _types[msgId] = typeof(T);
        }

        public static Type GetType(ushort msgId)
        {
            return _types.TryGetValue(msgId, out var type) ? type : null;
        }

        /// <summary>
        /// 反序列化消息（根据 MsgId 自动查找类型）
        /// </summary>
        public static IMessage Deserialize(ushort msgId, byte[] payload)
        {
            var type = GetType(msgId);
            if (type == null) return null;
            using var ms = new System.IO.MemoryStream(payload);
            return (IMessage)ProtoBuf.Serializer.NonGeneric.Deserialize(type, ms);
        }

        /// <summary>
        /// 注册所有已知消息类型
        /// </summary>
        public static void Init()
        {
            // User (10101-10200)
            Register<LoginResp>(MsgIdConst.LoginResp);
            Register<RoleListResp>(MsgIdConst.RoleListResp);
            Register<CreateRoleResp>(MsgIdConst.CreateRoleResp);
            Register<SelectRoleResp>(MsgIdConst.SelectRoleResp);
            Register<ReconnectResp>(MsgIdConst.ReconnectResp);
            Register<KickNtf>(MsgIdConst.KickNtf);

            // Misc (10201-10300)
            Register<HandshakeResp>(MsgIdConst.HandshakeResp);
            Register<HeartbeatResp>(MsgIdConst.HeartbeatResp);

            // Bag (10301-10400)
            Register<QueryBagAck>(MsgIdConst.QueryBagAck);
            Register<BagTidyAck>(MsgIdConst.BagTidyAck);
            Register<BagSwapAck>(MsgIdConst.BagSwapAck);
            Register<BagUseItemAck>(MsgIdConst.BagUseItemAck);
            Register<BagItemChangedNtf>(MsgIdConst.BagItemChangedNtf);

            // Mail (10401-10500)
            Register<QueryMailListAck>(MsgIdConst.QueryMailListAck);
            Register<MailReadAck>(MsgIdConst.MailReadAck);
            Register<MailGetItemAck>(MsgIdConst.MailGetItemAck);
            Register<MailDeleteAck>(MsgIdConst.MailDeleteAck);
            Register<MailNewNtf>(MsgIdConst.MailNewNtf);

            // Chat (10501-10600)
            Register<ChatAck>(MsgIdConst.ChatAck);
            Register<ChatNtf>(MsgIdConst.ChatNtf);
            Register<SystemChatNtf>(MsgIdConst.SystemChatNtf);

            // Task (10601-10700)
            Register<QueryTaskListAck>(MsgIdConst.QueryTaskListAck);
            Register<TaskAcceptAck>(MsgIdConst.TaskAcceptAck);
            Register<TaskSubmitAck>(MsgIdConst.TaskSubmitAck);
            Register<TaskGiveUpAck>(MsgIdConst.TaskGiveUpAck);
            Register<TaskChangedNtf>(MsgIdConst.TaskChangedNtf);

            // Map (10701-10800)
            Register<EnterMapAck>(MsgIdConst.EnterMapAck);
            Register<LeaveMapAck>(MsgIdConst.LeaveMapAck);
            Register<ChangeMapAck>(MsgIdConst.ChangeMapAck);
            Register<MapMoveAck>(MsgIdConst.MapMoveAck);
            Register<MapObjectsNtf>(MsgIdConst.MapObjectsNtf);
            Register<MapPlayerEnterNtf>(MsgIdConst.MapPlayerEnterNtf);
            Register<MapPlayerLeaveNtf>(MsgIdConst.MapPlayerLeaveNtf);
            Register<MapPlayerMoveNtf>(MsgIdConst.MapPlayerMoveNtf);

            // Fight (10801-10900)
            Register<UseSkillAck>(MsgIdConst.UseSkillAck);
            Register<HpChangedNtf>(MsgIdConst.HpChangedNtf);
            Register<KilledByNtf>(MsgIdConst.KilledByNtf);
            Register<PkModeChangedNtf>(MsgIdConst.PkModeChangedNtf);
        }
    }
}
