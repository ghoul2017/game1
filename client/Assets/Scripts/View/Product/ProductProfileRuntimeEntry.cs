using System;
using ProductContent;
using UnityEngine;

namespace Client.View.Product
{
    /// <summary>
    /// T6 产品切换入口：只消费产品内容与主题，不持有局内规则状态。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProductProfileRuntimeEntry : MonoBehaviour
    {
        [SerializeField]
        private string _productId = ProductCatalog.LabyrinthProductId;

        private Camera _targetCamera;

        public string ActiveProductId { get; private set; } = string.Empty;

        public string ActiveContentId { get; private set; } = string.Empty;

        public string EnvironmentToken { get; private set; } = string.Empty;

        public Color PrimaryColor { get; private set; }

        public Color AccentColor { get; private set; }

        public Camera TargetCamera => _targetCamera;

        private void Awake()
        {
            ApplyProduct(_productId);
        }

        public void ApplyProduct(string productId)
        {
            ProductProfile profile = ProductCatalog.Create(productId);
            if (!ColorUtility.TryParseHtmlString(profile.Theme.PrimaryColor, out Color primaryColor) ||
                !ColorUtility.TryParseHtmlString(profile.Theme.AccentColor, out Color accentColor))
            {
                throw new InvalidOperationException($"产品 {profile.ProductId} 包含无效主题颜色");
            }

            _productId = profile.ProductId;
            ActiveProductId = profile.ProductId;
            ActiveContentId = profile.Content.ContentId;
            EnvironmentToken = profile.Theme.EnvironmentToken;
            PrimaryColor = primaryColor;
            AccentColor = accentColor;

            ApplyThemeToTargetCamera();
        }

        public void ApplyTheme(Camera targetCamera)
        {
            _targetCamera = targetCamera ?? throw new ArgumentNullException(nameof(targetCamera));
            ApplyThemeToTargetCamera();
        }

        private void ApplyThemeToTargetCamera()
        {
            if (_targetCamera == null)
            {
                return;
            }

            _targetCamera.clearFlags = CameraClearFlags.SolidColor;
            _targetCamera.backgroundColor = PrimaryColor;
        }
    }
}
