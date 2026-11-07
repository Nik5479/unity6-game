using System.Collections;
using System.Threading.Tasks;
using GLTFast;
using UnityEngine;

/// <summary>
/// Loads .glb models at runtime from Assets/StreamingAssets/Models/
/// via glTFast (com.atteneder.gltfast 6.20.0). Sketchfab models arrive in
/// wildly inconsistent units (metres, centimetres, millimetres, arbitrary),
/// so every model is normalised by its rendered bounds: uniform scale so
/// its target dimension matches, then grounded (bounds.min.y = 0) and
/// centred on its placement point. A model that fails to load is logged
/// and skipped — it must never kill the village. The holder GameObject is
/// created immediately (so GameObject.Find works before the load finishes)
/// and destroyed again on failure, so fallbacks can reuse the same name.
/// </summary>
public static class GltfModelLoader
{
    public enum NormalizeMode { ByWidth, ByHeight }
    public enum ColliderMode { None, FullBounds, Trunk }

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
                loadTask = gltf.LoadFile(fullPath);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("[Gltf] Load start failed: " + fileName + " — " + e.Message);
            Cleanup(gltf, holder);
            if (onDone != null) onDone(null);
            yield break;
        }

        yield return new WaitUntil(() => loadTask.IsCompleted);
        if (loadTask.IsFaulted || loadTask.IsCanceled || !loadTask.Result)
        {
            string why = (loadTask.Exception != null)
                ? loadTask.Exception.GetBaseException().Message : "Load returned false";
            Debug.LogError("[Gltf] Load failed: " + fileName + " — " + why);
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
            Debug.LogError("[Gltf] Instantiate start failed: " + fileName + " — " + e.Message);
            Cleanup(gltf, holder);
            if (onDone != null) onDone(null);
            yield break;
        }

        yield return new WaitUntil(() => instTask.IsCompleted);
        if (instTask.IsFaulted || instTask.IsCanceled || !instTask.Result)
        {
            Debug.LogError("[Gltf] Instantiate failed: " + fileName);
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

        gltf.Dispose();
        Debug.Log("[Gltf] Loaded: " + fileName + " as " + goName);
        if (onDone != null) onDone(holder);
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
