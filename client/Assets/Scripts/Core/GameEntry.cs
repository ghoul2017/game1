using UnityEngine;
using Client.Loading;

namespace Client.Core
{
    public class GameEntry : MonoBehaviour
    {
        private void Start()
        {
            Debug.Log("[GameEntry] 游戏启动");
            UIManager.EnsureCreated();
            UIManager.Instance.ShowPanel<LoadingPanel>();
        }
    }
}
