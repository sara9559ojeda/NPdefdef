using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Script simple para botones de cambio de nivel.
/// Poner este componente en el mismo boton o en un objeto vacío.
/// </summary>
public class SceneButtonLoader : MonoBehaviour
{
    public void LoadNivel1()
    {
        if (SceneManager.GetActiveScene().name == "MainMenu")
        {
            NeuroPhobiaMainMenuModern menu = FindFirstObjectByType<NeuroPhobiaMainMenuModern>();
            if (menu != null) menu.StartExperience();
            return;
        }
        SceneManager.LoadScene("Nivel1");
    }

    public void LoadNivel2()
    {
        Debug.LogWarning("Nivel2 bloqueado: escenario nuevo de x no presente en repositorio.");
    }

    public void LoadNivel3()
    {
        if (SceneManager.GetActiveScene().name == "MainMenu") { LoadNivel1(); return; }
        SceneManager.LoadScene("Nivel3");
    }

    public void LoadNivel4()
    {
        if (SceneManager.GetActiveScene().name == "MainMenu") { LoadNivel1(); return; }
        SceneManager.LoadScene("Nivel4");
    }

    public void LoadNivel5()
    {
        Debug.LogWarning("Nivel5 pendiente de escenario visual adecuado.");
    }

    public void LoadMainMenu()
    {
        SceneManager.LoadScene("MainMenu");
    }
}
