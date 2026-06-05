using System;
using System.Linq;
using System.Threading.Tasks;
using Shared.Common.Protos;
using Shared.Core.Event;

namespace Client.Logic
{
    public class LoginController
    {
        public static LoginController Instance { get; } = new();

        public bool IsLoggingIn { get; private set; }
        public string ErrorMessage { get; private set; }

        private string _pendingUsername;
        private string _pendingPassword;

        /// <summary>
        /// 返回 null 表示验证通过，非 null 是错误提示
        /// </summary>
        public string ValidateInput(string username, string password)
        {
            if (string.IsNullOrEmpty(username)) return "请输入账号";
            if (string.IsNullOrEmpty(password)) return "请输入密码";
            if (IsLoggingIn) return null;
            return null;
        }

        /// <summary>
        /// 开始登录流程。返回 true 表示需要先连接服务器，false 表示已连接直接发送。
        /// </summary>
        public bool BeginLogin(string username, string password)
        {
            IsLoggingIn = true;
            ErrorMessage = "";
            _pendingUsername = username;
            _pendingPassword = password;

            if (NetClient.Instance.IsConnected)
            {
                DoSendLogin(username, password);
                return false;
            }
            else
            {
                ErrorMessage = "正在连接服务器...";
                return true;
            }
        }

        /// <summary>
        /// 连接成功后继续登录
        /// </summary>
        public void ContinuePendingLoginAfterConnection()
        {
            if (IsLoggingIn && !string.IsNullOrEmpty(_pendingUsername))
            {
                DoSendLogin(_pendingUsername, _pendingPassword);
            }
        }

        public void FailPendingLoginConnection(string reason)
        {
            IsLoggingIn = false;
            ErrorMessage = GetConnectionErrorText(reason);
        }

        public static string GetConnectionErrorText(string reason)
        {
            return string.IsNullOrEmpty(reason) ? "连接服务器失败" : "连接服务器失败: " + reason;
        }

        private void DoSendLogin(string username, string password)
        {
            ErrorMessage = "正在登录...";
            _ = DoLoginAsync(username, password);
        }

        private async Task DoLoginAsync(string username, string password)
        {
            try
            {
                LoginRes res = await NetClient.Instance.CallAsync<LoginRes>(
                    new LoginReq { Username = username, Password = password });

                if (string.IsNullOrEmpty(res.ErrorMessage))
                {
                    GameData.PlayerId = res.PlayerId;
                    GameData.Token = res.Token;
                    GameData.Roles = res.Roles.Select(r => new RoleData
                    {
                        RoleId = r.RoleId,
                        Name = r.Name,
                        Level = r.Level,
                        Job = GameData.GetJobName(r.Job)
                    }).ToList();
                    IsLoggingIn = false;
                    PublishLoginCompleted(true, "");
                }
                else
                {
                    IsLoggingIn = false;
                    ErrorMessage = res.ErrorMessage;
                    PublishLoginCompleted(false, ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                IsLoggingIn = false;
                ErrorMessage = "登录失败: " + ex.Message;
                PublishLoginCompleted(false, ErrorMessage);
            }
        }

        private static void PublishLoginCompleted(bool success, string errorMessage)
        {
            EventBus.Instance.Publish(new EventType.LoginCompletedEvent
            {
                Success = success,
                ErrorMessage = errorMessage
            });
        }
    }

}
