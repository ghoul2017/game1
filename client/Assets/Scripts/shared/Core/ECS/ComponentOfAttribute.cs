using System;

#nullable enable
namespace Shared.Core.ECS
{
    /// <summary>
    /// 组件类父级实体类型约束
    /// 父级实体类型唯一的 标记指定父级实体类型[ComponentOf(typeof(parentType)]
    /// 不唯一则标记[ComponentOf]
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class ComponentOfAttribute : Attribute
    {
        public readonly Type? type;

        public ComponentOfAttribute(Type? type = null)
        {
            this.type = type;
        }
    }
}