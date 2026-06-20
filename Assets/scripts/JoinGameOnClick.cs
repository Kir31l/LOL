using UnityEngine;

/// <summary>
/// Click handler for the JOIN button.
/// Saves the username and connects via Relay using the code from the Num field.
/// </summary>
public class JoinGameOnClick : MonoBehaviour
{
    [Header("Inputs")]
    [SerializeField] private BitmapInputField usernameInput;
    [SerializeField] private BitmapInputField codeInput;

    /// <summary>Optional text to flash feedback (e.g. an error).</summary>
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

        // Read join code from field
        string code = codeInput != null ? codeInput.GetText().Trim() : "";
        if (string.IsNullOrEmpty(code))
        {
            ShowFeedback("ENTER A CODE");
            return;
        }

        // Show what we're doing
        ShowFeedback($"JOINING {code}...");

        // Connect via Relay
        var cm = FindFirstObjectByType<ConnectionManager>();
        if (cm != null)
            cm.JoinGame(code);
        else
            ShowFeedback("NO CONNECTION MANAGER");
    }

    private void ShowFeedback(string msg)
    {
        Debug.Log($"[JoinGameOnClick] {msg}");
        if (feedbackText != null)
            feedbackText.SetText(msg);
    }
}
