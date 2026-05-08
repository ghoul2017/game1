using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using ProtoBuf;
using Shared.Core.Network;

namespace Client.Network
{
    /// <summary>
    /// 通知风格消息处理器适配器（无响应）。
    /// 将扩展方法风格 Handler 适配到 IMessageHandler 接口。
    /// </summary>
    public class NtfMethodHandler : AMethodHandler
    {
        private MethodInfo _method = null!;
        private Type _ntfType = null!;

        public override void Init(MethodInfo method)
        {
            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length != 2)
            {
                throw new Exception($"MethodHandler Handler与Method的参数必须一致{method.Name}");
            }

            var sessionType = parameters[0].ParameterType;
            if (sessionType != typeof(TcpSession))
            {
                throw new Exception($"MethodHandler 参数一必须是TcpSession：{method.Name}");
            }

            var ntfType = parameters[1].ParameterType;
            _method = method;
            _ntfType = ntfType;
        }

        public override async Task<IMessage?> HandleAsync(TcpSession session, byte[] payload)
        {
            // 反序列化请求
            object ntf;
            using (var ms = new MemoryStream(payload))
                ntf = Serializer.NonGeneric.Deserialize(_ntfType, ms);

            // 调用扩展方法
            var task = (Task)_method.Invoke(null, new object[] { session, ntf })!;
            await task;

            // 无响应
            return null;
        }
    }
}