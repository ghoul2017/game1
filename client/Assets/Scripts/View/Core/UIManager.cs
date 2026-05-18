using UnityEngine;
using UnityEngine.EventSystems;
using System;
using System.Collections.Generic;
using Client.Logic;

namespace Client.View
{
    /// <summary>
    /// UI 管理器，负责所有面板的加载、显示、隐藏和栈管理。
    /// 挂在 Canvas 所在的 GameObject 上，或由 GameEntry 自动创建。
    /// </summary>
    public class UIManager : MonoBehaviour, IClientUI
    {
        private static UIManager instance;
        public static UIManager Instance => instance;

        public Canvas MainCanvas { get; private set; }

        private Dictionary<Type, UIPanel> panels = new Dictionary<Type, UIPanel>();
        private Stack<UIPanel> panelStack = new Stack<UIPanel>();

        public static UIManager EnsureCreated()
        {
            if (instance != null) return instance;

            // 创建 Canvas
            var canvasObj = new GameObject("Canvas");
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;

            // CanvasScaler
            var scaler = canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // EventSystem（UI 交互必需，挂到 Canvas 下随 DontDestroyOnLoad 保留）
            if (FindObjectOfType<EventSystem>() == null)
            {
                var esObj = new GameObject("EventSystem");
                esObj.transform.SetParent(canvasObj.transform, false);
                esObj.AddComponent<EventSystem>();
                esObj.AddComponent<StandaloneInputModule>();
            }

            // 添加 UIManager
            var mgr = canvasObj.AddComponent<UIManager>();
            mgr.MainCanvas = canvas;

            DontDestroyOnLoad(canvasObj);
            return mgr;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;

            if (MainCanvas == null)
                MainCanvas = GetComponent<Canvas>();
        }

        private void Start()
        {
            // EnsureCreated() 在 AddComponent 后才设置 MainCanvas，Start 确保引用就绪
            if (MainCanvas == null)
                MainCanvas = GetComponent<Canvas>();
        }

        /// <summary>
        /// 显示指定类型的面板
        /// </summary>
        public T ShowPanel<T>(float fadeDuration = 0.3f) where T : UIPanel
        {
            var type = typeof(T);
            T panel = GetOrCreatePanel<T>();

            if (panel == null) return null;

            // 隐藏当前栈顶面板
            if (panelStack.Count > 0 && panelStack.Peek() != panel)
            {
                panelStack.Peek().Hide(fadeDuration);
            }

            panelStack.Push(panel);
            panel.Show(fadeDuration);
            return panel;
        }

        /// <summary>
        /// 隐藏指定类型的面板
        /// </summary>
        public void HidePanel<T>(float fadeDuration = 0.3f) where T : UIPanel
        {
            var type = typeof(T);
            if (panels.TryGetValue(type, out var panel))
            {
                panel.Hide(fadeDuration);
                RemoveFromStack(panel);

                // 恢复新栈顶面板的显示
                if (panelStack.Count > 0)
                {
                    panelStack.Peek().Show(fadeDuration);
                }
            }
        }

        /// <summary>
        /// 返回上一个面板
        /// </summary>
        public void Back(float fadeDuration = 0.3f)
        {
            if (panelStack.Count <= 1) return;

            var current = panelStack.Pop();
            current.Hide(fadeDuration);

            if (panelStack.Count > 0)
            {
                var prev = panelStack.Peek();
                prev.Show(fadeDuration);
            }
        }

        /// <summary>
        /// 清空所有面板
        /// </summary>
        public void ClearAll()
        {
            foreach (var panel in panels.Values)
            {
                if (panel != null) panel.HideImmediate();
            }
            panelStack.Clear();
        }

        /// <summary>
        /// 替换当前面板（弹出栈顶，压入新面板）
        /// </summary>
        public void ReplacePanel<T>(float fadeDuration = 0.3f) where T : UIPanel
        {
            if (panelStack.Count > 0)
            {
                var current = panelStack.Pop();
                current.Hide(fadeDuration);
            }

            T panel = GetOrCreatePanel<T>();
            if (panel == null) return;

            panelStack.Push(panel);
            panel.Show(fadeDuration);
        }

        private T GetOrCreatePanel<T>() where T : UIPanel
        {
            var type = typeof(T);
            if (panels.TryGetValue(type, out var existPanel))
            {
                if (existPanel != null) return (T)existPanel;
                panels.Remove(type);
            }

            string panelName = typeof(T).Name;
            GameObject prefab = Resources.Load<GameObject>($"UI/{panelName}");

            GameObject inst;
            if (prefab != null)
            {
                inst = Instantiate(prefab, MainCanvas.transform, false);
            }
            else
            {
                // 无预制体时自动创建带 CanvasGroup + RectTransform 的 GameObject
                inst = new GameObject(panelName);
                inst.transform.SetParent(MainCanvas.transform, false);
            }

            T panel = inst.GetComponent<T>();
            if (panel == null)
            {
                panel = inst.AddComponent<T>();
            }

            panels[type] = panel;
            return panel;
        }

        private void RemoveFromStack(UIPanel panel)
        {
            if (panelStack.Count == 0) return;

            var temp = new Stack<UIPanel>();
            while (panelStack.Count > 0)
            {
                var p = panelStack.Pop();
                if (p != panel) temp.Push(p);
            }
            while (temp.Count > 0)
            {
                panelStack.Push(temp.Pop());
            }
        }

        /// <summary>
        /// 获取已打开的面板实例，未找到返回 null
        /// </summary>
        public T GetPanel<T>() where T : UIPanel
        {
            var type = typeof(T);
            if (panels.TryGetValue(type, out var panel) && panel != null)
                return (T)panel;
            return null;
        }

        // IClientUI 显式接口实现 —— 供 Logic 层通过 ClientContext.UI 调用

        T IClientUI.GetPanel<T>()
        {
            foreach (var panel in panels.Values)
            {
                if (panel is T t)
                    return t;
            }
            return null;
        }

        void IClientUI.ShowPanel<T>()
        {
            if (typeof(UIPanel).IsAssignableFrom(typeof(T)))
            {
                var method = typeof(UIManager).GetMethod(nameof(ShowPanel), new Type[] { typeof(float) });
                var generic = method.MakeGenericMethod(typeof(T));
                generic.Invoke(this, new object[] { 0.3f });
            }
        }

        void IClientUI.HidePanel<T>()
        {
            if (typeof(UIPanel).IsAssignableFrom(typeof(T)))
            {
                var method = typeof(UIManager).GetMethod(nameof(HidePanel), new Type[] { typeof(float) });
                var generic = method.MakeGenericMethod(typeof(T));
                generic.Invoke(this, new object[] { 0.3f });
            }
        }

        void IClientUI.ReplacePanel<T>()
        {
            if (typeof(UIPanel).IsAssignableFrom(typeof(T)))
            {
                var method = typeof(UIManager).GetMethod(nameof(ReplacePanel), new Type[] { typeof(float) });
                var generic = method.MakeGenericMethod(typeof(T));
                generic.Invoke(this, new object[] { 0.3f });
            }
        }

        void IClientUI.ClearAll()
        {
            ClearAll();
        }

        void IClientUI.Back()
        {
            Back();
        }

        void IClientUI.ShowToast(string msg)
        {
            UIToast.Show(msg);
        }

        void IClientUI.ShowMessageBox(string title, string content, Action onOk, Action onCancel)
        {
            UIMessageBox.Show(title, content, onOk, onCancel);
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }
    }
}
