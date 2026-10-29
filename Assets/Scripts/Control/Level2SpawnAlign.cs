using System.Collections;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

/// <summary>
/// Ubica al jugador y a la araña en la sala de Nivel2 al iniciar la escena.
/// La cabeza real queda en la posición inicial del XR Origin mirando hacia un lado despejado,
/// y la araña se coloca delante, en el suelo y de cara al jugador.
/// Se crea solo en Nivel2; no modifica la escena.
/// </summary>
public sealed class Level2SpawnAlign : MonoBehaviour
{
    private const string SpiderName = "NP_WolfSpider_Nivel2_Static";
    private const string JarName = "NP_GlassJar_Nivel2";
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

    private static void Align(XROrigin origin, Vector3 spawn)
    {
        Camera cam = origin.Camera;
        if (cam == null) return;

        Vector3 direction = FindFreeDirection(spawn);

        CharacterController controller = origin.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;

        Vector3 forward = cam.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f) forward = origin.transform.forward;

        float currentYaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        float targetYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        origin.RotateAroundCameraUsingOriginUp(Mathf.DeltaAngle(currentYaw, targetYaw));
        origin.MoveCameraToWorldLocation(new Vector3(spawn.x, cam.transform.position.y, spawn.z));

        if (controller != null) controller.enabled = true;

        PlaceSpider(origin, spawn, direction);
        Debug.Log("NeuroPhobia: jugador ubicado en Nivel2 -> " + cam.transform.position);
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

    private static void PlaceSpider(XROrigin origin, Vector3 spawn, Vector3 direction)
    {
        GameObject spider = GameObject.Find(SpiderName);
        if (spider == null)
        {
            Debug.LogWarning("NeuroPhobia: Level2SpawnAlign no encontró " + SpiderName);
            return;
        }

        Vector3 position = spawn + direction * SpiderDistance;
        position.y = spawn.y;

        // Suelo bajo la araña, sin contar al propio jugador ni a la araña.
        RaycastHit[] hits = Physics.RaycastAll(
            position + Vector3.up * 3f, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore);
        float bestDistance = float.MaxValue;
        foreach (RaycastHit hit in hits)
        {
            Transform t = hit.collider.transform;
            if (t.IsChildOf(origin.transform) || t.IsChildOf(spider.transform)) continue;
            if (hit.distance < bestDistance)
            {
                bestDistance = hit.distance;
                position.y = hit.point.y;
            }
        }

        spider.transform.SetPositionAndRotation(position, Quaternion.LookRotation(-direction, Vector3.up));
        BuildGlassJar(spider);
    }

    // Jarrón de cristal alrededor de la araña. No cuelga de la araña ni lleva "spider" en el nombre
    // para que NeuroPhobiaLevel2StableGaze no lo confunda con la araña al buscarla por nombre.
    private static void BuildGlassJar(GameObject spider)
    {
        if (GameObject.Find(JarName) != null) return;

        Material glass = Resources.Load<Material>("NP_GlassJar");
        if (glass == null)
        {
            Debug.LogWarning("NeuroPhobia: falta Resources/NP_GlassJar; la araña queda sin jarrón.");
            return;
        }

        Bounds bounds = default;
        bool hasBounds = false;
        foreach (Renderer renderer in spider.GetComponentsInChildren<Renderer>())
        {
            if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; }
            else bounds.Encapsulate(renderer.bounds);
        }

        if (!hasBounds) return;

        float radius = Mathf.Max(bounds.extents.x, bounds.extents.z) * 1.35f + 0.05f;
        float height = bounds.size.y * 1.3f + 0.1f;
        Vector3 basePoint = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);

        GameObject jar = new GameObject(JarName);
        jar.transform.position = basePoint;

        // Cilindro primitivo mide 2 de alto y 1 de diámetro: se escala a la medida del jarrón.
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        body.name = "Cuerpo";
        body.transform.SetParent(jar.transform, false);
        body.transform.localPosition = Vector3.up * (height * 0.5f);
        body.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
        body.GetComponent<Renderer>().sharedMaterial = glass;
        Destroy(body.GetComponent<Collider>());

        GameObject lid = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        lid.name = "Tapa";
        lid.transform.SetParent(jar.transform, false);
        lid.transform.localPosition = Vector3.up * (height + 0.02f);
        lid.transform.localScale = new Vector3(radius * 2.1f, 0.02f, radius * 2.1f);
        lid.GetComponent<Renderer>().sharedMaterial = glass;
        Destroy(lid.GetComponent<Collider>());
    }
}
