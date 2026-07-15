using System.Threading.Tasks;
using Shared.Common.Protos;
using Shared.Core.Event;
using Shared.Core.Network;

namespace Client.Logic
{
    public sealed class TaskChangedNtfHandler : NtfMessageHandler<TaskChangedNtf>
    {
        protected override async Task ExecuteAsync(TcpSession session, TaskChangedNtf ntf)
        {
            if (ntf.Tasks == null)
                return;

            var controller = TaskController.Instance;
            foreach (var changed in ntf.Tasks)
            {
                int idx = controller.Tasks.FindIndex(t => t.TaskId == changed.TaskId);
                if (idx >= 0)
                    controller.Tasks[idx] = changed;
                else
                    controller.Tasks.Add(changed);
            }

            EventBus.Instance.Publish(new EventType.TaskDataChangedEvent());
            await Task.CompletedTask;
        }
    }
}
