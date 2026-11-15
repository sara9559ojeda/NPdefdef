using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Level5HUD : MonoBehaviour
{
    public TMP_Text instructionText;
    public TMP_Text taskProgressText;
    public TMP_Text completionText;
    public Button finishButton;

    public const string Title = "NIVEL 5 · ANTES DE DORMIR";
    public const string Story = "Es de noche. Prepárate para dormir:";

    // Each task of the bedtime routine: room and what to do, in the order of the story (TaskManager.StoryOrder).
    private static readonly Dictionary<string, string> StoryLines = new Dictionary<string, string>
    {
        { "TASK_FRIDGE", "Cocina: abre la nevera y toma agua" },
        { "TASK_DOOR", "Cuarto: abre la puerta" },
        { "TASK_BATH", "Baño: abre la puerta y lávate los dientes" },
        { "TASK_WINDOW", "Cuarto: abre la ventana para ventilar" },
        { "TASK_LAMP", "Sala: apaga la lámpara para dormir" }
    };

    /// <summary>Text of one task of the routine.</summary>
    public static string StoryLine(string taskId) => StoryLines.TryGetValue(taskId, out string line) ? line : taskId;

    private void Start()
    {
        HideSceneCanvas();
        TaskManager manager = FindFirstObjectByType<TaskManager>();
        if (manager != null) manager.RefreshHUD();
    }

    // The task list saved in the scene is replaced by the shared wrist HUD (LevelHud): its canvas is not drawn.
    private void HideSceneCanvas()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas != null) canvas.enabled = false;
        if (completionText != null) completionText.gameObject.SetActive(false);
    }

    public void SetTasks(HashSet<string> done, int total)
    {
        // The next pending task of the story is marked with an arrow; the order is suggested, not enforced.
        var lines = new System.Text.StringBuilder();
        bool nextMarked = false;
        foreach (string id in TaskManager.StoryOrder)
        {
            string mark;
            if (done.Contains(id)) mark = "<color=#7BD88F>[x]</color> ";
            else if (!nextMarked) { mark = "<b>></b> "; nextMarked = true; }
            else mark = "[ ] ";
            if (lines.Length > 0) lines.Append('\n');
            lines.Append(mark).Append(StoryLine(id));
        }
        string tasks = lines.ToString();

        if (taskProgressText != null) taskProgressText.text = tasks;
        LevelHud.SetMission(
            Title,
            "<size=85%>" + Story + "</size>\n<size=80%>" + tasks + "</size>\n" + HowTo,
            total > 0 ? done.Count / (float)total : 0f);
    }

    // How to interact: what can be opened or pressed lights up when pointed at (Level5HoverHighlight).
    public const string HowTo = "<size=70%>Lo que se puede abrir se ilumina al apuntarlo.</size>";

    public void ShowAvoidanceLimit(int maximum)
    {
        string message = $"Tareas completas. Se superó el límite de intentos de alejamiento ({maximum}). Puedes finalizar desde Pausa.";
        if (completionText != null) completionText.text = message;
        LevelHud.SetMission(Title, message);
    }

    // Compatibilidad con el componente doméstico anterior, que se desactiva en la escena.
    public void SetTaskProgress(int completed, int total) { }
    public void ShowCompleted(bool show) { }
    public void FinishSession() { if (SessionManager.Instance != null) SessionManager.Instance.EndSession(); }
}
