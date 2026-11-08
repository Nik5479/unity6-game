using UnityEngine;

/// <summary>
/// Day-night cycle for THE FORGOTTEN VILLAGE (Phase 1).
/// Day: warm sun, blue sky, light haze. Dusk: sun sinks and cools.
/// Night: cold moonlight (same directional light, low + blue),
/// thick fog, dark ambient, chowk lamp flickers on, moon fades in.
/// Dawn: dim grey morning, night ends (survived).
/// Fires OnNightStart / OnDawn (wired by VillageBuilder).
/// </summary>
public class DayNightCycle : MonoBehaviour
{
    public Light sun;
    public Light lampLight;
    public Material moonMaterial;
    public GameObject lampGlow;
    public GameObject moonGlow;
    // OVERNIGHT R1 (2026-11-09): the moon DISC itself. SetMoon(0) only
    // dimmed its material to 0.3-grey — the sphere stayed visible as a
    // pale disc in the day sky (audit 4.4). Now the whole object hides
    // by day and returns at night.
    public GameObject moonObject;

    public float dayLength = 60f;
    public float duskLength = 8f;
    public float nightLength = 75f;

    public bool IsNight { get; private set; }

    public System.Action OnNightStart;
    public System.Action OnDawn;

    private Camera cam;
    private float t;
    private int phase; // 0 day, 1 dusk, 2 night, 3 dawn (held)
    private bool nightFired;
    private bool dawnFired;
    private float lampFlickerT;

    private static readonly Color kDaySky = new Color(0.53f, 0.81f, 0.98f);
    private static readonly Color kNightSky = new Color(0.02f, 0.03f, 0.07f);
    private static readonly Color kDaySun = new Color(1f, 0.95f, 0.84f);
    private static readonly Color kNightMoon = new Color(0.55f, 0.65f, 0.92f);
    private static readonly Color kDayFog = new Color(0.6f, 0.65f, 0.7f);
    private static readonly Color kNightFog = new Color(0.05f, 0.06f, 0.11f);
    private static readonly Color kDayAmbient = new Color(0.55f, 0.55f, 0.55f);
    // Night must still READ (reference target: a moonlit village you can
    // see, not pitch black). Attempt-11 proved 0.22 ambient + 0.5
    // moonlight still leaves the ground near-black and the demon a
    // silhouette — raised to 0.30 ambient / 0.7 moonlight (2026-11-07).
    // Fix pass 2026-11-08: night backgrounds were still near pitch
    // black in the close-ups — ambient 0.30 -> 0.40, fog lifted a
    // touch, moonlight 0.35 -> 0.5. Warm lamp vs cool moon contrast
    // is kept (lamps are warm, moon is cool blue).
    private static readonly Color kNightAmbient = new Color(0.40f, 0.41f, 0.48f);

    // Daytime sky (audit M8): the only skybox was the moonless NIGHT
    // HDRI, so the day sky read near-black at the top. By day a
    // procedural sky (Skybox/Procedural, in Always Included Shaders) is
    // used; the night HDRI comes back as the light fades. If the shader
    // is missing the camera clears to the day sky colour instead.
    private Material nightSkybox, daySkybox;

    void Awake()
    {
        cam = Camera.main;
        nightSkybox = RenderSettings.skybox;
        Shader proc = Shader.Find("Skybox/Procedural");
        if (proc != null)
        {
            daySkybox = new Material(proc);
            daySkybox.name = "DaySkyProcedural";
            if (daySkybox.HasProperty("_SunDisk")) daySkybox.SetFloat("_SunDisk", 2f);
            if (daySkybox.HasProperty("_SunSize")) daySkybox.SetFloat("_SunSize", 0.035f);
            if (daySkybox.HasProperty("_AtmosphereThickness")) daySkybox.SetFloat("_AtmosphereThickness", 1.15f);
            if (daySkybox.HasProperty("_SkyTint")) daySkybox.SetColor("_SkyTint", new Color(0.52f, 0.56f, 0.62f));
            if (daySkybox.HasProperty("_GroundColor")) daySkybox.SetColor("_GroundColor", new Color(0.42f, 0.38f, 0.33f));
            if (daySkybox.HasProperty("_Exposure")) daySkybox.SetFloat("_Exposure", 1.2f);
        }
        else
        {
            Debug.LogWarning("[DayNight] Skybox/Procedural not in build - day sky uses a solid colour");
        }
    }

    void Update()
    {
        t += Time.deltaTime;

        if (phase == 0)
        {
            ApplyDay();
            if (t >= dayLength)
            {
                phase = 1;
                t = 0f;
            }
        }
        else if (phase == 1)
        {
            ApplyBlend(Mathf.Clamp01(t / duskLength));
            if (t >= duskLength)
            {
                phase = 2;
                t = 0f;
                IsNight = true;
                if (!nightFired)
                {
                    nightFired = true;
                    if (OnNightStart != null) OnNightStart();
                }
            }
        }
        else if (phase == 2)
        {
            ApplyNight();
            if (t >= nightLength)
            {
                phase = 3;
                t = 0f;
                IsNight = false;
                if (!dawnFired)
                {
                    dawnFired = true;
                    if (OnDawn != null) OnDawn();
                }
            }
        }
        else
        {
            ApplyDawn();
        }
    }

    // Lighting pass 2026-11-07: jump straight to night (used by the
    // close-up test — the village takes 60 s day + 8 s dusk to get there
    // on its own). Fires OnNightStart so the monster activates.
    public void SkipToNight()
    {
        phase = 2;
        t = 0f;
        IsNight = true;
        if (!nightFired)
        {
            nightFired = true;
            if (OnNightStart != null) OnNightStart();
        }
        ApplyNight();
    }

    private void ApplyDay()
    {
        // Phase 2 day target (2026-11-08): the day reference is bright
        // sun, blue sky, hard light — the old sun 1.0 left the day flat
        // and the puddles matte. Sun 1.3 + thinner day haze (0.0025);
        // night/dusk values are untouched (full lighting pass is Phase 3).
        SetSun(1.3f, kDaySun, 50f);
        SetSky(kDaySky, kDayFog, 0.0025f, kDayAmbient);
        SetLamp(0f);
        SetMoon(0f);
    }

    private void ApplyBlend(float k)
    {
        // Lighting pass: moonlight bottoms out at 0.35 (was 0.25) so
        // moonlit surfaces still read cool blue instead of pitch black.
        // Fix pass: fog 0.022 -> 0.03 so the distant flat ground fades
        // into the dark instead of reading as a flat brown plane.
        SetSun(Mathf.Lerp(1f, 0.7f, k), Color.Lerp(kDaySun, kNightMoon, k), Mathf.Lerp(50f, 16f, k));
        SetSky(Color.Lerp(kDaySky, kNightSky, k), Color.Lerp(kDayFog, kNightFog, k), Mathf.Lerp(0.004f, 0.03f, k), Color.Lerp(kDayAmbient, kNightAmbient, k));
        SetLamp(k);
        SetMoon(k);
    }

    private void ApplyNight()
    {
        // Lighting pass: stronger cool moonlight (0.35, was 0.25) + a
        // touch more haze (0.022, was 0.018) for depth, like the
        // reference's foggy night. Warm lamp light contrasts against it.
        // Fix pass: moonlight 0.35 -> 0.5, fog 0.022 -> 0.03 — the
        // night backgrounds were near pitch black.
        SetSun(0.5f, kNightMoon, 16f);
        SetSky(kNightSky, kNightFog, 0.03f, kNightAmbient);
        // Lamp flickers: mostly on, occasional dips (horror feel).
        lampFlickerT += Time.deltaTime;
        float flicker = 1.35f;
        float n = Mathf.PerlinNoise(lampFlickerT * 7f, 0f);
        if (n < 0.18f)
        {
            flicker = 0.25f;
        }
        else if (n < 0.3f)
        {
            flicker = 0.7f;
        }
        SetLamp(1f, flicker);
        SetMoon(1f);
    }

    private void ApplyDawn()
    {
        // Dim grey morning after the night.
        SetSun(0.6f, new Color(0.8f, 0.82f, 0.85f), 30f);
        SetSky(new Color(0.45f, 0.48f, 0.55f), new Color(0.4f, 0.42f, 0.48f), 0.008f, new Color(0.35f, 0.36f, 0.4f));
        SetLamp(0f);
        SetMoon(0f);
    }

    private void SetSun(float intensity, Color color, float elevationDeg)
    {
        if (sun == null) return;
        sun.intensity = intensity;
        sun.color = color;
        sun.transform.rotation = Quaternion.Euler(elevationDeg, -30f, 0f);
    }

    private void SetSky(Color sky, Color fog, float density, Color ambient)
    {
        if (cam != null)
        {
            cam.backgroundColor = sky;
        }
        RenderSettings.fog = true;
        RenderSettings.fogColor = fog;
        RenderSettings.fogDensity = density;
        RenderSettings.ambientLight = ambient;
        // URP skybox: the moonless-HDRI sky is a night image, so its
        // exposure follows the phase — brighter in the day (reads as an
        // overcast grey-blue sky), darker at night. backgroundColor above
        // is a no-op while the camera clears to Skybox, but harmless.
        // Ambient stays Flat (animated here) by design: switching ambient
        // to Skybox would freeze it at the dark HDRI level all day.
        float skyLum = 0.2126f * sky.r + 0.7152f * sky.g + 0.0722f * sky.b;
        bool dayish = skyLum > 0.3f;
        if (dayish && daySkybox != null)
        {
            if (RenderSettings.skybox != daySkybox) RenderSettings.skybox = daySkybox;
            if (cam != null) cam.clearFlags = CameraClearFlags.Skybox;
            if (daySkybox.HasProperty("_Exposure"))
                daySkybox.SetFloat("_Exposure", Mathf.Lerp(0.5f, 1.25f, Mathf.InverseLerp(0.3f, 0.76f, skyLum)));
            return;
        }
        if (RenderSettings.skybox != nightSkybox && nightSkybox != null) RenderSettings.skybox = nightSkybox;
        if (cam != null)
            cam.clearFlags = (dayish && daySkybox == null) ? CameraClearFlags.SolidColor : CameraClearFlags.Skybox;
        Material skybox = RenderSettings.skybox;
        if (skybox != null && skybox.HasProperty("_Exposure"))
        {
            float lum = 0.2126f * sky.r + 0.7152f * sky.g + 0.0722f * sky.b;
            // PILOT ROUND 2: the moonless-HDRI night sky rendered pale
            // grey-white (sampled (200,200,200) in the CI night shot) —
            // the old 1.9 base exposure blew the night HDRI out. Now 0.8
            // so the night reads dark blue with stars, like the
            // reference. (Day uses the procedural skybox branch above,
            // so this only touches dusk/night.)
            skybox.SetFloat("_Exposure", 0.8f + Mathf.Clamp01(lum / 0.8f) * 1.1f);
        }
    }

    private void SetLamp(float k, float flickerOverride = -1f)
    {
        if (lampLight != null)
        {
            lampLight.intensity = (flickerOverride >= 0f) ? flickerOverride : (1.4f * k);
        }
        if (lampGlow != null)
        {
            lampGlow.SetActive(k > 0.05f);
        }
    }

    private void SetMoon(float k)
    {
        if (moonMaterial != null)
        {
            // Lighting pass: brighter moon disc at night (was ~1.35) so it
            // reads as the big full moon in the reference, not a dim ball.
            moonMaterial.SetColor("_EmissionColor", new Color(0.5f + 1.5f * k, 0.5f + 1.5f * k, 0.55f + 1.6f * k));
            // Fix pass: the moon is URP/Unlit now (the Lit+emission-map
            // disc rendered dark). Unlit brightness is the base colour.
            if (moonMaterial.shader != null && moonMaterial.shader.name.Contains("Unlit"))
            {
                float b = 0.3f + 0.7f * k;
                moonMaterial.color = new Color(b, b, b);
            }
        }
        if (moonGlow != null)
        {
            moonGlow.SetActive(k > 0.05f);
        }
        // OVERNIGHT R1: hide the disc itself by day — dimming its
        // colour to 0.3 grey still left a visible pale disc in the sky.
        if (moonObject != null)
        {
            moonObject.SetActive(k > 0.05f);
        }
    }
}
