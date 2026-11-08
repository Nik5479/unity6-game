using System.Collections;
using System.Threading.Tasks;
using GLTFast;
using UnityEngine;

/// <summary>
/// Loads .glb AND .gltf models at runtime from Assets/StreamingAssets/Models/
/// via glTFast (com.atteneder.gltfast 6.20.0). fileName may include a
/// subfolder (e.g. "SoilGround/xdhhdhl_tier_2.gltf") — glTFast resolves a
/// .gltf's .bin + textures relative to the .gltf file, so the whole bundle
/// folder goes under Models/. Sketchfab models arrive in wildly
/// inconsistent units (metres, centimetres, millimetres, arbitrary),
/// so every model is normalised by its rendered bounds: uniform scale so
/// its target dimension matches, then grounded (bounds.min.y = 0) and
/// centred on its placement point. A model that fails to load is logged
/// and skipped — it must never kill the village. The holder GameObject is
/// created immediately (so GameObject.Find works before the load finishes)
/// and destroyed again on failure, so fallbacks can reuse the same name.
/// NOTE: on Android, StreamingAssets live inside the APK (jar: URL) and
/// glTFast loads .gltf bundles through UnityWebRequest; a .gltf whose
/// relative resources fail to resolve is skipped like any other failure.
/// </summary>
public static class GltfModelLoader
{
    public enum NormalizeMode { ByWidth, ByHeight }
    public enum ColliderMode { None, FullBounds, Trunk }

    // LIVE IMPORTS — DO NOT DISPOSE. glTFast's GltfImport owns the meshes,
    // materials and textures of the scene it instantiated: Dispose() destroys
    // them (GltfImport.Dispose → SafeDestroy(m_Materials/m_Textures/m_Meshes)),
    // so disposing after a successful load left every .glb renderer with
    // zero bounds and null materials (2026-11-07: empty village on phone,
    // only primitives visible). Every successfully loaded import stays in
    // this list for the whole scene lifetime and is never disposed during
    // gameplay. Dispose happens ONLY in Cleanup(), on the failure path,
    // where the holder GameObject is destroyed anyway.
    private static readonly System.Collections.Generic.List<GltfImport> s_AliveImports
        = new System.Collections.Generic.List<GltfImport>();

    // FREEZE FIX 2026-11-08 — two timeouts bound every load, so the
    // village build can never hang the game again:
    // (1) kLoadTimeoutSeconds: one load must never wait forever. Load()
    //     and InstantiateMainSceneAsync used to be awaited with a bare
    //     WaitUntil (no timeout), so a single stalled glTFast task (e.g.
    //     a UnityWebRequest to a jar: URL that never returns) stalled
    //     the whole build forever and the game never started.
    // (2) BuildDeadline: VillageBuilder sets this to the wall-clock time
    //     by which the whole build must finish; a load that starts (or is
    //     still running) after it is skipped, so the game starts with
    //     whatever loaded instead of waiting minutes on a budget phone.
    // LoadTimings records every load's wall time for diagnostics — the
    // slowest models are the first suspects when a phone build drags.
    public const float kLoadTimeoutSeconds = 30f;
    public static float BuildDeadline = float.PositiveInfinity;
    public static bool DeadlineReached;
    public static readonly System.Collections.Generic.List<string> LoadTimings =
        new System.Collections.Generic.List<string>();
    // Progress hook: VillageBuilder wires this to the boot status text so
    // the player sees each model load instead of a frozen "Building...".
    public static System.Action<string> OnProgress;

    // URP Unlit shader (cached). glTFast's Shader Graph "glTF-unlit" renders
    // pure black under URP (see the fix below) — those materials are swapped
    // to the built-in URP Unlit shader instead.
    private static Shader s_UrpUnlitShader;
    private static bool s_UrpUnlitQueried;

    private static Shader GetUrpUnlitShader()
    {
        if (!s_UrpUnlitQueried)
        {
            s_UrpUnlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            s_UrpUnlitQueried = true;
            if (s_UrpUnlitShader == null)
                Debug.LogWarning("[Gltf] URP Unlit shader not found — glTF unlit materials keep the Shader Graph shader.");
        }
        return s_UrpUnlitShader;
    }

    // URP Lit shader (cached) — for black-specular spec-gloss materials.
    private static Shader s_UrpLitShader;
    private static bool s_UrpLitQueried;

    private static Shader GetUrpLitShader()
    {
        if (!s_UrpLitQueried)
        {
            s_UrpLitShader = Shader.Find("Universal Render Pipeline/Lit");
            s_UrpLitQueried = true;
            if (s_UrpLitShader == null)
                Debug.LogWarning("[Gltf] URP Lit shader not found — black-specular spec-gloss materials keep the Shader Graph shader.");
        }
        return s_UrpLitShader;
    }
    // Loads one model. onDone(holder) — holder is null on failure.
    // Never throws. The load is sequential per call; callers yield on it.
    public static IEnumerator LoadModel(
        string fileName, Transform parent, string goName, Vector3 pos, float yaw,
        float targetSize, NormalizeMode normMode, ColliderMode colMode,
        System.Action<GameObject> onDone)
    {
        GameObject holder = new GameObject(goName);
        holder.transform.SetParent(parent, false);
        holder.transform.position = pos;
        holder.transform.rotation = Quaternion.identity;

        // Freeze fix: skip the load entirely once the build deadline has
        // passed — the game starts with whatever already loaded.
        if (Time.realtimeSinceStartup > BuildDeadline)
        {
            DeadlineReached = true;
            Debug.LogWarning("[Gltf] Skipped (build deadline reached): " + fileName);
            Object.Destroy(holder);
            if (onDone != null) onDone(null);
            yield break;
        }
        float loadStart = Time.realtimeSinceStartup;

        string fullPath = System.IO.Path.Combine(Application.streamingAssetsPath, "Models", fileName);
        var gltf = new GltfImport();
        Task<bool> loadTask = null;
        try
        {
            if (Application.streamingAssetsPath.StartsWith("jar:"))
            {
                // Android: StreamingAssets live inside the APK — read them
                // through UnityWebRequest (glTFast's Load handles jar: URLs).
                loadTask = gltf.Load(fullPath);
            }
            else
            {
                // Editor / standalone: plain file on disk.
                // Pass the file's own absolute URI as the base URI: .gltf
                // bundles reference their .bin + textures by RELATIVE URIs
                // (e.g. "xdhhdhl_tier_2.bin", "Textures/T_...png"), and
                // without a base URI glTFast cannot resolve them — every
                // .gltf load failed with "URI must be absolute" (2026-11-07:
                // soil, forest floor, 2 lanterns all missing). For .glb the
                // base URI is ignored (resources are embedded), so passing
                // it for every file is safe.
                loadTask = gltf.LoadFile(fullPath, new System.Uri(fullPath));
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Gltf] Load start failed: " + fileName + " — " + e.Message);
            Cleanup(gltf, holder);
            if (onDone != null) onDone(null);
            yield break;
        }

        // Freeze fix: bounded wait, not WaitUntil — a stalled task must
        // never hang the build. Abandoning a load mid-flight is safe:
        // Load() only parses data (no scene objects are created until
        // Instantiate), so a late-completing task just leaves parsed data
        // behind. Do NOT Dispose the import here: the task may still be
        // running on a background thread, and disposing under it risks a
        // late-completing job writing into disposed state.
        while (!loadTask.IsCompleted)
        {
            if (Time.realtimeSinceStartup > BuildDeadline)
            {
                DeadlineReached = true;
                Debug.LogWarning("[Gltf] Load abandoned at build deadline: " + fileName + " — skipping");
                Object.Destroy(holder);
                if (onDone != null) onDone(null);
                yield break;
            }
            if (Time.realtimeSinceStartup - loadStart > kLoadTimeoutSeconds)
            {
                Debug.LogWarning("[Gltf] Load TIMEOUT after " + kLoadTimeoutSeconds + "s: " + fileName + " — skipping");
                Object.Destroy(holder);
                if (onDone != null) onDone(null);
                yield break;
            }
            yield return null;
        }
        if (loadTask.IsFaulted || loadTask.IsCanceled || !loadTask.Result)
        {
            string why = (loadTask.Exception != null)
                ? loadTask.Exception.GetBaseException().Message : "Load returned false";
            Debug.LogWarning("[Gltf] Load failed: " + fileName + " — " + why);
            Cleanup(gltf, holder);
            if (onDone != null) onDone(null);
            yield break;
        }

        Task<bool> instTask = null;
        try
        {
            instTask = gltf.InstantiateMainSceneAsync(holder.transform);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Gltf] Instantiate start failed: " + fileName + " — " + e.Message);
            Cleanup(gltf, holder);
            if (onDone != null) onDone(null);
            yield break;
        }

        // Freeze fix: same bounded wait for the instantiate phase.
        float instStart = Time.realtimeSinceStartup;
        while (!instTask.IsCompleted)
        {
            if (Time.realtimeSinceStartup > BuildDeadline)
            {
                DeadlineReached = true;
                Debug.LogWarning("[Gltf] Instantiate abandoned at build deadline: " + fileName + " — skipping");
                Object.Destroy(holder);
                if (onDone != null) onDone(null);
                yield break;
            }
            if (Time.realtimeSinceStartup - instStart > kLoadTimeoutSeconds)
            {
                Debug.LogWarning("[Gltf] Instantiate TIMEOUT after " + kLoadTimeoutSeconds + "s: " + fileName + " — skipping");
                Object.Destroy(holder);
                if (onDone != null) onDone(null);
                yield break;
            }
            yield return null;
        }
        if (instTask.IsFaulted || instTask.IsCanceled || !instTask.Result)
        {
            Debug.LogWarning("[Gltf] Instantiate failed: " + fileName);
            Cleanup(gltf, holder);
            if (onDone != null) onDone(null);
            yield break;
        }

        try
        {
            Normalize(holder, targetSize, normMode, pos);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Gltf] Normalize failed (keeping original size): "
                + fileName + " — " + e.Message);
        }

        // URP has no skybox / reflection probe in this scene, so any glTF
        // material whose metallicFactor defaulted to 1.0 (glTF spec default
        // when the property is absent — most Sketchfab exports) renders
        // pure black: a metal surface with nothing to reflect. A village
        // has nothing chrome, so force every imported material dielectric.
        // NOTE (2026-11-07 URP fix): the old code set "_Metallic" — that
        // property does NOT exist on glTFast's Shader Graph materials, so
        // the fix silently did nothing. The real property is
        // "metallicFactor" (glTFast MaterialProperty.Metallic). The thatched
        // hut's metallic-roughness texture has a blue (metallic) mean of
        // 0.61, so its roof rendered black. Set metallicFactor to 0 here.
        // ALSO: glTFast's Shader Graph "glTF-unlit" renders pure black
        // under URP (horse/palms/dog: mid-tone base textures, white color,
        // yet black on screen). Swap those materials to the built-in
        // "Universal Render Pipeline/Unlit" shader, copying base map +
        // color and preserving the transparent/opaque render setup.
        try
        {
            foreach (Renderer r in holder.GetComponentsInChildren<Renderer>())
            {
                foreach (Material m in r.sharedMaterials)
                {
                    if (m == null) continue;
                    if (m.HasProperty("_Metallic"))
                        m.SetFloat("_Metallic", 0f);
                    if (m.HasProperty("metallicFactor"))
                        m.SetFloat("metallicFactor", 0f);
                    if (m.shader != null && m.shader.name == "Shader Graphs/glTF-unlit")
                    {
                        Shader unlit = GetUrpUnlitShader();
                        if (unlit != null)
                        {
                            Texture baseTex = m.HasProperty("baseColorTexture")
                                ? m.GetTexture("baseColorTexture") : null;
                            Color baseCol = m.HasProperty("baseColorFactor")
                                ? m.GetColor("baseColorFactor") : Color.white;
                            bool transparent = m.renderQueue >= 3000;
                            m.shader = unlit;
                            if (baseTex != null) m.SetTexture("_BaseMap", baseTex);
                            m.SetColor("_BaseColor", baseCol);
                            m.SetFloat("_Cull", 0f); // double-sided (palm leaves are single-sided quads)
                            m.SetOverrideTag("RenderType", transparent ? "Transparent" : "Opaque");
                            if (transparent)
                            {
                                m.SetFloat("_Surface", 1f);
                                m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                                m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                                m.SetFloat("_ZWrite", 0f);
                                m.SetFloat("_AlphaClip", 0f);
                                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                                m.DisableKeyword("_ALPHATEST_ON");
                            }
                            else
                            {
                                m.SetFloat("_Surface", 0f);
                                m.SetFloat("_ZWrite", 1f);
                                m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                                m.DisableKeyword("_ALPHATEST_ON");
                            }
                        }
                    }
                    if (m.shader != null && m.shader.name == "Shader Graphs/glTF-pbrSpecularGlossiness")
                    {
                        // Spec-gloss materials whose specularFactor is ~black
                        // render pure black under URP (the Shader Graph's
                        // spec-gloss path ties visible brightness to the
                        // specular colour — the pointed thatch hut, specular
                        // [0,0,0], renders as a black pyramid, while trees
                        // with specular [1,1,1] render fine). Convert the
                        // black-specular ones to URP Lit with the equivalent
                        // PBR setup (diffuse map+colour, metallic 0,
                        // smoothness = glossiness, spec colour = specular).
                        Vector4 spec = m.HasProperty("specularFactor")
                            ? m.GetVector("specularFactor") : new Vector4(1f, 1f, 1f, 1f);
                        float specMax = Mathf.Max(Mathf.Max(spec.x, spec.y), spec.z);
                        if (specMax < 0.05f)
                        {
                            Shader lit = GetUrpLitShader();
                            if (lit != null)
                            {
                                Texture diffTex = m.mainTexture;
                                Color diffCol = m.color;
                                float gloss = m.HasProperty("glossinessFactor")
                                    ? m.GetFloat("glossinessFactor") : 1f;
                                m.shader = lit;
                                if (diffTex != null) m.SetTexture("_BaseMap", diffTex);
                                m.SetColor("_BaseColor", diffCol);
                                m.SetFloat("_Metallic", 0f);
                                m.SetFloat("_Smoothness", Mathf.Clamp01(gloss));
                                m.SetColor("_SpecColor", new Color(spec.x, spec.y, spec.z, 1f));
                                m.SetFloat("_Cull", 0f); // double-sided (the thatch hut is)
                            }
                        }
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Gltf] Metallic fix failed: " + fileName + " — " + e.Message);
        }

        holder.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        if (colMode != ColliderMode.None)
        {
            try
            {
                RegisterCollider(holder, colMode);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Gltf] Collider failed: " + fileName + " — " + e.Message);
            }
        }

        // KEEP the import alive: the instantiated meshes/materials/textures
        // belong to this GltfImport — disposing it would destroy them.
        // (See s_AliveImports; GltfImport has no finalizer, but the list
        // makes the lifetime explicit and protects against future GC/finalizer
        // changes in the package.)
        s_AliveImports.Add(gltf);
        float loadSeconds = Time.realtimeSinceStartup - loadStart;
        LoadTimings.Add(fileName + ": " + loadSeconds.ToString("F1") + "s");
        Debug.Log("[Gltf] Loaded: " + fileName + " as " + goName + " in " + loadSeconds.ToString("F1") + "s");
        if (OnProgress != null) OnProgress(fileName);
        if (onDone != null) onDone(holder);
    }

    // Starts any embedded glTF animations under a loaded model (legacy
    // Animation clips created by glTFast). glTFast assigns the clip AFTER
    // AddComponent<Animation>, so playAutomatically never fires — an
    // explicit Play() is required, or the model stays frozen in its bind
    // (T-)pose. Only call for characters (Nilesh, Meera, Villager_Man):
    // props with long clips (e.g. the horse's 60 s clip) stay static.
    public static void PlayEmbeddedAnimations(GameObject holder)
    {
        if (holder == null) return;
        foreach (Animation anim in holder.GetComponentsInChildren<Animation>())
        {
            anim.wrapMode = WrapMode.Loop;
            anim.playAutomatically = true;
            if (anim.clip != null)
            {
                anim.Play();
                Debug.Log("[Gltf] Playing embedded animation '" + anim.clip.name
                    + "' (" + anim.clip.length.ToString("F1") + "s) on " + holder.name);
            }
            else
            {
                Debug.LogWarning("[Gltf] Animation component on " + holder.name + " has no clip");
            }
        }
    }

    // Plays ONE named embedded clip (e.g. the HellDemon's "WALKING" loop)
    // instead of whatever clip glTFast assigned as anim.clip. Falls back
    // to anim.clip when no clip matches the name.
    public static void PlayEmbeddedAnimation(GameObject holder, string clipNameContains)
    {
        if (holder == null) return;
        foreach (Animation anim in holder.GetComponentsInChildren<Animation>())
        {
            anim.wrapMode = WrapMode.Loop;
            anim.playAutomatically = true;
            AnimationClip wanted = null;
            if (anim.clip != null && anim.clip.name.IndexOf(clipNameContains, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                wanted = anim.clip;
            }
            else
            {
                foreach (AnimationState state in anim)
                {
                    if (state.clip != null && state.clip.name.IndexOf(clipNameContains, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        wanted = state.clip;
                        break;
                    }
                }
            }
            if (wanted != null)
            {
                anim.clip = wanted;
                anim.Play();
                Debug.Log("[Gltf] Playing embedded animation '" + wanted.name
                    + "' (" + wanted.length.ToString("F1") + "s) on " + holder.name);
            }
            else if (anim.clip != null)
            {
                anim.Play();
                Debug.LogWarning("[Gltf] Clip '" + clipNameContains + "' not found on " + holder.name
                    + " — playing '" + anim.clip.name + "' instead");
            }
            else
            {
                Debug.LogWarning("[Gltf] Animation component on " + holder.name + " has no clip");
            }
        }
    }

    private static void Cleanup(GltfImport gltf, GameObject holder)
    {
        try { gltf.Dispose(); } catch (System.Exception) { }
        if (holder != null)
        {
            Object.Destroy(holder);
        }
    }

    // Uniform scale + ground + centre, using the RENDERED bounds (which
    // include the model's own node transforms — raw glTF accessor bounds
    // do not, and Sketchfab files are inconsistent there).
    private static void Normalize(GameObject holder, float targetSize, NormalizeMode mode, Vector3 pos)
    {
        Renderer[] renderers = holder.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            Debug.LogWarning("[Gltf] No renderers in " + holder.name + " — nothing to normalise");
            return;
        }
        Bounds b = renderers[0].bounds;
        foreach (Renderer r in renderers)
        {
            b.Encapsulate(r.bounds);
        }
        float dim = (mode == NormalizeMode.ByHeight)
            ? b.size.y : Mathf.Max(b.size.x, b.size.z);
        if (dim < 0.001f)
        {
            Debug.LogWarning("[Gltf] Degenerate bounds for " + holder.name + " — keeping original size");
            return;
        }
        float s = targetSize / dim;
        if (s < 0.0001f || s > 1000f)
        {
            Debug.LogWarning("[Gltf] Absurd scale " + s + " for " + holder.name + " — keeping original size");
            return;
        }
        holder.transform.localScale = Vector3.one * s;

        // Recompute after scaling, then shift the content (via a ModelRoot
        // wrapper, so the holder itself stays at its placement point) so
        // the footprint is centred on pos and the base sits on the ground.
        renderers = holder.GetComponentsInChildren<Renderer>();
        b = renderers[0].bounds;
        foreach (Renderer r in renderers)
        {
            b.Encapsulate(r.bounds);
        }
        Vector3 shift = new Vector3(pos.x - b.center.x, -b.min.y, pos.z - b.center.z);
        // Animated models (rigged characters): glTFast bakes the legacy
        // Animation clip paths relative to the holder. Wrapping the content
        // in a ModelRoot would insert one level and break those paths, so
        // apply the grounding shift to the single scene-root child directly
        // and skip the wrapper. (Only valid with exactly one child; the
        // shift is a world delta, the child lives in holder-local units,
        // hence shift / s — same math as the ModelRoot branch below.)
        if (holder.GetComponentInChildren<Animation>() != null && holder.transform.childCount == 1)
        {
            holder.transform.GetChild(0).localPosition += shift / s;
            return;
        }
        GameObject content = new GameObject("ModelRoot");
        content.transform.SetParent(holder.transform, false);
        Transform[] children = new Transform[holder.transform.childCount];
        int n = 0;
        foreach (Transform child in holder.transform)
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
        // Holder rotation is identity here, so a world shift maps to a
        // local shift divided by the holder's scale.
        content.transform.localPosition = shift / s;
    }

    private static void RegisterCollider(GameObject holder, ColliderMode mode)
    {
        if (mode == ColliderMode.Trunk)
        {
            // Trees: only the trunk blocks — the canopy must not wall off
            // a huge area.
            VillageColliders.Register(new Bounds(
                holder.transform.position + new Vector3(0f, 1.25f, 0f),
                new Vector3(0.7f, 2.5f, 0.7f)));
            return;
        }
        // FullBounds: recompute AFTER the yaw was applied (the footprint
        // changes for non-square models).
        Renderer[] renderers = holder.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;
        Bounds b = renderers[0].bounds;
        foreach (Renderer r in renderers)
        {
            b.Encapsulate(r.bounds);
        }
        VillageColliders.Register(b);
    }
}
