using server.common.protos;
using Server.Core.Entity;
using Server.Core.Log;
using Server.Game.Player;

namespace Server.Game.TaskSystem;

/// <summary>
/// 任务管理器。提供任务接取、进度更新、完成判定、领奖等功能。
/// 支持事件驱动的任务进度更新（击杀怪物、收集物品等触发目标进度）。
/// </summary>
public class TaskManager
{
    private static readonly ILog Log = LogManager.GetLogger<TaskManager>();

    public static TaskManager Instance { get; } = new();

    /// <summary>每个角色同时进行的任务上限</summary>
    private const int MaxActiveTaskCount = 20;

    private TaskManager()
    {
    }

    /// <summary>加载角色任务列表</summary>
    public async Task<RoleTaskList> LoadTasksAsync(long roleId)
    {
        var taskList = await EntityFactory.Instance.GetAsync<RoleTaskList>(roleId);
        if (taskList == null)
        {
            taskList = new RoleTaskList { Id = roleId };
            await EntityFactory.Instance.InsertAsync(taskList);
            Log.Info($"创建新任务列表 RoleId:{roleId}");
        }

        return taskList;
    }

    /// <summary>保存任务列表（Write-Behind）</summary>
    public async Task SaveTasksAsync(RoleTaskList taskList)
    {
        await EntityFactory.Instance.UpdateAsync(taskList);
    }

    /// <summary>接取任务</summary>
    public TaskAcceptResult AcceptTask(RoleTaskList taskList, int taskId)
    {
        // 检查是否已接取
        if (taskList.GetByTaskId(taskId) != null)
            return TaskAcceptResult.Fail(TaskErrorCode.TaskAlreadyAccepted);

        // 检查任务数量上限
        var activeCount = taskList.Tasks.Count(t => t.IsInProgress);
        if (activeCount >= MaxActiveTaskCount)
            return TaskAcceptResult.Fail(TaskErrorCode.TaskFull);

        // TODO: 从配置表读取任务定义，创建目标列表
        // 目前先用空目标列表，等配置表完善后补充
        var task = new RoleTask
        {
            TaskId = taskId,
            State = 0,
            AcceptTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            CompleteTime = 0,
            Goals = [],
        };

        taskList.Tasks.Add(task);
        taskList.GetTaskMap()[taskId] = task;

        return TaskAcceptResult.Ok();
    }

    /// <summary>提交已完成的任务，领取奖励</summary>
    public TaskSubmitResult SubmitTask(RoleTaskList taskList, int taskId)
    {
        var task = taskList.GetByTaskId(taskId);
        if (task == null)
            return TaskSubmitResult.Fail(TaskErrorCode.TaskNotFound);

        if (!task.IsCompleted)
            return TaskSubmitResult.Fail(TaskErrorCode.TaskNotCompleted);

        if (task.IsClaimed)
            return TaskSubmitResult.Fail(TaskErrorCode.TaskAlreadyClaimed);

        // 标记为已领奖
        task.State = 2;

        // TODO: 从配置表读取奖励，发放到背包
        // 目前先记录日志，等背包+配置表联动完善后补充

        return TaskSubmitResult.Ok();
    }

    /// <summary>放弃任务</summary>
    public TaskGiveUpResult GiveUpTask(RoleTaskList taskList, int taskId)
    {
        var task = taskList.GetByTaskId(taskId);
        if (task == null)
            return TaskGiveUpResult.Fail(TaskErrorCode.TaskNotFound);

        if (task.IsCompleted)
            return TaskGiveUpResult.Fail(TaskErrorCode.TaskCompleted);

        taskList.Tasks.Remove(task);
        taskList.GetTaskMap().Remove(taskId);

        return TaskGiveUpResult.Ok();
    }

    /// <summary>
    /// 更新任务进度（事件驱动）。
    /// 当玩家完成某个游戏行为（击杀、收集等）时调用，自动匹配相关目标并更新进度。
    /// </summary>
    /// <param name="taskList">角色任务列表</param>
    /// <param name="goalType">目标类型</param>
    /// <param name="goalParam">目标参数（0=匹配所有同类型目标）</param>
    /// <param name="count">增加的数量</param>
    /// <returns>有进度变化的任务ID列表</returns>
    public List<int> UpdateProgress(RoleTaskList taskList, int goalType, int goalParam, int count)
    {
        var changedTaskIds = new List<int>();

        foreach (var task in taskList.Tasks)
        {
            if (!task.IsInProgress) continue;

            bool changed = false;
            foreach (var goal in task.Goals)
            {
                if (goal.GoalType != goalType) continue;
                if (goalParam != 0 && goal.GoalParam != goalParam) continue;
                if (goal.IsFinished) continue;

                goal.CurrentCount = Math.Min(goal.CurrentCount + count, goal.TargetCount);
                changed = true;
            }

            if (changed)
            {
                // 检查任务是否所有目标都完成
                if (task.Goals.All(g => g.IsFinished))
                {
                    task.State = 1; // 标记为已完成(待领奖)
                    task.CompleteTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    Log.Info($"任务完成 RoleId:{taskList.Id} TaskId:{task.TaskId}");
                }

                changedTaskIds.Add(task.TaskId);
            }
        }

        return changedTaskIds;
    }

    /// <summary>获取任务列表</summary>
    public List<RoleTask> GetTaskList(RoleTaskList taskList)
    {
        return taskList.Tasks.ToList();
    }

    /// <summary>转换为网络传输用的 TaskInfo</summary>
    public TaskInfo ToTaskInfo(RoleTask task)
    {
        return new TaskInfo
        {
            TaskId = task.TaskId,
            State = task.State,
            AcceptTime = task.AcceptTime,
            CompleteTime = task.CompleteTime,
            Goals = task.Goals.Select(g => new TaskGoalInfo
            {
                GoalType = g.GoalType,
                GoalParam = g.GoalParam,
                CurrentCount = g.CurrentCount,
                TargetCount = g.TargetCount,
            }).ToList(),
        };
    }

    /// <summary>推送任务变更通知给在线玩家</summary>
    public void NotifyTaskChanged(long roleId, List<int> changedTaskIds, RoleTaskList taskList)
    {
        var tasks = changedTaskIds
            .Select(taskList.GetByTaskId)
            .Where(t => t != null)
            .Select(t => ToTaskInfo(t!))
            .ToList();

        if (tasks.Count == 0) return;

        var ntf = new TaskChangedNtf { Tasks = tasks };
        PlayerManager.Instance.SendToPlayer(roleId, ntf);
    }
}

#region 结果类型

public record TaskAcceptResult(bool Success, int ErrorCode)
{
    public static TaskAcceptResult Ok() => new(true, 0);
    public static TaskAcceptResult Fail(int error) => new(false, error);
}

public record TaskSubmitResult(bool Success, int ErrorCode)
{
    public static TaskSubmitResult Ok() => new(true, 0);
    public static TaskSubmitResult Fail(int error) => new(false, error);
}

public record TaskGiveUpResult(bool Success, int ErrorCode)
{
    public static TaskGiveUpResult Ok() => new(true, 0);
    public static TaskGiveUpResult Fail(int error) => new(false, error);
}

public static class TaskErrorCode
{
    public const int Ok = 0;
    public const int InvalidParam = 3001;
    public const int TaskNotFound = 3002;
    public const int TaskAlreadyAccepted = 3003;
    public const int TaskFull = 3004;
    public const int TaskNotCompleted = 3005;
    public const int TaskAlreadyClaimed = 3006;
    public const int TaskCompleted = 3007;
}

#endregion