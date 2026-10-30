using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NeuroPhobia.Level45;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Da vida a las arañas del Nivel 5 y abre la puerta al acercarse.
/// - Las arañas quietas/de adorno se reemplazan por arañas con movimiento natural
///   (clones de NP_Spider_Level5_*, con su misma patrulla y suelo).
/// - Una araña vigila cada tarea (puerta, ventana, nevera, lámpara) y sigue al jugador cuando está cerca;
///   otra lo sigue siempre; el resto deambula por sus puntos originales.
/// - La puerta (TASK_DOOR) se abre sola cuando el jugador llega a ella.
/// Se crea solo en Nivel5; no modifica la escena.
/// </summary>
public sealed class Level5CompanionSpiders : MonoBehaviour
{
    private const float VisualScale = 0.7f;
    private const float SpiderSpeed = 0.5f;
    private const float FollowRange = 10f;
    private const float FollowDistance = 3f;
    private const float TaskLeash = 6f;
    private const float WanderRadius = 1.4f;
    private const float DoorOpenDistance = 2.3f;
    private const string DoorTaskId = "TASK_DOOR";

    private sealed class Companion
    {
        public NPNaturalSpiderPatrol patrol;
        public Transform goal;
        public Vector3 anchor;
        public bool followsPlayer;   // sigue siempre
        public bool followsNearby;   // sigue mientras el jugador esté cerca
        public float leash;
        public float nextWander;
    }

    private readonly List<Companion> companions = new List<Companion>();
    private Level5TaskInteraction door;
    private Camera playerCamera;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Nivel5") return;
        new GameObject("NP_Level5_CompanionSpiders").AddComponent<Level5CompanionSpiders>();
    }

    private IEnumerator Start()
    {
        // Espera a que el resto de scripts del nivel creen/activen lo suyo.
        for (int i = 0; i < 5; i++) yield return null;

        door = FindObjectsByType<Level5TaskInteraction>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(t => t.taskId == DoorTaskId);

        Transform[] all = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        List<Transform> templates = all
            .Where(t => t.name == "NP_Spider_Level5_Central" || t.name == "NP_Spider_Level5_A" || t.name == "NP_Spider_Level5_B")
            .Where(t => t.GetComponent<NPNaturalSpiderPatrol>() != null)
            .ToList();

        if (templates.Count == 0)
        {
            Debug.LogWarning("NeuroPhobia: Level5CompanionSpiders no encontró NP_Spider_Level5_*.");
            yield break;
        }

        Vector3 start = FindPlayerStart(all);
        List<Vector3> roamSpots = CollectStaticSpiderSpots(all);
        HideDeadSpiders(all);

        int index = 0;
        foreach (Vector3 anchor in FindTaskAnchors(all, start))
        {
            Create(templates[index++ % templates.Count], anchor, TaskLeash, false, true);
        }

        Create(templates[index++ % templates.Count], start + Vector3.forward * 3f, float.MaxValue, true, false);

        foreach (Vector3 spot in roamSpots)
        {
            Create(templates[index++ % templates.Count], spot, WanderRadius * 2f, false, false);
        }

        Debug.Log("NeuroPhobia: Nivel5 arañas vivas -> " + companions.Count);
    }

    private void Update()
    {
        if (playerCamera == null || !playerCamera.isActiveAndEnabled) playerCamera = FindPlayerCamera();
        if (playerCamera == null) return;
        Vector3 player = playerCamera.transform.position;

        TryOpenDoor(player);

        foreach (Companion c in companions)
        {
            if (c.patrol == null) continue;
            c.goal.position = ChooseGoal(c, player);
        }
    }

    private Vector3 ChooseGoal(Companion c, Vector3 player)
    {
        Vector3 spider = c.patrol.transform.position;
        Vector3 toPlayer = player - spider;
        toPlayer.y = 0f;
        float distance = toPlayer.magnitude;

        bool follow = c.followsPlayer || (c.followsNearby && distance < FollowRange);
        Vector3 desired;

        if (follow && distance > FollowDistance)
        {
            // Se detiene a unos metros del jugador, sin tocarlo.
            desired = player - toPlayer.normalized * FollowDistance;
            Vector3 fromAnchor = desired - c.anchor;
            fromAnchor.y = 0f;
            if (fromAnchor.magnitude > c.leash) desired = c.anchor + fromAnchor.normalized * c.leash;
        }
        else if (follow)
        {
            desired = spider;
        }
        else
        {
            if (Time.time >= c.nextWander || (c.goal.position - c.anchor).magnitude > WanderRadius * 2f + 0.1f)
            {
                Vector2 offset = Random.insideUnitCircle * WanderRadius;
                c.goal.position = c.anchor + new Vector3(offset.x, 0f, offset.y);
                c.nextWander = Time.time + Random.Range(3f, 7f);
            }
            desired = c.goal.position;
        }

        desired.y = spider.y;

        // Sin pathfinding: si hay un obstáculo en el camino se queda donde está hasta elegir otro punto.
        Vector3 eye = spider + Vector3.up * 0.15f;
        Vector3 path = desired - spider;
        if (path.magnitude > 0.2f &&
            Physics.Raycast(eye, path.normalized, out RaycastHit hit, path.magnitude, ~0, QueryTriggerInteraction.Ignore) &&
            !hit.transform.IsChildOf(c.patrol.transform))
        {
            c.nextWander = 0f;
            return spider;
        }

        return desired;
    }

    private void TryOpenDoor(Vector3 player)
    {
        if (door == null || GameStateManager.CurrentState != GameState.PLAYING) return;

        Collider collider = door.GetComponent<Collider>();
        Vector3 doorPoint = collider != null ? collider.ClosestPoint(player) : door.transform.position;
        doorPoint.y = player.y;
        if (Vector3.Distance(player, doorPoint) <= DoorOpenDistance)
        {
            door.Activate();
            door = null;
        }
    }

    private void Create(Transform template, Vector3 position, float leash, bool followsPlayer, bool followsNearby)
    {
        position = Grounded(position);

        GameObject clone = Instantiate(template.gameObject, position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        clone.name = "NP_L5_LiveSpider_" + (companions.Count + 1);

        NPNaturalSpiderPatrol patrol = clone.GetComponent<NPNaturalSpiderPatrol>();

        GameObject goalObject = new GameObject(clone.name + "_Goal");
        goalObject.transform.position = position;

        patrol.waypoints = new[] { goalObject.transform };
        patrol.moveSpeed = SpiderSpeed;
        patrol.pauseAtPoint = 0.4f;
        patrol.maximumDistanceFromRoute = 60f;

        if (patrol.visualRoot != null) patrol.visualRoot.localScale *= VisualScale;

        // Sin NPLevel5Controller pausando ni referencias cruzadas al original.
        clone.SetActive(true);

        companions.Add(new Companion
        {
            patrol = patrol,
            goal = goalObject.transform,
            anchor = position,
            followsPlayer = followsPlayer,
            followsNearby = followsNearby,
            leash = leash
        });
    }

    private static Vector3 Grounded(Vector3 position)
    {
        if (Physics.Raycast(position + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 8f, ~0, QueryTriggerInteraction.Ignore))
        {
            position.y = hit.point.y + 0.015f;
        }
        return position;
    }

    // Un punto frente a cada tarea, del lado por donde se llega a ella.
    private static IEnumerable<Vector3> FindTaskAnchors(Transform[] all, Vector3 start)
    {
        var targets = new List<Vector3>();

        foreach (Level5TaskInteraction task in FindObjectsByType<Level5TaskInteraction>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Collider collider = task.GetComponent<Collider>();
            targets.Add(collider != null ? collider.bounds.center : task.transform.position);
        }

        Level5WindowGaze window = FindFirstObjectByType<Level5WindowGaze>(FindObjectsInactive.Include);
        if (window != null && window.windowTarget != null)
        {
            Collider collider = window.windowTarget.GetComponent<Collider>();
            targets.Add(collider != null ? collider.bounds.center : window.windowTarget.position);
        }

        foreach (Vector3 target in targets)
        {
            Vector3 toStart = start - target;
            toStart.y = 0f;
            Vector3 direction = toStart.sqrMagnitude > 0.01f ? toStart.normalized : Vector3.back;
            yield return target + direction * 1.4f;
        }
    }

    private static Vector3 FindPlayerStart(Transform[] all)
    {
        Transform rig = all.FirstOrDefault(t => t.name.StartsWith("XR Origin") || t.name.StartsWith("NP_DesktopPlayer"));
        return rig != null ? rig.position : Vector3.zero;
    }

    // Las arañas de adorno (inmóviles) se convierten en arañas que deambulan en el mismo lugar.
    private static List<Vector3> CollectStaticSpiderSpots(Transform[] all)
    {
        var spots = new List<Vector3>();
        GameObject infestation = all.Select(t => t.gameObject).FirstOrDefault(g => g.name == "NP_Level5_StaticInfestation");
        if (infestation == null) return spots;

        foreach (Transform child in infestation.transform) spots.Add(child.position);
        Destroy(infestation);
        return spots;
    }

    // Las arañas "muertas" del nivel (roam/AranaController sin animación) se ocultan y quedan en reposo.
    private static void HideDeadSpiders(Transform[] all)
    {
        foreach (AranaController spider in FindObjectsByType<AranaController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (spider.name.StartsWith("NP_")) continue;

            spider.SetMovementPattern(MovementPattern.IDLE);
            Level5SpiderRoam roam = spider.GetComponent<Level5SpiderRoam>();
            if (roam != null) roam.enabled = false;

            foreach (Renderer renderer in spider.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
        }
    }

    private static Camera FindPlayerCamera()
    {
        if (Camera.main != null && Camera.main.isActiveAndEnabled) return Camera.main;
        return Camera.allCameras.FirstOrDefault(c => c.isActiveAndEnabled && c.targetTexture == null);
    }
}
