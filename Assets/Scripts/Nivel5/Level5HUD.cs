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
        string tasks = Mark(done, "TASK_LAMP", "Apagar lámpara") + "\n" +
            Mark(done, "TASK_DOOR", "Abrir puerta") + "\n" +
            Mark(done, "TASK_FRIDGE", "Abrir nevera") + "\n" +
            Mark(done, "TASK_WINDOW", "Abrir ventana");

        if (taskProgressText != null) taskProgressText.text = tasks;
        LevelHud.SetMission(
            "NIVEL 5 · TAREAS EN CASA",
            tasks + "\n" + HowTo,
            total > 0 ? done.Count / (float)total : 0f);
    }

    // How to interact: what can be opened or pressed lights up when pointed at (Level5HoverHighlight).
    public const string HowTo = "<size=75%>Lo que se puede abrir se ilumina al apuntarlo.</size>";

    private static string Mark(HashSet<string> done, string id, string name) => (done.Contains(id) ? "[x] " : "[ ] ") + name;

    public void ShowAvoidanceLimit(int maximum)
    {
        string message = $"Tareas completas. Se superó el límite de intentos de alejamiento ({maximum}). Puedes finalizar desde Pausa.";
        if (completionText != null) completionText.text = message;
        LevelHud.SetMission("NIVEL 5 · TAREAS EN CASA", message);
    }

    // Compatibilidad con el componente doméstico anterior, que se desactiva en la escena.
    public void SetTaskProgress(int completed, int total) { }
    public void ShowCompleted(bool show) { }
    public void FinishSession() { if (SessionManager.Instance != null) SessionManager.Instance.EndSession(); }
}
