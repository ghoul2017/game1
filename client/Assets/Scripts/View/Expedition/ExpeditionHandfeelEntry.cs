using System;
using GameplayCore;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Client.Expedition
{
    /// <summary>
    /// 组装隔离于旧 RPG 的 T1 灰盒局，并提供切入 T2 完整闭环的兼容入口。
    /// </summary>
    public sealed class ExpeditionHandfeelEntry : MonoBehaviour
    {
        private GameObject _activeRunRoot;
        private GameObject _verticalSliceRoot;

        public LocalExpeditionHost ActiveHost { get; private set; }

        public GameObject ActiveRunRoot => _activeRunRoot;

        public LocalCampaignHost VerticalSliceHost { get; private set; }

        private void Awake()
        {
            Screen.orientation = ScreenOrientation.Portrait;
            CreateEventSystem();
            RestartExpedition();
        }

        public void RestartExpedition()
        {
            if (_verticalSliceRoot != null)
            {
                _verticalSliceRoot.SetActive(false);
                Destroy(_verticalSliceRoot);
                _verticalSliceRoot = null;
                VerticalSliceHost = null;
            }

            if (_activeRunRoot != null)
            {
                _activeRunRoot.SetActive(false);
                Destroy(_activeRunRoot);
            }

            ExpeditionSetup setup = T1ExpeditionSetupFactory.Create(T1ExpeditionSetupFactory.DefaultTicksPerSecond);
            ExpeditionRun run = ExpeditionRun.Start(setup);

            _activeRunRoot = new GameObject("ActiveExpeditionRun");
            _activeRunRoot.transform.SetParent(transform, false);

            GameObject worldObject = new GameObject("ExpeditionWorld");
            worldObject.transform.SetParent(_activeRunRoot.transform, false);
            ExpeditionWorldView world = worldObject.AddComponent<ExpeditionWorldView>();
            world.Initialize(run);

            ExpeditionHud hud = ExpeditionHud.Create(_activeRunRoot.transform, RestartExpedition);
            ExpeditionInputView input = ExpeditionInputView.Create(hud.CanvasTransform);
            LocalExpeditionHost host = _activeRunRoot.AddComponent<LocalExpeditionHost>();
            host.Initialize(run, input, world, hud);
            ActiveHost = host;
        }

        public void StartVerticalSlice()
        {
            StartVerticalSlice(T2ExpeditionCampaignFactory.Create());
        }

        public void StartVerticalSlice(ExpeditionCampaignDefinition definition)
        {
            if (_activeRunRoot != null)
            {
                _activeRunRoot.SetActive(false);
                Destroy(_activeRunRoot);
                _activeRunRoot = null;
                ActiveHost = null;
            }

            if (_verticalSliceRoot != null)
            {
                _verticalSliceRoot.SetActive(false);
                Destroy(_verticalSliceRoot);
            }

            _verticalSliceRoot = new GameObject("ExpeditionVerticalSlice");
            _verticalSliceRoot.transform.SetParent(transform, false);
            ExpeditionVerticalSliceEntry verticalEntry =
                _verticalSliceRoot.AddComponent<ExpeditionVerticalSliceEntry>();
            verticalEntry.Initialize(definition);
            VerticalSliceHost = verticalEntry.CampaignHost;
        }

        private void CreateEventSystem()
        {
            if (EventSystem.current != null)
            {
                throw new InvalidOperationException("[Expedition] T1 隔离场景启动前不能存在其他 EventSystem");
            }

            GameObject eventSystemObject = new GameObject(
                "ExpeditionEventSystem",
                typeof(EventSystem),
                typeof(StandaloneInputModule));
            eventSystemObject.transform.SetParent(transform, false);
        }
    }
}
