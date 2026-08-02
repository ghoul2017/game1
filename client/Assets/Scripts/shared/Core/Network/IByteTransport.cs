using System;
using Shared.Common.Enums;

#nullable enable
namespace Shared.Core.Network
{
    /// <summary>
    /// 区分必须按序发送的消息和只需保留最新值的状态快照。
    /// </summary>
    public enum TransportSendMode
    {
        Reliable = 0,
        LatestSnapshot = 1
    }

    /// <summary>
    /// 为 TcpSession 提供平台无关的二进制双向传输，不承载封包或业务语义。
    /// </summary>
    public interface IByteTransport : IDisposable
    {
        bool IsConnected { get; }

        string RemoteIp { get; }

        long BytesSent { get; }

        long BytesReceivedCount { get; }

        int QueuedBytes { get; }

        int PeakQueuedBytes { get; }

        long ReplacedSnapshotCount { get; }

        event Action<byte[]>? BytesReceived;

        event Action<LogoutReason>? Disconnected;

        void StartReceive();

        void Send(byte[] bytes, TransportSendMode mode);

        void Disconnect(LogoutReason reason);
    }
}
