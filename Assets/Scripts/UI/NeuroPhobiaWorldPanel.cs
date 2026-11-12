using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// Colocación de lienzos en el espacio del mundo para el visor.
/// Los paneles con botones se ponen delante de la persona, a la altura de los ojos y sin atravesar paredes;
/// los HUD informativos siguen la vista con retardo, lejos y fuera del centro (NeuroPhobiaHudFollow).
/// Un lienzo pegado a la cámara (ScreenSpaceCamera) queda a la distancia del plano de la cámara y el rayo de XRI
/// no interactúa bien con él: por eso todo lienzo de nivel pasa a World Space.
/// </summary>
public static class NeuroPhobiaWorldPanel
{
    public const float PanelDistance = 1.7f;
    public const float MinimumDistance = 0.6f;
    public const float WallMargin = 0.12f;
    public const float EyeDrop = 0.08f;

    /// <summary>Pasa el lienzo a World Space con un tamaño físico fijo (anchura en metros) y lo deja interactuable con el rayo.</summary>
    public static void MakeWorldSpace(Canvas canvas, Vector2 size, float widthMeters, Camera eventCamera, bool interactive = true, int sortingOrder = 0)
    {
        if (canvas == null) return;
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = eventCamera;
        RectTransform rect = canvas.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.localScale = Vector3.one * (widthMeters / Mathf.Max(1f, size.x));

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            scaler.dynamicPixelsPerUnit = 3f;
        }

        // El orden de dibujo se fija ANTES de añadir los raycasters: TrackedDeviceGraphicRaycaster se registra con el
        // estado del lienzo y, si este cambia después, al desactivarse lanza KeyNotFoundException.
        if (sortingOrder != 0) canvas.sortingOrder = sortingOrder;
        if (!interactive) return;   // un HUD informativo no necesita recibir el rayo
        if (canvas.GetComponent<TrackedDeviceGraphicRaycaster>() == null) canvas.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
        if (canvas.GetComponent<GraphicRaycaster>() == null) canvas.gameObject.AddComponent<GraphicRaycaster>();
    }

    /// <summary>Cámara de juego activa (XR o escritorio).</summary>
    public static Camera GameplayCamera()
    {
        Camera main = Camera.main;
        if (main != null && main.isActiveAndEnabled) return main;
        foreach (Camera camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            if (camera.isActiveAndEnabled && camera.targetTexture == null) return camera;
        }
        return null;
    }

    /// <summary>
    /// Punto delante de la cabeza, a la altura de los ojos, recortado para no quedar detrás de una pared.
    /// Devuelve la distancia final, por si hay que escalar el panel para que conserve su tamaño aparente.
    /// </summary>
    public static float PlaceInFront(Transform panel, Transform head, float distance, Vector3 localOffset)
    {
        Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.ProjectOnPlane(head.up, Vector3.up);
        forward.Normalize();
        Quaternion facing = Quaternion.LookRotation(forward, Vector3.up);

        Vector3 eye = head.position;
        Vector3 offset = facing * new Vector3(localOffset.x, localOffset.y - EyeDrop, 0f);
        Vector3 direction = (forward * distance + offset).normalized;
        float reach = ClearDistance(eye, direction, (forward * distance + offset).magnitude, head.root);
        float scale = reach / Mathf.Max(0.01f, (forward * distance + offset).magnitude);

        panel.position = eye + direction * reach;
        panel.rotation = facing;
        return distance * scale;
    }

    // Distancia libre en una dirección, ignorando triggers, el propio rig y los lienzos.
    private static float ClearDistance(Vector3 origin, Vector3 direction, float wanted, Transform rig)
    {
        RaycastHit[] hits = Physics.RaycastAll(origin, direction, wanted + WallMargin, ~0, QueryTriggerInteraction.Ignore);
        float nearest = wanted;
        foreach (RaycastHit hit in hits)
        {
            if (rig != null && hit.collider.transform.IsChildOf(rig)) continue;
            if (hit.collider.GetComponentInParent<Canvas>() != null) continue;
            if (hit.collider.attachedRigidbody != null && !hit.collider.attachedRigidbody.isKinematic) continue;
            nearest = Mathf.Min(nearest, hit.distance - WallMargin);
        }
        return Mathf.Max(MinimumDistance, nearest);
    }
}
