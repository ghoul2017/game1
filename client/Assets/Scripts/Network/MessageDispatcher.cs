using System;
using System.Collections.Generic;
using System.IO;
using Shared.Core.Network;
using Shared.Common.Protos;
using ProtoBuf;

namespace Client.Network
{
    /// <summary>
    /// 客户端消息路由分发器
    /// 按 MsgId 注册 Handler 回调，收到服务端消息后自动反序列化并路由
    /// </summary>
    public class MessageDispatcher
    {
        private readonly Dictionary<ushort, Action<byte[]>> _handlers = new Dictionary<ushort, Action<byte[]>>();

        /// <summary>
        /// 注册强类型消息处理器（自动反序列化）
        /// </summary>
        public void Register<T>(ushort msgId, Action<T> handler) where T : class, IMessage, new()
        {
            _handlers[msgId] = payload =>
            {
                T msg;
                using (var ms = new MemoryStream(payload))
                    msg = Serializer.Deserialize<T>(ms);
                handler(msg);
            };
        }

        /// <summary>
        /// 注册原始字节处理器（手动反序列化）
        /// </summary>
        public void RegisterRaw(ushort msgId, Action<byte[]> handler)
        {
            _handlers[msgId] = handler;
        }

        /// <summary>
        /// 取消注册
        /// </summary>
        public void Unregister(ushort msgId)
        {
            _handlers.Remove(msgId);
        }

        /// <summary>
        /// 分发消息：反序列化并调用注册的 Handler
        /// </summary>
        public void Dispatch(ushort msgId, byte[] payload)
        {
            if (_handlers.TryGetValue(msgId, out var handler))
            {
                try
                {
                    handler(payload);
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError($"[MessageDispatcher] 处理消息 MsgId={msgId} 异常: {e}");
                }
            }
        }

        /// <summary>
        /// 是否已注册指定消息的处理器
        /// </summary>
        public bool HasHandler(ushort msgId)
        {
            return _handlers.ContainsKey(msgId);
        }

        /// <summary>
        /// 清除所有注册
        /// </summary>
        public void Clear()
        {
            _handlers.Clear();
        }
    }
}
