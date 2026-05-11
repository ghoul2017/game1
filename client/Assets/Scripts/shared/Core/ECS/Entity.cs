using System;
using System.Collections.Generic;
using Shared.Core;
using Shared.Common.Protos;

#nullable enable
namespace Shared.Core.ECS
{
    public class Entity
    {
        private long _id;
        public long Id => _id;
        private Dictionary<Type, Component> _components = new();


        public static T CreateEntity<T>() where T : Entity, new()
        {
            var entity = new T();
            entity._id = IdGenerator.Instance.NextId();
            return entity;
        }

        public static T CreateEntityById<T>(long id) where T : Entity, new()
        {
            var entity = new T();
            entity._id = id;
            return entity;
        }

        public T AddComponent<T>() where T : Component, new()
        {
            Type type = typeof(T);
            if (_components.ContainsKey(type))
            {
                throw new Exception($"重复添加组件: {type.FullName}");
            }

            var component = new T();
            component.SetParent(this);
            _components.Add(type, component);
            return component;
        }
        public void AddComponent<T>(T component) where T : Component, new()
        {
            Type type = typeof(T);
            if (_components.ContainsKey(type))
            {
                throw new Exception($"重复添加组件: {type.FullName}");
            }

            component.SetParent(this);
            _components.Add(type, component);
        }

        public T GetComponent<T>() where T : Component
        {
            Type type = typeof(T);
            return (T)_components[type];
        }

        /// <summary>按类型获取组件（非泛型，用于反射调用）</summary>
        public Component GetComponent(Type type)
        {
            return _components[type];
        }

        public void RemoveComponent(Component component)
        {
            component.SetParent(null);
            _components.Remove(component.GetType());
        }

        public bool TryGetComponent<T>(out T? component) where T : Component
        {
            if (_components.TryGetValue(typeof(T), out var c))
            {
                component = (T)c;
                return true;
            }
            component = null;
            return false;
        }

        public bool HasComponent<T>() where T : Component
        {
            return _components.ContainsKey(typeof(T));
        }

        public IEnumerable<Component> GetAllComponents()
        {
            return _components.Values;
        }
    }
}