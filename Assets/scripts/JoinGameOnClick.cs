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

    void OnMouseDown()
    {
        // Save username
        if (usernameInput != null)
            PlayerSession.Username = usernameInput.GetText();

        // Read Relay join code from Num field
        string code = codeInput != null ? codeInput.GetText() : "";
        if (string.IsNullOrEmpty(code))
        {
            Debug.LogWarning("No join code entered");
            return;
        }

        // Connect via Relay
        var cm = FindFirstObjectByType<ConnectionManager>();
        if (cm != null)
            cm.JoinGame(code);
    }
}
