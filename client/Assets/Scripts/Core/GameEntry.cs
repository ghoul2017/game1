using System.IO;
using UnityEngine;
using Client.Loading;
using Client.Network;
using Shared.Core.Config;

namespace Client.Core
{
    public class GameEntry : MonoBehaviour
    {
        private void Start()
        {
            LogClientInitializer.Init();
            Debug.Log("[GameEntry] 游戏启动");

            // 开发环境直接引用服务端表目录
            var tablesDir = Path.Combine(Application.dataPath, "..", "..", "server", "Game", "Data", "Tables");
            ConfigManager.Instance.LoadAll(tablesDir);

            NetClient.EnsureCreated();

            UIManager.EnsureCreated();
            NetworkStatusPanel.EnsureCreated();
            UIManager.Instance.ShowPanel<LoadingPanel>();
        }
    }
}
