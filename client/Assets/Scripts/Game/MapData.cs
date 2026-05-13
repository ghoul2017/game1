using UnityEngine;

namespace Client.Game
{
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
}
