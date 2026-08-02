namespace Client.Logic
{
    /// <summary>
    /// 保存本次客户端进程选择的产品入口。它只影响进入角色后的客户端路由，不改变服务端身份或玩法权威。
    /// </summary>
    public static class ClientRuntimeMode
    {
        public static bool UsesOnlineExpeditionEntry { get; private set; }

        public static void SetOnlineExpeditionEntry(bool enabled)
        {
            UsesOnlineExpeditionEntry = enabled;
        }
    }
}
