using System.Collections.Generic;
using UnityEngine;

public class TaskManager : MonoBehaviour
{
    public MetricsCollector metricsCollector;
    [Tooltip("Límite de intentos de alejamiento. Valor heredado, sin fuente: pendiente de validar clínicamente.")]
    [Min(0)] public int maxAvoidanceAttempts = 5;
    public Level5HUD hud;
    private static readonly string[] OfficialTasks = { "TASK_LAMP", "TASK_DOOR", "TASK_FRIDGE", "TASK_WINDOW" };
    private readonly HashSet<string> completedTasks = new HashSet<string>();
    private bool finished;
    private bool repeatOffered;

    /// <summary>true cuando se superó el límite de alejamientos y se ofrece repetir el nivel.</summary>
    public bool RepeatOffered => repeatOffered;

    public int TotalTasks => OfficialTasks.Length;
    public int CompletedCount => completedTasks.Count;
    public List<string> CompletedIds => new List<string>(completedTasks);
    public bool IsCompleted(string id) => completedTasks.Contains(id);

    private void Start()
    {
        if (metricsCollector == null) metricsCollector = FindFirstObjectByType<MetricsCollector>();
        Refresh();
    }

    private void Update()
    {
        if (!finished && completedTasks.Count == TotalTasks && GameStateManager.CurrentState == GameState.PLAYING) TryFinish();
    }

    public void CompleteTask(string taskId)
    {
        if (finished || GameStateManager.CurrentState != GameState.PLAYING || System.Array.IndexOf(OfficialTasks, taskId) < 0) return;
        if (!completedTasks.Add(taskId)) return;
        if (metricsCollector != null) metricsCollector.RegisterTaskCompletion(taskId);
        Refresh();
        TryFinish();
    }

    private void Refresh()
    {
        if (hud != null) hud.SetTasks(completedTasks, TotalTasks);
    }

    public void RefreshHUD() => Refresh();

    private void TryFinish()
    {
        if (completedTasks.Count != TotalTasks || metricsCollector == null) return;
        LevelMetrics metrics = metricsCollector.BuildMetrics(false);
        if (metrics.avoidanceAttempts > maxAvoidanceAttempts)
        {
            // Con demasiados alejamientos el nivel no se da por superado: se ofrece repetirlo (HU-03).
            if (repeatOffered) return;
            string message = $"Tareas completas. Se superó el límite de intentos de alejamiento ({maxAvoidanceAttempts}).";
            repeatOffered = GlobalSessionUI.OfferRepeat(5, message);
            if (!repeatOffered && hud != null) hud.ShowAvoidanceLimit(maxAvoidanceAttempts);
            return;
        }
        finished = true;
        metrics.level = 5;
        metrics.levelPassed = true;
        if (SessionManager.Instance != null) SessionManager.Instance.AddLevelMetrics(metrics);
        GameStateManager.SetState(GameState.SESSION_END);
        if (SessionManager.Instance != null) SessionManager.Instance.EndSession();
        GlobalSessionUI.ShowSessionSummary();
    }
}
