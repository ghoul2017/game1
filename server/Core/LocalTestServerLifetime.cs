using System.Diagnostics;
using System.Text.Json;
using Shared.Core;

namespace Game;

/// <summary>
/// Unity Editor 启动的本地测试服务端生命周期。正常服务端启动不会启用此机制。
/// </summary>
public sealed class LocalTestServerLifetime : IAsyncDisposable
{
    public const string ParentProcessIdEnvironmentVariable = "GAME1_LOCAL_SERVER_PARENT_PID";
    public const string ParentStartTimeEnvironmentVariable = "GAME1_LOCAL_SERVER_PARENT_START_TIME_UTC_TICKS";
    public const string TokenEnvironmentVariable = "GAME1_LOCAL_SERVER_TOKEN";
    public const string ControlFileEnvironmentVariable = "GAME1_LOCAL_SERVER_CONTROL_FILE";

    private const int ControlVersion = 1;
    private const int MonitorIntervalMilliseconds = 500;
    private readonly LocalTestServerConfiguration _configuration;
    private readonly CancellationTokenSource _monitorCancellation = new();
    private readonly Task _monitorTask;

    private LocalTestServerLifetime(LocalTestServerConfiguration configuration)
    {
        _configuration = configuration;
        _monitorTask = Task.Run(MonitorAsync);
    }

    /// <summary>
    /// 仅在四个 GAME1_LOCAL_SERVER_* 环境变量全部不存在时返回 null。
    /// 变量部分存在或所有权证明无效时抛出异常，阻止启动不可控的本地服务端。
    /// </summary>
    public static LocalTestServerLifetime? StartIfConfigured()
    {
        Dictionary<string, string?> environment = new()
        {
            [ParentProcessIdEnvironmentVariable] = Environment.GetEnvironmentVariable(
                ParentProcessIdEnvironmentVariable),
            [ParentStartTimeEnvironmentVariable] = Environment.GetEnvironmentVariable(
                ParentStartTimeEnvironmentVariable),
            [TokenEnvironmentVariable] = Environment.GetEnvironmentVariable(TokenEnvironmentVariable),
            [ControlFileEnvironmentVariable] = Environment.GetEnvironmentVariable(ControlFileEnvironmentVariable)
        };

        if (!TryReadConfiguration(environment, out LocalTestServerConfiguration? configuration))
        {
            return null;
        }

        LocalTestServerStopReason stopReason = Evaluate(configuration!, CaptureParentProcess, File.ReadAllText);
        if (stopReason != LocalTestServerStopReason.None)
        {
            throw new InvalidOperationException(
                $"[LocalTestServer] Unity Editor 所有权校验失败：{Describe(stopReason)}");
        }

        Console.WriteLine(
            $"[LocalTestServer] 已绑定 Unity PID {configuration!.ParentProcessId}，控制文件 " +
            $"{configuration.ControlFilePath}");
        return new LocalTestServerLifetime(configuration);
    }

    public async ValueTask DisposeAsync()
    {
        _monitorCancellation.Cancel();
        try
        {
            await _monitorTask;
        }
        catch (OperationCanceledException)
        {
            // 主服务端已结束，监控任务随之退出。
        }
        finally
        {
            _monitorCancellation.Dispose();
        }
    }

    internal static bool TryReadConfiguration(
        IReadOnlyDictionary<string, string?> environment,
        out LocalTestServerConfiguration? configuration)
    {
        configuration = null;
        string? parentProcessIdText = ReadEnvironmentValue(environment, ParentProcessIdEnvironmentVariable);
        string? parentStartTimeText = ReadEnvironmentValue(environment, ParentStartTimeEnvironmentVariable);
        string? token = ReadEnvironmentValue(environment, TokenEnvironmentVariable);
        string? controlFilePath = ReadEnvironmentValue(environment, ControlFileEnvironmentVariable);

        int suppliedValueCount = new[]
        {
            parentProcessIdText,
            parentStartTimeText,
            token,
            controlFilePath
        }.Count(value => !string.IsNullOrWhiteSpace(value));
        if (suppliedValueCount == 0)
        {
            return false;
        }

        if (suppliedValueCount != 4)
        {
            throw new InvalidOperationException(
                "[LocalTestServer] GAME1_LOCAL_SERVER_* 环境变量必须全部提供或全部省略。");
        }

        if (!int.TryParse(parentProcessIdText, out int parentProcessId) || parentProcessId <= 0)
        {
            throw new InvalidOperationException("[LocalTestServer] Unity 父进程 PID 无效。");
        }

        if (!long.TryParse(parentStartTimeText, out long parentStartTimeUtcTicks) || parentStartTimeUtcTicks <= 0L)
        {
            throw new InvalidOperationException("[LocalTestServer] Unity 父进程启动时间无效。");
        }

        if (token!.Length < 32 || token.Any(char.IsWhiteSpace))
        {
            throw new InvalidOperationException("[LocalTestServer] 所有权 token 无效。");
        }

        if (!Path.IsPathFullyQualified(controlFilePath!))
        {
            throw new InvalidOperationException("[LocalTestServer] 控制文件必须使用绝对路径。");
        }

        configuration = new LocalTestServerConfiguration(
            parentProcessId,
            parentStartTimeUtcTicks,
            token,
            Path.GetFullPath(controlFilePath!));
        return true;
    }

    internal static LocalTestServerStopReason Evaluate(
        LocalTestServerConfiguration configuration,
        Func<int, LocalTestServerParentProcess?> captureParentProcess,
        Func<string, string> readControlFile)
    {
        LocalTestServerParentProcess? parentProcess;
        try
        {
            parentProcess = captureParentProcess(configuration.ParentProcessId);
        }
        catch
        {
            return LocalTestServerStopReason.ParentProcessMissing;
        }

        if (parentProcess == null)
        {
            return LocalTestServerStopReason.ParentProcessMissing;
        }

        if (parentProcess.StartTimeUtcTicks != configuration.ParentStartTimeUtcTicks)
        {
            return LocalTestServerStopReason.ParentProcessReused;
        }

        if (!string.Equals(parentProcess.ProcessName, "Unity", StringComparison.OrdinalIgnoreCase))
        {
            return LocalTestServerStopReason.ParentIsNotUnity;
        }

        string json;
        try
        {
            json = readControlFile(configuration.ControlFilePath);
        }
        catch
        {
            return LocalTestServerStopReason.ControlFileUnavailable;
        }

        LocalTestServerControlState? controlState;
        try
        {
            controlState = JsonSerializer.Deserialize<LocalTestServerControlState>(json);
        }
        catch (JsonException)
        {
            return LocalTestServerStopReason.ControlFileInvalid;
        }

        if (controlState == null || controlState.Version != ControlVersion)
        {
            return LocalTestServerStopReason.ControlFileInvalid;
        }

        if (controlState.ParentProcessId != configuration.ParentProcessId ||
            controlState.ParentStartTimeUtcTicks != configuration.ParentStartTimeUtcTicks)
        {
            return LocalTestServerStopReason.ControlOwnerMismatch;
        }

        if (!string.Equals(controlState.Token, configuration.Token, StringComparison.Ordinal))
        {
            return LocalTestServerStopReason.ControlTokenMismatch;
        }

        return string.Equals(controlState.RequestedState, "run", StringComparison.Ordinal)
            ? LocalTestServerStopReason.None
            : LocalTestServerStopReason.StopRequested;
    }

    private async Task MonitorAsync()
    {
        using PeriodicTimer timer = new(TimeSpan.FromMilliseconds(MonitorIntervalMilliseconds));
        try
        {
            while (await timer.WaitForNextTickAsync(_monitorCancellation.Token))
            {
                LocalTestServerStopReason stopReason = Evaluate(
                    _configuration,
                    CaptureParentProcess,
                    File.ReadAllText);
                if (stopReason == LocalTestServerStopReason.None)
                {
                    continue;
                }

                Console.WriteLine($"[LocalTestServer] 请求优雅停服：{Describe(stopReason)}");
                ShutdownToken.Instance.Cancel();
                return;
            }
        }
        catch (OperationCanceledException) when (_monitorCancellation.IsCancellationRequested)
        {
            // DisposeAsync 正常结束。
        }
    }

    private static LocalTestServerParentProcess? CaptureParentProcess(int processId)
    {
        using Process process = Process.GetProcessById(processId);
        if (process.HasExited)
        {
            return null;
        }

        return new LocalTestServerParentProcess(
            process.ProcessName,
            process.StartTime.ToUniversalTime().Ticks);
    }

    private static string? ReadEnvironmentValue(
        IReadOnlyDictionary<string, string?> environment,
        string name)
    {
        return environment.TryGetValue(name, out string? value) ? value : null;
    }

    private static string Describe(LocalTestServerStopReason stopReason)
    {
        return stopReason switch
        {
            LocalTestServerStopReason.ParentProcessMissing => "Unity 父进程已退出",
            LocalTestServerStopReason.ParentProcessReused => "Unity PID 已被其他进程复用",
            LocalTestServerStopReason.ParentIsNotUnity => "父进程不是 Unity Editor",
            LocalTestServerStopReason.ControlFileUnavailable => "控制文件不存在或不可读",
            LocalTestServerStopReason.ControlFileInvalid => "控制文件格式无效",
            LocalTestServerStopReason.ControlOwnerMismatch => "控制文件的 Unity 所有权不匹配",
            LocalTestServerStopReason.ControlTokenMismatch => "控制文件 token 不匹配",
            LocalTestServerStopReason.StopRequested => "Unity Editor 已请求停止",
            _ => "未知原因"
        };
    }
}

internal sealed record LocalTestServerConfiguration(
    int ParentProcessId,
    long ParentStartTimeUtcTicks,
    string Token,
    string ControlFilePath);

internal sealed record LocalTestServerParentProcess(string ProcessName, long StartTimeUtcTicks);

internal sealed class LocalTestServerControlState
{
    public int Version { get; set; }
    public int ParentProcessId { get; set; }
    public long ParentStartTimeUtcTicks { get; set; }
    public string Token { get; set; } = string.Empty;
    public string RequestedState { get; set; } = string.Empty;
    public long UpdatedAtUtcTicks { get; set; }
}

internal enum LocalTestServerStopReason
{
    None,
    ParentProcessMissing,
    ParentProcessReused,
    ParentIsNotUnity,
    ControlFileUnavailable,
    ControlFileInvalid,
    ControlOwnerMismatch,
    ControlTokenMismatch,
    StopRequested
}
