using UnityEngine;

namespace Client.Game
{
    public class MapConfig
    {
        public int Id;
        public string Name;
        public int Width;
        public int Height;
        public int BirthX;
        public int BirthY;
    }

    public static class MapData
    {
        public const float CellSize = 1f;

        public static Vector3 GridToWorld(int gridX, int gridY)
        {
            return new Vector3(gridX * CellSize, 0, gridY * CellSize);
        }

        public static Vector3 GridToWorld(float gridX, float gridY)
        {
            return new Vector3(gridX * CellSize, 0, gridY * CellSize);
        }

        public static (int x, int y) WorldToGrid(Vector3 worldPos)
        {
            return (Mathf.RoundToInt(worldPos.x / CellSize), Mathf.RoundToInt(worldPos.z / CellSize));
        }
    }

    public static class DefaultMapConfigs
    {
        public static readonly MapConfig[] Maps = {
            new MapConfig { Id = 1, Name = "新手村", Width = 200, Height = 200, BirthX = 100, BirthY = 100 },
            new MapConfig { Id = 2, Name = "主城", Width = 300, Height = 300, BirthX = 150, BirthY = 150 },
            new MapConfig { Id = 3, Name = "野外", Width = 500, Height = 500, BirthX = 250, BirthY = 250 }
        };

        public static MapConfig GetById(int id)
        {
            foreach (var m in Maps)
                if (m.Id == id) return m;
            return Maps[0];
        }
    }
}
