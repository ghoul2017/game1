using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

namespace Shared.Core.Network
{
    public static class MessageRegistry
    {
        private static readonly object InitLock = new();
        private static bool _initialized;
        private static readonly Dictionary<string, Type> NameToType = new();
        private static readonly Dictionary<Type, string> TypeToName = new();

        public static void EnsureInitialized()
        {
            if (_initialized)
                return;

            lock (InitLock)
            {
                if (_initialized)
                    return;

                RegisterAll();
                _initialized = true;
            }
        }

        public static string GetName(Type messageType)
        {
            EnsureInitialized();
            if (TypeToName.TryGetValue(messageType, out var name))
                return name;
            throw new InvalidOperationException($"消息类型未注册: {messageType.FullName}");
        }

        public static Type GetType(string messageName)
        {
            EnsureInitialized();
            if (NameToType.TryGetValue(messageName, out var type))
                return type;
            throw new InvalidOperationException($"消息名未注册: {messageName}");
        }

        public static bool TryGetType(string messageName, [NotNullWhen(true)] out Type? messageType)
        {
            EnsureInitialized();
            return NameToType.TryGetValue(messageName, out messageType);
        }

        private static void RegisterAll()
        {
            foreach (var type in GetLoadableTypes())
            {
                if (type.IsAbstract || !typeof(IMessage).IsAssignableFrom(type))
                    continue;

                var attr = type.GetCustomAttribute<MessageAttribute>();
                if (attr == null)
                    throw new InvalidOperationException($"消息类型缺少 MessageAttribute: {type.FullName}");

                if (NameToType.TryGetValue(attr.Name, out var existingType))
                    throw new InvalidOperationException($"消息名重复: {attr.Name}, {existingType.FullName}, {type.FullName}");

                NameToType[attr.Name] = type;
                TypeToName[type] = attr.Name;
            }
        }

        private static IEnumerable<Type> GetLoadableTypes()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types.Where(t => t != null).Cast<Type>().ToArray();
                }

                foreach (var type in types)
                    yield return type;
            }
        }
    }
}
