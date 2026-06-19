using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[ExecuteAlways]
public class BitmapText : MonoBehaviour
{
    [Header("Texture Source (drag texture here, then click Auto-Fill)")]
    public Sprite textureSource;

    [Header("References (auto-filled from source)")]
    [SerializeField] private Sprite[] spriteSet;

    [Header("Settings")]
    [SerializeField] private string characters = "!\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLM";
    [SerializeField] private string text = "HELLO";
    [SerializeField] private float charSpacing = 1f;
    [SerializeField] private float lineSpacing = 12f;
    [SerializeField] private float pixelsPerUnit = 16f;
    [SerializeField] private int sortingOrder = 0;
    [TextArea]
    [SerializeField] private string unknownCharReplacement = "?";

    [Header("Debug")]
    [SerializeField] private bool showDebugLog;

    private List<SpriteRenderer> charRenderers = new List<SpriteRenderer>();

    private void OnEnable()
    {
        Rebuild();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Auto-rebuild in editor when values change
        if (Application.isPlaying == false)
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null && isActiveAndEnabled)
                    Rebuild();
            };
        }
    }

    [ContextMenu("Auto-Fill Sprites")]
    private void ContextAutoFill()
    {
        if (textureSource == null) return;
        string path = UnityEditor.AssetDatabase.GetAssetPath(textureSource);
        if (string.IsNullOrEmpty(path)) return;

        Sprite[] allSprites = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<Sprite>()
            .ToArray();

        // Order by y descending (top row first), then x ascending
        System.Array.Sort(allSprites, (a, b) =>
        {
            int yCmp = -a.rect.y.CompareTo(b.rect.y);
            if (yCmp != 0) return yCmp;
            return a.rect.x.CompareTo(b.rect.x);
        });

        if (allSprites.Length > 0)
        {
            spriteSet = allSprites;
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"BitmapText: Auto-filled {allSprites.Length} sprites from {textureSource.name}");
        }
    }
#endif

    public void SetText(string newText)
    {
        if (text != newText)
        {
            text = newText;
            Rebuild();
        }
    }

    public string GetText() => text;

    public void Rebuild()
    {
        // Remove old children
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
                Destroy(transform.GetChild(i).gameObject);
            else
                DestroyImmediate(transform.GetChild(i).gameObject);
#else
            Destroy(transform.GetChild(i).gameObject);
#endif
        }
        charRenderers.Clear();

        if (spriteSet == null || spriteSet.Length == 0)
            return;

        if (string.IsNullOrEmpty(text))
            return;

        // Debug mode: show sprite index grid and missing-char report, skip normal render
        if (showDebugLog)
        {
            ShowSpriteIndexGrid();
            DebugMissingChars();
            return;
        }

        // Build character-to-index map
        Dictionary<char, int> charMap = new Dictionary<char, int>();
        for (int i = 0; i < characters.Length; i++)
        {
            if (!charMap.ContainsKey(characters[i]))
                charMap[characters[i]] = i;
        }

        // Track line width to center text
        string[] splitLines = text.Split('\n');
        LineInfo[] lines = new LineInfo[splitLines.Length];
        float maxLineWidth = 0f;

        for (int lineIdx = 0; lineIdx < splitLines.Length; lineIdx++)
        {
            string line = splitLines[lineIdx];
            float lineWidth = 0f;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == ' ')
                {
                    lineWidth += charSpacing * 8f;
                    continue;
                }
                if (charMap.TryGetValue(c, out int spriteIdx) && spriteIdx < spriteSet.Length)
                {
                    Sprite s = spriteSet[spriteIdx];
                    float w = (s != null) ? s.rect.width / pixelsPerUnit : charSpacing * 8f;
                    lineWidth += w + charSpacing;
                }
                else
                {
                    lineWidth += charSpacing * 8f;
                }
            }
            if (lineWidth > maxLineWidth) maxLineWidth = lineWidth;
            lines[lineIdx] = new LineInfo { Text = line, Width = lineWidth };
        }

        float totalHeight = (lines.Length - 1) * lineSpacing;
        float startY = totalHeight * 0.5f;

        for (int lineIdx = 0; lineIdx < lines.Length; lineIdx++)
        {
            string line = lines[lineIdx].Text;
            float lineWidth = lines[lineIdx].Width;
            float cursorX = -lineWidth * 0.5f;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == ' ')
                {
                    cursorX += charSpacing * 8f;
                    continue;
                }

                Sprite s = null;
                if (charMap.TryGetValue(c, out int spriteIdx) && spriteIdx < spriteSet.Length)
                    s = spriteSet[spriteIdx];

                // Try replacement char if not found
                if (s == null && !string.IsNullOrEmpty(unknownCharReplacement))
                {
                    char rc = unknownCharReplacement[0];
                    if (charMap.TryGetValue(rc, out int rIdx) && rIdx < spriteSet.Length)
                        s = spriteSet[rIdx];
                }

                if (s == null) continue;

                GameObject go = new GameObject($"char_{c}_{i}");
                go.transform.SetParent(transform, false);

                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = s;
                sr.sortingOrder = sortingOrder;

                float charWidth = s.rect.width / pixelsPerUnit;
                float charHeight = s.rect.height / pixelsPerUnit;

                go.transform.localPosition = new Vector3(
                    cursorX + charWidth * 0.5f,
                    startY - lineIdx * lineSpacing,
                    0f
                );

                charRenderers.Add(sr);
                cursorX += charWidth + charSpacing;
            }
        }
    }

    private void DebugMissingChars()
    {
        Dictionary<char, int> charMap = new Dictionary<char, int>();
        for (int i = 0; i < characters.Length; i++)
        {
            if (!charMap.ContainsKey(characters[i]))
                charMap[characters[i]] = i;
        }

        var missing = new List<char>();
        foreach (char c in text)
        {
            if (c == ' ' || c == '\n') continue;
            if (!charMap.ContainsKey(c))
                missing.Add(c);
        }

        if (missing.Count > 0)
        {
            string msg = string.Join(" ", missing);
            Debug.LogWarning($"[BitmapText] Characters NOT in char map: '{msg}' — try updating the 'Characters' string in the Inspector");
        }
        else
        {
            Debug.Log($"[BitmapText] All text characters found in char map ✓");
        }
    }

    private void ShowSpriteIndexGrid()
    {
        int perRow = 10;
        for (int i = 0; i < spriteSet.Length; i++)
        {
            Sprite s = spriteSet[i];
            if (s == null) continue;

            GameObject go = new GameObject($"sprite_{i}");
            go.transform.SetParent(transform, false);

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s;
            sr.sortingOrder = sortingOrder;

            int row = i / perRow;
            int col = i % perRow;
            float x = col * (s.rect.width / pixelsPerUnit + charSpacing);
            float y = -row * lineSpacing;
            go.transform.localPosition = new Vector3(x, y, 0);
        }
        Debug.Log($"[BitmapText] ShowSpriteIndexGrid: {spriteSet.Length} sprites rendered");
    }

    private struct LineInfo
    {
        public string Text;
        public float Width;
    }
}
