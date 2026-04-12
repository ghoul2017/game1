using UnityEngine;

namespace Client.Game
{
    public class RoleData
    {
        public long RoleId;
        public string Name;
        public int Level;
        public string Job;
    }

    public static class GameData
    {
        public static RoleData CurrentRole { get; set; }
    }
}
