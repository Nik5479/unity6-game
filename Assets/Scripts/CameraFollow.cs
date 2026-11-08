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
    // GTA-style third-person (fix 2026-11-08, user video v1.0.2): the
    // old values (distance 9 / height 4.6 / pitch 18) put the camera
    // 7.4 m above the player, 8.6 m back, looking down at 35 degrees —
    // a bird's-eye surveillance view, not a third-person game camera.
    // GTA V sits ~4 m behind at ~2 m up, looking down ~10-15 degrees,
    // so the hero fills the frame and the street ahead reads. The old
    // high values were chosen only so the screenshot test saw >= 3
    // houses; that test's bar is now >= 1 house (see ScreenshotTest).
    public float distance = 4.5f;
    public float height = 2.0f;
    public float pitch = 10f;

    private float yaw;
    private float shakeAmount;
    private int camFingerId = -1;
    private Vector2 lastDragPos;
    private bool mouseDragging;
    // GTA V pass 2026-11-08: camera collision + auto-centre behind the
    // player while moving. lastManualDragTime gates the auto-centre so a
    // manual look-around is never fought by the camera.
    private float lastManualDragTime = -10f;
    private Vector3 prevTargetPos;
    private bool hasPrevTargetPos;

    void Start()
    {
        if (target != null)
        {
            // GTA fix 2026-11-08: start BEHIND the player (yaw 0 = camera
            // south of the chowk, facing north the way Nilesh faces), so
            // the opening frame is over his shoulder, not a front view.
            // The old yaw 180 put the camera NORTH of the player facing
            // south — in front of him, showing his face — chosen only to
            // frame Houses 3, 4, 6 for the screenshot test.
            yaw = 0f;
            prevTargetPos = target.position;
            hasPrevTargetPos = true;
        }
    }

    void LateUpdate()
    {
        HandleTouchDrag();
        HandleMouseDrag();

        if (target == null) return;

        // GTA V auto-centre: while the player is actually moving and the
        // user has not dragged the camera recently, ease the yaw behind
        // the player's facing direction (the player faces its movement
        // direction, PlayerController). Slow rate so it never snaps.
        float targetSpeed = 0f;
        if (hasPrevTargetPos)
        {
            targetSpeed = (target.position - prevTargetPos).magnitude / Mathf.Max(Time.deltaTime, 0.001f);
        }
        prevTargetPos = target.position;
        hasPrevTargetPos = true;
        if (targetSpeed > 0.5f && Time.time - lastManualDragTime > 1.5f)
        {
            yaw = Mathf.MoveTowardsAngle(yaw, target.eulerAngles.y, 60f * Time.deltaTime);
        }

        Vector3 offset = Quaternion.Euler(pitch, yaw, 0f) * new Vector3(0f, 0f, -distance);
        Vector3 want = target.position + new Vector3(0f, height, 0f) + offset;

        // GTA V camera collision: never let a wall/house sit between the
        // player and the camera. Raycast from the player's head to the
        // wanted position; on a hit, pull the camera just in front of the
        // obstacle. Default view is unchanged (no hit on open ground), so
        // the screenshot test's >= 3 houses framing is unaffected.
        Vector3 headPos = target.position + Vector3.up * 1.4f;
        Vector3 toCam = want - headPos;
        float toCamDist = toCam.magnitude;
        if (toCamDist > 0.01f
            && Physics.Raycast(headPos, toCam.normalized, out RaycastHit hit, toCamDist))
        {
            want = hit.point - toCam.normalized * 0.4f;
            if (Vector3.Distance(want, headPos) < 1.2f)
                want = headPos + toCam.normalized * 1.2f;
        }

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
    /// GTA fix 2026-11-08: the fallback is now the same over-the-shoulder
    /// framing as the follow camera (behind the player), not the old
    /// (0,4,10) high view — a reset must never bring back the top-down
    /// look the user complained about.
    /// </summary>
    public void ResetToStarterView()
    {
        if (target != null)
        {
            Vector3 offset = Quaternion.Euler(pitch, target.eulerAngles.y, 0f)
                * new Vector3(0f, 0f, -distance);
            transform.position = target.position + new Vector3(0f, height, 0f) + offset;
            transform.LookAt(target.position + Vector3.up * 1.4f);
        }
        else
        {
            transform.position = new Vector3(0f, 2f, -4.5f);
            transform.rotation = Quaternion.Euler(10f, 0f, 0f);
        }
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
                lastManualDragTime = Time.time;
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
            lastManualDragTime = Time.time;
        }
        else if (Input.GetMouseButtonUp(0))
        {
            mouseDragging = false;
        }
    }
}
