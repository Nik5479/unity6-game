using System.Collections.Generic;
using UnityEngine;

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
/// THE FORGOTTEN VILLAGE — Phase 1 village builder.
/// Builds the whole Phase 1 scene at runtime from primitives with
/// Poly Haven CC0 PBR textures (Assets/Resources/Textures):
///  - 6 houses around the main chowk (one enterable: Nilesh's house)
///  - well, chowk lamp, small field, jungle edge (trees), fences, props
///  - working door (hinge), monster figure, food pickup near the well
///  - player placeholder figure (dark-clothed capsule figure — TEMPORARY,
///    no realistic human model is available yet)
/// Materials are created at runtime from Resources textures so no
/// hand-written .mat GUID references are needed.
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
    private Material matWood;
    private Material matRoof;
    private Material matStone;
    private Material matGrass;
    private Material matLeaf;
    private Material matTrunk;
    private Material matDark;
    private Material matGlass;
    private Material matSkin;
    private Material matKurta;
    private Material matEye;
    private Material matLampGlass;

    private Transform villageRoot;
    private Camera mainCam;

    void Awake()
    {
        VillageColliders.Clear();
        mainCam = Camera.main;
        villageRoot = new GameObject("Village").transform;

        BuildMaterials();
        BuildGround();
        BuildPlayerFigure();
        BuildHouses();
        BuildWellAndLamp();
        BuildField();
        BuildJungleAndFences();
        BuildProps();
        BuildMoon();

        SetupSystems();
        SetupSceneAmbience();
        StartCoroutine(IntroSequence());
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
    // Materials (Standard shader PBR; albedo from Poly Haven CC0 1k maps)
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

    private Material MakeMat(string name, Texture2D tex, Color tint, float smoothness, Vector2 tiling)
    {
        Material m = new Material(Shader.Find("Standard"));
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
        Material m = new Material(Shader.Find("Standard"));
        m.name = name;
        m.color = color;
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_Glossiness", smoothness);
        return m;
    }

    private Material MakeEmissive(string name, Color color, Color emission)
    {
        Material m = new Material(Shader.Find("Standard"));
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
        matGround = MakeMat("GroundMud", LoadTex("ground_mud"), new Color(0.85f, 0.78f, 0.68f), 0.05f, new Vector2(14f, 14f));
        matPlaster = MakeMat("WallPlaster", LoadTex("wall_plaster"), new Color(0.92f, 0.85f, 0.72f), 0.15f, new Vector2(2f, 1.5f));
        matWood = MakeMat("WoodPlanks", LoadTex("wood_planks"), new Color(0.75f, 0.6f, 0.42f), 0.3f, new Vector2(1f, 1f));
        matRoof = MakeMat("RoofTiles", LoadTex("roof_tiles"), new Color(0.95f, 0.8f, 0.62f), 0.25f, new Vector2(3f, 2f));
        matStone = MakeMat("StoneWall", LoadTex("stone_wall"), new Color(0.8f, 0.78f, 0.72f), 0.1f, new Vector2(2f, 1f));
        matGrass = MakeMat("FieldGrass", LoadTex("field_grass"), new Color(0.7f, 0.8f, 0.55f), 0.05f, new Vector2(4f, 3f));
        matLeaf = MakeFlat("Leaves", new Color(0.10f, 0.22f, 0.08f), 0.05f);
        matTrunk = MakeFlat("Trunk", new Color(0.16f, 0.10f, 0.06f), 0.1f);
        matDark = MakeFlat("MonsterDark", new Color(0.02f, 0.02f, 0.025f), 0.0f);
        matGlass = MakeFlat("WindowGlass", new Color(0.03f, 0.04f, 0.07f), 0.6f);
        matSkin = MakeFlat("Skin", new Color(0.45f, 0.30f, 0.20f), 0.4f);
        matKurta = MakeFlat("Kurta", new Color(0.12f, 0.11f, 0.14f), 0.35f);
        matEye = MakeEmissive("MonsterEyes", new Color(0.9f, 0.85f, 0.6f), new Color(1.4f, 1.1f, 0.35f));
        matLampGlass = MakeEmissive("LampGlass", new Color(1f, 0.85f, 0.55f), new Color(1.6f, 1.0f, 0.35f));
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

    private GameObject MakeGlowQuad(string name, Vector3 pos, float size, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(villageRoot, false);
        go.transform.position = pos;
        go.transform.localScale = new Vector3(size, size, size);
        MeshRenderer r = go.AddComponent<MeshRenderer>();
        MeshFilter f = go.AddComponent<MeshFilter>();
        f.mesh = BuildQuadMesh();
        Material m = new Material(Shader.Find("Unlit/Transparent"));
        m.mainTexture = MakeRadialGlowTexture();
        m.color = color;
        r.material = m;
        return go;
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
            if (r != null)
            {
                r.material = matGround;
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

        // TEMPORARY placeholder figure for Nilesh (no realistic human model yet):
        // dark kurta capsule body, plain head (no face/features), simple limbs.
        Transform root = player.transform;
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.transform.SetParent(root, false);
        body.transform.localPosition = new Vector3(0f, -0.05f, 0f);
        body.transform.localScale = new Vector3(0.62f, 1.0f, 0.62f);
        body.GetComponent<Renderer>().material = matKurta;
        Destroy(body.GetComponent<Collider>());

        GameObject head = Sphere("Head", Vector3.zero, new Vector3(0.42f, 0.42f, 0.42f), matSkin, root);
        head.transform.localPosition = new Vector3(0f, 0.72f, 0f);
        Destroy(head.GetComponent<Collider>());

        GameObject legL = Box("LegL", Vector3.zero, new Vector3(0.2f, 0.5f, 0.2f), matDark, root);
        legL.transform.localPosition = new Vector3(-0.16f, -0.55f, 0f);
        Destroy(legL.GetComponent<Collider>());

        GameObject legR = Box("LegR", Vector3.zero, new Vector3(0.2f, 0.5f, 0.2f), matDark, root);
        legR.transform.localPosition = new Vector3(0.16f, -0.55f, 0f);
        Destroy(legR.GetComponent<Collider>());

        GameObject armL = Box("ArmL", Vector3.zero, new Vector3(0.16f, 0.55f, 0.16f), matKurta, root);
        armL.transform.localPosition = new Vector3(-0.4f, 0.15f, 0f);
        Destroy(armL.GetComponent<Collider>());

        GameObject armR = Box("ArmR", Vector3.zero, new Vector3(0.16f, 0.55f, 0.16f), matKurta, root);
        armR.transform.localPosition = new Vector3(0.4f, 0.15f, 0f);
        Destroy(armR.GetComponent<Collider>());
    }

    // ------------------------------------------------------------------
    // Houses. DoorSide: East = door on +X wall, West = door on -X wall.
    // All Phase 1 doors face the chowk.
    // ------------------------------------------------------------------
    private enum DoorSide { East, West }

    private void BuildHouses()
    {
        BuildHouse("House_Player", new Vector3(-9f, 0f, 6f), 6f, 5f, 3f, DoorSide.East, true);
        BuildHouse("House_2", new Vector3(9f, 0f, 8f), 5f, 4f, 3f, DoorSide.West, false);
        BuildHouse("House_3", new Vector3(-9f, 0f, -9f), 5f, 4f, 3f, DoorSide.East, false);
        BuildHouse("House_4", new Vector3(9f, 0f, -7f), 6f, 5f, 3f, DoorSide.West, false);
        BuildHouse("House_5", new Vector3(-13f, 0f, 13f), 4f, 4f, 2.8f, DoorSide.East, false);
        BuildHouse("House_6", new Vector3(13f, 0f, -14f), 4f, 4f, 2.8f, DoorSide.West, false);
    }

    private void BuildHouse(string name, Vector3 center, float w, float d, float wallH, DoorSide doorSide, bool isPlayerHouse)
    {
        Transform root = new GameObject(name).transform;
        root.SetParent(villageRoot, false);
        root.position = center;

        float t = 0.3f;          // wall thickness
        float doorW = 1.2f;
        float doorH = 2.2f;

        // Floor slab (stone plinth).
        Box(name + "_Floor", center + new Vector3(0f, 0.06f, 0f), new Vector3(w + 0.4f, 0.12f, d + 0.4f), matStone, root);

        // Back wall (+/-X opposite the door) and side walls (+/-Z).
        float doorX = (doorSide == DoorSide.East) ? (w / 2f) : (-w / 2f);
        float backX = -doorX;
        Box(name + "_WallBack", center + new Vector3(backX, wallH / 2f, 0f), new Vector3(t, wallH, d), matPlaster, root);
        Box(name + "_WallSideA", center + new Vector3(0f, wallH / 2f, d / 2f), new Vector3(w, wallH, t), matPlaster, root);
        Box(name + "_WallSideB", center + new Vector3(0f, wallH / 2f, -d / 2f), new Vector3(w, wallH, t), matPlaster, root);

        // Front (door) wall: two segments + lintel above the door.
        float segLen = (d - doorW) / 2f;
        Box(name + "_WallFrontA", center + new Vector3(doorX, wallH / 2f, d / 2f - segLen / 2f), new Vector3(t, wallH, segLen), matPlaster, root);
        Box(name + "_WallFrontB", center + new Vector3(doorX, wallH / 2f, -d / 2f + segLen / 2f), new Vector3(t, wallH, segLen), matPlaster, root);
        Box(name + "_WallLintel", center + new Vector3(doorX, doorH + (wallH - doorH) / 2f, 0f), new Vector3(t, wallH - doorH, doorW), matPlaster, root);

        // Sloped clay-tile roof: two panels meeting at a ridge along X.
        float roofY = wallH + 0.55f;
        GameObject panelA = Box(name + "_RoofA", center + new Vector3(0f, roofY, d / 4f), new Vector3(w + 0.8f, 0.12f, d * 0.62f), matRoof, root);
        panelA.transform.rotation = Quaternion.Euler(30f, 0f, 0f);
        GameObject panelB = Box(name + "_RoofB", center + new Vector3(0f, roofY, -d / 4f), new Vector3(w + 0.8f, 0.12f, d * 0.62f), matRoof, root);
        panelB.transform.rotation = Quaternion.Euler(-30f, 0f, 0f);
        // Ridge beam.
        Box(name + "_RoofRidge", center + new Vector3(0f, wallH + 1.05f, 0f), new Vector3(w + 0.8f, 0.18f, 0.25f), matWood, root);

        // Windows (dark glass) on the side walls.
        Box(name + "_WindowA", center + new Vector3(-w / 4f, 1.7f, d / 2f + 0.02f), new Vector3(0.8f, 0.8f, 0.06f), matGlass, root);
        Box(name + "_WindowB", center + new Vector3(w / 4f, 1.7f, -d / 2f - 0.02f), new Vector3(0.8f, 0.8f, 0.06f), matGlass, root);

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
        // Bed in the far corner.
        Box("Bed", center + new Vector3(-w / 2f + 1.0f, 0.35f, -d / 2f + 0.8f), new Vector3(0.9f, 0.5f, 1.9f), matWood, root);
        Box("BedPillow", center + new Vector3(-w / 2f + 1.0f, 0.65f, -d / 2f + 0.25f), new Vector3(0.7f, 0.18f, 0.4f), matPlaster, root);
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
    private void BuildWellAndLamp()
    {
        // Stone well at the chowk edge (kept clear of the monster's lane path).
        Vector3 wellPos = new Vector3(-4.5f, 0f, -3.5f);
        Cylinder("Well", wellPos + new Vector3(0f, 0.5f, 0f), new Vector3(2.0f, 1.0f, 2.0f), matStone, villageRoot);
        Cylinder("WellWater", wellPos + new Vector3(0f, 0.92f, 0f), new Vector3(1.7f, 0.06f, 1.7f), MakeFlat("WellWater", new Color(0.02f, 0.06f, 0.09f), 0.9f), villageRoot);
        Cylinder("WellPostA", wellPos + new Vector3(-0.9f, 1.6f, 0f), new Vector3(0.18f, 2.2f, 0.18f), matWood, villageRoot);
        Cylinder("WellPostB", wellPos + new Vector3(0.9f, 1.6f, 0f), new Vector3(0.18f, 2.2f, 0.18f), matWood, villageRoot);
        GameObject wellRoof = Box("WellRoof", wellPos + new Vector3(0f, 2.8f, 0f), new Vector3(2.6f, 0.12f, 1.8f), matRoof, villageRoot);
        wellRoof.transform.rotation = Quaternion.Euler(0f, 0f, 12f);
        Block(wellPos + new Vector3(0f, 0.5f, 0f), new Vector3(2.2f, 1.1f, 2.2f));

        // Food pickup (crate) next to the well.
        GameObject crate = Box("Pickup_Food", wellPos + new Vector3(1.9f, 0.3f, 1.2f), new Vector3(0.65f, 0.6f, 0.65f), matWood, villageRoot);
        foodPickup = crate.AddComponent<Pickup>();
        foodPickup.itemName = "Khana";
        foodPickup.bobHeight = 0.12f;

        // Chowk lamp: pole + warm flickering point light + glow.
        Vector3 lampPos = new Vector3(2.5f, 0f, 1.5f);
        Cylinder("LampPole", lampPos + new Vector3(0f, 1.6f, 0f), new Vector3(0.22f, 3.2f, 0.22f), matDark, villageRoot);
        Box("LampHead", lampPos + new Vector3(0f, 3.3f, 0f), new Vector3(0.5f, 0.45f, 0.5f), matLampGlass, villageRoot);
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
        Cylinder("Trunk", pos + new Vector3(0f, 1.1f * scale, 0f), new Vector3(0.35f * scale, 2.2f * scale, 0.35f * scale), matTrunk, root);
        Sphere("CanopyA", pos + new Vector3(0f, 2.8f * scale, 0f), new Vector3(2.4f * scale, 1.8f * scale, 2.4f * scale), matLeaf, root);
        Sphere("CanopyB", pos + new Vector3(0.7f * scale, 2.2f * scale, 0.3f * scale), new Vector3(1.5f * scale, 1.1f * scale, 1.5f * scale), matLeaf, root);
        Block(pos + new Vector3(0f, 1.1f, 0f), new Vector3(0.6f, 2.2f, 0.6f));
    }

    private void BuildJungleAndFences()
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
        // A few trees inside the village.
        BuildTree(new Vector3(-16f, 0f, -2f), 1.1f);
        BuildTree(new Vector3(16f, 0f, 2f), 1.3f);
        BuildTree(new Vector3(-4f, 0f, 18f), 1.0f);

        // Wood fences along the main lane.
        for (int i = 0; i < 5; i++)
        {
            float z = -12f + i * 3.2f;
            Box("Fence", new Vector3(-5.5f, 0.55f, z), new Vector3(0.12f, 1.1f, 2.8f), matWood, villageRoot);
            Block(new Vector3(-5.5f, 0.55f, z), new Vector3(0.3f, 1.1f, 2.8f));
        }
        for (int i = 0; i < 4; i++)
        {
            float z = 10f + i * 3.0f;
            Box("Fence", new Vector3(5.5f, 0.55f, z), new Vector3(0.12f, 1.1f, 2.6f), matWood, villageRoot);
            Block(new Vector3(5.5f, 0.55f, z), new Vector3(0.3f, 1.1f, 2.6f));
        }
    }

    private void BuildProps()
    {
        // Crates and barrels scattered near houses.
        Box("Crate1", new Vector3(6.5f, 0.35f, 4.5f), new Vector3(0.7f, 0.7f, 0.7f), matWood, villageRoot);
        Box("Crate2", new Vector3(6.5f, 1.05f, 4.5f), new Vector3(0.55f, 0.55f, 0.55f), matWood, villageRoot);
        Cylinder("Barrel1", new Vector3(-6f, 0.5f, -4f), new Vector3(0.8f, 1.0f, 0.8f), matWood, villageRoot);
        Cylinder("Barrel2", new Vector3(12f, 0.5f, 11f), new Vector3(0.8f, 1.0f, 0.8f), matWood, villageRoot);
        Block(new Vector3(6.5f, 0.5f, 4.5f), new Vector3(0.9f, 1.2f, 0.9f));
        Block(new Vector3(-6f, 0.5f, -4f), new Vector3(0.9f, 1.0f, 0.9f));
        Block(new Vector3(12f, 0.5f, 11f), new Vector3(0.9f, 1.0f, 0.9f));
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
