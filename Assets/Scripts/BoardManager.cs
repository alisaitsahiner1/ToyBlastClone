using UnityEngine;
using ToyBlast.Core;
using UnityEngine.InputSystem;
using System.Collections.Generic;   // List, Queue
using DG.Tweening;
using System.Collections;
using UnityEditor.Experimental.GraphView; // IEnumerator için



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

        [SerializeField] private int startingMoves = 20;
        private int moves;

        [SerializeField] private GameObject rocketPrefab;
        [SerializeField] private GameObject tntPrefab;
        [SerializeField] private GameObject rubikPrefab;

        [SerializeField] private float rocketStepDelay = 0.05f; // adım adım yok etme gecikmesi
        [SerializeField] private float puzzleFocusDelay = 0.35f; // odaklanma bekleme süresi

        private bool _resolving = false;                         // patlama sırasında ikinci tıklamayı kilitle

        private int _resolveDepth = 0;   // zincir derinliği

        private HashSet<Vector2Int> _destroyedThisResolve; // bu çözüm sırasında zaten silinmiş hücreler
        private HashSet<Vector2Int> _activatingPowerups = new HashSet<Vector2Int>();





        // fields
        private int _activeTweens;

        // abonelik:
        private void OnEnable()
        {
            eventHub.CellClicked.AddListener(OnCellClicked);
            eventHub.BoardSettled.AddListener(RecomputeAllHints); // <-- yeni ekledik
        }

        private void OnDisable()
        {
            eventHub.CellClicked.RemoveListener(OnCellClicked);
            eventHub.BoardSettled.RemoveListener(RecomputeAllHints); // <-- yeni ekledik
        }


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

            moves = startingMoves;
            eventHub?.MovesChanged?.Invoke(moves);

            RecomputeAllHints();

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
            if (startBlock == null || startBlock.IsPowerup)
                return connected;   // Powerup bloklardan grup başlamasın

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

                    if (neighborBlock != null
                        && !neighborBlock.IsPowerup               // <<< ek
                        && neighborBlock.Color == targetColor)
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
            int destroyedCount = 0;

            foreach (Vector2Int pos in blockPositions)
            {
                // 1) aynı çözümde aynı hücreyi iki kere silme
                if (_destroyedThisResolve != null && !_destroyedThisResolve.Add(pos))
                    continue;

                var block = blocks[pos.x, pos.y];
                if (block == null) continue;   // zaten silinmiş olabilir -> atla

                // 2) bu objeye bağlı TÜM tween’leri öldür (DOMove, bounce vs.)
                var tr = block.transform;
                if (tr != null) tr.DOKill(false);
                DOTween.Kill(block, false);

                // 3) event’te powerup’ları -1 ile geç (renk partikülü çalışmasın)
                int colorIndex = block.IsPowerup ? -1 : (int)block.Color;
                eventHub?.BlockDestroyed?.Invoke(pos.x, pos.y, colorIndex);

                // 4) objeyi sil ve diziyi boşalt
                Destroy(block.gameObject);
                blocks[pos.x, pos.y] = null;

                destroyedCount++;
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

                        if (fallingBlock == null || fallingBlock.transform == null)  // <<< EMNİYET
                            continue;

                        Vector3 targetPos = gridSystem.GridToWorldPosition(x, fallToY);

                        // hedef belirlendikten sonra:
                        Vector3 startPosNow = fallingBlock.transform.position;
                        float dur = ComputeFallDuration(startPosNow, targetPos);

                        int lx = x, ly = fallToY;
                        int fallCells = Mathf.Abs(y - fallToY);

                        _activeTweens++; // DOMove başlamadan hemen önce

                        if (fallingBlock == null || fallingBlock.transform == null) continue; // DOMove’dan HEMEN önce

                        fallingBlock.transform
                            .DOMove(targetPos, dur)
                            .SetEase(Ease.Linear)
                            .OnComplete(() =>
                            {
                                ApplySortingAt(lx, ly);
                                // bounce'i al ve tamamlandığında sayaç düş + event
                                if (fallingBlock != null && fallingBlock.transform != null)
                                {
                                    var bounce = PlayLandBounce(fallingBlock.transform, targetPos, fallCells);
                                    bounce.OnComplete(() =>
                                    {
                                        int colorIdx = -1;
                                        if (lx >= 0 && lx < gridSystem.GridWidth &&
                                            ly >= 0 && ly < gridSystem.GridHeight &&
                                            blocks[lx, ly] != null && !blocks[lx, ly].IsPowerup)
                                        {
                                            colorIdx = (int)blocks[lx, ly].Color;
                                        }

                                        eventHub?.BlockLanded?.Invoke(lx, ly, colorIdx);
                                        if (--_activeTweens == 0) eventHub?.BoardSettled?.Invoke();
                                    });
                                }
                                else
                                {
                                    if (--_activeTweens == 0) eventHub?.BoardSettled?.Invoke();
                                }

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

                    if (b == null || b.transform == null) continue;  // <<< EMNİYET

                    b.transform
                    .DOMove(targetPos, dur)
                    .SetEase(Ease.Linear)
                    .OnComplete(() =>
                    {
                        ApplySortingAt(lx, ly);
                        if (b != null && b.transform != null)
                        {
                            var bounce = PlayLandBounce(b.transform, targetPos, fallCells);
                            bounce.OnComplete(() =>
                            {
                                int colorIdx = -1;
                                if (lx >= 0 && lx < gridSystem.GridWidth &&
                                    ly >= 0 && ly < gridSystem.GridHeight &&
                                    blocks[lx, ly] != null && !blocks[lx, ly].IsPowerup)
                                {
                                    colorIdx = (int)blocks[lx, ly].Color;
                                }

                                eventHub?.BlockLanded?.Invoke(lx, ly, colorIdx);
                                if (--_activeTweens == 0) eventHub?.BoardSettled?.Invoke();
                            });
                        }
                        else
                        {
                            if (--_activeTweens == 0) eventHub?.BoardSettled?.Invoke();
                        }

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
            if (tr == null) return DOTween.Sequence(); // boş sequence; OnComplete set edilirse hemen çalışır


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
            float dur = dist / (cell * Mathf.Max(0.0001f, fallSpeedCellsPerSecond));
            return Mathf.Max(minFallDuration, dur);
        }
        // tıklama akışı (eski Update içeriği burada çalışır)
        private void OnCellClicked(int x, int y)
        {

            if (_resolving) return; // şu an patlama çözülüyorsa tıklama alma

            var clickedBlock = blocks[x, y];
            if (clickedBlock != null && clickedBlock.IsPowerup && clickedBlock.PowerupKind == HintPowerupKind.Rocket)
            {
                StartCoroutine(ActivateRocket(x, y, clickedBlock.RocketOrientation));
                return;
            }

            if (clickedBlock != null && clickedBlock.IsPowerup && clickedBlock.PowerupKind == HintPowerupKind.TNT)
            {
                StartCoroutine(ActivateTNT(x, y));
                return;
            }

            if (clickedBlock != null && clickedBlock.IsPowerup && clickedBlock.PowerupKind == HintPowerupKind.Rubik)
            {
                var targetColor = clickedBlock.OriginColorForVFX; // <<< Artık sabit "Red" değil
                StartCoroutine(ActivatePuzzle(x, y, targetColor));
                return;
            }

            if (moves <= 0) return;

            var clicked = blocks[x, y];
            if (clicked == null) return;

            var connected = FindConnectedBlocks(new Vector2Int(x, y));
            if (connected.Count < 2)
                return;

            // 1) Kaçlı → powerup türü
            var kind = EvaluateHintByCount(connected.Count);

            if (kind != HintPowerupKind.None)
            {
                // İPUCU: görsel çakışma olmasın diye mevcut hintleri önce temizle
                ClearAllHints();   // (bir önceki adımda eklediğimiz yardımcı)

                // 3) Kümeyi patlatmadan ÖNCE:
                BlockColor originColor = blocks[x, y].Color;

                // 2) Roketse yön seç
                var rocketOri = (kind == HintPowerupKind.Rocket)
                    ? DecideRocketOrientation(connected)
                    : RocketOrientation.Vertical; // (TNT/Rubik için önemsiz)

                // 3) Kümeyi patlat
                DestroyBlocks(connected);

                // 4) Powerup doğururken bu rengi ilet
                SpawnPowerupAt(x, y, kind, rocketOri, originColor); // imzayı bir kez genişleteceğiz
            }
            else
            {
                // <5 ise normal patlat
                DestroyBlocks(connected);
            }

            // 5) Yerçekimi ve doldurma her zamanki gibi
            DropBlocks();
            SpawnMissingBlocks();

            if (_activeTweens == 0)
                eventHub?.BoardSettled?.Invoke();
        }

        private HintPowerupKind EvaluateHintByCount(int c)
        {
            if (c >= 9) return HintPowerupKind.Rubik;
            if (c >= 7) return HintPowerupKind.TNT;
            if (c >= 5) return HintPowerupKind.Rocket;
            return HintPowerupKind.None;
        }

        private void ClearAllHints()
        {
            for (int x = 0; x < gridSystem.GridWidth; x++)
                for (int y = 0; y < gridSystem.GridHeight; y++)
                    blocks[x, y]?.ClearHint();
        }

        private void RecomputeAllHints()
        {
            ClearAllHints();

            bool[,] visited = new bool[gridSystem.GridWidth, gridSystem.GridHeight];

            for (int x = 0; x < gridSystem.GridWidth; x++)
            {
                for (int y = 0; y < gridSystem.GridHeight; y++)
                {
                    if (visited[x, y] || blocks[x, y] == null) continue;

                    var cluster = FindConnectedBlocks(new Vector2Int(x, y));
                    foreach (var p in cluster) visited[p.x, p.y] = true;

                    var kind = EvaluateHintByCount(cluster.Count);
                    if (kind == HintPowerupKind.None) continue;

                    foreach (var p in cluster)
                        blocks[p.x, p.y]?.ShowHint(kind);
                }
            }
        }

        private RocketOrientation DecideRocketOrientation(List<Vector2Int> cluster)
        {
            int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
            foreach (var p in cluster)
            {
                minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
                minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y);
            }
            int width = (maxX - minX + 1);
            int height = (maxY - minY + 1);
            return (width >= height) ? RocketOrientation.Horizontal : RocketOrientation.Vertical;
        }

        private void SpawnPowerupAt(int x, int y, HintPowerupKind kind, RocketOrientation rocketOri, BlockColor originColor)
        {
            GameObject prefab = null;
            switch (kind)
            {
                case HintPowerupKind.Rocket: prefab = rocketPrefab; break;
                case HintPowerupKind.TNT: prefab = tntPrefab; break;
                case HintPowerupKind.Rubik: prefab = rubikPrefab; break;
                default: return;
            }

            var worldPos = gridSystem.GridToWorldPosition(x, y);
            var go = Instantiate(prefab, worldPos, Quaternion.identity, blocksParent);

            // Roket prefabi dikey ise varsayılan kalsın; yatay istiyorsak 90° çevirebiliriz:
            if (kind == HintPowerupKind.Rocket && rocketOri == RocketOrientation.Horizontal)
                go.transform.rotation = Quaternion.Euler(0, 0, 90f);

            var b = go.GetComponent<Block>();
            b.SetPowerup(kind, rocketOri);
            b.SetOriginColor(originColor);
            blocks[x, y] = b;              // ızgarayı doldur
            ApplySortingAt(x, y);          // sorting order tutarlı kalsın
        }

        private BlockColor GetPowerupTargetColor(Block b)
        {
            return b.OriginColorForVFX;
        }


        private IEnumerator ActivateRocket(int cx, int cy, RocketOrientation orientation)
        {
            BeginResolve();

            _resolving = true;
            ClearAllHints(); // ikonlu hintleri kapat, görsel karışıklık olmasın

            // 0) Roketin kendisini önce yok et
            DestroyBlocks(new List<Vector2Int> { new Vector2Int(cx, cy) });
            yield return new WaitForSeconds(rocketStepDelay);

            if (orientation == RocketOrientation.Horizontal)
            {
                int step = 1;
                while (true)
                {
                    var burst = new List<Vector2Int>();

                    int lx = cx - step;
                    int rx = cx + step;

                    bool anyInside = false;

                    // SOL hücre
                    if (lx >= 0)
                    {
                        anyInside = true;
                        if (blocks[lx, cy] != null)
                        {
                            if (blocks[lx, cy].IsPowerup)
                            {
                                // önce powerup'ı tetikle
                                TriggerPowerupAt_FireAndForget(lx, cy);
                                // tetiklediğimiz için normal yok etme listesine EKLEME
                            }
                            else
                            {
                                burst.Add(new Vector2Int(lx, cy));
                            }
                        }
                    }

                    // SAĞ hücre
                    if (rx < gridSystem.GridWidth)
                    {
                        anyInside = true;
                        if (blocks[rx, cy] != null)
                        {
                            if (blocks[rx, cy].IsPowerup)
                            {
                               TriggerPowerupAt_FireAndForget(rx, cy);
                            }
                            else
                            {
                                burst.Add(new Vector2Int(rx, cy));
                            }
                        }
                    }

                    if (!anyInside) break;                 // grid dışına tamamen çıktıysak bitti
                    if (burst.Count > 0) DestroyBlocks(burst); // o adımda normal blokları yok et

                    yield return new WaitForSeconds(rocketStepDelay);
                    step++;
                }
            }
            else // vertical
            {
                int step = 1;
                while (true)
                {
                    var burst = new List<Vector2Int>();

                    int by = cy - step;
                    int uy = cy + step;

                    bool anyInside = false;

                    // AŞAĞI hücre
                    if (by >= 0)
                    {
                        anyInside = true;
                        if (blocks[cx, by] != null)
                        {
                            if (blocks[cx, by].IsPowerup)
                            {
                                TriggerPowerupAt_FireAndForget(cx, by);
                            }
                            else
                            {
                                burst.Add(new Vector2Int(cx, by));
                            }
                        }
                    }

                    // YUKARI hücre
                    if (uy < gridSystem.GridHeight)
                    {
                        anyInside = true;
                        if (blocks[cx, uy] != null)
                        {
                            if (blocks[cx, uy].IsPowerup)
                            {
                                TriggerPowerupAt_FireAndForget(cx, uy);
                            }
                            else
                            {
                                burst.Add(new Vector2Int(cx, uy));
                            }
                        }
                    }

                    if (!anyInside) break;
                    if (burst.Count > 0) DestroyBlocks(burst);

                    yield return new WaitForSeconds(rocketStepDelay);
                    step++;
                }
            }


            _resolving = false;

            EndResolve();
            yield break;
        }

        private IEnumerator ActivateTNT(int cx, int cy)
        {
            BeginResolve();
            ClearAllHints();

            var normals = new List<Vector2Int>(8);
            var powerups = new List<Vector2Int>(4);

            // 3x3 alanı dolaş
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    int nx = cx + dx, ny = cy + dy;
                    if (!gridSystem.IsValidGridPosition(nx, ny)) continue;

                    var b = blocks[nx, ny];
                    if (b == null) continue;

                    // Merkez TNT (cx,cy): tetikleyici kendisi, powerup listesine ekleme
                    if (nx == cx && ny == cy) continue;

                    if (b.IsPowerup)
                        powerups.Add(new Vector2Int(nx, ny));
                    else
                        normals.Add(new Vector2Int(nx, ny));
                }
            }

            // 1) Önce 3x3 içindeki powerup'ları tetikle (ANINDA)
            foreach (var p in powerups)
                TriggerPowerupAt_FireAndForget(p.x, p.y);

            // 2) Sonra 3x3 içindeki NORMAL blokları ve MERKEZ TNT'yi tek seferde yok et
            normals.Add(new Vector2Int(cx, cy)); // merkez TNT de gitsin
            if (normals.Count > 0)
                DestroyBlocks(normals);

            // 3) Zincirin settle'ını merkezi yerden yaptır
            EndResolve();
            yield break;
        }

        private IEnumerator ActivatePuzzle(int cx, int cy, BlockColor targetColor)
        {
            BeginResolve();

            _resolving = true;
            ClearAllHints(); // ekranda eski hint kalmasın

            // 1) Tüm tahtada hedef renkteki NORMAL blokları topla
            var toBlow = new List<Vector2Int>(gridSystem.GridWidth * gridSystem.GridHeight);
            for (int x = 0; x < gridSystem.GridWidth; x++)
            {
                for (int y = 0; y < gridSystem.GridHeight; y++)
                {
                    var b = blocks[x, y];
                    if (b == null) continue;

                    // Powerup'ları es geç; SADECE normal hedef renk
                    if (!b.IsPowerup && b.Color == targetColor)
                        toBlow.Add(new Vector2Int(x, y));
                }
            }

            // 2) (opsiyonel basit odak): hepsine kısa süreli Rubik hint'i bas
            foreach (var p in toBlow)
                blocks[p.x, p.y]?.ShowHint(HintPowerupKind.Rubik);

            // powerup'ın kendisi (cx,cy) de patlayacağı için oradaki hint'e gerek yok,
            // ama istersen görsel tutarlılık için dokunma.

            // 3) Odak beklemesi
            yield return new WaitForSeconds(puzzleFocusDelay);

            // 4) Hintleri temizle ve TEK SEFERDE yok et
            foreach (var p in toBlow)
                blocks[p.x, p.y]?.ClearHint();

            // Puzzle’ın kendisi de yok olmalı (merkez powerup)
            var withCenter = new List<Vector2Int>(toBlow.Count + 1);
            withCenter.AddRange(toBlow);
            withCenter.Add(new Vector2Int(cx, cy));

            DestroyBlocks(withCenter);

            _resolving = false;

            EndResolve();
            yield break;
        }

        private void BeginResolve()
        {
            if (_resolveDepth == 0) _destroyedThisResolve = new HashSet<Vector2Int>();
            _resolveDepth++;
            ClearAllHints();
        }
        private void EndResolve()
        {
            _resolveDepth = Mathf.Max(0, _resolveDepth - 1);
            if (_resolveDepth == 0)
            {
                DropBlocks();
                SpawnMissingBlocks();
                _destroyedThisResolve?.Clear();   // <<< eklendi
                _destroyedThisResolve = null;     // <<< eklendi
                if (_activeTweens == 0)
                    eventHub?.BoardSettled?.Invoke();
            }
        }


        private void TriggerPowerupAt_FireAndForget(int px, int py)
        {
            // Aynı powerup aynı anda birden çok kez tetiklenmesin:
            var key = new Vector2Int(px, py);
            if (_activatingPowerups.Contains(key)) return;

            _activatingPowerups.Add(key);
            StartCoroutine(RunPowerupAt(px, py));
        }

        // Asıl çalıştırıcı: bittiğinde set’ten düşer
        private IEnumerator RunPowerupAt(int px, int py)
        {
            var b = (px >= 0 && px < gridSystem.GridWidth && py >= 0 && py < gridSystem.GridHeight) ? blocks[px, py] : null;

            if (b != null && b.IsPowerup)
            {
                // Her powerup kendi BeginResolve/EndResolve'ünü zaten çağırıyor olmalı
                if (b.PowerupKind == HintPowerupKind.Rocket)
                    yield return StartCoroutine(ActivateRocket(px, py, b.RocketOrientation));
                else if (b.PowerupKind == HintPowerupKind.TNT)
                    yield return StartCoroutine(ActivateTNT(px, py));
                else if (b.PowerupKind == HintPowerupKind.Rubik)
                    yield return StartCoroutine(ActivatePuzzle(px, py, GetPowerupTargetColor(b)));
            }

            _activatingPowerups.Remove(new Vector2Int(px, py));
        }

    }
}
