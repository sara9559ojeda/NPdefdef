using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Tarea doméstica del Nivel5. La tarea se cumple SOLO por el estado físico del objeto, que la persona cambia con
/// el XR Interaction Toolkit:
/// - Puerta y nevera: la hoja (con bisagra, <see cref="Level5HingeHandle"/>) se agarra y se gira hasta abrirla
///   al menos <see cref="openThreshold"/> de su recorrido. Empiezan cerradas.
/// - Lámpara: se pulsa su interruptor (<see cref="Level5LampSwitch"/>) y la luz queda apagada de verdad.
/// - Ventana: el cristal (<see cref="Level5SlideHandle"/>) se desliza hacia arriba hasta abrirla.
/// Acercarse, mirar, atravesar un trigger o apretar el gatillo apuntando sin mover nada no completa ninguna tarea.
/// </summary>
public class Level5TaskInteraction : MonoBehaviour
{
    public string taskId;
    public TaskManager taskManager;
    [Tooltip("Hoja que gira (puerta, nevera).")]
    public Transform movingPart;
    [Tooltip("Punto de la bisagra.")]
    public Transform hingePivot;
    [Tooltip("Ángulo de apertura en grados (el sentido se elige para no chocar con el mueble).")]
    public Vector3 openEuler = new Vector3(0, 70, 0);
    public Transform secondMovingPart;
    public Transform secondHingePivot;
    public Vector3 secondOpenEuler = new Vector3(0, -80, 0);
    [Tooltip("Heredado: ya no se usa (la hoja la mueve la mano).")]
    public float animationSpeed = 120f;
    public Light controlledLight;
    [Tooltip("Heredado: ya no se usa. El collider de la hoja gira con ella: cerrada bloquea, abierta deja paso.")]
    public bool clearPathWhenOpen = false;
    [Tooltip("Colliders que tapaban el hueco (p. ej. un marco macizo): se desactivan al montar la puerta.")]
    public Collider[] alsoClearWhenOpen = new Collider[0];
    [Tooltip("Fracción del recorrido (0–1) que cuenta como «abierta».")]
    [Range(0.1f, 1f)] public float openThreshold = 0.6f;

    [HideInInspector] public Level5HingeHandle hinge;
    [HideInInspector] public Level5HingeHandle secondHinge;
    [HideInInspector] public Level5SlideHandle slide;
    [HideInInspector] public Level5LampSwitch lampSwitch;

    private bool activated;
    private bool configured;

    public bool IsActivated => activated;

    private void Start()
    {
        Configure();
    }

    /// <summary>Monta la interacción física de la tarea (idempotente).</summary>
    public void Configure()
    {
        if (configured) return;
        configured = true;
        Level5InteractiveHome.Build();
        if (taskManager == null) taskManager = FindFirstObjectByType<TaskManager>();

        // El objeto raíz ya no es un botón gigante: su XRSimpleInteractable (que se activaba con solo tocarlo con el
        // mando o apuntarlo y apretar el gatillo) y su caja de colisión se retiran.
        XRSimpleInteractable rootInteractable = GetComponent<XRSimpleInteractable>();
        bool rootIsPiece = slide != null && slide.transform == transform;
        if (rootInteractable != null && !rootIsPiece && controlledLight == null) Destroy(rootInteractable);
        if (movingPart != null)
        {
            foreach (Collider own in GetComponents<Collider>()) own.enabled = false;
            foreach (Collider extra in alsoClearWhenOpen) if (extra != null) extra.enabled = false;
            hinge = Level5InteractiveHome.MakeHinge(movingPart, hingePivot, Mathf.Abs(openEuler.y), closeFirst: secondMovingPart != null || movingPart.name != "door_body");
            if (secondMovingPart != null)
                secondHinge = Level5InteractiveHome.MakeHinge(secondMovingPart, secondHingePivot, Mathf.Abs(secondOpenEuler.y), closeFirst: true);
        }

        if (controlledLight != null && lampSwitch == null)
        {
            if (rootInteractable != null) Destroy(rootInteractable);
            foreach (Collider own in GetComponents<Collider>()) own.enabled = false;
            lampSwitch = Level5LampSwitch.Create(transform, controlledLight);
        }
    }

    private void Update()
    {
        if (!configured) Configure();
        if (activated || GameStateManager.CurrentState != GameState.PLAYING) return;
        if (PhysicallyDone()) Complete();
    }

    /// <summary>¿El objeto está en el estado que pide la tarea?</summary>
    public bool PhysicallyDone()
    {
        if (hinge != null && hinge.OpenFraction >= openThreshold) return true;
        if (secondHinge != null && secondHinge.OpenFraction >= openThreshold) return true;
        if (slide != null && slide.OpenFraction >= openThreshold) return true;
        if (lampSwitch != null && !lampSwitch.IsOn) return true;
        return false;
    }

    /// <summary>
    /// Lleva el objeto a su estado final como lo haría la mano (abre la hoja, desliza el cristal, apaga la luz).
    /// Solo para pruebas y herramientas: el juego nunca lo llama por proximidad, mirada ni gatillo.
    /// </summary>
    public void Activate()
    {
        Configure();
        if (hinge != null) hinge.SetAngle(hinge.openAngle);
        else if (slide != null) slide.SetOffset(slide.travel);
        else if (lampSwitch != null) lampSwitch.SetOn(false);
        if (GameStateManager.CurrentState == GameState.PLAYING && PhysicallyDone()) Complete();
    }

    private void Complete()
    {
        if (activated) return;
        activated = true;
        if (taskManager != null) taskManager.CompleteTask(taskId);
    }
}
