using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEngine.InputSystem;
using UI = NeuroPhobiaUITheme;

/// <summary>Una sola interfaz de sesión por escena jugable; no modifica las escenas XR.</summary>
public sealed class GlobalSessionUI : MonoBehaviour
{
    private const float MenuPlaneDistance = 0.45f;
    private const float MenuScale = 0.75f;
    private static GlobalSessionUI current;
    private Canvas canvas;
    private GameObject pausePanel;
    private GameObject confirmPanel;
    private GameObject resultPanel;
    private GameObject summaryPanel;
    private GameObject optionsPanel;
    private GameObject repeatPanel;
    private TMP_Text repeatText;
    private TMP_Text resultText;
    private TMP_Text summaryText;
    private TMP_Text syncText;
    private FastAPIClient apiClient;
    private Button nextButton;
    private Button pauseButton;
    private InputAction pauseAction;
    private InputAction nextAction;
    private InputAction menuAction;
    private int level;
    private float sceneStart;
    private bool completed;
    private string exitOutcome;
    // SUDS (ansiedad 0–100) antes y después de cada intento. Mientras el panel está abierto el juego queda en pausa.
    private GameObject sudsPanel;
    private TMP_Text sudsTitle;
    private System.Action<int> sudsAnswer;
    private bool sudsBeforeAsked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (current != null) Destroy(current.gameObject);
        current = null;
        if (!scene.name.StartsWith("Nivel") || !int.TryParse(scene.name.Substring(5), out int number) ||
            number < 1 || number > 5) return;
        GameObject root = new GameObject("NP_GlobalSessionUI", typeof(RectTransform));
        current = root.AddComponent<GlobalSessionUI>();
        current.level = number;
    }

    private void Start()
    {
        if (canvas != null) return;
        sceneStart = Time.realtimeSinceStartup;
        Time.timeScale = 1f;
        GameStateManager.SetState(GameState.PLAYING);
        EnsureEventSystem();
        BuildCanvas();
        CreateInputActions();
        AskSudsBefore();
    }

    private void AskSudsBefore()
    {
        if (sudsBeforeAsked || SessionManager.Instance == null || SessionManager.Instance.GetReport() == null) return;
        sudsBeforeAsked = true;
        ShowSuds("Antes de empezar", value =>
        {
            if (value >= 0) SessionManager.Instance.RecordSudsBefore(level, value);
            HidePanels();
            Time.timeScale = 1f;
            GameStateManager.SetState(GameState.PLAYING);
        });
    }

    /// <param name="onAnswer">Recibe 0–100, o -1 si la persona prefiere no responder.</param>
    private void ShowSuds(string title, System.Action<int> onAnswer)
    {
        HidePanels();
        sudsTitle.text = title;
        sudsAnswer = onAnswer;
        if (!completed) GameStateManager.SetState(GameState.PAUSED);
        Time.timeScale = 0f;
        DisableOtherRaycasters();
        sudsPanel.SetActive(true);
    }

    private void AnswerSuds(int value)
    {
        System.Action<int> answer = sudsAnswer;
        sudsAnswer = null;
        sudsPanel.SetActive(false);
        answer?.Invoke(value);
    }

    // Tras registrar el intento: pregunta el SUDS «después» si aún falta y luego continúa.
    private void AfterSudsThen(System.Action next)
    {
        if (SessionManager.Instance == null || !SessionManager.Instance.AwaitingSudsAfter(level)) { next(); return; }
        ShowSuds("Al terminar", value =>
        {
            SessionManager.Instance.RecordSudsAfter(level, value);
            next();
        });
    }

    private void Update()
    {
        KeepCameraBinding();
        if (syncText != null && summaryPanel != null && summaryPanel.activeSelf)
        {
            if (apiClient == null) apiClient = FindFirstObjectByType<FastAPIClient>();
            syncText.text = apiClient != null ? apiClient.SyncStatusText() : "";
            syncText.color = SyncColor(apiClient);
        }

        bool escapePressed = false;
#if ENABLE_LEGACY_INPUT_MANAGER
        escapePressed = Input.GetKeyDown(KeyCode.Escape);
#endif
#if ENABLE_INPUT_SYSTEM
        escapePressed |= Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#endif
        if (sudsPanel != null && sudsPanel.activeSelf)
        {
            // Algunos niveles ponen el estado en PLAYING en su Start: el panel SUDS mantiene la pausa.
            Time.timeScale = 0f;
            if (!completed && GameStateManager.CurrentState == GameState.PLAYING) GameStateManager.SetState(GameState.PAUSED);
            return;
        }

        if (escapePressed || (pauseAction != null && pauseAction.WasPressedThisFrame()))
        {
            if (confirmPanel.activeSelf) ShowPause();
            else if (pausePanel.activeSelf) Resume();
            else if (!completed) ShowPause();
        }

        // Respaldo con controles para el menú de nivel completado (A = siguiente, B = menú).
        if (completed && resultPanel.activeSelf)
        {
            if (nextButton.interactable && nextAction.WasPressedThisFrame()) NextLevel();
            else if (menuAction.WasPressedThisFrame()) FinishAndShowSummary();
        }
        else if (completed && repeatPanel != null && repeatPanel.activeSelf)
        {
            if (nextAction.WasPressedThisFrame()) RepeatLevel();
            else if (menuAction.WasPressedThisFrame()) EndAndMenu();
        }
    }

    private void OnDestroy()
    {
        if (current == this) current = null;
        pauseAction?.Dispose();
        nextAction?.Dispose();
        menuAction?.Dispose();
        Time.timeScale = 1f;
    }

    // Botón de menú del control derecho (y B / menú izquierdo) abre y cierra la pausa.
    private void CreateInputActions()
    {
        pauseAction = new InputAction("NP_Session_Pause", InputActionType.Button);
        pauseAction.AddBinding("<XRController>{RightHand}/menu");
        pauseAction.AddBinding("<XRController>{RightHand}/secondaryButton");
        pauseAction.AddBinding("<XRController>{LeftHand}/menu");
        pauseAction.Enable();

        nextAction = new InputAction("NP_Session_Next", InputActionType.Button);
        nextAction.AddBinding("<XRController>{RightHand}/primaryButton");
        nextAction.Enable();

        menuAction = new InputAction("NP_Session_Menu", InputActionType.Button);
        menuAction.AddBinding("<XRController>{RightHand}/secondaryButton");
        menuAction.AddBinding("<XRController>{RightHand}/menu");
        menuAction.AddBinding("<XRController>{LeftHand}/menu");
        menuAction.Enable();
    }

    // Otros canvas del nivel (psicoeducación, HUD antiguo) pueden quedar delante y capturar el rayo.
    private void DisableOtherRaycasters()
    {
        foreach (GraphicRaycaster raycaster in FindObjectsByType<GraphicRaycaster>(FindObjectsSortMode.None))
        {
            if (raycaster.gameObject != gameObject) raycaster.enabled = false;
        }

        foreach (TrackedDeviceGraphicRaycaster raycaster in FindObjectsByType<TrackedDeviceGraphicRaycaster>(FindObjectsSortMode.None))
        {
            if (raycaster.gameObject != gameObject) raycaster.enabled = false;
        }
    }

    public static void CompleteLevel(int completedLevel)
    {
        if (current == null || current.level != completedLevel || current.completed) return;
        current.ShowResult();
    }

    private void ShowResult()
    {
        completed = true;
        LevelMetrics metrics = Capture(true);
        GameStateManager.SetState(GameState.LEVEL_COMPLETE);
        Time.timeScale = 0f;
        AfterSudsThen(() => ShowResultPanel(metrics));
    }

    private void ShowResultPanel(LevelMetrics metrics)
    {
        HidePanels();
        resultText.text = MetricsColumns(MetricsText(metrics));
        nextButton.interactable = NextLevelOf(level) > 0;
        pauseButton.gameObject.SetActive(false);
        DisableOtherRaycasters();
        resultPanel.SetActive(true);
    }

    /// <summary>Ofrece repetir el nivel con la misma interfaz de sesión. Devuelve false si no hay interfaz para ese nivel.</summary>
    public static bool OfferRepeat(int offeredLevel, string message)
    {
        if (current == null || current.level != offeredLevel || current.repeatPanel == null) return false;
        current.ShowRepeat(message);
        return true;
    }

    public static bool IsRepeatOffered => current != null && current.repeatPanel != null && current.repeatPanel.activeSelf;

    private void ShowRepeat(string message)
    {
        completed = true;
        // El intento fallido cuenta como intento: se registra ya (levelPassed = false), elija repetir o salir.
        Capture(false);
        GameStateManager.SetState(GameState.LEVEL_COMPLETE);
        Time.timeScale = 0f;
        AfterSudsThen(() => ShowRepeatPanel(message));
    }

    private void ShowRepeatPanel(string message)
    {
        HidePanels();
        repeatText.text = message;
        pauseButton.gameObject.SetActive(false);
        DisableOtherRaycasters();
        repeatPanel.SetActive(true);
    }

    private void RepeatLevel()
    {
        Capture(false);   // ya registrado en ShowRepeat; no duplica
        if (SessionManager.Instance != null) SessionManager.Instance.RegisterLevelRetry(level);
        Time.timeScale = 1f;
        GameStateManager.SetState(GameState.PLAYING);
        SceneManager.LoadScene("Nivel" + level);
    }

    private LevelMetrics Capture(bool passed, string outcome = null)
    {
        // Solo el intento en curso: los intentos anteriores del mismo nivel ya son otros registros.
        LevelMetrics recorded = SessionManager.Instance != null ? SessionManager.Instance.CurrentAttemptMetrics(level) : null;
        if (recorded != null) return recorded;
        MetricsCollector collector = FindFirstObjectByType<MetricsCollector>();
        LevelMetrics metrics = collector != null ? collector.BuildMetrics(passed) : new LevelMetrics { level = level, levelPassed = passed, proximityMin = -1f, sessionDuration = Time.realtimeSinceStartup - sceneStart };
        metrics.level = level;
        metrics.levelPassed = passed;
        if (!string.IsNullOrEmpty(outcome)) metrics.outcome = outcome;
        if (SessionManager.Instance != null) SessionManager.Instance.AddLevelMetrics(metrics);
        return metrics;
    }

    // Si el jugador cambia de cámara (rig de escritorio o XR), el menú la sigue.
    private void KeepCameraBinding()
    {
        if (canvas == null || canvas.renderMode != RenderMode.ScreenSpaceCamera) return;
        Camera main = Camera.main;
        if (main != null && main.isActiveAndEnabled && canvas.worldCamera != main) canvas.worldCamera = main;
    }

    private void BuildCanvas()
    {
        Camera camera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
        canvas = gameObject.AddComponent<Canvas>();
        if (camera != null)
        {
            // Pegado a la cámara, como el HUD de los niveles: un lienzo en el mundo a 1,7 m
            // quedaba tapado o atravesado por paredes y muebles.
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = MenuPlaneDistance;
            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = MenuScale;
        }
        else canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 32000;
        RectTransform root = GetComponent<RectTransform>();
        if (root == null) root = gameObject.AddComponent<RectTransform>();
        gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
        // Sin GraphicRaycaster estándar los botones no responden al mouse/pantalla.
        gameObject.AddComponent<GraphicRaycaster>();
        if (camera != null) canvas.worldCamera = camera;

        pauseButton = Button("Pausa", transform, "PAUSA", new Vector2(415,270), new Vector2(150,58), ShowPause);
        // La pausa se abre solo con el botón del mando.
        pauseButton.gameObject.SetActive(false);

        pausePanel = Panel("CanvasPause", "Pausa", "Puedes continuar o finalizar la exposición.");
        Button("Continuar", pausePanel.transform, "Continuar", new Vector2(0,75), new Vector2(480,68), Resume, UI.ButtonKind.Primary);
        Button("Opciones", pausePanel.transform, "Opciones", new Vector2(0,-7), new Vector2(480,68), ShowOptions);
        Button("Menu", pausePanel.transform, "Volver al menú principal", new Vector2(0,-89), new Vector2(480,68), EndAndMenu);
        Button("Emergencia", pausePanel.transform, "Salida de emergencia", new Vector2(0,-171), new Vector2(480,68), ShowConfirm, UI.ButtonKind.Alert);
        pausePanel.SetActive(false);

        confirmPanel = Panel("CanvasEmergency", "Detener exposición", "¿Deseas finalizar esta exposición y volver al menú?");
        Button("Si", confirmPanel.transform, "Sí, finalizar", new Vector2(0,-20), new Vector2(480,68), EmergencyExit, UI.ButtonKind.Alert);
        Button("No", confirmPanel.transform, "Continuar sesión", new Vector2(0,-105), new Vector2(480,68), Resume, UI.ButtonKind.Primary);
        confirmPanel.SetActive(false);

        optionsPanel = Panel("CanvasOptions", "Opciones / Controles", "Usa los controles existentes de movimiento e interacción.\nPuedes abrir pausa con este botón. En PC, Escape si está disponible.");
        Button("Volver", optionsPanel.transform, "Volver", new Vector2(0,-150), new Vector2(480,68), ShowPause);
        optionsPanel.SetActive(false);

        resultPanel = Panel("CanvasLevelComplete", "Nivel completado", "Has finalizado esta etapa de exposición.");
        resultText = Label("Metricas", resultPanel.transform, "", 21, new Vector2(0,-5), new Vector2(820,250));
        resultText.alignment = TextAlignmentOptions.TopLeft;
        nextButton = Button("Siguiente", resultPanel.transform, "Siguiente nivel", new Vector2(-210,-200), new Vector2(400,68), NextLevel, UI.ButtonKind.Primary);
        Button("Menu", resultPanel.transform, "Menú principal", new Vector2(210,-200), new Vector2(400,68), FinishAndShowSummary);
        Label("Ayuda", resultPanel.transform, "Control derecho: A = Siguiente nivel · B = Menú principal", 18, new Vector2(0,-265), new Vector2(820,30), UI.Face.Type, UI.InkSoft);
        resultPanel.SetActive(false);

        // Oferta de repetir el nivel (p. ej. Nivel5 con demasiados intentos de alejamiento).
        repeatPanel = Panel("CanvasRepeat", "Repetir nivel", "");
        repeatText = repeatPanel.transform.Find("Descripcion").GetComponent<TMP_Text>();
        Button("Repetir", repeatPanel.transform, "Repetir nivel", new Vector2(-210,-200), new Vector2(400,68), RepeatLevel, UI.ButtonKind.Primary);
        Button("Menu", repeatPanel.transform, "Menú principal", new Vector2(210,-200), new Vector2(400,68), EndAndMenu);
        Label("Ayuda", repeatPanel.transform, "Control derecho: A = Repetir nivel · B = Menú principal", 18, new Vector2(0,-265), new Vector2(820,30), UI.Face.Type, UI.InkSoft);
        repeatPanel.SetActive(false);

        summaryPanel = Panel("CanvasSessionSummary", "Resumen de sesión", "Datos registrados localmente");
        GameObject reportBox = Box("ReporteFondo", summaryPanel.transform, new Vector2(0,30), new Vector2(840,230), UI.PaperLight);
        GameObject reportBorder = UI.Node(reportBox.transform, "Borde");
        UI.Stretch(reportBorder);
        UI.AddImage(reportBorder, UI.Frame, UI.WithAlpha(UI.Ink, .2f)).fillCenter = false;
        summaryText = Label("Reporte", summaryPanel.transform, "", 19, new Vector2(0,30), new Vector2(800,210), UI.Face.Type);
        summaryText.alignment = TextAlignmentOptions.TopLeft;
        summaryText.overflowMode = TextOverflowModes.Ellipsis;
        // Estado del envío al backend (BPMN «¿Almacena datos?» y secuencia «Retroalimentación guardada»).
        syncText = Label("EnvioServidor", summaryPanel.transform, "", 20, new Vector2(0,-118), new Vector2(820,52), UI.Face.Type, UI.SageDeep);
        Button("Finalizar", summaryPanel.transform, "Finalizar", new Vector2(-210,-210), new Vector2(400,68), EndAndMenu);
        Button("Menu", summaryPanel.transform, "Ver resultado en el Refugio", new Vector2(210,-210), new Vector2(400,68), EndAndMenu, UI.ButtonKind.Primary);
        summaryPanel.SetActive(false);

        // SUDS (Wolpe, 1969): 0 = nada de ansiedad, 100 = la máxima que puedas imaginar. Textos aprobados por Sara (2026-11-05).
        sudsPanel = Panel("CanvasSuds", "Antes de empezar", "¿Cuánta ansiedad sientes en este momento?\n0 = nada de ansiedad · 100 = la máxima ansiedad que puedas imaginar");
        sudsTitle = sudsPanel.transform.Find("Titulo").GetComponent<TMP_Text>();
        for (int i = 0; i <= 10; i++)
        {
            int value = i * 10;
            Button("Suds" + value, sudsPanel.transform, value.ToString(), new Vector2(-400 + i * 80, 10), new Vector2(72,72), () => AnswerSuds(value),
                i == 0 || i == 10 ? UI.ButtonKind.Primary : UI.ButtonKind.Key);
        }
        Label("Extremos", sudsPanel.transform, "Nada                                                                                          Máxima", 18, new Vector2(0,-48), new Vector2(880,30), UI.Face.Type, UI.InkSoft);
        Button("NoResponder", sudsPanel.transform, "Prefiero no responder", new Vector2(0,-160), new Vector2(480,68), () => AnswerSuds(-1));
        Label("Ayuda", sudsPanel.transform, "Apunta con el mando y pulsa el número que mejor te describa.", 18, new Vector2(0,-245), new Vector2(820,30), UI.Face.Type, UI.InkSoft);
        sudsPanel.SetActive(false);
    }

    // Hoja de papel (estética de la maqueta aprobada) centrada en el lienzo de 1000×650.
    private GameObject Panel(string name, string title, string description)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform));
        panel.transform.SetParent(transform, false);
        UI.Stretch(panel);
        UI.Sheet(panel.transform, "Hoja", 20f, 15f, 960f, 620f);
        Label("Titulo", panel.transform, title, 46, new Vector2(0,235), new Vector2(820,64), UI.Face.Display);
        Label("Descripcion", panel.transform, description, 23, new Vector2(0,163), new Vector2(820,84), UI.Face.Body, UI.InkSoft);
        return panel;
    }

    private static GameObject Box(string name, Transform parent, Vector2 position, Vector2 size, Color color)
    {
        GameObject box = new GameObject(name, typeof(RectTransform));
        box.transform.SetParent(parent, false);
        RectTransform rect = box.GetComponent<RectTransform>();
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        UI.AddImage(box, UI.Solid, color);
        return box;
    }

    private static TMP_Text Label(string name, Transform parent, string value, float size, Vector2 position, Vector2 dimensions,
        UI.Face face = UI.Face.Body, Color? color = null)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;
        return UI.AddText(go, value, face, size, color ?? UI.Ink, TextAlignmentOptions.Center);
    }

    private static Button Button(string name, Transform parent, string label, Vector2 position, Vector2 size,
        UnityEngine.Events.UnityAction action, UI.ButtonKind kind = UI.ButtonKind.Secondary)
    {
        UI.Button(parent, name, label, kind, 24f, action, out Button button);
        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return button;
    }

    // Color de la línea de envío: ámbar si falló, tinta suave si está pendiente o sin vincular, salvia si se guardó.
    private static Color SyncColor(FastAPIClient api)
    {
        if (api == null) return UI.InkSoft;
        if (api.StorageFailed) return UI.AmberInk;
        if (!api.IsPaired || api.PendingCount > 0) return UI.InkSoft;
        return UI.SageDeep;
    }

    // Presenta las métricas del nivel en dos columnas (mismos datos que MetricsText; solo cambia la disposición).
    private static string MetricsColumns(string metrics)
    {
        string[] lines = metrics.Split('\n');
        string[] items = lines[0].Split(new[] { " | " }, System.StringSplitOptions.None);
        StringBuilder text = new StringBuilder();
        for (int i = 0; i < items.Length; i += 2)
        {
            text.Append(Capitalize(items[i]));
            if (i + 1 < items.Length) text.Append("<pos=52%>").Append(Capitalize(items[i + 1]));
            text.Append('\n');
        }
        string soft = ColorUtility.ToHtmlStringRGB(UI.InkSoft);
        for (int i = 1; i < lines.Length; i++) text.Append("<color=#").Append(soft).Append('>').Append(lines[i]).Append("</color>\n");
        return text.ToString().TrimEnd('\n');
    }

    private static string Capitalize(string value)
    {
        return string.IsNullOrEmpty(value) ? value : char.ToUpperInvariant(value[0]) + value.Substring(1);
    }

    private static void EnsureEventSystem()
    {
        EventSystem events = FindFirstObjectByType<EventSystem>();
        if (events == null) new GameObject("EventSystem", typeof(EventSystem), typeof(XRUIInputModule));
        else if (events.GetComponent<XRUIInputModule>() == null)
        {
            StandaloneInputModule legacy = events.GetComponent<StandaloneInputModule>();
            if (legacy != null) Destroy(legacy);
            events.gameObject.AddComponent<XRUIInputModule>();
        }
    }

    private void ShowPause()
    {
        if (completed || (sudsPanel != null && sudsPanel.activeSelf)) return;
        HidePanels();
        GameStateManager.SetState(GameState.PAUSED);
        Time.timeScale = 0f;
        pausePanel.SetActive(true);
    }

    private void ShowConfirm() { HidePanels(); confirmPanel.SetActive(true); }
    private void ShowOptions() { HidePanels(); optionsPanel.SetActive(true); }
    private void HidePanels()
    {
        pausePanel.SetActive(false);
        confirmPanel.SetActive(false);
        optionsPanel.SetActive(false);
        if (sudsPanel != null) sudsPanel.SetActive(false);
    }

    private void Resume()
    {
        HidePanels();
        Time.timeScale = 1f;
        GameStateManager.SetState(GameState.PLAYING);
    }

    // Progresión: 1 -> 2 -> 3 -> 4 -> 5. Nivel5 cierra la sesión.
    private static int NextLevelOf(int completedLevel)
    {
        switch (completedLevel)
        {
            case 1: return 2;
            case 2: return 3;
            case 3: return 4;
            case 4: return 5;
            default: return 0;
        }
    }

    private void NextLevel()
    {
        int next = NextLevelOf(level);
        if (next <= 0) return;
        Time.timeScale = 1f;
        if (LevelManager.Instance != null) LevelManager.Instance.LoadLevel(next);
        else SceneManager.LoadScene("Nivel" + next);
    }

    // Salida de emergencia confirmada: el nivel queda como safe_exit y la sesión se cierra por seguridad (contrato API v1).
    private void EmergencyExit()
    {
        exitOutcome = DeviceRecordMapper.OutcomeSafeExit;
        EndAndMenu();
    }

    private void EndAndMenu()
    {
        // Volver al menú sin terminar el nivel = abandoned; con la salida de emergencia = safe_exit.
        // Si se ofreció repetir, el intento fallido ya quedó registrado en ShowRepeat (criterion_not_met).
        string outcome = exitOutcome ?? DeviceRecordMapper.OutcomeAbandoned;
        if (!completed || IsRepeatOffered) Capture(false, outcome);
        string endReason = !completed && outcome == DeviceRecordMapper.OutcomeSafeExit ? DeviceRecordMapper.EndSafeExit : null;
        if (SessionManager.Instance != null) SessionManager.Instance.EndSession(endReason);
        GameStateManager.SetState(GameState.MENU);
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }

    private void FinishAndShowSummary()
    {
        ShowSessionSummary();
    }

    public static void ShowSessionSummary()
    {
        // También permite enlazar el futuro cierre de Nivel5 sin reconstruir este Canvas.
        if (current == null)
        {
            GameObject root = new GameObject("NP_GlobalSessionUI", typeof(RectTransform));
            current = root.AddComponent<GlobalSessionUI>();
            current.level = SessionManager.Instance != null ? SessionManager.Instance.CurrentLevel : 5;
            current.sceneStart = Time.realtimeSinceStartup;
            EnsureEventSystem();
            current.BuildCanvas();
        }
        current.completed = true;
        // Nivel5 llega aquí al terminar: antes de cerrar la sesión se pregunta el SUDS «después».
        current.AfterSudsThen(current.FinishSummary);
    }

    private void FinishSummary()
    {
        GlobalSessionUI current = this;
        if (SessionManager.Instance != null) SessionManager.Instance.EndSession();
        GameStateManager.SetState(GameState.SESSION_END);
        Time.timeScale = 0f;
        current.completed = true;
        current.HidePanels();
        current.resultPanel.SetActive(false);
        current.summaryText.text = ReportText(SessionManager.Instance != null ? SessionManager.Instance.GetReport() : null);
        current.summaryPanel.SetActive(true);
    }

    private static string ReportText(SessionReport report)
    {
        if (report == null) return "No hay una sesión registrada.";
        StringBuilder text = new StringBuilder();
        text.AppendLine("Sesión: " + report.sessionId);
        text.AppendLine("Grado inicial: " + report.phobiaGrade + " / 5");
        text.AppendLine("Duración total: " + FormatTime(report.totalDuration));
        foreach (LevelMetrics metrics in report.levelMetrics) text.AppendLine(MetricsText(metrics));
        return text.ToString();
    }

    private static string MetricsText(LevelMetrics metrics)
    {
        if (metrics == null) return "Sin métricas disponibles.";
        StringBuilder text = new StringBuilder("Nivel " + metrics.level + ": " + (metrics.levelPassed ? "completado" : "interrumpido"));
        text.Append(" | tiempo: " + FormatTime(metrics.sessionDuration));
        text.Append(" | intentos de alejamiento: " + metrics.avoidanceAttempts);
        text.Append(" | movimiento de cabeza: " + metrics.headMovementFrequency.ToString("0.00"));
        text.Append(" | intentos de nivel: " + metrics.levelAttempts);
        text.Append(" | retiros de mano: " + metrics.handWithdrawals);
        if (metrics.handContactDuration > 0f) text.Append(" | contacto de mano: " + metrics.handContactDuration.ToString("0.0") + " s");
        if (metrics.proximityMin >= 0f) text.Append(" | proximidad mínima: " + metrics.proximityMin.ToString("0.0") + " m");
        if (metrics.gazeTimeOnSpider > 0f) text.Append(" | observación: " + metrics.gazeTimeOnSpider.ToString("0.0") + " s");
        if (metrics.taskCompletions != null && metrics.taskCompletions.Count > 0) text.Append(" | tareas: " + string.Join(", ", metrics.taskCompletions));
        if (metrics.breathingActivations > 0) text.Append(" | respiraciones: " + metrics.breathingActivations);
        if (metrics.avoidanceAttempts > 0) text.Append("\nSe registraron intentos de alejamiento durante la exposición.");
        if (metrics.breathingActivations > 0) text.Append("\nSe utilizó el sistema de respiración durante la sesión.");
        return text.ToString();
    }

    private static string FormatTime(float seconds)
    {
        int whole = Mathf.FloorToInt(Mathf.Max(0f, seconds));
        return (whole / 60) + ":" + (whole % 60).ToString("00");
    }
}
