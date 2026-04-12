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
            UIManager.EnsureCreated();
            UIManager.Instance.ShowPanel<LoadingPanel>();
        }
    }
}
