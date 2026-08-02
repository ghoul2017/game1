using System;
using System.Threading.Tasks;
using Client.Logic;
using Client.View.Arena;
using Shared.Common.Protos;
using UnityEngine;
using UnityEngine.UI;

namespace Client.View.OnlineTechnical
{
    /// <summary>竞技场技术入口。服务端生成结果和录像，客户端只按录像游标表现。</summary>
    public sealed class OnlineArenaPanel : MonoBehaviour
    {
        private static readonly Color ActionColor = new Color(0.52f, 0.28f, 0.12f, 1.0f);
        private static readonly Color ReplayColor = new Color(0.27f, 0.18f, 0.48f, 1.0f);

        private long _battleId;
        private string _battleRequestId = string.Empty;
        private Button _getReplayButton;
        private bool _operationInProgress;
        private ArenaReplayPlayer _replayPlayer;
        private Button _startButton;
        private Text _statusText;

        public int BoundCommandCount { get; private set; }

        public ArenaReplayPlayer ReplayPlayer => _replayPlayer;

        public string StatusText => _statusText == null ? string.Empty : _statusText.text;

        public void Initialize()
        {
            BuildView();
            RefreshStatus("等待登录并进入角色后开始竞技场战斗");
        }

        public void StartBattle()
        {
            _ = StartBattleAsync();
        }

        public void GetReplay()
        {
            _ = GetReplayAsync();
        }

        public void PlayAtNormalSpeed()
        {
            if (!RequireReplay())
            {
                return;
            }

            _replayPlayer.SetSpeed(1);
            RefreshReplayStatus("播放速度已切换为 1 倍");
        }

        public void PlayAtFastSpeed()
        {
            if (!RequireReplay())
            {
                return;
            }

            _replayPlayer.SetSpeed(4);
            RefreshReplayStatus("播放速度已切换为 4 倍");
        }

        public void SkipReplay()
        {
            if (!RequireReplay())
            {
                return;
            }

            _replayPlayer.SkipToEnd();
            RefreshReplayStatus("录像已跳到结尾");
        }

        private void Update()
        {
            if (!_replayPlayer.HasReplay)
            {
                return;
            }

            string state = _replayPlayer.HasEnded ? "已结束" : "播放中";
            _statusText.text =
                $"Battle {_battleId} | Tick {_replayPlayer.CurrentTick} | {state} | " +
                $"结果 {_replayPlayer.VisibleOutcome}";
        }

        private void BuildView()
        {
            _replayPlayer = gameObject.AddComponent<ArenaReplayPlayer>();

            Text title = OnlineTechnicalUiFactory.CreateText(
                "ArenaTitle",
                transform,
                "竞技场（服务端录像）",
                30,
                TextAnchor.MiddleLeft);
            OnlineTechnicalUiFactory.SetRect(
                title.rectTransform,
                new Vector2(0.03f, 0.81f),
                new Vector2(0.97f, 0.97f),
                Vector2.zero,
                Vector2.zero);

            _statusText = OnlineTechnicalUiFactory.CreateText(
                "ArenaStatus",
                transform,
                string.Empty,
                21,
                TextAnchor.UpperLeft);
            OnlineTechnicalUiFactory.SetRect(
                _statusText.rectTransform,
                new Vector2(0.03f, 0.51f),
                new Vector2(0.97f, 0.80f),
                Vector2.zero,
                Vector2.zero);

            _startButton = CreateButton(
                "ArenaStartButton",
                "开始战斗",
                new Vector2(0.03f, 0.35f),
                new Vector2(0.31f, 0.47f),
                ActionColor,
                StartBattle);
            _getReplayButton = CreateButton(
                "ArenaGetReplayButton",
                "取回放",
                new Vector2(0.34f, 0.35f),
                new Vector2(0.62f, 0.47f),
                ActionColor,
                GetReplay);

            CreateButton(
                "ArenaSpeed1Button",
                "1 倍",
                new Vector2(0.03f, 0.13f),
                new Vector2(0.24f, 0.27f),
                ReplayColor,
                PlayAtNormalSpeed);
            CreateButton(
                "ArenaSpeed4Button",
                "4 倍",
                new Vector2(0.27f, 0.13f),
                new Vector2(0.48f, 0.27f),
                ReplayColor,
                PlayAtFastSpeed);
            CreateButton(
                "ArenaSkipButton",
                "跳过",
                new Vector2(0.51f, 0.13f),
                new Vector2(0.72f, 0.27f),
                ReplayColor,
                SkipReplay);
            BoundCommandCount = 5;
        }

        private Button CreateButton(
            string objectName,
            string label,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Color color,
            UnityEngine.Events.UnityAction action)
        {
            Button button = OnlineTechnicalUiFactory.CreateButton(objectName, transform, label, color);
            OnlineTechnicalUiFactory.SetRect(
                button.GetComponent<RectTransform>(),
                anchorMin,
                anchorMax,
                Vector2.zero,
                Vector2.zero);
            button.onClick.AddListener(action);
            return button;
        }

        private async Task StartBattleAsync()
        {
            if (!BeginOperation("正在请求竞技场战斗……"))
            {
                return;
            }

            try
            {
                if (string.IsNullOrEmpty(_battleRequestId))
                {
                    _battleRequestId = Guid.NewGuid().ToString("N");
                }

                ArenaBattleStartAck response = await ArenaController.Instance.StartBattleAsync(_battleRequestId);
                if (!string.IsNullOrEmpty(response.ErrorMessage))
                {
                    _battleRequestId = string.Empty;
                    RefreshStatus($"战斗失败：{response.ErrorMessage}");
                    return;
                }

                _battleId = response.BattleId;
                _battleRequestId = string.Empty;
                RefreshStatus($"战斗已完成，BattleId={_battleId}，正在取回放……");
                await LoadReplayAsync();
            }
            catch (Exception exception)
            {
                RefreshStatus($"战斗失败：{exception.Message}");
            }
            finally
            {
                EndOperation();
            }
        }

        private async Task GetReplayAsync()
        {
            if (_battleId <= 0L)
            {
                RefreshStatus("请先开始一场竞技场战斗");
                return;
            }

            if (!BeginOperation("正在获取竞技场回放……"))
            {
                return;
            }

            try
            {
                await LoadReplayAsync();
            }
            catch (Exception exception)
            {
                RefreshStatus($"获取回放失败：{exception.Message}");
            }
            finally
            {
                EndOperation();
            }
        }

        private async Task LoadReplayAsync()
        {
            ArenaReplayAck replay = await ArenaController.Instance.GetReplayAsync(_battleId);
            if (!string.IsNullOrEmpty(replay.ErrorMessage))
            {
                RefreshStatus($"获取回放失败：{replay.ErrorMessage}");
                return;
            }

            _replayPlayer.Load(replay);
            _replayPlayer.SetSpeed(1);
            RefreshReplayStatus("回放已加载，按 1 倍速度播放");
        }

        private bool BeginOperation(string message)
        {
            if (_operationInProgress)
            {
                return false;
            }

            _operationInProgress = true;
            _startButton.interactable = false;
            _getReplayButton.interactable = false;
            RefreshStatus(message);
            return true;
        }

        private void EndOperation()
        {
            _operationInProgress = false;
            _startButton.interactable = true;
            _getReplayButton.interactable = true;
        }

        private bool RequireReplay()
        {
            if (_replayPlayer.HasReplay)
            {
                return true;
            }

            RefreshStatus("竞技场录像尚未加载");
            return false;
        }

        private void RefreshReplayStatus(string message)
        {
            _statusText.text = $"{message}\nBattle {_battleId} | Tick {_replayPlayer.CurrentTick}";
        }

        private void RefreshStatus(string message)
        {
            if (_statusText != null)
            {
                _statusText.text = message;
            }
        }
    }
}
