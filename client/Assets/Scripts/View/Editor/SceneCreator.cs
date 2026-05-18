using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering.Universal;

namespace Client.View.Editor
{
    public static class SceneCreator
    {
        [MenuItem("Tools/Setup Build Scenes")]
        public static void SetupBuildScenes()
        {
            // 确保 Scenes 目录下所有 .unity 文件都加入 Build Settings
            var guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" });
            var paths = new System.Collections.Generic.List<string>();
            foreach (var g in guids)
                paths.Add(AssetDatabase.GUIDToAssetPath(g));

            Debug.Log($"[SceneCreator] 发现场景: {string.Join(", ", paths)}");

            var scenes = new EditorBuildSettingsScene[paths.Count];
            for (int i = 0; i < paths.Count; i++)
                scenes[i] = new EditorBuildSettingsScene(paths[i], true);

            EditorBuildSettings.scenes = scenes;
            Debug.Log($"[SceneCreator] Build Settings 已更新，共 {scenes.Length} 个场景");
        }

        [MenuItem("Tools/Create GameScene")]
        public static void CreateGameScene()
        {
            string scenePath = "Assets/Scenes/GameScene.unity";

            // 如果已存在，先删除
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) != null)
            {
                AssetDatabase.DeleteAsset(scenePath);
            }

            // 创建新场景
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Main Camera
            var camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            var cam = camObj.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 8f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 200f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.15f, 0.22f);
            cam.transform.position = new Vector3(100f, 15f, 85f);
            cam.transform.rotation = Quaternion.Euler(45f, 0f, 0f);

            // 添加 UniversalAdditionalCameraData（URP 需要）
            var camData = camObj.AddComponent<UniversalAdditionalCameraData>();
            camData.renderShadows = true;
            camData.renderPostProcessing = false;

            // Directional Light
            var lightObj = new GameObject("Directional Light");
            var light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.95f, 0.85f);
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // Global Volume
            var volumeObj = new GameObject("Global Volume");
            volumeObj.AddComponent<UnityEngine.Rendering.Volume>();

            // GameSceneEntry
            var entryObj = new GameObject("GameSceneEntry");
            entryObj.AddComponent<Client.Game.GameSceneEntry>();

            // 保存场景
            EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log($"[SceneCreator] GameScene 已创建: {scenePath}");

            AssetDatabase.Refresh();

            // 添加到 Build Settings
            AddSceneToBuildSettings(scenePath);
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            var original = EditorBuildSettings.scenes;
            foreach (var s in original)
            {
                if (s.path == scenePath)
                {
                    Debug.Log($"[SceneCreator] {scenePath} 已在 Build Settings 中");
                    return;
                }
            }

            var newScenes = new EditorBuildSettingsScene[original.Length + 1];
            for (int i = 0; i < original.Length; i++)
                newScenes[i] = original[i];
            newScenes[original.Length] = new EditorBuildSettingsScene(scenePath, true);
            EditorBuildSettings.scenes = newScenes;
            Debug.Log($"[SceneCreator] 已将 {scenePath} 添加到 Build Settings");
        }
    }
}
