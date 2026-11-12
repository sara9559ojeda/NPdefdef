using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Pieza que se desliza a lo largo de un eje (cajón, cortina, hoja de ventana corredera) cuando el mando la agarra.
/// Mientras se sujeta, sigue la proyección de la mano sobre el eje, entre 0 (cerrada) y <see cref="travel"/> metros.
/// Empieza cerrada. Sin física: no la empuja nadie.
/// </summary>
[RequireComponent(typeof(XRSimpleInteractable))]
public sealed class Level5SlideHandle : MonoBehaviour
{
    [Tooltip("Pieza que se mueve (por defecto, este objeto).")]
    public Transform part;
    [Tooltip("Dirección de apertura en el espacio del padre de la pieza.")]
    public Vector3 localAxis = Vector3.forward;
    [Tooltip("Recorrido máximo en metros.")]
    public float travel = 0.35f;

    private XRSimpleInteractable interactable;
    private IXRSelectInteractor holder;
    private float reach;
    private float offset;
    private float grabOffset;
    private Vector3 closedLocalPosition;
    private bool initialized;

    public float Offset => offset;
    public float OpenFraction => travel <= 0f ? 0f : Mathf.Clamp01(offset / travel);

    private void Awake()
    {
        Initialize();
    }

    public void Initialize()
    {
        if (initialized) return;
        initialized = true;
        if (part == null) part = transform;
        closedLocalPosition = part.localPosition;
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
        reach = Level5HandPoint.ReachAtGrab(holder.GetAttachTransform(interactable), part);
        grabOffset = offset - HandOffset(Level5HandPoint.Point(holder.GetAttachTransform(interactable), reach));
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        if (args.interactorObject == holder) holder = null;
    }

    private void Update()
    {
        if (holder == null || GameStateManager.CurrentState != GameState.PLAYING) return;
        SetOffset(HandOffset(Level5HandPoint.Point(holder.GetAttachTransform(interactable), reach)) + grabOffset);
    }

    public void SetOffset(float value)
    {
        Initialize();
        offset = Mathf.Clamp(value, 0f, Mathf.Max(0f, travel));
        part.localPosition = closedLocalPosition + localAxis.normalized * offset;
    }

    private Vector3 WorldAxis()
    {
        Transform parent = part.parent;
        Vector3 axis = parent != null ? parent.TransformDirection(localAxis.normalized) : localAxis.normalized;
        return axis.normalized;
    }

    private float HandOffset(Vector3 hand)
    {
        Transform parent = part.parent;
        Vector3 closedWorld = parent != null ? parent.TransformPoint(closedLocalPosition) : closedLocalPosition;
        float worldDistance = Vector3.Dot(hand - closedWorld, WorldAxis());
        float scale = parent != null ? parent.TransformVector(localAxis.normalized).magnitude : 1f;
        return worldDistance / Mathf.Max(0.0001f, scale);
    }
}
