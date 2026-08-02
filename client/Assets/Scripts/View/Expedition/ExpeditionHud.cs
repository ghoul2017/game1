using System;
using System.Collections.Generic;
using System.Text;
using GameplayCore;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Expedition
{
    /// <summary>
    /// 显示成员生命、局内统计、最近事件和由宿主决定后续动作的结算界面。
    /// </summary>
    public sealed class ExpeditionHud : MonoBehaviour
    {
        private static Font _font;

        private Text _eventText;
        private GameObject _resultPanel;
        private Text _resultText;
        private Button _restartButton;
        private Text _statusText;

        public Transform CanvasTransform => transform;

        public bool ResultVisible => _resultPanel.activeSelf;

        public string Status => _statusText.text;

        public string LastEventText => _eventText.text;

        public string ResultText => _resultText.text;

        public Button RestartButton => _restartButton;

        public static ExpeditionHud Create(Transform parent, Action restart, string actionLabel = "重新开始")
        {
            GameObject canvasObject = new GameObject(
                "ExpeditionHud",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(parent, false);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080.0f, 1920.0f);
            scaler.matchWidthOrHeight = 0.5f;

            ExpeditionHud hud = canvasObject.AddComponent<ExpeditionHud>();
            hud.Build(restart, actionLabel);
            return hud;
        }

        public void Synchronize(ExpeditionRun run, IReadOnlyList<ExpeditionEvent> events)
        {
            UpdateStatus(run);
            UpdateEventText(events);

            if (!run.HasEnded)
            {
                return;
            }

            _resultPanel.SetActive(true);
            string phase = run.Phase == ExpeditionPhase.Completed ? "远征完成" : "队伍战败";
            float seconds = (float)run.Tick / run.TicksPerSecond;
            _resultText.text =
                $"{phase}\n用时 {seconds:0.0} 秒\n击败 {run.Kills}\n局内资源 {run.CollectedResource}";
        }

        private void Build(Action restart, string actionLabel)
        {
            _statusText = CreateText("Status", transform, 30, TextAnchor.UpperLeft);
            RectTransform statusRect = _statusText.rectTransform;
            statusRect.anchorMin = new Vector2(0.0f, 1.0f);
            statusRect.anchorMax = new Vector2(1.0f, 1.0f);
            statusRect.pivot = new Vector2(0.5f, 1.0f);
            statusRect.anchoredPosition = new Vector2(0.0f, -24.0f);
            statusRect.sizeDelta = new Vector2(-48.0f, 180.0f);

            _eventText = CreateText("Event", transform, 34, TextAnchor.UpperCenter);
            RectTransform eventRect = _eventText.rectTransform;
            eventRect.anchorMin = new Vector2(0.0f, 1.0f);
            eventRect.anchorMax = new Vector2(1.0f, 1.0f);
            eventRect.pivot = new Vector2(0.5f, 1.0f);
            eventRect.anchoredPosition = new Vector2(0.0f, -220.0f);
            eventRect.sizeDelta = new Vector2(-80.0f, 100.0f);

            CreateResultPanel(restart, actionLabel);
        }

        private void CreateResultPanel(Action restart, string actionLabel)
        {
            _resultPanel = new GameObject("ResultPanel", typeof(RectTransform), typeof(Image));
            _resultPanel.transform.SetParent(transform, false);
            RectTransform panelRect = _resultPanel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(680.0f, 520.0f);
            _resultPanel.GetComponent<Image>().color = new Color(0.03f, 0.05f, 0.08f, 0.94f);

            _resultText = CreateText("Result", _resultPanel.transform, 54, TextAnchor.MiddleCenter);
            RectTransform resultRect = _resultText.rectTransform;
            resultRect.anchorMin = new Vector2(0.0f, 0.35f);
            resultRect.anchorMax = new Vector2(1.0f, 1.0f);
            resultRect.offsetMin = new Vector2(30.0f, 0.0f);
            resultRect.offsetMax = new Vector2(-30.0f, -20.0f);

            GameObject buttonObject = new GameObject(
                "RestartButton",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(_resultPanel.transform, false);
            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.5f, 0.0f);
            buttonRect.anchorMax = new Vector2(0.5f, 0.0f);
            buttonRect.pivot = new Vector2(0.5f, 0.0f);
            buttonRect.anchoredPosition = new Vector2(0.0f, 45.0f);
            buttonRect.sizeDelta = new Vector2(360.0f, 110.0f);
            buttonObject.GetComponent<Image>().color = new Color(0.12f, 0.65f, 0.35f, 1.0f);
            _restartButton = buttonObject.GetComponent<Button>();
            _restartButton.onClick.AddListener(() => restart());

            Text buttonText = CreateText("Label", buttonObject.transform, 42, TextAnchor.MiddleCenter);
            buttonText.text = actionLabel;
            buttonText.rectTransform.anchorMin = Vector2.zero;
            buttonText.rectTransform.anchorMax = Vector2.one;
            buttonText.rectTransform.offsetMin = Vector2.zero;
            buttonText.rectTransform.offsetMax = Vector2.zero;
            _resultPanel.SetActive(false);
        }

        private void UpdateStatus(ExpeditionRun run)
        {
            StringBuilder builder = new StringBuilder(160);
            float seconds = (float)run.Tick / run.TicksPerSecond;
            builder.Append($"用时 {seconds:0.0} 秒   局内资源 {run.CollectedResource}   击败 {run.Kills}");
            if (run.UsesRoomGraph)
            {
                builder.Append($"   房间 {run.CurrentRoomId}   已发现 {run.DiscoveredRooms.Count}");
            }

            builder.Append("\n生命 ");

            for (int index = 0; index < run.Members.Count; index++)
            {
                ExpeditionMember member = run.Members[index];
                if (index > 0)
                {
                    builder.Append("   ");
                }

                builder.Append($"队员{member.Id} {member.Health}/{member.MaxHealth}");
            }

            _statusText.text = builder.ToString();
        }

        private void UpdateEventText(IReadOnlyList<ExpeditionEvent> events)
        {
            for (int index = 0; index < events.Count; index++)
            {
                string description = DescribeEvent(events[index]);
                if (description.Length > 0)
                {
                    _eventText.text = description;
                }
            }
        }

        private static string DescribeEvent(ExpeditionEvent current)
        {
            switch (current.Kind)
            {
                case ExpeditionEventKind.TargetChanged:
                    return current.TargetKind == ExpeditionObjectKind.None
                        ? "目标已清除"
                        : $"锁定{DescribeObject(current.TargetKind)} {current.TargetId}";
                case ExpeditionEventKind.MemberAttacked:
                    return $"队员{current.SourceId} 攻击敌人{current.TargetId}，伤害 {current.Value}";
                case ExpeditionEventKind.EnemyAttacked:
                    return $"敌人{current.SourceId} 攻击队员{current.TargetId}";
                case ExpeditionEventKind.MemberDamaged:
                    return $"队员{current.TargetId} 受到 {current.Value} 点伤害";
                case ExpeditionEventKind.MemberDied:
                    return $"队员{current.TargetId} 倒下";
                case ExpeditionEventKind.EnemyDied:
                    return $"敌人{current.TargetId} 被击败";
                case ExpeditionEventKind.ResourceCollected:
                    return $"采集局内资源 +{current.Value}";
                case ExpeditionEventKind.ResourceDepleted:
                    return "资源点已采尽";
                case ExpeditionEventKind.BossWarningStarted:
                    return "首领即将攻击，请离开危险区";
                case ExpeditionEventKind.BossWarningHit:
                    return $"首领命中全队，伤害 {current.Value}";
                case ExpeditionEventKind.BossWarningDodged:
                    return "已躲开首领攻击";
                case ExpeditionEventKind.ExitOpened:
                    return "出口已开启";
                case ExpeditionEventKind.Completed:
                    return "远征完成";
                case ExpeditionEventKind.Failed:
                    return "队伍战败";
                case ExpeditionEventKind.RoomDiscovered:
                    return $"发现房间 {current.SourceId}";
                case ExpeditionEventKind.RoomChanged:
                    return $"进入房间 {current.TargetId}";
                case ExpeditionEventKind.RoomCleared:
                    return $"房间 {current.SourceId} 已清理";
                case ExpeditionEventKind.RouteMarked:
                    return $"路线已记录：{current.SourceId} -> {current.TargetId}";
                case ExpeditionEventKind.DangerHint:
                    return "前方检测到高危目标，请谨慎进入";
                case ExpeditionEventKind.RoomRewarded:
                    return $"房间奖励 +{current.Value}";
                default:
                    return string.Empty;
            }
        }

        private static string DescribeObject(ExpeditionObjectKind kind)
        {
            switch (kind)
            {
                case ExpeditionObjectKind.Member:
                    return "队员";
                case ExpeditionObjectKind.Enemy:
                    return "敌人";
                case ExpeditionObjectKind.Resource:
                    return "资源点";
                case ExpeditionObjectKind.Room:
                    return "房间";
                default:
                    return "目标";
            }
        }

        private static Text CreateText(string objectName, Transform parent, int fontSize, TextAnchor alignment)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            Text text = textObject.GetComponent<Text>();
            text.font = GetFont();
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static Font GetFont()
        {
            if (_font == null)
            {
                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            return _font;
        }
    }
}
