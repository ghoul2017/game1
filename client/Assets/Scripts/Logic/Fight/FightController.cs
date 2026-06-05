using System;
using System.Threading.Tasks;
using Shared.Common.Protos;
using Shared.Core.Event;

namespace Client.Logic
{
    public class FightController
    {
        public static FightController Instance { get; } = new();

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
                    ClientUiEventPublisher.ShowToast(ack.ErrorMessage);
            }
            catch (Exception ex)
            {
                ClientUiEventPublisher.ShowToast("使用技能失败: " + ex.Message);
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
                EventBus.Instance.Publish(new EventType.FightPkModeChangedEvent());
            }
            catch (Exception ex)
            {
                ClientUiEventPublisher.ShowToast("切换PK模式失败: " + ex.Message);
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

    }

}
