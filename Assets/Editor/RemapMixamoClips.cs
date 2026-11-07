using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-shot editor tool: remaps the Mixamo Remy FBX animation clips onto
/// Nilesh (indian_man.glb). Both skeletons are the SAME Mixamo skeleton
/// (joints Hips_01, Spine_02, LeftArm_013, ...) but the rest poses differ
/// a lot — the shoulders differ by ~160 deg (Sketchfab/Blender conversion
/// re-rolled the bones), so a plain path rename or a per-bone rest delta
/// is NOT enough: the arm swing would come out in the wrong plane.
/// This tool does a full hierarchical (FK) retarget: for each frame it
/// computes Remy's bone WORLD rotations down the chain, then the Nilesh
/// local rotations that reproduce those same world orientations. That is
/// exact for rotations regardless of rest-pose differences.
///
/// Humanoid retargeting is NOT possible here: Nilesh is loaded at RUNTIME
/// via glTFast as a legacy Animation rig with no Avatar, so the clips are
/// remapped as legacy curves and saved as .anim assets that the runtime
/// NileshAnimator plays on Nilesh's Animation component.
///
/// Run from the menu: Forgotten Village -&gt; Remap Mixamo Clips to Nilesh.
/// Reads Assets/Editor/NileshRigMap.json (generated from the .glb).
/// Writes Assets/Resources/Animations/Nilesh{Idle,Walk,Run,Sprint}.anim.
/// </summary>
public static class RemapMixamoClips
{
    [System.Serializable]
    private class JointEntry
    {
        public string @base;
        public string full;
        public string parent;
        public float[] t;
        public float[] r;
    }

    [System.Serializable]
    private class RigMap
    {
        public string prefix;
        public float nativeHeight;
        public JointEntry[] joints;
    }

    private struct RestPose
    {
        public string full;
        public string parent;
        public Vector3 pos;
        public Quaternion rot;
    }

    private static readonly string[] kSourceFiles = { "Idle", "Walking", "Running", "Sprint" };
    private static readonly string[] kOutNames = { "NileshIdle", "NileshWalk", "NileshRun", "NileshSprint" };

    [MenuItem("Forgotten Village/Remap Mixamo Clips to Nilesh")]
    public static void Remap()
    {
        TextAsset mapAsset = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Editor/NileshRigMap.json");
        if (mapAsset == null)
        {
            Debug.LogError("[Remap] NileshRigMap.json not found at Assets/Editor/NileshRigMap.json");
            return;
        }
        RigMap map = JsonUtility.FromJson<RigMap>(mapAsset.text);
        var nilesh = new Dictionary<string, RestPose>();
        foreach (JointEntry j in map.joints)
        {
            nilesh[j.@base] = new RestPose
            {
                full = j.full,
                parent = j.parent,
                pos = new Vector3(j.t[0], j.t[1], j.t[2]),
                rot = new Quaternion(j.r[0], j.r[1], j.r[2], j.r[3])
            };
        }
        Debug.Log("[Remap] Rig map: " + nilesh.Count + " joints, prefix=" + map.prefix
            + ", nativeHeight=" + map.nativeHeight.ToString("F3"));

        for (int i = 0; i < kSourceFiles.Length; i++)
        {
            RemapOne("Assets/Mixamo/Remy-" + kSourceFiles[i] + ".fbx",
                "Assets/Resources/Animations/" + kOutNames[i] + ".anim",
                map, nilesh);
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Remap] DONE");
    }

    private static void RemapOne(string fbxPath, string outPath, RigMap map, Dictionary<string, RestPose> nilesh)
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (model == null)
        {
            Debug.LogError("[Remap] Model not found: " + fbxPath);
            return;
        }

        // Remy rest pose: bone local transforms from the imported model.
        var remy = new Dictionary<string, RestPose>();
        foreach (Transform tr in model.GetComponentsInChildren<Transform>(true))
        {
            if (tr.name.StartsWith("mixamorig:"))
                remy[tr.name.Substring("mixamorig:".Length)] = new RestPose { pos = tr.localPosition, rot = tr.localRotation };
        }
        // Remy height from the imported mesh (for the translation scale).
        // NOTE: the Mixamo FBX imports at ~2x scale (3.65 m tall), so the
        // scale is ~0.5 — that is real, not a bug (Hips sits at 2.07 m).
        float remyHeight = 1.8f;
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
        {
            if (o is Mesh mesh && mesh.bounds.size.y > 0.5f)
            {
                remyHeight = mesh.bounds.size.y;
                break;
            }
        }
        float posScale = map.nativeHeight / remyHeight;

        // The clip with the most curves is the animation take.
        AnimationClip src = null;
        int bestCount = 0;
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
        {
            if (o is AnimationClip clip)
            {
                int n = AnimationUtility.GetCurveBindings(clip).Length;
                if (n > bestCount) { bestCount = n; src = clip; }
            }
        }
        if (src == null)
        {
            Debug.LogError("[Remap] No AnimationClip found in " + fbxPath);
            return;
        }
        Debug.Log("[Remap] Source clip '" + src.name + "' (" + src.length.ToString("F2") + "s)");

        // Group bindings per bone (base name) + property kind.
        var rotCurves = new Dictionary<string, Dictionary<string, AnimationCurve>>();
        var posCurves = new Dictionary<string, Dictionary<string, AnimationCurve>>();
        var skippedProps = new Dictionary<string, int>();
        foreach (EditorCurveBinding b in AnimationUtility.GetCurveBindings(src))
        {
            if (b.type != typeof(Transform))
            {
                Bump(skippedProps, "non-Transform:" + b.type.Name);
                continue;
            }
            bool isRot = b.propertyName.StartsWith("m_LocalRotation");
            bool isPos = b.propertyName.StartsWith("m_LocalPosition");
            if (!isRot && !isPos)
            {
                Bump(skippedProps, b.propertyName);
                continue;
            }
            string baseName = BaseOfPath(b.path);
            if (baseName == null || !nilesh.ContainsKey(baseName) || !remy.ContainsKey(baseName))
            {
                Bump(skippedProps, "unmapped-path:" + b.path);
                continue;
            }
            var dict = isRot ? rotCurves : posCurves;
            if (!dict.TryGetValue(baseName, out var comps))
            {
                comps = new Dictionary<string, AnimationCurve>();
                dict[baseName] = comps;
            }
            string comp = b.propertyName.Substring(b.propertyName.Length - 1); // x/y/z/w
            comps[comp] = AnimationUtility.GetEditorCurve(src, b);
        }
        int skippedTotal = 0;
        foreach (var kv in skippedProps) skippedTotal += kv.Value;
        Debug.Log("[Remap] Kept rotBones=" + rotCurves.Count + ", posBones=" + posCurves.Count
            + ", skipped=" + skippedTotal + " " + string.Join(", ", skippedProps));

        // Top-down bone order (parents before children) from the rig map.
        var order = TopDownOrder(nilesh);

        // Global time grid: union of all rotation key times.
        var grid = new SortedSet<float>();
        foreach (var kv in rotCurves)
            foreach (var c in kv.Value.Values)
                foreach (Keyframe k in c.keys) grid.Add(k.time);
        if (grid.Count == 0)
        {
            Debug.LogError("[Remap] No rotation keys in " + fbxPath);
            return;
        }

        AnimationClip dst = new AnimationClip();
        dst.name = Path.GetFileNameWithoutExtension(outPath);
        dst.legacy = true;
        dst.wrapMode = WrapMode.Loop;
        dst.frameRate = 30f;

        // ---- FK retarget (rotations) ----
        // remyWorld[bone] and nileshWorld[bone] at the current time step.
        var remyWorld = new Dictionary<string, Quaternion>();
        var nileshWorld = new Dictionary<string, Quaternion>();
        // Output keys per bone.
        var outRot = new Dictionary<string, List<Quaternion>>();
        var outTime = new List<float>();
        foreach (float t in grid)
        {
            outTime.Add(t);
            remyWorld.Clear();
            nileshWorld.Clear();
            foreach (string bone in order)
            {
                // Remy local rotation at t (clip sample, or rest pose).
                Quaternion remyLocal;
                if (rotCurves.TryGetValue(bone, out var comps))
                {
                    remyLocal = new Quaternion(
                        Eval(comps, "x", t), Eval(comps, "y", t),
                        Eval(comps, "z", t), Eval(comps, "w", t));
                }
                else if (remy.TryGetValue(bone, out RestPose rr))
                {
                    remyLocal = rr.rot;
                }
                else
                {
                    continue;
                }
                Quaternion parentRemyWorld = ParentWorld(remyWorld, nilesh, bone);
                Quaternion parentNileshWorld = ParentWorld(nileshWorld, nilesh, bone);
                Quaternion rw = parentRemyWorld * remyLocal;
                remyWorld[bone] = rw;
                // Nilesh local that reproduces the same world orientation.
                Quaternion nLocal = Quaternion.Inverse(parentNileshWorld) * rw;
                nileshWorld[bone] = parentNileshWorld * nLocal;
                if (!outRot.TryGetValue(bone, out var list))
                {
                    list = new List<Quaternion>();
                    outRot[bone] = list;
                }
                list.Add(nLocal);
            }
        }
        foreach (var kv in outRot)
        {
            string newPath = BonePath(map, nilesh, kv.Key);
            var xs = new List<Keyframe>(); var ys = new List<Keyframe>();
            var zs = new List<Keyframe>(); var ws = new List<Keyframe>();
            for (int i = 0; i < kv.Value.Count; i++)
            {
                Quaternion q = kv.Value[i];
                float t = outTime[i];
                xs.Add(new Keyframe(t, q.x));
                ys.Add(new Keyframe(t, q.y));
                zs.Add(new Keyframe(t, q.z));
                ws.Add(new Keyframe(t, q.w));
            }
            dst.SetCurve(newPath, typeof(Transform), "m_LocalRotation.x", new AnimationCurve(xs.ToArray()));
            dst.SetCurve(newPath, typeof(Transform), "m_LocalRotation.y", new AnimationCurve(ys.ToArray()));
            dst.SetCurve(newPath, typeof(Transform), "m_LocalRotation.z", new AnimationCurve(zs.ToArray()));
            dst.SetCurve(newPath, typeof(Transform), "m_LocalRotation.w", new AnimationCurve(ws.ToArray()));
        }

        // ---- Translations (only Hips has position curves in Mixamo clips) ----
        // Delta from the bone's rest position, scaled to Nilesh's size.
        int posBones = 0;
        foreach (var kv in posCurves)
        {
            string bone = kv.Key;
            if (!nilesh.TryGetValue(bone, out RestPose nRest)) continue;
            if (!remy.TryGetValue(bone, out RestPose rRest)) continue;
            string newPath = BonePath(map, nilesh, bone);
            var times = new SortedSet<float>();
            foreach (var c in kv.Value.Values)
                foreach (Keyframe k in c.keys) times.Add(k.time);
            var xs = new List<Keyframe>(); var ys = new List<Keyframe>(); var zs = new List<Keyframe>();
            foreach (float t in times)
            {
                Vector3 p = new Vector3(Eval(kv.Value, "x", t), Eval(kv.Value, "y", t), Eval(kv.Value, "z", t));
                Vector3 np = nRest.pos + (p - rRest.pos) * posScale;
                xs.Add(new Keyframe(t, np.x));
                ys.Add(new Keyframe(t, np.y));
                zs.Add(new Keyframe(t, np.z));
            }
            dst.SetCurve(newPath, typeof(Transform), "m_LocalPosition.x", new AnimationCurve(xs.ToArray()));
            dst.SetCurve(newPath, typeof(Transform), "m_LocalPosition.y", new AnimationCurve(ys.ToArray()));
            dst.SetCurve(newPath, typeof(Transform), "m_LocalPosition.z", new AnimationCurve(zs.ToArray()));
            posBones++;
        }

        if (File.Exists(outPath)) AssetDatabase.DeleteAsset(outPath);
        AssetDatabase.CreateAsset(dst, outPath);
        Debug.Log("[Remap] WROTE " + outPath + " (" + dst.length.ToString("F2") + "s): rotBones=" + outRot.Count
            + ", posBones=" + posBones + ", frames=" + outTime.Count);
    }

    private static void Bump(Dictionary<string, int> d, string key)
    {
        d.TryGetValue(key, out int v);
        d[key] = v + 1;
    }

    private static float Eval(Dictionary<string, AnimationCurve> comps, string comp, float t)
    {
        return comps.TryGetValue(comp, out AnimationCurve c) ? c.Evaluate(t) : 0f;
    }

    // World rotation of a bone's parent (identity for the top bone).
    private static Quaternion ParentWorld(Dictionary<string, Quaternion> worlds, Dictionary<string, RestPose> nilesh, string bone)
    {
        if (nilesh.TryGetValue(bone, out RestPose rp) && rp.parent != null
            && worlds.TryGetValue(rp.parent, out Quaternion pw))
            return pw;
        return Quaternion.identity;
    }

    // Full slash path from the skeleton root's CHILD to this bone (joint
    // full names). The root joint itself is EXCLUDED: map.prefix already
    // ends at the root joint, so including it here doubled the segment
    // (".../_rootJoint/_rootJoint/Hips_01") and NO curve resolved — the
    // remapped clips played but Nilesh stayed in a T-pose (found
    // 2026-11-08 by VerifyAnimPathsTest). Returns "" for the root itself;
    // callers use map.prefix in that case.
    private static string FullPath(Dictionary<string, RestPose> nilesh, string bone)
    {
        var parts = new List<string>();
        string cur = bone;
        while (cur != null && nilesh.TryGetValue(cur, out RestPose rp) && rp.parent != null)
        {
            parts.Add(rp.full);
            cur = rp.parent;
        }
        parts.Reverse();
        return string.Join("/", parts);
    }

    // Path for a bone's curves: the rig-map prefix (path to the root
    // joint) plus the below-root path from FullPath().
    private static string BonePath(RigMap map, Dictionary<string, RestPose> nilesh, string bone)
    {
        string rel = FullPath(nilesh, bone);
        return string.IsNullOrEmpty(rel) ? map.prefix : map.prefix + "/" + rel;
    }

    // Top-down order: parents before children (by ancestor depth).
    private static List<string> TopDownOrder(Dictionary<string, RestPose> nilesh)
    {
        var depth = new Dictionary<string, int>();
        foreach (string b in nilesh.Keys) depth[b] = Depth(nilesh, b);
        var list = new List<string>(nilesh.Keys);
        list.Sort((a, b2) => depth[a].CompareTo(depth[b2]));
        return list;
    }

    private static int Depth(Dictionary<string, RestPose> nilesh, string bone)
    {
        int d = 0;
        string cur = bone;
        while (cur != null && nilesh.TryGetValue(cur, out RestPose rp) && rp.parent != null)
        {
            d++;
            cur = rp.parent;
        }
        return d;
    }

    // Last path segment's base bone name ("mixamorig:Hips" -> "Hips").
    // Returns null for an empty path.
    private static string BaseOfPath(string srcPath)
    {
        if (string.IsNullOrEmpty(srcPath)) return null;
        int slash = srcPath.LastIndexOf('/');
        string last = slash >= 0 ? srcPath.Substring(slash + 1) : srcPath;
        if (last.StartsWith("mixamorig:")) last = last.Substring("mixamorig:".Length);
        return last;
    }
}
