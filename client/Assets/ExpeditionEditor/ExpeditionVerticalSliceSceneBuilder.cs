using System;
using System.Collections.Generic;
using System.Linq;
using Client.Expedition;
using Client.View.Product;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Client.Expedition.Editor
{
    public static class ExpeditionVerticalSliceSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/ExpeditionVerticalSliceScene.unity";
        public const string TimedProductScenePath = "Assets/Scenes/CosmicLegionPlaceholderScene.unity";

        private const string OnlineTechnicalScenePath = "Assets/Scenes/OnlineTechnicalScene.unity";

        [MenuItem("Tools/Expedition/Create T2 Vertical Slice Scene")]
        public static void CreateScene()
        {
            CreateSceneAsset(
                ScenePath,
                "ExpeditionVerticalSliceEntry",
                ProductExpedition.ProductExpeditionRun.DefaultProductId,
                T1ExpeditionSetupFactory.DefaultTicksPerSecond);
            UpdateBuildSettings();
            Debug.Log($"[Expedition] T2 场景已生成并置于在线入口之后: {ScenePath}");
        }

        [MenuItem("Tools/Expedition/Create T6 Timed Product Scene")]
        public static void CreateTimedProductScene()
        {
            CreateSceneAsset(
                TimedProductScenePath,
                "CosmicLegionPlaceholderEntry",
                ProductExpedition.ProductExpeditionRun.TimedPlaceholderProductId,
                10);
            UpdateBuildSettings();
            Debug.Log($"[Expedition] T6 限时产品场景已生成: {TimedProductScenePath}");
        }

        public static void CreateSceneFromCommandLine()
        {
            CreateSceneAsset(
                ScenePath,
                "ExpeditionVerticalSliceEntry",
                ProductExpedition.ProductExpeditionRun.DefaultProductId,
                T1ExpeditionSetupFactory.DefaultTicksPerSecond);
            CreateSceneAsset(
                TimedProductScenePath,
                "CosmicLegionPlaceholderEntry",
                ProductExpedition.ProductExpeditionRun.TimedPlaceholderProductId,
                10);
            UpdateBuildSettings();
            Debug.Log("[Expedition] T2 与 T6 场景均已生成，在线/T2/T6 的固定顺序保持不变");
        }

        private static void CreateSceneAsset(
            string path,
            string entryName,
            string productId,
            int ticksPerSecond)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject entryObject = new GameObject(entryName);
            ExpeditionVerticalSliceEntry entry = entryObject.AddComponent<ExpeditionVerticalSliceEntry>();
            entry.ConfigureProduct(productId);
            entry.ConfigureTicksPerSecond(ticksPerSecond);
            ProductProfileRuntimeEntry productProfileEntry = entryObject.AddComponent<ProductProfileRuntimeEntry>();
            productProfileEntry.ApplyProduct(productId);

            if (!EditorSceneManager.SaveScene(scene, path))
            {
                throw new InvalidOperationException($"无法保存远征场景: {path}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void UpdateBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes
                .Where(item => !string.Equals(item.path, OnlineTechnicalScenePath, StringComparison.Ordinal))
                .Where(item => !string.Equals(item.path, ScenePath, StringComparison.Ordinal))
                .Where(item => !string.Equals(item.path, TimedProductScenePath, StringComparison.Ordinal))
                .ToList();
            List<EditorBuildSettingsScene> orderedScenes = new List<EditorBuildSettingsScene>();
            AddSceneIfPresent(orderedScenes, OnlineTechnicalScenePath);
            AddSceneIfPresent(orderedScenes, ScenePath);
            AddSceneIfPresent(orderedScenes, TimedProductScenePath);
            orderedScenes.AddRange(scenes);
            EditorBuildSettings.scenes = orderedScenes.ToArray();
        }

        private static void AddSceneIfPresent(List<EditorBuildSettingsScene> scenes, string path)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
            {
                scenes.Add(new EditorBuildSettingsScene(path, true));
            }
        }
    }
}
