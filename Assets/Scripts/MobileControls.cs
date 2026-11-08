using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Mobile controls + HUD, built entirely at runtime (no hand-written
/// UI scene YAML). Creates:
///  - Canvas (ScreenSpaceOverlay + SafeArea panel) + EventSystem
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
    private CanvasGroup subtitleGroup;
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

    // ------------------------------------------------------------------
    // UI shader/material fix (magenta buttons on device).
    // Root cause: the scene has no Canvas, so the UI/Default shader was
    // stripped from the Android build and every runtime-built Image/Text
    // rendered with the magenta error material. The fix is two layers:
    //  (1) ProjectSettings/GraphicsSettings AlwaysIncludedShaders now
    //      lists the UI shaders, and (2) every Graphic created here gets
    //      an EXPLICIT material from Shader.Find + a real white sprite,
    //      so nothing depends on Unity's default UI material. If the
    //      shader is still missing, the error is logged (never silent).
    // ------------------------------------------------------------------
    private static Shader s_uiShader;
    private static Shader s_uiFontShader;
    private static Sprite s_whiteSprite;

    public static Shader UiDefaultShader()
    {
        if (s_uiShader == null)
        {
            try { s_uiShader = Shader.Find("UI/Default"); } catch (System.Exception) { s_uiShader = null; }
            if (s_uiShader == null)
            {
                Debug.LogWarning("[MobileControls] UI/Default shader NOT found — UI will render magenta!");
            }
        }
        return s_uiShader;
    }

    public static Shader UiDefaultFontShader()
    {
        if (s_uiFontShader == null)
        {
            try { s_uiFontShader = Shader.Find("UI/Default Font"); } catch (System.Exception) { s_uiFontShader = null; }
            if (s_uiFontShader == null)
            {
                // Fallback: UI/Default samples the font texture the same way.
                s_uiFontShader = UiDefaultShader();
            }
        }
        return s_uiFontShader;
    }

    // White 1x1 sprite shared by all runtime-built Images (an Image with a
    // null sprite is undefined behaviour on some drivers).
    public static Sprite WhiteSprite()
    {
        if (s_whiteSprite == null)
        {
            Texture2D t = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            t.SetPixel(0, 0, Color.white);
            t.Apply();
            s_whiteSprite = Sprite.Create(t, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f);
        }
        return s_whiteSprite;
    }

    // Give a runtime-built Graphic a real material + (for Images) a real
    // sprite. Safe to call from other builders (VillageBuilder boot text).
    public static void ApplyUiMaterial(Graphic g, bool isText)
    {
        if (g == null) return;
        try
        {
            Shader s = isText ? UiDefaultFontShader() : UiDefaultShader();
            if (s != null)
            {
                g.material = new Material(s);
            }
            if (!isText)
            {
                Image img = g as Image;
                if (img != null && img.sprite == null)
                {
                    img.sprite = WhiteSprite();
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[MobileControls] ApplyUiMaterial failed: " + e.Message);
        }
    }

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
        if (subtitleText != null)
        {
            if (Time.time > subtitleUntil)
            {
                if (subtitleQueued != null && Time.time < subtitleQueuedUntil)
                {
                    subtitleText.text = subtitleQueued;
                    subtitleUntil = subtitleQueuedUntil;
                    subtitleQueued = null;
                    if (subtitleGroup != null) subtitleGroup.alpha = 1f;
                }
                else
                {
                    subtitleText.text = "";
                    if (subtitleGroup != null) subtitleGroup.alpha = 0f;
                }
            }
            else if (subtitleGroup != null)
            {
                // Phase 1 (2026-11-08): fade the subtitle out over its
                // last 0.8 s instead of snapping it off — and it always
                // fades, never lingers on screen after the line ends.
                subtitleGroup.alpha = Mathf.Clamp01((subtitleUntil - Time.time) / 0.8f);
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
        if (subtitleGroup != null) subtitleGroup.alpha = 1f;
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
        // Canvas — Screen Space - OVERLAY (audit H1): the HUD used to sit on
        // a ScreenSpaceCamera canvas, so URP post-processing (tonemapping,
        // bloom, vignette) and MSAA ran over the controls and a wall closer
        // than 1 m could cover them. Overlay draws after the camera stack.
        // CI screenshots use adb screencap, which captures overlay UI too.
        GameObject canvasGO = new GameObject("MobileCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // Safe area (audit M5): every HUD element lives under a panel that
        // is fitted to Screen.safeArea, so notches / rounded corners never
        // cover the TORCH/LOCK buttons or the objective text.
        GameObject safeGO = new GameObject("SafeArea", typeof(RectTransform));
        RectTransform safeRect = safeGO.GetComponent<RectTransform>();
        safeRect.SetParent(canvasGO.transform, false);
        safeRect.anchorMin = Vector2.zero;
        safeRect.anchorMax = Vector2.one;
        safeRect.offsetMin = Vector2.zero;
        safeRect.offsetMax = Vector2.zero;
        safeGO.AddComponent<SafeAreaFitter>();
        Transform hudRoot = safeRect;

        // EventSystem (needed for touch buttons / joystick).
        GameObject esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();
        esGO.AddComponent<StandaloneInputModule>();

        // Objective (top-left). Phase 1 (2026-11-08): the old 900x44 box
        // at font 24 clipped long hints to one line on 20:9 phones —
        // 820x64 at font 22 gives the hint room for a second line.
        // Pivot fix (2026-11-08, emulator final shot): MakeText leaves
        // the default CENTRE pivot, so a top-left-anchored rect's top
        // half sat above the screen and the objective rendered half
        // cut off at the top edge. Pin the pivot to the top-left corner
        // so (16,-16) is the rect's top-left and the text sits fully
        // inside the safe area.
        objectiveText = MakeText(hudRoot, "Objective", new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(16f, -16f), new Vector2(820f, 64f), 22, TextAnchor.UpperLeft,
            "Bhairavpur — Nilesh");
        objectiveText.rectTransform.pivot = new Vector2(0f, 1f);
        Shadow objSh = objectiveText.gameObject.AddComponent<Shadow>();
        objSh.effectColor = new Color(0f, 0f, 0f, 0.9f);
        objSh.effectDistance = new Vector2(2f, -2f);

        // Subtitle (bottom-centre, ABOVE the button row: y=210 clears the
        // joystick top (182) and RUN button top (165) — controls redesign
        // shrank the controls, so the subtitle moved down from y=300).
        subtitleText = MakeText(hudRoot, "Subtitle", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 210f), new Vector2(900f, 70f), 26, TextAnchor.MiddleCenter, "");
        Shadow sh = subtitleText.gameObject.AddComponent<Shadow>();
        sh.effectColor = new Color(0f, 0f, 0f, 0.9f);
        sh.effectDistance = new Vector2(2f, -2f);
        // Phase 1: CanvasGroup drives the fade-out (see Update).
        subtitleGroup = subtitleText.gameObject.AddComponent<CanvasGroup>();
        subtitleGroup.alpha = 0f;

        // Centre announcement (hidden).
        centerText = MakeText(hudRoot, "CenterText", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 60f), new Vector2(1000f, 160f), 44, TextAnchor.MiddleCenter, "");
        Shadow ctrSh = centerText.gameObject.AddComponent<Shadow>();
        ctrSh.effectColor = new Color(0f, 0f, 0f, 0.9f);
        ctrSh.effectDistance = new Vector2(3f, -3f);
        centerText.gameObject.SetActive(false);

        // Restart button (hidden).
        restartButton = MakeButton(hudRoot, "Restart", "DOBARA KHELO",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -90f), new Vector2(260f, 80f), 28);
        restartButton.onClick.AddListener(delegate
        {
            AudioManager.PlayUIClick();
            if (gameManager != null) gameManager.Restart();
        });
        restartButton.gameObject.SetActive(false);

        // Joystick (bottom-left) — controls redesign 2026-11-07: the old
        // 210 px flat-grey disc read as a plain grey blob. Now 140 px
        // (~2/3), darker defined base + light rim + 4 directional
        // triangles (like the reference image) + brighter knob.
        // CHILD ORDER MATTERS: VirtualJoystick takes GetChild(0) as the
        // knob, so the knob is added FIRST; ring + triangles come after.
        GameObject joyBG = new GameObject("Joystick");
        RectTransform joyRect = joyBG.AddComponent<RectTransform>();
        joyRect.SetParent(hudRoot, false);
        joyRect.anchorMin = new Vector2(0f, 0f);
        joyRect.anchorMax = new Vector2(0f, 0f);
        joyRect.anchoredPosition = new Vector2(112f, 112f);
        joyRect.sizeDelta = new Vector2(140f, 140f);
        Image joyImg = joyBG.AddComponent<Image>();
        joyImg.color = new Color(0.08f, 0.08f, 0.12f, 0.38f);
        ApplyUiMaterial(joyImg, false);
        joyImg.sprite = ControlIcons.CircleSprite();
        GameObject knob = new GameObject("Knob");
        RectTransform knobRect = knob.AddComponent<RectTransform>();
        knobRect.SetParent(joyRect, false);
        knobRect.anchoredPosition = Vector2.zero;
        knobRect.sizeDelta = new Vector2(64f, 64f);
        Image knobImg = knob.AddComponent<Image>();
        knobImg.color = new Color(1f, 1f, 1f, 0.5f);
        ApplyUiMaterial(knobImg, false);
        knobImg.sprite = ControlIcons.CircleSprite();
        joystick = joyBG.AddComponent<VirtualJoystick>();
        joystick.radius = 40f;
        // Rim ring (decorative — must not eat touches).
        GameObject joyRing = new GameObject("Ring");
        RectTransform ringRect = joyRing.AddComponent<RectTransform>();
        ringRect.SetParent(joyRect, false);
        ringRect.anchorMin = Vector2.zero;
        ringRect.anchorMax = Vector2.one;
        ringRect.offsetMin = Vector2.zero;
        ringRect.offsetMax = Vector2.zero;
        Image ringImg = joyRing.AddComponent<Image>();
        ringImg.color = new Color(1f, 1f, 1f, 0.35f);
        ringImg.raycastTarget = false;
        ApplyUiMaterial(ringImg, false);
        ringImg.sprite = ControlIcons.RingSprite();
        // Directional triangles at N/E/S/W (decorative, no raycast).
        AddJoyDirection(joyRect, new Vector2(0f, 48f), 0f);
        AddJoyDirection(joyRect, new Vector2(48f, 0f), -90f);
        AddJoyDirection(joyRect, new Vector2(0f, -48f), 180f);
        AddJoyDirection(joyRect, new Vector2(-48f, 0f), 90f);

        // Buttons (right side) — ROUND with symbols, no text labels.
        // Controls redesign 2026-11-07: ~2/3 size (Action 130->86,
        // Jump/Run 100->66, Torch/Lock 90->60), tighter cluster, icons
        // bigger relative to the button (72%, was 62%) + a light rim so
        // the symbols read clearly. Same anchors/functions as before.
        Button actionBtn = MakeIconButton(hudRoot, "ActionBtn", ControlIcons.IconKind.Hand,
            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-62f, 62f), new Vector2(86f, 86f));
        actionBtn.onClick.AddListener(delegate { AudioManager.PlayUIClick(); if (player != null) player.Interact(); });

        Button jumpBtn = MakeIconButton(hudRoot, "JumpBtn", ControlIcons.IconKind.Jump,
            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-158f, 56f), new Vector2(66f, 66f));
        jumpBtn.onClick.AddListener(delegate { AudioManager.PlayUIClick(); if (player != null) player.Jump(); });

        Button runBtn = MakeIconButton(hudRoot, "RunBtn", ControlIcons.IconKind.Run,
            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-158f, 132f), new Vector2(66f, 66f));
        AddHold(runBtn, delegate(bool held) { if (player != null) player.runHeld = held; });

        // OVERNIGHT R3: CROUCH button (hold) — left of the Run/Jump pair.
        Button crouchBtn = MakeIconButton(hudRoot, "CrouchBtn", ControlIcons.IconKind.Crouch,
            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-238f, 94f), new Vector2(66f, 66f));
        AddHold(crouchBtn, delegate(bool held) { if (player != null) player.crouchHeld = held; });

        torchButton = MakeIconButton(hudRoot, "TorchBtn", ControlIcons.IconKind.Flame,
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-54f, -32f), new Vector2(60f, 60f));
        torchButton.onClick.AddListener(delegate { AudioManager.PlayUIClick(); if (torch != null) torch.Toggle(); });

        lockButton = MakeIconButton(hudRoot, "LockBtn", ControlIcons.IconKind.Lock,
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-154f, -32f), new Vector2(60f, 60f));
        lockButton.onClick.AddListener(delegate { AudioManager.PlayUIClick(); if (gameManager != null) gameManager.ToggleLock(); });
    }

    // One joystick direction triangle (decorative — never eats touches).
    private void AddJoyDirection(RectTransform parent, Vector2 pos, float rotZ)
    {
        GameObject tri = new GameObject("Dir");
        RectTransform rt = tri.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(22f, 22f);
        rt.localRotation = Quaternion.Euler(0f, 0f, rotZ);
        Image img = tri.AddComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.5f);
        img.raycastTarget = false;
        ApplyUiMaterial(img, false);
        img.sprite = ControlIcons.TriangleSprite();
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
        t.fontStyle = FontStyle.Bold;
        t.alignment = align;
        t.color = Color.white;
        t.text = content;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        ApplyUiMaterial(t, true);
        if (t.font == null)
        {
            Debug.LogWarning("[MobileControls] UI font is NULL for text '" + name + "' — labels will be invisible!");
        }
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
        ApplyUiMaterial(img, false);
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
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.text = label;
        ApplyUiMaterial(t, true);
        Shadow ls = labelGO.AddComponent<Shadow>();
        ls.effectColor = new Color(0f, 0f, 0f, 0.9f);
        ls.effectDistance = new Vector2(2f, -2f);
        return b;
    }

    // Round control button with a symbol icon — no text label.
    // Background is the procedural circle sprite (dark, semi-transparent);
    // the icon is a white symbol child, centred at ~72% of the button
    // (controls redesign: was 62% — symbols must read clearly at the
    // smaller ~2/3 size). A thin light rim outlines the button.
    private Button MakeIconButton(Transform parent, string name, ControlIcons.IconKind icon,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size)
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
        ApplyUiMaterial(img, false);
        img.sprite = ControlIcons.CircleSprite();
        Button b = go.AddComponent<Button>();
        b.targetGraphic = img;

        GameObject rimGO = new GameObject("Rim");
        RectTransform rimRt = rimGO.AddComponent<RectTransform>();
        rimRt.SetParent(rt, false);
        rimRt.anchorMin = Vector2.zero;
        rimRt.anchorMax = Vector2.one;
        rimRt.offsetMin = Vector2.zero;
        rimRt.offsetMax = Vector2.zero;
        Image rimImg = rimGO.AddComponent<Image>();
        rimImg.color = new Color(1f, 1f, 1f, 0.22f);
        rimImg.raycastTarget = false;
        ApplyUiMaterial(rimImg, false);
        rimImg.sprite = ControlIcons.RingSprite();

        GameObject iconGO = new GameObject("Icon");
        RectTransform irt = iconGO.AddComponent<RectTransform>();
        irt.SetParent(rt, false);
        irt.anchorMin = new Vector2(0.14f, 0.14f);
        irt.anchorMax = new Vector2(0.86f, 0.86f);
        irt.offsetMin = Vector2.zero;
        irt.offsetMax = Vector2.zero;
        Image iconImg = iconGO.AddComponent<Image>();
        iconImg.color = Color.white;
        ApplyUiMaterial(iconImg, false);
        iconImg.sprite = ControlIcons.IconSprite(icon);
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
