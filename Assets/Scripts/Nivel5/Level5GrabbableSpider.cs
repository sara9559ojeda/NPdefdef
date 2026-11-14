using System.Collections;
using System.Linq;
using NeuroPhobia.Level45;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Nivel5: a live spider can be picked up with the hand.
/// - Only with the hand next to it (grip within <see cref="GrabReach"/> perceived metres): the controller ray cannot
///   grab spiders, as it would not be realistic.
/// - Held, it stays where it was taken (dynamic attach), follows the hand and kicks its legs.
/// - Released, it falls to the floor below, rights itself and walks on from there (in the room where it landed).
/// While it is held its patrol is off, so the spider rules of Level5CompanionSpiders do not move it.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public sealed class Level5GrabbableSpider : MonoBehaviour, IXRSelectFilter
{
    private const float GrabReach = 0.2f;
    private const float HeldLegSpeed = 1.6f;
    private const float Gravity = 9.81f;
    private const float MaxDropSearch = 3f;

    private NPNaturalSpiderPatrol patrol;
    private XRGrabInteractable grab;
    private Animator[] animators;
    private Coroutine falling;

    /// <summary>True while a hand holds the spider.</summary>
    public bool IsHeld { get; private set; }

    /// <summary>Called when the spider lands after being released (new position on the floor).</summary>
    public System.Action<Level5GrabbableSpider, Vector3> Landed;

    /// <summary>Called when the spider is picked up.</summary>
    public System.Action<Level5GrabbableSpider> Grabbed;

    public NPNaturalSpiderPatrol Patrol => patrol;

    /// <summary>Makes a live spider grabbable (colliders, kinematic body, grab interactable).</summary>
    public static Level5GrabbableSpider Attach(NPNaturalSpiderPatrol spider)
    {
        GameObject go = spider.gameObject;
        if (go.GetComponentInChildren<Collider>() == null)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);
                BoxCollider box = go.AddComponent<BoxCollider>();
                box.center = go.transform.InverseTransformPoint(bounds.center);
                Vector3 scale = go.transform.lossyScale;
                box.size = new Vector3(bounds.size.x / Mathf.Max(0.0001f, scale.x), bounds.size.y / Mathf.Max(0.0001f, scale.y), bounds.size.z / Mathf.Max(0.0001f, scale.z));
            }
        }

        Rigidbody body = go.GetComponent<Rigidbody>();
        if (body == null) body = go.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;

        Level5GrabbableSpider grabbable = go.AddComponent<Level5GrabbableSpider>();
        grabbable.patrol = spider;
        return grabbable;
    }

    private void Awake()
    {
        if (patrol == null) patrol = GetComponent<NPNaturalSpiderPatrol>();
        animators = GetComponentsInChildren<Animator>(true);

        grab = gameObject.AddComponent<XRGrabInteractable>();
        grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
        grab.useDynamicAttach = true;
        grab.throwOnDetach = false;
        grab.trackPosition = true;
        grab.trackRotation = true;
        grab.selectFilters.Add(this);
        grab.selectEntered.AddListener(OnGrab);
        grab.selectExited.AddListener(OnRelease);
    }

    private void OnDestroy()
    {
        if (grab == null) return;
        grab.selectEntered.RemoveListener(OnGrab);
        grab.selectExited.RemoveListener(OnRelease);
    }

    // ------------------------------------------------------------------ only with the hand next to it

    public bool canProcess => isActiveAndEnabled;

    public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
    {
        if (GameStateManager.CurrentState != GameState.PLAYING) return false;
        Transform hand = interactor.GetAttachTransform(interactable);
        if (hand == null) return false;
        Unity.XR.CoreUtils.XROrigin origin = hand.GetComponentInParent<Unity.XR.CoreUtils.XROrigin>();
        float scale = origin != null ? Mathf.Max(0.01f, origin.transform.lossyScale.x) : 1f;
        Vector3 handPosition = hand.position;
        foreach (Collider c in grab.colliders)
        {
            if (c == null || !c.enabled) continue;
            if (Vector3.Distance(handPosition, c.ClosestPoint(handPosition)) <= GrabReach * scale) return true;
        }
        return false;
    }

    // ------------------------------------------------------------------ held and released

    private void OnGrab(SelectEnterEventArgs args)
    {
        if (falling != null) { StopCoroutine(falling); falling = null; }
        IsHeld = true;
        if (patrol != null) patrol.enabled = false;
        SetLegs(HeldLegSpeed);
        Grabbed?.Invoke(this);
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        if (grab.isSelected) return;   // still held by the other hand
        falling = StartCoroutine(FallAndWalkOn());
    }

    private IEnumerator FallAndWalkOn()
    {
        float floor = FloorBelow(transform.position);
        float speed = 0f;
        Quaternion upright = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        while (transform.position.y > floor + 0.001f)
        {
            speed += Gravity * Time.deltaTime;
            Vector3 position = transform.position;
            position.y = Mathf.Max(floor, position.y - speed * Time.deltaTime);
            transform.position = position;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, upright, 540f * Time.deltaTime);
            yield return null;
        }
        transform.rotation = upright;

        IsHeld = false;
        falling = null;
        if (patrol != null) patrol.enabled = true;
        Landed?.Invoke(this, transform.position);
    }

    // The room floor under the point (the apartment floors are named "Piso_..."); otherwise the lowest surface.
    private float FloorBelow(Vector3 from)
    {
        RaycastHit[] hits = Physics.RaycastAll(from + Vector3.up * 0.05f, Vector3.down, MaxDropSearch, ~0, QueryTriggerInteraction.Collide)
            .Where(h => !h.collider.transform.IsChildOf(transform) && h.normal.y > 0.7f).ToArray();
        RaycastHit[] floors = hits.Where(h => h.collider.name.StartsWith("Piso") || h.collider.GetComponentInParent<NPWalkableSurface>() != null).ToArray();
        if (floors.Length > 0) return floors.Max(h => h.point.y) + 0.015f;
        if (hits.Length > 0) return hits.Min(h => h.point.y) + 0.015f;
        return transform.position.y;
    }

    private void SetLegs(float speed)
    {
        foreach (Animator animator in animators)
        {
            if (animator == null || animator.runtimeAnimatorController == null) continue;
            animator.enabled = true;
            animator.speed = speed;
        }
    }
}
