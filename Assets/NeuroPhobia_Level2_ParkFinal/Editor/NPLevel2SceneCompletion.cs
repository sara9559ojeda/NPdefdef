#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// One-shot, Nivel2-only scene assembly using the park generator shipped with this package.
public static class NPLevel2SceneCompletion
{
    private const string ScenePath = "Assets/Scenes/Nivel2.unity";
    private const string SpiderModel = "Assets/NeuroPhobia_Levels123/External/Nivel2/WolfSpider/source/Wolf_spider.fbx";

    [MenuItem("Tools/NeuroPhobia/Nivel 2/Completar escena y jugabilidad")]
    public static void Run()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (Find(scene, "NP_ENV_NIVEL2_PARQUE_GRANDE_FINAL") == null)
        {
            NeuroPhobiaNivel2ParqueGrandeFinal.Build();
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        var park = Find(scene, "NP_ENV_NIVEL2_PARQUE_GRANDE_FINAL");
        var xr = Find(scene, "XR Origin (XR Rig)");
        if (park == null || xr == null) throw new InvalidOperationException("Faltan el parque o XR Origin en Nivel2.");

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(SpiderModel);
        if (model == null) throw new InvalidOperationException("No se encontró el modelo Wolf_spider.fbx.");

        var spider = Find(scene, "NP_WolfSpider_Nivel2_Static");
        if (spider == null)
        {
            spider = new GameObject("NP_WolfSpider_Nivel2_Static");
            SceneManager.MoveGameObjectToScene(spider, scene);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
            visual.name = "Visual_WolfSpider";
            visual.transform.SetParent(spider.transform, false);
            var renderers = visual.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException("El modelo de araña carece de Renderer.");
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            float maxSize = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (maxSize > 0.001f) visual.transform.localScale *= 0.55f / maxSize;
            bounds = visual.GetComponentsInChildren<Renderer>(true)[0].bounds;
            foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true).Skip(1)) bounds.Encapsulate(renderer.bounds);
            visual.transform.position -= new Vector3(bounds.center.x - spider.transform.position.x, bounds.min.y - spider.transform.position.y, bounds.center.z - spider.transform.position.z);
            var collider = spider.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.25f, 0f);
            collider.size = new Vector3(0.65f, 0.5f, 0.65f);
            spider.AddComponent<Level2SpiderIdleLife>();
        }

        spider.transform.position = park.transform.position + new Vector3(0f, 0f, 4.5f);
        xr.transform.position = park.transform.position + new Vector3(0f, 0f, -11f);
        xr.transform.rotation = Quaternion.identity;
        xr.SetActive(true);

        var setup = Find(scene, "NP_NIVEL2_FINAL");
        if (setup == null)
        {
            setup = new GameObject("NP_NIVEL2_FINAL");
            SceneManager.MoveGameObjectToScene(setup, scene);
        }

        var gaze = setup.GetComponent<NeuroPhobiaLevel2StableGaze>() ?? setup.AddComponent<NeuroPhobiaLevel2StableGaze>();
        gaze.requiredSeconds = 30f;
        gaze.acceptedGazeAngle = 12f;
        gaze.gazeGraceSeconds = 0.35f;
        gaze.spiderRenderer = spider.GetComponentInChildren<Renderer>(true);

        var safe = setup.GetComponent<NeuroPhobiaLevel2SafeDistance>() ?? setup.AddComponent<NeuroPhobiaLevel2SafeDistance>();
        safe.spiderTarget = spider.transform;
        safe.xrOrigin = xr;
        safe.minimumDistance = 8f;
        safe.boundaryPadding = 0.08f;

        var metrics = setup.GetComponent<MetricsCollector>() ?? setup.AddComponent<MetricsCollector>();
        metrics.level = 2;
        metrics.spider = spider.transform;
        var camera = xr.GetComponentInChildren<Camera>(true);
        if (camera == null) throw new InvalidOperationException("XR Origin no tiene cámara.");
        metrics.player = camera.transform;
        gaze.metricsCollector = metrics;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Nivel2: parque, araña, XR, mirada, 8 m y métricas conectados.");
    }

    private static GameObject Find(Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var match = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
            if (match != null) return match.gameObject;
        }
        return null;
    }
}
#endif
