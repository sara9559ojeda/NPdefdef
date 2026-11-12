using System.Collections;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

/// <summary>
/// Ubica al jugador sobre el piso de Nivel5 al iniciar la escena.
/// Sin modo de seguimiento de piso, en Quest la cabeza queda a ras (o bajo) el suelo del mapa y el
/// jugador no puede moverse. Este componente fuerza el modo Floor, deja la cabeza sobre la posición
/// inicial del XR Origin y apoya el rig en el piso. Se crea solo en Nivel5; no modifica la escena.
/// </summary>
public sealed class Level5SpawnAlign : MonoBehaviour
{
    private const float MaxWaitSeconds = 10f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Nivel5") return;
        new GameObject("NP_Level5_SpawnAlign").AddComponent<Level5SpawnAlign>();
    }

    private IEnumerator Start()
    {
        float waited = 0f;
        XROrigin origin = null;

        while (origin == null && waited < MaxWaitSeconds)
        {
            origin = FindFirstObjectByType<XROrigin>();
            if (origin != null) break;
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        if (origin == null)
        {
            Debug.LogWarning("NeuroPhobia: Level5SpawnAlign no encontró XR Origin.");
            Destroy(gameObject);
            yield break;
        }

        Vector3 spawn = origin.transform.position;
        origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;

        while (waited < MaxWaitSeconds &&
               (origin.Camera == null ||
                (XRSettings.isDeviceActive && origin.CameraInOriginSpacePos.sqrMagnitude < 0.0001f)))
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        yield return new WaitForSecondsRealtime(0.5f);

        Align(origin, spawn);
        Destroy(gameObject);
    }

    private static void Align(XROrigin origin, Vector3 spawn)
    {
        Camera cam = origin.Camera;
        if (cam == null) return;

        CharacterController controller = origin.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;

        // Con seguimiento de piso la altura de ojos la da el casco: se quita la del prefab (1.36 m).
        Transform offset = origin.CameraFloorOffsetObject != null ? origin.CameraFloorOffsetObject.transform : null;
        if (offset != null && origin.CurrentTrackingOriginMode == TrackingOriginModeFlags.Floor)
        {
            Vector3 local = offset.localPosition;
            local.y = 0f;
            offset.localPosition = local;
        }

        origin.MoveCameraToWorldLocation(new Vector3(spawn.x, cam.transform.position.y, spawn.z));

        // Piso bajo el jugador: el más alto que esté cerca de la altura inicial, sin contar al propio rig.
        RaycastHit[] hits = Physics.RaycastAll(
            spawn + Vector3.up * 2f, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore);
        float floorY = float.MinValue;
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.IsChildOf(origin.transform)) continue;
            if (Mathf.Abs(hit.point.y - spawn.y) > 0.5f) continue;
            if (hit.point.y > floorY) floorY = hit.point.y;
        }

        Vector3 position = origin.transform.position;
        position.y = floorY > float.MinValue ? floorY : Mathf.Max(position.y, spawn.y);
        origin.transform.position = position;

        if (controller != null) controller.enabled = true;

        Debug.Log("NeuroPhobia: jugador ubicado en Nivel5 -> " + cam.transform.position);
    }
}
