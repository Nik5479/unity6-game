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

    void Awake()
    {
        cam = Camera.main;
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
        SetSun(1f, kDaySun, 50f);
        SetSky(kDaySky, kDayFog, 0.004f, kDayAmbient);
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
        Material skybox = RenderSettings.skybox;
        if (skybox != null && skybox.HasProperty("_Exposure"))
        {
            float lum = 0.2126f * sky.r + 0.7152f * sky.g + 0.0722f * sky.b;
            // Lighting pass: night exposure 1.65 (was 1.5) — the night sky
            // reads deep blue, not black, so the moon and haze show.
            // Day sky (lum ~0.76) -> exposure 2.75; night sky (lum ~0.03) -> 1.65.
            skybox.SetFloat("_Exposure", 1.9f + Mathf.Clamp01(lum / 0.8f) * 1.1f);
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
    }
}
