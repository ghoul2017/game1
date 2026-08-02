using System;
using System.Collections.Generic;
using System.Linq;
using Client.View.OnlineTechnical;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Client.View.Editor
{
    public static class OnlineTechnicalSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/OnlineTechnicalScene.unity";

        [MenuItem("Tools/Online Technical/Create Online Technical Scene")]
        public static void CreateScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject gameEntryObject = new GameObject("GameEntry");
            gameEntryObject.AddComponent<GameEntry>();

            GameObject technicalEntryObject = new GameObject("OnlineTechnicalEntry");
            technicalEntryObject.AddComponent<OnlineTechnicalEntry>();

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new InvalidOperationException($"无法保存在线技术切片场景：{ScenePath}");
            }

            AddToBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[OnlineTechnical] 场景已生成并加入 Build Settings：{ScenePath}");
        }

        public static void CreateSceneFromCommandLine()
        {
            CreateScene();
        }

        private static void AddToBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes
                .Where(item => !string.Equals(item.path, ScenePath, StringComparison.Ordinal))
                .ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
