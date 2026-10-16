using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class FastAPIClient : MonoBehaviour
{
    public const string BaseUrlPrefsKey = "NeuroPhobia.BackendUrl";
    public const string BaseUrlFileName = "backend_url.txt";

    // Valor por defecto; se anula con persistentDataPath/backend_url.txt o con PlayerPrefs (ver PARAMETROS_IA.txt).
    [SerializeField] private string baseUrl = "http://localhost:8000";
    [SerializeField] private int timeoutSeconds = 5;

    public string BaseUrl => baseUrl;

    private void Awake()
    {
        baseUrl = ResolveBaseUrl(baseUrl);
        Debug.Log($"NeuroPhobia: backend en {baseUrl}");
    }

    /// <summary>Guarda una URL en PlayerPrefs (p. ej. http://192.168.1.50:8000) y la aplica de inmediato.</summary>
    public void SetBaseUrl(string url)
    {
        string normalized = Normalize(url);
        if (normalized == null) return;
        PlayerPrefs.SetString(BaseUrlPrefsKey, normalized);
        PlayerPrefs.Save();
        baseUrl = normalized;
    }

    // Prioridad: archivo backend_url.txt > PlayerPrefs > valor serializado.
    private static string ResolveBaseUrl(string fallback)
    {
        try
        {
            string path = System.IO.Path.Combine(Application.persistentDataPath, BaseUrlFileName);
            if (System.IO.File.Exists(path))
            {
                string fromFile = Normalize(System.IO.File.ReadAllText(path));
                if (fromFile != null) return fromFile;
                Debug.LogWarning($"NeuroPhobia: {path} no contiene una URL http(s) valida; se ignora.");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"NeuroPhobia: no se pudo leer {BaseUrlFileName}. {ex.Message}");
        }

        string fromPrefs = Normalize(PlayerPrefs.GetString(BaseUrlPrefsKey, ""));
        return fromPrefs ?? fallback.TrimEnd('/');
    }

    private static string Normalize(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        url = url.Trim().TrimEnd('/');
        bool valid = System.Uri.TryCreate(url, System.UriKind.Absolute, out System.Uri uri) && (uri.Scheme == "http" || uri.Scheme == "https");
        return valid ? url : null;
    }

    public IEnumerator PostSessionStart(string sessionId, int phobiaGrade)
    {
        SessionStartPayload payload = new SessionStartPayload
        {
            sessionId = sessionId,
            phobiaGrade = phobiaGrade,
            timestamp = System.DateTime.UtcNow.ToString("o")
        };
        yield return PostJson("/api/session/start", JsonUtility.ToJson(payload));
    }

    public IEnumerator PostLevelMetrics(string sessionId, int level, LevelMetrics metrics)
    {
        LevelMetricsPayload payload = new LevelMetricsPayload { sessionId = sessionId, level = level, metrics = metrics };
        yield return PostJson("/api/session/metrics", JsonUtility.ToJson(payload));
    }

    public IEnumerator PostSessionEnd(string sessionId, SessionReport fullReport)
    {
        SessionEndPayload payload = new SessionEndPayload { sessionId = sessionId, fullReport = fullReport };
        yield return PostJson("/api/session/end", JsonUtility.ToJson(payload));
    }

    public IEnumerator GetAdaptiveCommand(string sessionId, System.Action<string> onCommand)
    {
        string url = $"{baseUrl}/api/session/command/{sessionId}";
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = timeoutSeconds;
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                onCommand?.Invoke("none");
                yield break;
            }

            string body = request.downloadHandler != null ? request.downloadHandler.text : null;
            AdaptiveCommandResponse response = null;
            if (!string.IsNullOrWhiteSpace(body))
            {
                try { response = JsonUtility.FromJson<AdaptiveCommandResponse>(body); }
                catch (System.ArgumentException) { Debug.LogWarning("NeuroPhobia: respuesta de comando no es JSON valido; se ignora."); }
            }
            onCommand?.Invoke(response == null || string.IsNullOrEmpty(response.action) ? "none" : response.action.Trim().ToLowerInvariant());
        }
    }

    private IEnumerator PostJson(string endpoint, string json)
    {
        byte[] data = Encoding.UTF8.GetBytes(json);
        using (UnityWebRequest request = new UnityWebRequest(baseUrl + endpoint, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(data);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = timeoutSeconds;

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"NeuroPhobia: backend no disponible para {endpoint}. Se continua localmente.");
            }
        }
    }

    [System.Serializable] private class SessionStartPayload { public string sessionId; public int phobiaGrade; public string timestamp; }
    [System.Serializable] private class LevelMetricsPayload { public string sessionId; public int level; public LevelMetrics metrics; }
    [System.Serializable] private class SessionEndPayload { public string sessionId; public SessionReport fullReport; }
    [System.Serializable] private class AdaptiveCommandResponse { public string action; }
}
