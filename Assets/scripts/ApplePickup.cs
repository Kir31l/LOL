using UnityEngine;

public class ApplePickup : MonoBehaviour
{
    [Header("Score")]
    [SerializeField] private int points = 50;

    [Header("Respawn")]
    [SerializeField] private float respawnTime = 5f;

    [Header("Animator")]
    [SerializeField] private string takenBool = "Taken";

    private Collider2D appleCollider;
    private SpriteRenderer spriteRenderer;
    private Animator animator;

    private void Awake()
    {
        appleCollider = GetComponent<Collider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Make sure it's the player
        if (other.GetComponent<PlayerMove>() == null)
            return;

        Collect();
    }

    private void Collect()
    {
        // Disable collision so it can't be re-collected
        appleCollider.enabled = false;

        // Play the "taken" animation
        if (animator != null)
            animator.SetBool(takenBool, true);

        // Award points
        ScoreManager.AddPoints(points);

        // Start respawn timer
        StartCoroutine(RespawnRoutine());
    }

    private System.Collections.IEnumerator RespawnRoutine()
    {
        // Wait for the "taken" animation to play, then hide
        if (animator != null)
        {
            // Get the current animation state length (if available)
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            float animLength = state.length > 0 ? state.length : 0.3f;
            yield return new WaitForSeconds(animLength);
        }
        else
        {
            yield return new WaitForSeconds(0.3f);
        }

        // Hide the apple visually
        if (spriteRenderer != null)
            spriteRenderer.enabled = false;

        // Wait the remaining respawn time
        yield return new WaitForSeconds(respawnTime);

        // Reset animator to default state (reverts scale, color, etc.)
        if (animator != null)
            animator.Rebind();

        if (spriteRenderer != null)
            spriteRenderer.enabled = true;

        appleCollider.enabled = true;
    }
}
