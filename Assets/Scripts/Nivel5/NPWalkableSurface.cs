using UnityEngine;

/// <summary>
/// Marca un objeto como suelo transitable para las arañas aunque su collider sea un trigger.
/// Los pisos visibles de Nivel5 (Piso_Sala, Piso_Cocina, Piso_Cuarto, Piso_Bano) se ven 13 cm por encima
/// del suelo físico, y sin esta marca las arañas quedan hundidas en ellos. Un trigger no afecta al
/// CharacterController del jugador ni a los rayos de interacción (que ignoran triggers).
/// </summary>
[DisallowMultipleComponent]
public sealed class NPWalkableSurface : MonoBehaviour
{
    /// <summary>Indica si un collider cuenta como suelo para el apoyo de las arañas.</summary>
    public static bool IsGround(Collider collider)
    {
        if (collider == null) return false;
        return !collider.isTrigger || collider.GetComponent<NPWalkableSurface>() != null;
    }
}
