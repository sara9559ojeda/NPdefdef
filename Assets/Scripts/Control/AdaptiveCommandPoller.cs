using UnityEngine;

/// <summary>
/// Retirado por el contrato API v1 (decisión D13): el visor ya no consulta comandos al backend durante la exposición.
/// El componente se conserva solo para que las escenas que lo referencian no muestren «Missing Script»;
/// se desactiva al arrancar y no hace ninguna petición.
/// </summary>
public class AdaptiveCommandPoller : MonoBehaviour
{
    [SerializeField, HideInInspector] private float pollingInterval = 5f;
    [SerializeField, HideInInspector] private float pauseSeconds = 15f;

    private void Awake()
    {
        enabled = false;
    }
}
