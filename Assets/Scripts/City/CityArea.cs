using UnityEngine;

/// <summary>
/// Geometry of the city (the east bank of the river): which positions count as "in the city", district names, and
/// compass/landmark wording used by quest hints. Matches the procedural terrain the map was generated from.
/// </summary>
public static class CityArea
{
    public const float MinX = 300f, MaxX = 494f, MinZ = 6f, MaxZ = 494f;

    /// <summary>World X of the river's center line at world Z (the curve that carved the terrain).</summary>
    public static float RiverCenterX(float z)
    {
        float t = (500f - z) / 500f;
        return 250f + 40f * Mathf.Sin(t * 2.6f + 0.3f);
    }

    /// <summary>True for walkable city ground: east of the river bank, inside the map edges.</summary>
    public static bool Contains(Vector3 p) =>
        p.x >= Mathf.Max(MinX, RiverCenterX(p.z) + 30f) && p.x <= MaxX && p.z >= MinZ && p.z <= MaxZ;

    public static Vector3 RandomPoint()
    {
        float z = Random.Range(MinZ, MaxZ);
        float x = Random.Range(Mathf.Max(MinX, RiverCenterX(z) + 30f), MaxX);
        return new Vector3(x, 0f, z);
    }

    static readonly (string name, float z)[] Bridges =
    {
        ("Suspension Bridge", 410f), ("S1 Bridge", 350f), ("Truss Bridge", 240f), ("Arch Bridge", 140f),
    };

    /// <summary>Broad area wording, e.g. "among the tall towers downtown" or "up in the north of the city, by the S1 Bridge".</summary>
    public static string DescribeArea(Vector3 p)
    {
        string district = p.z >= 300f ? "up in the north part of the city"
            : p.z >= 175f ? "among the tall towers downtown"
            : "down in the south part of the city";

        float riverEdge = RiverCenterX(p.z) + 26f;
        if (p.x - riverEdge < 40f)
        {
            foreach (var b in Bridges)
                if (Mathf.Abs(p.z - b.z) < 45f) return $"{district}, near the {b.name}";
            return $"{district}, close to the river";
        }
        if (p.x > 450f) return $"{district}, over on the far east side";
        return district;
    }

    /// <summary>"north-east of here, about 70 m" from one point to another.</summary>
    public static string DescribeDirection(Vector3 from, Vector3 to)
    {
        Vector3 d = to - from;
        d.y = 0f;
        float dist = d.magnitude;
        if (dist < 12f) return "really close to here";
        string[] names = { "north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west" };
        float angle = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;     // 0 = +Z = north on the map
        int index = Mathf.RoundToInt(((angle + 360f) % 360f) / 45f) % 8;
        int rounded = Mathf.Max(10, Mathf.RoundToInt(dist / 10f) * 10);
        return $"{names[index]} of here, about {rounded} m away";
    }
}
