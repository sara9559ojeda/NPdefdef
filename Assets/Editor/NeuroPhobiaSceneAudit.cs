#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Inventario de puertas, cajones, ventanas y luces del Nivel5 (y orbes/arañas del Nivel3) para el informe.
/// Uso en batch: -executeMethod NeuroPhobiaSceneAudit.Run  (escribe Logs/NeuroPhobiaSceneAudit.txt).
/// No modifica las escenas.
/// </summary>
public static class NeuroPhobiaSceneAudit
{
    private static readonly string[] Keywords = { "door", "puerta", "drawer", "cajon", "cajón", "fridge", "nevera", "window", "ventana", "curtain", "cortina", "lamp", "lampara", "lámpara", "switch", "wardrobe", "armario", "cabinet", "TASK_" };

    [MenuItem("NeuroPhobia/Auditar escenas (Nivel3 y Nivel5)")]
    public static void Run()
    {
        var text = new StringBuilder();
        Audit("Assets/Scenes/Nivel5.unity", text);
        Audit("Assets/Scenes/Nivel3.unity", text);
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/NeuroPhobiaSceneAudit.txt", text.ToString());
        Debug.Log("NeuroPhobiaSceneAudit -> Logs/NeuroPhobiaSceneAudit.txt");
    }

    private static void Audit(string path, StringBuilder text)
    {
        var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        text.AppendLine("==== " + path);
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                string lower = t.name.ToLowerInvariant();
                bool rig = t.root.name.StartsWith("XR Origin") && t.GetComponents<Behaviour>().Length > 0;
                bool interesting = rig || Keywords.Any(k => lower.Contains(k.ToLowerInvariant())) || t.GetComponent<Light>() != null ||
                                   lower.Contains("orbe") || lower.Contains("spider") || lower.Contains("arana");
                if (!interesting) continue;
                Collider[] colliders = t.GetComponents<Collider>();
                Renderer renderer = t.GetComponent<Renderer>();
                GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
                string prefabRot = "";
                if (source != null)
                {
                    Quaternion delta = Quaternion.Inverse(source.transform.localRotation) * t.localRotation;
                    float angle = Quaternion.Angle(Quaternion.identity, delta);
                    prefabRot = angle > 1f ? " ROT≠PREFAB(" + angle.ToString("0") + "°, prefab " + source.transform.localEulerAngles + ")" : "";
                }
                text.Append(PathOf(t)).Append(" | active=").Append(t.gameObject.activeInHierarchy)
                    .Append(" pos=").Append(t.position.ToString("F2")).Append(" rotL=").Append(t.localEulerAngles.ToString("F0"))
                    .Append(" colliders=").Append(string.Join(",", colliders.Select(c => c.GetType().Name + (c.isTrigger ? "(trigger)" : "") + (c.enabled ? "" : "(off)"))))
                    .Append(renderer != null ? " bounds=" + renderer.bounds.size.ToString("F2") : "")
                    .Append(t.GetComponent<Light>() != null ? " LIGHT(" + t.GetComponent<Light>().type + ",on=" + t.GetComponent<Light>().enabled + ")" : "")
                    .Append(" comps=").Append(string.Join(",", t.GetComponents<Component>().Where(c => c != null && !(c is Transform) && !(c is MeshFilter) && !(c is Renderer) && !(c is Collider)).Select(c => c.GetType().Name + (c is Behaviour b && !b.enabled ? "(off)" : ""))))
                    .Append(prefabRot).AppendLine();
            }
        }
    }

    private static string PathOf(Transform t)
    {
        string path = t.name;
        while (t.parent != null) { t = t.parent; path = t.name + "/" + path; }
        return path;
    }
}
#endif

#if UNITY_EDITOR
public static class NeuroPhobiaDrawerAudit
{
    // Uso: -executeMethod NeuroPhobiaDrawerAudit.Run → Logs/NeuroPhobiaDrawerAudit.txt
    public static void Run()
    {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Nivel5.unity");
        var text = new System.Text.StringBuilder();
        foreach (Transform t in GameObject.Find("NP_L5_APARTAMENTO_VISUAL").GetComponentsInChildren<Transform>(true))
        {
            if (!Level5InteractiveHome.IsDrawer(t.name.ToLowerInvariant())) continue;
            Transform body = null;
            foreach (Transform c in t.parent) if (c.name.ToLowerInvariant().EndsWith("_body")) body = c;
            text.Append(t.parent.name + "/" + t.name + " localPos=" + t.localPosition.ToString("F3") + " localRot=" + t.localEulerAngles.ToString("F0"));
            if (body != null)
            {
                Bounds d = Local(t), b = Local(body);
                text.Append(" drawer[" + d.min.ToString("F3") + ".." + d.max.ToString("F3") + "] body[" + b.min.ToString("F3") + ".." + b.max.ToString("F3") + "]");
            }
            text.AppendLine();
        }
        System.IO.File.WriteAllText("Logs/NeuroPhobiaDrawerAudit.txt", text.ToString());
    }

    private static Bounds Local(Transform t)
    {
        Bounds mesh = t.GetComponent<MeshFilter>().sharedMesh.bounds;
        Matrix4x4 m = Matrix4x4.TRS(t.localPosition, t.localRotation, t.localScale);
        Bounds r = new Bounds(m.MultiplyPoint3x4(mesh.min), Vector3.zero);
        for (int i = 0; i < 8; i++)
            r.Encapsulate(m.MultiplyPoint3x4(mesh.center + Vector3.Scale(mesh.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
        return r;
    }
}
#endif
