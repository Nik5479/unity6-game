using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// THE FORGOTTEN VILLAGE — self-contained audio manager (Phase 1).
///
/// - Creates itself at runtime (no scene edit, no Inspector setup needed).
/// - Creates the game's ONLY AudioListener and follows Camera.main with it.
///   (Main Camera has no AudioListener in the scene — do NOT add one.)
/// - Loads all clips from StreamingAssets/Audio/*.ogg at runtime, so no
///   clip references are needed in the Inspector.
/// - 3D sounds (knock / door / footsteps / lock / roar) are positional;
///   night ambience (wind + crickets) is a 2D loop.
/// - Every Play* method is a safe no-op if its clip failed to load, so a
///   missing file can never crash the game or block a build.
///
/// WIRING: see AUDIO_WIRING.md next to this file. Nothing in this file
/// needs to be edited to wire the sounds in — the other scripts just call
/// the static methods (e.g. AudioManager.PlayKnock()).
///
/// Built-in RP only, no packages. Unity 6 (6000.3) compatible.
/// </summary>
public class AudioManager : MonoBehaviour
{
    // ------------------------------------------------------------------
    // Clip file names (in Assets/StreamingAssets/Audio/).
    // To swap a sound, replace the .ogg file — no code change needed.
    // ------------------------------------------------------------------
    private const string kKnock = "knock.ogg";               // Kenney chop.ogg (wood impact)
    private const string kDoorCreak = "door_creak.ogg";       // Kenney creak2.ogg
    private const string kDoorOpen = "door_open.ogg";         // Kenney doorOpen_2.ogg
    private const string kDoorClose = "door_close.ogg";       // Kenney doorClose_1.ogg
    private const string kLockClunk = "lock_clunk.ogg";       // Kenney metalLatch.ogg
    private const string kFootstep1 = "footstep_1.ogg";       // Kenney footstep00.ogg
    private const string kFootstep2 = "footstep_2.ogg";       // Kenney footstep01.ogg
    private const string kPickup = "pickup.ogg";              // Kenney handleCoins.ogg
    private const string kUiClick = "ui_click.ogg";           // Kenney click1.ogg
    private const string kNightWind = "night_wind.ogg";       // Mixkit "Breeze through the trees"
    private const string kNightCrickets = "night_crickets.ogg"; // Mixkit "Summer crickets loop"
    private const string kGameOverSting = "gameover_sting.ogg"; // Mixkit "Cinematic transition swoosh heartbeat trailer"
    private const string kMonsterRoar = "monster_roar.ogg";   // Mixkit "Aggressive beast roar"
    private const string kDawnBirds = "dawn_birds.ogg";       // Mixkit "Little birds singing in the trees"

    // TODO (PENDING USER STEP — do NOT fake): the 3 mimicked-voice Hindi
    // whisper lines ("Nilesh... darwaza kholo...", etc.) must be recorded
    // by the user on his phone (whisper, pitch slightly lowered) and saved
    // as whisper_1.ogg / whisper_2.ogg / whisper_3.ogg in the same
    // StreamingAssets/Audio/ folder. Until then PlayWhisperLine() is a
    // safe no-op (subtitle text already shows the line on screen).
    private const string kWhisperPrefix = "whisper_";

    private static AudioManager s_instance;

    private Dictionary<string, AudioClip> m_clips = new Dictionary<string, AudioClip>();
    private AudioSource m_ambienceWind;
    private AudioSource m_ambienceCrickets;
    private AudioSource m_2dSource;      // UI / pickup / sting / dawn (non-positional)
    private List<AudioSource> m_3dPool = new List<AudioSource>();
    private int m_3dIndex;
    private int m_footstepAlt;
    private Coroutine m_ambienceFade;

    // ------------------------------------------------------------------
    // Bootstrap: create the persistent GameObject before the first scene.
    // ------------------------------------------------------------------
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (s_instance != null) return;
        GameObject go = new GameObject("AudioManager");
        DontDestroyOnLoad(go);
        s_instance = go.AddComponent<AudioManager>();
    }

    void Awake()
    {
        if (s_instance != null && s_instance != this)
        {
            Destroy(gameObject);
            return;
        }
        s_instance = this;
        DontDestroyOnLoad(gameObject);

        // The game's only AudioListener. It follows Camera.main (see
        // AudioListenerFollow below) so 3D sounds stay positioned right.
        if (GetComponent<AudioListener>() == null)
        {
            gameObject.AddComponent<AudioListener>();
        }
        gameObject.AddComponent<AudioListenerFollow>();

        m_ambienceWind = MakeSource("AmbienceWind", spatialBlend: 0f, loop: true, volume: 0f);
        m_ambienceCrickets = MakeSource("AmbienceCrickets", spatialBlend: 0f, loop: true, volume: 0f);
        m_2dSource = MakeSource("OneShot2D", spatialBlend: 0f, loop: false, volume: 1f);
        for (int i = 0; i < 4; i++)
        {
            m_3dPool.Add(MakeSource("OneShot3D_" + i, spatialBlend: 1f, loop: false, volume: 1f));
        }

        StartCoroutine(LoadAllClips());
    }

    private AudioSource MakeSource(string name, float spatialBlend, bool loop, float volume)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(transform, false);
        AudioSource src = child.AddComponent<AudioSource>();
        src.spatialBlend = spatialBlend;
        src.loop = loop;
        src.volume = volume;
        src.playOnAwake = false;
        src.minDistance = 1.5f;
        src.maxDistance = 28f;
        src.rolloffMode = AudioRolloffMode.Linear;
        return src;
    }

    // ------------------------------------------------------------------
    // Runtime clip loading (StreamingAssets needs UnityWebRequest on
    // Android, where the folder lives inside the APK).
    // ------------------------------------------------------------------
    private IEnumerator LoadAllClips()
    {
        string[] files =
        {
            kKnock, kDoorCreak, kDoorOpen, kDoorClose, kLockClunk,
            kFootstep1, kFootstep2, kPickup, kUiClick,
            kNightWind, kNightCrickets, kGameOverSting, kMonsterRoar, kDawnBirds,
            kWhisperPrefix + "1.ogg", kWhisperPrefix + "2.ogg", kWhisperPrefix + "3.ogg",
        };
        string basePath = Path.Combine(Application.streamingAssetsPath, "Audio");
        foreach (string file in files)
        {
            string url = Path.Combine(basePath, file);
#if UNITY_ANDROID && !UNITY_EDITOR
            // Android: streamingAssetsPath is already a jar:file:// URI
            // inside the APK — UnityWebRequest handles it as-is.
#else
            url = "file://" + url;
#endif
            using (UnityWebRequest req = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.OGGVORBIS))
            {
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success)
                {
                    AudioClip clip = DownloadHandlerAudioClip.GetContent(req);
                    if (clip != null) m_clips[file] = clip;
                }
                else if (!file.StartsWith(kWhisperPrefix))
                {
                    Debug.LogWarning("[AudioManager] Could not load " + file + ": " + req.error);
                }
                // Whisper files are expected to be missing until the user
                // records them — no warning spam for those.
            }
        }
        m_ambienceWind.clip = GetClip(kNightWind);
        m_ambienceCrickets.clip = GetClip(kNightCrickets);
    }

    private AudioClip GetClip(string file)
    {
        AudioClip clip;
        return m_clips.TryGetValue(file, out clip) ? clip : null;
    }

    // ------------------------------------------------------------------
    // Internal one-shot players.
    // ------------------------------------------------------------------
    private void Play3D(string file, Vector3 position, float volume, float pitch)
    {
        AudioClip clip = GetClip(file);
        if (clip == null) return; // safe no-op until the clip has loaded
        AudioSource src = m_3dPool[m_3dIndex];
        m_3dIndex = (m_3dIndex + 1) % m_3dPool.Count;
        src.transform.position = position;
        src.pitch = pitch;
        src.PlayOneShot(clip, volume);
    }

    private void Play2D(string file, float volume, float pitch)
    {
        AudioClip clip = GetClip(file);
        if (clip == null) return;
        m_2dSource.pitch = pitch;
        m_2dSource.PlayOneShot(clip, volume);
    }

    private Vector3 DefaultPosition()
    {
        Camera cam = Camera.main;
        return cam != null ? cam.transform.position : Vector3.zero;
    }

    private static AudioManager Instance
    {
        get
        {
            if (s_instance == null) Bootstrap();
            return s_instance;
        }
    }

    // ==================================================================
    // PUBLIC STATIC API — this is what the game scripts call.
    // Every method is null-safe: if audio isn't ready yet, it does
    // nothing instead of crashing.
    // ==================================================================

    /// <summary>One door knock (wood thud). Call once per knock; the
    /// pitch varies slightly each call so 3 knocks don't sound identical.
    /// Pass the door position for a positional knock.</summary>
    public static void PlayKnock() { PlayKnock(Vector3.zero, false); }
    public static void PlayKnock(Vector3 position)
    {
        PlayKnock(position, true);
    }
    private static void PlayKnock(Vector3 position, bool hasPosition)
    {
        AudioManager m = Instance;
        if (m == null) return;
        Vector3 pos = hasPosition ? position : m.DefaultPosition();
        float pitch = Random.Range(0.92f, 1.08f);
        m.Play3D(kKnock, pos, 1f, pitch);
    }

    /// <summary>Long door creak (monster forcing the door / slow open).</summary>
    public static void PlayDoorCreak() { PlayDoorCreak(Vector3.zero, false); }
    public static void PlayDoorCreak(Vector3 position)
    {
        PlayDoorCreak(position, true);
    }
    private static void PlayDoorCreak(Vector3 position, bool hasPosition)
    {
        AudioManager m = Instance;
        if (m == null) return;
        m.Play3D(kDoorCreak, hasPosition ? position : m.DefaultPosition(), 1f, 1f);
    }

    /// <summary>Door swinging open (hinge + swing).</summary>
    public static void PlayDoorOpen() { PlayDoorOpen(Vector3.zero, false); }
    public static void PlayDoorOpen(Vector3 position)
    {
        PlayDoorOpen(position, true);
    }
    private static void PlayDoorOpen(Vector3 position, bool hasPosition)
    {
        AudioManager m = Instance;
        if (m == null) return;
        m.Play3D(kDoorOpen, hasPosition ? position : m.DefaultPosition(), 1f, 1f);
    }

    /// <summary>Door swinging shut.</summary>
    public static void PlayDoorClose() { PlayDoorClose(Vector3.zero, false); }
    public static void PlayDoorClose(Vector3 position)
    {
        PlayDoorClose(position, true);
    }
    private static void PlayDoorClose(Vector3 position, bool hasPosition)
    {
        AudioManager m = Instance;
        if (m == null) return;
        m.Play3D(kDoorClose, hasPosition ? position : m.DefaultPosition(), 1f, 1f);
    }

    /// <summary>Lock / barricade clunk (metal latch).</summary>
    public static void PlayLockClunk()
    {
        AudioManager m = Instance;
        if (m == null) return;
        m.Play3D(kLockClunk, m.DefaultPosition(), 1f, Random.Range(0.95f, 1.05f));
    }

    /// <summary>One footstep at the player's position. Alternates two
    /// footstep clips with slight pitch variation.</summary>
    public static void PlayFootstep() { PlayFootstep(Vector3.zero, false); }
    public static void PlayFootstep(Vector3 position)
    {
        PlayFootstep(position, true);
    }
    private static void PlayFootstep(Vector3 position, bool hasPosition)
    {
        AudioManager m = Instance;
        if (m == null) return;
        string file = (m.m_footstepAlt++ % 2 == 0) ? kFootstep1 : kFootstep2;
        m.Play3D(file, hasPosition ? position : m.DefaultPosition(), 0.7f, Random.Range(0.94f, 1.06f));
    }

    /// <summary>Pickup chime (food crate collected).</summary>
    public static void PlayPickup()
    {
        AudioManager m = Instance;
        if (m == null) return;
        m.Play2D(kPickup, 0.9f, 1f);
    }

    /// <summary>UI button click.</summary>
    public static void PlayUIClick()
    {
        AudioManager m = Instance;
        if (m == null) return;
        m.Play2D(kUiClick, 0.8f, 1f);
    }

    /// <summary>Game-over sting (heartbeat trailer) + monster roar.
    /// Call once when the monster gets in.</summary>
    public static void PlayGameOverSting()
    {
        AudioManager m = Instance;
        if (m == null) return;
        m.Play2D(kGameOverSting, 1f, 1f);
        m.Play3D(kMonsterRoar, m.DefaultPosition(), 1f, 1f);
        StopNightAmbience();
    }

    /// <summary>Dawn relief cue (morning birds) when the player survives.</summary>
    public static void PlayDawnRelief()
    {
        AudioManager m = Instance;
        if (m == null) return;
        m.Play2D(kDawnBirds, 0.9f, 1f);
    }

    // ------------------------------------------------------------------
    // Night ambience: wind + crickets loops, faded in/out over ~3s.
    // ------------------------------------------------------------------
    public static void StartNightAmbience()
    {
        AudioManager m = Instance;
        if (m == null) return;
        if (m.m_ambienceFade != null) m.StopCoroutine(m.m_ambienceFade);
        m.m_ambienceFade = m.StartCoroutine(m.FadeAmbience(0.45f, 0.32f, 3f));
    }

    public static void StopNightAmbience()
    {
        AudioManager m = Instance;
        if (m == null) return;
        if (m.m_ambienceFade != null) m.StopCoroutine(m.m_ambienceFade);
        m.m_ambienceFade = m.StartCoroutine(m.FadeAmbience(0f, 0f, 2f));
    }

    private IEnumerator FadeAmbience(float windTarget, float cricketTarget, float seconds)
    {
        if (m_ambienceWind.clip != null && !m_ambienceWind.isPlaying) m_ambienceWind.Play();
        if (m_ambienceCrickets.clip != null && !m_ambienceCrickets.isPlaying) m_ambienceCrickets.Play();
        float windStart = m_ambienceWind.volume;
        float cricketStart = m_ambienceCrickets.volume;
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / seconds);
            m_ambienceWind.volume = Mathf.Lerp(windStart, windTarget, k);
            m_ambienceCrickets.volume = Mathf.Lerp(cricketStart, cricketTarget, k);
            yield return null;
        }
        m_ambienceWind.volume = windTarget;
        m_ambienceCrickets.volume = cricketTarget;
        if (windTarget <= 0f && cricketTarget <= 0f)
        {
            m_ambienceWind.Stop();
            m_ambienceCrickets.Stop();
        }
        m_ambienceFade = null;
    }

    // ------------------------------------------------------------------
    // Mimicked-voice whispers — PENDING USER RECORDING. Do NOT fake.
    // See the TODO at the top of this file. When whisper_1/2/3.ogg exist
    // in StreamingAssets/Audio/, this plays the matching line at the
    // door position. Until then it silently does nothing.
    // ------------------------------------------------------------------
    public static void PlayWhisperLine(int index)
    {
        AudioManager m = Instance;
        if (m == null) return;
        AudioClip clip = m.GetClip(kWhisperPrefix + (index + 1) + ".ogg");
        if (clip == null) return; // not recorded yet — subtitle already shows the line
        m.Play3D(kWhisperPrefix + (index + 1) + ".ogg", m.DefaultPosition(), 1f, 0.92f);
    }

    // ------------------------------------------------------------------
    // Keeps the runtime-created AudioListener glued to Camera.main.
    // ------------------------------------------------------------------
    private class AudioListenerFollow : MonoBehaviour
    {
        void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            transform.position = cam.transform.position;
            transform.rotation = cam.transform.rotation;
        }
    }
}
