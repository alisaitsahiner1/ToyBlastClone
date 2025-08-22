using UnityEngine;
using ToyBlast.Core;
using UnityEngine.InputSystem;
using System.Collections.Generic;   // List, Queue
using DG.Tweening;


namespace ToyBlast.Managers
{
    public class BoardManager : MonoBehaviour
    {
        [SerializeField] private ToyBlast.Events.GameEventHub eventHub;

        [SerializeField] private GridSystem gridSystem;
        [SerializeField] private GameObject[] blockPrefabs;
        [SerializeField] private Transform blocksParent;
        private Block[,] blocks;


        private Vector3 verticalStep;   // bir hücrelik dikey world farkı
        [SerializeField] private int spawnAboveOffset = 50; // grid üstünden kaç satır yukarıdan başlasın
     // [SerializeField] private float fallDuration = 0.25f;

        [SerializeField] private int baseSorting = 0;

        [SerializeField] private float bounceOvershootCells = 0.015f; // bir hücrenin %si kadar aşırma (0.10–0.20 arası güzel)
        [SerializeField] private Vector2 bounceDurRange = new Vector2(0.06f, 0.14f); // aşağı ve yukarı adımlar için süre aralığı
        [SerializeField] private int bounceMaxCellsScale = 6; // 6 hücreden daha uzun düşüşler için şiddeti clamp'le

        [SerializeField] private float fallSpeedCellsPerSecond = 12f; // tek gerçek hız (hücre/sn)
        [SerializeField] private float minFallDuration = 0.06f;       // çok kısa atlamaları yumuşat

        // fields
        private int _activeTweens;
        
        // abonelik:
        private void OnEnable()  => eventHub.CellClicked.AddListener(OnCellClicked);
        private void OnDisable() => eventHub.CellClicked.RemoveListener(OnCellClicked);


        private void Start()
        {
            GenerateInitialBoard();


            // DİKKAT: GridHeight >= 2 ise güvenli. Eğer 1 ise, alternatif hesap yaparız.
            if (gridSystem.GridHeight >= 2)
            {
                var p0 = gridSystem.GridToWorldPosition(0, 0);
                var p1 = gridSystem.GridToWorldPosition(0, 1);
                verticalStep = p1 - p0;  // "bir hücre yukarı" world vektörü
            }
            else
            {
                // Fallback: 1 hücrelik world adımı tahmini (oyunun ölçüsüne göre ayarla)
                verticalStep = Vector3.up * 1f;
            }
        }

        private void Update()
        {

        }


        private void GenerateInitialBoard()
        {
            blocks = new Block[gridSystem.GridWidth, gridSystem.GridHeight];

            for (int x = 0; x < gridSystem.GridWidth; x++)
            {
                for (int y = 0; y < gridSystem.GridHeight; y++)
                {
                    int randomIndex = Random.Range(0, blockPrefabs.Length);
                    Vector3 pos = gridSystem.GridToWorldPosition(x, y);
                    GameObject randomBlock = Instantiate(blockPrefabs[randomIndex], pos, Quaternion.identity, blocksParent);

                    // ... instantiate ettikten sonra:
                    Block block = randomBlock.GetComponent<Block>();
                    blocks[x, y] = block;

                    // Sıralamayı TEK YERDEN ayarla
                    ApplySortingAt(x, y);

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
            int destroyedCount = blockPositions.Count;

            foreach (Vector2Int pos in blockPositions)
            {
                var block = blocks[pos.x, pos.y];
                int colorIndex = (int)block.Color;

                eventHub?.BlockDestroyed?.Invoke(pos.x, pos.y, colorIndex);

                Destroy(block.gameObject);
                blocks[pos.x, pos.y] = null;

            }

            eventHub?.BlocksDestroyed?.Invoke(destroyedCount);

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

                        // hedef belirlendikten sonra:
                        Vector3 startPosNow = fallingBlock.transform.position;
                        float dur = ComputeFallDuration(startPosNow, targetPos);

                        int lx = x, ly = fallToY;
                        int fallCells = Mathf.Abs(y - fallToY);

                        _activeTweens++; // DOMove başlamadan hemen önce

                        fallingBlock.transform
                            .DOMove(targetPos, dur)
                            .SetEase(Ease.Linear)
                            .OnComplete(() =>
                            {
                                ApplySortingAt(lx, ly);
                                // bounce'i al ve tamamlandığında sayaç düş + event
                                var bounce = PlayLandBounce(fallingBlock.transform, targetPos, fallCells);
                                bounce.OnComplete(() =>
                                {
                                    eventHub?.BlockLanded?.Invoke(lx, ly, (int)blocks[lx, ly].Color);

                                    if (--_activeTweens == 0)
                                        eventHub?.BoardSettled?.Invoke();
                                });
                            });


                    }
                }
            }
        }

        private int[] CalculateMissingCountsPerColumn()
        {
            int w = gridSystem.GridWidth;
            int h = gridSystem.GridHeight;
            var counts = new int[w];

            for (int x = 0; x < w; x++)
            {
                int missing = 0;
                for (int y = 0; y < h; y++)
                {
                    if (blocks[x, y] == null)
                        missing++;
                }
                counts[x] = missing;
            }

            return counts;
        }

        private void SpawnMissingBlocks()
        {
            int w = gridSystem.GridWidth;
            int h = gridSystem.GridHeight;

            // 1) Her kolon için eksik sayısını al
            int[] counts = CalculateMissingCountsPerColumn();

            for (int x = 0; x < w; x++)
            {
                int m = counts[x];           // bu kolonda kaç yeni küp spawn edilecek
                if (m <= 0) continue;

                // Stack tabanı: en üst satır world + spawnAboveOffset
                Vector3 topWorld = gridSystem.GridToWorldPosition(x, h - 1);
                Vector3 baseStart = topWorld + verticalStep * spawnAboveOffset;

                // Alttan üste doğru boş hücreleri taramak için bir pointer
                int yPtr = 0;

                // 2) m adet küpü ÜST ÜSTE DİZ ve AYNI ANDA düşür
                for (int i = 0; i < m; i++)
                {
                    // bir sonraki boş y'yi bul
                    while (yPtr < h && blocks[x, yPtr] != null) yPtr++;
                    if (yPtr >= h) break; // emniyet

                    int targetY = yPtr;

                    Vector3 startPos = baseStart + verticalStep * i;            // üst üste istif
                    Vector3 targetPos = gridSystem.GridToWorldPosition(x, targetY);

                    int rand = Random.Range(0, blockPrefabs.Length);
                    GameObject go = Instantiate(blockPrefabs[rand], startPos, Quaternion.identity, blocksParent);
                    Block b = go.GetComponent<Block>();

                    // Diziyi hedefte doldur (yerine düşecek olan blok bu)
                    blocks[x, targetY] = b;

                    // Sıralamayı hedef Y'ye göre ata (hemen + tween bitince tekrar)
                    ApplySortingAt(x, targetY);

                    float dur = ComputeFallDuration(startPos, targetPos);

                    int lx = x, ly = targetY;
                    int fallCells = Mathf.Max(1,
                        Mathf.RoundToInt(Vector3.Distance(startPos, targetPos) / verticalStep.magnitude));

                    _activeTweens++; // DOMove başlamadan önce

                    b.transform
                    .DOMove(targetPos, dur)
                    .SetEase(Ease.Linear)
                    .OnComplete(() =>
                    {
                        ApplySortingAt(lx, ly);
                        var bounce = PlayLandBounce(b.transform, targetPos, fallCells);
                        bounce.OnComplete(() =>
                        {
                            eventHub?.BlockSpawnedAndLanded?.Invoke(lx, ly, (int)b.Color);

                            if (--_activeTweens == 0)
                                eventHub?.BoardSettled?.Invoke();
                        });
                    });



                }
            }
        }



        // Tek bir hücre için order ayarla
        private void ApplySortingAt(int x, int y)
        {
            // bounds guard
            if (x < 0 || x >= gridSystem.GridWidth || y < 0 || y >= gridSystem.GridHeight)
                return;

            var b = blocks[x, y];
            if (b == null) return;

            var sr = b.GetComponent<SpriteRenderer>();
            if (sr != null)
                sr.sortingOrder = baseSorting + y; // en alttan yukarıya doğru artar
        }

        // (İsteğe bağlı) tüm tahtayı güncelle
        private void ApplySortingForAll()
        {
            for (int x = 0; x < gridSystem.GridWidth; x++)
                for (int y = 0; y < gridSystem.GridHeight; y++)
                    ApplySortingAt(x, y);
        }

        private Tween PlayLandBounce(Transform tr, Vector3 targetPos, int fallCells)
        {
            float cellMag = verticalStep.magnitude;                 // 1 hücrelik world mesafesi
            int fc = Mathf.Clamp(fallCells, 1, bounceMaxCellsScale);

            // şiddet: uzun düşüşte biraz art, ama clamp'li
            float overshoot = Mathf.Clamp(bounceOvershootCells * cellMag * fc, 0f, 0.25f * cellMag);

            // süreleri düşüş uzunluğuna göre hafifçe ölçekle
            float mid = (bounceDurRange.x + bounceDurRange.y) * 0.5f;
            float k = Mathf.Clamp01((fc - 1f) / (bounceMaxCellsScale - 1f));
            float upDur = Mathf.Lerp(bounceDurRange.x, mid, k);
            float downDur = Mathf.Lerp(mid, bounceDurRange.y, k);

            // HEDEFİN biraz ÜSTÜNE çık, sonra hedefe geri dön
            Vector3 peak = targetPos + verticalStep.normalized * overshoot;

            tr.DOKill(false); // mevcut pozisyon tweeenlerini temizle (scale vs. dokunmuyor)
            var seq = DOTween.Sequence();
            seq.Append(tr.DOMove(peak, upDur).SetEase(Ease.OutQuad)); // mini yukarı zıpla
            seq.Append(tr.DOMove(targetPos, downDur).SetEase(Ease.InQuad)); // hedefe otur

            return seq;
        }
   
        private float ComputeFallDuration(Vector3 startPos, Vector3 targetPos)
        {
            float cell = verticalStep.magnitude;             // 1 hücrenin world uzunluğu
            float dist = (targetPos - startPos).magnitude;   // world mesafe
            float dur  = dist / (cell * Mathf.Max(0.0001f, fallSpeedCellsPerSecond));
            return Mathf.Max(minFallDuration, dur);
        }   
// tıklama akışı (eski Update içeriği burada çalışır)
        private void OnCellClicked(int x, int y)
        {
            var clicked = blocks[x, y];
            if (clicked == null) return;

            var connected = FindConnectedBlocks(new Vector2Int(x, y));
            if (connected.Count >= 2)
            {
                DestroyBlocks(connected);
            }

            DropBlocks();
            SpawnMissingBlocks();

            if (_activeTweens == 0)
                eventHub?.BoardSettled?.Invoke();
        }   
   
    }
}
