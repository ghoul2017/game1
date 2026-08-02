using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Client.Logic;
using Shared.Common.Protos;
using UnityEngine;
using UnityEngine.UI;

namespace Client.View.OnlineTechnical
{
    /// <summary>
    /// 在线远征的完整客户端宿主：角色就绪、恢复/开局、局内表现、权威结算、基地升级和第二局。
    /// </summary>
    public sealed class OnlineExpeditionGameplayView : MonoBehaviour
    {
        private static readonly Color ActionColor = new Color(0.08f, 0.52f, 0.72f, 1.0f);
        private static readonly Color PrimaryColor = new Color(0.1f, 0.68f, 0.38f, 1.0f);

        private GameObject _basePanel;
        private Text _baseStatusText;
        private string _baseMessage = string.Empty;
        private Button _departButton;
        private Text _eventText;
        private long _lifecycleGeneration;
        private Text _operationText;
        private bool _operationInProgress;
        private OnlineExpeditionInputView _input;
        private GameObject _resultPanel;
        private Text _resultText;
        private long _roleId;
        private Text _statusText;
        private Button _settleButton;
        private Button _upgradeButton;
        private GameObject _worldRoot;

        public bool BaseVisible => _basePanel != null && _basePanel.activeSelf;

        public OnlineExpeditionInputView InputView => _input;

        public bool ResultVisible => _resultPanel != null && _resultPanel.activeSelf;

        public long RoleId => _roleId;

        public OnlineExpeditionWorldView World { get; private set; }

        public void Initialize()
        {
            BuildInterface();
            ExpeditionController controller = ExpeditionController.Instance;
            controller.SnapshotChanged += OnSnapshotChanged;
            controller.ProgressChanged += OnProgressChanged;
            controller.SettlementChanged += OnSettlementChanged;
            controller.ResultReceived += OnResultReceived;
            controller.EventBatchReceived += OnEventBatchReceived;
        }

        public void Enter(long roleId)
        {
            if (roleId <= 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(roleId));
            }

            long lifecycleGeneration = Interlocked.Increment(ref _lifecycleGeneration);
            _roleId = roleId;
            _operationInProgress = false;
            gameObject.SetActive(true);
            DestroyWorld();
            _basePanel.SetActive(false);
            _resultPanel.SetActive(false);
            _input.gameObject.SetActive(false);
            _baseMessage = string.Empty;
            _operationText.text = string.Empty;
            ExpeditionController.Instance.ResetForRole(roleId);
            _ = EnterAsync(lifecycleGeneration, roleId);
        }

        public void ExitToLogin()
        {
            Interlocked.Increment(ref _lifecycleGeneration);
            _roleId = 0L;
            _operationInProgress = false;
            _operationText.text = string.Empty;
            DestroyWorld();
            _basePanel.SetActive(false);
            _resultPanel.SetActive(false);
            _input.gameObject.SetActive(false);
            gameObject.SetActive(false);
            ExpeditionController.Instance.ResetForRole(0L);
        }

        public void StartNextRun()
        {
            long lifecycleGeneration = Interlocked.Read(ref _lifecycleGeneration);
            long roleId = _roleId;
            if (roleId > 0L)
            {
                _ = StartNextRunAsync(lifecycleGeneration, roleId);
            }
        }

        public void SettleAndReturnToBase()
        {
            long lifecycleGeneration = Interlocked.Read(ref _lifecycleGeneration);
            long roleId = _roleId;
            if (roleId > 0L)
            {
                _ = SettleAndReturnToBaseAsync(lifecycleGeneration, roleId);
            }
        }

        public void UpgradeFacility()
        {
            long lifecycleGeneration = Interlocked.Read(ref _lifecycleGeneration);
            long roleId = _roleId;
            if (roleId > 0L)
            {
                _ = UpgradeFacilityAsync(lifecycleGeneration, roleId);
            }
        }

        private void Update()
        {
            ExpeditionController controller = ExpeditionController.Instance;
            ExpeditionSnapshotData snapshot = controller.LatestSnapshot;
            if (snapshot == null || snapshot.Phase != 0 || World == null || BaseVisible)
            {
                return;
            }

            Vector2 direction = _input.Direction;
            controller.SendInput(direction.x, direction.y);
            controller.AdvanceRootPrediction(Time.unscaledDeltaTime);
            World.Render(controller.PredictedSquadX, controller.PredictedSquadY, Time.unscaledDeltaTime);
        }

        private void OnDisable()
        {
            ExpeditionSnapshotData snapshot = ExpeditionController.Instance.LatestSnapshot;
            if (snapshot != null && snapshot.Phase == 0)
            {
                ExpeditionController.Instance.SendInput(0.0f, 0.0f);
            }
        }

        private void OnDestroy()
        {
            Interlocked.Increment(ref _lifecycleGeneration);
            ExpeditionController controller = ExpeditionController.Instance;
            controller.SnapshotChanged -= OnSnapshotChanged;
            controller.ProgressChanged -= OnProgressChanged;
            controller.SettlementChanged -= OnSettlementChanged;
            controller.ResultReceived -= OnResultReceived;
            controller.EventBatchReceived -= OnEventBatchReceived;
        }

        private async Task EnterAsync(long lifecycleGeneration, long roleId)
        {
            if (!IsCurrentLifecycle(lifecycleGeneration, roleId))
            {
                return;
            }

            if (!BeginOperation("正在恢复在线远征……"))
            {
                return;
            }

            try
            {
                ExpeditionController controller = ExpeditionController.Instance;
                ExpeditionResumeRes response = await controller.ResumeAsync();
                if (!IsCurrentLifecycle(lifecycleGeneration, roleId))
                {
                    return;
                }

                if (!string.IsNullOrEmpty(response.ErrorMessage))
                {
                    ShowBase($"恢复失败：{response.ErrorMessage}");
                    return;
                }

                if (response.HasActiveRun)
                {
                    if (response.Snapshot.Phase == 0)
                    {
                        HideBase();
                    }

                    return;
                }

                if (response.Progress.LastStartedRunIndex == 0L)
                {
                    EndOperation();
                    await StartNextRunAsync(lifecycleGeneration, roleId);
                    return;
                }

                ShowBase("当前没有进行中的远征，可以开始下一局");
            }
            catch (Exception exception)
            {
                if (IsCurrentLifecycle(lifecycleGeneration, roleId))
                {
                    ShowBase($"恢复失败：{exception.Message}");
                }
            }
            finally
            {
                if (IsCurrentLifecycle(lifecycleGeneration, roleId))
                {
                    EndOperation();
                }
            }
        }

        private async Task StartNextRunAsync(long lifecycleGeneration, long roleId)
        {
            if (!IsCurrentLifecycle(lifecycleGeneration, roleId))
            {
                return;
            }

            if (!BeginOperation("正在创建服务端权威远征……"))
            {
                return;
            }

            try
            {
                ExpeditionStartRes response = await ExpeditionController.Instance.StartAsync();
                if (!IsCurrentLifecycle(lifecycleGeneration, roleId))
                {
                    return;
                }

                if (!string.IsNullOrEmpty(response.ErrorMessage))
                {
                    ExpeditionResumeRes resume = await ExpeditionController.Instance.ResumeAsync();
                    if (!IsCurrentLifecycle(lifecycleGeneration, roleId))
                    {
                        return;
                    }

                    if (string.IsNullOrEmpty(resume.ErrorMessage) && resume.HasActiveRun)
                    {
                        if (resume.Snapshot.Phase == 0)
                        {
                            HideBase();
                        }

                        _operationText.text = $"已恢复 Run {resume.Snapshot.RunId}";
                        return;
                    }

                    ShowBase($"开始失败：{response.ErrorMessage}");
                    return;
                }

                HideBase();
                _operationText.text = $"Run {response.Snapshot.RunId} 已开始";
            }
            catch (Exception exception)
            {
                if (IsCurrentLifecycle(lifecycleGeneration, roleId))
                {
                    ShowBase($"开始失败：{exception.Message}");
                }
            }
            finally
            {
                if (IsCurrentLifecycle(lifecycleGeneration, roleId))
                {
                    EndOperation();
                }
            }
        }

        private async Task SettleAndReturnToBaseAsync(long lifecycleGeneration, long roleId)
        {
            if (!IsCurrentLifecycle(lifecycleGeneration, roleId))
            {
                return;
            }

            ExpeditionController controller = ExpeditionController.Instance;
            bool clearOperationMessage = true;
            long runId = controller.LatestResult != null
                ? controller.LatestResult.RunId
                : controller.LatestSnapshot?.RunId ?? 0L;
            if (runId <= 0L || !BeginOperation("正在提交唯一结算……"))
            {
                return;
            }

            try
            {
                ExpeditionSettlementRes response = await controller.SettleAsync(runId);
                if (!IsCurrentLifecycle(lifecycleGeneration, roleId))
                {
                    return;
                }

                if (!string.IsNullOrEmpty(response.ErrorMessage))
                {
                    _operationText.text = $"结算失败：{response.ErrorMessage}";
                    clearOperationMessage = false;
                    return;
                }

                DestroyWorld();
                _resultPanel.SetActive(false);
                ShowBase(
                    $"上局入账 {response.Settlement.AwardedResource}，" +
                    $"基地资源 {response.Progress.ResourceBalance}");
            }
            catch (Exception exception)
            {
                if (IsCurrentLifecycle(lifecycleGeneration, roleId))
                {
                    _operationText.text = $"结算失败：{exception.Message}";
                    clearOperationMessage = false;
                }
            }
            finally
            {
                if (IsCurrentLifecycle(lifecycleGeneration, roleId))
                {
                    EndOperation(clearOperationMessage);
                }
            }
        }

        private async Task UpgradeFacilityAsync(long lifecycleGeneration, long roleId)
        {
            if (!IsCurrentLifecycle(lifecycleGeneration, roleId))
            {
                return;
            }

            if (!BeginOperation("正在升级设施……"))
            {
                return;
            }

            try
            {
                ExpeditionUpgradeRes response = await ExpeditionController.Instance.UpgradeAsync();
                if (!IsCurrentLifecycle(lifecycleGeneration, roleId))
                {
                    return;
                }

                if (!string.IsNullOrEmpty(response.ErrorMessage))
                {
                    SynchronizeBase($"升级失败：{response.ErrorMessage}");
                    return;
                }

                SynchronizeBase(
                    $"升级完成：代表伤害 {response.OldRepresentativeDamage} → " +
                    $"{response.NewRepresentativeDamage}");
            }
            catch (Exception exception)
            {
                if (IsCurrentLifecycle(lifecycleGeneration, roleId))
                {
                    SynchronizeBase($"升级失败：{exception.Message}");
                }
            }
            finally
            {
                if (IsCurrentLifecycle(lifecycleGeneration, roleId))
                {
                    EndOperation();
                }
            }
        }

        private void BuildInterface()
        {
            GameObject canvasObject = new GameObject(
                "OnlineExpeditionGameplayCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080.0f, 1920.0f);
            scaler.matchWidthOrHeight = 0.5f;

            _statusText = OnlineTechnicalUiFactory.CreateText(
                "OnlineGameplayStatus",
                canvasObject.transform,
                "等待权威快照",
                30,
                TextAnchor.UpperLeft);
            OnlineTechnicalUiFactory.SetRect(
                _statusText.rectTransform,
                new Vector2(0.025f, 0.88f),
                new Vector2(0.975f, 0.985f),
                Vector2.zero,
                Vector2.zero);

            _eventText = OnlineTechnicalUiFactory.CreateText(
                "OnlineGameplayEvent",
                canvasObject.transform,
                string.Empty,
                34,
                TextAnchor.UpperCenter);
            OnlineTechnicalUiFactory.SetRect(
                _eventText.rectTransform,
                new Vector2(0.08f, 0.80f),
                new Vector2(0.92f, 0.88f),
                Vector2.zero,
                Vector2.zero);

            _operationText = OnlineTechnicalUiFactory.CreateText(
                "OnlineGameplayOperation",
                canvasObject.transform,
                string.Empty,
                25,
                TextAnchor.LowerCenter);
            OnlineTechnicalUiFactory.SetRect(
                _operationText.rectTransform,
                new Vector2(0.18f, 0.015f),
                new Vector2(0.82f, 0.07f),
                Vector2.zero,
                Vector2.zero);

            _input = OnlineExpeditionInputView.Create(canvasObject.transform);
            BuildResultPanel(canvasObject.transform);
            BuildBasePanel(canvasObject.transform);
        }

        private void BuildResultPanel(Transform parent)
        {
            _resultPanel = OnlineTechnicalUiFactory.CreatePanel(
                "OnlineExpeditionResultPanel",
                parent,
                new Color(0.03f, 0.05f, 0.08f, 0.96f));
            OnlineTechnicalUiFactory.SetAnchoredRect(
                _resultPanel.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(680.0f, 520.0f));

            _resultText = OnlineTechnicalUiFactory.CreateText(
                "OnlineExpeditionResult",
                _resultPanel.transform,
                string.Empty,
                48,
                TextAnchor.MiddleCenter);
            OnlineTechnicalUiFactory.SetRect(
                _resultText.rectTransform,
                new Vector2(0.06f, 0.32f),
                new Vector2(0.94f, 0.94f),
                Vector2.zero,
                Vector2.zero);

            _settleButton = OnlineTechnicalUiFactory.CreateButton(
                "OnlineExpeditionReturnToBaseButton",
                _resultPanel.transform,
                "结算并返回基地",
                PrimaryColor);
            OnlineTechnicalUiFactory.SetRect(
                _settleButton.GetComponent<RectTransform>(),
                new Vector2(0.18f, 0.08f),
                new Vector2(0.82f, 0.27f),
                Vector2.zero,
                Vector2.zero);
            _settleButton.onClick.AddListener(SettleAndReturnToBase);
            _resultPanel.SetActive(false);
        }

        private void BuildBasePanel(Transform parent)
        {
            _basePanel = OnlineTechnicalUiFactory.CreatePanel(
                "OnlineExpeditionBase",
                parent,
                new Color(0.035f, 0.05f, 0.075f, 1.0f));
            OnlineTechnicalUiFactory.SetRect(
                _basePanel.GetComponent<RectTransform>(),
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);

            Text title = OnlineTechnicalUiFactory.CreateText(
                "Title",
                _basePanel.transform,
                "远征基地（服务端权威）",
                60,
                TextAnchor.MiddleCenter);
            OnlineTechnicalUiFactory.SetRect(
                title.rectTransform,
                new Vector2(0.08f, 0.78f),
                new Vector2(0.92f, 0.94f),
                Vector2.zero,
                Vector2.zero);

            _baseStatusText = OnlineTechnicalUiFactory.CreateText(
                "OnlineBaseStatus",
                _basePanel.transform,
                string.Empty,
                39,
                TextAnchor.MiddleCenter);
            OnlineTechnicalUiFactory.SetRect(
                _baseStatusText.rectTransform,
                new Vector2(0.08f, 0.34f),
                new Vector2(0.92f, 0.76f),
                Vector2.zero,
                Vector2.zero);

            _upgradeButton = OnlineTechnicalUiFactory.CreateButton(
                "OnlineBaseUpgradeButton",
                _basePanel.transform,
                "升级设施",
                ActionColor);
            OnlineTechnicalUiFactory.SetRect(
                _upgradeButton.GetComponent<RectTransform>(),
                new Vector2(0.15f, 0.16f),
                new Vector2(0.85f, 0.26f),
                Vector2.zero,
                Vector2.zero);
            _upgradeButton.onClick.AddListener(UpgradeFacility);

            _departButton = OnlineTechnicalUiFactory.CreateButton(
                "OnlineBaseDepartButton",
                _basePanel.transform,
                "开始下一局",
                PrimaryColor);
            OnlineTechnicalUiFactory.SetRect(
                _departButton.GetComponent<RectTransform>(),
                new Vector2(0.15f, 0.04f),
                new Vector2(0.85f, 0.14f),
                Vector2.zero,
                Vector2.zero);
            _departButton.onClick.AddListener(StartNextRun);
            _basePanel.SetActive(false);
        }

        private void OnSnapshotChanged(ExpeditionSnapshotData snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            bool rebuildWorld = World == null || World.RenderedRunId != snapshot.RunId;
            bool roomChanged = !rebuildWorld && World.RenderedRoomId != snapshot.CurrentRoomId;
            if (rebuildWorld)
            {
                BuildWorld(snapshot);
            }

            World.Synchronize(snapshot);
            if (rebuildWorld || roomChanged || snapshot.Phase != 0)
            {
                World.Render(
                    ExpeditionController.Instance.PredictedSquadX,
                    ExpeditionController.Instance.PredictedSquadY,
                    0.0f);
            }

            SynchronizeHud(snapshot);
            if (snapshot.Phase != 0)
            {
                ShowResult(snapshot.RunId, snapshot.Phase, snapshot.ServerTick, snapshot.CollectedResource);
            }
        }

        private void OnProgressChanged(ExpeditionProgressData progress)
        {
            if (BaseVisible)
            {
                SynchronizeBase(string.Empty);
            }
        }

        private void OnSettlementChanged(ExpeditionSettlementData settlement)
        {
            if (settlement != null && settlement.RunId > 0L && BaseVisible)
            {
                SynchronizeBase($"上局入账 {settlement.AwardedResource}");
            }
        }

        private void OnResultReceived(ExpeditionResultNtf result)
        {
            ExpeditionSnapshotData snapshot = ExpeditionController.Instance.LatestSnapshot;
            long collected = snapshot != null && snapshot.RunId == result.RunId
                ? snapshot.CollectedResource
                : 0L;
            ShowResult(result.RunId, result.Phase, result.ServerTick, collected);
        }

        private void OnEventBatchReceived(ExpeditionEventBatchNtf batch)
        {
            for (int index = 0; index < batch.Events.Count; index++)
            {
                string description = DescribeEvent(batch.Events[index]);
                if (description.Length > 0)
                {
                    _eventText.text = description;
                }
            }
        }

        private void BuildWorld(ExpeditionSnapshotData snapshot)
        {
            DestroyWorld();
            _worldRoot = new GameObject($"OnlineExpeditionRun_{snapshot.RunId}");
            _worldRoot.transform.SetParent(transform, false);
            World = _worldRoot.AddComponent<OnlineExpeditionWorldView>();
            World.Initialize(snapshot);
            _basePanel.SetActive(false);
            _resultPanel.SetActive(false);
            _input.gameObject.SetActive(snapshot.Phase == 0);
        }

        private void DestroyWorld()
        {
            World = null;
            if (_worldRoot == null)
            {
                return;
            }

            _worldRoot.SetActive(false);
            Destroy(_worldRoot);
            _worldRoot = null;
        }

        private void SynchronizeHud(ExpeditionSnapshotData snapshot)
        {
            StringBuilder builder = new StringBuilder(180);
            float seconds = snapshot.TicksPerSecond <= 0
                ? 0.0f
                : (float)snapshot.ServerTick / snapshot.TicksPerSecond;
            builder.Append($"Run {snapshot.RunIndex}   用时 {seconds:0.0} 秒   ");
            builder.Append($"房间 {snapshot.CurrentRoomId}   击败 {snapshot.Kills}   ");
            builder.Append($"局内资源 {snapshot.CollectedResource}\n生命 ");
            for (int index = 0; index < snapshot.Members.Count; index++)
            {
                ExpeditionMemberStateData member = snapshot.Members[index];
                if (index > 0)
                {
                    builder.Append("   ");
                }

                builder.Append($"队员{member.Id} {member.Health}/{member.MaxHealth}");
            }

            _statusText.text = builder.ToString();
        }

        private void ShowResult(long runId, int phase, long endTick, long collectedResource)
        {
            int ticksPerSecond = ExpeditionController.Instance.LatestSnapshot?.TicksPerSecond ?? 0;
            float seconds = ticksPerSecond <= 0 ? 0.0f : (float)endTick / ticksPerSecond;
            string resultName = phase == 1 ? "远征完成" : "队伍战败";
            _resultText.text =
                $"{resultName}\nRun {runId}\n用时 {seconds:0.0} 秒\n局内资源 {collectedResource}";
            _resultPanel.SetActive(true);
            _settleButton.interactable = !_operationInProgress;
            _input.gameObject.SetActive(false);
        }

        private void ShowBase(string message)
        {
            DestroyWorld();
            _resultPanel.SetActive(false);
            _basePanel.SetActive(true);
            _input.gameObject.SetActive(false);
            SynchronizeBase(message);
        }

        private void HideBase()
        {
            _basePanel.SetActive(false);
            _input.gameObject.SetActive(true);
        }

        private void SynchronizeBase(string message)
        {
            if (!string.IsNullOrEmpty(message))
            {
                _baseMessage = message;
            }

            ExpeditionProgressData progress = ExpeditionController.Instance.Progress;
            long upgradeCost = progress.UpgradeCost;
            bool canUpgrade = progress.FacilityLevel < progress.MaximumFacilityLevel &&
                progress.ResourceBalance >= upgradeCost;
            StringBuilder builder = new StringBuilder(200);
            if (!string.IsNullOrEmpty(_baseMessage))
            {
                builder.Append(_baseMessage).Append("\n\n");
            }

            builder.Append($"基地资源：{progress.ResourceBalance}\n");
            builder.Append($"训练设施：{progress.FacilityLevel}/{progress.MaximumFacilityLevel}\n");
            builder.Append($"代表队员伤害：{progress.RepresentativeDamage}\n");
            builder.Append(progress.FacilityLevel >= progress.MaximumFacilityLevel
                ? "设施已经升到当前测试上限"
                : $"升级消耗：{upgradeCost}，升级会提高下一局伤害");
            _baseStatusText.text = builder.ToString();
            _upgradeButton.interactable = !_operationInProgress && canUpgrade;
            _departButton.interactable = !_operationInProgress;
        }

        private bool BeginOperation(string message)
        {
            if (_operationInProgress)
            {
                return false;
            }

            _operationInProgress = true;
            _operationText.text = message;
            SetButtonsInteractable(false);
            return true;
        }

        private bool IsCurrentLifecycle(long lifecycleGeneration, long roleId)
        {
            return Interlocked.Read(ref _lifecycleGeneration) == lifecycleGeneration && _roleId == roleId;
        }

        private void EndOperation(bool clearMessage = true)
        {
            _operationInProgress = false;
            if (clearMessage)
            {
                _operationText.text = string.Empty;
            }
            SetButtonsInteractable(true);
            if (BaseVisible)
            {
                SynchronizeBase(string.Empty);
            }
        }

        private void SetButtonsInteractable(bool interactable)
        {
            if (_settleButton != null)
            {
                _settleButton.interactable = interactable;
            }

            if (_upgradeButton != null)
            {
                _upgradeButton.interactable = interactable;
            }

            if (_departButton != null)
            {
                _departButton.interactable = interactable;
            }
        }

        private static string DescribeEvent(ExpeditionEventData current)
        {
            switch (current.Kind)
            {
                case 0:
                    return current.TargetKind == 0 ? "目标已清除" : $"锁定目标 {current.TargetId}";
                case 1:
                    return $"队员{current.SourceId} 攻击敌人{current.TargetId}，伤害 {current.Value}";
                case 2:
                    return $"敌人{current.SourceId} 发动攻击";
                case 3:
                    return $"队员{current.TargetId} 受到 {current.Value} 点伤害";
                case 4:
                    return $"队员{current.TargetId} 倒下";
                case 5:
                    return $"敌人{current.TargetId} 受到 {current.Value} 点伤害";
                case 6:
                    return $"敌人{current.TargetId} 被击败";
                case 7:
                    return $"采集局内资源 +{current.Value}";
                case 8:
                    return "资源点已采尽";
                case 9:
                    return "首领即将攻击，请离开红色危险区";
                case 10:
                    return $"首领命中全队，伤害 {current.Value}";
                case 11:
                    return "已躲开首领攻击";
                case 12:
                    return "出口已开启";
                case 13:
                    return "远征完成";
                case 14:
                    return "队伍战败";
                case 15:
                    return $"发现房间 {current.SourceId}";
                case 16:
                    return $"进入房间 {current.TargetId}";
                case 17:
                    return $"房间 {current.SourceId} 已清理";
                case 18:
                    return "新路线已标记";
                case 19:
                    return "侦测到危险路线";
                case 20:
                    return $"房间奖励 +{current.Value}";
                default:
                    return string.Empty;
            }
        }
    }
}
