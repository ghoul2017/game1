using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.UI;
using WeChatWASM;

namespace P0PlatformSmoke
{
    public sealed class P0PlatformSmokeRunner : MonoBehaviour
    {
        private const int EchoCount = 20;
        private const double SdkInitTimeoutSeconds = 10.0;
        private const double ConfigReadTimeoutSeconds = 10.0;
        private const double ConnectTimeoutSeconds = 10.0;
        private const double RequestTimeoutSeconds = 5.0;
        private const double TotalTcpTimeoutSeconds = 70.0;

        [SerializeField]
        private Text _logText;

        [SerializeField]
        private string _tcpAddress = "";

        [SerializeField]
        private int _tcpPort;

        private readonly List<string> _lines = new List<string>();
        private readonly List<byte> _receivedBytes = new List<byte>();
        private bool _tcpConnected;
        private bool _tcpClosed;
        private bool _expectedClose;
        private bool _sdkInitialized;
        private string _transportError = "";
        private double _startedAt;
        private double _configReadMilliseconds;
        private WXTCPSocket _tcpSocket;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void P0ExposeWechatSdkUtf8RuntimeMethods();
#endif

        private IEnumerator Start()
        {
            _startedAt = Time.realtimeSinceStartupAsDouble;
            Append($"P0_BOOT elapsed_ms={ElapsedMilliseconds(_startedAt):F1}");

            yield return InitializeSdkAsync();
            if (!_sdkInitialized)
            {
                yield break;
            }

            if (!ValidateProtobuf())
            {
                yield break;
            }

            yield return ReadConfigAsync();
            if (_configReadMilliseconds < 0.0)
            {
                yield break;
            }

            yield return RunTcpSocketAsync();
            if (_transportError.Length > 0)
            {
                yield break;
            }

            double readyMilliseconds = ElapsedMilliseconds(_startedAt);
            Append($"P0_READY elapsed_ms={readyMilliseconds:F1}");
            AppendMetrics("ready");

            yield return new WaitForSecondsRealtime(60.0f);
            Append("P0_STABLE_60S");
            AppendMetrics("stable_60s");
        }

        private IEnumerator InitializeSdkAsync()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            _sdkInitialized = false;
            bool callbackReceived = false;
            int initCode = int.MinValue;

            try
            {
                WX.InitSDK(code =>
                {
                    initCode = code;
                    callbackReceived = true;
                });
            }
            catch (Exception exception)
            {
                Fail("sdk_init", exception.Message);
                yield break;
            }

            double deadline = Time.realtimeSinceStartupAsDouble + SdkInitTimeoutSeconds;
            while (!callbackReceived && Time.realtimeSinceStartupAsDouble < deadline)
            {
                yield return null;
            }

            if (!callbackReceived)
            {
                Fail("sdk_init", "WX.InitSDK 回调超时");
                yield break;
            }

            try
            {
                P0ExposeWechatSdkUtf8RuntimeMethods();
                object systemInfo = WX.GetSystemInfoSync();
                if (systemInfo == null)
                {
                    throw new InvalidOperationException("微信 SDK 未返回系统信息");
                }

                _sdkInitialized = true;
                Append($"P0_SDK_OK init_code={initCode}");
            }
            catch (Exception exception)
            {
                Fail("sdk", exception.Message);
                yield break;
            }
#else
            _sdkInitialized = true;
            Append("P0_SDK_EDITOR_ONLY");
            yield break;
#endif
        }

        private bool ValidateProtobuf()
        {
            try
            {
                byte[] packet = P0PlatformSmokePayload.CreateValidatedPacket();
                Append($"P0_PROTO_OK packet_bytes={packet.Length}");
                return true;
            }
            catch (Exception exception)
            {
                Fail("protobuf", exception.Message);
                return false;
            }
        }

        private IEnumerator ReadConfigAsync()
        {
            _configReadMilliseconds = -1.0;
            double startedAt = Time.realtimeSinceStartupAsDouble;
            Append("P0_CONFIG_BEGIN source=package_resources_async");
            ResourceRequest request = Resources.LoadAsync<TextAsset>("p0-smoke");
            double deadline = startedAt + ConfigReadTimeoutSeconds;
            while (!request.isDone && Time.realtimeSinceStartupAsDouble < deadline)
            {
                yield return null;
            }

            if (!request.isDone)
            {
                Fail("config", "随包 Resources 异步读取超时");
                yield break;
            }

            TextAsset configAsset = request.asset as TextAsset;
            if (configAsset == null)
            {
                Fail("config", "随包 Resources 缺少 p0-smoke.csv");
                yield break;
            }

            string text = configAsset.text;
            int byteCount = Encoding.UTF8.GetByteCount(text);

            try
            {
                P0PlatformSmokePayload.ValidateConfig(text);
            }
            catch (Exception exception)
            {
                Fail("config", exception.Message);
                yield break;
            }

            Resources.UnloadAsset(configAsset);
            _configReadMilliseconds = ElapsedMilliseconds(startedAt);
            Append($"P0_CONFIG_OK elapsed_ms={_configReadMilliseconds:F1} bytes={byteCount}");
        }

        private IEnumerator RunTcpSocketAsync()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            _tcpConnected = false;
            _tcpClosed = false;
            _expectedClose = false;
            _transportError = "";
            _receivedBytes.Clear();

            if (string.IsNullOrWhiteSpace(_tcpAddress) || _tcpPort <= 0)
            {
                Fail("tcp_config", "未配置局域网 TCP 地址或端口");
                yield break;
            }

            double tcpStartedAt = Time.realtimeSinceStartupAsDouble;
            Append("P0_TCP_BEGIN route=lan");
            _tcpSocket = WX.CreateTCPSocket();
            if (_tcpSocket == null)
            {
                Fail("tcp_create", "WX.CreateTCPSocket 未返回实例");
                yield break;
            }

            _tcpSocket.OnConnect(OnTcpConnect);
            _tcpSocket.OnMessage(OnTcpMessage, false);
            _tcpSocket.OnError(OnTcpError);
            _tcpSocket.OnClose(OnTcpClose);
            _tcpSocket.Connect(new TCPSocketConnectOption
            {
                address = _tcpAddress,
                port = _tcpPort,
                timeout = ConnectTimeoutSeconds * 1000.0,
            });

            double connectDeadline = tcpStartedAt + ConnectTimeoutSeconds;
            while (!_tcpConnected && _transportError.Length == 0 &&
                   Time.realtimeSinceStartupAsDouble < connectDeadline)
            {
                yield return null;
            }

            if (!_tcpConnected)
            {
                Fail("tcp_connect", _transportError.Length > 0 ? _transportError : "连接超时");
                CloseTcpSocket();
                yield break;
            }

            byte[] expectedPacket = P0PlatformSmokePayload.CreateValidatedPacket();
            List<double> roundTrips = new List<double>(EchoCount);
            int successCount = 0;

            for (int index = 0; index < EchoCount; index++)
            {
                if (Time.realtimeSinceStartupAsDouble - tcpStartedAt > TotalTcpTimeoutSeconds)
                {
                    Fail("tcp_total", "总测试超时");
                    CloseTcpSocket();
                    yield break;
                }

                _receivedBytes.Clear();
                double sentAt = Time.realtimeSinceStartupAsDouble;
                _tcpSocket.Write(expectedPacket);
                double requestDeadline = sentAt + RequestTimeoutSeconds;

                while (_receivedBytes.Count < expectedPacket.Length && _transportError.Length == 0 &&
                       Time.realtimeSinceStartupAsDouble < requestDeadline)
                {
                    yield return null;
                }

                if (_transportError.Length > 0)
                {
                    Fail("tcp_send", _transportError);
                    CloseTcpSocket();
                    yield break;
                }

                if (_receivedBytes.Count < expectedPacket.Length)
                {
                    Fail("tcp_echo", $"第 {index + 1} 次请求超时，received={_receivedBytes.Count}");
                    CloseTcpSocket();
                    yield break;
                }

                if (_receivedBytes.Count != expectedPacket.Length)
                {
                    Fail("tcp_stream", $"第 {index + 1} 次收到多余字节: {_receivedBytes.Count}");
                    CloseTcpSocket();
                    yield break;
                }

                byte[] echoedPacket = _receivedBytes.ToArray();
                try
                {
                    if (!P0PlatformSmokePayload.BytesEqual(expectedPacket, echoedPacket))
                    {
                        throw new InvalidDataException("echo 字节不一致");
                    }

                    P0PlatformSmokePayload.ValidatePacket(echoedPacket);
                }
                catch (Exception exception)
                {
                    Fail("tcp_payload", $"第 {index + 1} 次: {exception.Message}");
                    CloseTcpSocket();
                    yield break;
                }

                roundTrips.Add(ElapsedMilliseconds(sentAt));
                successCount++;
            }

            roundTrips.Sort();
            double median = Percentile(roundTrips, 0.50);
            double p95 = Percentile(roundTrips, 0.95);
            Append($"P0_TCP_OK success={successCount}/{EchoCount} median_ms={median:F1} p95_ms={p95:F1}");
            CloseTcpSocket();
#else
            _transportError = "当前不是导出的 WebGL/微信小游戏运行时";
            Append($"P0_TCP_BLOCKED error={_transportError}");
            yield break;
#endif
        }

        private void OnTcpConnect(GeneralCallbackResult result)
        {
            _tcpConnected = true;
        }

        private void OnTcpMessage(TCPSocketOnMessageListenerResult result)
        {
            if (result == null || result.message == null)
            {
                _transportError = "TCP 收到空消息";
                return;
            }

            _receivedBytes.AddRange(result.message);
        }

        private void OnTcpError(GeneralCallbackResult result)
        {
            string error = result == null ? "未知 TCP 错误" : result.errMsg;
            _transportError = string.IsNullOrWhiteSpace(error) ? "未知 TCP 错误" : error;
        }

        private void OnTcpClose(GeneralCallbackResult result)
        {
            _tcpClosed = true;
            if (!_expectedClose && _transportError.Length == 0)
            {
                string reason = result == null ? "TCP 意外关闭" : result.errMsg;
                _transportError = string.IsNullOrWhiteSpace(reason) ? "TCP 意外关闭" : reason;
            }
        }

        private void OnDestroy()
        {
            CloseTcpSocket();
        }

        private void CloseTcpSocket()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (_tcpSocket != null && !_tcpClosed)
            {
                _expectedClose = true;
                _tcpSocket.Close();
                _tcpClosed = true;
            }

            _tcpSocket = null;
#endif
        }

        private void AppendMetrics(string phase)
        {
            long allocatedBytes = Profiler.GetTotalAllocatedMemoryLong();
            long reservedBytes = Profiler.GetTotalReservedMemoryLong();
            Append(
                $"P0_METRIC phase={phase} config_ms={_configReadMilliseconds:F1} " +
                $"allocated_bytes={allocatedBytes} reserved_bytes={reservedBytes}");
        }

        private void Append(string message)
        {
            Debug.Log($"[P0PlatformSmoke] {message}");
            _lines.Add(message);
            if (_lines.Count > 30)
            {
                _lines.RemoveAt(0);
            }

            if (_logText != null)
            {
                _logText.text = string.Join("\n", _lines);
            }
        }

        private void Fail(string stage, string error)
        {
            _transportError = Sanitize(error);
            Append($"P0_FAIL stage={stage} error={_transportError}");
        }

        private static double ElapsedMilliseconds(double startedAt)
        {
            return (Time.realtimeSinceStartupAsDouble - startedAt) * 1000.0;
        }

        private static double Percentile(IReadOnlyList<double> sortedValues, double percentile)
        {
            if (sortedValues.Count == 0)
            {
                return 0.0;
            }

            int percentileIndex = Mathf.CeilToInt((float)(sortedValues.Count * percentile)) - 1;
            int index = Mathf.Clamp(percentileIndex, 0, sortedValues.Count - 1);
            return sortedValues[index];
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "未知错误";
            }

            return value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        }
    }
}
