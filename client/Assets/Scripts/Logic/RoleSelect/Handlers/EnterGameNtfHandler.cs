using System.Threading.Tasks;
using Shared.Common.Protos;
using Shared.Core.Event;
using Shared.Core.Network;

namespace Client.Logic
{
    public sealed class EnterGameNtfHandler : NtfMessageHandler<EnterGameNtf>
    {
        protected override async Task ExecuteAsync(TcpSession session, EnterGameNtf ntf)
        {
            var controller = RoleSelectController.Instance;
            GameData.CurrentRole = new RoleData
            {
                RoleId = ntf.RoleId,
                Name = ntf.Name,
                Level = ntf.Level,
                Job = GameData.GetJobName(ntf.Job)
            };
            BagController.Instance.ResetForRole(ntf.RoleId);

            // Robot 复用 Logic 消息注册，但不拥有 Unity 客户端的 NetClient 生命周期。
            if (NetClient.Instance == null)
            {
                await Task.CompletedTask;
                return;
            }

            _ = BagController.Instance.QueryBag();
            controller.StatusText = "正在进入地图...";
            controller.HasError = false;
            EventBus.Instance.Publish(new EventType.RoleSelectDataChangedEvent());

            try
            {
                EnterMapAck ack = await NetClient.Instance.CallAsync<EnterMapAck>(
                    new EnterMapReq { MapCfgId = 1, Line = 1 });

                if (string.IsNullOrEmpty(ack.ErrorMessage))
                {
                    controller.StatusText = "正在加载地图...";
                    controller.HasError = false;
                }
                else
                {
                    controller.StatusText = "进入地图失败: " + ack.ErrorMessage;
                    controller.HasError = true;
                }
            }
            catch (System.Exception ex)
            {
                controller.StatusText = "进入地图失败: " + ex.Message;
                controller.HasError = true;
            }

            EventBus.Instance.Publish(new EventType.RoleSelectDataChangedEvent());
        }
    }
}
