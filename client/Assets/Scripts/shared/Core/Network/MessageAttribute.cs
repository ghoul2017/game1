using System;

namespace Shared.Core.Network
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class MessageAttribute : Attribute
    {
        public string Name { get; }

        public MessageAttribute(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("消息名不能为空", nameof(name));
            Name = name;
        }
    }
}
