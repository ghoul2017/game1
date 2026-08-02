using System;
using Client.View.Product;
using GameplayCore;
using ProductExpedition;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Client.Expedition
{
    /// <summary>
    /// 在独立场景或 T1 入口切换后启动完整的 T2 本地闭环。
    /// </summary>
    public sealed class ExpeditionVerticalSliceEntry : MonoBehaviour
    {
        [SerializeField]
        private string _productId = ProductExpeditionRun.DefaultProductId;

        [SerializeField]
        private int _ticksPerSecond = T1ExpeditionSetupFactory.DefaultTicksPerSecond;

        private bool _initialized;

        public LocalCampaignHost CampaignHost { get; private set; }

        public ProductProfileRuntimeEntry ProductProfileEntry { get; private set; }

        public string ProductId => _productId;

        private void Start()
        {
            if (!_initialized)
            {
                Initialize(T2ExpeditionCampaignFactory.Create(_ticksPerSecond), _productId);
            }
        }

        public void Initialize(ExpeditionCampaignDefinition definition)
        {
            Initialize(definition, _productId);
        }

        public void Initialize(ExpeditionCampaignDefinition definition, string productId)
        {
            if (_initialized)
            {
                return;
            }

            ProductExpeditionRun.ValidateProductId(productId);
            _productId = productId;
            _initialized = true;
            Screen.orientation = ScreenOrientation.Portrait;
            EnsureEventSystem();
            ProductProfileEntry = GetComponent<ProductProfileRuntimeEntry>();
            if (ProductProfileEntry == null)
            {
                ProductProfileEntry = gameObject.AddComponent<ProductProfileRuntimeEntry>();
            }

            ProductProfileEntry.ApplyProduct(_productId);
            CampaignHost = gameObject.AddComponent<LocalCampaignHost>();
            CampaignHost.Initialize(definition, _productId, ProductProfileEntry);
        }

        public void ConfigureProduct(string productId)
        {
            if (_initialized)
            {
                throw new InvalidOperationException("运行中的 T2/T6 入口不能切换产品");
            }

            ProductExpeditionRun.ValidateProductId(productId);
            _productId = productId;
        }

        public void ConfigureTicksPerSecond(int ticksPerSecond)
        {
            if (_initialized)
            {
                throw new InvalidOperationException("运行中的 T2/T6 入口不能切换 Tick 率");
            }

            if (ticksPerSecond <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ticksPerSecond));
            }

            _ticksPerSecond = ticksPerSecond;
        }

        private void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            GameObject eventSystemObject = new GameObject(
                "ExpeditionEventSystem",
                typeof(EventSystem),
                typeof(StandaloneInputModule));
            eventSystemObject.transform.SetParent(transform, false);
        }
    }
}
