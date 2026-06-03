using System.IO;
using UnityEngine;
using Client.Logic;
using Shared.Core.Config;

namespace Client.View
{
    public class GameEntry : MonoBehaviour
    {

        private void Start()
        {
            LogClientInitializer.Init();
            Debug.Log("[GameEntry] 游戏启动");

            var tablesDir = GetTablesDirectory();
            ConfigManager.Instance.LoadAll(tablesDir);

            Context.Start();
            UIManager.EnsureCreated();
            Context.UI = UIManager.Instance;
            NetworkStatusPanel.EnsureCreated();
            UIManager.Instance.ShowPanel<LoadingPanel>();
        }

        private void Update()
        {
            Context.Update(Time.deltaTime);
        }

        private void OnDestroy()
        {
            Context.OnDestroy();
        }

        private static string GetTablesDirectory()
        {
#if UNITY_EDITOR
            var devTablesDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "server", "Data", "Tables"));
            if (Directory.Exists(devTablesDir))
                return devTablesDir;
#endif
            return Path.Combine(Application.streamingAssetsPath, "Tables");
        }
    }
}
