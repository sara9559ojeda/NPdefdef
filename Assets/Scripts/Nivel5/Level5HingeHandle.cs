using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Hoja con bisagra (puerta, nevera, armario) que se agarra con el mando y se gira como una palanca.
/// Mientras el mando la sujeta (grip o gatillo, rayo o mano), la hoja sigue el ángulo de la mano alrededor del eje
/// vertical de la bisagra, dentro de los límites [0, openAngle]. Empieza cerrada. No usa física: no la empujan
/// el jugador ni las arañas, y su collider gira con ella (cerrada bloquea el paso, abierta lo deja libre).
/// </summary>
[RequireComponent(typeof(XRSimpleInteractable))]
public sealed class Level5HingeHandle : MonoBehaviour
{
    [Tooltip("Hoja que gira (por defecto, este objeto).")]
    public Transform leaf;
    [Tooltip("Punto de la bisagra (eje vertical que pasa por él).")]
    public Transform pivot;
    [Tooltip("Ángulo máximo de apertura en grados (con signo: indica el sentido).")]
    public float openAngle = 85f;

    private XRSimpleInteractable interactable;
    private IXRSelectInteractor holder;
    private float reach;
    private float angle;
    private float grabOffset;
    private Vector3 closedLocalPosition;
    private Quaternion closedLocalRotation;
    private Vector3 hingeLocalPoint;
    private bool initialized;

    public float Angle => angle;
    public float OpenFraction => Mathf.Approximately(openAngle, 0f) ? 0f : Mathf.Clamp01(angle / openAngle);
    public bool IsHeld => holder != null;

    private void Awake()
    {
        Initialize();
    }

    public void Initialize()
    {
        if (initialized) return;
        initialized = true;
        if (leaf == null) leaf = transform;
        closedLocalPosition = leaf.localPosition;
        closedLocalRotation = leaf.localRotation;
        // Punto de la bisagra guardado en el espacio del padre (si el padre se mueve, la bisagra lo acompaña).
        Vector3 hingeWorld = pivot != null ? pivot.position : leaf.position;
        hingeLocalPoint = leaf.parent != null ? leaf.parent.InverseTransformPoint(hingeWorld) : hingeWorld;
        interactable = GetComponent<XRSimpleInteractable>();
        interactable.selectEntered.AddListener(OnGrab);
        interactable.selectExited.AddListener(OnRelease);
    }

    private void OnDestroy()
    {
        if (interactable == null) return;
        interactable.selectEntered.RemoveListener(OnGrab);
        interactable.selectExited.RemoveListener(OnRelease);
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        holder = args.interactorObject;
        reach = Level5HandPoint.ReachAtGrab(holder.GetAttachTransform(interactable), leaf);
        grabOffset = angle - HandAngle(Level5HandPoint.Point(holder.GetAttachTransform(interactable), reach));
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        if (args.interactorObject == holder) holder = null;
    }

    private void Update()
    {
        if (holder == null || GameStateManager.CurrentState != GameState.PLAYING) return;
        SetAngle(HandAngle(Level5HandPoint.Point(holder.GetAttachTransform(interactable), reach)) + grabOffset);
    }

    /// <summary>Gira la hoja al ángulo dado (recortado a los límites), alrededor del eje de la bisagra.</summary>
    public void SetAngle(float value)
    {
        Initialize();
        float min = Mathf.Min(0f, openAngle);
        float max = Mathf.Max(0f, openAngle);
        angle = Mathf.Clamp(value, min, max);

        // Se parte siempre de la pose cerrada para no acumular errores.
        // Siempre alrededor del eje VERTICAL DEL MUNDO que pasa por la bisagra, el mismo que se usa para elegir el
        // sentido de apertura. (Girar sobre el eje local invertía el sentido en muebles girados o reflejados.)
        leaf.localPosition = closedLocalPosition;
        leaf.localRotation = closedLocalRotation;
        Vector3 hinge = leaf.parent != null ? leaf.parent.TransformPoint(hingeLocalPoint) : hingeLocalPoint;
        leaf.RotateAround(hinge, Vector3.up, angle);
    }

    // Ángulo de la mano alrededor del eje vertical de la bisagra (plano horizontal).
    private float HandAngle(Vector3 hand)
    {
        Vector3 center = leaf.parent != null ? leaf.parent.TransformPoint(hingeLocalPoint) : hingeLocalPoint;
        Vector3 flat = hand - center;
        flat.y = 0f;
        if (flat.sqrMagnitude < 0.0001f) return angle;
        return Vector3.SignedAngle(Vector3.forward, flat, Vector3.up);
    }
}
