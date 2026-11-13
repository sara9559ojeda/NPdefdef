using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;

/// <summary>
/// Single locomotion controller shared by the five levels, so the player moves the same way everywhere.
/// - Left stick: continuous movement relative to the head, at the same perceived speed in every level
///   (the speed is multiplied by the rig scale, because each level was built at a different world scale).
/// - Right stick: continuous or snap turn, chosen in the accessibility menu.
/// - No jump, teleport, climb or turn-around: every XRI locomotion provider of the rig is disabled.
/// The XR Origin is moved through its CharacterController so walls and floors are respected.
/// Created automatically in Nivel1..Nivel5; scenes are not modified.
/// </summary>
public sealed class PlayerLocomotion : MonoBehaviour
{
    /// <summary>Walking speed in metres per second as perceived by the player (rig scale 1).</summary>
    public const float PerceivedMoveSpeed = 1.2f;
    private const float ContinuousTurnDegreesPerSecond = 60f;
    private const float MoveDeadzone = 0.15f;
    private const float TurnDeadzone = 0.2f;
    private const float SnapEngageThreshold = 0.7f;
    private const float SnapReleaseThreshold = 0.3f;
    private const float CapsuleRadius = 0.25f;
    private const float StepOffset = 0.2f;
    private const float MinCapsuleHeight = 1f;
    private const float MaxCapsuleHeight = 2.2f;
    private const float GravityAcceleration = -9.81f;

    /// <summary>Set while the player is seated (Level2SitToggle): the stick does not move the rig.</summary>
    public static bool MovementLocked;

    private XROrigin origin;
    private CharacterController controller;
    private InputAction moveAction;
    private InputAction turnAction;
    private bool snapArmed = true;
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
        MovementLocked = false;
        if (!LevelScenes.IsLevel(scene.name)) return;
        new GameObject("NP_PlayerLocomotion").AddComponent<PlayerLocomotion>();
    }

    private void OnEnable()
    {
        moveAction = new InputAction("NP_Player_Move", InputActionType.Value);
        moveAction.AddBinding("<XRController>{LeftHand}/thumbstick");
        moveAction.AddBinding("<Gamepad>/leftStick");
        moveAction.Enable();

        turnAction = new InputAction("NP_Player_Turn", InputActionType.Value);
        turnAction.AddBinding("<XRController>{RightHand}/thumbstick");
        turnAction.AddBinding("<Gamepad>/rightStick");
        turnAction.Enable();
    }

    private void OnDisable()
    {
        DisposeAction(ref moveAction);
        DisposeAction(ref turnAction);
    }

    private static void DisposeAction(ref InputAction action)
    {
        if (action == null) return;
        action.Disable();
        action.Dispose();
        action = null;
    }

    private void Update()
    {
        if (origin == null && !TryBindRig()) return;
        if (controller == null || !controller.enabled || origin.Camera == null) return;

        FollowHead();
        // Panels pause the game with timeScale 0: the player neither turns nor moves while one is open.
        if (Time.timeScale <= 0f) return;

        Turn();
        if (!MovementLocked) Move();
    }

    private bool TryBindRig()
    {
        origin = FindFirstObjectByType<XROrigin>();
        if (origin == null) return false;

        controller = origin.GetComponent<CharacterController>();
        if (controller != null)
        {
            // Same body in every level: wide enough to keep the head out of walls, and a low step so the
            // player cannot climb onto tables or sofas.
            controller.radius = CapsuleRadius;
            controller.stepOffset = StepOffset;
        }

        foreach (LocomotionProvider provider in origin.GetComponentsInChildren<LocomotionProvider>(true))
        {
            provider.enabled = false;
        }
        return true;
    }

    private void Move()
    {
        Vector2 input = moveAction.ReadValue<Vector2>();
        if (input.magnitude < MoveDeadzone) input = Vector2.zero;

        Transform head = origin.Camera.transform;
        Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.ProjectOnPlane(head.up, Vector3.up);
        forward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, forward);

        float speed = PerceivedMoveSpeed * RigScale * NeuroPhobiaAccessibility.MoveSpeedScale;
        Vector3 horizontal = (forward * input.y + right * input.x) * speed;

        verticalVelocity = controller.isGrounded ? -1f : verticalVelocity + GravityAcceleration * RigScale * Time.deltaTime;
        controller.Move((horizontal + Vector3.up * verticalVelocity) * Time.deltaTime);
    }

    private void Turn()
    {
        float x = turnAction.ReadValue<Vector2>().x;

        if (NeuroPhobiaAccessibility.ContinuousTurn)
        {
            snapArmed = true;
            if (Mathf.Abs(x) < TurnDeadzone) return;
            origin.RotateAroundCameraUsingOriginUp(x * ContinuousTurnDegreesPerSecond * Time.deltaTime);
            return;
        }

        // Snap turn: one push turns one step; the stick must be released before the next one.
        if (snapArmed && Mathf.Abs(x) > SnapEngageThreshold)
        {
            origin.RotateAroundCameraUsingOriginUp(Mathf.Sign(x) * NeuroPhobiaAccessibility.SnapTurnDegrees);
            snapArmed = false;
        }
        else if (Mathf.Abs(x) < SnapReleaseThreshold)
        {
            snapArmed = true;
        }
    }

    // Keeps the collision capsule under the head so walls stop the player where the head actually is.
    private void FollowHead()
    {
        Vector3 head = origin.CameraInOriginSpacePos;
        float height = Mathf.Clamp(head.y, MinCapsuleHeight, MaxCapsuleHeight);
        controller.height = height;
        controller.center = new Vector3(head.x, height * 0.5f + controller.skinWidth, head.z);
    }

    private float RigScale => Mathf.Max(0.01f, origin.transform.lossyScale.x);
}

/// <summary>Names of the playable level scenes.</summary>
public static class LevelScenes
{
    /// <summary>Returns the level number (1-5) for a scene name such as "Nivel3", or 0 if it is not a level.</summary>
    public static int Number(string sceneName)
    {
        if (sceneName == null || !sceneName.StartsWith("Nivel")) return 0;
        return int.TryParse(sceneName.Substring(5), out int number) && number >= 1 && number <= 5 ? number : 0;
    }

    public static bool IsLevel(string sceneName) => Number(sceneName) > 0;
}
