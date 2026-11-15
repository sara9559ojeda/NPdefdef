using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NeuroPhobia.Level45
{
    /// <summary>
    /// Exposure in Nivel4: when the player reaches a glowing orb, the spiders closest to it stop roaming and walk
    /// slowly towards the orb, stopping at a safe distance from the player that shrinks with each orb
    /// (gradual exposure: 2.5 m, 2 m, 1.5 m, perceived). They head for the orb, not for the player, so they never
    /// chase: if the player steps back they stop where they are, and they never come closer than the stop distance.
    /// When the orb is completed they go back to roaming. The scene is not modified.
    /// </summary>
    public sealed class Level4SpiderApproach : MonoBehaviour
    {
        // Perceived metres for the first, second and following orbs (multiplied by the rig scale).
        private static readonly float[] StopDistances = { 2.5f, 2.0f, 1.5f };
        private const int ApproachingSpiders = 2;
        // Same pace as when they roam (they used to slow down to 60 % while approaching).
        private const float ApproachSpeedFactor = 1f;
        private const float RetargetSeconds = 0.5f;

        private sealed class Approacher
        {
            public NPNaturalSpiderPatrol patrol;
            public SpiderWanderGoal wander;
            public Transform[] savedWaypoints;
            public bool savedLoop;
            public float savedSpeed;
            public Transform goal;
        }

        private static Level4SpiderApproach instance;
        private readonly List<Approacher> approachers = new List<Approacher>();
        private Vector3 orb;
        private float stopDistance;
        private float nextRetarget;

        /// <summary>Spiders currently walking towards the orb (for tests).</summary>
        public static IEnumerable<NPNaturalSpiderPatrol> Approaching =>
            instance != null ? instance.approachers.Select(a => a.patrol).Where(p => p != null) : Enumerable.Empty<NPNaturalSpiderPatrol>();

        /// <summary>Stop distance in world metres for the current orb (for tests).</summary>
        public static float CurrentStopDistance => instance != null ? instance.stopDistance : 0f;

        /// <summary>Starts the approach towards the orb of the given point (0-based).</summary>
        public static void Begin(Vector3 orbPosition, int pointIndex)
        {
            End();
            Camera head = Camera.main;
            float scale = NeuroPhobiaWorldPanel.RigScale(head != null ? head.transform : null);
            instance = new GameObject("NP_Level4_SpiderApproach").AddComponent<Level4SpiderApproach>();
            instance.orb = orbPosition;
            instance.stopDistance = StopDistances[Mathf.Clamp(pointIndex, 0, StopDistances.Length - 1)] * scale;

            IEnumerable<NPNaturalSpiderPatrol> closest = FindObjectsByType<NPNaturalSpiderPatrol>(FindObjectsSortMode.None)
                .Where(p => p.isActiveAndEnabled)
                .OrderBy(p => Flat(p.transform.position - orbPosition).sqrMagnitude)
                .Take(ApproachingSpiders);
            foreach (NPNaturalSpiderPatrol patrol in closest) instance.Take(patrol);
            instance.Retarget();
        }

        /// <summary>Gives the spiders back their own roaming or route.</summary>
        public static void End()
        {
            if (instance == null) return;
            foreach (Approacher a in instance.approachers) Release(a);
            instance.approachers.Clear();
            Destroy(instance.gameObject);
            instance = null;
        }

        private void Take(NPNaturalSpiderPatrol patrol)
        {
            var a = new Approacher
            {
                patrol = patrol,
                wander = patrol.GetComponent<SpiderWanderGoal>(),
                savedWaypoints = patrol.waypoints,
                savedLoop = patrol.loop,
                savedSpeed = patrol.moveSpeed,
                goal = new GameObject(patrol.name + "_Acercamiento").transform
            };
            a.goal.position = patrol.transform.position;
            if (a.wander != null) a.wander.enabled = false;
            patrol.waypoints = new[] { a.goal };
            patrol.loop = true;
            patrol.moveSpeed = a.savedSpeed * ApproachSpeedFactor;
            approachers.Add(a);
        }

        private static void Release(Approacher a)
        {
            if (a.patrol != null)
            {
                a.patrol.waypoints = a.savedWaypoints;
                a.patrol.loop = a.savedLoop;
                a.patrol.moveSpeed = a.savedSpeed;
            }
            if (a.wander != null) a.wander.enabled = true;
            if (a.goal != null) Destroy(a.goal.gameObject);
        }

        private void Update()
        {
            if (Time.time >= nextRetarget) Retarget();
        }

        // Each spider aims at a point next to the orb, on its own side, at the stop distance from the player; once
        // it is at the stop distance from the player (or the player came closer) it holds its position.
        private void Retarget()
        {
            nextRetarget = Time.time + RetargetSeconds;
            Camera head = Camera.main;
            Vector3? player = head != null ? head.transform.position : (Vector3?)null;

            foreach (Approacher a in approachers)
            {
                if (a.patrol == null || a.goal == null) continue;
                Vector3 spider = a.patrol.transform.position;
                if (player.HasValue && Flat(spider - player.Value).magnitude <= stopDistance)
                {
                    a.goal.position = spider;   // close enough: stays (does not chase)
                    continue;
                }

                Vector3 reference = player.HasValue ? new Vector3(player.Value.x, spider.y, player.Value.z) : new Vector3(orb.x, spider.y, orb.z);
                // Use the orb when the player has walked away from it: the spiders do not follow the player.
                if (player.HasValue && Flat(player.Value - orb).magnitude > stopDistance * 2f) reference = new Vector3(orb.x, spider.y, orb.z);
                Vector3 fromReference = Flat(spider - reference);
                if (fromReference.sqrMagnitude < 0.0001f) fromReference = Vector3.forward;
                a.goal.position = reference + fromReference.normalized * stopDistance;
            }
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    }
}
