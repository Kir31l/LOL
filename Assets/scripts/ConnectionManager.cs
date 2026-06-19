using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Handles create / join / disconnect flow from the menu scene.
/// Attach to a persistent GameObject that survives between scenes.
/// </summary>
public class ConnectionManager : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string lobbySceneName = "lobby";

    // The UnityTransport address is set in the inspector on the NetworkManager.
    // For Join, the user enters an IP via the menu UI, which updates the transport.

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>Create a new game (host = server + local client).</summary>
    public void CreateGame()
    {
        // Generate a 4-digit room code before the lobby loads
        RoomCodeManager.GenerateCode();

        // Tell the host to load the lobby scene once the server is up
        NetworkManager.Singleton.OnServerStarted += OnServerStarted;
        NetworkManager.Singleton.StartHost();
    }

    /// <summary>Join an existing game at the given IP.</summary>
    public void JoinGame(string ip)
    {
        SetAddress(ip);
        NetworkManager.Singleton.StartClient();
    }

    /// <summary>Join at the default IP (set in UnityTransport inspector).</summary>
    public void JoinGame()
    {
        NetworkManager.Singleton.StartClient();
    }

    /// <summary>Disconnect and return to the menu scene.</summary>
    public void Disconnect()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.Shutdown();
        SceneManager.LoadScene("menu");
    }

    private void OnServerStarted()
    {
        NetworkManager.Singleton.OnServerStarted -= OnServerStarted;

        // Server loads the lobby scene — all clients follow
        NetworkManager.Singleton.SceneManager.LoadScene(lobbySceneName, LoadSceneMode.Single);
    }

    /// <summary>
    /// Try to set the UnityTransport address by direct reference.
    /// If the transport type is unknown (future versions), falls back to reflection.
    /// </summary>
    private static void SetAddress(string ip)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        // Direct approach for UnityTransport (NGO 2.x)
        var transport = nm.NetworkConfig.NetworkTransport;
        if (transport == null) return;

        // UnityTransport stores its connection data in a public field.
        // Here we use reflection for cross-version compatibility.
        var dataField = transport.GetType().GetField("ConnectionData");
        if (dataField == null) return;

        var data = dataField.GetValue(transport);
        var addressField = data.GetType().GetField("Address");
        if (addressField != null)
            addressField.SetValue(data, ip);
    }
}
