using System.Collections;
using UnityEngine;

public class EnemyController : MonoBehaviour
{
    public Vector2Int GridPos   { get; private set; }
    public int        HP        { get; private set; }
    public EnemyType  EnemyType { get; private set; }

    private int         maxHP;
    private int         atk;
    private GridManager gridManager;
    private Color       baseColor;

    private static readonly Color NormalColor = new Color(0.90f, 0.20f, 0.20f);
    private static readonly Color FastColor   = new Color(0.60f, 0.10f, 0.80f);
    private static readonly Color TankColor   = new Color(0.40f, 0.08f, 0.08f);

    // Animation timing constants
    private const float SlideTime = 0.12f;  // movement slide (fire-and-forget, runs during player turn)
    private const float FlashTime = 0.18f;  // damage flash
    private const float DeathTime = 0.25f;  // death shrink + fade

    private Coroutine flashCoroutine;
    private Coroutine slideCoroutine;

    // -------------------------------------------------------
    // Initialisation
    // -------------------------------------------------------

    public void Initialize(Vector2Int startPos, GridManager gm, EnemyType type = EnemyType.Normal)
    {
        gridManager = gm;
        GridPos     = startPos;
        EnemyType   = type;

        switch (type)
        {
            case EnemyType.Fast:
                maxHP = GameConfig.FastEnemyHP;
                atk   = GameConfig.FastEnemyATK;
                break;
            case EnemyType.Tank:
                maxHP = GameConfig.TankEnemyHP;
                atk   = GameConfig.TankEnemyATK;
                break;
            default:
                maxHP = GameConfig.EnemyHP;
                atk   = GameConfig.EnemyATK;
                break;
        }
        HP = maxHP;

        SyncTransform(); // instant snap on spawn

        baseColor = type == EnemyType.Fast ? FastColor
                  : type == EnemyType.Tank ? TankColor
                  : NormalColor;
        EnsureSpriteRenderer(baseColor);

        string label = type == EnemyType.Fast ? "F"
                     : type == EnemyType.Tank ? "T"
                     : "N";
        LabelUtil.AddLabel(transform, label, Color.white, true);
    }

    // -------------------------------------------------------
    // Combat
    // -------------------------------------------------------

    public void TakeDamage(int amount)
    {
        HP = Mathf.Max(0, HP - amount);
        Debug.Log($"[Combat] {EnemyType} at {GridPos} takes {amount} damage. HP: {HP}/{maxHP}");

        // Always flash on any damage hit, regardless of whether it is fatal.
        if (flashCoroutine != null) StopCoroutine(flashCoroutine);

        if (HP <= 0)
        {
            // Remove from game logic IMMEDIATELY so this enemy cannot take another turn.
            // The visual (flash → death animation) runs asynchronously afterwards.
            Debug.Log($"[Combat] {EnemyType} at {GridPos} defeated!");
            GameManager.Instance?.AddKill();
            GameManager.Instance?.UnregisterEnemy(this);
            if (slideCoroutine != null) { StopCoroutine(slideCoroutine); slideCoroutine = null; }
            flashCoroutine = StartCoroutine(FlashThenDestroy());
        }
        else
        {
            flashCoroutine = StartCoroutine(FlashRoutine());
        }
    }

    private void AttackPlayer()
    {
        Debug.Log($"[Combat] {EnemyType} at {GridPos} attacks player for {atk} damage.");
        GameManager.Instance?.DamagePlayer(atk);
    }

    // -------------------------------------------------------
    // Turn dispatch
    // -------------------------------------------------------

    public void TakeTurn(Vector2Int playerPos)
    {
        switch (EnemyType)
        {
            case EnemyType.Fast: TakeTurnFast(playerPos);   break;
            default:             TakeTurnNormal(playerPos); break;
        }
    }

    private void TakeTurnNormal(Vector2Int playerPos)
    {
        int dist = ManhattanDist(GridPos, playerPos);
        if      (dist == 1)                          AttackPlayer();
        else if (dist <= GameConfig.EnemyChaseRange) ChasePlayer(playerPos);
        else                                         Wander();
    }

    // Fast moves up to 2 steps per turn; attacks if it closes to distance 1 mid-turn.
    private void TakeTurnFast(Vector2Int playerPos)
    {
        int dist = ManhattanDist(GridPos, playerPos);
        if (dist == 1) { AttackPlayer(); return; }

        if (dist <= GameConfig.EnemyChaseRange)
        {
            ChasePlayer(playerPos);                      // step 1 (updates GridPos instantly)
            int dist2 = ManhattanDist(GridPos, playerPos);
            if      (dist2 == 1) AttackPlayer();         // now adjacent → attack
            else if (dist2 > 1)  ChasePlayer(playerPos); // step 2
        }
        else
            Wander();
    }

    // -------------------------------------------------------
    // Movement (logic is synchronous; visual is async)
    // -------------------------------------------------------

    private void ChasePlayer(Vector2Int playerPos)
    {
        int dx = playerPos.x - GridPos.x;
        int dy = playerPos.y - GridPos.y;

        var hDir = dx != 0 ? new Vector2Int((int)Mathf.Sign(dx), 0) : Vector2Int.zero;
        var vDir = dy != 0 ? new Vector2Int(0, (int)Mathf.Sign(dy)) : Vector2Int.zero;

        Vector2Int primary, secondary;
        if      (Mathf.Abs(dx) > Mathf.Abs(dy)) { primary = hDir; secondary = vDir; }
        else if (Mathf.Abs(dy) > Mathf.Abs(dx)) { primary = vDir; secondary = hDir; }
        else
        {
            if (Random.value < 0.5f) { primary = hDir; secondary = vDir; }
            else                     { primary = vDir; secondary = hDir; }
        }

        if (primary   != Vector2Int.zero && TryMoveTo(GridPos + primary))   return;
        if (secondary != Vector2Int.zero)                                     TryMoveTo(GridPos + secondary);
    }

    private void Wander()
    {
        if (Random.value >= GameConfig.EnemyWanderRate) return;
        var dirs = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        int start = Random.Range(0, 4);
        for (int i = 0; i < 4; i++)
            if (TryMoveTo(GridPos + dirs[(start + i) % 4])) return;
    }

    private bool TryMoveTo(Vector2Int next)
    {
        if (!gridManager.IsWalkable(next)) return false;
        if (GameManager.Instance != null && GameManager.Instance.IsOccupiedByEnemy(next)) return false;

        GridPos = next; // logical update is immediate

        // Visual slide is fire-and-forget — runs in background while player takes their next turn.
        // If Fast enemy calls TryMoveTo twice, the first slide is cancelled and one smooth slide plays.
        if (slideCoroutine != null) StopCoroutine(slideCoroutine);
        slideCoroutine = StartCoroutine(SlideToGrid(GridPos));

        // DEBUG: confirm damage floor does NOT affect enemy HP (spec §12 MVP).
        if (gridManager.GetTile(GridPos) == TileType.Damage)
            Debug.Log($"[Debug] {EnemyType} stepped on damage floor at {GridPos}. HP: {HP}/{maxHP} (no damage — immune per spec)");

        return true;
    }

    // Instant snap — only for initialisation (never mid-turn).
    private void SyncTransform() => transform.position = GridManager.GridToWorld(GridPos);

    private static int ManhattanDist(Vector2Int a, Vector2Int b) =>
        Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

    // -------------------------------------------------------
    // Visual effects (coroutines)
    // -------------------------------------------------------

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
        transform.position = end;
        slideCoroutine     = null;
    }

    // Fatal hit: flash white → death animation → Destroy.
    // Game-logic cleanup (AddKill / UnregisterEnemy) already happened in TakeDamage
    // before this coroutine started, so the enemy is safe to animate freely.
    private IEnumerator FlashThenDestroy()
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = Color.white;
        yield return new WaitForSeconds(FlashTime);
        flashCoroutine = null;
        yield return StartCoroutine(DeathAnimation());
    }

    private IEnumerator FlashRoutine()
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr == null) { flashCoroutine = null; yield break; }

        sr.color = Color.white;
        yield return new WaitForSeconds(FlashTime);

        // Null-check: enemy may have been destroyed (e.g. floor cleared) while flashing.
        if (sr != null) sr.color = baseColor;
        flashCoroutine = null;
    }

    // Shrink to zero scale + fade alpha, then Destroy.
    // Children (label GO) scale with the parent automatically.
    private IEnumerator DeathAnimation()
    {
        var     sr         = GetComponent<SpriteRenderer>();
        Vector3 startScale = transform.localScale;
        Color   startColor = sr != null ? sr.color : baseColor;
        float   elapsed    = 0f;

        while (elapsed < DeathTime)
        {
            elapsed += Time.deltaTime;
            float t  = Mathf.Clamp01(elapsed / DeathTime);

            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
            if (sr != null) sr.color = Color.Lerp(startColor, Color.clear, t);

            yield return null;
        }

        Destroy(gameObject);
    }

    // -------------------------------------------------------
    // Sprite setup
    // -------------------------------------------------------

    private void EnsureSpriteRenderer(Color color)
    {
        if (GetComponent<SpriteRenderer>() != null) return;
        var sr      = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite   = CreateUnitSprite();
        sr.color    = color;
        sr.sortingOrder = 2;
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
