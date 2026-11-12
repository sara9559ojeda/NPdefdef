using System;
using System.Collections;
using System.IO;
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
    /// Capturas de evidencia (no es una prueba de aceptación): renderiza vistas del Nivel3 y del Nivel5 a PNG en
    /// Logs/capturas_agente/. Solo se ejecuta a mano (Explicit): -testFilter NeuroPhobia.Tests.CapturasPlayModeTests
    /// Son capturas del Editor, no del visor: la validación en las gafas la hace Sara.
    /// </summary>
    [Explicit("Solo genera capturas de evidencia")]
    public class CapturasPlayModeTests
    {
        private const string Folder = "Logs/capturas_agente";

        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            Directory.CreateDirectory(Folder);
            object session = Static("SessionManager", "Instance");
            if (session == null || (UnityEngine.Object)session == null) session = new GameObject("TEST_SessionManager").AddComponent(Game("SessionManager"));
            Call(session, "StartSession", 3, true);
        }

        [TearDown]
        public void TearDown()
        {
            object session = Static("SessionManager", "Instance");
            if (session != null && (UnityEngine.Object)session != null) UnityEngine.Object.DestroyImmediate(((Component)session).gameObject);
            Time.timeScale = 1f;
        }

        private static Type Game(string name) => Type.GetType(name + ", Assembly-CSharp", true);
        private static object Static(string t, string m) => Game(t).GetProperty(m, BindingFlags.Public | BindingFlags.Static).GetValue(null);
        private static object Get(object target, string member)
        {
            Type type = target.GetType();
            PropertyInfo p = type.GetProperty(member, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (p != null) return p.GetValue(target);
            return type.GetField(member, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);
        }
        private static object Call(object target, string method, params object[] args) =>
            target.GetType().GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args);

        private static IEnumerator Load(string scene, bool keepSuds = false)
        {
            Time.timeScale = 1f;
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/" + scene + ".unity", new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync(scene);
#endif
            for (int i = 0; i < 4; i++) yield return null;
            if (!keepSuds) yield return Nivel5PlayModeTests.SkipSudsBefore();
            // En el Editor sin visor la cámara del rig XR se queda a ras de suelo: se simula una persona de pie (1,6 m).
            Camera head = Head();
            if (head != null)
            {
                RaycastHit[] below = Physics.RaycastAll(head.transform.position + Vector3.up * 0.2f, Vector3.down, 5f, ~0, QueryTriggerInteraction.Ignore)
                    .Where(h => h.normal.y > 0.9f && !h.collider.transform.IsChildOf(head.transform.root)).ToArray();
                float floorY = below.Length > 0 ? below.Min(h => h.point.y) : head.transform.position.y;
                if (head.transform.position.y - floorY < 1.0f)
                    head.transform.position = new Vector3(head.transform.position.x, floorY + 1.6f, head.transform.position.z);
            }
            for (int i = 0; i < 30; i++) yield return null;
        }

        private static Camera Head()
        {
            Camera head = Camera.main;
            if (head == null) head = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).FirstOrDefault(c => c.isActiveAndEnabled);
            return head;
        }

        private static void Shot(Camera camera, string name)
        {
            var rt = new RenderTexture(1600, 1000, 24);
            RenderTexture previous = camera.targetTexture;
            float fov = camera.fieldOfView;
            camera.fieldOfView = 80f;
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(Folder, name + ".png"), tex.EncodeToPNG());
            camera.targetTexture = previous;
            camera.fieldOfView = fov;
            RenderTexture.active = null;
            UnityEngine.Object.Destroy(rt);
            UnityEngine.Object.Destroy(tex);
        }

        private static void LookAt(Camera camera, Vector3 target, Vector3 fromDirection, float distance, float height = 1.6f)
        {
            Vector3 dir = fromDirection; dir.y = 0f; dir.Normalize();
            camera.transform.position = new Vector3(target.x, height, target.z) + dir * distance;
            camera.transform.rotation = Quaternion.LookRotation(target - camera.transform.position, Vector3.up);
        }

        private static Bounds BoundsOf(GameObject go)
        {
            Renderer[] rs = go.GetComponentsInChildren<Renderer>();
            Bounds b = rs[0].bounds;
            foreach (Renderer r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        // Frente de un mueble: del centro de su cuerpo hacia el centro de sus hojas y cajones.
        private static Vector3 FrontOf(GameObject root)
        {
            Renderer body = root.GetComponentsInChildren<Renderer>().FirstOrDefault(r => r.name.ToLowerInvariant().EndsWith("_body"));
            Component[] pieces = root.GetComponentsInChildren<Component>().Where(c => c != null &&
                (c.GetType().Name == "Level5HingeHandle" || c.GetType().Name == "Level5SlideHandle")).ToArray();
            if (body == null || pieces.Length == 0) return ApartmentCenter() - BoundsOf(root).center;
            Vector3 avg = Vector3.zero;
            foreach (Component p in pieces) avg += p.GetComponent<Renderer>() != null ? p.GetComponent<Renderer>().bounds.center : p.transform.position;
            avg /= pieces.Length;
            Vector3 dir = avg - body.bounds.center; dir.y = 0f;
            return dir.sqrMagnitude > 0.0001f ? dir : ApartmentCenter() - BoundsOf(root).center;
        }

        private static Vector3 ApartmentCenter() => BoundsOf(GameObject.Find("NP_L5_APARTAMENTO_VISUAL")).center;

        [UnityTest]
        public IEnumerator Capturas_PanelesDeSesion()
        {
            foreach (string scene in new[] { "Nivel1", "Nivel3", "Nivel5" })
            {
                yield return Load(scene, keepSuds: true);
                yield return null;
                Shot(Head(), scene + "_panel_SUDS_antes");
            }
        }

        [UnityTest]
        public IEnumerator Capturas_Nivel5Casa()
        {
            yield return Load("Nivel5");
            Camera cam = Head();
            Vector3 inside = ApartmentCenter();
            foreach (Behaviour b in UnityEngine.Object.FindObjectsByType<Behaviour>(FindObjectsSortMode.None).Where(b => b.GetType().Name.Contains("Desktop")))
                b.enabled = false;

            GameObject wardrobe = GameObject.Find("Armario_Cuarto");
            Bounds wb = BoundsOf(wardrobe);
            LookAt(cam, wb.center, FrontOf(wardrobe), 2.6f);
            Shot(cam, "Nivel5_armario_cerrado");

            GameObject dresser = GameObject.Find("Comoda_Cuarto");
            if (dresser != null)
            {
                Bounds db = BoundsOf(dresser);
                LookAt(cam, db.center, FrontOf(dresser), 1.8f);
                Shot(cam, "Nivel5_comoda_cajones_cerrados");
            }

            object fridgeTask = UnityEngine.Object.FindObjectsByType(Game("Level5TaskInteraction"), FindObjectsSortMode.None).Cast<Component>().First(t => (string)Get(t, "taskId") == "TASK_FRIDGE");
            GameObject fridge = ((Component)fridgeTask).gameObject;
            Bounds fb = BoundsOf(fridge);
            LookAt(cam, fb.center, FrontOf(fridge), 2.2f);
            Shot(cam, "Nivel5_nevera_cerrada");
            object hinge = Get(fridgeTask, "hinge");
            Call(hinge, "SetAngle", (float)Get(hinge, "openAngle"));
            yield return null;
            Shot(cam, "Nivel5_nevera_abierta");

            GameObject kitchen = GameObject.Find("Isla_Cocina");
            if (kitchen != null)
            {
                Bounds kb = BoundsOf(kitchen);
                LookAt(cam, kb.center, FrontOf(kitchen), 2.4f);
                Shot(cam, "Nivel5_cocina_cajones_y_gabinetes");
            }

            object doorTask = UnityEngine.Object.FindObjectsByType(Game("Level5TaskInteraction"), FindObjectsSortMode.None).Cast<Component>().First(t => (string)Get(t, "taskId") == "TASK_DOOR");
            Component doorHinge = (Component)Get(doorTask, "hinge");
            Bounds lb = doorHinge.GetComponent<Collider>().bounds;
            LookAt(cam, lb.center, Vector3.back + Vector3.right * 0.6f, 2.6f);
            Shot(cam, "Nivel5_puerta_cerrada");
            Call(doorHinge, "SetAngle", (float)Get(doorHinge, "openAngle"));
            yield return null;
            Shot(cam, "Nivel5_puerta_abierta");

            object lampTask = UnityEngine.Object.FindObjectsByType(Game("Level5TaskInteraction"), FindObjectsSortMode.None).Cast<Component>().First(t => (string)Get(t, "taskId") == "TASK_LAMP");
            Component lampSwitch = (Component)Get(lampTask, "lampSwitch");
            LookAt(cam, lampSwitch.transform.position, lampSwitch.transform.forward, 1.6f, 1.5f);
            Shot(cam, "Nivel5_lampara_encendida_interruptor");
            Call(lampSwitch, "SetOn", false);
            yield return null;
            Shot(cam, "Nivel5_lampara_apagada");

            GameObject glass = GameObject.Find("WindowGlass2m");
            Bounds gb = glass.GetComponent<Collider>().bounds;
            LookAt(cam, gb.center, inside - gb.center, 2.4f, 1.5f);
            Shot(cam, "Nivel5_ventana_exterior_y_arana");
            object windowTask = UnityEngine.Object.FindObjectsByType(Game("Level5TaskInteraction"), FindObjectsSortMode.None).Cast<Component>().First(t => (string)Get(t, "taskId") == "TASK_WINDOW");
            object slide = Get(windowTask, "slide");
            Call(slide, "SetOffset", (float)Get(slide, "travel"));
            yield return null;
            Shot(cam, "Nivel5_ventana_abierta");

            // Vista general con el HUD de tareas.
            yield return null;
            Shot(Head(), "Nivel5_hud_tareas");
        }

        [UnityTest]
        public IEnumerator Capturas_Nivel3()
        {
            yield return Load("Nivel3");
            yield return new WaitForSeconds(1f);
            Camera cam = Head();
            Shot(cam, "Nivel3_inicio_hud_mision");
            Component mission = (Component)UnityEngine.Object.FindFirstObjectByType(Game("NPLevel3RealOrbs"));
            GameObject orb = (GameObject)Get(mission, "orb1");
            foreach (Behaviour b in UnityEngine.Object.FindObjectsByType<Behaviour>(FindObjectsSortMode.None).Where(b => b.GetType().Name.Contains("Desktop")))
                b.enabled = false;
            cam.transform.position = orb.transform.position + new Vector3(4f, 1.6f, 4f);
            cam.transform.rotation = Quaternion.LookRotation(orb.transform.position - cam.transform.position);
            yield return null;
            Shot(cam, "Nivel3_primer_orbe_y_aranas");
        }
    }
}
