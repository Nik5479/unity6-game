// Cheap-wins PBR wiring (2026-11-07) — one-shot editor script.
// Wires the Poly Haven normal / metallic-smoothness / AO maps into the
// 6 URP Lit .mat materials (diffuse maps are NOT changed) and fixes the
// import settings of every new map texture (normal maps -> NormalMap type,
// AO + metal-smoothness maps -> linear/sRGB off).
// Run: Unity -batchmode -executeMethod WirePbrMaps.Wire
// WoodPlanks is intentionally skipped: no matching Poly Haven texture
// exists (bark_brown_02 is tree bark, not planks — a wrong normal map
// would look worse than none).
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class WirePbrMaps
{
    // .mat name -> Poly Haven texture stem (semantic match to its diffuse)
    private static readonly Dictionary<string, string> kMatMap = new Dictionary<string, string>
    {
        { "GroundMud", "brown_mud_dry" },
        { "WallPlaster", "clay_block_wall" },
        { "RoofTiles", "clay_roof_tiles_02" },
        { "StoneWall", "cobblestone_floor_04" },
        { "FieldGrass", "withered_grass" },
    };

    // Stems whose maps are actually used (by the .mat files above and by
    // VillageBuilder.MakeMat). Bark + reed maps were moved out of the
    // project (~/workspace/polyhaven/unused-maps/) — no material uses them.
    private static readonly string[] kUsedStems =
    {
        "brick_4", "thatch_roof_angled", "clay_block_wall", "brown_mud_dry",
        "cobblestone_floor_04", "withered_grass", "clay_roof_tiles_02",
        "brown_mud_leaves_01",
    };

    private const string kTexDir = "Assets/Resources/Textures/";
    private const string kMatDir = "Assets/Resources/Materials/";

    [MenuItem("Tools/Cheap Wins/Wire PBR Maps")]
    public static void Wire()
    {
        // 1. Import settings for every used map, then reimport.
        foreach (string stem in kUsedStems)
        {
            SetNormalMap(kTexDir + stem + "_nor_gl_1k.jpg");
            SetLinear(kTexDir + stem + "_ao_1k.jpg");
            SetLinear(kTexDir + stem + "_metal_smooth_1k.png");
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 2. Wire the .mat assets. Tiling comes from each .mat's own
        // _BaseMap scale so the maps align with the diffuse tiling.
        foreach (KeyValuePair<string, string> kv in kMatMap)
        {
            Material m = AssetDatabase.LoadAssetAtPath<Material>(kMatDir + kv.Key + ".mat");
            if (m == null)
            {
                Debug.LogWarning("[WirePbrMaps] Material missing: " + kv.Key);
                continue;
            }
            Texture2D nor = AssetDatabase.LoadAssetAtPath<Texture2D>(kTexDir + kv.Value + "_nor_gl_1k.jpg");
            Texture2D ms = AssetDatabase.LoadAssetAtPath<Texture2D>(kTexDir + kv.Value + "_metal_smooth_1k.png");
            Texture2D ao = AssetDatabase.LoadAssetAtPath<Texture2D>(kTexDir + kv.Value + "_ao_1k.jpg");
            Vector2 tiling = m.mainTextureScale;
            if (nor != null)
            {
                m.SetTexture("_BumpMap", nor);
                m.SetTextureScale("_BumpMap", tiling);
                m.SetFloat("_BumpScale", 1f);
                m.EnableKeyword("_NORMALMAP");
            }
            else Debug.LogWarning("[WirePbrMaps] Normal map missing for " + kv.Key);
            if (ms != null)
            {
                m.SetTexture("_MetallicGlossMap", ms);
                m.SetTextureScale("_MetallicGlossMap", tiling);
                m.SetFloat("_Smoothness", 1f);
                m.SetFloat("_Glossiness", 1f);
                m.SetFloat("_SmoothnessTextureChannel", 0f);
                m.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            else Debug.LogWarning("[WirePbrMaps] Metal-smooth map missing for " + kv.Key);
            if (ao != null)
            {
                m.SetTexture("_OcclusionMap", ao);
                m.SetTextureScale("_OcclusionMap", tiling);
                m.SetFloat("_OcclusionStrength", 1f);
                m.EnableKeyword("_OCCLUSIONMAP");
            }
            else Debug.LogWarning("[WirePbrMaps] AO map missing for " + kv.Key);
            EditorUtility.SetDirty(m);
            Debug.Log("[WirePbrMaps] Wired " + kv.Key + " <- " + kv.Value + " (tiling " + tiling + ")");
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[WirePbrMaps] DONE");
    }

    private static void SetNormalMap(string path)
    {
        TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null)
        {
            Debug.LogWarning("[WirePbrMaps] Texture missing: " + path);
            return;
        }
        if (ti.textureType != TextureImporterType.NormalMap)
        {
            ti.textureType = TextureImporterType.NormalMap;
            ti.SaveAndReimport();
            Debug.Log("[WirePbrMaps] NormalMap type set: " + path);
        }
    }

    private static void SetLinear(string path)
    {
        TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null)
        {
            Debug.LogWarning("[WirePbrMaps] Texture missing: " + path);
            return;
        }
        if (ti.sRGBTexture)
        {
            ti.sRGBTexture = false;
            ti.SaveAndReimport();
            Debug.Log("[WirePbrMaps] sRGB off (linear): " + path);
        }
    }
}
