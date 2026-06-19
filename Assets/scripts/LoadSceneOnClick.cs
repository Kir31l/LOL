using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadSceneOnClick : MonoBehaviour
{
    public enum Mode
    {
        /// <summary>Load a scene directly (single-player).</summary>
        LoadScene,
        /// <summary>Start as host (server + client) and load the network scene.</summary>
        StartHost,
        /// <summary>Connect as a client to an existing host.</summary>
        StartClient,
    }

    [SerializeField] private Mode mode = Mode.LoadScene;
    [SerializeField] private string sceneName = "menu";

    [Header("Optional: auto-save username before loading")]
    [SerializeField] private BitmapInputField inputField;

    void OnMouseDown()
    {
        // Save username from input field if one is set
        if (inputField != null && !string.IsNullOrEmpty(inputField.GetText()))
            PlayerSession.Username = inputField.GetText();

        switch (mode)
        {
            case Mode.LoadScene:
                SceneManager.LoadScene(sceneName);
                break;
            case Mode.StartHost:
                FindFirstObjectByType<ConnectionManager>()?.CreateGame();
                break;
            case Mode.StartClient:
                FindFirstObjectByType<ConnectionManager>()?.JoinGame();
                break;
        }
    }
}
