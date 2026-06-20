using Fusion;
using UnityEngine;

/// <summary>
/// Handles player movement, jumping, wall sliding, knockback, and animation.
/// 
/// KEY FIX for multiplayer sync:
/// - Uses Fusion's FixedUpdateNetwork() instead of Unity's Update()/FixedUpdate()
/// - Reads input from GetInput<NetworkInputData>() instead of Input.GetAxisRaw()
/// - Fusion distributes input to all peers — the SERVER simulates all players' physics
///   and NetworkTransform syncs the results to every client.
/// - Clients predict their local player with the same input and get corrected if needed.
/// </summary>
public class PlayerMove : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField] private float speed = 5f;
    [SerializeField] private float jumpForce = 10f;
    [SerializeField] private int maxJumps = 2;

    [Header("Ground Check")]
    [SerializeField] private string groundLayerName = "Ground";
    [SerializeField] private float checkRadius = 0.2f;

    [Header("Wall Slide")]
    [SerializeField] private float wallSlideSpeed = 2f;
    [SerializeField] private float wallJumpForceX = 10f;
    [SerializeField] private float wallJumpForceY = 10f;

    [Header("Damage")]
    [SerializeField] private float invulnerabilityDuration = 1.5f;

    /// <summary>True while the player cannot take damage.</summary>
    public bool IsInvulnerable { get; private set; }

    private LayerMask groundLayer;
    private Rigidbody2D rb;
    private CapsuleCollider2D capsule;
    private Animator anim;
    private bool facingRight = true;
    private bool isGrounded;
    private bool wasGrounded;
    private int jumpCount;
    private bool doubleJumped;
    private bool isWallSliding;
    private bool isTouchingWall;
    private int wallDirection; // -1 left, 1 right
    private float invulnerabilityTimer;
    private float knockbackTimer;

    /// <summary>Previous tick's buttons for manual edge detection (Fusion 2.0.12 doesn't have IsDown).</summary>
    private NetworkButtons previousButtons;

    private static readonly int StateParam = Animator.StringToHash("state");
    private const int StateIdle = 0;
    private const int StateRun = 1;
    private const int StateHit = 2;
    private const int StateJump = 3;
    private const int StateDoubleJump = 4;
    private const int StateFall = 5;
    private const int StateWallSlide = 6;
    private const int StateDisappear = 7;

    // ─── Fusion lifecycle ────────────────────────────────────

    public override void Spawned()
    {
        rb = GetComponent<Rigidbody2D>();
        capsule = GetComponent<CapsuleCollider2D>();
        anim = GetComponent<Animator>();
        groundLayer = LayerMask.GetMask(groundLayerName);

        // Disable Rigidbody2D interpolation — NetworkTransform handles all smoothing.
        // Both active together = double interpolation = visual lag.
        if (rb != null)
            rb.interpolation = RigidbodyInterpolation2D.None;

    }

    // ─── Fusion simulation tick (runs on ALL peers) ──────────

    public override void FixedUpdateNetwork()
    {
        // Poll NetworkPlayer.CurrentLives directly instead of global LivesManager.
        // This ensures each player's death/respawn is INDEPENDENT from others.
        var netPlayer = GetComponent<NetworkPlayer>();
        if (netPlayer != null)
        {
            if (netPlayer.CurrentLives <= 0 && enabled)
            {
                if (anim != null && anim.runtimeAnimatorController != null)
                    anim.SetInteger(StateParam, StateDisappear);
                enabled = false;
            }
            else if (netPlayer.CurrentLives > 0 && !enabled)
            {
                enabled = true;
                IsInvulnerable = true;
                invulnerabilityTimer = invulnerabilityDuration;
            }
        }

        // GetInput returns the correct input for THIS player on every peer.
        // On the client: the input they just sent via OnInput.
        // On the server: the input queue from all connected clients.
        if (!GetInput<NetworkInputData>(out var input))
            return;

        // ─── Timers ────────────────────────────────────────
        if (IsInvulnerable)
        {
            invulnerabilityTimer -= Runner.DeltaTime;
            if (invulnerabilityTimer <= 0f)
                IsInvulnerable = false;
        }

        // ─── Physics state checks ──────────────────────────
        isGrounded = IsGrounded();
        CheckWall();

        // Reset jumps / wall slide when landing
        if (isGrounded && !wasGrounded)
        {
            jumpCount = 0;
            doubleJumped = false;
            isWallSliding = false;
        }
        wasGrounded = isGrounded;

        // Manual edge detection for jump (Fusion 2.0.12 removed IsDown)
        bool jumpPressed = input.Buttons.IsSet(MyButtons.Jump) && !previousButtons.IsSet(MyButtons.Jump);
        previousButtons = input.Buttons;

        // Wall slide — only in air, pressing toward a wall
        isWallSliding = isTouchingWall && !isGrounded
                        && Mathf.Sign(input.HorizontalDirection) == wallDirection;

        // ─── Jump ──────────────────────────────────────────
        if (jumpPressed && isWallSliding)
        {
            // Wall jump
            rb.linearVelocity = new Vector2(-wallDirection * wallJumpForceX, wallJumpForceY);
            isWallSliding = false;
            jumpCount = 0;
            doubleJumped = false;
            if (wallDirection == 1 && facingRight) Flip();
            else if (wallDirection == -1 && !facingRight) Flip();
        }
        else if (jumpPressed && jumpCount < maxJumps)
        {
            // Ground jump or air (double) jump
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            jumpCount++;
            doubleJumped = jumpCount >= 2;
            isWallSliding = false;
        }

        // ─── Sprite flip ───────────────────────────────────
        if (!isWallSliding)
        {
            if (input.HorizontalDirection > 0 && !facingRight) Flip();
            else if (input.HorizontalDirection < 0 && facingRight) Flip();
        }

        // ─── Apply velocity ────────────────────────────────
        if (knockbackTimer > 0f)
        {
            knockbackTimer -= Runner.DeltaTime;
            // Let knockback play out — don't override velocity
        }
        else if (isWallSliding)
        {
            rb.linearVelocity = new Vector2(0f, Mathf.Max(rb.linearVelocity.y, -wallSlideSpeed));
        }
        else
        {
            rb.linearVelocity = new Vector2(input.HorizontalDirection * speed, rb.linearVelocity.y);
        }

        // ─── Animation state ───────────────────────────────
        UpdateAnimation();
    }

    // ─── Animation ───────────────────────────────────────────

    private void UpdateAnimation()
    {
        if (anim == null || anim.runtimeAnimatorController == null) return;

        int newState;

        if (isWallSliding)
            newState = StateWallSlide;
        else if (isGrounded)
            newState = Mathf.Abs(rb.linearVelocity.x) > 0.1f ? StateRun : StateIdle;
        else if (doubleJumped)
            newState = StateDoubleJump;
        else
            newState = rb.linearVelocity.y > 0 ? StateJump : StateFall;

        anim.SetInteger(StateParam, newState);
    }

    // ─── External call (via RPC from NetworkPlayer) ──────────

    /// <summary>Apply knockback and start invulnerability. Called locally via RPC on the affected client.</summary>
    public void TakeHit(Vector2 knockbackVelocity, float overrideInvulnDuration = 0f)
    {
        if (IsInvulnerable) return;

        IsInvulnerable = true;
        invulnerabilityTimer = overrideInvulnDuration > 0f ? overrideInvulnDuration : invulnerabilityDuration;

        rb.linearVelocity = knockbackVelocity;
        knockbackTimer = 0.15f;

        if (anim != null && anim.runtimeAnimatorController != null)
            anim.SetInteger(StateParam, StateHit);
    }

    // ─── Physics helpers ─────────────────────────────────────

    private bool IsGrounded()
    {
        if (capsule == null) return false;
        Vector2 bottom = capsule.bounds.center + Vector3.down * capsule.bounds.extents.y;
        return Physics2D.OverlapCircle(bottom, checkRadius, groundLayer);
    }

    private void CheckWall()
    {
        if (capsule == null) return;
        Vector2 center = capsule.bounds.center;
        Vector2 extents = capsule.bounds.extents;
        float skin = Physics2D.defaultContactOffset;

        Vector2 leftOrigin = center + Vector2.left * (extents.x + skin);
        Vector2 rightOrigin = center + Vector2.right * (extents.x + skin);

        Collider2D leftHit = Physics2D.OverlapCircle(leftOrigin, skin, groundLayer);
        Collider2D rightHit = Physics2D.OverlapCircle(rightOrigin, skin, groundLayer);

        if (leftHit != null) { isTouchingWall = true; wallDirection = -1; }
        else if (rightHit != null) { isTouchingWall = true; wallDirection = 1; }
        else { isTouchingWall = false; wallDirection = 0; }
    }

    private void Flip()
    {
        facingRight = !facingRight;
        Vector3 scale = transform.localScale;
        scale.x *= -1;
        transform.localScale = scale;
    }

    void OnDrawGizmosSelected()
    {
        if (capsule == null) capsule = GetComponent<CapsuleCollider2D>();
        if (capsule == null) return;
        Vector2 center = capsule.bounds.center;
        Vector2 extents = capsule.bounds.extents;

        Vector2 bottom = center + Vector2.down * extents.y;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(bottom, checkRadius);

        Gizmos.color = Color.cyan;
        float skin = Physics2D.defaultContactOffset;
        Gizmos.DrawWireSphere(center + Vector2.left * (extents.x + skin), skin);
        Gizmos.DrawWireSphere(center + Vector2.right * (extents.x + skin), skin);
    }
}
