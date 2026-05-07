using Game.Components.Chat;
using Game.Components.Fight;
using Game.Components.Map;
using Game.Components.Monster;
using Game.Components.Player;
using Game.Core;
using Game.Entities;

namespace Game;

public static class Context
{
    public static Module Server = new();
    public static AppConfig Config = null!;

    public static PlayerComponent Players => Server.GetComponent<PlayerComponent>();
    public static FightComponent Fight => Server.GetComponent<FightComponent>();
    public static ChatComponent Chat => Server.GetComponent<ChatComponent>();
    public static MapComponent Map => Server.GetComponent<MapComponent>();
    public static MonsterComponent Monster => Server.GetComponent<MonsterComponent>();
}
