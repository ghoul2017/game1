using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Client.Expedition.WeChat
{
    /// <summary>
    /// 在微信目标环境为 T1 的 EventSystem 安装 SDK 触摸结束与取消覆盖层。
    /// </summary>
    public sealed class WeChatTouchInputAdapter : MonoBehaviour
    {
        public bool TouchOverrideInstalled { get; private set; }

        private void Start()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                throw new InvalidOperationException("[Expedition] 微信触摸适配需要活动的 EventSystem");
            }

#if (UNITY_WEBGL || WEIXINMINIGAME) && !UNITY_EDITOR
            InstallTouchOverride(eventSystem);
#endif
        }

#if UNITY_WEBGL || WEIXINMINIGAME || UNITY_EDITOR
        private void InstallTouchOverride(EventSystem eventSystem)
        {
            WXTouchInputOverride touchOverride = eventSystem.GetComponent<WXTouchInputOverride>();
            if (touchOverride == null)
            {
                touchOverride = eventSystem.gameObject.AddComponent<WXTouchInputOverride>();
            }

            TouchOverrideInstalled = touchOverride != null;
        }
#endif
    }
}
