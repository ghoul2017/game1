using System;
using GameplayCore;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Client.Expedition
{
    /// <summary>
    /// 在干净场景中组装隔离于旧 RPG 的 T1 灰盒局，并负责销毁旧局后立即重开。
    /// </summary>
    public sealed class ExpeditionHandfeelEntry : MonoBehaviour
    {
        private GameObject _activeRunRoot;

        public LocalExpeditionHost ActiveHost { get; private set; }

        public GameObject ActiveRunRoot => _activeRunRoot;

        private void Awake()
        {
            Screen.orientation = ScreenOrientation.Portrait;
            CreateEventSystem();
            RestartExpedition();
        }

        public void RestartExpedition()
        {
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
