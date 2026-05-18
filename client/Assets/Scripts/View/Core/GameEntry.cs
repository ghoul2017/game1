using System.IO;
using UnityEngine;
using Client.Logic;
using Client.View;
using Shared.Core.Config;

namespace Client.View
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
            ClientContext.UI = UIManager.Instance;
            NetworkStatusPanel.EnsureCreated();
            UIManager.Instance.ShowPanel<LoadingPanel>();
        }
    }
}
