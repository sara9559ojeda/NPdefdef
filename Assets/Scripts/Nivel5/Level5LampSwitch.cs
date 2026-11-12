using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Interruptor de la lámpara: un botón pequeño en el pie de la lámpara que se pulsa con el dedo del mando (poke) o
/// apuntándolo y apretando el gatillo. Cada pulsación enciende o apaga la lámpara de verdad: sus luces y el brillo
/// de la pantalla. Solo la pieza del interruptor reacciona; acercarse a la lámpara no hace nada.
/// </summary>
[RequireComponent(typeof(XRSimpleInteractable))]
public sealed class Level5LampSwitch : MonoBehaviour
{
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    private readonly List<Light> lights = new List<Light>();
    private readonly List<Renderer> shades = new List<Renderer>();
    private readonly List<float> intensities = new List<float>();
    private bool on = true;
    private float lastToggle = -10f;

    public bool IsOn => on;

    public static Level5LampSwitch Create(Transform lamp, Light mainLight)
    {
        Level5LampSwitch existing = lamp.GetComponentInChildren<Level5LampSwitch>();
        if (existing != null) return existing;

        Bounds bounds = new Bounds(lamp.position, Vector3.zero);
        bool any = false;
        foreach (Renderer r in lamp.GetComponentsInChildren<Renderer>())
        {
            if (!any) { bounds = r.bounds; any = true; } else bounds.Encapsulate(r.bounds);
        }

        // Botón a 1,05 m del suelo, en la cara del pie que mira al centro de la sala.
        Camera head = Camera.main;
        Vector3 toward = head != null ? head.transform.position - bounds.center : -lamp.forward;
        toward.y = 0f;
        toward = toward.sqrMagnitude > 0.001f ? toward.normalized : Vector3.forward;
        float floor = bounds.min.y;
        Vector3 position = new Vector3(bounds.center.x, floor + 1.05f, bounds.center.z) + toward * (Mathf.Min(bounds.extents.x, bounds.extents.z) * 0.6f + 0.06f);

        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "NP_L5_Interruptor_Lampara";
        go.transform.SetParent(lamp, true);
        go.transform.position = position;
        go.transform.rotation = Quaternion.LookRotation(toward, Vector3.up);
        go.transform.localScale = Vector3.one;
        // Botón de 12 x 16 cm, amarillo y sin sombreado (material Unlit de Resources): se ve aunque la sala esté oscura.
        go.transform.localScale = new Vector3(0.12f / Mathf.Max(0.0001f, lamp.lossyScale.x), 0.16f / Mathf.Max(0.0001f, lamp.lossyScale.y), 0.06f / Mathf.Max(0.0001f, lamp.lossyScale.z));
        Renderer lampRenderer = lamp.GetComponentInChildren<Renderer>();
        MeshRenderer renderer = go.GetComponent<MeshRenderer>();
        Material unlit = Resources.Load<Material>("NeuroPhobiaExterior");
        renderer.sharedMaterial = unlit != null ? unlit : lampRenderer != null ? lampRenderer.sharedMaterial : renderer.sharedMaterial;
        var block = new MaterialPropertyBlock();
        block.SetColor(BaseColor, new Color(.95f, .76f, .18f));
        renderer.SetPropertyBlock(block);

        go.AddComponent<XRSimpleInteractable>();
        Level5LampSwitch lampSwitch = go.AddComponent<Level5LampSwitch>();
        lampSwitch.Collect(lamp, mainLight);
        Level5HoverHighlight.Attach(go);
        return lampSwitch;
    }

    private void Collect(Transform lamp, Light mainLight)
    {
        if (mainLight != null) lights.Add(mainLight);
        foreach (Light l in lamp.GetComponentsInChildren<Light>(true)) if (!lights.Contains(l)) lights.Add(l);
        foreach (Light l in lights) intensities.Add(l.intensity);
        foreach (Renderer r in lamp.GetComponentsInChildren<Renderer>(true)) if (r.transform != transform) shades.Add(r);
        on = lights.Count == 0 || lights.Exists(l => l.enabled);
    }

    private void Awake()
    {
        GetComponent<XRSimpleInteractable>().selectEntered.AddListener(OnPressed);
    }

    private void OnDestroy()
    {
        XRSimpleInteractable interactable = GetComponent<XRSimpleInteractable>();
        if (interactable != null) interactable.selectEntered.RemoveListener(OnPressed);
    }

    private void OnPressed(SelectEnterEventArgs args)
    {
        if (GameStateManager.CurrentState != GameState.PLAYING) return;
        // Un poke puede entrar y salir varias veces en un instante: una pulsación cada 0,4 s.
        if (Time.unscaledTime - lastToggle < 0.4f) return;
        lastToggle = Time.unscaledTime;
        SetOn(!on);
    }

    public void SetOn(bool value)
    {
        on = value;
        for (int i = 0; i < lights.Count; i++)
        {
            if (lights[i] == null) continue;
            lights[i].enabled = value;
            lights[i].intensity = intensities[i];
        }
        // Apagada, la pantalla deja de brillar; encendida, recupera su material tal cual.
        foreach (Renderer r in shades)
        {
            if (r == null) continue;
            if (value) { r.SetPropertyBlock(null); continue; }
            var block = new MaterialPropertyBlock();
            block.SetColor(EmissionColor, Color.black);
            r.SetPropertyBlock(block);
        }
    }
}
