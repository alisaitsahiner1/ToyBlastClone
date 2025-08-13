using UnityEngine;
using ToyBlast.Core;
using UnityEngine.InputSystem;
using System.Collections.Generic;   // List, Queue
using DG.Tweening;


namespace ToyBlast.Managers
{
    public class BoardManager : MonoBehaviour
    {
        [SerializeField] private GridSystem gridSystem;
        [SerializeField] private GameObject[] blockPrefabs;
        [SerializeField] private Transform blocksParent;
        private Block[,] blocks;

        [SerializeField] private GameObject[] particlePrefabs;

        private Dictionary<BlockColor, GameObject> particleMap;



        private void Start()
        {
            GenerateInitialBoard();

            particleMap = new Dictionary<BlockColor, GameObject>();

            foreach (GameObject ps in particlePrefabs)
            {
                BlockColor color = ps.GetComponent<ParticleColorTag>().Color;
                particleMap[color] = ps;
            }
            
        }

        private void Update()
        {
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
                Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(mouseScreenPos);
                mouseWorldPos.z = 0f;

                Vector2Int gridPos = gridSystem.WorldToGridPosition(mouseWorldPos);

                if (gridSystem.IsValidGridPosition(gridPos))
                {
                    Block clickedBlock = blocks[gridPos.x, gridPos.y];

                    if (clickedBlock != null)
                    {
                        Debug.Log($"Tıklanan blok: {clickedBlock.Color} [{gridPos.x}, {gridPos.y}]");

                        List<Vector2Int> connectedBlocks = FindConnectedBlocks(gridPos);

                        if (connectedBlocks.Count >= 2)
                        {
                            DestroyBlocks(connectedBlocks);
                        }

                        DropBlocks();
                        

                    }
                }
            }

        }


        private void GenerateInitialBoard()
        {
            blocks = new Block[gridSystem.GridWidth, gridSystem.GridHeight];

            for (int x = 0; x < gridSystem.GridWidth; x++)
            {
                for (int y = 0; y < gridSystem.GridHeight; y++)
                {
                    int randomIndex = Random.Range(0, blockPrefabs.Length);
                    GameObject randomBlock = Instantiate(blockPrefabs[randomIndex]);
                    randomBlock.transform.position = gridSystem.GridToWorldPosition(x, y);

                    SpriteRenderer sr = randomBlock.GetComponent<SpriteRenderer>();
                    if (sr != null)
                    {
                        sr.sortingOrder = +y;
                    }

                    randomBlock.transform.parent = blocksParent;

                    Block block = randomBlock.GetComponent<Block>();
                    blocks[x, y] = block;
                }
            }
        }

        private List<Vector2Int> FindConnectedBlocks(Vector2Int startPos)
        {
            List<Vector2Int> connected = new List<Vector2Int>();
            Block startBlock = blocks[startPos.x, startPos.y];
            if (startBlock == null) return connected;

            BlockColor targetColor = startBlock.Color;
            bool[,] visited = new bool[gridSystem.GridWidth, gridSystem.GridHeight];

            Queue<Vector2Int> toCheck = new Queue<Vector2Int>();
            toCheck.Enqueue(startPos);
            visited[startPos.x, startPos.y] = true;

            while (toCheck.Count > 0)
            {
                Vector2Int current = toCheck.Dequeue();
                connected.Add(current);

                Vector2Int[] directions = {
                    new Vector2Int(1, 0), // right
                    new Vector2Int(-1, 0), // left
                    new Vector2Int(0, 1), // up
                    new Vector2Int(0, -1) // down
                };

                foreach (Vector2Int dir in directions)
                {
                    Vector2Int neighbor = current + dir;

                    if (!gridSystem.IsValidGridPosition(neighbor)) continue;
                    if (visited[neighbor.x, neighbor.y]) continue;

                    Block neighborBlock = blocks[neighbor.x, neighbor.y];
                    if (neighborBlock != null && neighborBlock.Color == targetColor)
                    {
                        toCheck.Enqueue(neighbor);
                        visited[neighbor.x, neighbor.y] = true;
                    }
                }
            }

            return connected;
        }

        private void DestroyBlocks(List<Vector2Int> blockPositions)
        {
            foreach (Vector2Int pos in blockPositions)
            {
                Block block = blocks[pos.x, pos.y];
                GameObject prefab;
                if (particleMap.TryGetValue(block.Color, out prefab))
                {
                    var go = Instantiate(prefab, block.transform.position + Vector3.back * 0.1f, Quaternion.identity);
                    var ps = go.GetComponentInChildren<ParticleSystem>();
                    ps?.Play();
                    Destroy(go, ps.main.duration + ps.main.startLifetime.constantMax);
                }

                    Destroy(block.gameObject);
                    blocks[pos.x, pos.y] = null;
                }
            }
        

        private void DropBlocks()
        {
            for (int x = 0; x < gridSystem.GridWidth; x++)
            {
                for (int y = 1; y < gridSystem.GridHeight; y++) // y=0 zaten en alt
                {
                    if (blocks[x, y] != null && blocks[x, y - 1] == null)
                    {
                        int fallToY = y;
                        while (fallToY > 0 && blocks[x, fallToY - 1] == null)
                        {
                            fallToY--;
                        }

                        Block fallingBlock = blocks[x, y];
                        blocks[x, y] = null;
                        blocks[x, fallToY] = fallingBlock;

                        Vector3 targetPos = gridSystem.GridToWorldPosition(x, fallToY);

                        fallingBlock.transform
                            .DOMove(targetPos, 0.25f)
                            .SetEase(Ease.OutQuad);
                    }
                }
            }
        }


    }
}
