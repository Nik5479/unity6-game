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
    public float moveSpeed = 5f;
    public float runMultiplier = 1.8f;
    public float jumpForce = 5f;

    // Set every frame by MobileControls (virtual joystick). Overrides keyboard.
    public Vector2 moveInput;
    // Set by the RUN button (hold).
    public bool runHeld;

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
            float speed = moveSpeed * (runHeld ? runMultiplier : 1f);
            Vector3 step = move.normalized * speed * Time.deltaTime;
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
        if (!isGrounded) return;
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
