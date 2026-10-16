using System.Collections;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

/// <summary>
/// Ubica al jugador dentro de la sala del Nivel 1 al iniciar la escena.
/// En Quest el origen de seguimiento no coincide con el lugar físico donde está parada la
/// persona, así que mover solo el XR Origin puede dejar la cabeza fuera de la habitación.
/// Este componente mueve el origen para que la cabeza real quede en HeadSpawn y mire hacia
/// las tarjetas (FacingYaw). No modifica la escena: se crea solo en Nivel1.
/// </summary>
public sealed class Level1SpawnAlign : MonoBehaviour
{
    // Posición de la cabeza en el mundo (x, z) y orientación horizontal inicial.
    private static readonly Vector3 HeadSpawn = new Vector3(0.3f, 0f, -39.7f);
    private const float FacingYaw = 40f;
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
        if (scene.name != "Nivel1") return;
        new GameObject("NP_Level1_SpawnAlign").AddComponent<Level1SpawnAlign>();
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
            Debug.LogWarning("NeuroPhobia: Level1SpawnAlign no encontró XR Origin.");
            Destroy(gameObject);
            yield break;
        }

        // Esperar a que el casco entregue una pose válida (cámara fuera del origen).
        while (waited < MaxWaitSeconds &&
               (origin.Camera == null ||
                (XRSettings.isDeviceActive && origin.CameraInOriginSpacePos.sqrMagnitude < 0.0001f)))
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        yield return new WaitForSecondsRealtime(0.5f);

        Align(origin);
        Destroy(gameObject);
    }

    private static void Align(XROrigin origin)
    {
        Camera cam = origin.Camera;
        if (cam == null) return;

        CharacterController controller = origin.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;

        Vector3 forward = cam.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f) forward = origin.transform.forward;

        float currentYaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        origin.RotateAroundCameraUsingOriginUp(Mathf.DeltaAngle(currentYaw, FacingYaw));
        origin.MoveCameraToWorldLocation(new Vector3(HeadSpawn.x, cam.transform.position.y, HeadSpawn.z));

        if (controller != null) controller.enabled = true;

        Debug.Log("NeuroPhobia: jugador ubicado en Nivel1 -> " + cam.transform.position);
    }
}
