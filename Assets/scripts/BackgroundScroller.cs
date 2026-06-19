using UnityEngine;
using UnityEngine.Tilemaps;

public class BackgroundScroller : MonoBehaviour
{
    [Header("Scroll Speed (world units per second)")]
    [SerializeField] private Vector2 scrollSpeed = new Vector2(0.5f, 0f);

    [Header("Wrap Size (auto-detected if possible)")]
    [SerializeField] private float wrapWidth;
    [SerializeField] private float wrapHeight;

    private float tileWidth;
    private float tileHeight;
    private Vector3 startPos;

    void Start()
    {
        startPos = transform.position;

        // Try SpriteRenderer first
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
        {
            Sprite s = sr.sprite;
            tileWidth = s.rect.width / s.pixelsPerUnit;
            tileHeight = s.rect.height / s.pixelsPerUnit;
            return;
        }

        // Try Tilemap
        Tilemap tm = GetComponent<Tilemap>();
        if (tm != null)
        {
            BoundsInt bounds = tm.cellBounds;
            Vector3 cellSize = tm.layoutGrid.cellSize;
            if (bounds.size.x > 0) tileWidth = bounds.size.x * cellSize.x;
            if (bounds.size.y > 0) tileHeight = bounds.size.y * cellSize.y;
            return;
        }

        // Fallback to manual values
        if (wrapWidth > 0f) tileWidth = wrapWidth;
        if (wrapHeight > 0f) tileHeight = wrapHeight;
    }

    void Update()
    {
        Vector3 pos = startPos + (Vector3)(scrollSpeed * Time.time);

        if (tileWidth > 0f && scrollSpeed.x != 0)
            pos.x = startPos.x + Mathf.Repeat(pos.x - startPos.x, tileWidth);
        if (tileHeight > 0f && scrollSpeed.y != 0)
            pos.y = startPos.y + Mathf.Repeat(pos.y - startPos.y, tileHeight);

        transform.position = pos;
    }
}
