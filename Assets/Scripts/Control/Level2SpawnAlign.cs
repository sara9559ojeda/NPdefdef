using System.Collections;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

/// <summary>
/// Ubica al jugador en la sala de Nivel2 al iniciar la escena: de pie junto al sillón, mirando entre el sillón y la
/// araña, que se queda donde la pone la escena (dentro del jarrón de cristal, sobre la mesita frente al sillón).
/// Se crea solo en Nivel2; no modifica la escena.
/// </summary>
public sealed class Level2SpawnAlign : MonoBehaviour
{
    private const string SpiderName = "NP_WolfSpider_Nivel2_Static";
    private const float SpiderDistance = 4.5f;
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
        if (scene.name != "Nivel2") return;
        new GameObject("NP_Level2_SpawnAlign").AddComponent<Level2SpawnAlign>();
    }

    private void OnDestroy()
    {
        PlayerSpawnSequence.EndAlignment();
    }

    private IEnumerator Start()
    {
        PlayerSpawnSequence.BeginAlignment();
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
            Debug.LogWarning("NeuroPhobia: Level2SpawnAlign no encontró XR Origin.");
            Destroy(gameObject);
            yield break;
        }

        // Posición inicial del rig en la escena = donde debe quedar la cabeza.
        Vector3 spawn = origin.transform.position;

        // Igual que Nivel1: sin modo Floor, en Quest las gafas quedan a ras del piso.
        origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;

        // Esperar a que el casco entregue una pose válida (cámara fuera del origen).
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

    // Perceived metres (multiplied by the rig scale of the level).
    private const float SpawnAheadOfSeat = 1.2f;
    private const float SpawnBesideSeat = 1.7f;
    private const float ClearanceRadius = 0.35f;

    private static void Align(XROrigin origin, Vector3 spawn)
    {
        Camera cam = origin.Camera;
        if (cam == null) return;

        // The spider stays where the scene puts it: inside the glass vase on the tea table, in front of the armchair
        // (Level2SpiderIdleLife keeps it there). The player starts beside the armchair, facing between the seat and
        // the vase, so the spider and the place to sit are both in view (before, the player faced the first
        // direction without colliders, which was a door).
        GameObject spider = GameObject.Find(SpiderName);
        Vector3 direction;
        if (spider != null && TryArmchairLayout(origin, spider.transform.position, out Vector3 armchairSpawn, out Vector3 armchairFacing))
        {
            spawn = new Vector3(armchairSpawn.x, spawn.y, armchairSpawn.z);
            direction = armchairFacing;
        }
        else if (spider != null && Level2Armchairs.Flat(spider.transform.position - spawn).sqrMagnitude > 0.01f)
        {
            direction = Level2Armchairs.Flat(spider.transform.position - spawn).normalized;
        }
        else
        {
            direction = FindFreeDirection(spawn);
        }

        CharacterController controller = origin.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;

        Vector3 forward = cam.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f) forward = origin.transform.forward;

        float currentYaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        float targetYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        origin.RotateAroundCameraUsingOriginUp(Mathf.DeltaAngle(currentYaw, targetYaw));
        RigPlacement.MoveCameraTo(origin, new Vector3(spawn.x, cam.transform.position.y, spawn.z));

        if (controller != null) controller.enabled = true;

        GroundPlayer(origin, spawn);
        Debug.Log("NeuroPhobia: jugador ubicado en Nivel2 -> " + cam.transform.position);
    }

    // Player beside the armchair (the free side), looking between the seat and the spider's vase.
    private static bool TryArmchairLayout(XROrigin origin, Vector3 jar, out Vector3 spawn, out Vector3 facing)
    {
        spawn = facing = Vector3.zero;
        Level2Armchairs.Seat armchair = Level2Armchairs.Armchair();
        if (armchair == null) return false;

        float scale = Mathf.Max(0.01f, origin.transform.lossyScale.x);
        Vector3 front = armchair.front;
        Vector3 side = Vector3.Cross(Vector3.up, front);
        Vector3 seat = Level2Armchairs.Flat(armchair.seatPoint) + Vector3.up * armchair.bounds.min.y;

        foreach (float sign in new[] { 1f, -1f })
        {
            Vector3 candidate = seat + front * (SpawnAheadOfSeat * scale) + side * (sign * SpawnBesideSeat * scale);
            if (!IsStandable(origin, candidate, scale)) continue;
            Vector3 toJar = Level2Armchairs.Flat(jar - candidate).normalized;
            Vector3 toSeat = Level2Armchairs.Flat(seat - candidate).normalized;
            facing = (toJar * 0.6f + toSeat * 0.4f).normalized;
            spawn = candidate;
            return true;
        }
        return false;
    }

    // Floor under the point and room for a standing person (no wall or furniture around the body).
    private static bool IsStandable(XROrigin origin, Vector3 point, float scale)
    {
        float radius = ClearanceRadius * scale;
        for (float h = 0.5f; h <= 1.7f; h += 0.6f)
        {
            foreach (Collider c in Physics.OverlapSphere(point + Vector3.up * (h * scale), radius, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!c.transform.IsChildOf(origin.transform)) return false;
            }
        }
        return Physics.Raycast(point + Vector3.up * scale, Vector3.down, 2f * scale, ~0, QueryTriggerInteraction.Ignore);
    }

    // Deja los pies del rig sobre el suelo y quita la altura de ojos por defecto del prefab cuando el
    // seguimiento es de piso: con el rig a escala 2.6 esos 1.36 m sobrantes sumaban ~3.5 m de vuelo.
    private static void GroundPlayer(XROrigin origin, Vector3 spawn)
    {
        Transform offset = origin.CameraFloorOffsetObject != null ? origin.CameraFloorOffsetObject.transform : null;
        if (offset != null && origin.CurrentTrackingOriginMode == TrackingOriginModeFlags.Floor)
        {
            Vector3 local = offset.localPosition;
            local.y = 0f;
            offset.localPosition = local;
        }

        RaycastHit[] hits = Physics.RaycastAll(
            spawn + Vector3.up * 2f, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore);
        float floorY = float.MinValue;
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.IsChildOf(origin.transform)) continue;
            if (Mathf.Abs(hit.point.y - origin.transform.position.y) > 0.5f) continue;
            if (hit.point.y > floorY) floorY = hit.point.y;
        }

        if (floorY > float.MinValue)
        {
            Vector3 position = origin.transform.position;
            position.y = floorY;
            origin.transform.position = position;
        }
    }

    // Primer rumbo (empezando por +Z) con espacio libre hasta la araña; si todos están bloqueados, +Z.
    private static Vector3 FindFreeDirection(Vector3 spawn)
    {
        Vector3 eye = spawn + Vector3.up * 1.0f;
        float[] yawSteps = { 0f, 30f, -30f, 60f, -60f, 90f, -90f, 120f, -120f, 150f, -150f, 180f };

        foreach (float yaw in yawSteps)
        {
            Vector3 direction = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            if (!Physics.Raycast(eye, direction, SpiderDistance + 1f, ~0, QueryTriggerInteraction.Ignore))
            {
                return direction;
            }
        }

        return Vector3.forward;
    }
}
