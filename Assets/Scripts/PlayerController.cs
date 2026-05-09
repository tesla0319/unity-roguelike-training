using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private GridManager gridManager;
    [SerializeField] private FloorManager floorManager;

    public Vector2Int GridPos { get; private set; }
    public int HP      { get; private set; }
    public int MaxHP   => GameConfig.PlayerMaxHP;
    public int Potions { get; private set; }

    private static readonly Color PlayerColor = new Color(0.2f, 0.45f, 0.9f);

    private void Start()
    {
        if (gridManager == null)
            gridManager = FindObjectOfType<GridManager>();
        if (floorManager == null)
            floorManager = FindObjectOfType<FloorManager>();

        if (gridManager == null)
            Debug.LogError("[PlayerController] GridManager not found in scene.");
        if (floorManager == null)
            Debug.LogWarning("[PlayerController] FloorManager not found. Add a FloorManager component to the scene.");

        InitHP();
        EnsureSpriteRenderer();
        Spawn(gridManager.GetRandomFloorPosition());
    }

    private void Update()
    {
        HandleInput();
    }

    // --- Input ---

    private void HandleInput()
    {
        // R works in any game state (spec §8.1).
        if (Input.GetKeyDown(KeyCode.R))
        {
            if (GameManager.Instance != null) GameManager.Instance.ResetGame();
            else Restart();
            return;
        }

        // All other inputs require the player turn.
        if (GameManager.Instance != null && !GameManager.Instance.IsPlayerTurn) return;

        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
            TryMove(Vector2Int.up);
        else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
            TryMove(Vector2Int.down);
        else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
            TryMove(Vector2Int.left);
        else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
            TryMove(Vector2Int.right);
        else if (Input.GetKeyDown(KeyCode.H))
            TryUsePotion();
    }

    private void TryMove(Vector2Int delta)
    {
        Vector2Int next = GridPos + delta;

        // Enemy at next tile → attack instead of move (spec §10.2).
        EnemyController target = GameManager.Instance?.GetEnemyAt(next);
        if (target != null)
        {
            AttackEnemy(target);
            NotifyTurnComplete();
            return;
        }

        if (!gridManager.IsWalkable(next)) return;

        GridPos = next;
        SyncTransform();

        // Stair transitions don't consume a turn (spec §7.2).
        if (!CheckTileEvent())
            NotifyTurnComplete();
    }

    private void AttackEnemy(EnemyController target)
    {
        int damage = GameConfig.PlayerATK;
        Debug.Log($"[Combat] Player attacks enemy at {target.GridPos} for {damage} damage.");
        target.TakeDamage(damage);
    }

    private void NotifyTurnComplete()
    {
        if (GameManager.Instance == null)
            Debug.LogWarning("[PlayerController] GameManager not found — enemy turn skipped. " +
                             "Add a GameManager GameObject with GameManager component to the scene.");
        else
            GameManager.Instance.OnPlayerActionComplete();
    }

    // Returns true only for stair (floor transition skips turn).
    // Potion pickup and damage floor are side effects of movement — turn is still consumed.
    private bool CheckTileEvent()
    {
        TileType tile = gridManager.GetTile(GridPos);

        // Auto-pickup on entering a Potion tile (spec §11.1).
        if (tile == TileType.Potion)
            TryPickUpPotion();

        // Take damage when stepping on a damage floor (spec §12).
        // DamagePlayer handles GameOver detection; if State becomes GameOver,
        // OnPlayerActionComplete will be a no-op.
        if (tile == TileType.Damage)
            GameManager.Instance?.DamagePlayer(GameConfig.DamageFloorDamage);

        if (tile == TileType.Stair)
        {
            if (floorManager == null)
            {
                Debug.LogWarning("[PlayerController] Stair reached but FloorManager is null.");
                return false;
            }
            floorManager.GoToNextFloor();
            return true;
        }

        return false;
    }

    // Potion auto-pickup — only removes from map if inventory has space (spec §11.1).
    private void TryPickUpPotion()
    {
        if (Potions >= GameConfig.PotionMaxStock)
        {
            Debug.Log($"[Item] Potion not picked up — inventory full ({Potions}/{GameConfig.PotionMaxStock}).");
            return;
        }
        if (gridManager.TryConsumePotion(GridPos))
        {
            Potions++;
            Debug.Log($"[Item] Picked up potion. Potions: {Potions}/{GameConfig.PotionMaxStock}");
        }
    }

    // H key — consumes a turn only when HP < max and potions > 0 (spec §7.2).
    private void TryUsePotion()
    {
        if (Potions <= 0 || HP >= MaxHP) return;
        Potions--;
        HP = Mathf.Min(HP + GameConfig.PotionHealAmount, MaxHP);
        Debug.Log($"[Item] Used potion. HP: {HP}/{MaxHP}, Potions: {Potions}/{GameConfig.PotionMaxStock}");
        NotifyTurnComplete();
    }

    // --- Public API ---

    public void Spawn(Vector2Int pos)
    {
        GridPos = pos;
        SyncTransform();
    }

    public void InitHP()
    {
        HP      = GameConfig.PlayerMaxHP;
        Potions = 0;
    }

    // Pure HP reduction — logging and GameOver are handled by GameManager.DamagePlayer.
    public void TakeDamage(int amount) => HP = Mathf.Max(0, HP - amount);

    // Called by GameManager in later phases; usable standalone here.
    public void Restart()
    {
        InitHP();
        if (floorManager != null)
            floorManager.ResetToFloor1();
        else
        {
            gridManager.GenerateMap(GameConfig.Floors[0]);
            Spawn(gridManager.GetRandomFloorPosition());
        }
    }

    // --- Helpers ---

    private void SyncTransform()
    {
        transform.position = GridManager.GridToWorld(GridPos);
    }

    private void EnsureSpriteRenderer()
    {
        if (GetComponent<SpriteRenderer>() != null) return;

        var sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = CreateUnitSprite();
        sr.color = PlayerColor;
        sr.sortingOrder = 1; // render above tiles (sortingOrder 0)
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
}
