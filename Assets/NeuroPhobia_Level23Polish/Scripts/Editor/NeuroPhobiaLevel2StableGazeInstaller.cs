#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class NeuroPhobiaLevel2StableGazeInstaller
{
    private const string ScenePath =
        "Assets/Scenes/Nivel2.unity";

    [MenuItem(
        "Tools/NeuroPhobia/Reparar mirada y temporizador Nivel 2",
        priority = 157)]
    public static void Install()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "NeuroPhobia",
                "Detén Play antes de ejecutar esta reparación.",
                "Aceptar");
            return;
        }

        if (!File.Exists(ScenePath))
        {
            EditorUtility.DisplayDialog(
                "NeuroPhobia",
                "No encontré Assets/Scenes/Nivel2.unity",
                "Aceptar");
            return;
        }

        Scene scene =
            EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Single);

        GameObject root =
            FindSceneObject(
                scene,
                "NP_Nivel2_MiradaEstable");

        if (root == null)
        {
            root =
                new GameObject(
                    "NP_Nivel2_MiradaEstable");

            SceneManager.MoveGameObjectToScene(
                root,
                scene);
        }

        NeuroPhobiaLevel2StableGaze gaze =
            root.GetComponent<
                NeuroPhobiaLevel2StableGaze>();

        if (gaze == null)
        {
            gaze =
                root.AddComponent<
                    NeuroPhobiaLevel2StableGaze>();
        }

        gaze.requiredSeconds = 30f;
        gaze.acceptedGazeAngle = 12f;
        gaze.gazeGraceSeconds = 0.35f;
        gaze.enabled = true;

        EditorUtility.SetDirty(root);
        EditorUtility.SetDirty(gaze);

        AddNivel3ToBuild();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Selection.activeGameObject = root;

        EditorUtility.DisplayDialog(
            "NeuroPhobia",
            "Nivel 2 reparado.\n\n" +
            "La mirada ahora usa la cámara activa de PC o RV.\n" +
            "Se amplió la tolerancia para que pequeños movimientos " +
            "no reinicien el tiempo.\n\n" +
            "Pulsa Play y mira la araña.",
            "Aceptar");
    }

    private static void AddNivel3ToBuild()
    {
        string nivel3 =
            "Assets/Scenes/Nivel3.unity";

        if (!File.Exists(nivel3))
        {
            return;
        }

        List<EditorBuildSettingsScene> scenes =
            new List<EditorBuildSettingsScene>(
                EditorBuildSettings.scenes);

        bool found = false;

        foreach (EditorBuildSettingsScene scene in scenes)
        {
            if (scene.path == nivel3)
            {
                scene.enabled = true;
                found = true;
                break;
            }
        }

        if (!found)
        {
            scenes.Add(
                new EditorBuildSettingsScene(
                    nivel3,
                    true));
        }

        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static GameObject FindSceneObject(
        Scene scene,
        string exactName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform found =
                FindRecursive(
                    root.transform,
                    exactName);

            if (found != null)
            {
                return found.gameObject;
            }
        }

        return null;
    }

    private static Transform FindRecursive(
        Transform current,
        string exactName)
    {
        if (current.name == exactName)
        {
            return current;
        }

        foreach (Transform child in current)
        {
            Transform found =
                FindRecursive(
                    child,
                    exactName);

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
#endif
