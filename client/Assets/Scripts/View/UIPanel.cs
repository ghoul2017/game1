using UnityEngine;
using System.Collections;

namespace Client.View
{
    /// <summary>
    /// UI 面板基类，所有 UI 面板继承此类
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class UIPanel : MonoBehaviour
    {
        protected CanvasGroup canvasGroup;
        protected bool isInitialized = false;

        public bool IsVisible => canvasGroup != null && canvasGroup.alpha > 0;

        protected virtual void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        /// <summary>
        /// 面板首次显示时调用，仅一次
        /// </summary>
        public virtual void OnInit() { }

        /// <summary>
        /// 面板每次显示时调用
        /// </summary>
        public virtual void OnShow() { }

        /// <summary>
        /// 面板每次隐藏时调用
        /// </summary>
        public virtual void OnHide() { }

        /// <summary>
        /// 面板销毁时调用
        /// </summary>
        public virtual void OnDestroyed() { }

        /// <summary>
        /// 显示面板（带淡入动画）
        /// </summary>
        public void Show(float fadeDuration = 0.3f)
        {
            if (!isInitialized)
            {
                EnsureRectTransform();
                OnInit();
                isInitialized = true;
            }

            gameObject.SetActive(true);
            StopAllCoroutines();
            StartCoroutine(FadeCoroutine(0f, 1f, fadeDuration, () =>
            {
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }));
            OnShow();
        }

        /// <summary>
        /// 隐藏面板（带淡出动画）
        /// </summary>
        public void Hide(float fadeDuration = 0.3f)
        {
            OnHide();
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            StopAllCoroutines();
            StartCoroutine(FadeCoroutine(1f, 0f, fadeDuration, () =>
            {
                gameObject.SetActive(false);
            }));
        }

        /// <summary>
        /// 立即显示，不带动画
        /// </summary>
        public void ShowImmediate()
        {
            if (!isInitialized)
            {
                EnsureRectTransform();
                OnInit();
                isInitialized = true;
            }
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            gameObject.SetActive(true);
            OnShow();
        }

        /// <summary>
        /// 立即隐藏，不带动画
        /// </summary>
        public void HideImmediate()
        {
            OnHide();
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 确保面板拥有全屏拉伸的 RectTransform
        /// </summary>
        private void EnsureRectTransform()
        {
            var rect = transform as RectTransform;
            if (rect == null)
            {
                rect = gameObject.AddComponent<RectTransform>();
            }
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private IEnumerator FadeCoroutine(float from, float to, float duration, System.Action onComplete = null)
        {
            canvasGroup.alpha = from;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
                yield return null;
            }
            canvasGroup.alpha = to;
            onComplete?.Invoke();
        }

        protected virtual void OnDestroy()
        {
            OnDestroyed();
        }
    }
}
