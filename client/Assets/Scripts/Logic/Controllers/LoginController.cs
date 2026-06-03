using System;
using System.Linq;
using Shared.Common.Protos;

namespace Client.Logic
{
    public class LoginController
    {
        public static LoginController Instance { get; } = new();

        public bool IsLoggingIn { get; private set; }
        public string ErrorMessage { get; private set; }

        /// <summary>
        /// 登录完成事件。参数: (是否成功, 错误消息)
        /// </summary>
        public event Action<bool, string> OnLoginCompleted;

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
        public void OnConnectionSuccess()
        {
            if (IsLoggingIn && !string.IsNullOrEmpty(_pendingUsername))
            {
                DoSendLogin(_pendingUsername, _pendingPassword);
            }
        }

        public void OnConnectionError(string reason)
        {
            IsLoggingIn = false;
            ErrorMessage = "连接服务器失败";
        }

        private void DoSendLogin(string username, string password)
        {
            ErrorMessage = "正在登录...";
            NetClient.Instance.Send(new LoginReq { Username = username, Password = password });
        }

        public void HandleLoginRes(LoginRes res)
        {
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
                OnLoginCompleted?.Invoke(true, "");
            }
            else
            {
                IsLoggingIn = false;
                ErrorMessage = res.ErrorMessage;
                OnLoginCompleted?.Invoke(false, ErrorMessage);
            }
        }
    }
}
