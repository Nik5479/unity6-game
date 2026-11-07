using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// RUNTIME DIAGNOSTIC for the invisible-.glb bug (2026-11-07).
/// Loads Main, waits for ModelsLoaded, then dumps the RUNTIME state of
/// every .glb house / School / Nilesh: position, scale, renderer bounds,
/// material shader names, mainTexture presence, material color, and the
/// camera. No assertions that can fail — this test only LOGS evidence.
/// Does not reference any game class (test asmdef cannot see them).
/// </summary>
public class RenderDiagTest
{
    [UnityTest]
    public IEnumerator DiagnoseInvisibleModels()
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
        Debug.Log("[DIAG] Waited " + modelWait + "s for models; loaded="
            + (GameObject.Find("ModelsLoaded") != null));

        yield return new WaitForSeconds(1.5f);

        Camera cam = Camera.main;
        if (cam != null)
        {
            Debug.Log("[DIAG] CAMERA pos=" + F(cam.transform.position)
                + " rot=" + F(cam.transform.rotation.eulerAngles)
                + " farClip=" + cam.farClipPlane
                + " nearClip=" + cam.nearClipPlane
                + " cullingMask=" + cam.cullingMask
                + " enabled=" + cam.enabled);
        }
        else
        {
            Debug.Log("[DIAG] CAMERA main=null!");
        }

        Dump("House_Player");
        Dump("House_2");
        Dump("House_3");
        Dump("House_4");
        Dump("House_5");
        Dump("House_6");
        Dump("School");
        Dump("Player/Nilesh");
        Dump("Village");

        // Also dump every top-level child of Village with its renderer count,
        // so we can see which .glb objects exist at all.
        GameObject village = GameObject.Find("Village");
        if (village != null)
        {
            foreach (Transform child in village.transform)
            {
                Renderer[] rs = child.GetComponentsInChildren<Renderer>();
                Debug.Log("[DIAG] VILLAGE-CHILD name=" + child.name
                    + " active=" + child.gameObject.activeInHierarchy
                    + " pos=" + F(child.position)
                    + " lossyScale=" + F(child.lossyScale)
                    + " renderers=" + rs.Length);
            }
        }

        Debug.Log("[DIAG] DONE");
        yield return null;
    }

    private static void Dump(string path)
    {
        GameObject go = GameObject.Find(path);
        if (go == null)
        {
            Debug.Log("[DIAG] " + path + " NOT FOUND");
            return;
        }
        Debug.Log("[DIAG] " + path
            + " active=" + go.activeInHierarchy
            + " pos=" + F(go.transform.position)
            + " localScale=" + F(go.transform.localScale)
            + " lossyScale=" + F(go.transform.lossyScale)
            + " layer=" + go.layer);
        Renderer[] rs = go.GetComponentsInChildren<Renderer>();
        Debug.Log("[DIAG] " + path + " rendererCount=" + rs.Length);
        for (int i = 0; i < rs.Length; i++)
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
                    string tex = (mat.mainTexture != null) ? "TEX_OK" : "TEX_NULL";
                    mats += "[" + sh + " " + tex
                        + " color=" + C(mat.color)
                        + " queue=" + mat.renderQueue + "] ";
                }
            }
            Debug.Log("[DIAG] " + path + " R" + i + " " + r.GetType().Name
                + " go=" + r.gameObject.name
                + " enabled=" + r.enabled
                + " boundsCenter=" + F(r.bounds.center)
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
