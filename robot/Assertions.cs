using System;

namespace Robot
{
    /// <summary>
    /// 断言工具。测试失败时抛出 TestFailedException。
    /// </summary>
    public static class Assertions
    {
        public static void Equal<T>(T expected, T actual, string message)
        {
            if (!Equals(expected, actual))
                throw new TestFailedException($"{message}: 期望={expected}, 实际={actual}");
        }

        public static void True(bool condition, string message)
        {
            if (!condition)
                throw new TestFailedException($"{message}: 期望=true, 实际=false");
        }

        public static void False(bool condition, string message)
        {
            if (condition)
                throw new TestFailedException($"{message}: 期望=false, 实际=true");
        }

        public static void NotNull(object? obj, string message)
        {
            if (obj == null)
                throw new TestFailedException($"{message}: 期望非null");
        }

        public static void Null(object? obj, string message)
        {
            if (obj != null)
                throw new TestFailedException($"{message}: 期望null");
        }

        public static void Fail(string message)
        {
            throw new TestFailedException(message);
        }
    }

    public class TestFailedException : Exception
    {
        public TestFailedException(string message) : base($"  FAIL: {message}")
        {
        }
    }
}
