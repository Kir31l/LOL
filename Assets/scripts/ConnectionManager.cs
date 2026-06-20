using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Handles create / join / disconnect flow via Photon Fusion.
/// Attach to a persistent GameObject that survives between scenes.
/// </summary>
public class ConnectionManager : MonoBehaviour, INetworkRunnerCallbacks
{
    [Header("Scene")]
    [SerializeField] private string lobbySceneName = "lobby"; // used in OnSceneLoadDone to verify correct scene loaded

    [Header("Player (prefab with NetworkObject + NetworkPlayer)")]
    [SerializeField] private GameObject playerPrefab;

    [Header("Fusion")]
    [SerializeField] private int maxPlayers = 4;

    [Header("Feedback (optional)")]
    [SerializeField] private BitmapText statusText;

    /// <summary>The active Fusion runner (null when disconnected).</summary>
    public static NetworkRunner Runner { get; private set; }

    /// <summary>4-digit room code displayed to players.</summary>
    public static string LobbyCode { get; private set; } = "";

    private bool isConnecting;
    private bool gameStarted;
    private System.Collections.Generic.List<PlayerRef> pendingSpawns = new();

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // Auto-find a status text if none assigned
        if (statusText == null)
        {
            var allTexts = FindObjectsByType<BitmapText>(FindObjectsSortMode.None);
            foreach (var t in allTexts)
            {
                if (t.gameObject.name.Contains("CODE") || t.gameObject.name.Contains("Status"))
                {
                    statusText = t;
                    break;
                }
            }
            if (statusText == null && allTexts.Length > 0)
                statusText = allTexts[0];
        }
    }

    // ─── Host ────────────────────────────────────────────────

    /// <summary>Create a new game as the host (server + local client).</summary>
    public async void CreateGame()
    {
        if (isConnecting) { SetStatus("Already connecting, please wait..."); return; }
        isConnecting = true;
        SetStatus("Initializing...");

        try
        {
            // 1. Generate a 4-digit room code
            string code = Random.Range(1000, 10000).ToString();

            // 2. Create a new NetworkRunner
            var go = new GameObject("Fusion Runner");
            DontDestroyOnLoad(go);
            var runner = go.AddComponent<NetworkRunner>();
            go.AddComponent<NetworkSceneManagerDefault>();
            go.AddComponent<NetworkObjectProviderDefault>();

            // 3. Add callbacks and provide input flag
            runner.AddCallbacks(this);

            // 4. Start as host
            SetStatus("Starting host...");
            var args = new StartGameArgs
            {
                GameMode = GameMode.Host,
                SessionName = code,
                PlayerCount = maxPlayers,
                SceneManager = runner.GetComponent<INetworkSceneManager>(),
                ObjectProvider = runner.GetComponent<INetworkObjectProvider>(),
            };

            var result = await runner.StartGame(args);

            if (!result.Ok)
                throw new System.Exception($"Failed to start game: {result.ShutdownReason}");

            // 5. Store references
            Runner = runner;
            LobbyCode = code;
            SetStatus($"YOUR CODE: {code}");

            Debug.Log($"[ConnectionManager] Host started, code: {code}. Loading lobby scene...");

            // 6. Load lobby scene (clients will sync automatically)
            await runner.LoadScene(SceneRef.FromIndex(1), LoadSceneMode.Single);
            Debug.Log($"[ConnectionManager] Lobby scene loaded successfully");
        }
        catch (System.Exception e)
        {
            SetStatus($"ERROR: {e.Message}");
            Debug.LogError($"[ConnectionManager] Create game failed: {e}");
            Cleanup();
        }
        finally
        {
            isConnecting = false;
        }
    }

    // ─── Client ──────────────────────────────────────────────

    /// <summary>Join an existing game via a 4-digit room code.</summary>
    public async void JoinGame(string code)
    {
        if (isConnecting) { SetStatus("Already connecting, please wait..."); return; }
        isConnecting = true;
        SetStatus("Initializing...");

        try
        {
            // 1. Create a new NetworkRunner
            var go = new GameObject("Fusion Runner");
            DontDestroyOnLoad(go);
            var runner = go.AddComponent<NetworkRunner>();
            go.AddComponent<NetworkSceneManagerDefault>();
            go.AddComponent<NetworkObjectProviderDefault>();

            // 2. Add callbacks
            runner.AddCallbacks(this);

            // 3. Start as client
            SetStatus($"Connecting to {code}...");
            Debug.Log($"[ConnectionManager] Client starting GameMode.Client, session='{code}'");
            var args = new StartGameArgs
            {
                GameMode = GameMode.Client,
                SessionName = code,
                SceneManager = runner.GetComponent<INetworkSceneManager>(),
                ObjectProvider = runner.GetComponent<INetworkObjectProvider>(),
            };

            var result = await runner.StartGame(args);

            Debug.Log($"[ConnectionManager] StartGame result: Ok={result.Ok} ShutdownReason={result.ShutdownReason}");

            if (!result.Ok)
                throw new System.Exception($"Failed to join game: {result.ShutdownReason}");

            // 4. Store reference
            Runner = runner;
            Debug.Log($"[ConnectionManager] Client connected to session: {code}");
        }
        catch (System.Exception e)
        {
            SetStatus($"ERROR: {e.Message}");
            Debug.LogError($"[ConnectionManager] Join game failed: {e}");
            Cleanup();
        }
        finally
        {
            isConnecting = false;
        }
    }

    // ─── Disconnect ──────────────────────────────────────────

    /// <summary>Disconnect and return to the menu scene.</summary>
    public void Disconnect()
    {
        Cleanup();
        SceneManager.LoadScene("menu");
    }

    private void Cleanup()
    {
        if (Runner != null)
        {
            Runner.Shutdown();
            if (Runner != null && Runner.gameObject != null)
                Destroy(Runner.gameObject);
            Runner = null;
        }
        LobbyCode = "";
        isConnecting = false;
        gameStarted = false;
        pendingSpawns.Clear();
    }

    // ─── INetworkRunnerCallbacks ─────────────────────────────

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"[ConnectionManager] OnPlayerJoined — player={player.PlayerId} IsServer={runner.IsServer} gameStarted={gameStarted}");

        if (!runner.IsServer) return;

        // If the lobby scene hasn't loaded yet (host starting up), defer the spawn
        // so the player is created in the correct scene with physics/colliders.
        if (!gameStarted)
        {
            Debug.Log($"[ConnectionManager] Deferring spawn for player {player.PlayerId} until scene loads");
            pendingSpawns.Add(player);
            return;
        }

        SpawnPlayer(runner, player);
    }

    private void SpawnPlayer(NetworkRunner runner, PlayerRef player)
    {
        if (playerPrefab == null)
        {
            Debug.LogError("[ConnectionManager] playerPrefab not assigned!");
            return;
        }

        var prefabNO = playerPrefab.GetComponent<NetworkObject>();
        if (prefabNO == null)
        {
            Debug.LogError($"[ConnectionManager] Prefab '{playerPrefab.name}' has no NetworkObject!");
            return;
        }

        // Spawn at a safe position away from obstacles
        // Lobby scene floor is typically around y=-3 to -4, so y=0 puts player above ground.
        // Will fall onto the floor via gravity.
        Vector3 spawnPos = new Vector3(0f, 2f, 0f);
        var spawned = runner.Spawn(prefabNO, spawnPos, Quaternion.identity, player);
        if (spawned == null)
        {
            Debug.LogError("[ConnectionManager] runner.Spawn returned null!");
            return;
        }

        var netPlayer = spawned.GetComponent<NetworkPlayer>();
        Debug.Log($"[ConnectionManager] Spawned player: obj={spawned.name} scene={spawned.gameObject.scene.name} hasNetworkPlayer={(netPlayer != null)}");
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"[ConnectionManager] Player left: {player.PlayerId}");
        if (!runner.IsServer) return;

        // Despawn the player's NetworkObject so it doesn't linger in the scene
        foreach (var netPlayer in FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None))
        {
            if (netPlayer.Object != null && netPlayer.Object.InputAuthority == player)
            {
                Debug.Log($"[ConnectionManager] Despawning player object for {player.PlayerId}");
                runner.Despawn(netPlayer.Object);
                break;
            }
        }
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        // Pack the local player's real input into the struct so Fusion sends it to the server.
        // Use GetButton() (continuous isDown) NOT GetButtonDown() because OnInput fires at
        // tick rate (64Hz) — GetButtonDown's single-frame edge can be missed between ticks.
        var data = new NetworkInputData();
        data.HorizontalDirection = Input.GetAxisRaw("Horizontal");
        data.Buttons.Set(MyButtons.Jump, Input.GetButton("Jump"));
        input.Set(data);
    }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        Debug.Log($"[ConnectionManager] Runner shutdown: {shutdownReason}");
        if (runner == Runner)
            Runner = null;
    }

    public void OnConnectedToServer(NetworkRunner runner)
    {
        Debug.Log($"[ConnectionManager] Connected to server");
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        Debug.Log($"[ConnectionManager] Disconnected from server: {reason}");
        Cleanup();
    }

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
    {
        request.Accept();
    }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        Debug.LogError($"[ConnectionManager] Connect failed: {reason}");
    }

    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, System.Collections.Generic.List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, System.Collections.Generic.Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }

    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, System.ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadDone(NetworkRunner runner)
    {
        Debug.Log($"[ConnectionManager] Scene '{lobbySceneName}' load done: {SceneManager.GetActiveScene().name}");

        // Mark game as started — lobby is now the active scene with physics
        gameStarted = true;

        // Spawn all players that were deferred during startup
        if (runner.IsServer && pendingSpawns.Count > 0)
        {
            Debug.Log($"[ConnectionManager] Spawning {pendingSpawns.Count} deferred player(s)");
            foreach (var player in pendingSpawns)
                SpawnPlayer(runner, player);
            pendingSpawns.Clear();
        }
    }
    public void OnSceneLoadStart(NetworkRunner runner)
    {
        Debug.Log($"[ConnectionManager] Scene load starting...");
    }

    // ─── Helpers ─────────────────────────────────────────────

    private void SetStatus(string msg)
    {
        Debug.Log($"[ConnectionManager] {msg}");
        if (statusText != null)
            statusText.SetText(msg);
    }
}
