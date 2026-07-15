using System;
using System.Linq;
using System.Threading.Tasks;
using Shared.Common.Protos;

namespace Robot
{
    /// <summary>
    /// 任务持久化回归：接取、进度、提交奖励和断线重登恢复。
    /// </summary>
    public class TaskPersistenceTest : TestScenario
    {
        private const int TaskId = 1;
        private const string Password = "123456";
        private const int RewardItemId = 1001;
        private const int DisconnectSaveWaitMs = 1_000;
        private readonly string _username;
        private readonly string _roleName;

        public TaskPersistenceTest()
        {
            var runId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            _username = $"robot_task_{runId}";
            _roleName = $"任务回归{runId[^8..]}";
        }

        public override string Name => "task_persistence_test";

        public override async Task Run()
        {
            var role = await LoginAndEnterAsync();
            var initialList = await Session.CallAsync<QueryTaskListAck>(new QueryTaskListReq());
            Assertions.True(string.IsNullOrEmpty(initialList.ErrorMessage), $"首次查询任务列表应成功: {initialList.ErrorMessage}");

            Assertions.True(initialList.Tasks.All(t => t.TaskId != TaskId), "独立回归账号不应存在遗留任务");

            var acceptAck = await Session.CallAsync<TaskAcceptAck>(new TaskAcceptReq { TaskId = TaskId });
            Assertions.True(string.IsNullOrEmpty(acceptAck.ErrorMessage), $"接取任务应成功: {acceptAck.ErrorMessage}");
            Assertions.Equal(TaskId, acceptAck.Task.TaskId, "接取响应应返回目标任务");
            Assertions.Equal(0, acceptAck.Task.State, "新接任务应处于进行中状态");

            var duplicateAck = await Session.CallAsync<TaskAcceptAck>(new TaskAcceptReq { TaskId = TaskId });
            Assertions.Equal("任务已接取", duplicateAck.ErrorMessage, "重复接取应返回稳定业务错误");

            await DisconnectAndWaitForBatchSaveAsync();
            await Session.ReconnectAsync();
            await LoginAndEnterAsync(role.RoleId);

            var reloadedList = await Session.CallAsync<QueryTaskListAck>(new QueryTaskListReq());
            Assertions.True(string.IsNullOrEmpty(reloadedList.ErrorMessage), $"重登查询任务列表应成功: {reloadedList.ErrorMessage}");
            var reloadedTask = reloadedList.Tasks.SingleOrDefault(t => t.TaskId == TaskId);
            Assertions.NotNull(reloadedTask, "重登后应恢复已接取任务");
            Assertions.Equal(acceptAck.Task.AcceptTime, reloadedTask!.AcceptTime, "重登后接取时间应来自持久化数据");

            var mapObjectsTask = Session.WaitForAsync<MapObjectsNtf>();
            var enterMapAck = await Session.CallAsync<EnterMapAck>(new EnterMapReq { MapCfgId = 1, Line = 1 });
            Assertions.True(string.IsNullOrEmpty(enterMapAck.ErrorMessage), $"进入新手村应成功: {enterMapAck.ErrorMessage}");
            var mapObjects = await mapObjectsTask;
            var target = mapObjects.Objects.FirstOrDefault(obj => obj.CfgId == 101);
            Assertions.NotNull(target, "新手村视野内应存在野猪 101");

            var progress = 0;
            for (var attackCount = 0; attackCount < 80 && progress < 5; attackCount++)
            {
                if (attackCount > 0)
                {
                    await Task.Delay(550);
                    var refreshedObjectsTask = Session.WaitForAsync<MapObjectsNtf>();
                    var refreshAck = await Session.CallAsync<EnterMapAck>(new EnterMapReq { MapCfgId = 1, Line = 1 });
                    Assertions.True(string.IsNullOrEmpty(refreshAck.ErrorMessage), $"刷新地图视野应成功: {refreshAck.ErrorMessage}");
                    var refreshedObjects = await refreshedObjectsTask;
                    target = refreshedObjects.Objects.FirstOrDefault(obj => obj.ObjectId == target?.ObjectId)
                        ?? refreshedObjects.Objects.FirstOrDefault(obj => obj.CfgId == 101);
                    Assertions.NotNull(target, "新手村视野内应有可继续攻击的野猪 101");
                }

                var moveAck = await Session.CallAsync<MapMoveAck>(new MapMoveReq { X = target!.X, Y = target.Y });
                Assertions.True(string.IsNullOrEmpty(moveAck.ErrorMessage), $"移动到野猪位置应成功: {moveAck.ErrorMessage}");
                var skillAck = await Session.CallAsync<UseSkillAck>(new UseSkillReq
                {
                    SkillId = 1,
                    TargetId = target.ObjectId,
                    X = target.X,
                    Y = target.Y,
                });
                Assertions.True(string.IsNullOrEmpty(skillAck.ErrorMessage), $"攻击野猪应成功: {skillAck.ErrorMessage}");

                var currentList = await Session.CallAsync<QueryTaskListAck>(new QueryTaskListReq());
                var currentTask = currentList.Tasks.Single(t => t.TaskId == TaskId);
                progress = currentTask.Goals.Single(goal => goal.GoalType == 1 && goal.GoalParam == 101).CurrentCount;
            }

            var progressedList = await Session.CallAsync<QueryTaskListAck>(new QueryTaskListReq());
            var progressedTask = progressedList.Tasks.Single(t => t.TaskId == TaskId);
            var killGoal = progressedTask.Goals.Single(goal => goal.GoalType == 1 && goal.GoalParam == 101);
            Assertions.Equal(5, killGoal.CurrentCount, "击杀 5 只野猪后任务进度应达到目标");
            Assertions.Equal(1, progressedTask.State, "达到目标后任务应处于已完成待提交状态");

            await DisconnectAndWaitForBatchSaveAsync();
            await Session.ReconnectAsync();
            await LoginAndEnterAsync(role.RoleId);
            var progressReloadedList = await Session.CallAsync<QueryTaskListAck>(new QueryTaskListReq());
            var progressReloadedTask = progressReloadedList.Tasks.Single(t => t.TaskId == TaskId);
            var progressReloadedGoal = progressReloadedTask.Goals.Single(goal => goal.GoalType == 1 && goal.GoalParam == 101);
            Assertions.Equal(killGoal.CurrentCount, progressReloadedGoal.CurrentCount, "再次重登后应恢复任务进度");

            var bagBeforeSubmit = await Session.CallAsync<QueryBagAck>(new QueryBagReq());
            Assertions.True(string.IsNullOrEmpty(bagBeforeSubmit.ErrorMessage), $"提交前查询背包应成功: {bagBeforeSubmit.ErrorMessage}");
            var rewardCountBeforeSubmit = bagBeforeSubmit.Items
                .Where(item => item.ConfigId == RewardItemId)
                .Sum(item => item.Count);

            var submitAck = await Session.CallAsync<TaskSubmitAck>(new TaskSubmitReq { TaskId = TaskId });
            Assertions.True(string.IsNullOrEmpty(submitAck.ErrorMessage), $"提交任务应成功: {submitAck.ErrorMessage}");
            var reward = submitAck.Rewards.SingleOrDefault(item => item.ConfigId == RewardItemId);
            Assertions.NotNull(reward, $"提交响应应包含物品奖励 {RewardItemId}");
            Assertions.Equal(1, reward!.Count, "提交响应中的奖励数量应与任务配置一致");

            var duplicateSubmitAck = await Session.CallAsync<TaskSubmitAck>(new TaskSubmitReq { TaskId = TaskId });
            Assertions.Equal("任务已提交", duplicateSubmitAck.ErrorMessage, "重复提交应返回稳定业务错误");

            var bagAck = await Session.CallAsync<QueryBagAck>(new QueryBagReq());
            Assertions.True(string.IsNullOrEmpty(bagAck.ErrorMessage), $"查询背包应成功: {bagAck.ErrorMessage}");
            var rewardCount = bagAck.Items.Where(item => item.ConfigId == RewardItemId).Sum(item => item.Count);
            Assertions.Equal(rewardCountBeforeSubmit + 1, rewardCount, "首次提交应在已有掉落基础上增加一份任务奖励");

            await DisconnectAndWaitForBatchSaveAsync();
            await Session.ReconnectAsync();
            await LoginAndEnterAsync(role.RoleId);
            var submittedList = await Session.CallAsync<QueryTaskListAck>(new QueryTaskListReq());
            var submittedTask = submittedList.Tasks.Single(t => t.TaskId == TaskId);
            Assertions.Equal(2, submittedTask.State, "领奖后重登应恢复已提交状态");
            var reloadedBag = await Session.CallAsync<QueryBagAck>(new QueryBagReq());
            var reloadedRewardCount = reloadedBag.Items.Where(item => item.ConfigId == RewardItemId).Sum(item => item.Count);
            Assertions.Equal(rewardCount, reloadedRewardCount, "领奖后重登应恢复奖励且不得重复发放");

            Log($"任务持久化验证完成: Username={_username}, RoleId={role.RoleId}, TaskId={TaskId}, Progress={killGoal.CurrentCount}, Reward={RewardItemId}:{rewardCount}");
        }

        private async Task DisconnectAndWaitForBatchSaveAsync()
        {
            Session.Disconnect();
            await Task.Delay(DisconnectSaveWaitMs);
        }

        private async Task<RoleInfo> LoginAndEnterAsync(long expectedRoleId = 0)
        {
            var handshakeRes = await Session.CallAsync<HandshakeRes>(new HandshakeReq());
            Assertions.True(string.IsNullOrEmpty(handshakeRes.ErrorMessage), $"握手应成功: {handshakeRes.ErrorMessage}");

            var loginRes = await Session.CallAsync<LoginRes>(new LoginReq
            {
                Username = _username,
                Password = Password,
            });
            Assertions.True(string.IsNullOrEmpty(loginRes.ErrorMessage), $"登录应成功: {loginRes.ErrorMessage}");
            Session.UserId = loginRes.PlayerId;

            var role = expectedRoleId == 0
                ? loginRes.Roles.FirstOrDefault()
                : loginRes.Roles.SingleOrDefault(r => r.RoleId == expectedRoleId);

            if (role == null)
            {
                var createRes = await Session.CallAsync<CreateRoleRes>(new CreateRoleReq
                {
                    Name = _roleName,
                    Job = 0,
                });
                Assertions.True(string.IsNullOrEmpty(createRes.ErrorMessage), $"创建回归角色应成功: {createRes.ErrorMessage}");
                role = createRes.Role;
            }

            var enterRes = await Session.CallAsync<EnterGameRes>(new EnterGameReq { RoleId = role.RoleId });
            Assertions.True(string.IsNullOrEmpty(enterRes.ErrorMessage), $"进入游戏应成功: {enterRes.ErrorMessage}");
            Session.RoleId = role.RoleId;
            return role;
        }
    }
}
