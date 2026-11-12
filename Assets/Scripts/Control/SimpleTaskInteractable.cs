using UnityEngine;

/// <summary>
/// Marcador heredado de las tareas del Nivel5 (TASK_DOOR, TASK_LAMP…). Antes completaba la tarea al entrar la
/// cámara en su trigger o al hacer clic; eso permitía cumplir una tarea solo con acercarse. Ya no reacciona a la
/// proximidad ni al ratón: las tareas las cumple <see cref="Level5TaskInteraction"/> por el estado físico del objeto.
/// <see cref="Complete"/> queda para herramientas y pruebas.
/// </summary>
public class SimpleTaskInteractable : MonoBehaviour
{
    [SerializeField] private string taskId = "TASK";
    private bool completed;

    public string TaskId => taskId;

    public void Complete()
    {
        if (completed) return;
        completed = true;
        TaskManager manager = FindFirstObjectByType<TaskManager>();
        if (manager != null) manager.CompleteTask(taskId);
        gameObject.name = taskId + "_COMPLETED";
    }
}
