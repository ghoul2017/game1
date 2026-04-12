using System.Collections.Generic;
using GameShared.Protos;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Game
{
    /// <summary>
    /// 其他玩家视图组件。挂载在远程玩家 GameObject 上。
    /// 绿色身体 + 平滑插值移动 + 名字板。
    /// </summary>
    public class OtherPlayerView : MonoBehaviour
    {
        private Vector3 _targetPos;
        private Canvas _nameplateCanvas;
        private RectTransform _hpFillRect;
        private int _maxHp = 100;

        public long RoleId { get; private set; }

        public void Init(long roleId, string name, int x, int y)
        {
            RoleId = roleId;
            transform.position = MapData.GridToWorld(x, y);
            _targetPos = transform.position;

            CreateBody();
            CreateHead();
            CreateNameplate(name);
        }

        public void MoveTo(int x, int y)
        {
            _targetPos = MapData.GridToWorld(x, y);
        }

        public void UpdateHp(int hp, int maxHp)
        {
            _maxHp = maxHp > 0 ? maxHp : 100;
            if (_hpFillRect != null)
            {
                float ratio = (float)hp / _maxHp;
                _hpFillRect.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);
            }
        }

        private void Update()
        {
            if (Vector3.SqrMagnitude(transform.position - _targetPos) > 0.001f)
            {
                transform.position = Vector3.Lerp(transform.position, _targetPos, Time.deltaTime * 8f);

                Vector3 dir = _targetPos - transform.position;
                if (dir.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.LookRotation(dir);
            }
        }

        private void CreateBody()
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.parent = transform;
            body.transform.localPosition = new Vector3(0, 0.5f, 0);
            body.transform.localScale = new Vector3(0.6f, 0.5f, 0.6f);

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(0.2f, 0.7f, 0.3f);
            body.GetComponent<Renderer>().material = mat;
        }

        private void CreateHead()
        {
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.parent = transform;
            head.transform.localPosition = new Vector3(0, 1.4f, 0);
            head.transform.localScale = Vector3.one * 0.35f;

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(0.9f, 0.75f, 0.6f);
            head.GetComponent<Renderer>().material = mat;
            Destroy(head.GetComponent<SphereCollider>());
        }

        private void CreateNameplate(string name)
        {
            var canvasObj = new GameObject("Nameplate");
            canvasObj.transform.parent = transform;
            canvasObj.transform.localPosition = new Vector3(0, 2.2f, 0);

            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 10;

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 100;

            _nameplateCanvas = canvas;

            var bgObj = new GameObject("Bg");
            bgObj.transform.SetParent(canvasObj.transform, false);
            var bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.sizeDelta = new Vector2(160, 48);
            bgObj.AddComponent<Image>().color = new Color(0, 0, 0, 0.5f);

            var textObj = new GameObject("NameText");
            textObj.transform.SetParent(canvasObj.transform, false);
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0, 0.5f);
            textRect.anchorMax = new Vector2(1, 1);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var nameText = textObj.AddComponent<Text>();
            nameText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            nameText.text = name;
            nameText.fontSize = 14;
            nameText.color = new Color(0.7f, 0.9f, 1f);
            nameText.alignment = TextAnchor.MiddleCenter;
            nameText.fontStyle = FontStyle.Bold;

            // HP条背景
            var hpBgObj = new GameObject("HpBg");
            hpBgObj.transform.SetParent(canvasObj.transform, false);
            var hpBgRect = hpBgObj.AddComponent<RectTransform>();
            hpBgRect.anchorMin = new Vector2(0.5f, 0);
            hpBgRect.anchorMax = new Vector2(0.5f, 0);
            hpBgRect.pivot = new Vector2(0.5f, 0);
            hpBgRect.sizeDelta = new Vector2(140, 10);
            hpBgObj.AddComponent<Image>().color = new Color(0.2f, 0.1f, 0.1f, 0.8f);

            // HP条填充
            var hpFillObj = new GameObject("HpFill");
            hpFillObj.transform.SetParent(hpBgObj.transform, false);
            _hpFillRect = hpFillObj.AddComponent<RectTransform>();
            _hpFillRect.anchorMin = Vector2.zero;
            _hpFillRect.anchorMax = Vector2.one;
            _hpFillRect.offsetMin = Vector2.zero;
            _hpFillRect.offsetMax = Vector2.zero;
            hpFillObj.AddComponent<Image>().color = new Color(0.8f, 0.2f, 0.2f);

            var canvasRect = canvasObj.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(160, 48);

            canvasObj.transform.localScale = Vector3.one * 0.015f;
        }

        private void LateUpdate()
        {
            if (_nameplateCanvas != null && Camera.main != null)
            {
                _nameplateCanvas.transform.rotation = Camera.main.transform.rotation;
            }
        }
    }

    /// <summary>
    /// 其他玩家管理器。管理视野内其他玩家的创建、移动、销毁。
    /// </summary>
    public class OtherPlayerManager
    {
        private static OtherPlayerManager _instance;
        public static OtherPlayerManager Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new OtherPlayerManager();
                return _instance;
            }
        }

        private readonly Dictionary<long, OtherPlayerView> _players = new Dictionary<long, OtherPlayerView>();
        private Transform _container;

        public void Init(Transform container)
        {
            _container = container;
        }

        public void SpawnPlayers(List<MapObjectInfo> objects)
        {
            if (objects == null) return;
            foreach (var obj in objects)
            {
                SpawnPlayer(obj);
            }
        }

        public void SpawnPlayer(MapObjectInfo info)
        {
            if (_players.ContainsKey(info.ObjectId)) return;

            var obj = new GameObject("OtherPlayer_" + info.ObjectId);
            if (_container != null)
                obj.transform.parent = _container;

            var view = obj.AddComponent<OtherPlayerView>();
            view.Init(info.ObjectId, info.Name, info.X, info.Y);
            _players[info.ObjectId] = view;

            Debug.Log(string.Format("[OtherPlayerManager] 玩家进入视野 RoleId:{0} Name:{1} Pos:({2},{3})",
                info.ObjectId, info.Name, info.X, info.Y));
        }

        public void RemovePlayer(long roleId)
        {
            if (!_players.TryGetValue(roleId, out var view)) return;

            _players.Remove(roleId);
            if (view != null)
                Object.Destroy(view.gameObject);

            Debug.Log(string.Format("[OtherPlayerManager] 玩家离开视野 RoleId:{0}", roleId));
        }

        public void MovePlayer(long roleId, int x, int y)
        {
            if (!_players.TryGetValue(roleId, out var view)) return;
            view.MoveTo(x, y);
        }

        public void UpdatePlayerHp(long roleId, int hp, int maxHp)
        {
            if (!_players.TryGetValue(roleId, out var view)) return;
            view.UpdateHp(hp, maxHp);
        }

        public void ClearAll()
        {
            foreach (var kvp in _players)
            {
                if (kvp.Value != null)
                    Object.Destroy(kvp.Value.gameObject);
            }
            _players.Clear();
        }
    }
}
