using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// The walkable city streets. Builds the city NavMesh with every building blocked out (so nothing walks on roofs or
/// inside buildings), and only accepts ground-level points connected to the main street network, so NPCs, lost pets
/// and items always end up outside on the roads and pavements.
/// </summary>
public static class CityNav
{
    /// <summary>Street level: anything higher is a roof, a bridge deck or a prop.</summary>
    const float MaxGroundY = 1f;

    /// <summary>A point on the main street network; everything valid must be reachable from here.</summary>
    public static Vector3? Hub { get; private set; }

    static readonly NavMeshPath path = new NavMeshPath();

    public static void Build(GameObject root)
    {
        float started = Time.realtimeSinceStartup;

        // Recast only sees a collider box's faces, so it would put walkable floor inside each building and on its
        // roof. Mark each building's whole volume as not walkable instead.
        var blockers = new GameObject("BuildingBlockers").transform;
        blockers.SetParent(root.transform, false);
        const int notWalkable = 1;
        foreach (var building in CityColorizer.Buildings)
        {
            var go = new GameObject("Blocker");
            go.transform.SetParent(blockers, false);
            go.transform.position = building.Bounds.center;
            var volume = go.AddComponent<NavMeshModifierVolume>();
            volume.center = Vector3.zero;
            volume.size = building.Bounds.size + new Vector3(0.3f, 2f, 0.3f);
            volume.area = notWalkable;
        }

        var surface = root.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Volume;
        // East bank only (the river curves, so leave a margin; CityArea.Contains trims the rest).
        var min = new Vector3(CityArea.MinX - 20f, -5f, CityArea.MinZ - 5f);
        var max = new Vector3(CityArea.MaxX + 5f, 15f, CityArea.MaxZ + 5f);
        surface.center = (min + max) * 0.5f;
        surface.size = max - min;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.overrideVoxelSize = true;
        surface.voxelSize = 0.3f;
        surface.BuildNavMesh();

        FindHub();
        Debug.Log($"[CityLife] City NavMesh built in {(Time.realtimeSinceStartup - started) * 1000f:0} ms " +
                  $"({CityColorizer.Buildings.Count} buildings blocked out, hub {Hub}).");
    }

    /// <summary>Of a handful of random street points, the one that can reach the most others: it's on the main network,
    /// not in a closed-off courtyard.</summary>
    static void FindHub()
    {
        Hub = null;
        var samples = new List<Vector3>();
        for (int i = 0; i < 200 && samples.Count < 16; i++)
        {
            Vector3 p = CityArea.RandomPoint();
            if (NavMesh.SamplePosition(new Vector3(p.x, 0.3f, p.z), out var hit, 3f, NavMesh.AllAreas) && hit.position.y <= MaxGroundY)
                samples.Add(hit.position);
        }
        int best = -1;
        foreach (var a in samples)
        {
            int reach = 0;
            foreach (var b in samples)
                if (NavMesh.CalculatePath(a, b, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete) reach++;
            if (reach > best) { best = reach; Hub = a; }
        }
    }

    /// <summary>On the street network in the city: ground level, outside, reachable from the hub.</summary>
    public static bool IsStreet(Vector3 p)
    {
        if (p.y > MaxGroundY || !CityArea.Contains(p)) return false;
        if (!Hub.HasValue) return true;
        return NavMesh.CalculatePath(Hub.Value, p, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
    }

    /// <summary>The nearest street point to <paramref name="p"/> (within a few metres), or null.</summary>
    public static Vector3? Snap(Vector3 p, float radius = 3f)
    {
        if (NavMesh.SamplePosition(new Vector3(p.x, 0.3f, p.z), out var hit, radius, NavMesh.AllAreas) && IsStreet(hit.position))
            return hit.position;
        return null;
    }

    /// <summary>A random street point anywhere in the city.</summary>
    public static Vector3? RandomPoint(int tries = 30)
    {
        for (int i = 0; i < tries; i++)
        {
            Vector3? p = Snap(CityArea.RandomPoint());
            if (p.HasValue) return p;
        }
        return null;
    }
}
