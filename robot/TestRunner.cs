using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Robot
{
    /// <summary>
    /// 测试运行器。加载场景、顺序执行、收集结果。
    /// </summary>
    public class TestRunner
    {
        public string Host { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 9000;

        private readonly List<TestScenario> _scenarios = new();

        public int ScenarioCount => _scenarios.Count;

        public void AddScenario(TestScenario scenario)
        {
            _scenarios.Add(scenario);
        }

        /// <summary>运行所有已注册的测试场景</summary>
        public async Task<int> RunAll()
        {
            var passed = 0;
            var failed = 0;

            Console.WriteLine($"=== Robot 测试开始 ({_scenarios.Count} 个场景) ===");
            Console.WriteLine();

            foreach (var scenario in _scenarios)
            {
                Console.WriteLine($"--- {scenario.Name} ---");

                using var session = new RobotSession();

                try
                {
                    await session.ConnectAsync(Host, Port);
                    scenario.Session = session;
                    await scenario.Run();

                    Console.WriteLine($"  PASS: {scenario.Name}");
                    passed++;
                }
                catch (TestFailedException ex)
                {
                    Console.WriteLine(ex.Message);
                    Console.WriteLine($"  FAIL: {scenario.Name}");
                    failed++;
                }
                catch (TimeoutException ex)
                {
                    Console.WriteLine($"  FAIL: {scenario.Name} — 超时: {ex.Message}");
                    failed++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  FAIL: {scenario.Name} — 异常: {ex}");
                    failed++;
                }
                finally
                {
                    session.Disconnect();
                }

                Console.WriteLine();
            }

            Console.WriteLine($"=== 完成: {passed} 通过, {failed} 失败 ===");
            return failed > 0 ? 1 : 0;
        }
    }
}
