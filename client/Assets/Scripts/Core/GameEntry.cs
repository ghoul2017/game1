using UnityEngine;
using Client.Loading;
using Client.Network;

namespace Client.Core
{
    public class GameEntry : MonoBehaviour
    {
        private void Start()
        {
            Debug.Log("[GameEntry] 游戏启动");

            NetClient.EnsureCreated();
            NetClient.Instance.SetDispatcher(new MessageDispatcher());
            MsgTypeRegistry.Init();

            UIManager.EnsureCreated();
            NetworkStatusPanel.EnsureCreated();
            UIManager.Instance.ShowPanel<LoadingPanel>();
        }
    }
}
