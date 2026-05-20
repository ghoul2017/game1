using System.IO;
using UnityEngine;
using Client.Logic;
using Shared.Core.Config;

namespace Client.View
{
    public class GameEntry : MonoBehaviour
    {
        private NetClient _netClient;

        private void Start()
        {
            LogClientInitializer.Init();
            Debug.Log("[GameEntry] 游戏启动");

            var tablesDir = Path.Combine(Application.dataPath, "..", "..", "server", "Data", "Tables");
            ConfigManager.Instance.LoadAll(tablesDir);

            // 初始化 NetClient（已从 View 迁移到 Logic，不再依赖 MonoBehaviour）
            _netClient = new NetClient();
            _netClient.Initialize();
            ClientContext.Network = _netClient;

            UIManager.EnsureCreated();
            ClientContext.UI = UIManager.Instance;
            NetworkStatusPanel.EnsureCreated();
            UIManager.Instance.ShowPanel<LoadingPanel>();
        }

        private void Update()
        {
            _netClient?.Update(Time.deltaTime);
        }

        private void OnDestroy()
        {
            _netClient?.Dispose();
        }
    }
}
