using UnityEngine;

/// <summary>
/// Punto de «la mano» para las piezas que se agarran: con la mano cerca es el propio mando; agarrada con el rayo
/// desde lejos, es un punto a la misma distancia del mando que la pieza en el momento de agarrarla, de modo que la
/// pieza sigue a la punta del rayo.
/// </summary>
public static class Level5HandPoint
{
    public static float ReachAtGrab(Transform attach, Transform part)
    {
        if (attach == null || part == null) return 0f;
        Collider collider = part.GetComponentInChildren<Collider>();
        Vector3 closest = collider != null && collider.enabled ? collider.ClosestPoint(attach.position) : part.position;
        float distance = Vector3.Distance(attach.position, closest);
        return distance < 0.25f ? 0f : distance;
    }

    public static Vector3 Point(Transform attach, float reach)
    {
        return attach.position + attach.forward * reach;
    }
}
