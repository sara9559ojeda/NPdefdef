using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

/// <summary>
/// Protección para los builds: el XR Device Simulator solo sirve en el Editor.
/// En un build (Quest o PC) se desactiva al cargar cada escena, para que nunca
/// se quede con los mandos reales.
/// </summary>
public static class NoDeviceSimulatorInBuild
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
#if !UNITY_EDITOR
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        DisableAll();
#endif
    }

#if !UNITY_EDITOR
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        DisableAll();
    }
#endif

    /// <summary>Desactiva todo XRDeviceSimulator que haya en las escenas cargadas. Devuelve cuántos.</summary>
    public static int DisableAll()
    {
        int count = 0;
        foreach (XRDeviceSimulator simulator in Object.FindObjectsByType<XRDeviceSimulator>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            simulator.gameObject.SetActive(false);
            count++;
        }
        return count;
    }
}
