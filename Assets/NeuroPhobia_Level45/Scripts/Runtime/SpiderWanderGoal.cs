using UnityEngine;

namespace NeuroPhobia.Level45
{
    /// <summary>
    /// Small "brain" for a spider driven by <see cref="NPNaturalSpiderPatrol"/>: instead of a fixed route it keeps
    /// moving the patrol's single waypoint to a new free spot around its home. A new spot is chosen when the spider
    /// arrives, when it bumps into something, or after a while, so it never stands frozen against a wall.
    /// Spots must be on the floor, reachable in a straight line by the spider's body, and away from the player.
    /// </summary>
    public sealed class SpiderWanderGoal : MonoBehaviour
    {
        public NPNaturalSpiderPatrol patrol;
        public Transform goal;
        public Vector3 home;
        public float radius = 2.5f;
        [Tooltip("Shortest leg (m): avoids tiny steps that look like trembling.")]
        public float minLegDistance = 0.8f;
        [Tooltip("The spider does not choose spots closer than this (m) to the player's head.")]
        public float playerClearance = 1.2f;
        public Vector2 repickSeconds = new Vector2(8f, 14f);

        private const int PickAttempts = 16;
        private const float ArrivedDistance = 0.25f;
        private const float FloorTolerance = 0.3f;

        private float nextPickTime;

        private void Update()
        {
            if (patrol == null || goal == null) return;

            Vector3 toGoal = goal.position - transform.position;
            toGoal.y = 0f;
            if (toGoal.magnitude > ArrivedDistance && !patrol.IsBlocked && Time.time < nextPickTime) return;

            if (TryPickSpot(out Vector3 spot)) goal.position = spot;
            else goal.position = transform.position;   // nowhere free right now: rest here and retry soon
            nextPickTime = Time.time + Random.Range(repickSeconds.x, repickSeconds.y);
        }

        private bool TryPickSpot(out Vector3 spot)
        {
            Camera head = Camera.main;
            Vector3 from = transform.position;

            for (int attempt = 0; attempt < PickAttempts; attempt++)
            {
                Vector2 offset = Random.insideUnitCircle * radius;
                Vector3 candidate = new Vector3(home.x + offset.x, from.y, home.z + offset.y);
                Vector3 leg = candidate - from;
                leg.y = 0f;
                if (leg.magnitude < minLegDistance) continue;

                if (head != null)
                {
                    Vector3 toPlayer = head.transform.position - candidate;
                    toPlayer.y = 0f;
                    if (toPlayer.magnitude < playerClearance) continue;
                }

                if (!HasFloor(candidate) || !PathIsClear(from, leg)) continue;
                spot = candidate;
                return true;
            }

            spot = from;
            return false;
        }

        // There is walkable floor under the spot, at about the spider's own height.
        private bool HasFloor(Vector3 point)
        {
            Vector3 origin = point + Vector3.up * patrol.rayHeight;
            foreach (RaycastHit hit in Physics.RaycastAll(origin, Vector3.down, patrol.rayDistance, patrol.groundMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(transform)) continue;
                if (hit.normal.y > 0.7f && Mathf.Abs(hit.point.y - point.y) <= FloorTolerance) return true;
            }
            return false;
        }

        // Straight path free for the spider's body: walls and furniture block it; floor, other spiders and the
        // player's capsule do not (the distance to the player is handled separately).
        private bool PathIsClear(Vector3 from, Vector3 leg)
        {
            Vector3 origin = from + Vector3.up * patrol.obstacleHeight;
            foreach (RaycastHit hit in Physics.SphereCastAll(origin, patrol.obstacleRadius, leg.normalized, leg.magnitude, ~0, QueryTriggerInteraction.Ignore))
            {
                Transform other = hit.collider.transform;
                if (other.IsChildOf(transform) || hit.normal.y > 0.7f) continue;
                if (other.GetComponentInParent<NPNaturalSpiderPatrol>() != null) continue;
                if (other.GetComponentInParent<CharacterController>() != null) continue;
                return false;
            }
            return true;
        }
    }
}
