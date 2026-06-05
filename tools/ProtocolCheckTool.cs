using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Shared.Core.Network;

namespace Tools;

public class ProtocolCheckTool : ITool
{
    private static readonly Regex MessageNamePattern = new("^[A-Za-z][A-Za-z0-9]*\\.[A-Za-z][A-Za-z0-9]*$",
        RegexOptions.Compiled);

    public string Name => "check-protocol";
    public string Desc => "检查协议消息名和 Handler 注册一致性";

    public Task<int> Run(string[] args)
    {
        var sharedAssembly = typeof(IMessage).Assembly;
        var clientLogicAssembly = typeof(Client.Logic.Context).Assembly;
        var serverAssembly = typeof(Game.AppStart).Assembly;
        var allAssemblies = new[] { sharedAssembly, clientLogicAssembly, serverAssembly }.Distinct().ToArray();
        var clientAssemblies = new[] { sharedAssembly, clientLogicAssembly }.Distinct().ToArray();
        var serverAssemblies = new[] { sharedAssembly, serverAssembly }.Distinct().ToArray();

        var errors = new List<string>();
        var messageNameToType = new Dictionary<string, Type>();
        var messageTypeToName = new Dictionary<Type, string>();

        foreach (var type in allAssemblies.SelectMany(GetLoadableTypes))
        {
            if (type.IsAbstract || type.IsInterface || !typeof(IMessage).IsAssignableFrom(type))
                continue;

            var attr = type.GetCustomAttribute<MessageAttribute>();
            if (attr == null)
            {
                errors.Add($"消息类型缺少 [Message]: {type.FullName}");
                continue;
            }

            if (string.IsNullOrWhiteSpace(attr.Name))
            {
                errors.Add($"消息名不能为空: {type.FullName}");
                continue;
            }

            if (!MessageNamePattern.IsMatch(attr.Name))
                errors.Add($"消息名格式必须是 模块.消息名 且只包含字母数字: {attr.Name}, {type.FullName}");

            var nameByteCount = Encoding.UTF8.GetByteCount(attr.Name);
            if (nameByteCount > PacketCodec.MaxMessageNameBytes)
                errors.Add($"消息名超过 {PacketCodec.MaxMessageNameBytes} 字节: {attr.Name}, bytes={nameByteCount}");

            if (messageNameToType.TryGetValue(attr.Name, out var existingType))
            {
                errors.Add($"消息名重复: {attr.Name}, {existingType.FullName}, {type.FullName}");
                continue;
            }

            messageNameToType[attr.Name] = type;
            messageTypeToName[type] = attr.Name;
        }

        var handlerCount = CheckHandlerDomain("客户端", clientAssemblies, messageTypeToName, errors)
                           + CheckHandlerDomain("服务端", serverAssemblies, messageTypeToName, errors);

        CheckLegacyProtocolResidue(errors);

        if (errors.Count > 0)
        {
            Console.WriteLine("协议一致性检查失败:");
            foreach (var error in errors)
                Console.WriteLine($"  - {error}");
            return Task.FromResult(1);
        }

        Console.WriteLine($"协议一致性检查通过: 消息 {messageTypeToName.Count} 个，Handler {handlerCount} 个");
        return Task.FromResult(0);
    }

    private static int CheckHandlerDomain(
        string domainName,
        Assembly[] assemblies,
        Dictionary<Type, string> messageTypeToName,
        List<string> errors)
    {
        var handlerCount = 0;
        var handledMessageNames = new Dictionary<string, string>();
        var types = assemblies.Distinct().SelectMany(GetLoadableTypes).ToArray();

        foreach (var type in types.Where(IsClassHandlerType))
        {
            handlerCount++;
            if (!TryGetClassHandlerMessageType(domainName, type, errors, out var messageType) || messageType == null)
                continue;

            ValidateClassHandlerDefinition(domainName, type, messageTypeToName, errors);
            ValidateMessageParameter(domainName, FormatType(type), messageType, "消息", messageTypeToName, errors);
            RegisterHandledMessage(
                domainName,
                handledMessageNames,
                messageTypeToName,
                messageType,
                FormatType(type),
                errors);
        }

        return handlerCount;
    }

    private static void ValidateClassHandlerDefinition(
        string domainName,
        Type type,
        Dictionary<Type, string> messageTypeToName,
        List<string> errors)
    {
        if (TryGetGenericBase(type, typeof(RpcMessageHandler<,>), out var rpcBase)
            || TryGetGenericBase(type, typeof(PlayerRpcMessageHandler<,>), out rpcBase))
        {
            var genericArguments = rpcBase!.GetGenericArguments();
            ValidateMessageParameter(domainName, FormatType(type), genericArguments[0], "请求", messageTypeToName, errors);
            ValidateMessageParameter(domainName, FormatType(type), genericArguments[1], "响应", messageTypeToName, errors);
            return;
        }

        if (TryGetGenericBase(type, typeof(NtfMessageHandler<>), out var ntfBase)
            || TryGetGenericBase(type, typeof(PlayerNtfMessageHandler<>), out ntfBase))
        {
            var genericArguments = ntfBase!.GetGenericArguments();
            ValidateMessageParameter(domainName, FormatType(type), genericArguments[0], "通知", messageTypeToName, errors);
            return;
        }
    }

    private static bool TryGetGenericBase(Type type, Type genericTypeDefinition, out Type? genericBase)
    {
        for (var current = type; current != null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == genericTypeDefinition)
            {
                genericBase = current;
                return true;
            }
        }

        genericBase = null;
        return false;
    }

    private static bool IsClassHandlerType(Type type)
    {
        return !type.IsAbstract
               && !type.IsInterface
               && !type.ContainsGenericParameters
               && typeof(IClassMessageHandler).IsAssignableFrom(type);
    }

    private static bool TryGetClassHandlerMessageType(
        string domainName,
        Type type,
        List<string> errors,
        out Type? messageType)
    {
        messageType = null;
        if (type.GetConstructor(Type.EmptyTypes) == null)
        {
            errors.Add($"{domainName} 继承式 Handler 必须有无参构造: {FormatType(type)}");
            return false;
        }

        try
        {
            var handler = (IClassMessageHandler)Activator.CreateInstance(type)!;
            messageType = handler.MessageType;
            return true;
        }
        catch (Exception ex)
        {
            errors.Add($"{domainName} 继承式 Handler 创建失败: {FormatType(type)}, error={ex.Message}");
            return false;
        }
    }

    private static void RegisterHandledMessage(
        string domainName,
        Dictionary<string, string> handledMessageNames,
        Dictionary<Type, string> messageTypeToName,
        Type messageType,
        string source,
        List<string> errors)
    {
        if (!messageTypeToName.TryGetValue(messageType, out var messageName))
            return;

        if (handledMessageNames.TryGetValue(messageName, out var existingHandler))
        {
            errors.Add($"{domainName} 重复注册 Handler: {messageName}, {existingHandler}, {source}");
            return;
        }

        handledMessageNames[messageName] = source;
    }

    private static void ValidateMessageParameter(
        string domainName,
        string source,
        Type type,
        string label,
        Dictionary<Type, string> messageTypeToName,
        List<string> errors)
    {
        if (!typeof(IMessage).IsAssignableFrom(type))
        {
            errors.Add($"{domainName} Handler {label}类型必须实现 IMessage: {source}, type={type.FullName}");
            return;
        }

        if (!messageTypeToName.ContainsKey(type))
            errors.Add($"{domainName} Handler {label}类型未注册消息名: {source}, type={type.FullName}");
    }

    private static void CheckLegacyProtocolResidue(List<string> errors)
    {
        var root = Directory.GetCurrentDirectory();
        var searchDirs = new[] { "client/Assets/Scripts", "server", "robot", "tools", "shared", "client-logic" };
        var patterns = new[] { "MsgId" + "Const", "." + "MsgId", "Req" + "MsgId" };

        foreach (var dir in searchDirs)
        {
            var fullDir = Path.Combine(root, dir);
            if (!Directory.Exists(fullDir))
                continue;

            foreach (var file in Directory.GetFiles(fullDir, "*.cs", SearchOption.AllDirectories))
            {
                var text = File.ReadAllText(file);
                foreach (var pattern in patterns)
                {
                    if (!text.Contains(pattern, StringComparison.Ordinal))
                        continue;

                    var relativePath = Path.GetRelativePath(root, file);
                    errors.Add($"发现旧协议残留 {pattern}: {relativePath}");
                }
            }
        }
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        if (assembly.IsDynamic)
            yield break;

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

    private static string FormatType(Type type)
    {
        return type.FullName ?? type.Name;
    }
}
