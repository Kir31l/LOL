using UnityEngine;

public class PlayerMove : MonoBehaviour
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

    private LayerMask groundLayer;

    private Rigidbody2D rb;
    private CapsuleCollider2D capsule;
    private Animator anim;
    private float moveInput;
    private bool facingRight = true;
    private bool isGrounded;
    private bool wasGrounded;
    private int jumpCount;
    private bool doubleJumped;
    private bool isWallSliding;
    private bool isTouchingWall;
    private int wallDirection; // -1 left, 1 right
    private static readonly int StateParam = Animator.StringToHash("state");

    // Animator state values — must match the controller
    private const int StateIdle = 0;
    private const int StateRun = 1;
    private const int StateJump = 3;
    private const int StateDoubleJump = 4;
    private const int StateFall = 5;
    private const int StateWallSlide = 6;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        capsule = GetComponent<CapsuleCollider2D>();
        anim = GetComponent<Animator>();
        groundLayer = LayerMask.GetMask(groundLayerName);
    }

    void Update()
    {
        moveInput = Input.GetAxisRaw("Horizontal");

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

        // Wall slide — only in air, pressing toward a wall
        isWallSliding = isTouchingWall && !isGrounded
                        && Mathf.Sign(moveInput) == wallDirection;

        // Wall jump
        if (Input.GetButtonDown("Jump") && isWallSliding)
        {
            rb.linearVelocity = new Vector2(-wallDirection * wallJumpForceX, wallJumpForceY);
            isWallSliding = false;
            jumpCount = 0;      // reset so you can air-jump after wall jump
            doubleJumped = false;
            // Flip to face away from wall
            if (wallDirection == 1 && facingRight) Flip();
            else if (wallDirection == -1 && !facingRight) Flip();
        }
        // Regular jump (ground or air)
        else if (Input.GetButtonDown("Jump") && jumpCount < maxJumps)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            jumpCount++;
            doubleJumped = jumpCount >= 2;
            isWallSliding = false;
        }

        // Flip sprite (only when not wall sliding)
        if (!isWallSliding)
        {
            if (moveInput > 0 && !facingRight) Flip();
            else if (moveInput < 0 && facingRight) Flip();
        }

        // Animator state
        UpdateAnimation();
    }

    private void UpdateAnimation()
    {
        int newState;

        if (isWallSliding)
        {
            newState = StateWallSlide;
        }
        else if (isGrounded)
        {
            newState = Mathf.Abs(moveInput) > 0.1f ? StateRun : StateIdle;
        }
        else if (doubleJumped)
        {
            newState = StateDoubleJump;
        }
        else
        {
            newState = rb.linearVelocity.y > 0 ? StateJump : StateFall;
        }

        anim.SetInteger(StateParam, newState);
    }

    void FixedUpdate()
    {
        if (isWallSliding)
        {
            // Slow fall while sliding — zero horizontal force so we stick to the wall
            rb.linearVelocity = new Vector2(0f, Mathf.Max(rb.linearVelocity.y, -wallSlideSpeed));
        }
        else
        {
            rb.linearVelocity = new Vector2(moveInput * speed, rb.linearVelocity.y);
        }
    }

    private bool IsGrounded()
    {
        Vector2 bottom = capsule.bounds.center + Vector3.down * capsule.bounds.extents.y;
        return Physics2D.OverlapCircle(bottom, checkRadius, groundLayer);
    }

    private void CheckWall()
    {
        Vector2 center = capsule.bounds.center;
        Vector2 extents = capsule.bounds.extents;

        // Tiny circle at collider edge — uses defaultContactOffset so detection
        // fires at the exact moment the physics engine registers wall contact.
        // This is ~0.16 pixels — invisible.
        float skin = Physics2D.defaultContactOffset;

        Vector2 leftOrigin = center + Vector2.left * (extents.x + skin);
        Vector2 rightOrigin = center + Vector2.right * (extents.x + skin);

        Collider2D leftHit = Physics2D.OverlapCircle(leftOrigin, skin, groundLayer);
        Collider2D rightHit = Physics2D.OverlapCircle(rightOrigin, skin, groundLayer);

        if (leftHit != null)
        {
            isTouchingWall = true;
            wallDirection = -1;
        }
        else if (rightHit != null)
        {
            isTouchingWall = true;
            wallDirection = 1;
        }
        else
        {
            isTouchingWall = false;
            wallDirection = 0;
        }
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

        Vector2 center = capsule.bounds.center;
        Vector2 extents = capsule.bounds.extents;

        // Ground check
        Vector2 bottom = center + Vector2.down * extents.y;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(bottom, checkRadius);

        // Wall check — tiny circles at the collider edges
        Gizmos.color = Color.cyan;
        float skin = Physics2D.defaultContactOffset;
        Gizmos.DrawWireSphere(center + Vector2.left * (extents.x + skin), skin);
        Gizmos.DrawWireSphere(center + Vector2.right * (extents.x + skin), skin);
    }
}
