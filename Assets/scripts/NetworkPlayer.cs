using Fusion;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Manages multiplayer-specific lifecycle for the player.
/// Uses Fusion [Networked] properties for synced state and [Rpc] for remote calls.
/// </summary>
public class NetworkPlayer : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerMove playerMove;
    [SerializeField] private LobbyPlayerSetup lobbySetup;

    // ─── Synced state ────────────────────────────────────────

    [Networked] public int CharacterIndex { get; set; }
    [Networked] public int Score { get; set; }
    [Networked] public int Deaths { get; set; }
    [Networked] public int CurrentLives { get; set; }
    [Networked] public NetworkString<_32> UsernameNV { get; set; }
    [Networked] public NetworkBool IsInvulnerable { get; set; }

    private Coroutine respawnCoroutine;
    private int lastScore;
    private int lastLives;
    private int lastCharacterIndex = -1;
    private string lastUsername = "";

    public override void Spawned()
    {
        // Only the input owner (local player) applies their own character here.
        if (Object.HasInputAuthority)
        {
            ApplyCharacterState(PlayerSession.SelectedCharacter, PlayerSession.Username);
            SetupCamera();

            // Tell state authority (server) to store the synced character state
            SetInitialStateRpc(PlayerSession.SelectedCharacter, PlayerSession.Username);
        }

        lastScore = Score;
        lastLives = CurrentLives;
    }

    public override void Render()
    {

        // Poll for changes (Fusion doesn't have per-property callbacks)
        // Only update the local static managers for the local player's values.
        if (Score != lastScore)
        {
            if (Object.HasInputAuthority)
                ScoreManager.AddPoints(Score - lastScore);
            lastScore = Score;
        }
        if (CurrentLives != lastLives)
        {
            if (Object.HasInputAuthority)
                LivesManager.SetLives(CurrentLives);
            lastLives = CurrentLives;
        }

        // Apply character/username when the synced values arrive from the server
        string name = UsernameNV.ToString();
        if (CharacterIndex != lastCharacterIndex || name != lastUsername)
        {
            ApplyCharacterState(CharacterIndex, name);
            lastCharacterIndex = CharacterIndex;
            lastUsername = name;
        }
    }

    // ─── Character visuals ───────────────────────────────────

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

    // ─── Server RPCs ─────────────────────────────────────────

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void SetInitialStateRpc(int characterIndex, string username)
    {
        UsernameNV = username;
        CharacterIndex = characterIndex;
        CurrentLives = 3;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void AddScoreServerRpc(int amount)
    {
        if (!Object.HasStateAuthority) return;
        Score += amount;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void ReportHitServerRpc(Vector2 knockbackVelocity)
    {
        if (!Object.HasStateAuthority) return;
        if (IsInvulnerable) return;

        Deaths++;
        CurrentLives--;
        IsInvulnerable = true;

        // Apply knockback on the owner client
        ApplyKnockbackClientRpc(knockbackVelocity);
        StartCoroutine(InvulnerabilityTimer());

        if (CurrentLives <= 0)
        {
            if (respawnCoroutine != null) StopCoroutine(respawnCoroutine);
            respawnCoroutine = StartCoroutine(RespawnRoutine());
        }
    }

    // ─── Client RPCs ─────────────────────────────────────────

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void ApplyKnockbackClientRpc(Vector2 knockbackVelocity)
    {
        if (Object.HasInputAuthority && playerMove != null)
            playerMove.TakeHit(knockbackVelocity);
    }

    // ─── Server helpers ──────────────────────────────────────

    private System.Collections.IEnumerator InvulnerabilityTimer()
    {
        yield return new WaitForSeconds(1.5f);
        if (Object.HasStateAuthority)
            IsInvulnerable = false;
    }

    private System.Collections.IEnumerator RespawnRoutine()
    {
        // Disable player on all clients
        SetPlayerStateClientRpc(false);

        yield return new WaitForSeconds(5f);

        // Reset position to a fixed spawn spot
        transform.position = Vector3.zero;

        // Reset lives and re-enable
        CurrentLives = 3;
        IsInvulnerable = false;
        SetPlayerStateClientRpc(true);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void SetPlayerStateClientRpc(bool alive)
    {
        if (Object.HasInputAuthority)
        {
            if (playerMove != null)
                playerMove.enabled = alive;
        }

        var sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = alive;
        var col = GetComponent<Collider2D>();
        if (col != null) col.enabled = alive;
    }
}
