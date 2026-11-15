using UnityEngine;

/// <summary>
/// Segunda esfera de respiración del Nivel 1. Antes respiraba a su propio ritmo (vaivén de 19 s, sin colores) y no
/// coincidía con la esfera 4-7-8, lo que confundía. Ahora se convierte en una <see cref="BreathingSphereGuide"/> y
/// todas las esferas del nivel arrancan juntas: inhalan, mantienen y exhalan a la vez, con la misma etiqueta.
/// </summary>
public class BreathingGuide : MonoBehaviour
{
    private void Start()
    {
        // Solo anima la esfera; no registra breathingActivations (antes sumaba una por instancia en Start).
        if (GetComponent<BreathingSphereGuide>() == null) gameObject.AddComponent<BreathingSphereGuide>();
        enabled = false;

        foreach (BreathingSphereGuide guide in FindObjectsByType<BreathingSphereGuide>(FindObjectsSortMode.None))
            guide.StartBreathing();
    }
}
