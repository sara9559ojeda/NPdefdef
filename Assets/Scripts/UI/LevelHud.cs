using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR;

/// <summary>
/// The one in-level HUD, identical in the five levels. It replaces the floating panels each level used to build,
/// which sat at a fixed distance and ended up inside walls and furniture.
/// - Wrist panel: mission, progress and a one-line reminder of the controls. In the headset it floats just above
///   the left controller and is shown only while the player raises that hand and looks at it, so it never covers
///   the scene, never crosses scenery and does not appear by accident (angle and distance thresholds with
///   hysteresis, plus a short dwell time). It is hidden while a session panel is open.
/// - View cue: a small ring near the centre of the view that fills while a gaze timer is counting (the player
///   cannot look at the wrist and at the target at once), and a one-line hint used to teach where the wrist
///   panel is until the player has looked at it once.
/// Without a headset (Editor, desktop) the panel is drawn in the lower-left corner of the screen instead.
/// Levels talk to it only through the static methods; it is created on first use.
/// </summary>
public sealed class LevelHud : MonoBehaviour
{
    public const string FindPanelHint = "Levanta la mano izquierda y mírala para ver tu misión.";

    private const string BaseControls = "Stick izq.: moverte · Stick der.: girar · Botón menú o B: pausa";
    private static readonly string[] LevelControls =
    {
        BaseControls,
        BaseControls + "\nA junto al sillón: sentarte o levantarte",
        BaseControls + "\nGrip o gatillo: agarrar el orbe · Pulsar stick izq.: correr",
        BaseControls,
        BaseControls + "\nGatillo o grip sobre lo iluminado: abrir, deslizar o pulsar"
    };

    // Wrist panel (canvas units and perceived metres).
    private static readonly Vector2 PanelSize = new Vector2(520f, 400f);
    private const float PanelWidthMeters = 0.2f;
    private const float PanelLiftMeters = 0.1f;
    private const float ShowAngle = 22f;
    private const float HideAngle = 32f;
    private const float MaxViewDistanceMeters = 0.75f;
    private const float ShowDwellSeconds = 0.2f;
    private const float FadeSpeed = 8f;
    private const float ViewedAfterSeconds = 0.6f;

    // View cue.
    private static readonly Vector2 CueSize = new Vector2(900f, 260f);
    private const float CueWidthMeters = 0.62f;
    private const float CueDistanceMeters = 1f;
    private const float CueDegreesBelowCentre = 11f;
    private const float FindPanelHintMaxSeconds = 30f;

    private const int SortingOrder = 500;

    private static LevelHud instance;

    private Canvas panelCanvas;
    private CanvasGroup panelGroup;
    private RectTransform panel;
    private TMP_Text titleText;
    private TMP_Text bodyText;
    private TMP_Text controlsText;
    private Image progressFill;
    private GameObject progressBar;

    private Canvas cueCanvas;
    private RectTransform cue;
    private Image gazeRing;
    private GameObject gazeRingRoot;
    private TMP_Text hintText;

    private XROrigin origin;
    private Transform leftController;
    private bool xrLayout;
    private bool layoutApplied;
    private bool panelWanted;
    private float lookingSeconds;
    private bool viewedOnce;
    private float playSeconds;
    private bool leftHandCueSent;
    private string timedHint;
    private float timedHintUntil;
    private bool gazeVisible;

    /// <summary>True once the player has looked at the wrist panel in this level.</summary>
    public static bool ViewedOnce => instance != null && instance.viewedOnce;

    /// <summary>Mission text currently shown (for tests).</summary>
    public static string MissionBody => instance != null ? instance.bodyText.text : string.Empty;

    /// <summary>Sets the mission shown on the wrist panel. A negative progress hides the progress bar.</summary>
    public static void SetMission(string title, string body, float progress = -1f)
    {
        LevelHud hud = GetOrCreate();
        if (hud == null) return;
        hud.titleText.text = title;
        hud.bodyText.text = body;
        hud.progressBar.SetActive(progress >= 0f);
        hud.progressFill.fillAmount = Mathf.Clamp01(progress);
    }

    /// <summary>Shows or hides the gaze ring near the centre of the view and sets how full it is (0-1).</summary>
    public static void SetGaze(float progress, bool visible)
    {
        LevelHud hud = GetOrCreate();
        if (hud == null) return;
        hud.gazeVisible = visible;
        hud.gazeRing.fillAmount = Mathf.Clamp01(progress);
    }

    /// <summary>Shows a one-line hint near the bottom of the view for a few seconds.</summary>
    public static void ShowHint(string text, float seconds)
    {
        LevelHud hud = GetOrCreate();
        if (hud == null) return;
        hud.timedHint = text;
        hud.timedHintUntil = Time.unscaledTime + seconds;
    }

    private static LevelHud GetOrCreate()
    {
        if (instance != null) return instance;
        int level = LevelScenes.Number(SceneManager.GetActiveScene().name);
        if (level == 0) return null;
        instance = new GameObject("NP_LevelHud").AddComponent<LevelHud>();
        instance.Build(LevelControls[level - 1]);
        return instance;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // ------------------------------------------------------------------ construction

    private void Build(string controls)
    {
        panelCanvas = CreateCanvas("WristPanel", out RectTransform panelRoot);
        panelGroup = panelCanvas.gameObject.AddComponent<CanvasGroup>();
        panelGroup.alpha = 0f;
        panelGroup.interactable = false;
        panelGroup.blocksRaycasts = false;

        panel = CreateRect("Panel", panelRoot, PanelSize);
        Image background = panel.gameObject.AddComponent<Image>();
        background.color = new Color(0.03f, 0.04f, 0.05f, 0.92f);
        background.raycastTarget = false;

        titleText = CreateText("Title", panel, 30f, FontStyles.Bold, TextAlignmentOptions.TopLeft);
        Anchor(titleText.rectTransform, 0.05f, 0.80f, 0.95f, 0.96f);
        bodyText = CreateText("Mission", panel, 25f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        Anchor(bodyText.rectTransform, 0.05f, 0.34f, 0.95f, 0.79f);
        // Long missions (the bedtime routine of Nivel5) shrink to fit instead of spilling over the progress bar.
        bodyText.enableAutoSizing = true;
        bodyText.fontSizeMin = 14f * NeuroPhobiaAccessibility.TextScale;
        bodyText.fontSizeMax = 25f * NeuroPhobiaAccessibility.TextScale;

        progressBar = CreateRect("Progress", panel, Vector2.zero).gameObject;
        Anchor((RectTransform)progressBar.transform, 0.05f, 0.27f, 0.95f, 0.31f);
        Image barBackground = progressBar.AddComponent<Image>();
        barBackground.color = new Color(0.16f, 0.2f, 0.24f, 1f);
        barBackground.raycastTarget = false;
        RectTransform fill = CreateRect("Fill", (RectTransform)progressBar.transform, Vector2.zero);
        Anchor(fill, 0f, 0f, 1f, 1f);
        progressFill = fill.gameObject.AddComponent<Image>();
        progressFill.sprite = SolidSprite();
        progressFill.color = new Color(0.1f, 0.8f, 0.72f, 1f);
        progressFill.type = Image.Type.Filled;
        progressFill.fillMethod = Image.FillMethod.Horizontal;
        progressFill.fillAmount = 0f;
        progressFill.raycastTarget = false;
        progressBar.SetActive(false);

        controlsText = CreateText("Controls", panel, 17f, FontStyles.Normal, TextAlignmentOptions.BottomLeft);
        Anchor(controlsText.rectTransform, 0.05f, 0.04f, 0.95f, 0.25f);
        controlsText.text = controls;
        controlsText.color = new Color(0.85f, 0.85f, 0.8f, 1f);

        cueCanvas = CreateCanvas("ViewCue", out RectTransform cueRoot);
        cue = CreateRect("Cue", cueRoot, CueSize);

        gazeRingRoot = CreateRect("GazeRing", cue, new Vector2(84f, 84f)).gameObject;
        ((RectTransform)gazeRingRoot.transform).anchoredPosition = new Vector2(0f, 70f);
        Image ringTrack = gazeRingRoot.AddComponent<Image>();
        ringTrack.sprite = RingSprite();
        ringTrack.color = new Color(1f, 1f, 1f, 0.22f);
        ringTrack.raycastTarget = false;
        RectTransform ringFill = CreateRect("Fill", (RectTransform)gazeRingRoot.transform, Vector2.zero);
        Anchor(ringFill, 0f, 0f, 1f, 1f);
        gazeRing = ringFill.gameObject.AddComponent<Image>();
        gazeRing.sprite = RingSprite();
        gazeRing.color = new Color(0.1f, 0.85f, 0.75f, 1f);
        gazeRing.type = Image.Type.Filled;
        gazeRing.fillMethod = Image.FillMethod.Radial360;
        gazeRing.fillOrigin = (int)Image.Origin360.Top;
        gazeRing.fillClockwise = true;
        gazeRing.fillAmount = 0f;
        gazeRing.raycastTarget = false;
        gazeRingRoot.SetActive(false);

        hintText = CreateText("Hint", cue, 30f, FontStyles.Bold, TextAlignmentOptions.Center);
        Anchor(hintText.rectTransform, 0f, 0f, 1f, 0.42f);
        hintText.gameObject.SetActive(false);

        // The cue floats 1 m ahead of the eyes and the wrist panel close to the hand: drawn over the scene so a
        // nearby wall or piece of furniture never cuts them (re-applied in LateUpdate, see KeepDrawnOnTop).
        NeuroPhobiaWorldPanel.DrawOnTop(transform);
    }

    private const float OnTopCheckSeconds = 0.5f;
    private float nextOnTopCheck;

    // The HUD styler switches these texts to the project font a moment after they are created, which gives them a
    // normal material again: the panel background (still drawn over the scene) then covered the letters. Checked
    // twice a second, it only touches texts whose material is not drawn over the scene.
    private void KeepDrawnOnTop()
    {
        if (Time.unscaledTime < nextOnTopCheck) return;
        nextOnTopCheck = Time.unscaledTime + OnTopCheckSeconds;
        NeuroPhobiaWorldPanel.DrawOnTop(transform);
    }

    private Canvas CreateCanvas(string objectName, out RectTransform root)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform));
        go.transform.SetParent(transform, false);
        root = (RectTransform)go.transform;
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.sortingOrder = SortingOrder;
        go.AddComponent<CanvasScaler>();
        return canvas;
    }

    private static RectTransform CreateRect(string objectName, RectTransform parent, Vector2 size)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.sizeDelta = size;
        return rect;
    }

    private static TMP_Text CreateText(string objectName, RectTransform parent, float size, FontStyles style, TextAlignmentOptions alignment)
    {
        RectTransform rect = CreateRect(objectName, parent, Vector2.zero);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    private static void Anchor(RectTransform rect, float minX, float minY, float maxX, float maxY)
    {
        rect.anchorMin = new Vector2(minX, minY);
        rect.anchorMax = new Vector2(maxX, maxY);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static Sprite solidSprite;
    private static Sprite ringSprite;

    private static Sprite SolidSprite()
    {
        if (solidSprite == null)
            solidSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));
        return solidSprite;
    }

    // Anti-aliased ring drawn once into a small texture.
    private static Sprite RingSprite()
    {
        if (ringSprite != null) return ringSprite;
        const int size = 128;
        const float outer = 62f;
        const float inner = 46f;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        Color32[] pixels = new Color32[size * size];
        Vector2 centre = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), centre);
                float alpha = Mathf.Clamp01(outer - distance) * Mathf.Clamp01(distance - inner);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        ringSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
        return ringSprite;
    }

    // ------------------------------------------------------------------ layout

    private void LateUpdate()
    {
        KeepDrawnOnTop();
        Camera head = NeuroPhobiaWorldPanel.GameplayCamera();
        if (head == null) return;

        bool xr = XRSettings.isDeviceActive;
        if (!layoutApplied || xr != xrLayout || panelCanvas.worldCamera != head) ApplyLayout(xr, head);

        bool playing = GameStateManager.CurrentState == GameState.PLAYING && !ScreenFader.IsCovering;
        if (playing) playSeconds += Time.unscaledDeltaTime;

        if (xrLayout)
        {
            UpdateWristPanel(head, playing);
            PlaceCue(head);
        }
        else
        {
            panelWanted = playing;
        }

        float targetAlpha = panelWanted ? 1f : 0f;
        panelGroup.alpha = Mathf.MoveTowards(panelGroup.alpha, targetAlpha, FadeSpeed * Time.unscaledDeltaTime);

        UpdateCue(playing);
    }

    private void ApplyLayout(bool xr, Camera head)
    {
        layoutApplied = true;
        xrLayout = xr;
        ConfigureCanvas(panelCanvas, xr, head, PanelSize);
        ConfigureCanvas(cueCanvas, xr, head, CueSize);

        if (xr)
        {
            Centre(panel, Vector2.zero, 1f);
            Centre(cue, Vector2.zero, 1f);
        }
        else
        {
            // Desktop: mission in the lower-left corner, cue at the bottom centre.
            panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.zero;
            panel.anchoredPosition = new Vector2(24f, 24f);
            panel.localScale = Vector3.one * 0.7f;
            cue.anchorMin = cue.anchorMax = new Vector2(0.5f, 0f);
            cue.pivot = new Vector2(0.5f, 0f);
            cue.anchoredPosition = new Vector2(0f, 30f);
            cue.localScale = Vector3.one * 0.7f;
        }
    }

    private static void Centre(RectTransform rect, Vector2 position, float scale)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.localScale = Vector3.one * scale;
    }

    private static void ConfigureCanvas(Canvas canvas, bool xr, Camera head, Vector2 size)
    {
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        canvas.worldCamera = head;
        if (xr)
        {
            canvas.renderMode = RenderMode.WorldSpace;
            ((RectTransform)canvas.transform).sizeDelta = size;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.dynamicPixelsPerUnit = 3f;
        }
        else
        {
            // ScreenSpaceCamera, not Overlay: Level3XRHud converts every overlay canvas it finds.
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.planeDistance = 0.6f;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
        }
    }

    // The panel floats above the left controller, facing the head, and is wanted only while the player holds the
    // hand up close and looks at it.
    private void UpdateWristPanel(Camera head, bool playing)
    {
        if (origin == null) origin = FindFirstObjectByType<XROrigin>();
        if (origin != null && leftController == null) leftController = FindChild(origin.transform, "Left Controller");
        if (origin == null || leftController == null)
        {
            panelWanted = false;
            return;
        }

        float scale = Mathf.Max(0.01f, origin.transform.lossyScale.x);
        Transform canvasTransform = panelCanvas.transform;
        Vector3 position = leftController.position + Vector3.up * (PanelLiftMeters * scale);
        Vector3 fromHead = position - head.transform.position;
        canvasTransform.position = position;
        if (fromHead.sqrMagnitude > 0.0001f) canvasTransform.rotation = Quaternion.LookRotation(fromHead, Vector3.up);
        canvasTransform.localScale = Vector3.one * (PanelWidthMeters * scale / PanelSize.x);

        bool tracked = leftController.localPosition.sqrMagnitude > 0.0001f;
        float angle = Vector3.Angle(head.transform.forward, fromHead);
        float distance = fromHead.magnitude / scale;
        bool looking = playing && tracked && distance < MaxViewDistanceMeters && angle < (panelWanted ? HideAngle : ShowAngle);

        lookingSeconds = looking ? lookingSeconds + Time.unscaledDeltaTime : 0f;
        panelWanted = looking && (panelWanted || lookingSeconds >= ShowDwellSeconds);
        if (panelWanted && lookingSeconds >= ViewedAfterSeconds) viewedOnce = true;
    }

    // Head-locked, slightly below the centre of the view.
    private void PlaceCue(Camera head)
    {
        float scale = origin != null ? Mathf.Max(0.01f, origin.transform.lossyScale.x) : 1f;
        Transform headTransform = head.transform;
        Quaternion tilt = headTransform.rotation * Quaternion.Euler(CueDegreesBelowCentre, 0f, 0f);
        Transform canvasTransform = cueCanvas.transform;
        canvasTransform.position = headTransform.position + tilt * Vector3.forward * (CueDistanceMeters * scale);
        canvasTransform.rotation = tilt;
        canvasTransform.localScale = Vector3.one * (CueWidthMeters * scale / CueSize.x);
    }

    private void UpdateCue(bool playing)
    {
        gazeRingRoot.SetActive(playing && gazeVisible);

        string hint = null;
        if (playing && Time.unscaledTime < timedHintUntil)
        {
            hint = timedHint;
        }
        else if (playing && xrLayout && !viewedOnce && playSeconds < FindPanelHintMaxSeconds)
        {
            hint = FindPanelHint;
            if (!leftHandCueSent)
            {
                leftHandCueSent = true;
                ControllerHaptics.Pulse(XRNode.LeftHand);
            }
        }

        bool show = !string.IsNullOrEmpty(hint);
        if (show && hintText.text != hint) hintText.text = hint;
        if (hintText.gameObject.activeSelf != show) hintText.gameObject.SetActive(show);
    }

    private static Transform FindChild(Transform root, string childName)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == childName) return child;
        }
        return null;
    }
}
