using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Game.Components.Player;
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
        var handledMessageNames = new Dictionary<string, MethodInfo>();

        foreach (var method in assemblies.SelectMany(GetLoadableTypes)
                     .SelectMany(t => t.GetMethods(BindingFlags.Static | BindingFlags.Public)))
        {
            var attr = method.GetCustomAttribute<MessageHandlerAttribute>();
            if (attr == null)
                continue;

            handlerCount++;
            if (!typeof(AMethodHandler).IsAssignableFrom(attr.type))
            {
                errors.Add($"{domainName} Handler 类型必须继承 AMethodHandler: {FormatMethod(method)}, handlerType={attr.type.FullName}");
                continue;
            }

            ValidateHandlerSignature(domainName, method, attr.type, messageTypeToName, errors);

            var parameters = method.GetParameters();
            if (parameters.Length < 2 || !typeof(IMessage).IsAssignableFrom(parameters[1].ParameterType))
                continue;

            if (!messageTypeToName.TryGetValue(parameters[1].ParameterType, out var messageName))
                continue;

            if (handledMessageNames.TryGetValue(messageName, out var existingHandler))
            {
                errors.Add($"{domainName} 重复注册 Handler: {messageName}, {FormatMethod(existingHandler)}, {FormatMethod(method)}");
                continue;
            }

            handledMessageNames[messageName] = method;
        }

        return handlerCount;
    }

    private static void ValidateHandlerSignature(
        string domainName,
        MethodInfo method,
        Type handlerType,
        Dictionary<Type, string> messageTypeToName,
        List<string> errors)
    {
        if (handlerType == typeof(RpcMethodHandler))
        {
            ValidateRpcSignature(domainName, method, typeof(TcpSession), messageTypeToName, errors);
            return;
        }

        if (handlerType == typeof(NtfMethodHandler))
        {
            ValidateNtfSignature(domainName, method, typeof(TcpSession), messageTypeToName, errors);
            return;
        }

        if (handlerType == typeof(PlayerRpcMethodHandler))
        {
            ValidateRpcSignature(domainName, method, typeof(PlayerEntity), messageTypeToName, errors);
            return;
        }

        if (handlerType == typeof(PlayerNtfMethodHandler))
        {
            ValidateNtfSignature(domainName, method, typeof(PlayerEntity), messageTypeToName, errors);
            return;
        }

        errors.Add($"{domainName} 未知 Handler 适配器: {FormatMethod(method)}, handlerType={handlerType.FullName}");
    }

    private static void ValidateRpcSignature(
        string domainName,
        MethodInfo method,
        Type firstParamType,
        Dictionary<Type, string> messageTypeToName,
        List<string> errors)
    {
        var parameters = method.GetParameters();
        if (parameters.Length != 3)
        {
            errors.Add($"{domainName} RPC Handler 参数必须为3个: {FormatMethod(method)}");
            return;
        }

        if (parameters[0].ParameterType != firstParamType)
            errors.Add($"{domainName} RPC Handler 参数1必须是 {firstParamType.Name}: {FormatMethod(method)}");

        ValidateMessageParameter(domainName, method, parameters[1].ParameterType, "请求", messageTypeToName, errors);
        ValidateMessageParameter(domainName, method, parameters[2].ParameterType, "响应", messageTypeToName, errors);

        if (method.ReturnType != typeof(Task<IMessage>))
            errors.Add($"{domainName} RPC Handler 返回值必须是 Task<IMessage>: {FormatMethod(method)}, returnType={method.ReturnType.FullName}");
    }

    private static void ValidateNtfSignature(
        string domainName,
        MethodInfo method,
        Type firstParamType,
        Dictionary<Type, string> messageTypeToName,
        List<string> errors)
    {
        var parameters = method.GetParameters();
        if (parameters.Length != 2)
        {
            errors.Add($"{domainName} NTF Handler 参数必须为2个: {FormatMethod(method)}");
            return;
        }

        if (parameters[0].ParameterType != firstParamType)
            errors.Add($"{domainName} NTF Handler 参数1必须是 {firstParamType.Name}: {FormatMethod(method)}");

        ValidateMessageParameter(domainName, method, parameters[1].ParameterType, "通知", messageTypeToName, errors);

        if (method.ReturnType != typeof(void) && method.ReturnType != typeof(Task))
            errors.Add($"{domainName} NTF Handler 返回值必须是 void 或 Task: {FormatMethod(method)}, returnType={method.ReturnType.FullName}");
    }

    private static void ValidateMessageParameter(
        string domainName,
        MethodInfo method,
        Type type,
        string label,
        Dictionary<Type, string> messageTypeToName,
        List<string> errors)
    {
        if (!typeof(IMessage).IsAssignableFrom(type))
        {
            errors.Add($"{domainName} Handler {label}参数必须实现 IMessage: {FormatMethod(method)}, type={type.FullName}");
            return;
        }

        if (!messageTypeToName.ContainsKey(type))
            errors.Add($"{domainName} Handler {label}类型未注册消息名: {FormatMethod(method)}, type={type.FullName}");
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

    private static string FormatMethod(MethodInfo method)
    {
        return $"{method.DeclaringType?.FullName}.{method.Name}";
    }
}
