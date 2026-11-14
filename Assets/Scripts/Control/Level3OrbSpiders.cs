using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Reparte las arañas del Nivel 3. La cantidad total (incluida la araña original de la escena) sale de
/// <see cref="Level3Settings.SpiderCountForGrade"/> según el grado de fobia de la sesión; una parte patrulla cerca de
/// los orbes (sin taparlos) y el resto por la sala, siempre en suelo libre, lejos del inicio del jugador y entre sí.
/// Cada araña es un clon de la original con su propia ruta circular. Se crea solo en Nivel3; no modifica la escena.
/// </summary>
public sealed class Level3OrbSpiders : MonoBehaviour
{
    private const string SpiderName = "NP_WolfSpider_Nivel3_Patrol";
    private static readonly string[] OrbNames = { "NP_Orbe_1_Azul", "NP_Orbe_2_Naranja" };
    private const string FloorName = "PISO_CONCRETO_64x48";
    private const float WallMargin = 2.5f;
    private const float BodyRadius = 0.35f;
    private const int Attempts = 600;

    public static int LastPlaced { get; private set; } = -1;
    public static int LastRequested { get; private set; } = -1;

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
        LastPlaced = -1;
        new GameObject("NP_Level3_OrbSpiders").AddComponent<Level3OrbSpiders>();
    }

    private IEnumerator Start()
    {
        yield return null;
        Level3Settings settings = Level3Settings.Load();

        Transform[] all = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Transform original = all.FirstOrDefault(t => t.name == SpiderName);
        if (original == null)
        {
            Debug.LogWarning("NeuroPhobia: Level3OrbSpiders no encontró " + SpiderName);
            yield break;
        }

        int grade = SessionManager.Instance != null ? SessionManager.Instance.PhobiaGrade : 0;
        int total = settings.SpiderCountForGrade(grade);
        int extra = Mathf.Max(0, total - 1);
        LastRequested = total;
        int nearOrbs = Mathf.RoundToInt(extra * settings.nearOrbsFraction);

        Transform[] orbs = OrbNames.Select(n => all.FirstOrDefault(t => t.name == n)).Where(t => t != null).ToArray();
        Transform player = all.FirstOrDefault(t => t.name.StartsWith("XR Origin")) ?? all.FirstOrDefault(t => t.name.StartsWith("NP_DesktopPlayer"));
        var placed = new List<Vector3> { original.position };
        int count = 0;

        // La araña original deambula igual que los clones: misma zona, lejos de los orbes y del jugador.
        ConfigureWander(original.GetComponent<Level3SpiderPatrolV2>(), settings, orbs, settings.routeRadius);

        // Cerca de los orbes, repartidas por igual en anillo (2,5–4,5 m), sin tapar el orbe.
        for (int i = 0; i < nearOrbs && orbs.Length > 0; i++)
        {
            Transform orb = orbs[i % orbs.Length];
            if (TryFindSpot(settings, orb.position, settings.minDistanceFromOrb + 0.3f, settings.minDistanceFromOrb + 2.3f, player, orbs, placed, out Vector3 spot))
            {
                Spawn(original, spot, settings, orbs, ++count);
                placed.Add(spot);
            }
        }

        // El resto, por toda la sala.
        Collider floor = all.Where(t => t.name == FloorName).Select(t => t.GetComponent<Collider>()).FirstOrDefault(c => c != null);
        if (floor != null)
        {
            Bounds area = floor.bounds;
            area.Expand(new Vector3(-WallMargin * 2f, 0f, -WallMargin * 2f));
            int target = extra - count;
            for (int attempt = 0; attempt < Attempts && target > 0; attempt++)
            {
                Vector3 candidate = new Vector3(Random.Range(area.min.x, area.max.x), area.max.y, Random.Range(area.min.z, area.max.z));
                if (!IsFree(settings, candidate, player, orbs, placed, out Vector3 grounded)) continue;
                Spawn(original, grounded, settings, orbs, ++count);
                placed.Add(grounded);
                target--;
            }
        }

        LastPlaced = count + 1;
        Debug.Log("NeuroPhobia: Nivel3 arañas (grado " + grade + ") -> " + LastPlaced + " de " + total);
        Destroy(gameObject);
    }

    private static bool TryFindSpot(Level3Settings settings, Vector3 center, float minRadius, float maxRadius, Transform player,
        Transform[] orbs, List<Vector3> placed, out Vector3 spot)
    {
        for (int attempt = 0; attempt < 80; attempt++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float radius = Random.Range(minRadius, maxRadius);
            Vector3 candidate = center + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * radius;
            if (IsFree(settings, candidate, player, orbs, placed, out spot)) return true;
        }
        spot = default;
        return false;
    }

    /// <summary>
    /// Punto de suelo libre: hay suelo debajo, el cuerpo de la araña (una esfera por encima del suelo) no toca muebles
    /// ni paredes, y respeta las distancias al jugador, a los orbes y a las demás arañas.
    /// </summary>
    public static bool IsFree(Level3Settings settings, Vector3 candidate, Transform player, Transform[] orbs, List<Vector3> placed, out Vector3 grounded)
    {
        grounded = candidate;
        if (!Physics.Raycast(new Vector3(candidate.x, candidate.y + 2.5f, candidate.z), Vector3.down, out RaycastHit hit, 6f, ~0, QueryTriggerInteraction.Ignore)) return false;
        if (hit.normal.y < 0.9f || hit.point.y > 0.5f) return false;   // encima de un mueble o en una pared
        grounded = hit.point + Vector3.up * 0.02f;

        // La esfera empieza por encima del suelo: antes tocaba el suelo y descartaba todos los puntos.
        if (Physics.CheckSphere(grounded + Vector3.up * (BodyRadius + 0.08f), BodyRadius, ~0, QueryTriggerInteraction.Ignore)) return false;
        if (player != null && Flat(player.position - grounded) < settings.minDistanceFromPlayer) return false;
        Vector3 spot = grounded;
        if (orbs.Any(o => Flat(o.position - spot) < settings.minDistanceFromOrb)) return false;
        if (placed.Any(p => Flat(p - spot) < settings.minDistanceBetweenSpiders)) return false;
        return true;
    }

    private static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }

    private static void Spawn(Transform original, Vector3 position, Level3Settings settings, Transform[] orbs, int number)
    {
        GameObject clone = Instantiate(original.gameObject, position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        clone.name = "NP_L3_OrbSpider_" + number;
        Level3SpiderPatrolV2 patrol = clone.GetComponent<Level3SpiderPatrolV2>();
        if (patrol != null)
        {
            // Start() aún no corrió: se sueltan los puntos del original para que el clon cree su propia ruta.
            patrol.pointA = null;
            patrol.pointB = null;
            patrol.SetBaseMoveSpeed(settings.moveSpeed * Random.Range(0.85f, 1.15f));
            patrol.turnRate = settings.turnRate;
            ConfigureWander(patrol, settings, orbs, settings.routeRadius * Random.Range(0.8f, 1.2f));
        }
        clone.SetActive(true);
    }

    // Zona por la que deambula, puntos que no pisa (los orbes) y distancia al jugador.
    private static void ConfigureWander(Level3SpiderPatrolV2 patrol, Level3Settings settings, Transform[] orbs, float radius)
    {
        if (patrol == null) return;
        patrol.patrolHalfDistance = radius;
        patrol.keepAway = orbs.Select(o => o.position).ToArray();
        patrol.keepAwayRadius = settings.minDistanceFromOrb;
        patrol.playerClearance = settings.spiderPlayerClearance;
    }
}
