using UnityEngine;

public class Hud : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BitmapText scoreText;
    [SerializeField] private BitmapText codeText;
    [SerializeField] private Vector2 padding = new Vector2(0.5f, 0.5f);

    private static Hud instance;

    private void Awake()
    {
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        ScoreManager.OnScoreChanged += UpdateScore;
        UpdateScore(ScoreManager.Score);
        UpdateCode();
    }

    private void OnDisable()
    {
        ScoreManager.OnScoreChanged -= UpdateScore;
    }

    private void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        // Position at top-left of camera viewport
        Vector3 topLeft = cam.ViewportToWorldPoint(new Vector3(0f, 1f, 0f));
        transform.position = new Vector3(
            topLeft.x + padding.x,
            topLeft.y - padding.y,
            0f
        );
    }

    private void UpdateScore(int score)
    {
        if (scoreText != null)
        {
            scoreText.SetText($"SCORE {score:D4}");
        }
    }

    private void UpdateCode()
    {
        if (codeText != null && !string.IsNullOrEmpty(ConnectionManager.LobbyCode))
        {
            codeText.SetText($"{ConnectionManager.LobbyCode}");
        }
    }
}
