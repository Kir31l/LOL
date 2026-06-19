using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Handles create / join / disconnect flow via Unity Relay + Lobby.
/// Attach to a persistent GameObject that survives between scenes.
/// </summary>
public class ConnectionManager : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string lobbySceneName = "lobby";

    [Header("Relay")]
    [SerializeField] private int maxPlayers = 4;
    [SerializeField] private string connectionType = "dtls";

    [Header("Feedback (optional)")]
    [SerializeField] private BitmapText statusText;

    /// <summary>4-digit numeric lobby code displayed to players.</summary>
    public static string LobbyCode { get; private set; } = "";

    private bool isConnecting;
    private Lobby currentLobby;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>Create a new game (host = server + local client) via Relay and Lobby.</summary>
    public async void CreateGame()
    {
        if (isConnecting)
        {
            SetStatus("Already connecting, please wait...");
            return;
        }
        isConnecting = true;

        SetStatus("Initializing...");

        if (NetworkManager.Singleton == null)
        {
            SetStatus("ERROR: NetworkManager.Singleton is null.\nAdd a NetworkManager GameObject to the menu scene.");
            isConnecting = false;
            return;
        }

        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport == null)
        {
            SetStatus("ERROR: No UnityTransport on NetworkManager.\nAdd a UnityTransport component.");
            isConnecting = false;
            return;
        }

        try
        {
            // 1. Authenticate with Unity Services
            SetStatus("Connecting to Unity Services...");
            await UnityServices.InitializeAsync();

            SetStatus("Signing in anonymously...");
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();

            // 2. Generate a unique 4-digit lobby code
            string code4Digit = await GenerateUniqueCode();

            // 3. Create Relay allocation + get join code
            SetStatus("Allocating Relay server...");
            var allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers - 1);
            string relayJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            Debug.Log($"Relay join code: {relayJoinCode}");

            // 4. Create Unity Lobby with the 4-digit code as the lobby name
            //    Store the Relay join code in publicly visible lobby data so clients can read it.
            SetStatus("Creating lobby...");
            currentLobby = await LobbyService.Instance.CreateLobbyAsync(
                code4Digit,
                maxPlayers,
                new CreateLobbyOptions
                {
                    IsPrivate = false,
                    Data = new Dictionary<string, DataObject>
                    {
                        { "RelayCode", new DataObject(DataObject.VisibilityOptions.Public, relayJoinCode) }
                    },
                    Player = new Player(
                        id: AuthenticationService.Instance.PlayerId,
                        allocationId: allocation.AllocationId.ToString()
                    )
                }
            );
            LobbyCode = code4Digit;
            Debug.Log($"Created lobby '{currentLobby.Name}' (ID: {currentLobby.Id}, code: {LobbyCode})");

            // 5. Configure Unity Transport with Relay host data
            SetStatus("Configuring transport...");
            bool isSecure = connectionType == "dtls";
            transport.SetHostRelayData(
                allocation.RelayServer.IpV4,
                (ushort)allocation.RelayServer.Port,
                allocation.AllocationIdBytes,
                allocation.Key,
                allocation.ConnectionData,
                isSecure);

            // 6. Start the host
            SetStatus("Starting host...");
            NetworkManager.Singleton.OnServerStarted += OnServerStarted;
            NetworkManager.Singleton.StartHost();
        }
        catch (System.Exception e)
        {
            SetStatus($"ERROR: {e.Message}");
            Debug.LogError($"Failed to create game: {e}");
        }
        finally
        {
            isConnecting = false;
        }
    }

    /// <summary>Join an existing game via a 4-digit lobby code.</summary>
    public async void JoinGame(string code4Digit)
    {
        if (isConnecting)
        {
            SetStatus("Already connecting, please wait...");
            return;
        }
        isConnecting = true;

        SetStatus("Initializing...");

        if (NetworkManager.Singleton == null)
        {
            SetStatus("ERROR: NetworkManager.Singleton is null.");
            isConnecting = false;
            return;
        }

        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport == null)
        {
            SetStatus("ERROR: No UnityTransport on NetworkManager.");
            isConnecting = false;
            return;
        }

        try
        {
            // 1. Authenticate with Unity Services
            SetStatus("Connecting to Unity Services...");
            await UnityServices.InitializeAsync();

            SetStatus("Signing in anonymously...");
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();

            // 2. Find the lobby by its name (the 4-digit code)
            SetStatus($"Finding lobby {code4Digit}...");
            var qr = await LobbyService.Instance.QueryLobbiesAsync(new QueryLobbiesOptions
            {
                Filters = new List<QueryFilter>
                {
                    new QueryFilter(QueryFilter.FieldOptions.Name, code4Digit, QueryFilter.OpOptions.EQ)
                }
            });

            if (qr.Results.Count == 0)
                throw new System.Exception($"No lobby found with code {code4Digit}");

            currentLobby = qr.Results[0];

            // 3. Read the Relay join code from the lobby's public data
            if (!currentLobby.Data.TryGetValue("RelayCode", out var relayData) || string.IsNullOrEmpty(relayData.Value))
                throw new System.Exception("Lobby has no Relay code");

            string relayJoinCode = relayData.Value;

            // 4. Join Relay allocation
            SetStatus("Joining Relay allocation...");
            var joinAllocation = await RelayService.Instance.JoinAllocationAsync(relayJoinCode);

            // 5. Configure Unity Transport with Relay client data
            SetStatus("Configuring transport...");
            bool isSecure = connectionType == "dtls";
            transport.SetClientRelayData(
                joinAllocation.RelayServer.IpV4,
                (ushort)joinAllocation.RelayServer.Port,
                joinAllocation.AllocationIdBytes,
                joinAllocation.Key,
                joinAllocation.ConnectionData,
                joinAllocation.HostConnectionData,
                isSecure);

            // 6. Start the client
            SetStatus("Starting client...");
            NetworkManager.Singleton.StartClient();
        }
        catch (System.Exception e)
        {
            SetStatus($"ERROR: {e.Message}");
            Debug.LogError($"Failed to join game: {e}");
        }
        finally
        {
            isConnecting = false;
        }
    }

    /// <summary>Disconnect and return to the menu scene.</summary>
    public void Disconnect()
    {
        // Clean up lobby if we created/joined one
        if (currentLobby != null)
        {
            try
            {
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
                    LobbyService.Instance.DeleteLobbyAsync(currentLobby.Id);
            }
            catch { /* best-effort cleanup */ }
            currentLobby = null;
        }

        LobbyCode = "";

        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.Shutdown();

        SceneManager.LoadScene("menu");
    }

    private void OnServerStarted()
    {
        NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
        NetworkManager.Singleton.SceneManager.LoadScene(lobbySceneName, LoadSceneMode.Single);
    }

    /// <summary>Generate a random 1000-9999 code not used by any existing lobby.</summary>
    private async System.Threading.Tasks.Task<string> GenerateUniqueCode()
    {
        for (int attempt = 0; attempt < 10; attempt++)
        {
            string code = Random.Range(1000, 10000).ToString();

            var qr = await LobbyService.Instance.QueryLobbiesAsync(new QueryLobbiesOptions
            {
                Filters = new List<QueryFilter>
                {
                    new QueryFilter(QueryFilter.FieldOptions.Name, code, QueryFilter.OpOptions.EQ)
                }
            });

            if (qr.Results.Count == 0)
                return code;
        }

        // Fallback: very unlikely to reach here
        return Random.Range(1000, 10000).ToString();
    }

    private void SetStatus(string msg)
    {
        Debug.Log($"[ConnectionManager] {msg}");
        if (statusText != null)
            statusText.SetText(msg);
    }
}
