using UnityEngine;

/// <summary>
/// Starter player controller for unity6-game.
/// WASD / arrow keys on keyboard, touch-drag on phone.
/// Full-screen landscape, Android target. No external assets needed.
/// </summary>
public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float jumpForce = 5f;

    private float yVelocity;
    private bool isGrounded = true;
    private Vector2 touchStart;
    private bool touchActive;

    void Update()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        // Touch: drag anywhere on screen to move.
        if (Input.touchCount > 0)
        {
            Touch t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Began)
            {
                touchStart = t.position;
                touchActive = true;
            }
            else if (t.phase == TouchPhase.Moved && touchActive)
            {
                Vector2 delta = (t.position - touchStart) / 100f;
                h = Mathf.Clamp(delta.x, -1f, 1f);
                v = Mathf.Clamp(delta.y, -1f, 1f);
            }
            else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
            {
                touchActive = false;
            }
        }

        // Move in world space, camera is behind the player so this feels natural.
        Vector3 move = new Vector3(h, 0f, v);
        if (move.sqrMagnitude > 0.01f)
        {
            transform.Translate(move.normalized * moveSpeed * Time.deltaTime, Space.World);
            transform.position = new Vector3(
                Mathf.Clamp(transform.position.x, -24f, 24f),
                transform.position.y,
                Mathf.Clamp(transform.position.z, -24f, 24f));
        }

        // Jump: Space / Jump button.
        if ((Input.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.Space)) && isGrounded)
        {
            yVelocity = jumpForce;
            isGrounded = false;
        }

        // Simple gravity, ground is at y = 0.5 (player cube centre).
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
}
