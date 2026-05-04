using System;
using System.Linq;
using System.Threading.Tasks;
using Game.Core;
using Game.Core.Network;
using Game.Core.Entity;
using Game.Database;
using Game.Game.Player;
using Game.Network;
using Shared.Common.Protos;
using Shared.Core.Log;
using Shared.Core.Network;

namespace Game.Components.User;

/// <summary>
/// 用户组件系统 — 扩展方法风格 Handler。
/// </summary>
public static class UserComponentSystem
{
    private static readonly ILog Log = LogManager.GetLogger<UserComponent>();
    
}