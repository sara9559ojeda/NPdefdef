using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void LoadLevel(int level)
    {
        level = Mathf.Clamp(level, 1, 5);
        if (SessionManager.Instance != null) SessionManager.Instance.CurrentLevel = level;
        SceneManager.LoadScene($"Nivel{level}");
    }

    public void UnlockNextLevel()
    {
        int current = SessionManager.Instance != null ? SessionManager.Instance.CurrentLevel : ExtractCurrentLevel();
        int next = Mathf.Clamp(current + 1, 1, 5);
        LoadLevel(next);
    }

    public void OnLevelComplete(int level, LevelMetrics metrics)
    {
        if (SessionManager.Instance != null) SessionManager.Instance.AddLevelMetrics(metrics);
        if (level >= 5) { GameStateManager.SetState(GameState.SESSION_END); if (SessionManager.Instance != null) SessionManager.Instance.EndSession(); return; }
        GlobalSessionUI.CompleteLevel(level);
    }

    private int ExtractCurrentLevel()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if (sceneName.StartsWith("Nivel") && int.TryParse(sceneName.Replace("Nivel", ""), out int level)) return level;
        return 1;
    }
}
