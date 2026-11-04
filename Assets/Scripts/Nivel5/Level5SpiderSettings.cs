using UnityEngine;

/// <summary>
/// Parámetros de exposición de las arañas de Nivel5. Todos son variables clínicas:
/// quedan fuera del código para que el profesional pueda ajustarlas y están
/// pendientes de validar clínicamente (ver comentario de cada campo).
/// El asset vive en Resources/Level5SpiderSettings; si falta se usan estos valores por defecto.
/// </summary>
[CreateAssetMenu(fileName = "Level5SpiderSettings", menuName = "NeuroPhobia/Nivel5/Ajustes de arañas")]
public sealed class Level5SpiderSettings : ScriptableObject
{
    public const string ResourceName = "Level5SpiderSettings";

    public enum FollowMode
    {
        /// <summary>Una araña sigue siempre al jugador y las que vigilan una tarea lo siguen cuando está cerca (diseño actual).</summary>
        Original = 0,
        /// <summary>Todas las arañas siguen al jugador mientras esté dentro del alcance.</summary>
        AllNearby = 1,
        /// <summary>Todas las arañas siguen al jugador siempre.</summary>
        AllAlways = 2,
        /// <summary>Ninguna araña sigue al jugador; solo deambulan.</summary>
        Never = 3
    }

    [Header("Cantidad (decisión de Sara, pendiente de validar clínicamente)")]
    [Min(0)] public int spiderCount = 8;

    [Header("Aparición (pendiente de validar clínicamente)")]
    [Tooltip("Distancia mínima (m) entre un punto de aparición y la posición inicial del jugador.")]
    public float minSpawnDistanceFromPlayer = 3f;
    [Tooltip("Separación mínima (m) entre arañas al aparecer.")]
    public float minSpacingBetweenSpiders = 1.5f;
    [Tooltip("Semilla del azar para los puntos de aparición. 0 = distinta en cada partida.")]
    public int randomSeed = 0;

    [Header("Seguimiento (pendiente de validar clínicamente)")]
    public FollowMode followMode = FollowMode.Original;
    [Tooltip("Distancia (m) a la que una araña empieza a seguir al jugador.")]
    public float followRange = 10f;
    [Tooltip("Distancia (m) a la que se detiene una araña que sigue al jugador.")]
    public float followStopDistance = 3f;
    [Tooltip("Ninguna araña se acerca al jugador a menos de esta distancia (m).")]
    public float minDistanceToPlayer = 1.5f;
    [Tooltip("Cuánto (m) puede alejarse de su sitio una araña que vigila una tarea.")]
    public float taskLeash = 6f;

    [Header("Movimiento y tamaño (pendiente de validar clínicamente)")]
    public float moveSpeed = 0.5f;
    [Tooltip("Multiplicador del tamaño visual de cada araña.")]
    public float visualScale = 0.7f;
    public float wanderRadius = 1.4f;

    [Header("Puerta (TASK_DOOR)")]
    [Tooltip("Si es true la puerta se abre sola al acercarse el jugador. Sara pidió que se abra solo con el gesto: false.")]
    public bool doorOpensOnApproach = false;

    public static Level5SpiderSettings Load()
    {
        Level5SpiderSettings loaded = Resources.Load<Level5SpiderSettings>(ResourceName);
        return loaded != null ? loaded : CreateInstance<Level5SpiderSettings>();
    }
}
