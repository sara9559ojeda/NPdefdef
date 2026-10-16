using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>Una sola interfaz de sesión por escena jugable; no modifica las escenas XR.</summary>
public sealed class GlobalSessionUI : MonoBehaviour
{
    private static GlobalSessionUI current;
    private Canvas canvas;
    private GameObject pausePanel;
    private GameObject confirmPanel;
    private GameObject resultPanel;
    private GameObject summaryPanel;
    private GameObject optionsPanel;
    private TMP_Text resultText;
    private TMP_Text summaryText;
    private Button nextButton;
    private int level;
    private float sceneStart;
    private bool completed;

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
            (number != 1 && number != 3 && number != 4 && number != 5)) return;
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
    }

    private void Update()
    {
        bool escapePressed = false;
#if ENABLE_LEGACY_INPUT_MANAGER
        escapePressed = Input.GetKeyDown(KeyCode.Escape);
#endif
#if ENABLE_INPUT_SYSTEM
        escapePressed |= Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#endif
        if (escapePressed)
        {
            if (confirmPanel.activeSelf) ShowPause();
            else if (pausePanel.activeSelf) Resume();
            else if (!completed) ShowPause();
        }
    }

    private void OnDestroy()
    {
        if (current == this) current = null;
        Time.timeScale = 1f;
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
        resultText.text = "Nivel completado\nHas finalizado esta etapa de exposición.\n" + MetricsText(metrics);
        nextButton.interactable = level == 3 || level == 4;
        if (level == 1) resultText.text += "\nNivel 2 pendiente: escenario nuevo no disponible.";
        resultPanel.SetActive(true);
    }

    private LevelMetrics Capture(bool passed)
    {
        SessionReport existingReport = SessionManager.Instance != null ? SessionManager.Instance.GetReport() : null;
        if (existingReport != null)
        {
            LevelMetrics recorded = existingReport.levelMetrics.Find(item => item.level == level);
            if (recorded != null) return recorded;
        }
        MetricsCollector collector = FindFirstObjectByType<MetricsCollector>();
        LevelMetrics metrics = collector != null ? collector.BuildMetrics(passed) : new LevelMetrics { level = level, levelPassed = passed, proximityMin = -1f, sessionDuration = Time.realtimeSinceStartup - sceneStart };
        metrics.level = level;
        metrics.levelPassed = passed;
        if (SessionManager.Instance != null) SessionManager.Instance.AddLevelMetrics(metrics);
        return metrics;
    }

    private void BuildCanvas()
    {
        Camera camera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
        canvas = gameObject.AddComponent<Canvas>();
        if (camera != null)
        {
            canvas.renderMode = RenderMode.WorldSpace;
            transform.SetParent(camera.transform, false);
            transform.localPosition = new Vector3(0f, 0f, 1.7f);
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one * .001f;
        }
        else canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 32000;
        RectTransform root = GetComponent<RectTransform>();
        if (root == null) root = gameObject.AddComponent<RectTransform>();
        root.sizeDelta = new Vector2(1000f, 650f);
        gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();

        Button("Pausa", transform, "PAUSA", new Vector2(415,270), new Vector2(150,58), ShowPause);

        pausePanel = Panel("CanvasPause", "PAUSA", "Puedes continuar o finalizar la exposición.");
        Button("Continuar", pausePanel.transform, "Continuar", new Vector2(0,75), new Vector2(420,62), Resume);
        Button("Opciones", pausePanel.transform, "Opciones", new Vector2(0,-5), new Vector2(420,62), ShowOptions);
        Button("Menu", pausePanel.transform, "Volver al menú principal", new Vector2(0,-85), new Vector2(420,62), EndAndMenu);
        Button("Emergencia", pausePanel.transform, "Salida de emergencia", new Vector2(0,-165), new Vector2(420,62), ShowConfirm);
        pausePanel.SetActive(false);

        confirmPanel = Panel("CanvasEmergency", "Detener exposición", "¿Deseas finalizar esta exposición y volver al menú?");
        Button("Si", confirmPanel.transform, "Sí, finalizar", new Vector2(0,-20), new Vector2(420,62), EndAndMenu);
        Button("No", confirmPanel.transform, "Continuar sesión", new Vector2(0,-105), new Vector2(420,62), Resume);
        confirmPanel.SetActive(false);

        optionsPanel = Panel("CanvasOptions", "Opciones / Controles", "Usa los controles existentes de movimiento e interacción.\nPuedes abrir pausa con este botón. En PC, Escape si está disponible.");
        Button("Volver", optionsPanel.transform, "Volver", new Vector2(0,-120), new Vector2(420,62), ShowPause);
        optionsPanel.SetActive(false);

        resultPanel = Panel("CanvasLevelComplete", "Nivel completado", "");
        resultText = Label("Metricas", resultPanel.transform, "", 21, new Vector2(0,15), new Vector2(780,240));
        nextButton = Button("Siguiente", resultPanel.transform, "Siguiente nivel", new Vector2(-205,-190), new Vector2(360,62), NextLevel);
        Button("Menu", resultPanel.transform, "Menú principal", new Vector2(205,-190), new Vector2(360,62), FinishAndShowSummary);
        resultPanel.SetActive(false);

        summaryPanel = Panel("CanvasSessionSummary", "Resumen de sesión", "Datos registrados localmente");
        summaryText = Label("Reporte", summaryPanel.transform, "", 19, new Vector2(0,10), new Vector2(780,340));
        Button("Finalizar", summaryPanel.transform, "Finalizar", new Vector2(-205,-190), new Vector2(360,62), EndAndMenu);
        Button("Menu", summaryPanel.transform, "Volver al menú principal", new Vector2(205,-190), new Vector2(360,62), EndAndMenu);
        summaryPanel.SetActive(false);
    }

    private GameObject Panel(string name, string title, string description)
    {
        GameObject panel = Box(name, transform, Vector2.zero, new Vector2(860,560), new Color(.025f,.04f,.07f,.98f));
        Label("Titulo", panel.transform, title, 36, new Vector2(0,220), new Vector2(780,65));
        Label("Descripcion", panel.transform, description, 21, new Vector2(0,155), new Vector2(780,90));
        return panel;
    }

    private static GameObject Box(string name, Transform parent, Vector2 position, Vector2 size, Color color)
    {
        GameObject box = new GameObject(name, typeof(RectTransform), typeof(Image));
        box.transform.SetParent(parent, false);
        RectTransform rect = box.GetComponent<RectTransform>();
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        box.GetComponent<Image>().color = color;
        return box;
    }

    private static TMP_Text Label(string name, Transform parent, string value, float size, Vector2 position, Vector2 dimensions)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    private static Button Button(string name, Transform parent, string label, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action)
    {
        GameObject go = Box(name, parent, position, size, new Color(.08f,.45f,.47f,1f));
        Button button = go.AddComponent<Button>();
        button.targetGraphic = go.GetComponent<Image>();
        button.onClick.AddListener(action);
        Label("Texto", go.transform, label, 22, Vector2.zero, size);
        return button;
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
        if (completed) return;
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
    }

    private void Resume()
    {
        HidePanels();
        Time.timeScale = 1f;
        GameStateManager.SetState(GameState.PLAYING);
    }

    private void NextLevel()
    {
        if (level != 3 && level != 4) return;
        Time.timeScale = 1f;
        int next = level + 1;
        if (LevelManager.Instance != null) LevelManager.Instance.LoadLevel(next);
        else SceneManager.LoadScene("Nivel" + next);
    }

    private void EndAndMenu()
    {
        if (!completed) Capture(false);
        if (SessionManager.Instance != null) SessionManager.Instance.EndSession();
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
