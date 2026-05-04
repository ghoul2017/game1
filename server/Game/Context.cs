using Game.Core;
using Game.Entities;

namespace Game;

public static class Context
{
    public static Module Server =new();
    public static  AppConfig Config = null!;
    /// <summary>游戏主线程同步上下文</summary>
    public static GameSynchronizationContext GameCtx = new GameSynchronizationContext();
    
}