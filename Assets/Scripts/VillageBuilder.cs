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

    public static void Register(Bounds b)
    {
        s_blocks.Add(b);
    }

    public static void Unregister(Bounds b)
    {
        s_blocks.Remove(b);
    }

    public static void Clear()
    {
        s_blocks.Clear();
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
    private Vector3 mukhiyaPos = new Vector3(-3.5f, 0f, 2.5f);
    private Vector3 meeraPos = new Vector3(0.5f, 0f, 6.5f);
    // (x 2 -> 3.6, 2026-11-07: the visual Pujari was moved 1.6 m east by
    // cheap-wins; the NPC trigger must sit on the visible priest.)
    private Vector3 pujariPos = new Vector3(3.6f, 0f, -14.2f);
    public Light lampLight;
    public Material moonMaterial;
    public GameObject lampGlow;
    public GameObject moonGlow;

    private Material matGround;
    private Material matPlaster;
    private Material matPlaster2;
    private Material matWood;
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
    private Material matShadowBlob;
    // Phase 4: Poly Haven CC0 1K textures (Assets/Resources/Textures/).
    private Material matBrick;
    private Material matThatch;
    private Material matMudDry;
    private Material matCobble;
    private Material matLeaves;
    private Material matWitheredGrass;

    private Transform villageRoot;
    private Camera mainCam;
    private Shader standardShader;
    private Text bootStatusText;
    private int buildErrors;

    // Phase 3: real .glb models load async via glTFast. The CI test waits
    // for this flag (signalled by a "ModelsLoaded" GameObject — the test
    // assembly cannot reference game classes) before asserting/screenshots.
    public static bool AllModelsLoaded;
    // Bed spot inside Nilesh's house (set while building the house, so the
    // real hospital-bed model lands exactly where the primitive bed was).
    private Vector3 playerBedPos = new Vector3(-11f, 0f, 4.3f);

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
    private const float kBuildDeadlineSeconds = 90f;

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
            Screen.orientation = ScreenOrientation.LandscapeRight;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Village] Orientation lock failed: " + e.Message);
        }

        VillageColliders.Clear();
        mainCam = Camera.main;
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

        // Each step is isolated: one failing step must not kill the village.
        AllModelsLoaded = false;
        // Freeze fix: arm the build deadline and the per-model progress
        // display (so the player sees models loading, not a frozen
        // "Building..." line).
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
    private System.Collections.IEnumerator BuildVillageAsync()
    {
        try
        {
            yield return BuildPlayerModelAsync();
            yield return BuildHousesAsync();
            yield return BuildWellAndLampAsync();
            // Freeze fix: the monster is gameplay-critical (the night
            // knock IS the game) — load it early, while the deadline
            // still allows it, instead of last (where a slow phone never
            // reached it and the game started with the capsule fallback).
            yield return BuildMonsterModelAsync();
            RunStep("Field", BuildField);
            yield return BuildJungleAndFencesAsync();
            yield return BuildPropsAsync();
            yield return BuildNewModelsAsync();
            RunStep("WarmLights", BuildWarmLights);
            RunStep("CleanupStrays", CleanupStrays);
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
    private void FinishBuild()
    {
        if (buildFinished) return;
        buildFinished = true;
        // Teardown guard: if the scene is going away, do not wire systems.
        if (villageRoot == null || gameObject == null || !gameObject.scene.isLoaded) return;
        RunStep("SetupSystems", SetupSystems);
        RunStep("SceneAmbience", SetupSceneAmbience);
        bool cutShort = GltfModelLoader.DeadlineReached;
        SetBootStatus("Village: " + CountVillageObjects() + " objects built"
            + (cutShort ? " — build timed out, started with what loaded" : "")
            + (buildErrors > 0 ? " (" + buildErrors + " step errors — see log)" : ""));
        // One-time environment reflection for the village (URP fix):
        // render the reflection probe now that every model is loaded.
        SetupReflectionProbe();
        StartCoroutine(IntroSequence());
        StartCoroutine(VerifyCameraView());
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
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Village] Boot status UI failed: " + e.Message);
        }
    }

    private void SetBootStatus(string msg)
    {
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
        if (matGround == null) matGround = MakeMat("GroundMud", LoadTex("ground_mud"), new Color(0.85f, 0.78f, 0.68f), 0.05f, new Vector2(5f, 5f));
        if (matPlaster == null) matPlaster = MakeMat("WallPlaster", LoadTex("wall_plaster"), new Color(0.92f, 0.85f, 0.72f), 0.15f, new Vector2(2f, 1.5f));
        if (matWood == null) matWood = MakeMat("WoodPlanks", LoadTex("wood_planks"), new Color(0.75f, 0.6f, 0.42f), 0.3f, new Vector2(1f, 1f));
        if (matRoof == null) matRoof = MakeMat("RoofTiles", LoadTex("roof_tiles"), new Color(0.95f, 0.8f, 0.62f), 0.25f, new Vector2(3f, 2f));
        if (matStone == null) matStone = MakeMat("StoneWall", LoadTex("stone_wall"), new Color(0.8f, 0.78f, 0.72f), 0.1f, new Vector2(2f, 1f));
        if (matGrass == null) matGrass = MakeMat("FieldGrass", LoadTex("field_grass"), new Color(0.7f, 0.8f, 0.55f), 0.05f, new Vector2(4f, 3f));

        // Safety net: re-assign textures by name if a loaded .mat lost its reference.
        AssignTex(matGround, "ground_mud", new Vector2(5f, 5f));
        AssignTex(matPlaster, "wall_plaster", new Vector2(2f, 1.5f));
        AssignTex(matWood, "wood_planks", new Vector2(1f, 1f));
        AssignTex(matRoof, "roof_tiles", new Vector2(3f, 2f));
        AssignTex(matStone, "stone_wall", new Vector2(2f, 1f));
        AssignTex(matGrass, "field_grass", new Vector2(4f, 3f));

        // Resolve the lit shader once for the flat/emissive materials
        // (URP Lit under URP, Standard under Built-in — see GetStandardShader).
        standardShader = GetStandardShader();
        if (standardShader == null)
        {
            Debug.LogWarning("[Village] Standard shader NOT found even after .mat loads!");
        }

        matPlaster2 = MakeMat("WallPlaster2", LoadTex("wall_plaster"), new Color(0.85f, 0.76f, 0.62f), 0.15f, new Vector2(2f, 1.5f));
        matDark = MakeFlat("MonsterDark", new Color(0.02f, 0.02f, 0.025f), 0.0f);
        matGlass = MakeFlat("WindowGlass", new Color(0.03f, 0.04f, 0.07f), 0.6f);
        matSkin = MakeFlat("Skin", new Color(0.45f, 0.30f, 0.20f), 0.4f);
        matKurtaCream = MakeFlat("KurtaCream", new Color(0.88f, 0.82f, 0.68f), 0.5f);
        matPagdi = MakeFlat("Pagdi", new Color(0.85f, 0.45f, 0.10f), 0.4f);
        matPants = MakeFlat("Pants", new Color(0.18f, 0.16f, 0.14f), 0.4f);
        matEye = MakeEmissive("MonsterEyes", new Color(0.9f, 0.85f, 0.6f), new Color(1.4f, 1.1f, 0.35f));
        matLampGlass = MakeEmissive("LampGlass", new Color(1f, 0.85f, 0.55f), new Color(1.6f, 1.0f, 0.35f));
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
        matMudDry = MakeMat("MudDry", LoadTex("brown_mud_dry_diff_1k"), new Color(0.85f, 0.78f, 0.68f), 0.05f, new Vector2(2f, 2f),
            LoadTex("brown_mud_dry_nor_gl_1k"), LoadTex("brown_mud_dry_metal_smooth_1k"), LoadTex("brown_mud_dry_ao_1k"));
        matCobble = MakeMat("Cobble", LoadTex("cobblestone_floor_04_diff_1k"), new Color(0.85f, 0.84f, 0.8f), 0.1f, new Vector2(4f, 4f),
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
        if (ground != null)
        {
            Renderer r = ground.GetComponent<Renderer>();
            if (r != null && matGround != null)
            {
                r.material = matGround;
            }
        }

        // Dirt path patches along the main lane (worn ground, darker mud).
        // Phase 4: real dry-mud texture when available, flat colour fallback.
        // Lighting pass 2026-11-07: the old lane read as FLAT TILES — 6
        // identical boxes in a perfect line. Now each patch varies in
        // width/length, staggers sideways, rotates a few degrees and
        // alternates mud/ground material, so the lane reads as a worn
        // track, not paving.
        Material dirt = (matMudDry != null)
            ? matMudDry
            : MakeFlat("DirtPath", new Color(0.45f, 0.35f, 0.24f), 0.05f);
        Material dirtAlt = (matGround != null) ? matGround : dirt;
        for (int i = 0; i < 6; i++)
        {
            float z = -10f + i * 4f;
            float w = 2.4f + ((i % 3) - 1) * 0.35f;
            float l = 3.4f + ((i % 2) * 0.5f);
            float x = ((i % 3) - 1) * 0.3f;
            GameObject patch = Box("DirtPath", new Vector3(x, 0.03f, z), new Vector3(w, 0.04f, l), (i % 2 == 0) ? dirt : dirtAlt, villageRoot);
            patch.transform.rotation = Quaternion.Euler(0f, ((i * 7) % 13) - 6f, 0f);
            Destroy(patch.GetComponent<Collider>());
        }

        // Fix-push P2-17: east-west dirt patches across the lane at z ~ 7
        // (same dirt material / patch style as the north-south lane above).
        // Lighting pass: same variation treatment as the main lane.
        for (int i = 0; i < 5; i++)
        {
            float x = -8f + i * 4f;
            float w = 3.4f + ((i % 2) * 0.5f);
            float l = 2.4f + ((i % 3) - 1) * 0.35f;
            float z = 7f + ((i % 3) - 1) * 0.3f;
            GameObject patchEW = Box("DirtPathEW", new Vector3(x, 0.03f, z), new Vector3(w, 0.04f, l), (i % 2 == 0) ? dirt : dirtAlt, villageRoot);
            patchEW.transform.rotation = Quaternion.Euler(0f, ((i * 11) % 13) - 6f, 0f);
            Destroy(patchEW.GetComponent<Collider>());
        }

        // Lighting pass 2026-11-07 — PUDDLES (the reference image): dark
        // glossy discs on the chowk and lane that catch the warm lamp
        // light and the moon — wet mud after rain. Very high smoothness
        // (0.95) + dark blue-grey colour; the reflection probe + lamp
        // specular make them glint instead of reading as flat paint.
        // y=0.045 sits above the chowk (0.02) and lane patches (0.03).
        Material puddleMat = MakeFlat("Puddle", new Color(0.07f, 0.08f, 0.11f), 0.95f);
        Vector3[] puddleSpots = new Vector3[]
        {
            new Vector3(1.5f, 0.045f, 2.5f), new Vector3(3.6f, 0.045f, 0.4f),
            new Vector3(0f, 0.045f, -4f), new Vector3(-1.6f, 0.045f, 5f),
            new Vector3(4.2f, 0.045f, 8.2f),
        };
        for (int i = 0; i < puddleSpots.Length; i++)
        {
            float r = 0.9f + (i % 3) * 0.35f;
            GameObject puddle = Cylinder("Puddle", puddleSpots[i],
                new Vector3(r, 0.02f, r * (0.7f + (i % 2) * 0.25f)), puddleMat, villageRoot);
            puddle.transform.rotation = Quaternion.Euler(0f, (i * 53) % 180f, 0f);
            Destroy(puddle.GetComponent<Collider>());
        }

        // Phase 4: cobbled chowk circle around the village centre.
        if (matCobble != null)
        {
            GameObject chowk = Cylinder("ChowkCobble", new Vector3(0f, 0.02f, 0f), new Vector3(9f, 0.04f, 9f), matCobble, villageRoot);
            Destroy(chowk.GetComponent<Collider>());
        }

        // Grass patches near the field edge — FLAT textured patches, not
        // the old vertical green boxes (they read as green dice).
        // Withered-grass photo texture, varied rotation/scale.
        for (int i = 0; i < 16; i++)
        {
            float x = 4f + (i % 8) * 1.4f;
            float z = 12.5f + (i / 8) * 1.2f + ((i % 3) * 0.4f);
            Material gm = (matWitheredGrass != null) ? matWitheredGrass : matGrass;
            GameObject tuft = Box("GrassPatch", new Vector3(x, 0.06f, z), new Vector3(1.1f, 0.04f, 1.1f), gm, villageRoot);
            tuft.transform.rotation = Quaternion.Euler(0f, i * 23f, 0f);
            Destroy(tuft.GetComponent<Collider>());
        }

        // Scattered ground variation across the village: leaf litter,
        // withered grass and dry-mud patches break up the flat brown.
        // Kept outside the chowk disc (r ~5) and off the dirt lanes so
        // nothing z-fights the path/chowk patches (these sit at y=0.06,
        // above the lane patches at 0.03 and the chowk at 0.02).
        Vector3[] patchSpots = new Vector3[]
        {
            new Vector3(-12f, 0f, -6f), new Vector3(12f, 0f, -10f),
            new Vector3(-14f, 0f, 4f), new Vector3(14f, 0f, 6f),
            new Vector3(-6f, 0f, -14f), new Vector3(8f, 0f, 14f),
            new Vector3(-10f, 0f, 12f), new Vector3(16f, 0f, -6f),
            new Vector3(-16f, 0f, -10f), new Vector3(10f, 0f, -14f),
            new Vector3(-4f, 0f, 16f), new Vector3(18f, 0f, 10f),
            // Second ring — the big empty stretches (north lane sides,
            // east/west edges, south field edge).
            new Vector3(-8f, 0f, -12f), new Vector3(8f, 0f, -12f),
            new Vector3(-15f, 0f, -4f), new Vector3(15f, 0f, 4f),
            new Vector3(-10f, 0f, 18f), new Vector3(4f, 0f, 18f),
            new Vector3(14f, 0f, 16f), new Vector3(18f, 0f, -12f),
            new Vector3(-18f, 0f, 8f), new Vector3(6f, 0f, -8f),
            // Third ring — foreground/south and mid-village bare stretches,
            // where the main camera looks (kept off the dirt lanes z=7 / x=0
            // and outside the chowk disc r~5).
            new Vector3(-4f, 0f, 12f), new Vector3(4f, 0f, 12f),
            new Vector3(-10f, 0f, 14f), new Vector3(10f, 0f, 14f),
            new Vector3(-14f, 0f, -2f), new Vector3(14f, 0f, -2f),
            new Vector3(-8f, 0f, -14f), new Vector3(8f, 0f, -14f),
            new Vector3(12f, 0f, 10f), new Vector3(-12f, 0f, 10f),
            new Vector3(2f, 0f, 18f), new Vector3(18f, 0f, -4f),
        };
        for (int i = 0; i < patchSpots.Length; i++)
        {
            Material pm = (i % 3 == 0 && matLeaves != null) ? matLeaves
                : (i % 3 == 1 && matWitheredGrass != null) ? matWitheredGrass
                : (matMudDry != null ? matMudDry : matGround);
            float s = 2.2f + (i % 4) * 0.5f;
            GameObject patch = Box("GroundPatch", new Vector3(patchSpots[i].x, 0.06f, patchSpots[i].z),
                new Vector3(s, 0.04f, s * (0.8f + (i % 3) * 0.15f)), pm, villageRoot);
            patch.transform.rotation = Quaternion.Euler(0f, (i * 37) % 180f, 0f);
            Destroy(patch.GetComponent<Collider>());
        }
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
        GameObject nilesh = null;
        yield return GltfModelLoader.LoadModel(
            "indian_man_77_hwkm.glb", player.transform, "Nilesh",
            player.transform.position, 0f, 1.7f,
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
        Vector3 playerCenter = new Vector3(-9f, 0f, 6f);
        GameObject hut = null;
        yield return GltfModelLoader.LoadModel(
            "thatched_hut_4_p31g.glb", villageRoot, "House_Player",
            playerCenter, 0f, 6.5f, GltfModelLoader.NormalizeMode.ByWidth,
            GltfModelLoader.ColliderMode.None, g => hut = g);
        if (hut != null)
        {
            Bounds hb = GetWorldBounds(hut);
            float w = hb.size.x;
            float d = hb.size.z;
            float doorX = hb.max.x - playerCenter.x;
            BuildDoor(hut.transform, playerCenter, doorX, DoorSide.East, 1.2f, 2.2f);
            BuildHouseInterior(hut.transform, playerCenter, w, d);
            RegisterHouseDoorGap(hb, DoorSide.East, 1.2f, 2.2f);
        }
        else
        {
            BuildHouse("House_Player", playerCenter, 6f, 5f, 3f, DoorSide.East, true, false);
        }

        // Houses 2-6: one different real model each.
        // Follow-up 2026-11-07: Indian House Old (35,565 tris, albedo-only)
        // replaces the 4,100-tri low-poly box.
        yield return BuildRealHouse("House_2", "indian_house_old_18_qlsq.glb",
            new Vector3(9f, 0f, 8f), 15f, 5.5f);
        // Replacement 2026-11-07: stealth86 Thatched Hut, decimated
        // 161k -> ~50k tris (full PBR on Material.002). House_4 stays the
        // old pointed hut until the round-2 hut arrives — every house
        // must be different.
        yield return BuildRealHouse("House_3", "thatched_hut_stealth86_50k.glb",
            new Vector3(-9f, 0f, -9f), -10f, 4.5f);
        // Follow-up 2026-11-07: traditional ROUND thatched hut, decimated
        // 149k -> 50k tris (full PBR). Round silhouette, genuinely different
        // from House_3's gabled stealth86 hut.
        yield return BuildRealHouse("House_4", "traditional_round_thatched_hut_50k.glb",
            new Vector3(9f, 0f, -7f), 25f, 5f);
        yield return BuildRealHouse("House_5", "old_village_house_-_indian_game_asset_24_bac7.glb",
            new Vector3(-13f, 0f, 13f), -20f, 5f);
        yield return BuildRealHouse("House_6", "indian_village_house_famous_3d_model_37_4zjm.glb",
            new Vector3(13f, 0f, -14f), 10f, 5f);
    }

    private System.Collections.IEnumerator BuildRealHouse(
        string name, string file, Vector3 pos, float yaw, float size)
    {
        GameObject h = null;
        yield return GltfModelLoader.LoadModel(
            file, villageRoot, name, pos, yaw, size,
            GltfModelLoader.NormalizeMode.ByWidth,
            GltfModelLoader.ColliderMode.FullBounds, g => h = g);
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
        float doorH = 2.2f;

        // Floor slab (stone plinth).
        Box(name + "_Floor", center + new Vector3(0f, 0.06f, 0f), new Vector3(w + 0.4f, 0.12f, d + 0.4f), matStone, root);

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
            // Primitive fallback (Phase 2 well).
            Cylinder("Well", wellPos + new Vector3(0f, 0.5f, 0f), new Vector3(2.0f, 1.0f, 2.0f), matStone, villageRoot);
            Cylinder("WellWater", wellPos + new Vector3(0f, 0.92f, 0f), new Vector3(1.7f, 0.06f, 1.7f), MakeFlat("WellWater", new Color(0.02f, 0.06f, 0.09f), 0.9f), villageRoot);
            Cylinder("WellPostA", wellPos + new Vector3(-0.9f, 1.6f, 0f), new Vector3(0.18f, 2.2f, 0.18f), matWood, villageRoot);
            Cylinder("WellPostB", wellPos + new Vector3(0.9f, 1.6f, 0f), new Vector3(0.18f, 2.2f, 0.18f), matWood, villageRoot);
            GameObject wellRoof = Box("WellRoof", wellPos + new Vector3(0f, 2.8f, 0f), new Vector3(2.6f, 0.12f, 1.8f), matRoof, villageRoot);
            wellRoof.transform.rotation = Quaternion.Euler(0f, 0f, 12f);
            // Crossed wood beams under the well roof.
            GameObject beamA = Box("WellBeamA", wellPos + new Vector3(0f, 2.62f, 0f), new Vector3(2.4f, 0.12f, 0.12f), matWood, villageRoot);
            beamA.transform.rotation = Quaternion.Euler(0f, 0f, 10f);
            GameObject beamB = Box("WellBeamB", wellPos + new Vector3(0f, 2.62f, 0f), new Vector3(0.12f, 0.12f, 1.7f), matWood, villageRoot);
            beamB.transform.rotation = Quaternion.Euler(10f, 0f, 0f);
            Block(wellPos + new Vector3(0f, 0.5f, 0f), new Vector3(2.2f, 1.1f, 2.2f));
        }

        // Food pickup (crate) next to the well.
        GameObject crate = Box("Pickup_Food", wellPos + new Vector3(1.9f, 0.3f, 1.2f), new Vector3(0.65f, 0.6f, 0.65f), matWood, villageRoot);
        foodPickup = crate.AddComponent<Pickup>();
        foodPickup.itemName = "Khana";
        foodPickup.bobHeight = 0.12f;

        // Fix-push P2-18b: 3 more pickups, same Pickup system as the food
        // crate — water by the handpump, medicine inside Nilesh's house,
        // battery by the chowk lamp.
        waterPickup = MakePickup("Pickup_Water", new Vector3(5f, 0.3f, 10.2f), "Paani");
        medicinePickup = MakePickup("Pickup_Medicine", new Vector3(-10.5f, 0.3f, 5f), "Dawai");
        batteryPickup = MakePickup("Pickup_Battery", new Vector3(1.2f, 0.3f, 2.8f), "Battery");

        // Chowk lamp: pole + warm flickering point light + glow.
        Vector3 lampPos = new Vector3(2.5f, 0f, 1.5f);
        Cylinder("LampPole", lampPos + new Vector3(0f, 1.6f, 0f), new Vector3(0.22f, 3.2f, 0.22f), matDark, villageRoot);
        Box("LampHead", lampPos + new Vector3(0f, 3.3f, 0f), new Vector3(0.5f, 0.45f, 0.5f), matLampGlass, villageRoot);
        GameObject bulb = Sphere("LampBulb", lampPos + new Vector3(0f, 3.28f, 0f), new Vector3(0.3f, 0.3f, 0.3f), matLampGlass, villageRoot);
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
        GameObject mango = null, banyan = null, palm = null;
        yield return LoadRealTree("mango_tree_3_sl11.glb", "Tree_Mango",
            new Vector3(-16f, 0f, -2f), 6f, GltfModelLoader.ColliderMode.Trunk, g => mango = g);
        // Replacement 2026-11-07: Chinese Banyan, 111k tris with real
        // aerial roots (the old 3.2k-tri banyan + its 35 m base disc are gone).
        yield return LoadRealTree("chinese_banyan_ficus_microcarpa_9_tpa1.glb", "Tree_Banyan",
            new Vector3(16f, 0f, 2f), 7f, GltfModelLoader.ColliderMode.Trunk, g => banyan = g);
        // Replacement 2026-11-07: Realistic Palm Tree Free, 27k tris,
        // single date palm (the old 2.9k-tri cluster rendered black).
        yield return LoadRealTree("realistic_palm_tree_free_8_k8rm.glb", "Tree_Palm",
            new Vector3(-4f, 0f, 18f), 5f, GltfModelLoader.ColliderMode.FullBounds, g => palm = g);
        yield return LoadRealTree("trees_25_wae0.glb", "Tree_Pack",
            new Vector3(-18f, 0f, -18f), 8f, GltfModelLoader.ColliderMode.FullBounds, null);

        // Jungle edge: REAL tree clones ringing the village. The old
        // primitive blob trees (BuildTree) are gone — user ordered no
        // cartoon. Same ring positions as before, sources round-robin.
        GameObject[] ringSources = new GameObject[] { mango, banyan, palm };
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
    private void CloneRingTree(GameObject[] sources, int index, Vector3 pos)
    {
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
        yield return LoadProp("medieval_wooden_cart_1_jab4.glb", "Cart",
            new Vector3(5.5f, 0f, 3.0f), 0f, 2.2f,
            GltfModelLoader.NormalizeMode.ByWidth, GltfModelLoader.ColliderMode.FullBounds);
        // Replacement 2026-11-07: Bullcart, 105k tris — good geometry but
        // ZERO textures as distributed (confirmed twice). Wood PBR is
        // applied in code (ApplyWoodPbr) — never used flat-coloured.
        // Placement fix 2026-11-07: the cart sat at (-6, -4) — ON the
        // west fence line (x=-5.5, z -13.4 to -1), the rail passing
        // through it. Now (-7.5, -4): 0.75 m clear of the fence.
        yield return LoadProp("Bullcart/scene.gltf", "BullockCart",
            new Vector3(-7.5f, 0f, -4f), 0f, 2.5f,
            GltfModelLoader.NormalizeMode.ByWidth, GltfModelLoader.ColliderMode.FullBounds,
            ApplyWoodPbr);
        // Replacement 2026-11-07: Classic Tractor, 81.7k tris — good
        // geometry but ZERO textures as distributed. PBR is applied per
        // material name in code (ApplyTractorPbr) — never flat-coloured.
        yield return LoadProp("classic_tractor__1__11_scro.glb", "Tractor",
            new Vector3(12f, 0f, 11f), 30f, 3f,
            GltfModelLoader.NormalizeMode.ByWidth, GltfModelLoader.ColliderMode.FullBounds,
            ApplyTractorPbr);
        // Fix-push P2-14: hero's broken-down car (rusty old car, once only,
        // at the north village edge by the gate — agreed story override of
        // the low-poly rule).
        // Replacement 2026-11-07: Hindustan Ambassador, 12.3k tris, FULL
        // PBR — but CLEAN, no rust. Rust/weathering is applied in code
        // (WeatherAmbassador: mud detail maps + darkened paint) so it
        // still reads as the broken-down car from the story.
        // Placement fix 2026-11-07: the car sat at (5, 18.5) — ON the
        // east fence line (x=5.5, z 8.7-20.3), the fence rail passing
        // through it. Now (1, 16): in the south lane by the gate, clear
        // of the fence (east edge 3.4 < 5.5) and of the gate (z <= 18.6).
        yield return LoadProp("hindustan_ambassador_4_rh04.glb", "HeroCar",
            new Vector3(1f, 0f, 16f), 30f, 3f,
            GltfModelLoader.NormalizeMode.ByWidth, GltfModelLoader.ColliderMode.FullBounds,
            WeatherAmbassador);
        // Ancient gate at the north lane entrance — no collider, so the
        // player can walk through the arch.
        yield return LoadProp("ancient_gate_11_4q5m.glb", "Gate",
            new Vector3(0f, 0f, 20.5f), 0f, 4f,
            GltfModelLoader.NormalizeMode.ByWidth, GltfModelLoader.ColliderMode.None);
        // Small props near the houses.
        // Placement fix 2026-11-07: the handpump sat at (5, 9), 0.25 m
        // from the east fence rail. Now (4.5, 9): clear of the fence.
        yield return LoadProp("handpump_3d_39_aah5.glb", "Handpump",
            new Vector3(4.5f, 0f, 9f), 0f, 1.3f,
            GltfModelLoader.NormalizeMode.ByHeight, GltfModelLoader.ColliderMode.FullBounds);
        yield return LoadProp("indian_terracotta_matka_red_clay_water_pot_40_ris7.glb", "Matka",
            new Vector3(-5f, 0f, 8.5f), 0f, 0.5f,
            GltfModelLoader.NormalizeMode.ByWidth, GltfModelLoader.ColliderMode.None);
        yield return LoadProp("rusty_and_oil_stained_oil_barrel_43_s99h.glb", "OilBarrel",
            new Vector3(7.5f, 0f, 3.5f), 0f, 0.7f,
            GltfModelLoader.NormalizeMode.ByWidth, GltfModelLoader.ColliderMode.FullBounds);
        // Hospital bed INSIDE Nilesh's house (replaces the primitive bed).
        yield return LoadProp("old_and_worn_out_hospital_bed_42_kn6o.glb", "HospitalBed",
            playerBedPos, 90f, 2.1f,
            GltfModelLoader.NormalizeMode.ByWidth, GltfModelLoader.ColliderMode.FullBounds);
        // Animals: cow, goat and buffalo in the field; horse by the cart;
        // the dog by Nilesh's house.
        yield return LoadProp("cow_15_u1bj.glb", "Cow",
            new Vector3(8f, 0f, 14f), 0f, 1.8f,
            GltfModelLoader.NormalizeMode.ByWidth, GltfModelLoader.ColliderMode.FullBounds);
        // Replacement 2026-11-07: Boer Goat, 10.4k tris, FULL PBR
        // (the old 1.5k-tri toy goat is gone).
        // Placement fix 2026-11-07: the goat stood at (12, 13) — INSIDE
        // the tractor's footprint (tractor at (12, 11) spans z 8.5-13.5).
        // Now (13, 15): inside the crop field, clear of the tractor.
        yield return LoadProp("boer_goat__3d_model_free_7_dr1w.glb", "Goat",
            new Vector3(13f, 0f, 15f), 0f, 1f,
            GltfModelLoader.NormalizeMode.ByWidth, GltfModelLoader.ColliderMode.FullBounds);
        yield return LoadProp("water_buffalo_18_44fy.glb", "Buffalo",
            new Vector3(6f, 0f, 17f), 0f, 2f,
            GltfModelLoader.NormalizeMode.ByWidth, GltfModelLoader.ColliderMode.FullBounds);
        // Replacement 2026-11-07: Horse, 70.5k tris, albedo+normal
        // (the old 2.9k-tri horse rendered black).
        yield return LoadProp("horse_6_jrh4.glb", "Horse",
            new Vector3(5f, 0f, 6f), 0f, 2.4f,
            GltfModelLoader.NormalizeMode.ByWidth, GltfModelLoader.ColliderMode.FullBounds);
        // Wolf swap 2026-11-08: Dire Wolf Lite (Literock Studios, itch.io),
        // 9,991 tris, FULL PBR, rigged, 4 anims — replaces the old Shiba
        // (4,316 tris, 0 PBR; backed up to fix-backups/wolf-swap-20261108/).
        // Raw length 2.53 m; ByWidth 1.8 m => scale ~0.71, shoulder ~1.0 m.
        yield return LoadProp("Dire_Wolf_Lite.glb", "Wolf",
            new Vector3(-6f, 0f, 8f), 0f, 1.8f,
            GltfModelLoader.NormalizeMode.ByWidth, GltfModelLoader.ColliderMode.FullBounds,
            g => GltfModelLoader.PlayEmbeddedAnimation(g, "idle_loop"));
    }

    // Fix-push P2-18b: a pickup crate like the food one (Box + Pickup).
    private Pickup MakePickup(string name, Vector3 pos, string itemName)
    {
        GameObject crate = Box(name, pos, new Vector3(0.55f, 0.5f, 0.55f), matWood, villageRoot);
        Pickup p = crate.AddComponent<Pickup>();
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
            }
        }
        Debug.Log("[Village] Tractor PBR applied to " + seen.Count + " materials");
    }

    // Ambassador: full PBR but showroom-clean. Weather it with mud detail
    // maps (grime over the paint) + a darkened, slightly browned paint
    // colour + matte smoothness, so it reads as the broken-down car.
    // Lighting pass 2026-11-07: the first weathering went TOO FAR — the
    // car rendered near-black in the close-up. Now it is lightened
    // instead of darkened (paint x1.08/1.04/1.0), the mud detail is much
    // subtler (0.15, was 0.35) and the paint has a little sheen (0.5, was
    // 0.35) so it catches the gate lantern and reads as a dark-green
    // vintage car, not a black blob.
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
                // The Ambassador's dark-green paint lives in its TEXTURE
                // (blinn1's baseColorFactor is null — the material colour
                // is white), so the old 1.08x base-colour multiplier did
                // nothing (attempt-20/22: the car stayed black at night).
                // Brighten by raising the base colour ABOVE 1 — URP
                // multiplies the texture by it. blinn1 is the body paint
                // (+ wheels, same material — the tyres go dark grey,
                // acceptable); blinn2 is the glass/chrome and is left
                // alone so the windows stay dark.
                bool isBody = m.name.StartsWith("blinn1");
                m.SetFloat("_Smoothness", 0.5f);
                if (isBody)
                {
                    m.SetColor("_BaseColor", new Color(2.2f, 2.2f, 2.2f, 1f));
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
        Debug.Log("[Village] Ambassador weathering applied to " + seen.Count + " materials");
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
        yield return LoadProp("hindu_temple_88_62er.glb", "Temple",
            new Vector3(2f, 0f, -17f), 0f, 7f,
            GltfModelLoader.NormalizeMode.ByWidth, GltfModelLoader.ColliderMode.FullBounds);
        // School landmark (south-west, clear of House_3 and the jungle ring).
        yield return LoadProp("school-building_74_yizq.glb", "School",
            new Vector3(-16.5f, 0f, -14f), 25f, 7f,
            GltfModelLoader.NormalizeMode.ByWidth, GltfModelLoader.ColliderMode.FullBounds);
        // HAVELI (Meshy, free tier) — the big missing landmark, east of
        // the chowk facing it. Set back from the jungle ring so it only
        // brushes one ring tree. Fix-push P2-17: moved 1 m west (17 -> 16).
        yield return LoadProp("meshy_haveli_86_hvl.glb", "Haveli",
            new Vector3(16f, 0f, -3.5f), -90f, 8f,
            GltfModelLoader.NormalizeMode.ByWidth, GltfModelLoader.ColliderMode.FullBounds);
        // Shivling (cheap-wins 2026-11-07): the OLD 58 MB file was
        // defective (5 of 15 embedded textures zeroed) and is gone for
        // good — this is the NEW clean Mahadev (5.9 MB, 213k tris,
        // 1 texture, verified valid glTF v2), a seated Shiva murti.
        // Placed ON the shrine platform just inside the front (z=-16,
        // not -17): the shrine's solid interior block spans z -17.8 to
        // -16.2, so at -17 the murti sat embedded inside solid stone.
        yield return LoadProp("mahadev_001__1__2_t4b6.glb", "Shivling",
            new Vector3(2f, 0f, -16f), 0f, 1.0f,
            GltfModelLoader.NormalizeMode.ByHeight, GltfModelLoader.ColliderMode.None);
        // NANDI (cheap-wins 2026-11-07): user-sent granite Nandi,
        // decimated 441k -> 150k tris. Placed in the temple courtyard
        // NORTH of the gateway (z=-12.8), facing the temple (yaw 180):
        // the temple's own gateway at z=-13.82 carries a giant hanging
        // bell on the axis that hid the Nandi when it stood between the
        // gateway and the shrine. Pujari stands to the side so the
        // Nandi-shivling sightline stays clear.
        yield return LoadProp("granite_figure_of_a_nandi_bull_3_3z0y.glb", "Nandi",
            new Vector3(2f, 0f, -12.8f), 180f, 1.2f,
            GltfModelLoader.NormalizeMode.ByHeight, GltfModelLoader.ColliderMode.None);
        // Pujari at the temple entrance, facing the chowk.
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
            new Vector3(3.6f, 0f, -14.2f), 270f, 1.7f,
            GltfModelLoader.NormalizeMode.ByHeight, GltfModelLoader.ColliderMode.None);
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
            new Vector3(6.5f, 0f, 10.5f), -60f, 1.7f,
            GltfModelLoader.NormalizeMode.ByHeight, GltfModelLoader.ColliderMode.None);
        GltfModelLoader.PlayEmbeddedAnimations(GameObject.Find("Villager_Man"));
        // MEERA — second main character (rigged indian_woman_in_saree,
        // like Nilesh: static pose first, animation retargeting is a later
        // step, never a blocker). Stands on the chowk lane by Nilesh's
        // house, facing the player spawn / camera.
        yield return LoadProp("indian_woman_in_saree_84_8ssr.glb", "Meera",
            new Vector3(0.5f, 0f, 6.5f), 180f, 1.65f,
            GltfModelLoader.NormalizeMode.ByHeight, GltfModelLoader.ColliderMode.None);
        GltfModelLoader.PlayEmbeddedAnimations(GameObject.Find("Meera"));
        // MUKHIYA (Meshy, free tier) — the old village headman, standing
        // by the chowk near the well.
        yield return LoadProp("meshy_mukhiya_87_mky.glb", "Mukhiya",
            new Vector3(-3.5f, 0f, 2.5f), 45f, 1.7f,
            GltfModelLoader.NormalizeMode.ByHeight, GltfModelLoader.ColliderMode.None);
        // Chickens (user-made on Meshy) — placed ONCE (fix-push P0-4b:
        // was 3 copies of the same 2.4M-tri file; the file itself is
        // replaced with a decimated version by another worker).
        yield return LoadProp("chicken_73_eoja.glb", "Chicken_1",
            new Vector3(-6.5f, 0f, 8f), 40f, 0.6f,
            GltfModelLoader.NormalizeMode.ByWidth, GltfModelLoader.ColliderMode.None);
        // yield return LoadProp("chicken_73_eoja.glb", "Chicken_2",
        //     new Vector3(7f, 0f, 5.5f), -120f, 0.6f,
        //     GltfModelLoader.NormalizeMode.ByWidth, GltfModelLoader.ColliderMode.None);
        // yield return LoadProp("chicken_73_eoja.glb", "Chicken_3",
        //     new Vector3(11f, 0f, -4.5f), 200f, 0.6f,
        //     GltfModelLoader.NormalizeMode.ByWidth, GltfModelLoader.ColliderMode.None);
        // Small props: wicker basket by the well, wooden stand by Nilesh's door.
        yield return LoadProp("historic_miscellaneous_basket_wicker_old_03_xdwjadj_mid_51_y3o8.glb", "Basket",
            new Vector3(-3f, 0f, -1.5f), 30f, 0.7f,
            GltfModelLoader.NormalizeMode.ByWidth, GltfModelLoader.ColliderMode.None);
        yield return LoadProp("historic_miscellaneous_stand_wood_old_05_xeokah0_mid_50_69zs.glb", "WoodStand",
            new Vector3(-5.5f, 0f, 3.5f), -20f, 1.1f,
            GltfModelLoader.NormalizeMode.ByHeight, GltfModelLoader.ColliderMode.FullBounds);
        // Ground surfaces: sandstone by the temple lane, soil north of the
        // chowk, forest floor at the south jungle edge. Sandstone sits
        // 0.15 m sunk so it reads as rocky ground, not a raised white slab.
        yield return LoadProp("sandstone_rocky_ground_vd3oeic_mid_59_23jb.glb", "SandstoneGround",
            new Vector3(-2f, -0.15f, -18f), 0f, 5f,
            GltfModelLoader.NormalizeMode.ByWidth, GltfModelLoader.ColliderMode.None);
        yield return LoadProp("SoilGround/xdhhdhl_tier_2.gltf", "SoilGround",
            new Vector3(-4f, 0f, 12f), 0f, 8f,
            GltfModelLoader.NormalizeMode.ByWidth, GltfModelLoader.ColliderMode.None);
        yield return LoadProp("ForestFloor/sfjmafua_tier_2.gltf", "ForestFloor",
            new Vector3(6f, 0f, -18f), 0f, 6f,
            GltfModelLoader.NormalizeMode.ByWidth, GltfModelLoader.ColliderMode.None);
        // Shamshan (south-east corner): grave stone set + small grave.
        // Fix-push P2-17: moved 2 m east (17 -> 19 / 14.5 -> 16.5) so the
        // graves no longer overlap House_6's corner (~0.25 m before).
        yield return LoadProp("grave_stone_collection_64_7j05.glb", "GraveStones",
            new Vector3(19f, 0f, -18f), 0f, 3.5f,
            GltfModelLoader.NormalizeMode.ByWidth, GltfModelLoader.ColliderMode.FullBounds);
        // Fix-push P2-14: shamshan pyre (woodpile) + fire, by the graves.
        yield return LoadProp("woodpile_game_ready_asset_91_qdgo.glb", "PyreWood",
            new Vector3(18.5f, 0f, -19.5f), 20f, 1.8f,
            GltfModelLoader.NormalizeMode.ByWidth, GltfModelLoader.ColliderMode.FullBounds);
        yield return LoadProp("small_fire_place_90_23ou.glb", "ShamshanFire",
            new Vector3(16.8f, 0f, -19.5f), 0f, 1.5f,
            GltfModelLoader.NormalizeMode.ByWidth, GltfModelLoader.ColliderMode.FullBounds);
        // Phase B: BHUMIJA SHIKARA (replacement 2026-11-07) — a real
        // 12th-century Hoysala shrine scan (150k tris, albedo-only) as the
        // OLD ABANDONED temple at the north-east jungle edge, once only,
        // far from the chowk and the main hindu temple (2, -17). A place
        // nobody goes to. (User's FINAL choice after two flips; Old Ruined
        // Temple stays on disk as backup only — do NOT place it.)
        yield return LoadProp("Bhumija/scene.gltf", "AngkorTemple",
            new Vector3(21f, 0f, 12f), -90f, 8f,
            GltfModelLoader.NormalizeMode.ByWidth, GltfModelLoader.ColliderMode.FullBounds);
        // Follow-up 2026-11-07: Abandoned hospital building (54,041 tris,
        // 21 meshes, 11 materials, ALL full PBR) replaces the decimated
        // kralls clinic (149,968 tris, 724 meshes / 60 materials) — same
        // spot at the west village edge facing the chowk. 21 vs 724 meshes
        // is a huge draw-call win.
        yield return LoadProp("abandoned_hospital_building_20_2516.glb", "Clinic",
            new Vector3(-18f, 0f, -2f), 90f, 6f,
            GltfModelLoader.NormalizeMode.ByWidth, GltfModelLoader.ColliderMode.FullBounds);
        // Wooden dock/pier at the north village edge (no river mesh yet —
        // it reads as the pier at the water's edge; river comes later).
        yield return LoadProp("dock_pier_78_n66e.glb", "Dock",
            new Vector3(-7f, 0f, 19f), 0f, 6f,
            GltfModelLoader.NormalizeMode.ByWidth, GltfModelLoader.ColliderMode.None);
        // Lanterns: brass diya inside Nilesh's house, chowk lantern by the lamp.
        yield return LoadProp("brass_diya_lantern/brass_diya_lantern_1k.gltf", "DiyaLantern",
            new Vector3(-10.5f, 0f, 4.5f), 0f, 0.45f,
            GltfModelLoader.NormalizeMode.ByHeight, GltfModelLoader.ColliderMode.None);
        yield return LoadProp("Lantern_01/Lantern_01_1k.gltf", "ChowkLantern",
            new Vector3(3.2f, 0f, 2.2f), 0f, 0.9f,
            GltfModelLoader.NormalizeMode.ByHeight, GltfModelLoader.ColliderMode.None);
        // Neem substitute (PlantCatalog Realistic HD Chinaberry tree,
        // CC-BY, 221,798 tris): ONE instance only (cheap-wins 2026-11-07:
        // was 2 instances = 444k tris; the second one on the north lane
        // is removed — the tree itself is the best in the project, the
        // count was the cost). South of the chowk. Trunk-only collider
        // like the other real trees, so the canopy never walls off an area.
        yield return LoadProp("realistic_hd_chinaberry_tree_1330_83_1wiy.glb", "Tree_Neem1",
            new Vector3(4f, 0f, -10f), 0f, 6f,
            GltfModelLoader.NormalizeMode.ByHeight, GltfModelLoader.ColliderMode.Trunk);
        // FBX props (native Unity import, under Assets/Resources/):
        // fallen pine at the north-west jungle edge, small grave in the shamshan.
        RunStep("FallenPine", delegate
        {
            GameObject pine = InstantiateResourceModel("FallenPine/FallenPine", "FallenPine",
                new Vector3(-18f, 0f, 18f), 40f, 4f, false);
            if (pine == null) Debug.LogWarning("[Village] Fallen pine FBX not placed");
        });
        RunStep("SmallGrave", delegate
        {
            // SmallGrave FBX is a FLAT slab (raw 1.1 wide x 0.23 thick x 2.2
            // long) — normalise by WIDTH to a real 2.2 m grave length.
            // ByHeight (old: 1.2 m tall) scaled it to a 13 m wide slab.
            GameObject grave = InstantiateResourceModel("SmallGrave/SmallGrave", "SmallGrave",
                new Vector3(16.5f, 0f, -18f), -30f, 2.2f, false);
            if (grave == null) Debug.LogWarning("[Village] Small grave FBX not placed");
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
        moonMaterial = MakeEmissive("Moon", new Color(0.85f, 0.88f, 0.95f), new Color(0.25f, 0.25f, 0.3f));
        moonMaterial.SetFloat("_Fog", 0f);
        moonMaterial.DisableKeyword("_FOG");
        GameObject moon = Sphere("Moon", new Vector3(-28f, 26f, -42f), new Vector3(8f, 8f, 8f), moonMaterial, villageRoot);
        Destroy(moon.GetComponent<Collider>());
        moonGlow = MakeGlowQuad("MoonGlow", new Vector3(-28f, 26f, -42f), 28f, new Color(0.7f, 0.75f, 0.95f, 0.55f));
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
            new Vector3(-2f, 0f, -19f), 0f, 2.3f,
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
        AddPointLight("MonsterLight", new Vector3(-2f, 2f, -18f),
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
        m.transform.position = new Vector3(-2f, 0f, -19f);

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
        // Gate + Ambassador (car at (1, 16), gate at (0, 20.5)). The
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
            QualitySettings.shadowDistance = 45f;
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
            probe.resolution = 128;
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
