using System.Collections;
using Client.Expedition;
using Client.View.Product;
using GameplayCore;
using NUnit.Framework;
using ProductExpedition;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Client.Expedition.Tests
{
    public sealed class ProductExpeditionPlayModeTests
    {
        [UnityTest]
        public IEnumerator TimedProductSceneFailsSettlesAndStartsNextRunWithFreshTimer()
        {
            SceneManager.LoadScene("CosmicLegionPlaceholderScene", LoadSceneMode.Single);
            yield return null;

            Scene scene = SceneManager.GetActiveScene();
            ExpeditionVerticalSliceEntry entry = Object.FindObjectOfType<ExpeditionVerticalSliceEntry>();
            Assert.That(scene.path, Is.EqualTo("Assets/Scenes/CosmicLegionPlaceholderScene.unity"));
            Assert.That(SceneUtility.GetBuildIndexByScenePath(scene.path), Is.EqualTo(2));
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry.ProductId, Is.EqualTo(ProductExpeditionRun.TimedPlaceholderProductId));

            ProductProfileRuntimeEntry profileEntry = entry.GetComponent<ProductProfileRuntimeEntry>();
            Assert.That(profileEntry, Is.Not.Null);
            Assert.That(entry.ProductProfileEntry, Is.SameAs(profileEntry));
            Assert.That(profileEntry.ActiveProductId, Is.EqualTo(ProductExpeditionRun.TimedPlaceholderProductId));
            Assert.That(profileEntry.ActiveContentId, Is.EqualTo("cosmic_legion_content_v1"));
            Assert.That(profileEntry.EnvironmentToken, Is.EqualTo("cosmic_legion_placeholder"));
            Assert.That(entry.GetComponent<Camera>(), Is.Null);

            LocalCampaignHost campaign = entry.CampaignHost;
            LocalExpeditionHost host = campaign.ActiveHost;
            host.enabled = false;
            Assert.That(profileEntry.TargetCamera, Is.SameAs(host.World.WorldCamera));
            Assert.That(host.World.WorldCamera.backgroundColor, Is.EqualTo(profileEntry.PrimaryColor));
            Assert.That(host.Run.TicksPerSecond, Is.EqualTo(10));
            float secondsPerTick = 1.0f / host.Run.TicksPerSecond;

            for (int tick = 0; tick < 1_799; tick++)
            {
                host.AdvanceFrame(secondsPerTick, Vector2.zero);
            }

            Assert.That(host.Run.Tick, Is.EqualTo(1_799L));
            Assert.That(host.Run.HasEnded, Is.False);
            Assert.That(host.ProductRun.RemainingRuleTicks, Is.EqualTo(1L));

            host.AdvanceFrame(secondsPerTick, Vector2.zero);

            Assert.That(host.Run.Tick, Is.EqualTo(1_800L));
            Assert.That(host.Run.Phase, Is.EqualTo(ExpeditionPhase.Failed));
            Assert.That(host.Run.Result, Is.Not.Null);
            Assert.That(host.Run.Result.Tick, Is.EqualTo(1_800L));
            Assert.That(host.Hud.ResultVisible, Is.True);
            Assert.That(host.Hud.ResultText, Does.Contain("队伍战败"));

            ExpeditionResult originalResult = host.Run.Result;
            host.AdvanceFrame(secondsPerTick, Vector2.zero);
            Assert.That(host.Run.Tick, Is.EqualTo(1_800L));
            Assert.That(host.Run.Result, Is.SameAs(originalResult));

            host.Hud.RestartButton.onClick.Invoke();
            Assert.That(campaign.IsAtBase, Is.True);
            Assert.That(campaign.Session.State.LatestSettlement.Phase, Is.EqualTo(ExpeditionPhase.Failed));
            Assert.That(campaign.BaseView.SettlementText, Does.Contain("远征失败"));

            campaign.BaseView.DepartButton.onClick.Invoke();
            LocalExpeditionHost nextHost = campaign.ActiveHost;
            nextHost.enabled = false;

            Assert.That(nextHost.Run.RunId, Is.Not.EqualTo(originalResult.RunId));
            Assert.That(nextHost.Run.Tick, Is.EqualTo(0L));
            Assert.That(nextHost.ProductRun.RuleTick, Is.EqualTo(0L));
            Assert.That(nextHost.ProductRun.RemainingRuleTicks, Is.EqualTo(1_800L));
            Assert.That(nextHost.ProductRun.ProductId, Is.EqualTo(ProductExpeditionRun.TimedPlaceholderProductId));
            Assert.That(profileEntry.TargetCamera, Is.SameAs(nextHost.World.WorldCamera));
            Assert.That(nextHost.World.WorldCamera.backgroundColor, Is.EqualTo(profileEntry.PrimaryColor));
            yield return null;
        }
    }
}
