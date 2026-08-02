using System;
using Client.View.Product;
using GameplayCore;
using ProductExpedition;
using UnityEngine;

namespace Client.Expedition
{
    /// <summary>
    /// 负责本地战役的一局、结算、基地升级和下一局对象生命周期。
    /// </summary>
    public sealed class LocalCampaignHost : MonoBehaviour
    {
        private GameObject _activeRunRoot;
        private GameObject _baseRoot;
        private string _productId;
        private ProductProfileRuntimeEntry _productProfileEntry;
        private ExpeditionCampaignSession _session;

        public LocalExpeditionHost ActiveHost { get; private set; }

        public GameObject ActiveRunRoot => _activeRunRoot;

        public ExpeditionBaseView BaseView { get; private set; }

        public bool IsAtBase => BaseView != null;

        public FacilityUpgradeReceipt LatestUpgradeReceipt { get; private set; }

        public ExpeditionCampaignSession Session => _session;

        public string ProductId => _productId;

        public void Initialize(ExpeditionCampaignDefinition definition)
        {
            Initialize(definition, ProductExpeditionRun.DefaultProductId);
        }

        public void Initialize(ExpeditionCampaignDefinition definition, string productId)
        {
            Initialize(definition, productId, null);
        }

        public void Initialize(
            ExpeditionCampaignDefinition definition,
            string productId,
            ProductProfileRuntimeEntry productProfileEntry)
        {
            if (_session != null)
            {
                throw new InvalidOperationException("本地战役宿主不能重复初始化");
            }

            ProductExpeditionRun.ValidateProductId(productId);
            _productId = productId;
            _productProfileEntry = productProfileEntry;
            _session = new ExpeditionCampaignSession(
                definition ?? throw new ArgumentNullException(nameof(definition)));
            StartNextRun();
        }

        public ExpeditionRun StartNextRun()
        {
            EnsureInitialized();
            if (_session.ActiveRun != null)
            {
                throw new InvalidOperationException("当前远征尚未结算");
            }

            DestroyRoot(ref _baseRoot);
            BaseView = null;
            LatestUpgradeReceipt = null;

            ExpeditionRun run = _session.StartRun();
            BuildRun(run);
            return run;
        }

        public SettlementReceipt ReturnToBase()
        {
            EnsureInitialized();
            if (BaseView != null)
            {
                return _session.State.LatestSettlement;
            }

            if (ActiveHost == null || !ActiveHost.Run.HasEnded)
            {
                throw new InvalidOperationException("只能在远征结束后返回基地");
            }

            SettlementReceipt receipt = _session.ConsumeResult(ActiveHost.Run);
            ActiveHost.enabled = false;
            DestroyRoot(ref _activeRunRoot);
            ActiveHost = null;
            BuildBase();
            return receipt;
        }

        public bool TryUpgradeFacility()
        {
            EnsureInitialized();
            if (!IsAtBase || !_session.TryUpgradeFacility(out FacilityUpgradeReceipt receipt))
            {
                return false;
            }

            LatestUpgradeReceipt = receipt;
            BaseView.Synchronize(_session, receipt);
            return true;
        }

        private void BuildRun(ExpeditionRun run)
        {
            DestroyRoot(ref _activeRunRoot);
            _activeRunRoot = new GameObject($"ActiveExpeditionRun_{run.RunId}");
            _activeRunRoot.transform.SetParent(transform, false);

            GameObject worldObject = new GameObject("ExpeditionWorld");
            worldObject.transform.SetParent(_activeRunRoot.transform, false);
            ExpeditionWorldView world = worldObject.AddComponent<ExpeditionWorldView>();
            world.Initialize(run);
            if (_productProfileEntry != null)
            {
                _productProfileEntry.ApplyProduct(_productId);
                _productProfileEntry.ApplyTheme(world.WorldCamera);
            }

            ExpeditionHud hud = ExpeditionHud.Create(
                _activeRunRoot.transform,
                () => ReturnToBase(),
                "返回基地");
            ExpeditionInputView input = ExpeditionInputView.Create(hud.CanvasTransform);
            LocalExpeditionHost host = _activeRunRoot.AddComponent<LocalExpeditionHost>();
            host.Initialize(run, input, world, hud, _productId);
            ActiveHost = host;
        }

        private void BuildBase()
        {
            _baseRoot = new GameObject("ExpeditionBaseRoot");
            _baseRoot.transform.SetParent(transform, false);
            BaseView = ExpeditionBaseView.Create(
                _baseRoot.transform,
                () => TryUpgradeFacility(),
                () => StartNextRun());
            BaseView.Synchronize(_session, LatestUpgradeReceipt);
        }

        private void EnsureInitialized()
        {
            if (_session == null)
            {
                throw new InvalidOperationException("本地战役宿主尚未初始化");
            }
        }

        private static void DestroyRoot(ref GameObject root)
        {
            if (root == null)
            {
                return;
            }

            root.SetActive(false);
            Destroy(root);
            root = null;
        }
    }
}
