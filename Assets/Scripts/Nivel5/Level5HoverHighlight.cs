using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Pista visual de «esto se puede abrir/pulsar»: al apuntar a la pieza con el rayo del mando (o acercar la mano), la
/// pieza se tiñe de un tono cálido; mientras se agarra, el tono es más intenso. Al soltar o dejar de apuntar vuelve a su
/// color. Solo cambia el color (MaterialPropertyBlock): no completa nada ni mueve la pieza.
/// </summary>
[RequireComponent(typeof(XRSimpleInteractable))]
public sealed class Level5HoverHighlight : MonoBehaviour
{
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly Color HoverTint = new Color(1f, 0.82f, 0.35f);
    private const float HoverAmount = 0.35f;
    private const float HeldAmount = 0.6f;

    private readonly List<Renderer> renderers = new List<Renderer>();
    private readonly List<Color> originals = new List<Color>();
    private XRSimpleInteractable interactable;
    private int hovers;

    /// <summary>Estado actual: 0 = normal, 1 = apuntada, 2 = agarrada (para las pruebas).</summary>
    public int State { get; private set; }

    public static Level5HoverHighlight Attach(GameObject piece)
    {
        if (piece == null || piece.GetComponent<XRSimpleInteractable>() == null) return null;
        Level5HoverHighlight highlight = piece.GetComponent<Level5HoverHighlight>();
        return highlight != null ? highlight : piece.AddComponent<Level5HoverHighlight>();
    }

    private void Awake()
    {
        var block = new MaterialPropertyBlock();
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            if (r.sharedMaterial == null || !r.sharedMaterial.HasProperty(BaseColor)) continue;
            renderers.Add(r);
            // Si la pieza ya tiene un color propio en su MaterialPropertyBlock (p. ej. el interruptor amarillo), se respeta.
            r.GetPropertyBlock(block);
            originals.Add(block.HasColor(BaseColor) ? block.GetColor(BaseColor) : r.sharedMaterial.GetColor(BaseColor));
            block.Clear();
        }
        interactable = GetComponent<XRSimpleInteractable>();
        interactable.hoverEntered.AddListener(OnHoverEntered);
        interactable.hoverExited.AddListener(OnHoverExited);
        interactable.selectEntered.AddListener(OnSelectEntered);
        interactable.selectExited.AddListener(OnSelectExited);
    }

    private void OnDestroy()
    {
        if (interactable == null) return;
        interactable.hoverEntered.RemoveListener(OnHoverEntered);
        interactable.hoverExited.RemoveListener(OnHoverExited);
        interactable.selectEntered.RemoveListener(OnSelectEntered);
        interactable.selectExited.RemoveListener(OnSelectExited);
    }

    private const string PromptText = "Gatillo o grip";

    private void OnHoverEntered(HoverEnterEventArgs args)
    {
        hovers++;
        Refresh();
        if (hovers != 1 || GameStateManager.CurrentState != GameState.PLAYING) return;
        // Button badge on the piece and a short vibration on the hand that points at it.
        InteractionPrompt.Show(this, PromptAnchor(), PromptText);
        ControllerHaptics.Pulse(ControllerHaptics.HandOf(args.interactorObject.transform));
    }

    private void OnHoverExited(HoverExitEventArgs args)
    {
        hovers = Mathf.Max(0, hovers - 1);
        Refresh();
        if (hovers == 0) InteractionPrompt.Hide(this);
    }

    // Centre of the piece, pulled a little towards the head so the badge is not buried in it.
    private Vector3 PromptAnchor()
    {
        Bounds bounds = new Bounds(transform.position, Vector3.zero);
        bool any = false;
        foreach (Renderer r in renderers)
        {
            if (r == null) continue;
            if (!any) { bounds = r.bounds; any = true; }
            else bounds.Encapsulate(r.bounds);
        }

        Camera head = NeuroPhobiaWorldPanel.GameplayCamera();
        if (head == null) return bounds.center;
        Vector3 toHead = head.transform.position - bounds.center;
        float pull = Mathf.Min(toHead.magnitude * 0.5f, Mathf.Min(bounds.extents.x, bounds.extents.z) + 0.12f);
        return bounds.center + toHead.normalized * pull;
    }
    private void OnSelectEntered(SelectEnterEventArgs args) => Refresh();
    private void OnSelectExited(SelectExitEventArgs args) => Refresh();

    private void Refresh()
    {
        State = interactable.isSelected ? 2 : hovers > 0 ? 1 : 0;
        float amount = State == 2 ? HeldAmount : State == 1 ? HoverAmount : 0f;
        var block = new MaterialPropertyBlock();
        for (int i = 0; i < renderers.Count; i++)
        {
            if (renderers[i] == null) continue;
            renderers[i].GetPropertyBlock(block);
            block.SetColor(BaseColor, Color.Lerp(originals[i], HoverTint, amount));
            renderers[i].SetPropertyBlock(block);
            block.Clear();
        }
    }
}
