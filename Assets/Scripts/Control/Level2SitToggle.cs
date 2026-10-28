using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Permite sentarse y pararse en Nivel2 con el botón A del mando derecho (C en teclado).
/// Sentado baja la vista hasta una altura de ojos de silla y bloquea el movimiento con el joystick;
/// al pararse restaura ambas cosas. Se crea solo en Nivel2; no modifica la escena.
/// </summary>
public sealed class Level2SitToggle : MonoBehaviour
{
    private const float SeatedEyeHeight = 1.1f;
    private const float MaxDrop = 0.9f;

    private XROrigin origin;
    private Transform cameraOffset;
    private Vector3 standingOffset;
    private InputAction toggleAction;
    private bool seated;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Nivel2") return;
        new GameObject("NP_Level2_SitToggle").AddComponent<Level2SitToggle>();
    }

    private void OnEnable()
    {
        toggleAction = new InputAction("NP_Level2_Sit", InputActionType.Button);
        toggleAction.AddBinding("<XRController>{RightHand}/primaryButton");
        toggleAction.AddBinding("<Keyboard>/c");
        toggleAction.Enable();
    }

    private void OnDisable()
    {
        if (toggleAction != null)
        {
            toggleAction.Disable();
            toggleAction.Dispose();
            toggleAction = null;
        }

        StandUp();
    }

    private void Update()
    {
        if (origin == null)
        {
            origin = FindFirstObjectByType<XROrigin>();
            if (origin == null || origin.CameraFloorOffsetObject == null) { origin = null; return; }
            cameraOffset = origin.CameraFloorOffsetObject.transform;
            standingOffset = cameraOffset.localPosition;
        }

        if (toggleAction.WasPressedThisFrame())
        {
            if (seated) StandUp();
            else SitDown();
        }
    }

    private void SitDown()
    {
        if (seated || cameraOffset == null) return;

        // Altura actual de la cabeza sobre el piso del rig, para bajar solo lo necesario.
        float head = origin.CameraInOriginSpacePos.y;
        float drop = Mathf.Clamp(head - SeatedEyeHeight, 0f, MaxDrop);

        Vector3 position = standingOffset;
        position.y -= drop;
        cameraOffset.localPosition = position;

        seated = true;
        Level1ThumbstickMove.MovementLocked = true;
    }

    private void StandUp()
    {
        if (!seated) return;

        if (cameraOffset != null) cameraOffset.localPosition = standingOffset;
        seated = false;
        Level1ThumbstickMove.MovementLocked = false;
    }
}
