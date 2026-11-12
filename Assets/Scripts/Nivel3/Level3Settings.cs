using UnityEngine;

/// <summary>
/// Parámetros del Nivel 3 (aproximación progresiva). Son variables de exposición: quedan fuera del código para que
/// el profesional pueda ajustarlas. El asset vive en Resources/Level3Settings; si falta se usan estos valores.
/// Todos los valores están pendientes de validar clínicamente (decisión de Sara).
/// </summary>
[CreateAssetMenu(fileName = "Level3Settings", menuName = "NeuroPhobia/Nivel3/Ajustes del nivel")]
public sealed class Level3Settings : ScriptableObject
{
    public const string ResourceName = "Level3Settings";

    [Header("Misión (pendiente de validar clínicamente)")]
    [Tooltip("Distancia máxima (m) entre la cabeza y el orbe para que cuente la mirada.")]
    public float reachDistance = 1.5f;
    [Tooltip("Segundos seguidos mirando el orbe, ya cerca de él, para alcanzarlo.")]
    public float gazeSeconds = 3f;
    [Tooltip("Ángulo (grados) entre la mirada y el orbe que todavía cuenta como «mirarlo».")]
    public float gazeAngle = 14f;

    [Header("Arañas según el grado de fobia (pendiente de validar clínicamente)")]
    [Tooltip("Arañas en total (incluida la original) para los grados 1 a 5.")]
    public int[] spidersByGrade = { 4, 6, 8, 10, 12 };
    [Tooltip("Fracción de las arañas que patrullan cerca de los orbes; el resto se reparte por la sala.")]
    [Range(0f, 1f)] public float nearOrbsFraction = 0.5f;
    [Tooltip("Distancia mínima (m) de cualquier araña al punto de inicio del jugador.")]
    public float minDistanceFromPlayer = 4f;
    [Tooltip("Distancia mínima (m) de una araña a un orbe: el orbe debe poder alcanzarse.")]
    public float minDistanceFromOrb = 2.2f;
    [Tooltip("Separación mínima (m) entre arañas.")]
    public float minDistanceBetweenSpiders = 2.5f;

    [Header("Movimiento")]
    [Tooltip("Velocidad de crucero (m/s).")]
    public float moveSpeed = 0.14f;
    [Tooltip("Giro máximo (grados por segundo). Bajo = sin giros bruscos.")]
    public float turnRate = 70f;
    [Tooltip("Radio (m) de la ruta circular de cada araña.")]
    public float routeRadius = 1.3f;

    /// <summary>Arañas en total para un grado (1–5). Un grado ausente o fuera de rango usa el del grado 1.</summary>
    public int SpiderCountForGrade(int phobiaGrade)
    {
        if (spidersByGrade == null || spidersByGrade.Length == 0) return 1;
        int index = phobiaGrade >= 1 && phobiaGrade <= spidersByGrade.Length ? phobiaGrade - 1 : 0;
        return Mathf.Max(1, spidersByGrade[index]);
    }

    public static Level3Settings Load()
    {
        Level3Settings loaded = Resources.Load<Level3Settings>(ResourceName);
        return loaded != null ? loaded : CreateInstance<Level3Settings>();
    }
}
