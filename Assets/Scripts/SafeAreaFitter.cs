using UnityEngine;

/// <summary>
/// Fits its RectTransform to Screen.safeArea (notch / rounded corners /
/// gesture bars) and re-fits when the safe area or orientation changes.
/// Added by MobileControls to the HUD root (audit M5).
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SafeAreaFitter : MonoBehaviour
{
    private RectTransform rect;
    private Rect lastSafe;
    private Vector2Int lastSize;

    void Awake()
    {
        rect = GetComponent<RectTransform>();
        Apply();
    }

    void Update()
    {
        if (Screen.safeArea != lastSafe || Screen.width != lastSize.x || Screen.height != lastSize.y)
            Apply();
    }

    private void Apply()
    {
        lastSafe = Screen.safeArea;
        lastSize = new Vector2Int(Screen.width, Screen.height);
        if (Screen.width <= 0 || Screen.height <= 0) return;
        Vector2 min = lastSafe.position;
        Vector2 max = lastSafe.position + lastSafe.size;
        min.x /= Screen.width; min.y /= Screen.height;
        max.x /= Screen.width; max.y /= Screen.height;
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
