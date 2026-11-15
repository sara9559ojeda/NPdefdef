using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Convierte el apartamento del Nivel5 en una casa con la que se interactúa físicamente:
/// - Todas las puertas de muebles (armario, nevera, gabinetes, fregadero, horno) empiezan cerradas y se abren
///   agarrándolas y girándolas (<see cref="Level5HingeHandle"/>); todos los cajones empiezan cerrados y se deslizan
///   (<see cref="Level5SlideHandle"/>). Las hojas y cajones tienen collider y son sólidos.
/// - Los marcos de las puertas de paso dejan de ser una caja maciza: quedan dos jambas y el dintel, y la hoja
///   (con su collider) es la que bloquea el paso cuando está cerrada.
/// - La ventana se abre deslizando el cristal hacia arriba; detrás hay un exterior (edificios y suelo) y una barrera
///   invisible impide salir por el hueco.
/// - Giro por pasos y desplazamiento continuo con el stick.
/// Ninguna tarea se completa por proximidad, por mirar ni por atravesar un trigger: las tareas las evalúa
/// <see cref="Level5TaskInteraction"/> a partir del estado físico de estas piezas.
/// Se crea solo en Nivel5; no modifica la escena guardada.
/// </summary>
public sealed class Level5InteractiveHome : MonoBehaviour
{
    public const string ApartmentName = "NP_L5_APARTAMENTO_VISUAL";
    public const string WindowName = "NP_L5_WINDOW_VISUAL";
    public const string WindowGlassName = "WindowGlass2m";
    public const float WindowTravel = 0.62f;

    // Puertas de paso: la de la entrada queda cerrada y bloqueada (es la salida del apartamento).
    private static readonly string[] LockedDoors = { "Puerta principal" };

    private static readonly List<Level5HingeHandle> hinges = new List<Level5HingeHandle>();
    private static readonly List<Level5SlideHandle> slides = new List<Level5SlideHandle>();

    public static IReadOnlyList<Level5HingeHandle> Hinges => hinges;
    public static IReadOnlyList<Level5SlideHandle> Slides => slides;
    public static Level5SlideHandle WindowSlide { get; private set; }
    public static Vector3 WindowOutside { get; private set; }
    public static bool Ready { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Ready = false;
        hinges.Clear();
        slides.Clear();
        WindowSlide = null;
        if (scene.name != "Nivel5") return;
        if (FindFirstObjectByType<Level5InteractiveHome>() != null) return;
        new GameObject("NP_Level5_InteractiveHome").AddComponent<Level5InteractiveHome>();
    }

    private void Awake()
    {
        Build();
    }

    /// <summary>Monta la casa. Idempotente: se puede llamar más de una vez.</summary>
    public static void Build()
    {
        if (Ready) return;
        GameObject apartment = GameObject.Find(ApartmentName);
        if (apartment == null) return;

        // Las tareas antiguas por proximidad o mirada quedan fuera (sus componentes se desactivan, no se borran).
        foreach (Level5WindowGaze gaze in FindObjectsByType<Level5WindowGaze>(FindObjectsInactive.Include, FindObjectsSortMode.None)) gaze.enabled = false;

        // El XRSimpleInteractable antiguo de la raíz de cada tarea (puerta, nevera, lámpara) se quedaba con TODOS los
        // colliders de sus hijos, incluidas las hojas: el interactable propio de cada hoja no podía registrarlos y el
        // rayo del mando no la detectaba. Se quita antes de montar las piezas.
        foreach (Level5TaskInteraction task in FindObjectsByType<Level5TaskInteraction>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            XRSimpleInteractable root = task.GetComponent<XRSimpleInteractable>();
            if (root != null) DestroyImmediate(root);
        }

        Transform[] all = apartment.GetComponentsInChildren<Transform>(true).ToArray();
        // Primero los marcos (para que, al elegir el sentido de apertura, la caja maciza ya no estorbe) y los cajones.
        foreach (Transform t in all.Where(t => t.name == "door_frame")) MakeFrameHollow(t);
        foreach (Transform t in all.Where(t => IsDrawer(t.name.ToLowerInvariant()))) MakeDrawer(t);
        foreach (Transform t in all)
        {
            string name = t.name.ToLowerInvariant();
            // La puerta del horno se abre hacia abajo (eje horizontal): no es de ninguna tarea y queda cerrada y sólida.
            if (name == "stove_door") { Vector3 e = t.localEulerAngles; t.localEulerAngles = new Vector3(e.x, 0f, e.z); EnsureSolidCollider(t); continue; }
            if (IsLeaf(name) && !IsLocked(t)) MakeHinge(t, null, 90f, closeFirst: name != "door_body");
        }

        SetUpWindow(apartment.transform);
        SetUpBathTask();
        if (!RestOnSurface(apartment.transform, "Accesorio_Jabon_Bano"))
            PlaceOnCounter(apartment.transform, "Accesorio_Jabon_Bano", "Lavamanos_Bano");
        UseSnapTurn();
        foreach (Level5HingeHandle hinge in hinges) Level5HoverHighlight.Attach(hinge.gameObject);
        foreach (Level5SlideHandle slide in slides) Level5HoverHighlight.Attach(slide.gameObject);
        if (WindowSlide != null) Level5HoverHighlight.Attach(WindowSlide.gameObject);
        Ready = true;
    }

    public static bool IsLeaf(string name)
    {
        // Los cajones de los gabinetes se llaman «storage1_door1..3»: son cajones, nunca hojas con bisagra.
        if (IsDrawer(name)) return false;
        if (name.EndsWith("_body") && name != "door_body") return false;
        if (name.Contains("drawer") || name.Contains("dresser_dresser") || name.Contains("frame") || name.Contains("hinge")) return false;
        if (name == "door_body") return true;
        return name.Contains("_door") || name.EndsWith("leftdoor") || name.EndsWith("rightdoor");
    }

    public static bool IsDrawer(string name)
    {
        if (name.EndsWith("_body")) return false;
        return name.Contains("_drawer") || name.Contains("dresser_dresser") ||
               // Los «door1..3» del gabinete storage1 son en realidad cajones (anchos y bajos).
               (name.StartsWith("storage1_door") && name != "storage1_door4");
    }

    public static bool IsLocked(Transform t)
    {
        for (Transform p = t; p != null; p = p.parent)
            if (LockedDoors.Any(l => p.name.Trim() == l)) return true;
        return false;
    }

    // ------------------------------------------------------------------ bisagras

    /// <summary>
    /// Añade a la hoja collider sólido, XRSimpleInteractable y bisagra. Si <paramref name="closeFirst"/>, la hoja de mueble
    /// se cierra (giro local 0, la pose cerrada de los modelos de nappin). El sentido de apertura es el que aleja la hoja
    /// del cuerpo del mueble.
    /// </summary>
    public static Level5HingeHandle MakeHinge(Transform leaf, Transform pivot, float maxAngle, bool closeFirst)
    {
        Level5HingeHandle existing = leaf.GetComponent<Level5HingeHandle>();
        if (existing != null) return existing;

        if (closeFirst)
        {
            Vector3 e = leaf.localEulerAngles;
            leaf.localEulerAngles = new Vector3(e.x, 0f, e.z);
        }

        EnsureSolidCollider(leaf);
        XRSimpleInteractable interactable = leaf.GetComponent<XRSimpleInteractable>();
        if (interactable == null) interactable = leaf.gameObject.AddComponent<XRSimpleInteractable>();

        Level5HingeHandle hinge = leaf.gameObject.AddComponent<Level5HingeHandle>();
        hinge.leaf = leaf;
        hinge.pivot = pivot;
        // Puertas de las habitaciones («puerta sala a cuarto», «puerta cuarto a baño»): abren hacia dentro del cuarto al
        // que llevan y se abren o cierran con un toque. (Antes el sentido salía de un desempate casi al azar y había que
        // arrastrar la mano en arco.)
        // Passage doors hinge on the edge opposite the handle (see TryPassageDoorHinge), whatever pivot the scene has.
        bool passageDoor = TryPassageDoorHinge(leaf, out Vector3 doorHinge);
        Vector3 hingePoint = passageDoor ? doorHinge : pivot != null ? pivot.position : leaf.position;
        if (passageDoor) hinge.SetHingePoint(doorHinge);
        bool roomDoor = TryRoomDoorSign(leaf, hingePoint, maxAngle, out float roomSign);
        hinge.openAngle = Mathf.Abs(maxAngle) * (roomDoor ? roomSign : OpeningSign(leaf, pivot, maxAngle));
        hinge.toggleOnPress = roomDoor;
        hinge.Initialize();
        hinges.Add(hinge);
        return hinge;
    }

    /// <summary>
    /// Hinge point of a passage door leaf ("door_body" of the nappin door model). The origin of that model sits on
    /// the edge that carries the handle, so turning around it made the door open from the wrong side. The hinge is
    /// the opposite vertical edge of the leaf: the one farther from the model origin along the leaf's width.
    /// Returns false for anything that is not a passage door leaf.
    /// </summary>
    public static bool TryPassageDoorHinge(Transform leaf, out Vector3 hinge)
    {
        hinge = leaf.position;
        if (leaf.name != "door_body") return false;
        BoxCollider box = leaf.GetComponent<BoxCollider>();
        if (box == null) return false;

        bool widthOnZ = box.size.z >= box.size.x;
        float centre = widthOnZ ? box.center.z : box.center.x;
        float half = (widthOnZ ? box.size.z : box.size.x) * 0.5f;
        float farEdge = Mathf.Abs(centre + half) >= Mathf.Abs(centre - half) ? centre + half : centre - half;

        Vector3 local = widthOnZ ? new Vector3(box.center.x, 0f, farEdge) : new Vector3(farEdge, 0f, box.center.z);
        hinge = leaf.TransformPoint(local);
        return true;
    }

    // Pisos de cada cuarto (nombres de la escena) según la palabra del destino en «puerta X a Y».
    private static readonly (string word, string floor)[] RoomFloors =
    {
        ("cuarto", "Piso_Cuarto"), ("baño", "Piso_Bano"), ("bano", "Piso_Bano"), ("sala", "Piso_Sala"), ("cocina", "Piso_Cocina")
    };

    /// <summary>
    /// Sentido de una puerta de habitación: el que deja la hoja abierta del lado del cuarto al que lleva
    /// (el «Y» de «puerta X a Y»), es decir, se empuja al entrar. Devuelve false si la hoja no es de una puerta de paso
    /// con ese nombre o no se encuentra el piso del cuarto.
    /// </summary>
    public static bool TryRoomDoorSign(Transform leaf, Vector3 hinge, float angle, out float sign)
    {
        sign = 1f;
        if (leaf.name != "door_body") return false;
        string destination = null;
        for (Transform p = leaf.parent; p != null && destination == null; p = p.parent)
        {
            string name = p.name.Trim().ToLowerInvariant();
            int split = name.LastIndexOf(" a ", System.StringComparison.Ordinal);
            if (name.StartsWith("puerta ") && split > 0) destination = name.Substring(split + 3).Trim();
        }
        if (destination == null) return false;

        string floorName = RoomFloors.Where(r => r.word == destination).Select(r => r.floor).FirstOrDefault();
        Transform floor = floorName != null
            ? FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(t => t.name == floorName)
            : null;
        Renderer floorRenderer = floor != null ? floor.GetComponentInChildren<Renderer>() : null;
        if (floorRenderer == null) return false;

        // Normal horizontal de la hoja cerrada (su eje más delgado) y lado en el que está el cuarto de destino.
        BoxCollider box = leaf.GetComponent<BoxCollider>();
        Vector3 thin = box != null && box.size.z < box.size.x ? leaf.forward : leaf.right;
        Vector3 normal = Flat(thin).normalized;
        float roomSide = Mathf.Sign(Vector3.Dot(Flat(floorRenderer.bounds.center - hinge), normal));

        Vector3 closed = LeafCenter(leaf);
        Vector3 plusCenter = Quaternion.AngleAxis(Mathf.Abs(angle), Vector3.up) * (closed - hinge) + hinge;
        sign = Mathf.Sign(Vector3.Dot(Flat(plusCenter - hinge), normal)) == roomSide ? 1f : -1f;
        return true;
    }

    // Prueba los dos sentidos: gana el que, abierto, choca con menos cosas (el cuerpo del mueble, una pared);
    // si empatan, el que deja el centro de la hoja más lejos del cuerpo del mueble o del marco.
    private static float OpeningSign(Transform leaf, Transform pivot, float angle)
    {
        // Mueble (la hoja tiene un «_body» hermano): la hoja abierta debe quedar DELANTE del mueble, nunca dentro.
        // El frente es la cara del cuerpo en la que está la hoja cerrada.
        Renderer body = BodyRenderer(leaf);
        if (body != null)
        {
            Vector3 front = FrontOf(body.bounds, LeafCenter(leaf));
            Vector3 hingePoint = pivot != null ? pivot.position : leaf.position;
            Vector3 closed = LeafCenter(leaf);
            Vector3 plusCenter = Quaternion.AngleAxis(Mathf.Abs(angle), Vector3.up) * (closed - hingePoint) + hingePoint;
            Vector3 minusCenter = Quaternion.AngleAxis(-Mathf.Abs(angle), Vector3.up) * (closed - hingePoint) + hingePoint;
            float plusGain = Vector3.Dot(plusCenter - closed, front);
            float minusGain = Vector3.Dot(minusCenter - closed, front);
            if (Mathf.Abs(plusGain - minusGain) > 0.01f) return plusGain > minusGain ? 1f : -1f;
        }

        Physics.SyncTransforms();
        int plus = OverlapsWhenOpen(leaf, pivot, Mathf.Abs(angle));
        int minus = OverlapsWhenOpen(leaf, pivot, -Mathf.Abs(angle));
        if (plus != minus) return plus < minus ? 1f : -1f;

        Vector3 reference = BodyCenter(leaf);
        Vector3 hinge = pivot != null ? pivot.position : leaf.position;
        Vector3 center = LeafCenter(leaf);
        Vector3 a = Quaternion.AngleAxis(Mathf.Abs(angle), Vector3.up) * (center - hinge) + hinge;
        Vector3 b = Quaternion.AngleAxis(-Mathf.Abs(angle), Vector3.up) * (center - hinge) + hinge;
        return Flat(a - reference).magnitude >= Flat(b - reference).magnitude ? 1f : -1f;
    }

    private static Renderer BodyRenderer(Transform leaf)
    {
        if (leaf.parent == null) return null;
        Transform body = leaf.parent.Cast<Transform>().FirstOrDefault(c => c != leaf && c.name.ToLowerInvariant().EndsWith("_body") && c.name != "door_body");
        return body != null ? body.GetComponent<Renderer>() : null;
    }

    // Cara del cuerpo (±x o ±z del mundo; los muebles van alineados con los ejes) en la que está la hoja cerrada.
    private static Vector3 FrontOf(Bounds body, Vector3 leafCenter)
    {
        Vector3 offset = leafCenter - body.center;
        float x = Mathf.Abs(offset.x) / Mathf.Max(0.01f, body.extents.x);
        float z = Mathf.Abs(offset.z) / Mathf.Max(0.01f, body.extents.z);
        return x >= z ? new Vector3(Mathf.Sign(offset.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(offset.z));
    }

    private static int OverlapsWhenOpen(Transform leaf, Transform pivot, float angle)
    {
        BoxCollider box = leaf.GetComponent<BoxCollider>();
        if (box == null) return 0;
        Vector3 hinge = pivot != null ? pivot.position : leaf.position;
        Quaternion turn = Quaternion.AngleAxis(angle * 0.9f, Vector3.up);
        Vector3 center = turn * (leaf.TransformPoint(box.center) - hinge) + hinge;
        Vector3 half = Vector3.Scale(box.size, Abs(leaf.lossyScale)) * 0.45f;
        Collider[] hits = Physics.OverlapBox(center, half, turn * leaf.rotation, ~0, QueryTriggerInteraction.Ignore);
        int count = 0;
        foreach (Collider hit in hits)
        {
            if (hit.transform == leaf || hit.transform.IsChildOf(leaf)) continue;
            if (hit is CharacterController || hit.GetComponentInParent<Unity.XR.CoreUtils.XROrigin>() != null) continue;
            count++;
        }
        return count;
    }

    private static Vector3 LeafCenter(Transform leaf)
    {
        Renderer renderer = leaf.GetComponent<Renderer>();
        return renderer != null ? renderer.bounds.center : leaf.position;
    }

    private static Vector3 BodyCenter(Transform leaf)
    {
        Transform parent = leaf.parent;
        if (parent != null)
        {
            Transform body = parent.Cast<Transform>().FirstOrDefault(c => c != leaf &&
                (c.name.ToLowerInvariant().EndsWith("_body") || c.name == "door_frame"));
            if (body != null && body.GetComponent<Renderer>() != null) return body.GetComponent<Renderer>().bounds.center;
        }
        return leaf.position + leaf.forward;
    }

    // ------------------------------------------------------------------ cajones

    /// <summary>
    /// Cierra el cajón a ras del frente del mueble y lo hace deslizable hacia fuera.
    /// Eje de apertura: aquel (x o z del padre) en el que difieren las posiciones de los cajones hermanos (los modelos
    /// traen unos cajones más fuera que otros); si todos coinciden, el fondo del mueble (su lado horizontal más corto).
    /// Sentido: el lado por el que sobresalen. El cajón se desplaza para que su cara quede a ras de la del mueble.
    /// </summary>
    public static Level5SlideHandle MakeDrawer(Transform drawer)
    {
        Level5SlideHandle existing = drawer.GetComponent<Level5SlideHandle>();
        if (existing != null) return existing;
        Transform parent = drawer.parent;
        Transform body = parent != null ? parent.Cast<Transform>().FirstOrDefault(c => c.name.ToLowerInvariant().EndsWith("_body")) : null;
        Vector3 axis = Vector3.forward;
        float depth = 0.3f;
        if (body != null && TryLocalBounds(body, out Bounds bodyBounds) && TryLocalBounds(drawer, out Bounds drawerBounds))
        {
            Transform[] siblings = parent.Cast<Transform>().Where(c => IsDrawer(c.name.ToLowerInvariant()) &&
                Quaternion.Angle(c.localRotation, drawer.localRotation) < 1f).ToArray();
            float spreadX = siblings.Length > 1 ? siblings.Max(c => c.localPosition.x) - siblings.Min(c => c.localPosition.x) : 0f;
            float spreadZ = siblings.Length > 1 ? siblings.Max(c => c.localPosition.z) - siblings.Min(c => c.localPosition.z) : 0f;
            // Variación «de apertura»: pequeña (menos que el fondo del cajón); una variación grande es la fila de cajones.
            float maxOpen = Mathf.Max(drawerBounds.size.x, drawerBounds.size.z);
            bool openX = spreadX > 0.01f && spreadX < maxOpen * 0.9f && (spreadX >= spreadZ || spreadZ >= maxOpen * 0.9f);
            bool openZ = spreadZ > 0.01f && spreadZ < maxOpen * 0.9f && !openX;
            Vector3 along = openX ? Vector3.right : openZ ? Vector3.forward
                : (bodyBounds.size.x < bodyBounds.size.z ? Vector3.right : Vector3.forward);
            // Frente = el lado por el que algún cajón hermano sobresale más del mueble (los abiertos sobresalen por delante;
            // por detrás quedan metidos tras el fondo).
            float best = float.MinValue;
            float side = 1f;
            foreach (float s in new[] { 1f, -1f })
            {
                float most = float.MinValue;
                foreach (Transform sibling in siblings)
                    if (TryLocalBounds(sibling, out Bounds b)) most = Mathf.Max(most, Face(b, along * s) - Face(bodyBounds, along * s));
                if (most > best) { best = most; side = s; }
            }
            axis = along * side;
            // Referencia de «cerrado»: la cara del hermano más metido si ese ya está cerrado (sobresale menos de 10 cm,
            // lo que mide su tirador); si todos están abiertos, la cara del mueble.
            float bodyFace = Face(bodyBounds, axis);
            float innermost = float.MaxValue;
            foreach (Transform sibling in siblings)
                if (TryLocalBounds(sibling, out Bounds b)) innermost = Mathf.Min(innermost, Face(b, axis));
            float reference = innermost - bodyFace <= 0.1f ? innermost : bodyFace;
            float protrusion = Face(drawerBounds, axis) - reference;
            depth = Vector3.Dot(drawerBounds.size, Abs(axis));
            if (Mathf.Abs(protrusion) < depth) drawer.localPosition -= axis * protrusion;
        }

        EnsureSolidCollider(drawer);
        if (drawer.GetComponent<XRSimpleInteractable>() == null) drawer.gameObject.AddComponent<XRSimpleInteractable>();
        Level5SlideHandle slide = drawer.gameObject.AddComponent<Level5SlideHandle>();
        slide.part = drawer;
        slide.localAxis = axis;
        slide.travel = Mathf.Clamp(depth * 0.7f, 0.12f, 0.45f);
        slide.Initialize();
        slides.Add(slide);
        return slide;
    }

    private static float Face(Bounds b, Vector3 side) => Vector3.Dot(side, b.center) + Vector3.Dot(Abs(side), b.extents);
    private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

    // Caja del objeto en el espacio de su padre. Se parte de Renderer.bounds (en el mundo): con el static batching,
    // MeshFilter.sharedMesh de un cuerpo estático es la malla combinada del lote y sus límites no sirven.
    private static bool TryLocalBounds(Transform t, out Bounds bounds)
    {
        bounds = default;
        Renderer renderer = t.GetComponent<Renderer>();
        if (renderer == null || t.parent == null) return false;
        Bounds world = renderer.bounds;
        bool first = true;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = world.center + Vector3.Scale(world.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            Vector3 p = t.parent.InverseTransformPoint(corner);
            if (first) { bounds = new Bounds(p, Vector3.zero); first = false; }
            else bounds.Encapsulate(p);
        }
        return true;
    }

    // ------------------------------------------------------------------ marcos y colliders

    private static void EnsureSolidCollider(Transform t)
    {
        Collider collider = t.GetComponent<Collider>();
        if (collider == null)
        {
            if (t.GetComponent<MeshFilter>() == null) return;
            collider = t.gameObject.AddComponent<BoxCollider>();
        }
        collider.isTrigger = false;
        collider.enabled = true;
    }

    // El marco era una caja maciza que tapaba el hueco: se sustituye por jambas y dintel.
    private static void MakeFrameHollow(Transform frame)
    {
        BoxCollider box = frame.GetComponent<BoxCollider>();
        if (box == null || frame.Find("NP_Jamba_Izquierda") != null) return;
        Vector3 size = box.size;
        Vector3 center = box.center;
        bool widthOnX = size.x >= size.z;
        float width = widthOnX ? size.x : size.z;
        float depth = widthOnX ? size.z : size.x;
        const float jamb = 0.08f;
        const float header = 0.12f;
        Vector3 along = widthOnX ? Vector3.right : Vector3.forward;
        Vector3 across = widthOnX ? Vector3.forward : Vector3.right;

        AddBox(frame, "NP_Jamba_Izquierda", center - along * (width / 2f - jamb / 2f), along * jamb + across * depth + Vector3.up * size.y);
        AddBox(frame, "NP_Jamba_Derecha", center + along * (width / 2f - jamb / 2f), along * jamb + across * depth + Vector3.up * size.y);
        AddBox(frame, "NP_Dintel", center + Vector3.up * (size.y / 2f - header / 2f), along * width + across * depth + Vector3.up * header);
        box.enabled = false;
    }

    private static void AddBox(Transform parent, string name, Vector3 localCenter, Vector3 localSize)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        BoxCollider collider = go.AddComponent<BoxCollider>();
        collider.center = localCenter;
        collider.size = localSize;
    }

    // ------------------------------------------------------------------ ventana

    private static void SetUpWindow(Transform apartment)
    {
        Transform window = apartment.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == WindowName);
        if (window == null) return;
        Transform glass = window.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == WindowGlassName);
        Transform wall = window.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "WallWindow2m");

        // El muro de la ventana no tenía collider: se le pone uno con la forma de la malla (respeta el hueco).
        if (wall != null && wall.GetComponent<Collider>() == null && wall.GetComponent<MeshFilter>() != null)
            wall.gameObject.AddComponent<MeshCollider>().sharedMesh = wall.GetComponent<MeshFilter>().sharedMesh;

        if (glass != null)
        {
            EnsureSolidCollider(glass);
            if (glass.GetComponent<XRSimpleInteractable>() == null) glass.gameObject.AddComponent<XRSimpleInteractable>();
            Level5SlideHandle slide = glass.GetComponent<Level5SlideHandle>();
            if (slide == null) slide = glass.gameObject.AddComponent<Level5SlideHandle>();
            slide.part = glass;
            slide.localAxis = glass.parent != null ? glass.parent.InverseTransformDirection(Vector3.up) : Vector3.up;
            slide.travel = WindowTravel;
            slide.Initialize();
            WindowSlide = slide;

            Level5TaskInteraction task = glass.GetComponent<Level5TaskInteraction>();
            if (task == null) task = glass.gameObject.AddComponent<Level5TaskInteraction>();
            task.taskId = "TASK_WINDOW";
            if (task.taskManager == null) task.taskManager = FindFirstObjectByType<TaskManager>();
            task.slide = slide;

            Renderer glassRenderer = glass.GetComponent<Renderer>();
            // El cristal era casi un espejo (metálico 0,77, suavidad 0,97) y solo reflejaba el cielo: se deja como vidrio
            // normal para que se vea el exterior.
            if (glassRenderer != null)
            {
                var block = new MaterialPropertyBlock();
                glassRenderer.GetPropertyBlock(block);
                block.SetFloat("_Metallic", 0f);
                block.SetFloat("_Smoothness", 0.55f);
                glassRenderer.SetPropertyBlock(block);
            }
            Bounds opening = glassRenderer != null ? glassRenderer.bounds : new Bounds(glass.position, Vector3.one);
            Vector3 outside = OutsideDirection(opening, apartment);
            WindowOutside = outside;
            AddWindowBarrier(window, opening, outside);
            Level5WindowExterior.Build(window, opening, outside);
            if (Level5SpiderSettings.Load().windowSpider) AddWindowSpider(glass, opening, outside);
        }
    }

    /// <summary>
    /// Dirección hacia el exterior: la normal horizontal del cristal (su eje más delgado), con el sentido que se aleja
    /// del centro del apartamento.
    /// </summary>
    public static Vector3 OutsideDirection(Bounds glass, Transform apartment)
    {
        Bounds all = new Bounds(glass.center, Vector3.zero);
        foreach (Renderer r in apartment.GetComponentsInChildren<Renderer>()) all.Encapsulate(r.bounds);
        Vector3 toCenter = Flat(all.center - glass.center);
        if (glass.size.x <= glass.size.z) return new Vector3(toCenter.x > 0f ? -1f : 1f, 0f, 0f);
        return new Vector3(0f, 0f, toCenter.z > 0f ? -1f : 1f);
    }

    // Barrera invisible un poco por fuera del cristal: con la ventana abierta no se puede salir por el hueco.
    private static void AddWindowBarrier(Transform window, Bounds opening, Vector3 outside)
    {
        if (window.Find("NP_L5_Ventana_Barrera") != null) return;
        GameObject barrier = new GameObject("NP_L5_Ventana_Barrera");
        barrier.transform.SetParent(window, true);
        barrier.transform.position = opening.center + outside * 0.12f + Vector3.up * WindowTravel * 0.5f;
        BoxCollider box = barrier.AddComponent<BoxCollider>();
        Vector3 size = opening.size + Vector3.up * WindowTravel;
        box.size = new Vector3(Mathf.Abs(outside.x) > 0.5f ? 0.05f : size.x + 0.2f, size.y, Mathf.Abs(outside.z) > 0.5f ? 0.05f : size.z + 0.2f);
        // En la capa «Ignore Raycast»: no tapa el rayo de los mandos ni la mirada.
        barrier.layer = 2;
    }

    // Objetos de adorno que la escena dejó flotando (el jabón del baño): bajan hasta apoyarse en la superficie que
    // tienen debajo. Solo si está cerca (menos de MaxRestDrop), para no mandarlos al suelo si la superficie no tiene collider.
    private const float MaxRestDrop = 0.5f;

    private static bool RestOnSurface(Transform apartment, string objectName)
    {
        Transform item = apartment.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == objectName);
        if (item == null) return false;
        Renderer[] renderers = item.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return false;
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);

        float surface = float.MinValue;
        Vector3 from = new Vector3(bounds.center.x, bounds.min.y + 0.02f, bounds.center.z);
        foreach (RaycastHit hit in Physics.RaycastAll(from, Vector3.down, MaxRestDrop + 0.02f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.IsChildOf(item) || hit.normal.y < 0.7f) continue;
            surface = Mathf.Max(surface, hit.point.y);
        }
        // Sin collider debajo: la parte de arriba del mueble visible que tiene justo debajo (el lavabo, una repisa).
        if (surface == float.MinValue)
        {
            foreach (Renderer below in apartment.GetComponentsInChildren<Renderer>())
            {
                if (below.transform.IsChildOf(item) || !below.enabled) continue;
                Bounds b = below.bounds;
                if (b.size.x > 3f || b.size.z > 3f) continue;   // suelo, paredes
                if (from.x < b.min.x || from.x > b.max.x || from.z < b.min.z || from.z > b.max.z) continue;
                if (b.max.y > bounds.min.y + 0.005f) continue;
                surface = Mathf.Max(surface, b.max.y);
            }
        }
        if (surface == float.MinValue) return false;
        float gap = bounds.min.y - surface;
        if (gap > 0.003f) item.position -= Vector3.up * gap;
        return true;
    }

    // El jabón del baño quedó flotando en medio del cuarto, sin nada debajo: va sobre la encimera del lavamanos.
    // Se muestrea la parte de arriba del mueble y se elige la altura plana más alta que se repite (la encimera, no el
    // grifo ni el fondo de la pila), lo más cerca posible de la pared.
    private static void PlaceOnCounter(Transform apartment, string objectName, string furnitureName)
    {
        Transform[] all = apartment.GetComponentsInChildren<Transform>(true);
        Transform item = all.FirstOrDefault(t => t.name == objectName);
        Transform furniture = all.FirstOrDefault(t => t.name == furnitureName);
        if (item == null || furniture == null) return;
        Collider[] colliders = furniture.GetComponentsInChildren<Collider>();
        Renderer[] renderers = item.GetComponentsInChildren<Renderer>();
        if (colliders.Length == 0 || renderers.Length == 0) return;

        Bounds area = colliders[0].bounds;
        foreach (Collider c in colliders) area.Encapsulate(c.bounds);
        var hits = new List<Vector3>();
        const int steps = 8;
        for (int i = 0; i <= steps; i++)
        {
            for (int j = 0; j <= steps; j++)
            {
                float x = Mathf.Lerp(area.min.x + area.size.x * 0.08f, area.max.x - area.size.x * 0.08f, i / (float)steps);
                float z = Mathf.Lerp(area.min.z + area.size.z * 0.08f, area.max.z - area.size.z * 0.08f, j / (float)steps);
                Ray ray = new Ray(new Vector3(x, area.max.y + 0.5f, z), Vector3.down);
                float best = float.MinValue; Vector3 bestPoint = Vector3.zero; bool flat = false;
                foreach (Collider c in colliders)
                {
                    if (!c.Raycast(ray, out RaycastHit hit, area.size.y + 1f) || hit.point.y <= best) continue;
                    best = hit.point.y; bestPoint = hit.point; flat = hit.normal.y > 0.95f;
                }
                if (best > float.MinValue && flat) hits.Add(bestPoint);
            }
        }
        if (hits.Count == 0) return;

        // Altura de la encimera: la más alta que comparten al menos 4 muestras (±1 cm).
        float counter = hits.Select(h => h.y).OrderByDescending(y => y)
            .FirstOrDefault(y => hits.Count(h => Mathf.Abs(h.y - y) < 0.01f) >= 4);
        if (counter == 0f) counter = hits.Max(h => h.y);
        List<Vector3> top = hits.Where(h => Mathf.Abs(h.y - counter) < 0.01f).ToList();
        // Lo más cerca de la pared más próxima al mueble (el borde de la encimera pegado a la pared).
        Vector3 wallSide = new Vector3(area.center.x, 0f, area.center.z) - new Vector3(item.position.x, 0f, item.position.z);
        Vector3 spot = top.OrderByDescending(h => Vector3.Dot(new Vector3(h.x, 0f, h.z), wallSide.normalized)).First();

        Bounds bounds = renderers[0].bounds;
        foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);
        Vector3 offset = item.position - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        item.position = spot + offset;
    }

    public const string BathTaskId = "TASK_BATH";
    private const string BathDoorRoot = "puerta cuarto a baño";

    // Tarea del baño de la rutina «Antes de dormir»: abrir la puerta del baño para lavarse los dientes. Se cumple
    // solo con la hoja abierta (la misma regla física que la puerta del cuarto). El componente va en un objeto propio:
    // en la hoja borraría el interactable de la bisagra (Level5TaskInteraction.Configure quita el de su objeto).
    private static void SetUpBathTask()
    {
        Level5HingeHandle bathDoor = hinges.FirstOrDefault(h => h != null && h.name == "door_body" &&
            h.transform.parent != null && h.transform.parent.parent != null &&
            h.transform.parent.parent.name.Trim().ToLowerInvariant() == BathDoorRoot);
        if (bathDoor == null || bathDoor.transform.parent.parent.Find("NP_L5_Tarea_Bano") != null) return;
        GameObject holder = new GameObject("NP_L5_Tarea_Bano");
        holder.transform.SetParent(bathDoor.transform.parent.parent, false);
        Level5TaskInteraction task = holder.AddComponent<Level5TaskInteraction>();
        task.taskId = BathTaskId;
        task.hinge = bathDoor;
    }

    public const string WindowSpiderName = "NP_L5_Arana_Ventana";

    // Araña posada en la cara exterior del cristal, con la cabeza hacia arriba; sube con el cristal al abrir.
    // Es un clon quieto de la araña del nivel (sin patrulla ni colliders): no camina ni la empuja nadie.
    private static void AddWindowSpider(Transform glass, Bounds opening, Vector3 outside)
    {
        if (GameObject.Find(WindowSpiderName) != null) return;
        Transform original = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(t => t.name == "NP_Spider_Level5_Central");
        if (original == null) return;
        GameObject spider = Instantiate(original.gameObject);
        spider.name = WindowSpiderName;
        // Mismo tamaño que las demás arañas del nivel (Level5CompanionSpiders escala su visualRoot igual).
        var patrol = spider.GetComponent<NeuroPhobia.Level45.NPNaturalSpiderPatrol>();
        if (patrol != null && patrol.visualRoot != null) patrol.visualRoot.localScale *= Level5SpiderSettings.Load().visualScale;
        foreach (MonoBehaviour behaviour in spider.GetComponentsInChildren<MonoBehaviour>(true))
            if (behaviour.GetType().Name.Contains("Patrol")) DestroyImmediate(behaviour);
        foreach (Collider collider in spider.GetComponentsInChildren<Collider>(true)) DestroyImmediate(collider);
        foreach (Rigidbody body in spider.GetComponentsInChildren<Rigidbody>(true)) body.isKinematic = true;
        // Quieta: sin su patrulla nadie corrige la deriva de la animación de caminar, y el modelo se iba deslizando
        // del cristal a la pared mientras se jugaba.
        foreach (Animator animator in spider.GetComponentsInChildren<Animator>(true)) animator.speed = 0f;
        spider.transform.SetPositionAndRotation(
            new Vector3(opening.center.x, opening.min.y + opening.size.y * 0.35f, opening.center.z) + outside * 0.04f,
            Quaternion.LookRotation(Vector3.up, outside));
        spider.transform.SetParent(glass, true);
        spider.SetActive(true);
    }

    // ------------------------------------------------------------------ locomoción

    /// <summary>Desplazamiento continuo con el stick izquierdo y giro por pasos con el derecho.</summary>
    private static void UseSnapTurn()
    {
        foreach (MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (behaviour.GetType().Name != "ControllerInputActionManager") continue;
            SetBool(behaviour, "smoothTurnEnabled", false);
            SetBool(behaviour, "smoothMotionEnabled", true);
        }
    }

    private static void SetBool(object target, string property, bool value)
    {
        var info = target.GetType().GetProperty(property);
        if (info != null && info.CanWrite) info.SetValue(target, value);
    }

    private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
}
