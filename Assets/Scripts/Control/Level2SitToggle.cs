using System.Collections;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

/// <summary>
/// Sitting is the one action that exists only in Nivel2. With the A button of the right controller (C on a
/// keyboard) the player sits on an armchair and stands up again, but only inside the invisible zone around an
/// armchair (every scene object whose root name contains "sofa").
/// - Entering a zone shows the "A" badge on the armchair and gives a short vibration, so the player knows it
///   can be used.
/// - Sitting is a short trip hidden by a fade: the player is carried onto the seat, facing the spider, with the
///   eyes at chair height. Movement with the stick is locked while seated; turning still works.
/// - Standing up fades again and returns the player to where they were standing.
/// Created automatically in Nivel2; the scene is not modified.
/// </summary>
public sealed class Level2SitToggle : MonoBehaviour
{
    private const string SpiderName = "NP_WolfSpider_Nivel2_Static";
    private const string SitPrompt = "A · Sentarte";
    private const string StandHint = "Pulsa A para levantarte.";
    private const float StandHintSeconds = 5f;
    private const float SeatedEyeHeight = 1.1f;
    private const float MaxDrop = 0.9f;
    // Horizontal margin (m) of the zone around the armchair.
    private const float ZoneMargin = 0.6f;
    // Zone height (m) at rig scale 1; multiplied by the rig scale so it always covers the standing head.
    private const float ZoneHeight = 2.6f;
    private const float FadeSeconds = 0.25f;
    private const int IgnoreRaycastLayer = 2;

    private sealed class Seat
    {
        public BoxCollider zone;
        public Vector3 seatPoint;
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

    // One zone per armchair. The meshes are called "Plane.010", "cloth.015"...; the name with "sofa" belongs to
    // the root object of the furniture, so the zone is built from all the renderers under that root.
    private void BuildSeats()
    {
        float rigScale = Mathf.Max(1f, origin.transform.lossyScale.y);
        foreach (Transform t in FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!IsSofaRoot(t)) continue;
            Renderer[] renderers = t.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) continue;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

            float height = ZoneHeight * rigScale;
            GameObject zoneObject = new GameObject("NP_Seat_Zone_" + t.name);
            zoneObject.layer = IgnoreRaycastLayer;   // does not block the controller rays or the gaze
            zoneObject.transform.position = new Vector3(bounds.center.x, bounds.min.y + height * 0.5f, bounds.center.z);
            BoxCollider zone = zoneObject.AddComponent<BoxCollider>();
            zone.isTrigger = true;
            zone.size = new Vector3(bounds.size.x + ZoneMargin * 2f, height, bounds.size.z + ZoneMargin * 2f);

            seats.Add(new Seat
            {
                zone = zone,
                seatPoint = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z),
                promptPoint = new Vector3(bounds.center.x, bounds.max.y + 0.2f * rigScale, bounds.center.z)
            });
        }
    }

    private static bool IsSofaRoot(Transform t)
    {
        if (!t.name.ToLowerInvariant().Contains("sofa")) return false;
        for (Transform p = t.parent; p != null; p = p.parent)
            if (p.name.ToLowerInvariant().Contains("sofa")) return false;
        return true;
    }

    // Tracks which armchair zone the head is in; entering one shows the badge and vibrates the right hand.
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

        // Lower the view only as much as needed to reach chair height.
        float drop = Mathf.Clamp(origin.CameraInOriginSpacePos.y - SeatedEyeHeight, 0f, MaxDrop);
        cameraOffset.localPosition = standingOffset - Vector3.up * drop;

        FaceSpider(seat.seatPoint);
        Vector3 head = origin.Camera.transform.position;
        origin.MoveCameraToWorldLocation(new Vector3(seat.seatPoint.x, head.y, seat.seatPoint.z));

        if (controller != null) controller.enabled = true;
        seated = true;
        PlayerLocomotion.MovementLocked = true;

        yield return ScreenFader.FadeIn(FadeSeconds);
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

    // Turns the rig so the player, once seated, looks towards the spider in the jar.
    private void FaceSpider(Vector3 from)
    {
        GameObject spider = GameObject.Find(SpiderName);
        if (spider == null) return;

        Vector3 toSpider = spider.transform.position - from;
        toSpider.y = 0f;
        Vector3 forward = origin.Camera.transform.forward;
        forward.y = 0f;
        if (toSpider.sqrMagnitude < 0.01f || forward.sqrMagnitude < 0.01f) return;

        origin.RotateAroundCameraUsingOriginUp(Vector3.SignedAngle(forward, toSpider, Vector3.up));
    }
}
