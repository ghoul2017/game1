using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Client.Logic;
using Shared.Core.Config;

namespace Client.View
{
    public class GameEntry : MonoBehaviour
    {

        private IEnumerator Start()
        {
            LogClientInitializer.Init();
            Debug.Log("[GameEntry] 游戏启动");

#if UNITY_WEBGL && !UNITY_EDITOR
            Task platformInitialization = WeChatClientTransport.InitializeSdkAsync();
            while (!platformInitialization.IsCompleted)
            {
                yield return null;
            }

            if (platformInitialization.IsFaulted)
            {
                Exception exception = platformInitialization.Exception?.GetBaseException() ??
                    new InvalidOperationException("微信平台初始化失败");
                Debug.LogError($"[GameEntry] 平台初始化失败：{exception.Message}");
                yield break;
            }
#endif

            IReadOnlyList<string> requiredFiles = ConfigManager.Instance.GetRequiredFileNames();
            Dictionary<string, string> tableTexts = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int index = 0; index < requiredFiles.Count; index++)
            {
                string fileName = requiredFiles[index];
                string tableUri = GetTableUri(fileName);
                using (UnityWebRequest request = UnityWebRequest.Get(tableUri))
                {
                    yield return request.SendWebRequest();
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogError($"[GameEntry] 配置加载失败：{fileName}，{request.error}");
                        yield break;
                    }

                    string text = request.downloadHandler.text;
                    if (string.IsNullOrEmpty(text))
                    {
                        Debug.LogError($"[GameEntry] 配置加载失败：{fileName} 内容为空");
                        yield break;
                    }

                    tableTexts.Add(fileName, text);
                }
            }

            try
            {
                ConfigManager.Instance.LoadAll(tableTexts);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[GameEntry] 配置解析失败：{exception.Message}");
                yield break;
            }

            Context.Start();
#if UNITY_WEBGL && !UNITY_EDITOR
            Context.NetClient.SessionFactory = WeChatClientTransport.ConnectSessionAsync;
#endif
            UIManager.EnsureCreated();
            NetworkStatusPanel.EnsureCreated();
            UIManager.Instance.ShowPanel<LoadingPanel>();
        }

        private static string GetTableUri(string fileName)
        {
            string tablesRoot = Application.streamingAssetsPath.TrimEnd('/', '\\') + "/Tables";
            if (tablesRoot.Contains("://") || tablesRoot.StartsWith("jar:", StringComparison.OrdinalIgnoreCase))
            {
                return $"{tablesRoot}/{Uri.EscapeDataString(fileName)}";
            }

            string filePath = Path.Combine(tablesRoot, fileName);
            return new Uri(filePath).AbsoluteUri;
        }
    }
}
