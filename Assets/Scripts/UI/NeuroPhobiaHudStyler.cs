using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lleva los textos de los HUD de los niveles (creados por código o guardados en las escenas) a la
/// misma estética que los menús: tipografía de máquina de escribir, letra crema y paneles oscuros del
/// color de la noche, en vez del blanco sobre azul de cada script. Revisa la escena cada segundo para
/// alcanzar también los paneles que aparecen más tarde. Los textos que ya usan una fuente de
/// NeuroPhobiaUITheme (menús, pausa, resultados) no se tocan. Se crea solo.
/// </summary>
public sealed class NeuroPhobiaHudStyler : MonoBehaviour
{
    private const float DisplayMinSize = 36f;
    private const float PanelDarkLimit = 0.2f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        if (FindFirstObjectByType<NeuroPhobiaHudStyler>() != null) return;
        GameObject host = new GameObject("NP_HudStyler");
        DontDestroyOnLoad(host);
        host.AddComponent<NeuroPhobiaHudStyler>();
    }

    private IEnumerator Start()
    {
        var wait = new WaitForSecondsRealtime(1f);
        while (true)
        {
            Apply();
            yield return wait;
        }
    }

    private static void Apply()
    {
        foreach (TMP_Text text in FindObjectsByType<TMP_Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (text.font != null && text.font.name.EndsWith("(NeuroPhobia)")) continue;

            bool bold = (text.fontStyle & FontStyles.Bold) != 0;
            NeuroPhobiaUITheme.Face face = text.fontSize >= DisplayMinSize ? NeuroPhobiaUITheme.Face.Display
                : bold ? NeuroPhobiaUITheme.Face.TypeBold
                : NeuroPhobiaUITheme.Face.Type;

            Color color = text.color;
            if (Mathf.Min(color.r, color.g, color.b) > 0.8f)
                color = new Color(NeuroPhobiaUITheme.Cream.r, NeuroPhobiaUITheme.Cream.g, NeuroPhobiaUITheme.Cream.b, color.a);

            NeuroPhobiaUITheme.Style(text, face, text.fontSize * NeuroPhobiaAccessibility.TextScale, color);
            StylePanel(text);
        }
    }

    // El panel oscuro de fondo (una Image sin sprite) pasa al color de noche de los menús, conservando su opacidad.
    private static void StylePanel(TMP_Text text)
    {
        Image panel = text.GetComponentInParent<Image>();
        if (panel == null || panel.sprite != null) return;
        Color c = panel.color;
        if (Mathf.Max(c.r, c.g, c.b) > PanelDarkLimit) return;
        Color night = NeuroPhobiaUITheme.Night;
        panel.color = new Color(night.r, night.g, night.b, c.a);
    }
}
