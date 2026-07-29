using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WeChatWASM;

namespace P0PlatformSmoke.Editor
{
    public static class P0PlatformSmokeEditor
    {
        private const string ScenePath = "Assets/P0PlatformSmoke/P0PlatformSmoke.unity";
        private const string ConfigRelativePath = "P0PlatformSmoke/Resources/p0-smoke.csv";
        private const string TcpAddressEnvironment = "GAME1_P0_TCP_ADDRESS";
        private const string TcpPortEnvironment = "GAME1_P0_TCP_PORT";
        private const string SdkCommit = "9de9b8d7730f65a617d9992f07c3ecfae8b9858a";

        [MenuItem("Tools/P0 Platform Smoke/Create Scene")]
        public static void CreateScene()
        {
            Directory.CreateDirectory("Assets/P0PlatformSmoke");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject cameraObject = new GameObject("P0Camera", typeof(Camera));
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.047f, 0.075f, 1.0f);

            GameObject canvasObject = new GameObject(
                "P0Canvas",
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080.0f, 1920.0f);
            scaler.matchWidthOrHeight = 0.5f;

            GameObject textObject = new GameObject("P0Log", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(canvasObject.transform, false);
            RectTransform rectTransform = textObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = new Vector2(36.0f, 36.0f);
            rectTransform.offsetMax = new Vector2(-36.0f, -36.0f);

            Text logText = textObject.GetComponent<Text>();
            logText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            logText.fontSize = 28;
            logText.alignment = TextAnchor.UpperLeft;
            logText.color = Color.white;
            logText.horizontalOverflow = HorizontalWrapMode.Wrap;
            logText.verticalOverflow = VerticalWrapMode.Overflow;
            logText.text = "P0 platform smoke waiting...";

            GameObject runnerObject = new GameObject("P0PlatformSmokeRunner");
            P0PlatformSmokeRunner runner = runnerObject.AddComponent<P0PlatformSmokeRunner>();
            SerializedObject serializedRunner = new SerializedObject(runner);
            SerializedProperty logTextProperty = serializedRunner.FindProperty("_logText");
            logTextProperty.objectReferenceValue = logText;
            serializedRunner.ApplyModifiedPropertiesWithoutUndo();

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new InvalidOperationException($"无法保存 P0 场景: {ScenePath}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[P0PlatformSmoke] 已生成隔离场景: {ScenePath}");
        }

        [MenuItem("Tools/P0 Platform Smoke/Validate Payloads")]
        public static void ValidatePayloads()
        {
            byte[] packet = P0PlatformSmokePayload.CreateValidatedPacket();
            P0PlatformSmokePayload.ValidatePacket(packet);

            string configPath = Path.Combine(Application.dataPath, ConfigRelativePath);
            if (!File.Exists(configPath))
            {
                throw new FileNotFoundException("找不到 P0 配置样例", configPath);
            }

            string configText = File.ReadAllText(configPath, Encoding.UTF8);
            P0PlatformSmokePayload.ValidateConfig(configText);
            Debug.Log($"[P0PlatformSmoke] 静态载荷验证通过，packet_bytes={packet.Length}");
        }

        public static void BuildAndConvert()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string expectedBuildCopyRoot = RequireEnvironment("GAME1_P0_BUILD_COPY_ROOT");
            if (!PathsEqual(projectRoot, expectedBuildCopyRoot))
            {
                throw new InvalidOperationException(
                    $"P0 平台构建只能在任务副本执行: actual={projectRoot}, expected={expectedBuildCopyRoot}");
            }

            string outputRoot = Path.GetFullPath(RequireEnvironment("GAME1_P0_OUTPUT_ROOT"));
            if (IsInside(outputRoot, projectRoot))
            {
                throw new InvalidOperationException($"构建输出不能位于 Unity 项目内: {outputRoot}");
            }

            string webGlOutput = Path.Combine(outputRoot, "webgl");
            string miniGameOutput = Path.Combine(outputRoot, "minigame");
            if (Directory.Exists(webGlOutput) || Directory.Exists(miniGameOutput))
            {
                throw new InvalidOperationException($"构建输出已存在，拒绝覆盖: {outputRoot}");
            }

            Directory.CreateDirectory(outputRoot);
            ValidatePayloads();
            ConfigureSceneBuildInputs();
            ConfigureSdk(outputRoot);

            WXConvertCore.PreInit();
            PlayerSettings.runInBackground = true;
            AssetDatabase.SaveAssets();

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL &&
                !EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL))
            {
                throw new InvalidOperationException("无法切换到 WebGL 构建目标");
            }

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = webGlOutput,
                target = BuildTarget.WebGL,
                options = BuildOptions.Development,
            };

            Debug.Log(
                $"[P0PlatformSmoke] 开始 WebGL 构建，runInBackground={PlayerSettings.runInBackground}, " +
                $"output={webGlOutput}");
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"WebGL 构建失败: result={report.summary.result}, errors={report.summary.totalErrors}");
            }

            ValidateWebGlOutput(webGlOutput);

            WXConvertCore.WXExportError exportResult = WXConvertCore.DoExport(false);
            if (exportResult != WXConvertCore.WXExportError.SUCCEED)
            {
                throw new InvalidOperationException($"微信小游戏转换失败: {exportResult}");
            }

            ValidateConvertedOutput(miniGameOutput);
            WriteBuildRecord(outputRoot, report);

            PlayerSettings.runInBackground = true;
            AssetDatabase.SaveAssets();
            Debug.Log($"[P0PlatformSmoke] P0_BUILD_OK output={outputRoot}");
        }

        private static void ConfigureSdk(string outputRoot)
        {
            object config = WXConvertCore.config;
            string appId = Environment.GetEnvironmentVariable("GAME1_P0_APP_ID");
            if (string.IsNullOrWhiteSpace(appId))
            {
                appId = "touristappid";
            }

            SetNestedMember(config, "ProjectConf.relativeDST", outputRoot);
            SetNestedMember(config, "ProjectConf.DST", outputRoot);
            SetNestedMember(config, "ProjectConf.projectName", "game1-p0-platform-smoke");
            SetNestedMember(config, "ProjectConf.Appid", appId);
            SetNestedMember(config, "ProjectConf.assetLoadType", 1);
            SetNestedMember(config, "CompileOptions.DevelopBuild", true);
            SetNestedMember(config, "CompileOptions.Webgl2", true);
            SetNestedMember(config, "CompileOptions.AutoProfile", false);
            SetNestedMember(config, "CompileOptions.ScriptOnly", false);
            SetNestedMember(config, "CompileOptions.fbslim", false);
            SetNestedMember(config, "CompileOptions.DeleteStreamingAssets", false);
            SetNestedMember(config, "CompileOptions.ProfilingMemory", false);
            SetNestedMember(config, "CompileOptions.enablePerfAnalysis", false);

            UnityEngine.Object configAsset = config as UnityEngine.Object;
            if (configAsset != null)
            {
                EditorUtility.SetDirty(configAsset);
            }
        }

        private static void ConfigureSceneBuildInputs()
        {
            string tcpAddress = ValidateLanIpv4(RequireEnvironment(TcpAddressEnvironment));
            int tcpPort = ValidateTcpPort(RequireEnvironment(TcpPortEnvironment));
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            P0PlatformSmokeRunner runner = UnityEngine.Object.FindObjectOfType<P0PlatformSmokeRunner>();
            if (runner == null)
            {
                throw new InvalidOperationException($"P0 场景缺少 {nameof(P0PlatformSmokeRunner)}");
            }

            SerializedObject serializedRunner = new SerializedObject(runner);
            SerializedProperty tcpAddressProperty = serializedRunner.FindProperty("_tcpAddress");
            SerializedProperty tcpPortProperty = serializedRunner.FindProperty("_tcpPort");
            if (tcpAddressProperty == null || tcpPortProperty == null)
            {
                throw new MissingMemberException(nameof(P0PlatformSmokeRunner), "_tcpAddress/_tcpPort");
            }

            tcpAddressProperty.stringValue = tcpAddress;
            tcpPortProperty.intValue = tcpPort;
            serializedRunner.ApplyModifiedPropertiesWithoutUndo();
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new InvalidOperationException($"无法保存 P0 构建输入: {ScenePath}");
            }

            Debug.Log("[P0PlatformSmoke] 已向任务副本场景注入局域网 TCP 地址和端口");
        }

        private static void ValidateWebGlOutput(string webGlOutput)
        {
            string[] dataFiles = Directory.GetFiles(webGlOutput, "*.data*", SearchOption.AllDirectories);
            if (dataFiles.Length == 0)
            {
                throw new FileNotFoundException("WebGL 构建缺少包含随包 Resources 的 data 文件", webGlOutput);
            }
        }

        private static void ValidateConvertedOutput(string miniGameOutput)
        {
            string[] requiredPaths =
            {
                Path.Combine(miniGameOutput, "game.js"),
                Path.Combine(miniGameOutput, "game.json"),
                Path.Combine(miniGameOutput, "project.config.json"),
            };

            foreach (string requiredPath in requiredPaths)
            {
                if (!File.Exists(requiredPath))
                {
                    throw new FileNotFoundException("微信小游戏转换缺少必要文件", requiredPath);
                }
            }
        }

        private static void WriteBuildRecord(string outputRoot, BuildReport report)
        {
            string appId = Environment.GetEnvironmentVariable("GAME1_P0_APP_ID");
            BuildRecord record = new BuildRecord
            {
                unityVersion = Application.unityVersion,
                sdkCommit = SdkCommit,
                appIdType = string.IsNullOrWhiteSpace(appId) ? "测试号(touristappid)" : "开发号",
                configSource = "package-resources-async",
                transport = "wx.createTCPSocket",
                tcpAddressType = "private-lan-ipv4",
                tcpPort = ValidateTcpPort(RequireEnvironment(TcpPortEnvironment)),
                runInBackground = true,
                totalSizeBytes = report.summary.totalSize,
                buildSeconds = report.summary.totalTime.TotalSeconds,
            };
            string json = JsonUtility.ToJson(record, true);
            File.WriteAllText(Path.Combine(outputRoot, "p0-build-result.json"), json, new UTF8Encoding(false));
        }

        private static string ValidateLanIpv4(string value)
        {
            string trimmed = value.Trim();
            string[] parts = trimmed.Split('.');
            if (parts.Length != 4)
            {
                throw new InvalidOperationException($"{TcpAddressEnvironment} 必须是局域网 IPv4 地址");
            }

            int[] octets = new int[4];
            for (int index = 0; index < parts.Length; index++)
            {
                if (!int.TryParse(parts[index], out octets[index]) || octets[index] < 0 || octets[index] > 255)
                {
                    throw new InvalidOperationException($"{TcpAddressEnvironment} 必须是局域网 IPv4 地址");
                }
            }

            bool isPrivate = octets[0] == 10 ||
                (octets[0] == 172 && octets[1] >= 16 && octets[1] <= 31) ||
                (octets[0] == 192 && octets[1] == 168);
            if (!isPrivate)
            {
                throw new InvalidOperationException($"{TcpAddressEnvironment} 只允许 RFC1918 局域网 IPv4 地址");
            }

            return $"{octets[0]}.{octets[1]}.{octets[2]}.{octets[3]}";
        }

        private static int ValidateTcpPort(string value)
        {
            if (!int.TryParse(value.Trim(), out int port) || port < 1024 || port > 65535)
            {
                throw new InvalidOperationException($"{TcpPortEnvironment} 必须是 1024-65535 的端口");
            }

            if (IsWechatForbiddenTcpPort(port))
            {
                throw new InvalidOperationException($"{TcpPortEnvironment} 命中微信 TCPSocket 禁用端口: {port}");
            }

            return port;
        }

        private static bool IsWechatForbiddenTcpPort(int port)
        {
            if (port >= 8000 && port <= 8100)
            {
                return true;
            }

            int[] forbiddenPorts =
            {
                1099, 1433, 1521, 1719, 1720, 1723, 2049, 2375, 3128, 3306, 3389, 3659, 4045,
                5060, 5061, 5432, 5984, 6379, 6000, 6566, 7001, 7002, 8443, 8888, 9200, 9300,
                10051, 10080, 11211, 27017, 27018, 27019,
            };
            return Array.IndexOf(forbiddenPorts, port) >= 0;
        }

        private static void SetNestedMember(object root, string path, object value)
        {
            string[] parts = path.Split('.');
            object target = root;
            for (int index = 0; index < parts.Length - 1; index++)
            {
                target = GetMemberValue(target, parts[index]);
            }

            string memberName = parts[parts.Length - 1];
            Type targetType = target.GetType();
            FieldInfo field = targetType.GetField(memberName, BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(target, ConvertValue(value, field.FieldType));
                return;
            }

            PropertyInfo property = targetType.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic);
            if (property == null || !property.CanWrite)
            {
                throw new MissingMemberException(targetType.FullName, memberName);
            }

            property.SetValue(target, ConvertValue(value, property.PropertyType));
        }

        private static object GetMemberValue(object target, string memberName)
        {
            Type targetType = target.GetType();
            FieldInfo field = targetType.GetField(memberName, BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic);
            if (field != null)
            {
                return field.GetValue(target) ?? throw new InvalidOperationException($"成员为空: {memberName}");
            }

            PropertyInfo property = targetType.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic);
            if (property == null || !property.CanRead)
            {
                throw new MissingMemberException(targetType.FullName, memberName);
            }

            return property.GetValue(target) ?? throw new InvalidOperationException($"成员为空: {memberName}");
        }

        private static object ConvertValue(object value, Type targetType)
        {
            if (targetType.IsInstanceOfType(value))
            {
                return value;
            }

            if (targetType.IsEnum && value is string enumName)
            {
                return Enum.Parse(targetType, enumName, true);
            }

            return Convert.ChangeType(value, targetType);
        }

        private static string RequireEnvironment(string name)
        {
            string value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException($"缺少环境变量: {name}");
            }

            return value;
        }

        private static bool PathsEqual(string left, string right)
        {
            string normalizedLeft = Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar);
            string normalizedRight = Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar);
            return string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsInside(string path, string parent)
        {
            string normalizedPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            string normalizedParent = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            return normalizedPath.StartsWith(normalizedParent, StringComparison.OrdinalIgnoreCase);
        }

        [Serializable]
        private sealed class BuildRecord
        {
            public string unityVersion;
            public string sdkCommit;
            public string appIdType;
            public string configSource;
            public string transport;
            public string tcpAddressType;
            public int tcpPort;
            public bool runInBackground;
            public ulong totalSizeBytes;
            public double buildSeconds;
        }
    }
}
