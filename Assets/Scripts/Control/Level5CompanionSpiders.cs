using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NeuroPhobia.Level45;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Da vida a las arañas del Nivel 5.
/// - Las arañas quietas/de adorno se reemplazan por arañas con movimiento natural
///   (clones de NP_Spider_Level5_*, con su misma patrulla y suelo).
/// - Una araña vigila cada tarea (puerta, ventana, nevera, lámpara) y, según el modo de seguimiento,
///   puede seguir al jugador; el resto deambula.
/// - La cantidad sigue el grado de fobia de la sesión (grado N -> N arañas; sin grado válido, 1).
///   Distancias, seguimiento y tamaño salen de Level5SpiderSettings.
/// - Las arañas aparecen solo en suelo alcanzable (Level5SpiderGrid) y nunca se acercan al jugador
///   a menos de minDistanceToPlayer.
/// - Son las únicas arañas activas del nivel: las arañas antiguas (AranaController) se desactivan del todo
///   y MetricsCollector mide proximidad y alejamientos contra estas.
/// Se crea solo en Nivel5; no modifica la escena.
/// </summary>
public sealed class Level5CompanionSpiders : MonoBehaviour
{
    private const string DoorTaskId = "TASK_DOOR";
    private const float SearchHalfExtent = 25f;

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
    private Level5SpiderSettings settings;
    private Level5SpiderGrid grid;
    private Level5TaskInteraction door;
    private Camera playerCamera;
    private System.Random random;
    private int requestedCount;

    public static Level5CompanionSpiders Instance { get; private set; }

    /// <summary>true cuando las arañas ya se crearon.</summary>
    public bool Ready { get; private set; }
    public Level5SpiderSettings Settings => settings;
    public int SpiderCount => companions.Count;
    /// <summary>Arañas pedidas según el grado de fobia de la sesión.</summary>
    public int RequestedCount => requestedCount;

    public IEnumerable<Transform> SpiderTransforms
    {
        get { return companions.Where(c => c.patrol != null).Select(c => c.patrol.transform); }
    }

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
        if (FindFirstObjectByType<Level5CompanionSpiders>() != null) return;
        new GameObject("NP_Level5_CompanionSpiders").AddComponent<Level5CompanionSpiders>();
    }

    private void Awake() { Instance = this; }
    private void OnDestroy() { if (Instance == this) Instance = null; }

    private IEnumerator Start()
    {
        settings = Level5SpiderSettings.Load();
        random = settings.randomSeed != 0 ? new System.Random(settings.randomSeed) : new System.Random();
        requestedCount = Level5SpiderSettings.SpiderCountForGrade(SessionManager.Instance != null ? SessionManager.Instance.PhobiaGrade : 0);

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

        Transform rig = FindPlayerRig(all);
        Vector3 start = rig != null ? rig.position : Vector3.zero;

        DestroyStaticInfestation(all);
        DisableLegacySpiders();

        // Las arañas viven dentro del apartamento: la zona es la unión de sus pisos (NPWalkableSurface con trigger).
        Bounds? apartment = null;
        foreach (NPWalkableSurface surface in FindObjectsByType<NPWalkableSurface>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            Collider surfaceCollider = surface.GetComponent<Collider>();
            if (surfaceCollider == null) continue;
            if (apartment == null) apartment = surfaceCollider.bounds;
            else { Bounds merged = apartment.Value; merged.Encapsulate(surfaceCollider.bounds); apartment = merged; }
        }

        grid = Level5SpiderGrid.Build(start, SearchHalfExtent, collider => IgnoredForReachability(collider, rig), apartment);
        if (grid.Count < 4)
        {
            Debug.LogWarning("NeuroPhobia: Nivel5 sin suelo alcanzable para las arañas (celdas: " + grid.Count + ").");
            yield break;
        }

        foreach (SpiderSpawn spawn in PlanSpawns(all, start))
        {
            Create(templates[companions.Count % templates.Count], spawn);
        }

        ConnectMetrics();
        Ready = true;
        Debug.Log("NeuroPhobia: Nivel5 arañas vivas -> " + companions.Count + " (pedidas: " + requestedCount + ")");
    }

    // ---------------------------------------------------------------- colocación

    private struct SpiderSpawn
    {
        public Vector3 position;
        public bool followsPlayer;
        public bool followsNearby;
        public float leash;
    }

    private bool IgnoredForReachability(Collider collider, Transform rig)
    {
        if (collider == null) return true;
        if (rig != null && collider.transform.IsChildOf(rig)) return true;
        if (collider.GetComponentInParent<NPNaturalSpiderPatrol>() != null) return true;
        // La puerta de TASK_DOOR se puede abrir: no separa zonas.
        Level5TaskInteraction task = collider.GetComponentInParent<Level5TaskInteraction>();
        return task != null && task.taskId == DoorTaskId;
    }

    private List<SpiderSpawn> PlanSpawns(Transform[] all, Vector3 start)
    {
        var plan = new List<SpiderSpawn>();
        var taken = new List<Vector3>();
        float minSpawn = settings.minSpawnDistanceFromPlayer;

        bool allFollow = settings.followMode == Level5SpiderSettings.FollowMode.AllAlways;
        bool allNearby = settings.followMode == Level5SpiderSettings.FollowMode.AllNearby;
        bool original = settings.followMode == Level5SpiderSettings.FollowMode.Original;

        // 1) Una que acompaña al jugador (según el modo de seguimiento).
        if (requestedCount > 0 && grid.TryNearest(start + Vector3.forward * (minSpawn + 1f), out Vector3 followerSpot, start, minSpawn))
        {
            plan.Add(new SpiderSpawn { position = followerSpot, followsPlayer = original || allFollow, followsNearby = allNearby, leash = float.MaxValue });
            taken.Add(followerSpot);
        }

        // 2) Una por tarea: puerta, ventana, nevera, lámpara.
        foreach (Vector3 target in FindTaskTargets(all))
        {
            if (plan.Count >= requestedCount) break;
            Vector3 toStart = start - target;
            toStart.y = 0f;
            Vector3 direction = toStart.sqrMagnitude > 0.01f ? toStart.normalized : Vector3.back;
            Vector3 wanted = target + direction * 1.4f;
            if (!grid.TryNearest(wanted, out Vector3 spot, start, minSpawn)) continue;
            if (taken.Any(p => Flat(p - spot).magnitude < settings.minSpacingBetweenSpiders * 0.5f)) continue;
            plan.Add(new SpiderSpawn { position = spot, followsPlayer = allFollow, followsNearby = original || allNearby, leash = settings.taskLeash });
            taken.Add(spot);
        }

        // 3) El resto deambula en puntos alcanzables al azar, separados entre sí y del jugador.
        List<Vector3> candidates = grid.Points()
            .Where(p => Flat(p - start).magnitude >= minSpawn)
            .OrderBy(_ => random.Next())
            .ToList();
        foreach (Vector3 candidate in candidates)
        {
            if (plan.Count >= requestedCount) break;
            if (taken.Any(p => Flat(p - candidate).magnitude < settings.minSpacingBetweenSpiders)) continue;
            plan.Add(new SpiderSpawn { position = candidate, followsPlayer = allFollow, followsNearby = allNearby, leash = settings.wanderRadius * 2f });
            taken.Add(candidate);
        }

        if (plan.Count < requestedCount)
        {
            Debug.LogWarning("NeuroPhobia: Nivel5 solo encontró sitio para " + plan.Count + " de " + requestedCount + " arañas.");
        }

        return plan;
    }

    private static IEnumerable<Vector3> FindTaskTargets(Transform[] all)
    {
        Level5TaskInteraction[] tasks = FindObjectsByType<Level5TaskInteraction>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (string id in new[] { DoorTaskId, "TASK_WINDOW", "TASK_FRIDGE", "TASK_LAMP" })
        {
            if (id == "TASK_WINDOW")
            {
                Level5WindowGaze window = FindFirstObjectByType<Level5WindowGaze>(FindObjectsInactive.Include);
                if (window != null && window.windowTarget != null)
                {
                    Collider windowCollider = window.windowTarget.GetComponent<Collider>();
                    yield return windowCollider != null ? windowCollider.bounds.center : window.windowTarget.position;
                }
                continue;
            }

            Level5TaskInteraction task = tasks.FirstOrDefault(t => t.taskId == id);
            if (task == null) continue;
            Collider collider = task.GetComponent<Collider>();
            yield return collider != null ? collider.bounds.center : task.transform.position;
        }
    }

    private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    private static Transform FindPlayerRig(Transform[] all)
    {
        Transform active = all.FirstOrDefault(t => t.gameObject.activeInHierarchy && (t.name.StartsWith("XR Origin") || t.name.StartsWith("NP_DesktopPlayer")));
        if (active != null) return active;
        return all.FirstOrDefault(t => t.name.StartsWith("XR Origin") || t.name.StartsWith("NP_DesktopPlayer"));
    }

    // ---------------------------------------------------------------- vida

    private void Update()
    {
        if (playerCamera == null || !playerCamera.isActiveAndEnabled) playerCamera = FindPlayerCamera();
        if (playerCamera == null || !Ready) return;
        Vector3 player = playerCamera.transform.position;

        if (settings.doorOpensOnApproach) TryOpenDoor(player);

        foreach (Companion c in companions)
        {
            if (c.patrol == null) continue;
            c.goal.position = ChooseGoal(c, player);
        }
    }

    // Garantía de distancia mínima: pase lo que pase, ninguna araña queda más cerca que minDistanceToPlayer.
    private void LateUpdate()
    {
        if (playerCamera == null || !Ready) return;
        Vector3 player = playerCamera.transform.position;
        float min = settings.minDistanceToPlayer;

        foreach (Companion c in companions)
        {
            if (c.patrol == null) continue;
            Vector3 offset = Flat(c.patrol.transform.position - player);
            if (offset.magnitude >= min) continue;

            Vector3 direction = offset.sqrMagnitude > 0.0001f ? offset.normalized : Flat(-playerCamera.transform.forward).normalized;
            Vector3 position = c.patrol.transform.position;
            Vector3 pushed = new Vector3(player.x, position.y, player.z) + direction * min;
            // El empujón nunca la mete en una pared o un mueble: si cae fuera de suelo libre, va al punto libre más cercano
            // que respete la distancia mínima.
            if (grid != null && !grid.IsReachable(pushed) && grid.TryNearest(pushed, out Vector3 free, player, min))
            {
                pushed = new Vector3(free.x, position.y, free.z);
            }
            c.patrol.transform.position = pushed;
            c.goal.position = pushed;
        }
    }

    private Vector3 ChooseGoal(Companion c, Vector3 player)
    {
        Vector3 spider = c.patrol.transform.position;
        Vector3 toPlayer = player - spider;
        toPlayer.y = 0f;
        float distance = toPlayer.magnitude;

        bool follow = settings.followMode != Level5SpiderSettings.FollowMode.Never &&
                      (c.followsPlayer || (c.followsNearby && distance < settings.followRange));
        float stopDistance = Mathf.Max(settings.followStopDistance, settings.minDistanceToPlayer + 0.25f);
        Vector3 desired;

        if (follow && distance > stopDistance)
        {
            // Se detiene a unos metros del jugador, sin tocarlo.
            desired = player - toPlayer.normalized * stopDistance;
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
            if (Time.time >= c.nextWander || (c.goal.position - c.anchor).magnitude > settings.wanderRadius * 2f + 0.1f)
            {
                Vector2 offset = Random.insideUnitCircle * settings.wanderRadius;
                c.goal.position = c.anchor + new Vector3(offset.x, 0f, offset.y);
                c.nextWander = Time.time + Random.Range(3f, 7f);
            }
            desired = c.goal.position;
        }

        // El destino siempre cae en suelo alcanzable (nada de muebles ni zonas cerradas).
        if (grid != null && !grid.IsReachable(desired))
        {
            if (grid.TryNearest(desired, out Vector3 reachablePoint)) desired = reachablePoint;
            else desired = spider;
        }

        // Nunca se elige un destino a menos de la distancia mínima del jugador.
        Vector3 desiredFromPlayer = Flat(desired - player);
        if (desiredFromPlayer.magnitude < settings.minDistanceToPlayer)
        {
            desired = spider;
        }

        desired.y = spider.y;

        // Sin pathfinding: si hay un obstáculo en el camino (medido con el cuerpo de la araña, no con un rayo fino)
        // se queda donde está hasta elegir otro punto. Si chocó al avanzar, también elige otro.
        if (c.patrol.IsBlocked) c.nextWander = 0f;

        Vector3 eye = spider + Vector3.up * c.patrol.obstacleHeight;
        Vector3 path = desired - spider;
        path.y = 0f;
        if (path.magnitude > 0.2f)
        {
            RaycastHit[] hits = Physics.SphereCastAll(eye, c.patrol.obstacleRadius, path.normalized, path.magnitude, ~0, QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in hits)
            {
                Transform other = hit.collider.transform;
                if (other.IsChildOf(c.patrol.transform) || hit.normal.y > 0.7f) continue;
                if (other.GetComponentInParent<NPNaturalSpiderPatrol>() != null || other.GetComponentInParent<CharacterController>() != null) continue;
                c.nextWander = 0f;
                return spider;
            }
        }

        return desired;
    }

    private void TryOpenDoor(Vector3 player)
    {
        if (door == null || GameStateManager.CurrentState != GameState.PLAYING) return;

        Collider collider = door.GetComponent<Collider>();
        Vector3 doorPoint = collider != null ? collider.ClosestPoint(player) : door.transform.position;
        doorPoint.y = player.y;
        if (Vector3.Distance(player, doorPoint) <= 2.3f)
        {
            door.Activate();
            door = null;
        }
    }

    private void Create(Transform template, SpiderSpawn spawn)
    {
        Vector3 position = spawn.position + Vector3.up * 0.015f;

        GameObject clone = Instantiate(template.gameObject, position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        clone.name = "NP_L5_LiveSpider_" + (companions.Count + 1);

        NPNaturalSpiderPatrol patrol = clone.GetComponent<NPNaturalSpiderPatrol>();

        GameObject goalObject = new GameObject(clone.name + "_Goal");
        goalObject.transform.position = position;

        patrol.waypoints = new[] { goalObject.transform };
        patrol.moveSpeed = settings.moveSpeed;
        patrol.pauseAtPoint = 0.4f;
        patrol.maximumDistanceFromRoute = 60f;
        patrol.blockOnObstacles = true;

        if (patrol.visualRoot != null) patrol.visualRoot.localScale *= settings.visualScale;

        // Sin NPLevel5Controller pausando ni referencias cruzadas al original.
        clone.SetActive(true);

        companions.Add(new Companion
        {
            patrol = patrol,
            goal = goalObject.transform,
            anchor = position,
            followsPlayer = spawn.followsPlayer,
            followsNearby = spawn.followsNearby,
            leash = spawn.leash
        });
    }

    // Las arañas de adorno (inmóviles) se sustituyen por las arañas que deambulan.
    private static void DestroyStaticInfestation(Transform[] all)
    {
        GameObject infestation = all.Select(t => t.gameObject).FirstOrDefault(g => g.name == "NP_Level5_StaticInfestation");
        if (infestation != null) Destroy(infestation);
    }

    // Las arañas antiguas del nivel (AranaController: CaveSpider, roam sin animación) se desactivan del todo.
    // Solo apagar el renderer dejaba su collider: tapaba la mirada, bloqueaba a las arañas vivas y
    // MetricsCollector medía proximidad y alejamientos contra arañas invisibles.
    private static void DisableLegacySpiders()
    {
        foreach (AranaController spider in FindObjectsByType<AranaController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (spider.name.StartsWith("NP_")) continue;
            spider.gameObject.SetActive(false);
        }
    }

    // MetricsCollector del nivel (el del TaskManager) mide contra exactamente las arañas visibles y activas.
    private void ConnectMetrics()
    {
        TaskManager taskManager = FindFirstObjectByType<TaskManager>();
        MetricsCollector collector = taskManager != null && taskManager.metricsCollector != null
            ? taskManager.metricsCollector
            : FindFirstObjectByType<MetricsCollector>();
        if (collector != null) collector.SetSpiders(SpiderTransforms.ToArray());
        else Debug.LogWarning("NeuroPhobia: Nivel5 sin MetricsCollector para las arañas vivas.");
    }

    private static Camera FindPlayerCamera()
    {
        if (Camera.main != null && Camera.main.isActiveAndEnabled) return Camera.main;
        return Camera.allCameras.FirstOrDefault(c => c.isActiveAndEnabled && c.targetTexture == null);
    }
}
