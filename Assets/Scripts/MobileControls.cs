using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Mobile controls + HUD, built entirely at runtime (no hand-written
/// UI scene YAML). Creates:
///  - Canvas (ScreenSpaceOverlay) + EventSystem
///  - virtual joystick (bottom-left), buttons RUN / JUMP / ACTION /
///    TORCH / LOCK (bottom-right + top-right)
///  - subtitle line (monster's mimicked voice), objective (top-left),
///    centre announcement + restart button
/// All text is Roman Hinglish (ASCII) so the built-in font renders it.
/// </summary>
public class MobileControls : MonoBehaviour
{
    public PlayerController player;
    public TorchController torch;
    public GameManager gameManager;

    private VirtualJoystick joystick;
    private Text subtitleText;
    private Text objectiveText;
    private Text centerText;
    private Button restartButton;
    private Button torchButton;
    private Button lockButton;
    private float subtitleUntil;
    private string subtitleQueued;
    private float subtitleQueuedUntil;

    private static readonly Color kBtnColor = new Color(0.12f, 0.12f, 0.16f, 0.62f);
    private static readonly Color kBtnOnColor = new Color(0.85f, 0.55f, 0.15f, 0.85f);

    void Awake()
    {
        BuildUI();
    }

    void Update()
    {
        if (player != null && joystick != null)
        {
            player.moveInput = joystick.Value;
        }
        if (subtitleText != null && Time.time > subtitleUntil)
        {
            if (subtitleQueued != null && Time.time < subtitleQueuedUntil)
            {
                subtitleText.text = subtitleQueued;
                subtitleUntil = subtitleQueuedUntil;
                subtitleQueued = null;
            }
            else
            {
                subtitleText.text = "";
            }
        }
        // E = interact on keyboard.
        if (Input.GetKeyDown(KeyCode.E) && player != null)
        {
            player.Interact();
        }
        // L = lock on keyboard.
        if (Input.GetKeyDown(KeyCode.L) && gameManager != null)
        {
            gameManager.ToggleLock();
        }
    }

    public void ShowSubtitle(string text, float duration)
    {
        if (subtitleText == null) return;
        if (Time.time < subtitleUntil)
        {
            // A line is already showing — queue this one after it.
            subtitleQueued = text;
            subtitleQueuedUntil = subtitleUntil + duration;
            return;
        }
        subtitleText.text = text;
        subtitleUntil = Time.time + duration;
    }

    public void SetObjective(string text)
    {
        if (objectiveText != null)
        {
            objectiveText.text = text;
        }
    }

    public void ShowCenter(string text, bool isBad)
    {
        if (centerText != null)
        {
            centerText.text = text;
            centerText.color = isBad ? new Color(1f, 0.25f, 0.2f) : new Color(0.5f, 1f, 0.55f);
            centerText.gameObject.SetActive(true);
        }
    }

    public void ShowRestartButton(bool show)
    {
        if (restartButton != null)
        {
            restartButton.gameObject.SetActive(show);
        }
    }

    public void SetTorchState(bool on)
    {
        if (torchButton != null)
        {
            torchButton.GetComponent<Image>().color = on ? kBtnOnColor : kBtnColor;
        }
    }

    public void SetLockState(bool locked)
    {
        if (lockButton != null)
        {
            lockButton.GetComponent<Image>().color = locked ? kBtnOnColor : kBtnColor;
        }
    }

    // ------------------------------------------------------------------
    // UI construction
    // ------------------------------------------------------------------
    private Font uiFont;

    private Font GetFont()
    {
        if (uiFont == null)
        {
            uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        if (uiFont == null)
        {
            uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
        return uiFont;
    }

    private void BuildUI()
    {
        // Canvas.
        GameObject canvasGO = new GameObject("MobileCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // EventSystem (needed for touch buttons / joystick).
        GameObject esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();
        esGO.AddComponent<StandaloneInputModule>();

        // Objective (top-left).
        objectiveText = MakeText(canvasGO.transform, "Objective", new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(16f, -16f), new Vector2(900f, 44f), 24, TextAnchor.UpperLeft,
            "Bhairavpur — Nilesh");

        // Subtitle (bottom-centre).
        subtitleText = MakeText(canvasGO.transform, "Subtitle", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 185f), new Vector2(980f, 70f), 26, TextAnchor.MiddleCenter, "");
        Shadow sh = subtitleText.gameObject.AddComponent<Shadow>();
        sh.effectColor = new Color(0f, 0f, 0f, 0.9f);
        sh.effectDistance = new Vector2(2f, -2f);

        // Centre announcement (hidden).
        centerText = MakeText(canvasGO.transform, "CenterText", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 60f), new Vector2(1000f, 160f), 44, TextAnchor.MiddleCenter, "");
        centerText.gameObject.SetActive(false);

        // Restart button (hidden).
        restartButton = MakeButton(canvasGO.transform, "Restart", "DOBARA KHELO",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -90f), new Vector2(260f, 80f), 28);
        restartButton.onClick.AddListener(delegate
        {
            if (gameManager != null) gameManager.Restart();
        });
        restartButton.gameObject.SetActive(false);

        // Joystick (bottom-left).
        GameObject joyBG = new GameObject("Joystick");
        RectTransform joyRect = joyBG.AddComponent<RectTransform>();
        joyRect.SetParent(canvasGO.transform, false);
        joyRect.anchorMin = new Vector2(0f, 0f);
        joyRect.anchorMax = new Vector2(0f, 0f);
        joyRect.anchoredPosition = new Vector2(150f, 150f);
        joyRect.sizeDelta = new Vector2(210f, 210f);
        Image joyImg = joyBG.AddComponent<Image>();
        joyImg.color = new Color(1f, 1f, 1f, 0.14f);
        joystick = joyBG.AddComponent<VirtualJoystick>();
        GameObject knob = new GameObject("Knob");
        RectTransform knobRect = knob.AddComponent<RectTransform>();
        knobRect.SetParent(joyRect, false);
        knobRect.anchoredPosition = Vector2.zero;
        knobRect.sizeDelta = new Vector2(96f, 96f);
        Image knobImg = knob.AddComponent<Image>();
        knobImg.color = new Color(1f, 1f, 1f, 0.35f);

        // Buttons (right side).
        Button actionBtn = MakeButton(canvasGO.transform, "ActionBtn", "ACTION",
            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-90f, 90f), new Vector2(130f, 130f), 26);
        actionBtn.onClick.AddListener(delegate { if (player != null) player.Interact(); });

        Button jumpBtn = MakeButton(canvasGO.transform, "JumpBtn", "JUMP",
            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-240f, 80f), new Vector2(100f, 100f), 24);
        jumpBtn.onClick.AddListener(delegate { if (player != null) player.Jump(); });

        Button runBtn = MakeButton(canvasGO.transform, "RunBtn", "RUN",
            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-240f, 200f), new Vector2(100f, 100f), 24);
        AddHold(runBtn, delegate(bool held) { if (player != null) player.runHeld = held; });

        torchButton = MakeButton(canvasGO.transform, "TorchBtn", "TORCH",
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-80f, -46f), new Vector2(130f, 60f), 22);
        torchButton.onClick.AddListener(delegate { if (torch != null) torch.Toggle(); });

        lockButton = MakeButton(canvasGO.transform, "LockBtn", "LOCK",
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-230f, -46f), new Vector2(130f, 60f), 22);
        lockButton.onClick.AddListener(delegate { if (gameManager != null) gameManager.ToggleLock(); });
    }

    private Text MakeText(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 anchoredPos, Vector2 size, int fontSize, TextAnchor align, string content)
    {
        GameObject go = new GameObject(name);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        Text t = go.AddComponent<Text>();
        t.font = GetFont();
        t.fontSize = fontSize;
        t.alignment = align;
        t.color = Color.white;
        t.text = content;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    private Button MakeButton(Transform parent, string name, string label,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size, int fontSize)
    {
        GameObject go = new GameObject(name);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        Image img = go.AddComponent<Image>();
        img.color = kBtnColor;
        Button b = go.AddComponent<Button>();
        b.targetGraphic = img;

        GameObject labelGO = new GameObject("Label");
        RectTransform lrt = labelGO.AddComponent<RectTransform>();
        lrt.SetParent(rt, false);
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;
        Text t = labelGO.AddComponent<Text>();
        t.font = GetFont();
        t.fontSize = fontSize;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.text = label;
        return b;
    }

    // Hold-to-run: pointer down/up sets the flag.
    private void AddHold(Button button, System.Action<bool> onHold)
    {
        EventTrigger trigger = button.gameObject.AddComponent<EventTrigger>();
        EventTrigger.Entry down = new EventTrigger.Entry();
        down.eventID = EventTriggerType.PointerDown;
        down.callback.AddListener(delegate(BaseEventData e) { onHold(true); });
        trigger.triggers.Add(down);
        EventTrigger.Entry up = new EventTrigger.Entry();
        up.eventID = EventTriggerType.PointerUp;
        up.callback.AddListener(delegate(BaseEventData e) { onHold(false); });
        trigger.triggers.Add(up);
        EventTrigger.Entry exit = new EventTrigger.Entry();
        exit.eventID = EventTriggerType.PointerExit;
        exit.callback.AddListener(delegate(BaseEventData e) { onHold(false); });
        trigger.triggers.Add(exit);
    }
}
