using System.Collections.Generic;
using Shared.Core.Config;
using Shared.Core.Entities;
using Shared.Core.ECS;

namespace Game.Components.User;

[ComponentOf(typeof(Module))]
public class UserComponent : Component
{
    public const int MaxRolesPerUser = 4;
    
    public Dictionary<long,User> Users = new();
    public Dictionary<string,User> Usernames = new();
}