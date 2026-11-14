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
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace NeuroPhobia.Tests
{
    /// <summary>
    /// Paneles de sesión (SUDS, pausa, nivel completado…) en todos los niveles: World Space, delante de la persona a
    /// unos 1,5–2 m y a la altura de los ojos, sin pared en medio, con la hoja y el texto en un solo bloque, y pulsables
    /// con el rayo de un mando (TrackedDeviceGraphicRaycaster) y con el ratón.
    /// </summary>
    public class MenusPlayModeTests
    {
        private static readonly string[] Levels = { "Nivel1", "Nivel2", "Nivel3", "Nivel4", "Nivel5" };

        [SetUp]
        public void IgnoreEditorNoise()
        {
            LogAssert.ignoreFailingMessages = true;
            object session = Static("SessionManager", "Instance");
            if (session == null || (UnityEngine.Object)session == null) session = new GameObject("TEST_SessionManager").AddComponent(Game("SessionManager"));
            Call(session, "StartSession", 2, true);
        }

        [TearDown]
        public void End()
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

        private static IEnumerator Load(string scene)
        {
            Time.timeScale = 1f;
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/" + scene + ".unity", new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync(scene);
#endif
            for (int i = 0; i < 4; i++) yield return null;
            LogAssert.ignoreFailingMessages = true;
        }

        [UnityTest]
        public IEnumerator Paneles_SonWorldSpace_DelanteALaAlturaDeLosOjos_YPulsablesConElRayo([ValueSource(nameof(Levels))] string scene)
        {
            yield return Load(scene);

            Component ui = (Component)UnityEngine.Object.FindFirstObjectByType(Game("GlobalSessionUI"));
            Assert.IsNotNull(ui, scene + ": no existe GlobalSessionUI.");
            Canvas canvas = ui.GetComponent<Canvas>();
            Assert.AreEqual(RenderMode.WorldSpace, canvas.renderMode, scene + ": el panel debe estar en el espacio del mundo.");
            Assert.IsNotNull(ui.GetComponent<TrackedDeviceGraphicRaycaster>(), scene + ": sin raycaster de mandos XR.");
            Assert.IsNotNull(ui.GetComponent<GraphicRaycaster>(), scene + ": sin raycaster de ratón.");
            EventSystem events = EventSystem.current != null ? EventSystem.current : UnityEngine.Object.FindFirstObjectByType<EventSystem>();
            Assert.IsNotNull(events, scene + ": sin EventSystem.");
            Assert.IsNotNull(events.GetComponent<XRUIInputModule>(), scene + ": el EventSystem no tiene XRUIInputModule.");

            // El SUDS «antes de empezar» se abre cuando termina la aparición del jugador (vista ya abierta).
            GameObject suds = (GameObject)Get(ui, "sudsPanel");
            float waitStart = Time.realtimeSinceStartup;
            while (!suds.activeSelf && Time.realtimeSinceStartup - waitStart < 10f) yield return null;
            Assert.IsTrue(suds.activeSelf, scene + ": el SUDS «antes» debería abrirse al terminar la aparición.");
            Assert.IsTrue((bool)Static("PlayerSpawnSequence", "Opened"), scene + ": el SUDS se abrió antes de abrirse la vista.");
            yield return null;

            Camera head = canvas.worldCamera;
            Assert.IsNotNull(head, scene + ": el panel no tiene cámara de eventos.");

            // En el Editor sin visor, la cámara del rig XR se queda a ras de suelo (no hay pose de la cabeza).
            // Se simula una persona de pie: ojos a 1,6 m. El panel debe recolocarse solo a la nueva altura.
            RaycastHit[] below = Physics.RaycastAll(head.transform.position + Vector3.up * 0.2f, Vector3.down, 5f, ~0, QueryTriggerInteraction.Ignore)
                .Where(h => h.normal.y > 0.9f && !h.collider.transform.IsChildOf(head.transform.root)).ToArray();
            float floorY = below.Length > 0 ? below.Min(h => h.point.y) : head.transform.position.y;
            if (head.transform.position.y - floorY < 1.0f)
            {
                head.transform.position = new Vector3(head.transform.position.x, floorY + 1.6f, head.transform.position.z);
                yield return null;
                yield return null;
            }
            // Distancias en metros percibidos: cada nivel tiene su escala de rig y el panel debe verse igual en todos.
            float rigScale = (float)Game("NeuroPhobiaWorldPanel").GetMethod("RigScale").Invoke(null, new object[] { head.transform });
            Vector3 toPanel = canvas.transform.position - head.transform.position;
            Assert.That(toPanel.magnitude / rigScale, Is.InRange(0.55f, 2.1f), scene + ": el panel está a " + (toPanel.magnitude / rigScale).ToString("0.00") + " m percibidos.");
            Assert.That((canvas.transform.position.y - head.transform.position.y) / rigScale, Is.InRange(-0.3f, 0.1f), scene + ": el panel no está a la altura de los ojos.");
            Assert.AreEqual(1.25f, canvas.GetComponent<RectTransform>().rect.width * canvas.transform.lossyScale.x / rigScale * 1.7f / (toPanel.magnitude / rigScale), 0.05f,
                scene + ": el panel no tiene el mismo tamaño aparente que en los demás niveles.");
            Vector3 flatLook = Vector3.ProjectOnPlane(head.transform.forward, Vector3.up);
            Vector3 flatPanel = Vector3.ProjectOnPlane(toPanel, Vector3.up);
            Assert.Less(Vector3.Angle(flatLook, flatPanel), 20f, scene + ": el panel no está delante.");

            // Ninguna pared o mueble entre los ojos y el panel.
            foreach (RaycastHit hit in Physics.RaycastAll(head.transform.position, toPanel.normalized, toPanel.magnitude, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(head.transform.root)) continue;
                Assert.Fail(scene + ": " + hit.collider.name + " tapa el panel. Cabeza " + head.name + " en " + head.transform.position.ToString("F2") +
                    ", panel en " + canvas.transform.position.ToString("F2") + ", choque en " + hit.point.ToString("F2") + " (bounds " + hit.collider.bounds + ")");
            }

            // La hoja de papel y los textos forman un bloque: los textos caen dentro de la hoja.
            RectTransform sheet = suds.GetComponentsInChildren<RectTransform>(true).First(r => r.name == "Hoja");
            Rect sheetRect = WorldRect(sheet);
            foreach (TMPro.TMP_Text text in suds.GetComponentsInChildren<TMPro.TMP_Text>())
            {
                Vector2 center = WorldRect(text.rectTransform).center;
                Assert.IsTrue(sheetRect.Contains(center), scene + ": el texto «" + text.text + "» queda fuera de la hoja.");
            }

            // Un rayo de mando (TrackedDevice) hacia el botón «50» lo encuentra, desde cualquiera de las dos manos.
            Button target = suds.GetComponentsInChildren<Button>().First(b => b.name == "Suds50");
            Vector3 buttonCenter = target.GetComponent<RectTransform>().TransformPoint(target.GetComponent<RectTransform>().rect.center);
            foreach (float side in new[] { -0.25f, 0.25f })
            {
                Vector3 hand = head.transform.position + (head.transform.right * side - Vector3.up * 0.3f) * rigScale;
                var data = new TrackedDeviceEventData(events)
                {
                    rayPoints = new List<Vector3> { hand, hand + (buttonCenter - hand).normalized * 5f },
                    layerMask = ~0
                };
                var results = new List<RaycastResult>();
                ui.GetComponent<TrackedDeviceGraphicRaycaster>().Raycast(data, results);
                Assert.IsTrue(results.Any(r => r.gameObject.transform.IsChildOf(target.transform)),
                    scene + ": el rayo desde la mano " + (side < 0 ? "izquierda" : "derecha") + " no alcanza el botón.");
            }

            // Pulsar el botón cierra el SUDS y el nivel continúa.
            target.onClick.Invoke();
            yield return null;
            Assert.IsFalse(suds.activeSelf, scene + ": el SUDS no se cerró al pulsar.");
        }

        // Si una pared está más cerca que la distancia del panel, el panel se acerca y no la atraviesa.
        [UnityTest]
        public IEnumerator Paneles_NoAtraviesanParedes()
        {
            yield return Load("Nivel5");
            Component ui = (Component)UnityEngine.Object.FindFirstObjectByType(Game("GlobalSessionUI"));
            Canvas canvas = ui.GetComponent<Canvas>();
            Camera head = canvas.worldCamera;

            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "TEST_Pared";
            wall.transform.position = head.transform.position + Vector3.ProjectOnPlane(head.transform.forward, Vector3.up).normalized * 1.0f;
            wall.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(head.transform.forward, Vector3.up));
            wall.transform.localScale = new Vector3(4f, 4f, 0.1f);
            Physics.SyncTransforms();

            Call(ui, "PlaceInFront", head.transform);
            float distance = Vector3.Distance(canvas.transform.position, head.transform.position);
            Assert.Less(distance, 1.0f - 0.05f, "El panel debe quedar delante de la pared (está a " + distance.ToString("0.00") + " m).");
            UnityEngine.Object.Destroy(wall);
        }

        private static Rect WorldRect(RectTransform rect)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Transform canvas = rect.GetComponentInParent<Canvas>().transform;
            Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
            foreach (Vector3 corner in corners)
            {
                Vector3 local = canvas.InverseTransformPoint(corner);
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
