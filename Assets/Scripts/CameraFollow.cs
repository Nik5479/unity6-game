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

    // PHONE-VIDEO FIX (2026-11-09, Worker C): camera occluders the shared
    // registry misses. The phone video (~21-24 s) shows the camera going
    // INSIDE the wooden slats of the stilt structure — the Dock pier at
    // (-7, 19) is ColliderMode.None, so VillageColliders has no entry for
    // it at all — and the stilt house. Two holes in the old test:
    //   (a) VillageColliders.CameraRay SKIPS any volume containing the ray
    //       origin (the player's head) — right for standing inside a house,
    //       wrong for a stilt house / porch overhang, where the head can sit
    //       inside the big rendered AABB while the camera is still out in
    //       the open and then swings into the slats;
    //   (b) ColliderMode.None structures are never registered, and low
    //       volumes are filtered out by the >= 1.5 m height rule (audit M1).
    // Fix, all inside this file (VillageBuilder is owned by another worker):
    // collect the big structures' rendered bounds from the scene itself,
    // ray-test them here with origin-inside handling, and take the nearest
    // hit of the two tests. Framing (distance 3.4 / height 1.15 / pitch 5)
    // is untouched — only collision.
    private readonly System.Collections.Generic.List<Bounds> camOccluders =
        new System.Collections.Generic.List<Bounds>();
    private bool occludersCollected;
    // PHONE-VIDEO FIX: smooth the camera DISTANCE along the head->want ray
    // instead of lerping the position. The old position Lerp let the camera
    // arc through walls during fast yaw turns (the lerped point sits off
    // the collision-checked ray); on the ray, every frame's position is a
    // collision-safe distance by construction.
    private float currentDist;
    private bool hasCurrentDist;

    // ROUND 5 (final chance, 2026-11-09): the user reported a small
    // downward drag flips the camera fully down, and the camera passes
    // through walls/objects. Two causes, both here:
    //   (a) Drag sensitivity: yaw 0.28 / pitch 0.12 deg per px moved the
    //       FULL pitch range (2..28) in ~200 px, with no dead zone and no
    //       smoothing — one flick slammed the camera to an extreme. Now
    //       slower (yaw 0.12, pitch 0.05), a 2 px dead zone, a tighter
    //       pitch clamp (3..18), and the live pitch/yaw ease toward
    //       TARGET values so a flick can never snap the camera.
    //   (b) Occluder staleness: the occluder list was collected ONCE, the
    //       first frame House_Player/Dock existed, while House_6 and other
    //       structures were still loading (no renderers / zero bounds) —
    //       so the camera could swing into the stilt house all session.
    //       The list now re-collects every 2 s until the village reports
    //       built, and VillageBuilder calls RefreshOccluders() at
    //       FinishBuild, so the final list covers every structure.
    public float dragSensitivityYaw = 0.12f;
    public float dragSensitivityPitch = 0.05f;
    public float dragDeadZonePx = 2f;
    public float dragSmoothRate = 10f;
    public float minPitch = 3f;
    public float maxPitch = 18f;
    private float targetYaw = 0f;
    private float targetPitch = 5f;
    private float lastOccluderRefresh = -10f;

    void OnEnable()
    {
        // ResetToStarterView disables this component; on re-enable the
        // distance smoothing must re-seed from the live camera position.
        hasCurrentDist = false;
    }

    // ROUND 5: re-collect the occluders from the live scene (clear +
    // rebuild). Called by VillageBuilder at FinishBuild and every 2 s
    // until the village reports built, so a structure that loads late
    // (House_6, Dock) is never missing from the camera collision test.
    public void RefreshOccluders()
    {
        occludersCollected = false;
        camOccluders.Clear();
        EnsureOccluders();
        lastOccluderRefresh = Time.time;
    }

    // Collect camera occluders from the scene itself. Runs lazily until the
    // runtime-built village exists (the camera is added by VillageBuilder,
    // possibly before the structures spawn). Recomputes from the CURRENT
    // renderers every call (ROUND 5: the old one-time snapshot missed
    // structures that had not loaded yet).
    private void EnsureOccluders()
    {
        if (occludersCollected) return;
        if (GameObject.Find("House_Player") == null && GameObject.Find("Dock") == null) return;
        camOccluders.Clear();
        occludersCollected = true;
        string[] roots = { "House_Player", "House_2", "House_3", "House_4", "House_5", "House_6",
            "Temple", "School", "Haveli", "Clinic", "Dock", "Well", "Gate",
            "HeroCar", "Tractor", "Cart", "BullockCart",
            "Tree_Mango", "Tree_Banyan", "Tree_Palm" };
        foreach (string name in roots)
        {
            GameObject go = GameObject.Find(name);
            if (go == null) continue;
            Renderer[] rr = go.GetComponentsInChildren<Renderer>();
            if (rr.Length == 0) continue;
            Bounds b = rr[0].bounds;
            foreach (Renderer r in rr) b.Encapsulate(r.bounds);
            camOccluders.Add(b);
        }
        // Fence segments: 9 segments x 4 boxes, all named FencePostA/B /
        // FenceRailTop/Low. Their 1.2 m height sits below the head->camera
        // ray (head 1.4 m, camera ~1.95 m), so they rarely trigger — included
        // so the camera can never dip into a fence line if framing ever lowers.
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (!t.name.StartsWith("Fence")) continue;
            Renderer r = t.GetComponent<Renderer>();
            if (r != null) camOccluders.Add(r.bounds);
        }
        Debug.Log("[Camera] Occluders collected: " + camOccluders.Count + " volumes");
    }

    // Ray vs one AABB, slab method. Unlike Bounds.IntersectRay, a ray that
    // STARTS inside the box is handled: tExit is the distance to the far
    // wall, so the caller can tell whether the WANTED camera point (at
    // maxDist) is still inside the box.
    private static bool RayBox(Vector3 origin, Vector3 dir, Bounds b, float maxDist, out float tEnter, out float tExit)
    {
        tEnter = 0f;
        tExit = maxDist;
        float t0 = 0f, t1 = maxDist;
        for (int a = 0; a < 3; a++)
        {
            float o = a == 0 ? origin.x : (a == 1 ? origin.y : origin.z);
            float d = a == 0 ? dir.x : (a == 1 ? dir.y : dir.z);
            float mn = a == 0 ? b.min.x : (a == 1 ? b.min.y : b.min.z);
            float mx = a == 0 ? b.max.x : (a == 1 ? b.max.y : b.max.z);
            if (Mathf.Abs(d) < 1e-8f)
            {
                if (o < mn || o > mx) return false;
            }
            else
            {
                float inv = 1f / d;
                float ta = (mn - o) * inv;
                float tb = (mx - o) * inv;
                if (ta > tb) { float tmp = ta; ta = tb; tb = tmp; }
                t0 = Mathf.Max(t0, ta);
                t1 = Mathf.Min(t1, tb);
                if (t0 > t1) return false;
            }
        }
        tEnter = t0;
        tExit = t1;
        return true;
    }

    // Nearest camera hit over the scene-discovered occluders. Origin-inside
    // boxes: the camera is inside the box only when the wanted point lies
    // before the far wall (maxDist <= tExit) — then pull in to the 1.2 m
    // minimum instead of skipping the box entirely (the old skip is what
    // let the camera into the stilt house). Head inside but camera past the
    // far wall means the camera is out in the open: no pull needed.
    private bool CameraRayLocal(Vector3 origin, Vector3 dir, float maxDist, out float hitDist)
    {
        hitDist = maxDist;
        bool hit = false;
        foreach (Bounds b in camOccluders)
        {
            float tEnter, tExit;
            if (!RayBox(origin, dir, b, maxDist, out tEnter, out tExit)) continue;
            if (tEnter >= 0f)
            {
                if (tEnter < hitDist) { hitDist = tEnter; hit = true; }
            }
            else if (maxDist <= tExit)
            {
                // Pull in tight: 1.6 pre-margin -> 1.2 m after the 0.4 margin.
                if (1.6f < hitDist) { hitDist = 1.6f; hit = true; }
            }
        }
        return hit;
    }

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
            targetYaw = yaw;
            targetPitch = pitch;
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
                // ROUND 5: the auto-centre steers the TARGET yaw; the live
                // yaw eases toward it below, so a manual drag is never
                // fought frame-by-frame.
                targetYaw = Mathf.MoveTowardsAngle(targetYaw, target.eulerAngles.y, 60f * Time.deltaTime);
            }
        }

        // ROUND 5: ease the live yaw/pitch toward their targets and clamp
        // the pitch every frame (a drag can only move the targets, so the
        // camera can never snap to an extreme in one frame, and a stale
        // pitch value can never survive a frame unclamped).
        float ease = 1f - Mathf.Exp(-dragSmoothRate * Time.deltaTime);
        yaw = Mathf.LerpAngle(yaw, targetYaw, ease);
        pitch = Mathf.Lerp(pitch, targetPitch, ease);
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        targetPitch = Mathf.Clamp(targetPitch, minPitch, maxPitch);

        // ROUND 5: re-collect the occluders every 2 s until the village
        // reports built, so late-loading structures join the collision
        // test as soon as they exist (the one-time snapshot missed them).
        if (!VillageBuilder.AllModelsLoaded && Time.time - lastOccluderRefresh > 2f)
        {
            RefreshOccluders();
        }

        Vector3 want;
        Vector3 look;
        ComputeFraming(yaw, out want, out look);

        // GTA V camera collision: never let a wall/house sit between the
        // player and the camera. Raycast from the player's head to the
        // wanted position; on a hit, pull the camera just in front of the
        // obstacle. Default view is unchanged (no hit on open ground), so
        // the screenshot test's >= 1 house framing is unaffected.
        // PHONE-VIDEO FIX: two tests now — the shared registry (houses,
        // walls, fences as movement blockers) AND the scene-discovered
        // occluders (Dock / stilt house / any ColliderMode.None structure
        // the registry never sees, with origin-inside handling). Take the
        // nearest hit of the two.
        Vector3 headPos = target.position + Vector3.up * 1.4f;
        Vector3 toCam = want - headPos;
        float toCamDist = toCam.magnitude;
        if (toCamDist > 0.01f)
        {
            EnsureOccluders();
            float hitDist = toCamDist;
            bool hit = false;
            float d;
            if (VillageColliders.CameraRay(headPos, toCam.normalized, toCamDist, out d) && d < hitDist)
            {
                hitDist = d;
                hit = true;
            }
            if (CameraRayLocal(headPos, toCam.normalized, toCamDist, out d) && d < hitDist)
            {
                hitDist = d;
                hit = true;
            }
            if (hit)
            {
                want = headPos + toCam.normalized * hitDist - toCam.normalized * 0.4f;
                if (Vector3.Distance(want, headPos) < 1.2f)
                    want = headPos + toCam.normalized * 1.2f;
            }
        }

        // PHONE-VIDEO FIX: smooth the DISTANCE along the head->want ray,
        // not the position. The old position Lerp let the camera arc through
        // walls on fast yaw turns (the lerped point sits off the
        // collision-checked ray). On the ray, the camera is always at a
        // collision-safe distance by construction. Pull-in is instant (a
        // wall swinging in front of the lens must not leave the camera
        // inside it for a single frame); pull-out eases at the old 8/s so
        // the view does not snap.
        float wantDist = Vector3.Distance(headPos, want);
        if (!hasCurrentDist)
        {
            currentDist = Vector3.Distance(headPos, transform.position);
            hasCurrentDist = true;
        }
        if (wantDist < currentDist)
        {
            currentDist = wantDist;
        }
        else
        {
            currentDist = Mathf.Lerp(currentDist, wantDist, 1f - Mathf.Exp(-8f * Time.deltaTime));
        }
        Vector3 dirToWant = toCamDist > 0.01f ? toCam.normalized : transform.forward;
        transform.position = headPos + dirToWant * currentDist;

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

    /// <summary>
    /// ROUND 5 (final chance): one place for drag input — touch and mouse
    /// both call this, and the PlayMode test calls it directly (real touch
    /// input cannot be injected in a test). A small drag no longer slams
    /// the camera: deltas under the dead zone are ignored, the rest move
    /// the TARGET pitch/yaw at the tuned sensitivity, and the targets are
    /// clamped so a full-screen flick can never flip the camera down.
    /// Drag down (delta.y &lt; 0) lowers the camera, drag up raises it —
    /// same mapping as before, only the magnitude changed.
    /// </summary>
    public void ApplyDragDelta(Vector2 delta)
    {
        if (delta.magnitude < dragDeadZonePx) return;
        targetYaw -= delta.x * dragSensitivityYaw;
        targetPitch = Mathf.Clamp(targetPitch + delta.y * dragSensitivityPitch, minPitch, maxPitch);
        lastManualDragTime = Time.time;
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
                ApplyDragDelta(delta);
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
            ApplyDragDelta(delta);
            lastDragPos = Input.mousePosition;
        }
        else if (Input.GetMouseButtonUp(0))
        {
            mouseDragging = false;
        }
    }
}
