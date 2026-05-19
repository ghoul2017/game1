using System;

namespace Client.Logic
{
    public static class MapData
    {
        public const float CellSize = 1f;

        public static (float x, float y, float z) GridToWorld(int gridX, int gridY)
        {
            return (gridX * CellSize, 0, gridY * CellSize);
        }

        public static (float x, float y, float z) GridToWorld(float gridX, float gridY)
        {
            return (gridX * CellSize, 0, gridY * CellSize);
        }

        public static (int x, int y) WorldToGrid(float worldX, float worldZ)
        {
            return ((int)Math.Round(worldX / CellSize), (int)Math.Round(worldZ / CellSize));
        }
    }
}
