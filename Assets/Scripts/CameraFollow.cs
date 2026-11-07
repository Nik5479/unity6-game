using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Third-person follow camera (Phase 1).
/// Follows the player from behind at a fixed distance/height.
/// Drag on the right half of the screen (outside UI) rotates the yaw;
/// mouse drag works the same in the Editor. AddShake is used by the
/// monster knocks for a horror jolt.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    public Transform target;
    // Phase 4 (fix-push): pulled back + higher + wider pitch so the
    // gameplay camera sees >= 3 houses from the chowk spawn. Run #19
    // failed the screenshot test with only 2 houses in the frustum at
    // distance 7 / height 3.4 / pitch 14 with the default 60-degree FOV.
    public float distance = 9f;
    public float height = 4.6f;
    public float pitch = 18f;

    private float yaw;
    private float shakeAmount;
    private int camFingerId = -1;
    private Vector2 lastDragPos;
    private bool mouseDragging;

    void Start()
    {
        if (target != null)
        {
            yaw = 0f;
        }
    }

    void LateUpdate()
    {
        HandleTouchDrag();
        HandleMouseDrag();

        if (target == null) return;

        Vector3 offset = Quaternion.Euler(pitch, yaw, 0f) * new Vector3(0f, 0f, -distance);
        Vector3 want = target.position + new Vector3(0f, height, 0f) + offset;
        transform.position = Vector3.Lerp(transform.position, want, 1f - Mathf.Exp(-8f * Time.deltaTime));

        Vector3 look = target.position + Vector3.up * 1.4f;
        if (shakeAmount > 0.01f)
        {
            look += Random.insideUnitSphere * shakeAmount * 0.18f;
            shakeAmount = Mathf.Max(0f, shakeAmount - Time.deltaTime * 2.2f);
        }
        transform.LookAt(look);
    }

    public void AddShake(float amount)
    {
        shakeAmount = Mathf.Max(shakeAmount, amount);
    }

    /// <summary>
    /// Reset to the known-good starter camera view and stop following.
    /// Used by VillageBuilder when the camera-frustum check fails, so the
    /// player always gets a view of the village instead of an empty frame.
    /// </summary>
    public void ResetToStarterView()
    {
        // Phase 4: same wide framing as the normal follow view (see
        // distance/height/pitch above) so a reset still shows >= 3 houses.
        transform.position = new Vector3(0f, 4f, -10f);
        transform.rotation = Quaternion.Euler(22f, 0f, 0f);
        enabled = false;
    }

    private bool IsTouchOnUI(int fingerId)
    {
        if (EventSystem.current == null) return false;
        return EventSystem.current.IsPointerOverGameObject(fingerId);
    }

    private void HandleTouchDrag()
    {
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch t = Input.GetTouch(i);
            if (t.phase == TouchPhase.Began)
            {
                // Right half of the screen, not starting on a UI element.
                if (t.position.x > Screen.width * 0.45f && !IsTouchOnUI(t.fingerId) && camFingerId == -1)
                {
                    camFingerId = t.fingerId;
                    lastDragPos = t.position;
                }
            }
            else if (t.phase == TouchPhase.Moved && t.fingerId == camFingerId)
            {
                Vector2 delta = t.position - lastDragPos;
                yaw -= delta.x * 0.28f;
                pitch = Mathf.Clamp(pitch + delta.y * 0.12f, 4f, 32f);
                lastDragPos = t.position;
            }
            else if ((t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) && t.fingerId == camFingerId)
            {
                camFingerId = -1;
            }
        }
    }

    private void HandleMouseDrag()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (Input.mousePosition.x > Screen.width * 0.45f && !IsTouchOnUI(-1))
            {
                mouseDragging = true;
                lastDragPos = Input.mousePosition;
            }
        }
        else if (Input.GetMouseButton(0) && mouseDragging)
        {
            Vector2 delta = (Vector2)Input.mousePosition - lastDragPos;
            yaw -= delta.x * 0.28f;
            pitch = Mathf.Clamp(pitch + delta.y * 0.12f, 4f, 32f);
            lastDragPos = Input.mousePosition;
        }
        else if (Input.GetMouseButtonUp(0))
        {
            mouseDragging = false;
        }
    }
}
