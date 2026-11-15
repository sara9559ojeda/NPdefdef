using System.Collections;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

/// <summary>
/// Sitting is the one action that exists only in Nivel2. With the A button of the right controller (C on a
/// keyboard) the player sits on the armchair or the sofa and stands up again, but only next to one.
/// - Next to a seat the "A · Sentarte" badge shows over it and the right hand vibrates once.
/// - Sitting: the view fades, the player is placed ON the cushion (measured by Level2Armchairs: hips over the
///   cushion, eyes at seated height, facing the front of the seat, never inside the furniture), the view opens and
///   settles down a few centimetres, like sitting down. Movement with the stick is locked while seated; turning works.
/// - Standing up fades again and returns the player to where they were standing, next to the seat.
/// Created automatically in Nivel2; the scene is not modified.
/// </summary>
public sealed class Level2SitToggle : MonoBehaviour
{
    private const string SitPrompt = "A · Sentarte";
    private const string StandHint = "Pulsa A para levantarte.";
    private const float StandHintSeconds = 5f;
    // Perceived metres (multiplied by the rig scale of the level).
    private const float SeatedEyeAboveCushion = 0.72f;
    private const float EyesAheadOfHips = 0.08f;
    private const float SettleDrop = 0.08f;
    private const float ZoneMargin = 0.6f;
    private const float ZoneHeight = 2.6f;
    private const float FadeSeconds = 0.3f;
    private const float SettleSeconds = 0.35f;
    private const int IgnoreRaycastLayer = 2;

    private sealed class Seat
    {
        public Level2Armchairs.Seat info;
        public BoxCollider zone;
        public Vector3 promptPoint;
    }

    private readonly List<Seat> seats = new List<Seat>();
    private XROrigin origin;
    private Transform cameraOffset;
    private InputAction toggleAction;
    private Seat nearbySeat;
    private bool seated;
    private bool busy;
    private Vector3 standingOffset;
    private Vector3 standingPosition;
    private Quaternion standingRotation;

    /// <summary>True while the player is seated (for tests).</summary>
    public bool IsSeated => seated;

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

        // Leaving the level while seated: restore the rig without a transition.
        if (seated) RestoreStanding();
        InteractionPrompt.Hide(this);
    }

    private void Update()
    {
        if (origin == null && !TryBindRig()) return;
        if (origin.Camera == null) return;

        UpdateNearbySeat();

        bool playing = GameStateManager.CurrentState == GameState.PLAYING;
        if (busy || !playing || !toggleAction.WasPressedThisFrame()) return;

        if (seated) StartCoroutine(StandUp());
        else if (nearbySeat != null) StartCoroutine(SitDown(nearbySeat));
    }

    private bool TryBindRig()
    {
        origin = FindFirstObjectByType<XROrigin>();
        if (origin == null || origin.CameraFloorOffsetObject == null)
        {
            origin = null;
            return false;
        }
        cameraOffset = origin.CameraFloorOffsetObject.transform;
        BuildSeats();
        return true;
    }

    private float RigScale => Mathf.Max(0.01f, origin.transform.lossyScale.y);

    // One zone per seat, around the furniture (which is now solid), tall enough for the standing head.
    private void BuildSeats()
    {
        float rigScale = RigScale;
        foreach (Level2Armchairs.Seat info in Level2Armchairs.All())
        {
            Bounds bounds = info.bounds;
            float margin = ZoneMargin * rigScale;
            float height = ZoneHeight * rigScale;
            GameObject zoneObject = new GameObject("NP_Seat_Zone_" + info.root.name);
            zoneObject.layer = IgnoreRaycastLayer;   // does not block the controller rays or the gaze
            zoneObject.transform.position = new Vector3(bounds.center.x, bounds.min.y + height * 0.5f, bounds.center.z);
            BoxCollider zone = zoneObject.AddComponent<BoxCollider>();
            zone.isTrigger = true;
            zone.size = new Vector3(bounds.size.x + margin * 2f, height, bounds.size.z + margin * 2f);

            seats.Add(new Seat
            {
                info = info,
                zone = zone,
                promptPoint = new Vector3(info.seatPoint.x, bounds.max.y + 0.15f * rigScale, info.seatPoint.z)
            });
        }
    }

    // Tracks which seat zone the head is in; entering one shows the badge and vibrates the right hand.
    private void UpdateNearbySeat()
    {
        Seat current = null;
        if (!seated && !busy)
        {
            Vector3 head = origin.Camera.transform.position;
            foreach (Seat seat in seats)
            {
                if (seat.zone != null && seat.zone.bounds.Contains(head)) { current = seat; break; }
            }
        }

        bool playing = GameStateManager.CurrentState == GameState.PLAYING;
        if (current != null && playing)
        {
            if (current != nearbySeat) ControllerHaptics.Pulse(XRNode.RightHand);
            if (!InteractionPrompt.IsShownBy(this)) InteractionPrompt.Show(this, current.promptPoint, SitPrompt);
        }
        else
        {
            InteractionPrompt.Hide(this);
        }
        nearbySeat = current;
    }

    private IEnumerator SitDown(Seat seat)
    {
        busy = true;
        InteractionPrompt.Hide(this);
        yield return ScreenFader.FadeOut(FadeSeconds);

        standingOffset = cameraOffset.localPosition;
        standingPosition = origin.transform.position;
        standingRotation = origin.transform.rotation;

        CharacterController controller = origin.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;

        float rigScale = RigScale;
        Level2Armchairs.Seat info = seat.info;

        // Facing the front of the seat (the spider jar stands in front of the armchair).
        FaceDirection(info.front);

        // Eyes over the hips, at seated height above the cushion; a few centimetres higher before the settle.
        Vector3 eyes = info.seatPoint + info.front * (EyesAheadOfHips * rigScale) +
                       Vector3.up * ((SeatedEyeAboveCushion + SettleDrop) * rigScale);
        float lift = eyes.y - origin.Camera.transform.position.y;
        cameraOffset.localPosition += Vector3.up * (lift / rigScale);
        RigPlacement.MoveCameraTo(origin, eyes);

        if (controller != null) controller.enabled = true;
        seated = true;
        PlayerLocomotion.MovementLocked = true;

        yield return ScreenFader.FadeIn(FadeSeconds);

        // Settle: the view sinks gently into the cushion.
        Vector3 start = cameraOffset.localPosition;
        Vector3 end = start - Vector3.up * SettleDrop;
        for (float t = 0f; t < SettleSeconds; t += Time.unscaledDeltaTime)
        {
            float k = 1f - (1f - t / SettleSeconds) * (1f - t / SettleSeconds);
            cameraOffset.localPosition = Vector3.Lerp(start, end, k);
            yield return null;
        }
        cameraOffset.localPosition = end;

        LevelHud.ShowHint(StandHint, StandHintSeconds);
        busy = false;
    }

    private IEnumerator StandUp()
    {
        busy = true;
        yield return ScreenFader.FadeOut(FadeSeconds);
        RestoreStanding();
        yield return ScreenFader.FadeIn(FadeSeconds);
        busy = false;
    }

    private void RestoreStanding()
    {
        if (origin != null && cameraOffset != null)
        {
            CharacterController controller = origin.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            cameraOffset.localPosition = standingOffset;
            origin.transform.SetPositionAndRotation(standingPosition, standingRotation);
            if (controller != null) controller.enabled = true;
        }
        seated = false;
        PlayerLocomotion.MovementLocked = false;
    }

    // Turns the rig around the head so the player looks along the given horizontal direction.
    private void FaceDirection(Vector3 direction)
    {
        Vector3 forward = origin.Camera.transform.forward;
        forward.y = 0f;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.01f || forward.sqrMagnitude < 0.01f) return;
        origin.RotateAroundCameraUsingOriginUp(Vector3.SignedAngle(forward, direction, Vector3.up));
    }
}
