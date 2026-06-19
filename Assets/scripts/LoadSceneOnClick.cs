using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadSceneOnClick : MonoBehaviour
{
    [Header("Optional: auto-save username before loading")]
    [SerializeField] private BitmapInputField inputField;

    void OnMouseDown()
    {
        // Save username from input field if one is set
        if (inputField != null && !string.IsNullOrEmpty(inputField.GetText()))
            PlayerSession.Username = inputField.GetText();

        FindFirstObjectByType<ConnectionManager>()?.CreateGame();
    }
}
