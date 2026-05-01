namespace Game.Core.Network;

/// <summary>
/// 标记扩展方法为消息处理器。
/// 使用方式：
/// [MessageHandler(typeof(MessageHandler&lt;,&gt;))] - RPC 风格（有响应）
/// [MessageHandler(typeof(MessageNtfHandler&lt;&gt;))] - 通知风格（无响应）
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class MessageHandlerAttribute : Attribute
{
    /// <summary>Handler 类型，用于区分 RPC/通知风格</summary>
    public readonly Type type;

    public MessageHandlerAttribute(Type type)
    {
        this.type = type;
    }
}
