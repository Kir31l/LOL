using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Server-authoritative saw damage. The server detects the trigger,
/// calls ReportHitServerRpc on the hitting player's NetworkPlayer,
/// and relies on the NetworkPlayer to sync lives/deaths/knockback.
/// </summary>
public class SawDamage : NetworkBehaviour
{
    [Header("Damage")]
    [SerializeField] private float knockbackForce = 14f;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsServer) return;
        if (!other.CompareTag("Player")) return;

        var netPlayer = other.GetComponent<NetworkPlayer>();
        if (netPlayer == null || netPlayer.IsInvulnerable.Value) return;

        // Knockback direction: away from saw, always some upward
        Vector2 dir = (other.transform.position - transform.position).normalized;
        if (dir.y < 0.3f)
            dir.y = 0.3f;
        dir.Normalize();

        netPlayer.ReportHitServerRpc(dir * knockbackForce);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector3 pos = transform.position;
        Gizmos.DrawRay(pos, Vector2.right * 1.5f);
        Gizmos.DrawRay(pos, Vector2.left * 1.5f);
    }
}
