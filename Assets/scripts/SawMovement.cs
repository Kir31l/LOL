using Fusion;
using UnityEngine;

/// <summary>
/// Oscillates the saw left and right in a smooth loop.
/// Server-authoritative: only the server moves the saw.
/// If used outside a network session (Play mode without Create/Join), move locally for testing.
/// </summary>
public class SawMovement : NetworkBehaviour
{
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float moveRange = 3f;
    [SerializeField] private float startDelay = 0f;

    private Vector2 startPos;
    private float timer;

    public override void Spawned()
    {
        startPos = transform.position;
        timer = -startDelay;
    }

    public override void FixedUpdateNetwork()
    {
        // Only the state authority (server/host) moves the saw
        if (!Object.HasStateAuthority) return;

        timer += Runner.DeltaTime;
        if (timer < 0f) return;

        float offset = Mathf.Sin(timer * moveSpeed) * moveRange;
        transform.position = startPos + Vector2.right * offset;
    }

    void OnDrawGizmosSelected()
    {
        Vector2 center = Application.isPlaying ? startPos : (Vector2)transform.position;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(center + Vector2.left * moveRange, center + Vector2.right * moveRange);
    }
}
