using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Client.Expedition.Tests
{
    public sealed class OnlineTechnicalPlayModeTests
    {
        [UnityTearDown]
        public IEnumerator RemovePersistentTechnicalEntry()
        {
            GameObject entry = GameObject.Find("OnlineTechnicalEntry");
            if (entry != null)
            {
                Component entryComponent = GetComponentByTypeName(entry, "OnlineTechnicalEntry");
                object controller = GetSingleton(
                    entryComponent.GetType().Assembly,
                    "Client.Logic.ExpeditionController");
                controller.GetType().GetMethod("ResetForRole").Invoke(controller, new object[] { 0L });
                UnityEngine.Object.Destroy(entry);
            }

            GameObject applicationCanvas = GameObject.Find("Canvas");
            if (applicationCanvas != null)
            {
                UnityEngine.Object.Destroy(applicationCanvas);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator SceneMountsCommandsReplaysAndSurvivesSceneChange()
        {
            SceneManager.LoadScene("OnlineTechnicalScene", LoadSceneMode.Single);
            yield return null;

            Scene scene = SceneManager.GetActiveScene();
            GameObject entryObject = GameObject.Find("OnlineTechnicalEntry");
            Assert.That(scene.path, Is.EqualTo("Assets/Scenes/OnlineTechnicalScene.unity"));
            Assert.That(SceneUtility.GetBuildIndexByScenePath(scene.path), Is.EqualTo(0));
            Assert.That(entryObject, Is.Not.Null);

            Component entry = GetComponentByTypeName(entryObject, "OnlineTechnicalEntry");
            Component expeditionPanel = GetComponentProperty(entry, "ExpeditionPanel");
            Component arenaPanel = GetComponentProperty(entry, "ArenaPanel");
            Assert.That(GetIntProperty(expeditionPanel, "BoundCommandCount"), Is.EqualTo(5));
            Assert.That(GetIntProperty(arenaPanel, "BoundCommandCount"), Is.EqualTo(5));
            AssertCommandObjectsExist();
            VerifyClearedExpeditionStatus(expeditionPanel);

            Button toggle = GameObject.Find("OnlineTechnicalToggleButton").GetComponent<Button>();
            toggle.onClick.Invoke();
            Assert.That(GetBooleanProperty(entry, "ContentVisible"), Is.False);
            toggle.onClick.Invoke();
            Assert.That(GetBooleanProperty(entry, "ContentVisible"), Is.True);

            VerifyDirectionButton(expeditionPanel);
            VerifyArenaReplayControls(arenaPanel);

            int entryInstanceId = entryObject.GetInstanceID();
            SceneManager.LoadScene("ExpeditionVerticalSliceScene", LoadSceneMode.Single);
            yield return null;

            GameObject survivingEntry = GameObject.Find("OnlineTechnicalEntry");
            Assert.That(survivingEntry, Is.Not.Null);
            Assert.That(survivingEntry.GetInstanceID(), Is.EqualTo(entryInstanceId));
        }

        [UnityTest]
        public IEnumerator RoleEntryStaysOutsideOldRpgAndRendersAuthoritativeWorld()
        {
            SceneManager.LoadScene("OnlineTechnicalScene", LoadSceneMode.Single);
            yield return null;

            GameObject entryObject = GameObject.Find("OnlineTechnicalEntry");
            Component entry = GetComponentByTypeName(entryObject, "OnlineTechnicalEntry");
            MethodInfo enterGameplay = entry.GetType().GetMethod(
                "TryEnterGameplay",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That((bool)enterGameplay.Invoke(null, new object[] { 7001L }), Is.True);

            Assembly assembly = entry.GetType().Assembly;
            object controller = GetSingleton(assembly, "Client.Logic.ExpeditionController");
            object runningSnapshot = CreateExpeditionSnapshot(assembly, 8801L, 0, 1L);
            controller.GetType().GetMethod("ApplySnapshot").Invoke(controller, new[] { runningSnapshot });
            yield return null;

            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("OnlineTechnicalScene"));
            Assert.That(GameObject.Find("GameSceneEntry"), Is.Null);
            Assert.That(GameObject.Find("OnlineExpeditionGameplay"), Is.Not.Null);
            Assert.That(GameObject.Find("OnlineExpeditionCamera"), Is.Not.Null);
            Assert.That(GameObject.Find("OnlineExpeditionJoystick"), Is.Not.Null);
            Assert.That(GameObject.Find("OnlineMember_1"), Is.Not.Null);
            Assert.That(GameObject.Find("OnlineBoss_900"), Is.Not.Null);
            Assert.That(GameObject.Find("OnlineResource_2501"), Is.Not.Null);
            Assert.That(GameObject.Find("OnlineRoomDoor_2"), Is.Not.Null);
            Assert.That(GameObject.Find("OnlineBossWarning").activeSelf, Is.True);

            Component gameplay = GetComponentProperty(entry, "GameplayView");
            object endedSnapshot = CreateExpeditionSnapshot(assembly, 8801L, 1, 2L);
            controller.GetType().GetMethod("ApplySnapshot").Invoke(controller, new[] { endedSnapshot });
            yield return null;
            Assert.That(GetBooleanProperty(gameplay, "ResultVisible"), Is.True);

            MethodInfo returnToLogin = entry.GetType().GetMethod(
                "TryReturnToLogin",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That((bool)returnToLogin.Invoke(null, new object[] { "测试断线" }), Is.True);
            yield return null;
            Assert.That(GameObject.Find("OnlineExpeditionGameplay"), Is.Null);
            Assert.That(GameObject.Find("LoginPanel"), Is.Not.Null);
            Assert.That((bool)returnToLogin.Invoke(null, new object[] { "后续通用断线" }), Is.False);
        }

        [UnityTest]
        public IEnumerator RegularSnapshotCorrectionDoesNotInstantlyRewindVisibleSquad()
        {
            SceneManager.LoadScene("OnlineTechnicalScene", LoadSceneMode.Single);
            yield return null;

            GameObject entryObject = GameObject.Find("OnlineTechnicalEntry");
            Component entry = GetComponentByTypeName(entryObject, "OnlineTechnicalEntry");
            Assembly assembly = entry.GetType().Assembly;
            MethodInfo enterGameplay = entry.GetType().GetMethod(
                "TryEnterGameplay",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That((bool)enterGameplay.Invoke(null, new object[] { 7002L }), Is.True);

            object controller = GetSingleton(assembly, "Client.Logic.ExpeditionController");
            long runId = 8802L;
            object initialSnapshot = CreateExpeditionSnapshot(assembly, runId, 0, 1L);
            controller.GetType().GetMethod("ApplySnapshot").Invoke(controller, new[] { initialSnapshot });
            yield return null;

            Component gameplay = GetComponentProperty(entry, "GameplayView");
            Component world = GetComponentProperty(gameplay, "World");
            SetProperty(controller, "PredictedSquadX", 1.65f);
            float predictedX = GetFloatProperty(controller, "PredictedSquadX");
            float predictedY = GetFloatProperty(controller, "PredictedSquadY");
            world.GetType().GetMethod("Render").Invoke(world, new object[] { predictedX, predictedY, 0.0f });
            float visibleXBeforeSnapshot = GameObject.Find("OnlineMember_1").transform.position.x;

            object laggingSnapshot = CreateExpeditionSnapshot(assembly, runId, 0, 2L);
            SetProperty(laggingSnapshot, "SquadX", 1.1f);
            controller.GetType().GetMethod("ApplySnapshot").Invoke(controller, new[] { laggingSnapshot });

            Assert.That(GetFloatProperty(controller, "PredictedSquadX"), Is.LessThan(visibleXBeforeSnapshot));
            Assert.That(
                GameObject.Find("OnlineMember_1").transform.position.x,
                Is.EqualTo(visibleXBeforeSnapshot).Within(0.0001f));
        }

        private static void AssertCommandObjectsExist()
        {
            string[] objectNames =
            {
                "ExpeditionStartButton",
                "ExpeditionResumeButton",
                "ExpeditionSettleButton",
                "ExpeditionUpgradeButton",
                "ExpeditionMoveUpButton",
                "ArenaStartButton",
                "ArenaGetReplayButton",
                "ArenaSpeed1Button",
                "ArenaSpeed4Button",
                "ArenaSkipButton"
            };

            for (int index = 0; index < objectNames.Length; index++)
            {
                Assert.That(GameObject.Find(objectNames[index]), Is.Not.Null, objectNames[index]);
            }
        }

        private static void VerifyDirectionButton(Component expeditionPanel)
        {
            GameObject upButton = GameObject.Find("ExpeditionMoveUpButton");
            Component directionButton = GetComponentByTypeName(upButton, "OnlineDirectionButton");
            MethodInfo pointerDown = directionButton.GetType().GetMethod("OnPointerDown");
            MethodInfo pointerUp = directionButton.GetType().GetMethod("OnPointerUp");
            FieldInfo movement = expeditionPanel.GetType().GetField(
                "_movementDirection",
                BindingFlags.Instance | BindingFlags.NonPublic);

            pointerDown.Invoke(directionButton, new object[] { null });
            Assert.That((Vector2)movement.GetValue(expeditionPanel), Is.EqualTo(Vector2.up));
            pointerUp.Invoke(directionButton, new object[] { null });
            Assert.That((Vector2)movement.GetValue(expeditionPanel), Is.EqualTo(Vector2.zero));
        }

        private static void VerifyClearedExpeditionStatus(Component expeditionPanel)
        {
            MethodInfo snapshotChanged = expeditionPanel.GetType().GetMethod(
                "OnSnapshotChanged",
                BindingFlags.Instance | BindingFlags.NonPublic);
            snapshotChanged.Invoke(expeditionPanel, new object[] { null });
            string status = (string)expeditionPanel.GetType().GetProperty("StatusText").GetValue(expeditionPanel);
            Assert.That(status, Is.EqualTo("当前没有进行中的远征"));
        }

        private static void VerifyArenaReplayControls(Component arenaPanel)
        {
            Component replayPlayer = GetComponentProperty(arenaPanel, "ReplayPlayer");
            object replay = CreateReplay(replayPlayer.GetType().Assembly);
            replayPlayer.GetType().GetMethod("Load").Invoke(replayPlayer, new[] { replay });

            FieldInfo speed = replayPlayer.GetType().GetField(
                "_speed",
                BindingFlags.Instance | BindingFlags.NonPublic);
            GameObject.Find("ArenaSpeed4Button").GetComponent<Button>().onClick.Invoke();
            Assert.That((int)speed.GetValue(replayPlayer), Is.EqualTo(4));
            GameObject.Find("ArenaSpeed1Button").GetComponent<Button>().onClick.Invoke();
            Assert.That((int)speed.GetValue(replayPlayer), Is.EqualTo(1));
            GameObject.Find("ArenaSkipButton").GetComponent<Button>().onClick.Invoke();
            Assert.That(GetLongProperty(replayPlayer, "CurrentTick"), Is.EqualTo(10L));
            Assert.That(GetIntProperty(replayPlayer, "VisibleOutcome"), Is.EqualTo(1));
        }

        private static object CreateReplay(Assembly assembly)
        {
            Type replayType = assembly.GetType("Shared.Common.Protos.ArenaReplayAck", true);
            Type keyFrameType = assembly.GetType("Shared.Common.Protos.ArenaReplayKeyFrameInfo", true);
            Type unitFrameType = assembly.GetType("Shared.Common.Protos.ArenaReplayUnitFrameInfo", true);
            object replay = Activator.CreateInstance(replayType);
            SetProperty(replay, "BattleId", 9001L);
            SetProperty(replay, "TicksPerSecond", 10);
            SetProperty(replay, "FinalTick", 10L);
            SetProperty(replay, "Outcome", 1);

            IList keyFrames = (IList)replayType.GetProperty("KeyFrames").GetValue(replay);
            keyFrames.Add(CreateKeyFrame(keyFrameType, unitFrameType, 0L, 100));
            keyFrames.Add(CreateKeyFrame(keyFrameType, unitFrameType, 10L, 40));
            return replay;
        }

        private static object CreateExpeditionSnapshot(Assembly assembly, long runId, int phase, long tick)
        {
            Type snapshotType = assembly.GetType("Shared.Common.Protos.ExpeditionSnapshotData", true);
            object snapshot = Activator.CreateInstance(snapshotType);
            SetProperty(snapshot, "RunId", runId);
            SetProperty(snapshot, "RunIndex", 1L);
            SetProperty(snapshot, "ServerTick", tick);
            SetProperty(snapshot, "Phase", phase);
            SetProperty(snapshot, "SquadX", 0.0f);
            SetProperty(snapshot, "SquadY", 0.0f);
            SetProperty(snapshot, "CurrentRoomId", 1);
            SetProperty(snapshot, "TargetKind", 1);
            SetProperty(snapshot, "TargetId", 900);
            SetProperty(snapshot, "TicksPerSecond", 10);
            SetProperty(snapshot, "MoveSpeedPerSecond", 5.5f);
            SetProperty(snapshot, "MinimumX", -6.0f);
            SetProperty(snapshot, "MaximumX", 6.0f);
            SetProperty(snapshot, "MinimumY", -6.0f);
            SetProperty(snapshot, "MaximumY", 6.0f);

            IList members = (IList)snapshotType.GetProperty("Members").GetValue(snapshot);
            members.Add(CreateMember(assembly, 1, 120, 120));
            members.Add(CreateMember(assembly, 2, 95, 95));
            members.Add(CreateMember(assembly, 3, 105, 105));

            IList enemies = (IList)snapshotType.GetProperty("Enemies").GetValue(snapshot);
            enemies.Add(CreateEnemy(assembly, 101, 0, -1.0f, 1.0f, 45, false));
            enemies.Add(CreateEnemy(assembly, 900, 1, 1.0f, 1.5f, 180, true));

            IList resources = (IList)snapshotType.GetProperty("Resources").GetValue(snapshot);
            resources.Add(CreateResource(assembly, 2501, 0.0f, 2.0f));

            IList rooms = (IList)snapshotType.GetProperty("RevealedRooms").GetValue(snapshot);
            rooms.Add(CreateRoom(assembly, 1, true, 2));
            rooms.Add(CreateRoom(assembly, 2, false, 0));
            return snapshot;
        }

        private static object CreateMember(Assembly assembly, int id, int health, int maximumHealth)
        {
            Type type = assembly.GetType("Shared.Common.Protos.ExpeditionMemberStateData", true);
            object member = Activator.CreateInstance(type);
            SetProperty(member, "Id", id);
            SetProperty(member, "Health", health);
            SetProperty(member, "MaxHealth", maximumHealth);
            return member;
        }

        private static object CreateEnemy(
            Assembly assembly,
            int id,
            int kind,
            float x,
            float y,
            int health,
            bool warningActive)
        {
            Type type = assembly.GetType("Shared.Common.Protos.ExpeditionEnemyStateData", true);
            object enemy = Activator.CreateInstance(type);
            SetProperty(enemy, "Id", id);
            SetProperty(enemy, "Kind", kind);
            SetProperty(enemy, "X", x);
            SetProperty(enemy, "Y", y);
            SetProperty(enemy, "Health", health);
            SetProperty(enemy, "MaxHealth", health);
            SetProperty(enemy, "WarningActive", warningActive);
            SetProperty(enemy, "WarningX", 1.0f);
            SetProperty(enemy, "WarningY", 1.5f);
            SetProperty(enemy, "WarningRadius", 2.0f);
            return enemy;
        }

        private static object CreateResource(Assembly assembly, int id, float x, float y)
        {
            Type type = assembly.GetType("Shared.Common.Protos.ExpeditionResourceStateData", true);
            object resource = Activator.CreateInstance(type);
            SetProperty(resource, "Id", id);
            SetProperty(resource, "X", x);
            SetProperty(resource, "Y", y);
            SetProperty(resource, "RemainingAmount", 30);
            SetProperty(resource, "InitialAmount", 30);
            return resource;
        }

        private static object CreateRoom(Assembly assembly, int id, bool isCurrent, int targetRoomId)
        {
            Type type = assembly.GetType("Shared.Common.Protos.ExpeditionRoomStateData", true);
            object room = Activator.CreateInstance(type);
            SetProperty(room, "RoomId", id);
            SetProperty(room, "Kind", id == 1 ? 0 : 1);
            SetProperty(room, "State", isCurrent ? 1 : 0);
            SetProperty(room, "IsCurrent", isCurrent);
            if (!isCurrent)
            {
                return room;
            }

            Type exitType = assembly.GetType("Shared.Common.Protos.ExpeditionVisibleExitData", true);
            object exit = Activator.CreateInstance(exitType);
            SetProperty(exit, "Direction", 0);
            SetProperty(exit, "TargetRoomId", targetRoomId);
            SetProperty(exit, "IsOpen", true);
            SetProperty(exit, "X", 0.0f);
            SetProperty(exit, "Y", 5.0f);
            IList exits = (IList)type.GetProperty("VisibleExits").GetValue(room);
            exits.Add(exit);
            return room;
        }

        private static object GetSingleton(Assembly assembly, string typeName)
        {
            Type type = assembly.GetType(typeName, true);
            return type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
        }

        private static object CreateKeyFrame(Type keyFrameType, Type unitFrameType, long tick, int health)
        {
            object keyFrame = Activator.CreateInstance(keyFrameType);
            SetProperty(keyFrame, "Tick", tick);
            object unit = Activator.CreateInstance(unitFrameType);
            SetProperty(unit, "Side", 1);
            SetProperty(unit, "UnitId", 1);
            SetProperty(unit, "Health", health);
            IList units = (IList)keyFrameType.GetProperty("Units").GetValue(keyFrame);
            units.Add(unit);
            return keyFrame;
        }

        private static Component GetComponentProperty(Component owner, string propertyName)
        {
            return (Component)owner.GetType().GetProperty(propertyName).GetValue(owner);
        }

        private static Component GetComponentByTypeName(GameObject owner, string typeName)
        {
            Component[] components = owner.GetComponents<Component>();
            for (int index = 0; index < components.Length; index++)
            {
                if (components[index] != null && components[index].GetType().Name == typeName)
                {
                    return components[index];
                }
            }

            return null;
        }

        private static bool GetBooleanProperty(Component owner, string propertyName)
        {
            return (bool)owner.GetType().GetProperty(propertyName).GetValue(owner);
        }

        private static int GetIntProperty(Component owner, string propertyName)
        {
            return (int)owner.GetType().GetProperty(propertyName).GetValue(owner);
        }

        private static float GetFloatProperty(object owner, string propertyName)
        {
            return (float)owner.GetType().GetProperty(propertyName).GetValue(owner);
        }

        private static long GetLongProperty(Component owner, string propertyName)
        {
            return (long)owner.GetType().GetProperty(propertyName).GetValue(owner);
        }

        private static void SetProperty(object owner, string propertyName, object value)
        {
            owner.GetType().GetProperty(propertyName).SetValue(owner, value);
        }
    }
}
