using System;
using System.Collections.Generic;
using Game.Core;
using Shared.Core.Log;

#nullable enable
namespace Shared.Core.ECS
{
    public class Component
    {
        private static readonly ILog Log = LogManager.GetLogger<Component>();

        private Entity? _parent;
        public Entity? Parent => _parent;

        public void SetParent(Entity? parent)
        {
            if (parent != null)
            {
                ComponentOfAttribute? componentOfAttr =
                    (ComponentOfAttribute?)Attribute.GetCustomAttribute(this.GetType(), typeof(ComponentOfAttribute));
                if (componentOfAttr == null)
                {
                    Log.Error(
                        $"ComponentOf约束违反: 组件 {this.GetType().Name} 没有声明了 [ComponentOf()]，但实际挂载到 {parent.GetType().Name}");
                }
                else if (componentOfAttr.type != null)
                {
                    if (!componentOfAttr.type.IsInstanceOfType(parent))
                    {
                        Log.Error(
                            $"ComponentOf约束违反: 组件 {this.GetType().Name} 声明了 [ComponentOf({componentOfAttr.type.Name})]，但实际挂载到 {parent.GetType().Name}");
                    }
                }
            }

            _parent = parent;
        }
    }
}