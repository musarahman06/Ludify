using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// The F1 circuit's centerline: 396 points 2 m apart, in racing direction, loaded from
/// Resources/LudifyTrackPath.txt (x,z;x,z;…). Used for lap progress, checkpoints and question triggers.
/// </summary>
public sealed class TrackPath
{
    /// <summary>World XZ of the checkered start/finish line.</summary>
    static readonly Vector2 StartLine = new Vector2(125f, 40f);

    readonly List<Vector3> points = new List<Vector3>();
    int lastIndex = -1;

    public int Count => points.Count;
    public int StartIndex { get; }
    public Vector3 PointAt(int i) => points[Wrap(i)];
    public Vector3 TangentAt(int i) => (PointAt(i + 1) - PointAt(i - 1)).normalized;
    public int Wrap(int i) => ((i % Count) + Count) % Count;

    TrackPath(string data)
    {
        foreach (var pair in data.Split(';'))
        {
            if (string.IsNullOrWhiteSpace(pair)) continue;
            var xy = pair.Split(',');
            points.Add(new Vector3(float.Parse(xy[0], CultureInfo.InvariantCulture), 0f, float.Parse(xy[1], CultureInfo.InvariantCulture)));
        }
        float best = float.MaxValue;
        for (int i = 0; i < points.Count; i++)
        {
            float d = (new Vector2(points[i].x, points[i].z) - StartLine).sqrMagnitude;
            if (d < best) { best = d; StartIndex = i; }
        }
    }

    public static TrackPath Load()
    {
        var asset = Resources.Load<TextAsset>("LudifyTrackPath");
        if (asset == null) { Debug.LogError("[TrackPath] Resources/LudifyTrackPath.txt is missing."); return null; }
        return new TrackPath(asset.text);
    }

    /// <summary>Index of the centerline point nearest to a world position (searches near the last result first).</summary>
    public int NearestIndex(Vector3 position)
    {
        var p = new Vector2(position.x, position.z);
        if (lastIndex >= 0)
        {
            int local = Search(p, lastIndex - 25, lastIndex + 25, out float localDist);
            if (localDist < 30f * 30f) return lastIndex = local;
        }
        return lastIndex = Search(p, 0, Count - 1, out _);
    }

    public void ResetSearch() => lastIndex = -1;

    /// <summary>Distance (in points) ahead of the start line: 0 at the line, Count-1 just before it.</summary>
    public int ProgressFromStart(int index) => Wrap(index - StartIndex);

    int Search(Vector2 p, int from, int to, out float bestDist)
    {
        int bestIndex = Wrap(from);
        bestDist = float.MaxValue;
        for (int i = from; i <= to; i++)
        {
            var q = points[Wrap(i)];
            float d = (new Vector2(q.x, q.z) - p).sqrMagnitude;
            if (d < bestDist) { bestDist = d; bestIndex = Wrap(i); }
        }
        return bestIndex;
    }
}
