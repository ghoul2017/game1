using System.Threading.Tasks;
using Shared.Common.Protos;
using Shared.Core.Network;

namespace Client.Logic
{
    public sealed class TaskChangedNtfHandler : NtfMessageHandler<TaskChangedNtf>
    {
        protected override Task ExecuteAsync(TcpSession session, TaskChangedNtf ntf)
        {
            TaskController.Instance.HandleTaskChangedNtf(ntf);
            return Task.CompletedTask;
        }
    }
}
