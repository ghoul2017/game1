using System.Collections.Generic;
using GameplayCore;
using UnityEngine;

namespace Client.Expedition
{
    /// <summary>
    /// 表现队伍根和成员阵型，并根据核心生命状态更新成员颜色。
    /// </summary>
    public sealed class SquadView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private Transform[] _memberTransforms;
        private Renderer[] _renderers;
        private MaterialPropertyBlock _propertyBlock;

        public int MemberViewCount => _memberTransforms.Length;

        public Vector3 LeaderPosition => _memberTransforms[0].position;

        public void Initialize(IReadOnlyList<ExpeditionMember> members, float squadX, float squadY)
        {
            _memberTransforms = new Transform[members.Count];
            _renderers = new Renderer[members.Count];
            _propertyBlock = new MaterialPropertyBlock();
            Vector3 rootPosition = ToWorldPosition(squadX, squadY);

            for (int index = 0; index < members.Count; index++)
            {
                ExpeditionMember member = members[index];
                GameObject memberObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                memberObject.name = $"Member_{member.Id}";
                memberObject.transform.SetParent(transform, false);
                memberObject.transform.localScale = index == 0
                    ? new Vector3(0.9f, 1.0f, 0.9f)
                    : new Vector3(0.75f, 0.8f, 0.75f);
                memberObject.transform.position = rootPosition + GetFormationOffset(index);
                DisableCollider(memberObject);

                _memberTransforms[index] = memberObject.transform;
                _renderers[index] = memberObject.GetComponent<Renderer>();
            }

            Synchronize(members);
        }

        public void Synchronize(IReadOnlyList<ExpeditionMember> members)
        {
            for (int index = 0; index < members.Count; index++)
            {
                ExpeditionMember member = members[index];
                Color color = Color.gray;
                if (member.IsAlive)
                {
                    float lostHealth = 1.0f - (float)member.Health / member.MaxHealth;
                    color = Color.Lerp(GetMemberColor(index), new Color(0.65f, 0.15f, 0.15f), lostHealth * 0.6f);
                }

                SetColor(_renderers[index], color);
            }
        }

        public void Render(ExpeditionRun run, float deltaTime)
        {
            Vector3 rootPosition = ToWorldPosition(run.SquadX, run.SquadY);
            float leaderBlend = GetBlend(10.0f, deltaTime);
            _memberTransforms[0].position = Vector3.Lerp(
                _memberTransforms[0].position,
                rootPosition,
                leaderBlend);

            float partnerBlend = GetBlend(6.0f, deltaTime);
            for (int index = 1; index < _memberTransforms.Length; index++)
            {
                Vector3 target = _memberTransforms[0].position + GetFormationOffset(index);
                _memberTransforms[index].position = Vector3.Lerp(
                    _memberTransforms[index].position,
                    target,
                    partnerBlend);
            }
        }

        public Vector3 GetMemberPosition(int index)
        {
            return _memberTransforms[index].position;
        }

        public Vector3 GetFormationPosition(int index)
        {
            return _memberTransforms[0].position + GetFormationOffset(index);
        }

        private static Vector3 ToWorldPosition(float x, float y)
        {
            return new Vector3(x, 1.0f, y);
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

        private static float GetBlend(float speed, float deltaTime)
        {
            return deltaTime <= 0.0f ? 1.0f : 1.0f - Mathf.Exp(-speed * deltaTime);
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

        private static void DisableCollider(GameObject target)
        {
            Collider collider = target.GetComponent<Collider>();
            collider.enabled = false;
            Object.Destroy(collider);
        }

        private void SetColor(Renderer target, Color color)
        {
            _propertyBlock.Clear();
            _propertyBlock.SetColor(ColorId, color);
            _propertyBlock.SetColor(BaseColorId, color);
            target.SetPropertyBlock(_propertyBlock);
        }
    }
}
