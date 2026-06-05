using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shared.Common.Protos;
using Shared.Core.Event;

namespace Client.Logic
{
    public class RoleSelectController
    {
        public static RoleSelectController Instance { get; } = new();

        public List<RoleData> Roles { get; private set; } = new();
        public int SelectedRoleIndex { get; private set; } = -1;
        public string StatusText { get; internal set; } = "";
        public bool HasError { get; internal set; }

        public void LoadRoles()
        {
            Roles = new List<RoleData>(GameData.Roles);
            SelectedRoleIndex = -1;
            StatusText = Roles.Count > 0 ? "请选择一个角色进入游戏" : "没有角色，请创建新角色";
            HasError = false;
            PublishDataChanged();
        }

        public void SelectRole(int index)
        {
            if (index < 0 || index >= Roles.Count) return;
            SelectedRoleIndex = index;
            StatusText = string.Format("已选择: {0} (Lv.{1} {2})", Roles[index].Name, Roles[index].Level, Roles[index].Job);
            HasError = false;
            PublishDataChanged();
        }

        public async Task CreateRole(string name, int job)
        {
            if (string.IsNullOrEmpty(name))
            {
                StatusText = "请输入角色名称";
                HasError = true;
                PublishDataChanged();
                return;
            }
            if (name.Length > 12)
            {
                StatusText = "角色名称最多12个字符";
                HasError = true;
                PublishDataChanged();
                return;
            }
            StatusText = "正在创建角色...";
            HasError = false;
            PublishDataChanged();

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
            PublishDataChanged();
        }

        public async Task EnterGame()
        {
            if (SelectedRoleIndex < 0 || SelectedRoleIndex >= Roles.Count) return;
            var role = Roles[SelectedRoleIndex];
            StatusText = "正在进入游戏...";
            HasError = false;
            PublishDataChanged();
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
            PublishDataChanged();
        }

        private static void PublishDataChanged()
        {
            EventBus.Instance.Publish(new EventType.RoleSelectDataChangedEvent());
        }
    }

}
