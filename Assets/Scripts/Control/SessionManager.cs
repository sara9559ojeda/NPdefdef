using System;
using System.IO;
using UnityEngine;

public class SessionManager : MonoBehaviour
{
    public static SessionManager Instance { get; private set; }

    public string SessionId { get; private set; }
    public int CurrentLevel { get; set; }
    public int PhobiaGrade { get; private set; }

    private SessionReport report;
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
    }

    public void StartSession(int phobiaGrade)
    {
        SessionId = Guid.NewGuid().ToString();
        if (phobiaGrade < 1 || phobiaGrade > 5) throw new ArgumentOutOfRangeException(nameof(phobiaGrade));
        PhobiaGrade = phobiaGrade;
        CurrentLevel = 1;
        sessionStartTime = Time.realtimeSinceStartup;
        ended = false;

        report = new SessionReport
        {
            sessionId = SessionId,
            phobiaGrade = PhobiaGrade,
            timestamp = DateTime.UtcNow.ToString("o")
        };

        if (apiClient != null) StartCoroutine(apiClient.PostSessionStart(SessionId, PhobiaGrade));
        Debug.Log($"NeuroPhobia: sesion iniciada {SessionId}");
    }

    public void AddLevelMetrics(LevelMetrics metrics)
    {
        if (report == null || ended || metrics == null) return;
        if (report.levelMetrics.Exists(existing => existing.level == metrics.level)) return;
        report.levelMetrics.Add(metrics);

        if (apiClient != null) StartCoroutine(apiClient.PostLevelMetrics(SessionId, metrics.level, metrics));
    }

    public void EndSession()
    {
        if (report == null || ended) return;

        report.totalDuration = Time.realtimeSinceStartup - sessionStartTime;
        report.timestamp = DateTime.UtcNow.ToString("o");
        ended = true;
        SaveReportLocally();

        if (apiClient != null) StartCoroutine(apiClient.PostSessionEnd(SessionId, report));
        Debug.Log("NeuroPhobia: sesion finalizada.");
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
