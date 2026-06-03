using System;
using System.Threading.Tasks;
using Shared.Common.Protos;

namespace Client.Logic
{
    public class FightController
    {
        public static FightController Instance { get; } = new();

        public event Action<UseSkillAck> OnSkillCasted;
        public event Action<HpChangedNtf> OnHpChanged;
        public event Action OnPkModeChanged;
        public event Action<KilledByNtf> OnKilledBy;

        public async Task UseSkill(int skillId, int x, int y, long targetId)
        {
            var req = new UseSkillReq();
            req.SkillId = skillId;
            req.X = x;
            req.Y = y;
            req.TargetId = targetId;
            try
            {
                UseSkillAck ack = await NetClient.Instance.CallAsync<UseSkillAck>(req);
                if (!string.IsNullOrEmpty(ack.ErrorMessage))
                    Context.UI?.ShowToast(ack.ErrorMessage);
            }
            catch (Exception ex)
            {
                Context.UI?.ShowToast("使用技能失败: " + ex.Message);
            }
        }

        public async Task TogglePkMode()
        {
            int nextMode = (GameData.CurrentPkMode + 1) % 4;
            var req = new SetPkModeReq();
            req.PkMode = nextMode;
            try
            {
                PkModeChangedNtf ntf = await NetClient.Instance.CallAsync<PkModeChangedNtf>(req);
                GameData.CurrentPkMode = ntf.PkMode;
                OnPkModeChanged?.Invoke();
            }
            catch (Exception ex)
            {
                Context.UI?.ShowToast("切换PK模式失败: " + ex.Message);
            }
        }

        public async Task RespawnAtCurrentMap()
        {
            var mapData = GameData.CurrentMap;
            if (mapData != null)
            {
                await MapController.Instance.EnterMap(mapData.MapCfgId, mapData.Line);
            }
        }

        public void ApplySkillEffect(UseSkillAck ack)
        {
            if (!string.IsNullOrEmpty(ack.ErrorMessage))
            {
                Context.UI?.ShowToast(ack.ErrorMessage);
                return;
            }
            OnSkillCasted?.Invoke(ack);
        }

        public void HandleHpChangedNtf(HpChangedNtf ntf)
        {
            long selfId = GameData.CurrentRole != null ? GameData.CurrentRole.RoleId : 0;
            if (ntf.ObjectId == selfId)
            {
                GameData.CurrentHp = ntf.Hp;
            }
            OnHpChanged?.Invoke(ntf);
        }

        public void HandleKilledByNtf(KilledByNtf ntf)
        {
            OnKilledBy?.Invoke(ntf);
        }

    }
}
