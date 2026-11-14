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
        public Vector3 lastPosition;
        public bool hasLastPosition;
        public Bounds? room;          // habitación por la que deambula (toda, no un círculo pequeño)
        public Level5GrabbableSpider grabbable;
        public bool Held => grabbable != null && (grabbable.IsHeld || !patrol.enabled);
    }

    // Pisos de las habitaciones (lejos de las paredes), para saber en qué habitación cae una araña al soltarla.
    private readonly List<Bounds> roomFloors = new List<Bounds>();

    // Ninguna araña elige un destino a menos de esto (m) del centro de una puerta de paso: antes una podía quedarse
    // quieta en el hueco de la puerta y no dejaba pasar.
    private const float DoorClearance = 1.0f;
    private readonly List<Vector3> doorways = new List<Vector3>();

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
        requestedCount = Level5SpiderSettings.Level5SpiderCount(SessionManager.Instance != null ? SessionManager.Instance.PhobiaGrade : 0);

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

        doorways.Clear();
        foreach (Transform t in all.Where(t => t.name == "door_frame"))
        {
            Renderer r = t.GetComponentInChildren<Renderer>();
            doorways.Add(r != null ? r.bounds.center : t.position);
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
        public Bounds? room;
    }

    private bool IgnoredForReachability(Collider collider, Transform rig)
    {
        if (collider == null) return true;
        if (rig != null && collider.transform.IsChildOf(rig)) return true;
        if (collider.GetComponentInParent<NPNaturalSpiderPatrol>() != null) return true;
        // Las puertas de paso (cuarto y baño) se pueden abrir: no separan zonas (si no, el baño quedaba fuera y no
        // podía tener araña).
        Level5HingeHandle leaf = collider.GetComponentInParent<Level5HingeHandle>();
        if (leaf != null && leaf.name == "door_body") return true;
        Level5TaskInteraction task = collider.GetComponentInParent<Level5TaskInteraction>();
        return task != null && task.taskId == DoorTaskId;
    }

    // Habitaciones del apartamento (pisos de la escena) y el punto de cada una junto al que se pone su araña.
    private static readonly (string floor, string room)[] Rooms =
    {
        ("Piso_Sala", "sala"), ("Piso_Cocina", "cocina"), ("Piso_Cuarto", "cuarto"), ("Piso_Bano", "baño")
    };

    private Vector3? RoomAnchor(string room, Transform[] all, Bounds floor)
    {
        switch (room)
        {
            case "sala":
                Level5LampSwitch lamp = FindFirstObjectByType<Level5LampSwitch>();
                return lamp != null ? lamp.transform.position : (Vector3?)null;
            case "cocina":
                return TaskPoint("TASK_FRIDGE");
            case "cuarto":
                return TaskPoint("TASK_WINDOW");
            default:
                return floor.center;
        }
    }

    private static Vector3? TaskPoint(string id)
    {
        Level5TaskInteraction task = FindObjectsByType<Level5TaskInteraction>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(t => t.taskId == id);
        if (task == null) return null;
        Collider collider = task.GetComponent<Collider>();
        return collider != null && collider.enabled ? collider.bounds.center : task.transform.position;
    }

    private List<SpiderSpawn> PlanSpawns(Transform[] all, Vector3 start)
    {
        var plan = new List<SpiderSpawn>();
        var taken = new List<Vector3>();
        float minSpawn = settings.minSpawnDistanceFromPlayer;

        bool allFollow = settings.followMode == Level5SpiderSettings.FollowMode.AllAlways;
        bool allNearby = settings.followMode == Level5SpiderSettings.FollowMode.AllNearby;
        bool original = settings.followMode == Level5SpiderSettings.FollowMode.Original;

        // 1) Una araña por habitación (sala, cocina, cuarto y baño), cerca del objeto de la rutina de esa habitación,
        //    para que toda la casa tenga arañas (antes, con grado 2, solo había dos y las dos en el cuarto).
        //    La de la habitación donde empieza el jugador lo acompaña según el modo de seguimiento; las demás vigilan
        //    su habitación y lo siguen cuando está cerca.
        foreach ((string floorName, string room) in Rooms)
        {
            if (plan.Count >= requestedCount) break;
            Transform floorTransform = all.FirstOrDefault(t => t.name == floorName);
            Renderer floorRenderer = floorTransform != null ? floorTransform.GetComponentInChildren<Renderer>() : null;
            if (floorRenderer == null) continue;
            Bounds floor = floorRenderer.bounds;
            floor.Expand(new Vector3(-0.8f, 10f, -0.8f));   // lejos de las paredes
            roomFloors.Add(floor);
            Vector3 wanted = RoomAnchor(room, all, floorRenderer.bounds) ?? floor.center;

            Vector3? best = grid.Points()
                .Where(p => floor.Contains(new Vector3(p.x, floor.center.y, p.z)))
                .Where(p => Flat(p - start).magnitude >= minSpawn)
                .Where(p => !taken.Any(q => Flat(q - p).magnitude < settings.minSpacingBetweenSpiders))
                .OrderBy(p => Flat(p - wanted).magnitude)
                .Select(p => (Vector3?)p)
                .FirstOrDefault();
            if (!best.HasValue) continue;

            bool playerRoom = floorRenderer.bounds.Contains(new Vector3(start.x, floorRenderer.bounds.center.y, start.z));
            plan.Add(new SpiderSpawn
            {
                position = best.Value,
                followsPlayer = allFollow || (original && playerRoom),
                followsNearby = allNearby || (original && !playerRoom),
                leash = playerRoom ? float.MaxValue : settings.taskLeash,
                room = floor
            });
            taken.Add(best.Value);
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
            if (c.patrol == null || c.Held) continue;
            c.goal.position = ChooseGoal(c, player);
        }
    }

    // Distancia mínima: ninguna araña ENTRA por su cuenta a menos de minDistanceToPlayer (si su paso de este frame la
    // acercaría, se deshace). Si es la persona la que se acerca, la araña no reacciona a su cuerpo: no se la empuja ni
    // se la desplaza; se queda quieta mientras la persona esté dentro de esa distancia.
    // (Antes se la teletransportaba fuera del radio, y en el visor parecía que se podía empujar a la araña.)
    private void LateUpdate()
    {
        if (playerCamera == null || !Ready) return;
        Vector3 player = playerCamera.transform.position;
        float min = settings.minDistanceToPlayer;

        foreach (Companion c in companions)
        {
            if (c.patrol == null) continue;
            if (c.Held) { c.hasLastPosition = false; continue; }   // en la mano: las reglas de distancia no la mueven
            Transform spider = c.patrol.transform;
            Vector3 now = spider.position;
            float distanceNow = Flat(now - player).magnitude;
            if (c.hasLastPosition && distanceNow < min)
            {
                float distanceBefore = Flat(c.lastPosition - player).magnitude;
                if (distanceNow < distanceBefore - 0.0005f && Flat(now - c.lastPosition).magnitude > 0.0001f)
                {
                    // Fue la araña la que se acercó: vuelve a donde estaba.
                    spider.position = c.lastPosition;
                    now = c.lastPosition;
                }
                c.goal.position = now;
            }

            // Separación entre arañas: ninguna se mete a menos de minSpacingBetweenSpiders de otra (antes no se evitaban
            // y las que seguían al jugador iban al mismo punto, una encima de otra). Si su paso la acercaría, se deshace
            // y elige otro destino.
            if (c.hasLastPosition && Flat(now - c.lastPosition).magnitude > 0.0001f && StepCrowdsAnother(c, c.lastPosition, now))
            {
                spider.position = c.lastPosition;
                now = c.lastPosition;
                c.goal.position = now;
                c.nextWander = 0f;
            }
            c.lastPosition = now;
            c.hasLastPosition = true;
        }
    }

    // ¿Ir de «from» a «to» deja a la araña más cerca de otra y dentro de la separación mínima?
    private bool StepCrowdsAnother(Companion self, Vector3 from, Vector3 to)
    {
        float spacing = settings.minSpacingBetweenSpiders;
        foreach (Companion other in companions)
        {
            if (other == self || other.patrol == null) continue;
            Vector3 position = other.patrol.transform.position;
            float after = Flat(to - position).magnitude;
            if (after < spacing && after < Flat(from - position).magnitude - 0.0005f) return true;
        }
        return false;
    }

    /// <summary>Último punto válido de cada araña (para las pruebas).</summary>
    public bool IsHolding(Transform spider)
    {
        Companion c = companions.FirstOrDefault(x => x.patrol != null && x.patrol.transform == spider);
        return c != null && (c.goal.position - spider.position).sqrMagnitude < 0.0001f;
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

        Vector3 followTarget = spider;
        if (follow && distance > stopDistance)
        {
            // Se detiene a unos metros del jugador, sin tocarlo.
            followTarget = player - toPlayer.normalized * stopDistance;
            Vector3 fromAnchor = followTarget - c.anchor;
            fromAnchor.y = 0f;
            if (fromAnchor.magnitude > c.leash) followTarget = c.anchor + fromAnchor.normalized * c.leash;
            // Solo sigue si tiene camino libre hacia el jugador (misma habitación, sin muebles en medio). Si no, deambula
            // por su sitio: antes se quedaban quietas intentando atravesar la pared hacia el jugador.
            followTarget.y = spider.y;
            if (!PathClear(c, spider, followTarget) || NearDoorway(followTarget)) follow = false;
        }

        if (follow && distance > stopDistance)
        {
            desired = followTarget;
        }
        else if (follow)
        {
            // Ya está a la distancia de parada: descansa un momento y luego cambia de sitio (sin acercarse más).
            if (Time.time >= c.nextWander)
            {
                c.goal.position = PickWanderPoint(c, player, stopDistance);
                c.nextWander = Time.time + Random.Range(3f, 6f);
            }
            desired = c.goal.position;
        }
        else
        {
            bool goalOutside = c.room.HasValue
                ? !c.room.Value.Contains(new Vector3(c.goal.position.x, c.room.Value.center.y, c.goal.position.z))
                : (c.goal.position - c.anchor).magnitude > settings.wanderRadius * 2f + 0.1f;
            if (Time.time >= c.nextWander || goalOutside || NearDoorway(spider))
            {
                c.goal.position = PickWanderPoint(c, player);
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

        // Ni un destino pegado a otra araña: la que deambula busca otro punto; la que sigue al jugador espera.
        if (StepCrowdsAnother(c, spider, desired))
        {
            c.nextWander = 0f;
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
                if (hit.distance <= 0f) continue;   // ya rozando algo al empezar
                Transform other = hit.collider.transform;
                if (other.IsChildOf(c.patrol.transform) || hit.normal.y > 0.7f) continue;
                if (other.GetComponentInParent<NPNaturalSpiderPatrol>() != null || other.GetComponentInParent<CharacterController>() != null) continue;
                c.nextWander = 0f;
                return spider;
            }
        }

        return desired;
    }

    // Several tries around its spot: reachable floor, a straight path free for its body, away from the player and
    // from the other spiders. With a single random try, a spider among furniture kept picking blocked points and
    // stood still (two of the three in the bedroom and kitchen did not move at all).
    private Vector3 PickWanderPoint(Companion c, Vector3 player, float minFromPlayer = -1f)
    {
        Vector3 spider = c.patrol.transform.position;
        float keepAway = Mathf.Max(settings.minDistanceToPlayer, minFromPlayer);
        for (int attempt = 0; attempt < 16; attempt++)
        {
            Vector3 candidate;
            if (c.room.HasValue)
            {
                Bounds room = c.room.Value;
                candidate = new Vector3(Random.Range(room.min.x, room.max.x), spider.y, Random.Range(room.min.z, room.max.z));
            }
            else
            {
                Vector2 offset = Random.insideUnitCircle * settings.wanderRadius;
                candidate = c.anchor + new Vector3(offset.x, 0f, offset.y);
            }
            if (grid != null && !grid.IsReachable(candidate))
            {
                if (!grid.TryNearest(candidate, out candidate)) continue;
            }
            candidate.y = spider.y;
            if (Flat(candidate - spider).magnitude < 0.5f) continue;
            if (Flat(candidate - player).magnitude < keepAway) continue;
            if (NearDoorway(candidate)) continue;
            if (StepCrowdsAnother(c, spider, candidate)) continue;
            if (!PathClear(c, spider, candidate)) continue;
            return candidate;
        }
        return spider;
    }

    private void OnSpiderLanded(Companion c, Vector3 position)
    {
        c.goal.position = position;
        c.anchor = position;
        c.hasLastPosition = false;
        c.nextWander = Time.time + Random.Range(1f, 2.5f);
        foreach (Bounds floor in roomFloors)
        {
            if (floor.Contains(new Vector3(position.x, floor.center.y, position.z))) { c.room = floor; return; }
        }
    }

    private bool NearDoorway(Vector3 point)
    {
        foreach (Vector3 door in doorways)
            if (Flat(point - door).magnitude < DoorClearance) return true;
        return false;
    }

    private static bool PathClear(Companion c, Vector3 from, Vector3 to)
    {
        Vector3 path = to - from;
        path.y = 0f;
        if (path.magnitude < 0.05f) return true;
        Vector3 eye = from + Vector3.up * c.patrol.obstacleHeight;
        foreach (RaycastHit hit in Physics.SphereCastAll(eye, c.patrol.obstacleRadius, path.normalized, path.magnitude, ~0, QueryTriggerInteraction.Ignore))
        {
            // Ya rozando algo al empezar (distancia 0): no cuenta, o una araña junto a un mueble no se movía nunca.
            if (hit.distance <= 0f) continue;
            Transform other = hit.collider.transform;
            if (other.IsChildOf(c.patrol.transform) || hit.normal.y > 0.7f) continue;
            if (other.GetComponentInParent<NPNaturalSpiderPatrol>() != null || other.GetComponentInParent<CharacterController>() != null) continue;
            return false;
        }
        return true;
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
        // Parada, quieta: antes movía las patas en el sitio sin desplazarse y parecía atascada.
        patrol.idleAnimationSpeed = 0f;
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
            leash = spawn.leash,
            room = spawn.room
        });

        // Se puede agarrar con la mano; al soltarla sigue caminando por la habitación donde cae.
        Companion created = companions[companions.Count - 1];
        created.grabbable = Level5GrabbableSpider.Attach(patrol);
        created.grabbable.Landed += (grabbable, position) => OnSpiderLanded(created, position);

        // El cuerpo del jugador no choca con las arañas: una araña en un paso estrecho ya no le cierra el camino.
        Unity.XR.CoreUtils.XROrigin rigOrigin = FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
        CharacterController body = rigOrigin != null ? rigOrigin.GetComponent<CharacterController>() : null;
        if (body != null)
            foreach (Collider spiderCollider in clone.GetComponentsInChildren<Collider>(true))
                Physics.IgnoreCollision(body, spiderCollider, true);
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
