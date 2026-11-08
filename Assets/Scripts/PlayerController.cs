using UnityEngine;

/// <summary>
/// Player controller for THE FORGOTTEN VILLAGE (Phase 1).
/// Hero: Nilesh. Keyboard WASD / arrows, on-screen virtual joystick
/// (MobileControls) and RUN / JUMP buttons on phone.
/// Movement is camera-relative. Simple AABB collision against the
/// village (walls, well, trees, closed door) via VillageColliders.
/// Temporary placeholder figure: the player cube's renderer is hidden
/// and a simple dark-clothed capsule figure is built as children
/// by VillageBuilder (no realistic human model yet).
/// </summary>
public class PlayerController : MonoBehaviour
{
    // Phase 1 real-world speeds (2026-11-08): a human walks ~1.4 m/s,
    // jogs ~2.5 m/s and runs ~4.0 m/s. The old 5 m/s walk / 9 m/s run
    // made Nilesh cross the whole village in seconds and looked
    // nothing like a person (owner's phone video).
    public const float WalkSpeed = 1.4f;
    public const float JogSpeed = 2.5f;
    public const float RunSpeed = 4.0f;
    // Acceleration / deceleration (m/s^2): reaching jog takes ~0.3 s,
    // stopping ~0.25 s — no more instant full speed / dead stops.
    public float accel = 8f;
    public float decel = 10f;
    public float jumpForce = 5f;
    // Actual smoothed ground speed (m/s), for the animator's foot-speed
    // matching. Read-only outside this class.
    public float CurrentSpeed { get; private set; }
    private float speedSmooth;

    // Set every frame by MobileControls (virtual joystick). Overrides keyboard.
    public Vector2 moveInput;
    // Set by the RUN button (hold).
    public bool runHeld;
    // OVERNIGHT R3: crouch (hold the CROUCH button). Slow crouch-walk,
    // no jumping while crouched. The animator plays the remapped
    // CrouchWalk clip.
    public bool crouchHeld;
    public bool IsCrouching { get; private set; }
    private const float kCrouchSpeed = 0.7f;

    // Set by VillageBuilder.
    public Camera playerCamera;

    private float yVelocity;
    private bool isGrounded = true;
    private const float kRadius = 0.45f;

    void Update()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        if (moveInput.sqrMagnitude > 0.01f)
        {
            h = moveInput.x;
            v = moveInput.y;
        }

        // Camera-relative movement (camera yaw only).
        float yaw = 0f;
        if (playerCamera != null)
        {
            yaw = playerCamera.transform.eulerAngles.y * Mathf.Deg2Rad;
        }
        Vector3 fwd = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
        Vector3 right = new Vector3(fwd.z, 0f, -fwd.x);
        Vector3 move = right * h + fwd * v;

        if (move.sqrMagnitude > 0.01f)
        {
            // OVERNIGHT R3: crouch state follows the held button (grounded only).
            IsCrouching = crouchHeld && isGrounded;
            // Joystick magnitude sets the speed: half deflection = walk,
            // full deflection = jog, RUN button held = run. Below half
            // deflection the walk scales with the stick (slow creep).
            // Crouching overrides: slow crouch-walk scaled by the stick.
            float mag = Mathf.Clamp01(move.magnitude);
            float target = IsCrouching ? kCrouchSpeed * mag
                : runHeld ? RunSpeed
                : (mag < 0.5f ? WalkSpeed * (mag / 0.5f)
                              : Mathf.Lerp(WalkSpeed, JogSpeed, (mag - 0.5f) / 0.5f));
            speedSmooth = Mathf.MoveTowards(speedSmooth, target,
                (target > speedSmooth ? accel : decel) * Time.deltaTime);
            CurrentSpeed = speedSmooth;
            Vector3 step = move.normalized * speedSmooth * Time.deltaTime;
            Vector3 p = transform.position;
            TryMove(ref p, step);
            p.x = Mathf.Clamp(p.x, -22f, 22f);
            p.z = Mathf.Clamp(p.z, -22f, 22f);
            transform.position = p;

            // Face the movement direction (visual figure rotates with the body).
            float targetYaw = Mathf.Atan2(move.x, move.z) * Mathf.Rad2Deg;
            float curYaw = transform.eulerAngles.y;
            float newYaw = Mathf.MoveTowardsAngle(curYaw, targetYaw, 720f * Time.deltaTime);
            transform.rotation = Quaternion.Euler(0f, newYaw, 0f);
        }
        else
        {
            // No input: decelerate to a stop (never snap to zero).
            // OVERNIGHT R3: standing still with the crouch button held
            // still counts as crouching (hide behind cover).
            IsCrouching = crouchHeld && isGrounded;
            speedSmooth = Mathf.MoveTowards(speedSmooth, 0f, decel * Time.deltaTime);
            CurrentSpeed = speedSmooth;
        }

        // Jump: Space / JUMP button.
        if ((Input.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.Space)) && isGrounded)
        {
            Jump();
        }

        // Simple gravity, ground is at y = 0.5 (player centre).
        if (!isGrounded)
        {
            yVelocity -= 20f * Time.deltaTime;
            Vector3 p = transform.position;
            p.y += yVelocity * Time.deltaTime;
            if (p.y <= 0.5f)
            {
                p.y = 0.5f;
                yVelocity = 0f;
                isGrounded = true;
            }
            transform.position = p;
        }
    }

    public void Jump()
    {
        if (!isGrounded || IsCrouching) return;
        yVelocity = jumpForce;
        isGrounded = false;
    }

    public void Interact()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.TryInteract();
        }
    }

    // Move with simple AABB collision: try full step, then X-only, then Z-only.
    private void TryMove(ref Vector3 p, Vector3 step)
    {
        Vector3 full = new Vector3(p.x + step.x, p.y, p.z + step.z);
        if (VillageColliders.CanMoveTo(full, kRadius))
        {
            p = full;
            return;
        }
        Vector3 onlyX = new Vector3(p.x + step.x, p.y, p.z);
        if (VillageColliders.CanMoveTo(onlyX, kRadius))
        {
            p = onlyX;
            return;
        }
        Vector3 onlyZ = new Vector3(p.x, p.y, p.z + step.z);
        if (VillageColliders.CanMoveTo(onlyZ, kRadius))
        {
            p = onlyZ;
        }
    }
}
