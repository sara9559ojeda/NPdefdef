using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NeuroPhobia.Level45
{
    public sealed class NPNaturalSpiderPatrol : MonoBehaviour
    {
        [Header("Ruta")]
        public Transform[] waypoints;
        public float moveSpeed = 0.18f;
        public float turnSpeed = 5f;
        public float arrivalDistance = 0.10f;
        public float pauseAtPoint = 0.9f;
        public bool loop = true;

        [Header("Movimiento natural")]
        [Tooltip("Segundos que tarda en alcanzar o perder la velocidad de marcha.")]
        public float speedSmoothing = 0.6f;
        [Tooltip("Desvío máximo (grados) del rumbo al caminar, para que no avance en línea perfecta.")]
        public float wanderDegrees = 14f;
        public float wanderFrequency = 0.3f;
        [Tooltip("Cada cuántos segundos (mín, máx) se detiene un instante a mitad de camino.")]
        public Vector2 stopInterval = new Vector2(3.5f, 8f);
        public Vector2 stopDuration = new Vector2(0.4f, 1.6f);

        [Header("Animación")]
        public Animator animator;
        public Transform visualRoot;
        public float animationSpeed = 0.35f;
        public bool useProceduralMotionWhenNoClip = true;

        [Header("Suelo")]
        public float rayHeight = 1.2f;
        public float rayDistance = 3.5f;
        public float groundOffset = 0.015f;
        public LayerMask groundMask = ~0;

        [Header("Seguridad")]
        public float maximumDistanceFromRoute = 12f;
        public float maximumVerticalDeviation = 1.5f;

        private int currentWaypoint;
        private float waitingUntil;
        private bool paused;
        private Vector3 safePosition;
        private float stableGroundY;
        private Vector3 visualBasePosition;
        private Quaternion visualBaseRotation;
        private Animator[] childAnimators;
        private float currentSpeed;
        private float wanderSeed;
        private float nextStopTime;
        private float animationScale = 1f;

        private void Awake()
        {
            PrepareObject();
        }

        private void Start()
        {
            safePosition = transform.position;
            stableGroundY = transform.position.y;
            wanderSeed = Random.value * 100f;
            nextStopTime = Time.time + Random.Range(stopInterval.x, stopInterval.y);
            SnapToGround();
            LiftVisualToGround();
            safePosition = transform.position;
        }

        // El pivote del modelo no siempre está en las patas: se sube (o baja) el visual
        // hasta que su punto más bajo toque el suelo y no quede enterrado.
        private void LiftVisualToGround()
        {
            if (visualRoot == null)
            {
                return;
            }

            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return;
            }

            Bounds bounds = renderers[0].bounds;
            foreach (Renderer rendererComponent in renderers)
            {
                bounds.Encapsulate(rendererComponent.bounds);
            }

            float delta = (transform.position.y - groundOffset) - bounds.min.y;
            if (Mathf.Abs(delta) < 0.005f)
            {
                return;
            }

            visualRoot.position += Vector3.up * delta;
            visualBasePosition = visualRoot.localPosition;
        }

        private void Update()
        {
            EnsureVisible();

            if (PositionIsInvalid())
            {
                transform.position = safePosition;
            }

            if (paused ||
                waypoints == null ||
                waypoints.Length == 0 ||
                Time.time < waitingUntil)
            {
                currentSpeed = 0f;
                UpdateAnimation(false);
                return;
            }

            Transform destinationTransform = waypoints[
                Mathf.Clamp(currentWaypoint, 0, waypoints.Length - 1)];

            if (destinationTransform == null)
            {
                AdvanceWaypoint();
                return;
            }

            Vector3 direction = destinationTransform.position - transform.position;
            direction.y = 0f;

            float distance = direction.magnitude;

            if (distance <= arrivalDistance)
            {
                safePosition = transform.position;
                currentSpeed = 0f;
                waitingUntil = Time.time + pauseAtPoint * Random.Range(0.6f, 1.8f);
                AdvanceWaypoint();
                UpdateAnimation(false);
                return;
            }

            // Parada breve a mitad de camino, como hacen las arañas al "escuchar" el entorno.
            if (Time.time >= nextStopTime && distance > 1f)
            {
                currentSpeed = 0f;
                waitingUntil = Time.time + Random.Range(stopDuration.x, stopDuration.y);
                nextStopTime = waitingUntil + Random.Range(stopInterval.x, stopInterval.y);
                UpdateAnimation(false);
                return;
            }

            // Rumbo con un leve desvío suave (ruido) que se atenúa al acercarse al punto.
            float wander = (Mathf.PerlinNoise(wanderSeed, Time.time * wanderFrequency) - 0.5f) * 2f
                           * wanderDegrees * Mathf.Clamp01(distance / 2f);
            Vector3 heading = Quaternion.Euler(0f, wander, 0f) * direction.normalized;

            Quaternion desiredRotation = Quaternion.LookRotation(heading, Vector3.up);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                desiredRotation,
                turnSpeed * Time.deltaTime);

            float angle = Quaternion.Angle(
                transform.rotation,
                desiredRotation);

            // Avanza mientras gira (curva); si el giro es muy cerrado casi se detiene.
            float alignment = Mathf.Clamp01(1f - angle / 75f);
            float approach = Mathf.Clamp01(distance / 0.8f + 0.3f);
            float targetSpeed = moveSpeed * alignment * approach;

            currentSpeed = Mathf.MoveTowards(
                currentSpeed,
                targetSpeed,
                moveSpeed / Mathf.Max(speedSmoothing, 0.05f) * Time.deltaTime);

            bool moving = currentSpeed > moveSpeed * 0.08f;

            if (currentSpeed > 0.0001f)
            {
                Vector3 forward = transform.forward;
                forward.y = 0f;
                Vector3 step = forward.normalized * currentSpeed * Time.deltaTime;

                // Cerca del punto se acerca directo para no rodearlo sin llegar.
                transform.position = distance < 0.4f
                    ? Vector3.MoveTowards(
                        transform.position,
                        new Vector3(destinationTransform.position.x, transform.position.y, destinationTransform.position.z),
                        currentSpeed * Time.deltaTime)
                    : transform.position + step;

                SnapToGround();
                safePosition = transform.position;
            }

            animationScale = Mathf.Clamp(currentSpeed / Mathf.Max(moveSpeed, 0.001f), 0.4f, 1.2f);
            UpdateAnimation(moving);
        }

        public void PausePatrol()
        {
            paused = true;
            SetChildAnimatorsPaused(true);
            UpdateProceduralAnimation(false);
        }

        public void ResumePatrol()
        {
            paused = false;
            SetChildAnimatorsPaused(false);
        }

        public int DetectedAnimatorCount =>
            childAnimators != null ? childAnimators.Length : 0;

        public int PausedAnimatorCount
        {
            get
            {
                if (childAnimators == null)
                    return 0;

                int count = 0;
                foreach (Animator childAnimator in childAnimators)
                {
                    if (childAnimator != null &&
                        Mathf.Approximately(childAnimator.speed, 0f))
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        private void PrepareObject()
        {
            Rigidbody rigidbodyComponent = GetComponent<Rigidbody>();
            if (rigidbodyComponent != null)
            {
                rigidbodyComponent.isKinematic = true;
                rigidbodyComponent.useGravity = false;
            }

            childAnimators =
                GetComponentsInChildren<Animator>(true);

            if (animator == null)
            {
                animator = childAnimators.FirstOrDefault();
            }

            if (visualRoot == null)
            {
                Renderer rendererComponent =
                    GetComponentInChildren<Renderer>(true);

                visualRoot = rendererComponent != null
                    ? rendererComponent.transform
                    : (transform.childCount > 0
                        ? transform.GetChild(0)
                        : transform);
            }

            if (visualRoot != null)
            {
                visualBasePosition = visualRoot.localPosition;
                visualBaseRotation = visualRoot.localRotation;
            }

            foreach (Animator childAnimator in childAnimators)
            {
                if (childAnimator == null)
                    continue;

                childAnimator.applyRootMotion = false;
                childAnimator.cullingMode =
                    AnimatorCullingMode.AlwaysAnimate;

                bool hasController =
                    childAnimator.runtimeAnimatorController != null;

                childAnimator.enabled = hasController;
                childAnimator.speed = hasController
                    ? animationSpeed
                    : 0f;
            }

            EnsureVisible();
        }

        private void EnsureVisible()
        {
            foreach (Renderer rendererComponent in
                     GetComponentsInChildren<Renderer>(true))
            {
                rendererComponent.enabled = true;
            }
        }

        private void AdvanceWaypoint()
        {
            if (waypoints == null || waypoints.Length == 0)
            {
                return;
            }

            currentWaypoint++;

            if (currentWaypoint >= waypoints.Length)
            {
                currentWaypoint = loop
                    ? 0
                    : waypoints.Length - 1;
            }
        }

        private void SnapToGround()
        {
            Vector3 origin = transform.position + Vector3.up * rayHeight;

            RaycastHit[] hits = Physics.RaycastAll(
                origin,
                Vector3.down,
                rayDistance,
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

                if (hit.normal.y < 0.30f)
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

        private bool PositionIsInvalid()
        {
            Vector3 position = transform.position;

            if (float.IsNaN(position.x) ||
                float.IsNaN(position.y) ||
                float.IsNaN(position.z))
            {
                return true;
            }

            Vector3 routeCenter = GetRouteCenter();
            Vector3 flatOffset = position - routeCenter;
            flatOffset.y = 0f;

            return flatOffset.magnitude > maximumDistanceFromRoute ||
                   Mathf.Abs(position.y - stableGroundY) >
                   maximumVerticalDeviation;
        }

        private Vector3 GetRouteCenter()
        {
            if (waypoints == null || waypoints.Length == 0)
            {
                return safePosition;
            }

            Vector3 sum = Vector3.zero;
            int validCount = 0;

            foreach (Transform point in waypoints)
            {
                if (point == null)
                {
                    continue;
                }

                sum += point.position;
                validCount++;
            }

            return validCount > 0
                ? sum / validCount
                : safePosition;
        }

        private void UpdateAnimation(bool moving)
        {
            if (childAnimators != null && childAnimators.Length > 0)
            {
                if (!paused)
                    SetChildAnimatorsPaused(!moving);

                return;
            }

            UpdateProceduralAnimation(moving);
        }

        private void SetChildAnimatorsPaused(bool pauseAnimations)
        {
            if (childAnimators == null)
            {
                childAnimators =
                    GetComponentsInChildren<Animator>(true);
            }

            foreach (Animator childAnimator in childAnimators)
            {
                if (childAnimator == null ||
                    childAnimator.runtimeAnimatorController == null)
                {
                    continue;
                }

                childAnimator.enabled = true;
                childAnimator.speed =
                    pauseAnimations ? 0f : animationSpeed * animationScale;
            }
        }

        private void UpdateProceduralAnimation(bool moving)
        {

            if (!useProceduralMotionWhenNoClip ||
                visualRoot == null)
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

            float phase = Time.time * 7f;
            float bob = Mathf.Sin(phase) * 0.006f;
            float roll = Mathf.Sin(phase * 0.5f) * 1.2f;

            visualRoot.localPosition =
                visualBasePosition + Vector3.up * bob;

            visualRoot.localRotation =
                visualBaseRotation *
                Quaternion.Euler(0f, 0f, roll);
        }
    }
}
