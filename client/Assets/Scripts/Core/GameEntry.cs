using UnityEngine;
using Client.Loading;
using Client.Network;

namespace Client.Core
{
    public class GameEntry : MonoBehaviour
    {
        private void Start()
        {
            LogClientInitializer.Init();
            Debug.Log("[GameEntry] 游戏启动");

            NetClient.EnsureCreated();
            NetClient.Instance.SetDispatcher(new MessageDispatcher());

            UIManager.EnsureCreated();
            NetworkStatusPanel.EnsureCreated();
            UIManager.Instance.ShowPanel<LoadingPanel>();
        }
    }
}
