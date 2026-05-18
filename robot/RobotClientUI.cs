using System;

namespace Robot
{
    /// <summary>
    /// Robot 的 IClientUI 实现。所有 UI 操作转为控制台输出，GetPanel 返回 null。
    /// </summary>
    public class RobotClientUI : Client.Logic.IClientUI
    {
        public T GetPanel<T>() where T : class
        {
            return null!;
        }

        public void ShowPanel<T>() where T : class
        {
            Console.WriteLine($"[UI] ShowPanel<{typeof(T).Name}>");
        }

        public void HidePanel<T>() where T : class
        {
            Console.WriteLine($"[UI] HidePanel<{typeof(T).Name}>");
        }

        public void ReplacePanel<T>() where T : class
        {
            Console.WriteLine($"[UI] ReplacePanel<{typeof(T).Name}>");
        }

        public void ClearAll()
        {
            Console.WriteLine("[UI] ClearAll");
        }

        public void Back()
        {
            Console.WriteLine("[UI] Back");
        }

        public void ShowToast(string msg)
        {
            Console.WriteLine($"[UI] Toast: {msg}");
        }

        public void ShowMessageBox(string title, string content, Action onOk, Action onCancel)
        {
            Console.WriteLine($"[UI] MessageBox: [{title}] {content}");
            onOk?.Invoke();
        }
    }
}
