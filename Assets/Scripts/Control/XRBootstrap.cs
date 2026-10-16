using System.Collections;
using UnityEngine;
using UnityEngine.XR.Management;

/// <summary>
/// Inicializa el loader XR de forma manual y determinista al arrancar la app.
/// El sistema "automático" de Unity XR Management (InitManagerOnStart) depende
/// de que XRGeneralSettings se cargue como Preloaded Asset antes de
/// RuntimeInitializeLoadType.AfterAssembliesLoaded; en este proyecto ese asset
/// se activa más tarde (a mitad de la carga de la primera escena), por lo que
/// el hook automático nunca llega a inicializar el loader. Este bootstrap
/// reemplaza esa dependencia frágil con una inicialización explícita.
/// </summary>
internal static class XRBootstrapEntryPoint
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        GameObject host = new GameObject("XRBootstrap");
        Object.DontDestroyOnLoad(host);
        host.AddComponent<XRBootstrap>();
    }
}

internal sealed class XRBootstrap : MonoBehaviour
{
    private void Awake()
    {
        StartCoroutine(InitializeXR());
    }

    private IEnumerator InitializeXR()
    {
        XRGeneralSettings settings = XRGeneralSettings.Instance;

        if (settings == null || settings.Manager == null)
        {
            Debug.LogError(
                "NeuroPhobia: XRGeneralSettings o su Manager no están " +
                "disponibles; no se puede inicializar XR.");
            yield break;
        }

        XRManagerSettings manager = settings.Manager;

        if (manager.activeLoader != null)
        {
            // XR Management ya lo inicializó y arrancó (caso normal en Android/Quest).
            // Volver a arrancar los subsistemas OpenXR puede bloquear la sesión.
            Destroy(gameObject);
            yield break;
        }

        yield return manager.InitializeLoader();

        if (manager.activeLoader == null)
        {
            Debug.LogError(
                "NeuroPhobia: no se pudo inicializar ningún XR Loader " +
                "(runtime/headset no detectado).");
            yield break;
        }

        manager.StartSubsystems();

        Debug.Log(
            "NeuroPhobia: XR inicializado y subsistemas iniciados " +
            "correctamente (" + manager.activeLoader.name + ").");

        Destroy(gameObject);
    }
}
