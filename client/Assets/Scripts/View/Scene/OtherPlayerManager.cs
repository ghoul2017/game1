using System.Collections.Generic;
using Shared.Common.Protos;
using UnityEngine;

namespace Client.View
{
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
            if (objects == null)
                return;
            foreach (var obj in objects)
            {
                SpawnPlayer(obj);
            }
        }

        public void SpawnPlayer(MapObjectInfo info)
        {
            if (_players.ContainsKey(info.ObjectId))
                return;

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
            if (!_players.TryGetValue(roleId, out var view))
                return;

            _players.Remove(roleId);
            if (view != null)
                Object.Destroy(view.gameObject);

            Debug.Log(string.Format("[OtherPlayerManager] 玩家离开视野 RoleId:{0}", roleId));
        }

        public void MovePlayer(long roleId, int x, int y)
        {
            if (!_players.TryGetValue(roleId, out var view))
                return;
            view.MoveTo(x, y);
        }

        public void UpdatePlayerHp(long roleId, int hp, int maxHp)
        {
            if (!_players.TryGetValue(roleId, out var view))
                return;
            view.UpdateHp(hp, maxHp);
        }

        public void PlayAttack(long roleId, int skillId)
        {
            if (!_players.TryGetValue(roleId, out var view))
                return;
            view.PlayAttack(skillId);
        }

        public void ShowDamage(long roleId, int damage, bool isCritical)
        {
            if (!_players.TryGetValue(roleId, out var view))
                return;
            view.ShowDamageNumber(damage, isCritical);
            view.PlayHitFlash();
        }

        public void PlayDeath(long roleId)
        {
            if (!_players.TryGetValue(roleId, out var view))
                return;
            view.PlayDeath();
        }

        public void PlayRevive(long roleId)
        {
            if (!_players.TryGetValue(roleId, out var view))
                return;
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
