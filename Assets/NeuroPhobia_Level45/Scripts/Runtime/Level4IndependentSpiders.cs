using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NeuroPhobia.Level45
{
    /// <summary>
    /// Gives every spider of Nivel4 a life of its own.
    /// In the saved scene the four spider models hang from a single patrol object: only one of them really walked,
    /// while the other three were dragged along metres away (sliding and swinging around it) and all four froze
    /// together. When the level loads, each extra model is moved under its own object with its own
    /// <see cref="NPNaturalSpiderPatrol"/> (ground snapping, heading correction, obstacle check) and a
    /// <see cref="SpiderWanderGoal"/> that keeps choosing free spots around the place where the model was.
    /// The original spider keeps its authored route. The scene file is not modified.
    /// </summary>
    public static class Level4IndependentSpiders
    {
        private const string SceneName = "Nivel4";
        private const string SharedSpiderName = "NP_Spider_Level4";
        private const string VisualPrefix = "Visual_WolfSpider";
        private const float WanderRadius = 2.5f;

        /// <summary>Spiders that were given their own patrol in the current scene (for tests).</summary>
        public static int SplitCount { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SplitCount = 0;
            if (scene.name != SceneName) return;

            GameObject shared = GameObject.Find(SharedSpiderName);
            NPNaturalSpiderPatrol sharedPatrol = shared != null ? shared.GetComponent<NPNaturalSpiderPatrol>() : null;
            if (sharedPatrol == null) return;

            List<Transform> extras = new List<Transform>();
            foreach (Transform child in shared.transform)
            {
                if (child != sharedPatrol.visualRoot && child.name.StartsWith(VisualPrefix)) extras.Add(child);
            }

            foreach (Transform visual in extras)
            {
                GiveOwnPatrol(sharedPatrol, visual, ++SplitCount);
            }

            // The shared patrol cached the animators of the models that just left it.
            sharedPatrol.RefreshChildAnimators();
        }

        private static void GiveOwnPatrol(NPNaturalSpiderPatrol template, Transform visual, int number)
        {
            Vector3 home = visual.position;
            home.y = template.transform.position.y;
            float visualHeight = visual.localPosition.y;

            // Built inactive so the patrol reads its settings in Awake only after they are assigned.
            GameObject spider = new GameObject(SharedSpiderName + "_Roaming_" + number);
            spider.SetActive(false);
            spider.transform.SetParent(template.transform.parent, false);
            spider.transform.SetPositionAndRotation(home, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));

            visual.SetParent(spider.transform, false);
            visual.localPosition = new Vector3(0f, visualHeight, 0f);

            BoxCollider templateCollider = template.GetComponent<BoxCollider>();
            if (templateCollider != null)
            {
                BoxCollider collider = spider.AddComponent<BoxCollider>();
                collider.center = templateCollider.center;
                collider.size = templateCollider.size;
                collider.isTrigger = templateCollider.isTrigger;
            }

            GameObject goal = new GameObject(spider.name + "_Goal");
            goal.transform.SetParent(template.transform.parent, false);
            goal.transform.position = home;

            NPNaturalSpiderPatrol patrol = spider.AddComponent<NPNaturalSpiderPatrol>();
            patrol.waypoints = new[] { goal.transform };
            patrol.moveSpeed = template.moveSpeed * Random.Range(0.85f, 1.15f);
            patrol.turnSpeed = template.turnSpeed;
            patrol.arrivalDistance = Mathf.Max(template.arrivalDistance, 0.15f);
            patrol.pauseAtPoint = template.pauseAtPoint;
            patrol.speedSmoothing = template.speedSmoothing;
            patrol.wanderDegrees = template.wanderDegrees;
            patrol.wanderFrequency = template.wanderFrequency;
            patrol.stopInterval = template.stopInterval;
            patrol.stopDuration = template.stopDuration;
            patrol.animator = visual.GetComponentInChildren<Animator>(true);
            patrol.visualRoot = visual;
            patrol.animationSpeed = template.animationSpeed;
            patrol.visualYawOffset = template.visualYawOffset;
            patrol.rayHeight = template.rayHeight;
            patrol.rayDistance = template.rayDistance;
            patrol.groundOffset = template.groundOffset;
            patrol.groundMask = template.groundMask;
            patrol.blockOnObstacles = true;
            // The route centre is the moving goal, so this limit only guards against a runaway position.
            patrol.maximumDistanceFromRoute = WanderRadius * 4f;
            patrol.maximumVerticalDeviation = template.maximumVerticalDeviation;

            SpiderWanderGoal wander = spider.AddComponent<SpiderWanderGoal>();
            wander.patrol = patrol;
            wander.goal = goal.transform;
            wander.home = home;
            wander.radius = WanderRadius;

            spider.SetActive(true);
        }
    }
}
