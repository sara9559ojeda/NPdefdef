using System;
using System.IO;
using UnityEngine;

public class SessionManager : MonoBehaviour
{
    public static SessionManager Instance { get; private set; }

    public string SessionId { get; private set; }
    public int CurrentLevel { get; set; }
    public int PhobiaGrade { get; private set; }
    public bool HasEnded => ended;

    private SessionReport report;
    private readonly System.Collections.Generic.Dictionary<int, int> levelRetries = new System.Collections.Generic.Dictionary<int, int>();
    // Métricas ya registradas del intento en curso de cada nivel (evita registrar dos veces el mismo intento).
    private readonly System.Collections.Generic.Dictionary<int, LevelMetrics> currentAttemptMetrics = new System.Collections.Generic.Dictionary<int, LevelMetrics>();
    // SUDS «antes» del intento en curso de cada nivel (se adjunta al registrar sus métricas).
    private readonly System.Collections.Generic.Dictionary<int, SudsEntry> beforeSuds = new System.Collections.Generic.Dictionary<int, SudsEntry>();
    // Último intento registrado y aún sin enviar: espera al SUDS «después» (o a que se salte) para salir completo.
    private LevelMetrics pendingPost;
    private float sessionStartTime;
    private bool ended;
    private FastAPIClient apiClient;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        apiClient = GetComponent<FastAPIClient>();
        // Red de seguridad: al cambiar de escena nada queda sin enviar.
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (_, __) => PostPendingLevel();
    }

    /// <param name="testSession">true si se abrió desde el selector de nivel (solo builds de desarrollo); el reporte lo marca para excluirlo del análisis.</param>
    public void StartSession(int phobiaGrade, bool testSession = false)
    {
        SessionId = Guid.NewGuid().ToString();
        if (phobiaGrade < 1 || phobiaGrade > 5) throw new ArgumentOutOfRangeException(nameof(phobiaGrade));
        PhobiaGrade = phobiaGrade;
        CurrentLevel = 1;
        levelRetries.Clear();
        currentAttemptMetrics.Clear();
        beforeSuds.Clear();
        pendingPost = null;
        sessionStartTime = Time.realtimeSinceStartup;
        ended = false;

        report = new SessionReport
        {
            sessionId = SessionId,
            phobiaGrade = PhobiaGrade,
            timestamp = DateTime.UtcNow.ToString("o"),
            testSession = testSession
        };

        if (apiClient != null) StartCoroutine(apiClient.PostSessionStart(SessionId, PhobiaGrade, testSession));
        Debug.Log($"NeuroPhobia: sesion iniciada {SessionId}");
    }

    /// <summary>Cuenta una repetición de un nivel (se refleja en «intentos de nivel»). El siguiente intento se registra aparte.</summary>
    public void RegisterLevelRetry(int level)
    {
        levelRetries.TryGetValue(level, out int current);
        levelRetries[level] = current + 1;
        PostPendingLevel();
        currentAttemptMetrics.Remove(level);
        beforeSuds.Remove(level);
    }

    public int LevelRetries(int level)
    {
        return levelRetries.TryGetValue(level, out int current) ? current : 0;
    }

    /// <summary>Número del intento en curso de un nivel, desde 1 (como `attempt` del contrato).</summary>
    public int CurrentAttempt(int level)
    {
        return LevelRetries(level) + 1;
    }

    /// <summary>Métricas ya registradas del intento en curso del nivel, o null.</summary>
    public LevelMetrics CurrentAttemptMetrics(int level)
    {
        return currentAttemptMetrics.TryGetValue(level, out LevelMetrics metrics) ? metrics : null;
    }

    /// <summary>Registra las métricas de un intento. Cada intento de un nivel se guarda una sola vez;
    /// tras RegisterLevelRetry, el nuevo intento del mismo nivel se añade como otro registro.</summary>
    public void AddLevelMetrics(LevelMetrics metrics)
    {
        if (report == null || ended || metrics == null) return;
        if (currentAttemptMetrics.ContainsKey(metrics.level)) return;
        currentAttemptMetrics[metrics.level] = metrics;
        if (metrics.suds == null) metrics.suds = new System.Collections.Generic.List<SudsEntry>();
        if (beforeSuds.TryGetValue(metrics.level, out SudsEntry before) && !metrics.suds.Exists(s => s.phase == "before")) metrics.suds.Insert(0, before);
        report.levelMetrics.Add(metrics);

        PostPendingLevel();
        pendingPost = metrics;
    }

    /// <summary>SUDS «antes» del intento en curso (0–100). Se ignora si el valor no es válido.</summary>
    public void RecordSudsBefore(int level, int value)
    {
        if (report == null || ended || value < 0 || value > 100) return;
        beforeSuds[level] = new SudsEntry { phase = "before", value = value, at = 0f };
    }

    /// <summary>true si el último intento registrado de ese nivel aún espera el SUDS «después».</summary>
    public bool AwaitingSudsAfter(int level)
    {
        return pendingPost != null && pendingPost.level == level && !pendingPost.suds.Exists(s => s.phase == "after");
    }

    /// <summary>SUDS «después» (0–100) del último intento; value &lt; 0 = prefirió no responder. En ambos casos el registro se envía.</summary>
    public void RecordSudsAfter(int level, int value)
    {
        if (pendingPost == null || pendingPost.level != level) return;
        if (value >= 0 && value <= 100 && !pendingPost.suds.Exists(s => s.phase == "after"))
            pendingPost.suds.Add(new SudsEntry { phase = "after", value = value, at = Mathf.Max(0f, pendingPost.sessionDuration) });
        PostPendingLevel();
    }

    /// <summary>Envía (a la bandeja) el intento que esperaba el SUDS «después».</summary>
    public void PostPendingLevel()
    {
        if (pendingPost == null) return;
        LevelMetrics metrics = pendingPost;
        pendingPost = null;
        if (apiClient != null) StartCoroutine(apiClient.PostLevelMetrics(SessionId, metrics.level, metrics));
    }

    /// <param name="endReason">Motivo del contrato API v1: completed, user_finished, safe_exit o app_closed. Null = se deduce del reporte.</param>
    public void EndSession(string endReason = null)
    {
        if (report == null || ended) return;

        PostPendingLevel();
        report.totalDuration = Time.realtimeSinceStartup - sessionStartTime;
        report.timestamp = DateTime.UtcNow.ToString("o");
        ended = true;
        SaveReportLocally();

        if (apiClient != null) StartCoroutine(apiClient.PostSessionEnd(SessionId, report, endReason));
        Debug.Log("NeuroPhobia: sesion finalizada.");
    }

    // Si la app se cierra con una sesión abierta, el cierre queda en la bandeja y se envía al volver a abrirla.
    private void OnApplicationQuit()
    {
        EndSession(DeviceRecordMapper.EndAppClosed);
    }

    public SessionReport GetReport()
    {
        return report;
    }

    public int ResolveInitialLevel(int phobiaGrade)
    {
        if (phobiaGrade < 1 || phobiaGrade > 5) throw new ArgumentOutOfRangeException(nameof(phobiaGrade));
        return 1;
    }

    private void SaveReportLocally()
    {
        try
        {
            string json = JsonUtility.ToJson(report, true);
            string path = Path.Combine(Application.persistentDataPath, $"session_{SessionId}.json");
            File.WriteAllText(path, json);
            Debug.Log($"NeuroPhobia: reporte guardado en {path}");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"NeuroPhobia: no se pudo guardar reporte local. {ex.Message}");
        }
    }
}
