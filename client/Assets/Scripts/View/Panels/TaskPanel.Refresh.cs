using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Shared.Common.Protos;
using Client.Logic;

namespace Client.View
{
    public partial class TaskPanel
    {
        // ==================== UI Refresh ====================

        internal void RefreshList()
        {
            var tasks = TaskController.Instance.Tasks;

            _taskCountText.text = tasks.Count + " 个";

            // 清理旧条目
            for (int i = _listContent.transform.childCount - 1; i >= 0; i--)
                Destroy(_listContent.transform.GetChild(i).gameObject);

            float totalH = tasks.Count * (TASK_ITEM_H + TASK_ITEM_GAP);
            var lcRect = _listContent.GetComponent<RectTransform>();
            lcRect.sizeDelta = new Vector2(0, totalH);

            for (int i = 0; i < tasks.Count; i++)
            {
                var task = tasks[i];
                var item = CreateTaskListItem(_listContent.transform, task, i);
                var itemRect = item.GetComponent<RectTransform>();
                itemRect.anchoredPosition = new Vector2(0, -i * (TASK_ITEM_H + TASK_ITEM_GAP));
            }
        }

        private GameObject CreateTaskListItem(Transform parent, TaskInfo task, int index)
        {
            bool isSelected = task.TaskId == TaskController.Instance.SelectedTaskId;
            string stateText = GetStateText(task.State);
            Color stateColor = GetStateColor(task.State);

            var item = new GameObject("Task_" + index);
            item.transform.SetParent(parent, false);
            var rect = item.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, TASK_ITEM_H);
            rect.pivot = new Vector2(0.5f, 1);
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);

            var bg = item.AddComponent<Image>();
            bg.color = isSelected
                ? new Color(0.2f, 0.18f, 0.35f, 0.95f)
                : new Color(0.1f, 0.1f, 0.22f, 0.95f);

            var btn = item.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f);
            colors.pressedColor = new Color(0.9f, 0.9f, 0.9f);
            colors.fadeDuration = 0.1f;
            btn.colors = colors;

            // 标题：任务名和任务ID
            var title = UIBuilder.CreateText(item.transform, "Title", "任务 #" + task.TaskId, 14,
                UIBuilder.TextWhite, TextAnchor.MiddleLeft, FontStyle.Bold);
            var tRect = title.GetComponent<RectTransform>();
            tRect.anchorMin = new Vector2(0, 0.5f);
            tRect.anchorMax = new Vector2(1, 1);
            tRect.offsetMin = new Vector2(10, 0);
            tRect.offsetMax = new Vector2(-10, -2);

            // 状态标签
            var state = UIBuilder.CreateText(item.transform, "State", stateText, 12,
                stateColor, TextAnchor.MiddleLeft);
            var sRect = state.GetComponent<RectTransform>();
            sRect.anchorMin = new Vector2(0, 0);
            sRect.anchorMax = new Vector2(1, 0.5f);
            sRect.offsetMin = new Vector2(10, 2);
            sRect.offsetMax = new Vector2(-10, 0);

            // 进度提示：已完成目标数
            if (task.Goals != null && task.Goals.Count > 0)
            {
                int done = 0;
                foreach (var g in task.Goals)
                    if (g.CurrentCount >= g.TargetCount) done++;
                string progress = done + "/" + task.Goals.Count;
                var progText = UIBuilder.CreateText(item.transform, "Progress", progress, 12,
                    UIBuilder.TextDim, TextAnchor.MiddleRight);
                var pRect = progText.GetComponent<RectTransform>();
                pRect.anchorMin = new Vector2(1, 0);
                pRect.anchorMax = new Vector2(1, 1);
                pRect.pivot = new Vector2(1, 0.5f);
                pRect.sizeDelta = new Vector2(40, 24);
                pRect.anchoredPosition = new Vector2(-6, 0);
            }

            int taskId = task.TaskId;
            btn.onClick.AddListener(() => OnTaskItemClick(taskId));

            return item;
        }

        private void OnTaskItemClick(int taskId)
        {
            TaskController.Instance.SelectTask(taskId);
        }

        internal void RefreshDetail()
        {
            var task = TaskController.Instance.Tasks.Find(t => t.TaskId == TaskController.Instance.SelectedTaskId);
            if (task == null)
            {
                ClearDetail();
                return;
            }

            _detailTitle.text = "任务 #" + task.TaskId;
            _detailState.text = "状态: " + GetStateText(task.State);
            _detailState.color = GetStateColor(task.State);

            // 目标
            if (task.Goals != null && task.Goals.Count > 0)
            {
                var lines = new List<string>();
                foreach (var g in task.Goals)
                {
                    string goalName = GetGoalTypeName(g.GoalType);
                    if (g.GoalParam > 0) goalName += " " + g.GoalParam;
                    string check = g.CurrentCount >= g.TargetCount ? "<color=#88FF88>[完成]</color>" : "";
                    lines.Add(string.Format("  {0}  {1}/{2} {3}",
                        goalName, g.CurrentCount, g.TargetCount, check));
                }
                _detailGoals.text = "目标:\n" + string.Join("\n", lines.ToArray());
            }
            else
            {
                _detailGoals.text = "目标: 无";
            }

            // 时间
            var timeParts = new List<string>();
            if (task.AcceptTime > 0)
                timeParts.Add("接取: " + FormatTime(task.AcceptTime));
            if (task.CompleteTime > 0)
                timeParts.Add("完成: " + FormatTime(task.CompleteTime));
            _detailTime.text = string.Join("  ", timeParts.ToArray());

            // 更新按钮状态
            _submitBtn.interactable = (task.State == 1);
            _giveUpBtn.interactable = (task.State == 0);
        }

        private void ClearDetail()
        {
            _detailTitle.text = "选择一个任务查看详情";
            _detailState.text = "";
            _detailGoals.text = "";
            _detailTime.text = "";
            _submitBtn.interactable = false;
            _giveUpBtn.interactable = false;
        }

        // ==================== Actions ====================

        private void OnAcceptClick()
        {
            if (_acceptInput == null)
                return;
            string text = _acceptInput.text.Trim();
            string error = TaskController.Instance.ValidateAcceptInput(text);
            if (error != null)
            {
                UIToast.Show(error);
                return;
            }
            int taskId = int.Parse(text);
            TaskController.Instance.AcceptTask(taskId);
            _acceptInput.text = "";
        }

        private void OnSubmitClick()
        {
            int selectedTaskId = TaskController.Instance.SelectedTaskId;
            if (selectedTaskId <= 0)
            {
                UIToast.Show("请先选择一个任务");
                return;
            }
            TaskController.Instance.SubmitTask(selectedTaskId);
        }

        private void OnGiveUpClick()
        {
            int selectedTaskId = TaskController.Instance.SelectedTaskId;
            if (selectedTaskId <= 0)
            {
                UIToast.Show("请先选择一个任务");
                return;
            }
            UIMessageBox.Show("放弃任务", "确定要放弃这个任务吗？进度将丢失。",
                () => TaskController.Instance.GiveUpTask(selectedTaskId),
                null, "确定", "取消", true);
        }

        private void OnCloseClick()
        {
            UIManager.Instance.Back();
        }

        // ==================== Utility ====================

        private static GameObject CreateSection(Transform parent, string name, float width, float height)
        {
            return UIBuilder.CreatePanel(parent, name, width, height, new Color(0.06f, 0.06f, 0.12f, 0.9f));
        }

        private static string GetStateText(int state)
        {
            switch (state)
            {
                case 0: return "进行中";
                case 1: return "可提交";
                case 2: return "已完成";
                default: return "未知";
            }
        }

        private static Color GetStateColor(int state)
        {
            switch (state)
            {
                case 0: return UIBuilder.TextWhite;
                case 1: return UIBuilder.Gold;
                case 2: return UIBuilder.TextDim;
                default: return UIBuilder.TextGray;
            }
        }

        private static string GetGoalTypeName(int goalType)
        {
            switch (goalType)
            {
                case 1: return "击杀怪物";
                case 2: return "收集物品";
                case 3: return "对话NPC";
                case 4: return "达到等级";
                case 5: return "进入场景";
                case 6: return "使用物品";
                case 7: return "击杀玩家";
                case 8: return "通关副本";
                default: return "目标#" + goalType;
            }
        }

        private static string FormatTime(long timestamp)
        {
            if (timestamp <= 0)
                return "";
            var dt = new System.DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc);
            dt = dt.AddMilliseconds(timestamp).ToLocalTime();
            return dt.ToString("yyyy-MM-dd HH:mm");
        }
    }
}
