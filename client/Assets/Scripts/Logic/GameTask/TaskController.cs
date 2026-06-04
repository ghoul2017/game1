using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shared.Common.Protos;

namespace Client.Logic
{
    public class TaskController
    {
        public static TaskController Instance { get; } = new();

        public List<TaskInfo> Tasks { get; private set; } = new();
        public int SelectedTaskId { get; private set; } = -1;

        public event Action OnDataChanged;

        public async Task QueryTaskList()
        {
            try
            {
                QueryTaskListAck ack = await NetClient.Instance.CallAsync<QueryTaskListAck>(new QueryTaskListReq());
                if (!string.IsNullOrEmpty(ack.ErrorMessage))
                {
                    Context.UI?.ShowToast(ack.ErrorMessage);
                    return;
                }
                Tasks = ack.Tasks ?? new List<TaskInfo>();
                SelectedTaskId = -1;
                OnDataChanged?.Invoke();
            }
            catch (Exception ex)
            {
                Context.UI?.ShowToast("查询任务失败: " + ex.Message);
            }
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

        public async Task AcceptTask(int taskId)
        {
            try
            {
                TaskAcceptAck ack = await NetClient.Instance.CallAsync<TaskAcceptAck>(
                    new TaskAcceptReq { TaskId = taskId });
                if (!string.IsNullOrEmpty(ack.ErrorMessage))
                {
                    Context.UI?.ShowToast(ack.ErrorMessage);
                    return;
                }
                if (ack.Task != null)
                {
                    Tasks.Add(ack.Task);
                    OnDataChanged?.Invoke();
                    Context.UI?.ShowToast("接取任务成功");
                }
            }
            catch (Exception ex)
            {
                Context.UI?.ShowToast("接取任务失败: " + ex.Message);
            }
        }

        public async Task SubmitTask(int taskId)
        {
            try
            {
                TaskSubmitAck ack = await NetClient.Instance.CallAsync<TaskSubmitAck>(
                    new TaskSubmitReq { TaskId = taskId });
                if (!string.IsNullOrEmpty(ack.ErrorMessage))
                {
                    Context.UI?.ShowToast(ack.ErrorMessage);
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
                    Context.UI?.ShowToast("提交成功，获得: " + string.Join(", ", parts.ToArray()));
                }
                else
                {
                    Context.UI?.ShowToast("提交成功");
                }
            }
            catch (Exception ex)
            {
                Context.UI?.ShowToast("提交任务失败: " + ex.Message);
            }
        }

        public async Task GiveUpTask(int taskId)
        {
            try
            {
                TaskGiveUpAck ack = await NetClient.Instance.CallAsync<TaskGiveUpAck>(
                    new TaskGiveUpReq { TaskId = taskId });
                if (!string.IsNullOrEmpty(ack.ErrorMessage))
                {
                    Context.UI?.ShowToast(ack.ErrorMessage);
                    return;
                }
                Tasks.RemoveAll(t => t.TaskId == ack.TaskId);
                if (SelectedTaskId == ack.TaskId)
                    SelectedTaskId = -1;
                OnDataChanged?.Invoke();
                Context.UI?.ShowToast("已放弃任务");
            }
            catch (Exception ex)
            {
                Context.UI?.ShowToast("放弃任务失败: " + ex.Message);
            }
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
