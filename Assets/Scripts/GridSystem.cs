using UnityEngine;

namespace ToyBlast.Core
{
    public class GridSystem : MonoBehaviour
    {
        [Header("Grid Configuration")]
        [SerializeField] private int gridWidth = 9;
        [SerializeField] private int gridHeight = 9;
        [SerializeField] private float tileSize = 1.0f;
        [SerializeField] private float tileSpacing = 0.1f;

        // Grid bounds
        private Vector2 gridCenter;
        private Vector2 gridBottomLeft;
        private Vector2 gridTopRight;

        private void Awake()
        {
            CalculateGridBounds();
        }

        /// <summary>
        /// Grid sınırlarını hesapla
        /// </summary>
        private void CalculateGridBounds()
        {
            // Toplam grid boyutu (tile + spacing)
            float totalWidth = (gridWidth * tileSize) + ((gridWidth - 1) * tileSpacing);
            float totalHeight = (gridHeight * tileSize) + ((gridHeight - 1) * tileSpacing);

            // Grid merkezi (0,0)
            gridCenter = Vector2.zero;

            // Grid köşeleri
            gridBottomLeft = gridCenter - new Vector2(totalWidth * 0.5f, totalHeight * 0.5f);
            gridTopRight = gridCenter + new Vector2(totalWidth * 0.5f, totalHeight * 0.5f);
        }

        /// <summary>
        /// Grid koordinatını world pozisyonuna çevir
        /// </summary>
        /// <param name="gridX">Grid X koordinatı (0-8)</param>
        /// <param name="gridY">Grid Y koordinatı (0-8)</param>
        /// <returns>World pozisyonu</returns>
        public Vector3 GridToWorldPosition(int gridX, int gridY)
        {
            if (!IsValidGridPosition(gridX, gridY))
            {
                Debug.LogError($"Invalid grid position: ({gridX}, {gridY})");
                return Vector3.zero;
            }

            float worldX = gridBottomLeft.x + (gridX * (tileSize + tileSpacing)) + (tileSize * 0.5f);
            float worldY = gridBottomLeft.y + (gridY * (tileSize + tileSpacing)) + (tileSize * 0.5f);

            return new Vector3(worldX, worldY, 0f);
        }

        /// <summary>
        /// World pozisyonunu grid koordinatına çevir
        /// </summary>
        /// <param name="worldPosition">World pozisyonu</param>
        /// <returns>Grid koordinatı (Vector2Int)</returns>
        public Vector2Int WorldToGridPosition(Vector3 worldPosition)
        {
            float localX = worldPosition.x - gridBottomLeft.x;
            float localY = worldPosition.y - gridBottomLeft.y;

            int gridX = Mathf.FloorToInt(localX / (tileSize + tileSpacing));
            int gridY = Mathf.FloorToInt(localY / (tileSize + tileSpacing));

            return new Vector2Int(gridX, gridY);
        }

        /// <summary>
        /// Grid pozisyonu geçerli mi kontrol et
        /// </summary>
        /// <param name="gridX">Grid X koordinatı</param>
        /// <param name="gridY">Grid Y koordinatı</param>
        /// <returns>Geçerli ise true</returns>
        public bool IsValidGridPosition(int gridX, int gridY)
        {
            return gridX >= 0 && gridX < gridWidth && gridY >= 0 && gridY < gridHeight;
        }

        /// <summary>
        /// Grid pozisyonu geçerli mi kontrol et (Vector2Int)
        /// </summary>
        public bool IsValidGridPosition(Vector2Int gridPos)
        {
            return IsValidGridPosition(gridPos.x, gridPos.y);
        }

        // Properties
        public int GridWidth => gridWidth;
        public int GridHeight => gridHeight;
        public float TileSize => tileSize;
        public float TileSpacing => tileSpacing;
        public Vector2 GridCenter => gridCenter;

        // Debug için grid çizimi
        private void OnDrawGizmos()
        {
            if (Application.isPlaying)
            {
                // Grid sınırlarını çiz
                Gizmos.color = Color.yellow;
                Vector3 size = new Vector3(
                    gridTopRight.x - gridBottomLeft.x,
                    gridTopRight.y - gridBottomLeft.y,
                    0.1f
                );
                Gizmos.DrawWireCube(gridCenter, size);

                // Grid noktalarını çiz
                Gizmos.color = Color.red;
                for (int x = 0; x < gridWidth; x++)
                {
                    for (int y = 0; y < gridHeight; y++)
                    {
                        Vector3 worldPos = GridToWorldPosition(x, y);
                        Gizmos.DrawWireCube(worldPos, Vector3.one * tileSize);
                    }
                }
            }
        }
    }
}