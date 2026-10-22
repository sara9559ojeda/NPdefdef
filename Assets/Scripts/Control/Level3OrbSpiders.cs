using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Coloca arañas de patrulla alrededor de cada orbe del Nivel 3, además de la araña original del spawn.
/// Cada araña es un clon de la original con su propia ruta corta; ninguna se acerca al orbe ni persigue al jugador.
/// Se crea solo en Nivel3; no modifica la escena.
/// </summary>
public sealed class Level3OrbSpiders : MonoBehaviour
{
    private const string SpiderName = "NP_WolfSpider_Nivel3_Patrol";
    private static readonly string[] OrbNames = { "NP_Orbe_1_Azul", "NP_Orbe_2_Naranja" };
    private const int SpidersPerOrb = 2;
    private static readonly float[] Radii = { 2.8f, 3.6f, 4.4f };
    private const float ClearanceRadius = 0.6f;

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
                    Spawn(original, spot, orb.position, ++count);
                }
            }
        }

        Debug.Log("NeuroPhobia: Nivel3 arañas junto a los orbes -> " + count);
        Destroy(gameObject);
    }

    // Prueba ángulos alrededor del orbe hasta encontrar un punto libre de muebles y paredes.
    private static bool TryFindSpot(Vector3 orbPosition, int index, out Vector3 spot)
    {
        float baseAngle = 40f + index * 180f;
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

    private static void Spawn(Transform original, Vector3 position, Vector3 orbPosition, int number)
    {
        Vector3 away = position - orbPosition;
        away.y = 0f;
        // La patrulla corre sobre transform.right: mirando en la dirección orbe->araña, la ruta
        // queda perpendicular a esa línea y la araña nunca se acerca al orbe.
        Quaternion rotation = Quaternion.LookRotation(away.normalized, Vector3.up);

        GameObject clone = Instantiate(original.gameObject, position, rotation);
        clone.name = "NP_L3_OrbSpider_" + number;

        // Start() aún no corrió: se sueltan los puntos del original para que el clon cree los suyos.
        Level3SpiderPatrolV2 patrol = clone.GetComponent<Level3SpiderPatrolV2>();
        if (patrol != null)
        {
            patrol.pointA = null;
            patrol.pointB = null;
            patrol.patrolHalfDistance = 1.2f;
        }

        clone.SetActive(true);
    }
}
