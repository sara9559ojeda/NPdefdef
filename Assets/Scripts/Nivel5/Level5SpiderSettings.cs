using UnityEngine;

/// <summary>
/// Parámetros de exposición de las arañas de Nivel5. Todos son variables clínicas y
/// quedan fuera del código para que el profesional pueda ajustarlas. Sara aprobó el
/// 2026-11-05 la cantidad por grado, las distancias, la velocidad y la escala; el resto
/// sigue pendiente de validar clínicamente (ver el encabezado de cada grupo).
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

    // Cantidad: la fija el grado de fobia de la sesión (ver SpiderCountForGrade), no este asset.

    [Header("Aparición (aprobado por Sara)")]
    [Tooltip("Distancia mínima (m) entre un punto de aparición y la posición inicial del jugador.")]
    public float minSpawnDistanceFromPlayer = 3f;
    [Tooltip("Separación mínima (m) entre arañas al aparecer.")]
    public float minSpacingBetweenSpiders = 1.5f;
    [Tooltip("Semilla del azar para los puntos de aparición. 0 = distinta en cada partida.")]
    public int randomSeed = 0;

    [Header("Seguimiento (distancias aprobadas por Sara)")]
    [Tooltip("Modo de seguimiento. Pendiente de validar clínicamente.")]
    public FollowMode followMode = FollowMode.Original;
    [Tooltip("Distancia (m) a la que una araña empieza a seguir al jugador.")]
    public float followRange = 10f;
    [Tooltip("Distancia (m) a la que se detiene una araña que sigue al jugador.")]
    public float followStopDistance = 3f;
    [Tooltip("Ninguna araña se acerca al jugador a menos de esta distancia (m).")]
    public float minDistanceToPlayer = 1.5f;
    [Tooltip("Cuánto (m) puede alejarse de su sitio una araña que vigila una tarea. Pendiente de validar clínicamente.")]
    public float taskLeash = 6f;

    [Header("Movimiento y tamaño (velocidad y escala aprobadas por Sara)")]
    public float moveSpeed = 0.5f;
    [Tooltip("Multiplicador del tamaño visual de cada araña.")]
    public float visualScale = 0.7f;
    [Tooltip("Radio (m) de deambulación alrededor de su sitio. Pendiente de validar clínicamente.")]
    public float wanderRadius = 1.4f;

    [Header("Ventana (decisión de Sara 2026-11-11, pendiente de validar clínicamente)")]
    [Tooltip("Una araña posada por fuera del cristal de la ventana. No camina ni cuenta en el número por grado.")]
    public bool windowSpider = true;

    [Header("Puerta (TASK_DOOR)")]
    [Tooltip("Si es true la puerta se abre sola al acercarse el jugador. Sara pidió que se abra solo con el gesto: false.")]
    public bool doorOpensOnApproach = false;

    /// <summary>
    /// Número de arañas de Nivel5 según el grado de fobia de la sesión: grado N -> N arañas (1-5).
    /// Un grado ausente o fuera de rango usa 1 araña (regla de ESTADO_FINAL_ENTREGA.md).
    /// </summary>
    public static int SpiderCountForGrade(int phobiaGrade)
    {
        return phobiaGrade >= 1 && phobiaGrade <= 5 ? phobiaGrade : 1;
    }

    /// <summary>Habitaciones del apartamento del Nivel5 (sala, cocina, cuarto y baño).</summary>
    public const int Level5Rooms = 4;

    /// <summary>
    /// Arañas del Nivel5: al menos una por habitación, y más si el grado de fobia lo pide (grado N -> N si N > 4).
    /// Así toda la casa tiene arañas con cualquier grado (decisión de Sara, 2026-11-13). El Nivel3 sigue usando
    /// <see cref="SpiderCountForGrade"/>.
    /// </summary>
    public static int Level5SpiderCount(int phobiaGrade)
    {
        return Mathf.Max(Level5Rooms, SpiderCountForGrade(phobiaGrade));
    }

    public static Level5SpiderSettings Load()
    {
        Level5SpiderSettings loaded = Resources.Load<Level5SpiderSettings>(ResourceName);
        return loaded != null ? loaded : CreateInstance<Level5SpiderSettings>();
    }
}
