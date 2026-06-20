using Fusion;
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
    [Networked] public NetworkBool IsAvailable { get; set; }

    private Collider2D appleCollider;
    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private NetworkBool lastAvailable;

    private void Awake()
    {
        appleCollider = GetComponent<Collider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
    }

    public override void Spawned()
    {
        // By default apples are available when the game starts
        if (Object.HasStateAuthority)
            IsAvailable = true;

        // Sync initial visual state
        ApplyAvailability(IsAvailable);
        lastAvailable = IsAvailable;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (Object == null || !Object.HasStateAuthority) return;
        if (!IsAvailable) return;

        var netPlayer = other.GetComponent<NetworkPlayer>();
        if (netPlayer == null) return;

        // Award points
        netPlayer.AddScoreServerRpc(points);

        // Mark as taken and start respawn
        IsAvailable = false;
        appleCollider.enabled = false;

        StartCoroutine(RespawnRoutine());
    }

    private System.Collections.IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(respawnTime);

        IsAvailable = true;
        appleCollider.enabled = true;
    }

    public override void Render()
    {
        // Only apply when state changes (avoids spamming Animator every frame)
        if (IsAvailable != lastAvailable)
        {
            ApplyAvailability(IsAvailable);
            lastAvailable = IsAvailable;

            // When apple is taken, start a brief timer so the disappear animation
            // has time to play before we hide the sprite
            if (!IsAvailable && gameObject.activeInHierarchy)
                StartCoroutine(HideAfterTakenAnimation());
        }
    }

    private void ApplyAvailability(bool available)
    {
        if (animator != null)
        {
            if (available)
                animator.Rebind();    // force back to idle animation immediately
            else
                animator.SetBool(takenBool, true);
        }

        // On respawn: immediately show the sprite again
        if (spriteRenderer != null && available)
            spriteRenderer.enabled = true;
    }

    /// <summary>Let the disappear animation play, then hide the sprite.</summary>
    private System.Collections.IEnumerator HideAfterTakenAnimation()
    {
        yield return new WaitForSeconds(0.4f); // enough for the "taken" animation to play
        if (spriteRenderer != null && !IsAvailable)
            spriteRenderer.enabled = false;
    }
}
