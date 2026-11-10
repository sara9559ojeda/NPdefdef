using System.Collections;
using System.Linq;
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
    /// Nivel4 usa el mismo modelo (Wolf_spider.fbx, «Visual_WolfSpider») que Nivel3 y Nivel5, con rotación 0 en la escena.
    /// Desde el PR #2, NPNaturalSpiderPatrol gira el visual 180° (visualYawOffset) y corrige el rumbo del clip.
    /// Esta prueba comprueba que en Nivel4 la cabeza también apunta hacia donde avanza la araña.
    /// </summary>
    public class Nivel4PlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/Nivel4.unity";
        private const string SpiderName = "NP_Spider_Level4";

        [UnityTest]
        public IEnumerator Arana_LaCabezaApuntaHaciaDondeAvanza_ToleranciaVeinteGrados()
        {
            Time.timeScale = 1f;
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync("Nivel4");
#endif
            yield return null;
            yield return Nivel5PlayModeTests.SkipSudsBefore();

            GameObject spider = GameObject.Find(SpiderName);
            Assert.IsNotNull(spider, "No se encontró " + SpiderName + " en Nivel4.");
            Transform fang = spider.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Fang.L");
            Transform back = spider.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Back");
            Assert.IsNotNull(fang, "El modelo de Nivel4 no tiene el hueso Fang.L.");
            Assert.IsNotNull(back, "El modelo de Nivel4 no tiene el hueso Back.");

            // Solo se mide mientras la araña se desplaza (el controlador de Nivel4 la pausa a ratos).
            int measured = 0;
            Vector3 last = spider.transform.position;
            float start = Time.realtimeSinceStartup;
            while (measured < 5 && Time.realtimeSinceStartup - start < 40f)
            {
                yield return new WaitForSeconds(0.7f);
                yield return new WaitForEndOfFrame();
                Vector3 moved = spider.transform.position - last; moved.y = 0f;
                last = spider.transform.position;
                if (moved.magnitude < 0.03f) continue;

                Vector3 head = fang.position - back.position; head.y = 0f;
                float angle = Vector3.Angle(head, moved);
                Assert.LessOrEqual(angle, 20f, "La cabeza forma " + angle.ToString("0") + "° con el avance (muestra " + measured + ").");
                measured++;
            }
            Assert.Greater(measured, 0, "La araña de Nivel4 no se movió en 40 s: no se pudo medir la orientación.");
        }
    }
}
