using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Gravity;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;

/// <summary>
/// Movimiento del jugador en el Nivel 1 con el joystick izquierdo (Quest).
/// Lee el stick directamente con el Input System, así no depende de las acciones del rig XRI,
/// y mueve el XR Origin con su CharacterController para respetar paredes y piso.
/// Sustituye a los proveedores de movimiento/gravedad del rig, que se desactivan al iniciar.
/// Se crea solo en Nivel1; no modifica la escena.
/// </summary>
public sealed class Level1ThumbstickMove : MonoBehaviour
{
    private const float MoveSpeed = 1.6f;
    private const float Deadzone = 0.15f;
    private const float GravityAcceleration = -9.81f;

    private XROrigin origin;
    private CharacterController controller;
    private InputAction moveAction;
    private float verticalVelocity;

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
        new GameObject("NP_Level1_ThumbstickMove").AddComponent<Level1ThumbstickMove>();
    }

    private void OnEnable()
    {
        moveAction = new InputAction("NP_Level1_Move", InputActionType.Value);
        moveAction.AddBinding("<XRController>{LeftHand}/thumbstick");
        moveAction.AddBinding("<Gamepad>/leftStick");
        moveAction.Enable();
    }

    private void OnDisable()
    {
        if (moveAction == null) return;
        moveAction.Disable();
        moveAction.Dispose();
        moveAction = null;
    }

    private void Update()
    {
        if (origin == null)
        {
            origin = FindFirstObjectByType<XROrigin>();
            if (origin == null) return;
            controller = origin.GetComponent<CharacterController>();
            DisableRigLocomotion(origin);
        }

        if (controller == null || !controller.enabled || origin.Camera == null) return;

        FollowHead();

        Vector2 input = moveAction.ReadValue<Vector2>();
        if (input.magnitude < Deadzone) input = Vector2.zero;

        Transform cam = origin.Camera.transform;
        Vector3 forward = Vector3.ProjectOnPlane(cam.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.ProjectOnPlane(cam.up, Vector3.up);
        forward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, forward);

        Vector3 horizontal = (forward * input.y + right * input.x) * MoveSpeed;

        verticalVelocity = controller.isGrounded ? -1f : verticalVelocity + GravityAcceleration * Time.deltaTime;

        controller.Move((horizontal + Vector3.up * verticalVelocity) * Time.deltaTime);
    }

    // Mantiene la cápsula de colisión bajo la cabeza para que las paredes detengan al jugador.
    private void FollowHead()
    {
        Vector3 head = origin.CameraInOriginSpacePos;
        float height = Mathf.Clamp(head.y, 1f, 2.2f);
        controller.height = height;
        controller.center = new Vector3(head.x, height * 0.5f + controller.skinWidth, head.z);
    }

    private static void DisableRigLocomotion(XROrigin rig)
    {
        foreach (ContinuousMoveProvider provider in rig.GetComponentsInChildren<ContinuousMoveProvider>(true))
        {
            provider.enabled = false;
        }

        foreach (GravityProvider gravity in rig.GetComponentsInChildren<GravityProvider>(true))
        {
            gravity.enabled = false;
        }
    }
}
