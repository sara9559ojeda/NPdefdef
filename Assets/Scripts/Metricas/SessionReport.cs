using System;
using System.Collections.Generic;

[Serializable]
public class SessionReport
{
    public string sessionId;
    public int phobiaGrade;
    public float totalDuration;
    public List<LevelMetrics> levelMetrics = new List<LevelMetrics>();
    public string timestamp;
    // Sesión de prueba abierta con el selector de nivel: no se usa para el análisis ni la IA.
    public bool testSession;
}

[Serializable]
public class LevelMetrics
{
    public int level;
    public float gazeTimeOnSpider;
    public int avoidanceAttempts;
    public float headMovementFrequency;
    public float proximityMin;
    public List<string> taskCompletions = new List<string>();
    public int breathingActivations;
    public bool levelPassed;
    public float sessionDuration;
    public int levelAttempts;
    public int handWithdrawals;
    public float handContactDuration;
    // Resultado según el contrato API v1 (completed, criterion_not_met, safe_exit, abandoned).
    // Vacío = se deduce de levelPassed (ver DeviceRecordMapper.ResolveOutcome).
    public string outcome;

    // ── Datos ampliados para el análisis (contrato API v1, level_record) ──
    // Ángulo medio (grados) entre la orientación de la cabeza y la araña más cercana; -1 si no se midió.
    public float gazeMeanAngleDeg = -1f;
    // Muestras periódicas de distancia (m) y ángulo (grados) a la araña más cercana; frecuencia en sampleHz.
    public float sampleHz;
    public List<float> distanceSamples = new List<float>();
    public List<float> angleSamples = new List<float>();
    // Eventos con tiempo en segundos de exposición (sin contar pausas): avoidance, pause, resume.
    public List<LevelEvent> events = new List<LevelEvent>();
    // Ansiedad autorreportada (SUDS 0–100, Wolpe 1969): fase before / after.
    public List<SudsEntry> suds = new List<SudsEntry>();
}

[Serializable]
public class LevelEvent
{
    public float t;
    public string type;
    // Solo en avoidance: velocidad de retroceso (m/s). 0 en el resto.
    public float value;
}

[Serializable]
public class SudsEntry
{
    public string phase;
    public int value;
    public float at;
}
