using System;
using System.Collections.Generic;
using Game.Core;
using Shared.Common.Protos;

#nullable enable
namespace Shared.Core.ECS
{
    public class Entity
    {
        private Entity? _parent;
        public Entity? Parent => _parent;
        private long _id;
        public long Id => _id;


        private Dictionary<Type, Component> _components = new();

        public T AddComponent<T>() where T : Component, new()
        {
            Type type = typeof(T);
            if (_components.ContainsKey(type))
            {
                throw new Exception($"重复添加组件: {type.FullName}");
            }

            var component = new T
            {
                _parent = this,
                _id = IdGenerator.Instance.NextId()
            };
            _components.Add(type, component);
            return component;
        }

        public T GetComponent<T>() where T : Component
        {
            Type type = typeof(T);
            return (T)_components[type];
        }

        public void RemoveComponent(Component component)
        {
            component._parent = null;
            _components.Remove(component.GetType());
        }
    }
}