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
            // Fix-push P1-8: start facing south (yaw 180) so Houses 3, 4, 6
            // are in the opening frame (was yaw 0 = facing north).
            yaw = 180f;
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
    /// </summary>
    public void ResetToStarterView()
    {
        // Phase 4: same wide framing as the normal follow view (see
        // distance/height/pitch above) so a reset still shows >= 3 houses.
        // Fix-push P1-8: south-facing like the Start yaw (was north).
        transform.position = new Vector3(0f, 4f, 10f);
        transform.rotation = Quaternion.Euler(22f, 180f, 0f);
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
