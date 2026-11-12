using System.Collections;
using System.Linq;
using UnityEngine;

/// <summary>
/// Patrulla segura y natural de la araña del Nivel 3.
/// Recorre en bucle una ruta circular de puntos alrededor de su sitio (antes iba y volvía entre dos puntos con giros
/// de 180° en el sitio). Gira de forma continua con un giro máximo por segundo, mira siempre hacia donde avanza,
/// frena en las curvas y acelera con suavidad; de vez en cuando se detiene un momento.
/// Ignora sus propios colliders al buscar el suelo y no sale de su zona.
/// </summary>
public class Level3SpiderPatrolV2 : MonoBehaviour
{
    public Transform pointA;
    public Transform pointB;
    public float moveSpeed = 0.13f;
    public float reducedSpeedMultiplier = 0.5f;
    public float turnSpeed = 4.5f;
    [Tooltip("Giro máximo en grados por segundo (sin giros bruscos).")]
    public float turnRate = 70f;
    [Tooltip("Aceleración (m/s²) al arrancar y frenar.")]
    public float acceleration = 0.12f;
    [Tooltip("Puntos de la ruta circular.")]
    public int routePoints = 6;
    [Tooltip("Probabilidad de detenerse un momento al llegar a un punto.")]
    [Range(0f, 1f)] public float pauseChance = 0.3f;
    public Animator animator;
    public float animatorSpeed = 0.28f;
    public Transform visualRoot;
    public float endpointPauseMin = 0.8f;
    public float endpointPauseMax = 1.8f;
    public float groundRayHeight = 1.5f;
    public float groundOffset = 0.015f;
    public LayerMask groundMask = ~0;

    [Header("Protección de ruta")]
    public float patrolHalfDistance = 1.8f;
    public float maxRouteDistanceFromSpawn = 6f;
    public float maxVerticalDeviation = 1.2f;

    private Transform target;
    private bool paused;
    private bool reduced;
    private bool waiting;
    private Vector3 lastPosition;
    private Vector3 visualBasePosition;
    private Quaternion visualBaseRotation;
    private bool hasAnimatorMotion;
    private Vector3 initialSpawn;
    private Quaternion initialRotation;
    private float stableGroundY;
    private readonly System.Collections.Generic.List<Vector3> route = new System.Collections.Generic.List<Vector3>();
    private int routeIndex;
    private float currentSpeed;

    public float CurrentSpeed => currentSpeed;
    public int RouteCount => route.Count;

    private void OnEnable()
    {
        GameStateManager.OnStateChanged += HandleState;
    }

    private void OnDisable()
    {
        GameStateManager.OnStateChanged -= HandleState;
    }

    private void Start()
    {
        initialSpawn = transform.position;
        initialRotation = transform.rotation;
        stableGroundY = initialSpawn.y;

        PreparePhysics();
        PrepareVisuals();
        ValidateOrRecenterRoute();

        target = pointB != null ? pointB : pointA;
        lastPosition = transform.position;

        SnapToGroundSafe();
        BuildRoute();
    }

    // Ruta circular alrededor del sitio de la araña; se descartan los puntos dentro de muebles o paredes.
    private void BuildRoute()
    {
        route.Clear();
        float radius = Mathf.Max(0.4f, patrolHalfDistance);
        float start = Random.Range(0f, 360f);
        float direction = Random.value < 0.5f ? 1f : -1f;
        int count = Mathf.Max(4, routePoints);
        for (int i = 0; i < count; i++)
        {
            float angle = (start + direction * i * 360f / count) * Mathf.Deg2Rad;
            float r = radius * Random.Range(0.8f, 1.15f);
            Vector3 point = initialSpawn + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * r;
            if (Physics.CheckSphere(point + Vector3.up * 0.35f, 0.25f, groundMask, QueryTriggerInteraction.Ignore) &&
                !OnlySelfAt(point + Vector3.up * 0.35f, 0.25f)) continue;
            route.Add(point);
        }
        if (route.Count < 2 && pointA != null && pointB != null)
        {
            route.Clear();
            route.Add(pointA.position);
            route.Add(pointB.position);
        }
        routeIndex = 0;
    }

    private bool OnlySelfAt(Vector3 center, float radius)
    {
        foreach (Collider c in Physics.OverlapSphere(center, radius, groundMask, QueryTriggerInteraction.Ignore))
        {
            if (c.transform == transform || c.transform.IsChildOf(transform)) continue;
            if (c.GetComponentInParent<Level3SpiderPatrolV2>() != null) continue;
            return false;
        }
        return true;
    }

    private void Update()
    {
        EnsureVisible();

        if (RoutePositionIsInvalid())
        {
            ResetToSafeRoutePosition();
        }

        if (paused || waiting || route.Count < 2)
        {
            currentSpeed = 0f;
            SetAnimatorMoving(false, 0f);
            ApplyFallbackLegMotion(false, 0f);
            return;
        }

        float cruise = moveSpeed * (reduced ? reducedSpeedMultiplier : 1f);
        Vector3 destination = route[routeIndex];
        Vector3 flatDirection = destination - transform.position;
        flatDirection.y = 0f;
        float distance = flatDirection.magnitude;

        if (distance <= 0.2f)
        {
            routeIndex = (routeIndex + 1) % route.Count;
            if (Random.value < pauseChance)
            {
                StartCoroutine(WaitAtEndpoint(Random.Range(endpointPauseMin, endpointPauseMax)));
            }
            return;
        }

        // Giro continuo y limitado; la velocidad baja en las curvas y nunca se desliza de lado: avanza
        // siempre en la dirección a la que mira.
        Quaternion desiredRotation = Quaternion.LookRotation(flatDirection.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, desiredRotation, turnRate * Time.deltaTime);
        float misalignment = Quaternion.Angle(transform.rotation, desiredRotation);
        float targetSpeed = cruise * Mathf.Clamp01(1f - misalignment / 75f);
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * Time.deltaTime);

        Vector3 forward = transform.forward;
        forward.y = 0f;
        Vector3 next = transform.position + forward.normalized * Mathf.Min(currentSpeed * Time.deltaTime, distance);
        next.y = transform.position.y;
        transform.position = next;
        SnapToGroundSafe();

        float actualSpeed =
            Vector3.Distance(lastPosition, transform.position) /
            Mathf.Max(Time.deltaTime, 0.0001f);

        lastPosition = transform.position;
        bool moving = actualSpeed > 0.005f;

        SetAnimatorMoving(
            moving,
            actualSpeed / Mathf.Max(moveSpeed, 0.001f));

        ApplyFallbackLegMotion(moving, actualSpeed);
    }

    private void LateUpdate()
    {
        EnsureVisible();
        CancelAnimationTurnAndDrift();
    }

    // Igual que NPNaturalSpiderPatrol (Nivel5): el clip de caminar hace girar el cuerpo unos ±35° y lo desplaza.
    // Se corrige después de la animación para que la cabeza apunte siempre hacia donde avanza la araña
    // y el cuerpo siga al punto lógico (si no, parece que camina de lado o se desliza).
    private Transform headLeftBone;
    private Transform headRightBone;
    private Transform abdomenBone;
    private Transform animationRootBone;
    private float appliedYaw;
    private bool bonesSearched;

    private void CancelAnimationTurnAndDrift()
    {
        if (visualRoot == null || visualRoot.parent != transform || !hasAnimatorMotion) return;
        if (!bonesSearched)
        {
            bonesSearched = true;
            Transform[] bones = visualRoot.GetComponentsInChildren<Transform>(true);
            headLeftBone = bones.FirstOrDefault(b => b.name == "Fang.L");
            headRightBone = bones.FirstOrDefault(b => b.name == "Fang.R");
            abdomenBone = bones.FirstOrDefault(b => b.name == "Back");
            animationRootBone = bones.FirstOrDefault(b => b.name == "Root");
        }

        if (headLeftBone != null && abdomenBone != null)
        {
            Vector3 tip = headRightBone != null ? (headLeftBone.position + headRightBone.position) * 0.5f : headLeftBone.position;
            Vector3 axis = transform.InverseTransformDirection(tip - abdomenBone.position);
            axis.y = 0f;
            if (axis.sqrMagnitude > 1e-6f)
            {
                float measuredYaw = Mathf.Atan2(axis.x, axis.z) * Mathf.Rad2Deg;
                float rawYaw = measuredYaw - appliedYaw;
                appliedYaw = -rawYaw;
                visualRoot.localRotation = Quaternion.Euler(0f, appliedYaw, 0f) * visualBaseRotation;
            }
        }

        if (animationRootBone != null)
        {
            Vector3 bodyOffset = transform.InverseTransformPoint(animationRootBone.position);
            bodyOffset.y = 0f;
            Vector3 position = visualRoot.localPosition - bodyOffset;
            position.y = visualRoot.localPosition.y;
            visualRoot.localPosition = position;
        }
    }

    private void PreparePhysics()
    {
        Rigidbody body = GetComponent<Rigidbody>();
        if (body != null)
        {
            body.isKinematic = true;
            body.useGravity = false;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
    }

    private void PrepareVisuals()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }

        if (visualRoot == null)
        {
            Transform named = transform.Find("Visual_WolfSpider");
            visualRoot = named != null
                ? named
                : (transform.childCount > 0 ? transform.GetChild(0) : null);
        }

        if (visualRoot != null)
        {
            visualBasePosition = visualRoot.localPosition;
            visualBaseRotation = visualRoot.localRotation;
        }

        if (animator != null)
        {
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            hasAnimatorMotion = animator.runtimeAnimatorController != null;
            animator.enabled = hasAnimatorMotion;
            animator.speed = hasAnimatorMotion ? animatorSpeed : 0f;
        }

        EnsureVisible();
    }

    private void ValidateOrRecenterRoute()
    {
        bool missingRoute = pointA == null || pointB == null;

        bool routeTooFar = !missingRoute &&
            (Vector3.Distance(pointA.position, initialSpawn) > maxRouteDistanceFromSpawn ||
             Vector3.Distance(pointB.position, initialSpawn) > maxRouteDistanceFromSpawn);

        if (!missingRoute && !routeTooFar)
        {
            return;
        }

        Transform routeParent = transform.parent != null
            ? transform.parent
            : null;

        if (pointA == null)
        {
            GameObject pointObject = new GameObject("Patrol_A");
            pointA = pointObject.transform;
            pointA.SetParent(routeParent, true);
        }

        if (pointB == null)
        {
            GameObject pointObject = new GameObject("Patrol_B");
            pointB = pointObject.transform;
            pointB.SetParent(routeParent, true);
        }

        Vector3 routeAxis = transform.right;
        routeAxis.y = 0f;
        if (routeAxis.sqrMagnitude < 0.001f)
        {
            routeAxis = Vector3.right;
        }
        routeAxis.Normalize();

        pointA.position = initialSpawn - routeAxis * patrolHalfDistance;
        pointB.position = initialSpawn + routeAxis * patrolHalfDistance;
    }

    private void SnapToGroundSafe()
    {
        Vector3 origin = transform.position + Vector3.up * groundRayHeight;

        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            Vector3.down,
            groundRayHeight * 3f,
            groundMask,
            QueryTriggerInteraction.Ignore);

        foreach (RaycastHit hit in hits.OrderBy(candidate => candidate.distance))
        {
            if (hit.collider == null)
            {
                continue;
            }

            Transform hitTransform = hit.collider.transform;

            if (hitTransform == transform ||
                hitTransform.IsChildOf(transform))
            {
                continue;
            }

            if (hit.normal.y < 0.20f)
            {
                continue;
            }

            Vector3 position = transform.position;
            position.y = hit.point.y + groundOffset;
            transform.position = position;
            stableGroundY = position.y;
            return;
        }

        Vector3 fallback = transform.position;
        fallback.y = stableGroundY;
        transform.position = fallback;
    }

    private bool RoutePositionIsInvalid()
    {
        if (float.IsNaN(transform.position.x) ||
            float.IsNaN(transform.position.y) ||
            float.IsNaN(transform.position.z))
        {
            return true;
        }

        Vector3 routeCenter = initialSpawn;
        Vector3 flatOffset = transform.position - routeCenter;
        flatOffset.y = 0f;

        if (flatOffset.magnitude > maxRouteDistanceFromSpawn)
        {
            return true;
        }

        return Mathf.Abs(transform.position.y - stableGroundY) >
               maxVerticalDeviation;
    }

    private Vector3 GetRouteCenter()
    {
        if (pointA != null && pointB != null)
        {
            return (pointA.position + pointB.position) * 0.5f;
        }

        return initialSpawn;
    }

    private void ResetToSafeRoutePosition()
    {
        Vector3 resetPosition = initialSpawn;
        resetPosition.y = stableGroundY;

        transform.SetPositionAndRotation(
            resetPosition,
            initialRotation);

        lastPosition = resetPosition;
        target = pointB != null ? pointB : pointA;
    }

    private void EnsureVisible()
    {
        foreach (Renderer rendererComponent in
                 GetComponentsInChildren<Renderer>(true))
        {
            rendererComponent.enabled = true;
        }
    }

    private void SetAnimatorMoving(bool moving, float speedRatio)
    {
        if (!hasAnimatorMotion || animator == null)
        {
            return;
        }

        animator.speed = moving
            ? animatorSpeed *
              Mathf.Clamp(speedRatio, 0.55f, 1.25f) *
              (reduced ? reducedSpeedMultiplier : 1f)
            : 0f;
    }

    private void ApplyFallbackLegMotion(bool moving, float actualSpeed)
    {
        if (hasAnimatorMotion || visualRoot == null)
        {
            return;
        }

        if (!moving)
        {
            visualRoot.localPosition = Vector3.Lerp(
                visualRoot.localPosition,
                visualBasePosition,
                Time.deltaTime * 5f);

            visualRoot.localRotation = Quaternion.Slerp(
                visualRoot.localRotation,
                visualBaseRotation,
                Time.deltaTime * 5f);

            return;
        }

        float phase = Time.time * (5.5f + actualSpeed * 12f);
        float bob = Mathf.Sin(phase) * 0.006f;
        float roll = Mathf.Sin(phase * 0.5f) * 0.4f;

        visualRoot.localPosition =
            visualBasePosition + Vector3.up * bob;

        visualRoot.localRotation =
            visualBaseRotation * Quaternion.Euler(0f, 0f, roll);
    }

    private IEnumerator WaitAtEndpoint(float seconds)
    {
        waiting = true;
        currentSpeed = 0f;
        SetAnimatorMoving(false, 0f);
        yield return new WaitForSeconds(seconds);
        waiting = false;
    }

    public void SetReducedSpeed(bool value)
    {
        reduced = value;
    }

    public void PauseMovement()
    {
        paused = true;
        SetAnimatorMoving(false, 0f);
    }

    public void ResumeMovement()
    {
        paused = false;
    }

    public void PauseFor(float seconds)
    {
        StartCoroutine(PauseRoutine(seconds));
    }

    private IEnumerator PauseRoutine(float seconds)
    {
        PauseMovement();
        yield return new WaitForSeconds(seconds);

        if (GameStateManager.CurrentState == GameState.PLAYING)
        {
            ResumeMovement();
        }
    }

    private void HandleState(GameState state)
    {
        if (state == GameState.BREATHING ||
            state == GameState.PAUSED)
        {
            PauseMovement();
        }
        else if (state == GameState.PLAYING)
        {
            ResumeMovement();
        }
    }
}
