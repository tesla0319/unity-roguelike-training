using UnityEngine;
using TMPro;

// World-space TMP label helper.
// Returns the TextMeshPro component so callers can store a reference
// for visibility control (GridManager.tileLabels).
//
// sortingOrder convention:
//   Tile sprites (Floor/Wall/Damage) : 0
//   Item sprites (Potion/Stair)      : 1
//   Character sprites (P/E)          : 2
//   Tile / Item labels  (S, H, D)    : 3
//   Character labels    (P,N,F,T)    : 4
public static class LabelUtil
{
    private const float RectSize = 0.85f;

    // isCharacter = false → tile/item label (SO 3)
    // isCharacter = true  → character label (SO 4)
    public static TextMeshPro AddLabel(Transform parent, string text, Color color,
                                       bool isCharacter = false)
    {
        var go = new GameObject("Label");
        go.transform.SetParent(parent);
        go.transform.localPosition    = Vector3.zero;
        go.transform.localScale       = Vector3.one;
        go.transform.localEulerAngles = Vector3.zero;

        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text      = text;
        tmp.color     = color;
        tmp.alignment = TextAlignmentOptions.Center;

        tmp.sortingOrder = isCharacter ? 4 : 3;

        tmp.enableAutoSizing = true;
        tmp.fontSizeMin      = 0.1f;
        tmp.fontSizeMax      = 500f;
        tmp.overflowMode     = TextOverflowModes.Overflow;

        tmp.rectTransform.sizeDelta = new Vector2(RectSize, RectSize);

        tmp.outlineWidth = 0.25f;
        tmp.outlineColor = new Color32(0, 0, 0, 220);

        return tmp;
    }
}
