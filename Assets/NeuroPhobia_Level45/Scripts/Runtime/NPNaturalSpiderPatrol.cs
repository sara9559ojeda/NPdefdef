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

        private void Awake()
        {
            PrepareObject();
        }

        private void Start()
        {
            safePosition = transform.position;
            stableGroundY = transform.position.y;
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

            if (direction.magnitude <= arrivalDistance)
            {
                safePosition = transform.position;
                waitingUntil = Time.time + pauseAtPoint;
                AdvanceWaypoint();
                UpdateAnimation(false);
                return;
            }

            Quaternion desiredRotation = Quaternion.LookRotation(
                direction.normalized,
                Vector3.up);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                desiredRotation,
                turnSpeed * Time.deltaTime);

            float angle = Quaternion.Angle(
                transform.rotation,
                desiredRotation);

            bool moving = angle < 35f;

            if (moving)
            {
                Vector3 nextPosition = Vector3.MoveTowards(
                    transform.position,
                    new Vector3(
                        destinationTransform.position.x,
                        transform.position.y,
                        destinationTransform.position.z),
                    moveSpeed * Time.deltaTime);

                transform.position = nextPosition;
                SnapToGround();
                safePosition = transform.position;
            }

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
                    pauseAnimations ? 0f : animationSpeed;
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
