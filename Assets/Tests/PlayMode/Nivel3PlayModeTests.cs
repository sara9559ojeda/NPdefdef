using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace NeuroPhobia.Tests
{
    /// <summary>
    /// Nivel 3 (aproximación progresiva): un solo controlador decide el progreso, el nivel no termina antes de cumplir
    /// la misión (alcanzar cada orbe en orden y mantenerlo en la mirada), la cantidad de arañas sale de la configuración
    /// según el grado y su movimiento es natural.
    /// </summary>
    public class Nivel3PlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/Nivel3.unity";
        private const int TestPhobiaGrade = 3;
        private Camera testCamera;

        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            object session = Static("SessionManager", "Instance");
            if (session == null || (UnityEngine.Object)session == null) session = new GameObject("TEST_SessionManager").AddComponent(Game("SessionManager"));
            Call(session, "StartSession", TestPhobiaGrade, true);
        }

        [TearDown]
        public void TearDown()
        {
            object session = Static("SessionManager", "Instance");
            if (session != null && (UnityEngine.Object)session != null) UnityEngine.Object.DestroyImmediate(((Component)session).gameObject);
            Time.timeScale = 1f;
        }

        private static Type Game(string name) => Type.GetType(name + ", Assembly-CSharp", true);
        private static object Static(string typeName, string member) =>
            Game(typeName).GetProperty(member, BindingFlags.Public | BindingFlags.Static).GetValue(null);

        private static object Get(object target, string member)
        {
            Type type = target.GetType();
            PropertyInfo property = type.GetProperty(member, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property != null) return property.GetValue(target);
            FieldInfo field = type.GetField(member, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "No existe " + member);
            return field.GetValue(target);
        }

        private static object Call(object target, string method, params object[] args) =>
            target.GetType().GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args);

        private IEnumerator Load()
        {
            Time.timeScale = 1f;
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync("Nivel3");
#endif
            yield return null;
            yield return null;
            yield return Nivel5PlayModeTests.SkipSudsBefore();
            LogAssert.ignoreFailingMessages = true;
        }

        // Cámara de prueba controlada por la prueba (las demás se apagan para que sea Camera.main).
        private Camera UseTestCamera()
        {
            foreach (Camera camera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) camera.enabled = false;
            var go = new GameObject("TEST_Cabeza") { tag = "MainCamera" };
            testCamera = go.AddComponent<Camera>();
            return testCamera;
        }

        private static Component Mission() => (Component)UnityEngine.Object.FindFirstObjectByType(Game("NPLevel3RealOrbs"));

        private static string State() => Static("GameStateManager", "CurrentState").ToString();

        // Mantiene la cabeza en un punto mirando en una dirección durante unos segundos.
        private IEnumerator Hold(Vector3 position, Vector3 lookAt, float seconds)
        {
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < seconds)
            {
                testCamera.transform.position = position;
                testCamera.transform.rotation = Quaternion.LookRotation(lookAt - position, Vector3.up);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator UnSoloControladorDecideElProgreso()
        {
            yield return Load();
            string[] controllers = { "NPLevel3RealOrbs", "Level3ProgressControllerV2", "Level3ProgressController", "Level3OrbSequenceFlow", "NeuroPhobiaLevel3OrbCompatibility" };
            var active = new List<string>();
            foreach (string name in controllers)
            {
                foreach (Behaviour b in UnityEngine.Object.FindObjectsByType(Game(name), FindObjectsInactive.Include, FindObjectsSortMode.None).Cast<Behaviour>())
                    if (b.isActiveAndEnabled) active.Add(name + " en " + b.name);
            }
            Assert.AreEqual(1, active.Count, "Controladores activos: " + string.Join(", ", active));
            Assert.AreEqual("NPLevel3RealOrbs", active[0].Split(' ')[0]);
        }

        [UnityTest]
        public IEnumerator ElNivelNoTerminaAntesDeCumplirLaMision()
        {
            yield return Load();
            UseTestCamera();
            Component mission = Mission();
            Assert.AreEqual(2, (int)Get(mission, "TotalOrbs"));
            GameObject orb1 = (GameObject)Get(mission, "orb1");
            GameObject orb2 = (GameObject)Get(mission, "orb2");
            Assert.IsTrue(orb1.activeSelf && !orb2.activeSelf, "Al empezar solo debe verse el primer orbe.");

            // Cerca del primer orbe pero mirando hacia otro lado: no cuenta.
            Vector3 near1 = orb1.transform.position + new Vector3(0.8f, 1.0f, 0f);
            yield return Hold(near1, near1 + (near1 - orb1.transform.position), 4f);
            Assert.AreEqual(0, (int)Get(mission, "ReachedOrbs"), "Estar cerca sin mirar el orbe no debe contar.");

            // Mirándolo desde lejos: tampoco.
            Vector3 far1 = orb1.transform.position + new Vector3(5f, 1.0f, 0f);
            yield return Hold(far1, orb1.transform.position, 4f);
            Assert.AreEqual(0, (int)Get(mission, "ReachedOrbs"), "Mirar el orbe de lejos no debe contar.");

            // Llegar al segundo orbe (aún oculto) antes que al primero: no cuenta.
            Vector3 near2 = orb2.transform.position + new Vector3(0.8f, 1.0f, 0f);
            yield return Hold(near2, orb2.transform.position, 4f);
            Assert.AreEqual(0, (int)Get(mission, "ReachedOrbs"), "Los orbes se alcanzan en orden.");

            // Cerca y mirándolo el tiempo pedido: cuenta el primero, y el nivel sigue.
            yield return Hold(near1, orb1.transform.position, 4f);
            Assert.AreEqual(1, (int)Get(mission, "ReachedOrbs"), "Cerca y mirando debe alcanzar el primer orbe.");
            Assert.IsFalse((bool)Get(mission, "IsComplete"), "Con un orbe el nivel no debe terminar.");
            Assert.AreEqual("PLAYING", State(), "Con un orbe el nivel debe seguir en juego.");

            // Segundo orbe: cerca sin mirar, no; mirando, sí, y entonces termina.
            yield return Hold(near2, near2 + (near2 - orb2.transform.position), 4f);
            Assert.AreEqual(1, (int)Get(mission, "ReachedOrbs"));
            Assert.IsFalse((bool)Get(mission, "IsComplete"));
            yield return Hold(near2, orb2.transform.position, 4f);
            Assert.AreEqual(2, (int)Get(mission, "ReachedOrbs"));
            Assert.IsTrue((bool)Get(mission, "IsComplete"), "Con la misión cumplida el nivel debe terminar.");
        }

        [UnityTest]
        public IEnumerator LaMisionSeVeEnElHud()
        {
            yield return Load();
            yield return null;
            TMPro.TMP_Text[] texts = UnityEngine.Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsSortMode.None);
            string mission = (string)Game("NPLevel3RealOrbs").GetField("MissionText").GetValue(null);
            Assert.IsTrue(texts.Any(t => t.text == mission && t.isActiveAndEnabled), "La misión no aparece en el HUD.");
            Canvas hud = texts.First(t => t.text == mission).canvas.rootCanvas;
            Assert.AreEqual(RenderMode.WorldSpace, hud.renderMode, "El HUD del nivel 3 debe estar en el mundo.");
        }

        [UnityTest]
        public IEnumerator LaCantidadDeAranasSaleDeLaConfiguracionSegunElGrado()
        {
            yield return Load();
            Type behaviour = Game("Level3OrbSpiders");
            float start = Time.realtimeSinceStartup;
            while ((int)behaviour.GetProperty("LastPlaced").GetValue(null) < 0 && Time.realtimeSinceStartup - start < 10f) yield return null;

            object settings = Game("Level3Settings").GetMethod("Load").Invoke(null, null);
            int expected = (int)Call(settings, "SpiderCountForGrade", TestPhobiaGrade);
            Assert.AreEqual(expected, (int)behaviour.GetProperty("LastRequested").GetValue(null), "La cantidad pedida no sale de la configuración.");
            Assert.AreEqual(expected, (int)behaviour.GetProperty("LastPlaced").GetValue(null), "No se colocaron todas las arañas pedidas.");

            Transform[] spiders = Spiders();
            Assert.AreEqual(expected, spiders.Length, "Arañas activas en la escena.");
            float minOrb = (float)Get(settings, "minDistanceFromOrb");
            Vector3[] orbs = { ((GameObject)Get(Mission(), "orb1")).transform.position, ((GameObject)Get(Mission(), "orb2")).transform.position };
            foreach (Transform spider in spiders)
            {
                Assert.Less(spider.position.y, 0.5f, spider.name + " no está en el suelo.");
                if (spider.name.StartsWith("NP_L3_OrbSpider_"))
                    foreach (Vector3 orb in orbs) Assert.GreaterOrEqual(Flat(spider.position - orb), minOrb - 0.05f, spider.name + " tapa un orbe.");
            }
        }

        [UnityTest]
        public IEnumerator LasAranasSeMuevenConNaturalidad()
        {
            yield return Load();
            yield return new WaitForSeconds(1.5f);
            Transform[] spiders = Spiders();
            Assert.Greater(spiders.Length, 1);
            object settings = Game("Level3Settings").GetMethod("Load").Invoke(null, null);
            float turnRate = (float)Get(settings, "turnRate");

            var lastPos = spiders.ToDictionary(s => s, s => s.position);
            var lastYaw = spiders.ToDictionary(s => s, s => s.eulerAngles.y);
            float moved = 0f;
            for (int frame = 0; frame < 240; frame++)
            {
                yield return null;
                float dt = Mathf.Max(Time.deltaTime, 0.0001f);
                foreach (Transform spider in spiders)
                {
                    float yawStep = Mathf.Abs(Mathf.DeltaAngle(lastYaw[spider], spider.eulerAngles.y));
                    Assert.LessOrEqual(yawStep, turnRate * dt + 1f, spider.name + ": giro brusco de " + yawStep.ToString("0.0") + "° en un frame.");
                    Vector3 step = spider.position - lastPos[spider]; step.y = 0f;
                    if (step.magnitude > 0.0005f)
                    {
                        Vector3 forward = spider.forward; forward.y = 0f;
                        Assert.LessOrEqual(Vector3.Angle(step, forward), 20f, spider.name + " se desliza de lado.");
                        Assert.LessOrEqual(step.magnitude / dt, 0.5f, spider.name + " va demasiado rápido.");
                        moved += step.magnitude;
                    }
                    lastPos[spider] = spider.position;
                    lastYaw[spider] = spider.eulerAngles.y;
                }
            }
            Assert.Greater(moved, 0.05f, "Las arañas no se mueven.");
        }

        private static Transform[] Spiders() =>
            UnityEngine.Object.FindObjectsByType(Game("Level3SpiderPatrolV2"), FindObjectsSortMode.None)
                .Cast<Component>().Where(c => ((Behaviour)c).isActiveAndEnabled).Select(c => c.transform).ToArray();

        private static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }
    }
}
