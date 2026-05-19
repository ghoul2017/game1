using System.Collections;
using System.Collections.Generic;
using Shared.Common.Protos;
using Client.Logic;
using UnityEngine;
using UnityEngine.UI;

namespace Client.View
{
    /// <summary>
    /// 其他玩家视图组件。挂载在远程玩家 GameObject 上。
    /// 绿色身体 + 平滑插值移动 + 名字板 + 战斗表现。
    /// </summary>
    public class OtherPlayerView : MonoBehaviour
    {
        private Vector3 _targetPos;
        private Canvas _nameplateCanvas;
        private RectTransform _hpFillRect;
        private int _maxHp = 100;
        private Material _bodyMat;
        private Material _headMat;
        private Color _bodyColor = new Color(0.2f, 0.7f, 0.3f);
        private Color _headColor = new Color(0.9f, 0.75f, 0.6f);
        private bool _isDead;

        // Attack animation state
        private Vector3 _attackStartPos;
        private Vector3 _attackEndPos;
        private float _attackTimer;
        private bool _isAttacking;

        // Hit flash state
        private float _flashTimer;

        // Death animation state
        private float _deathAngle;

        public long RoleId { get; private set; }

        public void Init(long roleId, string name, int x, int y)
        {
            RoleId = roleId;
            (float px, float py, float pz) = MapData.GridToWorld(x, y);
            transform.position = new Vector3(px, py, pz);
            _targetPos = transform.position;

            CreateBody();
            CreateHead();
            CreateNameplate(name);
        }

        public void MoveTo(int x, int y)
        {
            if (_isDead) return;
            (float px, float py, float pz) = MapData.GridToWorld(x, y);
            _targetPos = new Vector3(px, py, pz);
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

        public void PlayAttack(int skillId)
        {
            if (_isDead) return;
            _isAttacking = true;
            _attackTimer = 0f;
            _attackStartPos = transform.position;
            // Lunge forward 0.4 units in facing direction
            _attackEndPos = transform.position + transform.forward * 0.4f;
        }

        public void ShowDamageNumber(int damage, bool isCritical)
        {
            if (damage == 0) return;

            var dmgObj = new GameObject("DmgNum");
            dmgObj.transform.SetParent(transform, false);
            dmgObj.transform.localPosition = new Vector3(
                Random.Range(-0.3f, 0.3f), 1.8f, 0);

            var canvas = dmgObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 20;
            dmgObj.AddComponent<CanvasScaler>();
            dmgObj.transform.localScale = Vector3.one * 0.008f;

            var textObj = new GameObject("Text");
            textObj.transform.SetParent(dmgObj.transform, false);
            var rect = textObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(200, 80);
            var txt = textObj.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.text = isCritical ? damage + "!" : damage.ToString();
            txt.fontSize = isCritical ? 40 : 30;
            txt.color = isCritical ? new Color(1f, 0.2f, 0.2f) : new Color(1f, 1f, 0.3f);
            txt.alignment = TextAnchor.MiddleCenter;
            txt.fontStyle = FontStyle.Bold;

            StartCoroutine(FloatDamageNumber(dmgObj));
        }

        public void PlayHitFlash()
        {
            if (_isDead) return;
            _flashTimer = 0.15f;
            if (_bodyMat != null) _bodyMat.color = Color.red;
            if (_headMat != null) _headMat.color = Color.red;
        }

        public void PlayDeath()
        {
            _isDead = true;
            StartCoroutine(DeathAnimation());
        }

        public void PlayRevive()
        {
            _isDead = false;
            _deathAngle = 0f;
            transform.rotation = Quaternion.identity;
            SetAlpha(1f);
        }

        private void Update()
        {
            if (_isDead) return;

            // Attack lunge
            if (_isAttacking)
            {
                _attackTimer += Time.deltaTime * 6f;
                if (_attackTimer >= 1f)
                {
                    _isAttacking = false;
                    transform.position = _attackStartPos;
                }
                else
                {
                    // Forward then back
                    float t = _attackTimer < 0.5f
                        ? _attackTimer * 2f
                        : 2f - _attackTimer * 2f;
                    transform.position = Vector3.Lerp(_attackStartPos, _attackEndPos, t);
                }
                return;
            }

            // Normal movement
            if (Vector3.SqrMagnitude(transform.position - _targetPos) > 0.001f)
            {
                transform.position = Vector3.Lerp(transform.position, _targetPos, Time.deltaTime * 8f);

                Vector3 dir = _targetPos - transform.position;
                if (dir.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.LookRotation(dir);
            }

            // Hit flash recovery
            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.deltaTime;
                if (_flashTimer <= 0f)
                {
                    if (_bodyMat != null) _bodyMat.color = _bodyColor;
                    if (_headMat != null) _headMat.color = _headColor;
                }
            }
        }

        private void CreateBody()
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.parent = transform;
            body.transform.localPosition = new Vector3(0, 0.5f, 0);
            body.transform.localScale = new Vector3(0.6f, 0.5f, 0.6f);

            _bodyMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            _bodyMat.color = _bodyColor;
            body.GetComponent<Renderer>().material = _bodyMat;
        }

        private void CreateHead()
        {
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.parent = transform;
            head.transform.localPosition = new Vector3(0, 1.4f, 0);
            head.transform.localScale = Vector3.one * 0.35f;

            _headMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            _headMat.color = _headColor;
            head.GetComponent<Renderer>().material = _headMat;
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

        private IEnumerator FloatDamageNumber(GameObject dmgObj)
        {
            float elapsed = 0f;
            float duration = 1f;
            Vector3 startLocal = dmgObj.transform.localPosition;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                // Float upward
                dmgObj.transform.localPosition = startLocal + Vector3.up * t * 1.5f;
                // Fade out in last 30%
                if (t > 0.7f)
                {
                    float alpha = 1f - (t - 0.7f) / 0.3f;
                    var txt = dmgObj.GetComponentInChildren<Text>();
                    if (txt != null)
                    {
                        var c = txt.color;
                        c.a = alpha;
                        txt.color = c;
                    }
                }
                yield return null;
            }
            Destroy(dmgObj);
        }

        private IEnumerator DeathAnimation()
        {
            float duration = 0.5f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                _deathAngle = Mathf.Min(elapsed / duration * 90f, 90f);
                transform.rotation = Quaternion.Euler(_deathAngle, transform.rotation.eulerAngles.y, 0);
                yield return null;
            }
            // Fade out after falling
            yield return StartCoroutine(FadeOut(0.5f));
        }

        private IEnumerator FadeOut(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                SetAlpha(1f - elapsed / duration);
                yield return null;
            }
            SetAlpha(0f);
        }

        private void SetAlpha(float alpha)
        {
            if (_bodyMat != null)
            {
                var c = _bodyColor;
                c.a = alpha;
                _bodyMat.color = c;
            }
            if (_headMat != null)
            {
                var c = _headColor;
                c.a = alpha;
                _headMat.color = c;
            }
            // Nameplate alpha
            if (_nameplateCanvas != null)
            {
                var cg = _nameplateCanvas.GetComponent<CanvasGroup>();
                if (cg == null) cg = _nameplateCanvas.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = alpha;
            }
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
    /// 其他玩家管理器。管理视野内其他玩家的创建、移动、销毁、战斗表现。
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

        public void PlayAttack(long roleId, int skillId)
        {
            if (!_players.TryGetValue(roleId, out var view)) return;
            view.PlayAttack(skillId);
        }

        public void ShowDamage(long roleId, int damage, bool isCritical)
        {
            if (!_players.TryGetValue(roleId, out var view)) return;
            view.ShowDamageNumber(damage, isCritical);
            view.PlayHitFlash();
        }

        public void PlayDeath(long roleId)
        {
            if (!_players.TryGetValue(roleId, out var view)) return;
            view.PlayDeath();
        }

        public void PlayRevive(long roleId)
        {
            if (!_players.TryGetValue(roleId, out var view)) return;
            view.PlayRevive();
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
