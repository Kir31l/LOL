using Fusion;
using Fusion.Addons.Physics;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
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

    // ─── Input Buffer ────────────────────────────────────────
    // Input System processes events during Update(), but Fusion's OnInput runs in FixedUpdate.
    // We sample input in Update() and store it here so OnInput always gets the latest state.
    private static NetworkInputData _bufferedInput = new();
    private static bool _hasBufferedInput = false;

    /// <summary>Central place to check if a key is down. Uses ReadValue instead of isPressed
    /// because Unity 6's FastKeyboard backend may report sub-threshold values that isPressed
    /// (threshold 0.5) misses but ReadValue catches with our 0.1 threshold.</summary>
    private static bool KeyDown(Keyboard kb, Key key)
    {
        return kb[key].ReadValue() > 0.1f;
    }

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);

        // Log Input System diagnostics at startup
        Debug.Log($"[InputDiag] InputSystem devices count: {InputSystem.devices.Count}");
        Debug.Log($"[InputDiag] InputSystem settings updateMode: {InputSystem.settings?.updateMode}");
        foreach (var dev in InputSystem.devices)
            Debug.Log($"[InputDiag]   Device: {dev.name} ({dev.GetType().Name}) layout={dev.layout} id={dev.deviceId} added={dev.added} enabled={dev.enabled}");

        // Subscribe to raw input events to check if keyboard events arrive at all
        InputSystem.onEvent += OnInputSystemEvent;
    }

    // ─── Raw event-based key state tracker ──────────────────
    // This bypasses Keyboard.current entirely. We read key values directly from
    // OS input events so we're not dependent on the FastKeyboard backend's state.
    // Key: KeyCode int, Value: true if pressed according to raw event data.
    private static readonly System.Collections.Generic.Dictionary<Key, float> _rawKeyValues = new();

    private void OnInputSystemEvent(InputEventPtr eventPtr, InputDevice device)
    {
        // Log keyboard events to confirm OS-level input arrives
        if (device is Keyboard keyboard)
        {
            if (++_inputEventCount % 60 == 0)
            {
                Debug.Log($"[InputEvent] #{_inputEventCount} on {device.name} size={eventPtr.sizeInBytes}");
            }

            // Process state events to track individual key values directly from raw event data.
            // This is our failsafe: if Keyboard.current[key].ReadValue() returns wrong values,
            // this path reads the "truth" straight from the OS-level state event.
            if (eventPtr.type == StateEvent.Type || eventPtr.type == DeltaStateEvent.Type)
            {
                try
                {
                    foreach (var keyCode in new[] { Key.D, Key.A, Key.Space, Key.W, Key.UpArrow, Key.DownArrow, Key.LeftArrow, Key.RightArrow })
                    {
                        var ctrl = keyboard[keyCode];
                        if (ctrl != null)
                        {
                            // ReadUnprocessedValueFromEvent reads the value directly from the event's
                            // state data, bypassing any cached/processed state on the control.
                            if (ctrl.ReadUnprocessedValueFromEvent(eventPtr, out float val))
                                _rawKeyValues[keyCode] = val;
                        }
                    }
                }
                catch { /* event may not contain these controls; skip silently */ }
            }
        }
    }

    /// <summary>Read key state from raw events instead of Keyboard.current.</summary>
    private static bool RawKeyDown(Key key)
    {
        if (_rawKeyValues.TryGetValue(key, out float val))
            return val > 0.1f;
        return false;
    }

    private int _inputEventCount;

    private bool _keyboardDiagDone;

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            // One-shot keyboard layout diagnostic
            if (!_keyboardDiagDone)
            {
                _keyboardDiagDone = true;
                Debug.Log($"[InputDiag] Keyboard type={keyboard.GetType().Name} allKeys={keyboard.allKeys.Count} anyKeyReadValue={keyboard.anyKey.ReadValue()} anyKeyIsPressed={keyboard.anyKey.isPressed}");
                // Log first 20 key names
                string first20 = "";
                for (int i = 0; i < keyboard.allKeys.Count && i < 60; i++)
                    first20 += $"{keyboard.allKeys[i].keyCode} ";
                Debug.Log($"[InputDiag] Keys: {first20}");
            }
            _bufferedInput.HorizontalDirection = 0f;
            if (KeyDown(keyboard, Key.D) || KeyDown(keyboard, Key.RightArrow))
                _bufferedInput.HorizontalDirection = 1f;
            if (KeyDown(keyboard, Key.A) || KeyDown(keyboard, Key.LeftArrow))
                _bufferedInput.HorizontalDirection = -1f;
            _bufferedInput.Buttons.Set(MyButtons.Jump, KeyDown(keyboard, Key.Space));
            _hasBufferedInput = true;

            // Log state every ~60 frames for diagnosis
            if (Time.frameCount % 60 == 0)
            {
                // Also scan ALL keys when anyKey is pressed to find what's registering
                string pressedKeys = "";
                float anyKeyVal = keyboard.anyKey.ReadValue();
                if (anyKeyVal > 0.1f)
                {
                    foreach (var k in keyboard.allKeys)
                    {
                        if (k.ReadValue() > 0.1f)
                        {
                            if (pressedKeys.Length > 0) pressedKeys += ", ";
                            pressedKeys += $"{k.keyCode}({k.displayName}) val={k.ReadValue():F3}";
                            if (pressedKeys.Length > 140) { pressedKeys += "..."; break; }
                        }
                    }
                }

                Debug.Log($"[InputDiag frame={Time.frameCount}] Update() buffer: " +
                          $"D={KeyDown(keyboard, Key.D)}(raw={RawKeyDown(Key.D)}) " +
                          $"A={KeyDown(keyboard, Key.A)}(raw={RawKeyDown(Key.A)}) " +
                          $"Space={KeyDown(keyboard, Key.Space)}(raw={RawKeyDown(Key.Space)}) " +
                          $"W={KeyDown(keyboard, Key.W)}(raw={RawKeyDown(Key.W)}) " +
                          $"UP={KeyDown(keyboard, Key.UpArrow)} DOWN={KeyDown(keyboard, Key.DownArrow)} " +
                          $"LShift={KeyDown(keyboard, Key.LeftShift)} RShift={KeyDown(keyboard, Key.RightShift)} " +
                          $"LCtrl={KeyDown(keyboard, Key.LeftCtrl)} LAlt={KeyDown(keyboard, Key.LeftAlt)} " +
                          $"anyKey_isPressed={keyboard.anyKey.isPressed} anyKey_ReadValue={anyKeyVal:F3} " +
                          $"focus={Application.isFocused} " +
                          $"devices={InputSystem.devices.Count} rawKeys={_rawKeyValues.Count}" +
                          (pressedKeys.Length > 0 ? $"  PRESSED: {pressedKeys}" : ""));
            }
        }
        else
        {
            _hasBufferedInput = false;
            if (Time.frameCount % 60 == 0)
                Debug.LogWarning($"[InputDiag frame={Time.frameCount}] No keyboard in Update()! devices={InputSystem.devices.Count} focus={Application.isFocused}");
        }
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
            go.AddComponent<RunnerSimulatePhysics2D>();

            // 3. Enable client physics simulation (default is Disabled — clients would not simulate physics)
            var physics2D = go.GetComponent<RunnerSimulatePhysics2D>();
            physics2D.ClientPhysicsSimulation = ClientPhysicsSimulation.SimulateForward;

            // 4. Add callbacks and provide input flag
            runner.AddCallbacks(this);

            // 5. Start as host
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
            go.AddComponent<RunnerSimulatePhysics2D>();

            // 2. Enable client physics simulation (default is Disabled — clients would not simulate physics)
            var physics2D = go.GetComponent<RunnerSimulatePhysics2D>();
            physics2D.ClientPhysicsSimulation = ClientPhysicsSimulation.SimulateForward;

            // 3. Add callbacks
            runner.AddCallbacks(this);

            // 5. Start as client
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
        var frameCount = Time.frameCount;

        // Priority 1: Use the buffered input from Update() — most reliable across Unity 6
        if (_hasBufferedInput)
        {
            // Log if buffer shows no movement but raw events say keys are pressed (diagnostic)
            if (_bufferedInput.HorizontalDirection == 0f && !_bufferedInput.Buttons.IsSet(MyButtons.Jump))
            {
                if (RawKeyDown(Key.D) || RawKeyDown(Key.A) || RawKeyDown(Key.Space))
                {
                    Debug.LogWarning($"[OnInput frame={frameCount}] Buffer=0 but RAW event shows key pressed! " +
                        $"Draw={RawKeyDown(Key.D)} Araw={RawKeyDown(Key.A)} Spaceraw={RawKeyDown(Key.Space)}");
                }
            }
            input.Set(_bufferedInput);
            return;
        }

        // Priority 2: Read from raw event tracker (bypasses Keyboard.current entirely)
        var data = new NetworkInputData();
        if (RawKeyDown(Key.D) || RawKeyDown(Key.RightArrow))
            data.HorizontalDirection = 1f;
        if (RawKeyDown(Key.A) || RawKeyDown(Key.LeftArrow))
            data.HorizontalDirection = -1f;
        data.Buttons.Set(MyButtons.Jump, RawKeyDown(Key.Space));

        if (data.HorizontalDirection != 0f || data.Buttons.IsSet(MyButtons.Jump))
        {
            Debug.Log($"[OnInput frame={frameCount}] RAW event: D={RawKeyDown(Key.D)} A={RawKeyDown(Key.A)} Space={RawKeyDown(Key.Space)} → move={data.HorizontalDirection}");
        }
        else
        {
            // Priority 3: Fallback to direct Input System read (device-based API)
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (KeyDown(keyboard, Key.D) || KeyDown(keyboard, Key.RightArrow))
                    data.HorizontalDirection = 1f;
                if (KeyDown(keyboard, Key.A) || KeyDown(keyboard, Key.LeftArrow))
                    data.HorizontalDirection = -1f;
                data.Buttons.Set(MyButtons.Jump, KeyDown(keyboard, Key.Space));
                Debug.Log($"[OnInput frame={frameCount}] DIRECT fallback: D={KeyDown(keyboard, Key.D)} A={KeyDown(keyboard, Key.A)} → move={data.HorizontalDirection}");
            }
            else
            {
                Debug.LogWarning($"[OnInput frame={frameCount}] No keyboard — trying gamepad…");
                var gamepad = Gamepad.current;
                if (gamepad != null)
                {
                    float stick = gamepad.leftStick.ReadValue().x;
                    data.HorizontalDirection = stick;
                    data.Buttons.Set(MyButtons.Jump, gamepad.buttonSouth.isPressed);
                    Debug.Log($"[OnInput frame={frameCount}] Gamepad stick={stick}");
                }
                else
                {
                    Debug.LogWarning($"[OnInput frame={frameCount}] NO INPUT DEVICE — sending zero!");
                }
            }
        }

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
