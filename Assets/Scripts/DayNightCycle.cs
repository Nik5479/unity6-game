using UnityEngine;

/// <summary>
/// Day-night cycle for THE FORGOTTEN VILLAGE (Phase 1).
/// Day: warm sun, blue sky, light haze. Dusk: sun sinks and cools.
/// Night: cold moonlight (same directional light, low + blue),
/// thick fog, dark ambient, chowk lamp flickers on, moon fades in.
/// Dawn: dim grey morning, night ends (survived).
/// Fires OnNightStart / OnDawn (wired by VillageBuilder).
/// ROUND 4 (2026-11-09, look): the phone video was a flat bright
/// midday with no mood — the user's reference is a golden dusk /
/// moonlit night. Day is now a warm late afternoon (soft sun, light
/// haze) and dusk arrives fast (20 s) and lasts (15 s) through an
/// orange golden-hour stage; night fog is lighter and the lamps/moon
/// are brighter. Day stays readable — never pitch dark.
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

    // ROUND 4: day is short (20 s) so the golden dusk arrives fast,
    // and dusk is long (15 s) so the orange stage reads on screen.
    // Night still starts ~35 s in — the day objective (well pickup)
    // has time, and the monster still activates at night.
    public float dayLength = 20f;
    public float duskLength = 15f;
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
    // ROUND 4: golden-hour orange the dusk sun passes through on its
    // way from warm day to cool blue night (was a straight day->blue
    // lerp — no orange stage existed, so dusk read as grey-blue).
    private static readonly Color kDuskSun = new Color(1f, 0.55f, 0.25f);
    private static readonly Color kDayFog = new Color(0.6f, 0.65f, 0.7f);
    // ROUND 4: night fog lifted so the mist reads blue-grey, not black.
    private static readonly Color kNightFog = new Color(0.08f, 0.09f, 0.14f);
    // ROUND 4: day ambient slightly warm and a touch dimmer — the old
    // flat 0.55 grey was part of the "flat bright day" look.
    private static readonly Color kDayAmbient = new Color(0.52f, 0.50f, 0.46f);
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
            if (daySkybox.HasProperty("_SkyTint")) daySkybox.SetColor("_SkyTint", new Color(0.55f, 0.54f, 0.50f));
            if (daySkybox.HasProperty("_GroundColor")) daySkybox.SetColor("_GroundColor", new Color(0.42f, 0.38f, 0.33f));
            if (daySkybox.HasProperty("_Exposure")) daySkybox.SetFloat("_Exposure", 1.0f);
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

    // ROUND 4: jump to mid-dusk (used by the screenshot test for the
    // dusk shot). Does NOT fire OnNightStart — dusk is not night yet.
    public void SkipToDusk()
    {
        phase = 1;
        t = duskLength * 0.5f;
        ApplyBlend(0.5f);
    }

    private void ApplyDay()
    {
        // ROUND 4 (look): warm late afternoon instead of flat bright
        // midday — softer sun (1.05, was 1.3) lower in the sky (35 deg,
        // was 50) for longer shadows, and visible light haze (fog
        // 0.006, was 0.0025). Still bright enough to read clearly.
        SetSun(1.05f, kDaySun, 35f);
        SetSky(kDaySky, kDayFog, 0.006f, kDayAmbient);
        SetLamp(0f);
        SetMoon(0f);
    }

    private void ApplyBlend(float k)
    {
        // ROUND 4: golden-hour dusk. The sun cools through orange
        // (kDuskSun at mid-blend) instead of straight day->blue, and
        // the blend starts at the DAY sun (1.05, 35 deg) — the old
        // blend started at 1.0/50 deg, a visible intensity step at
        // t = dayLength. Fog stays light (0.018 at night, was 0.03).
        Color sunColor = k < 0.5f
            ? Color.Lerp(kDaySun, kDuskSun, k * 2f)
            : Color.Lerp(kDuskSun, kNightMoon, (k - 0.5f) * 2f);
        SetSun(Mathf.Lerp(1.05f, 0.7f, k), sunColor, Mathf.Lerp(35f, 16f, k));
        SetSky(Color.Lerp(kDaySky, kNightSky, k), Color.Lerp(kDayFog, kNightFog, k), Mathf.Lerp(0.006f, 0.018f, k), Color.Lerp(kDayAmbient, kNightAmbient, k));
        SetLamp(k);
        SetMoon(k);
    }

    private void ApplyNight()
    {
        // ROUND 4: lighter fog (0.018, was 0.03 — "light fog", not a
        // whiteout) and a brighter lamp (flicker base 1.6, was 1.35)
        // so the warm lamp vs cool moon contrast reads like the
        // reference. Moonlight stays 0.5 and ambient 0.40 — the
        // readability floor, do not go below.
        SetSun(0.5f, kNightMoon, 16f);
        SetSky(kNightSky, kNightFog, 0.018f, kNightAmbient);
        // Lamp flickers: mostly on, occasional dips (horror feel).
        lampFlickerT += Time.deltaTime;
        float flicker = 1.6f;
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
            // ROUND 4: base 0.8 -> 1.0 so the HDRI stars read brighter
            // at night (still dark blue, not blown out).
            skybox.SetFloat("_Exposure", 1.0f + Mathf.Clamp01(lum / 0.8f) * 1.1f);
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
            // ROUND 4: brighter moon disc at night (emission 2.0/2.1,
            // was 1.5/1.6) so it reads as the big full moon in the
            // reference, not a dim ball.
            moonMaterial.SetColor("_EmissionColor", new Color(0.5f + 2.0f * k, 0.5f + 2.0f * k, 0.55f + 2.1f * k));
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
