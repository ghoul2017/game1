using System;
using Shared.Core.Network;

namespace Client.Logic
{
    public interface IClientUI
    {
        T GetPanel<T>() where T : class;
        void ShowPanel<T>() where T : class;
        void HidePanel<T>() where T : class;
        void ReplacePanel<T>() where T : class;
        void ClearAll();
        void Back();
        void ShowToast(string msg);
        void ShowMessageBox(string title, string content, Action onOk, Action onCancel);
    }

    public interface INetworkSender
    {
        void Send(IMessage msg);
        bool IsConnected { get; }
    }

    public static class ClientContext
    {
        public static IClientUI UI { get; set; }
        public static INetworkSender Network { get; set; }
    }
}
