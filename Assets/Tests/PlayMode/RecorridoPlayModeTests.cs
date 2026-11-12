using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

namespace NeuroPhobia.Tests
{
    /// <summary>
    /// Pruebas del recorrido de Nivel5: colliders activos, jugador dentro del apartamento,
    /// y XR Device Simulator fuera de las escenas del build. Los menús se prueban en MenusPlayModeTests.
    /// </summary>
    public class RecorridoPlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/Nivel5.unity";

        [SetUp]
        public void IgnoreEditorNoise() { LogAssert.ignoreFailingMessages = true; }

        private static Type Game(string name) => Type.GetType(name + ", Assembly-CSharp", true);

        private static IEnumerator Load(string path)
        {
            Time.timeScale = 1f;
            LogAssert.ignoreFailingMessages = true;
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync(System.IO.Path.GetFileNameWithoutExtension(path));
#endif
            yield return null;
            yield return null;
            yield return Nivel5PlayModeTests.SkipSudsBefore();
            LogAssert.ignoreFailingMessages = true;
        }

        private static string PathOf(Transform t)
        {
            string path = t.name;
            while (t.parent != null) { t = t.parent; path = t.name + "/" + path; }
            return path;
        }

        // Paredes, techos, suelo de fábrica y mobiliario: ningún collider activo en la jerarquía puede estar apagado.
        // Excepciones sustituidas a propósito por la casa interactiva: la caja maciza de los marcos de puerta (ahora
        // jambas y dintel) y la caja-botón de los objetos de tarea (ahora cuentan los colliders de sus piezas).
        [UnityTest]
        public IEnumerator Nivel5_NingunColliderDeParedesSuelosOMueblesEstaDesactivado()
        {
            yield return Load(ScenePath);

            GameObject apartment = GameObject.Find("NP_L5_APARTAMENTO_VISUAL");
            Assert.IsNotNull(apartment, "No existe NP_L5_APARTAMENTO_VISUAL.");

            var apagados = new List<string>();
            foreach (Collider collider in apartment.GetComponentsInChildren<Collider>(false))
            {
                if (collider.enabled) continue;
                bool frameReplaced = collider.name == "door_frame" && collider.transform.Find("NP_Jamba_Izquierda") != null;
                bool taskButton = collider.GetComponent(Game("Level5TaskInteraction")) != null &&
                                  collider.GetComponentsInChildren<Collider>().Any(c => c != collider && c.enabled);
                if (!frameReplaced && !taskButton) apagados.Add(PathOf(collider.transform));
            }
            Assert.IsEmpty(apagados, apagados.Count + " colliders desactivados en el apartamento:\n" + string.Join("\n", apagados.Take(15)));

            foreach (string wall in new[] { "Exterior_Oeste", "Exterior_Este", "Exterior_Norte_Der", "Exterior_Sur_Izq", "Exterior_Sur_Der", "Divisor_Sala_Cuarto_A", "Techo_01_SALA" })
            {
                GameObject go = GameObject.Find(wall);
                Assert.IsNotNull(go, "No existe " + wall);
                Collider collider = go.GetComponent<Collider>();
                Assert.IsTrue(collider != null && collider.enabled, wall + " no tiene un collider activo.");
            }
        }

        // El apartamento está cerrado: desde cada habitación, rayos horizontales a tres alturas y 72 direcciones
        // chocan siempre con un collider sólido dentro del perímetro (sin huecos por donde salir).
        [UnityTest]
        public IEnumerator Apartamento_EstaCerrado_NoHayHuecosParaSalir()
        {
            yield return Load(ScenePath);
            Physics.SyncTransforms();

            var perimetro = new Bounds(new Vector3(3f, 1.5f, -6f), new Vector3(12.4f, 4f, 12.4f)); // x -3..9, z -12..0 + grosor de muros
            var puntos = new[]
            {
                new Vector3(0f, 0f, -3f), new Vector3(0f, 0f, -7.8f), new Vector3(6f, 0f, -3f),
                new Vector3(7.5f, 0f, -4.5f), new Vector3(0f, 0f, -10.5f), new Vector3(5f, 0f, -11f)
            };

            var fugas = new List<string>();
            foreach (Vector3 punto in puntos)
            {
                foreach (float altura in new[] { 0.4f, 1.1f, 1.7f })
                {
                    for (int angulo = 0; angulo < 360; angulo += 5)
                    {
                        Vector3 origen = punto + Vector3.up * altura;
                        Vector3 direccion = Quaternion.Euler(0f, angulo, 0f) * Vector3.forward;
                        bool choca = Physics.Raycast(origen, direccion, out RaycastHit hit, 40f, ~0, QueryTriggerInteraction.Ignore);
                        if (!choca || !perimetro.Contains(hit.point))
                            fugas.Add("desde " + punto + " h=" + altura + " ángulo " + angulo + (choca ? " choca fuera en " + hit.point : " no choca con nada"));
                    }
                }
            }
            Assert.IsEmpty(fugas, fugas.Count + " rayos escapan del apartamento (hueco en los muros): " + string.Join(" | ", fugas.Take(12)));
        }

        // El suelo de los cuartos es un trigger (para las arañas): el jugador debe seguir pisando suelo sólido.
        [UnityTest]
        public IEnumerator Jugador_PisaSueloSolidoEnCadaHabitacion()
        {
            yield return Load(ScenePath);

            foreach (Vector3 point in new[] { new Vector3(0f, 0.5f, -3f), new Vector3(6f, 0.5f, -3f), new Vector3(7.5f, 0.5f, -4.5f), new Vector3(0f, 0.5f, -10.5f) })
            {
                bool hitSolid = Physics.Raycast(point + Vector3.up, Vector3.down, out RaycastHit hit, 3f, ~0, QueryTriggerInteraction.Ignore);
                Assert.IsTrue(hitSolid, "No hay suelo sólido bajo " + point);
                Assert.Less(Mathf.Abs(hit.point.y), 0.2f, "El suelo bajo " + point + " está a y=" + hit.point.y);
            }
        }

#if UNITY_EDITOR
        // En ninguna escena del build puede haber un XR Device Simulator activo: en el visor se queda con los mandos.
        [UnityTest]
        public IEnumerator Build_NingunaEscenaTieneXRDeviceSimulatorActivo()
        {
            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            Assert.IsNotEmpty(scenes, "No hay escenas en el build.");

            var activos = new List<string>();
            foreach (string path in scenes)
            {
                yield return Load(path);
                foreach (XRDeviceSimulator simulator in UnityEngine.Object.FindObjectsByType<XRDeviceSimulator>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    activos.Add(System.IO.Path.GetFileNameWithoutExtension(path) + ": " + PathOf(simulator.transform));
                }
            }
            Assert.IsEmpty(activos, "XRDeviceSimulator activo en:\n" + string.Join("\n", activos));
        }

        // La protección de los builds desactiva cualquier simulador que aparezca.
        [UnityTest]
        public IEnumerator Proteccion_DesactivaUnSimuladorActivo()
        {
            yield return Load(ScenePath);
            var go = new GameObject("TEST_Simulator");
            go.AddComponent<XRDeviceSimulator>();
            Type guard = Game("NoDeviceSimulatorInBuild");
            MethodInfo disable = guard.GetMethod("DisableAll", BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(disable, "Falta NoDeviceSimulatorInBuild.DisableAll");
            int count = (int)disable.Invoke(null, null);
            Assert.GreaterOrEqual(count, 1);
            Assert.IsFalse(go.activeSelf, "El simulador sigue activo.");
            UnityEngine.Object.Destroy(go);
        }
#endif
    }
}
