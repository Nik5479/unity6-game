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
    // ROUND 6 (2026-11-10): GTA-like speeds — the user's v1.0.7 phone
    // verdict was "chalne me run karne me kitna slow ho jata hai"
    // (walk/run far too slow on the phone). The old 1.4 / 2.5 / 4.0
    // were real-world walking speeds, but the village is ~44 m across
    // and the phone video showed Nilesh covering almost no ground in
    // 35 s. Walk 2.0 / Jog 3.5 / Run 6.5 m/s: a run still takes ~7 s
    // to cross the village (no teleporting), but the game finally
    // feels like GTA instead of a slow shuffle. Supersedes the old
    // "fast speed na ho" order (Round 4) — the user explicitly asked
    // for faster movement after playing v1.0.7.
    public const float WalkSpeed = 2.0f;
    public const float JogSpeed = 3.5f;
    public const float RunSpeed = 6.5f;
    // Acceleration / deceleration (m/s^2): reaching jog takes ~0.5 s,
    // stopping ~0.35 s — GTA weight, no more instant full speed / dead
    // stops. (ROUND 4 WALK: was 8/10 — too twitchy.)
    public float accel = 5f;
    public float decel = 7f;
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

    // ROUND 4 (loading): input gate — false while the village is
    // still building, true once FinishBuild() releases it. The
    // v1.0.5 phone video showed the player walking an incomplete
    // village for ~15 s ("Village: building..." on screen). With the
    // gate closed the joystick/buttons are hidden AND dead; with it
    // open the game plays exactly as before.
    public bool inputEnabled = false;

    private float yVelocity;
    private bool isGrounded = true;
    private const float kRadius = 0.45f;

    // ROUND 5c (feet clipping): the temple plinth's first step is
    // ~0.34 m tall and the top step ~1.27 m — a player standing on the
    // steps must have his feet ON the step surface, not at terrain
    // height (y=0). The controller is transform-based (no
    // CharacterController / raycasts), so it samples the walkable-top
    // registry (VillageColliders.GroundHeightAt) every frame. StepUp is
    // the tallest single step the player can climb: 0.45 m covers the
    // temple's 0.34 m first step with margin, and still blocks walls /
    // the 1.43 m shrine platform from the ground.
    public const float StepUp = 0.45f;

    void Update()
    {
        // ROUND 4 (loading): gate closed — no movement, no jump, no
        // crouch. Zero the inputs so there is no residual drift when
        // the gate opens, and keep gravity running so a spawn above
        // ground still settles. Buttons call Jump()/Interact() directly
        // (bypassing Update), so those are guarded individually too.
        if (!inputEnabled)
        {
            moveInput = Vector2.zero;
            runHeld = false;
            crouchHeld = false;
            IsCrouching = false;
            speedSmooth = 0f;
            CurrentSpeed = 0f;
            ApplyGravity();
            UpdateGrounding();
            return;
        }

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
            // full deflection = jog, RUN button held = run SCALED by the
            // stick (was: full RunSpeed regardless of mag — a barely
            // touched stick + RUN held = full sprint). Below half
            // deflection the walk scales with the stick (slow creep).
            // Crouching overrides: slow crouch-walk scaled by the stick.
            float mag = Mathf.Clamp01(move.magnitude);
            float target = IsCrouching ? kCrouchSpeed * mag
                : runHeld ? Mathf.Lerp(JogSpeed, RunSpeed, mag)
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
            float newYaw = Mathf.MoveTowardsAngle(curYaw, targetYaw, 360f * Time.deltaTime);
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

        ApplyGravity();
        UpdateGrounding();
    }

    // ROUND 5c: sample the walkable-top registry and keep the feet on
    // the surface under the player. Snap UP when the ground rose under
    // him (teleport / step-up onto a step), snap DOWN for small drops
    // (walking off a low step), and start FALLING when the ground fell
    // away by more than a step (walked off a high ledge). The
    // camera-collision test teleports the player onto the temple plinth
    // — without this snap his feet stayed at terrain height and sank
    // ~0.5 m into the steps. Only runs while grounded; a jump is
    // governed by ApplyGravity.
    private void UpdateGrounding()
    {
        if (!isGrounded) return;
        Vector3 p = transform.position;
        float feetY = p.y - 0.5f;
        float ground = VillageColliders.GroundHeightAt(p.x, p.z);
        if (ground > feetY + 0.001f)
        {
            // Ground rose under him (teleport onto a step, or stepped
            // up): snap the feet onto it.
            p.y = ground + 0.5f;
            transform.position = p;
        }
        else if (feetY - ground > StepUp)
        {
            // Ground fell away by more than a step (walked off a high
            // ledge): start falling.
            isGrounded = false;
            yVelocity = 0f;
        }
        else if (feetY > ground + 0.001f)
        {
            // Small drop (walked off a low step): snap down.
            p.y = ground + 0.5f;
            transform.position = p;
        }
    }

    // Simple gravity, ground is the sampled walkable-top height under
    // the player (ROUND 5c: was hardcoded y = 0.5 centre, so a player
    // who jumped on the temple steps landed THROUGH them at terrain
    // height). Extracted so the loading gate can keep it running while
    // input is closed.
    private void ApplyGravity()
    {
        if (!isGrounded)
        {
            yVelocity -= 20f * Time.deltaTime;
            Vector3 p = transform.position;
            p.y += yVelocity * Time.deltaTime;
            float ground = VillageColliders.GroundHeightAt(p.x, p.z);
            if (p.y <= ground + 0.5f)
            {
                p.y = ground + 0.5f;
                yVelocity = 0f;
                isGrounded = true;
            }
            transform.position = p;
        }
    }

    public void Jump()
    {
        if (!inputEnabled || !isGrounded || IsCrouching) return;
        yVelocity = jumpForce;
        isGrounded = false;
    }

    public void Interact()
    {
        if (!inputEnabled) return;
        if (GameManager.Instance != null)
        {
            GameManager.Instance.TryInteract();
        }
    }

    // Move with simple AABB collision: try full step, then X-only, then Z-only.
    // ROUND 5c: never step UP onto a surface higher than one step above
    // the feet (a wall / the shrine platform from the ground). The
    // grounding snap handles legitimate steps (<= StepUp).
    private void TryMove(ref Vector3 p, Vector3 step)
    {
        float feetY = p.y - 0.5f;
        Vector3 full = new Vector3(p.x + step.x, p.y, p.z + step.z);
        if (VillageColliders.GroundHeightAt(full.x, full.z) <= feetY + StepUp
            && VillageColliders.CanMoveToDynamic(full, kRadius, null))
        {
            p = full;
            return;
        }
        Vector3 onlyX = new Vector3(p.x + step.x, p.y, p.z);
        if (VillageColliders.GroundHeightAt(onlyX.x, onlyX.z) <= feetY + StepUp
            && VillageColliders.CanMoveToDynamic(onlyX, kRadius, null))
        {
            p = onlyX;
            return;
        }
        Vector3 onlyZ = new Vector3(p.x, p.y, p.z + step.z);
        if (VillageColliders.GroundHeightAt(onlyZ.x, onlyZ.z) <= feetY + StepUp
            && VillageColliders.CanMoveToDynamic(onlyZ, kRadius, null))
        {
            p = onlyZ;
        }
    }
}
