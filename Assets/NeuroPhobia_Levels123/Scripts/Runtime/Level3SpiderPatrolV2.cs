using System.Collections;
using System.Linq;
using UnityEngine;

/// <summary>
/// Patrulla segura y natural de la araña del Nivel 3.
/// Deambula por su zona (un círculo de radio <see cref="patrolHalfDistance"/> alrededor de su sitio): al llegar a un
/// punto elige otro al azar, a más de <see cref="minLegDistance"/>, en suelo libre (no encima de muebles), con el camino
/// despejado, lejos de los orbes y sin acercarse al jugador. (Antes daba vueltas a un círculo de ~1,3 m y frenaba para
/// girar cada poco: se movía muy poco y de forma rara.) Gira de forma continua con un giro máximo por segundo, mira
/// siempre hacia donde avanza, frena en las curvas y acelera con suavidad; de vez en cuando se detiene un momento.
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
    [Tooltip("Probabilidad de detenerse un momento al llegar a un punto.")]
    [Range(0f, 1f)] public float pauseChance = 0.3f;

    [Header("Deambular")]
    [Tooltip("Distancia mínima (m) de cada tramo: evita pasitos cortos con giros en el sitio.")]
    public float minLegDistance = 1.5f;
    [Tooltip("Puntos que la araña no pisa (los orbes): no elige destinos a menos de keepAwayRadius de ellos.")]
    public Vector3[] keepAway = new Vector3[0];
    public float keepAwayRadius = 0f;
    [Tooltip("No elige destinos a menos de esta distancia (m) del jugador ni camina a través de él.")]
    public float playerClearance = 1.5f;
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
    private Vector3 destination;
    private bool hasDestination;
    private float nextPickTime;
    private float currentSpeed;

    // Altura y radio del «cuerpo» con el que se comprueba que un destino y su camino están libres.
    private const float BodyHeight = 0.35f;
    private const float BodyRadius = 0.22f;
    // Un apoyo más alto que esto sobre el suelo de la araña es un mueble: no se sube a él.
    private const float MaxStepUp = 0.3f;
    private const int PickAttempts = 24;

    public float CurrentSpeed => currentSpeed;
    public bool HasDestination => hasDestination;
    public Vector3 Destination => destination;

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
        PickDestination();
    }

    // Elige el siguiente punto de su zona: suelo libre, camino despejado, lejos de los orbes y del jugador.
    private void PickDestination()
    {
        float radius = Mathf.Max(0.4f, patrolHalfDistance);
        // La zona nunca debe disparar el «reinicio por salirse de la ruta».
        maxRouteDistanceFromSpawn = Mathf.Max(maxRouteDistanceFromSpawn, radius + 1.5f);
        Vector3? player = PlayerPosition();
        float minLeg = Mathf.Min(minLegDistance, radius);

        for (int attempt = 0; attempt < PickAttempts; attempt++)
        {
            Vector2 offset = Random.insideUnitCircle * radius;
            Vector3 candidate = initialSpawn + new Vector3(offset.x, 0f, offset.y);
            if (Flat(candidate - transform.position).magnitude < minLeg) continue;
            if (!IsFreeDestination(candidate, player, out Vector3 grounded)) continue;
            if (!PathIsClear(transform.position, grounded)) continue;
            destination = grounded;
            hasDestination = true;
            return;
        }

        // Sin destino válido ahora (p. ej. el jugador está en medio): vuelve a su sitio si puede, o lo intenta luego.
        if (Flat(initialSpawn - transform.position).magnitude > 0.3f && PathIsClear(transform.position, initialSpawn))
        {
            destination = initialSpawn;
            hasDestination = true;
            return;
        }
        hasDestination = false;
        nextPickTime = Time.time + 1f;
    }

    private bool IsFreeDestination(Vector3 candidate, Vector3? player, out Vector3 grounded)
    {
        grounded = candidate;
        if (!FloorHeightAt(candidate, out float floorY)) return false;
        grounded = new Vector3(candidate.x, floorY + groundOffset, candidate.z);

        Vector3 body = grounded + Vector3.up * BodyHeight;
        if (Physics.CheckSphere(body, BodyRadius, groundMask, QueryTriggerInteraction.Ignore) && !OnlySelfAt(body, BodyRadius)) return false;
        foreach (Vector3 point in keepAway)
            if (Flat(point - grounded).magnitude < keepAwayRadius) return false;
        if (player.HasValue && Flat(player.Value - grounded).magnitude < playerClearance) return false;
        return true;
    }

    // Altura del suelo en un punto. Si lo primero que hay debajo está más alto que el suelo de la araña, es un mueble.
    private bool FloorHeightAt(Vector3 point, out float floorY)
    {
        floorY = stableGroundY;
        Vector3 origin = new Vector3(point.x, stableGroundY + groundRayHeight, point.z);
        foreach (RaycastHit hit in Physics.RaycastAll(origin, Vector3.down, groundRayHeight * 3f, groundMask, QueryTriggerInteraction.Ignore)
                     .OrderBy(candidate => candidate.distance))
        {
            if (hit.collider == null || hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform)) continue;
            if (hit.collider.GetComponentInParent<Level3SpiderPatrolV2>() != null) continue;
            if (hit.normal.y < 0.7f) return false;                       // pared o borde
            if (hit.point.y > stableGroundY + MaxStepUp) return false;   // encima de un mueble
            floorY = hit.point.y;
            return true;
        }
        return false;
    }

    // Camino recto despejado para el cuerpo de la araña (muebles, paredes, orbes y el propio jugador lo cortan).
    private bool PathIsClear(Vector3 from, Vector3 to)
    {
        Vector3 path = to - from;
        path.y = 0f;
        float length = path.magnitude;
        if (length < 0.01f) return true;
        Vector3 start = new Vector3(from.x, stableGroundY + BodyHeight, from.z);
        foreach (RaycastHit hit in Physics.SphereCastAll(start, BodyRadius, path / length, length, groundMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.distance <= 0f) continue;   // ya solapado al empezar: lo decide la comprobación del destino
            Transform other = hit.collider.transform;
            if (other == transform || other.IsChildOf(transform)) continue;
            if (hit.collider.GetComponentInParent<Level3SpiderPatrolV2>() != null) continue;
            if (hit.normal.y > 0.7f) continue;  // suelo
            return false;
        }
        Vector3? player = PlayerPosition();
        if (player.HasValue && DistanceToSegment(Flat(player.Value), Flat(from), Flat(to)) < playerClearance) return false;
        return true;
    }

    private static Vector3? PlayerPosition()
    {
        Camera head = Camera.main;
        return head != null ? head.transform.position : (Vector3?)null;
    }

    private static float DistanceToSegment(Vector3 point, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float t = ab.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector3.Dot(point - a, ab) / ab.sqrMagnitude) : 0f;
        return Vector3.Distance(point, a + ab * t);
    }

    private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

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

        if (!hasDestination && !paused && !waiting && Time.time >= nextPickTime)
        {
            PickDestination();
        }

        if (paused || waiting || !hasDestination)
        {
            currentSpeed = 0f;
            SetAnimatorMoving(false, 0f);
            ApplyFallbackLegMotion(false, 0f);
            return;
        }

        float cruise = moveSpeed * (reduced ? reducedSpeedMultiplier : 1f);
        Vector3 flatDirection = destination - transform.position;
        flatDirection.y = 0f;
        float distance = flatDirection.magnitude;

        if (distance <= 0.2f)
        {
            PickDestination();
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

            // Un apoyo muy por encima de su suelo es un mueble (p. ej. pasa junto a una mesa): no se sube de golpe.
            if (hit.point.y > stableGroundY + MaxStepUp)
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
        currentSpeed = 0f;
        PickDestination();
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
