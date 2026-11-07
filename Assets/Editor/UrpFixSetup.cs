using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor.SceneManagement;

/// <summary>
/// One-shot URP fix setup (2026-11-07). Run once via:
///   Unity -batchmode -nographics -projectPath ... -executeMethod UrpFixSetup.Run -quit
/// Does: import the moonless HDRI + build a Panoramic skybox material,
/// assign it to RenderSettings.skybox, set Main Camera to Skybox clear,
/// wire the PostProcessProfile into the PostProcessVolume (its
/// sharedProfile was empty, so Bloom/Vignette/ACES never ran), and enable
/// soft shadows in the URP asset. Safe to re-run (idempotent).
/// NOTE: ambient stays Flat (animated by DayNightCycle) by design — see
/// DayNightCycle.SetSky. The skybox is the visible sky + the reflection
/// probe's environment, not the ambient source.
/// </summary>
public static class UrpFixSetup
{
    private const string kHdrPath = "Assets/Textures/moonless_golf_2k.hdr";
    private const string kSkyboxMatPath = "Assets/Settings/NightSkybox.mat";
    private const string kProfilePath = "Assets/Settings/PostProcessProfile.asset";
    private const string kScenePath = "Assets/Scenes/Main.unity";
    private const string kUrpAssetPath = "Assets/Settings/URP.asset";

    public static void Run()
    {
        // 1) HDR import settings (equirect 2D texture for Skybox/Panoramic).
        AssetDatabase.ImportAsset(kHdrPath, ImportAssetOptions.ForceUpdate);
        TextureImporter ti = AssetImporter.GetAtPath(kHdrPath) as TextureImporter;
        if (ti == null)
        {
            Debug.LogError("[URPFIX] HDR importer not found: " + kHdrPath);
            return;
        }
        ti.textureType = TextureImporterType.Default;
        ti.sRGBTexture = true;
        ti.mipmapEnabled = false;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.filterMode = FilterMode.Trilinear;
        ti.maxTextureSize = 2048;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.SaveAndReimport();
        Texture2D hdr = AssetDatabase.LoadAssetAtPath<Texture2D>(kHdrPath);
        Debug.Log("[URPFIX] HDR imported: " + (hdr != null ? hdr.width + "x" + hdr.height : "NULL"));

        // 2) Skybox material (Panoramic equirect).
        Shader pano = Shader.Find("Skybox/Panoramic");
        if (pano == null)
        {
            Debug.LogError("[URPFIX] Skybox/Panoramic shader not found.");
            return;
        }
        Material skyMat = AssetDatabase.LoadAssetAtPath<Material>(kSkyboxMatPath);
        if (skyMat == null)
        {
            skyMat = new Material(pano);
            skyMat.name = "NightSkybox";
            AssetDatabase.CreateAsset(skyMat, kSkyboxMatPath);
        }
        skyMat.shader = pano;
        skyMat.SetTexture("_MainTex", hdr);
        skyMat.SetFloat("_Exposure", 2.0f);
        skyMat.SetFloat("_Rotation", 0f);
        EditorUtility.SetDirty(skyMat);
        Debug.Log("[URPFIX] Skybox material ready: " + skyMat.name);

        // 3) Scene: skybox + camera + volume profile.
        var scene = EditorSceneManager.OpenScene(kScenePath, OpenSceneMode.Single);
        RenderSettings.skybox = skyMat;
        var camGo = GameObject.Find("Main Camera");
        if (camGo != null)
        {
            var cam = camGo.GetComponent<Camera>();
            if (cam != null) cam.clearFlags = CameraClearFlags.Skybox;
        }
        var volGo = GameObject.Find("PostProcessVolume");
        if (volGo != null)
        {
            var vol = volGo.GetComponent<Volume>();
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(kProfilePath);
            if (vol != null && profile != null)
            {
                vol.sharedProfile = profile;
                Debug.Log("[URPFIX] Volume profile wired: " + profile.name);
            }
            else
            {
                Debug.LogWarning("[URPFIX] Volume/profile missing: vol=" + (vol != null) + " profile=" + (profile != null));
            }
        }
        else
        {
            Debug.LogWarning("[URPFIX] PostProcessVolume not found in scene.");
        }

        // 4) URP asset: soft shadows on (the Directional Light asks for
        // Soft shadows but the pipeline asset had them disabled).
        UnityEngine.Object urp = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(kUrpAssetPath);
        if (urp != null)
        {
            var so = new SerializedObject(urp);
            var p = so.FindProperty("m_SoftShadowsSupported");
            if (p != null)
            {
                p.intValue = 1;
                so.ApplyModifiedProperties();
                Debug.Log("[URPFIX] Soft shadows enabled in URP asset.");
            }
            else
            {
                Debug.LogWarning("[URPFIX] m_SoftShadowsSupported property not found.");
            }
        }
        else
        {
            Debug.LogWarning("[URPFIX] URP asset not found: " + kUrpAssetPath);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[URPFIX] DONE — skybox=" + skyMat.name + ", camera Skybox clear, volume wired, soft shadows on.");
    }
}
