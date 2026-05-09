using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FloorManager : MonoBehaviour
{
    [SerializeField] private GridManager gridManager;
    [SerializeField] private PlayerController player;

    private Transform enemiesParent;

    public int CurrentFloor { get; private set; } = 1;

    private void Start()
    {
        if (gridManager == null) gridManager = FindObjectOfType<GridManager>();
        if (player      == null) player      = FindObjectOfType<PlayerController>();
        EnsureEnemiesParent();
        // Wait one frame so PlayerController.Start() has run and player.GridPos is valid.
        StartCoroutine(InitialEnemySpawn());
    }

    private IEnumerator InitialEnemySpawn()
    {
        yield return null;
        if (player == null)
        {
            Debug.LogError("[FloorManager] PlayerController not found. Enemies not spawned.");
            yield break;
        }
        SpawnEnemies(GameConfig.Floors[CurrentFloor - 1].EnemyCount, player.GridPos);
        Debug.Log($"[FloorManager] Floor {CurrentFloor} ready — {GameConfig.Floors[CurrentFloor - 1].EnemyCount} enemies spawned.");
    }

    // Public API kept for external callers (e.g. future systems).
    public void SpawnEnemiesForCurrentFloor(Vector2Int playerPos)
    {
        SpawnEnemies(GameConfig.Floors[CurrentFloor - 1].EnemyCount, playerPos);
    }

    // --- Floor transitions ---

    public void GoToNextFloor()
    {
        if (CurrentFloor >= GameConfig.MaxFloor)
        {
            GameManager.Instance?.TriggerClear();
            return;
        }

        CurrentFloor++;
        ClearEnemies();
        gridManager.GenerateMap(GameConfig.Floors[CurrentFloor - 1]);
        player.Spawn(gridManager.GetRandomFloorPosition());
        SpawnEnemies(GameConfig.Floors[CurrentFloor - 1].EnemyCount, player.GridPos);
        Debug.Log($"[FloorManager] Entered floor {CurrentFloor}.");
    }

    public void ResetToFloor1()
    {
        CurrentFloor = 1;
        ClearEnemies();
        gridManager.GenerateMap(GameConfig.Floors[0]);
        player.Spawn(gridManager.GetRandomFloorPosition());
        SpawnEnemies(GameConfig.Floors[0].EnemyCount, player.GridPos);
    }

    // --- Enemy spawning ---

    private void SpawnEnemies(int count, Vector2Int playerPos)
    {
        // Collect Floor tiles at Manhattan distance >= EnemyMinSpawnDistance from player.
        var candidates = new List<Vector2Int>();
        for (int x = 0; x < GameConfig.GridSize; x++)
        {
            for (int y = 0; y < GameConfig.GridSize; y++)
            {
                var pos = new Vector2Int(x, y);
                if (gridManager.GetTile(pos) != TileType.Floor) continue;
                int dist = Mathf.Abs(pos.x - playerPos.x) + Mathf.Abs(pos.y - playerPos.y);
                if (dist >= GameConfig.EnemyMinSpawnDistance)
                    candidates.Add(pos);
            }
        }

        int spawned = 0;
        while (spawned < count && candidates.Count > 0)
        {
            int idx = Random.Range(0, candidates.Count);
            Vector2Int pos = candidates[idx];
            candidates[idx] = candidates[candidates.Count - 1];
            candidates.RemoveAt(candidates.Count - 1);
            SpawnEnemy(pos);
            spawned++;
        }
    }

    private void SpawnEnemy(Vector2Int pos)
    {
        var go    = new GameObject($"Enemy_{pos.x}_{pos.y}");
        go.transform.SetParent(enemiesParent);
        var enemy = go.AddComponent<EnemyController>();
        enemy.Initialize(pos, gridManager);

        if (GameManager.Instance != null)
            GameManager.Instance.RegisterEnemy(enemy);
        else
            Debug.LogWarning("[FloorManager] GameManager not found — enemy not registered for turns. " +
                             "Add a GameManager GameObject with GameManager component to the scene.");
    }

    private void ClearEnemies()
    {
        GameManager.Instance?.ClearEnemyRegistry();
        EnsureEnemiesParent();
        for (int i = enemiesParent.childCount - 1; i >= 0; i--)
            Destroy(enemiesParent.GetChild(i).gameObject);
    }

    private void EnsureEnemiesParent()
    {
        if (enemiesParent != null) return;
        var existing  = GameObject.Find("Enemies");
        enemiesParent = existing != null ? existing.transform : new GameObject("Enemies").transform;
    }
}
