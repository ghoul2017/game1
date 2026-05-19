using System;
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

        public void UseSkill(int skillId, int x, int y, long targetId)
        {
            var req = new UseSkillReq();
            req.SkillId = skillId;
            req.X = x;
            req.Y = y;
            req.TargetId = targetId;
            ClientContext.Network?.Send(req);
        }

        public void TogglePkMode()
        {
            int nextMode = (GameData.CurrentPkMode + 1) % 4;
            var req = new SetPkModeReq();
            req.PkMode = nextMode;
            ClientContext.Network?.Send(req);
        }

        public void RespawnAtCurrentMap()
        {
            var mapData = GameData.CurrentMap;
            if (mapData != null)
            {
                var req = new EnterMapReq();
                req.MapCfgId = mapData.MapCfgId;
                req.Line = mapData.Line;
                ClientContext.Network?.Send(req);
            }
        }

        public void HandleUseSkillAck(UseSkillAck ack)
        {
            if (!string.IsNullOrEmpty(ack.ErrorMessage))
            {
                ClientContext.UI?.ShowToast(ack.ErrorMessage);
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

        public void HandlePkModeChangedNtf(PkModeChangedNtf ntf)
        {
            GameData.CurrentPkMode = ntf.PkMode;
            OnPkModeChanged?.Invoke();
        }
    }
}
