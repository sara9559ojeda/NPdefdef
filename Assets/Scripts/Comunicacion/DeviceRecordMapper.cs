using System;
using System.Collections.Generic;

/// <summary>
/// Convierte los datos de sesión de Unity en los sobres de <c>POST /api/v1/device/records</c>
/// (contrato API v1.1.0, sección 6.3). Sin dependencias de UnityEngine para poder probarlo fuera del Editor.
/// </summary>
public static class DeviceRecordMapper
{
    public const int SchemaVersion = 1;

    /// <summary>
    /// Versión de la definición de niveles que acompaña a cada level_record.
    /// Cambiarla cuando cambien los criterios de éxito de algún nivel.
    /// </summary>
    public const string LevelDefinitionVersion = "2026-11-05";

    /// <summary>
    /// La mirada se mide con un rayo desde el centro de la cabeza (MetricsCollector.TrackGaze),
    /// no con un cono. El contrato exige un valor mayor que 0: se envía 1° para indicar «rayo central».
    /// </summary>
    public const double GazeConeDeg = 1.0;

    public const string OutcomeCompleted = "completed";
    public const string OutcomeCriterionNotMet = "criterion_not_met";
    public const string OutcomeSafeExit = "safe_exit";
    public const string OutcomeAbandoned = "abandoned";

    public const string EndCompleted = "completed";
    public const string EndUserFinished = "user_finished";
    public const string EndSafeExit = "safe_exit";
    public const string EndAppClosed = "app_closed";

    private static readonly HashSet<string> Outcomes = new HashSet<string> { OutcomeCompleted, OutcomeCriterionNotMet, OutcomeSafeExit, OutcomeAbandoned };
    // Eventos de LevelMetrics que se envían (level_start, level_end, safe_exit y suds los añade el mapeo).
    private static readonly HashSet<string> RecordedEvents = new HashSet<string> { "avoidance", "pause", "resume" };
    private static readonly HashSet<string> EndReasons = new HashSet<string> { EndCompleted, EndUserFinished, EndSafeExit, EndAppClosed };

    public static string NewId() { return Guid.NewGuid().ToString(); }

    public static string SessionStart(string recordId, string sessionId, DateTime startedUtc, int phobiaGrade,
        string appVersion, string deviceModel, bool testSession)
    {
        NeuroPhobiaJson json = Envelope(recordId, "session_start", sessionId);
        json.Key("payload").BeginObject()
            .Prop("startedAt", NeuroPhobiaJson.Iso(startedUtc))
            .Prop("phobiaGrade", Clamp(phobiaGrade, 1, 5))
            .Prop("appVersion", SafeVersion(appVersion))
            .Prop("deviceModel", NormalizeDeviceModel(deviceModel))
            .Prop("testSession", testSession)
            .EndObject();
        return json.EndObject().ToString();
    }

    public static string LevelRecord(string recordId, string sessionId, LevelMetrics metrics, int attempt, DateTime endedUtc)
    {
        if (metrics == null) throw new ArgumentNullException(nameof(metrics));
        double permanence = Math.Max(0.0, Finite(metrics.sessionDuration));
        DateTime startedUtc = endedUtc.AddSeconds(-permanence);
        string outcome = ResolveOutcome(metrics);

        NeuroPhobiaJson json = Envelope(recordId, "level_record", sessionId);
        json.Key("payload").BeginObject()
            .Prop("level", Clamp(metrics.level, 1, 5))
            .Prop("attempt", Math.Max(1, attempt))
            .Prop("levelDefinitionVersion", LevelDefinitionVersion)
            .Prop("startedAt", NeuroPhobiaJson.Iso(startedUtc))
            .Prop("endedAt", NeuroPhobiaJson.Iso(endedUtc))
            .Prop("outcome", outcome)
            .Prop("criterionMet", metrics.levelPassed)
            .Prop("permanenceSeconds", permanence)
            // proximityMin = -1 en Unity significa «sin estímulo muestreado»; el contrato usa null.
            .Prop("minDistanceM", metrics.proximityMin >= 0f ? (double?)metrics.proximityMin : null);

        json.Key("gaze").BeginObject()
            .Prop("coneDeg", GazeConeDeg)
            .Prop("onStimulusSeconds", Math.Max(0.0, Finite(metrics.gazeTimeOnSpider)))
            .Prop("meanAngleDeg", metrics.gazeMeanAngleDeg >= 0f ? (double?)Math.Min(180.0, metrics.gazeMeanAngleDeg) : null)
            .EndObject();

        json.Prop("avoidanceCount", Math.Max(0, metrics.avoidanceAttempts));

        // SUDS antes y después (0–100). La fase «durante» no se pregunta (su frecuencia está pendiente de validar).
        json.Key("suds").BeginArray();
        if (metrics.suds != null)
            foreach (SudsEntry entry in metrics.suds)
            {
                if (entry == null || entry.value < 0 || entry.value > 100) continue;
                if (entry.phase != "before" && entry.phase != "during" && entry.phase != "after") continue;
                json.BeginObject().Prop("phase", entry.phase).Prop("value", entry.value).Prop("at", Math.Max(0.0, Finite(entry.at))).EndObject();
            }
        json.EndArray();

        json.Key("events").BeginArray();
        json.BeginObject().Prop("t", 0.0).Prop("type", "level_start").EndObject();
        if (metrics.events != null)
            foreach (LevelEvent item in metrics.events)
            {
                if (item == null || !RecordedEvents.Contains(item.type)) continue;
                json.BeginObject().Prop("t", Math.Max(0.0, Finite(item.t))).Prop("type", item.type);
                if (item.type == "avoidance") json.Key("data").BeginObject().Prop("retreatSpeed", Finite(item.value)).EndObject();
                json.EndObject();
            }
        if (metrics.suds != null)
            foreach (SudsEntry entry in metrics.suds)
                if (entry != null && entry.value >= 0 && entry.value <= 100)
                    json.BeginObject().Prop("t", Math.Max(0.0, Finite(entry.at))).Prop("type", "suds")
                        .Key("data").BeginObject().Prop("phase", entry.phase).Prop("value", entry.value).EndObject().EndObject();
        if (outcome == OutcomeSafeExit) json.BeginObject().Prop("t", permanence).Prop("type", "safe_exit").EndObject();
        json.BeginObject().Prop("t", permanence).Prop("type", "level_end").EndObject();
        json.EndArray();

        // Muestras periódicas de distancia y ángulo a la araña más cercana.
        if (metrics.sampleHz > 0f && metrics.distanceSamples != null && metrics.distanceSamples.Count > 0)
        {
            json.Key("samples").BeginObject().Prop("hz", (double)metrics.sampleHz);
            json.Key("distanceM").BeginArray();
            foreach (float value in metrics.distanceSamples) json.Value(Math.Round(Finite(value), 3));
            json.EndArray();
            json.Key("angleDeg").BeginArray();
            if (metrics.angleSamples != null) foreach (float value in metrics.angleSamples) json.Value(Math.Round(Finite(value), 1));
            json.EndArray();
            json.EndObject();
        }

        // Métricas que ya mide el proyecto (LevelMetrics) y no tienen campo propio en el contrato.
        json.Key("extra").BeginObject()
            .Prop("taskCompletions", metrics.taskCompletions ?? new List<string>())
            .Prop("breathingActivations", Math.Max(0, metrics.breathingActivations))
            .Prop("headMovementMean", Finite(metrics.headMovementFrequency))
            .Prop("handContactSeconds", Math.Max(0.0, Finite(metrics.handContactDuration)))
            .Prop("handWithdrawals", Math.Max(0, metrics.handWithdrawals))
            .Prop("levelAttempts", Math.Max(0, metrics.levelAttempts))
            .EndObject();

        json.EndObject();
        return json.EndObject().ToString();
    }

    public static string SessionEnd(string recordId, string sessionId, DateTime endedUtc, string endReason,
        int levelReached, double totalDurationSeconds)
    {
        NeuroPhobiaJson json = Envelope(recordId, "session_end", sessionId);
        json.Key("payload").BeginObject()
            .Prop("endedAt", NeuroPhobiaJson.Iso(endedUtc))
            .Prop("endReason", EndReasons.Contains(endReason) ? endReason : EndUserFinished)
            .Prop("levelReached", Clamp(levelReached, 0, 5))
            .Prop("totalDurationSeconds", Math.Max(0.0, Finite(totalDurationSeconds)))
            .EndObject();
        return json.EndObject().ToString();
    }

    /// <summary>Resultado del nivel: el explícito si es válido; si no, completado o criterio no cumplido.</summary>
    public static string ResolveOutcome(LevelMetrics metrics)
    {
        if (metrics != null && !string.IsNullOrEmpty(metrics.outcome) && Outcomes.Contains(metrics.outcome)) return metrics.outcome;
        return metrics != null && metrics.levelPassed ? OutcomeCompleted : OutcomeCriterionNotMet;
    }

    /// <summary>Motivo de cierre por defecto: completed si se superó el Nivel5, si no user_finished.</summary>
    public static string ResolveEndReason(SessionReport report, string requested)
    {
        if (!string.IsNullOrEmpty(requested) && EndReasons.Contains(requested)) return requested;
        if (report != null && report.levelMetrics != null && report.levelMetrics.Exists(m => m != null && m.level == 5 && m.levelPassed)) return EndCompleted;
        return EndUserFinished;
    }

    public static int LevelReached(SessionReport report)
    {
        int reached = 0;
        if (report != null && report.levelMetrics != null)
            foreach (LevelMetrics m in report.levelMetrics) if (m != null && m.level > reached) reached = m.level;
        return Clamp(reached, 0, 5);
    }

    /// <summary>Valores admitidos por el contrato: quest2, quest3, quest3s, editor.</summary>
    public static string NormalizeDeviceModel(string raw)
    {
        string value = (raw ?? "").ToLowerInvariant().Replace(" ", "");
        if (value == "quest2" || value == "quest3" || value == "quest3s" || value == "editor") return value;
        if (value.Contains("3s")) return "quest3s";
        if (value.Contains("quest3") || value.Contains("eureka")) return "quest3";
        if (value.Contains("quest")) return "quest2";
        return "editor";
    }

    /// <summary>appVersion solo admite letras, dígitos y _ . + - (máximo 32).</summary>
    public static string SafeVersion(string raw)
    {
        var sb = new System.Text.StringBuilder();
        foreach (char c in raw ?? "")
            if (char.IsLetterOrDigit(c) && c < 128 || c == '_' || c == '.' || c == '+' || c == '-') sb.Append(c);
        string value = sb.Length == 0 ? "0.0.0" : sb.ToString();
        return value.Length > 32 ? value.Substring(0, 32) : value;
    }

    private static NeuroPhobiaJson Envelope(string recordId, string type, string sessionId)
    {
        return new NeuroPhobiaJson().BeginObject()
            .Prop("recordId", recordId)
            .Prop("type", type)
            .Prop("schemaVersion", SchemaVersion)
            .Prop("sessionId", sessionId);
    }

    private static int Clamp(int value, int min, int max) { return value < min ? min : value > max ? max : value; }
    private static double Finite(double value) { return double.IsNaN(value) || double.IsInfinity(value) ? 0.0 : value; }
}
