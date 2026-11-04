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
        [SetUp]
        public void IgnoreEditorNoise() { LogAssert.ignoreFailingMessages = true; }

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

        private static object FinishLevelTasksExceptWindow()
        {
            PullTrigger(Ask(Task("TASK_LAMP")));
            PullTrigger(Ask(Task("TASK_DOOR")));
            PullTrigger(Ask(Task("TASK_FRIDGE")));
            return Find("TaskManager");
        }

        private static void LookAtWindowFromFreeSpot()
        {
            object gaze = Find("Level5WindowGaze");
            Assert.IsNotNull(gaze, "No existe Level5WindowGaze.");
            Transform window = (Transform)Get(gaze, "windowTarget");
            Collider windowCollider = window.GetComponentInChildren<Collider>();
            Vector3 center = windowCollider != null ? windowCollider.bounds.center : window.position;

            // Cámara de prueba frente a la ventana; mira desde el interior del apartamento.
            var cameraObject = new GameObject("TEST_Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            Vector3 toInside = (Vector3.zero - center); toInside.y = 0f;
            Vector3 inside = toInside.sqrMagnitude > 0.01f ? toInside.normalized : Vector3.forward;
            cameraObject.transform.position = center + inside * 1.5f;
            cameraObject.transform.rotation = Quaternion.LookRotation((center - cameraObject.transform.position).normalized, Vector3.up);
            Set(gaze, "playerCamera", camera);
            Set(gaze, "requiredSeconds", 0.3f);
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
        public IEnumerator Aranas_LaCantidadSaleDeLosAjustes_YRespetanLaDistanciaDeAparicion()
        {
            yield return LoadLevel();
            yield return WaitForSpiders();

            object manager = Find("Level5CompanionSpiders");
            int expected = (int)Get(Get(manager, "Settings"), "spiderCount");
            float minSpawn = (float)Get(Get(manager, "Settings"), "minSpawnDistanceFromPlayer");
            Transform[] spiders = LiveSpiders();
            Assert.AreEqual(expected, spiders.Length, "La cantidad de arañas no coincide con Level5SpiderSettings.spiderCount.");

            Vector3 start = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
            foreach (Transform spider in spiders)
            {
                Vector3 flat = spider.position - start; flat.y = 0f;
                Assert.GreaterOrEqual(flat.magnitude, minSpawn - 0.6f, spider.name + " apareció demasiado cerca del jugador.");
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
                yield return new WaitForEndOfFrame();
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

        [UnityTest]
        public IEnumerator Aranas_NingunaSeAcercaAlJugadorMasDeLaDistanciaMinima()
        {
            yield return LoadLevel();
            yield return WaitForSpiders();

            float min = (float)Get(Get(Find("Level5CompanionSpiders"), "Settings"), "minDistanceToPlayer");
            Transform player = Camera.main.transform;
            // Se coloca a la jugadora justo encima de una araña: el sistema debe apartarla.
            Transform victim = LiveSpiders()[0];
            player.position = victim.position + Vector3.up * 1.6f;
            yield return null;
            yield return null;

            foreach (Transform spider in LiveSpiders())
            {
                Vector3 flat = spider.position - player.position; flat.y = 0f;
                Assert.GreaterOrEqual(flat.magnitude, min - 0.05f, spider.name + " quedó a " + flat.magnitude.ToString("0.00") + " m del jugador.");
            }
        }

        [UnityTest]
        public IEnumerator Nivel5_LasCuatroTareasCompletanElNivel()
        {
            yield return LoadLevel();
            yield return WaitForSpiders();

            object manager = FinishLevelTasksExceptWindow();
            Assert.AreEqual(3, (int)Get(manager, "CompletedCount"), "Lámpara, puerta y nevera deben completarse con el gatillo.");

            LookAtWindowFromFreeSpot();
            float start = Time.realtimeSinceStartup;
            while ((int)Get(manager, "CompletedCount") < 4 && Time.realtimeSinceStartup - start < 10f) yield return null;

            Assert.AreEqual(4, (int)Get(manager, "CompletedCount"), "La ventana debe completarse al mirarla.");
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
            GameObject doorObject = Ask(door);
            Collider box = doorObject.GetComponent<Collider>();
            Assert.IsTrue(box.enabled, "La puerta cerrada debe tener su collider activo.");

            // Hueco de la puerta: centro del collider cerrado.
            Vector3 center = box.bounds.center;
            Assert.IsTrue(Physics.CheckCapsule(center - Vector3.up * 0.8f, center + Vector3.up * 0.8f, 0.25f, ~0, QueryTriggerInteraction.Ignore),
                "Con la puerta cerrada el hueco debe estar bloqueado.");

            PullTrigger(doorObject);
            yield return new WaitForSeconds(1.5f);   // la hoja gira 85° a 120°/s

            Assert.IsTrue((bool)Get(door, "IsActivated"));
            Assert.IsFalse(box.enabled, "El collider raíz de la puerta abierta no debe bloquear el paso.");
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
            player.position = Ask(door).GetComponent<Collider>().bounds.center + Vector3.forward * 0.8f;
            yield return new WaitForSeconds(1f);

            Assert.IsFalse((bool)Get(door, "IsActivated"), "La puerta no debe abrirse sola al acercarse.");
            Assert.AreEqual(0, (int)Get(Find("TaskManager"), "CompletedCount"));
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
            LookAtWindowFromFreeSpot();
            float start = Time.realtimeSinceStartup;
            while (!(bool)Get(manager, "RepeatOffered") && Time.realtimeSinceStartup - start < 10f) yield return null;

            Assert.IsTrue((bool)Get(manager, "RepeatOffered"), "Debe ofrecerse repetir el nivel.");
            Assert.IsTrue((bool)Static("GlobalSessionUI", "IsRepeatOffered"), "El panel de repetir debe estar visible.");
            Assert.AreNotEqual("SESSION_END", GameStateManagerState(), "No debe cerrarse la sesión si se superó el límite.");

            Button repeat = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(b => b.name == "Repetir");
            Assert.IsNotNull(repeat, "No existe el botón «Repetir nivel».");
            repeat.onClick.Invoke();

            yield return null;
            yield return null;
            yield return null;
            yield return WaitForSpiders();

            object freshManager = Find("TaskManager");
            Assert.AreEqual(0, (int)Get(freshManager, "CompletedCount"), "Al repetir, las tareas empiezan de cero.");
            Assert.AreEqual("PLAYING", GameStateManagerState());
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
        public IEnumerator Ventana_LasAranasNoTapanLaMirada()
        {
            yield return LoadLevel();
            yield return WaitForSpiders();

            LookAtWindowFromFreeSpot();
            object gaze = Find("Level5WindowGaze");
            Camera camera = (Camera)Get(gaze, "playerCamera");
            Transform window = (Transform)Get(gaze, "windowTarget");
            Collider windowCollider = window.GetComponentInChildren<Collider>();
            Vector3 target = windowCollider != null ? windowCollider.bounds.center : window.position;

            // Una araña se interpone entre la cámara y la ventana.
            Transform spider = LiveSpiders()[0];
            spider.position = Vector3.Lerp(camera.transform.position, target, 0.5f);
            spider.GetComponent<Collider>().enabled = true;
            yield return new WaitForFixedUpdate();

            object manager = Find("TaskManager");
            float start = Time.realtimeSinceStartup;
            while (!(bool)Call(manager, "IsCompleted", "TASK_WINDOW") && Time.realtimeSinceStartup - start < 10f) yield return null;
            Assert.IsTrue((bool)Call(manager, "IsCompleted", "TASK_WINDOW"), "Una araña delante no debe impedir completar la ventana.");
        }

        private static string GameStateManagerState() => Static("GameStateManager", "CurrentState").ToString();
    }
}
