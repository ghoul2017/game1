using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Client.Core;
using Client.Network;
using Shared.Common;
using Shared.Common.Protos;

namespace Client.Game
{
    public class TaskPanel : UIPanel
    {
        private const float TASK_ITEM_H = 55f;
        private const float TASK_ITEM_GAP = 4f;

        private List<TaskInfo> _tasks = new List<TaskInfo>();
        private int _selectedTaskId = -1;

        // List area
        private GameObject _listContent;
        private Text _taskCountText;

        // Detail area
        private Text _detailTitle;
        private Text _detailState;
        private Text _detailGoals;
        private Text _detailTime;

        // Accept input
        private InputField _acceptInput;

        // Buttons
        private Button _submitBtn;
        private Button _giveUpBtn;

        public override void OnInit()
        {
            UIBuilder.CreateFullScreenBg(transform, new Color(0, 0, 0, 0.6f));

            float panelW = 780f;
            float panelH = 600f;
            var (panelRoot, content) = UIBuilder.CreateBorderedPanel(transform, "TaskCard",
                panelW, panelH, UIBuilder.PanelBg, UIBuilder.GoldDark, 2f);
            panelRoot.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;

            // Title bar
            var titleBar = UIBuilder.CreateTitleBar(content, "TitleBar", "任 务", panelW, 50);
            var tbRect = titleBar.GetComponent<RectTransform>();
            tbRect.anchorMin = new Vector2(0, 1);
            tbRect.anchorMax = new Vector2(1, 1);
            tbRect.pivot = new Vector2(0.5f, 1);
            tbRect.offsetMin = new Vector2(0, -50);
            tbRect.offsetMax = Vector2.zero;

            _taskCountText = UIBuilder.CreateText(titleBar.transform, "CountText", "0 个", 16,
                UIBuilder.TextDim, TextAnchor.MiddleRight);
            var ctRect = _taskCountText.GetComponent<RectTransform>();
            ctRect.anchorMin = new Vector2(1, 0);
            ctRect.anchorMax = new Vector2(1, 1);
            ctRect.pivot = new Vector2(1, 0.5f);
            ctRect.offsetMin = new Vector2(-80, 0);
            ctRect.offsetMax = new Vector2(-10, 0);

            // Main body: left list + right detail
            float bodyTop = -56f;
            float bodyH = panelH - 56f - 60f;
            float listW = 300f;

            // Left: task list with scroll
            var listArea = CreateSection(content, "ListArea", listW, bodyH);
            var laRect = listArea.GetComponent<RectTransform>();
            laRect.anchorMin = new Vector2(0, 1);
            laRect.anchorMax = new Vector2(0, 1);
            laRect.pivot = new Vector2(0, 1);
            laRect.anchoredPosition = new Vector2(10, bodyTop);
            laRect.sizeDelta = new Vector2(listW, bodyH);

            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(listArea.transform, false);
            var vpRect = viewport.AddComponent<RectTransform>();
            vpRect.anchorMin = new Vector2(0, 0);
            vpRect.anchorMax = new Vector2(1, 1);
            vpRect.offsetMin = new Vector2(4, 4);
            vpRect.offsetMax = new Vector2(-4, -4);
            viewport.AddComponent<Image>().color = new Color(0, 0, 0, 0);
            viewport.AddComponent<Mask>().showMaskGraphic = false;

            _listContent = new GameObject("Content");
            _listContent.transform.SetParent(viewport.transform, false);
            var lcRect = _listContent.AddComponent<RectTransform>();
            lcRect.anchorMin = new Vector2(0, 1);
            lcRect.anchorMax = new Vector2(1, 1);
            lcRect.pivot = new Vector2(0.5f, 1);
            lcRect.anchoredPosition = Vector2.zero;

            var scrollRect = listArea.AddComponent<ScrollRect>();
            scrollRect.content = lcRect;
            scrollRect.viewport = vpRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;

            // Right: detail area
            float detailW = panelW - listW - 30f;
            var detailArea = CreateSection(content, "DetailArea", detailW, bodyH);
            var daRect = detailArea.GetComponent<RectTransform>();
            daRect.anchorMin = new Vector2(0, 1);
            daRect.anchorMax = new Vector2(0, 1);
            daRect.pivot = new Vector2(0, 1);
            daRect.anchoredPosition = new Vector2(listW + 20, bodyTop);
            daRect.sizeDelta = new Vector2(detailW, bodyH);

            _detailTitle = UIBuilder.CreateText(detailArea.transform, "DTitle", "选择一个任务查看详情",
                18, UIBuilder.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
            var dtRect = _detailTitle.GetComponent<RectTransform>();
            dtRect.anchorMin = new Vector2(0, 1);
            dtRect.anchorMax = new Vector2(1, 1);
            dtRect.pivot = new Vector2(0, 1);
            dtRect.offsetMin = new Vector2(10, -30);
            dtRect.offsetMax = new Vector2(-10, -8);

            _detailState = UIBuilder.CreateText(detailArea.transform, "DState", "",
                15, UIBuilder.TextDim, TextAnchor.MiddleLeft);
            var dsRect = _detailState.GetComponent<RectTransform>();
            dsRect.anchorMin = new Vector2(0, 1);
            dsRect.anchorMax = new Vector2(1, 1);
            dsRect.pivot = new Vector2(0, 1);
            dsRect.offsetMin = new Vector2(10, -50);
            dsRect.offsetMax = new Vector2(-10, -34);

            var divLine = UIBuilder.CreateDivider(detailArea.transform, "DivLine", detailW - 20f);
            var dlRect = divLine.GetComponent<RectTransform>();
            dlRect.anchorMin = new Vector2(0.5f, 1);
            dlRect.anchorMax = new Vector2(0.5f, 1);
            dlRect.pivot = new Vector2(0.5f, 1);
            dlRect.anchoredPosition = new Vector2(0, -56);
            dlRect.sizeDelta = new Vector2(detailW - 20f, 2f);

            _detailGoals = UIBuilder.CreateText(detailArea.transform, "DGoals", "",
                14, UIBuilder.TextWhite, TextAnchor.UpperLeft);
            var dgRect = _detailGoals.GetComponent<RectTransform>();
            dgRect.anchorMin = new Vector2(0, 1);
            dgRect.anchorMax = new Vector2(1, 1);
            dgRect.pivot = new Vector2(0, 1);
            dgRect.offsetMin = new Vector2(10, -120);
            dgRect.offsetMax = new Vector2(-10, -64);

            _detailTime = UIBuilder.CreateText(detailArea.transform, "DTime", "",
                12, UIBuilder.TextDim, TextAnchor.MiddleRight);
            var dtmRect = _detailTime.GetComponent<RectTransform>();
            dtmRect.anchorMin = new Vector2(0, 0);
            dtmRect.anchorMax = new Vector2(1, 0);
            dtmRect.pivot = new Vector2(1, 0);
            dtmRect.offsetMin = new Vector2(10, 10);
            dtmRect.offsetMax = new Vector2(-10, 28);

            // Bottom buttons
            float bottomY = bodyTop - bodyH - 10f;

            // Accept row: input + button
            _acceptInput = UIBuilder.CreateInputField(content, "AcceptInput", "输入任务ID...", 120, 38, 15);
            _acceptInput.GetComponent<RectTransform>().anchoredPosition = new Vector2(-240, bottomY);

            var acceptBtn = UIBuilder.CreateButton(content, "AcceptBtn", "接 取", 90, 38, 16,
                UIBuilder.AccentBlue, UIBuilder.TextWhite);
            acceptBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(-140, bottomY);
            acceptBtn.onClick.AddListener(OnAcceptClick);

            _submitBtn = UIBuilder.CreateButton(content, "SubmitBtn", "提 交", 90, 38, 16,
                UIBuilder.ButtonNormal, UIBuilder.Gold);
            _submitBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(-30, bottomY);
            _submitBtn.onClick.AddListener(OnSubmitClick);

            _giveUpBtn = UIBuilder.CreateButton(content, "GiveUpBtn", "放 弃", 90, 38, 16,
                UIBuilder.ButtonNormal, new Color(0.8f, 0.3f, 0.3f));
            _giveUpBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(80, bottomY);
            _giveUpBtn.onClick.AddListener(OnGiveUpClick);

            var closeBtn = UIBuilder.CreateButton(content, "CloseBtn", "关 闭", 90, 38, 16,
                UIBuilder.ButtonNormal, UIBuilder.TextGray);
            closeBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(200, bottomY);
            closeBtn.onClick.AddListener(OnCloseClick);

            canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
        }

        public override void OnShow()
        {
            RegisterHandlers();
            NetClient.Instance.Send(new QueryTaskListReq());
        }

        public override void OnHide()
        {
            UnregisterHandlers();
        }

        // ==================== Network ====================

        private void RegisterHandlers()
        {
            var nc = NetClient.Instance;
            if (nc == null || nc.Dispatcher == null) return;
            nc.Dispatcher.Register<QueryTaskListAck>(MsgIdConst.QueryTaskListAck, HandleQueryTaskListAck);
            nc.Dispatcher.Register<TaskAcceptAck>(MsgIdConst.TaskAcceptAck, HandleTaskAcceptAck);
            nc.Dispatcher.Register<TaskSubmitAck>(MsgIdConst.TaskSubmitAck, HandleTaskSubmitAck);
            nc.Dispatcher.Register<TaskGiveUpAck>(MsgIdConst.TaskGiveUpAck, HandleTaskGiveUpAck);
            nc.Dispatcher.Register<TaskChangedNtf>(MsgIdConst.TaskChangedNtf, HandleTaskChangedNtf);
        }

        private void UnregisterHandlers()
        {
            var nc = NetClient.Instance;
            if (nc == null || nc.Dispatcher == null) return;
            nc.Dispatcher.Unregister(MsgIdConst.QueryTaskListAck);
            nc.Dispatcher.Unregister(MsgIdConst.TaskAcceptAck);
            nc.Dispatcher.Unregister(MsgIdConst.TaskSubmitAck);
            nc.Dispatcher.Unregister(MsgIdConst.TaskGiveUpAck);
            nc.Dispatcher.Unregister(MsgIdConst.TaskChangedNtf);
        }

        // ==================== Handlers ====================

        private void HandleQueryTaskListAck(QueryTaskListAck ack)
        {
            if (!string.IsNullOrEmpty(ack.ErrorMessage))
            {
                UIToast.Show(ack.ErrorMessage);
                return;
            }
            _tasks = ack.Tasks ?? new List<TaskInfo>();
            _selectedTaskId = -1;
            RefreshList();
            ClearDetail();
        }

        private void HandleTaskAcceptAck(TaskAcceptAck ack)
        {
            if (!string.IsNullOrEmpty(ack.ErrorMessage))
            {
                UIToast.Show(ack.ErrorMessage);
                return;
            }
            if (ack.Task != null)
            {
                _tasks.Add(ack.Task);
                RefreshList();
                UIToast.Show("接取任务成功");
            }
        }

        private void HandleTaskSubmitAck(TaskSubmitAck ack)
        {
            if (!string.IsNullOrEmpty(ack.ErrorMessage))
            {
                UIToast.Show(ack.ErrorMessage);
                return;
            }
            // Remove submitted task
            _tasks.RemoveAll(t => t.TaskId == ack.TaskId);
            _selectedTaskId = -1;
            RefreshList();
            ClearDetail();

            // Show rewards
            if (ack.Rewards != null && ack.Rewards.Count > 0)
            {
                var parts = new List<string>();
                foreach (var r in ack.Rewards)
                    parts.Add("物品#" + r.ConfigId + " x" + r.Count);
                UIToast.Show("提交成功，获得: " + string.Join(", ", parts.ToArray()));
            }
            else
            {
                UIToast.Show("提交成功");
            }
        }

        private void HandleTaskGiveUpAck(TaskGiveUpAck ack)
        {
            if (!string.IsNullOrEmpty(ack.ErrorMessage))
            {
                UIToast.Show(ack.ErrorMessage);
                return;
            }
            _tasks.RemoveAll(t => t.TaskId == ack.TaskId);
            if (_selectedTaskId == ack.TaskId)
            {
                _selectedTaskId = -1;
                ClearDetail();
            }
            RefreshList();
            UIToast.Show("已放弃任务");
        }

        private void HandleTaskChangedNtf(TaskChangedNtf ntf)
        {
            if (ntf.Tasks == null) return;
            foreach (var changed in ntf.Tasks)
            {
                int idx = _tasks.FindIndex(t => t.TaskId == changed.TaskId);
                if (idx >= 0)
                    _tasks[idx] = changed;
                else
                    _tasks.Add(changed);
            }
            RefreshList();
            RefreshDetail();
        }

        // ==================== UI Refresh ====================

        private void RefreshList()
        {
            _taskCountText.text = _tasks.Count + " 个";

            // Clear old items
            for (int i = _listContent.transform.childCount - 1; i >= 0; i--)
                Destroy(_listContent.transform.GetChild(i).gameObject);

            float totalH = _tasks.Count * (TASK_ITEM_H + TASK_ITEM_GAP);
            var lcRect = _listContent.GetComponent<RectTransform>();
            lcRect.sizeDelta = new Vector2(0, totalH);

            for (int i = 0; i < _tasks.Count; i++)
            {
                var task = _tasks[i];
                var item = CreateTaskListItem(_listContent.transform, task, i);
                var itemRect = item.GetComponent<RectTransform>();
                itemRect.anchoredPosition = new Vector2(0, -i * (TASK_ITEM_H + TASK_ITEM_GAP));
            }
        }

        private GameObject CreateTaskListItem(Transform parent, TaskInfo task, int index)
        {
            bool isSelected = task.TaskId == _selectedTaskId;
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

            // Title: task name with id
            var title = UIBuilder.CreateText(item.transform, "Title", "任务 #" + task.TaskId, 14,
                UIBuilder.TextWhite, TextAnchor.MiddleLeft, FontStyle.Bold);
            var tRect = title.GetComponent<RectTransform>();
            tRect.anchorMin = new Vector2(0, 0.5f);
            tRect.anchorMax = new Vector2(1, 1);
            tRect.offsetMin = new Vector2(10, 0);
            tRect.offsetMax = new Vector2(-10, -2);

            // State label
            var state = UIBuilder.CreateText(item.transform, "State", stateText, 12,
                stateColor, TextAnchor.MiddleLeft);
            var sRect = state.GetComponent<RectTransform>();
            sRect.anchorMin = new Vector2(0, 0);
            sRect.anchorMax = new Vector2(1, 0.5f);
            sRect.offsetMin = new Vector2(10, 2);
            sRect.offsetMax = new Vector2(-10, 0);

            // Progress indicator (how many goals done)
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
            _selectedTaskId = taskId;
            RefreshList();
            RefreshDetail();
        }

        private void RefreshDetail()
        {
            var task = _tasks.Find(t => t.TaskId == _selectedTaskId);
            if (task == null)
            {
                ClearDetail();
                return;
            }

            _detailTitle.text = "任务 #" + task.TaskId;
            _detailState.text = "状态: " + GetStateText(task.State);
            _detailState.color = GetStateColor(task.State);

            // Goals
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

            // Time
            var timeParts = new List<string>();
            if (task.AcceptTime > 0)
                timeParts.Add("接取: " + FormatTime(task.AcceptTime));
            if (task.CompleteTime > 0)
                timeParts.Add("完成: " + FormatTime(task.CompleteTime));
            _detailTime.text = string.Join("  ", timeParts.ToArray());

            // Update button states
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
            if (_acceptInput == null) return;
            string text = _acceptInput.text.Trim();
            if (string.IsNullOrEmpty(text))
            {
                UIToast.Show("请输入任务ID");
                return;
            }
            int taskId;
            if (!int.TryParse(text, out taskId) || taskId <= 0)
            {
                UIToast.Show("无效的任务ID");
                return;
            }
            NetClient.Instance.Send(new TaskAcceptReq { TaskId = taskId });
            _acceptInput.text = "";
        }

        private void OnSubmitClick()
        {
            if (_selectedTaskId <= 0)
            {
                UIToast.Show("请先选择一个任务");
                return;
            }
            NetClient.Instance.Send(new TaskSubmitReq { TaskId = _selectedTaskId });
        }

        private void OnGiveUpClick()
        {
            if (_selectedTaskId <= 0)
            {
                UIToast.Show("请先选择一个任务");
                return;
            }
            UIMessageBox.Show("放弃任务", "确定要放弃这个任务吗？进度将丢失。",
                () => NetClient.Instance.Send(new TaskGiveUpReq { TaskId = _selectedTaskId }),
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
            if (timestamp <= 0) return "";
            var dt = new System.DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc);
            dt = dt.AddMilliseconds(timestamp).ToLocalTime();
            return dt.ToString("yyyy-MM-dd HH:mm");
        }

    }
}
