using System.Threading.Tasks;
using Shared.Core.Event;

namespace Client.Logic
{
    /// <summary>网络会话和 Player 重绑完成后，恢复同一进程中保留的权威远征局。</summary>
    public sealed class NetReconnectedExpeditionEventHandler : ISyncEventHandler<EventType.NetReconnectedEvent>
    {
        public void Handle(EventType.NetReconnectedEvent value)
        {
            if (ClientRuntimeMode.UsesOnlineExpeditionEntry)
            {
                // 在线入口由重连后的 EnterGameNtf 唯一触发，避免并发 Resume 覆盖较新响应。
                return;
            }

            _ = ResumeAsync();
        }

        private static async Task ResumeAsync()
        {
            ExpeditionController controller = ExpeditionController.Instance;
            long runId = controller.LatestSnapshot == null ? 0L : controller.LatestSnapshot.RunId;
            await controller.ResumeAsync(runId);
        }
    }
}
