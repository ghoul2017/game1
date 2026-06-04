using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shared.Common.Protos;

namespace Client.Logic
{
    public class RoleSelectController
    {
        public static RoleSelectController Instance { get; } = new();

        public List<RoleData> Roles { get; private set; } = new();
        public int SelectedRoleIndex { get; private set; } = -1;
        public string StatusText { get; private set; } = "";
        public bool HasError { get; private set; }

        public event Action OnDataChanged;
        public event Action OnEnterMapSuccess;

        public void LoadRoles()
        {
            Roles = new List<RoleData>(GameData.Roles);
            SelectedRoleIndex = -1;
            StatusText = Roles.Count > 0 ? "请选择一个角色进入游戏" : "没有角色，请创建新角色";
            HasError = false;
            OnDataChanged?.Invoke();
        }

        public void SelectRole(int index)
        {
            if (index < 0 || index >= Roles.Count) return;
            SelectedRoleIndex = index;
            StatusText = string.Format("已选择: {0} (Lv.{1} {2})", Roles[index].Name, Roles[index].Level, Roles[index].Job);
            HasError = false;
            OnDataChanged?.Invoke();
        }

        public async Task CreateRole(string name, int job)
        {
            if (string.IsNullOrEmpty(name))
            {
                StatusText = "请输入角色名称";
                HasError = true;
                OnDataChanged?.Invoke();
                return;
            }
            if (name.Length > 12)
            {
                StatusText = "角色名称最多12个字符";
                HasError = true;
                OnDataChanged?.Invoke();
                return;
            }
            StatusText = "正在创建角色...";
            HasError = false;
            OnDataChanged?.Invoke();

            try
            {
                CreateRoleRes res = await NetClient.Instance.CallAsync<CreateRoleRes>(
                    new CreateRoleReq { Name = name, Job = job });

                if (string.IsNullOrEmpty(res.ErrorMessage))
                {
                    var info = res.Role;
                    Roles.Add(new RoleData
                    {
                        RoleId = info.RoleId,
                        Name = info.Name,
                        Level = info.Level,
                        Job = GameData.GetJobName(info.Job)
                    });
                    SelectedRoleIndex = Roles.Count - 1;
                    StatusText = string.Format("已创建并选择: {0} ({1})", info.Name, GameData.GetJobName(info.Job));
                    HasError = false;
                }
                else
                {
                    StatusText = res.ErrorMessage;
                    HasError = true;
                }
            }
            catch (Exception ex)
            {
                StatusText = "创建角色失败: " + ex.Message;
                HasError = true;
            }
            OnDataChanged?.Invoke();
        }

        public async Task EnterGame()
        {
            if (SelectedRoleIndex < 0 || SelectedRoleIndex >= Roles.Count) return;
            var role = Roles[SelectedRoleIndex];
            StatusText = "正在进入游戏...";
            HasError = false;
            OnDataChanged?.Invoke();
            try
            {
                EnterGameRes res = await NetClient.Instance.CallAsync<EnterGameRes>(
                    new EnterGameReq { RoleId = role.RoleId });

                if (string.IsNullOrEmpty(res.ErrorMessage))
                {
                    GameData.Token = res.Token;
                    StatusText = "正在进入游戏...";
                    HasError = false;
                }
                else
                {
                    StatusText = res.ErrorMessage;
                    HasError = true;
                }
            }
            catch (Exception ex)
            {
                StatusText = "进入游戏失败: " + ex.Message;
                HasError = true;
            }
            OnDataChanged?.Invoke();
        }

        public void HandleEnterGameNtf(EnterGameNtf ntf)
        {
            GameData.CurrentRole = new RoleData
            {
                RoleId = ntf.RoleId,
                Name = ntf.Name,
                Level = ntf.Level,
                Job = GameData.GetJobName(ntf.Job)
            };
            StatusText = "正在进入地图...";
            HasError = false;
            OnDataChanged?.Invoke();
            _ = EnterInitialMapAsync();
        }

        private async Task EnterInitialMapAsync()
        {
            try
            {
                EnterMapAck ack = await NetClient.Instance.CallAsync<EnterMapAck>(
                    new EnterMapReq { MapCfgId = 1, Line = 1 });

                if (string.IsNullOrEmpty(ack.ErrorMessage))
                {
                    GameData.CurrentMap = new EnterMapData
                    {
                        MapCfgId = ack.MapCfgId,
                        Line = ack.Line,
                        SpawnX = ack.X,
                        SpawnY = ack.Y
                    };
                    OnEnterMapSuccess?.Invoke();
                }
                else
                {
                    StatusText = "进入地图失败: " + ack.ErrorMessage;
                    HasError = true;
                    OnDataChanged?.Invoke();
                }
            }
            catch (Exception ex)
            {
                StatusText = "进入地图失败: " + ex.Message;
                HasError = true;
                OnDataChanged?.Invoke();
            }
        }
    }
}
