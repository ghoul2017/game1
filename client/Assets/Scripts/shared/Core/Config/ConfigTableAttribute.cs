namespace Shared.Core.Config
{
    using System;

    /// <summary>
    /// 标注配置表类，ConfigManager 自动扫描并加载对应的 CSV 文件。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class ConfigTableAttribute : Attribute
    {
        /// <summary>CSV 文件名（如 "Skill.csv"）</summary>
        public string FileName { get; }

        /// <summary>主键属性名，默认 "Id"</summary>
        public string KeyName { get; set; } = "Id";

        public ConfigTableAttribute(string fileName)
        {
            FileName = fileName;
        }
    }
}