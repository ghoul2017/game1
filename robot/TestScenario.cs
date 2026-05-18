using System;
using System.Threading.Tasks;

namespace Robot
{
    /// <summary>
    /// 测试场景基类。子类实现 Run 方法定义测试步骤。
    /// </summary>
    public abstract class TestScenario
    {
        /// <summary>场景名称</summary>
        public abstract string Name { get; }

        /// <summary>Robot 会话（由 TestRunner 在运行前注入）</summary>
        public RobotSession Session { get; set; } = null!;

        /// <summary>测试场景执行入口</summary>
        public abstract Task Run();

        /// <summary>日志输出</summary>
        protected void Log(string msg)
        {
            Console.WriteLine($"  [{Name}] {msg}");
        }
    }
}
