using System;

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

    public static class ClientContext
    {
        public static IClientUI UI { get; set; }
    }
}
