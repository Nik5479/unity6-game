using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Static AABB collision registry. VillageBuilder registers wall
/// segments, well, trees, fences and the closed door as XZ bounds;
/// PlayerController checks proposed positions against them.
/// </summary>
public static class VillageColliders
{
    private static readonly List<Bounds> s_blocks = new List<Bounds>();

    // ROUND 5c (feet clipping, 2026-11-10): walkable raised surfaces.
    // The player controller grounds to the terrain plane (y=0) and never
    // knew about raised walkable geometry — the temple plinth steps are
    // ~0.34 m (first) to ~1.27 m (top) tall, so a player standing on
    // them sank to his shins (user's screenshot 2026-11-10 00:09 IST:
    // the camera-collision test teleports him to (6.27, -14), on the
    // plinth's 4th tier, feet at y=0, step top ~0.5 m). A walkable top
    // is an XZ square with a top Y; GroundHeightAt returns the HIGHEST
    // top containing (x,z), else 0 (terrain). Nested squares model the
    // stepped plinth (12 tiers) and the temple platform — the highest
    // applicable top is the surface under the player's feet.
    private struct WalkableTop { public float cx, cz, half, top; }
    private static readonly List<WalkableTop> s_walkables = new List<WalkableTop>();

    public static void RegisterWalkable(float cx, float cz, float half, float top)
    {
        s_walkables.Add(new WalkableTop { cx = cx, cz = cz, half = half, top = top });
    }

    public static float GroundHeightAt(float x, float z)
    {
        float g = 0f;
        foreach (WalkableTop w in s_walkables)
        {
            if (Mathf.Abs(x - w.cx) <= w.half && Mathf.Abs(z - w.cz) <= w.half)
            {
                if (w.top > g) g = w.top;
            }
        }
        return g;
    }

    public static void Register(Bounds b)
    {
        s_blocks.Add(b);
    }

    public static void Unregister(Bounds b)
    {
        s_blocks.Remove(b);
    }

    // ROUND 4: remove the (single) small registered volume containing
    // a point — an animal's own FullBounds entry, whose exact Bounds
    // may have shifted a hair since registration (pose / normalise),
    // so List.Remove silently misses. Size-guarded (< 4 m on its
    // longest horizontal side) so a house/wall footprint can never be
    // removed by mistake, and vertically overlapped with [0, 2] (the
    // animal's own height band).
    public static bool UnregisterContaining(Vector3 pos)
    {
        for (int i = s_blocks.Count - 1; i >= 0; i--)
        {
            Bounds b = s_blocks[i];
            if (pos.x < b.min.x || pos.x > b.max.x) continue;
            if (pos.z < b.min.z || pos.z > b.max.z) continue;
            if (0f > b.max.y || 2f < b.min.y) continue;
            if (Mathf.Max(b.size.x, b.size.z) >= 4f) continue;
            s_blocks.RemoveAt(i);
            return true;
        }
        return false;
    }

    // ROUND 6 (aar-paar, 2026-11-10): dynamic blockers — NPCs, animals
    // and the monster. They MOVE, so a static Bounds entry can never
    // follow them, and registering their FullBounds froze them (the
    // animal's own-bounds bug: its position sat at the centre of its
    // own blocker, so every step failed). A dynamic blocker is a
    // circle (XZ) that follows a Transform; CanMoveToDynamic checks
    // static blocks AND dynamics. The pathfinder keeps using
    // static-only CanMoveTo — paths are planned over fixed geometry,
    // dynamic avoidance is per-step. The PLAYER is never a dynamic
    // blocker (the monster must be able to reach him for game over).
    private struct DynamicBlock { public Transform t; public float radius; public float yMin; public float yMax; }
    private static readonly List<DynamicBlock> s_dynamics = new List<DynamicBlock>();

    public static void RegisterDynamic(Transform t, float radius)
    {
        if (t == null) return;
        for (int i = 0; i < s_dynamics.Count; i++)
            if (s_dynamics[i].t == t) return; // already registered
        s_dynamics.Add(new DynamicBlock { t = t, radius = radius, yMin = 0f, yMax = 2.2f });
    }

    public static void UnregisterDynamic(Transform t)
    {
        for (int i = s_dynamics.Count - 1; i >= 0; i--)
            if (s_dynamics[i].t == t) { s_dynamics.RemoveAt(i); return; }
    }

    public static int DynamicBlockerCount { get { return s_dynamics.Count; } }

    // Circle (XZ) vs static AABBs AND dynamic circles. `ignore` is the
    // mover's own transform — an NPC must not block itself.
    public static bool CanMoveToDynamic(Vector3 pos, float radius, Transform ignore)
    {
        if (!CanMoveTo(pos, radius)) return false;
        float feetY = pos.y - 0.5f;
        float headY = pos.y + 1.3f;
        foreach (DynamicBlock d in s_dynamics)
        {
            if (d.t == null || d.t == ignore) continue;
            if (feetY >= d.yMax || headY <= d.yMin) continue;
            float dx = pos.x - d.t.position.x;
            float dz = pos.z - d.t.position.z;
            float rr = radius + d.radius;
            if (dx * dx + dz * dz < rr * rr) return false;
        }
        return true;
    }

    public static void Clear()
    {
        s_blocks.Clear();
        s_walkables.Clear();
        s_dynamics.Clear();
    }

    // Camera collision (audit M1): ray vs the registered blocking volumes
    // (glTF houses/well/walls have no physics colliders, only these AABBs).
    // Only volumes big enough to hide the player count (>= 1.5 m tall and
    // >= 1 m wide), so fence rails and small props no longer make the
    // camera jerk forward, and volumes that contain the ray origin (the
    // player standing inside a house's outer bounds) are ignored.
    public static bool CameraRay(Vector3 origin, Vector3 dir, float maxDist, out float hitDist)
    {
        hitDist = maxDist;
        bool hit = false;
        Ray ray = new Ray(origin, dir);
        foreach (Bounds b in s_blocks)
        {
            if (b.size.y < 1.5f || Mathf.Max(b.size.x, b.size.z) < 1f) continue;
            if (b.Contains(origin)) continue;
            float d;
            if (b.IntersectRay(ray, out d) && d < hitDist)
            {
                hitDist = d;
                hit = true;
            }
        }
        return hit;
    }

    // Circle (XZ) vs AABB test, radius in metres.
    public static bool CanMoveTo(Vector3 pos, float radius)
    {
        foreach (Bounds b in s_blocks)
        {
            float cx = Mathf.Clamp(pos.x, b.min.x, b.max.x);
            float cz = Mathf.Clamp(pos.z, b.min.z, b.max.z);
            float dx = pos.x - cx;
            float dz = pos.z - cz;
            if (dx * dx + dz * dz < radius * radius)
            {
                // Also require vertical overlap (doors/walls are ground level).
                // AUDIT FIX 2026-11-08 (B1, blocking): pos.y is the player
                // CENTRE (0.5 m above ground), so the interval must be
                // feet-based [pos.y-0.5, pos.y+1.3] (1.8 m tall). The old
                // centre-based [pos.y, pos.y+1.8] overstated height by
                // 0.5 m, so the doorway lintel (min.y 2.2) blocked the
                // player forever and the house was unenterable.
                float feetY = pos.y - 0.5f;
                float headY = pos.y + 1.3f;
                if (feetY < b.max.y && headY > b.min.y)
                {
                    return false;
                }
            }
        }
        return true;
    }
}

/// <summary>
/// THE FORGOTTEN VILLAGE — Phase 3 village builder.
/// Builds the whole scene at runtime:
///  - Phase 3: REAL Sketchfab .glb models (Assets/StreamingAssets/Models/)
///    loaded at runtime via glTFast — 6 DIFFERENT houses (thatched hut
///    shell for Nilesh's house + 5 other house models), real mango/
///    banyan/palm trees, real well, carts, tractor, gate, animals
///    (cow/goat/buffalo/horse/dog), handpump, matka, hospital bed,
///    oil barrel. Each model is normalised by its rendered bounds
///    (Sketchfab units are inconsistent) and gets an AABB collider.
///    A model that fails to load falls back to the Phase 2 primitive
///    (houses/trees/well) or is skipped (props/animals) — never fatal.
///  - Phase 2 primitives remain as the structural fallback + the lane,
///    field, fences, moon and the jungle-edge tree ring.
///  - working door (hinge) on Nilesh's house, monster figure, food
///    pickup near the well
///  - player figure: the REAL rigged indian_man.glb (Phase 4) as Nilesh;
///    the Phase 2 clothed humanoid (cream kurta, skin head, saffron
///    pagdi, dark pants) survives only as a fallback if the .glb fails
///    to load. Same capsule collider as before.
///  - Phase 4 (fix-push): the screenshot test failed on run #19 (only 2
///    of 6 houses in the camera frustum), so the follow camera is pulled
///    back / raised / widened (CameraFollow distance 9, height 4.6,
///    pitch 18 + 70-degree FOV set in SetupSystems) and VerifyCameraView
///    now requires the same >= 3 houses the test requires. New real
///    models: school, temple (real hindu_temple .glb + pujari), chickens, dock, basket,
///    wooden stand, sandstone ground, soil + forest floor (.gltf), grave
///    stones, small grave (FBX), fallen pine (FBX), diya + chowk lanterns
///    (.gltf), neem substitute (PlantCatalog chinaberry, 2 instances),
///    MEERA (rigged indian_woman_in_saree, chowk NPC), HAVELI + MUKHIYA
///    (Meshy free tier), and a second rigged indian_man (sunglasses)
///    as a villager
///    NPC. Poly Haven CC0 1K textures are used for brick, thatch, mud
///    and cobble materials.
/// Materials are real .mat assets (Assets/Resources/Materials/) loaded by
/// name, so the Standard shader is referenced by assets and included in
/// the build — runtime Shader.Find is only a fallback. Each build step is
/// wrapped in try/catch so one failure cannot kill the whole village, and
/// an on-screen boot status line reports what was built. The glTF
/// shaders (glTF/PbrMetallicRoughness etc.) are in AlwaysIncludedShaders
/// so glTFast can find them in the player build too — without them the
/// runtime-loaded models would render magenta on the device.
/// </summary>
public class VillageBuilder : MonoBehaviour
{
    // Wired out for other systems.
    public DoorController playerDoor;
    public Transform monster;
    // Follow-up 2026-11-07: the real HellDemon model, loaded async during
    // the village build; BuildMonster() returns it when present.
    private GameObject monsterModel;
    public Transform doorFront;
    public Pickup foodPickup;
    public Pickup waterPickup;
    public Pickup medicinePickup;
    public Pickup batteryPickup;
    // Fix-push P2-15: NPC spots (same constants as the LoadProp calls in
    // BuildNewModelsAsync, so the lines work even if a model fails to load).
    // PILOT: Mukhiya stood at the chowk (-3.5, 2.5) as a STATIC unrigged
    // NPC — a statue among living villagers. He moves to his haveli
    // (13.5, -3.5), OUTSIDE the pilot bounds, where a standing elder by
    // his own house is plausible. (Pilot bounds: x -12..+10, z -19..+17.)
    private Vector3 mukhiyaPos = new Vector3(13.5f, 0f, -3.5f);
    private Vector3 meeraPos = new Vector3(0.5f, 0f, 6.5f);
    // (x 2 -> 3.6, 2026-11-07: the visual Pujari was moved 1.6 m east by
    // cheap-wins; the NPC trigger must sit on the visible priest.)
    // Phase 1 (2026-11-08): the temple is real-size now (10 m tall, front
    // at z=-10) — the priest moved with it to (4.5, -8.5), just outside
    // the entrance, still east of the Nandi-shivling sightline.
    private Vector3 pujariPos = new Vector3(4.5f, 0f, -8.5f);
    public Light lampLight;
    public Material moonMaterial;
    public GameObject lampGlow;
    public GameObject moonGlow;
    // ROUND 4 DOOR: persistent marker glow over the player's house.
    public GameObject houseMarkerGlow;
    // OVERNIGHT R1: the moon sphere itself (hidden by day, see DayNightCycle.moonObject).
    public GameObject moonGO;

    private Material matGround;
    private Material matPlaster;
    private Material matPlaster2;
    private Material matWood;
    private Material matCrate; // ROUND 5b: warm-brown crate body (wood_planks.jpg is grey).
    private Material matRoof;
    private Material matStone;
    private Material matGrass;
    private Material matDark;
    private Material matGlass;
    private Material matSkin;
    private Material matKurtaCream;
    private Material matPagdi;
    private Material matPants;
    private Material matEye;
    private Material matLampGlass;
    // OVERNIGHT R1: weathered dark grey-brown iron for the lamp pole /
    // frame. The old matDark (0.02 near-black) pole + emissive-yellow
    // head cube read as "black pole + yellow cube" by day (audit).
    private Material matLampPole;
    private Material matShadowBlob;
    // Phase 4: Poly Haven CC0 1K textures (Assets/Resources/Textures/).
    private Material matBrick;
    private Material matThatch;
    private Material matMudDry;
    private Material matMudWet; // PILOT: darkened wet-lane variant of matMudDry.
    // PHONE-VIDEO 2026-11-09: matMudFlat (OVERNIGHT R3, texture-less flat
    // mud) is GONE — the flat colour + 0.85 smoothness read as pale grey
    // floating plates on the phone (frames 22s/37s) and in CI.
    // ROUND 3 (2026-11-09): the textured flat PATCHES are gone too —
    // they still read as ovals (CI shot-07/shot-10). Wet lanes / chowk /
    // leaf litter now live IN the ground plane's own macro texture
    // (BuildGroundMacroTexture); these materials are kept only so their
    // source textures stay referenced — no patch object uses them.
    private Material matCobble;
    private Material matLeaves;
    private Material matWitheredGrass;

    private Transform villageRoot;
    private Camera mainCam;
    private Shader standardShader;
    private Text bootStatusText;
    // ROUND 4 (loading): full-screen dim + centre line shown while the
    // village builds, hidden by FinishBuild(). The BootStatus text
    // (per-model progress) sits above it.
    private GameObject loadingOverlay;
    // ROUND 4 (loading fix): the MobileCanvas reference, cached in Awake.
    // GameObject.Find CANNOT find an inactive object, so FinishBuild()
    // must use this field — Find("MobileCanvas") there returns null and
    // the HUD stays hidden forever (joystick/buttons never come back).
    private GameObject mobileCanvas;
    private int buildErrors;

    // Phase 3: real .glb models load async via glTFast. The CI test waits
    // for this flag (signalled by a "ModelsLoaded" GameObject — the test
    // assembly cannot reference game classes) before asserting/screenshots.
    public static bool AllModelsLoaded;
    // Bed spot inside Nilesh's house (set while building the house, so the
    // real hospital-bed model lands exactly where the primitive bed was).
    private Vector3 playerBedPos = new Vector3(-11f, 0f, 4.3f);
    // ROUND 4 DOOR: the player's hut footprint (world bounds, measured
    // while building the house) — dawn survival uses these half extents,
    // not the old hardcoded ±3.5/±3.0. Default = the old values, so a
    // house that never builds still leaves a sane check.
    private Bounds playerHouseBounds = new Bounds(new Vector3(-9f, 0f, 6f), new Vector3(7f, 4f, 6f));

    // FREEZE FIX 2026-11-08: the village build is ~50 sequential glTF
    // loads. Editor: ~22 s. A budget phone (4 GB RAM, slow flash):
    // minutes — and any single load that never returned stalled the whole
    // chain forever, so SetupSystems never ran (no DayNightCycle, no
    // GameManager) and the game sat frozen at "Village: building...
    // Field OK" for 5+ min. The build now has a hard deadline: after it,
    // remaining models are skipped and the game starts with whatever
    // loaded. 90 s is ~4x the editor build time (so the editor/CI full
    // build is unaffected) and still caps a budget phone's wait at a
    // minute and a half.
    // 2026-11-08 asset diet: models are now ~10x lighter (KTX2 textures,
    // simplified meshes), so the full village should load well inside
    // this. The deadline is only a safety net for a stalled load now.
    private const float kBuildDeadlineSeconds = 240f;

    void Awake()
    {
        // Landscape lock (runtime belt-and-braces for ProjectSettings):
        // the game must open in landscape even if the phone is held portrait.
        // NOTE: the old ProjectSettings.asset used the wrong field name
        // (defaultOrientation) which Unity ignores — fixed to
        // defaultScreenOrientation + allowedAutorotate flags.
        try
        {
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            // Both landscape ways allowed (audit M5): AutoRotation honours
            // the autorotate flags above; LandscapeRight used to lock it.
            Screen.orientation = ScreenOrientation.AutoRotation;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Village] Orientation lock failed: " + e.Message);
        }

        // Explicit 30 fps cap: stable frame pacing and less heat on
        // mid-range phones (Android's implicit default, now explicit).
        Application.targetFrameRate = 30;
        VillageColliders.Clear();
        mainCam = Camera.main;
        ApplyRenderDiagFlags();
        villageRoot = new GameObject("Village").transform;
        CreateBootStatus();
        // Camera + movement fix 2026-11-08 (user video, v1.0.2): wire the
        // GTA camera and the joystick BEFORE the village build starts, so
        // the user gets a third-person view and working controls from
        // second 0 instead of a static camera with no controls for the
        // whole ~10-30 s build (SetupSystems used to create both only at
        // the end of the build, and in the video the build never finished
        // in 33 s — so there was never a joystick at all).
        SetupCameraAndUIEarly();
        // ROUND 4 (loading): the HUD is built in SetupCameraAndUIEarly
        // (MobileControls.Awake runs synchronously on AddComponent), so
        // hide it now — the joystick/buttons sit behind the loading
        // screen until FinishBuild() shows them again. The input gate
        // in PlayerController is the real block; hiding the HUD is so
        // the player never sees dead controls. Cache the reference:
        // Find() cannot see it once it is inactive.
        mobileCanvas = GameObject.Find("MobileCanvas");
        if (mobileCanvas != null) mobileCanvas.SetActive(false);

        // Each step is isolated: one failing step must not kill the village.
        AllModelsLoaded = false;
        // Freeze fix: arm the build deadline and the per-model progress
        // display (so the player sees models loading, not a frozen
        // "Building..." line).
        buildStartTime = Time.realtimeSinceStartup;
        GltfModelLoader.BuildDeadline = Time.realtimeSinceStartup + kBuildDeadlineSeconds;
        GltfModelLoader.DeadlineReached = false;
        GltfModelLoader.LoadTimings.Clear();
        GltfModelLoader.OnProgress = f => SetBootStatus("Village: building... " + f);
        RunStep("Materials", BuildMaterials);
        RunStep("Ground", BuildGround);
        RunStep("PlayerFigure", BuildPlayerFigure);
        // The rest loads async: real .glb models come in via glTFast, and
        // the systems (door/monster/UI) are wired only after the village
        // exists. Intro + camera check start at the end of the coroutine.
        StartCoroutine(BuildVillageAsync());
    }

    // RENDER DIAGNOSTICS (audit H1, inverted colours on the emulator):
    // the CI script can push <persistentDataPath>/diag.txt containing
    // "nopost" and/or "nomsaa" and relaunch, so one CI run can tell
    // whether post-processing or the MSAA resolve corrupts the colours.
    // Without the file nothing changes. Logs the graphics API either way.
    private void ApplyRenderDiagFlags()
    {
        Debug.Log("[Village] Graphics: " + SystemInfo.graphicsDeviceType + " / " + SystemInfo.graphicsDeviceVersion
            + " / colour space " + QualitySettings.activeColorSpace);
        try
        {
            string path = System.IO.Path.Combine(Application.persistentDataPath, "diag.txt");
            if (!System.IO.File.Exists(path)) return;
            string flags = System.IO.File.ReadAllText(path);
            // Reflection keeps this file free of a compile-time URP
            // assembly reference.
            if (flags.Contains("nopost") && mainCam != null)
            {
                Component data = mainCam.GetComponent("UniversalAdditionalCameraData");
                var prop = (data != null) ? data.GetType().GetProperty("renderPostProcessing") : null;
                if (prop != null) prop.SetValue(data, false, null);
            }
            if (flags.Contains("nomsaa"))
            {
                object urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
                var prop = (urp != null) ? urp.GetType().GetProperty("msaaSampleCount") : null;
                if (prop != null && prop.CanWrite) prop.SetValue(urp, 1, null);
            }
            Debug.Log("[Village] Render diag flags: " + flags.Trim());
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Village] Render diag flags failed: " + e.Message);
        }
    }

    // Camera + movement fix 2026-11-08 (user video, v1.0.2): the follow
    // camera and the joystick used to be created only in SetupSystems, at
    // the END of the ~10-30 s village build — so for the whole build the
    // user saw a static scene camera with no controls at all, and Nilesh
    // could not move. They are wired here, before the build starts: a
    // GTA-style third-person camera behind Nilesh + a working joystick
    // from second 0. SetupSystems only fills in the parts that need the
    // built village (torch, game manager) and never creates a second copy.
    private void SetupCameraAndUIEarly()
    {
        GameObject player = GameObject.Find("Player");
        PlayerController pc = (player != null) ? player.GetComponent<PlayerController>() : null;
        if (pc != null)
        {
            pc.playerCamera = mainCam;
        }

        // Phase 4: wide 70-degree FOV so the gameplay camera sees the
        // chowk and the street ahead (was the scene default 60).
        if (mainCam != null)
        {
            mainCam.fieldOfView = 70f;
        }

        // Third-person follow camera (GTA values live in CameraFollow).
        if (mainCam != null)
        {
            CameraFollow follow = mainCam.GetComponent<CameraFollow>();
            if (follow == null)
            {
                follow = mainCam.gameObject.AddComponent<CameraFollow>();
            }
            follow.target = (player != null) ? player.transform : null;
        }

        // Mobile controls + HUD (joystick, buttons, subtitles).
        MobileControls ui = gameObject.GetComponent<MobileControls>();
        if (ui == null)
        {
            ui = gameObject.AddComponent<MobileControls>();
        }
        ui.player = pc;
    }

    // Phase 3: async village build. Real Sketchfab models load one by one
    // (each with a primitive fallback); a failed model never kills the
    // village. SetupSystems/SceneAmbience run after everything is placed.
    // AUDIT M4: an exception thrown inside a nested build iterator used to
    // end the whole coroutine chain without running the finally block, so
    // SetupSystems (night, monster, GameManager) never ran. SafeStep drives
    // the nested iterators itself, catches per step and moves on.
    private System.Collections.IEnumerator SafeStep(string name, System.Collections.IEnumerator root)
    {
        var stack = new System.Collections.Generic.Stack<System.Collections.IEnumerator>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            // ROUND 4 (pop-in): if the watchdog already called
            // FinishBuild() (buildFinished), stop placing objects — the
            // player is already playing behind the loading gate, and any
            // object placed after the gate opens is a pop-in.
            if (buildFinished) yield break;
            System.Collections.IEnumerator top = stack.Peek();
            bool moved = false; object cur = null; bool failed = false;
            try
            {
                moved = top.MoveNext();
                if (moved) cur = top.Current;
            }
            catch (System.Exception e)
            {
                Debug.LogError("[Village] Step " + name + " threw, skipping rest of it: " + e);
                failed = true;
            }
            if (failed) break;
            if (!moved) { stack.Pop(); continue; }
            System.Collections.IEnumerator nested = cur as System.Collections.IEnumerator;
            if (nested != null) { stack.Push(nested); continue; }
            yield return cur;
        }
    }

    // Watchdog: if the build has not finished shortly after the deadline
    // (e.g. a step hangs), wire the game systems anyway.
    private System.Collections.IEnumerator BuildWatchdog()
    {
        yield return new WaitForSecondsRealtime(kBuildDeadlineSeconds + 30f);
        if (!buildFinished)
        {
            Debug.LogWarning("[Village] Watchdog: build still running after deadline, starting game systems");
            AllModelsLoaded = true;
            FinishBuild();
        }
    }

    private System.Collections.IEnumerator BuildVillageAsync()
    {
        StartCoroutine(BuildWatchdog());
        try
        {
            yield return SafeStep("PlayerModel", BuildPlayerModelAsync());
            if (buildFinished) yield break;
            yield return SafeStep("Houses", BuildHousesAsync());
            if (buildFinished) yield break;
            yield return SafeStep("WellAndLamp", BuildWellAndLampAsync());
            if (buildFinished) yield break;
            // Freeze fix: the monster is gameplay-critical (the night
            // knock IS the game) — load it early, while the deadline
            // still allows it, instead of last (where a slow phone never
            // reached it and the game started with the capsule fallback).
            yield return SafeStep("MonsterModel", BuildMonsterModelAsync());
            if (buildFinished) yield break;
            RunStep("Field", BuildField);
            if (buildFinished) yield break;
            yield return SafeStep("JungleAndFences", BuildJungleAndFencesAsync());
            if (buildFinished) yield break;
            yield return SafeStep("Props", BuildPropsAsync());
            if (buildFinished) yield break;
            yield return SafeStep("NewModels", BuildNewModelsAsync());
            if (buildFinished) yield break;
            RunStep("WarmLights", BuildWarmLights);
            if (buildFinished) yield break;
            RunStep("CleanupStrays", CleanupStrays);
            if (buildFinished) yield break;
            RunStep("Moon", BuildMoon);
        }
        finally
        {
            // Always signal, even on failure, so the CI test never waits
            // forever — it proceeds with whatever did load.
            AllModelsLoaded = true;
            GameObject signal = new GameObject("ModelsLoaded");
            signal.transform.SetParent(villageRoot, false);
            // Freeze fix 2026-11-08: the game MUST start even when the
            // build was cut short by the deadline (or a step threw) —
            // SetupSystems creates the DayNightCycle + GameManager +
            // MonsterKnock, and without it the game sat frozen at
            // "Village: building... Field OK" for 5+ min on a 4 GB phone.
            // FinishBuild is idempotent, so it can never wire the
            // systems twice.
            FinishBuild();
        }
    }

    // Runs the end-of-build wiring exactly once, with whatever loaded:
    // DayNightCycle + GameManager + MonsterKnock + torch + UI fill-in,
    // the final boot status (the "Building..." text must clear), the
    // reflection probe, and the intro + camera check. Safe on a partial
    // village: SetupSystems null-checks the player/door, and
    // BuildMonster falls back to the capsule-man when the HellDemon
    // never loaded.
    private bool buildFinished;
    private float buildStartTime;
    private void FinishBuild()
    {
        if (buildFinished) return;
        buildFinished = true;
        // Dispose loads abandoned at the deadline once their tasks end.
        StartCoroutine(GltfModelLoader.ReapAbandoned());
        // Teardown guard: if the scene is going away, do not wire systems.
        if (villageRoot == null || gameObject == null || !gameObject.scene.isLoaded) return;
        RunStep("SetupSystems", SetupSystems);
        RunStep("SceneAmbience", SetupSceneAmbience);
        // ROUND 4 (loading): the village is built — release the input
        // gate and show the HUD. This runs for every exit (normal,
        // deadline-skip, watchdog) because FinishBuild is the single
        // funnel. The loading dim hides; the "N objects built" banner
        // stays 5 s (ClearBootStatusAfter) as before.
        GameObject player = GameObject.Find("Player");
        PlayerController pc = (player != null) ? player.GetComponent<PlayerController>() : null;
        if (pc != null) pc.inputEnabled = true;
        // ROUND 5 (camera): the village is fully loaded now — refresh the
        // camera's occluder list so late-loading structures (House_6, Dock)
        // join the collision test before the player can drag the camera.
        // The camera sat behind the loading screen until now, so the
        // refresh costs nothing visible.
        CameraFollow follow = mainCam != null ? mainCam.GetComponent<CameraFollow>() : null;
        if (follow != null) follow.RefreshOccluders();
        // ROUND 4 (loading fix): use the cached reference — Find() returns
        // null for the inactive canvas, which silently left the HUD hidden.
        if (mobileCanvas != null) mobileCanvas.SetActive(true);
        if (loadingOverlay != null) loadingOverlay.SetActive(false);
        bool cutShort = GltfModelLoader.DeadlineReached;
        SetBootStatus("Village: " + CountVillageObjects() + " objects built"
            + (cutShort ? " — build timed out, started with what loaded" : "")
            + (buildErrors > 0 ? " (" + buildErrors + " step errors — see log)" : ""));
        // Phase 1 (2026-11-08): the final "N objects built" line is a
        // debug banner — it must not sit on screen in the game (owner's
        // phone video). The build progress stays visible WHILE loading
        // (useful), then the banner clears 5 s after the build finishes.
        // The message itself is kept in LastBootStatus (and the log) so
        // CI tests can still verify the build finished / timed out.
        StartCoroutine(ClearBootStatusAfter(5f));
        // One-time environment reflection for the village (URP fix):
        // render the reflection probe now that every model is loaded.
        SetupReflectionProbe();
        StartCoroutine(IntroSequence());
        StartCoroutine(VerifyCameraView());
        // Diagnostics for CI (emulator-play.sh greps this line).
        Debug.Log("[Village] BuildStats: time=" + (Time.realtimeSinceStartup - buildStartTime).ToString("F1")
            + "s models=" + GltfModelLoader.LoadTimings.Count + " cutShort=" + cutShort
            + " slowest=" + SlowestLoads(5));
        if (cutShort)
        {
            Debug.LogWarning("[Village] Build cut short by the " + kBuildDeadlineSeconds
                + "s deadline — game started with a partial village. Slowest loads: "
                + SlowestLoads(5));
        }
    }

    // Slowest model loads, for the deadline log ("file: 12.3s, ...").
    private string SlowestLoads(int n)
    {
        var list = new System.Collections.Generic.List<string>(GltfModelLoader.LoadTimings);
        list.Sort((a, b) => ParseLoadSeconds(b).CompareTo(ParseLoadSeconds(a)));
        return string.Join(", ", list.GetRange(0, System.Math.Min(n, list.Count)).ToArray());
    }

    private float ParseLoadSeconds(string entry)
    {
        int c = entry.LastIndexOf(':');
        float v;
        if (c >= 0 && float.TryParse(entry.Substring(c + 1).Trim().TrimEnd('s'), out v)) return v;
        return 0f;
    }

    // Runs one build step; a failure is logged and the build continues.
    // NOTE (ROUND 4 pop-in fix, 2026-11-09): do NOT add a
    // "if (buildFinished) return;" guard here. FinishBuild() sets
    // buildFinished=true FIRST and then wires the game through this very
    // method (RunStep("SetupSystems", ...) / RunStep("SceneAmbience",
    // ...)) — a guard here silently skips SetupSystems, so the game
    // starts with no DayNightCycle, no GameManager and no monster knock
    // (that is exactly what broke CI build 42602733035: FreezeFixTest +
    // ScreenshotTest failed with "SetupSystems never ran"). The
    // watchdog-tail closure lives in SafeStep + the yield-break checks
    // between steps in BuildVillageAsync, which is where it belongs.
    private void RunStep(string name, System.Action step)
    {
        try
        {
            step();
            SetBootStatus("Village: building... " + name + " OK");
        }
        catch (System.Exception e)
        {
            buildErrors++;
            Debug.LogWarning("[Village] Build step FAILED: " + name + " — "
                + e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace);
            SetBootStatus("Village: ERROR in " + name + " — continuing");
        }
    }

    // On-screen boot status so a blank screen can never again be silent.
    private void CreateBootStatus()
    {
        try
        {
            GameObject canvasGO = new GameObject("BootStatusCanvas");
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            GameObject textGO = new GameObject("BootStatus");
            RectTransform rt = textGO.AddComponent<RectTransform>();
            rt.SetParent(canvasGO.transform, false);
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -30f);
            rt.sizeDelta = new Vector2(1100f, 40f);
            Text t = textGO.AddComponent<Text>();
            Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.font = f;
            t.fontSize = 24;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.text = "Village: starting...";
            MobileControls.ApplyUiMaterial(t, true);
            Shadow sh = textGO.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.9f);
            sh.effectDistance = new Vector2(2f, -2f);
            bootStatusText = t;
            // ROUND 4 (loading): full-screen dim + big centre line so
            // the build never looks like a frozen game. The per-model
            // progress (BootStatus text) stays on top of the dim — the
            // text is moved to last sibling below. Roman Hindi on
            // purpose: the boot font has no Devanagari glyphs.
            GameObject dimGO = new GameObject("LoadingDim");
            RectTransform dimRT = dimGO.AddComponent<RectTransform>();
            dimRT.SetParent(canvasGO.transform, false);
            dimRT.anchorMin = Vector2.zero;
            dimRT.anchorMax = Vector2.one;
            dimRT.sizeDelta = Vector2.zero;
            Image dimImg = dimGO.AddComponent<Image>();
            // ROUND 5 (final chance): the dim was 0.72 alpha, so the
            // player WATCHED the village assemble — empty mud plain,
            // fallback blocky Nilesh, houses popping in one by one — on
            // every launch AND every DOBARA KHELO reload (the user's
            // v1.0.6 video proves it: "Gaon ban raha hai..." over an empty
            // plain at 1 s, houses appearing at 2-5 s). A loading screen
            // that shows the build is not a loading screen. Fully opaque
            // now: the player sees only "Gaon ban raha hai..." + progress
            // until FinishBuild opens the gate with the complete village.
            dimImg.color = new Color(0f, 0f, 0f, 1f);
            GameObject loadGO = new GameObject("LoadingText");
            RectTransform loadRT = loadGO.AddComponent<RectTransform>();
            loadRT.SetParent(dimGO.transform, false);
            loadRT.anchorMin = new Vector2(0.5f, 0.5f);
            loadRT.anchorMax = new Vector2(0.5f, 0.5f);
            loadRT.anchoredPosition = Vector2.zero;
            loadRT.sizeDelta = new Vector2(1100f, 60f);
            Text lt = loadGO.AddComponent<Text>();
            lt.font = f;
            lt.fontSize = 34;
            lt.alignment = TextAnchor.MiddleCenter;
            lt.color = Color.white;
            lt.text = "Gaon ban raha hai...";
            MobileControls.ApplyUiMaterial(lt, true);
            Shadow lsh = loadGO.AddComponent<Shadow>();
            lsh.effectColor = new Color(0f, 0f, 0f, 0.9f);
            lsh.effectDistance = new Vector2(2f, -2f);
            loadingOverlay = dimGO;
            textGO.transform.SetAsLastSibling();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Village] Boot status UI failed: " + e.Message);
        }
    }

    // Last boot-status message (for CI tests: the on-screen text clears
    // after the build, but the message must stay checkable).
    public static string LastBootStatus = "";

    private System.Collections.IEnumerator ClearBootStatusAfter(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        if (bootStatusText != null) bootStatusText.text = "";
    }

    private void SetBootStatus(string msg)
    {
        LastBootStatus = msg;
        if (bootStatusText != null)
        {
            bootStatusText.text = msg;
        }
        Debug.Log("[Village] " + msg);
    }

    private int CountVillageObjects()
    {
        if (villageRoot == null) return 0;
        return villageRoot.GetComponentsInChildren<Transform>(true).Length - 1;
    }

    // After the follow camera settles, verify the player and at least one
    // house are actually inside the camera frustum. If not, reset to the
    // known-good starter view instead of showing an empty frame.
    private System.Collections.IEnumerator VerifyCameraView()
    {
        yield return new WaitForSeconds(0.6f);
        if (mainCam == null) yield break;
        try
        {
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(mainCam);
            bool playerVisible = false;
            GameObject player = GameObject.Find("Player");
            if (player != null)
            {
                Renderer pr = player.GetComponentInChildren<Renderer>();
                if (pr != null) playerVisible = GeometryUtility.TestPlanesAABB(planes, pr.bounds);
            }
            int housesVisible = 0;
            foreach (string houseName in new string[] { "House_Player", "House_2", "House_3", "House_4", "House_5", "House_6" })
            {
                GameObject h = GameObject.Find(houseName);
                if (h == null) continue;
                Renderer[] rs = h.GetComponentsInChildren<Renderer>();
                foreach (Renderer r in rs)
                {
                    if (GeometryUtility.TestPlanesAABB(planes, r.bounds)) { housesVisible++; break; }
                }
            }
            Debug.Log("[Village] Camera check: player visible=" + playerVisible + ", houses visible=" + housesVisible);
            // GTA camera fix 2026-11-08: the bar is >= 1 house, not >= 3.
            // The old >= 3 bar belonged to the high top-down camera; the
            // GTA camera sits 4.5 m behind Nilesh and shows the chowk +
            // the nearest house(s). Resetting to the starter view on a
            // close-up framing would bring back the top-down look.
            if (!playerVisible || housesVisible < 1)
            {
                Debug.LogWarning("[Village] Camera view check FAILED — resetting to starter view");
                SetBootStatus("Camera view failed — reset to starter view");
                CameraFollow follow = mainCam.GetComponent<CameraFollow>();
                if (follow != null) follow.ResetToStarterView();
                else
                {
                    mainCam.transform.position = new Vector3(0f, 3f, -8f);
                    mainCam.transform.rotation = Quaternion.Euler(20f, 0f, 0f);
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Village] Camera view check error: " + e.Message);
        }
    }

    void Update()
    {
        // Billboard the glow quads toward the camera.
        if (mainCam != null)
        {
            if (lampGlow != null)
            {
                lampGlow.transform.LookAt(mainCam.transform.position);
            }
            if (moonGlow != null)
            {
                moonGlow.transform.LookAt(mainCam.transform.position);
            }
            if (houseMarkerGlow != null)
            {
                houseMarkerGlow.transform.LookAt(mainCam.transform.position);
            }
        }
    }

    // ------------------------------------------------------------------
    // Materials. FIX (blank scene on device): materials were created at
    // runtime with Shader.Find("Standard"), but the Standard shader was
    // stripped from the Android build (no asset referenced it), so
    // new Material(null) threw and Awake died before building anything.
    // Now the six textured materials are real .mat assets under
    // Assets/Resources/Materials/ (their Standard-shader reference keeps
    // the shader in the build) and are loaded by name. Flat/emissive
    // materials are built at runtime from that same shader. Nothing here
    // may throw: a null material leaves the renderer's default material
    // (visible) instead of killing the village.
    // ------------------------------------------------------------------
    private Texture2D LoadTex(string name)
    {
        Texture2D t = Resources.Load<Texture2D>("Textures/" + name);
        if (t == null)
        {
            Debug.LogWarning("[Village] Texture missing, using flat colour: " + name);
        }
        return t;
    }

    private Material LoadMaterialAsset(string name)
    {
        try
        {
            Material m = Resources.Load<Material>("Materials/" + name);
            if (m == null)
            {
                Debug.LogWarning("[Village] Material asset missing: Materials/" + name);
            }
            return m;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Village] Material load failed: " + name + " — " + e.Message);
            return null;
        }
    }

    private void AssignTex(Material m, string texName, Vector2 tiling)
    {
        if (m == null) return;
        try
        {
            if (m.mainTexture == null)
            {
                Texture2D t = Resources.Load<Texture2D>("Textures/" + texName);
                if (t != null)
                {
                    m.mainTexture = t;
                    m.mainTextureScale = tiling;
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Village] Texture assign failed: " + texName + " — " + e.Message);
        }
    }

    private Shader GetStandardShader()
    {
        if (standardShader != null) return standardShader;
        if (matGround != null && matGround.shader != null)
        {
            standardShader = matGround.shader;
            return standardShader;
        }
        Shader s = null;
        try { s = Shader.Find("Universal Render Pipeline/Lit"); } catch (System.Exception) { s = null; }
        if (s == null) { try { s = Shader.Find("Standard"); } catch (System.Exception) { s = null; } }
        if (s != null) standardShader = s;
        return s;
    }

    private Material MakeMat(string name, Texture2D tex, Color tint, float smoothness, Vector2 tiling,
        Texture2D normalMap = null, Texture2D metalSmoothMap = null, Texture2D aoMap = null)
    {
        Shader s = GetStandardShader();
        if (s == null)
        {
            Debug.LogWarning("[Village] Standard shader not available for " + name);
            return null;
        }
        Material m = new Material(s);
        m.name = name;
        if (tex != null)
        {
            m.mainTexture = tex;
            m.mainTextureScale = tiling;
        }
        m.color = tint;
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_Glossiness", smoothness);
        m.SetFloat("_Smoothness", smoothness);
        // Cheap-wins PBR (2026-11-07): Poly Haven normal / metallic-smoothness
        // (R = metallic 0, A = smoothness from the roughness map) / AO maps.
        // URP Lit does NOT enable the keywords from SetTexture alone at
        // runtime — they must be enabled explicitly, or the maps are ignored.
        // Any map may be null (missing texture) — the material still works.
        if (normalMap != null)
        {
            m.SetTexture("_BumpMap", normalMap);
            m.SetTextureScale("_BumpMap", tiling);
            m.SetFloat("_BumpScale", 1f);
            m.EnableKeyword("_NORMALMAP");
        }
        if (metalSmoothMap != null)
        {
            m.SetTexture("_MetallicGlossMap", metalSmoothMap);
            m.SetTextureScale("_MetallicGlossMap", tiling);
            // The packed map drives smoothness via its alpha channel, so the
            // scalar multiplier must be 1 or it would flatten the map out.
            m.SetFloat("_Smoothness", 1f);
            m.SetFloat("_Glossiness", 1f);
            m.SetFloat("_SmoothnessTextureChannel", 0f);
            m.EnableKeyword("_METALLICSPECGLOSSMAP");
        }
        if (aoMap != null)
        {
            m.SetTexture("_OcclusionMap", aoMap);
            m.SetTextureScale("_OcclusionMap", tiling);
            m.SetFloat("_OcclusionStrength", 1f);
            m.EnableKeyword("_OCCLUSIONMAP");
        }
        return m;
    }

    // PILOT: add generic fine-grain PBR maps to a loaded .mat asset
    // (plaster/wood/roof/stone/grass have no matched PBR sets in the
    // repo). Mud's normal + metal-smooth are pattern-free at low bump,
    // so they add surface grain without fake seams. No AO: a mismatched
    // AO map would darken the wrong places.
    private void UpgradeRuntimeMat(Material m, float bump)
    {
        if (m == null) return;
        try
        {
            Texture2D normal = LoadTex("brown_mud_dry_nor_gl_1k");
            Texture2D metalSmooth = LoadTex("brown_mud_dry_metal_smooth_1k");
            Vector2 tiling = m.mainTextureScale;
            if (normal != null)
            {
                m.SetTexture("_BumpMap", normal);
                m.SetTextureScale("_BumpMap", tiling);
                m.SetFloat("_BumpScale", bump);
                m.EnableKeyword("_NORMALMAP");
            }
            if (metalSmooth != null)
            {
                m.SetTexture("_MetallicGlossMap", metalSmooth);
                m.SetTextureScale("_MetallicGlossMap", tiling);
                m.SetFloat("_Smoothness", 1f);
                m.SetFloat("_SmoothnessTextureChannel", 0f);
                m.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Village] PBR upgrade failed for " + m.name + " — " + e.Message);
        }
    }

    private Material MakeFlat(string name, Color color, float smoothness)
    {
        Shader s = GetStandardShader();
        if (s == null)
        {
            Debug.LogWarning("[Village] Standard shader not available for " + name);
            return null;
        }
        Material m = new Material(s);
        m.name = name;
        m.color = color;
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_Glossiness", smoothness);
        m.SetFloat("_Smoothness", smoothness);
        return m;
    }

    private Material MakeEmissive(string name, Color color, Color emission)
    {
        Shader s = GetStandardShader();
        if (s == null)
        {
            Debug.LogWarning("[Village] Standard shader not available for " + name);
            return null;
        }
        Material m = new Material(s);
        m.name = name;
        m.color = color;
        m.SetColor("_EmissionColor", emission);
        m.EnableKeyword("_EMISSION");
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_Glossiness", 0.2f);
        m.SetFloat("_Smoothness", 0.2f);
        return m;
    }

    private void BuildMaterials()
    {
        // Real .mat assets first (keeps the Standard shader in the build).
        matGround = LoadMaterialAsset("GroundMud");
        matPlaster = LoadMaterialAsset("WallPlaster");
        matWood = LoadMaterialAsset("WoodPlanks");
        matRoof = LoadMaterialAsset("RoofTiles");
        matStone = LoadMaterialAsset("StoneWall");
        matGrass = LoadMaterialAsset("FieldGrass");

        // Fallback: rebuild any missing .mat at runtime from its texture.
        // Phase 2 ground fix (2026-11-08): the Ground plane is Unity's
        // 10 m Plane at scale 50 = 500 m across, so the old 5x5 tiling
        // stretched one mud tile over 100 m and the ground read as flat
        // beige. 200x200 = 2.5 m per tile, the mud photo's real scale.
        if (matGround == null) matGround = MakeMat("GroundMud", LoadTex("ground_mud"), new Color(0.85f, 0.78f, 0.68f), 0.05f, new Vector2(200f, 200f));
        if (matPlaster == null) matPlaster = MakeMat("WallPlaster", LoadTex("wall_plaster"), new Color(0.92f, 0.85f, 0.72f), 0.15f, new Vector2(2f, 1.5f));
        if (matWood == null) matWood = MakeMat("WoodPlanks", LoadTex("wood_planks"), new Color(0.75f, 0.6f, 0.42f), 0.3f, new Vector2(1f, 1f));
        if (matRoof == null) matRoof = MakeMat("RoofTiles", LoadTex("roof_tiles"), new Color(0.95f, 0.8f, 0.62f), 0.25f, new Vector2(3f, 2f));
        if (matStone == null) matStone = MakeMat("StoneWall", LoadTex("stone_wall"), new Color(0.8f, 0.78f, 0.72f), 0.1f, new Vector2(2f, 1f));
        if (matGrass == null) matGrass = MakeMat("FieldGrass", LoadTex("field_grass"), new Color(0.7f, 0.8f, 0.55f), 0.05f, new Vector2(4f, 3f));

        // Safety net: re-assign textures by name if a loaded .mat lost its reference.
        // Ground tiling 200 (see above): the .mat asset itself is also
        // re-tiled to 200, so the plane is sharp even before this runs.
        AssignTex(matGround, "ground_mud", new Vector2(200f, 200f));
        AssignTex(matPlaster, "wall_plaster", new Vector2(2f, 1.5f));
        AssignTex(matWood, "wood_planks", new Vector2(1f, 1f));
        AssignTex(matRoof, "roof_tiles", new Vector2(3f, 2f));
        AssignTex(matStone, "stone_wall", new Vector2(2f, 1f));
        AssignTex(matGrass, "field_grass", new Vector2(4f, 3f));

        // PILOT 2026-11-08: the ground is the biggest surface in every
        // screenshot and it was albedo-only (GroundMud.mat = ground_mud.jpg,
        // no normal/roughness/AO). The whole 500 m plane is ONE object, so
        // the pilot ground fix is a full PBR rebuild from the matched
        // Poly Haven brown_mud_dry set (diff + normal + metal-smooth + AO),
        // same 200x200 tiling (2.5 m/tile). The .mat assets for plaster /
        // wood / roof / stone / grass have no matched PBR sets in the repo,
        // so they get a generic fine-grain normal + metal-smooth (mud's,
        // low bump) instead of mismatched pattern maps — surface grain
        // without fake seams. matMudWet is the darkened wet-lane variant
        // (reference: a wet road reads darker + shinier than dry ground).
        matGround = MakeMat("GroundMudPBR", LoadTex("brown_mud_dry_diff_1k"), Color.white, 0.05f, new Vector2(200f, 200f),
            LoadTex("brown_mud_dry_nor_gl_1k"), LoadTex("brown_mud_dry_metal_smooth_1k"), LoadTex("brown_mud_dry_ao_1k"));
        // ROUND 5b: the metal-smooth map averages 0.47 smoothness (peaks
        // 1.0) and MakeMat leaves the scalar at 1, so the ground
        // mirror-reflects the blue sky at grazing angles — the blue
        // smears in the ground close-up. Cap the scalar to 0.35: the map
        // still varies it, but the max is 0.35 and the average ~0.16.
        if (matGround != null) matGround.SetFloat("_Smoothness", 0.35f);
        UpgradeRuntimeMat(matPlaster, 0.3f);
        UpgradeRuntimeMat(matWood, 0.35f);
        UpgradeRuntimeMat(matRoof, 0.4f);
        UpgradeRuntimeMat(matStone, 0.4f);
        UpgradeRuntimeMat(matGrass, 0.3f);
        // PHONE-VIDEO 2026-11-09: matMudWet used to be built HERE — but
        // matMudDry is only created 60 lines below, so the `if (matMudDry
        // != null)` guard never fired and matMudWet stayed null for every
        // build; the wet lanes silently fell back to the texture-less
        // matMudFlat. It is now created right after matMudDry (below).

        // Resolve the lit shader once for the flat/emissive materials
        // (URP Lit under URP, Standard under Built-in — see GetStandardShader).
        standardShader = GetStandardShader();
        if (standardShader == null)
        {
            Debug.LogWarning("[Village] Standard shader NOT found even after .mat loads!");
        }

        matPlaster2 = MakeMat("WallPlaster2", LoadTex("wall_plaster"), new Color(0.85f, 0.76f, 0.62f), 0.15f, new Vector2(2f, 1.5f));
        matDark = MakeFlat("MonsterDark", new Color(0.02f, 0.02f, 0.025f), 0.0f);
        matCrate = MakeFlat("CrateWood", new Color(0.42f, 0.28f, 0.15f), 0.5f);
        matGlass = MakeFlat("WindowGlass", new Color(0.03f, 0.04f, 0.07f), 0.6f);
        matSkin = MakeFlat("Skin", new Color(0.45f, 0.30f, 0.20f), 0.4f);
        matKurtaCream = MakeFlat("KurtaCream", new Color(0.88f, 0.82f, 0.68f), 0.5f);
        matPagdi = MakeFlat("Pagdi", new Color(0.85f, 0.45f, 0.10f), 0.4f);
        matPants = MakeFlat("Pants", new Color(0.18f, 0.16f, 0.14f), 0.4f);
        matEye = MakeEmissive("MonsterEyes", new Color(0.9f, 0.85f, 0.6f), new Color(1.4f, 1.1f, 0.35f));
        matLampGlass = MakeEmissive("LampGlass", new Color(1f, 0.85f, 0.55f), new Color(1.3f, 0.8f, 0.3f));
        // OVERNIGHT R1: lamp iron — dark grey-brown, not near-black.
        matLampPole = MakeFlat("LampPole", new Color(0.30f, 0.27f, 0.23f), 0.35f);
        // PHONE-VIDEO 2026-11-09: matMudFlat (OVERNIGHT R3, texture-less
        // flat mud) is REMOVED — it read as pale grey floating plates on
        // the phone and in CI. The wet lane is matMudWet (below).
        matShadowBlob = MakeTransparent("ShadowBlob", new Color(0f, 0f, 0f, 0.35f));

        // Phase 4: Poly Haven CC0 1K textures — brick (temple), thatch
        // (temple roof), dry mud (dirt paths), cobble (chowk paving).
        // LoadTex warns and returns null if a texture is missing, so a
        // missing texture leaves a flat colour instead of killing the build.
        // Cheap-wins 2026-11-07: each material now also gets its matching
        // normal / metallic-smoothness / AO map (same tiling, exact match
        // — the maps belong to these very diffuse textures).
        matBrick = MakeMat("Brick", LoadTex("brick_4_diff_1k"), Color.white, 0.1f, new Vector2(2f, 1f),
            LoadTex("brick_4_nor_gl_1k"), LoadTex("brick_4_metal_smooth_1k"), LoadTex("brick_4_ao_1k"));
        matThatch = MakeMat("Thatch", LoadTex("thatch_roof_angled_diff_1k"), new Color(0.9f, 0.85f, 0.7f), 0.05f, new Vector2(2f, 2f),
            LoadTex("thatch_roof_angled_nor_gl_1k"), LoadTex("thatch_roof_angled_metal_smooth_1k"), LoadTex("thatch_roof_angled_ao_1k"));
        // Phase 2: mud tiling 2 -> 3 (dirt-path boxes are ~3 m wide, so
        // 3 repeats = ~1 m per tile, matching the ground's 2.5 m tiles);
        // cobble 4 -> 6 (9 m chowk disc, 1.5 m per tile) + a darker tint
        // so the stones read instead of washing out to flat beige.
        matMudDry = MakeMat("MudDry", LoadTex("brown_mud_dry_diff_1k"), new Color(0.85f, 0.78f, 0.68f), 0.05f, new Vector2(3f, 3f),
            LoadTex("brown_mud_dry_nor_gl_1k"), LoadTex("brown_mud_dry_metal_smooth_1k"), LoadTex("brown_mud_dry_ao_1k"));
        // PHONE-VIDEO 2026-11-09: matMudWet is created HERE, after
        // matMudDry exists (see the note above — the old spot never ran).
        // Wet = darkened mud (worn wet earth, darker + shinier than dry
        // ground) but TEXTURED: same PBR maps, tint 0.55/0.50/0.45. The
        // old 0.42 tint times the mid-brown texture read as near-black;
        // 0.55 keeps it a readable dark wet mud.
        matMudWet = CloneTiledMaterial(matMudDry, 3f);
        if (matMudWet != null) matMudWet.color = new Color(0.55f, 0.50f, 0.45f);
        matCobble = MakeMat("Cobble", LoadTex("cobblestone_floor_04_diff_1k"), new Color(0.72f, 0.71f, 0.68f), 0.1f, new Vector2(6f, 6f),
            LoadTex("cobblestone_floor_04_nor_gl_1k"), LoadTex("cobblestone_floor_04_metal_smooth_1k"), LoadTex("cobblestone_floor_04_ao_1k"));
        // Ground variation: leaf litter + withered grass patches scattered
        // over the village ground (real photo textures, not flat colour).
        matLeaves = MakeMat("Leaves", LoadTex("brown_mud_leaves_01_diff_1k"), Color.white, 0.05f, new Vector2(1f, 1f),
            LoadTex("brown_mud_leaves_01_nor_gl_1k"), LoadTex("brown_mud_leaves_01_metal_smooth_1k"), LoadTex("brown_mud_leaves_01_ao_1k"));
        matWitheredGrass = MakeMat("WitheredGrass", LoadTex("withered_grass_diff_1k"), Color.white, 0.05f, new Vector2(1f, 1f),
            LoadTex("withered_grass_nor_gl_1k"), LoadTex("withered_grass_metal_smooth_1k"), LoadTex("withered_grass_ao_1k"));
    }

    // Transparent flat material (ground shadow blobs, dirt patches).
    private Material MakeTransparent(string name, Color color)
    {
        Shader s = GetStandardShader();
        if (s == null)
        {
            Debug.LogWarning("[Village] Standard shader not available for " + name);
            return null;
        }
        Material m = new Material(s);
        m.name = name;
        m.color = color;
        m.SetFloat("_Mode", 3f);
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.DisableKeyword("_ALPHATEST_ON");
        m.EnableKeyword("_ALPHABLEND_ON");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        // URP Lit transparency (ignored by Standard): surface = Transparent, blend = Alpha.
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = 3000;
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_Glossiness", 0f);
        m.SetFloat("_Smoothness", 0f);
        return m;
    }

    // PILOT: real standing water — transparent, pale sky-blue over the mud
    // (alpha 0.5), smoothness 1.0 so the sun/lamp glints on it, with a fine
    // normal map for ripple sparkle. The old opaque dark discs reflected
    // nothing (no reflection probe in the scene) and read as black ovals.
    private Material MakeWaterMaterial()
    {
        Shader s = GetStandardShader();
        if (s == null)
        {
            Debug.LogWarning("[Village] Standard shader not available for WaterPuddle");
            return null;
        }
        Material m = new Material(s);
        m.name = "WaterPuddle";
        // OVERNIGHT R1b: the R1 CI shots cannot identify any puddle as
        // water — the discs are too small and too pale to read against
        // the lane. Bigger (0.9–1.4 m), darker and more opaque so they
        // read as distinct dark standing-water patches on the lane even
        // without a visible probe reflection.
        m.color = new Color(0.12f, 0.17f, 0.25f, 0.7f);
        m.SetFloat("_Mode", 3f);
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.DisableKeyword("_ALPHATEST_ON");
        m.EnableKeyword("_ALPHABLEND_ON");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = 3000;
        m.SetFloat("_Metallic", 0.35f);
        m.SetFloat("_Glossiness", 1f);
        m.SetFloat("_Smoothness", 1f);
        Texture2D ripple = LoadTex("brown_mud_dry_nor_gl_1k");
        if (ripple != null)
        {
            m.SetTexture("_BumpMap", ripple);
            m.SetFloat("_BumpScale", 0.3f);
            m.EnableKeyword("_NORMALMAP");
        }
        return m;
    }

    // ------------------------------------------------------------------
    // Primitive helpers
    // ------------------------------------------------------------------
    private GameObject Box(string name, Vector3 pos, Vector3 size, Material mat, Transform parent)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent != null ? parent : villageRoot, false);
        go.transform.position = pos;
        go.transform.localScale = size;
        if (mat != null)
        {
            go.GetComponent<Renderer>().material = mat;
        }
        return go;
    }

    private GameObject Cylinder(string name, Vector3 pos, Vector3 scale, Material mat, Transform parent)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent != null ? parent : villageRoot, false);
        go.transform.position = pos;
        go.transform.localScale = scale;
        if (mat != null)
        {
            go.GetComponent<Renderer>().material = mat;
        }
        return go;
    }

    // OVERNIGHT R1: flat ground disc — a flattened cylinder (~20 sides).
    // Replaces Box for ground patches / lanes / grass: a box has 4 hard
    // straight edges and reads as a raised rectangular tile from above
    // (audit 1.3, pilot round 2). A disc with an elliptical scale and a
    // random yaw reads as an irregular worn patch. Same scale semantics
    // as Cylinder (x/z = diameters, y = thickness).
    // NOTE (PHONE-VIDEO 2026-11-09): Disc() is kept for non-ground uses
    // only — ground patches must NEVER use it: the cylinder rim reads
    // as a floating plate (frames 9s/22s/37s).
    // ROUND 3 (2026-11-09): ground patches are GONE entirely — even the
    // rim-less FlatPatch fans read as ovals (CI shot-07/shot-10). Ground
    // variation lives in the ground plane's own texture now, so Disc()
    // currently has NO ground callers at all.
    private GameObject Disc(string name, Vector3 pos, Vector3 scale, Material mat, Transform parent)
    {
        return Cylinder(name, pos, scale, mat, parent);
    }

    private GameObject Sphere(string name, Vector3 pos, Vector3 scale, Material mat, Transform parent)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name;
        go.transform.SetParent(parent != null ? parent : villageRoot, false);
        go.transform.position = pos;
        go.transform.localScale = scale;
        if (mat != null)
        {
            go.GetComponent<Renderer>().material = mat;
        }
        return go;
    }

    private void Block(Vector3 center, Vector3 size)
    {
        VillageColliders.Register(new Bounds(center, size));
    }

    private Texture2D MakeRadialGlowTexture()
    {
        int s = 128;
        Texture2D t = new Texture2D(s, s, TextureFormat.RGBA32, false);
        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                float dx = (x - s / 2f) / (s / 2f);
                float dy = (y - s / 2f) / (s / 2f);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - d);
                a = a * a;
                t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        t.Apply();
        return t;
    }

    // Glow quad using the Standard shader in transparent mode (the
    // Unlit/Transparent shader is not guaranteed in the build either).
    // Cosmetic only: on any failure the glow is skipped, never fatal.
    private GameObject MakeGlowQuad(string name, Vector3 pos, float size, Color color)
    {
        try
        {
            Shader s = GetStandardShader();
            if (s == null)
            {
                Debug.LogWarning("[Village] Glow quad skipped (no Standard shader): " + name);
                return null;
            }
            GameObject go = new GameObject(name);
            go.transform.SetParent(villageRoot, false);
            go.transform.position = pos;
            go.transform.localScale = new Vector3(size, size, size);
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            MeshFilter f = go.AddComponent<MeshFilter>();
            f.mesh = BuildQuadMesh();
            Material m = new Material(s);
            m.mainTexture = MakeRadialGlowTexture();
            m.color = color;
            m.SetFloat("_Mode", 3f);
            // Fix pass: URP/Lit ignores the legacy _Mode float — the
            // surface type must be set explicitly, or the glow renders
            // as an OPAQUE quad (a solid rectangle in the sky).
            m.SetFloat("_Surface", 1f); // 1 = Transparent
            m.SetFloat("_Blend", 0f);   // 0 = Alpha
            m.SetFloat("_AlphaClip", 0f);
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = 3000;
            r.material = m;
            return go;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Village] Glow quad failed: " + name + " — " + e.Message);
            return null;
        }
    }

    private Mesh BuildQuadMesh()
    {
        Mesh m = new Mesh();
        m.vertices = new Vector3[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f)
        };
        m.uv = new Vector2[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f)
        };
        // NOTE: winding left as the original (front face -Z). A flip
        // to +Z made the MoonGlow halo visible but ALSO turned the
        // moon disc DARK (the halo quad at the same position interferes
        // with the disc); the disc is a pale moon only with the
        // original winding, so the halo stays culled from the south.
        m.triangles = new int[] { 0, 2, 1, 0, 3, 2 };
        m.RecalculateNormals();
        return m;
    }

    // Crossed-plane wheat plant (fix pass 2026-11-08): two quads at
    // 90 degrees (X-shape) so the plant reads from every angle. The
    // wheat_plant texture is an RGBA cutout (stalk + grain head).
    private Mesh cropPlantMesh;
    private Material cropPlantMat;

    private Material MakeCropPlantMaterial()
    {
        Texture2D tex = Tex("wheat_plant");
        if (tex == null) return null;
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null) lit = GetStandardShader();
        if (lit == null) return null;
        Material m = new Material(lit);
        m.name = "CropWheat";
        m.mainTexture = tex;
        m.color = Color.white;
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_Smoothness", 0.4f);
        // Alpha cutout (opaque surface + clip): the texture's alpha
        // channel cuts the quad to the plant silhouette.
        m.SetFloat("_Surface", 0f);
        m.SetFloat("_AlphaClip", 1f);
        m.SetFloat("_Cutoff", 0.4f);
        m.EnableKeyword("_ALPHATEST_ON");
        // Double-sided so the X reads from every angle.
        m.SetInt("_Cull", 0);
        m.renderQueue = 2450;
        return m;
    }

    private Mesh BuildCropPlantMesh()
    {
        Mesh m = new Mesh();
        m.name = "CropPlantX";
        float w = 0.35f, h = 0.9f;
        m.vertices = new Vector3[]
        {
            new Vector3(-w, 0f, 0f), new Vector3(w, 0f, 0f),
            new Vector3(w, h, 0f), new Vector3(-w, h, 0f),
            new Vector3(0f, 0f, -w), new Vector3(0f, 0f, w),
            new Vector3(0f, h, w), new Vector3(0f, h, -w)
        };
        m.uv = new Vector2[]
        {
            new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(1f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(1f, 1f), new Vector2(0f, 1f)
        };
        m.triangles = new int[] { 0, 2, 1, 0, 3, 2, 4, 6, 5, 4, 7, 6 };
        m.RecalculateNormals();
        return m;
    }

    // ------------------------------------------------------------------
    // Ground + player figure
    // ------------------------------------------------------------------
    private void BuildGround()
    {
        GameObject ground = GameObject.Find("Ground");
        Renderer groundRenderer = (ground != null) ? ground.GetComponent<Renderer>() : null;

        // ROUND 3 (PHONE-VIDEO 2026-11-09) — NO ground patch objects at
        // all. Round 1 replaced the grey cylinder discs with textured
        // flat fans (FlatPatch) and the parent STILL sees big flat ovals
        // in CI shot-07/shot-10: the 9 m cobble disc (hard curved rim,
        // uniform grey-beige), the dark-brown DirtPath / GroundPatch
        // ovals (bottom-centre + bottom-right), the grey disc by the
        // well. A perfect 24-segment circle with a hard 1-texel rim
        // reads as an oval at ANY height above the ground — re-tinting
        // or shrinking it cannot fix the shape. So every patch object
        // is DELETED (DirtPath, DirtPathEW, ChowkCobble, GrassPatch,
        // GroundPatch) and the WetSheen boxes go too (hard-edged
        // translucent strips). ALL ground variation now lives IN the
        // ground plane's own texture: BuildGroundMacroTexture() bakes
        // the wet lanes, the cobbled chowk and the leaf / withered-grass
        // blotches into one 1024 canvas with smoothstep-feathered
        // edges, tiled 7x7 over the plane — the village sits inside a
        // single tile, so nothing repeats or seams where the camera
        // looks. If generation fails for any reason the plain 200x PBR
        // mud stays (still no ovals).
        // (The macro texture is baked into matGround BEFORE it is handed
        // to the renderer: Renderer.material copies on access, so mutating
        // matGround after the hand-off would not reach the ground.)
        if (groundRenderer != null && matGround != null)
        {
            try
            {
                Texture2D macro = BuildGroundMacroTexture(groundRenderer);
                if (macro != null)
                {
                    matGround.mainTexture = macro;
                    matGround.mainTextureScale = new Vector2(7f, 7f);
                    Debug.Log("[Village] Ground macro texture applied (7x7, wet lanes + chowk + blotches baked in)");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Village] Ground macro texture failed, keeping plain PBR mud — " + e.Message);
            }
            groundRenderer.material = matGround;
        }

        // PUDDLES — REMOVED (OVERNIGHT R3). Three rounds of transparent
        // water discs (pilot + R1 clusters + R1b bigger/darker) never read
        // as water in either renderer: dark stain ovals in the CI render,
        // pale/white ovals in the emulator — in both cases they read as
        // bugs, not standing water, even with the reflection probe in the
        // scene. The wet look now lives in the ground macro texture above
        // (darkened wet-lane bands baked into the plane, not objects).
        // MakeWaterMaterial() is kept unused for a future real-water pass.
    }

    // ROUND 3 (2026-11-09): bake ALL ground variation into the ground
    // plane's own albedo texture — wet lanes, cobbled chowk, leaf litter
    // and withered-grass blotches — so no patch object exists to read as
    // an oval. Returns null (caller keeps the plain PBR mud) if any
    // source texture is missing or not CPU-readable.
    // Layout: the canvas is ONE material tile; the material tiles 7x7
    // over the ~500 m plane (odd count, so the village centre falls
    // INSIDE a tile, never on a seam). One tile is ~71 m, so the whole
    // village (r ~20 m) sits in the middle of the canvas and the pattern
    // only repeats ~70 m out, past the camera's view. The mud photo is
    // sampled 28x across the canvas (2.5 m/tile, its real scale); lane /
    // chowk / blotch edges are smoothstep-feathered, never hard. The
    // world<->canvas mapping is derived from the ground renderer's
    // world bounds, not from assumed numbers. Cost: ~1M bilinear
    // samples, once, at build time; the CPU copy is freed after Apply.
    // The PBR normal / metal-smooth / AO maps stay at their 200x tiling
    // (2.5 m/tile, the mud's real scale) — fine grain over the macro
    // albedo, no re-tiling needed.
    private Texture2D BuildGroundMacroTexture(Renderer groundRenderer)
    {
        Texture2D mud = LoadTex("brown_mud_dry_diff_1k");
        if (mud == null || !mud.isReadable)
        {
            Debug.LogWarning("[Village] Ground macro texture skipped — mud texture missing/unreadable");
            return null;
        }
        Texture2D leaves = LoadTex("brown_mud_leaves_01_diff_1k");
        Texture2D grass = LoadTex("withered_grass_diff_1k");
        Texture2D cobble = LoadTex("cobblestone_floor_04_diff_1k");
        if (leaves != null && !leaves.isReadable) leaves = null;
        if (grass != null && !grass.isReadable) grass = null;
        if (cobble != null && !cobble.isReadable) cobble = null;

        Bounds b = groundRenderer.bounds;
        if (b.size.x < 300f || b.size.z < 300f)
        {
            Debug.LogWarning("[Village] Ground macro texture skipped — ground plane too small (" + b.size.x + "x" + b.size.z + ")");
            return null;
        }
        const int S = 1024;
        const float Tiling = 7f;
        // ROUND 5b: MudRepeats 28 -> 16. At 28 the 1024px canvas holds
        // 36 px per 2.5 m mud repeat, so the ground blurs into a smudge
        // at close range (ground-closeup.png). At 16 each repeat is
        // 64 px / ~4.4 m — same bake cost, visibly sharper mud.
        const float MudRepeats = 16f;
        float tileU = Mathf.Floor(Tiling * 0.5f); // central tile index (3 of 0..6)
        float minX = b.min.x, sizeX = b.size.x, minZ = b.min.z, sizeZ = b.size.z;

        Color[] px = new Color[S * S];
        // (1) Base: mud photo, wrapped, 28 repeats per tile.
        for (int py = 0; py < S; py++)
        {
            float v = (py + 0.5f) / S * MudRepeats;
            for (int pxx = 0; pxx < S; pxx++)
            {
                float u = (pxx + 0.5f) / S * MudRepeats;
                px[py * S + pxx] = SampleWrapped(mud, u, v);
            }
        }
        // (2) Wet lanes — darker wet mud with its own grain (mud sampled
        // at an offset, tinted like the old matMudWet 0.55/0.50/0.45).
        // NS lane: |x| <= 1.3 m full, feathered to 2.2 m; z -12..12.
        // EW lane: |z - 7| <= 1.3 m full, feathered to 2.2 m; x -10..10.
        StampWetLane(px, S, minX, sizeX, minZ, sizeZ, Tiling, tileU, mud, true);
        StampWetLane(px, S, minX, sizeX, minZ, sizeZ, Tiling, tileU, mud, false);
        // (3) Cobbled chowk — cobble photo, stones ~0.5 m (sampled at
        // 1.5 m/tile like the old 6x chowk material), full inside r 3.6 m,
        // feathered to r 4.5 m. Painted AFTER the lanes, so paving wins
        // where they cross at the centre.
        if (cobble != null) StampChowk(px, S, minX, sizeX, minZ, sizeZ, Tiling, tileU, cobble);
        // (4) Leaf litter / withered-grass blotches at the old patch
        // spots (same positions the GroundPatch discs used) + the grass
        // tufts by the field edge — soft ellipses, never circles with a
        // hard rim: full strength inside 65% of the radius, feathered
        // out to the rim.
        StampBlotches(px, S, minX, sizeX, minZ, sizeZ, Tiling, tileU, mud, leaves, grass);

        Texture2D tex = new Texture2D(S, S, TextureFormat.RGBA32, true, false);
        tex.name = "GroundMacroMud";
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.anisoLevel = 8; // ROUND 5b: 4 -> 8, sharper mud at grazing angles (the close-up camera).
        tex.SetPixels(px);
        tex.Apply(true, true);
        return tex;
    }

    // Smoothstep between two edges (0 below a, 1 above b).
    private static float Smooth01(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3f - 2f * t);
    }

    // Bilinear sample with manual repeat-wrap (GetPixelBilinear alone
    // is not relied on to wrap).
    private static Color SampleWrapped(Texture2D t, float u, float v)
    {
        return t.GetPixelBilinear(Mathf.Repeat(u, 1f), Mathf.Repeat(v, 1f));
    }

    // World coordinate -> canvas pixel (canvas = the central material tile).
    private static float WorldToCanvas(float world, float min, float size, float tiling, float tileU, int s)
    {
        return ((world - min) / size * tiling - tileU) * s;
    }

    // Canvas pixel -> world coordinate.
    private static float CanvasToWorld(float p, float min, float size, float tiling, float tileU, int s)
    {
        return min + ((tileU + p / s) / tiling) * size;
    }

    // One wet-lane band baked into the canvas. northSouth: the main lane
    // along x = 0; otherwise the east-west lane along z = 7.
    private void StampWetLane(Color[] px, int s, float minX, float sizeX, float minZ, float sizeZ,
        float tiling, float tileU, Texture2D mud, bool northSouth)
    {
        float x0 = northSouth ? -2.2f : -10.9f;
        float x1 = northSouth ? 2.2f : 10.9f;
        float z0 = northSouth ? -12.9f : 4.8f;
        float z1 = northSouth ? 12.9f : 9.2f;
        int px0 = Mathf.Max(0, Mathf.FloorToInt(WorldToCanvas(x0, minX, sizeX, tiling, tileU, s)));
        int px1 = Mathf.Min(s - 1, Mathf.CeilToInt(WorldToCanvas(x1, minX, sizeX, tiling, tileU, s)));
        int py0 = Mathf.Max(0, Mathf.FloorToInt(WorldToCanvas(z0, minZ, sizeZ, tiling, tileU, s)));
        int py1 = Mathf.Min(s - 1, Mathf.CeilToInt(WorldToCanvas(z1, minZ, sizeZ, tiling, tileU, s)));
        for (int py = py0; py <= py1; py++)
        {
            float wz = CanvasToWorld(py + 0.5f, minZ, sizeZ, tiling, tileU, s);
            float maskZ = northSouth
                ? 1f - Smooth01(12f, 12.9f, Mathf.Abs(wz))
                : 1f - Smooth01(1.3f, 2.2f, Mathf.Abs(wz - 7f));
            if (maskZ <= 0f) continue;
            for (int pxx = px0; pxx <= px1; pxx++)
            {
                float wx = CanvasToWorld(pxx + 0.5f, minX, sizeX, tiling, tileU, s);
                float maskX = northSouth
                    ? 1f - Smooth01(1.3f, 2.2f, Mathf.Abs(wx))
                    : 1f - Smooth01(10f, 10.9f, Mathf.Abs(wx));
                float mask = maskX * maskZ;
                if (mask <= 0f) continue;
                Color wet = SampleWrapped(mud, (pxx + 0.5f) / s * 16f + 0.37f, (py + 0.5f) / s * 16f + 0.61f);
                // ROUND 5b: the old 0.55/0.50/0.45 tint read as near-black
                // in the close-up (the lane is most of that shot). Lighter
                // wet mud: 0.68/0.63/0.58 — still clearly darker than dry.
                wet = new Color(wet.r * 0.68f, wet.g * 0.63f, wet.b * 0.58f, 1f);
                int idx = py * s + pxx;
                px[idx] = Color.Lerp(px[idx], wet, mask * 0.85f);
            }
        }
    }

    // Cobbled chowk disc baked into the canvas (feathered rim, no edge).
    private void StampChowk(Color[] px, int s, float minX, float sizeX, float minZ, float sizeZ,
        float tiling, float tileU, Texture2D cobble)
    {
        int px0 = Mathf.Max(0, Mathf.FloorToInt(WorldToCanvas(-4.6f, minX, sizeX, tiling, tileU, s)));
        int px1 = Mathf.Min(s - 1, Mathf.CeilToInt(WorldToCanvas(4.6f, minX, sizeX, tiling, tileU, s)));
        int py0 = Mathf.Max(0, Mathf.FloorToInt(WorldToCanvas(-4.6f, minZ, sizeZ, tiling, tileU, s)));
        int py1 = Mathf.Min(s - 1, Mathf.CeilToInt(WorldToCanvas(4.6f, minZ, sizeZ, tiling, tileU, s)));
        for (int py = py0; py <= py1; py++)
        {
            float wz = CanvasToWorld(py + 0.5f, minZ, sizeZ, tiling, tileU, s);
            for (int pxx = px0; pxx <= px1; pxx++)
            {
                float wx = CanvasToWorld(pxx + 0.5f, minX, sizeX, tiling, tileU, s);
                float r = Mathf.Sqrt(wx * wx + wz * wz);
                if (r >= 4.5f) continue;
                float mask = 1f - Smooth01(3.6f, 4.5f, r);
                Color c = SampleWrapped(cobble, wx / 1.5f, wz / 1.5f);
                c = new Color(c.r * 0.72f, c.g * 0.71f, c.b * 0.68f, 1f);
                int idx = py * s + pxx;
                px[idx] = Color.Lerp(px[idx], c, mask * 0.9f);
            }
        }
    }

    // Leaf litter / withered-grass blotches at the old GroundPatch spots
    // plus the grass tufts by the field edge (same positions/sizes the
    // deleted discs used — the variation stays, the shape goes).
    private void StampBlotches(Color[] px, int s, float minX, float sizeX, float minZ, float sizeZ,
        float tiling, float tileU, Texture2D mud, Texture2D leaves, Texture2D grass)
    {
        Vector3[] spots = new Vector3[]
        {
            new Vector3(-12f, 0f, -6f), new Vector3(12f, 0f, -10f),
            new Vector3(-14f, 0f, 4f), new Vector3(14f, 0f, 6f),
            new Vector3(-6f, 0f, -14f), new Vector3(8f, 0f, 14f),
            new Vector3(-10f, 0f, 12f), new Vector3(16f, 0f, -6f),
            new Vector3(-16f, 0f, -10f), new Vector3(10f, 0f, -14f),
            new Vector3(-4f, 0f, 16f), new Vector3(18f, 0f, 10f),
            new Vector3(-8f, 0f, -12f), new Vector3(8f, 0f, -12f),
            new Vector3(-15f, 0f, -4f), new Vector3(15f, 0f, 4f),
            new Vector3(-10f, 0f, 18f), new Vector3(4f, 0f, 18f),
            new Vector3(14f, 0f, 16f), new Vector3(18f, 0f, -12f),
            new Vector3(-18f, 0f, 8f), new Vector3(6f, 0f, -8f),
            new Vector3(-4f, 0f, 12f), new Vector3(4f, 0f, 12f),
            new Vector3(-10f, 0f, 14f), new Vector3(10f, 0f, 14f),
            new Vector3(-14f, 0f, -2f), new Vector3(14f, 0f, -2f),
            new Vector3(-8f, 0f, -14f), new Vector3(8f, 0f, -14f),
            new Vector3(12f, 0f, 10f), new Vector3(-12f, 0f, 10f),
            new Vector3(2f, 0f, 18f), new Vector3(18f, 0f, -4f),
        };
        for (int i = 0; i < spots.Length; i++)
        {
            Texture2D src = (i % 3 == 0) ? leaves : (i % 3 == 1) ? grass : mud;
            Color tint = (i % 3 == 2) ? new Color(0.8f, 0.8f, 0.8f, 1f) : Color.white;
            if (src == null) { src = mud; tint = new Color(0.8f, 0.8f, 0.8f, 1f); }
            float r = (1.8f + (i % 4) * 0.33f) * 0.5f;
            float rz = r * (0.8f + (i % 3) * 0.15f);
            StampBlotch(px, s, minX, sizeX, minZ, sizeZ, tiling, tileU, src, tint,
                spots[i].x, spots[i].z, r, rz, 0.55f);
        }
        // Grass tufts by the field edge (old GrassPatch positions).
        for (int i = 0; i < 16; i++)
        {
            float x = 4f + (i % 8) * 1.4f;
            float z = 12.5f + (i / 8) * 1.2f + ((i % 3) * 0.4f);
            float pw = 1.1f + (i % 3) * 0.3f;
            float pl = 0.9f + (i % 2) * 0.4f;
            Texture2D src = (grass != null) ? grass : mud;
            Color tint = (grass != null) ? Color.white : new Color(0.8f, 0.8f, 0.8f, 1f);
            StampBlotch(px, s, minX, sizeX, minZ, sizeZ, tiling, tileU, src, tint,
                x, z, pw * 0.5f, pl * 0.5f, 0.55f);
        }
    }

    // One soft elliptical blotch: full strength inside 65% of the
    // radius, feathered out to the rim — no hard edge, no circle outline.
    private void StampBlotch(Color[] px, int s, float minX, float sizeX, float minZ, float sizeZ,
        float tiling, float tileU, Texture2D src, Color tint, float cx, float cz, float rx, float rz, float strength)
    {
        int px0 = Mathf.Max(0, Mathf.FloorToInt(WorldToCanvas(cx - rx, minX, sizeX, tiling, tileU, s)));
        int px1 = Mathf.Min(s - 1, Mathf.CeilToInt(WorldToCanvas(cx + rx, minX, sizeX, tiling, tileU, s)));
        int py0 = Mathf.Max(0, Mathf.FloorToInt(WorldToCanvas(cz - rz, minZ, sizeZ, tiling, tileU, s)));
        int py1 = Mathf.Min(s - 1, Mathf.CeilToInt(WorldToCanvas(cz + rz, minZ, sizeZ, tiling, tileU, s)));
        for (int py = py0; py <= py1; py++)
        {
            float wz = CanvasToWorld(py + 0.5f, minZ, sizeZ, tiling, tileU, s);
            for (int pxx = px0; pxx <= px1; pxx++)
            {
                float wx = CanvasToWorld(pxx + 0.5f, minX, sizeX, tiling, tileU, s);
                float ex = (wx - cx) / rx;
                float ez = (wz - cz) / rz;
                float d = Mathf.Sqrt(ex * ex + ez * ez);
                if (d >= 1f) continue;
                float mask = 1f - Smooth01(0.65f, 1f, d);
                Color c = SampleWrapped(src, wx / 2.5f, wz / 2.5f);
                c = new Color(c.r * tint.r, c.g * tint.g, c.b * tint.b, 1f);
                int idx = py * s + pxx;
                px[idx] = Color.Lerp(px[idx], c, mask * strength);
            }
        }
    }

    // Phase 2: clone a ground material with its texture tiling scaled to
    // the patch it covers, so a photo texture keeps the same texel density
    // on a 2 m patch as on the 500 m ground plane. All four PBR map scales
    // are set together — setting only the albedo scale would leave the
    // normal/AO maps stretched differently and the patch would shade wrong.
    // ROUND 3 (2026-11-09): retained unused — the ground patches it served
    // are deleted; ground variation is baked into the plane's texture now.
    private static Material CloneTiledMaterial(Material src, float tiling)
    {
        if (src == null) return null;
        Material m = new Material(src);
        m.mainTextureScale = new Vector2(tiling, tiling);
        string[] maps = new string[] { "_BaseMap", "_BumpMap", "_MetallicGlossMap", "_OcclusionMap" };
        foreach (string map in maps)
        {
            if (m.HasProperty(map)) m.SetTextureScale(map, new Vector2(tiling, tiling));
        }
        return m;
    }

    // Stray-object cleanup (runs after all models load):
    //  - leaf meshes detached at ground level (e.g. one big leaf lying on
    //    the dirt path in the 18:50 screenshots). A real canopy leaf never
    //    sits below ~1 m, so leaf-named renderers whose bounds centre is
    //    near the ground are disabled.
    //  - the temple's flat untextured 7x7 m base plate (Object_4): it reads
    //    as a white slab in front of the temple. The real stepped plinth
    //    (Object_25) stays.
    private void CleanupStrays()
    {
        if (villageRoot == null) return;
        int removed = 0;
        foreach (Renderer r in villageRoot.GetComponentsInChildren<Renderer>(true))
        {
            if (r == null || r.gameObject == null) continue;
            string lname = r.gameObject.name.ToLower();
            // Leaf litter on the ground: the whole mesh lies flat (max.y
            // under 0.6 m). A canopy cluster or drooping frond spans metres
            // vertically and is never caught by this. Matches "leaf",
            // "leaves" (mango canopy mesh) and "frond".
            bool isLeaf = lname.Contains("leaf") || lname.Contains("leaves") || lname.Contains("frond");
            if (isLeaf && r.bounds.max.y < 0.6f)
            {
                Debug.Log("[Village] Disabling ground-level leaf mesh: " + r.gameObject.name
                    + " at " + r.bounds.center.ToString("F2"));
                r.gameObject.SetActive(false);
                removed++;
            }
            // Detached leaf with a generic Sketchfab name ("Object_N" — no
            // "leaf" in it): a small FLAT mesh lying on the dirt under a
            // tree. Canopy clusters and drooping fronds span metres
            // vertically and trunks are tall, so flat + low + small + under
            // a Tree only ever matches a stray leaf.
            if (!isLeaf && r.bounds.max.y < 0.5f && r.bounds.size.y < 0.08f
                && r.bounds.size.x < 2f && r.bounds.size.z < 2f)
            {
                bool underTree = false;
                for (Transform p = r.transform.parent; p != null; p = p.parent)
                {
                    if (p.name.ToLower().Contains("tree")) { underTree = true; break; }
                }
                if (underTree)
                {
                    Debug.Log("[Village] Disabling detached flat leaf under tree: " + r.gameObject.name
                        + " at " + r.bounds.center.ToString("F2"));
                    r.gameObject.SetActive(false);
                    removed++;
                }
            }
        }
        if (removed > 0) Debug.Log("[Village] Disabled " + removed + " ground-level leaf mesh(es)");

        GameObject temple = GameObject.Find("Temple");
        if (temple != null)
        {
            foreach (Renderer r in temple.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || r.gameObject == null) continue;
                if (r.gameObject.name == "Object_4" && r.bounds.size.y < 0.05f)
                {
                    Debug.Log("[Village] Disabling temple base plate Object_4 (white slab)");
                    r.gameObject.SetActive(false);
                }
            }
        }
    }

    private void BuildPlayerFigure()
    {
        GameObject player = GameObject.Find("Player");
        if (player == null) return;

        // Hide the starter cube; keep its BoxCollider for the player body.
        Renderer cubeRenderer = player.GetComponent<Renderer>();
        if (cubeRenderer != null)
        {
            cubeRenderer.enabled = false;
        }

        // Nilesh — clothed humanoid figure (cream kurta, skin head, saffron
        // pagdi, dark pants). Phase 4: this is now only the FALLBACK —
        // BuildPlayerModelAsync (first step of BuildVillageAsync) swaps
        // in the real rigged indian_man.glb as "Player/Nilesh" and removes
        // this figure. Same capsule collider + camera logic as before;
        // only the visuals changed. All parts hang under a "Figure"
        // container so CharacterWalk can bob/swing them.
        Transform root = player.transform;
        GameObject figure = new GameObject("Figure");
        figure.transform.SetParent(root, false);
        figure.transform.localPosition = Vector3.zero;
        Transform fig = figure.transform;

        // Kurta torso (cream) + collar + waist band.
        // Player root sits at y=0.5 above ground, so local -0.5 = feet on the ground.
        GameObject torso = Box("Torso", Vector3.zero, new Vector3(0.58f, 0.78f, 0.34f), matKurtaCream, fig);
        torso.transform.localPosition = new Vector3(0f, 0.55f, 0f);
        Destroy(torso.GetComponent<Collider>());
        GameObject collar = Box("Collar", Vector3.zero, new Vector3(0.34f, 0.1f, 0.36f), matPagdi, fig);
        collar.transform.localPosition = new Vector3(0f, 0.97f, 0f);
        Destroy(collar.GetComponent<Collider>());
        GameObject waist = Box("Waist", Vector3.zero, new Vector3(0.6f, 0.08f, 0.36f), matPants, fig);
        waist.transform.localPosition = new Vector3(0f, 0.15f, 0f);
        Destroy(waist.GetComponent<Collider>());

        // Head (skin) + pagdi (saffron turban: flattened sphere + wrap band).
        GameObject head = Sphere("Head", Vector3.zero, new Vector3(0.42f, 0.46f, 0.42f), matSkin, fig);
        head.transform.localPosition = new Vector3(0f, 1.16f, 0f);
        Destroy(head.GetComponent<Collider>());
        GameObject pagdi = Sphere("Pagdi", Vector3.zero, new Vector3(0.46f, 0.26f, 0.46f), matPagdi, fig);
        pagdi.transform.localPosition = new Vector3(0f, 1.38f, 0f);
        Destroy(pagdi.GetComponent<Collider>());
        GameObject pagdiBand = Cylinder("PagdiBand", Vector3.zero, new Vector3(0.47f, 0.1f, 0.47f), matPagdi, fig);
        pagdiBand.transform.localPosition = new Vector3(0f, 1.3f, 0f);
        Destroy(pagdiBand.GetComponent<Collider>());

        // Arms: cream sleeves + skin hands.
        GameObject armL = Box("ArmL", Vector3.zero, new Vector3(0.17f, 0.62f, 0.17f), matKurtaCream, fig);
        armL.transform.localPosition = new Vector3(-0.4f, 0.55f, 0f);
        Destroy(armL.GetComponent<Collider>());
        GameObject handL = Sphere("HandL", Vector3.zero, new Vector3(0.15f, 0.15f, 0.15f), matSkin, fig);
        handL.transform.localPosition = new Vector3(-0.4f, 0.2f, 0f);
        Destroy(handL.GetComponent<Collider>());
        GameObject armR = Box("ArmR", Vector3.zero, new Vector3(0.17f, 0.62f, 0.17f), matKurtaCream, fig);
        armR.transform.localPosition = new Vector3(0.4f, 0.55f, 0f);
        Destroy(armR.GetComponent<Collider>());
        GameObject handR = Sphere("HandR", Vector3.zero, new Vector3(0.15f, 0.15f, 0.15f), matSkin, fig);
        handR.transform.localPosition = new Vector3(0.4f, 0.2f, 0f);
        Destroy(handR.GetComponent<Collider>());

        // Legs: dark pants + feet (feet bottom at local -0.5 = ground level).
        GameObject legL = Box("LegL", Vector3.zero, new Vector3(0.22f, 0.55f, 0.22f), matPants, fig);
        legL.transform.localPosition = new Vector3(-0.16f, -0.125f, 0f);
        Destroy(legL.GetComponent<Collider>());
        GameObject footL = Box("FootL", Vector3.zero, new Vector3(0.22f, 0.1f, 0.34f), matDark, fig);
        footL.transform.localPosition = new Vector3(-0.16f, -0.45f, 0.05f);
        Destroy(footL.GetComponent<Collider>());
        GameObject legR = Box("LegR", Vector3.zero, new Vector3(0.22f, 0.55f, 0.22f), matPants, fig);
        legR.transform.localPosition = new Vector3(0.16f, -0.125f, 0f);
        Destroy(legR.GetComponent<Collider>());
        GameObject footR = Box("FootR", Vector3.zero, new Vector3(0.22f, 0.1f, 0.34f), matDark, fig);
        footR.transform.localPosition = new Vector3(0.16f, -0.45f, 0.05f);
        Destroy(footR.GetComponent<Collider>());

        // Walk bob + arm/leg swing (visual only).
        CharacterWalk walk = player.AddComponent<CharacterWalk>();
        walk.figureRoot = fig;
        walk.armL = armL.transform;
        walk.armR = armR.transform;
        walk.legL = legL.transform;
        walk.legR = legR.transform;
    }

    // Phase 4: THE REAL NILESH. Loads the rigged indian_man.glb under
    // the Player root as "Nilesh" (1.7 m tall, static pose — the .glb's
    // single animation is NOT retargeted in this push; animation is a
    // later step, never a blocker). On success the primitive fallback
    // figure is removed and CharacterWalk is repointed at the real model
    // (arms/legs null — CharacterWalk null-checks them, bob still works).
    // On failure the primitive figure stays, so the game still works and
    // the (c3) test falls back to checking it.
    private System.Collections.IEnumerator BuildPlayerModelAsync()
    {
        GameObject player = GameObject.Find("Player");
        if (player == null)
        {
            Debug.LogWarning("[Village] Player missing — real Nilesh model skipped");
            yield break;
        }
        // Phase 1 real-size table (2026-11-08): Nilesh is 1.75 m tall.
        GameObject nilesh = null;
        yield return GltfModelLoader.LoadModel(
            "indian_man_77_hwkm.glb", player.transform, "Nilesh",
            player.transform.position, 0f, 1.75f,
            GltfModelLoader.NormalizeMode.ByHeight,
            GltfModelLoader.ColliderMode.None, g => nilesh = g);
        if (nilesh == null)
        {
            Debug.LogWarning("[Village] Real Nilesh model failed to load — keeping primitive figure");
            yield break;
        }
        // Play his Mixamo walk/run/idle clips (remapped legacy clips in
        // Resources/Animations). NileshAnimator CrossFades by movement
        // speed and falls back to the embedded 'mixamo.com' idle clip
        // when the remapped clips are missing, so he is never frozen in
        // the bind (T-)pose. glTFast assigns the embedded clip after
        // AddComponent, so playAutomatically never fires — the animator
        // handles the explicit Play(). Paths stay valid: Normalize skips
        // the ModelRoot wrapper for animated models.
        player.AddComponent<NileshAnimator>();
        // Remove the primitive fallback figure.
        Transform oldFigure = player.transform.Find("Figure");
        if (oldFigure != null)
        {
            Object.Destroy(oldFigure.gameObject);
        }
        // Repoint the walk bob at the real model (no arm/leg refs — the
        // rigged model has no primitive limbs to swing).
        CharacterWalk walk = player.GetComponent<CharacterWalk>();
        if (walk == null)
        {
            walk = player.AddComponent<CharacterWalk>();
        }
        // ROUND 4 WALK: DISABLE CharacterWalk for the real Nilesh. Its
        // procedural bob (+6 cm, ~1 Hz) runs out of phase with the mocap
        // clip's own 7.7 cm hip bob, so the feet float/pulse — and its
        // footstep audio fires on the same unsynced phase, so footsteps
        // never match foot strikes. The mocap clips already bob + step
        // correctly on their own. CharacterWalk stays ONLY for the
        // primitive fallback figure (which has no clips). Footstep audio
        // goes quiet with it — unsynced steps are worse than none.
        walk.enabled = false;
        walk.figureRoot = nilesh.transform;
        walk.armL = null;
        walk.armR = null;
        walk.legL = null;
        walk.legR = null;
        Debug.Log("[Village] Real Nilesh model active (indian_man.glb)");
    }

    // ------------------------------------------------------------------
    // Houses. DoorSide: East = door on +X wall, West = door on -X wall.
    // All Phase 1 doors face the chowk.
    // ------------------------------------------------------------------
    private enum DoorSide { East, West }

    // Phase 3: houses are REAL Sketchfab models now — 6 houses, EACH a
    // different model (the user ordered every house different). The
    // player's house keeps its working hinged door + interior + LOCK: the
    // door assembly is fitted to the real hut's actual bounds. A model
    // that fails to load falls back to the primitive house.
    private System.Collections.IEnumerator BuildHousesAsync()
    {
        // Player's house: real thatched hut shell + primitive door/interior.
        // Phase 1 real-size table: a kachcha hut is 3-4 m tall. ByHeight
        // 4.0 (top of the range — the interior must still fit the 2.1 m
        // hospital bed). The old ByWidth 6.5 made the hut 5.8 m tall, so
        // Nilesh (1.75 m) looked like a child next to his own house.
        Vector3 playerCenter = new Vector3(-9f, 0f, 6f);
        GameObject hut = null;
        yield return GltfModelLoader.LoadModel(
            "thatched_hut_4_p31g.glb", villageRoot, "House_Player",
            playerCenter, 0f, 4.0f, GltfModelLoader.NormalizeMode.ByHeight,
            GltfModelLoader.ColliderMode.None, g => hut = g);
        if (hut != null)
        {
            // PILOT: the hut's walls are one orange plaster texture and the
            // Phase 2 tint (x0.78/0.74/0.68) barely dented it — the walls
            // stayed bright orange-red in the fix-round screenshot. A colour
            // multiplier cannot fix a saturated texture, so the walls get
            // a real PBR re-texture — see WeatherHutPlaster.
            WeatherHutPlaster(hut);
            Bounds hb = GetWorldBounds(hut);
            // ROUND 4 DOOR: the WALL bounds, not the whole-hut bounds.
            // hb includes the thatch roof, which overhangs the walls by
            // ~0.44 m (wall max-X 4.359 vs roof max-X 4.661 in the .glb) —
            // anchoring the door at hb.max.x stood the panel ~0.44 m proud
            // of the wall face (phone frames 18s/22s) and put the collision
            // walls 0.44 m outside the visual walls (invisible wall). The
            // walls are the Material.002 renderers (same filter
            // WeatherHutPlaster uses). Falls back to hb if the filter
            // finds nothing, so a model change can never break the door.
            Bounds wb = GetWorldBoundsForMaterial(hut, "Material.002");
            if (wb.size.x < 1f || wb.size.z < 1f)
            {
                Debug.LogWarning("[Village] Wall bounds degenerate — falling back to whole-hut bounds for the door");
                wb = hb;
            }
            playerHouseBounds = wb;
            float w = wb.size.x;
            float d = wb.size.z;
            float doorX = wb.max.x - playerCenter.x;
            // ROUND 4 DOOR: doorW 1.2 -> 1.5 m (passable band 0.3 -> 0.6 m
            // with the 0.45 m player radius), and the door + interior are
            // parented to villageRoot (scale 1) — NOT hut.transform. The
            // hut holder is scaled ~1.46x by Normalize, so a door parented
            // to it rendered as a 1.8 x 3.1 m slab (phone frames 18s/22s).
            BuildDoor(villageRoot, playerCenter, doorX, DoorSide.East, 1.5f, 2.1f);
            BuildHouseInterior(villageRoot, playerCenter, w, d);
            RegisterHouseDoorGap(wb, DoorSide.East, 1.5f, 2.1f);
            // ROUND 4 DOOR: the real hut has no door frame (the primitive
            // house gets one) — add the same wood posts + lintel so the
            // doorway reads as a doorway, not a slab on a solid wall.
            // Frame sits proud of the WALL face (wb.max.x + 0.22), same
            // offset as the primitive house.
            float frameX = wb.max.x + 0.22f;
            Box("House_Player_DoorFrameL", new Vector3(frameX, 2.1f / 2f, 6f + 1.5f / 2f + 0.09f), new Vector3(0.18f, 2.2f, 0.18f), matWood, villageRoot);
            Box("House_Player_DoorFrameR", new Vector3(frameX, 2.1f / 2f, 6f - 1.5f / 2f - 0.09f), new Vector3(0.18f, 2.2f, 0.18f), matWood, villageRoot);
            Box("House_Player_DoorFrameTop", new Vector3(frameX, 2.1f + 0.09f, 6f), new Vector3(0.18f, 0.18f, 1.5f + 0.36f), matWood, villageRoot);
            // ROUND 4 DOOR: dark doorway behind the panel. Whether the
            // Sketchfab wall has a real hole is unverified from the mesh
            // data — this single-sided near-black quad (visible ONLY
            // while the door is open, toggled by DoorController) guarantees
            // an open door reveals darkness, not wall texture, and the
            // player walks through darkness into the interior. Single-
            // sided (faces east) so it never blocks the view OUT from
            // inside. Hidden while closed, so the wooden panel shows.
            if (matDark != null)
            {
                GameObject darkQuad = new GameObject("House_Player_DoorwayDark");
                darkQuad.transform.SetParent(villageRoot, false);
                darkQuad.transform.position = new Vector3(wb.max.x + 0.03f, 1.05f, 6f);
                darkQuad.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                darkQuad.transform.localScale = new Vector3(1.5f, 2.1f, 1f);
                MeshRenderer dqR = darkQuad.AddComponent<MeshRenderer>();
                MeshFilter dqF = darkQuad.AddComponent<MeshFilter>();
                dqF.mesh = BuildQuadMesh();
                dqR.material = matDark;
                darkQuad.SetActive(false);
                if (playerDoor != null) playerDoor.doorwayDark = darkQuad;
            }
            // ROUND 4 DOOR: the real hut has no floor and no interior
            // light (BuildHouseInterior adds only table/pot/diya). Thin
            // stone slab so the interior reads as a room, not dirt, +
            // a dim warm diya light above the diya.
            Box("House_Player_Floor", playerCenter + new Vector3(0f, 0f, 0f), new Vector3(w, 0.06f, d), matStone, villageRoot);
            // ROUND 5c: the floor slab top (0.03 m) is a walkable
            // surface so the feet rest on the floor, not 3 cm under it.
            VillageColliders.RegisterWalkable(playerCenter.x, playerCenter.z,
                Mathf.Max(w, d) / 2f, 0.03f);
            GameObject diyaLightGO = new GameObject("House_Player_DiyaLight");
            diyaLightGO.transform.SetParent(villageRoot, false);
            diyaLightGO.transform.position = playerCenter + new Vector3(0f, 1.2f, -d / 2f + 0.5f);
            Light diyaLight = diyaLightGO.AddComponent<Light>();
            diyaLight.type = LightType.Point;
            diyaLight.color = new Color(1f, 0.6f, 0.25f);
            diyaLight.intensity = 0.7f;
            diyaLight.range = 5f;
            // ROUND 4 DOOR: persistent marker over the player's house —
            // a warm amber glow above the roof peak, so the player can
            // tell WHICH house is his (phone video: he tried the stilt
            // house, which has no door). Billboarded in Update.
            houseMarkerGlow = MakeGlowQuad("House_Player_Marker",
                new Vector3(hb.center.x, hb.max.y + 0.8f, hb.center.z), 1.6f,
                new Color(1f, 0.65f, 0.15f, 0.55f));
        }
        else
        {
            BuildHouse("House_Player", playerCenter, 6f, 5f, 3f, DoorSide.East, true, false);
            playerHouseBounds = new Bounds(playerCenter, new Vector3(6f, 4f, 5f));
        }

        // Houses 2-6: one different real model each.
        // Phase 1 real-size table: houses are scaled by HEIGHT now, not
        // width. The old ByWidth sizes made squat models far too short —
        // House_5 was 2.6 m tall and House_6 3.05 m, so the 1.75 m hero
        // looked as tall as his neighbours' houses. Pakka houses 5.5 m,
        // kachcha huts 3.5 m.
        // Follow-up 2026-11-07: Indian House Old (35,565 tris, albedo-only)
        // replaces the 4,100-tri low-poly box.
        yield return BuildRealHouse("House_2", "indian_house_old_18_qlsq.glb",
            new Vector3(9f, 0f, 8f), 15f, 5.5f);
        // Replacement 2026-11-07: stealth86 Thatched Hut, decimated
        // 161k -> ~50k tris (full PBR on Material.002). House_4 stays the
        // old pointed hut until the round-2 hut arrives — every house
        // must be different.
        yield return BuildRealHouse("House_3", "thatched_hut_stealth86_50k.glb",
            new Vector3(-9f, 0f, -9f), -10f, 3.5f,
            g => WeatherHutPlaster(g)); // PILOT ROUND 2: same orange-wall
            // defect as House_Player (identical material layout: walls =
            // Material.002, thatch = Material.001) — the fix-round angle2
            // shot proves House_3's walls were still orange-red.
        // Follow-up 2026-11-07: traditional ROUND thatched hut, decimated
        // 149k -> 50k tris (full PBR). Round silhouette, genuinely different
        // from House_3's gabled stealth86 hut.
        yield return BuildRealHouse("House_4", "traditional_round_thatched_hut_50k.glb",
            new Vector3(9f, 0f, -7f), 25f, 3.5f);
        yield return BuildRealHouse("House_5", "old_village_house_-_indian_game_asset_24_bac7.glb",
            new Vector3(-13f, 0f, 13f), -20f, 5.5f,
            // OVERNIGHT R1: House_5 is the SAME single-"Material.002"
            // layout as House_Player/House_3 (verified in the .glb) and
            // the old 0.78x tint never dented its orange texture (audit
            // 1.1) — it gets the same real clay-PBR re-texture.
            g => WeatherHutPlaster(g));
        yield return BuildRealHouse("House_6", "indian_village_house_famous_3d_model_37_4zjm.glb",
            new Vector3(13f, 0f, -14f), 10f, 5.5f,
            // PHONE-VIDEO 2026-11-09 (fix #5): the stilt house renders
            // black-and-white sketch-like on the phone — see FixStiltHouse.
            g => FixStiltHouse(g));
    }

    private System.Collections.IEnumerator BuildRealHouse(
        string name, string file, Vector3 pos, float yaw, float size,
        System.Action<GameObject> onLoaded = null)
    {
        GameObject h = null;
        yield return GltfModelLoader.LoadModel(
            file, villageRoot, name, pos, yaw, size,
            GltfModelLoader.NormalizeMode.ByHeight,
            GltfModelLoader.ColliderMode.FullBounds, g => h = g);
        if (h != null && onLoaded != null) onLoaded(h);
        if (h == null)
        {
            // Primitive fallback with the same footprint.
            BuildHouse(name, pos, 5f, 4f, 3f,
                (pos.x > 0f) ? DoorSide.West : DoorSide.East, false,
                (name == "House_2" || name == "House_4"));
        }
    }

    // World-space bounds of everything rendered under a loaded model.
    private static Bounds GetWorldBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            // No visible geometry (should not happen for a hut) — fall
            // back to a default footprint so the door still gets built.
            return new Bounds(root.transform.position, new Vector3(6f, 4f, 4f));
        }
        Bounds b = renderers[0].bounds;
        foreach (Renderer r in renderers)
        {
            b.Encapsulate(r.bounds);
        }
        return b;
    }

    // World-space bounds of the renderers under a model that use one
    // material (by name). ROUND 4 DOOR: the player's hut WALLS are
    // Material.002; the whole-hut bounds include the thatch roof, which
    // overhangs the walls by ~0.44 m. Returns an empty (zero-size) bounds
    // if no renderer uses the material — the caller falls back to hb.
    private static Bounds GetWorldBoundsForMaterial(GameObject root, string matName)
    {
        Bounds b = new Bounds();
        bool any = false;
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
        {
            bool hit = false;
            foreach (Material m in r.sharedMaterials)
            {
                if (m != null && m.name == matName) { hit = true; break; }
            }
            if (!hit) continue;
            if (!any) { b = r.bounds; any = true; }
            else b.Encapsulate(r.bounds);
        }
        return b;
    }

    // Collision for a real hut used as the player's house: the hut's walls
    // as AABBs, with a door-sized gap on the door face (same pattern as the
    // primitive house) so the player can only enter through the door.
    private void RegisterHouseDoorGap(Bounds hb, DoorSide side, float doorW, float doorH)
    {
        float wallH = Mathf.Min(hb.size.y, 4f);
        float cx = hb.center.x;
        float cz = hb.center.z;
        float faceX = (side == DoorSide.East) ? hb.max.x : hb.min.x;
        float backX = (side == DoorSide.East) ? hb.min.x : hb.max.x;
        float segLen = (hb.size.z - doorW) / 2f;
        if (segLen > 0.1f)
        {
            Block(new Vector3(faceX, wallH / 2f, cz - hb.size.z / 2f + segLen / 2f),
                new Vector3(0.6f, wallH, segLen));
            Block(new Vector3(faceX, wallH / 2f, cz + hb.size.z / 2f - segLen / 2f),
                new Vector3(0.6f, wallH, segLen));
        }
        Block(new Vector3(faceX, doorH + (wallH - doorH) / 2f, cz),
            new Vector3(0.6f, wallH - doorH, doorW));
        Block(new Vector3(backX, wallH / 2f, cz),
            new Vector3(0.6f, wallH, hb.size.z));
        Block(new Vector3(cx, wallH / 2f, hb.min.z),
            new Vector3(hb.size.x, wallH, 0.6f));
        Block(new Vector3(cx, wallH / 2f, hb.max.z),
            new Vector3(hb.size.x, wallH, 0.6f));
    }

    private void BuildHouse(string name, Vector3 center, float w, float d, float wallH, DoorSide doorSide, bool isPlayerHouse, bool plasterAlt)
    {
        Transform root = new GameObject(name).transform;
        root.SetParent(villageRoot, false);
        root.position = center;

        // Alternate plaster tint per house so the village is not one flat colour.
        Material plaster = plasterAlt ? matPlaster2 : matPlaster;
        if (plaster == null) plaster = matPlaster;

        float t = 0.3f;          // wall thickness
        float doorW = 1.2f;
        float doorH = 2.1f;      // Phase 1 real-size table: a door is 2.1 m tall.

        // Floor slab (stone plinth).
        Box(name + "_Floor", center + new Vector3(0f, 0.06f, 0f), new Vector3(w + 0.4f, 0.12f, d + 0.4f), matStone, root);
        // ROUND 5c: the floor slab top (0.12 m) is a walkable surface.
        VillageColliders.RegisterWalkable(center.x, center.z,
            Mathf.Max(w + 0.4f, d + 0.4f) / 2f, 0.12f);

        // Back wall (+/-X opposite the door) and side walls (+/-Z).
        float doorX = (doorSide == DoorSide.East) ? (w / 2f) : (-w / 2f);
        float backX = -doorX;
        Box(name + "_WallBack", center + new Vector3(backX, wallH / 2f, 0f), new Vector3(t, wallH, d), plaster, root);
        Box(name + "_WallSideA", center + new Vector3(0f, wallH / 2f, d / 2f), new Vector3(w, wallH, t), plaster, root);
        Box(name + "_WallSideB", center + new Vector3(0f, wallH / 2f, -d / 2f), new Vector3(w, wallH, t), plaster, root);

        // Front (door) wall: two segments + lintel above the door.
        float segLen = (d - doorW) / 2f;
        Box(name + "_WallFrontA", center + new Vector3(doorX, wallH / 2f, d / 2f - segLen / 2f), new Vector3(t, wallH, segLen), plaster, root);
        Box(name + "_WallFrontB", center + new Vector3(doorX, wallH / 2f, -d / 2f + segLen / 2f), new Vector3(t, wallH, segLen), plaster, root);
        Box(name + "_WallLintel", center + new Vector3(doorX, doorH + (wallH - doorH) / 2f, 0f), new Vector3(t, wallH - doorH, doorW), plaster, root);

        // Sloped clay-tile roof: two panels meeting at a ridge along X.
        float roofY = wallH + 0.55f;
        GameObject panelA = Box(name + "_RoofA", center + new Vector3(0f, roofY, d / 4f), new Vector3(w + 0.8f, 0.12f, d * 0.62f), matRoof, root);
        panelA.transform.rotation = Quaternion.Euler(30f, 0f, 0f);
        GameObject panelB = Box(name + "_RoofB", center + new Vector3(0f, roofY, -d / 4f), new Vector3(w + 0.8f, 0.12f, d * 0.62f), matRoof, root);
        panelB.transform.rotation = Quaternion.Euler(-30f, 0f, 0f);
        // Ridge beam.
        Box(name + "_RoofRidge", center + new Vector3(0f, wallH + 1.05f, 0f), new Vector3(w + 0.8f, 0.18f, 0.25f), matWood, root);

        // Door frame: two wood posts + a lintel beam around the doorway.
        float outDir = (doorSide == DoorSide.East) ? 1f : -1f;
        float frameX = doorX + outDir * 0.22f;
        Box(name + "_DoorFrameL", center + new Vector3(frameX, doorH / 2f, doorW / 2f + 0.09f), new Vector3(0.18f, doorH + 0.1f, 0.18f), matWood, root);
        Box(name + "_DoorFrameR", center + new Vector3(frameX, doorH / 2f, -doorW / 2f - 0.09f), new Vector3(0.18f, doorH + 0.1f, 0.18f), matWood, root);
        Box(name + "_DoorFrameTop", center + new Vector3(frameX, doorH + 0.09f, 0f), new Vector3(0.18f, 0.18f, doorW + 0.36f), matWood, root);

        // Windows (dark glass) on the side walls, with wood frames + cross bars.
        FrameWindow(root, name, center + new Vector3(-w / 4f, 1.7f, d / 2f), 1f);
        FrameWindow(root, name, center + new Vector3(w / 4f, 1.7f, -d / 2f), -1f);

        // Collision: wall segments as AABBs (doorway stays open).
        Block(center + new Vector3(backX, wallH / 2f, 0f), new Vector3(t + 0.2f, wallH, d));
        Block(center + new Vector3(0f, wallH / 2f, d / 2f), new Vector3(w, wallH, t + 0.2f));
        Block(center + new Vector3(0f, wallH / 2f, -d / 2f), new Vector3(w, wallH, t + 0.2f));
        Block(center + new Vector3(doorX, wallH / 2f, d / 2f - segLen / 2f), new Vector3(t + 0.2f, wallH, segLen));
        Block(center + new Vector3(doorX, wallH / 2f, -d / 2f + segLen / 2f), new Vector3(t + 0.2f, wallH, segLen));
        Block(center + new Vector3(doorX, doorH + (wallH - doorH) / 2f, 0f), new Vector3(t + 0.2f, wallH - doorH, doorW));

        if (isPlayerHouse)
        {
            BuildDoor(root, center, doorX, doorSide, doorW, doorH);
            BuildHouseInterior(root, center, w, d);
        }
    }

    // Window glass + wood frame (top/bottom rails, side posts, cross bars).
    // zDir: +1 = window on the +Z wall, -1 = on the -Z wall.
    private void FrameWindow(Transform root, string name, Vector3 pos, float zDir)
    {
        float z = pos.z + zDir * 0.02f;
        float fz = pos.z + zDir * 0.06f;
        Box(name + "_WindowGlass", new Vector3(pos.x, pos.y, z), new Vector3(0.8f, 0.8f, 0.06f), matGlass, root);
        Box(name + "_WindowTop", new Vector3(pos.x, pos.y + 0.45f, fz), new Vector3(0.96f, 0.1f, 0.08f), matWood, root);
        Box(name + "_WindowBottom", new Vector3(pos.x, pos.y - 0.45f, fz), new Vector3(0.96f, 0.1f, 0.08f), matWood, root);
        Box(name + "_WindowLeft", new Vector3(pos.x - 0.45f, pos.y, fz), new Vector3(0.1f, 0.96f, 0.08f), matWood, root);
        Box(name + "_WindowRight", new Vector3(pos.x + 0.45f, pos.y, fz), new Vector3(0.1f, 0.96f, 0.08f), matWood, root);
        Box(name + "_WindowBarV", new Vector3(pos.x, pos.y, fz), new Vector3(0.07f, 0.8f, 0.06f), matWood, root);
        Box(name + "_WindowBarH", new Vector3(pos.x, pos.y, fz), new Vector3(0.8f, 0.07f, 0.06f), matWood, root);
    }

    private void BuildDoor(Transform root, Vector3 center, float doorX, DoorSide doorSide, float doorW, float doorH)
    {
        // Hinge pivot at one edge of the doorway.
        float closedYaw = (doorSide == DoorSide.East) ? 90f : -90f;
        float openYaw = closedYaw + ((doorSide == DoorSide.East) ? -100f : 100f);
        Vector3 hingePos = center + new Vector3(doorX, 0f, doorW / 2f);

        GameObject hinge = new GameObject("Door_Player");
        hinge.transform.SetParent(root, false);
        hinge.transform.position = hingePos;
        hinge.transform.rotation = Quaternion.Euler(0f, closedYaw, 0f);

        // Door panel: extends along hinge-local +X from the hinge edge.
        GameObject panel = Box("DoorPanel", Vector3.zero, new Vector3(doorW, doorH, 0.09f), matWood, hinge.transform);
        panel.transform.localPosition = new Vector3(doorW / 2f, doorH / 2f, 0f);
        Destroy(panel.GetComponent<Collider>());

        // Door handle.
        GameObject handle = Sphere("DoorHandle", Vector3.zero, new Vector3(0.12f, 0.12f, 0.12f), matDark, hinge.transform);
        handle.transform.localPosition = new Vector3(doorW - 0.18f, 1.05f, 0.08f);
        Destroy(handle.GetComponent<Collider>());

        // Door controller (added at runtime, no scene GUID needed).
        DoorController dc = hinge.AddComponent<DoorController>();
        dc.hinge = hinge.transform;
        dc.closedYaw = closedYaw;
        dc.openYaw = openYaw;
        // Closed-door collision block (thin in X, wide in Z for an East door).
        dc.closedBlockCenter = hingePos + new Vector3(0f, doorH / 2f, -doorW / 2f);
        dc.closedBlockSize = new Vector3(0.5f, doorH, doorW + 0.2f);
        dc.RegisterBlock();
        playerDoor = dc;

        // Spot in front of the door where the monster stops to knock.
        GameObject front = new GameObject("DoorFront");
        front.transform.SetParent(root, false);
        float outDir = (doorSide == DoorSide.East) ? 1f : -1f;
        front.transform.position = center + new Vector3(doorX + outDir * 2.0f, 0f, 0f);
        doorFront = front.transform;
    }

    private void BuildHouseInterior(Transform root, Vector3 center, float w, float d)
    {
        // Bed spot: the real hospital-bed .glb is placed here in
        // BuildPropsAsync (the primitive bed is gone). Remember the spot
        // so Props can use it whichever house build ran.
        playerBedPos = center + new Vector3(-w / 2f + 1.0f, 0f, -d / 2f + 0.8f);
        // Table + clay pot.
        Box("Table", center + new Vector3(w / 2f - 1.0f, 0.45f, -d / 2f + 0.8f), new Vector3(1.1f, 0.08f, 0.7f), matWood, root);
        Box("TableLeg", center + new Vector3(w / 2f - 1.0f, 0.22f, -d / 2f + 0.8f), new Vector3(0.1f, 0.45f, 0.1f), matWood, root);
        Cylinder("ClayPot", center + new Vector3(w / 2f - 1.0f, 0.75f, d / 2f - 0.8f), new Vector3(0.5f, 0.5f, 0.5f), MakeFlat("PotClay", new Color(0.45f, 0.22f, 0.12f), 0.4f), root);
        // Diya (clay lamp, emissive only — no extra light cost).
        Cylinder("Diya", center + new Vector3(0f, 0.12f, -d / 2f + 0.5f), new Vector3(0.3f, 0.12f, 0.3f), matLampGlass, root);
    }

    // ------------------------------------------------------------------
    // Well + chowk lamp
    // ------------------------------------------------------------------
    private System.Collections.IEnumerator BuildWellAndLampAsync()
    {
        // Real stone well (.glb) at the chowk edge (kept clear of the
        // monster's lane path). Normalised by height (the model includes
        // its roof posts, so it is taller than wide).
        // Placement fix 2026-11-07: the well's west rim (x=-5.5) was
        // tangent to the west fence rail. Now (-4, -3.5): 0.5 m clear.
        Vector3 wellPos = new Vector3(-4f, 0f, -3.5f);
        GameObject well = null;
        yield return GltfModelLoader.LoadModel(
            "old_well_with_hanging_bucket_2_qm9u.glb", villageRoot, "Well",
            wellPos, 0f, 2.4f, GltfModelLoader.NormalizeMode.ByHeight,
            GltfModelLoader.ColliderMode.FullBounds, g => well = g);
        if (well == null)
        {
            // Primitive fallback (Phase 2 well). Phase 1 real-size table:
            // the well WALL is 0.8 m tall (posts + roof above it are the
            // rest of the structure).
            Cylinder("Well", wellPos + new Vector3(0f, 0.4f, 0f), new Vector3(2.0f, 0.8f, 2.0f), matStone, villageRoot);
            Cylinder("WellWater", wellPos + new Vector3(0f, 0.72f, 0f), new Vector3(1.7f, 0.06f, 1.7f), MakeFlat("WellWater", new Color(0.02f, 0.06f, 0.09f), 0.9f), villageRoot);
            Cylinder("WellPostA", wellPos + new Vector3(-0.9f, 1.6f, 0f), new Vector3(0.18f, 2.2f, 0.18f), matWood, villageRoot);
            Cylinder("WellPostB", wellPos + new Vector3(0.9f, 1.6f, 0f), new Vector3(0.18f, 2.2f, 0.18f), matWood, villageRoot);
            GameObject wellRoof = Box("WellRoof", wellPos + new Vector3(0f, 2.8f, 0f), new Vector3(2.6f, 0.12f, 1.8f), matRoof, villageRoot);
            wellRoof.transform.rotation = Quaternion.Euler(0f, 0f, 12f);
            // Crossed wood beams under the well roof.
            GameObject beamA = Box("WellBeamA", wellPos + new Vector3(0f, 2.62f, 0f), new Vector3(2.4f, 0.12f, 0.12f), matWood, villageRoot);
            beamA.transform.rotation = Quaternion.Euler(0f, 0f, 10f);
            GameObject beamB = Box("WellBeamB", wellPos + new Vector3(0f, 2.62f, 0f), new Vector3(0.12f, 0.12f, 1.7f), matWood, villageRoot);
            beamB.transform.rotation = Quaternion.Euler(10f, 0f, 0f);
            Block(wellPos + new Vector3(0f, 0.4f, 0f), new Vector3(2.2f, 0.9f, 2.2f));
        }

        // Food pickup (crate) next to the well.
        foodPickup = MakePickup("Pickup_Food", wellPos + new Vector3(1.9f, 0.3f, 1.2f), "Khana", new Vector3(0.65f, 0.6f, 0.65f));

        // Fix-push P2-18b: 3 more pickups, same Pickup system as the food
        // crate — water by the handpump, medicine inside Nilesh's house,
        // battery by the chowk lamp.
        // PHONE-VIDEO FIX 2026-11-09: was (5, 10.2) — INSIDE House_2
        // (11.5 m wide, x[3.22,14.78] z[2.23,13.77]), unreachable. Now
        // by the handpump as intended, west of House_2.
        waterPickup = MakePickup("Pickup_Water", new Vector3(2.3f, 0.3f, 9f), "Paani");
        // Phase 2 collision fix (audit O6): the medicine crate sat ON
        // the hospital bed ((-10.5, 5) vs bed centre (-10.5, 4.8)).
        // Now (-8.2, 6.8): inside the house's north-east corner, clear
        // of the bed, the table and the door swing.
        medicinePickup = MakePickup("Pickup_Medicine", new Vector3(-8.2f, 0.3f, 6.8f), "Dawai");
        batteryPickup = MakePickup("Pickup_Battery", new Vector3(1.2f, 0.3f, 2.8f), "Battery");

        // Chowk lamp: pole + warm flickering point light + glow.
        // Phase 2: the old 0.5 m emissive head read as a pale yellow
        // cube in daylight (screenshot). Smaller glass (0.34 m) inside
        // a dark cap + base frame, so it reads as a lantern, not a box.
        // OVERNIGHT R1: still read as "black pole + yellow cube" by day
        // (audit) — the head was a solid emissive box and the pole
        // near-black iron. Now the pole/cap/base are weathered grey-brown
        // iron (matLampPole) and the head is the same iron frame; only
        // the small bulb sphere inside stays emissive glass.
        Vector3 lampPos = new Vector3(2.5f, 0f, 1.5f);
        Cylinder("LampPole", lampPos + new Vector3(0f, 1.6f, 0f), new Vector3(0.22f, 3.2f, 0.22f), matLampPole, villageRoot);
        Box("LampHead", lampPos + new Vector3(0f, 3.3f, 0f), new Vector3(0.34f, 0.3f, 0.34f), matLampPole, villageRoot);
        Box("LampCap", lampPos + new Vector3(0f, 3.5f, 0f), new Vector3(0.44f, 0.08f, 0.44f), matLampPole, villageRoot);
        Box("LampBase", lampPos + new Vector3(0f, 3.12f, 0f), new Vector3(0.4f, 0.06f, 0.4f), matLampPole, villageRoot);
        GameObject bulb = Sphere("LampBulb", lampPos + new Vector3(0f, 3.28f, 0f), new Vector3(0.22f, 0.22f, 0.22f), matLampGlass, villageRoot);
        Destroy(bulb.GetComponent<Collider>());
        GameObject lightGO = new GameObject("LampLight");
        lightGO.transform.SetParent(villageRoot, false);
        lightGO.transform.position = lampPos + new Vector3(0f, 3.2f, 0f);
        lampLight = lightGO.AddComponent<Light>();
        lampLight.type = LightType.Point;
        lampLight.color = new Color(1f, 0.75f, 0.4f);
        lampLight.intensity = 1.4f;
        lampLight.range = 10f;
        lampGlow = MakeGlowQuad("LampGlow", lampPos + new Vector3(0f, 3.3f, 0f), 2.2f, new Color(1f, 0.7f, 0.3f, 0.55f));
        Block(lampPos + new Vector3(0f, 1.6f, 0f), new Vector3(0.4f, 3.2f, 0.4f));
    }

    // ------------------------------------------------------------------
    // Field, jungle, fences, props, moon
    // ------------------------------------------------------------------
    private void BuildField()
    {
        // Crop field, north-east of the chowk.
        Vector3 fieldPos = new Vector3(10f, 0f, 16f);
        GameObject field = Box("Field", fieldPos + new Vector3(0f, 0.02f, 0f), new Vector3(10f, 0.06f, 6f), matGrass, villageRoot);
        Destroy(field.GetComponent<Collider>());

        // Crop rows: TEXTURED crossed-plane wheat (fix pass 2026-11-08).
        // The box stalks (0.14 m columns + box heads) still read as
        // primitive columns at night. Each plant is now two crossed
        // quads (X-shape, 0.7 x 0.9 m) with the wheat_plant cutout
        // texture — a real stalk + grain head silhouette that reads as
        // wheat from any angle, in the same 6x14 dense layout.
        if (cropPlantMat == null) cropPlantMat = MakeCropPlantMaterial();
        if (cropPlantMesh == null) cropPlantMesh = BuildCropPlantMesh();
        if (cropPlantMat != null && cropPlantMesh != null)
        {
            for (int r = 0; r < 6; r++)
            {
                for (int c = 0; c < 14; c++)
                {
                    float jx = ((r * 3 + c * 5) % 5 - 2) * 0.06f;
                    float jz = ((r * 11 + c * 7) % 5 - 2) * 0.06f;
                    float yaw = (r * 31 + c * 17) % 360;
                    float s = 0.85f + ((r * 7 + c * 13) % 5) * 0.07f;
                    Vector3 p = fieldPos + new Vector3(-4.55f + c * 0.7f + jx, 0.02f, -2.2f + r * 0.85f + jz);
                    GameObject plant = new GameObject("CropPlant");
                    plant.transform.SetParent(villageRoot, false);
                    plant.transform.position = p;
                    plant.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                    plant.transform.localScale = new Vector3(s, s, s);
                    MeshFilter mf = plant.AddComponent<MeshFilter>();
                    mf.sharedMesh = cropPlantMesh;
                    MeshRenderer mr = plant.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = cropPlantMat;
                }
            }
        }
    }

    private System.Collections.IEnumerator BuildJungleAndFencesAsync()
    {
        // Inner trees: REAL models first — they are also the clone sources
        // for the jungle-edge ring (clones share meshes/materials, so the
        // ring costs no extra texture memory).
        // Phase 1 real-size table: mango 9 m, banyan 13 m, palm 10 m.
        // The old heights (6/7/5 m) made the jungle read as shrubs next
        // to the 10 m temple.
        // Phase 2 collision fixes (audit O5/O9/O10): the mango's trunk
        // sat 0.5 m off the clinic's west wall, the banyan's canopy
        // overhung the haveli's north face, and the palm's trunk sat on
        // the dock's edge. All three move clear; the mango + banyan +
        // chinaberry leaf meshes were also restored from the original
        // files this pass (their canopies were decimated 87-94%).
        GameObject mango = null, banyan = null; // ROUND 5b: palm var dropped (hero + ring palms removed).
        yield return LoadRealTree("mango_tree_3_sl11.glb", "Tree_Mango",
            new Vector3(-18f, 0f, -5f), 9f, GltfModelLoader.ColliderMode.Trunk, g => mango = g);
        // Replacement 2026-11-07: Chinese Banyan, 111k tris with real
        // aerial roots (the old 3.2k-tri banyan + its 35 m base disc are gone).
        yield return LoadRealTree("chinese_banyan_ficus_microcarpa_9_tpa1.glb", "Tree_Banyan",
            new Vector3(19f, 0f, 5f), 13f, GltfModelLoader.ColliderMode.Trunk, g => banyan = g);
        // ROUND 5 (final chance): the hero palm is REMOVED, not replaced.
        // Its trunk is stacked flat yellow plates IN the model geometry
        // (verified in the .glb: a single trunk mesh of ringed plates) —
        // repainting cannot fix the shape, and no free realistic palm
        // .glb passes verification this round (searched Poly Haven,
        // GitHub, itch.io, Tripo, Meshy, free3d, CGTrader, Sketchfab,
        // Quaternius — the one downloadable candidate failed; everything
        // else needs a login, is low-poly/cartoon, or is paid). The palm
        // stood at (-1, 16.5), right on the spawn path, so it was the
        // worst offender in the user's phone videos. The jungle ring
        // keeps its far-variant palms (palm_far, distant, small on
        // screen). If a verified realistic palm is found later, restore
        // this call with the new file.
        // yield return LoadRealTree("realistic_palm_tree_free_8_k8rm.glb", "Tree_Palm",
        //     new Vector3(-1f, 0f, 16.5f), 10f, GltfModelLoader.ColliderMode.FullBounds,
        //     g => { palm = g; FixPalmMaterials(g); });
        // Phase 2 REMOVED: trees_25_wae0 "Tree_Pack" (was (-18,-18), 8 m).
        // Its three materials are flat colours with NO textures and
        // single-sided (doubleSided=false) — a cartoon-style cluster
        // that clashed with every PBR tree, and its spread clipped the
        // school's corner (audit O7). The SW corner is already covered
        // by the jungle ring; nothing replaces it.

        // Jungle edge: REAL tree clones ringing the village. The old
        // primitive blob trees (BuildTree) are gone — user ordered no
        // cartoon. Same ring positions as before, sources round-robin.
        // PERF 2026-11-08: the ring used to clone the full-detail trees 36x
        // (~3 M triangles). It now clones dedicated far-distance variants
        // (~3k tris each, 256 px KTX2 textures, made offline with
        // gltf-transform), and materials have GPU instancing on.
        // ROUND 4 (pop-in): the LODGroup that used to cull a clone when
        // it was tiny on screen is GONE — it made ring trees wink in/out
        // as the camera panned. If a far variant fails to load, the near
        // tree is used as before.
        GameObject mangoFar = null, banyanFar = null;
        Vector3 hidden = new Vector3(0f, 0f, -80f);
        yield return GltfModelLoader.LoadModel("mango_tree_far.glb", villageRoot, "Tree_MangoFar", hidden, 0f, 9f,
            GltfModelLoader.NormalizeMode.ByHeight, GltfModelLoader.ColliderMode.None, g => mangoFar = g);
        yield return GltfModelLoader.LoadModel("banyan_far.glb", villageRoot, "Tree_BanyanFar", hidden, 0f, 13f,
            GltfModelLoader.NormalizeMode.ByHeight, GltfModelLoader.ColliderMode.None, g => banyanFar = g);
        // ROUND 5b: the ring palm is REMOVED, not replaced — same broken
        // model as the hero palm. FixPalmMaterials DOES apply (CI log
        // proves 3 mats, baseMap=True) yet the fronds render BLACK on
        // Vulkan (emulator) while green in the CI editor: a renderer
        // difference no material tweak fixes. No verified replacement
        // exists (Round 5 search: Poly Haven has no palm, the rest are
        // low-poly/banned/paid/login-walled). The ring is mango/banyan
        // only now; restore a palm here if a verified one is found.
        // yield return GltfModelLoader.LoadModel("palm_far.glb", villageRoot, "Tree_PalmFar", hidden, 0f, 10f,
        //     GltfModelLoader.NormalizeMode.ByHeight, GltfModelLoader.ColliderMode.None, g => palmFar = g);
        GameObject[] ringSources = new GameObject[] {
            mangoFar != null ? mangoFar : mango,
            banyanFar != null ? banyanFar : banyan };
        foreach (GameObject rs in ringSources)
        {
            if (rs == null) continue;
            foreach (Renderer r in rs.GetComponentsInChildren<Renderer>())
                foreach (Material m in r.sharedMaterials)
                    if (m != null) m.enableInstancing = true;
        }
        int ringIndex = 0;
        for (int x = -20; x <= 20; x += 4)
        {
            CloneRingTree(ringSources, ringIndex++, new Vector3(x, 0f, -21f));
            CloneRingTree(ringSources, ringIndex++, new Vector3(x, 0f, 21f));
        }
        for (int z = -16; z <= 16; z += 5)
        {
            CloneRingTree(ringSources, ringIndex++, new Vector3(-21f, 0f, z));
            CloneRingTree(ringSources, ringIndex++, new Vector3(21f, 0f, z));
        }
        // ROUND 5b: second, sparser ring at ~30 m — cheap horizon
        // dressing. The first ring sits at 21 m and beyond it the bare
        // ground plane runs to the fog fade (~100 m), which reads as a
        // flat brown horizon in every wide shot. A second tree line at
        // 30 m (mango/banyan only — no palms left) breaks the band.
        // Deterministic radial jitter so it does not read as a fence
        // line; same keep-out rules as the first ring (CloneRingTree).
        for (int x = -30; x <= 30; x += 6)
        {
            float j = ((ringIndex % 3) - 1) * 1.5f;
            CloneRingTree(ringSources, ringIndex++, new Vector3(x, 0f, -30f + j));
            CloneRingTree(ringSources, ringIndex++, new Vector3(x, 0f, 30f - j));
        }
        for (int z = -24; z <= 24; z += 8)
        {
            float j = ((ringIndex % 3) - 1) * 1.5f;
            CloneRingTree(ringSources, ringIndex++, new Vector3(-30f + j, 0f, z));
            CloneRingTree(ringSources, ringIndex++, new Vector3(30f - j, 0f, z));
        }

        // The far sources were only templates for the ring clones.
        if (mangoFar != null) mangoFar.SetActive(false);
        if (banyanFar != null) banyanFar.SetActive(false);

        // Wood fences along the main lane: posts + two rails per segment.
        for (int i = 0; i < 5; i++)
        {
            BuildFenceSegment(new Vector3(-5.5f, 0f, -12f + i * 3.2f), 2.8f);
        }
        for (int i = 0; i < 4; i++)
        {
            BuildFenceSegment(new Vector3(5.5f, 0f, 10f + i * 3.0f), 2.6f);
        }
    }

    private System.Collections.IEnumerator LoadRealTree(
        string file, string goName, Vector3 pos, float height,
        GltfModelLoader.ColliderMode colMode, System.Action<GameObject> onDone)
    {
        GameObject t = null;
        yield return GltfModelLoader.LoadModel(
            file, villageRoot, goName, pos, 0f, height,
            GltfModelLoader.NormalizeMode.ByHeight, colMode, g => t = g);
        if (t == null)
        {
            // Real tree failed to load — leave the spot empty. Never fall
            // back to a primitive blob tree (user ordered no cartoon).
            Debug.LogWarning("[Village] Real tree failed to load, skipped: " + file);
            if (onDone != null) onDone(null);
            yield break;
        }
        // (Old banyan disc workaround REMOVED 2026-11-07: the old Sketchfab
        // banyan carried a 35 m base disc "pDisc1" that had to be hidden.
        // The replacement Chinese Banyan has real aerial roots and no
        // base disc, so there is nothing to hide.)
        if (onDone != null) onDone(t);
    }

    // Clone one of the loaded real trees onto the jungle-edge ring.
    // Clones share the source's meshes/materials (no extra texture
    // memory). Deterministic yaw + scale jitter so clones are not clones.
    // Phase 2 collision fix (audit O2/O3/O4): a ring tree used to grow
    // THROUGH the gate arch at (0, 21), two flanked the Angkor temple
    // at (21, 12), and one stood on the dock at (-7, 19). Clones whose
    // trunk would land inside a landmark's clearance are skipped — a
    // missing ring tree reads as a gap in the jungle, a tree through a
    // gateway reads as a bug.
    private void CloneRingTree(GameObject[] sources, int index, Vector3 pos)
    {
        Vector3[] keepOut = new Vector3[]
        {
            new Vector3(0f, 0f, 20.5f), new Vector3(21f, 0f, 12f), new Vector3(-7f, 0f, 19f),
        };
        float[] keepOutR = new float[] { 3.5f, 4.5f, 4f };
        for (int k = 0; k < keepOut.Length; k++)
        {
            float dx = pos.x - keepOut[k].x, dz = pos.z - keepOut[k].z;
            if (dx * dx + dz * dz < keepOutR[k] * keepOutR[k]) return;
        }
        GameObject src = null;
        for (int k = 0; k < sources.Length; k++)
        {
            GameObject cand = sources[(index + k) % sources.Length];
            if (cand != null) { src = cand; break; }
        }
        if (src == null) return;
        GameObject c = Object.Instantiate(src, villageRoot);
        c.name = src.name + "_Ring";
        c.transform.position = pos;
        c.transform.rotation = Quaternion.Euler(0f, (index * 47) % 360, 0f);
        float s = 0.9f + (index % 3) * 0.12f;
        c.transform.localScale = src.transform.localScale * s;
        c.SetActive(true);
        // ROUND 5 (lag): ring trees are distant far-variants — rendering
        // each one into the shadow map costs a draw call for almost no
        // visible gain (their shadows fall outside the play area). Shadow
        // casting stays ON for the hero trees, houses and props inside
        // the village. This is per-RENDERER, so the shared materials and
        // GPU instancing are untouched. (No LODGroup here — Round 4
        // proved a culling LOD makes ring trees wink in/out.)
        foreach (Renderer r in c.GetComponentsInChildren<Renderer>())
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        // ROUND 4 (pop-in): NO LODGroup here. A single LOD at 0.025
        // culls the whole tree when it covers < 2.5% of the screen, so
        // ring trees winked in/out as the camera panned — the "village
        // appears/disappears" bug. The ring already uses dedicated
        // ~3k-tri far variants, so ~36 trees staying rendered costs
        // ~108k tris total — trivial. Never a single-LOD setup whose
        // only next step is "culled".
        Block(pos + new Vector3(0f, 1.1f, 0f), new Vector3(0.6f, 2.2f, 0.6f));
    }

    private void BuildFenceSegment(Vector3 pos, float length)
    {
        float half = length / 2f;
        Box("FencePostA", pos + new Vector3(0f, 0.6f, -half), new Vector3(0.16f, 1.2f, 0.16f), matWood, villageRoot);
        Box("FencePostB", pos + new Vector3(0f, 0.6f, half), new Vector3(0.16f, 1.2f, 0.16f), matWood, villageRoot);
        Box("FenceRailTop", pos + new Vector3(0f, 0.95f, 0f), new Vector3(0.09f, 0.12f, length), matWood, villageRoot);
        Box("FenceRailLow", pos + new Vector3(0f, 0.45f, 0f), new Vector3(0.09f, 0.12f, length), matWood, villageRoot);
        Block(pos + new Vector3(0f, 0.55f, 0f), new Vector3(0.3f, 1.1f, length));
    }

    // Phase 3: props + animals are REAL Sketchfab models now. A model
    // that fails to load is simply skipped (decorative — no fallback).
    // NOTE: the charpai .glb is deliberately NOT placed — its file carries
    // a stray 911-unit "bake" plane that would render as a huge flat slab.
    // Fix-push P0-4: the file was deleted from the project (backed up in
    // fix-backups/) since nothing referenced it.
    private System.Collections.IEnumerator BuildPropsAsync()
    {
        // Vehicles / big props.
        // Phase 1 real-size table: carts, car and tractor are scaled by
        // HEIGHT now (a car is 1.5 m tall, not 3 m long — the old ByWidth
        // sizes made the Ambassador 1.4 m tall and the tractor 2.8 m).
        // PHONE-VIDEO FIX 2026-11-09: was (5.5, 3) — INSIDE House_2
        // (11.5 m wide, x[3.22,14.78] z[2.23,13.77]). Now south of it.
        yield return LoadProp("medieval_wooden_cart_1_jab4.glb", "Cart",
            new Vector3(5f, 0f, 0.5f), 0f, 1.5f,
            GltfModelLoader.NormalizeMode.ByHeight, GltfModelLoader.ColliderMode.FullBounds);
        // Replacement 2026-11-07: Bullcart, 105k tris — good geometry but
        // ZERO textures as distributed (confirmed twice). Wood PBR is
        // applied in code (ApplyWoodPbr) — never used flat-coloured.
        // Placement fix 2026-11-07: the cart sat at (-6, -4) — ON the
        // west fence line (x=-5.5, z -13.4 to -1), the rail passing
        // through it. Now (-7.5, -4): 0.75 m clear of the fence.
        yield return LoadProp("Bullcart.glb", "BullockCart",
            new Vector3(-7.5f, 0f, -4f), 0f, 1.6f,
            GltfModelLoader.NormalizeMode.ByHeight, GltfModelLoader.ColliderMode.FullBounds,
            ApplyWoodPbr);
        // Replacement 2026-11-07: Classic Tractor, 81.7k tris — good
        // geometry but ZERO textures as distributed. PBR is applied per
        // material name in code (ApplyTractorPbr) — never flat-coloured.
        // Phase 2 collision fix (audit O11): the tractor sat at (12, 11),
        // tangent to House_2 (9, 8) — 4.24 m apart vs a 4.25 m half-sum,
        // its nose on the house wall. Now (14, 12): 6.4 m from House_2,
        // 3.2 m from the goat, clear of the field's south edge (z=13).
        // PHONE-VIDEO FIX 2026-11-09: (14, 12) is INSIDE House_2
        // (11.5 m wide, x[3.22,14.78] z[2.23,13.77] — the "6.4 m"
        // was centre-to-centre, ignoring the width). Now (16, 15):
        // in the crop field, 1.2 m clear of House_2's east wall.
        yield return LoadProp("classic_tractor__1__11_scro.glb", "Tractor",
            new Vector3(16f, 0f, 15f), 30f, 2.5f,
            GltfModelLoader.NormalizeMode.ByHeight, GltfModelLoader.ColliderMode.FullBounds,
            ApplyTractorPbr);
        // OVERNIGHT (Ambassador FIXED, 2026-11-09): the isolated Blender
        // split SUCCEEDED — the repo file is now the fixed .glb (same
        // filename, 7,993 tris, geometry byte-identical): BodyCream
        // (cream 0.887/0.823/0.673, rough 0.35, normal map kept, NO base
        // texture) + WheelRubber (near-black) + blinn2 glass unchanged.
        // Verified in the file's own JSON + preview (clean cream/white
        // Ambassador, dark wheels, no red doors / orange front / yellow
        // wheels). The car is BACK in the lane at the gate — the story
        // beat (car breaks down at the gate) returns with it. NOTE: do
        // NOT run WeatherAmbassador on the fixed file — it targets the
        // old blinn1 material (gone now) and would force smoothness 0.9
        // onto the authored BodyCream paint. The original toy file stays
        // in git history as the backup.
        yield return LoadProp("hindustan_ambassador_4_rh04.glb", "HeroCar",
            new Vector3(2.5f, 0f, 15f), 30f, 1.5f,
            GltfModelLoader.NormalizeMode.ByHeight, GltfModelLoader.ColliderMode.FullBounds);
        // Ancient gate at the north lane entrance — a SOLID stair
        // monument (verified from ancient_gate_11_4q5m.glb: one mesh,
        // 2.8 m wide x 2.9 m deep x 4.5 m tall, solid from the ground
        // up — there is no arch to walk through). FullBounds, so the
        // player walks up the lane and stops at the gate instead of
        // passing through the stone. ROUND 6: was None ("walk through
        // the arch") — the arch does not exist in the mesh.
        yield return LoadProp("ancient_gate_11_4q5m.glb", "Gate",
            new Vector3(0f, 0f, 20.5f), 0f, 4.5f,
            GltfModelLoader.NormalizeMode.ByHeight, GltfModelLoader.ColliderMode.FullBounds);
        // Small props near the houses.
        // Placement fix 2026-11-07: the handpump sat at (5, 9), 0.25 m
        // from the east fence rail. Now (4.5, 9): clear of the fence.
        // PHONE-VIDEO FIX 2026-11-09: (4.5, 9) is INSIDE House_2
        // (11.5 m wide, x[3.22,14.78] z[2.23,13.77]). Now (1.5, 9):
        // west of House_2, still by Villager_Man's patrol.
        yield return LoadProp("handpump_3d_39_aah5.glb", "Handpump",
            new Vector3(1.5f, 0f, 9f), 0f, 1.3f,
            GltfModelLoader.NormalizeMode.ByHeight, GltfModelLoader.ColliderMode.FullBounds);
        // Phase 2 collision fix (audit O17): the matka grazed the wolf
        // (1.12 m apart vs a 1.15 m half-sum). Now (-4.2, 9.2): 2.2 m
        // from the wolf, 4.3 m from the chicken's new spot.
        yield return LoadProp("indian_terracotta_matka_red_clay_water_pot_40_ris7.glb", "Matka",
            new Vector3(-4.2f, 0f, 9.2f), 0f, 0.5f,
            GltfModelLoader.NormalizeMode.ByWidth, GltfModelLoader.ColliderMode.FullBounds,
            MuteMatka);
        // PHONE-VIDEO FIX 2026-11-09: was (7.5, 3.5) — INSIDE House_2
        // (11.5 m wide, x[3.22,14.78] z[2.23,13.77]). Now south of it.
        yield return LoadProp("rusty_and_oil_stained_oil_barrel_43_s99h.glb", "OilBarrel",
            new Vector3(7.5f, 0f, 0.5f), 0f, 0.9f,
            GltfModelLoader.NormalizeMode.ByHeight, GltfModelLoader.ColliderMode.FullBounds,
            MuteOilBarrel);
        // Hospital bed INSIDE Nilesh's house (replaces the primitive bed).
        yield return LoadProp("old_and_worn_out_hospital_bed_42_kn6o.glb", "HospitalBed",
            playerBedPos, 90f, 2.1f,
            GltfModelLoader.NormalizeMode.ByWidth, GltfModelLoader.ColliderMode.FullBounds);
        // Animals: cow, goat and buffalo in the field; horse by the cart;
        // the dog by Nilesh's house.
        // Phase 1 real-size table: cow/buffalo 1.45 m at the shoulder,
        // goat 0.7 m, horse 1.6 m, wolf 0.75 m — all by HEIGHT now. The
        // old ByWidth sizes made the horse 2.35 m tall and the wolf
        // 1.0 m at the shoulder (a pony-sized dog).
        yield return LoadProp("cow_15_u1bj.glb", "Cow",
            new Vector3(8f, 0f, 15.5f), 0f, 1.45f,
            GltfModelLoader.NormalizeMode.ByHeight, GltfModelLoader.ColliderMode.None,
            g => AddAnimalRoam(g, new Vector3(8f, 0f, 15.5f), 4f, 0.5f, false));
        // Replacement 2026-11-07: Boer Goat, 10.4k tris, FULL PBR
        // (the old 1.5k-tri toy goat is gone).
        // Placement fix 2026-11-07: the goat stood at (12, 13) — INSIDE
        // the tractor's footprint (tractor at (12, 11) spans z 8.5-13.5).
        // Now (13, 15): inside the crop field, clear of the tractor.
        yield return LoadProp("boer_goat__3d_model_free_7_dr1w.glb", "Goat",
            new Vector3(13f, 0f, 15f), 0f, 0.7f,
            GltfModelLoader.NormalizeMode.ByHeight, GltfModelLoader.ColliderMode.None,
            g => AddAnimalRoam(g, new Vector3(13f, 0f, 15f), 3.5f, 0.7f, false));
        yield return LoadProp("water_buffalo_18_44fy.glb", "Buffalo",
            new Vector3(6.8f, 0f, 17f), 0f, 1.45f,
            GltfModelLoader.NormalizeMode.ByHeight, GltfModelLoader.ColliderMode.None,
            g => AddAnimalRoam(g, new Vector3(6.8f, 0f, 17f), 4f, 0.5f, false));
        // Replacement 2026-11-07: Horse, 70.5k tris, albedo+normal
        // (the old 2.9k-tri horse rendered black).
        yield return LoadProp("horse_6_jrh4.glb", "Horse",
            new Vector3(2f, 0f, 5f), 0f, 1.6f,
            GltfModelLoader.NormalizeMode.ByHeight, GltfModelLoader.ColliderMode.None,
            g => AddAnimalRoam(g, new Vector3(2f, 0f, 5f), 3f, 0.8f, false));
        // Wolf swap 2026-11-08: Dire Wolf Lite (Literock Studios, itch.io),
        // 9,991 tris, FULL PBR, rigged, 4 anims — replaces the old Shiba
        // (4,316 tris, 0 PBR; backed up to fix-backups/wolf-swap-20261108/).
        // Phase 1 real-size table: a dog/wolf is 0.6-0.8 m at the shoulder.
        // The old ByWidth 1.8 made it 1.0 m at the shoulder — a calf-sized dog.
        yield return LoadProp("Dire_Wolf_Lite.glb", "Wolf",
            new Vector3(-6f, 0f, 8f), 0f, 0.75f,
            GltfModelLoader.NormalizeMode.ByHeight, GltfModelLoader.ColliderMode.None,
            g => AddAnimalRoam(g, new Vector3(-6f, 0f, 8f), 4f, 0.9f, true));
    }

    // Fix-push P2-18b: a pickup crate like the food one (Box + Pickup).
    // ROUND 5b: the old pickup was a bare cube in matWood — and the
    // wood_planks texture itself is GREY (avg RGB 107,100,90), so the
    // crates read as flat grey placeholder cubes in the middle of the
    // path. Now a real supply crate: warm-brown body + a coloured lid
    // per item (Battery yellow, Paani blue, Dawai white, Khana green),
    // built on a root so the Pickup bob/rotate moves the whole assembly.
    // Collection is distance-based (GameManager.TryCollectPickup, 2.8 m),
    // so the Pickup living on the root is safe.
    private Pickup MakePickup(string name, Vector3 pos, string itemName, Vector3 size = default(Vector3))
    {
        if (size == Vector3.zero) size = new Vector3(0.55f, 0.5f, 0.55f);
        GameObject root = new GameObject(name);
        root.transform.SetParent(villageRoot, false);
        root.transform.position = pos;
        Box(name + "_Body", pos, size, matCrate, root.transform);
        Color lidColor = new Color(0.25f, 0.6f, 0.25f); // Khana green
        if (itemName == "Battery") lidColor = new Color(0.9f, 0.75f, 0.1f);
        else if (itemName == "Paani") lidColor = new Color(0.15f, 0.45f, 0.8f);
        else if (itemName == "Dawai") lidColor = new Color(0.9f, 0.9f, 0.9f);
        Material lidMat = MakeFlat("Lid_" + itemName, lidColor, 0.4f);
        Vector3 lidSize = new Vector3(size.x * 0.78f, 0.08f, size.z * 0.78f);
        Box(name + "_Lid", pos + new Vector3(0f, size.y / 2f + lidSize.y / 2f, 0f), lidSize, lidMat, root.transform);
        Pickup p = root.AddComponent<Pickup>();
        p.itemName = itemName;
        p.bobHeight = 0.12f;
        return p;
    }

    // onLoaded (optional): runs after the model is loaded + URP-fixed, so
    // callers can texture/weather a model that shipped without textures
    // (bullock cart, tractor) or clean (Ambassador).
    private System.Collections.IEnumerator LoadProp(
        string file, string goName, Vector3 pos, float yaw, float size,
        GltfModelLoader.NormalizeMode normMode, GltfModelLoader.ColliderMode colMode,
        System.Action<GameObject> onLoaded = null)
    {
        GameObject p = null;
        yield return GltfModelLoader.LoadModel(
            file, villageRoot, goName, pos, yaw, size, normMode, colMode,
            g => { p = g; if (g != null && onLoaded != null) onLoaded(g); });
        if (p == null)
        {
            Debug.LogWarning("[Village] Prop skipped (load failed): " + file);
        }
    }

    // ---- Post-load texturing helpers (replacement 2026-11-07) ----
    // Some replacement models shipped WITHOUT textures (bullock cart,
    // tractor) or clean (Ambassador). Their geometry is good, so PBR is
    // applied here from the Poly Haven maps cheap-wins downloaded
    // (Resources/Textures/) — never used flat-coloured.

    private static Texture2D Tex(string name)
    {
        Texture2D t = Resources.Load<Texture2D>("Textures/" + name);
        if (t == null) Debug.LogWarning("[Village] Texture missing: Textures/" + name);
        return t;
    }

    // Swaps one glTF material to URP/Lit, preserving its colour and maps.
    // Shader Graph glTF materials cannot take URP detail maps or
    // metallic-gloss maps, so weathering/texturing needs URP/Lit.
    // NOTE: a glTF metallic-roughness texture is NOT a URP metallic-gloss
    // map (different channel packing) — do not copy it across; use the
    // metallic/smoothness floats instead.
    private static Material ToUrpLit(Material m)
    {
        if (m == null) return null;
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null || m.shader == lit) return m;
        Texture baseTex = m.HasProperty("baseColorTexture") ? m.GetTexture("baseColorTexture") : null;
        if (baseTex == null) baseTex = m.mainTexture;
        Color baseCol = m.HasProperty("baseColorFactor") ? m.GetColor("baseColorFactor") : m.color;
        Texture normTex = m.HasProperty("normalTexture") ? m.GetTexture("normalTexture") : null;
        Texture occTex = m.HasProperty("occlusionTexture") ? m.GetTexture("occlusionTexture") : null;
        m.shader = lit;
        if (baseTex != null) m.SetTexture("_BaseMap", baseTex);
        m.SetColor("_BaseColor", baseCol);
        if (normTex != null) m.SetTexture("_BumpMap", normTex);
        if (occTex != null) m.SetTexture("_OcclusionMap", occTex);
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_Smoothness", 0.5f);
        m.SetFloat("_Cull", 0f);
        return m;
    }

    // Phase 2: the thatched hut (House_Player) and the old village house
    // (House_5) ship with a flat saturated orange plaster texture that
    // reads as a bright toy slab in daylight. There is no separate wall
    // material to swap — the whole main texture is one material — so
    // the fix is a weathering tint on that material only (ToUrpLit first,
    // like WeatherAmbassador): multiply the base colour down and toward
    // grey-brown. Deliberately mild (0.78/0.74/0.68): the walls should
    // look like weathered terracotta, not repainted grey. Verify in the
    // CI screenshot — if the wrong house is orange, this tint is a no-op
    // on the right one and the real culprit needs its own pass.
    private static void MuteOrangePlaster(GameObject holder, string materialName)
    {
        if (holder == null) return;
        foreach (Renderer r in holder.GetComponentsInChildren<Renderer>())
        {
            foreach (Material m in r.sharedMaterials)
            {
                if (m == null || m.name != materialName) continue;
                ToUrpLit(m);
                Color c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white;
                m.SetColor("_BaseColor", new Color(c.r * 0.78f, c.g * 0.74f, c.b * 0.68f, c.a));
            }
        }
        Debug.Log("[Village] Orange plaster muted on " + holder.name + " (" + materialName + ")");
    }

    // PILOT: the hut's walls are ONE orange plaster texture (Material.002,
    // the only textured wall material — the 4 big bool meshes are the
    // thatch roof, the 5 cubes are the dark-wood frame) and the Phase 2
    // tint (x0.78/0.74/0.68 on the base colour) barely dented it — the
    // walls stayed bright orange-red in the fix-round screenshot. A colour
    // multiplier cannot fix a saturated texture, so the wall gets a REAL
    // PBR treatment: the muted clay-block Poly Haven set (diff + normal +
    // metal-smooth + AO) replaces the orange base map, tinted warm
    // grey-brown. The thatch roof (Material.001) is untouched.
    private static void WeatherHutPlaster(GameObject holder)
    {
        if (holder == null) return;
        Texture2D albedo = Tex("clay_block_wall_diff_1k");
        Texture2D normal = Tex("clay_block_wall_nor_gl_1k");
        Texture2D metalSmooth = Tex("clay_block_wall_metal_smooth_1k");
        Texture2D ao = Tex("clay_block_wall_ao_1k");
        foreach (Renderer r in holder.GetComponentsInChildren<Renderer>())
        {
            foreach (Material m in r.sharedMaterials)
            {
                if (m == null) continue;
                // Diagnostics: if the orange persists in the next CI shot,
                // this log says which material actually carries it.
                Debug.Log("[Village] Hut material: " + m.name + " baseColor=" +
                    (m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor").ToString("F2") : "?") +
                    " hasBaseMap=" + (m.mainTexture != null));
                if (m.name != "Material.002") continue;
                ToUrpLit(m);
                if (albedo != null) m.SetTexture("_BaseMap", albedo);
                m.SetTextureScale("_BaseMap", new Vector2(3f, 2f));
                m.SetColor("_BaseColor", new Color(0.88f, 0.82f, 0.70f));
                if (normal != null)
                {
                    m.SetTexture("_BumpMap", normal);
                    m.SetTextureScale("_BumpMap", new Vector2(3f, 2f));
                    m.SetFloat("_BumpScale", 1f);
                    m.EnableKeyword("_NORMALMAP");
                }
                if (metalSmooth != null)
                {
                    m.SetTexture("_MetallicGlossMap", metalSmooth);
                    m.SetTextureScale("_MetallicGlossMap", new Vector2(3f, 2f));
                    m.SetFloat("_Smoothness", 1f);
                    m.SetFloat("_SmoothnessTextureChannel", 0f);
                    m.EnableKeyword("_METALLICSPECGLOSSMAP");
                }
                if (ao != null)
                {
                    m.SetTexture("_OcclusionMap", ao);
                    m.SetTextureScale("_OcclusionMap", new Vector2(3f, 2f));
                    m.SetFloat("_OcclusionStrength", 1f);
                    m.EnableKeyword("_OCCLUSIONMAP");
                }
            }
        }
        Debug.Log("[Village] Hut walls re-textured (clay PBR) on " + holder.name);
    }

    // PHONE-VIDEO 2026-11-09 (fix #5): the stilt house (House_6,
    // indian_village_house_famous) renders black-and-white sketch-like
    // on the phone — white walls, near-black timber, no visible surface
    // detail. Verified in the .glb AND the R3c CI diag (not a loader
    // bug): ONE albedo-only Material.002, no normal/roughness/AO maps,
    // so the flat lighting response blows the white plaster out and
    // crushes the timber to black. The fix keeps the model's OWN texture
    // atlas (it carries the plaster/timber/thatch regions — replacing it
    // would brick the roof and stilts, which is what WeatherHutPlaster
    // does to House_5) and gives it the PBR response it lacks: URP Lit
    // + fine-grain normal + metal-smooth + a warm dim so the white stops
    // blowing out. House_5 is NOT touched here — its clay re-texture is
    // confirmed working (brown house in frame 37s, URP Lit in the diag).
    private static void FixStiltHouse(GameObject holder)
    {
        if (holder == null) return;
        Texture2D normal = Tex("brown_mud_dry_nor_gl_1k");
        Texture2D metalSmooth = Tex("brown_mud_dry_metal_smooth_1k");
        System.Collections.Generic.HashSet<Material> seen =
            new System.Collections.Generic.HashSet<Material>();
        int fixedCount = 0;
        foreach (Renderer r in holder.GetComponentsInChildren<Renderer>())
        {
            foreach (Material m in r.sharedMaterials)
            {
                if (m == null || !seen.Add(m)) continue;
                ToUrpLit(m);
                m.SetFloat("_Metallic", 0f);
                m.SetFloat("_Smoothness", 0.6f);
                // Warm dim: the white plaster blows out in the day sun.
                // 0.86/0.83/0.78 keeps the texture readable instead of
                // sketch-white; the timber stays timber, just not crushed.
                Color c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white;
                m.SetColor("_BaseColor", new Color(c.r * 0.86f, c.g * 0.83f, c.b * 0.78f, c.a));
                // Generic fine-grain PBR (same maps UpgradeRuntimeMat uses
                // for the plaster/wood/roof .mats): the model ships no
                // normal or metal-smooth map of its own.
                if (normal != null && (!m.HasProperty("_BumpMap") || m.GetTexture("_BumpMap") == null))
                {
                    m.SetTexture("_BumpMap", normal);
                    m.SetFloat("_BumpScale", 0.3f);
                    m.EnableKeyword("_NORMALMAP");
                }
                if (metalSmooth != null && (!m.HasProperty("_MetallicGlossMap") || m.GetTexture("_MetallicGlossMap") == null))
                {
                    m.SetTexture("_MetallicGlossMap", metalSmooth);
                    m.SetFloat("_Smoothness", 1f);
                    m.SetFloat("_SmoothnessTextureChannel", 0f);
                    m.EnableKeyword("_METALLICSPECGLOSSMAP");
                }
                Debug.Log("[Village] Stilt house material: " + m.name + " baseMap=" + (m.mainTexture != null)
                    + " baseColor=" + m.GetColor("_BaseColor").ToString("F2"));
                fixedCount++;
            }
        }
        Debug.Log("[Village] Stilt house fix applied to " + holder.name + " (" + fixedCount + " material(s))");
    }

    // ROUND 5 (final chance): the Bhumija shrine (AngkorTemple, NE jungle
    // edge, (21, 12)) renders as a bright silver-white jagged spire on
    // the phone AND in CI (village-east.png) — its single albedo-only
    // KTX2 texture is pale stone with no PBR response, so the day sun
    // blows it out to silver and it reads as a glitch, not a temple.
    // Same treatment as the stilt house: URP Lit + warm stone dim +
    // fine-grain normal, so it reads as old stone. If the CI shot still
    // shows it silver, the model must be REMOVED, not re-tinted again.
    private static void FixBhumija(GameObject holder)
    {
        if (holder == null) return;
        Texture2D normal = Tex("brown_mud_dry_nor_gl_1k");
        Texture2D metalSmooth = Tex("brown_mud_dry_metal_smooth_1k");
        System.Collections.Generic.HashSet<Material> seen =
            new System.Collections.Generic.HashSet<Material>();
        int fixedCount = 0;
        foreach (Renderer r in holder.GetComponentsInChildren<Renderer>())
        {
            foreach (Material m in r.sharedMaterials)
            {
                if (m == null || !seen.Add(m)) continue;
                ToUrpLit(m);
                m.SetFloat("_Metallic", 0f);
                m.SetFloat("_Smoothness", 0.6f);
                // Warm stone dim: the pale albedo blows out to silver-white
                // in the day sun. 0.62/0.58/0.52 keeps the carving readable
                // instead of a silver spike.
                Color c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white;
                m.SetColor("_BaseColor", new Color(c.r * 0.62f, c.g * 0.58f, c.b * 0.52f, c.a));
                if (normal != null && (!m.HasProperty("_BumpMap") || m.GetTexture("_BumpMap") == null))
                {
                    m.SetTexture("_BumpMap", normal);
                    m.SetFloat("_BumpScale", 0.3f);
                    m.EnableKeyword("_NORMALMAP");
                }
                if (metalSmooth != null && (!m.HasProperty("_MetallicGlossMap") || m.GetTexture("_MetallicGlossMap") == null))
                {
                    m.SetTexture("_MetallicGlossMap", metalSmooth);
                    m.SetFloat("_Smoothness", 1f);
                    m.SetFloat("_SmoothnessTextureChannel", 0f);
                    m.EnableKeyword("_METALLICSPECGLOSSMAP");
                }
                Debug.Log("[Village] Bhumija material: " + m.name + " baseMap=" + (m.mainTexture != null)
                    + " baseColor=" + m.GetColor("_BaseColor").ToString("F2"));
                fixedCount++;
            }
        }
        Debug.Log("[Village] Bhumija stone fix applied to " + holder.name + " (" + fixedCount + " material(s))");
    }

    // PHONE-VIDEO 2026-11-09 (fix #7): palm fronds and trunk render
    // dark/black on the phone while the CI render shows them green and
    // textured (size-palm.png). The R3c CI diag proves the materials are
    // NOT broken by the loader — they arrive as "Shader Graphs/glTF-
    // pbrMetallicRoughness", TEX_OK 1024x1024, white, metallicFactor 0 —
    // so this is a renderer difference, not a missing texture: the
    // Shader Graph's double-sided foliage lighting goes dark on the
    // phone GPU (backlit fronds read as black silhouettes). The fix
    // converts the palm to URP Lit — the engine's own mobile-proven
    // shader, the same treatment the banyan already has in the diag
    // (URP Lit, bright green in CI) — and restores the MASK alpha
    // cutout on the fronds (glTF alphaCutoff 0.32), which ToUrpLit alone
    // would drop. Only HP_Palm3_* materials are touched; mango/banyan/
    // chinaberry are left alone (diag-verified good).
    private static void FixPalmMaterials(GameObject holder)
    {
        if (holder == null) return;
        System.Collections.Generic.HashSet<Material> seen =
            new System.Collections.Generic.HashSet<Material>();
        int fixedCount = 0;
        foreach (Renderer r in holder.GetComponentsInChildren<Renderer>())
        {
            foreach (Material m in r.sharedMaterials)
            {
                if (m == null || !seen.Add(m)) continue;
                if (!m.name.StartsWith("HP_Palm3_")) continue;
                bool isLeaf = m.name.Contains("Leaf");
                ToUrpLit(m);
                m.SetFloat("_Metallic", 0f);
                m.SetFloat("_Smoothness", 0.4f); // glTF roughnessFactor 0.6
                m.SetFloat("_Cull", 0f); // double-sided (fronds are single quads)
                if (isLeaf)
                {
                    // Restore the MASK cutout (glTF alphaCutoff 0.32 on
                    // both leaf materials) so the quads clip to fronds
                    // instead of rendering as solid dark cards.
                    float cutoff = 0.32f;
                    if (m.HasProperty("alphaCutoff")) cutoff = m.GetFloat("alphaCutoff");
                    m.SetFloat("_Surface", 0f);
                    m.SetFloat("_AlphaClip", 1f);
                    m.SetFloat("_Cutoff", cutoff);
                    m.SetFloat("_ZWrite", 1f);
                    m.renderQueue = 2450;
                    m.EnableKeyword("_ALPHATEST_ON");
                    m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    m.SetOverrideTag("RenderType", "Opaque");
                }
                else
                {
                    m.SetFloat("_Surface", 0f);
                    m.SetFloat("_AlphaClip", 0f);
                    m.DisableKeyword("_ALPHATEST_ON");
                    m.renderQueue = 2000;
                }
                Debug.Log("[Village] Palm material: " + m.name + " baseMap=" + (m.mainTexture != null)
                    + " baseColor=" + m.GetColor("_BaseColor").ToString("F2") + (isLeaf ? " MASK cutout" : " opaque"));
                fixedCount++;
            }
        }
        Debug.Log("[Village] Palm material fix applied to " + holder.name + " (" + fixedCount + " material(s))");
    }

    // PILOT: the Pujari renders as a flat white sheet because his biggest
    // mesh (Cylinder_Material.001, ~6k tris — the dhoti/lower body) is an
    // UNTEXTURED flat [0.8,0.8,0.8] grey, and the small orange cylinder
    // (Material.002) is flat saturated orange. Verified in the .glb, not
    // a loader bug: only 3 of his 9 materials have textures (face,
    // outfit top, one more). The dhoti gets a warm cream tint + fine
    // normal grain so it reads as cloth instead of a white sheet; the
    // orange pot is muted to terracotta. Face / textured mats untouched.
    private static void FixPujari(GameObject holder)
    {
        if (holder == null) return;
        Texture2D normal = Tex("brown_mud_dry_nor_gl_1k");
        System.Collections.Generic.HashSet<Material> seen =
            new System.Collections.Generic.HashSet<Material>();
        foreach (Renderer r in holder.GetComponentsInChildren<Renderer>())
        {
            // ROUND 5b: the two static cylinders (Cylinder_Material.001
            // dhoti + Cylinder_Material.002 orange pot) are ~2.4 m
            // diameter tubes spanning Z 0.39..2.62 in model space — they
            // swallow the whole Wolf3D avatar. Only the black hair tuft
            // (top) and black footwear (bottom) poke out, so he reads as
            // a blank beige mannequin in the photos. Tinting the tube
            // (the old fix) only changed its colour. Hide both meshes;
            // the avatar underneath (textured face / outfit top, hair,
            // body, footwear) renders normally without them.
            // ROUND 5b FIX 1b: the first attempt matched on the mesh name
            // (StartsWith "Cylinder_") and hid NOTHING — the CI log shows
            // zero "cylinder hidden" lines and pujari.png still shows the
            // capsule, so glTFast evidently renames runtime meshes (or
            // the cylinders are skinned). Match on the MATERIAL names
            // instead: Material.001 and Material.002 exist ONLY on the
            // two cylinders (verified in the .glb), and material names
            // are proven preserved at runtime (tractor / Bhumija / palm
            // fixes all match on them). Mesh-name check stays as an OR.
            // Diagnostic line per renderer so the CI log proves the
            // runtime mesh/material names.
            Mesh mesh = null;
            if (r is SkinnedMeshRenderer smr) mesh = smr.sharedMesh;
            else
            {
                MeshFilter mf = r.GetComponent<MeshFilter>();
                if (mf != null) mesh = mf.sharedMesh;
            }
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                sb.Append("[Village] Pujari renderer mesh=");
                sb.Append(mesh != null ? mesh.name : "(none)");
                sb.Append(" mats=");
                foreach (Material mm in r.sharedMaterials) { if (mm != null) { sb.Append(mm.name); sb.Append(","); } }
                Debug.Log(sb.ToString());
            }
            bool isCylinder = mesh != null && mesh.name.StartsWith("Cylinder_");
            if (!isCylinder)
            {
                foreach (Material m in r.sharedMaterials)
                {
                    if (m != null && (m.name == "Material.001" || m.name == "Material.002"))
                    {
                        isCylinder = true;
                        break;
                    }
                }
            }
            if (isCylinder)
            {
                r.enabled = false;
                Debug.Log("[Village] Pujari cylinder hidden: " + (mesh != null ? mesh.name : "?"));
                continue;
            }
            foreach (Material m in r.sharedMaterials)
            {
                if (m == null || !seen.Add(m)) continue;
                if (m.name == "Material.001")
                {
                    ToUrpLit(m);
                    // PILOT ROUND 2: the first tint (0.78,0.72,0.60) was
                    // still pale enough to read as a white sheet at
                    // distance — deepened to a real dhoti cream/tan.
                    m.SetColor("_BaseColor", new Color(0.60f, 0.53f, 0.42f));
                    m.SetFloat("_Smoothness", 0.55f);
                    if (normal != null)
                    {
                        m.SetTexture("_BumpMap", normal);
                        m.SetFloat("_BumpScale", 0.35f);
                        m.EnableKeyword("_NORMALMAP");
                    }
                }
                else if (m.name == "Material.002")
                {
                    ToUrpLit(m);
                    m.SetColor("_BaseColor", new Color(0.55f, 0.30f, 0.12f));
                    m.SetFloat("_Smoothness", 0.5f);
                }
            }
        }
        Debug.Log("[Village] Pujari cloth fix applied");
    }

    // OVERNIGHT R1b: the 0.62/0.55/0.48 mute did NOT dent the red in the
    // R1 CI shot (the barrel still reads toy-red at the fence). Two
    // changes: (a) stronger factor, toward grey-brown; (b) apply to ALL
    // materials on the holder, not just "oil_barell" — and log every
    // material name found, so the CI log proves which material the
    // loaded model actually carries (glTFast may rename it).
    private static void MuteOilBarrel(GameObject holder)
    {
        if (holder == null) return;
        Texture2D normal = Tex("brown_mud_dry_nor_gl_1k");
        foreach (Renderer r in holder.GetComponentsInChildren<Renderer>())
        {
            foreach (Material m in r.sharedMaterials)
            {
                if (m == null) continue;
                Debug.Log("[Village] OilBarrel material: " + m.name);
                ToUrpLit(m);
                Color c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white;
                m.SetColor("_BaseColor", new Color(c.r * 0.5f, c.g * 0.45f, c.b * 0.42f, c.a));
                m.SetFloat("_Smoothness", 0.55f);
                if (normal != null)
                {
                    m.SetTexture("_BumpMap", normal);
                    m.SetFloat("_BumpScale", 0.3f);
                    m.EnableKeyword("_NORMALMAP");
                }
            }
        }
        Debug.Log("[Village] Oil barrel muted");
    }

    // ROUND 4 (ground): the Matka (red-clay water pot) renders bright
    // saturated red-orange on the phone (frames 22s/25s) — its single
    // material carries the red entirely in its base texture and no fix
    // ever touched it. Mirrors MuteOilBarrel with the stronger
    // terracotta multiplier (0.55/0.30/0.12, the values FixPujari uses
    // for its orange pot) — the mild 0.78/0.74/0.68 of
    // MuteOrangePlaster did NOT dent the red (R1b lesson). The pot's
    // material is unique to this .glb, so mutating sharedMaterials is
    // safe here (unlike the Dock fence, below).
    private static void MuteMatka(GameObject holder)
    {
        if (holder == null) return;
        Texture2D normal = Tex("brown_mud_dry_nor_gl_1k");
        foreach (Renderer r in holder.GetComponentsInChildren<Renderer>())
        {
            foreach (Material m in r.sharedMaterials)
            {
                if (m == null) continue;
                ToUrpLit(m);
                Color c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white;
                m.SetColor("_BaseColor", new Color(c.r * 0.55f, c.g * 0.30f, c.b * 0.12f, c.a));
                m.SetFloat("_Smoothness", 0.5f);
                if (normal != null)
                {
                    m.SetTexture("_BumpMap", normal);
                    m.SetFloat("_BumpScale", 0.3f);
                    m.EnableKeyword("_NORMALMAP");
                }
            }
        }
        Debug.Log("[Village] Matka muted to terracotta");
    }

    // OVERNIGHT R1: Mukhiya (Meshy, 1 textured material) reads as a flat
    // white sheet at distance — the pale texture blows out in the day sun.
    // A mild warm tint + grain knocks the blow-out down. 0.88 is
    // deliberately gentle: his texture carries the face detail and must
    // not be re-coloured, only dimmed.
    private static void MuteMukhiyaWhite(GameObject holder)
    {
        if (holder == null) return;
        Texture2D normal = Tex("brown_mud_dry_nor_gl_1k");
        System.Collections.Generic.HashSet<Material> seen =
            new System.Collections.Generic.HashSet<Material>();
        foreach (Renderer r in holder.GetComponentsInChildren<Renderer>())
        {
            foreach (Material m in r.sharedMaterials)
            {
                if (m == null || !seen.Add(m)) continue;
                ToUrpLit(m);
                Color c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white;
                m.SetColor("_BaseColor", new Color(c.r * 0.88f, c.g * 0.86f, c.b * 0.84f, c.a));
                m.SetFloat("_Smoothness", 0.6f);
                if (normal != null)
                {
                    m.SetTexture("_BumpMap", normal);
                    m.SetFloat("_BumpScale", 0.25f);
                    m.EnableKeyword("_NORMALMAP");
                }
            }
        }
        Debug.Log("[Village] Mukhiya white muted");
    }

    // OVERNIGHT R2: attach a day/night routine to a loaded NPC.
    private static void AddNpcRoutine(GameObject g, Vector3 home, Vector3[] waypoints, float walkSpeed, bool idleOnly)
    {
        if (g == null) return;
        NpcRoutine r = g.AddComponent<NpcRoutine>();
        r.home = home;
        r.waypoints = waypoints;
        r.walkSpeed = walkSpeed;
        r.idleOnly = idleOnly;
    }

    // OVERNIGHT R2: attach roaming to a loaded animal (wolf starts on
    // its idle_loop clip; AnimalRoam switches to walk_loop while moving).
    private static void AddAnimalRoam(GameObject g, Vector3 home, float radius, float speed, bool isWolf)
    {
        if (g == null) return;
        AnimalRoam a = g.AddComponent<AnimalRoam>();
        a.home = home;
        a.roamRadius = radius;
        a.walkSpeed = speed;
        a.isWolf = isWolf;
        if (isWolf) GltfModelLoader.PlayEmbeddedAnimation(g, "idle_loop");
    }

    // Bullock cart: 2 materials, zero textures. Weathered wood PBR from
    // Poly Haven bark (albedo) + mud normal/metal-smooth (bark's own
    // normal/roughness maps were never downloaded — mud's fine-grain
    // normal is the neutral rough-surface substitute).
    private static void ApplyWoodPbr(GameObject holder)
    {
        if (holder == null) return;
        Texture2D albedo = Tex("bark_brown_02_diff_1k");
        Texture2D normal = Tex("brown_mud_dry_nor_gl_1k");
        Texture2D metalSmooth = Tex("brown_mud_dry_metal_smooth_1k");
        System.Collections.Generic.HashSet<Material> seen =
            new System.Collections.Generic.HashSet<Material>();
        foreach (Renderer r in holder.GetComponentsInChildren<Renderer>())
        {
            foreach (Material m in r.sharedMaterials)
            {
                if (m == null || !seen.Add(m)) continue;
                ToUrpLit(m);
                if (albedo != null) m.SetTexture("_BaseMap", albedo);
                m.SetColor("_BaseColor", Color.white);
                if (normal != null) m.SetTexture("_BumpMap", normal);
                m.SetFloat("_BumpScale", 0.6f);
                if (metalSmooth != null) m.SetTexture("_MetallicGlossMap", metalSmooth);
            }
        }
        Debug.Log("[Village] Bullock cart wood PBR applied to " + seen.Count + " materials");
    }

    // ROUND 4 (ground): the Dock's fence (FenceH/FenceV meshes) renders
    // with bright-blue triangular patches on the phone (frames
    // 10s/12s/15s). Both fence meshes share material VoodoHut_Stairs,
    // whose authored texture atlas has a blue-painted region where the
    // fence UVs land — a _BaseColor tint cannot fix a saturated texture
    // (it only darkens the blue), the _BaseMap must be replaced. The
    // material is ALSO used by the Dock's stairs and stilt bases, so it
    // is CLONED per fence renderer (mutating sharedMaterials would
    // re-texture the stairs too). Matched by renderer name, since the
    // material name is shared.
    private static void FixDockFence(GameObject holder)
    {
        if (holder == null) return;
        Texture2D albedo = Tex("bark_brown_02_diff_1k");
        Texture2D normal = Tex("brown_mud_dry_nor_gl_1k");
        Texture2D metalSmooth = Tex("brown_mud_dry_metal_smooth_1k");
        int fixedCount = 0;
        foreach (Renderer r in holder.GetComponentsInChildren<Renderer>())
        {
            if (!r.gameObject.name.Contains("Fence")) continue;
            Material[] shared = r.sharedMaterials;
            Material[] cloned = new Material[shared.Length];
            for (int i = 0; i < shared.Length; i++)
            {
                Material m = shared[i];
                if (m == null) { cloned[i] = null; continue; }
                Material c = new Material(m);
                ToUrpLit(c);
                if (albedo != null) c.SetTexture("_BaseMap", albedo);
                c.SetColor("_BaseColor", Color.white);
                if (normal != null) c.SetTexture("_BumpMap", normal);
                c.SetFloat("_BumpScale", 0.6f);
                if (metalSmooth != null) c.SetTexture("_MetallicGlossMap", metalSmooth);
                cloned[i] = c;
            }
            r.materials = cloned;
            fixedCount++;
        }
        Debug.Log("[Village] Dock fence re-textured (wood PBR, cloned materials) on " + fixedCount + " renderer(s)");
    }

    // Tractor: 12 flat-colour materials, zero textures. PBR per material
    // name — original flat colours are KEPT (they are the right colours),
    // only surface response is added: a neutral normal map to break the
    // flat shading + per-part metallic/smoothness.
    private static void ApplyTractorPbr(GameObject holder)
    {
        if (holder == null) return;
        Texture2D normal = Tex("brown_mud_dry_nor_gl_1k");
        System.Collections.Generic.HashSet<Material> seen =
            new System.Collections.Generic.HashSet<Material>();
        foreach (Renderer r in holder.GetComponentsInChildren<Renderer>())
        {
            foreach (Material m in r.sharedMaterials)
            {
                if (m == null || !seen.Add(m)) continue;
                ToUrpLit(m);
                string name = m.name;
                float metallic = 0f, smooth = 0.4f, bump = 0.3f;
                switch (name)
                {
                    case "TireBlack":
                        smooth = 0.12f; bump = 0.8f; break;   // rubber
                    case "Chrome":
                        metallic = 1f; smooth = 0.9f; bump = 0f; break;
                    case "Glass":
                        smooth = 0.95f; bump = 0f; break;
                    case "HoodTexture":
                    case "WheelBlue":
                        // ROUND 5 (final chance): ROOT CAUSE of "the fix
                        // did not take" — the old code only added PBR
                        // response and KEPT the authored flat colours, and
                        // the authored colours ARE the defect: HoodTexture
                        // [0.012, 0.105, 0.549] and WheelBlue
                        // [0.031, 0.119, 0.411] are saturated blue
                        // (verified in the .glb). On the phone that reads
                        // as a flat bright-blue toy tractor. The blue is
                        // replaced with a muted Mahindra red, rough
                        // painted metal. WheelWhite stays white.
                        m.SetColor("_BaseColor", new Color(0.42f, 0.10f, 0.08f));
                        smooth = 0.35f; bump = 0.25f; break;
                    case "WheelWhite":
                        smooth = 0.55f; bump = 0.25f; break;  // painted metal
                    case "TurnSignal":
                        m.SetColor("_EmissionColor", new Color(1f, 0.45f, 0.05f));
                        m.EnableKeyword("_EMISSION");
                        smooth = 0.5f; break;
                    case "StopSignal":
                        m.SetColor("_EmissionColor", new Color(0.8f, 0.05f, 0.02f));
                        m.EnableKeyword("_EMISSION");
                        smooth = 0.5f; break;
                    default:
                        smooth = 0.35f; break;                 // black/grey parts
                }
                if (normal != null && bump > 0f)
                {
                    m.SetTexture("_BumpMap", normal);
                    m.SetFloat("_BumpScale", bump);
                }
                m.SetFloat("_Metallic", metallic);
                m.SetFloat("_Smoothness", smooth);
                // ROUND 5: log the resulting colour per material, so the
                // CI log proves the blue is gone (the old fix logged only
                // a count, which is why the blue survived unnoticed).
                Debug.Log("[Village] Tractor material: " + name + " baseColor="
                    + (m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor").ToString("F2") : "(none)"));
            }
        }
        Debug.Log("[Village] Tractor PBR applied to " + seen.Count + " materials");
    }

    // Ambassador: full PBR but showroom-clean. Weather it with mud detail
    // maps (grime over the paint) + matte smoothness, so it reads as the
    // broken-down car.
    // PILOT: the old weathering multiplied the base colour x2.2 — that blew
    // the texture's colours out, so the wheels read bright YELLOW and the
    // doors vivid RED (toy car). The paint lives in the TEXTURE, so the
    // multiplier is gone (base colour back to 1.0) and the mud detail stays
    // subtle. The wheels share the body's ONE material (blinn1 — verified
    // in the .glb: all 4 wheel meshes + body use blinn1), so wheels cannot
    // be fixed by material name: the 4 wheel meshes get their own
    // dark-rubber material by MESH name instead. If the doors still read
    // red in the CI render, the red is painted in the body texture itself
    // and the car must be REMOVED (replacement model needed) — code cannot
    // repaint one part of a shared texture.
    private static void WeatherAmbassador(GameObject holder)
    {
        if (holder == null) return;
        Texture2D mudAlbedo = Tex("brown_mud_dry_diff_1k");
        Texture2D mudNormal = Tex("brown_mud_dry_nor_gl_1k");
        System.Collections.Generic.HashSet<Material> seen =
            new System.Collections.Generic.HashSet<Material>();
        foreach (Renderer r in holder.GetComponentsInChildren<Renderer>())
        {
            foreach (Material m in r.sharedMaterials)
            {
                if (m == null || !seen.Add(m)) continue;
                ToUrpLit(m);
                // blinn1 is the body paint (+ wheels, same material);
                // blinn2 is the glass/chrome and is left alone so the
                // windows stay dark.
                bool isBody = m.name.StartsWith("blinn1");
                m.SetFloat("_Smoothness", isBody ? 0.45f : 0.9f);
                if (isBody)
                {
                    // PILOT: base colour back to 1.0 — the old 2.2x
                    // multiplier blew the yellow wheels / red doors out.
                    m.SetColor("_BaseColor", Color.white);
                    if (mudAlbedo != null)
                    {
                        m.SetTexture("_DetailMap", mudAlbedo);
                        m.SetFloat("_DetailAlbedoMapScale", 0.05f);
                        m.EnableKeyword("_DETAIL_MAP");
                    }
                    if (mudNormal != null)
                    {
                        m.SetTexture("_DetailNormalMap", mudNormal);
                        m.SetFloat("_DetailNormalMapScale", 0.25f);
                    }
                }
            }
        }
        // PILOT: wheels share blinn1 with the body — override them by
        // MESH name with real dark rubber.
        Material wheelMat = null;
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit != null)
        {
            wheelMat = new Material(lit);
            wheelMat.name = "AmbassadorWheelRubber";
            wheelMat.SetColor("_BaseColor", new Color(0.05f, 0.05f, 0.06f));
            wheelMat.SetFloat("_Metallic", 0f);
            wheelMat.SetFloat("_Smoothness", 0.15f);
            Texture2D mudN = Tex("brown_mud_dry_nor_gl_1k");
            if (mudN != null)
            {
                wheelMat.SetTexture("_BumpMap", mudN);
                wheelMat.SetFloat("_BumpScale", 0.5f);
                wheelMat.EnableKeyword("_NORMALMAP");
            }
        }
        int wheels = 0;
        if (wheelMat != null)
        {
            foreach (Renderer r in holder.GetComponentsInChildren<Renderer>())
            {
                if (r.gameObject.name.ToLower().Contains("wheel"))
                {
                    r.sharedMaterial = wheelMat;
                    wheels++;
                }
            }
        }
        Debug.Log("[Village] Ambassador weathering applied to " + seen.Count + " materials, wheels overridden: " + wheels);
    }

    // ------------------------------------------------------------------
    // Phase 4: temple + new real models.
    // ------------------------------------------------------------------
    // Fix-push P2-14: the temple is the REAL hindu_temple .glb now (loaded
    // once in BuildNewModelsAsync at the temple spot (2, -17)). The old
    // primitive temple shell (BuildTemple) was REMOVED — the real model
    // carries its own walls/roof and gets a FullBounds collider, so the
    // player cannot walk through it. The shivling stays OUT (defective).
    // ------------------------------------------------------------------

    // ROUND 5c (feet clipping): the temple's stepped plinth must be
    // walkable. The step profile is derived from the ACTUAL rendered
    // plinth mesh (Object_25), so the registered tops match the visible
    // geometry exactly regardless of the .glb normalisation scale: the
    // mesh vertices are transformed to world space, grouped by height,
    // and each height level becomes a nested walkable square (centre =
    // temple position, half-width = widest vertex at that height). The
    // shrine itself stays blocked (lower + middle AABB boxes) so the
    // player walks the plinth but not through the shrine walls. The
    // gateway pillars are NOT blocked (their positions are not
    // derivable from the .glb bounds) — minor known gap: the player
    // can walk through a gateway pillar. The temple platform top is
    // registered as walkable too (its ring is narrow and mostly blocked
    // by the shrine boxes, so the player rarely reaches it, but the
    // ground height there is correct).
    // Fallback (Object_25 not found, e.g. renamed): the tier profile
    // derived from the .glb on 2026-11-10 (ByHeight 10, 12 tiers) —
    // tops 0.343/0.427/0.512/0.596/0.681/0.766/0.850/0.935/1.019/1.104/
    // 1.189/1.273 m, half-widths 4.949 down to 2.771 m. Approximate;
    // the runtime mesh path is authoritative.
    private static void RegisterTempleWalkables(GameObject temple)
    {
        if (temple == null) return;
        Vector3 tp = temple.transform.position; // (2, 0, -17)
        bool foundPlinth = false;

        Transform plinth = FindChildRecursive(temple.transform, "Object_25");
        if (plinth != null)
        {
            Mesh mesh = null;
            Renderer plinthR = plinth.GetComponent<Renderer>();
            if (plinthR is MeshRenderer)
            {
                MeshFilter mf = plinth.GetComponent<MeshFilter>();
                if (mf != null) mesh = mf.sharedMesh;
            }
            else if (plinthR is SkinnedMeshRenderer)
            {
                mesh = ((SkinnedMeshRenderer)plinthR).sharedMesh;
            }
            if (mesh != null && plinthR != null)
            {
                // Group world-space vertices by height (2 cm buckets —
                // the tiers are ~8.5 cm apart, so no two tiers merge)
                // and take the widest vertex at each height as the
                // tier's half-width (Chebyshev from the plinth centre).
                Matrix4x4 m = plinthR.localToWorldMatrix;
                var levels = new Dictionary<int, float>();
                foreach (Vector3 v in mesh.vertices)
                {
                    Vector3 w = m.MultiplyPoint3x4(v);
                    if (w.y < 0.02f) continue; // ground level, not a step
                    int key = Mathf.RoundToInt(w.y / 0.02f);
                    float half = Mathf.Max(Mathf.Abs(w.x - tp.x), Mathf.Abs(w.z - tp.z));
                    float cur;
                    if (!levels.TryGetValue(key, out cur) || half > cur)
                        levels[key] = half;
                }
                foreach (var kv in levels)
                {
                    float top = kv.Key * 0.02f;
                    if (top < 0.05f || kv.Value < 0.5f) continue;
                    VillageColliders.RegisterWalkable(tp.x, tp.z, kv.Value, top);
                }
                foundPlinth = true;
                Debug.Log("[Village] Temple plinth: registered " + levels.Count
                    + " walkable tiers from Object_25 mesh");
            }
        }
        if (!foundPlinth)
        {
            Debug.LogWarning("[Village] Temple plinth Object_25 not found — "
                + "using hardcoded tier profile (approximate)");
            float[] tops = { 0.343f, 0.427f, 0.512f, 0.596f, 0.681f, 0.766f,
                0.850f, 0.935f, 1.019f, 1.104f, 1.189f, 1.273f };
            float[] halves = { 4.949f, 4.750f, 4.552f, 4.354f, 4.156f, 3.958f,
                3.760f, 3.563f, 3.365f, 3.167f, 2.969f, 2.771f };
            for (int i = 0; i < tops.Length; i++)
                VillageColliders.RegisterWalkable(tp.x, tp.z, halves[i], tops[i]);
        }

        // Temple platform top (Object_27): walkable square from its
        // rendered bounds.
        RegisterWalkableFromBounds(temple, "Object_27");
        // Shrine blocking boxes (from rendered bounds; lower extended
        // up so the player can never jump over the walls, upper kept
        // at its real height so the player can walk under its overhang
        // on the platform ring).
        RegisterBlockFromBounds(temple, "Object_32", 10f); // lower walls
        RegisterBlockFromBounds(temple, "Object_30", 10f); // middle tower
        RegisterBlockFromBounds(temple, "Object_34", 0f);  // upper block
        // ROUND 6 (aar-paar): the gateway bell tower north of the
        // plinth (Object_39 pillar_bell + the hanging bell Object_49).
        // Measured from hindu_temple_88_62er.glb: the tower stands at
        // x 0.31..3.99, z -11.19..-9.9, 0..6.59 m — a solid bell
        // pavilion the player must walk AROUND, not through. One block
        // from the base part extended to the tower top (the base is
        // the widest part) plus the bell itself (y 1.74..3.09 — the
        // player is 1.8 m tall, so he cannot walk under it). The
        // plinth steps stay open: the tower stands 0.86 m north of
        // the plinth's north edge (-12.05) and the steps span the
        // plinth's full width, so the player reaches them around
        // either side.
        RegisterBlockFromBounds(temple, "Object_39", 6.6f);
        RegisterBlockFromBounds(temple, "Object_49", 0f);
    }

    private static void RegisterWalkableFromBounds(GameObject root, string childName)
    {
        Transform t = FindChildRecursive(root.transform, childName);
        if (t == null) return;
        Bounds b = GetChildBounds(t);
        if (b.size.x < 0.1f || b.size.z < 0.1f) return;
        VillageColliders.RegisterWalkable(b.center.x, b.center.z,
            Mathf.Max(b.size.x, b.size.z) / 2f, b.max.y);
        Debug.Log("[Village] Walkable top: " + childName + " top "
            + b.max.y.ToString("F2") + " m, half "
            + (Mathf.Max(b.size.x, b.size.z) / 2f).ToString("F2") + " m");
    }

    private static void RegisterBlockFromBounds(GameObject root, string childName, float extendUpTo)
    {
        Transform t = FindChildRecursive(root.transform, childName);
        if (t == null) return;
        Bounds b = GetChildBounds(t);
        if (b.size.x < 0.1f || b.size.y < 0.1f || b.size.z < 0.1f) return;
        if (extendUpTo > b.max.y)
            b.max = new Vector3(b.max.x, extendUpTo, b.max.z);
        VillageColliders.Register(b);
        Debug.Log("[Village] Shrine block: " + childName + " " + b.ToString("F2"));
    }

    private static Transform FindChildRecursive(Transform parent, string name)
    {
        foreach (Transform c in parent)
        {
            if (c.name == name) return c;
            Transform found = FindChildRecursive(c, name);
            if (found != null) return found;
        }
        return null;
    }

    private static Bounds GetChildBounds(Transform t)
    {
        Renderer[] rs = t.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(t.position, Vector3.zero);
        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    // Phase 4: every new real model. Each load is independent — one
    // failure is logged and skipped, never fatal. .gltf bundles (soil,
    // forest floor, lanterns) load from their subfolders under
    // StreamingAssets/Models/ via the same glTFast path as .glb.
    private System.Collections.IEnumerator BuildNewModelsAsync()
    {
        // Fix-push P2-14: REAL temple (hindu_temple .glb, once only) at the
        // temple spot (2, -17) — REPLACES the primitive shell (removed
        // above). NOTE: model yaw is unverified — check the CI screenshot
        // that the entrance faces north (chowk).
        // Phase 1 real-size table: a temple with shikhar is 10-14 m tall.
        // The old ByWidth 7 made it 5.1 m tall — barely 3x the hero, so
        // Nilesh looked as big as the temple. ByHeight 10 (low end of
        // the range — the model is wider than tall, so 10 m already
        // spans 13.7 m wide).
        // ROUND 5c (feet clipping): was ColliderMode.FullBounds — the
        // whole 10 m temple (including its stepped plinth) was one
        // blocking box, so the player could never step onto the plinth
        // at all, and the feet-sink bug was invisible to the collider
        // system. Now None: RegisterTempleWalkables registers the
        // shrine as blocking boxes and the plinth tiers as walkable
        // tops, so the player walks UP the steps with his feet on the
        // step surface.
        yield return LoadProp("hindu_temple_88_62er.glb", "Temple",
            new Vector3(2f, 0f, -17f), 0f, 10f,
            GltfModelLoader.NormalizeMode.ByHeight, GltfModelLoader.ColliderMode.None,
            g => RegisterTempleWalkables(g));
        // School landmark (south-west, clear of House_3 and the jungle ring).
        // Phase 1: a pakka school is 5 m tall (the old ByWidth 7 made it
        // 2.6 m — shorter than two Nileshes stacked).
        yield return LoadProp("school-building_74_yizq.glb", "School",
            new Vector3(-16.5f, 0f, -14f), 25f, 5f,
            GltfModelLoader.NormalizeMode.ByHeight, GltfModelLoader.ColliderMode.FullBounds);
        // HAVELI (Meshy, free tier) — the big missing landmark, east of
        // the chowk facing it. Set back from the jungle ring so it only
        // brushes one ring tree. Fix-push P2-17: moved 1 m west (17 -> 16).
        // Phase 1: a 2-storey haveli is 8 m tall (was ByWidth 8 -> 9.1 m).
        yield return LoadProp("meshy_haveli_86_hvl.glb", "Haveli",
            new Vector3(16f, 0f, -3.5f), -90f, 8f,
            GltfModelLoader.NormalizeMode.ByHeight, GltfModelLoader.ColliderMode.FullBounds);
        // Shivling (cheap-wins 2026-11-07): the OLD 58 MB file was
        // defective (5 of 15 embedded textures zeroed) and is gone for
        // good — this is the NEW clean Mahadev (5.9 MB, 213k tris,
        // 1 texture, verified valid glTF v2), a seated Shiva murti.
        // Placed ON the shrine platform just inside the front: the
        // shrine's solid interior block scales with the temple, so at
        // the new 10 m temple the murti sits at z=-15 (was -16), still
        // just south of the block's south face.
        yield return LoadProp("mahadev_001__1__2_t4b6.glb", "Shivling",
            new Vector3(2f, 0f, -15f), 0f, 1.0f,
            GltfModelLoader.NormalizeMode.ByHeight, GltfModelLoader.ColliderMode.None);
        // NANDI (cheap-wins 2026-11-07): user-sent granite Nandi,
        // decimated 441k -> 150k tris. Placed in the temple courtyard
        // NORTH of the gateway, facing the temple (yaw 180). Phase 1:
        // the temple front moved south to z=-10 when the temple grew
        // to 10 m, so the Nandi moved with it to (2, -8.8) — still in
        // the courtyard, facing the shrine, clear of the entrance.
        yield return LoadProp("granite_figure_of_a_nandi_bull_3_3z0y.glb", "Nandi",
            new Vector3(2f, 0f, -8.8f), 180f, 1.2f,
            GltfModelLoader.NormalizeMode.ByHeight, GltfModelLoader.ColliderMode.None);
        // ROUND 6 (aar-paar): the Nandi is a solid granite bull in the
        // courtyard — the player must walk around it, not through it.
        // Block from its own rendered bounds (None mode keeps the
        // load, the block is the collision).
        GameObject nandiGO = GameObject.Find("Nandi");
        if (nandiGO != null) VillageColliders.Register(GetWorldBounds(nandiGO));
        // Pujari at the temple entrance, facing the chowk.
        // Phase 1: moved with the temple front to (4.5, -8.5) — just
        // outside the entrance, still east of the Nandi-shivling
        // sightline (x=2), and clear of the temple's own footprint.
        // Lighting pass 2026-11-07: yaw 0 -> 180. The close-up proved he
        // faced NORTH (toward the temple) at yaw 0 — the camera south of
        // him saw his back. Yaw 180 turns him to face the chowk (+z).
        // NULL-material note (verified from the .glb, not a defect):
        // Wolf3D_Teeth is flat white [0.8,0.8,0.8] = teeth (correct, no
        // texture needed); Material.003 is flat black [0,0,0] = footwear
        // (correct); Material.001/002 are flat-colour prop cylinders.
        // The FACE (Material.007) has its skin texture — it renders
        // skin-toned, verified in the follow-up close-up.
        // Replacement 2026-11-07: Indian Man In Dhoti, 21.6k tris,
        // RIGGED (the old 141k-tri priest had a flat grey untextured
        // face). 5 of his 9 materials have albedo textures; the rest are
        // flat colours — CHECK the screenshot: if his face renders flat
        // grey, the replacement has NOT fixed the defect.
        // Cheap-wins 2026-11-07: moved 1.6 m east (2 -> 3.6) so he no
        // longer stands on the Nandi-shivling sightline — from the chowk
        // he was hiding the Nandi behind him (same x, 1 m apart).
        // (Position NOT changed by this replacement — x stays 3.6.)
        yield return LoadProp("indian_man_in_dhoti__1__2_kfur.glb", "Pujari",
            new Vector3(4.5f, 0f, -8.5f), 270f, 1.75f,
            GltfModelLoader.NormalizeMode.ByHeight, GltfModelLoader.ColliderMode.None,
            // OVERNIGHT R2: PilotNpcIdle replaced by the day/night routine
            // (same breathing, plus a small temple-courtyard wander by day
            // and home at night). The priest no longer stands frozen.
            g => { if (g != null) { FixPujari(g); AddNpcRoutine(g, new Vector3(4.5f, 0f, -8.5f), new Vector3[] { new Vector3(3.5f, 0f, -8.5f), new Vector3(6f, 0f, -7f), new Vector3(4.5f, 0f, -8.5f) }, 0.7f, false); } });
        // Fix pass 2026-11-08: yaw 180 faced EAST (south close-up
        // showed his face in profile pointing right) and yaw 90 faced
        // NORTH (south close-up showed only his back). Yaw 270 faces
        // SOUTH, toward the chowk, so his textured face is visible
        // from the village. PujariLight (south of him) now lights
        // his front.
        // He is rigged but has NO embedded animation clip — this call is
        // a no-op unless a clip exists; he stands in his bind pose.
        GltfModelLoader.PlayEmbeddedAnimations(GameObject.Find("Pujari"));
        // Second villager (rigged indian_man, sunglasses variant) near the handpump.
        yield return LoadProp("indian_man__sunglasses_76_gmed.glb", "Villager_Man",
            new Vector3(1.5f, 0f, 10.5f), -60f, 1.75f,
            GltfModelLoader.NormalizeMode.ByHeight, GltfModelLoader.ColliderMode.None,
            // OVERNIGHT R2: day routine — wanders the handpump / north
            // lane by day, home at night. (His embedded idle clip keeps playing.)
            // PHONE-VIDEO FIX 2026-11-09: was (6.5, 10.5) — INSIDE House_2
            // (11.5 m wide, x[3.22,14.78] z[2.23,13.77]). Now west of it.
            g => AddNpcRoutine(g, new Vector3(1.5f, 0f, 10.5f), new Vector3[] { new Vector3(1.5f, 0f, 10.5f), new Vector3(0f, 0f, 8f), new Vector3(2f, 0f, 12.5f), new Vector3(-1f, 0f, 12f) }, 0.8f, false));
        GltfModelLoader.PlayEmbeddedAnimations(GameObject.Find("Villager_Man"));
        // MEERA — second main character (rigged indian_woman_in_saree,
        // like Nilesh: static pose first, animation retargeting is a later
        // step, never a blocker). Stands on the chowk lane by Nilesh's
        // house, facing the player spawn / camera.
        yield return LoadProp("indian_woman_in_saree_84_8ssr.glb", "Meera",
            new Vector3(0.5f, 0f, 6.5f), 180f, 1.70f,
            GltfModelLoader.NormalizeMode.ByHeight, GltfModelLoader.ColliderMode.None,
            // OVERNIGHT R2: day routine — wanders the chowk / Nilesh's
            // house lane by day, home at night. (Her embedded idle clip keeps playing.)
            g => AddNpcRoutine(g, new Vector3(0.5f, 0f, 6.5f), new Vector3[] { new Vector3(0.5f, 0f, 6.5f), new Vector3(-2f, 0f, 4f), new Vector3(2f, 0f, 8f), new Vector3(-3f, 0f, 9f) }, 0.8f, false));
        GltfModelLoader.PlayEmbeddedAnimations(GameObject.Find("Meera"));
        // MUKHIYA (Meshy, free tier) — the old village headman. PILOT: moved
        // from the chowk (-3.5, 2.5) to his haveli (13.5, -3.5) — he is
        // UNRIGGED (no skeleton, no animation), so he must not stand in
        // the pilot area as a "living" NPC. By the haveli he reads as a
        // static elder at his own house, outside the pilot bounds.
        yield return LoadProp("meshy_mukhiya_87_mky.glb", "Mukhiya",
            new Vector3(13.5f, 0f, -3.5f), 45f, 1.75f,
            GltfModelLoader.NormalizeMode.ByHeight, GltfModelLoader.ColliderMode.None,
            // OVERNIGHT R2: the elder stays by his haveli (unrigged — he
            // cannot walk), but he is no longer a statue: breathing + he
            // turns to face a player who comes near.
            g => { MuteMukhiyaWhite(g); AddNpcRoutine(g, new Vector3(13.5f, 0f, -3.5f), null, 0f, true); });
        // Chickens (user-made on Meshy) — placed ONCE (fix-push P0-4b:
        // was 3 copies of the same 2.4M-tri file; the file itself is
        // replaced with a decimated version by another worker).
        // Phase 1: a chicken is 0.45 m tall (was ByWidth 0.6 -> ~0.67 m).
        // Phase 2 collision fix (audit O1, HIGH): the chicken stood
        // 0.5 m from the wolf — the wolf stood on/through it. Now
        // (-8.5, 9.5): 2.9 m from the wolf, 3.6 m from the matka, just
        // north of Nilesh's house wall. (Matches the new night
        // reference: chicken near the hero, left of the lane.)
        yield return LoadProp("chicken_73_eoja.glb", "Chicken_1",
            new Vector3(-8.5f, 0f, 9.5f), 40f, 0.45f,
            GltfModelLoader.NormalizeMode.ByHeight, GltfModelLoader.ColliderMode.None,
            g => AddAnimalRoam(g, new Vector3(-8.5f, 0f, 9.5f), 3f, 0.9f, false));
        // yield return LoadProp("chicken_73_eoja.glb", "Chicken_2",
        //     new Vector3(7f, 0f, 5.5f), -120f, 0.6f,
        //     GltfModelLoader.NormalizeMode.ByWidth, GltfModelLoader.ColliderMode.None);
        // yield return LoadProp("chicken_73_eoja.glb", "Chicken_3",
        //     new Vector3(11f, 0f, -4.5f), 200f, 0.6f,
        //     GltfModelLoader.NormalizeMode.ByWidth, GltfModelLoader.ColliderMode.None);
        // Small props: wicker basket by the well, wooden stand by Nilesh's door.
        yield return LoadProp("historic_miscellaneous_basket_wicker_old_03_xdwjadj_mid_51_y3o8.glb", "Basket",
            new Vector3(-3f, 0f, -1.5f), 30f, 0.7f,
            GltfModelLoader.NormalizeMode.ByWidth, GltfModelLoader.ColliderMode.FullBounds);
        yield return LoadProp("historic_miscellaneous_stand_wood_old_05_xeokah0_mid_50_69zs.glb", "WoodStand",
            new Vector3(-5.5f, 0f, 3.5f), -20f, 1.1f,
            GltfModelLoader.NormalizeMode.ByHeight, GltfModelLoader.ColliderMode.FullBounds);
        // ROUND 4 (ground): the three single-quad ground tiles are
        // DELETED — SandstoneGround, SoilGround and ForestFloor were
        // the same defect as the Round 3 oval patches: a flat texture
        // tile (one 1024 KTX2 stretched over a 5-8 m quad) with a hard
        // edge sitting on top of the macro-textured ground plane.
        // SoilGround was the big smooth tan slab in front of the houses
        // (phone frames 22s/25s/28s/30s). The ground underneath is
        // already the Round 3 macro mud (wet lanes + cobble + blotches
        // baked in, incl. spots at (-4, 12)), so deletion leaves
        // textured ground, not a hole.
        // (LoadProp calls removed — see git history for the originals.)

        // Shamshan (south-east corner): grave stone set + small grave.
        // Fix-push P2-17: moved 2 m east (17 -> 19 / 14.5 -> 16.5) so the
        // graves no longer overlap House_6's corner (~0.25 m before).
        // Phase 1: House_6 grew to 5.5 m tall (9 m wide), so the whole
        // cluster shifts ~0.5-1 m south-east again to stay clear of it.
        // Phase 2 collision fix (audit O8): the pyre stood 1.58 m from
        // the grave stones (half-sum ~2.65 m) and the fire place 0.3 m
        // from the small grave — the whole shamshan cluster overlapped
        // itself. Spread along the SE jungle edge: graves stay, pyre
        // goes east (2.7 m from the graves), fire goes west (2.7 m from
        // the small grave), small grave goes south-east (3.4 m from the
        // graves). All clear of House_6 and the ring trunks.
        yield return LoadProp("grave_stone_collection_64_7j05.glb", "GraveStones",
            new Vector3(19.5f, 0f, -18.5f), 0f, 3.5f,
            GltfModelLoader.NormalizeMode.ByWidth, GltfModelLoader.ColliderMode.FullBounds);
        // Fix-push P2-14: shamshan pyre (woodpile) + fire, by the graves.
        yield return LoadProp("woodpile_game_ready_asset_91_qdgo.glb", "PyreWood",
            new Vector3(22f, 0f, -19.5f), 20f, 1.8f,
            GltfModelLoader.NormalizeMode.ByWidth, GltfModelLoader.ColliderMode.FullBounds);
        yield return LoadProp("small_fire_place_90_23ou.glb", "ShamshanFire",
            new Vector3(15f, 0f, -19.5f), 0f, 1.5f,
            GltfModelLoader.NormalizeMode.ByWidth, GltfModelLoader.ColliderMode.FullBounds);
        // Phase B: BHUMIJA SHIKARA (replacement 2026-11-07) — a real
        // 12th-century Hoysala shrine scan (150k tris, albedo-only) as the
        // OLD ABANDONED temple at the north-east jungle edge, once only,
        // far from the chowk and the main hindu temple (2, -17). A place
        // nobody goes to. (User's FINAL choice after two flips; Old Ruined
        // Temple stays on disk as backup only — do NOT place it.)
        yield return LoadProp("Bhumija.glb", "AngkorTemple",
            new Vector3(21f, 0f, 12f), -90f, 9f,
            GltfModelLoader.NormalizeMode.ByHeight, GltfModelLoader.ColliderMode.FullBounds,
            FixBhumija);
        // Follow-up 2026-11-07: Abandoned hospital building (54,041 tris,
        // 21 meshes, 11 materials, ALL full PBR) replaces the decimated
        // kralls clinic (149,968 tris, 724 meshes / 60 materials) — same
        // spot at the west village edge facing the chowk. 21 vs 724 meshes
        // is a huge draw-call win.
        // Phase 1: the old ByWidth 6 made the 3-storey hospital 2.6 m
        // tall. ByHeight 6 (a long low block — the model is 3.5x wider
        // than tall) and moved 4.5 m east to (-13.5, -2) so its 14 m
        // length still fits inside the jungle ring.
        yield return LoadProp("abandoned_hospital_building_20_2516.glb", "Clinic",
            new Vector3(-13.5f, 0f, -2f), 90f, 6f,
            GltfModelLoader.NormalizeMode.ByHeight, GltfModelLoader.ColliderMode.FullBounds);
        // Wooden dock/pier at the north village edge (no river mesh yet —
        // it reads as the pier at the water's edge; river comes later).
        // PHONE-VIDEO FIX 2026-11-09: was ColliderMode.None — the player
        // walked straight through/under it and the camera entered the
        // slats (~21-24s in the phone video). Now a movement blocker.
        yield return LoadProp("dock_pier_78_n66e.glb", "Dock",
            new Vector3(-7f, 0f, 19f), 0f, 6f,
            GltfModelLoader.NormalizeMode.ByWidth, GltfModelLoader.ColliderMode.FullBounds,
            FixDockFence);
        // Lanterns: brass diya inside Nilesh's house, chowk lantern by the lamp.
        // Phase 1: the player's hut shrank to 4 m tall (z span 4.93-7.07),
        // so the house diya moved from z=4.5 to z=5.5 to stay inside it.
        // Phase 2 collision fix (audit O6): the house diya stood in the
        // bed/medicine cluster. Now the north-west corner (-10.8, 6.6):
        // 1.8 m from the bed, inside the hut (z span 4.93-7.07).
        yield return LoadProp("brass_diya_lantern.glb", "DiyaLantern",
            new Vector3(-10.8f, 0f, 6.6f), 0f, 0.45f,
            GltfModelLoader.NormalizeMode.ByHeight, GltfModelLoader.ColliderMode.None);
        yield return LoadProp("Lantern_01.glb", "ChowkLantern",
            new Vector3(3.2f, 0f, 2.2f), 0f, 0.9f,
            GltfModelLoader.NormalizeMode.ByHeight, GltfModelLoader.ColliderMode.None);
        // Neem substitute (PlantCatalog Realistic HD Chinaberry tree,
        // CC-BY, 221,798 tris): ONE instance only (cheap-wins 2026-11-07:
        // was 2 instances = 444k tris; the second one on the north lane
        // is removed — the tree itself is the best in the project, the
        // count was the cost). South of the chowk. Trunk-only collider
        // like the other real trees, so the canopy never walls off an area.
        // Phase 1: the temple grew to 10 m tall and its front now sits
        // at z=-10, so the tree moved from (4, -10) to (8, -4) — still
        // north of the chowk, clear of the temple's front face, and 8 m
        // tall like a real neem.
        yield return LoadProp("realistic_hd_chinaberry_tree_1330_83_1wiy.glb", "Tree_Neem1",
            new Vector3(8f, 0f, -4f), 0f, 8f,
            GltfModelLoader.NormalizeMode.ByHeight, GltfModelLoader.ColliderMode.Trunk);
        // FBX props (native Unity import, under Assets/Resources/):
        // fallen pine at the north-west jungle edge, small grave in the shamshan.
        RunStep("FallenPine", delegate
        {
            GameObject pine = InstantiateResourceModel("FallenPine/FallenPine", "FallenPine",
                new Vector3(-18f, 0f, 18f), 40f, 4f, false);
            if (pine == null) Debug.LogWarning("[Village] Fallen pine FBX not placed");
            // ROUND 6 (aar-paar): a fallen trunk is solid — the player
            // must walk around it, not through it.
            else VillageColliders.Register(GetWorldBounds(pine));
        });
        RunStep("SmallGrave", delegate
        {
            // SmallGrave FBX is a FLAT slab (raw 1.1 wide x 0.23 thick x 2.2
            // long) — normalise by WIDTH to a real 2.2 m grave length.
            // ByHeight (old: 1.2 m tall) scaled it to a 13 m wide slab.
            // Phase 2: moved with the rest of the shamshan cluster to
            // (18, -21.5), clear of the grown House_6 and of the fire
            // place's new spot (15, -19.5).
            GameObject grave = InstantiateResourceModel("SmallGrave/SmallGrave", "SmallGrave",
                new Vector3(18f, 0f, -21.5f), -30f, 2.2f, false);
            if (grave == null) Debug.LogWarning("[Village] Small grave FBX not placed");
            // ROUND 6 (aar-paar): a grave slab is solid ground furniture.
            else VillageColliders.Register(GetWorldBounds(grave));
        });
    }

    // Instantiates a natively-imported FBX (under Assets/Resources/) and
    // normalises it by rendered bounds, the same way GltfModelLoader
    // normalises .glb/.gltf models: uniform scale to the target size,
    // footprint centred on pos, base on the ground. Rotation is applied
    // AFTER normalising (a yawed footprint would break the centre shift).
    // Returns null on any failure — never throws.
    private GameObject InstantiateResourceModel(
        string resourcePath, string goName, Vector3 pos, float yaw,
        float targetSize, bool byHeight)
    {
        try
        {
            GameObject prefab = Resources.Load<GameObject>(resourcePath);
            if (prefab == null)
            {
                Debug.LogWarning("[Village] Resource model missing: " + resourcePath);
                return null;
            }
            GameObject go = Object.Instantiate(prefab, pos, Quaternion.identity, villageRoot);
            go.name = goName;
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                Debug.LogWarning("[Village] Resource model has no renderers: " + resourcePath);
                return go;
            }
            Bounds b = renderers[0].bounds;
            foreach (Renderer r in renderers)
            {
                b.Encapsulate(r.bounds);
            }
            float dim = byHeight ? b.size.y : Mathf.Max(b.size.x, b.size.z);
            if (dim > 0.001f)
            {
                float s = targetSize / dim;
                if (s > 0.0001f && s < 1000f)
                {
                    go.transform.localScale = Vector3.one * s;
                    renderers = go.GetComponentsInChildren<Renderer>();
                    b = renderers[0].bounds;
                    foreach (Renderer r in renderers)
                    {
                        b.Encapsulate(r.bounds);
                    }
                    Vector3 shift = new Vector3(pos.x - b.center.x, -b.min.y, pos.z - b.center.z);
                    GameObject content = new GameObject("ModelRoot");
                    content.transform.SetParent(go.transform, false);
                    Transform[] children = new Transform[go.transform.childCount];
                    int n = 0;
                    foreach (Transform child in go.transform)
                    {
                        if (child != content.transform)
                        {
                            children[n++] = child;
                        }
                    }
                    for (int i = 0; i < n; i++)
                    {
                        children[i].SetParent(content.transform, true);
                    }
                    content.transform.localPosition = shift / s;
                }
            }
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            Debug.Log("[Village] Resource model placed: " + resourcePath + " as " + goName);
            return go;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Village] Resource model failed: " + resourcePath + " — " + e.Message);
            return null;
        }
    }

    private void BuildMoon()
    {
        // Big pale moon low in the sky + soft glow. DayNightCycle fades it in at night.
        // Lighting pass 2026-11-07: bigger disc (8 m, was 6) + wider glow
        // (20 m, was 16) — the reference's big full moon is the night's
        // anchor, and the old moon read as a small dim ball.
        // Fix pass 2026-11-08: the moon is a bright pale disc with a
        // wide glow halo. The disc is the original emissive sphere
        // (which renders as a visible pale disc). Fog is DISABLED on
        // the disc (_Fog 0): the moon sits 57 m out, and the night
        // fog was swallowing its emission and turning it into a dark
        // disc. DayNightCycle.SetMoon drives the disc's emission
        // colour. (The crater texture could not be made to show in
        // this headless render — Unlit rendered it black, the quad
        // invisible — so the disc stays a pale emissive disc; the
        // texture file moon_surface.png remains in the project.)
        // Phase 2 (new references, 2026-11-08): the moon is the night's
        // anchor in BOTH reference images — a BIG full moon high in a
        // dark blue sky. The old 8 m disc at 57 m read small and dim.
        // Now 11 m at ~52 m (about 12 degrees across, like the
        // reference) with a wider halo. This is a position/size change,
        // not another material tweak: the disc already has fog disabled
        // (_Fog 0) and DayNightCycle drives its brightness.
        moonMaterial = MakeEmissive("Moon", new Color(0.85f, 0.88f, 0.95f), new Color(0.25f, 0.25f, 0.3f));
        moonMaterial.SetFloat("_Fog", 0f);
        moonMaterial.DisableKeyword("_FOG");
        GameObject moon = Sphere("Moon", new Vector3(-24f, 28f, -36f), new Vector3(11f, 11f, 11f), moonMaterial, villageRoot);
        Destroy(moon.GetComponent<Collider>());
        // OVERNIGHT R1: the disc starts HIDDEN — DayNightCycle.SetMoon
        // fades it in at night and hides it by day (audit 4.4: pale disc
        // in the day sky). Without this the moon is visible for the first
        // frames before the cycle's first ApplyDay runs.
        moonGO = moon;
        moon.SetActive(false);
        moonGlow = MakeGlowQuad("MoonGlow", new Vector3(-24f, 28f, -36f), 34f, new Color(0.7f, 0.75f, 0.95f, 0.55f));
    }

    // ------------------------------------------------------------------
    // Systems wiring
    // ------------------------------------------------------------------
    private void SetupSystems()
    {
        GameObject player = GameObject.Find("Player");
        PlayerController pc = (player != null) ? player.GetComponent<PlayerController>() : null;
        if (pc != null)
        {
            pc.playerCamera = mainCam;
        }

        // Phase 4: wide 70-degree FOV so the gameplay camera sees >= 3
        // houses from the chowk (run #19 failed at the default 60).
        if (mainCam != null)
        {
            mainCam.fieldOfView = 70f;
        }

        // Third-person follow camera (created early in Awake by
        // SetupCameraAndUIEarly — only add it here if it is missing, so
        // there is never a second CameraFollow on the camera).
        CameraFollow follow = mainCam.GetComponent<CameraFollow>();
        if (follow == null)
        {
            follow = mainCam.gameObject.AddComponent<CameraFollow>();
            follow.target = (player != null) ? player.transform : null;
        }

        // Torch (spotlight attached to the camera).
        TorchController torch = gameObject.AddComponent<TorchController>();

        // Day/night.
        DayNightCycle cycle = gameObject.AddComponent<DayNightCycle>();
        GameObject sunGO = GameObject.Find("Directional Light");
        if (sunGO != null)
        {
            cycle.sun = sunGO.GetComponent<Light>();
            // Hard shadows from the sun (was off — the scene looked flat).
            // Quality level is forced to shadows-on below, so this applies.
            cycle.sun.shadows = LightShadows.Soft;
            cycle.sun.shadowStrength = 0.9f;
            cycle.sun.shadowBias = 0.02f;
            cycle.sun.shadowNormalBias = 0.05f;
        }
        cycle.lampLight = lampLight;
        cycle.moonMaterial = moonMaterial;
        cycle.lampGlow = lampGlow;
        cycle.moonGlow = moonGlow;
        cycle.moonObject = moonGO;

        // Monster (tall dark figure, hidden until night).
        GameObject monsterGO = BuildMonster();
        MonsterKnock knock = gameObject.AddComponent<MonsterKnock>();
        knock.monster = monsterGO.transform;
        knock.door = playerDoor;
        knock.doorFront = doorFront;
        knock.camFollow = follow;
        knock.player = (player != null) ? player.transform : null;
        knock.playerHouseCenter = new Vector3(-9f, 0f, 6f);
        monster = monsterGO.transform;

        // Game manager + mobile controls.
        GameManager gm = gameObject.AddComponent<GameManager>();
        gm.player = pc;
        gm.playerDoor = playerDoor;
        // ROUND 4 DOOR: dawn survival uses the ACTUAL hut footprint, not
        // the old hardcoded ±3.5/±3.0 — a player standing inside near the
        // walls counted as outside at dawn.
        GameManager.PlayerHouseHalfExtents = new Vector3(
            playerHouseBounds.size.x / 2f, 0f, playerHouseBounds.size.z / 2f);
        gm.dayNight = cycle;
        gm.monster = knock;
        gm.torch = torch;
        gm.foodPickup = foodPickup;
        gm.waterPickup = waterPickup;
        gm.medicinePickup = medicinePickup;
        gm.batteryPickup = batteryPickup;
        gm.totalPickups = 4;
        gm.npcMukhiyaPos = mukhiyaPos;
        gm.npcMeeraPos = meeraPos;
        gm.npcPujariPos = pujariPos;

        // Mobile controls (created early in Awake by SetupCameraAndUIEarly
        // — only fill in the parts that need the built village here, never
        // create a second copy).
        MobileControls ui = gameObject.GetComponent<MobileControls>();
        if (ui == null)
        {
            ui = gameObject.AddComponent<MobileControls>();
        }
        ui.player = pc;
        ui.torch = torch;
        ui.gameManager = gm;
        gm.ui = ui;

        cycle.OnNightStart = delegate { knock.BeginNight(); AudioManager.StartNightAmbience(); };
        // AUDIT FIX 2026-11-08: dawn birds only on a real survival. The old
        // delegate played them unconditionally, so they also played over
        // the dawn-outside GameOver screen. (IsOver alone cannot guard
        // this — Survived() sets it on a win too — so the check is the
        // same inside-house condition Survived() uses.)
        cycle.OnDawn = delegate { gm.Survived(); AudioManager.StopNightAmbience(); if (gm.player != null && GameManager.IsInsidePlayerHouse(gm.player.transform.position)) AudioManager.PlayDawnRelief(); };
    }

    // Follow-up 2026-11-07: the REAL monster — HellDemon Reborn (73,844
    // tris, rigged, 7 animations, full PBR) replaces the black capsule-man.
    // Loaded here during the village build so BuildMonster() (sync, called
    // later during setup) can return it. Horned demon, not hooded — the
    // user accepted that by downloading it.
    private System.Collections.IEnumerator BuildMonsterModelAsync()
    {
        GameObject demon = null;
        yield return GltfModelLoader.LoadModel(
            "helldemon_reborn_16_sr87.glb", villageRoot, "Monster",
            MonsterKnock.kLair, 0f, 2.3f,
            GltfModelLoader.NormalizeMode.ByHeight,
            GltfModelLoader.ColliderMode.None, g => demon = g);
        if (demon == null)
        {
            Debug.LogWarning("[Village] HellDemon failed to load — falling back to capsule monster");
            yield break;
        }
        // Walk loop while it hunts (MonsterKnock moves the transform).
        GltfModelLoader.PlayEmbeddedAnimation(demon, "WALKING");
        demon.SetActive(false);
        monsterModel = demon;
        // Lighting pass 2026-11-07: the demon rendered as a BLACK
        // silhouette at the jungle edge — night ambient (0.13) + moonlight
        // (0.35) never reach it and the chowk lamp is 20 m away. A cool
        // blue point light just in front of it (moonlight spill) rims its
        // horns and body so its detail reads instead of a black blob.
        // Attempt-8 shot proved 1.6 was still too dim to read the demon
        // at 6 m — raised to 3.0 / range 9.
        // MonsterLight: attempt-11 proved 3.0 at (-2, 2.8, -17.5) still
        // leaves the demon a silhouette (dark albedo, ~3.2 m to the
        // chest). Raised to 6.0 and moved closer/lower (-2, 2.0, -18)
        // so it hits the chest and face directly (2026-11-07).
        // OVERNIGHT R3c: the light follows the lair (see MonsterKnock) —
        // same 1 m-north offset as the old (-2,2,-18) light over (-2,-19).
        AddPointLight("MonsterLight", MonsterKnock.kLair + new Vector3(0f, 2f, 1f),
            new Color(0.55f, 0.65f, 1.0f), 6.0f, 9f);
    }

    private GameObject BuildMonster()
    {
        // Follow-up 2026-11-07: the real HellDemon model already loaded
        // (and hidden) by BuildMonsterModelAsync — use it. Fall back to
        // the capsule-man only if the .glb failed to load.
        if (monsterModel != null)
        {
            return monsterModel;
        }
        GameObject m = new GameObject("Monster");
        m.transform.SetParent(villageRoot, false);
        m.transform.position = MonsterKnock.kLair;

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "MonsterBody";
        body.transform.SetParent(m.transform, false);
        body.transform.localPosition = new Vector3(0f, 1.35f, 0f);
        body.transform.localScale = new Vector3(0.85f, 1.9f, 0.85f);
        body.GetComponent<Renderer>().material = matDark;
        Destroy(body.GetComponent<Collider>());

        GameObject head = Sphere("MonsterHead", Vector3.zero, new Vector3(0.55f, 0.65f, 0.55f), matDark, m.transform);
        head.transform.localPosition = new Vector3(0f, 2.55f, 0f);
        Destroy(head.GetComponent<Collider>());

        // Pale glowing eyes.
        GameObject eyeL = Sphere("EyeL", Vector3.zero, new Vector3(0.1f, 0.1f, 0.1f), matEye, m.transform);
        eyeL.transform.localPosition = new Vector3(-0.13f, 2.6f, 0.26f);
        Destroy(eyeL.GetComponent<Collider>());
        GameObject eyeR = Sphere("EyeR", Vector3.zero, new Vector3(0.1f, 0.1f, 0.1f), matEye, m.transform);
        eyeR.transform.localPosition = new Vector3(0.13f, 2.6f, 0.26f);
        Destroy(eyeR.GetComponent<Collider>());

        // Long arms.
        GameObject armL = Box("MonsterArmL", Vector3.zero, new Vector3(0.18f, 1.5f, 0.18f), matDark, m.transform);
        armL.transform.localPosition = new Vector3(-0.55f, 1.5f, 0f);
        Destroy(armL.GetComponent<Collider>());
        GameObject armR = Box("MonsterArmR", Vector3.zero, new Vector3(0.18f, 1.5f, 0.18f), matDark, m.transform);
        armR.transform.localPosition = new Vector3(0.55f, 1.5f, 0f);
        Destroy(armR.GetComponent<Collider>());

        m.SetActive(false);
        return m;
    }

    // Lighting pass 2026-11-07 — TWO-COLOUR night lighting (the
    // reference image): warm orange lamp light against cool blue
    // moonlight. The scene had ONE warm light (the chowk lamp) and cold
    // moonlight, so most of the village went black at night. Three small
    // warm point lights — temple entrance, haveli front, gate — give the
    // landmarks the warm glow the reference shows, and the gate lantern
    // also lights the Ambassador (which sits in the dark south lane).
    // Ranges are small (8-9 m) and the count is low (4 new point lights
    // total incl. the monster light) so the phone's forward renderer
    // stays within its additional-light budget.
    private void BuildWarmLights()
    {
        // Temple entrance — warm glow on the temple front, Pujari + Nandi.
        AddPointLight("TempleLantern", new Vector3(2f, 3f, -13.5f),
            new Color(1f, 0.7f, 0.35f), 1.4f, 9f);
        // Pujari's FRONT: the temple lantern sits behind him (north), so
        // his south face falls in shadow. A small warm light south of him
        // lights his face and the courtyard ground for the front close-up.
        // PujariLight: attempt-10 proved 0.8 leaves his front unreadable
        // at 2.2 m — raised to 1.2 / 6 m (2026-11-07).
        AddPointLight("PujariLight", new Vector3(3.6f, 2.2f, -11.5f),
            new Color(1f, 0.72f, 0.38f), 1.2f, 6f);
        // Haveli front (haveli sits at x=16 facing the chowk/west).
        AddPointLight("HaveliLantern", new Vector3(13.5f, 2.5f, -3.5f),
            new Color(1f, 0.72f, 0.38f), 1.0f, 8f);
        // Gate + Ambassador (car at (2.5, 15), gate at (0, 20.5)). The
        // lantern sits SOUTH of the car (z=14, not 18): attempt-20
        // proved a lantern north of the car lights only its far side —
        // the camera-facing south side stayed black. At z=14 it lights
        // the car's south side (the side the player/camera sees) and
        // still reaches the gate (6 m, within its 8 m range).
        AddPointLight("GateLantern", new Vector3(1f, 2.6f, 14f),
            new Color(1f, 0.72f, 0.38f), 1.6f, 8f);
    }

    // Small helper: one warm/cool point light under the village root.
    // Failures are warnings, never fatal (one bad light must not kill
    // the village build).
    private void AddPointLight(string name, Vector3 pos, Color color, float intensity, float range)
    {
        try
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(villageRoot, false);
            go.transform.position = pos;
            Light l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            Debug.Log("[Village] Light added: " + name + " at " + pos);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Village] Light failed (" + name + "): " + e.Message);
        }
    }

    private void SetupSceneAmbience()
    {
        // Shadows on for every quality level (Android default was Low = no shadows).
        try
        {
            QualitySettings.shadows = ShadowQuality.All;
            // ROUND 5 (lag): shadow distance 45 -> 30 m. The shadow map
            // covers a 45 m radius today — nearly every object in the
            // village renders into it a second time, which is the
            // predictable cause of the walking lag on the phone (draw
            // calls, not triangles: ~0.62M tris is fine for the GPU).
            // 30 m still covers the player's surroundings; shadows past
            // that range simply stop instead of costing a second render.
            QualitySettings.shadowDistance = 30f;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Village] Shadow settings failed: " + e.Message);
        }
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.004f;
        RenderSettings.fogColor = new Color(0.6f, 0.65f, 0.7f);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.55f);
        if (mainCam != null)
        {
            // URP skybox: the moonless-HDRI skybox material is assigned to
            // RenderSettings.skybox (see Assets/Editor/UrpFixSetup.cs) — the
            // camera must clear to Skybox or the sky never renders.
            mainCam.clearFlags = CameraClearFlags.Skybox;
            mainCam.backgroundColor = new Color(0.53f, 0.81f, 0.98f);
            mainCam.farClipPlane = 300f;
        }
    }

    // URP environment reflection (2026-11-07 URP fix): a single reflection
    // probe rendered ONCE, after every model is loaded, so metallic/PBR
    // materials have an environment to reflect. Realtime + ViaScripting
    // means exactly one cubemap render at village build time (no per-frame
    // cost on the phone). Renderers already use ReflectionProbeUsage
    // BlendProbes, so they pick this probe up automatically.
    private void SetupReflectionProbe()
    {
        try
        {
            GameObject probeGo = new GameObject("VillageReflectionProbe");
            probeGo.transform.SetParent(villageRoot, false);
            probeGo.transform.localPosition = new Vector3(0f, 2.5f, 0f);
            ReflectionProbe probe = probeGo.AddComponent<ReflectionProbe>();
            probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
            probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
            probe.size = new Vector3(55f, 30f, 55f);
            // ROUND 5 (lag): 128 -> 64 cubemap. One-time build cost, but
            // the probe renders the whole village six times — 64 is
            // plenty for PBR reflections on a phone.
            probe.resolution = 64;
            probe.nearClipPlane = 0.3f;
            probe.farClipPlane = 120f;
            probe.RenderProbe();
            Debug.Log("[Village] Reflection probe rendered (128 cubemap).");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Village] Reflection probe failed: " + e.Message);
        }
    }

    private System.Collections.IEnumerator IntroSequence()
    {
        yield return new WaitForSeconds(0.8f);
        GameManager gm = GetComponent<GameManager>();
        if (gm == null) yield break;
        gm.ShowSubtitle("Bhairavpur... ye gaon shaam hote hi darwaza band kar leta hai.", 4f);
        yield return new WaitForSeconds(4.2f);
        gm.ShowSubtitle("Raat me koi bhi darwaza khatkhataye to kholna mat, awaaz chahe kisi apne ki hi kyun na ho!", 4f);
        yield return new WaitForSeconds(4.2f);
        gm.SetObjective("Din: Kuaan ke paas se khana uthao, phir ghar ke andar jao.");
    }
}
