using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;

/// <summary>
/// Aplica los ajustes de accesibilidad al personaje al cargar cada nivel: multiplica la escala del
/// XR Origin por el tamaño elegido y ajusta velocidad y giro de los proveedores de locomoción de XRI
/// (sin uso desde PlayerLocomotion, que lee los mismos ajustes en cada fotograma). Se crea solo; no
/// modifica las escenas en disco.
/// </summary>
public static class NeuroPhobiaPlayerSettings
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "MainMenu") return;

        foreach (XROrigin origin in Resources.FindObjectsOfTypeAll<XROrigin>())
        {
            if (origin.gameObject.scene != scene) continue;

            origin.transform.localScale *= NeuroPhobiaAccessibility.PlayerScale;

            foreach (ContinuousMoveProvider move in origin.GetComponentsInChildren<ContinuousMoveProvider>(true))
                move.moveSpeed *= NeuroPhobiaAccessibility.MoveSpeedScale;

            foreach (SnapTurnProvider snap in origin.GetComponentsInChildren<SnapTurnProvider>(true))
                snap.turnAmount = NeuroPhobiaAccessibility.SnapTurnDegrees;
        }
    }

    /// <summary>
    /// Aplica al momento un cambio de «Tamaño del personaje» (botón Aplicar): multiplica la escala de los rigs del
    /// nivel por <paramref name="ratio"/> (nuevo / anterior) sin mover la cabeza de sitio y con los pies en el suelo.
    /// </summary>
    public static void RescaleRigs(float ratio)
    {
        if (Mathf.Approximately(ratio, 1f) || ratio <= 0f) return;
        foreach (XROrigin origin in Object.FindObjectsByType<XROrigin>(FindObjectsSortMode.None))
        {
            Camera head = origin.Camera;
            Vector3 headBefore = head != null ? head.transform.position : origin.transform.position;
            CharacterController controller = origin.GetComponent<CharacterController>();
            bool wasEnabled = controller != null && controller.enabled;
            if (controller != null) controller.enabled = false;

            origin.transform.localScale *= ratio;
            if (head != null)
            {
                Vector3 headAfter = head.transform.position;
                origin.transform.position += new Vector3(headBefore.x - headAfter.x, 0f, headBefore.z - headAfter.z);
            }

            if (controller != null) controller.enabled = wasEnabled;
        }
    }
}
