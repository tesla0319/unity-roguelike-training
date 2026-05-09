using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Spec §20: Singleton is allowed only for GameManager.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    private GridManager gridManager;
    private PlayerController player;
    private FloorManager floorManager;

    public GameState State { get; private set; } = GameState.Init;
    public TurnPhase CurrentTurn { get; private set; } = TurnPhase.PlayerTurn;

    // HP is owned by PlayerController; this passthrough keeps external reads working.
    public int PlayerHP => player?.HP ?? 0;

    private readonly List<EnemyController> enemies = new List<EnemyController>();

    public bool IsPlayerTurn =>
        State == GameState.Playing && CurrentTurn == TurnPhase.PlayerTurn;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        gridManager  = FindObjectOfType<GridManager>();
        player       = FindObjectOfType<PlayerController>();
        floorManager = FindObjectOfType<FloorManager>();

        if (gridManager  == null) Debug.LogError("[GameManager] GridManager not found in scene.");
        if (player       == null) Debug.LogError("[GameManager] PlayerController not found in scene.");
        if (floorManager == null) Debug.LogWarning("[GameManager] FloorManager not found in scene.");

        State       = GameState.Playing;
        CurrentTurn = TurnPhase.PlayerTurn;

        Debug.Log("[GameManager] Started.");
    }

    // --- Turn management ---

    // Called by PlayerController after any turn-consuming action.
    public void OnPlayerActionComplete()
    {
        if (State != GameState.Playing) return;
        RunEnemyTurn();
    }

    private void RunEnemyTurn()
    {
        CurrentTurn = TurnPhase.EnemyTurn;
        Debug.Log($"[GameManager] Enemy turn — {enemies.Count} enemies.");

        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            if (enemies[i] == null) { enemies.RemoveAt(i); continue; }
            enemies[i].TakeTurn(player.GridPos);
            if (State != GameState.Playing) break;
        }

        CurrentTurn = TurnPhase.PlayerTurn;
    }

    // --- State transitions ---

    public void DamagePlayer(int amount)
    {
        if (State != GameState.Playing || player == null) return;
        player.TakeDamage(amount);
        Debug.Log($"[Combat] Player takes {amount} damage. HP: {player.HP}/{player.MaxHP}");

        if (player.HP <= 0)
        {
            State = GameState.GameOver;
            Debug.Log("[GameManager] GAME OVER");
        }
    }

    public void TriggerClear()
    {
        if (State != GameState.Playing) return;
        State = GameState.Clear;
        Debug.Log("[GameManager] CLEAR!");
    }

    public void ResetGame()
    {
        player?.InitHP();
        State       = GameState.Playing;
        CurrentTurn = TurnPhase.PlayerTurn;
        floorManager?.ResetToFloor1();
        Debug.Log($"[GameManager] Restarted — HP: {GameConfig.PlayerMaxHP}/{GameConfig.PlayerMaxHP}");
    }

    // --- Enemy registry ---

    public bool IsOccupiedByEnemy(Vector2Int pos)
    {
        for (int i = 0; i < enemies.Count; i++)
            if (enemies[i] != null && enemies[i].GridPos == pos) return true;
        return false;
    }

    public void RegisterEnemy(EnemyController e)
    {
        if (e != null && !enemies.Contains(e)) enemies.Add(e);
    }

    public void UnregisterEnemy(EnemyController e) => enemies.Remove(e);

    public void ClearEnemyRegistry() => enemies.Clear();

    public EnemyController GetEnemyAt(Vector2Int pos)
    {
        for (int i = 0; i < enemies.Count; i++)
            if (enemies[i] != null && enemies[i].GridPos == pos) return enemies[i];
        return null;
    }
}
