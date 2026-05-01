using Game.Config;
using Game.Entities;
using Shared.Core.ECS;

namespace Game.Components.User;

[ComponentOf(typeof(Module))]
public class UserComponent : Component
{
    public List<User> Users = new();
}