using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Manages multiplayer-specific lifecycle for the player:
/// - Gates movement to owner only
/// - Syncs character selection and username via NetworkVariable
/// - Sets up camera to follow local player
/// </summary>
public class NetworkPlayer : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerMove playerMove;
    [SerializeField] private LobbyPlayerSetup lobbySetup;

    /// <summary>
    /// Character index synced from owner to all peers.
    /// Server-authoritative: only server can write, all can read.
    /// </summary>
    public NetworkVariable<int> CharacterIndex = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    /// <summary>Username set by the owner, synced via RPC.</summary>
    public string Username { get; private set; } = "PLAYER";

    public override void OnNetworkSpawn()
    {
        // Only the owner controls their own movement
        if (!IsOwner && playerMove != null)
            playerMove.enabled = false;

        if (IsOwner)
        {
            // Apply local selections immediately — no wait for server
            ApplyCharacterState(PlayerSession.SelectedCharacter, PlayerSession.Username);

            // Move to an available spawn point
            MoveToSpawnPoint();

            // Set camera to follow this player
            SetupCamera();

            // Tell the server what character / username we picked
            SendInitialStateServerRpc(PlayerSession.SelectedCharacter, PlayerSession.Username);
        }

        // React to changes from the server
        CharacterIndex.OnValueChanged += OnCharacterChanged;
    }

    public override void OnNetworkDespawn()
    {
        CharacterIndex.OnValueChanged -= OnCharacterChanged;
    }

    private void OnCharacterChanged(int oldValue, int newValue)
    {
        ApplyCharacterState(newValue, Username);
    }

    private void ApplyCharacterState(int characterIndex, string username)
    {
        if (lobbySetup != null)
            lobbySetup.ApplyCharacter(characterIndex, username);
    }

    /// <summary>
    /// Find a SpawnPosition by index (based on OwnerClientId) and move there.
    /// Falls back to current position if none found.
    /// </summary>
    private void MoveToSpawnPoint()
    {
        var spawns = GameObject.FindGameObjectsWithTag("SpawnPoint");
        if (spawns.Length == 0) return;

        int index = (int)(OwnerClientId % (ulong)spawns.Length);
        transform.position = spawns[index].transform.position;
    }

    private void SetupCamera()
    {
        // Main Camera already exists in the scene — make it follow this player
        var cam = Camera.main;
        if (cam != null)
        {
            // Simple follow via LateUpdate script added dynamically
            var follower = cam.GetComponent<CameraFollower>();
            if (follower == null)
                follower = cam.gameObject.AddComponent<CameraFollower>();
            follower.target = transform;
        }
    }

    [Rpc(SendTo.Server)]
    private void SendInitialStateServerRpc(int characterIndex, string username)
    {
        CharacterIndex.Value = characterIndex;
        Username = username;

        // Broadcast username to all clients
        SetUsernameClientRpc(username);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void SetUsernameClientRpc(string username)
    {
        Username = username;

        // Re-apply with correct username now available
        ApplyCharacterState(CharacterIndex.Value, username);
    }
}
