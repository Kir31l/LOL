using UnityEngine;

/// <summary>
/// Click handler for the CREATE button.
/// Saves the username and starts a hosted game via Relay + Lobby.
/// </summary>
public class CreateGameOnClick : MonoBehaviour
{
    [Header("Inputs")]
    [SerializeField] private BitmapInputField usernameInput;

    /// <summary>Optional text to show feedback.</summary>
    [SerializeField] private BitmapText feedbackText;

    private void Start()
    {
        // Auto-find a feedback text if none assigned
        if (feedbackText == null)
        {
            var allTexts = FindObjectsByType<BitmapText>(FindObjectsSortMode.None);
            foreach (var t in allTexts)
            {
                if (t.gameObject.name.Contains("CODE") || t.gameObject.name.Contains("Status"))
                {
                    feedbackText = t;
                    break;
                }
            }
            if (feedbackText == null && allTexts.Length > 0)
                feedbackText = allTexts[0];
        }
    }

    void OnMouseDown()
    {
        // Save username
        if (usernameInput != null)
            PlayerSession.Username = usernameInput.GetText();

        Debug.Log("[CreateGameOnClick] Starting host...");

        // Start hosting via ConnectionManager
        var cm = FindFirstObjectByType<ConnectionManager>();
        if (cm != null)
            cm.CreateGame();
        else
            ShowFeedback("NO CONNECTION MANAGER");
    }

    private void ShowFeedback(string msg)
    {
        Debug.Log($"[CreateGameOnClick] {msg}");
        if (feedbackText != null)
            feedbackText.SetText(msg);
    }
}
