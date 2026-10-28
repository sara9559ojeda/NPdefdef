using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Gravity;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;

/// <summary>
/// Movimiento del jugador en Nivel 1 y Nivel 2 con el joystick izquierdo (Quest).
/// Lee el stick directamente con el Input System, así no depende de las acciones del rig XRI,
/// y mueve el XR Origin con su CharacterController para respetar paredes y piso.
/// Sustituye a los proveedores de movimiento/gravedad del rig, que se desactivan al iniciar.
/// Se crea solo en Nivel1 y Nivel2; no modifica la escena.
/// </summary>
public sealed class Level1ThumbstickMove : MonoBehaviour
{
    private const float MoveSpeed = 1.6f;
    private const float Deadzone = 0.15f;
    private const float GravityAcceleration = -9.81f;
    private const float SnapTurnDegrees = 30f;
    private const float TurnEngageThreshold = 0.7f;
    private const float TurnReleaseThreshold = 0.3f;

    /// <summary>Lo activa Level2SitToggle mientras el jugador está sentado.</summary>
    public static bool MovementLocked;

    private XROrigin origin;
    private CharacterController controller;
    private InputAction moveAction;
    private InputAction turnAction;
    private bool turnArmed = true;
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
        if (scene.name != "Nivel1" && scene.name != "Nivel2") return;
        new GameObject("NP_ThumbstickMove").AddComponent<Level1ThumbstickMove>();
    }

    private void OnEnable()
    {
        moveAction = new InputAction("NP_Level1_Move", InputActionType.Value);
        moveAction.AddBinding("<XRController>{LeftHand}/thumbstick");
        moveAction.AddBinding("<Gamepad>/leftStick");
        moveAction.Enable();

        turnAction = new InputAction("NP_Level1_Turn", InputActionType.Value);
        turnAction.AddBinding("<XRController>{RightHand}/thumbstick");
        turnAction.AddBinding("<Gamepad>/rightStick");
        turnAction.Enable();
    }

    private void OnDisable()
    {
        if (moveAction == null) return;
        moveAction.Disable();
        moveAction.Dispose();
        moveAction = null;

        if (turnAction == null) return;
        turnAction.Disable();
        turnAction.Dispose();
        turnAction = null;
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
        SnapTurn();

        Vector2 input = MovementLocked ? Vector2.zero : moveAction.ReadValue<Vector2>();
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

    // Giro a saltos con el stick derecho: un empujón lateral gira SnapTurnDegrees y hay que soltar para repetir.
    private void SnapTurn()
    {
        float x = turnAction.ReadValue<Vector2>().x;

        if (turnArmed && Mathf.Abs(x) > TurnEngageThreshold)
        {
            origin.RotateAroundCameraUsingOriginUp(Mathf.Sign(x) * SnapTurnDegrees);
            turnArmed = false;
        }
        else if (Mathf.Abs(x) < TurnReleaseThreshold)
        {
            turnArmed = true;
        }
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

        foreach (SnapTurnProvider snap in rig.GetComponentsInChildren<SnapTurnProvider>(true))
        {
            snap.enabled = false;
        }

        foreach (ContinuousTurnProvider turn in rig.GetComponentsInChildren<ContinuousTurnProvider>(true))
        {
            turn.enabled = false;
        }

        foreach (GravityProvider gravity in rig.GetComponentsInChildren<GravityProvider>(true))
        {
            gravity.enabled = false;
        }
    }
}
