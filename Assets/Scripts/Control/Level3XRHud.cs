using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

/// <summary>
/// Los HUD de Nivel3 son Canvas ScreenSpaceOverlay: se ven en la vista Game pero no en el visor XR.
/// Con visor activo se pasan a WorldSpace pegados a la cabeza, delante de la cámara.
/// Se revisa cada frame porque algunos HUD se crean en runtime y XR inicia de forma asíncrona.
/// Se crea solo en Nivel3; no modifica la escena.
/// </summary>
public sealed class Level3XRHud : MonoBehaviour
{
    private const float LayerSpacing = 0.01f;

    private readonly List<Canvas> converted = new List<Canvas>();
    private Camera cam;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Nivel3") return;
        new GameObject("NP_Level3_XRHud").AddComponent<Level3XRHud>();
    }

    private void LateUpdate()
    {
        if (!XRSettings.isDeviceActive) return;

        if (cam == null || !cam.isActiveAndEnabled)
        {
            XROrigin origin = FindFirstObjectByType<XROrigin>();
            cam = origin != null && origin.Camera != null ? origin.Camera : Camera.main;
            if (cam == null) return;
        }

        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (canvas.isRootCanvas && canvas.renderMode == RenderMode.ScreenSpaceOverlay && !converted.Contains(canvas))
            {
                Convert(canvas);
            }
        }

        converted.RemoveAll(c => c == null);
    }

    // Antes se pegaban a la cabeza a 1 m, en el centro de la vista. Ahora van en el mundo, lejos (2,4 m), con el
    // centro libre, y siguen la vista con retardo (NeuroPhobiaHudFollow).
    private void Convert(Canvas canvas)
    {
        NeuroPhobiaHudFollow follow = NeuroPhobiaHudFollow.AttachFullScreen(canvas);
        if (follow != null) follow.distance += converted.Count * LayerSpacing;
        converted.Add(canvas);
    }
}
