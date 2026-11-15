using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// The armchair and the sofa of Nivel2 (scene objects whose root name contains "sofa").
/// - They had no colliders, so the player walked through them. Each visible part gets a box collider that follows
///   its own mesh (oriented with the part), which is enough to stop the player and to measure the seat.
/// - <see cref="TryGetSeat"/> finds where a person really sits: the side with the backrest is the highest one, the
///   front is the opposite side, and the seat is the cushion between the backrest and the front edge.
/// Shared by Level2SpawnAlign (the player starts next to the armchair, the spider jar in front of it) and
/// Level2SitToggle (sitting puts the eyes over the cushion, facing the front).
/// </summary>
public static class Level2Armchairs
{
    public sealed class Seat
    {
        public Transform root;
        public Bounds bounds;
        /// <summary>Point on the cushion where the hips go (world).</summary>
        public Vector3 seatPoint;
        /// <summary>Horizontal unit vector pointing out of the front of the seat.</summary>
        public Vector3 front;
        /// <summary>Height (world y) of the top of the cushion.</summary>
        public float cushionTop;
        /// <summary>Distance (world) from the seat point to the front edge of the furniture.</summary>
        public float frontReach;
        public bool isArmchair;
    }

    private const string CollidersFlag = "NP_Level2_SolidSofa";

    /// <summary>Every sofa root of the open scene, with its seat measured (colliders are added on first call).</summary>
    public static List<Seat> All()
    {
        var seats = new List<Seat>();
        foreach (Transform root in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!IsSofaRoot(root)) continue;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) continue;
            EnsureColliders(root);
            if (TryGetSeat(root, out Seat seat)) seats.Add(seat);
        }

        // The armchair is the smallest piece (the "small_sofa"); the long sofa is the other one.
        Seat smallest = seats.OrderBy(s => s.bounds.size.x * s.bounds.size.z).FirstOrDefault();
        foreach (Seat seat in seats)
            seat.isArmchair = seat.root.name.ToLowerInvariant().Contains("small") || (seats.Count > 0 && seat == smallest && !seats.Any(s => s.root.name.ToLowerInvariant().Contains("small")));
        return seats;
    }

    /// <summary>The armchair the level is built around, or null.</summary>
    public static Seat Armchair() => All().FirstOrDefault(s => s.isArmchair);

    public static bool IsSofaRoot(Transform t)
    {
        if (!t.name.ToLowerInvariant().Contains("sofa") || t.name.StartsWith("NP_Seat_Zone")) return false;
        for (Transform p = t.parent; p != null; p = p.parent)
            if (p.name.ToLowerInvariant().Contains("sofa")) return false;
        return true;
    }

    // The exact shape of each visible part (the sofa models are imported with Read/Write so this also works in the
    // headset); a box from the part's own bounds if a mesh cannot be read.
    private static void EnsureColliders(Transform root)
    {
        if (root.Find(CollidersFlag) != null) return;
        new GameObject(CollidersFlag).transform.SetParent(root, false);
        foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>())
        {
            if (renderer.GetComponent<Collider>() != null) continue;
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) continue;
            if (filter.sharedMesh.isReadable)
            {
                renderer.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
                continue;
            }
            Bounds local = filter.sharedMesh.bounds;
            BoxCollider box = renderer.gameObject.AddComponent<BoxCollider>();
            box.center = local.center;
            box.size = local.size;
        }
        Physics.SyncTransforms();
    }

    /// <summary>Measures the seat of one sofa root (its colliders must exist).</summary>
    public static bool TryGetSeat(Transform root, out Seat seat)
    {
        seat = null;
        Collider[] colliders = root.GetComponentsInChildren<Collider>();
        if (colliders.Length == 0) return false;
        Bounds bounds = colliders[0].bounds;
        foreach (Collider c in colliders) bounds.Encapsulate(c.bounds);

        // Candidate front directions: the four horizontal axes of the furniture.
        Vector3 forward = Flat(root.forward).normalized;
        Vector3 right = Flat(root.right).normalized;
        if (forward.sqrMagnitude < 0.5f) forward = Vector3.forward;
        if (right.sqrMagnitude < 0.5f) right = Vector3.right;
        Vector3[] axes = { forward, -forward, right, -right };

        // The backrest is the side whose top is highest, measured a little inside each edge.
        Vector3 back = forward;
        float highest = float.MinValue;
        foreach (Vector3 axis in axes)
        {
            float reach = Reach(bounds, axis);
            float top = TopAt(colliders, bounds, bounds.center + axis * reach * 0.75f);
            if (top > highest) { highest = top; back = axis; }
        }
        Vector3 front = -back;
        float depth = Reach(bounds, front);

        // Cushion: lowest top along the front half, between the middle and the front edge.
        float cushionTop = float.MaxValue;
        Vector3 cushionPoint = bounds.center;
        for (int i = 0; i <= 6; i++)
        {
            Vector3 point = bounds.center + front * depth * Mathf.Lerp(-0.2f, 0.7f, i / 6f);
            float top = TopAt(colliders, bounds, point);
            if (top <= bounds.min.y + 0.01f) continue;   // fell between parts
            if (top < cushionTop - 0.01f) { cushionTop = top; cushionPoint = point; }
        }
        if (cushionTop == float.MaxValue) { cushionTop = bounds.min.y + bounds.size.y * 0.4f; }

        // The hips go a bit behind the middle of the cushion (towards the backrest), as when really sitting.
        Vector3 seatPoint = bounds.center + front * depth * 0.05f;
        seatPoint.y = cushionTop;

        seat = new Seat
        {
            root = root,
            bounds = bounds,
            seatPoint = seatPoint,
            front = front,
            cushionTop = cushionTop,
            frontReach = Vector3.Dot(Flat(bounds.center + front * depth - seatPoint), front)
        };
        Debug.Log($"NeuroPhobia: asiento {root.name} frente={front} cojín={cushionTop:0.00} respaldo={highest:0.00} punto={seatPoint}");
        return true;
    }

    // Half size of the bounds along a horizontal axis.
    private static float Reach(Bounds bounds, Vector3 axis) =>
        Mathf.Abs(axis.x) * bounds.extents.x + Mathf.Abs(axis.z) * bounds.extents.z;

    // Highest point of the furniture's own colliders straight below a point.
    private static float TopAt(Collider[] colliders, Bounds bounds, Vector3 point)
    {
        Ray ray = new Ray(new Vector3(point.x, bounds.max.y + 1f, point.z), Vector3.down);
        float best = bounds.min.y;
        foreach (Collider c in colliders)
        {
            if (c.Raycast(ray, out RaycastHit hit, bounds.size.y + 2f) && hit.point.y > best) best = hit.point.y;
        }
        return best;
    }

    public static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
}
