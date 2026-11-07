using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// One-shot URP migration for THE FORGOTTEN VILLAGE (Built-in -> URP 17.3).
/// Run via: -executeMethod UrpSetup.Run
/// Idempotent: re-running deletes and recreates the URP assets / Volume.
/// Does NOT build an APK or push anything.
/// </summary>
public static class UrpSetup
{
    const string kSettingsDir = "Assets/Settings";
    const string kUrpAssetPath = "Assets/Settings/URP.asset";
    const string kRendererPath = "Assets/Settings/URP-Renderer.asset";
    const string kProfilePath = "Assets/Settings/PostProcessProfile.asset";

    public static void Run()
    {
        Debug.Log("[URP] Setup starting...");
        CreateUrpAssets();
        ConvertMaterials();
        UpdateAlwaysIncludedShaders();
        SetupScene();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[URP] Setup complete.");
    }

    static void CreateUrpAssets()
    {
        if (!AssetDatabase.IsValidFolder(kSettingsDir))
            AssetDatabase.CreateFolder("Assets", "Settings");

        // Idempotency: remove previous run's assets.
        AssetDatabase.DeleteAsset(kUrpAssetPath);
        AssetDatabase.DeleteAsset(kRendererPath);
        AssetDatabase.DeleteAsset("Assets/UniversalRenderer.asset");

        var urpAsset = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();
        AssetDatabase.CreateAsset(urpAsset, kUrpAssetPath);

        // Creates Assets/UniversalRenderer.asset via the package's own API
        // (also wires the default PostProcessData into the renderer).
        var rendererData = urpAsset.LoadBuiltinRendererData();
        if (rendererData == null)
        {
            Debug.LogError("[URP] LoadBuiltinRendererData returned null — aborting.");
            return;
        }
        AssetDatabase.MoveAsset("Assets/UniversalRenderer.asset", kRendererPath);

        // Mobile-safe configuration for a phone horror game.
        urpAsset.supportsHDR = false;                 // HDR off (mobile perf)
        urpAsset.msaaSampleCount = 2;                 // 2x MSAA
        urpAsset.renderScale = 1f;
        // Shadow toggles: the asset defaults are already correct
        // (main light shadows ON, additional light shadows OFF).
        urpAsset.shadowDistance = 60f;
        urpAsset.shadowCascadeCount = 2;
        urpAsset.supportsCameraDepthTexture = false;
        urpAsset.supportsCameraOpaqueTexture = true;  // school window-glass transmission
        EditorUtility.SetDirty(urpAsset);

        // Assign as the active pipeline (all quality levels), back to Medium.
        GraphicsSettings.defaultRenderPipeline = urpAsset;
        for (int i = 0; i < QualitySettings.names.Length; i++)
        {
            QualitySettings.SetQualityLevel(i, false);
            QualitySettings.renderPipeline = urpAsset;
        }
        QualitySettings.SetQualityLevel(1, false);
        Debug.Log("[URP] Asset created + assigned (HDR off, MSAA 2x, shadows on, opaque texture on).");
    }

    static void ConvertMaterials()
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null)
        {
            Debug.LogError("[URP] URP Lit shader not found — material conversion aborted.");
            return;
        }
        string[] names = { "GroundMud", "WallPlaster", "WoodPlanks", "RoofTiles", "StoneWall", "FieldGrass" };
        int converted = 0;
        foreach (string name in names)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Materials/" + name + ".mat");
            if (mat == null) { Debug.LogWarning("[URP] Material not found: " + name); continue; }
            float gloss = mat.HasProperty("_Glossiness") ? mat.GetFloat("_Glossiness") : 0.5f;
            float metallic = mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : 0f;
            mat.shader = lit;
            mat.SetFloat("_Smoothness", gloss);
            mat.SetFloat("_Metallic", metallic);
            EditorUtility.SetDirty(mat);
            converted++;
            Debug.Log("[URP] Converted " + name + " to URP Lit (smoothness " + gloss + ").");
        }
        Debug.Log("[URP] Materials converted: " + converted + "/6.");
    }

    static void UpdateAlwaysIncludedShaders()
    {
        try
        {
            var gsObj = AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/GraphicsSettings.asset");
            if (gsObj == null) { Debug.LogWarning("[URP] GraphicsSettings.asset not found."); return; }
            var so = new SerializedObject(gsObj);
            var arr = so.FindProperty("m_AlwaysIncludedShaders");
            if (arr == null) { Debug.LogWarning("[URP] m_AlwaysIncludedShaders not found."); return; }

            var builtinGltfGuids = new HashSet<string>
            {
                "99fa998bbbed3408aafa652b466d261d", // glTFPbrMetallicRoughness (Built-in)
                "f853aafa0bc764a138f572608a37a73c", // glTFPbrSpecularGlossiness (Built-in)
                "4340a3cf1cde6416d957808a6ac79eed"  // glTFUnlit (Built-in)
            };
            int removed = 0;
            for (int i = arr.arraySize - 1; i >= 0; i--)
            {
                var e = arr.GetArrayElementAtIndex(i);
                var guidProp = e.FindPropertyRelative("guid");
                if (guidProp != null && builtinGltfGuids.Contains(guidProp.stringValue))
                {
                    arr.DeleteArrayElementAtIndex(i);
                    removed++;
                }
            }
            string[] sgPaths =
            {
                "Packages/com.atteneder.gltfast/Runtime/Shader/glTF-pbrMetallicRoughness.shadergraph",
                "Packages/com.atteneder.gltfast/Runtime/Shader/glTF-pbrSpecularGlossiness.shadergraph",
                "Packages/com.atteneder.gltfast/Runtime/Shader/glTF-unlit.shadergraph",
                "Packages/com.atteneder.gltfast/Runtime/Shader/URP/glTF-pbrMetallicRoughness-Clearcoat.shadergraph"
            };
            int added = 0;
            foreach (string p in sgPaths)
            {
                var sh = AssetDatabase.LoadAssetAtPath<Shader>(p);
                if (sh == null) { Debug.LogWarning("[URP] ShaderGraph not found: " + p); continue; }
                arr.arraySize++;
                arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = sh;
                added++;
            }
            so.ApplyModifiedProperties();
            Debug.Log("[URP] AlwaysIncludedShaders: removed " + removed + " Built-in glTF, added " + added + " URP shadergraphs.");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[URP] AlwaysIncludedShaders update failed (non-fatal): " + e.Message);
        }
    }

    static void SetupScene()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Main.unity", OpenSceneMode.Single);

        // Remove a previous run's Volume / profile.
        var oldVol = GameObject.Find("PostProcessVolume");
        if (oldVol != null) Object.DestroyImmediate(oldVol);
        AssetDatabase.DeleteAsset(kProfilePath);

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, kProfilePath);

        var bloom = profile.Add<Bloom>();
        AssetDatabase.AddObjectToAsset(bloom, profile);
        bloom.active = true;
        Set(bloom.intensity, 0.35f);
        Set(bloom.threshold, 1.0f);
        Set(bloom.scatter, 0.5f);

        var vig = profile.Add<Vignette>();
        AssetDatabase.AddObjectToAsset(vig, profile);
        vig.active = true;
        Set(vig.intensity, 0.28f);
        Set(vig.smoothness, 0.4f);

        var tm = profile.Add<Tonemapping>();
        AssetDatabase.AddObjectToAsset(tm, profile);
        tm.active = true;
        tm.mode.overrideState = true;
        tm.mode.value = TonemappingMode.ACES;

        var ca = profile.Add<ColorAdjustments>();
        AssetDatabase.AddObjectToAsset(ca, profile);
        ca.active = true;
        Set(ca.saturation, 8f);
        Set(ca.contrast, 6f);

        var volGo = new GameObject("PostProcessVolume");
        var volume = volGo.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 1;
        volume.profile = profile;

        // Camera: solid-color clear (no skybox material in the scene) so the
        // sky is never black under URP; DayNightCycle drives backgroundColor.
        var camGo = GameObject.Find("Main Camera");
        if (camGo != null)
        {
            var cam = camGo.GetComponent<Camera>();
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.Color;
                var uacd = cam.GetComponent<UniversalAdditionalCameraData>();
                if (uacd == null) uacd = camGo.AddComponent<UniversalAdditionalCameraData>();
                uacd.renderPostProcessing = true;
                Debug.Log("[URP] Main Camera: SolidColor clear + post-processing ON.");
            }
        }
        else
        {
            Debug.LogWarning("[URP] Main Camera not found in scene.");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[URP] Scene saved with global Volume (Bloom/Vignette/ACES/warmth).");
    }

    static void Set(UnityEngine.Rendering.FloatParameter p, float v)
    {
        p.overrideState = true;
        p.value = v;
    }
}
