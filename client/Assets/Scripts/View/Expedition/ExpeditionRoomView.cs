using GameplayCore;
using UnityEngine;

namespace Client.Expedition
{
    /// <summary>
    /// 只依据玩家可见的房间投影绘制房门、危险提示和已发现路线。
    /// </summary>
    public sealed class ExpeditionRoomView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private int _currentRoomId;
        private int _doorViewCount;
        private bool _hasDangerHint;
        private MaterialPropertyBlock _propertyBlock;
        private int _routeMarkerCount;
        private int _signature = int.MinValue;
        private GameObject _visualRoot;

        public int CurrentRoomId => _currentRoomId;

        public int DoorViewCount => _doorViewCount;

        public int RouteMarkerCount => _routeMarkerCount;

        public bool HasDangerHint => _hasDangerHint;

        public void Initialize(ExpeditionRun run)
        {
            _propertyBlock = new MaterialPropertyBlock();
            Synchronize(run);
        }

        public void Synchronize(ExpeditionRun run)
        {
            if (!run.UsesRoomGraph)
            {
                gameObject.SetActive(false);
                return;
            }

            int signature = GetSignature(run);
            if (_signature == signature)
            {
                return;
            }

            _signature = signature;
            Rebuild(run);
        }

        private void Rebuild(ExpeditionRun run)
        {
            if (_visualRoot != null)
            {
                _visualRoot.SetActive(false);
                Destroy(_visualRoot);
            }

            _visualRoot = new GameObject("RoomProjection");
            _visualRoot.transform.SetParent(transform, false);
            _currentRoomId = run.CurrentRoomId;
            _doorViewCount = run.VisibleExits.Count;
            _routeMarkerCount = run.DiscoveredRooms.Count;
            _hasDangerHint = false;

            CreateDoors(run);
            CreateRouteMarkers(run);
        }

        private void CreateDoors(ExpeditionRun run)
        {
            for (int index = 0; index < run.VisibleExits.Count; index++)
            {
                ExpeditionVisibleExit exit = run.VisibleExits[index];
                _hasDangerHint |= exit.HasDangerHint;
                Color color = GetDoorColor(exit);
                Vector3 scale = exit.Direction == ExpeditionDoorDirection.North ||
                    exit.Direction == ExpeditionDoorDirection.South
                    ? new Vector3(2.4f, 1.0f, 0.35f)
                    : new Vector3(0.35f, 1.0f, 2.4f);
                CreatePrimitive(
                    $"RoomDoor_{exit.TargetRoomId}",
                    new Vector3(exit.X, 0.5f, exit.Y),
                    scale,
                    color);
            }
        }

        private void CreateRouteMarkers(ExpeditionRun run)
        {
            float roomWidth = run.MaximumX - run.MinimumX;
            float availableWidth = Mathf.Max(1.0f, roomWidth - 2.0f);
            float spacing = run.DiscoveredRooms.Count <= 1
                ? 0.0f
                : Mathf.Min(1.25f, availableWidth / (run.DiscoveredRooms.Count - 1));
            float startX = (run.MinimumX + run.MaximumX) * 0.5f -
                spacing * (run.DiscoveredRooms.Count - 1) * 0.5f;
            float markerY = run.MinimumY + 0.75f;

            for (int index = 0; index < run.DiscoveredRooms.Count; index++)
            {
                ExpeditionRoomPublicState room = run.DiscoveredRooms[index];
                Color color = GetRouteColor(room);
                CreatePrimitive(
                    $"RouteRoom_{room.Id}",
                    new Vector3(startX + spacing * index, 0.15f, markerY),
                    new Vector3(0.65f, 0.25f, 0.65f),
                    color);
            }
        }

        private GameObject CreatePrimitive(string objectName, Vector3 position, Vector3 scale, Color color)
        {
            GameObject result = GameObject.CreatePrimitive(PrimitiveType.Cube);
            result.name = objectName;
            result.transform.SetParent(_visualRoot.transform, false);
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

        private static Color GetDoorColor(ExpeditionVisibleExit exit)
        {
            if (exit.HasDangerHint)
            {
                return new Color(0.95f, 0.15f, 0.12f);
            }

            return exit.IsOpen
                ? new Color(0.1f, 0.9f, 0.35f)
                : new Color(0.28f, 0.3f, 0.34f);
        }

        private static Color GetRouteColor(ExpeditionRoomPublicState room)
        {
            if (room.IsCurrent)
            {
                return new Color(0.15f, 0.85f, 1.0f);
            }

            if (room.Kind == ExpeditionRoomKind.Boss)
            {
                return new Color(0.8f, 0.12f, 0.16f);
            }

            return room.Progress == ExpeditionRoomProgress.Cleared
                ? new Color(0.12f, 0.65f, 0.28f)
                : new Color(0.9f, 0.65f, 0.12f);
        }

        private static int GetSignature(ExpeditionRun run)
        {
            unchecked
            {
                int result = 17;
                result = result * 31 + run.CurrentRoomId;
                for (int index = 0; index < run.DiscoveredRooms.Count; index++)
                {
                    ExpeditionRoomPublicState room = run.DiscoveredRooms[index];
                    result = result * 31 + room.Id;
                    result = result * 31 + (int)room.Kind;
                    result = result * 31 + (int)room.Progress;
                    result = result * 31 + (room.IsCurrent ? 1 : 0);
                }

                for (int index = 0; index < run.VisibleExits.Count; index++)
                {
                    ExpeditionVisibleExit exit = run.VisibleExits[index];
                    result = result * 31 + (int)exit.Direction;
                    result = result * 31 + exit.TargetRoomId;
                    result = result * 31 + (exit.IsOpen ? 1 : 0);
                    result = result * 31 + (exit.HasDangerHint ? 1 : 0);
                }

                return result;
            }
        }
    }
}
