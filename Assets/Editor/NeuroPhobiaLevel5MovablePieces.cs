#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Las piezas que se mueven en el Nivel5 (hojas de puertas y muebles, cajones, cristal de la ventana) venían de los
/// prefabs de nappin marcados como estáticos del todo (m_StaticEditorFlags = 2147483647, incluido Batching Static).
/// Unity combina esas mallas al cargar la escena y luego no las mueve aunque se gire su Transform: por eso las puertas
/// no se veían abrirse en el visor. Esta herramienta quita Batching Static y la oclusión a esas piezas (sobrescritura de
/// la instancia del prefab, guardada en Nivel5.unity). Usa las mismas reglas de nombres que Level5InteractiveHome.
/// Uso en batch: -executeMethod NeuroPhobiaLevel5MovablePieces.Run
/// </summary>
public static class NeuroPhobiaLevel5MovablePieces
{
    private const StaticEditorFlags Removed = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic;

    [MenuItem("NeuroPhobia/Nivel5: quitar Batching Static a las piezas móviles")]
    public static void Run()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Nivel5.unity", OpenSceneMode.Single);
        GameObject apartment = GameObject.Find(Level5InteractiveHome.ApartmentName);
        var report = new StringBuilder();
        int changed = 0;
        foreach (Transform piece in MovablePieces(apartment.transform))
        {
            foreach (Transform t in piece.GetComponentsInChildren<Transform>(true))
            {
                StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(t.gameObject);
                if ((flags & Removed) == 0) continue;
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags & ~Removed);
                report.AppendLine(Path(t));
                changed++;
            }
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/NeuroPhobiaLevel5MovablePieces.txt", changed + " objetos sin Batching Static:\n" + report);
        Debug.Log("NeuroPhobiaLevel5MovablePieces: " + changed + " objetos actualizados");
    }

    public static IEnumerable<Transform> MovablePieces(Transform apartment)
    {
        foreach (Transform t in apartment.GetComponentsInChildren<Transform>(true))
        {
            string name = t.name.ToLowerInvariant();
            if ((Level5InteractiveHome.IsLeaf(name) && !Level5InteractiveHome.IsLocked(t)) || Level5InteractiveHome.IsDrawer(name) ||
                t.name == Level5InteractiveHome.WindowGlassName)
                yield return t;
        }
    }

    private static string Path(Transform t)
    {
        string path = t.name;
        while (t.parent != null) { t = t.parent; path = t.name + "/" + path; }
        return path;
    }
}
#endif
