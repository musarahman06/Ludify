using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Rebuilds the F1 circuit from the centreline in Resources/LudifyTrackPath.txt (the same file the time trial
/// uses), extending it into the west of the old farm: a long straight north, a chicane and a banked 180° corner.
/// Called by Editor/WorldCollidersSceneProcessor.cs on entering Play Mode and in builds, before static batching;
/// the saved scene is never changed.
///   - one new asphalt ribbon for the whole lap (the old mesh is hidden), banked on the hairpin with an embankment;
///   - red/white kerbs on the new corners; old kerbs from the removed corner switched off;
///   - old barriers kept only where they still line the track, new barriers along the new section;
///   - a tall catch wall with sponsor banners round the outside of the banked corner, so cars can't fly out;
///   - fictional sponsor boards along the barriers, F1 style;
///   - crops in the old west farm switched off, grid cars turned to face the racing direction (east).
/// </summary>
public static class TrackExtension
{
    public class Assets
    {
        public TextAsset NewPath, OldPath;
        public Material Asphalt, Kerb, Embankment;
    }

    public const string RootName = "_TrackExtension";
    /// <summary>Areas to paint as grass (old farm, removed corner), picked up by FarmDressing's ground painter.</summary>
    public static readonly List<Rect> GreenRects = new List<Rect>();

    const float HalfWidth = 7f, KerbWidth = 1.5f, SurfaceLift = 0.04f;
    const float BankDegrees = 10f, EmbankmentRun = 3.5f;
    const float BarrierOffset = 11f, BarrierSpacing = 2.5f;
    static readonly Vector2 HairpinCenter = new Vector2(70f, 270f);
    static readonly Rect OldFarmWest = new Rect(0f, 188f, 128f, 117f);   // x 0-128, z 188-305

    static Terrain terrain;

    public static void Build(Scene scene, Assets a)
    {
        GreenRects.Clear();
        var circuit = FindIn(scene, "F1Circuit");
        if (circuit == null || a.NewPath == null || FindIn(scene, RootName) != null) return;
        terrain = FindComponent<Terrain>(scene);
        var path = Parse(a.NewPath.text);
        var oldPath = a.OldPath != null ? Parse(a.OldPath.text) : new List<Vector3>();
        if (path.Count < 10) return;

        var root = new GameObject(RootName).transform;
        SceneManager.MoveGameObjectToScene(root.gameObject, scene);

        // Which points are new (not on the old circuit)?
        var isNew = new bool[path.Count];
        for (int i = 0; i < path.Count; i++) isNew[i] = DistanceToPath(path[i], oldPath) > 2.5f;

        var old = circuit.transform.Find("Track_Asphalt");
        if (old != null) old.gameObject.SetActive(false);

        BuildAsphalt(path, a, root);
        BuildKerbs(path, isNew, a.Kerb, root);
        int kerbsOff = CullOldKerbs(circuit, path);
        var (kept, removed, added) = Barriers(circuit, path, isNew, root);
        BuildCatchWall(path, a.Embankment, root);
        int boards = SponsorBoards(circuit, root, path, a.Embankment);
        int crops = ClearWestFarm(scene);
        TurnGridCars(circuit);

        GreenRects.Add(OldFarmWest);
        GreenRects.Add(new Rect(40f, 160f, 72f, 40f));   // the old north-west corner, now infield

        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            t.gameObject.isStatic = t.GetComponent<TextMeshPro>() == null;   // TMP text builds its mesh at runtime
        Debug.Log($"[TrackExtension] Lap {LapLength(path):0} m. Barriers kept {kept}, removed {removed}, added {added}; " +
                  $"old kerbs trimmed/removed {kerbsOff}; sponsor boards {boards}; farm crops cleared {crops}.");
    }

    // ------------------------------------------------------------------ path

    static List<Vector3> Parse(string text)
    {
        var list = new List<Vector3>();
        foreach (var pair in text.Split(';'))
        {
            if (string.IsNullOrWhiteSpace(pair)) continue;
            var xy = pair.Split(',');
            list.Add(new Vector3(float.Parse(xy[0], CultureInfo.InvariantCulture), 0f, float.Parse(xy[1], CultureInfo.InvariantCulture)));
        }
        return list;
    }

    static float LapLength(List<Vector3> p)
    {
        float l = 0f;
        for (int i = 0; i < p.Count; i++) l += Vector3.Distance(p[i], p[(i + 1) % p.Count]);
        return l;
    }

    static Vector3 At(List<Vector3> p, int i) => p[((i % p.Count) + p.Count) % p.Count];
    static Vector3 Tangent(List<Vector3> p, int i) => (At(p, i + 1) - At(p, i - 1)).normalized;
    static Vector3 Right(List<Vector3> p, int i) => Vector3.Cross(Vector3.up, Tangent(p, i)).normalized;

    static float GroundY(Vector3 p) => terrain != null ? terrain.SampleHeight(p) + terrain.GetPosition().y : 0f;

    static float DistanceToPath(Vector3 q, List<Vector3> path)
    {
        float best = float.MaxValue;
        foreach (var p in path) best = Mathf.Min(best, (new Vector2(p.x - q.x, p.z - q.z)).sqrMagnitude);
        return Mathf.Sqrt(best);
    }

    /// <summary>Signed curvature radius at i (positive = turning right).</summary>
    static float Radius(List<Vector3> p, int i, out bool right)
    {
        Vector3 a = At(p, i - 3), b = At(p, i), c = At(p, i + 3);
        float cross = (b.x - a.x) * (c.z - a.z) - (b.z - a.z) * (c.x - a.x);
        right = cross < 0f;
        float area = Mathf.Abs(cross) * 0.5f;
        if (area < 1e-4f) return float.MaxValue;
        return Vector3.Distance(a, b) * Vector3.Distance(b, c) * Vector3.Distance(c, a) / (4f * area);
    }

    /// <summary>0..1 banking weight: full around the hairpin, eased in and out.</summary>
    static float Bank(Vector3 p)
    {
        if (p.z < 245f) return 0f;
        float d = Vector2.Distance(new Vector2(p.x, p.z), HairpinCenter);
        return 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(22f, 34f, d));
    }

    // ------------------------------------------------------------------ asphalt

    static void BuildAsphalt(List<Vector3> path, Assets a, Transform root)
    {
        int n = path.Count;
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var road = new List<int>();
        var bank = new List<int>();
        float rise = HalfWidth * 2f * Mathf.Tan(BankDegrees * Mathf.Deg2Rad);
        float dist = 0f;

        // Road: two vertices per point (left, right). The outer edge of the hairpin is raised.
        var left = new Vector3[n + 1];
        var right = new Vector3[n + 1];
        var outerIsLeft = new bool[n + 1];
        var bankW = new float[n + 1];
        for (int k = 0; k <= n; k++)
        {
            int i = k % n;
            Vector3 p = path[i], r = Right(path, i);
            Vector3 l = p - r * HalfWidth, rr = p + r * HalfWidth;
            l.y = GroundY(l) + SurfaceLift;
            rr.y = GroundY(rr) + SurfaceLift;
            float b = Bank(p);
            Vector3 center3 = new Vector3(HairpinCenter.x, 0f, HairpinCenter.y);
            bool outerLeft = Vector3.Dot(center3 - p, r) > 0f;   // centre on the right → left edge is outside
            if (outerLeft) l.y += rise * b; else rr.y += rise * b;
            left[k] = l; right[k] = rr; outerIsLeft[k] = outerLeft; bankW[k] = b;

            if (k > 0) dist += Vector3.Distance(path[(k - 1) % n], p);
            verts.Add(l); uvs.Add(new Vector2(0f, dist / (HalfWidth * 2f)));
            verts.Add(rr); uvs.Add(new Vector2(1f, dist / (HalfWidth * 2f)));
        }
        for (int k = 0; k < n; k++)
        {
            int v = k * 2;
            Quad(verts, road, v, v + 2, v + 3, v + 1, Vector3.up);
        }

        // Embankment: a slope from the raised outer edge down to the ground, so the banking isn't floating.
        for (int k = 0; k < n; k++)
        {
            if (bankW[k] < 0.01f && bankW[k + 1] < 0.01f) continue;
            Vector3 top0 = outerIsLeft[k] ? left[k] : right[k], top1 = outerIsLeft[k + 1] ? left[k + 1] : right[k + 1];
            Vector3 out0 = (outerIsLeft[k] ? -1f : 1f) * Right(path, k % n), out1 = (outerIsLeft[k + 1] ? -1f : 1f) * Right(path, (k + 1) % n);
            Vector3 foot0 = top0 + out0 * EmbankmentRun, foot1 = top1 + out1 * EmbankmentRun;
            foot0.y = GroundY(foot0) - 0.05f;
            foot1.y = GroundY(foot1) - 0.05f;
            int b0 = verts.Count;
            verts.Add(top0); verts.Add(top1); verts.Add(foot1); verts.Add(foot0);
            uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(1f, 0f)); uvs.Add(new Vector2(1f, 1f)); uvs.Add(new Vector2(0f, 1f));
            Vector3 outward = (out0 + out1).normalized + Vector3.up;
            Quad(verts, bank, b0, b0 + 1, b0 + 2, b0 + 3, outward);
        }

        var mesh = new Mesh { name = "Track_Asphalt_Extended", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.subMeshCount = 2;
        mesh.SetTriangles(road, 0);
        mesh.SetTriangles(bank, 1);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        var go = new GameObject("Track_Asphalt_Extended");
        go.transform.SetParent(root, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterials = new[] { a.Asphalt, a.Embankment != null ? a.Embankment : a.Asphalt };
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
    }

    /// <summary>Adds a quad (a b c d, in order around it) as two triangles facing <paramref name="facing"/>.</summary>
    static void Quad(List<Vector3> v, List<int> tris, int a, int b, int c, int d, Vector3 facing)
    {
        Vector3 n = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
        if (Vector3.Dot(n, facing) >= 0f) { tris.Add(a); tris.Add(b); tris.Add(c); tris.Add(a); tris.Add(c); tris.Add(d); }
        else { tris.Add(a); tris.Add(c); tris.Add(b); tris.Add(a); tris.Add(d); tris.Add(c); }
    }

    // ------------------------------------------------------------------ kerbs

    static void BuildKerbs(List<Vector3> path, bool[] isNew, Material kerb, Transform root)
    {
        int n = path.Count;
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        float rise = HalfWidth * 2f * Mathf.Tan(BankDegrees * Mathf.Deg2Rad);

        foreach (float side in new[] { -1f, 1f })
        {
            float along = 0f;
            int prev = -1;
            for (int i = 0; i < n; i++)
            {
                bool nearNew = isNew[i] || isNew[(i + 3) % n] || isNew[(i - 3 + n) % n];
                float r = Radius(path, i, out _);
                bool want = nearNew && r < 60f;
                if (!want) { prev = -1; continue; }

                Vector3 p = path[i], rgt = Right(path, i);
                Vector3 inner = p + rgt * side * HalfWidth, outer = p + rgt * side * (HalfWidth + KerbWidth);
                inner.y = GroundY(inner) + SurfaceLift + 0.03f;
                outer.y = GroundY(outer) + SurfaceLift + 0.03f;
                // Follow the banking on the raised edge.
                float b = Bank(p);
                Vector3 c3 = new Vector3(HairpinCenter.x, 0f, HairpinCenter.y);
                bool outerLeft = Vector3.Dot(c3 - p, rgt) > 0f;
                if ((side < 0f) == outerLeft) { inner.y += rise * b; outer.y += rise * b; }

                int v = verts.Count;
                verts.Add(inner); verts.Add(outer);
                uvs.Add(new Vector2(along / 2f, 0f)); uvs.Add(new Vector2(along / 2f, 1f));
                if (prev >= 0) Quad(verts, tris, prev, prev + 1, v + 1, v, Vector3.up);
                prev = v;
                along += Vector3.Distance(path[i], path[(i + 1) % n]);
            }
        }
        if (tris.Count == 0) return;
        var mesh = new Mesh { name = "NewKerbs", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        var go = new GameObject("NewKerbs");
        go.transform.SetParent(root, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = kerb;
    }

    /// <summary>
    /// Trims the old kerbs to the new layout: only triangles lying along the new track's edge (6-9.5 m from the
    /// centreline) survive. Kerbs from the removed corner (or crossing the new asphalt) are cut away, and a kerb
    /// with nothing left is switched off. Works on in-memory mesh copies.
    /// </summary>
    static int CullOldKerbs(GameObject circuit, List<Vector3> path)
    {
        var kerbs = circuit.transform.Find("Kerbs");
        if (kerbs == null) return 0;
        int changed = 0;
        foreach (var mf in kerbs.GetComponentsInChildren<MeshFilter>())
        {
            var mesh = mf.sharedMesh;
            if (mesh == null) continue;
            var v = mesh.vertices;
            var ok = new bool[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                float d = DistanceToPath(mf.transform.TransformPoint(v[i]), path);
                ok[i] = d >= 6f && d <= 9.5f;
            }

            var clipped = Object.Instantiate(mesh);
            clipped.name = mesh.name + " (trimmed)";
            int kept = 0, total = 0;
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                var tris = mesh.GetTriangles(sub);
                var keep = new List<int>(tris.Length);
                for (int t = 0; t < tris.Length; t += 3)
                    if (ok[tris[t]] && ok[tris[t + 1]] && ok[tris[t + 2]]) { keep.Add(tris[t]); keep.Add(tris[t + 1]); keep.Add(tris[t + 2]); }
                clipped.SetTriangles(keep, sub);
                kept += keep.Count;
                total += tris.Length;
            }
            if (kept == total) { Object.DestroyImmediate(clipped); continue; }
            changed++;
            if (kept == 0) { mf.gameObject.SetActive(false); Object.DestroyImmediate(clipped); continue; }
            mf.sharedMesh = clipped;
        }
        return changed;
    }

    // ------------------------------------------------------------------ barriers

    static (int kept, int removed, int added) Barriers(GameObject circuit, List<Vector3> path, bool[] isNew, Transform root)
    {
        var group = circuit.transform.Find("Barriers");
        if (group == null) return (0, 0, 0);
        var keptPositions = new List<Vector3>();
        GameObject red = null, white = null;
        float yawOffset = 0f;
        int kept = 0, removed = 0;
        var offsets = new List<float>();

        foreach (Transform b in group)
        {
            float d = DistanceToPath(b.position, path);
            if (d < 12f || d > 19f) { b.gameObject.SetActive(false); removed++; continue; }
            kept++;
            keptPositions.Add(b.position);
            if (b.name.StartsWith("barrierRed") && red == null) red = b.gameObject;
            if (b.name.StartsWith("barrierWhite") && white == null) white = b.gameObject;
            if (offsets.Count < 30)
            {
                int i = NearestIndex(path, b.position);
                float tangentYaw = Mathf.Atan2(Tangent(path, i).x, Tangent(path, i).z) * Mathf.Rad2Deg;
                offsets.Add(Mathf.Repeat(b.eulerAngles.y - tangentYaw, 180f));
            }
        }
        if (offsets.Count > 0) { offsets.Sort(); yawOffset = offsets[offsets.Count / 2]; }
        if (red == null && white == null) return (kept, removed, 0);

        var parent = new GameObject("NewBarriers").transform;
        parent.SetParent(root, false);
        int added = 0;
        int n = path.Count;
        foreach (float side in new[] { -1f, 1f })
        {
            float since = BarrierSpacing;
            bool useRed = side > 0f;
            for (int i = 0; i < n; i++)
            {
                bool near = isNew[i] || isNew[(i + 4) % n] || isNew[(i - 4 + n) % n];
                float step = Vector3.Distance(path[i], path[(i + 1) % n]);
                if (!near) { since = BarrierSpacing; continue; }
                since += step;
                if (since < BarrierSpacing) continue;

                Vector3 pos = path[i] + Right(path, i) * side * BarrierOffset;
                if (DistanceToPath(pos, path) < 9.5f) continue;   // would sit on another part of the track
                bool tooClose = false;
                foreach (var q in keptPositions) if ((q - pos).sqrMagnitude < 4f) { tooClose = true; break; }
                if (tooClose) continue;
                since = 0f;

                var proto = (useRed ? red : white) ?? red ?? white;
                useRed = !useRed;
                var clone = Object.Instantiate(proto, parent);
                clone.name = proto.name;
                clone.SetActive(true);
                pos.y = GroundY(pos);
                float yaw = Mathf.Atan2(Tangent(path, i).x, Tangent(path, i).z) * Mathf.Rad2Deg + yawOffset;
                clone.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
                keptPositions.Add(pos);
                added++;
            }
        }
        return (kept, removed, added);
    }

    static int NearestIndex(List<Vector3> path, Vector3 q)
    {
        int best = 0;
        float bd = float.MaxValue;
        for (int i = 0; i < path.Count; i++)
        {
            float d = (new Vector2(path[i].x - q.x, path[i].z - q.z)).sqrMagnitude;
            if (d < bd) { bd = d; best = i; }
        }
        return best;
    }

    // ------------------------------------------------------------------ catch wall

    const float WallInset = HalfWidth + KerbWidth + 0.1f, WallThickness = 0.6f, WallAboveRoad = 1.5f;

    /// <summary>Height of the outer (raised) road edge at point i, and which side is outside.</summary>
    static float OuterEdgeY(List<Vector3> path, int i, out Vector3 outward)
    {
        Vector3 p = path[i], r = Right(path, i);
        bool outerLeft = Vector3.Dot(new Vector3(HairpinCenter.x, 0f, HairpinCenter.y) - p, r) > 0f;
        outward = outerLeft ? -r : r;
        Vector3 edge = p + outward * HalfWidth;
        float rise = HalfWidth * 2f * Mathf.Tan(BankDegrees * Mathf.Deg2Rad);
        return GroundY(edge) + SurfaceLift + rise * Bank(p);
    }

    /// <summary>A continuous wall along the outside of the banked corner (and its entry and exit), standing
    /// 1.5 m above the raised road edge, with sponsor banners on its inner face.</summary>
    static void BuildCatchWall(List<Vector3> path, Material concrete, Transform root)
    {
        int n = path.Count;
        var inWall = new bool[n];
        for (int i = 0; i < n; i++)
            if (Bank(path[i]) > 0.02f)
                for (int k = -8; k <= 8; k++) inWall[((i + k) % n + n) % n] = true;

        var verts = new List<Vector3>();
        var tris = new List<int>();
        var banners = new List<(Vector3 pos, Vector3 facing, float edgeY)>();
        float sinceBanner = 0f;
        int brand = 0;
        var parent = new GameObject("CatchWall").transform;
        parent.SetParent(root, false);

        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            if (!inWall[i] || !inWall[j]) continue;
            float e0 = OuterEdgeY(path, i, out var o0), e1 = OuterEdgeY(path, j, out var o1);
            Vector3 in0 = path[i] + o0 * WallInset, in1 = path[j] + o1 * WallInset;
            Vector3 out0 = in0 + o0 * WallThickness, out1 = in1 + o1 * WallThickness;
            float b0 = Mathf.Min(GroundY(in0), GroundY(out0)) - 0.2f, b1 = Mathf.Min(GroundY(in1), GroundY(out1)) - 0.2f;
            float t0 = e0 + WallAboveRoad, t1 = e1 + WallAboveRoad;
            Vector3 up = Vector3.up;
            Vector3 toTrack = -(o0 + o1).normalized;

            // Inner face, top, outer face.
            int v = verts.Count;
            verts.Add(new Vector3(in0.x, b0, in0.z)); verts.Add(new Vector3(in0.x, t0, in0.z));
            verts.Add(new Vector3(in1.x, t1, in1.z)); verts.Add(new Vector3(in1.x, b1, in1.z));
            Quad(verts, tris, v, v + 1, v + 2, v + 3, toTrack);
            v = verts.Count;
            verts.Add(new Vector3(in0.x, t0, in0.z)); verts.Add(new Vector3(out0.x, t0, out0.z));
            verts.Add(new Vector3(out1.x, t1, out1.z)); verts.Add(new Vector3(in1.x, t1, in1.z));
            Quad(verts, tris, v, v + 1, v + 2, v + 3, up);
            v = verts.Count;
            verts.Add(new Vector3(out0.x, b0, out0.z)); verts.Add(new Vector3(out0.x, t0, out0.z));
            verts.Add(new Vector3(out1.x, t1, out1.z)); verts.Add(new Vector3(out1.x, b1, out1.z));
            Quad(verts, tris, v, v + 1, v + 2, v + 3, -toTrack);

            sinceBanner += Vector3.Distance(path[i], path[j]);
            if (sinceBanner >= 6f)
            {
                sinceBanner = 0f;
                Vector3 face = (in0 + in1) * 0.5f + toTrack * 0.15f;   // just in front of the wall, clear of its curve
                banners.Add((face, -toTrack, (e0 + e1) * 0.5f));
            }
        }
        if (tris.Count == 0) return;

        var mesh = new Mesh { name = "CatchWall", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        parent.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        parent.gameObject.AddComponent<MeshRenderer>().sharedMaterial = concrete;
        parent.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh;

        foreach (var (pos, awayFromTrack, edgeY) in banners)
            MakeBoard(parent, new Vector3(pos.x, edgeY + 0.75f, pos.z), awayFromTrack, 4.6f, 1.0f, Sponsors[brand++ % Sponsors.Length], concrete);
    }

    // ------------------------------------------------------------------ sponsors

    /// <summary>Made-up brands (not real companies), F1-style trackside advertising.</summary>
    static readonly (string name, Color bg, Color fg)[] Sponsors =
    {
        ("VOLTEX", new Color(0.07f, 0.07f, 0.07f), new Color(1f, 0.83f, 0f)),
        ("ZEPHYRA", new Color(0.04f, 0.24f, 0.57f), Color.white),
        ("Quantix", Color.white, new Color(0.89f, 0f, 0.17f)),
        ("NOVA-X FUEL", new Color(0.89f, 0f, 0.17f), Color.white),
        ("Kestrel Timing", new Color(0.1f, 0.1f, 0.1f), new Color(0.79f, 0.64f, 0.15f)),
        ("BRIGHTPEAK", new Color(0f, 0.66f, 0.42f), Color.white),
        ("Orbitron Energy", new Color(0.36f, 0.16f, 0.53f), new Color(0.49f, 0.99f, 0f)),
        ("Pixel Cola", new Color(0.84f, 0.16f, 0.16f), Color.white),
        ("LUDIFY", new Color(0.12f, 0.53f, 0.9f), Color.white),
        ("HEXA TYRES", new Color(1f, 0.83f, 0f), new Color(0.07f, 0.07f, 0.07f)),
    };

    static readonly Dictionary<Color, Material> boardMats = new Dictionary<Color, Material>();

    /// <summary>Boards standing just in front of every 6th barrier, facing the track.</summary>
    static int SponsorBoards(GameObject circuit, Transform root, List<Vector3> path, Material template)
    {
        var barriers = new List<Transform>();
        var old = circuit.transform.Find("Barriers");
        if (old != null) foreach (Transform b in old) if (b.gameObject.activeSelf) barriers.Add(b);
        var added = root.Find("NewBarriers");
        if (added != null) foreach (Transform b in added) barriers.Add(b);

        var parent = new GameObject("SponsorBoards").transform;
        parent.SetParent(root, false);
        int count = 0;
        for (int k = 0; k < barriers.Count; k += 6)
        {
            var b = barriers[k];
            Vector3 c = path[NearestIndex(path, b.position)];
            Vector3 toTrack = new Vector3(c.x - b.position.x, 0f, c.z - b.position.z).normalized;
            if (toTrack.sqrMagnitude < 0.5f) continue;
            Vector3 pos = b.position + toTrack * 0.9f;
            pos.y = GroundY(pos) + 0.55f;
            MakeBoard(parent, pos, -toTrack, 4f, 0.9f, Sponsors[count % Sponsors.Length], template);
            count++;
        }
        return count;
    }

    /// <summary>A coloured board with the brand in big letters on its track-facing side.</summary>
    static void MakeBoard(Transform parent, Vector3 center, Vector3 awayFromTrack, float width, float height,
                          (string name, Color bg, Color fg) brand, Material template)
    {
        var board = new GameObject("Sponsor_" + brand.name).transform;
        board.SetParent(parent, false);
        board.SetPositionAndRotation(center, Quaternion.LookRotation(awayFromTrack, Vector3.up));

        var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(panel.GetComponent<Collider>());
        panel.transform.SetParent(board, false);
        panel.transform.localScale = new Vector3(width, height, 0.08f);
        if (!boardMats.TryGetValue(brand.bg, out var m) || m == null)
        {
            m = new Material(template) { name = "Sponsor_" + ColorUtility.ToHtmlStringRGB(brand.bg), color = brand.bg };
            boardMats[brand.bg] = m;
        }
        panel.GetComponent<MeshRenderer>().sharedMaterial = m;

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(board, false);
        textGo.transform.localPosition = new Vector3(0f, 0f, -0.05f);   // just in front, on the track side
        var tmp = textGo.AddComponent<TextMeshPro>();
        tmp.text = brand.name;
        tmp.color = brand.fg;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Bold | FontStyles.Italic;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 1f;
        tmp.fontSizeMax = 12f;
        tmp.rectTransform.sizeDelta = new Vector2(width * 0.9f, height * 0.8f);
    }

    // ------------------------------------------------------------------ farm & grid

    static int ClearWestFarm(Scene scene)
    {
        var crops = FindIn(scene, "FarmCrops");
        if (crops == null) return 0;
        int n = 0;
        foreach (Transform c in crops.transform)
            if (OldFarmWest.Contains(new Vector2(c.position.x, c.position.z)) && c.gameObject.activeSelf) { c.gameObject.SetActive(false); n++; }
        return n;
    }

    /// <summary>The grid cars were placed facing west; turn each one around on the spot to face the racing direction.</summary>
    static void TurnGridCars(GameObject circuit)
    {
        var grid = circuit.transform.Find("GridCars");
        if (grid == null) return;
        foreach (Transform car in grid)
        {
            var rs = car.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) continue;
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            car.RotateAround(new Vector3(b.center.x, car.position.y, b.center.z), Vector3.up, 180f);
        }
    }

    // ------------------------------------------------------------------ scene helpers

    static GameObject FindIn(Scene scene, string name)
    {
        foreach (var r in scene.GetRootGameObjects())
            foreach (var tr in r.GetComponentsInChildren<Transform>(true))
                if (tr.name == name) return tr.gameObject;
        return null;
    }

    static T FindComponent<T>(Scene scene) where T : Component
    {
        foreach (var r in scene.GetRootGameObjects())
        {
            var c = r.GetComponentInChildren<T>(true);
            if (c != null) return c;
        }
        return null;
    }
}
