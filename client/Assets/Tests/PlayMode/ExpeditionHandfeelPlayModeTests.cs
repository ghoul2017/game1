using System.Collections;
using Client.Expedition.WeChat;
using GameplayCore;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Client.Expedition.Tests
{
    /// <summary>
    /// 验证 T1 场景隔离、触摸适配、灰盒状态、固定 Tick、警告表现和重开清理。
    /// </summary>
    public sealed class ExpeditionHandfeelPlayModeTests
    {
        private ExpeditionHandfeelEntry _entry;

        [UnitySetUp]
        public IEnumerator LoadHandfeelScene()
        {
            SceneManager.LoadScene("ExpeditionHandfeelScene", LoadSceneMode.Single);
            yield return null;
            _entry = Object.FindObjectOfType<ExpeditionHandfeelEntry>();
            Assert.That(_entry, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator SceneStartsWithExpectedIsolatedViews()
        {
            yield return null;

            Scene scene = SceneManager.GetActiveScene();
            GameObject[] roots = scene.GetRootGameObjects();
            ExpeditionWorldView world = _entry.ActiveHost.World;

            Assert.That(roots, Has.Length.EqualTo(1));
            Assert.That(roots[0].name, Is.EqualTo("ExpeditionHandfeelEntry"));
            Assert.That(world.Squad.MemberViewCount, Is.EqualTo(3));
            Assert.That(world.EnemyViewCount, Is.EqualTo(4));
            Assert.That(world.NormalEnemyViewCount, Is.EqualTo(3));
            Assert.That(world.BossViewCount, Is.EqualTo(1));
            Assert.That(world.ResourceViewCount, Is.EqualTo(1));
            Assert.That(Object.FindObjectsOfType<LocalExpeditionHost>(), Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectsOfType<EventSystem>(), Has.Length.EqualTo(1));
            Assert.That(world.Squad.GetComponentsInChildren<Collider>(), Is.Empty);

            WeChatTouchInputAdapter touchAdapter = Object.FindObjectOfType<WeChatTouchInputAdapter>();
            Assert.That(touchAdapter, Is.Not.Null);
            Assert.That(touchAdapter.TouchOverrideInstalled, Is.False);

            Light[] lights = Object.FindObjectsOfType<Light>();
            Assert.That(lights, Has.Length.EqualTo(1));
            Assert.That(lights[0].type, Is.EqualTo(LightType.Directional));
            Assert.That(RenderSettings.ambientMode, Is.EqualTo(AmbientMode.Flat));
        }

        [UnityTest]
        public IEnumerator FixedDirectionMovesCoreAndPartnersReturnToFormation()
        {
            LocalExpeditionHost host = _entry.ActiveHost;
            host.enabled = false;
            float secondsPerTick = 1.0f / host.Run.TicksPerSecond;
            float initialX = host.Run.SquadX;

            for (int tick = 0; tick < host.Run.TicksPerSecond; tick++)
            {
                host.AdvanceFrame(secondsPerTick, Vector2.right);
            }

            for (int frame = 0; frame < 30; frame++)
            {
                host.World.Render(host.Run, 1.0f / 60.0f);
            }

            Assert.That(host.Run.SquadX, Is.GreaterThan(initialX + 4.0f));
            for (int index = 1; index < host.World.Squad.MemberViewCount; index++)
            {
                float distance = Vector3.Distance(
                    host.World.Squad.GetMemberPosition(index),
                    host.World.Squad.GetFormationPosition(index));
                Assert.That(distance, Is.LessThan(0.1f));
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator BossWarningViewAppearsAndClearsAfterMovingOut()
        {
            LocalExpeditionHost host = _entry.ActiveHost;
            host.enabled = false;
            float secondsPerTick = 1.0f / host.Run.TicksPerSecond;

            for (int tick = 0; tick < 180 && !host.World.WarningVisible; tick++)
            {
                host.AdvanceFrame(secondsPerTick, Vector2.up);
            }

            Assert.That(host.World.WarningVisible, Is.True);
            ExpeditionEnemy boss = FindBoss(host.Run);
            GameObject warning = GameObject.Find("BossWarning");
            Assert.That(warning, Is.Not.Null);
            AssertClose(boss.WarningX, warning.transform.position.x);
            AssertClose(boss.WarningY, warning.transform.position.z);
            AssertClose(boss.BossWarningRadius * 2.0f, warning.transform.localScale.x);
            AssertClose(boss.BossWarningRadius * 2.0f, warning.transform.localScale.z);

            Color warningColor = GetRendererColor(warning.GetComponent<Renderer>());
            Assert.That(warningColor.r, Is.GreaterThan(0.9f));
            Assert.That(warningColor.g, Is.LessThan(0.2f));
            Assert.That(warningColor.b, Is.LessThan(0.2f));

            Camera camera = Camera.main;
            Assert.That(camera, Is.Not.Null);
            Vector3 warningViewport = camera.WorldToViewportPoint(warning.transform.position);
            Assert.That(warningViewport.z, Is.GreaterThan(0.0f));
            Assert.That(warningViewport.x, Is.InRange(0.0f, 1.0f));
            Assert.That(warningViewport.y, Is.InRange(0.0f, 1.0f));

            int[] healthAtWarning =
            {
                host.Run.Members[0].Health,
                host.Run.Members[1].Health,
                host.Run.Members[2].Health
            };

            for (int tick = 0; tick <= host.Run.TicksPerSecond; tick++)
            {
                host.AdvanceFrame(secondsPerTick, Vector2.up);
            }

            Assert.That(host.World.WarningVisible, Is.False);
            Assert.That(host.Run.Members[0].Health, Is.EqualTo(healthAtWarning[0]));
            Assert.That(host.Run.Members[1].Health, Is.EqualTo(healthAtWarning[1]));
            Assert.That(host.Run.Members[2].Health, Is.EqualTo(healthAtWarning[2]));
            yield return null;
        }

        [UnityTest]
        public IEnumerator DeadMemberTurnsGray()
        {
            LocalExpeditionHost host = _entry.ActiveHost;
            host.enabled = false;
            float secondsPerTick = 1.0f / host.Run.TicksPerSecond;
            ExpeditionEnemy boss = FindBoss(host.Run);
            ExpeditionMember deadMember = null;

            for (int tick = 0; tick < 600 && deadMember == null; tick++)
            {
                Vector2 direction = host.Run.SquadY < boss.Y ? Vector2.up : Vector2.zero;
                host.AdvanceFrame(secondsPerTick, direction);
                deadMember = FindDeadMember(host.Run);
            }

            Assert.That(deadMember, Is.Not.Null);
            GameObject memberObject = GameObject.Find($"Member_{deadMember.Id}");
            Assert.That(memberObject, Is.Not.Null);
            Color memberColor = GetRendererColor(memberObject.GetComponent<Renderer>());
            AssertClose(Color.gray.r, memberColor.r);
            AssertClose(Color.gray.g, memberColor.g);
            AssertClose(Color.gray.b, memberColor.b);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FrameCatchUpIsCappedAtFiveTicks()
        {
            LocalExpeditionHost host = _entry.ActiveHost;
            host.enabled = false;
            LogAssert.Expect(
                LogType.Warning,
                "[Expedition] 单帧固定 Tick 追赶超过 5 次，已丢弃本地积压");
            long initialTick = host.Run.Tick;

            host.AdvanceFrame(1.0f, Vector2.zero);

            Assert.That(host.Run.Tick - initialTick, Is.EqualTo(5L));
            yield return null;
        }

        [UnityTest]
        public IEnumerator RestartReplacesRunWithoutAccumulatingHosts()
        {
            LocalExpeditionHost previousHost = _entry.ActiveHost;
            previousHost.enabled = false;
            previousHost.AdvanceFrame(1.0f / previousHost.Run.TicksPerSecond, Vector2.right);
            GameObject previousRoot = _entry.ActiveRunRoot;

            _entry.RestartExpedition();
            LocalExpeditionHost currentHost = _entry.ActiveHost;
            currentHost.enabled = false;
            Assert.That(currentHost.Run.Tick, Is.EqualTo(0L));
            Assert.That(currentHost.Run.CollectedResource, Is.EqualTo(0L));
            yield return null;

            Assert.That(previousRoot == null, Is.True);
            Assert.That(currentHost, Is.Not.SameAs(previousHost));
            Assert.That(Object.FindObjectsOfType<LocalExpeditionHost>(), Has.Length.EqualTo(1));
        }

        private static ExpeditionEnemy FindBoss(ExpeditionRun run)
        {
            for (int index = 0; index < run.Enemies.Count; index++)
            {
                if (run.Enemies[index].Kind == ExpeditionEnemyKind.Boss)
                {
                    return run.Enemies[index];
                }
            }

            return null;
        }

        private static ExpeditionMember FindDeadMember(ExpeditionRun run)
        {
            for (int index = 0; index < run.Members.Count; index++)
            {
                if (!run.Members[index].IsAlive)
                {
                    return run.Members[index];
                }
            }

            return null;
        }

        private static Color GetRendererColor(Renderer renderer)
        {
            MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(propertyBlock);
            return propertyBlock.GetColor(Shader.PropertyToID("_Color"));
        }

        private static void AssertClose(float expected, float actual)
        {
            Assert.That(actual, Is.EqualTo(expected).Within(0.0001f));
        }
    }
}
