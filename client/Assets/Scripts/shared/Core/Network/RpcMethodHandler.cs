using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Shared.Core;
using ProtoBuf;
using Shared.Core.Network;

namespace Shared.Core.Network
{
    /// <summary>
    /// RPC 风格消息处理器适配器。
    /// 将扩展方法风格 Handler 适配到 IMessageHandler 接口。
    /// </summary>
    public class RpcMethodHandler : AMethodHandler
    {
        private MethodInfo _method = null!;
        private Type _reqType = null!;
        private Type _respType = null!;

        public override void Init(MethodInfo method)
        {
            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length != 3)
            {
                throw new Exception($"MethodHandler Handler与Method的参数必须一致{method.Name}");
            }

            var sessionType = parameters[0].ParameterType;
            if (sessionType != typeof(TcpSession))
            {
                throw new Exception($"MethodHandler 参数一必须是TcpSession：{method.Name}");
            }

            var reqType = parameters[1].ParameterType;
            var respType = parameters[2].ParameterType;
            _method = method;
            _reqType = reqType;
            _respType = respType;
        }

        public override async Task<IMessage?> HandleAsync(TcpSession session, byte[] payload)
        {
            // 反序列化请求
            object req;
            using (var ms = new MemoryStream(payload))
                req = Serializer.NonGeneric.Deserialize(_reqType, ms);

            // 创建响应对象
            var res = System.Activator.CreateInstance(_respType);

            // 调用扩展方法
            var task = (Task<IMessage?>)_method.Invoke(null, new object[] { session, req, res! })!;
            return await task;
        }
    }
}