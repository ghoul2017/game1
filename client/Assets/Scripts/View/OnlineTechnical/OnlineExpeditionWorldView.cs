using System;
using System.Collections.Generic;
using Shared.Common.Protos;
using UnityEngine;
using UnityEngine.Rendering;

namespace Client.View.OnlineTechnical
{
    /// <summary>
    /// 把服务端完整快照投影为可玩的灰盒世界。它不创建或推进任何本地玩法实体。
    /// </summary>
    public sealed class OnlineExpeditionWorldView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private Camera _camera;
        private int _contentSignature = int.MinValue;
        private GameObject _contentRoot;
        private GameObject[] _enemyObjects = Array.Empty<GameObject>();
        private Renderer[] _enemyRenderers = Array.Empty<Renderer>();
        private int[] _enemyIds = Array.Empty<int>();
        private GameObject _ground;
        private Transform[] _memberTransforms = Array.Empty<Transform>();
        private Renderer[] _memberRenderers = Array.Empty<Renderer>();
        private int[] _memberIds = Array.Empty<int>();
        private MaterialPropertyBlock _propertyBlock;
        private GameObject[] _resourceObjects = Array.Empty<GameObject>();
        private Renderer[] _resourceRenderers = Array.Empty<Renderer>();
        private int[] _resourceIds = Array.Empty<int>();
        private GameObject _targetMarker;
        private GameObject _warning;

        public int DoorViewCount { get; private set; }

        public int EnemyViewCount => _enemyObjects.Length;

        public int RenderedRoomId { get; private set; }

        public long RenderedRunId { get; private set; }

        public int ResourceViewCount => _resourceObjects.Length;

        public int RouteMarkerCount { get; private set; }

        public bool WarningVisible => _warning != null && _warning.activeSelf;

        public Camera WorldCamera => _camera;

        public void Initialize(ExpeditionSnapshotData snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            _propertyBlock = new MaterialPropertyBlock();
            RenderedRunId = snapshot.RunId;
            CreateLighting();
            CreateGround(snapshot);
            CreateMembers(snapshot);
            CreateMarkers();
            CreateCamera(snapshot);
            Synchronize(snapshot);
            Render(snapshot.SquadX, snapshot.SquadY, 0.0f);
        }

        public void Synchronize(ExpeditionSnapshotData snapshot)
        {
            if (snapshot == null || snapshot.RunId != RenderedRunId)
            {
                return;
            }

            EnsureMembers(snapshot);
            int signature = GetContentSignature(snapshot);
            if (signature != _contentSignature)
            {
                _contentSignature = signature;
                RebuildRoomContent(snapshot);
            }

            SynchronizeMembers(snapshot);
            SynchronizeEnemies(snapshot);
            SynchronizeResources(snapshot);
            SynchronizeTarget(snapshot);
            SynchronizeWarning(snapshot);
        }

        public void Render(float squadX, float squadY, float deltaTime)
        {
            if (_memberTransforms.Length == 0 || _camera == null)
            {
                return;
            }

            Vector3 rootPosition = new Vector3(squadX, 1.0f, squadY);
            float leaderBlend = GetBlend(10.0f, deltaTime);
            _memberTransforms[0].position = Vector3.Lerp(
                _memberTransforms[0].position,
                rootPosition,
                leaderBlend);

            float followerBlend = GetBlend(6.0f, deltaTime);
            for (int index = 1; index < _memberTransforms.Length; index++)
            {
                Vector3 target = _memberTransforms[0].position + GetFormationOffset(index);
                _memberTransforms[index].position = Vector3.Lerp(
                    _memberTransforms[index].position,
                    target,
                    followerBlend);
            }

            Vector3 cameraTarget = _memberTransforms[0].position + new Vector3(0.0f, 15.0f, -11.0f);
            float cameraBlend = GetBlend(6.0f, deltaTime);
            _camera.transform.position = Vector3.Lerp(_camera.transform.position, cameraTarget, cameraBlend);
        }

        private void CreateLighting()
        {
            GameObject lightObject = new GameObject("OnlineExpeditionLight", typeof(Light));
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.rotation = Quaternion.Euler(50.0f, -30.0f, 0.0f);
            Light light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1.0f, 0.94f, 0.84f);
            light.intensity = 1.1f;
            light.shadows = LightShadows.None;

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.22f, 0.25f, 0.3f);
            RenderSettings.skybox = null;
        }

        private void CreateGround(ExpeditionSnapshotData snapshot)
        {
            float width = snapshot.MaximumX - snapshot.MinimumX;
            float depth = snapshot.MaximumY - snapshot.MinimumY;
            Vector3 center = new Vector3(
                (snapshot.MinimumX + snapshot.MaximumX) * 0.5f,
                -0.2f,
                (snapshot.MinimumY + snapshot.MaximumY) * 0.5f);
            _ground = CreatePrimitive(
                PrimitiveType.Cube,
                "OnlineExpeditionGround",
                center,
                new Vector3(width, 0.3f, depth),
                new Color(0.12f, 0.16f, 0.2f),
                transform);
        }

        private void CreateMembers(ExpeditionSnapshotData snapshot)
        {
            GameObject memberRoot = new GameObject("OnlineSquadView");
            memberRoot.transform.SetParent(transform, false);
            _memberTransforms = new Transform[snapshot.Members.Count];
            _memberRenderers = new Renderer[snapshot.Members.Count];
            _memberIds = new int[snapshot.Members.Count];
            Vector3 rootPosition = new Vector3(snapshot.SquadX, 1.0f, snapshot.SquadY);

            for (int index = 0; index < snapshot.Members.Count; index++)
            {
                ExpeditionMemberStateData member = snapshot.Members[index];
                GameObject memberObject = CreatePrimitive(
                    PrimitiveType.Capsule,
                    $"OnlineMember_{member.Id}",
                    rootPosition + GetFormationOffset(index),
                    index == 0
                        ? new Vector3(0.9f, 1.0f, 0.9f)
                        : new Vector3(0.75f, 0.8f, 0.75f),
                    GetMemberColor(index),
                    memberRoot.transform);
                _memberTransforms[index] = memberObject.transform;
                _memberRenderers[index] = memberObject.GetComponent<Renderer>();
                _memberIds[index] = member.Id;
            }
        }

        private void EnsureMembers(ExpeditionSnapshotData snapshot)
        {
            bool rebuild = snapshot.Members.Count != _memberIds.Length;
            for (int index = 0; !rebuild && index < snapshot.Members.Count; index++)
            {
                rebuild = snapshot.Members[index].Id != _memberIds[index];
            }

            if (!rebuild)
            {
                return;
            }

            GameObject oldRoot = GameObject.Find("OnlineSquadView");
            if (oldRoot != null && oldRoot.transform.IsChildOf(transform))
            {
                oldRoot.SetActive(false);
                Destroy(oldRoot);
            }

            CreateMembers(snapshot);
        }

        private void CreateMarkers()
        {
            _targetMarker = CreatePrimitive(
                PrimitiveType.Cylinder,
                "OnlineTargetMarker",
                Vector3.zero,
                new Vector3(2.0f, 0.03f, 2.0f),
                new Color(0.2f, 0.9f, 1.0f),
                transform);
            _targetMarker.SetActive(false);

            _warning = CreatePrimitive(
                PrimitiveType.Cylinder,
                "OnlineBossWarning",
                Vector3.zero,
                new Vector3(1.0f, 0.04f, 1.0f),
                new Color(1.0f, 0.05f, 0.05f),
                transform);
            _warning.SetActive(false);
        }

        private void CreateCamera(ExpeditionSnapshotData snapshot)
        {
            GameObject cameraObject = new GameObject("OnlineExpeditionCamera", typeof(Camera), typeof(AudioListener));
            cameraObject.transform.SetParent(transform, false);
            cameraObject.tag = "MainCamera";
            cameraObject.transform.rotation = Quaternion.Euler(55.0f, 0.0f, 0.0f);
            cameraObject.transform.position = new Vector3(snapshot.SquadX, 16.0f, snapshot.SquadY - 11.0f);

            _camera = cameraObject.GetComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = 9.5f;
            _camera.nearClipPlane = 0.3f;
            _camera.farClipPlane = 100.0f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.035f, 0.05f, 0.075f);
        }

        private void RebuildRoomContent(ExpeditionSnapshotData snapshot)
        {
            if (_contentRoot != null)
            {
                _contentRoot.SetActive(false);
                Destroy(_contentRoot);
            }

            _contentRoot = new GameObject("OnlineCurrentRoomContent");
            _contentRoot.transform.SetParent(transform, false);
            RenderedRoomId = snapshot.CurrentRoomId;
            CreateEnemies(snapshot);
            CreateResources(snapshot);
            CreateDoorsAndRoute(snapshot);
            _targetMarker.SetActive(false);
            _warning.SetActive(false);
        }

        private void CreateEnemies(ExpeditionSnapshotData snapshot)
        {
            _enemyObjects = new GameObject[snapshot.Enemies.Count];
            _enemyRenderers = new Renderer[snapshot.Enemies.Count];
            _enemyIds = new int[snapshot.Enemies.Count];
            for (int index = 0; index < snapshot.Enemies.Count; index++)
            {
                ExpeditionEnemyStateData enemy = snapshot.Enemies[index];
                bool isBoss = enemy.Kind == 1;
                GameObject enemyObject = CreatePrimitive(
                    isBoss ? PrimitiveType.Cube : PrimitiveType.Sphere,
                    isBoss ? $"OnlineBoss_{enemy.Id}" : $"OnlineEnemy_{enemy.Id}",
                    new Vector3(enemy.X, isBoss ? 1.25f : 0.7f, enemy.Y),
                    isBoss ? new Vector3(2.5f, 2.5f, 2.5f) : new Vector3(1.4f, 1.4f, 1.4f),
                    isBoss ? new Color(0.75f, 0.08f, 0.15f) : new Color(1.0f, 0.35f, 0.2f),
                    _contentRoot.transform);
                _enemyObjects[index] = enemyObject;
                _enemyRenderers[index] = enemyObject.GetComponent<Renderer>();
                _enemyIds[index] = enemy.Id;
            }
        }

        private void CreateResources(ExpeditionSnapshotData snapshot)
        {
            _resourceObjects = new GameObject[snapshot.Resources.Count];
            _resourceRenderers = new Renderer[snapshot.Resources.Count];
            _resourceIds = new int[snapshot.Resources.Count];
            for (int index = 0; index < snapshot.Resources.Count; index++)
            {
                ExpeditionResourceStateData resource = snapshot.Resources[index];
                GameObject resourceObject = CreatePrimitive(
                    PrimitiveType.Cylinder,
                    $"OnlineResource_{resource.Id}",
                    new Vector3(resource.X, 0.4f, resource.Y),
                    new Vector3(1.3f, 0.4f, 1.3f),
                    new Color(1.0f, 0.78f, 0.12f),
                    _contentRoot.transform);
                _resourceObjects[index] = resourceObject;
                _resourceRenderers[index] = resourceObject.GetComponent<Renderer>();
                _resourceIds[index] = resource.Id;
            }
        }

        private void CreateDoorsAndRoute(ExpeditionSnapshotData snapshot)
        {
            ExpeditionRoomStateData currentRoom = null;
            for (int index = 0; index < snapshot.RevealedRooms.Count; index++)
            {
                if (snapshot.RevealedRooms[index].IsCurrent)
                {
                    currentRoom = snapshot.RevealedRooms[index];
                    break;
                }
            }

            DoorViewCount = currentRoom == null ? 0 : currentRoom.VisibleExits.Count;
            if (currentRoom != null)
            {
                for (int index = 0; index < currentRoom.VisibleExits.Count; index++)
                {
                    ExpeditionVisibleExitData exit = currentRoom.VisibleExits[index];
                    bool northOrSouth = exit.Direction == 0 || exit.Direction == 2;
                    Color color = exit.HasDangerHint
                        ? new Color(0.95f, 0.15f, 0.12f)
                        : exit.IsOpen
                            ? new Color(0.1f, 0.9f, 0.35f)
                            : new Color(0.28f, 0.3f, 0.34f);
                    CreatePrimitive(
                        PrimitiveType.Cube,
                        $"OnlineRoomDoor_{exit.TargetRoomId}",
                        new Vector3(exit.X, 0.5f, exit.Y),
                        northOrSouth
                            ? new Vector3(2.4f, 1.0f, 0.35f)
                            : new Vector3(0.35f, 1.0f, 2.4f),
                        color,
                        _contentRoot.transform);
                }
            }

            RouteMarkerCount = snapshot.RevealedRooms.Count;
            float roomWidth = snapshot.MaximumX - snapshot.MinimumX;
            float availableWidth = Mathf.Max(1.0f, roomWidth - 2.0f);
            float spacing = RouteMarkerCount <= 1
                ? 0.0f
                : Mathf.Min(1.25f, availableWidth / (RouteMarkerCount - 1));
            float startX = (snapshot.MinimumX + snapshot.MaximumX) * 0.5f -
                spacing * (RouteMarkerCount - 1) * 0.5f;
            float markerY = snapshot.MinimumY + 0.75f;
            for (int index = 0; index < snapshot.RevealedRooms.Count; index++)
            {
                ExpeditionRoomStateData room = snapshot.RevealedRooms[index];
                Color color = room.IsCurrent
                    ? new Color(0.15f, 0.85f, 1.0f)
                    : room.Kind == 4
                        ? new Color(0.8f, 0.12f, 0.16f)
                        : room.State == 2
                            ? new Color(0.12f, 0.65f, 0.28f)
                            : new Color(0.9f, 0.65f, 0.12f);
                CreatePrimitive(
                    PrimitiveType.Cube,
                    $"OnlineRouteRoom_{room.RoomId}",
                    new Vector3(startX + spacing * index, 0.15f, markerY),
                    new Vector3(0.65f, 0.25f, 0.65f),
                    color,
                    _contentRoot.transform);
            }
        }

        private void SynchronizeMembers(ExpeditionSnapshotData snapshot)
        {
            for (int index = 0; index < snapshot.Members.Count; index++)
            {
                ExpeditionMemberStateData member = snapshot.Members[index];
                Color color = Color.gray;
                if (member.Health > 0)
                {
                    float lostHealth = 1.0f - (float)member.Health / member.MaxHealth;
                    color = Color.Lerp(GetMemberColor(index), new Color(0.65f, 0.15f, 0.15f), lostHealth * 0.6f);
                }

                SetColor(_memberRenderers[index], color);
            }
        }

        private void SynchronizeEnemies(ExpeditionSnapshotData snapshot)
        {
            for (int index = 0; index < snapshot.Enemies.Count; index++)
            {
                ExpeditionEnemyStateData enemy = snapshot.Enemies[index];
                GameObject enemyObject = FindObject(enemy.Id, _enemyIds, _enemyObjects);
                Renderer renderer = FindRenderer(enemy.Id, _enemyIds, _enemyRenderers);
                if (enemyObject == null || renderer == null)
                {
                    continue;
                }

                enemyObject.SetActive(enemy.Health > 0);
                if (enemy.Health <= 0)
                {
                    continue;
                }

                float lostHealth = 1.0f - (float)enemy.Health / enemy.MaxHealth;
                Color baseColor = enemy.Kind == 1
                    ? new Color(0.75f, 0.08f, 0.15f)
                    : new Color(1.0f, 0.35f, 0.2f);
                SetColor(renderer, Color.Lerp(baseColor, Color.black, lostHealth * 0.65f));
            }
        }

        private void SynchronizeResources(ExpeditionSnapshotData snapshot)
        {
            for (int index = 0; index < snapshot.Resources.Count; index++)
            {
                ExpeditionResourceStateData resource = snapshot.Resources[index];
                GameObject resourceObject = FindObject(resource.Id, _resourceIds, _resourceObjects);
                Renderer renderer = FindRenderer(resource.Id, _resourceIds, _resourceRenderers);
                if (resourceObject == null || renderer == null)
                {
                    continue;
                }

                resourceObject.SetActive(resource.RemainingAmount > 0);
                if (resource.RemainingAmount <= 0)
                {
                    continue;
                }

                float remaining = (float)resource.RemainingAmount / resource.InitialAmount;
                SetColor(
                    renderer,
                    Color.Lerp(new Color(0.45f, 0.3f, 0.05f), new Color(1.0f, 0.78f, 0.12f), remaining));
            }
        }

        private void SynchronizeTarget(ExpeditionSnapshotData snapshot)
        {
            if (snapshot.TargetKind == 0)
            {
                _targetMarker.SetActive(false);
                return;
            }

            if (!TryFindTarget(snapshot, out float x, out float y))
            {
                _targetMarker.SetActive(false);
                return;
            }

            _targetMarker.transform.position = new Vector3(x, 0.05f, y);
            _targetMarker.SetActive(true);
        }

        private void SynchronizeWarning(ExpeditionSnapshotData snapshot)
        {
            for (int index = 0; index < snapshot.Enemies.Count; index++)
            {
                ExpeditionEnemyStateData enemy = snapshot.Enemies[index];
                if (!enemy.WarningActive)
                {
                    continue;
                }

                _warning.transform.position = new Vector3(enemy.WarningX, 0.07f, enemy.WarningY);
                _warning.transform.localScale = new Vector3(
                    enemy.WarningRadius * 2.0f,
                    0.04f,
                    enemy.WarningRadius * 2.0f);
                _warning.SetActive(true);
                return;
            }

            _warning.SetActive(false);
        }

        private bool TryFindTarget(ExpeditionSnapshotData snapshot, out float x, out float y)
        {
            x = 0.0f;
            y = 0.0f;
            if (snapshot.TargetKind == 1)
            {
                for (int index = 0; index < snapshot.Enemies.Count; index++)
                {
                    ExpeditionEnemyStateData enemy = snapshot.Enemies[index];
                    if (enemy.Id == snapshot.TargetId && enemy.Health > 0)
                    {
                        x = enemy.X;
                        y = enemy.Y;
                        return true;
                    }
                }

                return false;
            }

            for (int index = 0; index < snapshot.Resources.Count; index++)
            {
                ExpeditionResourceStateData resource = snapshot.Resources[index];
                if (resource.Id == snapshot.TargetId && resource.RemainingAmount > 0)
                {
                    x = resource.X;
                    y = resource.Y;
                    return true;
                }
            }

            return false;
        }

        private GameObject CreatePrimitive(
            PrimitiveType primitiveType,
            string objectName,
            Vector3 position,
            Vector3 scale,
            Color color,
            Transform parent)
        {
            GameObject result = GameObject.CreatePrimitive(primitiveType);
            result.name = objectName;
            result.transform.SetParent(parent, false);
            result.transform.position = position;
            result.transform.localScale = scale;
            Collider collider = result.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            SetColor(result.GetComponent<Renderer>(), color);
            return result;
        }

        private void SetColor(Renderer target, Color color)
        {
            _propertyBlock.Clear();
            _propertyBlock.SetColor(ColorId, color);
            _propertyBlock.SetColor(BaseColorId, color);
            target.SetPropertyBlock(_propertyBlock);
        }

        private static int GetContentSignature(ExpeditionSnapshotData snapshot)
        {
            unchecked
            {
                int result = snapshot.CurrentRoomId;
                for (int index = 0; index < snapshot.Enemies.Count; index++)
                {
                    result = result * 31 + snapshot.Enemies[index].Id;
                }

                for (int index = 0; index < snapshot.Resources.Count; index++)
                {
                    result = result * 31 + snapshot.Resources[index].Id;
                }

                for (int index = 0; index < snapshot.RevealedRooms.Count; index++)
                {
                    ExpeditionRoomStateData room = snapshot.RevealedRooms[index];
                    result = result * 31 + room.RoomId;
                    result = result * 31 + room.State;
                    result = result * 31 + (room.IsCurrent ? 1 : 0);
                    for (int exitIndex = 0; exitIndex < room.VisibleExits.Count; exitIndex++)
                    {
                        ExpeditionVisibleExitData exit = room.VisibleExits[exitIndex];
                        result = result * 31 + exit.TargetRoomId;
                        result = result * 31 + (exit.IsOpen ? 1 : 0);
                        result = result * 31 + (exit.HasDangerHint ? 1 : 0);
                    }
                }

                return result;
            }
        }

        private static GameObject FindObject(int id, int[] ids, GameObject[] objects)
        {
            for (int index = 0; index < ids.Length; index++)
            {
                if (ids[index] == id)
                {
                    return objects[index];
                }
            }

            return null;
        }

        private static Renderer FindRenderer(int id, int[] ids, Renderer[] renderers)
        {
            for (int index = 0; index < ids.Length; index++)
            {
                if (ids[index] == id)
                {
                    return renderers[index];
                }
            }

            return null;
        }

        private static Vector3 GetFormationOffset(int index)
        {
            if (index == 0)
            {
                return Vector3.zero;
            }

            int row = (index + 1) / 2;
            float side = index % 2 == 1 ? -1.0f : 1.0f;
            return new Vector3(side * 1.2f, 0.0f, -1.1f * row);
        }

        private static Color GetMemberColor(int index)
        {
            if (index == 0)
            {
                return new Color(0.15f, 0.8f, 1.0f);
            }

            return index % 2 == 1
                ? new Color(0.25f, 0.95f, 0.45f)
                : new Color(0.65f, 0.45f, 1.0f);
        }

        private static float GetBlend(float speed, float deltaTime)
        {
            return deltaTime <= 0.0f ? 1.0f : 1.0f - Mathf.Exp(-speed * deltaTime);
        }
    }
}
