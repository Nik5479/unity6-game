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
                if (pos.y < b.max.y && pos.y + 1.8f > b.min.y)
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
///    models: school, temple (shivling + pujari), chickens, dock, basket,
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
    public Transform doorFront;
    public Pickup foodPickup;
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
    private Material matLeaf;
    private Material matLeaf2;
    private Material matLeaf3;
    private Material matTrunk;
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

        // Each step is isolated: one failing step must not kill the village.
        AllModelsLoaded = false;
        RunStep("Materials", BuildMaterials);
        RunStep("Ground", BuildGround);
        RunStep("PlayerFigure", BuildPlayerFigure);
        // The rest loads async: real .glb models come in via glTFast, and
        // the systems (door/monster/UI) are wired only after the village
        // exists. Intro + camera check start at the end of the coroutine.
        StartCoroutine(BuildVillageAsync());
    }

    // Phase 3: async village build. Real Sketchfab models load one by one
    // (each with a primitive fallback); a failed model never kills the
    // village. SetupSystems/SceneAmbience run after everything is placed.
    private System.Collections.IEnumerator BuildVillageAsync()
    {
        bool ok = false;
        try
        {
            yield return BuildPlayerModelAsync();
            yield return BuildHousesAsync();
            yield return BuildWellAndLampAsync();
            RunStep("Field", BuildField);
            yield return BuildJungleAndFencesAsync();
            yield return BuildPropsAsync();
            RunStep("Temple", BuildTemple);
            yield return BuildNewModelsAsync();
            RunStep("Moon", BuildMoon);
            RunStep("SetupSystems", SetupSystems);
            RunStep("SceneAmbience", SetupSceneAmbience);
            ok = true;
        }
        finally
        {
            // Always signal, even on failure, so the CI test never waits
            // forever — it proceeds with whatever did load.
            AllModelsLoaded = true;
            GameObject signal = new GameObject("ModelsLoaded");
            signal.transform.SetParent(villageRoot, false);
        }
        if (ok)
        {
            SetBootStatus("Village: " + CountVillageObjects() + " objects built"
                + (buildErrors > 0 ? " (" + buildErrors + " step errors — see log)" : ""));
            StartCoroutine(IntroSequence());
            StartCoroutine(VerifyCameraView());
        }
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
            Debug.LogError("[Village] Build step FAILED: " + name + " — "
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
            // Same bar as the CI screenshot test (>= 3 houses): if the
            // view fails, reset to the starter view instead of showing
            // an empty frame.
            if (!playerVisible || housesVisible < 3)
            {
                Debug.LogError("[Village] Camera view check FAILED — resetting to starter view");
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
                Debug.LogError("[Village] Material asset missing: Materials/" + name);
            }
            return m;
        }
        catch (System.Exception e)
        {
            Debug.LogError("[Village] Material load failed: " + name + " — " + e.Message);
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
        try { s = Shader.Find("Standard"); } catch (System.Exception) { s = null; }
        if (s != null) standardShader = s;
        return s;
    }

    private Material MakeMat(string name, Texture2D tex, Color tint, float smoothness, Vector2 tiling)
    {
        Shader s = GetStandardShader();
        if (s == null)
        {
            Debug.LogError("[Village] Standard shader not available for " + name);
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
        return m;
    }

    private Material MakeFlat(string name, Color color, float smoothness)
    {
        Shader s = GetStandardShader();
        if (s == null)
        {
            Debug.LogError("[Village] Standard shader not available for " + name);
            return null;
        }
        Material m = new Material(s);
        m.name = name;
        m.color = color;
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_Glossiness", smoothness);
        return m;
    }

    private Material MakeEmissive(string name, Color color, Color emission)
    {
        Shader s = GetStandardShader();
        if (s == null)
        {
            Debug.LogError("[Village] Standard shader not available for " + name);
            return null;
        }
        Material m = new Material(s);
        m.name = name;
        m.color = color;
        m.SetColor("_EmissionColor", emission);
        m.EnableKeyword("_EMISSION");
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_Glossiness", 0.2f);
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
        if (matGround == null) matGround = MakeMat("GroundMud", LoadTex("ground_mud"), new Color(0.85f, 0.78f, 0.68f), 0.05f, new Vector2(14f, 14f));
        if (matPlaster == null) matPlaster = MakeMat("WallPlaster", LoadTex("wall_plaster"), new Color(0.92f, 0.85f, 0.72f), 0.15f, new Vector2(2f, 1.5f));
        if (matWood == null) matWood = MakeMat("WoodPlanks", LoadTex("wood_planks"), new Color(0.75f, 0.6f, 0.42f), 0.3f, new Vector2(1f, 1f));
        if (matRoof == null) matRoof = MakeMat("RoofTiles", LoadTex("roof_tiles"), new Color(0.95f, 0.8f, 0.62f), 0.25f, new Vector2(3f, 2f));
        if (matStone == null) matStone = MakeMat("StoneWall", LoadTex("stone_wall"), new Color(0.8f, 0.78f, 0.72f), 0.1f, new Vector2(2f, 1f));
        if (matGrass == null) matGrass = MakeMat("FieldGrass", LoadTex("field_grass"), new Color(0.7f, 0.8f, 0.55f), 0.05f, new Vector2(4f, 3f));

        // Safety net: re-assign textures by name if a loaded .mat lost its reference.
        AssignTex(matGround, "ground_mud", new Vector2(14f, 14f));
        AssignTex(matPlaster, "wall_plaster", new Vector2(2f, 1.5f));
        AssignTex(matWood, "wood_planks", new Vector2(1f, 1f));
        AssignTex(matRoof, "roof_tiles", new Vector2(3f, 2f));
        AssignTex(matStone, "stone_wall", new Vector2(2f, 1f));
        AssignTex(matGrass, "field_grass", new Vector2(4f, 3f));

        // Resolve the Standard shader once for the flat/emissive materials.
        standardShader = (matGround != null && matGround.shader != null) ? matGround.shader : Shader.Find("Standard");
        if (standardShader == null)
        {
            Debug.LogError("[Village] Standard shader NOT found even after .mat loads!");
        }

        matLeaf = MakeFlat("Leaves", new Color(0.10f, 0.22f, 0.08f), 0.05f);
        matLeaf2 = MakeFlat("Leaves2", new Color(0.13f, 0.28f, 0.09f), 0.05f);
        matLeaf3 = MakeFlat("Leaves3", new Color(0.07f, 0.17f, 0.06f), 0.05f);
        matPlaster2 = MakeMat("WallPlaster2", LoadTex("wall_plaster"), new Color(0.85f, 0.76f, 0.62f), 0.15f, new Vector2(2f, 1.5f));
        matTrunk = MakeFlat("Trunk", new Color(0.16f, 0.10f, 0.06f), 0.1f);
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
        matBrick = MakeMat("Brick", LoadTex("brick_4_diff_1k"), Color.white, 0.1f, new Vector2(2f, 1f));
        matThatch = MakeMat("Thatch", LoadTex("thatch_roof_angled_diff_1k"), new Color(0.9f, 0.85f, 0.7f), 0.05f, new Vector2(2f, 2f));
        matMudDry = MakeMat("MudDry", LoadTex("brown_mud_dry_diff_1k"), new Color(0.85f, 0.78f, 0.68f), 0.05f, new Vector2(2f, 2f));
        matCobble = MakeMat("Cobble", LoadTex("cobblestone_floor_04_diff_1k"), new Color(0.85f, 0.84f, 0.8f), 0.1f, new Vector2(4f, 4f));
    }

    // Transparent flat material (ground shadow blobs, dirt patches).
    private Material MakeTransparent(string name, Color color)
    {
        Shader s = GetStandardShader();
        if (s == null)
        {
            Debug.LogError("[Village] Standard shader not available for " + name);
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
        m.renderQueue = 3000;
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_Glossiness", 0f);
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
        m.triangles = new int[] { 0, 2, 1, 0, 3, 2 };
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
        Material dirt = (matMudDry != null)
            ? matMudDry
            : MakeFlat("DirtPath", new Color(0.45f, 0.35f, 0.24f), 0.05f);
        for (int i = 0; i < 6; i++)
        {
            float z = -10f + i * 4f;
            GameObject patch = Box("DirtPath", new Vector3(0f, 0.03f, z), new Vector3(2.4f, 0.04f, 3.4f), dirt, villageRoot);
            Destroy(patch.GetComponent<Collider>());
        }

        // Phase 4: cobbled chowk circle around the village centre.
        if (matCobble != null)
        {
            GameObject chowk = Cylinder("ChowkCobble", new Vector3(0f, 0.02f, 0f), new Vector3(9f, 0.04f, 9f), matCobble, villageRoot);
            Destroy(chowk.GetComponent<Collider>());
        }

        // Grass tufts near the field edge.
        Material tuftMat = MakeFlat("GrassTuft", new Color(0.18f, 0.32f, 0.10f), 0.05f);
        for (int i = 0; i < 16; i++)
        {
            float x = 4f + (i % 8) * 1.4f;
            float z = 12.5f + (i / 8) * 1.2f + ((i % 3) * 0.4f);
            GameObject tuft = Box("GrassTuft", new Vector3(x, 0.2f, z), new Vector3(0.3f, 0.4f, 0.3f), tuftMat, villageRoot);
            tuft.transform.rotation = Quaternion.Euler(0f, i * 23f, 0f);
            Destroy(tuft.GetComponent<Collider>());
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
        yield return BuildRealHouse("House_2", "village_house_for_game_20_lafr.glb",
            new Vector3(9f, 0f, 8f), 15f, 5.5f);
        yield return BuildRealHouse("House_3", "village_hut_house_21_tyn9.glb",
            new Vector3(-9f, 0f, -9f), -10f, 4.5f);
        yield return BuildRealHouse("House_4", "hut_with_thatched_pointed_roof_22_96py.glb",
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
        Vector3 wellPos = new Vector3(-4.5f, 0f, -3.5f);
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

        // Crop rows: small dark-green tufts.
        Material cropMat = MakeFlat("Crop", new Color(0.16f, 0.35f, 0.10f), 0.05f);
        for (int r = 0; r < 4; r++)
        {
            for (int c = 0; c < 9; c++)
            {
                Vector3 p = fieldPos + new Vector3(-4f + c * 1.0f, 0.25f, -2.2f + r * 1.4f);
                Box("Crop", p, new Vector3(0.35f, 0.5f, 0.35f), cropMat, villageRoot);
            }
        }
    }

    private void BuildTree(Vector3 pos, float scale)
    {
        Transform root = new GameObject("Tree").transform;
        root.SetParent(villageRoot, false);
        root.position = pos;
        // Slight deterministic lean/rotation so trees are not clones.
        float lean = Mathf.Sin(pos.x * 12.9898f + pos.z * 78.233f) * 4f;
        root.rotation = Quaternion.Euler(lean * 0.5f, Mathf.Abs(Mathf.Sin(pos.x * 3.7f)) * 180f, lean);

        // Trunk (tapered look: two stacked cylinders).
        Cylinder("Trunk", pos + new Vector3(0f, 1.1f * scale, 0f), new Vector3(0.35f * scale, 2.2f * scale, 0.35f * scale), matTrunk, root);
        Cylinder("TrunkTop", pos + new Vector3(0f, 2.1f * scale, 0f), new Vector3(0.22f * scale, 0.6f * scale, 0.22f * scale), matTrunk, root);
        // Canopy: three overlapping blobs, varied greens — no more single lollipop ball.
        Sphere("CanopyA", pos + new Vector3(0f, 2.9f * scale, 0f), new Vector3(2.6f * scale, 1.9f * scale, 2.6f * scale), matLeaf, root);
        Sphere("CanopyB", pos + new Vector3(0.85f * scale, 2.35f * scale, 0.4f * scale), new Vector3(1.6f * scale, 1.15f * scale, 1.6f * scale), matLeaf2, root);
        Sphere("CanopyC", pos + new Vector3(-0.75f * scale, 2.45f * scale, -0.35f * scale), new Vector3(1.45f * scale, 1.05f * scale, 1.45f * scale), matLeaf3, root);
        Sphere("CanopyD", pos + new Vector3(0.1f * scale, 3.5f * scale, -0.2f * scale), new Vector3(1.3f * scale, 0.9f * scale, 1.3f * scale), matLeaf2, root);
        // Ground shadow blob under the tree.
        if (matShadowBlob != null)
        {
            GameObject blob = Cylinder("TreeShadow", pos + new Vector3(0f, 0.03f, 0f), new Vector3(3.2f * scale, 0.02f, 3.2f * scale), matShadowBlob, root);
            Destroy(blob.GetComponent<Collider>());
        }
        Block(pos + new Vector3(0f, 1.1f, 0f), new Vector3(0.6f, 2.2f, 0.6f));
    }

    private System.Collections.IEnumerator BuildJungleAndFencesAsync()
    {
        // Jungle edge: dark trees ringing the village.
        for (int x = -20; x <= 20; x += 4)
        {
            BuildTree(new Vector3(x, 0f, -21f), 1.1f + (Mathf.Abs(x) % 3) * 0.15f);
            BuildTree(new Vector3(x, 0f, 21f), 1.0f + (Mathf.Abs(x) % 2) * 0.2f);
        }
        for (int z = -16; z <= 16; z += 5)
        {
            BuildTree(new Vector3(-21f, 0f, z), 1.2f);
            BuildTree(new Vector3(21f, 0f, z), 1.0f + (Mathf.Abs(z) % 3) * 0.15f);
        }
        // Inner trees: REAL models (mango, banyan, palm + a tree cluster).
        // Normalised by height — trees are taller than wide.
        yield return LoadRealTree("mango_tree_3_sl11.glb", "Tree_Mango",
            new Vector3(-16f, 0f, -2f), 6f, GltfModelLoader.ColliderMode.Trunk);
        yield return LoadRealTree("banyan_tree_001_26_fyf3.glb", "Tree_Banyan",
            new Vector3(16f, 0f, 2f), 7f, GltfModelLoader.ColliderMode.Trunk);
        yield return LoadRealTree("palm_trees_27_x7l3.glb", "Tree_Palm",
            new Vector3(-4f, 0f, 18f), 5f, GltfModelLoader.ColliderMode.FullBounds);
        yield return LoadRealTree("trees_25_wae0.glb", "Tree_Pack",
            new Vector3(-18f, 0f, -18f), 8f, GltfModelLoader.ColliderMode.FullBounds);

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
        GltfModelLoader.ColliderMode colMode)
    {
        GameObject t = null;
        yield return GltfModelLoader.LoadModel(
            file, villageRoot, goName, pos, 0f, height,
            GltfModelLoader.NormalizeMode.ByHeight, colMode, g => t = g);
        if (t == null)
        {
            BuildTree(pos, 1.1f);
        }
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
    private System.Collections.IEnumerator BuildPropsAsync()
    {
        // Vehicles / big props.
        yield return LoadProp("medieval_wooden_cart_1_jab4.glb", "Cart",
            new Vector3(5.5f, 0f, 3.0f), 0f, 2.2f,
            GltfModelLoader.NormalizeMode.ByWidth, GltfModelLoader.ColliderMode.FullBounds);
        yield return LoadProp("an_old_bullock_cart_14_bxqa.glb", "BullockCart",
            new Vector3(-6f, 0f, -4f), 0f, 2.5f,
            GltfModelLoader.NormalizeMode.ByWidth, GltfModelLoader.ColliderMode.FullBounds);
        yield return LoadProp("tractor_12_35hh.glb", "Tractor",
            new Vector3(12f, 0f, 11f), 30f, 3f,
            GltfModelLoader.NormalizeMode.ByWidth, GltfModelLoader.ColliderMode.FullBounds);
        // Ancient gate at the south lane entrance — no collider, so the
        // player can walk through the arch.
        yield return LoadProp("ancient_gate_11_4q5m.glb", "Gate",
            new Vector3(0f, 0f, 20.5f), 0f, 4f,
            GltfModelLoader.NormalizeMode.ByWidth, GltfModelLoader.ColliderMode.None);
        // Small props near the houses.
        yield return LoadProp("handpump_3d_39_aah5.glb", "Handpump",
            new Vector3(5f, 0f, 9f), 0f, 1.3f,
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
        yield return LoadProp("goat_17_a8s1.glb", "Goat",
            new Vector3(12f, 0f, 13f), 0f, 1f,
            GltfModelLoader.NormalizeMode.ByWidth, GltfModelLoader.ColliderMode.FullBounds);
        yield return LoadProp("water_buffalo_18_44fy.glb", "Buffalo",
            new Vector3(6f, 0f, 17f), 0f, 2f,
            GltfModelLoader.NormalizeMode.ByWidth, GltfModelLoader.ColliderMode.FullBounds);
        yield return LoadProp("horse_riggedgame_ready_16_2abc.glb", "Horse",
            new Vector3(5f, 0f, 6f), 0f, 2.4f,
            GltfModelLoader.NormalizeMode.ByWidth, GltfModelLoader.ColliderMode.FullBounds);
        yield return LoadProp("shiba_19_54zt.glb", "Dog",
            new Vector3(-6f, 0f, 8f), 0f, 0.8f,
            GltfModelLoader.NormalizeMode.ByWidth, GltfModelLoader.ColliderMode.FullBounds);
    }

    private System.Collections.IEnumerator LoadProp(
        string file, string goName, Vector3 pos, float yaw, float size,
        GltfModelLoader.NormalizeMode normMode, GltfModelLoader.ColliderMode colMode)
    {
        GameObject p = null;
        yield return GltfModelLoader.LoadModel(
            file, villageRoot, goName, pos, yaw, size, normMode, colMode, g => p = g);
        if (p == null)
        {
            Debug.LogWarning("[Village] Prop skipped (load failed): " + file);
        }
    }

    // ------------------------------------------------------------------
    // Phase 4: temple + new real models.
    // ------------------------------------------------------------------
    // Primitive temple (south of the chowk, facing north): brick walls,
    // stone plinth, thatch roof. The real shivling .glb is placed inside
    // it and the pujari .glb stands at its entrance in BuildNewModelsAsync.
    private void BuildTemple()
    {
        Vector3 c = new Vector3(2f, 0f, -17f);
        Material brick = (matBrick != null) ? matBrick : matPlaster;
        Material thatch = (matThatch != null) ? matThatch : matRoof;
        Transform root = new GameObject("Temple").transform;
        root.SetParent(villageRoot, false);
        root.position = c;

        Box("Temple_Plinth", c + new Vector3(0f, 0.15f, 0f), new Vector3(5.5f, 0.3f, 5.5f), matStone, root);
        // Back (south) + side walls; front (north) stays open.
        Box("Temple_WallBack", c + new Vector3(0f, 1.8f, -2.35f), new Vector3(5f, 3f, 0.3f), brick, root);
        Box("Temple_WallSideA", c + new Vector3(-2.35f, 1.8f, 0f), new Vector3(0.3f, 3f, 4.7f), brick, root);
        Box("Temple_WallSideB", c + new Vector3(2.35f, 1.8f, 0f), new Vector3(0.3f, 3f, 4.7f), brick, root);
        // Front pillars.
        Box("Temple_PillarA", c + new Vector3(-2.35f, 1.8f, 2.35f), new Vector3(0.4f, 3f, 0.4f), matStone, root);
        Box("Temple_PillarB", c + new Vector3(2.35f, 1.8f, 2.35f), new Vector3(0.4f, 3f, 0.4f), matStone, root);
        // Sloped thatch roof: two panels + ridge + finial.
        GameObject panelA = Box("Temple_RoofA", c + new Vector3(0f, 3.9f, 1.2f), new Vector3(6f, 0.12f, 3.2f), thatch, root);
        panelA.transform.rotation = Quaternion.Euler(30f, 0f, 0f);
        GameObject panelB = Box("Temple_RoofB", c + new Vector3(0f, 3.9f, -1.2f), new Vector3(6f, 0.12f, 3.2f), thatch, root);
        panelB.transform.rotation = Quaternion.Euler(-30f, 0f, 0f);
        Box("Temple_Ridge", c + new Vector3(0f, 4.55f, 0f), new Vector3(6f, 0.18f, 0.25f), matWood, root);
        Sphere("Temple_Finial", c + new Vector3(0f, 4.9f, 0f), new Vector3(0.4f, 0.5f, 0.4f), matLampGlass, root);
        // Collision: back + side walls (front stays open so the player
        // can walk in to the shivling).
        Block(c + new Vector3(0f, 1.5f, -2.35f), new Vector3(5f, 3f, 0.5f));
        Block(c + new Vector3(-2.35f, 1.5f, 0f), new Vector3(0.5f, 3f, 4.7f));
        Block(c + new Vector3(2.35f, 1.5f, 0f), new Vector3(0.5f, 3f, 4.7f));
    }

    // Phase 4: every new real model. Each load is independent — one
    // failure is logged and skipped, never fatal. .gltf bundles (soil,
    // forest floor, lanterns) load from their subfolders under
    // StreamingAssets/Models/ via the same glTFast path as .glb.
    private System.Collections.IEnumerator BuildNewModelsAsync()
    {
        // School landmark (south-west, clear of House_3 and the jungle ring).
        yield return LoadProp("school-building_74_yizq.glb", "School",
            new Vector3(-16.5f, 0f, -14f), 25f, 7f,
            GltfModelLoader.NormalizeMode.ByWidth, GltfModelLoader.ColliderMode.FullBounds);
        // HAVELI (Meshy, free tier) — the big missing landmark, east of
        // the chowk facing it. Set back from the jungle ring so it only
        // brushes one ring tree.
        yield return LoadProp("meshy_haveli_86_hvl.glb", "Haveli",
            new Vector3(17f, 0f, -3.5f), -90f, 8f,
            GltfModelLoader.NormalizeMode.ByWidth, GltfModelLoader.ColliderMode.FullBounds);
        // Shivling INSIDE the temple (once only — the file is 58 MB).
        // NOTE (Phase 4): the .glb is EXCLUDED from this push — GitHub's
        // create-blob API rejects it (60 MB, HTTP 422; proven API limit
        // is between 35 MB pass and 45 MB fail) AND the source file is
        // defective (5 of 15 embedded textures are zeroed/garbage).
        // This LoadProp will log + skip gracefully, so the temple is
        // empty this run; the shivling needs a fresh Fab re-download
        // and a proper upload path (local clone push / LFS) in a later
        // push. Do NOT re-add the file to the push list until then.
        yield return LoadProp("mahadev_shiv_lingum_c4d_71_52qp.glb", "Shivling",
            new Vector3(2f, 0f, -17f), 0f, 1.0f,
            GltfModelLoader.NormalizeMode.ByHeight, GltfModelLoader.ColliderMode.FullBounds);
        // Pujari at the temple entrance, facing the chowk. NOTE: his face
        // is flat grey with no facial texture — he stands at mid-ground
        // distance on purpose, never in close-up.
        yield return LoadProp("pujari-mahant-guru_72_b1y1.glb", "Pujari",
            new Vector3(2f, 0f, -14.2f), 0f, 1.7f,
            GltfModelLoader.NormalizeMode.ByHeight, GltfModelLoader.ColliderMode.None);
        // Second villager (rigged indian_man, sunglasses variant) near the handpump.
        yield return LoadProp("indian_man__sunglasses_76_gmed.glb", "Villager_Man",
            new Vector3(6.5f, 0f, 10.5f), -60f, 1.7f,
            GltfModelLoader.NormalizeMode.ByHeight, GltfModelLoader.ColliderMode.None);
        // MEERA — second main character (rigged indian_woman_in_saree,
        // like Nilesh: static pose first, animation retargeting is a later
        // step, never a blocker). Stands on the chowk lane by Nilesh's
        // house, facing the player spawn / camera.
        yield return LoadProp("indian_woman_in_saree_84_8ssr.glb", "Meera",
            new Vector3(0.5f, 0f, 6.5f), 180f, 1.65f,
            GltfModelLoader.NormalizeMode.ByHeight, GltfModelLoader.ColliderMode.None);
        // MUKHIYA (Meshy, free tier) — the old village headman, standing
        // by the chowk near the well.
        yield return LoadProp("meshy_mukhiya_87_mky.glb", "Mukhiya",
            new Vector3(-3.5f, 0f, 2.5f), 45f, 1.7f,
            GltfModelLoader.NormalizeMode.ByHeight, GltfModelLoader.ColliderMode.None);
        // Chickens (user-made on Meshy) — 2-3 max, near houses.
        yield return LoadProp("chicken_73_eoja.glb", "Chicken_1",
            new Vector3(-6.5f, 0f, 8f), 40f, 0.6f,
            GltfModelLoader.NormalizeMode.ByWidth, GltfModelLoader.ColliderMode.None);
        yield return LoadProp("chicken_73_eoja.glb", "Chicken_2",
            new Vector3(7f, 0f, 5.5f), -120f, 0.6f,
            GltfModelLoader.NormalizeMode.ByWidth, GltfModelLoader.ColliderMode.None);
        yield return LoadProp("chicken_73_eoja.glb", "Chicken_3",
            new Vector3(11f, 0f, -4.5f), 200f, 0.6f,
            GltfModelLoader.NormalizeMode.ByWidth, GltfModelLoader.ColliderMode.None);
        // Small props: wicker basket by the well, wooden stand by Nilesh's door.
        yield return LoadProp("historic_miscellaneous_basket_wicker_old_03_xdwjadj_mid_51_y3o8.glb", "Basket",
            new Vector3(-3f, 0f, -1.5f), 30f, 0.7f,
            GltfModelLoader.NormalizeMode.ByWidth, GltfModelLoader.ColliderMode.None);
        yield return LoadProp("historic_miscellaneous_stand_wood_old_05_xeokah0_mid_50_69zs.glb", "WoodStand",
            new Vector3(-5.5f, 0f, 3.5f), -20f, 1.1f,
            GltfModelLoader.NormalizeMode.ByHeight, GltfModelLoader.ColliderMode.FullBounds);
        // Ground surfaces: sandstone by the temple lane, soil north of the
        // chowk, forest floor at the south jungle edge.
        yield return LoadProp("sandstone_rocky_ground_vd3oeic_mid_59_23jb.glb", "SandstoneGround",
            new Vector3(-2f, 0f, -18f), 0f, 5f,
            GltfModelLoader.NormalizeMode.ByWidth, GltfModelLoader.ColliderMode.None);
        yield return LoadProp("SoilGround/xdhhdhl_tier_2.gltf", "SoilGround",
            new Vector3(-4f, 0f, 12f), 0f, 8f,
            GltfModelLoader.NormalizeMode.ByWidth, GltfModelLoader.ColliderMode.None);
        yield return LoadProp("ForestFloor/sfjmafua_tier_2.gltf", "ForestFloor",
            new Vector3(6f, 0f, -18f), 0f, 6f,
            GltfModelLoader.NormalizeMode.ByWidth, GltfModelLoader.ColliderMode.None);
        // Shamshan (south-east corner): grave stone set + small grave.
        yield return LoadProp("grave_stone_collection_64_7j05.glb", "GraveStones",
            new Vector3(17f, 0f, -18f), 0f, 3.5f,
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
        // CC-BY, 221,798 tris): 2 instances — one south of the chowk,
        // one on the north lane. Trunk-only colliders like the other
        // real trees, so the canopy never walls off an area.
        yield return LoadProp("realistic_hd_chinaberry_tree_1330_83_1wiy.glb", "Tree_Neem1",
            new Vector3(4f, 0f, -10f), 0f, 6f,
            GltfModelLoader.NormalizeMode.ByHeight, GltfModelLoader.ColliderMode.Trunk);
        yield return LoadProp("realistic_hd_chinaberry_tree_1330_83_1wiy.glb", "Tree_Neem2",
            new Vector3(-3f, 0f, 15.5f), 120f, 6f,
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
            GameObject grave = InstantiateResourceModel("SmallGrave/SmallGrave", "SmallGrave",
                new Vector3(14.5f, 0f, -18f), -30f, 1.2f, true);
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
        moonMaterial = MakeEmissive("Moon", new Color(0.85f, 0.88f, 0.95f), new Color(0.25f, 0.25f, 0.3f));
        GameObject moon = Sphere("Moon", new Vector3(-28f, 26f, -42f), new Vector3(6f, 6f, 6f), moonMaterial, villageRoot);
        Destroy(moon.GetComponent<Collider>());
        moonGlow = MakeGlowQuad("MoonGlow", new Vector3(-28f, 26f, -42f), 16f, new Color(0.7f, 0.75f, 0.95f, 0.35f));
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

        // Third-person follow camera.
        CameraFollow follow = mainCam.gameObject.AddComponent<CameraFollow>();
        follow.target = (player != null) ? player.transform : null;

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

        MobileControls ui = gameObject.AddComponent<MobileControls>();
        ui.player = pc;
        ui.torch = torch;
        ui.gameManager = gm;
        gm.ui = ui;

        cycle.OnNightStart = delegate { knock.BeginNight(); };
        cycle.OnDawn = delegate { gm.Survived(); };
    }

    private GameObject BuildMonster()
    {
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
            mainCam.backgroundColor = new Color(0.53f, 0.81f, 0.98f);
            mainCam.farClipPlane = 300f;
        }
    }

    private System.Collections.IEnumerator IntroSequence()
    {
        yield return new WaitForSeconds(0.8f);
        GameManager gm = GetComponent<GameManager>();
        if (gm == null) yield break;
        gm.ShowSubtitle("Bhairavpur... ye gaon shaam hote hi darwaza band kar leta hai.", 4f);
        yield return new WaitForSeconds(4.2f);
        gm.ShowSubtitle("Mukhiya: Raat me koi bhi darwaza khatkhataye to kholna mat!", 4f);
        yield return new WaitForSeconds(4.2f);
        gm.SetObjective("Din: Kuaan ke paas se khana uthao, phir ghar ke andar jao.");
    }
}
