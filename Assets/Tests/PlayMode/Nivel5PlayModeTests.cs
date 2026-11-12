using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace NeuroPhobia.Tests
{
    /// <summary>
    /// Pruebas PlayMode de Nivel5 (primer ensamblado de pruebas del proyecto).
    /// El código del juego vive en Assembly-CSharp, que un asmdef no puede referenciar,
    /// así que se accede a él por reflexión (nombres de tipo y miembros públicos).
    /// </summary>
    public class Nivel5PlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/Nivel5.unity";

        // El Editor puede emitir errores ajenos al juego (p. ej. Package Manager sin sesión); no deben tumbar las pruebas.
        // Grado de fobia de las pruebas: N arañas en Nivel5 (al menos 2 hacen falta para la prueba de muros y muebles).
        private const int TestPhobiaGrade = 3;

        [SetUp]
        public void IgnoreEditorNoise()
        {
            LogAssert.ignoreFailingMessages = true;
            StartTestSession(TestPhobiaGrade);
        }

        [TearDown]
        public void EndTestSession() { DestroySession(); }

        // Sesión de prueba (testSession = true) con el grado indicado; sin FastAPIClient no envía nada.
        private static object StartTestSession(int grade)
        {
            object session = Static("SessionManager", "Instance");
            if (session == null || (UnityEngine.Object)session == null)
            {
                session = new GameObject("TEST_SessionManager").AddComponent(Game("SessionManager"));
            }
            Call(session, "StartSession", grade, true);
            return session;
        }

        private static void DestroySession()
        {
            object session = Static("SessionManager", "Instance");
            if (session != null && (UnityEngine.Object)session != null) UnityEngine.Object.DestroyImmediate(((Component)session).gameObject);
        }

        // ------------------------------------------------------------------ utilidades

        private static Type Game(string name) => Type.GetType(name + ", Assembly-CSharp", true);

        private static UnityEngine.Object Find(string typeName) =>
            UnityEngine.Object.FindFirstObjectByType(Game(typeName), FindObjectsInactive.Include);

        private static UnityEngine.Object[] FindAll(string typeName) =>
            UnityEngine.Object.FindObjectsByType(Game(typeName), FindObjectsInactive.Include, FindObjectsSortMode.None);

        private static object Get(object target, string member)
        {
            Type type = target.GetType();
            PropertyInfo property = type.GetProperty(member, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            if (property != null) return property.GetValue(target);
            FieldInfo field = type.GetField(member, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            Assert.IsNotNull(field, "No existe el miembro " + member + " en " + type.Name);
            return field.GetValue(target);
        }

        private static void Set(object target, string member, object value)
        {
            FieldInfo field = target.GetType().GetField(member, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "No existe el campo " + member + " en " + target.GetType().Name);
            field.SetValue(target, value);
        }

        private static object Call(object target, string method, params object[] args)
        {
            MethodInfo info = target.GetType().GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(info, "No existe el método " + method);
            return info.Invoke(target, args);
        }

        private static object Static(string typeName, string member)
        {
            Type type = Game(typeName);
            PropertyInfo property = type.GetProperty(member, BindingFlags.Public | BindingFlags.Static);
            return property.GetValue(null);
        }

        private static GameObject Ask(object component) => ((Component)component).gameObject;

        private static IEnumerator LoadLevel()
        {
            Time.timeScale = 1f;
            #if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync("Nivel5");
#endif
            // Deja que arranquen GlobalSessionUI, TaskManager, etc.
            yield return null;
            yield return null;
            yield return SkipSudsBefore();
        }

        // Al empezar el nivel, GlobalSessionUI pide el SUDS «antes» y pausa el juego hasta que se responde.
        // Las pruebas lo contestan con -1 («prefiero no responder») para que el nivel arranque.
        internal static IEnumerator SkipSudsBefore(float timeoutSeconds = 3f)
        {
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < timeoutSeconds)
            {
                UnityEngine.Object ui = UnityEngine.Object.FindFirstObjectByType(Game("GlobalSessionUI"));
                if (ui != null)
                {
                    GameObject panel = (GameObject)Get(ui, "sudsPanel");
                    if (panel != null && panel.activeSelf)
                    {
                        Call(ui, "AnswerSuds", -1);
                        yield return null;
                        yield break;
                    }
                }
                yield return null;
            }
        }

        private static IEnumerator WaitForSpiders(float timeoutSeconds = 60f)
        {
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < timeoutSeconds)
            {
                object spiders = Find("Level5CompanionSpiders");
                if (spiders != null && (bool)Get(spiders, "Ready")) yield break;
                yield return null;
            }
            Assert.Fail("Las arañas de Nivel5 no estuvieron listas en " + timeoutSeconds + " s.");
        }

        private static Transform[] LiveSpiders()
        {
            object spiders = Find("Level5CompanionSpiders");
            Assert.IsNotNull(spiders, "No existe Level5CompanionSpiders.");
            return ((System.Collections.Generic.IEnumerable<Transform>)Get(spiders, "SpiderTransforms")).ToArray();
        }

        private static object Task(string id)
        {
            object task = FindAll("Level5TaskInteraction").FirstOrDefault(t => (string)Get(t, "taskId") == id);
            Assert.IsNotNull(task, "No existe la tarea " + id);
            return task;
        }

        // Simula el gatillo: un interactor selecciona el interactable a través del XRInteractionManager.
        private static void PullTrigger(GameObject interactableObject)
        {
            XRSimpleInteractable interactable = interactableObject.GetComponent<XRSimpleInteractable>();
            Assert.IsNotNull(interactable, interactableObject.name + " no tiene XRSimpleInteractable.");

            var interactorObject = new GameObject("TEST_Interactor");
            var interactor = interactorObject.AddComponent<XRDirectInteractor>();
            interactorObject.AddComponent<SphereCollider>().isTrigger = true;
            interactorObject.transform.position = interactableObject.transform.position;
            interactable.interactionManager.SelectEnter((IXRSelectInteractor)interactor, (IXRSelectInteractable)interactable);
        }

        // Acciones físicas, como las haría la mano: girar la hoja, pulsar el interruptor, deslizar el cristal.
        private static void OpenHinge(object task)
        {
            object hinge = Get(task, "hinge");
            Assert.IsNotNull(hinge, Get(task, "taskId") + " no tiene bisagra.");
            Call(hinge, "SetAngle", (float)Get(hinge, "openAngle"));
        }

        private static void PressLampSwitch()
        {
            object lampSwitch = Get(Task("TASK_LAMP"), "lampSwitch");
            Assert.IsNotNull(lampSwitch, "La lámpara no tiene interruptor.");
            PullTrigger(((Component)lampSwitch).gameObject);
        }

        private static void OpenWindow()
        {
            object slide = Get(Task("TASK_WINDOW"), "slide");
            Assert.IsNotNull(slide, "La ventana no se desliza.");
            Call(slide, "SetOffset", (float)Get(slide, "travel"));
        }

        private static object FinishLevelTasksExceptWindow()
        {
            PressLampSwitch();
            OpenHinge(Task("TASK_DOOR"));
            OpenHinge(Task("TASK_FRIDGE"));
            return Find("TaskManager");
        }

        private static Collider LeafCollider(object task)
        {
            object hinge = Get(task, "hinge");
            Assert.IsNotNull(hinge, "La tarea no tiene bisagra.");
            return ((Component)hinge).GetComponent<Collider>();
        }

        // ------------------------------------------------------------------ pruebas

        [UnityTest]
        public IEnumerator Aranas_EstanApoyadasEnElSuelo_NiTechoNiBajoTierra()
        {
            yield return LoadLevel();
            yield return WaitForSpiders();
            yield return new WaitForSeconds(3f);   // ya han caminado un rato

            Transform[] spiders = LiveSpiders();
            Assert.GreaterOrEqual(spiders.Length, 1, "No hay arañas.");
            foreach (Transform spider in spiders)
            {
                Assert.Less(spider.position.y, 2f, spider.name + " está a " + spider.position.y + " m (¿en el techo?).");
                Assert.Greater(spider.position.y, -0.05f, spider.name + " está bajo el suelo: y=" + spider.position.y);
                Assert.Less(spider.position.y, 0.4f, spider.name + " no está en el suelo: y=" + spider.position.y);
            }
        }

        [UnityTest]
        public IEnumerator Aranas_LaCantidadSigueElGradoDeFobia_YRespetanLaDistanciaDeAparicion()
        {
            yield return LoadLevel();
            yield return WaitForSpiders();

            object manager = Find("Level5CompanionSpiders");
            float minSpawn = (float)Get(Get(manager, "Settings"), "minSpawnDistanceFromPlayer");
            Transform[] spiders = LiveSpiders();
            Assert.AreEqual(TestPhobiaGrade, (int)Get(manager, "RequestedCount"), "Grado " + TestPhobiaGrade + " debe pedir " + TestPhobiaGrade + " arañas.");
            Assert.AreEqual(TestPhobiaGrade, spiders.Length, "La cantidad de arañas no coincide con el grado de fobia.");

            Vector3 start = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
            foreach (Transform spider in spiders)
            {
                Vector3 flat = spider.position - start; flat.y = 0f;
                Assert.GreaterOrEqual(flat.magnitude, minSpawn - 0.6f, spider.name + " apareció demasiado cerca del jugador.");
            }
        }

        [UnityTest]
        public IEnumerator Aranas_SinGradoDeFobia_ApareceUnaSola()
        {
            DestroySession();
            yield return LoadLevel();
            yield return WaitForSpiders();

            Assert.AreEqual(1, (int)Get(Find("Level5CompanionSpiders"), "RequestedCount"), "Sin grado válido se usa 1 araña.");
            Assert.AreEqual(1, LiveSpiders().Length);
        }

        // Las arañas antiguas (CaveSpider de Level5SpawnManager, AranaController) no quedan ocultas con collider,
        // y las métricas se miden contra exactamente las arañas visibles.
        [UnityTest]
        public IEnumerator Aranas_SoloLasVisibles_YMetricasMidenContraEllas()
        {
            yield return LoadLevel();
            yield return WaitForSpiders();

            UnityEngine.Object[] legacy = UnityEngine.Object.FindObjectsByType(Game("AranaController"), FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(spider => !spider.name.StartsWith("NP_"))
                .ToArray();
            Assert.AreEqual(0, legacy.Length, "Quedan arañas antiguas activas: " + string.Join(", ", legacy.Select(spider => spider.name)));

            object collector = Get(Find("TaskManager"), "metricsCollector");
            Transform[] measured = (Transform[])Get(collector, "level5Spiders");
            Assert.IsNotNull(measured, "MetricsCollector no recibió las arañas vivas.");
            CollectionAssert.AreEquivalent(LiveSpiders(), measured, "MetricsCollector debe medir contra las arañas visibles.");
            foreach (Transform spider in measured)
            {
                Assert.IsTrue(spider.gameObject.activeInHierarchy, spider.name + " no está activa.");
                Assert.IsTrue(spider.GetComponentsInChildren<Renderer>().Any(renderer => renderer.enabled), spider.name + " no es visible.");
            }
        }

        [UnityTest]
        public IEnumerator Aranas_LaCabezaApuntaHaciaDondeAvanzan_ToleranciaVeinteGrados()
        {
            yield return LoadLevel();
            yield return WaitForSpiders();

            for (int sample = 0; sample < 6; sample++)
            {
                yield return new WaitForSeconds(0.7f);
                yield return null;
                foreach (Transform spider in LiveSpiders())
                {
                    Transform fang = spider.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Fang.L");
                    Transform back = spider.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Back");
                    Assert.IsNotNull(fang, spider.name + " no tiene el hueso Fang.L");
                    Assert.IsNotNull(back, spider.name + " no tiene el hueso Back");

                    Vector3 head = fang.position - back.position; head.y = 0f;
                    Vector3 forward = spider.forward; forward.y = 0f;
                    float angle = Vector3.Angle(head, forward);
                    Assert.LessOrEqual(angle, 20f, spider.name + ": la cabeza forma " + angle.ToString("0") + "° con el avance (muestra " + sample + ").");

                    // Patas hacia el suelo: el cuerpo queda por encima de las puntas de las patas.
                    float feet = spider.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("LegSegmentF")).Min(t => t.position.y);
                    Assert.Greater(back.position.y, feet, spider.name + ": el cuerpo no está sobre las patas.");
                }
            }
        }

        // Si la persona se acerca a una araña, la araña no reacciona a su cuerpo: no se la empuja ni se la desplaza.
        [UnityTest]
        public IEnumerator Aranas_NoSeLasPuedeEmpujar_AlAcercarseSeQuedanQuietas()
        {
            yield return LoadLevel();
            yield return WaitForSpiders();

            Transform player = Camera.main.transform;
            Transform victim = LiveSpiders()[0];
            Vector3 before = victim.position;
            player.position = victim.position + Vector3.up * 1.6f + victim.forward * 0.3f;
            for (int i = 0; i < 30; i++) { player.position = before + Vector3.up * 1.6f + victim.forward * 0.3f; yield return null; }

            Vector3 moved = victim.position - before; moved.y = 0f;
            Assert.Less(moved.magnitude, 0.05f, victim.name + " se desplazó " + moved.magnitude.ToString("0.00") + " m al acercarse la persona.");
            Rigidbody body = victim.GetComponent<Rigidbody>();
            Assert.IsTrue(body == null || body.isKinematic, "La araña no debe tener física dinámica.");
        }

        // Ninguna araña entra por su cuenta en el radio mínimo de la persona.
        [UnityTest]
        public IEnumerator Aranas_NingunaSeAcercaPorSuCuentaMasDeLaDistanciaMinima()
        {
            yield return LoadLevel();
            yield return WaitForSpiders();

            float min = (float)Get(Get(Find("Level5CompanionSpiders"), "Settings"), "minDistanceToPlayer");
            Transform player = Camera.main.transform;
            var previous = LiveSpiders().ToDictionary(t => t, t => Flat(t.position - player.position));
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < 4f)
            {
                yield return null;
                foreach (Transform spider in LiveSpiders())
                {
                    float now = Flat(spider.position - player.position);
                    if (previous.TryGetValue(spider, out float before) && now < min - 0.02f)
                        Assert.GreaterOrEqual(now, before - 0.001f, spider.name + " se acercó dentro del radio mínimo.");
                    previous[spider] = now;
                }
            }
        }

        private static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }

        [UnityTest]
        public IEnumerator Nivel5_LasCuatroTareasCompletanElNivel()
        {
            yield return LoadLevel();
            yield return WaitForSpiders();

            object manager = FinishLevelTasksExceptWindow();
            yield return null;
            yield return null;
            Assert.AreEqual(3, (int)Get(manager, "CompletedCount"), "Lámpara, puerta y nevera deben completarse al apagarla y abrirlas.");

            OpenWindow();
            float start = Time.realtimeSinceStartup;
            while ((int)Get(manager, "CompletedCount") < 4 && Time.realtimeSinceStartup - start < 10f) yield return null;

            Assert.AreEqual(4, (int)Get(manager, "CompletedCount"), "La ventana debe completarse al abrirla.");
            yield return null;
            Assert.AreEqual("SESSION_END", GameStateManagerState(), "Al terminar debe aparecer el resumen de sesión.");
            Assert.IsFalse((bool)Get(manager, "RepeatOffered"), "Sin alejamientos no debe ofrecerse repetir.");
        }

        [UnityTest]
        public IEnumerator Puerta_AlAbrirseElPasoQuedaLibre()
        {
            yield return LoadLevel();
            yield return WaitForSpiders();

            object door = Task("TASK_DOOR");
            Collider leaf = LeafCollider(door);
            Assert.IsTrue(leaf.enabled && !leaf.isTrigger, "La hoja cerrada debe tener un collider sólido.");

            // Hueco de la puerta: centro de la hoja cerrada.
            Physics.SyncTransforms();
            Vector3 center = leaf.bounds.center;
            Assert.IsTrue(Physics.CheckCapsule(center - Vector3.up * 0.8f, center + Vector3.up * 0.8f, 0.25f, ~0, QueryTriggerInteraction.Ignore),
                "Con la puerta cerrada el hueco debe estar bloqueado.");

            OpenHinge(door);
            yield return null;
            yield return new WaitForFixedUpdate();
            Physics.SyncTransforms();

            Assert.IsTrue((bool)Get(door, "IsActivated"), "Abrir la puerta debe completar la tarea.");
            Assert.IsFalse(Physics.CheckCapsule(center - Vector3.up * 0.8f, center + Vector3.up * 0.8f, 0.25f, ~0, QueryTriggerInteraction.Ignore),
                "Con la puerta abierta el hueco debe quedar libre.");
        }

        [UnityTest]
        public IEnumerator Puerta_SoloSeAbreConElGesto_NoAlAcercarse()
        {
            yield return LoadLevel();
            yield return WaitForSpiders();

            object door = Task("TASK_DOOR");
            Transform player = Camera.main.transform;
            player.position = LeafCollider(door).bounds.center + Vector3.forward * 0.8f;
            yield return new WaitForSeconds(1f);

            Assert.IsFalse((bool)Get(door, "IsActivated"), "La puerta no debe abrirse sola al acercarse.");
            Assert.AreEqual(0, (int)Get(Find("TaskManager"), "CompletedCount"));
        }

        // Ninguna tarea se cumple por proximidad, por mirar ni por apretar el gatillo sin mover nada.
        [UnityTest]
        public IEnumerator Tareas_NoSeCompletanPorProximidadMiradaNiGatilloSinMoverNada()
        {
            yield return LoadLevel();
            yield return WaitForSpiders();
            object manager = Find("TaskManager");
            string[] ids = { "TASK_DOOR", "TASK_FRIDGE", "TASK_LAMP", "TASK_WINDOW" };

            // 1) Proximidad: la cabeza atraviesa cada objeto de tarea y su antiguo trigger.
            Transform head = Camera.main.transform;
            foreach (string id in ids)
            {
                Transform target = Ask(Task(id)).transform;
                for (int i = 0; i < 20; i++) { head.position = target.position + Vector3.up * 0.2f; yield return new WaitForFixedUpdate(); }
            }
            foreach (Component marker in FindAll("SimpleTaskInteractable").Cast<Component>())
            {
                for (int i = 0; i < 10; i++) { head.position = marker.transform.position; yield return new WaitForFixedUpdate(); }
            }
            Assert.AreEqual(0, (int)Get(manager, "CompletedCount"), "Acercarse o atravesar un objeto no debe completar tareas.");

            // 2) Mirada: mirar la ventana fijamente varios segundos.
            Transform glass = Ask(Task("TASK_WINDOW")).transform;
            Vector3 glassCenter = glass.GetComponent<Collider>().bounds.center;
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < 6f)
            {
                head.position = glassCenter + (Vector3.zero - glassCenter).normalized * 1.5f;
                head.rotation = Quaternion.LookRotation(glassCenter - head.position);
                yield return null;
            }
            Assert.IsFalse((bool)Call(manager, "IsCompleted", "TASK_WINDOW"), "Mirar la ventana no debe completar la tarea.");

            // 3) Apuntar y apretar el gatillo sobre puerta, nevera y ventana sin mover la mano.
            PullTrigger(((Component)Get(Task("TASK_DOOR"), "hinge")).gameObject);
            PullTrigger(((Component)Get(Task("TASK_FRIDGE"), "hinge")).gameObject);
            PullTrigger(glass.gameObject);
            for (int i = 0; i < 30; i++) yield return null;
            Assert.AreEqual(0, (int)Get(manager, "CompletedCount"), "Apretar el gatillo sin girar ni deslizar no debe completar tareas.");

            // Los marcadores heredados ya no reaccionan a triggers.
            Assert.IsNull(Game("SimpleTaskInteractable").GetMethod("OnTriggerEnter", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance));
            foreach (Behaviour gaze in FindAll("Level5WindowGaze").Cast<Behaviour>()) Assert.IsFalse(gaze.enabled, "La tarea por mirada debe quedar desactivada.");
        }

        // Agarrar la hoja con el mando y girar la mano alrededor de la bisagra abre la puerta y cumple la tarea.
        [UnityTest]
        public IEnumerator Puerta_SeAbreAgarrandolaYGirandoLaMano()
        {
            yield return LoadLevel();
            yield return WaitForSpiders();

            object door = Task("TASK_DOOR");
            Component hinge = (Component)Get(door, "hinge");
            Transform leaf = hinge.transform;
            Transform pivotTransform = (Transform)Get(hinge, "pivot");
            Vector3 pivot = pivotTransform != null ? pivotTransform.position : leaf.position;
            Vector3 handle = leaf.GetComponent<Collider>().bounds.center;
            float sign = Mathf.Sign((float)Get(hinge, "openAngle"));

            var hand = new GameObject("TEST_Mano");
            var interactor = hand.AddComponent<XRDirectInteractor>();
            hand.AddComponent<SphereCollider>().isTrigger = true;
            hand.transform.position = handle;
            yield return null;
            XRSimpleInteractable interactable = leaf.GetComponent<XRSimpleInteractable>();
            interactable.interactionManager.SelectEnter((IXRSelectInteractor)interactor, (IXRSelectInteractable)interactable);

            for (int step = 1; step <= 40; step++)
            {
                Vector3 offset = handle - pivot;
                hand.transform.position = pivot + Quaternion.AngleAxis(sign * 80f * step / 40f, Vector3.up) * offset;
                yield return null;
            }
            yield return null;

            float fraction = (float)Get(hinge, "OpenFraction");
            Assert.Greater(fraction, 0.6f, "La hoja debe seguir a la mano (abierta " + fraction.ToString("0.00") + ").");
            Assert.IsTrue((bool)Get(door, "IsActivated"), "Abrir la puerta con la mano debe completar la tarea.");
        }

        // Todas las puertas y cajones de muebles: empiezan cerrados, tienen collider sólido y se pueden agarrar.
        [UnityTest]
        public IEnumerator Muebles_PuertasYCajonesCerradosConColliderEInteractuables()
        {
            yield return LoadLevel();
            System.Collections.IList hinges = (System.Collections.IList)Static("Level5InteractiveHome", "Hinges");
            System.Collections.IList slides = (System.Collections.IList)Static("Level5InteractiveHome", "Slides");
            Assert.GreaterOrEqual(hinges.Count, 10, "Faltan puertas interactivas (armario, nevera, gabinetes, puertas de paso).");
            Assert.GreaterOrEqual(slides.Count, 12, "Faltan cajones interactivos (armario, isla, cómoda, gabinetes).");

            foreach (Component piece in hinges.Cast<Component>().Concat(slides.Cast<Component>()))
            {
                Collider collider = piece.GetComponent<Collider>();
                Assert.IsNotNull(collider, piece.name + " no tiene collider.");
                Assert.IsTrue(collider.enabled && !collider.isTrigger, piece.name + " no es sólido.");
                Assert.IsNotNull(piece.GetComponent<XRSimpleInteractable>(), piece.name + " no se puede agarrar.");
                Assert.AreEqual(0f, (float)Get(piece, "OpenFraction"), 0.001f, piece.name + " no empieza cerrado.");
                // Si la malla quedó en un lote estático, Unity no la mueve aunque gire el Transform (se vería cerrada).
                foreach (Renderer renderer in piece.GetComponentsInChildren<Renderer>())
                    Assert.IsFalse(renderer.isPartOfStaticBatch, PathOf(renderer.transform) + " está en un lote estático: no se vería moverse.");
            }
            foreach (Component hinge in hinges.Cast<Component>().Where(h => !h.name.StartsWith("door_body")))
                Assert.AreEqual(0f, Mathf.DeltaAngle(0f, hinge.transform.localEulerAngles.y), 1f, hinge.name + " no está cerrada (giro local).");

            // Los marcos de paso ya no son cajas macizas.
            foreach (Transform frame in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t => t.name == "door_frame"))
            {
                BoxCollider box = frame.GetComponent<BoxCollider>();
                if (box != null) Assert.IsFalse(box.enabled, PathOf(frame) + ": el marco macizo sigue tapando el hueco.");
                Assert.IsNotNull(frame.Find("NP_Jamba_Izquierda"), PathOf(frame) + " sin jambas.");
            }
        }

        // El rayo del mando detecta cada pieza: su collider pertenece a su propio interactable en el XRInteractionManager.
        // (Antes, el interactable antiguo de la raíz de la nevera y de la puerta se quedaba con los colliders de las hojas
        // y el rayo no las veía, aunque seleccionarlas por código funcionara.) Al apuntarlas se iluminan.
        [UnityTest]
        public IEnumerator Piezas_ElRayoLasDetecta_YSeIluminanAlApuntarlas()
        {
            yield return LoadLevel();
            yield return WaitForSpiders();
            var pieces = ((System.Collections.IList)Static("Level5InteractiveHome", "Hinges")).Cast<Component>()
                .Concat(((System.Collections.IList)Static("Level5InteractiveHome", "Slides")).Cast<Component>())
                .Append((Component)Get(Task("TASK_LAMP"), "lampSwitch")).ToList();
            var failures = new System.Collections.Generic.List<string>();
            foreach (Component piece in pieces)
            {
                XRSimpleInteractable interactable = piece.GetComponent<XRSimpleInteractable>();
                Collider collider = piece.GetComponent<Collider>();
                if (interactable.interactionManager == null ||
                    !interactable.interactionManager.TryGetInteractableForCollider(collider, out IXRInteractable owner) ||
                    !ReferenceEquals(owner, interactable))
                    failures.Add(PathOf(piece.transform));
                if (piece.GetComponent(Game("Level5HoverHighlight")) == null) failures.Add(PathOf(piece.transform) + " (sin resaltado)");
            }
            Assert.IsEmpty(failures, "Piezas que el rayo no detecta:\n" + string.Join("\n", failures));

            // Un rayo real (XRRayInteractor) apuntando a la hoja de la nevera la detecta y la ilumina.
            Component leaf = (Component)Get(Task("TASK_FRIDGE"), "hinge");
            Vector3 target = leaf.GetComponent<Collider>().bounds.center;
            Transform body = leaf.transform.parent.Find("fridge_body");
            Vector3 front = Vector3.ProjectOnPlane(target - body.GetComponent<Renderer>().bounds.center, Vector3.up).normalized;
            if (front.sqrMagnitude < 0.01f) front = Vector3.forward;
            var hand = new GameObject("TEST_Rayo");
            hand.transform.position = target + front * 1.2f;
            hand.transform.rotation = Quaternion.LookRotation(target - hand.transform.position);
            var ray = hand.AddComponent<XRRayInteractor>();
            for (int i = 0; i < 5; i++) yield return null;
            Assert.IsTrue(ray.interactablesHovered.Any(h => ((Component)h).gameObject == leaf.gameObject),
                "El rayo no detecta la puerta de la nevera. Detecta: " + string.Join(", ", ray.interactablesHovered.Select(h => ((Component)h).name)));
            Assert.AreEqual(1, (int)Get(leaf.GetComponent(Game("Level5HoverHighlight")), "State"), "La puerta apuntada debe iluminarse.");
        }

        // La lámpara se apaga de verdad con su interruptor (no con acercarse).
        [UnityTest]
        public IEnumerator Lampara_ElInterruptorApagaLaLuzDeVerdad()
        {
            yield return LoadLevel();
            yield return WaitForSpiders();
            object task = Task("TASK_LAMP");
            Light light = (Light)Get(task, "controlledLight");
            Assert.IsTrue(light.enabled, "La lámpara debe empezar encendida.");

            PressLampSwitch();
            yield return null;
            yield return null;
            Assert.IsFalse(light.enabled, "El interruptor debe apagar la luz.");
            Assert.IsTrue((bool)Call(Find("TaskManager"), "IsCompleted", "TASK_LAMP"), "Apagar la lámpara debe completar la tarea.");
        }

        private static string PathOf(Transform t)
        {
            string path = t.name;
            while (t.parent != null) { t = t.parent; path = t.name + "/" + path; }
            return path;
        }

        [UnityTest]
        public IEnumerator Nivel5_ConDemasiadosAlejamientos_SeOfreceRepetirYSeReinicia()
        {
            yield return LoadLevel();
            yield return WaitForSpiders();

            object collector = Get(Find("TaskManager"), "metricsCollector");
            Assert.IsNotNull(collector, "TaskManager sin MetricsCollector.");
            object manager = Find("TaskManager");
            Set(collector, "avoidanceAttempts", (int)Get(manager, "maxAvoidanceAttempts") + 1);

            FinishLevelTasksExceptWindow();
            yield return null;
            OpenWindow();
            float start = Time.realtimeSinceStartup;
            while (!(bool)Get(manager, "RepeatOffered") && Time.realtimeSinceStartup - start < 10f) yield return null;
            // Antes del panel de repetir se pide el SUDS «después» del intento.
            yield return SkipSudsBefore();

            Assert.IsTrue((bool)Get(manager, "RepeatOffered"), "Debe ofrecerse repetir el nivel.");
            Assert.IsTrue((bool)Static("GlobalSessionUI", "IsRepeatOffered"), "El panel de repetir debe estar visible.");
            Assert.AreNotEqual("SESSION_END", GameStateManagerState(), "No debe cerrarse la sesión si se superó el límite.");

            // El intento fallido cuenta como intento y queda registrado antes de repetir.
            System.Collections.IList records = LevelRecords(5);
            Assert.AreEqual(1, records.Count, "El intento fallido debe registrarse al ofrecer repetir.");
            Assert.IsFalse((bool)Get(records[0], "levelPassed"), "El intento fallido debe tener levelPassed = false.");
            Assert.AreEqual(1, (int)Get(records[0], "levelAttempts"), "El primer intento es el número 1.");

            Button repeat = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(b => b.name == "Repetir");
            Assert.IsNotNull(repeat, "No existe el botón «Repetir nivel».");
            repeat.onClick.Invoke();

            yield return null;
            yield return null;
            yield return null;
            // El nivel repetido vuelve a pedir el SUDS «antes» y queda en pausa hasta responder.
            yield return SkipSudsBefore();
            yield return WaitForSpiders();

            object freshManager = Find("TaskManager");
            Assert.AreEqual(0, (int)Get(freshManager, "CompletedCount"), "Al repetir, las tareas empiezan de cero.");
            Assert.AreEqual("PLAYING", GameStateManagerState());

            // Repetir no borra el intento anterior y el nuevo intento es el número 2.
            Assert.AreEqual(1, LevelRecords(5).Count, "Repetir no debe añadir ni borrar registros por sí solo.");
            object freshCollector = Get(freshManager, "metricsCollector");
            object freshMetrics = Call(freshCollector, "BuildMetrics", false);
            Assert.AreEqual(2, (int)Get(freshMetrics, "levelAttempts"), "El intento repetido es el número 2.");

            // Un segundo intento del mismo nivel se registra aparte (SessionManager acepta varios intentos).
            Call(Static("SessionManager", "Instance"), "AddLevelMetrics", freshMetrics);
            Assert.AreEqual(2, LevelRecords(5).Count, "El segundo intento debe registrarse como otro registro.");
        }

        private static System.Collections.IList LevelRecords(int level)
        {
            object session = Static("SessionManager", "Instance");
            Assert.IsNotNull(session, "No hay SessionManager.");
            System.Collections.IList all = (System.Collections.IList)Get(Call(session, "GetReport"), "levelMetrics");
            return all.Cast<object>().Where(record => (int)Get(record, "level") == level).ToList();
        }

        // Una araña empujada contra un muro y contra un mueble se detiene ante ellos en vez de atravesarlos.
        [UnityTest]
        public IEnumerator Aranas_NoAtraviesanMurosNiMuebles()
        {
            yield return LoadLevel();
            yield return WaitForSpiders();

            // Se desactiva el gestor de arañas para controlar el destino a mano.
            ((Behaviour)Find("Level5CompanionSpiders")).enabled = false;
            Transform[] spiders = LiveSpiders();

            // Muro entre sala y cuarto (Divisor_Sala_Cuarto_B), z de -6,06 a -5,94.
            GameObject wall = GameObject.Find("Divisor_Sala_Cuarto_B");
            Assert.IsNotNull(wall, "No existe el muro Divisor_Sala_Cuarto_B.");
            Bounds wallBounds = wall.GetComponent<Collider>().bounds;
            float wallFaceZ = wallBounds.min.z;

            // Mueble de prueba en un hueco libre de la sala.
            GameObject crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crate.name = "TEST_Mueble";
            crate.transform.position = new Vector3(1.2f, 0.6f, -9.5f);
            crate.transform.localScale = new Vector3(0.6f, 1.0f, 2.0f);

            float groundY = spiders[0].position.y;
            SendAgainst(spiders[0], new Vector3(1.0f, groundY, wallFaceZ - 0.8f), new Vector3(1.0f, groundY, wallBounds.max.z + 1.5f));
            SendAgainst(spiders[1], new Vector3(-0.6f, groundY, -9.5f), new Vector3(3.0f, groundY, -9.5f));
            yield return new WaitForSeconds(7f);

            Assert.Less(spiders[0].position.z, wallFaceZ + 0.05f, "La araña atravesó el muro: z=" + spiders[0].position.z);
            Assert.Less(spiders[1].position.x, crate.transform.position.x, "La araña atravesó el mueble: x=" + spiders[1].position.x);
            Assert.IsTrue(spiders[0].GetComponent(Game("NeuroPhobia.Level45.NPNaturalSpiderPatrol")) != null);
        }

        private static void SendAgainst(Transform spider, Vector3 from, Vector3 to)
        {
            spider.position = from;
            var goal = new GameObject("TEST_Goal");
            goal.transform.position = to;
            Component patrol = spider.GetComponent(Game("NeuroPhobia.Level45.NPNaturalSpiderPatrol"));
            Set(patrol, "waypoints", new[] { goal.transform });
            Set(patrol, "moveSpeed", 0.6f);
            spider.rotation = Quaternion.LookRotation((to - from).normalized);
        }

        [UnityTest]
        public IEnumerator Ventana_SeAbreDeslizandoElCristal_HayExteriorYUnaArana()
        {
            yield return LoadLevel();
            yield return WaitForSpiders();

            object task = Task("TASK_WINDOW");
            Assert.IsFalse((bool)Get(task, "IsActivated"));
            OpenWindow();
            yield return null;
            yield return null;
            Assert.IsTrue((bool)Call(Find("TaskManager"), "IsCompleted", "TASK_WINDOW"), "Abrir la ventana debe completar la tarea.");

            Renderer glassRenderer = Ask(task).GetComponent<Renderer>();
            Assert.IsFalse(glassRenderer.isPartOfStaticBatch, "El cristal está en un lote estático: no se vería subir.");
            GameObject exterior = GameObject.Find("NP_L5_Exterior");
            Assert.IsNotNull(exterior, "Por la ventana solo se ve el skybox: falta el exterior.");
            // El exterior queda al otro lado del cristal respecto al apartamento.
            Bounds apartment = new Bounds(glassRenderer.bounds.center, Vector3.zero);
            foreach (Renderer r in GameObject.Find("NP_L5_APARTAMENTO_VISUAL").GetComponentsInChildren<Renderer>()) apartment.Encapsulate(r.bounds);
            Bounds outside = new Bounds(exterior.transform.position, Vector3.zero);
            foreach (Renderer r in exterior.GetComponentsInChildren<Renderer>()) outside.Encapsulate(r.bounds);
            Vector3 toApartment = apartment.center - glassRenderer.bounds.center; toApartment.y = 0f;
            Vector3 toExterior = outside.center - glassRenderer.bounds.center; toExterior.y = 0f;
            Assert.Less(Vector3.Dot(toApartment, toExterior), 0f, "El exterior se construyó del lado del apartamento.");
            Assert.Greater(exterior.GetComponentsInChildren<Renderer>().Length, 10, "El exterior debe tener edificios.");
            GameObject spider = GameObject.Find("NP_L5_Arana_Ventana");
            Assert.IsNotNull(spider, "Falta la araña de la ventana.");
            Assert.Less(Vector3.Distance(spider.transform.position, Ask(task).GetComponent<Collider>().bounds.center), 1.5f, "La araña debe estar en la ventana.");
        }

        private static string GameStateManagerState() => Static("GameStateManager", "CurrentState").ToString();
    }
}
