using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    private FloorManager     floorManager;
    private PlayerController player;

    // HUD
    private TextMeshProUGUI hpText;
    private TextMeshProUGUI floorText;
    private TextMeshProUGUI turnText;
    private TextMeshProUGUI scoreText;
    private TextMeshProUGUI potionsText;

    // Result panels
    private GameObject      gameOverPanel;
    private TextMeshProUGUI gameOverScoreText;
    private GameObject      clearPanel;
    private TextMeshProUGUI clearScoreText;

    // -------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------

    private void Awake()
    {
        Debug.Log("[UIManager] Awake — building UI.");
        BuildUI();
    }

    private void Start()
    {
        floorManager = FindObjectOfType<FloorManager>();
        player       = FindObjectOfType<PlayerController>();

        if (floorManager == null) Debug.LogWarning("[UIManager] FloorManager not found.");
        if (player       == null) Debug.LogWarning("[UIManager] PlayerController not found.");
    }

    private void Update()
    {
        if (GameManager.Instance == null) return;
        RefreshHUD();

        var state = GameManager.Instance.State;
        gameOverPanel.SetActive(state == GameState.GameOver);
        clearPanel.SetActive(state == GameState.Clear);
    }

    // -------------------------------------------------------
    // HUD refresh (called every frame — text update only)
    // -------------------------------------------------------

    private void RefreshHUD()
    {
        var gm    = GameManager.Instance;
        int floor = floorManager != null ? floorManager.CurrentFloor : 1;
        int score = gm.GetScore(floor);
        int hp    = player != null ? player.HP      : 0;
        int pots  = player != null ? player.Potions : 0;

        hpText.text      = $"HP: {hp} / {GameConfig.PlayerMaxHP}";
        floorText.text   = $"Floor: {floor} / {GameConfig.MaxFloor}";
        turnText.text    = $"Turn: {gm.TurnCount}";
        scoreText.text   = $"Score: {score}";
        potionsText.text = $"Potions: {pots} / {GameConfig.PotionMaxStock}";

        gameOverScoreText.text = $"Score: {score}";
        clearScoreText.text    = $"Score: {score}";
    }

    // -------------------------------------------------------
    // UI construction
    // KEY RULE: AddComponent<RectTransform>() BEFORE SetParent()
    // -------------------------------------------------------

    private void BuildUI()
    {
        Canvas canvas = CreateCanvas();
        Debug.Log($"[UIManager] Canvas created: {canvas.gameObject.name}");

        BuildHUD(canvas);
        Debug.Log("[UIManager] HUD built.");

        gameOverPanel = BuildResultPanel(canvas,
            "GAME OVER", new Color(0.95f, 0.25f, 0.25f), out gameOverScoreText);
        clearPanel    = BuildResultPanel(canvas,
            "CLEAR!",    new Color(0.20f, 0.90f, 0.30f), out clearScoreText);

        gameOverPanel.SetActive(false);
        clearPanel.SetActive(false);

        Debug.Log("[UIManager] UI build complete — HUD + 2 panels ready.");
    }

    private Canvas CreateCanvas()
    {
        var go     = new GameObject("UICanvas");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        go.AddComponent<CanvasScaler>();
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    // -------------------------------------------------------
    // HUD
    // -------------------------------------------------------

    private void BuildHUD(Canvas canvas)
    {
        // Correct order: RectTransform first, then SetParent
        var hud = new GameObject("HUD");
        var rt  = hud.AddComponent<RectTransform>();          // ← 先にRT追加
        hud.transform.SetParent(canvas.transform, false);     // ← 後でSetParent

        rt.anchorMin        = new Vector2(0f, 1f);
        rt.anchorMax        = new Vector2(0f, 1f);
        rt.pivot            = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(12f, -12f);
        rt.sizeDelta        = new Vector2(220f, 140f);

        // Semi-transparent background (confirms HUD is positioned correctly)
        var bg = hud.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.55f);

        const float lh = 26f;
        const float px = 8f;
        const float py = 6f;

        hpText      = MakeHUDLine(hud, "HP: --- / ---",      px,  -py);
        floorText   = MakeHUDLine(hud, "Floor: - / -",       px,  -py - lh);
        turnText    = MakeHUDLine(hud, "Turn: 0",            px,  -py - lh * 2f);
        scoreText   = MakeHUDLine(hud, "Score: 0",           px,  -py - lh * 3f);
        potionsText = MakeHUDLine(hud, "Potions: 0 / 2",     px,  -py - lh * 4f);
    }

    // Each HUD line: RectTransform added before SetParent
    private TextMeshProUGUI MakeHUDLine(GameObject parent, string initial, float xOff, float yOff)
    {
        var go = new GameObject("HUDLine");
        var rt = go.AddComponent<RectTransform>();             // ← 先にRT追加
        go.transform.SetParent(parent.transform, false);      // ← 後でSetParent

        rt.anchorMin        = new Vector2(0f, 1f);
        rt.anchorMax        = new Vector2(0f, 1f);
        rt.pivot            = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(xOff, yOff);
        rt.sizeDelta        = new Vector2(204f, 24f);

        var t = go.AddComponent<TextMeshProUGUI>();
        t.text      = initial;
        t.fontSize  = 14f;
        t.color     = Color.white;
        t.alignment = TextAlignmentOptions.Left;

        return t;
    }

    // -------------------------------------------------------
    // Result panels
    // -------------------------------------------------------

    private GameObject BuildResultPanel(Canvas canvas,
        string title, Color titleColor, out TextMeshProUGUI scoreOut)
    {
        var panel = new GameObject(title.Replace(" ", "") + "Panel");
        var rt    = panel.AddComponent<RectTransform>();       // ← 先にRT追加
        panel.transform.SetParent(canvas.transform, false);   // ← 後でSetParent

        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var bg = panel.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.78f);

        MakeCenteredText(panel, title,               52f, titleColor,                      90f, 640f, 72f);
        scoreOut =
        MakeCenteredText(panel, "Score: 0",          30f, Color.white,                      0f, 400f, 44f);
        MakeCenteredText(panel, "Press R to Restart",20f, new Color(0.80f, 0.80f, 0.80f), -80f, 400f, 32f);

        return panel;
    }

    private TextMeshProUGUI MakeCenteredText(GameObject parent,
        string initial, float size, Color color,
        float yOffset, float width, float height)
    {
        var go = new GameObject("Text");
        var rt = go.AddComponent<RectTransform>();             // ← 先にRT追加
        go.transform.SetParent(parent.transform, false);      // ← 後でSetParent

        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, yOffset);
        rt.sizeDelta        = new Vector2(width, height);

        var t = go.AddComponent<TextMeshProUGUI>();
        t.text      = initial;
        t.fontSize  = size;
        t.color     = color;
        t.alignment = TextAlignmentOptions.Center;

        return t;
    }
}
