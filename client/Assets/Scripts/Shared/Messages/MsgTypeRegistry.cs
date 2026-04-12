using System;
using System.Collections.Generic;

namespace Client.Shared.Messages
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
        /// 注册所有已知消息类型
        /// </summary>
        public static void Init()
        {
            // User (10101-10200)
            Register<LoginResp>(MsgId.LoginResp);
            Register<RoleListResp>(MsgId.RoleListResp);
            Register<CreateRoleResp>(MsgId.CreateRoleResp);
            Register<SelectRoleResp>(MsgId.SelectRoleResp);
            Register<ReconnectResp>(MsgId.ReconnectResp);
            Register<KickNtf>(MsgId.KickNtf);

            // Misc (10201-10300)
            Register<HandshakeResp>(MsgId.HandshakeResp);
            Register<HeartbeatResp>(MsgId.HeartbeatResp);

            // Bag (10301-10400)
            Register<QueryBagAck>(MsgId.QueryBagAck);
            Register<BagTidyAck>(MsgId.BagTidyAck);
            Register<BagSwapAck>(MsgId.BagSwapAck);
            Register<BagUseItemAck>(MsgId.BagUseItemAck);
            Register<BagItemChangedNtf>(MsgId.BagItemChangedNtf);

            // Mail (10401-10500)
            Register<QueryMailListAck>(MsgId.QueryMailListAck);
            Register<MailReadAck>(MsgId.MailReadAck);
            Register<MailGetItemAck>(MsgId.MailGetItemAck);
            Register<MailDeleteAck>(MsgId.MailDeleteAck);
            Register<MailNewNtf>(MsgId.MailNewNtf);

            // Chat (10501-10600)
            Register<ChatAck>(MsgId.ChatAck);
            Register<ChatNtf>(MsgId.ChatNtf);
            Register<SystemChatNtf>(MsgId.SystemChatNtf);

            // Task (10601-10700)
            Register<QueryTaskListAck>(MsgId.QueryTaskListAck);
            Register<TaskAcceptAck>(MsgId.TaskAcceptAck);
            Register<TaskSubmitAck>(MsgId.TaskSubmitAck);
            Register<TaskGiveUpAck>(MsgId.TaskGiveUpAck);
            Register<TaskChangedNtf>(MsgId.TaskChangedNtf);

            // Map (10701-10800)
            Register<EnterMapAck>(MsgId.EnterMapAck);
            Register<LeaveMapAck>(MsgId.LeaveMapAck);
            Register<ChangeMapAck>(MsgId.ChangeMapAck);
            Register<MapMoveAck>(MsgId.MapMoveAck);
            Register<MapObjectsNtf>(MsgId.MapObjectsNtf);
            Register<MapPlayerEnterNtf>(MsgId.MapPlayerEnterNtf);
            Register<MapPlayerLeaveNtf>(MsgId.MapPlayerLeaveNtf);
            Register<MapPlayerMoveNtf>(MsgId.MapPlayerMoveNtf);

            // Fight (10801-10900)
            Register<UseSkillAck>(MsgId.UseSkillAck);
            Register<HpChangedNtf>(MsgId.HpChangedNtf);
            Register<KilledByNtf>(MsgId.KilledByNtf);
            Register<PkModeChangedNtf>(MsgId.PkModeChangedNtf);
        }
    }
}
