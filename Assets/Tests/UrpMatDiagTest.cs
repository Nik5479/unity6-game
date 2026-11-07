using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// URP BLACK-MATERIAL DIAGNOSTIC (2026-11-07).
/// Some .glb models render as pure-black silhouettes under URP (thatched
/// hut, palms, animals) while others render fine. This test only LOGS:
/// the runtime ambient/sun state + per-material shader, _Metallic,
/// _BaseColor, mainTexture presence/size for the black suspects vs the
/// known-good ones. No assertions — evidence only.
/// </summary>
public class UrpMatDiagTest
{
    [UnityTest]
    public IEnumerator DiagnoseUrpMaterials()
    {
        if (!Application.isPlaying)
        {
            yield break;
        }

        yield return SceneManager.LoadSceneAsync("Main");

        yield return new WaitForSeconds(1f);
        float modelWait = 0f;
        while (GameObject.Find("ModelsLoaded") == null && modelWait < 150f)
        {
            yield return new WaitForSeconds(0.5f);
            modelWait += 0.5f;
        }
        Debug.Log("[URPDIAG] Waited " + modelWait + "s for models; loaded="
            + (GameObject.Find("ModelsLoaded") != null));
        yield return new WaitForSeconds(1.5f);

        // --- lighting / ambient state ---
        Debug.Log("[URPDIAG] AMBIENT mode=" + RenderSettings.ambientMode
            + " ambientLight=" + C(RenderSettings.ambientLight)
            + " ambientIntensity=" + RenderSettings.ambientIntensity
            + " fog=" + RenderSettings.fog
            + " fogColor=" + C(RenderSettings.fogColor));
        Light[] lights = Object.FindObjectsOfType<Light>();
        foreach (Light l in lights)
        {
            Debug.Log("[URPDIAG] LIGHT name=" + l.gameObject.name
                + " type=" + l.type
                + " intensity=" + l.intensity
                + " color=" + C(l.color)
                + " rot=" + F(l.transform.rotation.eulerAngles)
                + " shadows=" + l.shadows
                + " enabled=" + l.enabled);
        }
        Camera cam = Camera.main;
        if (cam != null)
        {
            Debug.Log("[URPDIAG] CAMERA pos=" + F(cam.transform.position)
                + " rot=" + F(cam.transform.rotation.eulerAngles)
                + " clear=" + cam.clearFlags);
        }
        Debug.Log("[URPDIAG] SKYBOX mat=" + (RenderSettings.skybox != null ? RenderSettings.skybox.name : "NULL")
            + " exposure=" + (RenderSettings.skybox != null && RenderSettings.skybox.HasProperty("_Exposure")
                ? RenderSettings.skybox.GetFloat("_Exposure").ToString("F2") : "n/a"));
        ReflectionProbe probe = Object.FindObjectOfType<ReflectionProbe>();
        Debug.Log("[URPDIAG] PROBE found=" + (probe != null)
            + (probe != null ? " mode=" + probe.mode + " size=" + F(probe.size)
                + " tex=" + (probe.texture != null ? probe.texture.width + "x" + probe.texture.height : "NULL") : ""));

        // --- material state: black suspects vs known-good ---
        Dump("House_Player");   // thatched hut — BLACK pyramid
        Dump("House_4");        // pointed thatch hut — BLACK
        Dump("Tree_Palm");      // palms — BLACK silhouettes (backlit?)
        Dump("Tree_Palm_Ring"); // a ring clone of the palm
        Dump("Tree_Pack");      // trees_25 cluster
        Dump("Cow");
        Dump("Goat");
        Dump("Buffalo");
        Dump("Horse");
        Dump("Wolf");
        Dump("Tree_Mango");     // known GOOD (green)
        Dump("Tree_Banyan");    // known GOOD (bright green)
        Dump("School");         // known GOOD

        Debug.Log("[URPDIAG] DONE");
        yield return null;
    }

    private static void Dump(string path)
    {
        GameObject go = GameObject.Find(path);
        if (go == null)
        {
            Debug.Log("[URPDIAG] " + path + " NOT FOUND");
            return;
        }
        Renderer[] rs = go.GetComponentsInChildren<Renderer>();
        Debug.Log("[URPDIAG] " + path + " rendererCount=" + rs.Length
            + " pos=" + F(go.transform.position));
        for (int i = 0; i < rs.Length && i < 12; i++)
        {
            Renderer r = rs[i];
            string mats = "";
            if (r.sharedMaterials != null)
            {
                for (int m = 0; m < r.sharedMaterials.Length; m++)
                {
                    Material mat = r.sharedMaterials[m];
                    if (mat == null)
                    {
                        mats += "[MAT_NULL] ";
                        continue;
                    }
                    string sh = (mat.shader != null) ? mat.shader.name : "<no-shader>";
                    string tex = "TEX_NULL";
                    if (mat.mainTexture != null)
                    {
                        tex = "TEX_OK " + mat.mainTexture.name
                            + " " + mat.mainTexture.width + "x" + mat.mainTexture.height;
                    }
                    string met = mat.HasProperty("_Metallic")
                        ? mat.GetFloat("_Metallic").ToString("F2") : "n/a";
                    string metF = mat.HasProperty("metallicFactor")
                        ? mat.GetFloat("metallicFactor").ToString("F2") : "n/a";
                    string mrTex = mat.HasProperty("metallicRoughnessTexture")
                        ? (mat.GetTexture("metallicRoughnessTexture") != null ? "MR_OK" : "MR_NULL") : "n/a";
                    string specF = mat.HasProperty("specularFactor")
                        ? "(" + mat.GetVector("specularFactor").x.ToString("F2") + ","
                            + mat.GetVector("specularFactor").y.ToString("F2") + ","
                            + mat.GetVector("specularFactor").z.ToString("F2") + ")" : "n/a";
                    string sm = mat.HasProperty("_Smoothness")
                        ? mat.GetFloat("_Smoothness").ToString("F2") : "n/a";
                    mats += "[" + sh + " met=" + met + " metF=" + metF + " " + mrTex
                        + " spec=" + specF
                        + " smooth=" + sm
                        + " color=" + C(mat.color) + " " + tex
                        + " queue=" + mat.renderQueue + "] ";
                }
            }
            Debug.Log("[URPDIAG] " + path + " R" + i + " " + r.GetType().Name
                + " go=" + r.gameObject.name
                + " pos=" + F(r.transform.position)
                + " boundsSize=" + F(r.bounds.size)
                + " mats=" + mats);
        }
    }

    private static string F(Vector3 v)
    {
        return "(" + v.x.ToString("F2") + "," + v.y.ToString("F2") + "," + v.z.ToString("F2") + ")";
    }

    private static string C(Color c)
    {
        return "(" + c.r.ToString("F2") + "," + c.g.ToString("F2") + "," + c.b.ToString("F2") + "," + c.a.ToString("F2") + ")";
    }
}
