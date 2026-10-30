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
}
