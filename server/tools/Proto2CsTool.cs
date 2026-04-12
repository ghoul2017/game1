namespace Tools;

public class Proto2CsTool : ITool
{
    public string Name => "proto2cs";
    public string Desc => "Protobuf(.proto) 转 C# 代码 (待实现)";

    public Task<int> Run(string[] args)
    {
        Console.WriteLine("proto2cs 尚未实现，敬请期待");
        return Task.FromResult(0);
    }
}
