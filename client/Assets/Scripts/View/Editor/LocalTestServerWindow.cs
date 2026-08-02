using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;
using Process = System.Diagnostics.Process;

namespace Client.View.Editor
{
    public sealed class LocalTestServerWindow : EditorWindow
    {
        private Vector2 _scrollPosition;
        private string _dotnetOverride = string.Empty;

        [MenuItem("Tools/Online Technical/Local Server Window")]
        public static void OpenWindow()
        {
            LocalTestServerWindow window = GetWindow<LocalTestServerWindow>("game1 Local Server");
            window.minSize = new Vector2(620f, 430f);
            window.Show();
        }

        [MenuItem("Tools/Online Technical/Build, Start Server and Play")]
        public static void BuildStartAndPlayFromMenu()
        {
            OpenWindow();
            LocalTestServerController.BuildStartAndPlay();
        }

        [MenuItem("Tools/Online Technical/Stop Local Server")]
        public static void StopFromMenu()
        {
            LocalTestServerController.Stop();
        }

        private void OnEnable()
        {
            _dotnetOverride = LocalTestServerController.DotnetOverride;
            LocalTestServerController.Changed += Repaint;
            LocalTestServerController.RefreshStatus();
        }

        private void OnDisable()
        {
            LocalTestServerController.Changed -= Repaint;
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("本地联机测试", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "此工具只在 Unity Editor 中构建并启动仓库 bin/Server.dll。测试 Player 不会携带或自动启动服务端。",
                MessageType.Info);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("状态", LocalTestServerController.StatusText);
            EditorGUILayout.LabelField("仓库", LocalTestServerController.RepositoryRoot);
            EditorGUILayout.LabelField("要求 SDK", LocalTestServerController.RequiredSdkVersion);
            EditorGUILayout.LabelField("dotnet", LocalTestServerController.ResolvedDotnetPath);
            EditorGUILayout.LabelField("MongoDB 127.0.0.1:27017", LocalTestServerController.MongoStatus);
            EditorGUILayout.LabelField("服务端 127.0.0.1:9000", LocalTestServerController.ServerPortStatus);

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("dotnet 路径覆盖（可留空）", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            string changedOverride = EditorGUILayout.TextField(_dotnetOverride);
            if (!string.Equals(changedOverride, _dotnetOverride, StringComparison.Ordinal))
            {
                _dotnetOverride = changedOverride;
            }

            if (GUILayout.Button("选择 dotnet.exe", GUILayout.Width(130f)))
            {
                string selectedPath = EditorUtility.OpenFilePanel("选择 dotnet", string.Empty, "exe");
                if (!string.IsNullOrEmpty(selectedPath))
                {
                    _dotnetOverride = selectedPath;
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("保存并检测"))
            {
                LocalTestServerController.SetDotnetOverride(_dotnetOverride);
            }

            if (GUILayout.Button("清除覆盖"))
            {
                _dotnetOverride = string.Empty;
                LocalTestServerController.SetDotnetOverride(string.Empty);
            }

            if (GUILayout.Button("刷新状态"))
            {
                LocalTestServerController.RefreshStatus();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(12f);
            GUI.enabled = !LocalTestServerController.IsBusy;
            if (GUILayout.Button("一键：构建服务端、启动、打开 OnlineTechnicalScene 并播放", GUILayout.Height(38f)))
            {
                LocalTestServerController.BuildStartAndPlay();
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("仅构建并启动"))
            {
                LocalTestServerController.BuildAndStart(false);
            }

            if (GUILayout.Button("启动已有 bin/Server.dll"))
            {
                LocalTestServerController.StartBuiltServer(false);
            }

            if (GUILayout.Button("打开场景并播放"))
            {
                LocalTestServerController.OpenSceneAndPlay();
            }
            EditorGUILayout.EndHorizontal();
            GUI.enabled = true;

            EditorGUILayout.BeginHorizontal();
            GUI.enabled = LocalTestServerController.CanStop;
            if (GUILayout.Button("停止本工具拥有的服务端"))
            {
                LocalTestServerController.Stop();
            }
            GUI.enabled = true;

            if (GUILayout.Button("打开服务端日志"))
            {
                LocalTestServerController.OpenServerLog();
            }

            if (GUILayout.Button("打开构建日志"))
            {
                LocalTestServerController.OpenBuildLog();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10f);
            EditorGUILayout.HelpBox(LocalTestServerController.DetailText, MessageType.None);
            EditorGUILayout.EndScrollView();
        }
    }

    [InitializeOnLoad]
    internal static class LocalTestServerController
    {
        private const int StateVersion = 1;
        private const int ServerPort = 9000;
        private const int MongoPort = 27017;
        private const string RequestedStateRun = "run";
        private const string RequestedStateStop = "stop";
        private const string DotnetOverrideKey = "game1.localServer.dotnetPath";
        private const string OnlineTechnicalScenePath = "Assets/Scenes/OnlineTechnicalScene.unity";
        private const string ParentProcessIdEnvironmentVariable = "GAME1_LOCAL_SERVER_PARENT_PID";
        private const string ParentStartTimeEnvironmentVariable =
            "GAME1_LOCAL_SERVER_PARENT_START_TIME_UTC_TICKS";
        private const string TokenEnvironmentVariable = "GAME1_LOCAL_SERVER_TOKEN";
        private const string ControlFileEnvironmentVariable = "GAME1_LOCAL_SERVER_CONTROL_FILE";
        private const double ServerStartTimeoutSeconds = 25d;
        private const double GracefulStopTimeoutSeconds = 20d;
        private static readonly object BuildLogLock = new object();
        private static Process _buildProcess;
        private static LocalServerLauncherState _state;
        private static string _activeBuildLogPath = string.Empty;
        private static string _statusText = "尚未检测";
        private static string _detailText = string.Empty;
        private static string _resolvedDotnetPath = "未检测";
        private static string _requiredSdkVersion = "未检测";
        private static string _mongoStatus = "未检测";
        private static string _serverPortStatus = "未检测";
        private static bool _startAfterBuild;
        private static bool _playAfterStart;
        private static bool _waitingForServerPort;
        private static bool _stopping;
        private static double _nextPortProbeAt;
        private static double _serverStartDeadline;
        private static double _stopDeadline;

        public static event Action Changed;

        static LocalTestServerController()
        {
            EditorApplication.update += Update;
            EditorApplication.quitting += OnEditorQuitting;
            RecoverOwnedServer();
        }

        public static string DotnetOverride => EditorPrefs.GetString(DotnetOverrideKey, string.Empty);
        public static string RepositoryRoot => GetRepositoryRoot();
        public static string RequiredSdkVersion => _requiredSdkVersion;
        public static string ResolvedDotnetPath => _resolvedDotnetPath;
        public static string MongoStatus => _mongoStatus;
        public static string ServerPortStatus => _serverPortStatus;
        public static string StatusText => _statusText;
        public static string DetailText => _detailText;
        public static bool IsBusy => _buildProcess != null || _waitingForServerPort || _stopping;
        public static bool CanStop
        {
            get
            {
                bool owned = TryGetOwnedServer(out Process process, out _);
                process?.Dispose();
                return owned;
            }
        }

        public static void SetDotnetOverride(string path)
        {
            string normalizedPath = string.IsNullOrWhiteSpace(path) ? string.Empty : Path.GetFullPath(path.Trim());
            EditorPrefs.SetString(DotnetOverrideKey, normalizedPath);
            RefreshStatus();
        }

        public static void RefreshStatus()
        {
            try
            {
                _requiredSdkVersion = ReadRequiredSdkVersion();
                _resolvedDotnetPath = ResolveDotnetPath(_requiredSdkVersion);
            }
            catch (Exception exception)
            {
                _resolvedDotnetPath = "不可用";
                SetStatus("依赖检测失败", exception.Message);
            }

            _mongoStatus = IsTcpPortOpen(MongoPort, 250) ? "可连接" : "不可连接";
            if (TryGetOwnedServer(out Process ownedServer, out string ownershipMessage))
            {
                _serverPortStatus = $"本工具拥有，PID {ownedServer.Id}";
                ownedServer.Dispose();
                if (!IsBusy)
                {
                    SetStatus("本地服务端正在运行", ownershipMessage);
                }
            }
            else
            {
                bool serverPortOpen = IsTcpPortOpen(ServerPort, 250);
                _serverPortStatus = serverPortOpen ? "已占用（非本工具所有）" : "未监听";
                if (!IsBusy && string.Equals(_statusText, "尚未检测", StringComparison.Ordinal))
                {
                    SetStatus("就绪", ownershipMessage);
                }
            }

            NotifyChanged();
        }

        public static void BuildStartAndPlay()
        {
            BuildAndStart(true);
        }

        public static void BuildAndStart(bool playAfterStart)
        {
            if (IsBusy)
            {
                SetStatus("已有操作进行中", "请等待当前构建、启动或停止操作完成。");
                return;
            }

            if (TryGetOwnedServer(out Process ownedServer, out _))
            {
                ownedServer.Dispose();
                if (playAfterStart)
                {
                    OpenSceneAndPlay();
                }
                else
                {
                    SetStatus("本地服务端已在运行", "未重复启动进程。");
                }
                return;
            }

            if (!PrepareStart(out string dotnetPath))
            {
                return;
            }

            string buildLogPath = GetBuildLogPath();
            _activeBuildLogPath = buildLogPath;
            Directory.CreateDirectory(Path.GetDirectoryName(buildLogPath));
            File.WriteAllText(
                buildLogPath,
                $"[{DateTime.Now:O}] dotnet build server/Server.csproj\n",
                new UTF8Encoding(false));

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = dotnetPath,
                Arguments = $"build {QuoteArgument(GetServerProjectPath())} --configuration Debug --nologo",
                WorkingDirectory = GetRepositoryRoot(),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            Process buildProcess = new Process
            {
                StartInfo = startInfo
            };
            buildProcess.OutputDataReceived += AppendBuildLog;
            buildProcess.ErrorDataReceived += AppendBuildLog;

            try
            {
                if (!buildProcess.Start())
                {
                    throw new InvalidOperationException("dotnet build 进程未启动。");
                }

                buildProcess.BeginOutputReadLine();
                buildProcess.BeginErrorReadLine();
                _buildProcess = buildProcess;
                _startAfterBuild = true;
                _playAfterStart = playAfterStart;
                SetStatus("正在构建服务端", $"构建日志：{buildLogPath}");
            }
            catch (Exception exception)
            {
                buildProcess.Dispose();
                SetStatus("构建启动失败", exception.Message);
            }
        }

        public static void StartBuiltServer(bool playAfterStart)
        {
            if (IsBusy)
            {
                SetStatus("已有操作进行中", "请等待当前构建、启动或停止操作完成。");
                return;
            }

            if (TryGetOwnedServer(out Process ownedServer, out _))
            {
                ownedServer.Dispose();
                if (playAfterStart)
                {
                    OpenSceneAndPlay();
                }
                return;
            }

            if (!PrepareStart(out string dotnetPath))
            {
                return;
            }

            string serverAssemblyPath = GetServerAssemblyPath();
            if (!File.Exists(serverAssemblyPath))
            {
                SetStatus("缺少已构建服务端", $"未找到 {serverAssemblyPath}，请先执行构建。");
                return;
            }

            StartServerProcess(dotnetPath, serverAssemblyPath, playAfterStart);
        }

        public static void Stop()
        {
            if (!TryGetOwnedServer(out Process process, out string ownershipMessage))
            {
                SetStatus("未停止任何进程", $"没有通过所有权校验的服务端。{ownershipMessage}");
                return;
            }

            process.Dispose();
            try
            {
                WriteControlState(_state, RequestedStateStop);
                _stopping = true;
                _stopDeadline = EditorApplication.timeSinceStartup + GracefulStopTimeoutSeconds;
                SetStatus("正在优雅停止服务端", "服务端将先保存玩家状态；超时后只会终止已核对 PID 和启动时间的进程。");
            }
            catch (Exception exception)
            {
                SetStatus("停止请求失败", exception.Message);
            }
        }

        public static void OpenSceneAndPlay()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                SetStatus("Unity 已在播放或切换播放状态", "未重复切换场景或播放状态。");
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            bool sameScene = string.Equals(activeScene.path, OnlineTechnicalScenePath, StringComparison.Ordinal);
            if (!sameScene && activeScene.isDirty)
            {
                SetStatus(
                    "未切换场景",
                    "当前场景存在未保存修改。工具不会静默丢弃修改；保存后再执行“打开场景并播放”。");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(OnlineTechnicalScenePath) == null)
            {
                SetStatus("缺少联机场景", $"未找到 {OnlineTechnicalScenePath}。");
                return;
            }

            if (!sameScene)
            {
                EditorSceneManager.OpenScene(OnlineTechnicalScenePath, OpenSceneMode.Single);
            }

            EditorApplication.isPlaying = true;
            SetStatus("已打开联机场景并进入播放", OnlineTechnicalScenePath);
        }

        public static void OpenServerLog()
        {
            string logDirectory = Path.Combine(GetBinDirectory(), "log");
            string newestGameLogPath = Directory.Exists(logDirectory)
                ? Directory.GetFiles(logDirectory, "game*.log")
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .FirstOrDefault()
                : null;
            OpenFileOrDirectory(newestGameLogPath, logDirectory);
        }

        public static void OpenBuildLog()
        {
            OpenFileOrDirectory(GetBuildLogPath(), GetStorageDirectory());
        }

        private static bool PrepareStart(out string dotnetPath)
        {
            dotnetPath = string.Empty;
            if (!IsTcpPortOpen(MongoPort, 350))
            {
                _mongoStatus = "不可连接";
                SetStatus("MongoDB 未启动", "请先确保 127.0.0.1:27017 可连接；工具不会启动或修改 MongoDB。");
                return false;
            }

            _mongoStatus = "可连接";
            if (IsTcpPortOpen(ServerPort, 350))
            {
                _serverPortStatus = "已占用（非本工具所有）";
                SetStatus("端口 9000 已被占用", "为避免停止错误进程，本工具不会接管未知监听者。");
                return false;
            }

            try
            {
                _requiredSdkVersion = ReadRequiredSdkVersion();
                dotnetPath = ResolveDotnetPath(_requiredSdkVersion);
                _resolvedDotnetPath = dotnetPath;
                return true;
            }
            catch (Exception exception)
            {
                _resolvedDotnetPath = "不可用";
                SetStatus("找不到项目要求的 dotnet SDK", exception.Message);
                return false;
            }
        }

        private static void StartServerProcess(string dotnetPath, string serverAssemblyPath, bool playAfterStart)
        {
            Process unityProcess = Process.GetCurrentProcess();
            string token = Guid.NewGuid().ToString("N");
            string controlFilePath = Path.Combine(GetStorageDirectory(), $"control-{token}.json");
            LocalServerLauncherState state = new LocalServerLauncherState
            {
                Version = StateVersion,
                RepositoryRoot = GetRepositoryRoot(),
                UnityProcessId = unityProcess.Id,
                UnityStartTimeUtcTicks = unityProcess.StartTime.ToUniversalTime().Ticks,
                ServerProcessId = 0,
                ServerStartTimeUtcTicks = 0L,
                Token = token,
                ControlFilePath = controlFilePath,
                DotnetPath = Path.GetFullPath(dotnetPath),
                ServerAssemblyPath = Path.GetFullPath(serverAssemblyPath)
            };
            unityProcess.Dispose();

            Directory.CreateDirectory(GetStorageDirectory());
            WriteControlState(state, RequestedStateRun);

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = state.DotnetPath,
                Arguments = QuoteArgument(Path.GetFileName(state.ServerAssemblyPath)),
                WorkingDirectory = GetBinDirectory(),
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            startInfo.EnvironmentVariables[ParentProcessIdEnvironmentVariable] = state.UnityProcessId.ToString();
            startInfo.EnvironmentVariables[ParentStartTimeEnvironmentVariable] =
                state.UnityStartTimeUtcTicks.ToString();
            startInfo.EnvironmentVariables[TokenEnvironmentVariable] = state.Token;
            startInfo.EnvironmentVariables[ControlFileEnvironmentVariable] = state.ControlFilePath;

            Process process = new Process
            {
                StartInfo = startInfo
            };

            try
            {
                if (!process.Start())
                {
                    throw new InvalidOperationException("dotnet Server.dll 进程未启动。");
                }

                state.ServerProcessId = process.Id;
                state.ServerStartTimeUtcTicks = process.StartTime.ToUniversalTime().Ticks;
                _state = state;
                WriteJsonAtomically(GetLauncherStatePath(), state);
                _playAfterStart = playAfterStart;
                _waitingForServerPort = true;
                _nextPortProbeAt = EditorApplication.timeSinceStartup;
                _serverStartDeadline = EditorApplication.timeSinceStartup + ServerStartTimeoutSeconds;
                _serverPortStatus = $"进程已启动，PID {process.Id}，等待监听";
                SetStatus("正在启动本地服务端", "服务端运行目录固定为仓库 bin，正在等待 9000 端口监听。");
            }
            catch (Exception exception)
            {
                SafeDelete(controlFilePath);
                SetStatus("服务端进程启动失败", exception.Message);
            }
            finally
            {
                process.Dispose();
            }
        }

        private static void Update()
        {
            UpdateBuildProcess();
            UpdateServerStart();
            UpdateServerStop();
            DetectUnexpectedServerExit();
        }

        private static void UpdateBuildProcess()
        {
            if (_buildProcess == null || !_buildProcess.HasExited)
            {
                return;
            }

            Process completedProcess = _buildProcess;
            _buildProcess = null;
            int exitCode = completedProcess.ExitCode;
            completedProcess.WaitForExit();
            completedProcess.Dispose();
            _activeBuildLogPath = string.Empty;

            if (exitCode != 0)
            {
                _startAfterBuild = false;
                _playAfterStart = false;
                SetStatus("服务端构建失败", $"dotnet build 退出码 {exitCode}。请打开构建日志查看详情。");
                return;
            }

            if (!_startAfterBuild)
            {
                SetStatus("服务端构建完成", GetServerAssemblyPath());
                return;
            }

            bool playAfterStart = _playAfterStart;
            _startAfterBuild = false;
            StartBuiltServer(playAfterStart);
        }

        private static void UpdateServerStart()
        {
            if (!_waitingForServerPort || EditorApplication.timeSinceStartup < _nextPortProbeAt)
            {
                return;
            }

            _nextPortProbeAt = EditorApplication.timeSinceStartup + 0.4d;
            if (!TryGetOwnedServer(out Process process, out string ownershipMessage))
            {
                _waitingForServerPort = false;
                _playAfterStart = false;
                SetStatus("服务端启动期间退出", ownershipMessage);
                return;
            }
            process.Dispose();

            if (IsTcpPortOpen(ServerPort, 200))
            {
                _waitingForServerPort = false;
                _serverPortStatus = $"本工具拥有，PID {_state.ServerProcessId}";
                bool shouldPlay = _playAfterStart;
                _playAfterStart = false;
                SetStatus("本地服务端已就绪", "127.0.0.1:9000 已开始监听。");
                if (shouldPlay)
                {
                    OpenSceneAndPlay();
                }
                return;
            }

            if (EditorApplication.timeSinceStartup >= _serverStartDeadline)
            {
                _waitingForServerPort = false;
                _playAfterStart = false;
                SetStatus("服务端启动超时", "进程仍受本工具所有；请打开服务端日志诊断，或点击停止。");
            }
        }

        private static void UpdateServerStop()
        {
            if (!_stopping)
            {
                return;
            }

            if (!TryGetOwnedServer(out Process process, out string ownershipMessage))
            {
                _stopping = false;
                ClearOwnedStateAfterExit();
                _serverPortStatus = IsTcpPortOpen(ServerPort, 200) ? "仍被其他进程占用" : "未监听";
                SetStatus("本地服务端已停止", ownershipMessage);
                return;
            }

            if (EditorApplication.timeSinceStartup < _stopDeadline)
            {
                process.Dispose();
                return;
            }

            try
            {
                if (!MatchesOwnedServer(process, _state, out string mismatchReason))
                {
                    _stopping = false;
                    SetStatus("拒绝强制停止进程", mismatchReason);
                    return;
                }

                process.Kill();
                SetStatus("优雅停止超时，已终止拥有的进程", $"PID {_state.ServerProcessId}");
            }
            catch (Exception exception)
            {
                _stopping = false;
                SetStatus("终止服务端失败", exception.Message);
            }
            finally
            {
                process.Dispose();
            }
        }

        private static void DetectUnexpectedServerExit()
        {
            if (_state == null || _waitingForServerPort || _stopping)
            {
                return;
            }

            if (TryGetOwnedServer(out Process process, out _))
            {
                process.Dispose();
                return;
            }

            ClearOwnedStateAfterExit();
            _serverPortStatus = "未监听";
            SetStatus("本地服务端已退出", "进程不再存在，已清理本次 Unity 会话的所有权记录。");
        }

        private static void RecoverOwnedServer()
        {
            string statePath = GetLauncherStatePath();
            if (!File.Exists(statePath))
            {
                return;
            }

            try
            {
                LocalServerLauncherState state = JsonUtility.FromJson<LocalServerLauncherState>(
                    File.ReadAllText(statePath, Encoding.UTF8));
                _state = state;
                if (TryGetOwnedServer(out Process process, out string ownershipMessage))
                {
                    _serverPortStatus = $"本工具拥有，PID {process.Id}";
                    process.Dispose();
                    SetStatus("已恢复本地服务端所有权", $"Domain Reload 后已核对：{ownershipMessage}");
                }
                else
                {
                    _state = null;
                    SetStatus("未恢复旧服务端所有权", ownershipMessage);
                }
            }
            catch (Exception exception)
            {
                _state = null;
                SetStatus("本地服务端状态文件无效", exception.Message);
            }
        }

        private static bool TryGetOwnedServer(out Process process, out string message)
        {
            process = null;
            message = string.Empty;
            if (_state == null || _state.Version != StateVersion)
            {
                message = "没有当前 Unity 会话的进程记录。";
                return false;
            }

            Process unityProcess = Process.GetCurrentProcess();
            try
            {
                if (_state.UnityProcessId != unityProcess.Id ||
                    _state.UnityStartTimeUtcTicks != unityProcess.StartTime.ToUniversalTime().Ticks ||
                    !string.Equals(unityProcess.ProcessName, "Unity", StringComparison.OrdinalIgnoreCase))
                {
                    message = "状态文件不属于当前 Unity Editor 进程。";
                    return false;
                }
            }
            finally
            {
                unityProcess.Dispose();
            }

            if (!PathEquals(_state.RepositoryRoot, GetRepositoryRoot()) ||
                !PathEquals(_state.ServerAssemblyPath, GetServerAssemblyPath()))
            {
                message = "状态文件指向其他仓库或服务端程序集。";
                return false;
            }

            if (!TryReadMatchingControlState(_state, out message))
            {
                return false;
            }

            try
            {
                process = Process.GetProcessById(_state.ServerProcessId);
                if (!MatchesOwnedServer(process, _state, out message))
                {
                    process.Dispose();
                    process = null;
                    return false;
                }

                message = $"PID {_state.ServerProcessId}、启动时间、dotnet 路径、token 和控制文件均匹配。";
                return true;
            }
            catch (Exception exception)
            {
                process = null;
                message = $"服务端进程不存在或不可读取：{exception.Message}";
                return false;
            }
        }

        private static bool MatchesOwnedServer(
            Process process,
            LocalServerLauncherState state,
            out string message)
        {
            if (process.HasExited)
            {
                message = "服务端进程已退出。";
                return false;
            }

            if (process.Id != state.ServerProcessId ||
                process.StartTime.ToUniversalTime().Ticks != state.ServerStartTimeUtcTicks)
            {
                message = "服务端 PID 或启动时间不匹配，可能发生 PID 复用。";
                return false;
            }

            try
            {
                string executablePath = process.MainModule == null ? string.Empty : process.MainModule.FileName;
                if (!PathEquals(executablePath, state.DotnetPath))
                {
                    message = "服务端进程的 dotnet 可执行文件不匹配。";
                    return false;
                }
            }
            catch (Exception exception)
            {
                message = $"无法核对服务端可执行文件：{exception.Message}";
                return false;
            }

            message = string.Empty;
            return true;
        }

        private static bool TryReadMatchingControlState(LocalServerLauncherState state, out string message)
        {
            try
            {
                LocalServerControlState controlState = JsonUtility.FromJson<LocalServerControlState>(
                    File.ReadAllText(state.ControlFilePath, Encoding.UTF8));
                if (controlState == null || controlState.Version != StateVersion ||
                    controlState.ParentProcessId != state.UnityProcessId ||
                    controlState.ParentStartTimeUtcTicks != state.UnityStartTimeUtcTicks ||
                    !string.Equals(controlState.Token, state.Token, StringComparison.Ordinal))
                {
                    message = "控制文件的版本、父进程或 token 不匹配。";
                    return false;
                }

                message = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                message = $"控制文件不可读取：{exception.Message}";
                return false;
            }
        }

        private static void WriteControlState(LocalServerLauncherState state, string requestedState)
        {
            LocalServerControlState controlState = new LocalServerControlState
            {
                Version = StateVersion,
                ParentProcessId = state.UnityProcessId,
                ParentStartTimeUtcTicks = state.UnityStartTimeUtcTicks,
                Token = state.Token,
                RequestedState = requestedState,
                UpdatedAtUtcTicks = DateTime.UtcNow.Ticks
            };
            WriteJsonAtomically(state.ControlFilePath, controlState);
        }

        private static void WriteJsonAtomically(string path, object value)
        {
            string directory = Path.GetDirectoryName(path);
            Directory.CreateDirectory(directory);
            string temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
            File.WriteAllText(temporaryPath, JsonUtility.ToJson(value, true), new UTF8Encoding(false));
            if (File.Exists(path))
            {
                File.Replace(temporaryPath, path, null);
            }
            else
            {
                File.Move(temporaryPath, path);
            }
        }

        private static string ReadRequiredSdkVersion()
        {
            string globalJsonPath = Path.Combine(GetRepositoryRoot(), "global.json");
            if (!File.Exists(globalJsonPath))
            {
                throw new FileNotFoundException("仓库缺少 global.json。", globalJsonPath);
            }

            GlobalJsonRoot root = JsonUtility.FromJson<GlobalJsonRoot>(File.ReadAllText(globalJsonPath, Encoding.UTF8));
            if (root == null || root.sdk == null || string.IsNullOrWhiteSpace(root.sdk.version))
            {
                throw new InvalidDataException("global.json 未声明 sdk.version。");
            }

            return root.sdk.version.Trim();
        }

        private static string ResolveDotnetPath(string requiredSdkVersion)
        {
            List<string> candidates = CollectDotnetCandidates();
            foreach (string candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (File.Exists(candidate) && ContainsSdk(candidate, requiredSdkVersion))
                {
                    return Path.GetFullPath(candidate);
                }
            }

            throw new FileNotFoundException(
                $"未找到包含 SDK {requiredSdkVersion} 的 dotnet。可在窗口中设置 dotnet.exe 路径覆盖。");
        }

        private static List<string> CollectDotnetCandidates()
        {
            List<string> candidates = new List<string>();
            AddDotnetCandidate(candidates, DotnetOverride);
            AddDotnetCandidate(candidates, Environment.GetEnvironmentVariable("DOTNET_ROOT"));
            AddDotnetCandidate(candidates, Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "dotnet"));
            AddDotnetCandidate(candidates, Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".dotnet"));
            AddDotnetCandidate(candidates, Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft",
                "dotnet"));

            string pathValue = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (string directory in pathValue.Split(Path.PathSeparator))
            {
                AddDotnetCandidate(candidates, directory.Trim());
            }
            return candidates;
        }

        private static void AddDotnetCandidate(List<string> candidates, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            string trimmedPath = path.Trim().Trim('"');
            if (Directory.Exists(trimmedPath))
            {
                candidates.Add(Path.Combine(trimmedPath, GetDotnetExecutableName()));
            }
            else
            {
                candidates.Add(trimmedPath);
            }
        }

        private static bool ContainsSdk(string dotnetPath, string requiredSdkVersion)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = dotnetPath,
                Arguments = "--list-sdks",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using Process process = Process.Start(startInfo);
            if (process == null)
            {
                return false;
            }

            string standardOutput = process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();
            if (!process.WaitForExit(5000))
            {
                process.Kill();
                return false;
            }

            return process.ExitCode == 0 && standardOutput
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Any(line => line.StartsWith($"{requiredSdkVersion} [", StringComparison.Ordinal));
        }

        private static bool IsTcpPortOpen(int port, int timeoutMilliseconds)
        {
            using TcpClient client = new TcpClient();
            try
            {
                IAsyncResult connectResult = client.BeginConnect("127.0.0.1", port, null, null);
                using WaitHandle waitHandle = connectResult.AsyncWaitHandle;
                if (!waitHandle.WaitOne(timeoutMilliseconds))
                {
                    return false;
                }

                client.EndConnect(connectResult);
                return client.Connected;
            }
            catch
            {
                return false;
            }
        }

        private static void AppendBuildLog(object sender, DataReceivedEventArgs eventArgs)
        {
            if (eventArgs.Data == null)
            {
                return;
            }

            lock (BuildLogLock)
            {
                if (!string.IsNullOrEmpty(_activeBuildLogPath))
                {
                    File.AppendAllText(_activeBuildLogPath, $"{eventArgs.Data}\n", new UTF8Encoding(false));
                }
            }
        }

        private static void OnEditorQuitting()
        {
            if (!TryGetOwnedServer(out Process process, out _))
            {
                return;
            }

            process.Dispose();
            try
            {
                WriteControlState(_state, RequestedStateStop);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[LocalTestServer] Unity 退出时写入停服请求失败：{exception.Message}");
            }
        }

        private static void ClearOwnedStateAfterExit()
        {
            if (_state != null)
            {
                SafeDelete(_state.ControlFilePath);
            }
            SafeDelete(GetLauncherStatePath());
            _state = null;
        }

        private static void OpenFileOrDirectory(string filePath, string fallbackDirectory)
        {
            if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
            {
                EditorUtility.OpenWithDefaultApp(filePath);
                return;
            }

            Directory.CreateDirectory(fallbackDirectory);
            EditorUtility.RevealInFinder(fallbackDirectory);
            SetStatus("日志尚未生成", $"已打开目录：{fallbackDirectory}");
        }

        private static void SafeDelete(string path)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[LocalTestServer] 清理状态文件失败：{exception.Message}");
            }
        }

        private static bool PathEquals(string firstPath, string secondPath)
        {
            if (string.IsNullOrWhiteSpace(firstPath) || string.IsNullOrWhiteSpace(secondPath))
            {
                return false;
            }

            return string.Equals(
                Path.GetFullPath(firstPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetFullPath(secondPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }

        private static string QuoteArgument(string argument)
        {
            return $"\"{argument.Replace("\"", "\\\"")}\"";
        }

        private static string GetRepositoryRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        }

        private static string GetBinDirectory()
        {
            return Path.Combine(GetRepositoryRoot(), "bin");
        }

        private static string GetServerProjectPath()
        {
            return Path.Combine(GetRepositoryRoot(), "server", "Server.csproj");
        }

        private static string GetServerAssemblyPath()
        {
            return Path.Combine(GetBinDirectory(), "Server.dll");
        }

        private static string GetStorageDirectory()
        {
            return Path.Combine(GetRepositoryRoot(), "client", "Library", "Game1LocalServer");
        }

        private static string GetLauncherStatePath()
        {
            return Path.Combine(GetStorageDirectory(), "launcher-state.json");
        }

        private static string GetBuildLogPath()
        {
            return Path.Combine(GetStorageDirectory(), "build.log");
        }

        private static string GetDotnetExecutableName()
        {
            return Environment.OSVersion.Platform == PlatformID.Win32NT ? "dotnet.exe" : "dotnet";
        }

        private static void SetStatus(string status, string detail)
        {
            _statusText = status;
            _detailText = detail;
            NotifyChanged();
        }

        private static void NotifyChanged()
        {
            Changed?.Invoke();
        }

        [Serializable]
        private sealed class GlobalJsonRoot
        {
            public GlobalJsonSdk sdk;
        }

        [Serializable]
        private sealed class GlobalJsonSdk
        {
            public string version;
        }

        [Serializable]
        private sealed class LocalServerLauncherState
        {
            public int Version;
            public string RepositoryRoot;
            public int UnityProcessId;
            public long UnityStartTimeUtcTicks;
            public int ServerProcessId;
            public long ServerStartTimeUtcTicks;
            public string Token;
            public string ControlFilePath;
            public string DotnetPath;
            public string ServerAssemblyPath;
        }

        [Serializable]
        private sealed class LocalServerControlState
        {
            public int Version;
            public int ParentProcessId;
            public long ParentStartTimeUtcTicks;
            public string Token;
            public string RequestedState;
            public long UpdatedAtUtcTicks;
        }
    }
}
