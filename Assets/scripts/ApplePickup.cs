using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Server-authoritative apple pickup. The server detects the trigger,
/// awards points to the hitting player's NetworkPlayer, and syncs
/// the apple's hidden/visible state to all clients.
/// </summary>
public class ApplePickup : NetworkBehaviour
{
    [Header("Score")]
    [SerializeField] private int points = 50;

    [Header("Respawn")]
    [SerializeField] private float respawnTime = 5f;

    [Header("Animator")]
    [SerializeField] private string takenBool = "Taken";

    /// <summary>Synced apple state — the server decides when it's available.</summary>
    private NetworkVariable<bool> isAvailable = new(true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private Collider2D appleCollider;
    private SpriteRenderer spriteRenderer;
    private Animator animator;

    private void Awake()
    {
        appleCollider = GetComponent<Collider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
    }

    public override void OnNetworkSpawn()
    {
        isAvailable.OnValueChanged += OnAvailabilityChanged;
        // Sync initial visual state
        ApplyAvailability(isAvailable.Value);
    }

    public override void OnNetworkDespawn()
    {
        isAvailable.OnValueChanged -= OnAvailabilityChanged;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsServer) return;
        if (!isAvailable.Value) return;

        var netPlayer = other.GetComponent<NetworkPlayer>();
        if (netPlayer == null) return;

        // Award points
        netPlayer.AddScoreServerRpc(points);

        // Mark as taken and start respawn
        isAvailable.Value = false;
        appleCollider.enabled = false;

        StartCoroutine(RespawnRoutine());
    }

    private System.Collections.IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(respawnTime);

        isAvailable.Value = true;
        appleCollider.enabled = true;
    }

    private void OnAvailabilityChanged(bool _, bool newValue)
    {
        ApplyAvailability(newValue);
    }

    private void ApplyAvailability(bool available)
    {
        if (animator != null)
        {
            if (available)
                animator.Rebind();
            else
                animator.SetBool(takenBool, true);
        }

        if (spriteRenderer != null)
            spriteRenderer.enabled = available;
    }
}
