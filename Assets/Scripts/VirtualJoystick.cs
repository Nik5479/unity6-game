using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Virtual joystick (left side of the screen). Drag the knob;
/// Value is a clamped -1..1 vector, Y up = forward.
/// Attached at runtime by MobileControls to the joystick background.
/// </summary>
public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public Vector2 Value { get; private set; }

    private RectTransform bg;
    private RectTransform knob;
    private float radius = 70f;

    void Awake()
    {
        bg = GetComponent<RectTransform>();
        if (transform.childCount > 0)
        {
            knob = transform.GetChild(0).GetComponent<RectTransform>();
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (bg == null) return;
        Vector2 local;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(bg, eventData.position, eventData.pressEventCamera, out local))
        {
            return;
        }
        Vector2 v = local / radius;
        if (v.sqrMagnitude > 1f)
        {
            v.Normalize();
        }
        Value = v;
        if (knob != null)
        {
            knob.anchoredPosition = v * radius;
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        Value = Vector2.zero;
        if (knob != null)
        {
            knob.anchoredPosition = Vector2.zero;
        }
    }
}
