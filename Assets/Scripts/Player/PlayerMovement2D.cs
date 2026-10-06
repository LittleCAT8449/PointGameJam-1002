using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Basic 2D platform movement using the project's Input System action asset.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D))]
public sealed class PlayerMovement2D : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionAsset inputActions;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float maxMoveSpeed = 6f;
    [SerializeField, Min(0f)] private float acceleration = 60f;
    [SerializeField, Min(0f)] private float deceleration = 80f;

    [Header("Jump")]
    [SerializeField, Min(0f)] private float jumpSpeed = 7f;
    [SerializeField, Min(0f)] private float coyoteTime = 0.1f;
    [SerializeField, Min(0f)] private float jumpBufferTime = 0.12f;
    [SerializeField, Range(0.1f, 1f)] private float jumpCutMultiplier = 0.5f;

    [Header("Ground Check")]
    [SerializeField] private LayerMask groundLayers = ~0;
    [SerializeField, Min(0.01f)] private float groundCheckDistance = 0.12f;
    [SerializeField, Range(0.1f, 1f)] private float groundCheckWidth = 0.85f;
    [SerializeField, Min(0f)] private float groundCheckInset = 0.02f;

    [Header("Visuals")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    private readonly Collider2D[] groundHits = new Collider2D[8];

    private Rigidbody2D body;
    private CapsuleCollider2D bodyCollider;
    private ContactFilter2D groundFilter;
    private InputActionMap playerActionMap;
    private InputAction moveAction;
    private InputAction jumpAction;

    private Vector2 moveInput;
    private float jumpBufferCounter;
    private float coyoteCounter;
    private bool jumpCutPending;

    public bool IsGrounded { get; private set; }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<CapsuleCollider2D>();

        // A platform character should remain upright when it lands against a surface.
        body.constraints |= RigidbodyConstraints2D.FreezeRotation;

        groundFilter = new ContactFilter2D();
        groundFilter.SetLayerMask(groundLayers);
        groundFilter.useTriggers = false;

        if (inputActions == null)
        {
            Debug.LogError($"Assign the Input System action asset to {nameof(PlayerMovement2D)} on '{name}'.", this);
            return;
        }

        playerActionMap = inputActions.FindActionMap("Player", false);
        if (playerActionMap != null)
        {
            moveAction = playerActionMap.FindAction("Move", false);
            jumpAction = playerActionMap.FindAction("Jump", false);
        }

        if (playerActionMap == null || moveAction == null || jumpAction == null)
            Debug.LogError("InputSystem_Actions must contain Player/Move and Player/Jump actions.", this);
    }

    private void OnEnable()
    {
        if (playerActionMap == null || moveAction == null || jumpAction == null)
            return;

        jumpAction.performed += OnJumpPerformed;
        jumpAction.canceled += OnJumpCanceled;
        playerActionMap.Enable();
    }

    private void Update()
    {
        if (moveAction != null)
            moveInput = Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f);

        if (spriteRenderer != null && Mathf.Abs(moveInput.x) > 0.01f)
            spriteRenderer.flipX = moveInput.x < 0f;
    }

    private void OnDisable()
    {
        if (jumpAction != null)
        {
            jumpAction.performed -= OnJumpPerformed;
            jumpAction.canceled -= OnJumpCanceled;
        }

        if (playerActionMap != null)
            playerActionMap.Disable();

        moveInput = Vector2.zero;
        jumpBufferCounter = 0f;
        coyoteCounter = 0f;
        jumpCutPending = false;
    }

    private void FixedUpdate()
    {
        float fixedDeltaTime = Time.fixedDeltaTime;
        IsGrounded = CheckGrounded();

        if (IsGrounded)
            coyoteCounter = coyoteTime;
        else
            coyoteCounter = Mathf.Max(0f, coyoteCounter - fixedDeltaTime);

        jumpBufferCounter = Mathf.Max(0f, jumpBufferCounter - fixedDeltaTime);

        Vector2 velocity = body.linearVelocity;
        float targetSpeed = moveInput.x * maxMoveSpeed;
        float speedChange = Mathf.Abs(targetSpeed) > 0.01f ? acceleration : deceleration;
        velocity.x = Mathf.MoveTowards(velocity.x, targetSpeed, speedChange * fixedDeltaTime);

        if (jumpBufferCounter > 0f && coyoteCounter > 0f)
        {
            velocity.y = jumpSpeed;
            jumpBufferCounter = 0f;
            coyoteCounter = 0f;
        }

        // Releasing jump early shortens the jump. Releasing after a buffered tap
        // still cuts the jump on the physics step when it begins.
        if (jumpCutPending)
        {
            if (velocity.y > 0f)
                velocity.y *= jumpCutMultiplier;

            jumpCutPending = false;
        }

        body.linearVelocity = velocity;
    }

    private void OnJumpPerformed(InputAction.CallbackContext context)
    {
        jumpBufferCounter = jumpBufferTime;
    }

    private void OnJumpCanceled(InputAction.CallbackContext context)
    {
        jumpCutPending = true;
    }

    private bool CheckGrounded()
    {
        Bounds bounds = bodyCollider.bounds;
        Vector2 probeSize = new Vector2(
            Mathf.Max(0.01f, bounds.size.x * groundCheckWidth),
            groundCheckDistance);
        Vector2 probeCenter = new Vector2(
            bounds.center.x,
            bounds.min.y - groundCheckDistance * 0.5f + groundCheckInset);

        int hitCount = Physics2D.OverlapBox(
            probeCenter,
            probeSize,
            0f,
            groundFilter,
            groundHits);

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = groundHits[i];
            if (hit == null)
                continue;

            if (hit.attachedRigidbody == body || hit.transform == transform || hit.transform.IsChildOf(transform))
                continue;

            return true;
        }

        return false;
    }

    private void OnDrawGizmosSelected()
    {
        CapsuleCollider2D capsule = bodyCollider != null ? bodyCollider : GetComponent<CapsuleCollider2D>();
        if (capsule == null)
            return;

        Bounds bounds = capsule.bounds;
        Vector3 size = new Vector3(
            Mathf.Max(0.01f, bounds.size.x * groundCheckWidth),
            groundCheckDistance,
            0.01f);
        Vector3 center = new Vector3(
            bounds.center.x,
            bounds.min.y - groundCheckDistance * 0.5f + groundCheckInset,
            bounds.center.z);

        Gizmos.color = IsGrounded ? Color.green : Color.yellow;
        Gizmos.DrawWireCube(center, size);
    }
}
