using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UI = NeuroPhobiaUITheme;

/// <summary>
/// Menú principal moderno de NeuroPhobia.
/// Se construye por código para mantener una apariencia consistente
/// tanto en computador como en Android/Meta Quest.
/// </summary>
[DisallowMultipleComponent]
public sealed class NeuroPhobiaMainMenuModern : MonoBehaviour
{
    private const string UiRootName = "NP_UI_Menu_Moderno";

    // Ajustes del menú en espacio mundo (VR / Meta Quest).
    private const float VrCanvasScale = 0.0012f; // 1440 px -> ~1.73 m de ancho (~60 grados de campo visual)
    private const float VrFollowSpeed = 4f;

    private GameObject uiRoot;
    private Transform vrCanvasTransform;
    private GameObject mainPanel;
    private GameObject levelsPanel;
    private GameObject instructionsPanel;
    private GameObject consentPanel;
    private GameObject gradePanel;
    private GameObject profilePanel;
    private GameObject resultsPanel;
    private TMP_Text resultsText;
    private string feedbackSessionId;
    private DeviceContextDto renderedFeedbackContext;
    private string renderedFeedbackSessionId;
    private GameObject accessibilityPanel;
    private TMP_Text[] accessibilityValues;
    // Vinculación del visor (contrato API v1, 6.1): es el «Inicio de sesión» del paciente en el BPMN.
    private const string PairingAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private GameObject pairingPanel;
    private TMP_Text[] pairingSlots;
    private Image[] pairingSlotBoxes;
    private Image[] pairingSlotLines;
    private TMP_Text pairingStatus;
    private string pairingCode = "";
    private bool pairingInProgress;
    private TMP_Text gradeHint;
    private TMP_Text recommendationHint;
    private TMP_Text deviceStatus;
    private TMP_Text deviceButtonLabel;
    private TMP_InputField nameInput;
    private TMP_InputField emailInput;
    private TMP_Text profileStatus;
    // Estado visual local en memoria: se pierde al cerrar la aplicación.
    private static string localName = "";
    private static string localEmail = "";
    private TMP_Text feedbackText;
    // Nivel elegido en "ELEGIR NIVEL"; 0 = flujo normal desde el nivel inicial.
    private int selectedLevel;

    // Decisión de Sara (D17, 2026-10-30): el selector existe para probar todos los niveles.
    // Solo en Editor y builds de desarrollo; en la build de entrega se empieza siempre por el nivel inicial (RQF009).
    private static bool LevelSelectorEnabled => Application.isEditor || Debug.isDebugBuild;

    // Color de fondo de la cámara en VR (el mismo tono noche del tablero).
    private static readonly Color Background = UI.Night;

    private void Start()
    {
        DisableTurnAround();
        RebuildMenu();
        // Contexto del backend en el Refugio (nunca durante la exposición): grado previo y nivel permitido.
        FastAPIClient api = Api();
        if (api != null && api.IsPaired) api.StartCoroutine(api.RefreshContext(_ => UpdateGradeHint()));
        if (SessionManager.Instance != null && SessionManager.Instance.HasEnded)
            OpenFeedback(SessionManager.Instance.SessionId);
    }

    public void RebuildMenu()
    {
        RemovePreviousUi();
        EnsureEventSystem();

        uiRoot = new GameObject(UiRootName, typeof(RectTransform));
        uiRoot.transform.SetParent(transform, false);

        Canvas canvas = uiRoot.AddComponent<Canvas>();
        bool vr = Application.isMobilePlatform || XRSettings.isDeviceActive;
        CanvasScaler scaler = uiRoot.AddComponent<CanvasScaler>();

        if (vr)
        {
            // En gafas un canvas de pantalla ocupa todo el campo de visión.
            // Se usa un panel en espacio mundo con tamaño físico fijo.
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = FindGameplayCamera();

            RectTransform rect = uiRoot.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(1440f, 900f);
            rect.localScale = Vector3.one * VrCanvasScale;
            vrCanvasTransform = rect;

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.dynamicPixelsPerUnit = 2f;
            ShowOnlyMenuInVr(canvas.worldCamera);
            PlaceInFrontOfCamera(true);
        }
        else
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = FindGameplayCamera();
            canvas.planeDistance = 1.5f;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 30000;

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1440f, 900f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        uiRoot.AddComponent<TrackedDeviceGraphicRaycaster>();

        BuildBackground(uiRoot.transform);
        BuildMainPanel(uiRoot.transform);
        BuildLevelsPanel(uiRoot.transform);
        BuildInstructionsPanel(uiRoot.transform);
        BuildAccessibilityPanel(uiRoot.transform);
        BuildSessionPanels(uiRoot.transform);

        if (vrCanvasTransform != null)
        {
            ShowOnlyMenuInVr(canvas.worldCamera);
        }

        ShowMain();
    }

    // En el menú, el giro del rig XR con el stick derecho (atrás 180°, izquierda/derecha a saltos o continuo) movía al
    // jugador y el menú, que sigue la vista con retardo, se deslizaba hasta volver a ponerse delante. Aquí se quita
    // todo el giro con el stick; el desplazamiento con el stick izquierdo sigue igual. Girar la cabeza o el cuerpo
    // de verdad sigue funcionando.
    private static void DisableTurnAround()
    {
        foreach (SnapTurnProvider snap in FindObjectsByType<SnapTurnProvider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            snap.enableTurnAround = false;
            snap.enableTurnLeftRight = false;
        }

        foreach (ContinuousTurnProvider turn in FindObjectsByType<ContinuousTurnProvider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            turn.enabled = false;
        }
    }

    // Sin skybox: el cartel se ve sobre fondo sólido (los controles siguen visibles).
    private void ShowOnlyMenuInVr(Camera cam)
    {
        const int uiLayer = 5;
        foreach (Transform t in uiRoot.GetComponentsInChildren<Transform>(true))
        {
            t.gameObject.layer = uiLayer;
        }

        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Background;
        }
    }

    private void LateUpdate()
    {
        if (vrCanvasTransform != null)
        {
            PlaceInFrontOfCamera(false);
        }
    }

    // Mantiene el panel frente al jugador con seguimiento suave (sin pegarlo a la cabeza).
    private void PlaceInFrontOfCamera(bool snap)
    {
        Camera cam = FindGameplayCamera();
        if (cam == null || vrCanvasTransform == null)
        {
            return;
        }

        Vector3 forward = cam.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.forward;
        }
        forward.Normalize();

        Vector3 targetPos = cam.transform.position + forward * NeuroPhobiaAccessibility.MenuDistance;
        Quaternion targetRot = Quaternion.LookRotation(forward, Vector3.up);

        if (snap)
        {
            vrCanvasTransform.SetPositionAndRotation(targetPos, targetRot);
            return;
        }

        float t = 1f - Mathf.Exp(-VrFollowSpeed * Time.unscaledDeltaTime);
        vrCanvasTransform.SetPositionAndRotation(
            Vector3.Lerp(vrCanvasTransform.position, targetPos, t),
            Quaternion.Slerp(vrCanvasTransform.rotation, targetRot, t));
    }

    // ───────────── Estética «tablero de corcho» (maqueta aprobada 2026-11-05) ─────────────
    // Coordenadas en píxeles del lienzo 1440×900 desde la esquina superior izquierda (UI.Place).

    private void BuildBackground(Transform parent)
    {
        GameObject bg = CreateUIObject(parent, "Fondo");
        Stretch(bg.GetComponent<RectTransform>());
        Image image = UI.AddImage(bg, UI.CorkBackground, Color.white);
        image.preserveAspect = false;

        // Filo vertical rugoso que enmarca la columna del menú.
        GameObject edge = UI.Node(bg.transform, "Filo");
        UI.Place(edge, 84f, 40f, 10f, 820f);
        UI.AddImage(edge, UI.RoughLine, UI.WithAlpha(UI.Cream, .75f));
    }

    private void BuildMainPanel(Transform parent)
    {
        mainPanel = CreateUIObject(parent, "PanelPrincipal");
        Stretch(mainPanel.GetComponent<RectTransform>());

        TMP_Text tag = UI.AddText(UI.Node(mainPanel.transform, "MarcaSuperior"), "EXPERIENCIA TERAPÉUTICA INMERSIVA",
            UI.Face.Type, 18f, UI.Sepia, TextAlignmentOptions.MidlineLeft);
        tag.characterSpacing = 16f;
        UI.Place(tag.gameObject, 120f, 84f, 620f, 30f);

        TMP_Text footer = UI.AddText(UI.Node(mainPanel.transform, "Pie"), "5 niveles de exposición gradual  ·  Computador y Realidad Virtual",
            UI.Face.Type, 17f, UI.Sepia, TextAlignmentOptions.MidlineRight);
        footer.characterSpacing = 6f;
        UI.Place(footer.gameObject, 700f, 40f, 670f, 30f);

        // Título sobre una franja de papel rasgado.
        GameObject strip = UI.Node(mainPanel.transform, "FranjaTitulo");
        UI.Place(strip, 102f, 130f, 634f, 108f);
        UI.AddImage(strip, UI.TornStrip, UI.Paper);
        TMP_Text title = UI.AddText(UI.Node(strip.transform, "Titulo"),
            "Neuro<color=#" + ColorUtility.ToHtmlStringRGB(UI.SageDeep) + ">Phobia</color>",
            UI.Face.Display, 84f, UI.Ink, TextAlignmentOptions.MidlineLeft);
        title.textWrappingMode = TextWrappingModes.NoWrap;
        UI.Place(title.gameObject, 36f, 4f, 590f, 104f);

        TMP_Text accent = UI.AddText(UI.Node(mainPanel.transform, "Acento"), "ENTRENAMIENTO GRADUAL CONTRA LA ARACNOFOBIA",
            UI.Face.Type, 19f, UI.Sage, TextAlignmentOptions.MidlineLeft);
        accent.characterSpacing = 7f;
        UI.Place(accent.gameObject, 120f, 258f, 680f, 30f);

        TMP_Text description = UI.AddText(UI.Node(mainPanel.transform, "Descripcion"),
            "Avanza a tu ritmo mediante una experiencia progresiva que combina observación, distancia segura, aproximación y contacto virtual controlado.",
            UI.Face.Body, 21f, UI.Sepia, TextAlignmentOptions.TopLeft);
        description.lineSpacing = 4f;
        UI.Place(description.gameObject, 120f, 298f, 540f, 100f);

        // Menú vertical: la opción apuntada se ilumina y lleva una marca de tinta.
        GameObject list = UI.Node(mainPanel.transform, "Opciones");
        UI.Place(list, 120f, 432f, 620f, 372f);
        float y = 0f;
        NeuroPhobiaMenuItem first = MenuItem(list.transform, "Iniciar", "Iniciar experiencia", ref y, StartExperience, false);
        if (LevelSelectorEnabled) MenuItem(list.transform, "SeleccionarNivel", "Elegir nivel", ref y, ShowLevels, true);
        MenuItem(list.transform, "ElegirNivel", "Mis datos", ref y, ShowProfile, false);
        MenuItem(list.transform, "Instrucciones", "Controles e instrucciones", ref y, ShowInstructions, false);
        MenuItem(list.transform, "Resultados", "Resultados guardados", ref y, ShowSavedFeedback, false);
        MenuItem(list.transform, "Accesibilidad", "Accesibilidad", ref y, ShowAccessibility, false);
        MenuItem(list.transform, "Salir", "Salir", ref y, QuitGame, false);
        first.Activate();

        feedbackText = UI.AddText(UI.Node(mainPanel.transform, "Estado"), "", UI.Face.Type, 21f, UI.Amber, TextAlignmentOptions.TopLeft);
        UI.Place(feedbackText.gameObject, 120f, 810f, 640f, 70f);

        // Nota «Tu recorrido» clavada en el tablero.
        GameObject routeShadow = UI.Node(mainPanel.transform, "RutaSombra");
        UI.Place(routeShadow, 942f, 262f, 424f, 470f).localEulerAngles = new Vector3(0f, 0f, 1f);
        UI.AddImage(routeShadow, UI.Shadow, new Color(0f, 0f, 0f, .55f));
        GameObject route = UI.Node(mainPanel.transform, "Ruta");
        UI.Place(route, 960f, 270f, 380f, LevelSelectorEnabled ? 430f : 360f).localEulerAngles = new Vector3(0f, 0f, 1f);
        UI.AddImage(route, UI.PaperSheet, UI.Paper);
        GameObject routeTape = UI.Node(route.transform, "Cinta");
        UI.Place(routeTape, 135f, -14f, 110f, 30f).localEulerAngles = new Vector3(0f, 0f, -2f);
        UI.AddImage(routeTape, UI.Tape, UI.WithAlpha(UI.Sage, .6f));

        TMP_Text routeTitle = UI.AddText(UI.Node(route.transform, "RutaTitulo"), "TU RECORRIDO", UI.Face.TypeBold, 20f, UI.InkSoft, TextAlignmentOptions.MidlineLeft);
        routeTitle.characterSpacing = 16f;
        UI.Place(routeTitle.gameObject, 32f, 30f, 316f, 30f);
        string[] steps = { "Psicoeducación", "Distancia segura", "Aproximación", "Contacto virtual", "Desafío final" };
        for (int i = 0; i < steps.Length; i++)
        {
            TMP_Text number = UI.AddText(UI.Node(route.transform, "RutaNumero" + (i + 1)), (i + 1).ToString("00"),
                UI.Face.Display, 25f, UI.SageDeep, TextAlignmentOptions.MidlineLeft);
            UI.Place(number.gameObject, 32f, 74f + i * 46f, 50f, 40f);
            TMP_Text step = UI.AddText(UI.Node(route.transform, "RutaPaso" + (i + 1)), steps[i],
                UI.Face.Body, 23f, UI.Ink, TextAlignmentOptions.MidlineLeft);
            UI.Place(step.gameObject, 84f, 74f + i * 46f, 270f, 40f);
        }

        if (LevelSelectorEnabled)
        {
            GameObject rule = UI.Node(route.transform, "Separador");
            UI.Place(rule, 32f, 316f, 316f, 1.5f);
            UI.AddImage(rule, UI.Solid, UI.WithAlpha(UI.Ink, .3f));
            TMP_Text helper = UI.AddText(UI.Node(route.transform, "Ayuda"), "Puedes iniciar desde el Nivel 1 o elegir un nivel específico.",
                UI.Face.Body, 18f, UI.InkSoft, TextAlignmentOptions.TopLeft);
            UI.Place(helper.gameObject, 32f, 330f, 316f, 70f);
        }
    }

    private NeuroPhobiaMenuItem MenuItem(Transform parent, string name, string label, ref float y,
        UnityEngine.Events.UnityAction action, bool devOnly)
    {
        GameObject item = UI.Node(parent, name);
        // Siete opciones (con el selector de niveles de las builds de desarrollo) caben en los 372 px de la lista.
        UI.Place(item, 0f, y, 620f, 50f);
        y += 53f;
        Image glow = UI.AddImage(item, UI.Solid, UI.WithAlpha(UI.Sage, 0f), true);
        Button button = item.AddComponent<Button>();
        button.targetGraphic = glow;
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(action);

        GameObject inkGo = UI.Node(item.transform, "Tinta");
        UI.Place(inkGo, 4f, 16f, 36f, 26f);
        Image inkMark = UI.AddImage(inkGo, UI.InkMark, UI.WithAlpha(UI.Sage, 0f));

        TMP_Text text = UI.AddText(UI.Node(item.transform, "Texto"), label, UI.Face.Type, 30f, UI.Sepia, TextAlignmentOptions.MidlineLeft);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        UI.Place(text.gameObject, 50f, 0f, 560f, 58f);
        if (devOnly) UI.DevTag(item.transform, 50f + text.GetPreferredValues(label, 560f, 58f).x + 16f, 29f);

        NeuroPhobiaMenuItem menuItem = item.AddComponent<NeuroPhobiaMenuItem>();
        menuItem.Setup(inkMark, glow, text);
        return menuItem;
    }

    /// <summary>Pantalla secundaria: tablero más oscuro, ruta arriba a la izquierda y una hoja de papel.</summary>
    private GameObject BuildScreen(Transform parent, string name, string crumb, float x, float y, float w, float h,
        string title, string lead, float leadHeight, out Transform sheet)
    {
        GameObject screen = CreateUIObject(parent, name);
        Stretch(screen.GetComponent<RectTransform>());
        UI.AddImage(screen, UI.Solid, UI.WithAlpha(UI.Night, .55f), true);

        TMP_Text path = UI.AddText(UI.Node(screen.transform, "RutaPantalla"), "NEUROPHOBIA  ·  " + crumb.ToUpperInvariant(),
            UI.Face.Type, 20f, UI.Sepia, TextAlignmentOptions.MidlineLeft);
        path.characterSpacing = 10f;
        UI.Place(path.gameObject, 120f, 44f, 1000f, 36f);

        sheet = UI.Sheet(screen.transform, "Hoja", x, y, w, h).transform;
        TMP_Text heading = UI.AddText(UI.Node(sheet, "Titulo"), title, UI.Face.Display, 50f, UI.Ink, TextAlignmentOptions.TopLeft);
        UI.Place(heading.gameObject, 60f, 48f, w - 120f, 64f);
        if (!string.IsNullOrEmpty(lead))
        {
            TMP_Text body = UI.AddText(UI.Node(sheet, "Descripcion"), lead, UI.Face.Body, 25f, UI.InkSoft, TextAlignmentOptions.TopLeft);
            UI.Place(body.gameObject, 60f, 124f, w - 120f, leadHeight);
        }
        screen.SetActive(false);
        return screen;
    }

    private static TMP_Text PaperButton(Transform parent, string name, string label, UI.ButtonKind kind,
        float x, float y, float w, float h, UnityEngine.Events.UnityAction action, float fontSize = 25f)
    {
        TMP_Text text = UI.Button(parent, name, label, kind, fontSize, action, out Button button);
        UI.Place(button.gameObject, x, y, w, h);
        return text;
    }

    private void BuildLevelsPanel(Transform parent)
    {
        levelsPanel = BuildScreen(parent, "PanelNiveles", "Elegir nivel", 110f, 130f, 1240f, 640f,
            "Selecciona un nivel", "Cada nivel aumenta gradualmente el grado de exposición.", 40f, out Transform sheet);

        string[] titles = { "PSICOEDUCACIÓN", "DISTANCIA SEGURA", "APROXIMACIÓN PROGRESIVA", "CONTACTO VIRTUAL", "DESAFÍO FINAL" };
        string[] descriptions =
        {
            "Respiración y exposición mediante imágenes.",
            "Observación continua de una araña a distancia.",
            "Acércate mediante puntos de exposición controlados.",
            "Interacción cercana en un entorno doméstico.",
            "Integra lo aprendido en un escenario más complejo."
        };
        const float cardWidth = 213f;
        for (int i = 0; i < titles.Length; i++)
        {
            int level = i + 1;
            CreateLevelButton(sheet, level, titles[i], descriptions[i], 50f + i * (cardWidth + 18f), cardWidth, () => ChooseLevel(level));
        }

        PaperButton(sheet, "Volver", "Volver", UI.ButtonKind.Secondary, 50f, 544f, 156f, 68f, ShowMain);
    }

    private static readonly NeuroPhobiaAccessibility.Setting[] AccessibilityRows =
    {
        NeuroPhobiaAccessibility.Setting.TextSize,
        NeuroPhobiaAccessibility.Setting.PlayerSize,
        NeuroPhobiaAccessibility.Setting.MoveSpeed,
        NeuroPhobiaAccessibility.Setting.TurnMode,
        NeuroPhobiaAccessibility.Setting.TurnAngle,
        NeuroPhobiaAccessibility.Setting.MenuDistance
    };

    private static readonly string[] AccessibilityNames =
    {
        "Tamaño de la letra", "Tamaño del personaje", "Velocidad de movimiento", "Tipo de giro", "Ángulo del giro a saltos", "Distancia del menú"
    };

    private void BuildAccessibilityPanel(Transform parent)
    {
        accessibilityPanel = BuildScreen(parent, "PanelAccesibilidad", "Accesibilidad", 210f, 100f, 1020f, 730f,
            "Accesibilidad", "Ajusta la experiencia a tu comodidad. Los cambios se guardan en este visor.", 40f, out Transform sheet);

        accessibilityValues = new TMP_Text[AccessibilityRows.Length];
        for (int i = 0; i < AccessibilityRows.Length; i++)
        {
            NeuroPhobiaAccessibility.Setting setting = AccessibilityRows[i];
            float y = 192f + i * 72f;
            TMP_Text name = UI.AddText(UI.Node(sheet, "Ajuste" + i), AccessibilityNames[i], UI.Face.Body, 25f, UI.Ink, TextAlignmentOptions.MidlineLeft);
            UI.Place(name.gameObject, 60f, y, 430f, 60f);
            PaperButton(sheet, "Menos" + i, "-", UI.ButtonKind.Key, 500f, y, 70f, 60f, () => ChangeAccessibility(setting, -1), 30f);
            accessibilityValues[i] = UI.AddText(UI.Node(sheet, "Valor" + i), "", UI.Face.TypeBold, 25f, UI.SageDeep, TextAlignmentOptions.Center);
            UI.Place(accessibilityValues[i].gameObject, 578f, y, 244f, 60f);
            PaperButton(sheet, "Mas" + i, "+", UI.ButtonKind.Key, 830f, y, 70f, 60f, () => ChangeAccessibility(setting, 1), 30f);
        }

        PaperButton(sheet, "Restablecer", "Restablecer", UI.ButtonKind.Secondary, 60f, 640f, 280f, 68f, ResetAccessibility);
        PaperButton(sheet, "Volver", "Volver al menú", UI.ButtonKind.Primary, 620f, 640f, 280f, 68f, ShowMain);
        RefreshAccessibility();
    }

    private void RefreshAccessibility()
    {
        if (accessibilityValues == null) return;
        for (int i = 0; i < accessibilityValues.Length; i++)
        {
            if (accessibilityValues[i] != null)
                accessibilityValues[i].text = NeuroPhobiaAccessibility.Label(AccessibilityRows[i]);
        }
    }

    // El tamaño de letra cambia los textos del propio menú, así que se reconstruye y se vuelve a esta pantalla.
    private void ChangeAccessibility(NeuroPhobiaAccessibility.Setting setting, int direction)
    {
        NeuroPhobiaAccessibility.Step(setting, direction);
        AfterAccessibilityChange(setting == NeuroPhobiaAccessibility.Setting.TextSize);
    }

    private void ResetAccessibility()
    {
        NeuroPhobiaAccessibility.ResetAll();
        AfterAccessibilityChange(true);
    }

    private void AfterAccessibilityChange(bool rebuild)
    {
        if (rebuild)
        {
            RebuildMenu();
            ShowAccessibility();
        }
        else
        {
            RefreshAccessibility();
            if (vrCanvasTransform != null) PlaceInFrontOfCamera(true);
        }
    }

    public void ShowAccessibility()
    {
        ShowMain();
        mainPanel.SetActive(false);
        accessibilityPanel.SetActive(true);
        RefreshAccessibility();
    }

    private void BuildInstructionsPanel(Transform parent)
    {
        instructionsPanel = BuildScreen(parent, "PanelInstrucciones", "Controles e instrucciones", 210f, 120f, 1020f, 614f,
            "Controles e instrucciones", null, 0f, out Transform sheet);

        Transform pc = InfoColumn(sheet, "Computador", "COMPUTADOR", 60f);
        string[,] keys = { { "W A S D", "Movimiento" }, { "Mouse", "Mirar" }, { "Shift", "Correr" }, { "Escape", "Liberar cursor" } };
        for (int i = 0; i < keys.GetLength(0); i++)
        {
            TMP_Text key = UI.AddText(UI.Node(pc, "Tecla" + i), keys[i, 0], UI.Face.TypeBold, 22f, UI.Ink, TextAlignmentOptions.MidlineLeft);
            UI.Place(key.gameObject, 28f, 64f + i * 42f, 140f, 36f);
            TMP_Text what = UI.AddText(UI.Node(pc, "Accion" + i), keys[i, 1], UI.Face.Body, 22f, UI.Ink, TextAlignmentOptions.MidlineLeft);
            UI.Place(what.gameObject, 172f, 64f + i * 42f, 240f, 36f);
        }

        Transform vr = InfoColumn(sheet, "RV", "REALIDAD VIRTUAL", 523f);
        TMP_Text vrText = UI.AddText(UI.Node(vr, "TextoRV"),
            "Usa el visor y los controladores.\nAntes de cada nivel podrás escoger entre Computador y RV.\nMantén movimientos cómodos y controlados.",
            UI.Face.Body, 22f, UI.Ink, TextAlignmentOptions.TopLeft);
        vrText.paragraphSpacing = 10f;
        UI.Place(vrText.gameObject, 28f, 64f, 381f, 176f);

        GameObject bar = UI.Node(sheet, "NotaBarra");
        UI.Place(bar, 60f, 404f, 4f, 70f);
        UI.AddImage(bar, UI.Solid, UI.SageDeep);
        TMP_Text note = UI.AddText(UI.Node(sheet, "Nota"),
            "La exposición es gradual. Puedes detenerte cuando lo necesites y continuar cuando estés preparado.",
            UI.Face.Body, 22f, UI.InkSoft, TextAlignmentOptions.MidlineLeft);
        UI.Place(note.gameObject, 80f, 404f, 880f, 70f);

        PaperButton(sheet, "Volver", "Volver al menú", UI.ButtonKind.Secondary, 60f, 500f, 250f, 68f, ShowMain);
    }

    private static Transform InfoColumn(Transform sheet, string name, string heading, float x)
    {
        GameObject column = UI.Node(sheet, name);
        UI.Place(column, x, 128f, 437f, 250f);
        UI.AddImage(column, UI.Solid, UI.PaperLight);
        GameObject border = UI.Node(column.transform, "Borde");
        UI.Stretch(border);
        UI.AddImage(border, UI.Frame, UI.WithAlpha(UI.Ink, .25f)).fillCenter = false;
        TMP_Text title = UI.AddText(UI.Node(column.transform, "Titulo"), heading, UI.Face.TypeBold, 22f, UI.SageDeep, TextAlignmentOptions.MidlineLeft);
        title.characterSpacing = 14f;
        UI.Place(title.gameObject, 28f, 20f, 381f, 34f);
        return column.transform;
    }

    private void CreateLevelButton(Transform parent, int number, string title, string description, float x, float width,
        UnityEngine.Events.UnityAction action)
    {
        UI.Button(parent, "Nivel_" + number, "", UI.ButtonKind.Secondary, 20f, action, out Button button);
        UI.Place(button.gameObject, x, 214f, width, 300f);
        Transform card = button.transform;
        Image border = card.Find("Borde").GetComponent<Image>();
        border.color = UI.WithAlpha(UI.Ink, .3f);

        GameObject band = UI.Node(card, "Banda");
        UI.Place(band, 0f, 0f, width, 8f);
        UI.AddImage(band, UI.Solid, UI.Ink);

        TMP_Text numberText = UI.AddText(UI.Node(card, "Numero"), number.ToString("00"), UI.Face.Display, 48f, UI.SageDeep, TextAlignmentOptions.TopLeft);
        UI.Place(numberText.gameObject, 20f, 24f, width - 40f, 56f);
        TMP_Text titleText = UI.AddText(UI.Node(card, "Titulo"), title, UI.Face.TypeBold, 22f, UI.Ink, TextAlignmentOptions.TopLeft);
        titleText.lineSpacing = -8f;
        UI.Place(titleText.gameObject, 20f, 86f, width - 36f, 60f);
        TMP_Text descriptionText = UI.AddText(UI.Node(card, "Descripcion"), description, UI.Face.Body, 20f, UI.InkSoft, TextAlignmentOptions.TopLeft);
        UI.Place(descriptionText.gameObject, 20f, 152f, width - 36f, 136f);
    }

    private static TMP_Text CreateText(
        Transform parent,
        string name,
        string value,
        float fontSize,
        FontStyles style,
        TextAlignmentOptions alignment,
        Color color)
    {
        GameObject go = CreateUIObject(parent, name);

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        UI.Style(text, style == FontStyles.Bold ? UI.Face.TypeBold : UI.Face.Body, fontSize, color);
        text.text = value;
        text.alignment = alignment;
        text.color = color;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;

        return text;
    }

    private static GameObject CreateUIObject(
        Transform parent,
        string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void Stretch(RectTransform rect)
    {
        SetAnchors(rect, Vector2.zero, Vector2.one);
    }

    private static void SetAnchors(
        RectTransform rect,
        Vector2 min,
        Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void RemovePreviousUi()
    {
        Transform old = transform.Find(UiRootName);

        if (old == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(old.gameObject);
        }
        else
        {
            DestroyImmediate(old.gameObject);
        }
    }

    private static void EnsureEventSystem()
    {
        EventSystem current = FindFirstObjectByType<EventSystem>();

        if (current != null)
        {
            current.gameObject.SetActive(true);

            if (current.GetComponent<XRUIInputModule>() == null)
            {
                StandaloneInputModule legacy =
                    current.GetComponent<StandaloneInputModule>();

                if (legacy != null)
                {
                    Destroy(legacy);
                }

                current.gameObject.AddComponent<XRUIInputModule>();
            }

            return;
        }

        GameObject eventObject = new GameObject(
            "EventSystem",
            typeof(EventSystem),
            typeof(XRUIInputModule));

        eventObject.transform.position = Vector3.zero;
    }

    private static Camera FindGameplayCamera()
    {
        if (Camera.main != null)
        {
            return Camera.main;
        }

        return FindFirstObjectByType<Camera>();
    }

    public void ShowMain()
    {
        HideSessionPanels();
        if (mainPanel != null)
        {
            mainPanel.SetActive(true);
        }

        if (levelsPanel != null)
        {
            levelsPanel.SetActive(false);
        }

        if (instructionsPanel != null)
        {
            instructionsPanel.SetActive(false);
        }

        if (accessibilityPanel != null)
        {
            accessibilityPanel.SetActive(false);
        }

        if (feedbackText != null)
        {
            // BPMN «¿Almacena datos? no → mensaje de error»: se avisa al volver al menú.
            FastAPIClient api = Api();
            feedbackText.text = api != null && api.StorageFailed ? api.LastError : "";
        }
    }

    public void ShowLevels()
    {
        ShowMain();
        mainPanel.SetActive(false);
        levelsPanel.SetActive(true);
    }

    // Elegir un nivel pasa por consentimiento y evaluación inicial para abrir la sesión.
    private void ChooseLevel(int level)
    {
        selectedLevel = LevelSelectorEnabled ? level : 0;
        ShowConsent();
    }

    public void ShowInstructions()
    {
        if (mainPanel != null)
        {
            mainPanel.SetActive(false);
        }

        if (levelsPanel != null)
        {
            levelsPanel.SetActive(false);
        }

        if (instructionsPanel != null)
        {
            instructionsPanel.SetActive(true);
        }
    }

    public void StartExperience()
    {
        selectedLevel = 0;
        ShowConsent();
    }

    private void BuildSessionPanels(Transform parent)
    {
        consentPanel = BuildScreen(parent, "Consentimiento", "Consentimiento", 300f, 180f, 840f, 384f, "Bienvenido a NeuroPhobia",
            "Esta es una experiencia de exposición gradual en realidad virtual. Puedes pausar o detener la experiencia cuando lo necesites.",
            120f, out Transform consentSheet);
        PaperButton(consentSheet, "Aceptar", "Aceptar y continuar", UI.ButtonKind.Primary, 60f, 270f, 351f, 68f, AfterConsent);
        PaperButton(consentSheet, "Salir", "Salir", UI.ButtonKind.Secondary, 429f, 270f, 351f, 68f, ShowMain);

        gradePanel = BuildScreen(parent, "EvaluacionInicial", "Evaluación inicial", 260f, 150f, 920f, 558f, "Evaluación inicial",
            "¿Qué grado de miedo o fobia percibes actualmente?", 40f, out Transform gradeSheet);
        const float gradeWidth = 147f;
        for (int grade = 1; grade <= 5; grade++)
        {
            int selected = grade;
            float x = 60f + (grade - 1) * (gradeWidth + 16f);
            TMP_Text number = PaperButton(gradeSheet, "Grado" + grade, grade.ToString(), UI.ButtonKind.Secondary, x, 210f, gradeWidth, 120f,
                () => SelectGrade(selected), 58f);
            UI.Style(number, UI.Face.Display, 58f, UI.Ink);
            number.margin = new Vector4(0f, 0f, 0f, 22f);
            if (grade == 1 || grade == 5)
            {
                TMP_Text end = UI.AddText(UI.Node(number.transform.parent, "Extremo"), grade == 1 ? "menor" : "mayor",
                    UI.Face.Type, 16f, UI.InkSoft, TextAlignmentOptions.Center);
                UI.Place(end.gameObject, 0f, 84f, gradeWidth, 24f);
            }
        }
        TMP_Text low = UI.AddText(UI.Node(gradeSheet, "LeyendaMenor"), "1 = menor intensidad", UI.Face.Type, 19f, UI.InkSoft, TextAlignmentOptions.MidlineLeft);
        UI.Place(low.gameObject, 60f, 342f, 400f, 30f);
        TMP_Text high = UI.AddText(UI.Node(gradeSheet, "LeyendaMayor"), "5 = mayor intensidad", UI.Face.Type, 19f, UI.InkSoft, TextAlignmentOptions.MidlineRight);
        UI.Place(high.gameObject, 460f, 342f, 400f, 30f);
        gradeHint = UI.AddText(UI.Node(gradeSheet, "UltimoGrado"), "", UI.Face.Type, 22f, UI.InkSoft, TextAlignmentOptions.MidlineLeft);
        UI.Place(gradeHint.gameObject, 60f, 392f, 800f, 34f);
        PaperButton(gradeSheet, "Volver", "Volver", UI.ButtonKind.Secondary, 60f, 444f, 156f, 68f, ShowConsent);
        // Recomendación del backend (GET /device/context), junto a «Volver».
        recommendationHint = UI.AddText(UI.Node(gradeSheet, "Recomendacion"), "", UI.Face.Type, 19f, UI.SageDeep, TextAlignmentOptions.MidlineLeft);
        recommendationHint.textWrappingMode = TextWrappingModes.Normal;
        UI.Place(recommendationHint.gameObject, 240f, 444f, 620f, 68f);

        BuildPairingPanel(parent);

        profilePanel = BuildScreen(parent, "MisDatos", "Mis datos", 190f, 130f, 1060f, 706f, "Mis datos",
            "Nombre y correo electrónico. Validación local durante esta ejecución.", 40f, out Transform profileSheet);
        nameInput = CreateInput(profileSheet, "Nombre", 60f);
        emailInput = CreateInput(profileSheet, "Correo electrónico", 542f);
        PaperButton(profileSheet, "Validar", "Validar", UI.ButtonKind.Secondary, 60f, 300f, 461f, 68f, ValidateProfile);
        PaperButton(profileSheet, "Guardar", "Guardar cambios", UI.ButtonKind.Primary, 539f, 300f, 461f, 68f, SaveProfile);
        profileStatus = UI.AddText(UI.Node(profileSheet, "EstadoValidacion"), "", UI.Face.Type, 22f, UI.AmberInk, TextAlignmentOptions.MidlineLeft);
        UI.Place(profileStatus.gameObject, 60f, 380f, 940f, 40f);

        GameObject device = UI.Node(profileSheet, "Dispositivo");
        UI.Place(device, 60f, 440f, 940f, 128f);
        UI.AddImage(device, UI.Solid, UI.WithAlpha(UI.SageDeep, .09f));
        GameObject deviceBorder = UI.Node(device.transform, "Borde");
        UI.Stretch(deviceBorder);
        UI.AddImage(deviceBorder, UI.Frame, UI.WithAlpha(UI.SageDeep, .5f)).fillCenter = false;
        deviceStatus = UI.AddText(UI.Node(device.transform, "EstadoVisor"), "", UI.Face.Type, 21f, UI.Ink, TextAlignmentOptions.MidlineLeft);
        UI.Place(deviceStatus.gameObject, 22f, 10f, 560f, 108f);
        deviceButtonLabel = PaperButton(device.transform, "Visor", "Cerrar sesión del visor", UI.ButtonKind.Secondary, 600f, 30f, 318f, 68f,
            ToggleDeviceSession, 23f);

        PaperButton(profileSheet, "Volver", "Volver", UI.ButtonKind.Secondary, 60f, 592f, 156f, 68f, ShowMain);

        resultsPanel = BuildScreen(parent, "ResultadosGuardados", "Resultados", 190f, 130f, 1060f, 706f,
            "Resultado al finalizar", "Consulta el análisis guardado y su revisión pendiente.", 40f, out Transform resultsSheet);
        resultsText = UI.AddText(UI.Node(resultsSheet, "Resultado"), "", UI.Face.Type, 23f, UI.Ink, TextAlignmentOptions.TopLeft);
        resultsText.richText = false;
        resultsText.enableAutoSizing = true;
        resultsText.fontSizeMin = 17f;
        resultsText.fontSizeMax = 23f;
        resultsText.textWrappingMode = TextWrappingModes.Normal;
        UI.Place(resultsText.gameObject, 60f, 180f, 940f, 380f);
        PaperButton(resultsSheet, "Volver", "Volver", UI.ButtonKind.Secondary, 60f, 592f, 156f, 68f, ShowMain);
        resultsPanel.SetActive(false);
    }

    public void ShowSavedFeedback()
    {
        OpenFeedback(null);
    }

    private void OpenFeedback(string sessionId)
    {
        ShowMain();
        mainPanel.SetActive(false);
        feedbackSessionId = sessionId;
        resultsPanel.SetActive(true);
        RefreshResults();
        FastAPIClient api = Api();
        if (api != null && api.IsPaired) api.StartCoroutine(api.RefreshContext(_ => RefreshResults()));
    }

    private void Update()
    {
        if (resultsPanel != null && resultsPanel.activeSelf) RefreshResults();
    }

    private void RefreshResults()
    {
        FastAPIClient api = Api();
        DeviceContextDto context = api != null ? api.Context : null;
        if (renderedFeedbackContext == context && renderedFeedbackSessionId == feedbackSessionId && !string.IsNullOrEmpty(resultsText.text)) return;
        renderedFeedbackContext = context;
        renderedFeedbackSessionId = feedbackSessionId;
        SessionReport report = SessionManager.Instance != null ? SessionManager.Instance.GetReport() : null;
        if (!string.IsNullOrEmpty(feedbackSessionId) && report != null && report.testSession)
            resultsText.text = "Esta es una sesión de prueba. Sus registros no generan recomendaciones ni cuentan como progreso.";
        else if (api == null || api.Context == null)
            resultsText.text = "Aún no hay un análisis disponible. Vincula el visor y comprueba el estado del envío.";
        else
            resultsText.text = api.Context.FeedbackText(feedbackSessionId);
    }

    private void HideSessionPanels()
    {
        if (consentPanel != null) consentPanel.SetActive(false);
        if (gradePanel != null) gradePanel.SetActive(false);
        if (profilePanel != null) profilePanel.SetActive(false);
        if (pairingPanel != null) pairingPanel.SetActive(false);
        if (resultsPanel != null) resultsPanel.SetActive(false);
    }

    private TMP_InputField CreateInput(Transform parent, string label, float x)
    {
        TMP_Text caption = UI.AddText(UI.Node(parent, label + "Etiqueta"), label.ToUpperInvariant(), UI.Face.Type, 19f, UI.InkSoft, TextAlignmentOptions.BottomLeft);
        caption.characterSpacing = 8f;
        UI.Place(caption.gameObject, x, 180f, 458f, 28f);

        GameObject field = UI.Node(parent, label + "Campo");
        UI.Place(field, x, 214f, 458f, 62f);
        Image background = UI.AddImage(field, UI.Solid, UI.PaperLight, true);
        GameObject underline = UI.Node(field.transform, "Subrayado");
        UI.Place(underline, 0f, 59f, 458f, 3f);
        UI.AddImage(underline, UI.Solid, UI.Ink);

        GameObject area = UI.Node(field.transform, "Area");
        UI.Place(area, 16f, 0f, 426f, 59f);
        TMP_Text value = UI.AddText(UI.Node(area.transform, "Valor"), "", UI.Face.Type, 26f, UI.Ink, TextAlignmentOptions.MidlineLeft);
        value.textWrappingMode = TextWrappingModes.NoWrap;
        UI.Stretch(value.gameObject);

        TMP_InputField input = field.AddComponent<TMP_InputField>();
        input.targetGraphic = background;
        input.textViewport = area.GetComponent<RectTransform>();
        input.textComponent = (TextMeshProUGUI)value;
        input.fontAsset = UI.Font(UI.Face.Type);
        input.pointSize = 26f;
        input.selectionColor = UI.WithAlpha(UI.Sage, .5f);
        input.caretColor = UI.Ink;
        ColorBlock colors = input.colors;
        colors.normalColor = UI.PaperLight;
        colors.highlightedColor = UI.OnAccent;
        colors.selectedColor = UI.OnAccent;
        input.colors = colors;
        input.text = label == "Nombre" ? localName : localEmail;
        return input;
    }

    public void ShowConsent() { ShowMain(); mainPanel.SetActive(false); consentPanel.SetActive(true); }
    public void ShowGrade() { ShowMain(); mainPanel.SetActive(false); gradePanel.SetActive(true); UpdateGradeHint(); }
    public void ShowProfile() { ShowMain(); mainPanel.SetActive(false); profilePanel.SetActive(true); UpdateDeviceStatus(); }

    private void SelectGrade(int grade)
    {
        if (grade < 1 || grade > 5) return;
        if (SessionManager.Instance == null)
        {
            ShowMain();
            feedbackText.text = "No se encontró SessionManager.";
            return;
        }
        FastAPIClient api = Api();
        // Sin visor vinculado solo se llega aquí en Editor o build de desarrollo: se marca como sesión de prueba.
        bool testSession = (LevelSelectorEnabled && selectedLevel > 0) || (api != null && !api.IsPaired);
        // BPMN «Ajustar grado de fobia»: el grado se guarda en el backend antes de comenzar.
        if (api != null && api.IsPaired) api.StartCoroutine(api.PutPhobiaGrade(grade));
        SessionManager.Instance.StartSession(grade, testSession);
        int initialLevel = LevelSelectorEnabled && selectedLevel > 0
            ? selectedLevel
            : Mathf.Max(SessionManager.Instance.ResolveInitialLevel(grade), api != null ? api.AllowedStartLevel() : 1);
        selectedLevel = 0;
        SessionManager.Instance.CurrentLevel = initialLevel;
        LoadLevel("Nivel" + initialLevel);
    }

    private static FastAPIClient Api()
    {
        return SessionManager.Instance != null ? SessionManager.Instance.GetComponent<FastAPIClient>() : FindFirstObjectByType<FastAPIClient>();
    }

    // Consentimiento aceptado → inicio de sesión del visor (si hace falta) → ajustar grado de fobia.
    private void AfterConsent()
    {
        FastAPIClient api = Api();
        if (api != null && !api.IsPaired) ShowPairing();
        else ShowGrade();
    }

    private void ShowPairing()
    {
        ShowMain();
        mainPanel.SetActive(false);
        pairingCode = "";
        pairingStatus.text = "";
        UpdatePairingCode();
        pairingPanel.SetActive(true);
    }

    private void BuildPairingPanel(Transform parent)
    {
        pairingPanel = BuildScreen(parent, "VincularVisor", "Vincular visor", 110f, 110f, 1260f, 690f, "Vincular visor",
            "Escribe el código de 6 caracteres que genera la web en «Mi visor». Vale 10 minutos y solo se puede usar una vez.",
            80f, out Transform sheet);

        // Seis casillas para el código (3 + 3), con la siguiente posición marcada en salvia.
        pairingSlots = new TMP_Text[6];
        pairingSlotBoxes = new Image[6];
        pairingSlotLines = new Image[6];
        for (int i = 0; i < 6; i++)
        {
            float x = 60f + i * 64f + (i >= 3 ? 30f : 0f);
            GameObject slot = UI.Node(sheet, "Casilla" + (i + 1));
            UI.Place(slot, x, 228f, 56f, 82f);
            pairingSlotBoxes[i] = UI.AddImage(slot, UI.Solid, UI.WithAlpha(UI.Ink, .05f));
            GameObject line = UI.Node(slot.transform, "Linea");
            UI.Place(line, 0f, 78f, 56f, 4f);
            pairingSlotLines[i] = UI.AddImage(line, UI.Solid, UI.Ink);
            pairingSlots[i] = UI.AddText(UI.Node(slot.transform, "Letra"), "", UI.Face.Display, 50f, UI.Ink, TextAlignmentOptions.Center);
            UI.Stretch(pairingSlots[i].gameObject);
        }
        TMP_Text dash = UI.AddText(UI.Node(sheet, "Guion"), "-", UI.Face.Display, 40f, UI.InkSoft, TextAlignmentOptions.Center);
        UI.Place(dash.gameObject, 60f + 3f * 64f - 6f, 228f, 30f, 82f);

        pairingStatus = UI.AddText(UI.Node(sheet, "Estado"), "", UI.Face.Type, 22f, UI.AmberInk, TextAlignmentOptions.TopLeft);
        UI.Place(pairingStatus.gameObject, 60f, 332f, 460f, 190f);

        // Teclado en pantalla: en las gafas no hay teclado físico. Solo los caracteres que usa el backend (sin 0/O ni 1/I).
        for (int i = 0; i < PairingAlphabet.Length; i++)
        {
            char key = PairingAlphabet[i];
            int row = i / 8;
            int col = i % 8;
            PaperButton(sheet, "Tecla" + key, key.ToString(), UI.ButtonKind.Key, 560f + col * 81f, 228f + row * 76f, 72f, 66f,
                () => TypePairingKey(key), 30f);
        }

        GameObject divider = UI.Node(sheet, "Separador");
        UI.Place(divider, 60f, 556f, 1140f, 1.5f);
        UI.AddImage(divider, UI.Solid, UI.WithAlpha(UI.Ink, .3f));

        float x0 = 60f;
        PaperButton(sheet, "Borrar", "Borrar", UI.ButtonKind.Secondary, x0, 582f, 137f, 68f, ErasePairingKey, 23f);
        x0 += 155f;
        PaperButton(sheet, "Vincular", "Vincular", UI.ButtonKind.Primary, x0, 582f, 164f, 68f, SubmitPairing, 23f);
        x0 += 182f;
        if (LevelSelectorEnabled)
        {
            const string testLabel = "Seguir sin vincular (prueba)";
            TMP_Text test = PaperButton(sheet, "SinVincular", testLabel, UI.ButtonKind.Secondary, x0, 582f, 617f, 68f, ShowGrade, 23f);
            test.alignment = TextAlignmentOptions.MidlineLeft;
            test.margin = new Vector4(26f, 0f, 0f, 0f);
            UI.DevTag(test.transform.parent, 26f + test.GetPreferredValues(testLabel, 600f, 68f).x + 16f, 34f);
            x0 += 635f;
        }
        PaperButton(sheet, "Volver", "Volver", UI.ButtonKind.Secondary, x0, 582f, 137f, 68f, ShowConsent, 23f);
        pairingPanel.SetActive(false);
    }

    private void TypePairingKey(char key)
    {
        if (pairingInProgress || pairingCode.Length >= 6) return;
        pairingCode += key;
        pairingStatus.text = "";
        UpdatePairingCode();
    }

    private void ErasePairingKey()
    {
        if (pairingInProgress || pairingCode.Length == 0) return;
        pairingCode = pairingCode.Substring(0, pairingCode.Length - 1);
        UpdatePairingCode();
    }

    private void UpdatePairingCode()
    {
        for (int i = 0; i < pairingSlots.Length; i++)
        {
            bool next = i == pairingCode.Length;
            pairingSlots[i].text = i < pairingCode.Length ? pairingCode[i].ToString() : "";
            pairingSlotLines[i].color = next ? UI.SageDeep : UI.Ink;
            pairingSlotBoxes[i].color = next ? UI.WithAlpha(UI.SageDeep, .10f) : UI.WithAlpha(UI.Ink, .05f);
        }
    }

    // BPMN «Inicio de sesión»: validar → ¿válido? sí: entra (ajustar grado); no: mensaje de error y se vuelve a escribir.
    private void SubmitPairing()
    {
        FastAPIClient api = Api();
        if (pairingInProgress) return;
        pairingStatus.color = UI.AmberInk;
        if (api == null) { pairingStatus.text = "No se encontró la conexión con el servidor."; return; }
        if (pairingCode.Length != 6) { pairingStatus.text = "El código tiene 6 caracteres. Revísalo e inténtalo de nuevo."; return; }
        pairingInProgress = true;
        pairingStatus.color = UI.InkSoft;
        pairingStatus.text = "Comprobando el código…";
        api.StartCoroutine(api.Pair(pairingCode, (ok, message) =>
        {
            pairingInProgress = false;
            if (this == null || pairingPanel == null) return;
            pairingStatus.color = ok ? UI.SageDeep : UI.AmberInk;
            pairingStatus.text = message;
            if (!ok) { pairingCode = ""; UpdatePairingCode(); return; }
            api.StartCoroutine(api.RefreshContext(_ => { if (this != null) ShowGrade(); }));
        }));
    }

    private void UpdateGradeHint()
    {
        if (gradeHint == null) return;
        FastAPIClient api = Api();
        int last = api != null ? api.LastPhobiaGrade() : 0;
        gradeHint.text = last > 0 ? "Último grado registrado: " + last : "";
        if (recommendationHint != null)
            recommendationHint.text = api != null && api.Context != null ? api.Context.RecommendationText() : "";
    }

    private void UpdateDeviceStatus()
    {
        FastAPIClient api = Api();
        if (deviceStatus == null) return;
        if (api == null) { deviceStatus.text = ""; return; }
        deviceStatus.text = (api.IsPaired ? "Visor vinculado. " : "Visor sin vincular. ") + api.SyncStatusText();
        if (deviceButtonLabel != null) deviceButtonLabel.text = api.IsPaired ? "Cerrar sesión del visor" : "Vincular visor";
    }

    // Contexto nivel 0 «Iniciar y cerrar sesión»: el visor puede cerrar su propia sesión (revoca su token).
    private void ToggleDeviceSession()
    {
        FastAPIClient api = Api();
        if (api == null) return;
        if (!api.IsPaired) { ShowPairing(); return; }
        deviceStatus.text = "Cerrando la sesión del visor…";
        api.StartCoroutine(api.Unpair(ok =>
        {
            if (this == null || deviceStatus == null) return;
            UpdateDeviceStatus();
            if (!ok) deviceStatus.text = api.PendingCount > 0
                ? "Quedan datos por enviar al servidor. Espera a que se envíen antes de cerrar la sesión del visor."
                : "No se pudo cerrar la sesión del visor. Revisa la conexión e inténtalo de nuevo.";
        }));
    }

    private void ValidateProfile()
    {
        string name = nameInput.text.Trim();
        string email = emailInput.text.Trim();
        profileStatus.color = UI.AmberInk;
        if (name.Length < 2 || name.Length > 80) { profileStatus.text = "Ingresa un nombre válido (2 a 80 caracteres)."; return; }
        int at = email.IndexOf('@');
        if (email.Length > 254 || at < 1 || at != email.LastIndexOf('@') || email.Contains(" ") ||
            email.LastIndexOf('.') <= at + 1 || email.EndsWith("."))
        { profileStatus.text = "Ingresa un correo electrónico válido."; return; }
        profileStatus.color = UI.SageDeep;
        profileStatus.text = "Datos validados correctamente.";
    }

    private void SaveProfile()
    {
        ValidateProfile();
        if (profileStatus.text != "Datos validados correctamente.") return;
        localName = nameInput.text.Trim();
        localEmail = emailInput.text.Trim();
        profileStatus.text = "Datos validados correctamente. Conservados solo durante esta ejecución.";
    }

    public void LoadLevel(string sceneName)
    {
        if (SessionManager.Instance == null || SessionManager.Instance.GetReport() == null)
        {
            if (feedbackText != null) feedbackText.text = "Inicia desde la evaluación inicial.";
            return;
        }
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            if (feedbackText != null)
            {
                feedbackText.text =
                    "No se puede cargar " +
                    sceneName +
                    ". Verifica Build Settings.";
            }

            Debug.LogError(
                "NeuroPhobia MainMenu: la escena " +
                sceneName +
                " no está disponible en Build Settings.");

            return;
        }

        SceneManager.LoadSceneAsync(sceneName);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        if (feedbackText != null)
        {
            feedbackText.text =
                "En una compilación, este botón cerrará la aplicación.";
        }
#else
        Application.Quit();
#endif
    }
}
