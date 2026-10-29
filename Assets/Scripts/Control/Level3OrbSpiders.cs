using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Coloca arañas de patrulla alrededor de cada orbe del Nivel 3 y otras repartidas por toda la sala,
/// además de la araña original del spawn.
/// Cada araña es un clon de la original con su propia ruta corta; ninguna se acerca al orbe ni persigue al jugador.
/// Se crea solo en Nivel3; no modifica la escena.
/// </summary>
public sealed class Level3OrbSpiders : MonoBehaviour
{
    private const string SpiderName = "NP_WolfSpider_Nivel3_Patrol";
    private static readonly string[] OrbNames = { "NP_Orbe_1_Azul", "NP_Orbe_2_Naranja" };
    private const int SpidersPerOrb = 3;
    private static readonly float[] Radii = { 2.8f, 3.6f, 4.4f };
    private const float ClearanceRadius = 0.6f;

    // Arañas repartidas por la sala: lejos del jugador, de los orbes y entre sí.
    private const string FloorName = "PISO_CONCRETO_64x48";
    private const int ScatteredSpiders = 10;
    private const float WallMargin = 3f;
    private const float MinDistanceFromPlayer = 6f;
    private const float MinDistanceFromOrb = 3f;
    private const float MinDistanceBetweenSpiders = 7f;
    private const int ScatterAttempts = 400;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Nivel3") return;
        new GameObject("NP_Level3_OrbSpiders").AddComponent<Level3OrbSpiders>();
    }

    private IEnumerator Start()
    {
        yield return null;

        Transform[] all = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Transform original = all.FirstOrDefault(t => t.name == SpiderName);
        if (original == null)
        {
            Debug.LogWarning("NeuroPhobia: Level3OrbSpiders no encontró " + SpiderName);
            yield break;
        }

        int count = 0;
        foreach (string orbName in OrbNames)
        {
            Transform orb = all.FirstOrDefault(t => t.name == orbName);
            if (orb == null) continue;

            for (int i = 0; i < SpidersPerOrb; i++)
            {
                if (TryFindSpot(orb.position, i, out Vector3 spot))
                {
                    Vector3 away = spot - orb.position;
                    away.y = 0f;
                    // La patrulla corre sobre transform.right: mirando en la dirección orbe->araña, la ruta
                    // queda perpendicular a esa línea y la araña nunca se acerca al orbe.
                    Spawn(original, spot, Quaternion.LookRotation(away.normalized, Vector3.up), ++count);
                }
            }
        }

        count = ScatterSpiders(original, all, count);

        Debug.Log("NeuroPhobia: Nivel3 arañas totales añadidas -> " + count);
        Destroy(gameObject);
    }

    // Reparte arañas por el piso con muestreo aleatorio, respetando distancias mínimas.
    private static int ScatterSpiders(Transform original, Transform[] all, int count)
    {
        Transform floor = all.FirstOrDefault(t => t.name == FloorName);
        Collider floorCollider = floor != null ? floor.GetComponent<Collider>() : null;
        if (floorCollider == null)
        {
            Debug.LogWarning("NeuroPhobia: Level3OrbSpiders no encontró " + FloorName + "; sin arañas repartidas.");
            return count;
        }

        Bounds area = floorCollider.bounds;
        area.Expand(new Vector3(-WallMargin * 2f, 0f, -WallMargin * 2f));

        var avoid = new System.Collections.Generic.List<(Vector3 position, float distance)>();
        Transform player = all.FirstOrDefault(t => t.name.StartsWith("XR Origin"));
        if (player != null) avoid.Add((player.position, MinDistanceFromPlayer));
        avoid.Add((original.position, MinDistanceBetweenSpiders));
        foreach (string orbName in OrbNames)
        {
            Transform orb = all.FirstOrDefault(t => t.name == orbName);
            if (orb != null) avoid.Add((orb.position, MinDistanceFromOrb));
        }
        foreach (Transform t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (t.name.StartsWith("NP_L3_OrbSpider_")) avoid.Add((t.position, MinDistanceBetweenSpiders * 0.6f));
        }

        int placed = 0;
        for (int attempt = 0; attempt < ScatterAttempts && placed < ScatteredSpiders; attempt++)
        {
            Vector3 candidate = new Vector3(
                Random.Range(area.min.x, area.max.x), 0.18f, Random.Range(area.min.z, area.max.z));

            if (avoid.Any(a => FlatDistance(a.position, candidate) < a.distance)) continue;
            if (Physics.CheckSphere(candidate + Vector3.up * 0.4f, ClearanceRadius, ~0, QueryTriggerInteraction.Ignore)) continue;
            if (!Physics.Raycast(candidate + Vector3.up * 1.5f, Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore)) continue;

            Spawn(original, candidate, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), ++count);
            avoid.Add((candidate, MinDistanceBetweenSpiders));
            placed++;
        }

        return count;
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    // Prueba ángulos alrededor del orbe hasta encontrar un punto libre de muebles y paredes.
    private static bool TryFindSpot(Vector3 orbPosition, int index, out Vector3 spot)
    {
        float baseAngle = 40f + index * 120f;
        foreach (float radius in Radii)
        {
            for (int step = 0; step < 12; step++)
            {
                float angle = (baseAngle + step * 30f) * Mathf.Deg2Rad;
                Vector3 candidate = orbPosition + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * radius;
                candidate.y = 0.18f;

                bool blocked = Physics.CheckSphere(
                    candidate + Vector3.up * 0.4f, ClearanceRadius, ~0, QueryTriggerInteraction.Ignore);
                bool hasFloor = Physics.Raycast(
                    candidate + Vector3.up * 1.5f, Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore);

                if (!blocked && hasFloor)
                {
                    spot = candidate;
                    return true;
                }
            }
        }

        spot = default;
        return false;
    }

    private static void Spawn(Transform original, Vector3 position, Quaternion rotation, int number)
    {
        GameObject clone = Instantiate(original.gameObject, position, rotation);
        clone.name = "NP_L3_OrbSpider_" + number;

        // Start() aún no corrió: se sueltan los puntos del original para que el clon cree los suyos.
        Level3SpiderPatrolV2 patrol = clone.GetComponent<Level3SpiderPatrolV2>();
        if (patrol != null)
        {
            patrol.pointA = null;
            patrol.pointB = null;
            patrol.patrolHalfDistance = Random.Range(0.9f, 1.6f);
            patrol.moveSpeed = Random.Range(0.10f, 0.16f);
        }

        clone.SetActive(true);
    }
}
