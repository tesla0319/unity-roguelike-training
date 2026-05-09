using System.Collections.Generic;
using UnityEngine;

public class GridManager : MonoBehaviour
{
    [SerializeField] private GameObject floorTilePrefab;
    [SerializeField] private GameObject wallTilePrefab;
    [SerializeField] private GameObject stairTilePrefab;
    [SerializeField] private Transform tilesParent;

    private TileType[,] grid;
    private Sprite sharedSprite;

    private static readonly Color FloorColor = new Color(0.75f, 0.75f, 0.75f);
    private static readonly Color WallColor  = new Color(0.25f, 0.25f, 0.25f);
    private static readonly Color StairColor = new Color(0.95f, 0.85f, 0.10f);

    private static readonly Vector2Int[] FourDirections =
    {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
    };

    private void Awake()
    {
        EnsureTilesParent();
        sharedSprite = CreateUnitSprite();
        SetupCamera();
        GenerateMap(GameConfig.Floors[0]);
    }

    // Called by FloorManager in later phases.
    public void GenerateMap(FloorConfig config)
    {
        GenerateMap(config.WallCount);
    }

    public void GenerateMap(int wallCount)
    {
        ClearTiles();

        bool success = TryGenerate(wallCount, GameConfig.MapGenMaxRetries);
        if (!success)
        {
            int reduced = Mathf.RoundToInt(wallCount * 0.8f);
            TryGenerate(reduced, GameConfig.MapGenMaxRetries);
        }

        PlaceStair();
        RenderTiles();
    }

    // --- Map generation ---

    private bool TryGenerate(int wallCount, int maxAttempts)
    {
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            // All cells initialise to TileType.Floor (value 0).
            grid = new TileType[GameConfig.GridSize, GameConfig.GridSize];
            PlaceWalls(wallCount);
            if (AreAllFloorTilesConnected())
                return true;
        }
        return false;
    }

    private void PlaceWalls(int count)
    {
        var candidates = new List<Vector2Int>(GameConfig.GridSize * GameConfig.GridSize);
        for (int x = 0; x < GameConfig.GridSize; x++)
            for (int y = 0; y < GameConfig.GridSize; y++)
                candidates.Add(new Vector2Int(x, y));

        int placed = 0;
        while (placed < count && candidates.Count > 0)
        {
            int idx = Random.Range(0, candidates.Count);
            Vector2Int pos = candidates[idx];
            // Swap-and-pop for O(1) removal
            candidates[idx] = candidates[candidates.Count - 1];
            candidates.RemoveAt(candidates.Count - 1);
            grid[pos.x, pos.y] = TileType.Wall;
            placed++;
        }
    }

    // BFS: all Floor tiles must form a single connected component.
    private bool AreAllFloorTilesConnected()
    {
        Vector2Int start = new Vector2Int(-1, -1);
        int totalFloor = 0;

        for (int x = 0; x < GameConfig.GridSize; x++)
        {
            for (int y = 0; y < GameConfig.GridSize; y++)
            {
                if (grid[x, y] == TileType.Floor)
                {
                    totalFloor++;
                    if (start.x < 0) start = new Vector2Int(x, y);
                }
            }
        }

        if (totalFloor == 0) return false;

        bool[,] visited = new bool[GameConfig.GridSize, GameConfig.GridSize];
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(start);
        visited[start.x, start.y] = true;
        int reached = 0;

        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            reached++;
            foreach (var dir in FourDirections)
            {
                var next = cur + dir;
                if (InBounds(next) && !visited[next.x, next.y] && grid[next.x, next.y] == TileType.Floor)
                {
                    visited[next.x, next.y] = true;
                    queue.Enqueue(next);
                }
            }
        }

        return reached == totalFloor;
    }

    // Stair is placed on a random Floor cell after wall layout is confirmed.
    private void PlaceStair()
    {
        var floors = new List<Vector2Int>();
        for (int x = 0; x < GameConfig.GridSize; x++)
            for (int y = 0; y < GameConfig.GridSize; y++)
                if (grid[x, y] == TileType.Floor)
                    floors.Add(new Vector2Int(x, y));

        if (floors.Count == 0)
        {
            Debug.LogWarning("[GridManager] No floor tiles available for stair placement.");
            return;
        }
        var pos = floors[Random.Range(0, floors.Count)];
        grid[pos.x, pos.y] = TileType.Stair;
    }

    // --- Rendering ---

    private void RenderTiles()
    {
        for (int x = 0; x < GameConfig.GridSize; x++)
        {
            for (int y = 0; y < GameConfig.GridSize; y++)
            {
                TileType tileType = grid[x, y];
                bool isWall  = tileType == TileType.Wall;
                bool isStair = tileType == TileType.Stair;

                GameObject prefab = isWall  ? wallTilePrefab
                                  : isStair ? stairTilePrefab
                                  : floorTilePrefab;
                GameObject go;

                if (prefab != null)
                {
                    go = Instantiate(prefab, new Vector3(x, y, 0f), Quaternion.identity, tilesParent);
                }
                else
                {
                    go = new GameObject();
                    go.transform.SetParent(tilesParent);
                    go.transform.localPosition = new Vector3(x, y, 0f);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = sharedSprite;
                    sr.color = isWall ? WallColor : (isStair ? StairColor : FloorColor);
                }

                go.name = $"{tileType}_{x}_{y}";
            }
        }
    }

    private void ClearTiles()
    {
        if (tilesParent == null) return;
        for (int i = tilesParent.childCount - 1; i >= 0; i--)
            Destroy(tilesParent.GetChild(i).gameObject);
    }

    // --- Public API (used by later phases) ---

    public Vector2Int GetRandomFloorPosition()
    {
        var floors = new List<Vector2Int>();
        for (int x = 0; x < GameConfig.GridSize; x++)
            for (int y = 0; y < GameConfig.GridSize; y++)
                if (grid[x, y] == TileType.Floor)
                    floors.Add(new Vector2Int(x, y));

        if (floors.Count == 0)
        {
            Debug.LogWarning("[GridManager] No floor tiles found; defaulting to (0,0).");
            return Vector2Int.zero;
        }
        return floors[Random.Range(0, floors.Count)];
    }

    public TileType GetTile(Vector2Int pos)
    {
        if (!InBounds(pos)) return TileType.Wall;
        return grid[pos.x, pos.y];
    }

    public bool IsWalkable(Vector2Int pos) => GetTile(pos) != TileType.Wall;

    public static Vector3 GridToWorld(Vector2Int pos) => new Vector3(pos.x, pos.y, 0f);

    public static Vector2Int WorldToGrid(Vector3 pos) =>
        new Vector2Int(Mathf.RoundToInt(pos.x), Mathf.RoundToInt(pos.y));

    // --- Setup helpers ---

    private void EnsureTilesParent()
    {
        if (tilesParent != null) return;
        var existing = GameObject.Find("Tiles");
        tilesParent = existing != null ? existing.transform : new GameObject("Tiles").transform;
    }

    private void SetupCamera()
    {
        var cam = Camera.main;
        if (cam == null) return;

        float half   = GameConfig.GridSize * 0.5f;   // 6
        float center = half - 0.5f;                  // 5.5
        cam.transform.position = new Vector3(center, center, -10f);

        // Fit both axes with 0.5-unit padding.
        float byHeight = half + 0.5f;
        float byWidth  = (half + 0.5f) / cam.aspect;
        cam.orthographicSize = Mathf.Max(byHeight, byWidth);
    }

    private static Sprite CreateUnitSprite()
    {
        const int res = 16;
        var tex = new Texture2D(res, res) { filterMode = FilterMode.Point };
        var pixels = new Color[res * res];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), res);
    }

    private bool InBounds(Vector2Int pos) =>
        pos.x >= 0 && pos.x < GameConfig.GridSize &&
        pos.y >= 0 && pos.y < GameConfig.GridSize;
}
