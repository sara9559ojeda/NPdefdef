using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Cliente del backend según el contrato API v1.1.0 (NP_Back/docs/api/CONTRATO_API_v1.md, sección 6):
/// emparejamiento del visor, contexto en el Refugio, grado de fobia y bandeja de salida de registros.
/// Nada de esto bloquea la terapia: si no hay red, los registros esperan en disco y se reenvían.
/// </summary>
public class FastAPIClient : MonoBehaviour
{
    public const string BaseUrlPrefsKey = "NeuroPhobia.BackendUrl";
    public const string BaseUrlFileName = "backend_url.txt";
    public const string ApiPrefix = "/api/v1";
    private const string CredentialsFileName = "device_credentials.json";
    private const string ContextFileName = "device_context.json";

    // Valor por defecto; se anula con persistentDataPath/backend_url.txt o con PlayerPrefs (ver PARAMETROS_IA.txt).
    // Se admite con o sin /api/v1 al final.
    [SerializeField] private string baseUrl = "http://localhost:8000";
    [SerializeField] private int timeoutSeconds = 10;

    private string apiRoot;
    private RecordOutbox outbox;
    private DeviceCredentials credentials;
    private DeviceContextDto cachedContext;
    private bool flushing;
    private int consecutiveFailures;
    private float nextAttemptAt;
    private readonly Dictionary<string, int> attemptsByLevel = new Dictionary<string, int>();

    /// <summary>Raíz de la API, terminada en /api/v1.</summary>
    public string BaseUrl => apiRoot;
    public bool IsPaired => credentials != null && !string.IsNullOrEmpty(credentials.deviceToken);
    /// <summary>Registros pendientes del emparejamiento actual (los de otro emparejamiento no se envían).</summary>
    public int PendingCount => outbox != null ? outbox.PendingCount(Owner) : 0;
    private string Owner => IsPaired && !string.IsNullOrEmpty(credentials.deviceId) ? credentials.deviceId : RecordOutbox.Unpaired;
    public int QuarantineCount => outbox != null ? outbox.QuarantineCount : 0;
    /// <summary>Último contexto recibido (o la copia guardada si no hay red). Null si nunca se recibió.</summary>
    public DeviceContextDto Context => cachedContext;
    /// <summary>true mientras el último envío falló por red o por la base de datos (BPMN «¿Almacena datos? no»).</summary>
    public bool StorageFailed { get; private set; }
    /// <summary>Mensaje visible del último problema, o vacío.</summary>
    public string LastError { get; private set; } = "";
    /// <summary>Estado de la retroalimentación de la última sesión cerrada: saved, pending, not_applicable, insufficient_data o vacío.</summary>
    public string LastFeedbackStatus { get; private set; } = "";
    public string LastFeedbackSessionId { get; private set; } = "";

    /// <summary>Se lanza cuando cambia el emparejamiento, la bandeja o el estado de envío.</summary>
    public event Action StatusChanged;

    private void Awake()
    {
        apiRoot = ApiRootFrom(ResolveBaseUrl(baseUrl));
        outbox = new RecordOutbox(Application.persistentDataPath);
        credentials = LoadJson<DeviceCredentials>(CredentialsFileName);
        cachedContext = LoadJson<DeviceContextDto>(ContextFileName);
        Debug.Log($"NeuroPhobia: backend en {apiRoot}; visor {(IsPaired ? "vinculado" : "sin vincular")}; {PendingCount} registros pendientes.");
    }

    private void Start()
    {
        StartCoroutine(FlushLoop());
    }

    // ───────────── configuración ─────────────

    /// <summary>Guarda una URL en PlayerPrefs (p. ej. http://192.168.1.50:8000) y la aplica de inmediato.</summary>
    public void SetBaseUrl(string url)
    {
        string normalized = Normalize(url);
        if (normalized == null) return;
        PlayerPrefs.SetString(BaseUrlPrefsKey, normalized);
        PlayerPrefs.Save();
        apiRoot = ApiRootFrom(normalized);
    }

    /// <summary>Añade /api/v1 si la URL configurada no lo trae.</summary>
    public static string ApiRootFrom(string url)
    {
        string trimmed = (url ?? "").Trim().TrimEnd('/');
        return trimmed.EndsWith(ApiPrefix, StringComparison.OrdinalIgnoreCase) ? trimmed : trimmed + ApiPrefix;
    }

    // Prioridad: archivo backend_url.txt > PlayerPrefs > valor serializado.
    private static string ResolveBaseUrl(string fallback)
    {
        try
        {
            string path = Path.Combine(Application.persistentDataPath, BaseUrlFileName);
            if (File.Exists(path))
            {
                string fromFile = Normalize(File.ReadAllText(path));
                if (fromFile != null) return fromFile;
                Debug.LogWarning($"NeuroPhobia: {path} no contiene una URL http(s) valida; se ignora.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"NeuroPhobia: no se pudo leer {BaseUrlFileName}. {ex.Message}");
        }

        string fromPrefs = Normalize(PlayerPrefs.GetString(BaseUrlPrefsKey, ""));
        return fromPrefs ?? fallback.TrimEnd('/');
    }

    public static string Normalize(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        url = url.Trim().TrimEnd('/');
        bool valid = Uri.TryCreate(url, UriKind.Absolute, out Uri uri) && (uri.Scheme == "http" || uri.Scheme == "https");
        return valid ? url : null;
    }

    // ───────────── emparejamiento (6.1) y cierre de sesión del visor (6.6) ─────────────

    /// <summary>Código de 6 caracteres que muestra la web. Se admite con o sin guion.</summary>
    public static string NormalizePairingCode(string code)
    {
        var sb = new StringBuilder();
        foreach (char c in (code ?? "").ToUpperInvariant()) if (char.IsLetterOrDigit(c)) sb.Append(c);
        string raw = sb.ToString();
        return raw.Length == 6 ? raw.Substring(0, 3) + "-" + raw.Substring(3) : raw;
    }

    /// <param name="onDone">(ok, mensaje para la persona).</param>
    public IEnumerator Pair(string code, Action<bool, string> onDone)
    {
        string normalized = NormalizePairingCode(code);
        if (normalized.Length != 7)
        {
            onDone?.Invoke(false, "El código tiene 6 caracteres. Revísalo e inténtalo de nuevo.");
            yield break;
        }

        string body = new NeuroPhobiaJson().BeginObject()
            .Prop("code", normalized)
            .Prop("deviceModel", DeviceModel())
            .Prop("appVersion", DeviceRecordMapper.SafeVersion(Application.version))
            .EndObject().ToString();

        using (UnityWebRequest request = JsonRequest("/device/pair", "POST", body, false))
        {
            yield return request.SendWebRequest();
            long status = request.responseCode;
            if (status == 201)
            {
                DeviceCredentials received = Parse<DeviceCredentials>(request.downloadHandler.text);
                if (received != null && !string.IsNullOrEmpty(received.deviceToken))
                {
                    credentials = received;
                    SaveJson(CredentialsFileName, credentials);
                    consecutiveFailures = 0;
                    nextAttemptAt = 0f;
                    LastError = "";
                    Notify();
                    onDone?.Invoke(true, "Visor vinculado correctamente.");
                    yield break;
                }
            }
            onDone?.Invoke(false, PairingError(status));
        }
    }

    public static string PairingError(long status)
    {
        switch (status)
        {
            case 404: return "El código no es válido. Revísalo e inténtalo de nuevo.";
            case 410: return "El código caducó o ya se usó. Pide uno nuevo en la web.";
            case 422: return "El código no tiene el formato correcto. Revísalo e inténtalo de nuevo.";
            case 429: return "Demasiados intentos. Espera unos minutos e inténtalo de nuevo.";
            case 0: return "No hay conexión con el servidor. Revisa la red e inténtalo de nuevo.";
            default: return "No se pudo vincular el visor. Inténtalo de nuevo.";
        }
    }

    /// <summary>Cierra la sesión del visor: revoca el token en el backend y lo borra del visor.</summary>
    public IEnumerator Unpair(Action<bool> onDone)
    {
        bool ok = true;
        if (IsPaired && PendingCount > 0)
        {
            // Esos registros solo se pueden enviar con este token: no se cierra la sesión hasta enviarlos.
            onDone?.Invoke(false);
            yield break;
        }
        if (IsPaired)
        {
            using (UnityWebRequest request = JsonRequest("/device/unpair", "POST", null, true))
            {
                yield return request.SendWebRequest();
                // 401 = ya estaba revocado: igual se borra localmente.
                ok = request.responseCode == 204 || request.responseCode == 401;
            }
        }
        if (ok) ForgetCredentials();
        onDone?.Invoke(ok);
    }

    private void ForgetCredentials()
    {
        credentials = null;
        DeleteFile(CredentialsFileName);
        // El contexto (grado y nivel permitido) era de ese paciente: no debe pasar al siguiente que vincule el visor.
        cachedContext = null;
        DeleteFile(ContextFileName);
        Notify();
    }

    // ───────────── contexto (6.2) y grado de fobia (6.4) ─────────────

    /// <summary>Pide el contexto en el Refugio (nunca durante la exposición). Sin red usa la última copia.</summary>
    public IEnumerator RefreshContext(Action<DeviceContextDto> onDone)
    {
        if (IsPaired)
        {
            using (UnityWebRequest request = JsonRequest("/device/context", "GET", null, true))
            {
                yield return request.SendWebRequest();
                if (request.responseCode == 200)
                {
                    DeviceContextDto received = Parse<DeviceContextDto>(request.downloadHandler.text);
                    if (received != null)
                    {
                        cachedContext = received;
                        SaveJson(ContextFileName, cachedContext);
                    }
                }
                else HandleAuthFailure(request);
            }
        }
        onDone?.Invoke(cachedContext);
    }

    /// <summary>BPMN «Ajustar grado de fobia». Si falla, el grado igual viaja en el session_start.</summary>
    public IEnumerator PutPhobiaGrade(int phobiaGrade)
    {
        if (!IsPaired || phobiaGrade < 1 || phobiaGrade > 5) yield break;
        string body = new NeuroPhobiaJson().BeginObject().Prop("phobiaGrade", phobiaGrade).EndObject().ToString();
        using (UnityWebRequest request = JsonRequest("/device/phobia-grade", "PUT", body, true))
        {
            yield return request.SendWebRequest();
            if (request.responseCode == 200)
            {
                if (cachedContext == null) cachedContext = new DeviceContextDto { allowedStartLevel = 1 };
                cachedContext.phobiaGrade = phobiaGrade;
                SaveJson(ContextFileName, cachedContext);
            }
            else HandleAuthFailure(request);
        }
    }

    /// <summary>Nivel inicial permitido por el backend (1–5); 1 si nunca se recibió contexto.</summary>
    public int AllowedStartLevel()
    {
        int level = cachedContext != null ? cachedContext.allowedStartLevel : 1;
        return level < 1 || level > 5 ? 1 : level;
    }

    /// <summary>Último grado guardado en el backend, o 0 si no hay.</summary>
    public int LastPhobiaGrade()
    {
        int grade = cachedContext != null ? cachedContext.phobiaGrade : 0;
        return grade < 1 || grade > 5 ? 0 : grade;
    }

    // ───────────── registros de sesión (6.3) ─────────────

    public IEnumerator PostSessionStart(string sessionId, int phobiaGrade)
    {
        return PostSessionStart(sessionId, phobiaGrade, false);
    }

    public IEnumerator PostSessionStart(string sessionId, int phobiaGrade, bool testSession)
    {
        ResetAttempts();
        string recordId = DeviceRecordMapper.NewId();
        string json = DeviceRecordMapper.SessionStart(recordId, sessionId, DateTime.UtcNow, phobiaGrade,
            Application.version, DeviceModel(), testSession);
        Enqueue("session_start", recordId, json);
        yield break;
    }

    public IEnumerator PostLevelMetrics(string sessionId, int level, LevelMetrics metrics)
    {
        if (metrics == null) yield break;
        string key = sessionId + ":" + level;
        attemptsByLevel.TryGetValue(key, out int previous);
        int attempt = Math.Max(previous + 1, 1);
        attemptsByLevel[key] = attempt;

        string recordId = DeviceRecordMapper.NewId();
        Enqueue("level_record", recordId, DeviceRecordMapper.LevelRecord(recordId, sessionId, metrics, attempt, DateTime.UtcNow));
        yield break;
    }

    public IEnumerator PostSessionEnd(string sessionId, SessionReport fullReport)
    {
        return PostSessionEnd(sessionId, fullReport, null);
    }

    /// <param name="endReason">completed, user_finished, safe_exit o app_closed; null = se deduce del reporte.</param>
    public IEnumerator PostSessionEnd(string sessionId, SessionReport fullReport, string endReason)
    {
        string recordId = DeviceRecordMapper.NewId();
        string json = DeviceRecordMapper.SessionEnd(recordId, sessionId, DateTime.UtcNow,
            DeviceRecordMapper.ResolveEndReason(fullReport, endReason),
            DeviceRecordMapper.LevelReached(fullReport),
            fullReport != null ? fullReport.totalDuration : 0f);
        Enqueue("session_end", recordId, json);
        yield break;
    }

    private void ResetAttempts() { attemptsByLevel.Clear(); }

    private void Enqueue(string type, string recordId, string json)
    {
        try
        {
            outbox.Enqueue(Owner, type, recordId, json);
        }
        catch (Exception ex)
        {
            StorageFailed = true;
            LastError = "No se pudieron guardar los datos en el visor.";
            Debug.LogWarning($"NeuroPhobia: no se pudo escribir el registro {type} en la bandeja. {ex.Message}");
        }
        // Un registro nuevo se intenta enviar enseguida.
        nextAttemptAt = 0f;
        Notify();
    }

    private IEnumerator FlushLoop()
    {
        var wait = new WaitForSecondsRealtime(1f);
        while (true)
        {
            if (!flushing && IsPaired && Time.realtimeSinceStartup >= nextAttemptAt && PendingCount > 0)
                yield return FlushOnce();
            yield return wait;
        }
    }

    /// <summary>Envía los sobres en orden. Se detiene en el primer fallo temporal para no desordenarlos.</summary>
    private IEnumerator FlushOnce()
    {
        flushing = true;
        foreach (string path in outbox.Pending(Owner))
        {
            if (!IsPaired) break;
            string json;
            try { json = File.ReadAllText(path); }
            catch (Exception ex) { Debug.LogWarning($"NeuroPhobia: no se pudo leer {Path.GetFileName(path)}. {ex.Message}"); continue; }

            using (UnityWebRequest request = JsonRequest("/device/records", "POST", json, true))
            {
                yield return request.SendWebRequest();
                long status = request.result == UnityWebRequest.Result.ConnectionError ? 0 : request.responseCode;
                OutboxAction action = RecordOutbox.Classify(status);
                outbox.Apply(path, action);

                if (action == OutboxAction.Delete)
                {
                    consecutiveFailures = 0;
                    StorageFailed = false;
                    LastError = "";
                    ReadFeedback(request.downloadHandler.text);
                }
                else if (action == OutboxAction.Quarantine)
                {
                    LastError = "El servidor rechazó un registro; quedó guardado aparte en el visor.";
                    Debug.LogWarning($"NeuroPhobia: registro {Path.GetFileName(path)} en cuarentena (HTTP {status}).");
                }
                else if (action == OutboxAction.NeedsPairing)
                {
                    HandleAuthFailure(request);
                    break;
                }
                else
                {
                    consecutiveFailures++;
                    StorageFailed = true;
                    LastError = "No se pudieron guardar los datos en el servidor. Se reintentará automáticamente.";
                    double delay = RecordOutbox.RetryDelaySeconds(consecutiveFailures, request.GetResponseHeader("Retry-After"));
                    nextAttemptAt = Time.realtimeSinceStartup + (float)delay;
                    Debug.LogWarning($"NeuroPhobia: backend no disponible (HTTP {status}); nuevo intento en {delay:0} s.");
                    break;
                }
            }
            Notify();
        }
        flushing = false;
        Notify();
    }

    private void ReadFeedback(string body)
    {
        RecordResultDto result = Parse<RecordResultDto>(body);
        if (result == null || result.feedback == null || string.IsNullOrEmpty(result.feedback.status)) return;
        // Solo interesa al cerrar la sesión: «retroalimentación guardada» del diagrama de secuencia.
        if (result.session != null && result.session.status == "closed")
        {
            LastFeedbackStatus = result.feedback.status;
            LastFeedbackSessionId = result.session.sessionId;
        }
    }

    private void HandleAuthFailure(UnityWebRequest request)
    {
        if (request.responseCode != 401) return;
        ProblemDto problem = Parse<ProblemDto>(request.downloadHandler != null ? request.downloadHandler.text : null);
        // Token revocado o desconocido: se borra y la app vuelve a pedir código. Los registros se conservan.
        Debug.LogWarning($"NeuroPhobia: el backend rechazó el token del visor ({(problem != null ? problem.code : "401")}).");
        LastError = "El visor no está vinculado. Vincúlalo con un código de la web para enviar los datos.";
        ForgetCredentials();
    }

    /// <summary>Texto breve del estado de envío para la interfaz.</summary>
    public string SyncStatusText()
    {
        if (!IsPaired) return "Datos guardados en el visor. Vincula el visor para enviarlos al servidor.";
        if (StorageFailed && !string.IsNullOrEmpty(LastError)) return LastError;
        int pending = PendingCount;
        if (pending > 0) return $"Enviando datos al servidor… ({pending} pendientes)";
        if (LastFeedbackStatus == "saved") return "Datos guardados en el servidor. Retroalimentación guardada.";
        return "Datos guardados en el servidor.";
    }

    // ───────────── utilidades ─────────────

    private UnityWebRequest JsonRequest(string endpoint, string method, string json, bool authorized)
    {
        var request = new UnityWebRequest(apiRoot + endpoint, method);
        if (json != null)
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.SetRequestHeader("Content-Type", "application/json");
        }
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Accept", "application/json");
        if (authorized && IsPaired) request.SetRequestHeader("Authorization", "Bearer " + credentials.deviceToken);
        request.timeout = timeoutSeconds;
        return request;
    }

    private static string DeviceModel()
    {
        if (Application.isEditor) return "editor";
        return DeviceRecordMapper.NormalizeDeviceModel(SystemInfo.deviceModel + " " + SystemInfo.deviceName);
    }

    private void Notify()
    {
        try { StatusChanged?.Invoke(); }
        catch (Exception ex) { Debug.LogWarning($"NeuroPhobia: error en un oyente de estado del backend. {ex.Message}"); }
    }

    private static readonly System.Text.RegularExpressions.Regex NullNumber =
        new System.Text.RegularExpressions.Regex("\"(phobiaGrade|allowedStartLevel)\"\\s*:\\s*null");

    private static T Parse<T>(string body) where T : class
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        // JsonUtility no admite null en campos numéricos (p. ej. "phobiaGrade": null): se leen como 0.
        body = NullNumber.Replace(body, "\"$1\":0");
        try { return JsonUtility.FromJson<T>(body); }
        catch (ArgumentException) { return null; }
    }

    private static T LoadJson<T>(string fileName) where T : class
    {
        try
        {
            string path = Path.Combine(Application.persistentDataPath, fileName);
            return File.Exists(path) ? Parse<T>(File.ReadAllText(path)) : null;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"NeuroPhobia: no se pudo leer {fileName}. {ex.Message}");
            return null;
        }
    }

    private static void SaveJson(string fileName, object value)
    {
        try { File.WriteAllText(Path.Combine(Application.persistentDataPath, fileName), JsonUtility.ToJson(value)); }
        catch (Exception ex) { Debug.LogWarning($"NeuroPhobia: no se pudo guardar {fileName}. {ex.Message}"); }
    }

    private static void DeleteFile(string fileName)
    {
        try
        {
            string path = Path.Combine(Application.persistentDataPath, fileName);
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception ex) { Debug.LogWarning($"NeuroPhobia: no se pudo borrar {fileName}. {ex.Message}"); }
    }

    // ───────────── tipos de respuesta (JsonUtility ignora los campos que no conoce) ─────────────

    [Serializable] private class DeviceCredentials { public string deviceId; public string deviceToken; }
    [Serializable] private class ProblemDto { public string code; public string detail; public int status; }
    [Serializable] private class SessionRefDto { public string sessionId; public string status; }
    [Serializable] private class FeedbackDto { public string status; public string recommendation; public string clinicalStatus; }
    [Serializable] private class RecordResultDto { public string status; public SessionRefDto session; public FeedbackDto feedback; }
}

/// <summary>Respuesta de GET /api/v1/device/context (los campos que usa la app).</summary>
[Serializable]
public class DeviceContextDto
{
    public int allowedStartLevel = 1;
    // 0 = sin grado guardado (el backend envía null).
    public int phobiaGrade;
    public string phobiaGradeUpdatedAt;
}
