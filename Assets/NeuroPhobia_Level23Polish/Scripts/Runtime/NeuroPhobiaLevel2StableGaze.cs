using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Reemplazo estable del temporizador de mirada del Nivel 2.
/// Busca siempre la cámara activa (PC o RV) y usa un ángulo de mirada
/// más tolerante para evitar reinicios por movimientos mínimos.
/// </summary>
[DefaultExecutionOrder(-300)]
public sealed class NeuroPhobiaLevel2StableGaze : MonoBehaviour
{
    [Header("Exposición")]
    public float requiredSeconds = 20f;
    [Range(3f, 25f)]
    public float acceptedGazeAngle = 12f;
    public float gazeGraceSeconds = 0.35f;
    public string nextScene = "Nivel3";

    [Header("Referencias opcionales")]
    public Renderer spiderRenderer;
    public MetricsCollector metricsCollector;

    private float currentTime;
    private float bestAttempt;
    private float lostGazeTime;
    private bool completed;

    private Canvas hudCanvas;
    private TMP_Text timerText;
    private TMP_Text bestText;
    private TMP_Text instructionText;
    private Image progressFill;

    private Canvas completionCanvas;

    private const string BestKey = "NeuroPhobia_Level2_BestStableGaze";

    private void Start()
    {
        bestAttempt = PlayerPrefs.GetFloat(BestKey, 0f);

        DisableOldLevel2GazeControllers();
        DisableOldLevel2Hud();

        FindSpider();
        if (metricsCollector == null) metricsCollector = FindFirstObjectByType<MetricsCollector>();
        BuildHud();
        UpdateHud(false);
    }

    private void Update()
    {
        if (completed)
        {
            return;
        }

        // Mientras la pantalla Computador/RV está visible no cuenta tiempo.
        if (IsControlSelectorVisible())
        {
            instructionText.text = "Selecciona Computador o Realidad Virtual";
            return;
        }

        Camera activeCamera = GetActiveGameplayCamera();
        if (activeCamera != null) AttachHudToCamera(activeCamera);

        if (activeCamera == null)
        {
            instructionText.text = "Buscando cámara activa...";
            return;
        }

        if (spiderRenderer == null || !spiderRenderer.gameObject.activeInHierarchy)
        {
            FindSpider();

            if (spiderRenderer == null)
            {
                instructionText.text = "Buscando la araña...";
                return;
            }
        }

        bool looking = IsLookingAtSpider(activeCamera);

        if (looking)
        {
            lostGazeTime = 0f;
            currentTime += Time.unscaledDeltaTime;

            if (currentTime > bestAttempt)
            {
                bestAttempt = currentTime;
            }

            if (currentTime >= requiredSeconds)
            {
                currentTime = requiredSeconds;
                SaveBest();
                UpdateHud(true);
                StartCoroutine(CompleteLevel());
                return;
            }
        }
        else
        {
            lostGazeTime += Time.unscaledDeltaTime;

            if (lostGazeTime > gazeGraceSeconds)
            {
                if (currentTime > bestAttempt)
                {
                    bestAttempt = currentTime;
                }

                currentTime = 0f;
                lostGazeTime = 0f;
                if (metricsCollector != null) metricsCollector.RegisterLevelAttempt();
                SaveBest();
            }
        }

        UpdateHud(looking);
    }

    private bool IsLookingAtSpider(Camera camera)
    {
        if (camera == null || spiderRenderer == null)
        {
            return false;
        }

        Vector3 targetPoint = spiderRenderer.bounds.center;
        Vector3 direction = targetPoint - camera.transform.position;

        if (direction.sqrMagnitude < 0.0001f)
        {
            return true;
        }

        float dot = Vector3.Dot(camera.transform.forward, direction.normalized);

        if (dot <= 0f)
        {
            return false;
        }

        float angle = Vector3.Angle(
            camera.transform.forward,
            direction.normalized);

        return angle <= acceptedGazeAngle;
    }

    private Camera GetActiveGameplayCamera()
    {
        GameObject desktop = FindSceneObject("NP_DesktopPlayer_Normalizado");

        if (desktop == null)
        {
            desktop = FindSceneObject("NP_DesktopPlayer_Niveles45");
        }

        if (desktop != null && desktop.activeInHierarchy)
        {
            Camera desktopCamera =
                desktop.GetComponentInChildren<Camera>(true);

            if (desktopCamera != null &&
                desktopCamera.enabled &&
                desktopCamera.gameObject.activeInHierarchy)
            {
                return desktopCamera;
            }
        }

        GameObject xr = FindSceneObject("XR Origin (XR Rig)");

        if (xr == null)
        {
            xr = FindSceneObject("XR Origin");
        }

        if (xr != null && xr.activeInHierarchy)
        {
            Camera xrCamera =
                xr.GetComponentInChildren<Camera>(true);

            if (xrCamera != null &&
                xrCamera.enabled &&
                xrCamera.gameObject.activeInHierarchy)
            {
                return xrCamera;
            }
        }

        if (Camera.main != null &&
            Camera.main.enabled &&
            Camera.main.gameObject.activeInHierarchy)
        {
            return Camera.main;
        }

        Camera[] cameras = Camera.allCameras;

        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];

            if (camera != null &&
                camera.enabled &&
                camera.gameObject.activeInHierarchy)
            {
                return camera;
            }
        }

        return null;
    }

    private void FindSpider()
    {
        Renderer[] renderers =
            FindObjectsByType<Renderer>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        Renderer best = null;
        int bestScore = int.MinValue;

        foreach (Renderer candidate in renderers)
        {
            if (candidate == null)
            {
                continue;
            }

            string hierarchyName =
                BuildHierarchyName(candidate.transform).ToLowerInvariant();

            if (!hierarchyName.Contains("spider") &&
                !hierarchyName.Contains("arana") &&
                !hierarchyName.Contains("araña") &&
                !hierarchyName.Contains("wolf"))
            {
                continue;
            }

            int score = 0;

            if (hierarchyName.Contains("nivel2"))
            {
                score += 200;
            }

            if (hierarchyName.Contains("wolf"))
            {
                score += 100;
            }

            if (hierarchyName.Contains("spider"))
            {
                score += 80;
            }

            if (candidate.gameObject.activeInHierarchy)
            {
                score += 40;
            }

            Bounds bounds = candidate.bounds;
            float size = bounds.size.magnitude;

            if (size > 0.02f && size < 20f)
            {
                score += 20;
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        spiderRenderer = best;

        if (spiderRenderer == null)
        {
            Debug.LogWarning(
                "NeuroPhobia Nivel2: no se encontró un Renderer de la araña.");
        }
    }

    private static string BuildHierarchyName(Transform current)
    {
        string value = current.name;
        Transform parent = current.parent;

        int guard = 0;

        while (parent != null && guard < 10)
        {
            value = parent.name + "/" + value;
            parent = parent.parent;
            guard++;
        }

        return value;
    }

    private void DisableOldLevel2GazeControllers()
    {
        MonoBehaviour[] behaviours =
            FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour == null || behaviour == this)
            {
                continue;
            }

            string type =
                behaviour.GetType().Name.ToLowerInvariant();

            bool isLevel2 = type.Contains("level2") || type.Contains("nivel2");

            bool isOldGaze =
                type.Contains("gaze") ||
                type.Contains("mirada") ||
                type.Contains("exposure") ||
                type.Contains("distancia");

            if (isLevel2 && isOldGaze)
            {
                behaviour.enabled = false;
            }
        }
    }

    private void DisableOldLevel2Hud()
    {
        Canvas[] canvases =
            FindObjectsByType<Canvas>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        foreach (Canvas canvas in canvases)
        {
            if (canvas == null)
            {
                continue;
            }

            TMP_Text[] texts =
                canvas.GetComponentsInChildren<TMP_Text>(true);

            bool oldHud = texts.Any(text =>
                text != null &&
                (
                    text.text.IndexOf(
                        "DISTANCIA SEGURA",
                        StringComparison.OrdinalIgnoreCase) >= 0 ||
                    text.text.IndexOf(
                        "Mirada continua",
                        StringComparison.OrdinalIgnoreCase) >= 0
                ));

            if (oldHud)
            {
                canvas.gameObject.SetActive(false);
            }
        }
    }

    private bool IsControlSelectorVisible()
    {
        GameObject selectorUi =
            GameObject.Find("NP_UI_Seleccion_PC_RV");

        return selectorUi != null &&
               selectorUi.activeInHierarchy;
    }

    private void BuildHud()
    {
        GameObject root =
            new GameObject("NP_Nivel2_HUD_MiradaEstable", typeof(RectTransform));

        root.transform.SetParent(transform, false);

        hudCanvas = root.AddComponent<Canvas>();
        hudCanvas.renderMode = RenderMode.WorldSpace;
        root.GetComponent<RectTransform>().sizeDelta = new Vector2(1280f, 720f);
        hudCanvas.overrideSorting = true;
        hudCanvas.sortingOrder = 32000;

        CanvasScaler scaler =
            root.AddComponent<CanvasScaler>();

        scaler.uiScaleMode =
            CanvasScaler.ScaleMode.ScaleWithScreenSize;

        scaler.referenceResolution =
            new Vector2(1280f, 720f);

        scaler.screenMatchMode =
            CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

        scaler.matchWidthOrHeight = 0.5f;

        root.AddComponent<GraphicRaycaster>();

        GameObject panel =
            new GameObject("Panel", typeof(RectTransform));

        panel.transform.SetParent(root.transform, false);

        RectTransform panelRect =
            panel.GetComponent<RectTransform>();

        // Esquina inferior izquierda para no tapar la araña.
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.zero;
        panelRect.pivot = Vector2.zero;
        panelRect.sizeDelta = new Vector2(560f, 150f);
        panelRect.anchoredPosition = new Vector2(40f, 40f);

        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.025f, 0.035f, 0.045f, 0.90f);

        TMP_Text title =
            CreateText(
                panel.transform,
                "Titulo",
                23f,
                FontStyles.Bold);

        title.text = "NIVEL 2 · DISTANCIA SEGURA";

        SetRect(
            title.rectTransform,
            new Vector2(0.03f, 0.76f),
            new Vector2(0.97f, 0.98f));

        timerText =
            CreateText(
                panel.transform,
                "Temporizador",
                21f,
                FontStyles.Bold);

        SetRect(
            timerText.rectTransform,
            new Vector2(0.03f, 0.55f),
            new Vector2(0.97f, 0.78f));

        bestText =
            CreateText(
                panel.transform,
                "MejorIntento",
                18f,
                FontStyles.Normal);

        SetRect(
            bestText.rectTransform,
            new Vector2(0.03f, 0.38f),
            new Vector2(0.97f, 0.57f));

        instructionText =
            CreateText(
                panel.transform,
                "Instruccion",
                18f,
                FontStyles.Normal);

        SetRect(
            instructionText.rectTransform,
            new Vector2(0.03f, 0.20f),
            new Vector2(0.97f, 0.40f));

        GameObject barBackground =
            new GameObject(
                "BarraFondo",
                typeof(RectTransform));

        barBackground.transform.SetParent(
            panel.transform,
            false);

        RectTransform barRect =
            barBackground.GetComponent<RectTransform>();

        SetRect(
            barRect,
            new Vector2(0.06f, 0.07f),
            new Vector2(0.94f, 0.15f));

        Image backgroundImage =
            barBackground.AddComponent<Image>();

        backgroundImage.color =
            new Color(0.18f, 0.22f, 0.25f, 0.95f);

        GameObject fill =
            new GameObject(
                "Progreso",
                typeof(RectTransform));

        fill.transform.SetParent(
            barBackground.transform,
            false);

        RectTransform fillRect =
            fill.GetComponent<RectTransform>();

        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        progressFill = fill.AddComponent<Image>();
        progressFill.color =
            new Color(0.08f, 0.78f, 0.72f, 1f);

        progressFill.type = Image.Type.Filled;
        progressFill.fillMethod = Image.FillMethod.Horizontal;
        progressFill.fillOrigin = 0;
        progressFill.fillAmount = 0f;
    }

    private static TMP_Text CreateText(
        Transform parent,
        string name,
        float fontSize,
        FontStyles style)
    {
        GameObject go =
            new GameObject(
                name,
                typeof(RectTransform));

        go.transform.SetParent(parent, false);

        TextMeshProUGUI text =
            go.AddComponent<TextMeshProUGUI>();

        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;

        return text;
    }

    private static void SetRect(
        RectTransform rect,
        Vector2 min,
        Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void UpdateHud(bool looking)
    {
        if (timerText == null)
        {
            return;
        }

        timerText.text =
            "Mirada continua: " +
            currentTime.ToString("0.0", CultureInfo.InvariantCulture) +
            " / " +
            requiredSeconds.ToString("0", CultureInfo.InvariantCulture) +
            " s";

        bestText.text =
            "Mejor intento: " +
            bestAttempt.ToString("0.0", CultureInfo.InvariantCulture) +
            " s";

        instructionText.text =
            looking
                ? "Bien. Mantén la mirada en la araña."
                : "Mira la araña · Stick: moverte · A: sentarte";

        if (progressFill != null)
        {
            progressFill.fillAmount =
                Mathf.Clamp01(currentTime / requiredSeconds);
        }
    }

    private void SaveBest()
    {
        PlayerPrefs.SetFloat(BestKey, bestAttempt);
        PlayerPrefs.Save();
    }

    private IEnumerator CompleteLevel()
    {
        if (completed)
        {
            yield break;
        }

        completed = true;
        SaveBest();

        if (metricsCollector != null && SessionManager.Instance != null)
        {
            LevelMetrics metrics = metricsCollector.BuildMetrics(true);
            metrics.level = 2;
            metrics.gazeTimeOnSpider = Mathf.Max(metrics.gazeTimeOnSpider, requiredSeconds);
            SessionManager.Instance.AddLevelMetrics(metrics);
        }

        if (hudCanvas != null)
        {
            hudCanvas.gameObject.SetActive(false);
        }

        BuildCompletionScreen();
        AttachHudToCamera(GetActiveGameplayCamera());

        float oldScale = Time.timeScale;
        Time.timeScale = 0f;

        float finishTime =
            Time.realtimeSinceStartup + 5f;

        while (Time.realtimeSinceStartup < finishTime)
        {
            yield return null;
        }

        Time.timeScale =
            oldScale > 0f ? oldScale : 1f;

        if (Application.CanStreamedLevelBeLoaded(nextScene))
        {
            SceneManager.LoadSceneAsync(nextScene);
        }
        else
        {
            Debug.LogError(
                "NeuroPhobia Nivel2: no se puede cargar " +
                nextScene +
                ". Verifica que la escena esté agregada al Build.");
        }
    }

    private void BuildCompletionScreen()
    {
        GameObject root =
            new GameObject("NP_Nivel2_Completado", typeof(RectTransform));

        root.transform.SetParent(transform, false);

        completionCanvas = root.AddComponent<Canvas>();
        completionCanvas.renderMode = RenderMode.WorldSpace;
        root.GetComponent<RectTransform>().sizeDelta = new Vector2(1280f, 720f);
        completionCanvas.overrideSorting = true;
        completionCanvas.sortingOrder = 32760;

        CanvasScaler scaler =
            root.AddComponent<CanvasScaler>();

        scaler.uiScaleMode =
            CanvasScaler.ScaleMode.ScaleWithScreenSize;

        scaler.referenceResolution =
            new Vector2(1280f, 720f);

        GameObject background =
            new GameObject(
                "Fondo",
                typeof(RectTransform));

        background.transform.SetParent(
            root.transform,
            false);

        RectTransform backgroundRect =
            background.GetComponent<RectTransform>();

        SetRect(
            backgroundRect,
            Vector2.zero,
            Vector2.one);

        Image image = background.AddComponent<Image>();
        image.color =
            new Color(0.02f, 0.03f, 0.05f, 0.98f);

        TMP_Text text =
            CreateText(
                background.transform,
                "Mensaje",
                45f,
                FontStyles.Bold);

        text.text =
            "¡Felicitaciones!\n\n" +
            "Completaste el Nivel 2\n\n" +
            "El Nivel 3 comenzará en 5 segundos.";

        SetRect(
            text.rectTransform,
            new Vector2(0.10f, 0.18f),
            new Vector2(0.90f, 0.82f));
    }

    private void AttachHudToCamera(Camera camera)
    {
        if (camera == null) return;
        Attach(hudCanvas, camera, 1.1f, 0.0012f);
        Attach(completionCanvas, camera, 1.5f, 0.0015f);
    }

    private static void Attach(Canvas canvas, Camera camera, float distance, float scale)
    {
        if (canvas == null || canvas.transform.parent == camera.transform) return;
        canvas.transform.SetParent(camera.transform, false);
        canvas.transform.localPosition = new Vector3(0f, 0f, distance);
        canvas.transform.localRotation = Quaternion.identity;
        canvas.transform.localScale = Vector3.one * scale;
        canvas.worldCamera = camera;
    }

    private static GameObject FindSceneObject(string exactName)
    {
        Scene scene = SceneManager.GetActiveScene();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform found =
                FindRecursive(
                    root.transform,
                    exactName);

            if (found != null)
            {
                return found.gameObject;
            }
        }

        return null;
    }

    private static Transform FindRecursive(
        Transform current,
        string exactName)
    {
        if (current.name == exactName)
        {
            return current;
        }

        foreach (Transform child in current)
        {
            Transform found =
                FindRecursive(
                    child,
                    exactName);

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
