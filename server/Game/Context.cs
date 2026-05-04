using Game.Core;
using Game.Entities;

namespace Game;

public static class Context
{
    public static Module Server = new();
    public static AppConfig Config = null!;
}