using UnityEngine;

public class EnemyController : MonoBehaviour
{
    public Vector2Int GridPos { get; private set; }
    public int HP { get; private set; }

    private GridManager gridManager;

    private static readonly Color EnemyColor = new Color(0.9f, 0.2f, 0.2f);

    // Called by FloorManager immediately after AddComponent.
    public void Initialize(Vector2Int startPos, GridManager gm)
    {
        gridManager = gm;
        GridPos     = startPos;
        HP          = GameConfig.EnemyHP;
        SyncTransform();
        EnsureSpriteRenderer();
    }

    public void TakeDamage(int amount)
    {
        HP = Mathf.Max(0, HP - amount);
        Debug.Log($"[Combat] Enemy at {GridPos} takes {amount} damage. HP: {HP}/{GameConfig.EnemyHP}");
        if (HP <= 0) Die();
    }

    private void Die()
    {
        Debug.Log($"[Combat] Enemy at {GridPos} defeated!");
        GameManager.Instance?.AddKill();
        GameManager.Instance?.UnregisterEnemy(this);
        Destroy(gameObject);
    }

    // Called once per enemy phase by GameManager.RunEnemyTurn.
    public void TakeTurn(Vector2Int playerPos)
    {
        int dist = ManhattanDist(GridPos, playerPos);

        if (dist == 1)
        {
            GameManager.Instance?.DamagePlayer(GameConfig.EnemyATK);
        }
        else if (dist <= GameConfig.EnemyChaseRange)
        {
            ChasePlayer(playerPos);
        }
        // Outside chase range: wait (wander behaviour added in later phase).
    }

    private void ChasePlayer(Vector2Int playerPos)
    {
        int dx = playerPos.x - GridPos.x;
        int dy = playerPos.y - GridPos.y;

        // Build axis-aligned unit vectors (zero if already aligned on that axis).
        var hDir = dx != 0 ? new Vector2Int((int)Mathf.Sign(dx), 0) : Vector2Int.zero;
        var vDir = dy != 0 ? new Vector2Int(0, (int)Mathf.Sign(dy)) : Vector2Int.zero;

        Vector2Int primary, secondary;
        if      (Mathf.Abs(dx) > Mathf.Abs(dy)) { primary = hDir; secondary = vDir; }
        else if (Mathf.Abs(dy) > Mathf.Abs(dx)) { primary = vDir; secondary = hDir; }
        else // |dx| == |dy| — random axis priority (spec §9.2)
        {
            if (Random.value < 0.5f) { primary = hDir; secondary = vDir; }
            else                     { primary = vDir; secondary = hDir; }
        }

        if (primary   != Vector2Int.zero && TryMoveTo(GridPos + primary))   return;
        if (secondary != Vector2Int.zero)                                     TryMoveTo(GridPos + secondary);
    }

    private bool TryMoveTo(Vector2Int next)
    {
        if (!gridManager.IsWalkable(next)) return false;
        if (GameManager.Instance != null && GameManager.Instance.IsOccupiedByEnemy(next)) return false;
        GridPos = next;
        SyncTransform();

        // DEBUG: confirm damage floor does NOT affect enemy HP (spec §12 MVP).
        if (gridManager.GetTile(GridPos) == TileType.Damage)
            Debug.Log($"[Debug] Enemy stepped on damage floor at {GridPos}. HP: {HP}/{GameConfig.EnemyHP} (no damage — immune per spec)");

        return true;
    }

    private void SyncTransform() =>
        transform.position = GridManager.GridToWorld(GridPos);

    private static int ManhattanDist(Vector2Int a, Vector2Int b) =>
        Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

    private void EnsureSpriteRenderer()
    {
        if (GetComponent<SpriteRenderer>() != null) return;
        var sr      = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite   = CreateUnitSprite();
        sr.color    = EnemyColor;
        sr.sortingOrder = 1;
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
