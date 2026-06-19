using UnityEngine;

public class LivesDisplay : MonoBehaviour
{
    [Header("Sprites")]
    [SerializeField] private Sprite[] characterSprites; // 4 sprites, indexed by character

    [Header("Layout")]
    [SerializeField] private float iconSpacing = 0.6f;
    [SerializeField] private Vector2 startOffset = new Vector2(0f, 0f);

    private SpriteRenderer[] lifeIcons;

    private void Awake()
    {
        CreateIcons();
    }

    private void OnEnable()
    {
        LivesManager.OnLivesChanged += UpdateLives;
        UpdateLives(LivesManager.Lives);
    }

    private void OnDisable()
    {
        LivesManager.OnLivesChanged -= UpdateLives;
    }

    private void CreateIcons()
    {
        if (characterSprites == null || characterSprites.Length == 0)
            return;

        int characterIdx = PlayerSession.SelectedCharacter;
        Sprite sprite = characterSprites[Mathf.Clamp(characterIdx, 0, characterSprites.Length - 1)];

        lifeIcons = new SpriteRenderer[LivesManager.MaxLives];
        for (int i = 0; i < lifeIcons.Length; i++)
        {
            GameObject icon = new GameObject($"Life_{i}");
            icon.transform.SetParent(transform, false);
            icon.transform.localPosition = new Vector3(
                startOffset.x + i * iconSpacing,
                startOffset.y,
                0f
            );
            SpriteRenderer sr = icon.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 10;
            lifeIcons[i] = sr;
        }
    }

    private void UpdateLives(int lives)
    {
        if (lifeIcons == null) return;

        for (int i = 0; i < lifeIcons.Length; i++)
        {
            if (lifeIcons[i] != null)
                lifeIcons[i].enabled = i < lives;
        }
    }
}
