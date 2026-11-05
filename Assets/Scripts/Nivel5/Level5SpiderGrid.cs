using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cuadrícula de suelo transitable para colocar y mover arañas en Nivel5.
/// Una celda es válida si hay suelo a la altura del piso (no la parte alta de un mueble ni el techo),
/// con espacio libre encima, y es alcanzable desde la posición inicial del jugador sin atravesar paredes.
/// </summary>
public sealed class Level5SpiderGrid
{
    public const float CellSize = 0.5f;
    private const float RayStartAboveFloor = 1.2f;   // por debajo del techo (3,2 m): el rayo nunca lo toca
    private const float RayLength = 2.0f;
    private const float FloorTolerance = 0.30f;       // el piso visible está 13 cm sobre el suelo físico
    private const float BodyRadius = 0.22f;

    private readonly Dictionary<Vector2Int, float> reachable = new Dictionary<Vector2Int, float>();
    private readonly Vector3 origin;
    private readonly float floorY;
    private readonly Func<Collider, bool> ignoreCollider;

    public int Count => reachable.Count;

    private Level5SpiderGrid(Vector3 start, float floorY, Func<Collider, bool> ignoreCollider)
    {
        origin = start;
        this.floorY = floorY;
        this.ignoreCollider = ignoreCollider;
    }

    /// <param name="start">Posición inicial del jugador (el suelo se toma de su altura).</param>
    /// <param name="halfExtent">Radio (m) de la zona que se explora alrededor del inicio.</param>
    /// <param name="ignoreCollider">Colliders que no cuentan como obstáculo (el propio jugador, puertas que se pueden abrir).</param>
    /// <param name="area">Si se indica, solo se exploran celdas dentro de esta zona (plano XZ): el interior del apartamento.</param>
    public static Level5SpiderGrid Build(Vector3 start, float halfExtent, Func<Collider, bool> ignoreCollider, Bounds? area = null)
    {
        var grid = new Level5SpiderGrid(start, start.y, ignoreCollider ?? (_ => false));
        grid.area = area;
        grid.Flood(halfExtent);
        return grid;
    }

    private Bounds? area;

    private Vector2Int ToCell(Vector3 position)
    {
        return new Vector2Int(Mathf.RoundToInt((position.x - origin.x) / CellSize), Mathf.RoundToInt((position.z - origin.z) / CellSize));
    }

    public Vector3 CellCenter(Vector2Int cell)
    {
        return new Vector3(origin.x + cell.x * CellSize, reachable.TryGetValue(cell, out float y) ? y : floorY, origin.z + cell.y * CellSize);
    }

    public bool IsReachable(Vector3 position) => reachable.ContainsKey(ToCell(position));

    public IEnumerable<Vector3> Points()
    {
        foreach (KeyValuePair<Vector2Int, float> item in reachable) yield return CellCenter(item.Key);
    }

    /// <summary>Devuelve el punto alcanzable más cercano, opcionalmente a una distancia mínima de otro punto.</summary>
    public bool TryNearest(Vector3 target, out Vector3 result, Vector3? awayFrom = null, float minAwayDistance = 0f)
    {
        float best = float.MaxValue;
        result = target;
        bool found = false;
        foreach (KeyValuePair<Vector2Int, float> item in reachable)
        {
            Vector3 point = CellCenter(item.Key);
            if (awayFrom.HasValue && Flat(point - awayFrom.Value).magnitude < minAwayDistance) continue;
            float distance = Flat(point - target).sqrMagnitude;
            if (distance < best) { best = distance; result = point; found = true; }
        }
        return found;
    }

    private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    // Altura del suelo de la celda o NaN si no es válida.
    private float SampleGround(float x, float z)
    {
        Vector3 from = new Vector3(x, floorY + RayStartAboveFloor, z);
        RaycastHit[] hits = Physics.RaycastAll(from, Vector3.down, RayLength, ~0, QueryTriggerInteraction.Collide);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            if (ignoreCollider(hit.collider)) continue;
            if (!NPWalkableSurface.IsGround(hit.collider)) continue;
            if (hit.normal.y < 0.7f) return float.NaN;
            if (hit.point.y < floorY - 0.05f || hit.point.y > floorY + FloorTolerance) return float.NaN; // mesa, cama, techo...
            return hit.point.y;
        }
        return float.NaN;
    }

    private bool BodyFits(Vector3 ground)
    {
        Collider[] overlaps = Physics.OverlapCapsule(ground + Vector3.up * (BodyRadius + 0.03f), ground + Vector3.up * 0.6f, BodyRadius, ~0, QueryTriggerInteraction.Ignore);
        foreach (Collider overlap in overlaps)
        {
            if (ignoreCollider(overlap)) continue;
            if (NPWalkableSurface.IsGround(overlap) && overlap.bounds.max.y <= floorY + FloorTolerance) continue; // el propio suelo
            return false;
        }
        return true;
    }

    private bool PathClear(Vector3 a, Vector3 b)
    {
        Vector3 from = a + Vector3.up * 0.25f;
        Vector3 to = b + Vector3.up * 0.25f;
        RaycastHit[] hits = Physics.RaycastAll(from, to - from, (to - from).magnitude, ~0, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit hit in hits)
        {
            if (ignoreCollider(hit.collider)) continue;
            return false;
        }
        return true;
    }

    private void Flood(float halfExtent)
    {
        int radius = Mathf.CeilToInt(halfExtent / CellSize);
        var queue = new Queue<Vector2Int>();
        var visited = new HashSet<Vector2Int>();

        Vector2Int startCell = Vector2Int.zero;
        float startGround = SampleGround(origin.x, origin.z);
        reachable[startCell] = float.IsNaN(startGround) ? floorY : startGround;
        queue.Enqueue(startCell);
        visited.Add(startCell);

        Vector2Int[] steps = { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };
        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            Vector3 currentPoint = CellCenter(current);
            foreach (Vector2Int step in steps)
            {
                Vector2Int next = current + step;
                if (Mathf.Abs(next.x) > radius || Mathf.Abs(next.y) > radius || !visited.Add(next)) continue;

                float x = origin.x + next.x * CellSize;
                float z = origin.z + next.y * CellSize;
                if (area.HasValue && (x < area.Value.min.x || x > area.Value.max.x || z < area.Value.min.z || z > area.Value.max.z)) continue;
                float y = SampleGround(x, z);
                if (float.IsNaN(y)) continue;
                var point = new Vector3(x, y, z);
                if (!BodyFits(point)) continue;
                if (!PathClear(currentPoint, point)) continue;

                reachable[next] = y;
                queue.Enqueue(next);
            }
        }
    }
}
