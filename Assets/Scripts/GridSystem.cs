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

        [Header("Tile Settings")]
        [SerializeField] private GameObject tilePrefab;

        // Tile tracking
        private GameObject[,] tileObjects;



        // Grid bounds
        private Vector2 gridCenter;
        private Vector2 gridBottomLeft;
        private Vector2 gridTopRight;

        private void Awake()
        {
            CalculateGridBounds();
            InitializeTileArray();
        }

        private void InitializeTileArray()
        {
            tileObjects = new GameObject[gridWidth, gridHeight];
        }

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

        public Vector2Int WorldToGridPosition(Vector3 worldPosition)
        {
            float localX = worldPosition.x - gridBottomLeft.x;
            float localY = worldPosition.y - gridBottomLeft.y;

            int gridX = Mathf.FloorToInt(localX / (tileSize + tileSpacing));
            int gridY = Mathf.FloorToInt(localY / (tileSize + tileSpacing));

            return new Vector2Int(gridX, gridY);
        }

        public bool IsValidGridPosition(int gridX, int gridY)
        {
            return gridX >= 0 && gridX < gridWidth && gridY >= 0 && gridY < gridHeight;
        }

        public bool IsValidGridPosition(Vector2Int gridPos)
        {
            return IsValidGridPosition(gridPos.x, gridPos.y);
        }


        public GameObject SpawnTile(int gridX, int gridY)
        {
            if (!IsValidGridPosition(gridX, gridY))
            {
                Debug.LogError($"Invalid spawn position: ({gridX}, {gridY})");
                return null;
            }

            if (tilePrefab == null)
            {
                Debug.Log($"Tile Prefab: {tilePrefab?.name ?? "NULL"}");
                Debug.LogError("Tile prefab not assigned!");
                return null;
            }

            // Eğer zaten tile varsa, önce onu yok et
            if (tileObjects[gridX, gridY] != null)
            {
                DestroyImmediate(tileObjects[gridX, gridY]);
            }

            // Yeni tile spawn et
            Vector3 worldPos = GridToWorldPosition(gridX, gridY);
            GameObject newTile = Instantiate(tilePrefab, worldPos, Quaternion.identity);
            newTile.name = $"Tile_({gridX},{gridY})";
            newTile.transform.SetParent(this.transform);

            // Array'e kaydet
            tileObjects[gridX, gridY] = newTile;

            return newTile;
        }

        public void DestroyTile(int gridX, int gridY)
        {
            if (!IsValidGridPosition(gridX, gridY))
                return;

            if (tileObjects[gridX, gridY] != null)
            {
                DestroyImmediate(tileObjects[gridX, gridY]);
                tileObjects[gridX, gridY] = null;
            }
        }

        [ContextMenu("Fill Grid With Tiles")]
        public void FillGridWithTiles()
        {
            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    SpawnTile(x, y);
                }
            }
        }

        [ContextMenu("Clear All Tiles")]
        public void ClearAllTiles()
        {
            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    DestroyTile(x, y);
                }
            }
        }

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
        public int GridWidth => gridWidth;
        public int GridHeight => gridHeight;

    }
}