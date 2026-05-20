namespace Tools;

/// <summary>
/// 工具接口。每个子命令实现此接口，自动注册到命令路由。
/// </summary>
public interface ITool
{
    /// <summary>命令名（如 xlsx2csv），用作 CLI 第一个参数</summary>
    string Name { get; }

    /// <summary>简短描述，--list 时显示</summary>
    string Desc { get; }

    /// <summary>
    /// 执行工具逻辑。
    /// </summary>
    /// <param name="args">工具专属参数（已去掉命令名本身）</param>
    /// <returns>退出码，0 表示成功</returns>
    Task<int> Run(string[] args);
}
