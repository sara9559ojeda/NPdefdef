using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class Level5TaskInteraction : MonoBehaviour
{
    public string taskId;
    public TaskManager taskManager;
    public Transform movingPart;
    public Transform hingePivot;
    public Vector3 openEuler = new Vector3(0, 70, 0);
    public Transform secondMovingPart;
    public Transform secondHingePivot;
    public Vector3 secondOpenEuler = new Vector3(0, -80, 0);
    public float animationSpeed = 120f;
    public Light controlledLight;
    private Quaternion targetRotation;
    private Quaternion secondTargetRotation;
    private float currentHingeAngle;
    private float currentSecondHingeAngle;
    [Tooltip("Al abrirse, el collider de interacción de este objeto deja de bloquear el paso (se activa en la puerta).")]
    public bool clearPathWhenOpen = false;
    [Tooltip("Otros colliders que también se desactivan al abrir (p. ej. el del marco, que cubre todo el hueco).")]
    public Collider[] alsoClearWhenOpen = new Collider[0];
    private bool activated;

    public bool IsActivated => activated;

    private void Awake()
    {
        XRSimpleInteractable interactable = GetComponent<XRSimpleInteractable>();
        if (interactable != null) interactable.selectEntered.AddListener(_ => Activate());
        if (movingPart != null) targetRotation = movingPart.localRotation * Quaternion.Euler(openEuler);
        if (secondMovingPart != null) secondTargetRotation = secondMovingPart.localRotation * Quaternion.Euler(secondOpenEuler);
    }

    private void Update()
    {
        if (!activated || GameStateManager.CurrentState != GameState.PLAYING) return;
        Swing(movingPart, hingePivot, targetRotation, openEuler.y, ref currentHingeAngle);
        Swing(secondMovingPart, secondHingePivot, secondTargetRotation, secondOpenEuler.y, ref currentSecondHingeAngle);
    }

    private void Swing(Transform part, Transform pivot, Quaternion target, float angle, ref float currentAngle)
    {
        if (part == null) return;
        if (pivot == null)
        {
            part.localRotation = Quaternion.RotateTowards(part.localRotation, target, animationSpeed * Time.deltaTime);
            return;
        }
        float next = Mathf.MoveTowards(currentAngle, angle, animationSpeed * Time.deltaTime);
        part.RotateAround(pivot.position, Vector3.up, next - currentAngle);
        currentAngle = next;
    }

    private void OnMouseDown() => Activate();

    public void Activate()
    {
        if (activated || GameStateManager.CurrentState != GameState.PLAYING) return;
        activated = true;
        // La puerta abierta (y la nevera) no deben bloquear el paso: se libera el collider del objeto raíz.
        if (clearPathWhenOpen && movingPart != null)
        {
            foreach (Collider own in GetComponents<Collider>()) own.enabled = false;
            // Colliders hijos que cubren el hueco, como el del marco de la puerta, que es una caja maciza.
            foreach (Collider extra in alsoClearWhenOpen) if (extra != null) extra.enabled = false;
        }
        if (controlledLight != null) controlledLight.enabled = !controlledLight.enabled;
        if (taskManager != null) taskManager.CompleteTask(taskId);
    }
}
