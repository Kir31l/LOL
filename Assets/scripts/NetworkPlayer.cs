using Unity.Cinemachine;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Manages multiplayer-specific lifecycle for the player:
/// - Gates movement to owner only
/// - Syncs character selection and stats via NetworkVariables
/// - Sets up camera to follow local player
/// - Handles respawn
/// </summary>
public class NetworkPlayer : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerMove playerMove;
    [SerializeField] private LobbyPlayerSetup lobbySetup;

    // --- Synced state ---
    public NetworkVariable<int> CharacterIndex = new(0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<int> Score = new(0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<int> Deaths = new(0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<int> CurrentLives = new(3,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<FixedString32Bytes> UsernameNV = new("PLAYER",
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<bool> IsInvulnerable = new(false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private Coroutine respawnCoroutine;

    public override void OnNetworkSpawn()
    {
        bool localOwner = IsOwner;

        if (!localOwner && playerMove != null)
            playerMove.enabled = false;

        // Host uses the scene NetworkPlayer — claim ownership and set up locally
        if (IsServer && IsClient && !localOwner)
        {
            NetworkObject.ChangeOwnership(NetworkManager.Singleton.LocalClientId);
            if (playerMove != null)
                playerMove.enabled = true;
            ApplyCharacterState(PlayerSession.SelectedCharacter, PlayerSession.Username);
            SetupCamera();
            SetInitialStateServerRpc(PlayerSession.SelectedCharacter, PlayerSession.Username);
            Score.OnValueChanged += OnScoreChanged;
        }
        else if (localOwner)
        {
            ApplyCharacterState(PlayerSession.SelectedCharacter, PlayerSession.Username);
            SetupCamera();
            SetInitialStateServerRpc(PlayerSession.SelectedCharacter, PlayerSession.Username);
            Score.OnValueChanged += OnScoreChanged;
        }

        CharacterIndex.OnValueChanged += OnCharacterChanged;
        CurrentLives.OnValueChanged += OnCurrentLivesChanged;
        // UsernameNV may arrive after CharacterIndex on non-owner clients
        UsernameNV.OnValueChanged += OnUsernameChanged;
    }

    public override void OnNetworkDespawn()
    {
        CharacterIndex.OnValueChanged -= OnCharacterChanged;
        CurrentLives.OnValueChanged -= OnCurrentLivesChanged;
        UsernameNV.OnValueChanged -= OnUsernameChanged;
        if (IsOwner)
            Score.OnValueChanged -= OnScoreChanged;
    }

    private void OnCharacterChanged(int oldValue, int newValue)
    {
        // Apply with whatever username we have (may be default "PLAYER" if not synced yet)
        ApplyCharacterState(newValue, UsernameNV.Value.ToString());
    }

    private void OnUsernameChanged(FixedString32Bytes oldValue, FixedString32Bytes newValue)
    {
        // Username arrived — make sure character visuals use the real name
        ApplyCharacterState(CharacterIndex.Value, newValue.ToString());
    }

    private void OnScoreChanged(int oldValue, int newValue)
    {
        ScoreManager.AddPoints(newValue - oldValue);
    }

    private void OnCurrentLivesChanged(int oldValue, int newValue)
    {
        LivesManager.SetLives(newValue);
    }

    private void ApplyCharacterState(int characterIndex, string username)
    {
        if (lobbySetup != null)
            lobbySetup.ApplyCharacter(characterIndex, username);
    }

    private void SetupCamera()
    {
        var vcam = FindAnyObjectByType<CinemachineCamera>();
        if (vcam != null)
            vcam.Follow = transform;
    }

    // ─── Server RPCs ────────────────────────────────────────

    [Rpc(SendTo.Server)]
    private void SetInitialStateServerRpc(int characterIndex, string username)
    {
        UsernameNV.Value = new FixedString32Bytes(username);
        CharacterIndex.Value = characterIndex;
    }

    [Rpc(SendTo.Server)]
    public void AddScoreServerRpc(int amount)
    {
        if (!IsServer) return;
        Score.Value += amount;
    }

    [Rpc(SendTo.Server)]
    public void ReportHitServerRpc(Vector2 knockbackVelocity)
    {
        if (!IsServer) return;
        if (IsInvulnerable.Value) return;

        Deaths.Value++;
        CurrentLives.Value--;
        IsInvulnerable.Value = true;

        // Visual knockback on the owner client
        ApplyKnockbackClientRpc(knockbackVelocity);
        StartCoroutine(InvulnerabilityTimer());

        if (CurrentLives.Value <= 0)
        {
            if (respawnCoroutine != null) StopCoroutine(respawnCoroutine);
            respawnCoroutine = StartCoroutine(RespawnRoutine());
        }
    }

    // ─── Client RPCs ────────────────────────────────────────

    [Rpc(SendTo.ClientsAndHost)]
    private void ApplyKnockbackClientRpc(Vector2 knockbackVelocity)
    {
        if (IsOwner && playerMove != null)
            playerMove.TakeHit(knockbackVelocity);
    }

    // ─── Server helpers ─────────────────────────────────────

    private System.Collections.IEnumerator InvulnerabilityTimer()
    {
        yield return new WaitForSeconds(1.5f);
        if (IsServer)
            IsInvulnerable.Value = false;
    }

    private System.Collections.IEnumerator RespawnRoutine()
    {
        // Disable player on all clients
        SetPlayerStateClientRpc(false);

        yield return new WaitForSeconds(5f);

        // Reset position to a fixed spawn spot
        transform.position = Vector3.zero;

        // Reset lives and re-enable
        CurrentLives.Value = 3;
        IsInvulnerable.Value = false;
        SetPlayerStateClientRpc(true);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void SetPlayerStateClientRpc(bool alive)
    {
        if (IsOwner)
        {
            if (playerMove != null)
                playerMove.enabled = alive;
        }

        // Visually disable/enable
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = alive;
        var col = GetComponent<Collider2D>();
        if (col != null) col.enabled = alive;
    }
}
