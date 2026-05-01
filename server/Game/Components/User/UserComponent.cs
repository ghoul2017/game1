using Game.Config;
using Shared.Core.ECS;

namespace Game.Components.User;

[ComponentOf(typeof(Server))]
public class UserComponent : Component
{
    public List<User> Users = new();
}