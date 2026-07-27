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
        private Dictionary<string, Component> _components = new();


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
            if (_components.ContainsKey(type.Name))
            {
                throw new Exception($"重复添加组件: {type.FullName}");
            }

            var component = new T();
            component.SetParent(this);
            _components.Add(type.Name, component);
            return component;
        }

        public void AddComponent<T>(T component) where T : Component, new()
        {
            Type type = typeof(T);
            if (_components.ContainsKey(type.Name))
            {
                throw new Exception($"重复添加组件: {type.FullName}");
            }

            component.SetParent(this);
            _components.Add(type.Name, component);
        }

        /// <summary>获取必需组件；缺失表示实体状态违反约束。</summary>
        public T GetComponent<T>() where T : Component
        {
            Type type = typeof(T);
            if (_components.TryGetValue(type.Name, out var component) && component is T typedComponent)
            {
                return typedComponent;
            }

            throw new InvalidOperationException(
                $"{GetType().Name} Id:{Id} 缺少必需组件 {type.Name}");
        }

        /// <summary>尝试获取可选组件。</summary>
        public T? TryGetComponent<T>() where T : Component
        {
            return _components.GetValueOrDefault(typeof(T).Name) as T;
        }

        /// <summary>按类型获取组件（非泛型，用于反射调用）</summary>
        public Component? GetComponent(Type type)
        {
            return _components.GetValueOrDefault(type.Name);
        }

        public void RemoveComponent(Component component)
        {
            component.SetParent(null);
            _components.Remove(component.GetType().Name);
        }

        public bool HasComponent<T>() where T : Component
        {
            return _components.ContainsKey(typeof(T).Name);
        }

        public IEnumerable<Component> GetAllComponents()
        {
            return _components.Values;
        }
    }
}
