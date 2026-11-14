using Unity.XR.CoreUtils;
using UnityEngine;

/// <summary>
/// Moving the XR rig so the head lands on a point.
/// XROrigin.MoveCameraToWorldLocation does not support a scaled rig: it measures the head-to-origin offset in the
/// camera's local space (divided by the rig scale) and rotates it back without scaling, so with the rigs of these
/// levels (x1.3 to x2.6) the feet ended up above the floor (2.2 m in Nivel2) and the head off the intended spot
/// whenever the player was not standing in the middle of their play area. This moves the origin by the exact
/// world-space difference instead.
/// </summary>
public static class RigPlacement
{
    public static void MoveCameraTo(XROrigin origin, Vector3 worldPosition)
    {
        if (origin == null || origin.Camera == null) return;
        origin.transform.position += worldPosition - origin.Camera.transform.position;
    }
}
