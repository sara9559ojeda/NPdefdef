using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Opción del menú principal: al apuntarla con el rayo, el ratón o el mando se ilumina y aparece
/// una marca de tinta a la izquierda. Solo una opción está activa a la vez (la última apuntada).
/// </summary>
[DisallowMultipleComponent]
public sealed class NeuroPhobiaMenuItem : MonoBehaviour, IPointerEnterHandler, ISelectHandler
{
    private Image inkMark;
    private Image highlight;
    private TMP_Text label;
    private bool active;
    private float reveal;

    public void Setup(Image ink, Image glow, TMP_Text text)
    {
        inkMark = ink;
        highlight = glow;
        label = text;
        SetActive(false, true);
    }

    public void OnPointerEnter(PointerEventData eventData) => Activate();
    public void OnSelect(BaseEventData eventData) => Activate();

    public void Activate()
    {
        if (transform.parent != null)
            foreach (NeuroPhobiaMenuItem other in transform.parent.GetComponentsInChildren<NeuroPhobiaMenuItem>())
                if (other != this) other.SetActive(false, false);
        SetActive(true, false);
    }

    private void SetActive(bool value, bool instant)
    {
        active = value;
        if (label != null)
        {
            label.color = value ? NeuroPhobiaUITheme.Cream : NeuroPhobiaUITheme.Sepia;
            label.font = NeuroPhobiaUITheme.Font(value ? NeuroPhobiaUITheme.Face.TypeBold : NeuroPhobiaUITheme.Face.Type);
        }
        if (instant) Apply(value ? 1f : 0f);
    }

    private void OnDisable() => Apply(active ? 1f : 0f);

    private void Update()
    {
        float target = active ? 1f : 0f;
        if (Mathf.Approximately(reveal, target)) return;
        // Fundido corto (sin parpadeo); usa tiempo sin escalar porque el menú puede estar con timeScale 0.
        Apply(Mathf.MoveTowards(reveal, target, Time.unscaledDeltaTime * 6f));
    }

    private void Apply(float value)
    {
        reveal = value;
        if (inkMark != null)
        {
            inkMark.color = NeuroPhobiaUITheme.WithAlpha(NeuroPhobiaUITheme.Sage, value);
            inkMark.rectTransform.localScale = new Vector3(.2f + .8f * value, 1f, 1f);
        }
        if (highlight != null) highlight.color = NeuroPhobiaUITheme.WithAlpha(NeuroPhobiaUITheme.Sage, .12f * value);
    }
}
