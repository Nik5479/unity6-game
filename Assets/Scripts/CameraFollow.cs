using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Third-person follow camera (GTA V pass, 2026-11-08).
/// Over-the-shoulder GTA V framing: close behind Nilesh, near head
/// height, looking slightly down the lane, hero large in the lower
/// centre-left of the frame — not the high surveillance view the user
/// rejected in the Phase 1 screenshots (distance 5.0 / height 2.2 /
/// pitch 12 put the lens ~3.7 m up, ~4.9 m back, ~21 deg down, and
/// Nilesh filled only ~20% of the frame).
/// Drag on the right half of the screen (outside UI) rotates the yaw;
/// mouse drag works the same in the Editor. AddShake is used by the
/// monster knocks for a horror jolt.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    public Transform target;
    // GTA V over-the-shoulder framing (fix 2026-11-08, user verdict on
    // the Phase 1 screenshots: "camera is NOT GTA V enough"). The
    // village camera runs at 70 deg vertical FOV (VillageBuilder), so
    // the visible height at the player is 2 * slant * tan(35 deg)
    // ~= 1.40 * slant. Nilesh is 1.79 m tall, so for him to fill
    // ~35-40% of the frame the slant distance must be ~3.4 m:
    // 1.79 / (1.40 * 3.45) ~= 0.37.
    //   distance 3.4:  camera ~3.4 m behind Nilesh (was 5.0).
    //   height 1.15:   camera ~1.95 m above the ground (player origin
    //                  sits at y 0.5, so 0.5 + 1.15 + the pitch lift
    //                  3.4 * sin(5 deg) ~= 0.30) — just above head
    //                  height, the way GTA V sits the lens over the
    //                  hero's shoulder instead of looking down at him.
    //   pitch 5:      near eye-level; the old 12 deg pitch plus the
    //                  2.2 m height is what made the ~21 deg down-angle.
    //   lookHeight 0.8: the camera looks at Nilesh's upper chest
    //                  (y ~= 1.3), not at y 1.9 above his head as
    //                  before — so his head sits above frame centre and
    //                  his feet near the bottom edge: hero lower-centre,
    //                  lane and houses reading over his shoulder, as in
    //                  the user's visual target (1001727684_1_8iul.png).
    //   shoulderOffset 0.65: GTA V sits the lens to the hero's right,
    //                  so Nilesh reads left-of-centre and the street
    //                  ahead stays open. Applied to the camera AND the
    //                  look target, so the view direction is unchanged.
    // CameraFollow is added at runtime by VillageBuilder (AddComponent),
    // so these field defaults ARE the live values — there is no
    // serialized scene copy to update.
    public float distance = 3.4f;
    public float height = 1.15f;
    public float pitch = 5f;
    public float lookHeight = 0.8f;
    public float shoulderOffset = 0.65f;

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

    // One place for the framing math: the wanted camera position and
    // look target for a given yaw. LateUpdate and ResetToStarterView
    // share it, so a reset can never bring back a different (high)
    // framing from the follow camera.
    private void ComputeFraming(float useYaw, out Vector3 camPos, out Vector3 lookAt)
    {
        Quaternion rot = Quaternion.Euler(pitch, useYaw, 0f);
        Vector3 right = Quaternion.Euler(0f, useYaw, 0f) * Vector3.right;
        Vector3 offset = rot * new Vector3(0f, 0f, -distance);
        camPos = target.position + new Vector3(0f, height, 0f) + offset
            + right * shoulderOffset;
        lookAt = target.position + Vector3.up * lookHeight
            + right * shoulderOffset;
    }

    void LateUpdate()
    {
        HandleTouchDrag();
        HandleMouseDrag();

        if (target == null) return;

        // GTA V auto-centre, gated on FORWARD motion (handoff §4.10 /
        // Phase 4: "camera auto-centre fights steering"). The old code
        // re-centred behind the player on ANY movement: movement is
        // camera-relative (PlayerController), so pushing the joystick
        // sideways turned Nilesh, the camera chased his new facing,
        // that changed what "forward" meant, and the player kept
        // circling — a feedback loop. Now the camera only eases behind
        // him while he is actually moving forward relative to the
        // camera (velocity within 40 deg of the camera's forward).
        // Strafing, turning on the spot and walking backwards leave the
        // camera where the player put it, so steering stays stable.
        // A manual drag still buys a 2.0 s grace period.
        float targetSpeed = 0f;
        Vector3 moveDir = Vector3.zero;
        if (hasPrevTargetPos)
        {
            Vector3 delta = target.position - prevTargetPos;
            delta.y = 0f;
            float moved = delta.magnitude;
            targetSpeed = moved / Mathf.Max(Time.deltaTime, 0.001f);
            if (moved > 0.001f) moveDir = delta / moved;
        }
        prevTargetPos = target.position;
        hasPrevTargetPos = true;
        if (targetSpeed > 0.5f && moveDir != Vector3.zero
            && Time.time - lastManualDragTime > 2.0f)
        {
            Vector3 camFwd = new Vector3(
                Mathf.Sin(yaw * Mathf.Deg2Rad), 0f, Mathf.Cos(yaw * Mathf.Deg2Rad));
            if (Vector3.Angle(moveDir, camFwd) < 40f)
            {
                yaw = Mathf.MoveTowardsAngle(yaw, target.eulerAngles.y, 60f * Time.deltaTime);
            }
        }

        Vector3 want;
        Vector3 look;
        ComputeFraming(yaw, out want, out look);

        // GTA V camera collision: never let a wall/house sit between the
        // player and the camera. Raycast from the player's head to the
        // wanted position; on a hit, pull the camera just in front of the
        // obstacle. Default view is unchanged (no hit on open ground), so
        // the screenshot test's >= 1 house framing is unaffected.
        Vector3 headPos = target.position + Vector3.up * 1.4f;
        Vector3 toCam = want - headPos;
        float toCamDist = toCam.magnitude;
        // Audit M1: test against the village's blocking volumes only (no
        // unmasked Physics.Raycast that hit fence rails/crates and missed
        // every glTF house).
        float hitDist;
        if (toCamDist > 0.01f
            && VillageColliders.CameraRay(headPos, toCam.normalized, toCamDist, out hitDist))
        {
            want = headPos + toCam.normalized * hitDist - toCam.normalized * 0.4f;
            if (Vector3.Distance(want, headPos) < 1.2f)
                want = headPos + toCam.normalized * 1.2f;
        }

        transform.position = Vector3.Lerp(transform.position, want, 1f - Mathf.Exp(-8f * Time.deltaTime));

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
    /// Same over-the-shoulder framing as the follow camera (via
    /// ComputeFraming) — a reset must never bring back the top-down
    /// look the user complained about.
    /// </summary>
    public void ResetToStarterView()
    {
        if (target != null)
        {
            Vector3 pos;
            Vector3 look;
            ComputeFraming(target.eulerAngles.y, out pos, out look);
            transform.position = pos;
            transform.LookAt(look);
        }
        else
        {
            transform.position = new Vector3(0f, 1.95f, -3.4f);
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
                pitch = Mathf.Clamp(pitch + delta.y * 0.12f, 2f, 28f);
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
            pitch = Mathf.Clamp(pitch + delta.y * 0.12f, 2f, 28f);
            lastDragPos = Input.mousePosition;
            lastManualDragTime = Time.time;
        }
        else if (Input.GetMouseButtonUp(0))
        {
            mouseDragging = false;
        }
    }
}
