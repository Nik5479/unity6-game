using UnityEngine;

/// <summary>
/// Torch: a warm spotlight attached to the main camera (so it always
/// points where the player looks). Toggled by the TORCH button / T key.
/// Most useful at night; the chowk lamp + moonlight are the other
/// night light sources.
/// </summary>
public class TorchController : MonoBehaviour
{
    public bool IsOn { get; private set; }

    private Light torch;

    void Awake()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        GameObject go = new GameObject("TorchLight");
        go.transform.SetParent(cam.transform, false);
        go.transform.localPosition = new Vector3(0.25f, -0.18f, 0.35f);
        go.transform.localRotation = Quaternion.Euler(6f, 0f, 0f);
        torch = go.AddComponent<Light>();
        torch.type = LightType.Spot;
        torch.color = new Color(1f, 0.9f, 0.68f);
        torch.intensity = 0f;
        torch.range = 16f;
        torch.spotAngle = 42f;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            Toggle();
        }
    }

    public void Toggle()
    {
        IsOn = !IsOn;
        if (torch != null)
        {
            torch.intensity = IsOn ? 3.2f : 0f;
        }
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnTorchToggled(IsOn);
        }
    }
}
