using System.Collections;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private GridManager  gridManager;
    [SerializeField] private FloorManager floorManager;

    public Vector2Int GridPos { get; private set; }
    public int HP      { get; private set; }
    public int MaxHP   => GameConfig.PlayerMaxHP;
    public int Potions { get; private set; }

    private static readonly Color PlayerColor = new Color(0.2f, 0.45f, 0.9f);

    // Animation timing constants
    private const float SlideTime = 0.10f;  // movement slide duration (s)
    private const float FlashTime = 0.18f;  // damage flash duration (s)

    // isAnimating blocks all movement/action input while player is sliding.
    // Input during enemy phase is blocked separately via GameManager.IsPlayerTurn.
    private bool      isAnimating   = false;
    private Coroutine flashCoroutine;

    // -------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------

    private void Start()
    {
        if (gridManager  == null) gridManager  = FindObjectOfType<GridManager>();
        if (floorManager == null) floorManager = FindObjectOfType<FloorManager>();

        if (gridManager  == null) Debug.LogError("[PlayerController] GridManager not found.");
        if (floorManager == null) Debug.LogWarning("[PlayerController] FloorManager not found.");

        InitHP();
        EnsureSpriteRenderer();
        Spawn(gridManager.GetRandomFloorPosition());
    }

    private void Update() => HandleInput();

    // -------------------------------------------------------
    // Input
    // -------------------------------------------------------

    private void HandleInput()
    {
        // R is always available regardless of game state or animation.
        if (Input.GetKeyDown(KeyCode.R))
        {
            if (GameManager.Instance != null) GameManager.Instance.ResetGame();
            else Restart();
            return;
        }

        // Gate: player turn AND not mid-slide.
        if (GameManager.Instance != null && !GameManager.Instance.IsPlayerTurn) return;
        if (isAnimating) return;

        if      (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))    TryMove(Vector2Int.up);
        else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))  TryMove(Vector2Int.down);
        else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))  TryMove(Vector2Int.left);
        else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) TryMove(Vector2Int.right);
        else if (Input.GetKeyDown(KeyCode.H))                                          TryUsePotion();
    }

    // -------------------------------------------------------
    // Movement & actions
    // -------------------------------------------------------

    private void TryMove(Vector2Int delta)
    {
        Vector2Int next = GridPos + delta;

        // Enemy present → attack in place (no movement slide needed).
        EnemyController target = GameManager.Instance?.GetEnemyAt(next);
        if (target != null)
        {
            AttackEnemy(target);
            NotifyTurnComplete();
            return;
        }

        if (!gridManager.IsWalkable(next)) return;

        // Logical position updates immediately — visual catches up via slide.
        GridPos     = next;
        isAnimating = true;
        StartCoroutine(SlideThenComplete());
    }

    // Slides the sprite to GridPos, then resolves tile events and advances the turn.
    // Deferring tile events ensures the player is visually at the destination first.
    private IEnumerator SlideThenComplete()
    {
        yield return StartCoroutine(SlideToGrid(GridPos));
        isAnimating = false;

        // CheckTileEvent returns true for stairs (no turn consumed).
        if (!CheckTileEvent())
            NotifyTurnComplete();
    }

    private void AttackEnemy(EnemyController target)
    {
        Debug.Log($"[Combat] Player attacks {target.EnemyType} at {target.GridPos} for {GameConfig.PlayerATK} damage.");
        target.TakeDamage(GameConfig.PlayerATK);
    }

    private void NotifyTurnComplete()
    {
        if (GameManager.Instance == null)
            Debug.LogWarning("[PlayerController] GameManager not found — enemy turn skipped.");
        else
            GameManager.Instance.OnPlayerActionComplete();
    }

    // Returns true if a stair was used (skips turn consumption).
    private bool CheckTileEvent()
    {
        TileType tile = gridManager.GetTile(GridPos);

        if (tile == TileType.Potion)  TryPickUpPotion();
        if (tile == TileType.Damage)  GameManager.Instance?.DamagePlayer(GameConfig.DamageFloorDamage);

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

    private void TryUsePotion()
    {
        if (Potions <= 0 || HP >= MaxHP) return;
        Potions--;
        HP = Mathf.Min(HP + GameConfig.PotionHealAmount, MaxHP);
        Debug.Log($"[Item] Used potion. HP: {HP}/{MaxHP}, Potions: {Potions}/{GameConfig.PotionMaxStock}");
        NotifyTurnComplete();
    }

    // -------------------------------------------------------
    // Public API
    // -------------------------------------------------------

    // Teleport (instant) — used for spawn and floor transitions.
    // Stops any in-progress animations so state is always clean.
    public void Spawn(Vector2Int pos)
    {
        StopAllCoroutines();
        isAnimating = false;
        RestoreSpriteColor();
        GridPos = pos;
        SyncTransform();
    }

    public void InitHP()
    {
        HP      = GameConfig.PlayerMaxHP;
        Potions = 0;
    }

    // HP reduction + damage flash. Logging and GameOver handled by GameManager.DamagePlayer.
    public void TakeDamage(int amount)
    {
        HP = Mathf.Max(0, HP - amount);
        if (flashCoroutine != null) StopCoroutine(flashCoroutine);
        flashCoroutine = StartCoroutine(FlashRoutine());
    }

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

    // -------------------------------------------------------
    // Visual effects (coroutines)
    // -------------------------------------------------------

    // Lerp transform.position toward the world position of targetGrid.
    private IEnumerator SlideToGrid(Vector2Int targetGrid)
    {
        Vector3 start   = transform.position;
        Vector3 end     = GridManager.GridToWorld(targetGrid);
        float   elapsed = 0f;

        while (elapsed < SlideTime)
        {
            elapsed           += Time.deltaTime;
            transform.position = Vector3.Lerp(start, end, Mathf.Clamp01(elapsed / SlideTime));
            yield return null;
        }
        transform.position = end; // snap to exact position
    }

    // Briefly flash white to indicate damage received.
    private IEnumerator FlashRoutine()
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr == null) { flashCoroutine = null; yield break; }
        sr.color = Color.white;
        yield return new WaitForSeconds(FlashTime);
        RestoreSpriteColor();
        flashCoroutine = null;
    }

    private void RestoreSpriteColor()
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = PlayerColor;
    }

    // -------------------------------------------------------
    // Helpers
    // -------------------------------------------------------

    // Instant snap — only for spawn / restart (never mid-game movement).
    private void SyncTransform() => transform.position = GridManager.GridToWorld(GridPos);

    private void EnsureSpriteRenderer()
    {
        if (GetComponent<SpriteRenderer>() != null) return;
        var sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite       = CreateUnitSprite();
        sr.color        = PlayerColor;
        sr.sortingOrder = 2;
        LabelUtil.AddLabel(transform, "P", Color.white, true);
    }

    private static Sprite CreateUnitSprite()
    {
        const int res = 16;
        var tex    = new Texture2D(res, res) { filterMode = FilterMode.Point };
        var pixels = new Color[res * res];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), res);
    }
}
