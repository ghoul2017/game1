using System;
using System.Threading.Tasks;
using Client.Logic;
using Shared.Common.Protos;
using UnityEngine;
using UnityEngine.UI;

namespace Client.View.OnlineTechnical
{
    /// <summary>
    /// 在线远征技术入口。只提交输入和业务请求，所有局内结果仍由服务端快照驱动。
    /// </summary>
    public sealed class OnlineExpeditionPanel : MonoBehaviour
    {
        private static readonly Color ActionColor = new Color(0.08f, 0.48f, 0.68f, 1.0f);
        private static readonly Color DirectionColor = new Color(0.16f, 0.22f, 0.32f, 1.0f);

        private Button _resumeButton;
        private Button _settleButton;
        private Button _startButton;
        private Text _statusText;
        private Button _upgradeButton;
        private InputField _targetInput;
        private Vector2 _movementDirection;
        private bool _operationInProgress;
        private bool _sendStopInput;

        public int BoundCommandCount { get; private set; }

        public string StatusText => _statusText == null ? string.Empty : _statusText.text;

        public void Initialize()
        {
            BuildView();
            ExpeditionController controller = ExpeditionController.Instance;
            controller.SnapshotChanged += OnSnapshotChanged;
            controller.ProgressChanged += OnProgressChanged;
            controller.SettlementChanged += OnSettlementChanged;
            controller.ResultReceived += OnResultReceived;
            RefreshStatus("等待登录并进入角色后开始在线远征");
        }

        public void StartExpedition()
        {
            _ = StartExpeditionAsync();
        }

        public void ResumeExpedition()
        {
            _ = ResumeExpeditionAsync();
        }

        public void SettleExpedition()
        {
            _ = SettleExpeditionAsync();
        }

        public void UpgradeFacility()
        {
            _ = UpgradeFacilityAsync();
        }

        public void BeginMovement(Vector2 direction)
        {
            _movementDirection = direction.normalized;
            _sendStopInput = false;
            SendMovementInput();
        }

        public void EndMovement(Vector2 direction)
        {
            if (_movementDirection != direction.normalized)
            {
                return;
            }

            _movementDirection = Vector2.zero;
            _sendStopInput = true;
            SendMovementInput();
        }

        private void Update()
        {
            if (_movementDirection != Vector2.zero || _sendStopInput)
            {
                SendMovementInput();
            }
        }

        private void OnDisable()
        {
            if (_movementDirection == Vector2.zero)
            {
                return;
            }

            _movementDirection = Vector2.zero;
            _sendStopInput = true;
            SendMovementInput();
        }

        private void OnDestroy()
        {
            ExpeditionController controller = ExpeditionController.Instance;
            controller.SnapshotChanged -= OnSnapshotChanged;
            controller.ProgressChanged -= OnProgressChanged;
            controller.SettlementChanged -= OnSettlementChanged;
            controller.ResultReceived -= OnResultReceived;
        }

        private void BuildView()
        {
            Text title = OnlineTechnicalUiFactory.CreateText(
                "ExpeditionTitle",
                transform,
                "在线远征（服务端权威）",
                30,
                TextAnchor.MiddleLeft);
            OnlineTechnicalUiFactory.SetRect(
                title.rectTransform,
                new Vector2(0.03f, 0.86f),
                new Vector2(0.97f, 0.98f),
                Vector2.zero,
                Vector2.zero);

            _statusText = OnlineTechnicalUiFactory.CreateText(
                "ExpeditionStatus",
                transform,
                string.Empty,
                21,
                TextAnchor.UpperLeft);
            OnlineTechnicalUiFactory.SetRect(
                _statusText.rectTransform,
                new Vector2(0.03f, 0.55f),
                new Vector2(0.97f, 0.85f),
                Vector2.zero,
                Vector2.zero);

            _startButton = CreateCommandButton(
                "ExpeditionStartButton",
                "开始",
                new Vector2(0.03f, 0.43f),
                StartExpedition);
            _resumeButton = CreateCommandButton(
                "ExpeditionResumeButton",
                "恢复",
                new Vector2(0.27f, 0.43f),
                ResumeExpedition);
            _settleButton = CreateCommandButton(
                "ExpeditionSettleButton",
                "结算",
                new Vector2(0.51f, 0.43f),
                SettleExpedition);
            _upgradeButton = CreateCommandButton(
                "ExpeditionUpgradeButton",
                "升级",
                new Vector2(0.75f, 0.43f),
                UpgradeFacility);

            _targetInput = OnlineTechnicalUiFactory.CreateIntegerInput(
                "ExpeditionInteractionTargetInput",
                transform,
                "交互目标 ID（可空）");
            OnlineTechnicalUiFactory.SetRect(
                _targetInput.GetComponent<RectTransform>(),
                new Vector2(0.03f, 0.31f),
                new Vector2(0.52f, 0.40f),
                Vector2.zero,
                Vector2.zero);

            CreateDirectionButton("ExpeditionMoveUpButton", "↑", new Vector2(0.73f, 0.27f), Vector2.up);
            CreateDirectionButton("ExpeditionMoveLeftButton", "←", new Vector2(0.61f, 0.12f), Vector2.left);
            CreateDirectionButton("ExpeditionMoveDownButton", "↓", new Vector2(0.73f, 0.02f), Vector2.down);
            CreateDirectionButton("ExpeditionMoveRightButton", "→", new Vector2(0.85f, 0.12f), Vector2.right);
            BoundCommandCount = 5;
        }

        private Button CreateCommandButton(
            string objectName,
            string label,
            Vector2 anchorMin,
            UnityEngine.Events.UnityAction action)
        {
            Button button = OnlineTechnicalUiFactory.CreateButton(objectName, transform, label, ActionColor);
            OnlineTechnicalUiFactory.SetRect(
                button.GetComponent<RectTransform>(),
                anchorMin,
                anchorMin + new Vector2(0.21f, 0.09f),
                Vector2.zero,
                Vector2.zero);
            button.onClick.AddListener(action);
            return button;
        }

        private void CreateDirectionButton(
            string objectName,
            string label,
            Vector2 anchorMin,
            Vector2 direction)
        {
            Button button = OnlineTechnicalUiFactory.CreateButton(objectName, transform, label, DirectionColor);
            OnlineTechnicalUiFactory.SetRect(
                button.GetComponent<RectTransform>(),
                anchorMin,
                anchorMin + new Vector2(0.10f, 0.12f),
                Vector2.zero,
                Vector2.zero);
            OnlineDirectionButton directionButton = button.gameObject.AddComponent<OnlineDirectionButton>();
            directionButton.Initialize(this, direction);
        }

        private async Task StartExpeditionAsync()
        {
            if (!BeginOperation("正在请求开始远征……"))
            {
                return;
            }

            try
            {
                ExpeditionStartRes response = await ExpeditionController.Instance.StartAsync();
                RefreshStatus(string.IsNullOrEmpty(response.ErrorMessage)
                    ? $"远征已开始，RunId={response.Snapshot.RunId}"
                    : $"开始失败：{response.ErrorMessage}");
            }
            catch (Exception exception)
            {
                RefreshStatus($"开始失败：{exception.Message}");
            }
            finally
            {
                EndOperation();
            }
        }

        private async Task ResumeExpeditionAsync()
        {
            if (!BeginOperation("正在恢复远征……"))
            {
                return;
            }

            try
            {
                ExpeditionController controller = ExpeditionController.Instance;
                long runId = controller.LatestSnapshot == null ? 0L : controller.LatestSnapshot.RunId;
                ExpeditionResumeRes response = await controller.ResumeAsync(runId);
                RefreshStatus(string.IsNullOrEmpty(response.ErrorMessage)
                    ? response.HasActiveRun ? $"已恢复 RunId={response.Snapshot.RunId}" : "当前没有进行中的远征"
                    : $"恢复失败：{response.ErrorMessage}");
            }
            catch (Exception exception)
            {
                RefreshStatus($"恢复失败：{exception.Message}");
            }
            finally
            {
                EndOperation();
            }
        }

        private async Task SettleExpeditionAsync()
        {
            ExpeditionController controller = ExpeditionController.Instance;
            long runId = ResolveRunId(controller);
            if (runId <= 0L)
            {
                RefreshStatus("当前没有可结算的远征");
                return;
            }

            if (!BeginOperation("正在提交远征结算……"))
            {
                return;
            }

            try
            {
                ExpeditionSettlementRes response = await controller.SettleAsync(runId);
                RefreshStatus(string.IsNullOrEmpty(response.ErrorMessage)
                    ? $"结算完成，资源余额={response.Progress.ResourceBalance}"
                    : $"结算失败：{response.ErrorMessage}");
            }
            catch (Exception exception)
            {
                RefreshStatus($"结算失败：{exception.Message}");
            }
            finally
            {
                EndOperation();
            }
        }

        private async Task UpgradeFacilityAsync()
        {
            if (!BeginOperation("正在升级远征设施……"))
            {
                return;
            }

            try
            {
                ExpeditionUpgradeRes response = await ExpeditionController.Instance.UpgradeAsync();
                RefreshStatus(string.IsNullOrEmpty(response.ErrorMessage)
                    ? $"升级完成，设施等级={response.NewFacilityLevel}，消耗={response.SpentResource}"
                    : $"升级失败：{response.ErrorMessage}");
            }
            catch (Exception exception)
            {
                RefreshStatus($"升级失败：{exception.Message}");
            }
            finally
            {
                EndOperation();
            }
        }

        private void SendMovementInput()
        {
            int interactionTargetId;
            if (!TryGetInteractionTargetId(out interactionTargetId))
            {
                return;
            }

            ExpeditionController.Instance.SendInput(
                _movementDirection.x,
                _movementDirection.y,
                interactionTargetId);
            if (_movementDirection == Vector2.zero)
            {
                _sendStopInput = false;
            }
        }

        private bool TryGetInteractionTargetId(out int interactionTargetId)
        {
            interactionTargetId = 0;
            string value = _targetInput == null ? string.Empty : _targetInput.text.Trim();
            if (value.Length == 0)
            {
                return true;
            }

            if (!int.TryParse(value, out interactionTargetId) || interactionTargetId < 0)
            {
                RefreshStatus("交互目标 ID 必须是非负整数");
                return false;
            }

            return true;
        }

        private bool BeginOperation(string message)
        {
            if (_operationInProgress)
            {
                return false;
            }

            _operationInProgress = true;
            SetCommandButtonsInteractable(false);
            RefreshStatus(message);
            return true;
        }

        private void EndOperation()
        {
            _operationInProgress = false;
            SetCommandButtonsInteractable(true);
        }

        private void SetCommandButtonsInteractable(bool interactable)
        {
            _startButton.interactable = interactable;
            _resumeButton.interactable = interactable;
            _settleButton.interactable = interactable;
            _upgradeButton.interactable = interactable;
        }

        private void OnSnapshotChanged(ExpeditionSnapshotData snapshot)
        {
            if (snapshot == null)
            {
                RefreshStatus("当前没有进行中的远征");
                return;
            }

            RefreshStatus(
                $"Run {snapshot.RunId} | Tick {snapshot.ServerTick} | 房间 {snapshot.CurrentRoomId} | " +
                $"队伍 ({snapshot.SquadX:0.0}, {snapshot.SquadY:0.0}) | 资源 {snapshot.CollectedResource}");
        }

        private void OnProgressChanged(ExpeditionProgressData progress)
        {
            RefreshStatus($"资源余额={progress.ResourceBalance}，设施等级={progress.FacilityLevel}");
        }

        private void OnSettlementChanged(ExpeditionSettlementData settlement)
        {
            if (settlement != null)
            {
                RefreshStatus($"Run {settlement.RunId} 已结算，入账 {settlement.AwardedResource}");
            }
        }

        private void OnResultReceived(ExpeditionResultNtf result)
        {
            RefreshStatus(result.SettlementReady
                ? $"Run {result.RunId} 已结束，可以结算"
                : $"Run {result.RunId} 已结束，等待服务端结算状态");
        }

        private void RefreshStatus(string message)
        {
            if (_statusText != null)
            {
                _statusText.text = message;
            }
        }

        private static long ResolveRunId(ExpeditionController controller)
        {
            if (controller.LatestResult != null)
            {
                return controller.LatestResult.RunId;
            }

            return controller.LatestSnapshot == null ? 0L : controller.LatestSnapshot.RunId;
        }
    }
}
