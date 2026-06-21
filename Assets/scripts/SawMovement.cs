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
    private Rigidbody2D rb;

    public override void Spawned()
    {
        startPos = transform.position;
        timer = -startDelay;
        rb = GetComponent<Rigidbody2D>();
    }

    public override void FixedUpdateNetwork()
    {
        // Only the state authority (server/host) moves the saw
        if (!Object.HasStateAuthority) return;

        timer += Runner.DeltaTime;
        if (timer < 0f) return;

        float offset = Mathf.Sin(timer * moveSpeed) * moveRange;
        Vector2 targetPos = startPos + Vector2.right * offset;

        // Use rb.MovePosition when Rigidbody2D exists so that Fusion's NetworkTransform
        // (which reads from the Rigidbody2D when one is present) detects the change and
        // syncs it to clients. Directly setting transform.position may not be seen by
        // NetworkTransform when a Rigidbody2D is on the same GameObject.
        if (rb != null)
            rb.MovePosition(targetPos);
        else
            transform.position = targetPos;
    }

    void OnDrawGizmosSelected()
    {
        Vector2 center = Application.isPlaying ? startPos : (Vector2)transform.position;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(center + Vector2.left * moveRange, center + Vector2.right * moveRange);
    }
}
