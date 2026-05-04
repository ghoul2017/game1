namespace Shared.Common
{
    /// <summary>
    /// 消息ID常量，客户端和服务端共享
    /// 分段规则：每模块 100 个ID
    /// </summary>
    public static class MsgIdConst
    {
        // User 登录/角色 (10101-10200)
        public const ushort LoginReq = 10101;
        public const ushort LoginRes = 10102;
        public const ushort RoleListReq = 10103;
        public const ushort RoleListRes = 10104;
        public const ushort CreateRoleReq = 10105;
        public const ushort CreateRoleRes = 10106;
        public const ushort SelectRoleReq = 10107;
        public const ushort SelectRoleRes = 10108;
        public const ushort ReconnectReq = 10109;
        public const ushort ReconnectRes = 10110;
        public const ushort KickNtf = 10111;

        // Misc 杂项 (10201-10300)
        public const ushort HandshakeReq = 10201;
        public const ushort HandshakeRes = 10202;
        public const ushort HeartbeatReq = 10203;
        public const ushort HeartbeatRes = 10204;

        // Bag 背包 (10301-10400)
        public const ushort QueryBagReq = 10301;
        public const ushort QueryBagAck = 10302;
        public const ushort BagTidyReq = 10303;
        public const ushort BagTidyAck = 10304;
        public const ushort BagSwapReq = 10305;
        public const ushort BagSwapAck = 10306;
        public const ushort BagUseItemReq = 10307;
        public const ushort BagUseItemAck = 10308;
        public const ushort BagItemChangedNtf = 10309;

        // Mail 邮件 (10401-10500)
        public const ushort QueryMailListReq = 10401;
        public const ushort QueryMailListAck = 10402;
        public const ushort MailReadReq = 10403;
        public const ushort MailReadAck = 10404;
        public const ushort MailGetItemReq = 10405;
        public const ushort MailGetItemAck = 10406;
        public const ushort MailDeleteReq = 10407;
        public const ushort MailDeleteAck = 10408;
        public const ushort MailNewNtf = 10409;

        // Chat 聊天 (10501-10600)
        public const ushort ChatReq = 10501;
        public const ushort ChatAck = 10502;
        public const ushort ChatNtf = 10503;
        public const ushort SystemChatNtf = 10504;

        // Task 任务 (10601-10700)
        public const ushort QueryTaskListReq = 10601;
        public const ushort QueryTaskListAck = 10602;
        public const ushort TaskAcceptReq = 10603;
        public const ushort TaskAcceptAck = 10604;
        public const ushort TaskSubmitReq = 10605;
        public const ushort TaskSubmitAck = 10606;
        public const ushort TaskGiveUpReq = 10607;
        public const ushort TaskGiveUpAck = 10608;
        public const ushort TaskChangedNtf = 10609;

        // Map 地图 (10701-10800)
        public const ushort EnterMapReq = 10701;
        public const ushort EnterMapAck = 10702;
        public const ushort LeaveMapReq = 10703;
        public const ushort LeaveMapAck = 10704;
        public const ushort ChangeMapReq = 10705;
        public const ushort ChangeMapAck = 10706;
        public const ushort MapMoveReq = 10707;
        public const ushort MapMoveAck = 10708;
        public const ushort MapObjectsNtf = 10709;
        public const ushort MapPlayerEnterNtf = 10710;
        public const ushort MapPlayerLeaveNtf = 10711;
        public const ushort MapPlayerMoveNtf = 10712;
        public const ushort MapNpcEnterNtf = 10713;
        public const ushort MapNpcLeaveNtf = 10714;
        public const ushort MapNpcMoveNtf = 10715;
        public const ushort MonsterDropNtf = 10716;

        // Fight 战斗 (10801-10900)
        public const ushort UseSkillReq = 10801;
        public const ushort UseSkillAck = 10802;
        public const ushort KilledByNtf = 10803;
        public const ushort HpChangedNtf = 10804;
        public const ushort SetPkModeReq = 10805;
        public const ushort PkModeChangedNtf = 10806;
        public const ushort ExpChangedNtf = 10807;
        public const ushort GoldChangedNtf = 10808;
        public const ushort LevelUpNtf = 10809;
        public const ushort ItemGainNtf = 10810;
        public const ushort MpChangedNtf = 10811;
    }
}
