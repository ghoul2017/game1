using System;
using System.Collections.Generic;
using Shared.Common.Protos;

namespace Client.Logic
{
    public class TaskController
    {
        public static TaskController Instance { get; } = new();

        public List<TaskInfo> Tasks { get; private set; } = new();
        public int SelectedTaskId { get; private set; } = -1;

        public event Action OnDataChanged;

        public void QueryTaskList()
        {
            ClientContext.Network?.Send(new QueryTaskListReq());
        }

        public void SelectTask(int taskId)
        {
            SelectedTaskId = taskId;
            OnDataChanged?.Invoke();
        }

        public string ValidateAcceptInput(string text)
        {
            if (string.IsNullOrEmpty(text)) return "请输入任务ID";
            if (!int.TryParse(text, out int taskId) || taskId <= 0) return "无效的任务ID";
            return null;
        }

        public void AcceptTask(int taskId)
        {
            ClientContext.Network?.Send(new TaskAcceptReq { TaskId = taskId });
        }

        public void SubmitTask(int taskId)
        {
            ClientContext.Network?.Send(new TaskSubmitReq { TaskId = taskId });
        }

        public void GiveUpTask(int taskId)
        {
            ClientContext.Network?.Send(new TaskGiveUpReq { TaskId = taskId });
        }

        public void HandleQueryTaskListAck(QueryTaskListAck ack)
        {
            if (!string.IsNullOrEmpty(ack.ErrorMessage))
            {
                ClientContext.UI?.ShowToast(ack.ErrorMessage);
                return;
            }
            Tasks = ack.Tasks ?? new List<TaskInfo>();
            SelectedTaskId = -1;
            OnDataChanged?.Invoke();
        }

        public void HandleTaskAcceptAck(TaskAcceptAck ack)
        {
            if (!string.IsNullOrEmpty(ack.ErrorMessage))
            {
                ClientContext.UI?.ShowToast(ack.ErrorMessage);
                return;
            }
            if (ack.Task != null)
            {
                Tasks.Add(ack.Task);
                OnDataChanged?.Invoke();
                ClientContext.UI?.ShowToast("接取任务成功");
            }
        }

        public void HandleTaskSubmitAck(TaskSubmitAck ack)
        {
            if (!string.IsNullOrEmpty(ack.ErrorMessage))
            {
                ClientContext.UI?.ShowToast(ack.ErrorMessage);
                return;
            }
            Tasks.RemoveAll(t => t.TaskId == ack.TaskId);
            if (SelectedTaskId == ack.TaskId)
                SelectedTaskId = -1;
            OnDataChanged?.Invoke();

            if (ack.Rewards != null && ack.Rewards.Count > 0)
            {
                var parts = new List<string>();
                foreach (var r in ack.Rewards)
                    parts.Add("物品#" + r.ConfigId + " x" + r.Count);
                ClientContext.UI?.ShowToast("提交成功，获得: " + string.Join(", ", parts.ToArray()));
            }
            else
            {
                ClientContext.UI?.ShowToast("提交成功");
            }
        }

        public void HandleTaskGiveUpAck(TaskGiveUpAck ack)
        {
            if (!string.IsNullOrEmpty(ack.ErrorMessage))
            {
                ClientContext.UI?.ShowToast(ack.ErrorMessage);
                return;
            }
            Tasks.RemoveAll(t => t.TaskId == ack.TaskId);
            if (SelectedTaskId == ack.TaskId)
                SelectedTaskId = -1;
            OnDataChanged?.Invoke();
            ClientContext.UI?.ShowToast("已放弃任务");
        }

        public void HandleTaskChangedNtf(TaskChangedNtf ntf)
        {
            if (ntf.Tasks == null) return;
            foreach (var changed in ntf.Tasks)
            {
                int idx = Tasks.FindIndex(t => t.TaskId == changed.TaskId);
                if (idx >= 0)
                    Tasks[idx] = changed;
                else
                    Tasks.Add(changed);
            }
            OnDataChanged?.Invoke();
        }
    }
}
