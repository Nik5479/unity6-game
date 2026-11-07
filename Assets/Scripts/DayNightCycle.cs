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
    private static readonly Color kNightFog = new Color(0.03f, 0.04f, 0.08f);
    private static readonly Color kDayAmbient = new Color(0.55f, 0.55f, 0.55f);
    private static readonly Color kNightAmbient = new Color(0.10f, 0.11f, 0.16f);

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

    private void ApplyDay()
    {
        SetSun(1f, kDaySun, 50f);
        SetSky(kDaySky, kDayFog, 0.004f, kDayAmbient);
        SetLamp(0f);
        SetMoon(0f);
    }

    private void ApplyBlend(float k)
    {
        SetSun(Mathf.Lerp(1f, 0.25f, k), Color.Lerp(kDaySun, kNightMoon, k), Mathf.Lerp(50f, 16f, k));
        SetSky(Color.Lerp(kDaySky, kNightSky, k), Color.Lerp(kDayFog, kNightFog, k), Mathf.Lerp(0.004f, 0.018f, k), Color.Lerp(kDayAmbient, kNightAmbient, k));
        SetLamp(k);
        SetMoon(k);
    }

    private void ApplyNight()
    {
        SetSun(0.25f, kNightMoon, 16f);
        SetSky(kNightSky, kNightFog, 0.018f, kNightAmbient);
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
            moonMaterial.SetColor("_EmissionColor", new Color(0.25f + 1.1f * k, 0.25f + 1.1f * k, 0.3f + 1.2f * k));
        }
        if (moonGlow != null)
        {
            moonGlow.SetActive(k > 0.05f);
        }
    }
}
