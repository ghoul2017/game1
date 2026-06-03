using System;
using Shared.Core.Entities;
using Shared.Core.Network;

namespace Client.Logic
{
    public static class Context
    {
        public static IClientUI UI { get; set; }
        public static Module Client = new();
        public static NetClient NetClient = new();
        public static NetworkComponent Network => Client.GetComponent<NetworkComponent>();

        public static void Start()
        {
            NetClient.Initialize();
        }

        public static void Update(float deltaTime)
        {
            NetClient.Update(deltaTime);
        }

        public static void OnDestroy()
        {
            NetClient.Dispose();
        }
    }

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

}
