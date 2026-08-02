using Client.View.Product;
using NUnit.Framework;
using ProductContent;
using UnityEngine;

namespace Client.Expedition.Tests
{
    public sealed class ProductProfilePlayModeTests
    {
        [Test]
        public void SwitchingProductsChangesContentAndThemeOnBoundCamera()
        {
            GameObject gameObject = new GameObject("ProductProfileSmoke", typeof(Camera));
            ProductProfileRuntimeEntry entry = gameObject.AddComponent<ProductProfileRuntimeEntry>();
            Camera targetCamera = gameObject.GetComponent<Camera>();

            try
            {
                entry.ApplyTheme(targetCamera);
                entry.ApplyProduct(ProductCatalog.LabyrinthProductId);
                string firstContentId = entry.ActiveContentId;
                Color firstColor = entry.PrimaryColor;
                Assert.That(targetCamera.backgroundColor, Is.EqualTo(firstColor));

                entry.ApplyProduct(ProductCatalog.CosmicLegionPlaceholderProductId);
                Assert.That(entry.ActiveContentId, Is.Not.EqualTo(firstContentId));
                Assert.That(entry.PrimaryColor, Is.Not.EqualTo(firstColor));
                Assert.That(targetCamera.backgroundColor, Is.EqualTo(entry.PrimaryColor));

                entry.ApplyProduct(ProductCatalog.LabyrinthProductId);
                Assert.That(entry.ActiveContentId, Is.EqualTo(firstContentId));
                Assert.That(targetCamera.backgroundColor, Is.EqualTo(firstColor));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
