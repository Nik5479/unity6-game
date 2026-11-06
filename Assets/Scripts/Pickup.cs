using UnityEngine;

/// <summary>
/// Simple collectible (Phase 1: one food crate near the well).
/// Walk close and press ACTION to collect. Bobs gently so the
/// player can spot it in the dark.
/// </summary>
public class Pickup : MonoBehaviour
{
    public string itemName = "Khana";
    public float bobHeight = 0.12f;
    public bool Collected { get; private set; }

    private Vector3 basePos;

    void Awake()
    {
        basePos = transform.position;
    }

    void Update()
    {
        if (Collected) return;
        transform.position = basePos + new Vector3(0f, bobHeight * (0.5f + 0.5f * Mathf.Sin(Time.time * 2.5f)), 0f);
        transform.Rotate(0f, 25f * Time.deltaTime, 0f);
    }

    public void Collect()
    {
        if (Collected) return;
        Collected = true;
        gameObject.SetActive(false);
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPickupCollected(itemName);
        }
    }
}
