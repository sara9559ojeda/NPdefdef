using System.Collections;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

/// <summary>
/// Level start seen by the player: the view stays black while the rig is placed, then opens like eyes waking up,
/// so the repositioning is never visible.
/// Nivel1, Nivel2 and Nivel5 already have their own placement components (LevelNSpawnAlign); they report here
/// through <see cref="BeginAlignment"/> / <see cref="EndAlignment"/>. Nivel3 and Nivel4 had none, so this component
/// also gives them floor tracking (the player's real height, as in the other levels).
/// In every level, if the head ends up inside or against scenery, the rig is nudged to the nearest free spot.
/// Created automatically in Nivel1..Nivel5; scenes are not modified.
/// </summary>
public sealed class PlayerSpawnSequence : MonoBehaviour
{
    private const float MaxWaitSeconds = 12f;
    private const float PoseSettleSeconds = 0.5f;
    private const float FloorSearchTolerance = 0.5f;
    private const float HeadClearanceRadius = 0.3f;
    private static readonly float[] NudgeDistances = { 0.4f, 0.8f, 1.2f };
    private const int NudgeDirections = 12;

    private static int pendingAlignments;

    /// <summary>Called by a level placement component when it starts moving the rig.</summary>
    public static void BeginAlignment() => pendingAlignments++;

    /// <summary>Called by a level placement component when the rig is in place.</summary>
    public static void EndAlignment() => pendingAlignments = Mathf.Max(0, pendingAlignments - 1);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        pendingAlignments = 0;
        int level = LevelScenes.Number(scene.name);
        if (level == 0) return;
        PlayerSpawnSequence sequence = new GameObject("NP_PlayerSpawnSequence").AddComponent<PlayerSpawnSequence>();
        sequence.alignsRigItself = level == 3 || level == 4;
    }

    private bool alignsRigItself;

    private IEnumerator Start()
    {
        ScreenFader.CoverNow();

        float waited = 0f;
        XROrigin origin = null;
        while (origin == null && waited < MaxWaitSeconds)
        {
            origin = FindFirstObjectByType<XROrigin>();
            waited += Time.unscaledDeltaTime;
            if (origin == null) yield return null;
        }

        if (origin != null)
        {
            if (alignsRigItself)
            {
                yield return AlignToFloor(origin);
            }
            else
            {
                // Two frames so the level placement component has run its Start and registered itself.
                yield return null;
                yield return null;
                while (pendingAlignments > 0 && waited < MaxWaitSeconds)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            MoveOutOfScenery(origin);
        }

        yield return ScreenFader.OpenEyes();
        Destroy(gameObject);
    }

    // Floor tracking with the head over the spot the scene intends, and the rig standing on the floor below it.
    private static IEnumerator AlignToFloor(XROrigin origin)
    {
        Vector3 rigStart = origin.transform.position;
        Transform offset = origin.CameraFloorOffsetObject != null ? origin.CameraFloorOffsetObject.transform : null;
        Vector3 intendedHead = offset != null ? offset.position : rigStart;

        origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;

        float waited = 0f;
        while (waited < MaxWaitSeconds &&
               (origin.Camera == null ||
                (XRSettings.isDeviceActive && origin.CameraInOriginSpacePos.sqrMagnitude < 0.0001f)))
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }
        yield return new WaitForSecondsRealtime(PoseSettleSeconds);

        Camera head = origin.Camera;
        if (head == null) yield break;

        CharacterController controller = origin.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;

        // With floor tracking the headset provides the eye height: the default height of the prefab is removed.
        if (offset != null && origin.CurrentTrackingOriginMode == TrackingOriginModeFlags.Floor)
        {
            Vector3 local = offset.localPosition;
            local.y = 0f;
            offset.localPosition = local;
        }

        origin.MoveCameraToWorldLocation(new Vector3(intendedHead.x, head.transform.position.y, intendedHead.z));

        if (TryFindFloor(origin, new Vector3(intendedHead.x, rigStart.y, intendedHead.z), out float floorY))
        {
            Vector3 position = origin.transform.position;
            position.y = floorY;
            origin.transform.position = position;
        }

        if (controller != null) controller.enabled = true;
        Debug.Log("NeuroPhobia: player placed in " + SceneManager.GetActiveScene().name + " -> " + head.transform.position);
    }

    // Highest floor close to the reference height, ignoring the rig itself.
    private static bool TryFindFloor(XROrigin origin, Vector3 reference, out float floorY)
    {
        floorY = float.MinValue;
        RaycastHit[] hits = Physics.RaycastAll(reference + Vector3.up * 2f, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.IsChildOf(origin.transform)) continue;
            if (Mathf.Abs(hit.point.y - reference.y) > FloorSearchTolerance) continue;
            if (hit.point.y > floorY) floorY = hit.point.y;
        }
        return floorY > float.MinValue;
    }

    // If the head starts inside or touching scenery, moves the rig horizontally to the nearest free spot.
    private static void MoveOutOfScenery(XROrigin origin)
    {
        Camera head = origin.Camera;
        if (head == null) return;

        float scale = Mathf.Max(0.01f, origin.transform.lossyScale.x);
        float radius = HeadClearanceRadius * scale;
        Vector3 headPosition = head.transform.position;
        if (IsFree(origin, headPosition, radius)) return;

        foreach (float distance in NudgeDistances)
        {
            for (int i = 0; i < NudgeDirections; i++)
            {
                Vector3 direction = Quaternion.Euler(0f, i * 360f / NudgeDirections, 0f) * Vector3.forward;
                Vector3 candidate = headPosition + direction * (distance * scale);
                if (!IsFree(origin, candidate, radius)) continue;
                if (!TryFindFloor(origin, new Vector3(candidate.x, origin.transform.position.y, candidate.z), out _)) continue;

                CharacterController controller = origin.GetComponent<CharacterController>();
                if (controller != null) controller.enabled = false;
                origin.transform.position += candidate - headPosition;
                if (controller != null) controller.enabled = true;
                Debug.Log("NeuroPhobia: spawn nudged out of scenery by " + (candidate - headPosition));
                return;
            }
        }
    }

    private static bool IsFree(XROrigin origin, Vector3 point, float radius)
    {
        foreach (Collider collider in Physics.OverlapSphere(point, radius, ~0, QueryTriggerInteraction.Ignore))
        {
            if (!collider.transform.IsChildOf(origin.transform)) return false;
        }
        return true;
    }
}
