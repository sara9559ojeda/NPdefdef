using System;
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

    [Header("Referencias opcionales")]
    public Renderer spiderRenderer;
    public MetricsCollector metricsCollector;

    private float currentTime;
    private float bestAttempt;
    private float lostGazeTime;
    private bool completed;

    // Shown on the wrist HUD instead of the mission while something is missing (for example the spider).
    private string statusMessage;

    private const string BestKey = "NeuroPhobia_Level2_BestStableGaze";

    private void Start()
    {
        bestAttempt = PlayerPrefs.GetFloat(BestKey, 0f);

        DisableOldLevel2GazeControllers();
        DisableOldLevel2Hud();

        FindSpider();
        if (metricsCollector == null) metricsCollector = FindFirstObjectByType<MetricsCollector>();
        UpdateHud(false);
    }

    private void Update()
    {
        if (completed)
        {
            return;
        }

        // Pausa global (GlobalSessionUI): el tiempo no cuenta.
        if (GameStateManager.CurrentState != GameState.PLAYING)
        {
            return;
        }

        statusMessage = null;

        // Mientras la pantalla Computador/RV está visible no cuenta tiempo.
        if (IsControlSelectorVisible())
        {
            return;
        }

        Camera activeCamera = GetActiveGameplayCamera();

        if (activeCamera == null)
        {
            return;
        }

        if (spiderRenderer == null || !spiderRenderer.gameObject.activeInHierarchy)
        {
            FindSpider();

            if (spiderRenderer == null)
            {
                statusMessage = "Buscando la araña...";
                UpdateHud(false);
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
                CompleteLevel();
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

    // Mission on the wrist HUD; the ring near the centre of the view fills while the look is being counted.
    private void UpdateHud(bool looking)
    {
        float progress = requiredSeconds > 0f ? Mathf.Clamp01(currentTime / requiredSeconds) : 0f;

        string mission = statusMessage ??
            "Mantén la mirada en la araña del frasco " +
            requiredSeconds.ToString("0", CultureInfo.InvariantCulture) + " s seguidos.\n" +
            "Mirada continua: " + currentTime.ToString("0.0", CultureInfo.InvariantCulture) + " s · " +
            "Mejor intento: " + bestAttempt.ToString("0.0", CultureInfo.InvariantCulture) + " s";

        LevelHud.SetMission("NIVEL 2 · DISTANCIA SEGURA", mission, progress);
        LevelHud.SetGaze(progress, looking || currentTime > 0f);
    }

    private void SaveBest()
    {
        PlayerPrefs.SetFloat(BestKey, bestAttempt);
        PlayerPrefs.Save();
    }

    // Mismo cierre que Nivel1, Nivel3 y Nivel4: GlobalSessionUI muestra el panel de nivel completado
    // (Siguiente nivel / Menú principal) en lugar de saltar solo a Nivel3.
    private void CompleteLevel()
    {
        if (completed)
        {
            return;
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

        LevelHud.SetGaze(0f, false);
        GlobalSessionUI.CompleteLevel(2);
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
