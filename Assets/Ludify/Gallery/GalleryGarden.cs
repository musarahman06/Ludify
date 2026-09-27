using System.Collections.Generic;
using UnityEngine;
using static Ludify.Import.ModelKit;

namespace Ludify.Gallery
{
    /// <summary>
    /// The cherry-blossom garden the gallery sits in: pink ground (painted on an in-memory copy of the terrain), a
    /// winding stone path from the entrance past every easel, low-poly cherry trees with glowing lanterns, fallen
    /// petals, rocks and ferns. Everything decorative is merged into a few meshes (one per color) so the whole garden
    /// costs only a handful of draw calls. Seeded, so it looks the same every run.
    /// </summary>
    public static class GalleryGarden
    {
        const int Seed = 20260927;
        const float PathHalfWidth = 1.4f;

        static readonly Color GroundPink = new Color(0.93f, 0.66f, 0.72f);
        static readonly Color[] Blossoms = { Hex("#F4A6C0"), Hex("#EE8FB0"), Hex("#F7BCD1"), Hex("#E27DA2") };
        static readonly Color[] Bark = { Hex("#6B4A3A"), Hex("#5A3D30") };
        static readonly Color[] StoneTones = { Hex("#9C9A9E"), Hex("#B4B1B5") };
        static readonly Color[] PetalTones = { Hex("#F59BB8"), Hex("#FBC4D6") };
        static readonly Color[] RockTones = { Hex("#7E7B84"), Hex("#908C96") };
        static readonly Color[] FernTones = { Hex("#4E9A3A"), Hex("#6DBB4A") };
        static readonly Color LanternGlow = Hex("#E6F77A");

        /// <summary>A path is a smoothed polyline on the ground (world XZ, y = 0).</summary>
        public sealed class PathLine
        {
            public readonly List<Vector3> Points = new List<Vector3>();
            /// <summary>Easels may line this path (not the short links to the bridges).</summary>
            public bool Exhibits = true;
        }

        // ------------------------------------------------------------------ ground

        /// <summary>West edge of the city ground at a given Z (the river bank; matches City Life's city area).</summary>
        public static float WestEdge(float z) => Mathf.Max(300f, CityArea.RiverCenterX(z) + 30f);

        /// <summary>Paints the gallery pink on the terrain (a runtime copy: the saved terrain asset is never touched).</summary>
        public static void PaintGround(Rect area)
        {
            Terrain terrain = Terrain.activeTerrain != null ? Terrain.activeTerrain : Object.FindAnyObjectByType<Terrain>();
            if (terrain == null) return;
            TerrainData data = RuntimeCopy(terrain);

            var layer = new TerrainLayer { name = "Z_GalleryPink", diffuseTexture = GroundTexture, tileSize = new Vector2(TileSize, TileSize) };
            var layers = new List<TerrainLayer>(data.terrainLayers) { layer };
            data.terrainLayers = layers.ToArray();
            int pink = layers.Count - 1;

            Vector3 origin = terrain.GetPosition(), size = data.size;
            int res = data.alphamapResolution, count = data.alphamapLayers;
            int x0 = Mathf.Clamp(Mathf.FloorToInt((area.xMin - origin.x) / size.x * res), 0, res - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((area.yMin - origin.z) / size.z * res), 0, res - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt((area.xMax - origin.x) / size.x * res), 0, res - 1);
            int z1 = Mathf.Clamp(Mathf.CeilToInt((area.yMax - origin.z) / size.z * res), 0, res - 1);
            float[,,] map = data.GetAlphamaps(x0, z0, x1 - x0 + 1, z1 - z0 + 1);
            for (int j = 0; j <= z1 - z0; j++)
                for (int i = 0; i <= x1 - x0; i++)
                {
                    float wx = origin.x + (x0 + i + 0.5f) / res * size.x, wz = origin.z + (z0 + j + 0.5f) / res * size.z;
                    // Soft edge: 3 m blend at the river bank and the gallery's south edge.
                    float edge = Mathf.Min(wx - (WestEdge(wz) - 4f), wz - area.yMin, area.xMax - wx, area.yMax - wz);
                    float blend = Mathf.Clamp01(edge / 3f);
                    if (blend <= 0f) continue;
                    for (int l = 0; l < count; l++) map[j, i, l] = Mathf.Lerp(map[j, i, l], l == pink ? 1f : 0f, blend);
                }
            data.SetAlphamaps(x0, z0, map);
        }

        const float TileSize = 12f;
        /// <summary>Road covers (area and top height), so stones and petals on them sit on top.</summary>
        static readonly List<(Rect area, float top)> covers = new List<(Rect, float)>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => covers.Clear();

        /// <summary>Height of the ground at a point: 0, or the top of a road cover.</summary>
        static float Lift(Vector3 p)
        {
            foreach (var (area, top) in covers) if (area.Contains(new Vector2(p.x, p.z))) return top;
            return 0f;
        }
        static Texture2D groundTexture;
        static Texture2D GroundTexture => groundTexture != null ? groundTexture : groundTexture = FacetTexture(GroundPink);

        /// <summary>
        /// Covers the part of a road that runs into the gallery but can't be hidden (it continues outside, e.g. the long
        /// east spine) with a thin slab of the same pink ground.
        /// </summary>
        public static void CoverRoad(Transform root, Rect overlap, float roadTop)
        {
            float top = roadTop + 0.015f;
            covers.Add((overlap, top));
            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = "RoadCover";
            Object.Destroy(slab.GetComponent<Collider>());
            slab.transform.SetParent(root, false);
            slab.transform.position = new Vector3(overlap.center.x, top / 2f, overlap.center.y);
            slab.transform.localScale = new Vector3(overlap.width + 0.6f, top, overlap.height);
            var mat = new Material(Mat(Color.white)) { name = "GalleryGroundCover" };
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", GroundTexture);
            mat.mainTexture = GroundTexture;
            mat.mainTextureScale = new Vector2((overlap.width + 0.6f) / TileSize, overlap.height / TileSize);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.08f);
            slab.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        /// <summary>The terrain's in-memory copy (made once per session), so painting never changes the asset on disk.</summary>
        static TerrainData RuntimeCopy(Terrain terrain)
        {
            TerrainData data = terrain.terrainData;
            if (data.name.Contains("(farm painted)") || data.name.Contains("(runtime)")) return data;
            data = Object.Instantiate(data);
            data.name = terrain.terrainData.name + " (runtime)";
            terrain.terrainData = data;
            var collider = terrain.GetComponent<TerrainCollider>();
            if (collider != null) collider.terrainData = data;
            return data;
        }

        /// <summary>Low-poly ground texture: small triangles in slightly different shades of one color.</summary>
        static Texture2D FacetTexture(Color baseColor)
        {
            const int n = 128, cells = 8;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            var rng = new System.Random(Seed);
            var shades = new float[cells, cells, 2];
            for (int a = 0; a < cells; a++) for (int b = 0; b < cells; b++) for (int k = 0; k < 2; k++) shades[a, b, k] = 0.94f + (float)rng.NextDouble() * 0.1f;
            int cellSize = n / cells;
            var pixels = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int cx = x / cellSize, cy = y / cellSize;
                    bool upper = (x % cellSize) + (y % cellSize) >= cellSize;   // two triangles per cell
                    float s = shades[cx, cy, upper ? 1 : 0];
                    pixels[y * n + x] = new Color(baseColor.r * s, baseColor.g * s, baseColor.b * s, 0.08f);   // terrain reads alpha as smoothness: keep it matte
                }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        // ------------------------------------------------------------------ paths

        /// <summary>
        /// The path network: a spine from the entrance to the far end, a big loop round the gallery with a cross path,
        /// and short links to the two bridges from the suburbs. Wobbled a little so it doesn't look ruled.
        /// </summary>
        public static List<PathLine> PathNetwork(Rect area, Vector3 entrance)
        {
            float cx = 400f, cz = (area.yMin + area.yMax) * 0.5f;
            float west = 332f, east = 468f, south = area.yMin + 30f, north = area.yMax - 26f;
            var lines = new List<PathLine>
            {
                Smooth(new[] { new Vector3(entrance.x, 0, entrance.z), new Vector3(cx, 0, south), new Vector3(cx, 0, cz), new Vector3(cx, 0, north), new Vector3(cx, 0, area.yMax - 8f) }, false),
                Smooth(new[] { new Vector3(west, 0, south), new Vector3(west, 0, north), new Vector3(east, 0, north), new Vector3(east, 0, south) }, true),
                Smooth(new[] { new Vector3(west, 0, cz), new Vector3(cx, 0, cz), new Vector3(east, 0, cz) }, false),
            };
            foreach (float bridgeZ in new[] { 350f, 410f })   // S1 and Suspension bridges come in on the west side
            {
                var link = Smooth(new[] { new Vector3(WestEdge(bridgeZ) - 2f, 0, bridgeZ), new Vector3(west, 0, bridgeZ) }, false);
                link.Exhibits = false;
                lines.Add(link);
            }
            return lines;
        }

        static PathLine Smooth(Vector3[] controls, bool closed)
        {
            // Catmull-Rom through the control points, then a gentle sideways wobble.
            var line = new PathLine();
            int n = controls.Length, segments = closed ? n : n - 1;
            for (int s = 0; s < segments; s++)
            {
                Vector3 p0 = controls[closed ? (s - 1 + n) % n : Mathf.Max(s - 1, 0)];
                Vector3 p1 = controls[s];
                Vector3 p2 = controls[(s + 1) % n];
                Vector3 p3 = controls[closed ? (s + 2) % n : Mathf.Min(s + 2, n - 1)];
                int steps = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(p1, p2) / 1.5f));
                for (int k = 0; k < steps; k++)
                {
                    float t = k / (float)steps;
                    line.Points.Add(0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t + (-p0 + 3f * p1 - 3f * p2 + p3) * t * t * t));
                }
            }
            if (closed) line.Points.Add(line.Points[0]);
            else line.Points.Add(controls[n - 1]);

            for (int i = 1; i < line.Points.Count - 1; i++)
            {
                Vector3 d = line.Points[i + 1] - line.Points[i - 1];
                Vector3 side = new Vector3(-d.z, 0, d.x).normalized;
                float w = (Mathf.PerlinNoise(line.Points[i].x * 0.03f, line.Points[i].z * 0.03f) - 0.5f) * 3f;
                line.Points[i] += side * w;
            }
            return line;
        }

        /// <summary>Spots for easels along the paths: alternating sides, facing the path, spread out.</summary>
        public static List<(Vector3 position, Quaternion rotation)> EaselSpots(List<PathLine> paths, Vector3 entrance, int max)
        {
            var spots = new List<(Vector3, Quaternion)>();
            var junctions = new List<Vector3> { entrance };
            foreach (var p in paths) { junctions.Add(p.Points[0]); junctions.Add(p.Points[p.Points.Count - 1]); }
            junctions.Add(new Vector3(400f, 0, (GalleryArea.Area.yMin + GalleryArea.Area.yMax) * 0.5f));

            bool left = false;
            foreach (var path in paths)
            {
                if (!path.Exhibits) continue;
                float walked = 12f;
                for (int i = 1; i < path.Points.Count - 1 && spots.Count < max; i++)
                {
                    walked += Vector3.Distance(path.Points[i], path.Points[i - 1]);
                    if (walked < 26f) continue;
                    Vector3 p = path.Points[i], d = (path.Points[i + 1] - path.Points[i - 1]).normalized;
                    Vector3 side = new Vector3(-d.z, 0, d.x) * (left ? 1f : -1f);
                    Vector3 spot = p + side * 4.6f;
                    if (NearAny(spot, junctions, 11f) || spot.x < WestEdge(spot.z) + 4f || !GalleryArea.Area.Contains(new Vector2(spot.x, spot.z))) continue;
                    bool crowded = false;
                    foreach (var (other, _) in spots) if ((other - spot).sqrMagnitude < 16f * 16f) { crowded = true; break; }
                    if (crowded) continue;
                    spots.Add((spot, Quaternion.LookRotation(side)));   // front (−Z) faces the path
                    left = !left;
                    walked = 0f;
                }
            }
            return spots;
        }

        static bool NearAny(Vector3 p, List<Vector3> points, float distance)
        {
            foreach (var q in points) if ((new Vector2(p.x - q.x, p.z - q.z)).sqrMagnitude < distance * distance) return true;
            return false;
        }

        // ------------------------------------------------------------------ decoration

        public static void Decorate(Transform root, Rect area, List<PathLine> paths, List<Vector3> easels, Vector3 entrance)
        {
            var rng = new System.Random(Seed);
            float R() => (float)rng.NextDouble();
            var mesh = new MeshSet();

            // Path stones: flat irregular slabs, two or three across.
            var pathSamples = new List<Vector3>();
            foreach (var path in paths)
            {
                float carry = 0f;
                for (int i = 1; i < path.Points.Count; i++)
                {
                    Vector3 a = path.Points[i - 1], b = path.Points[i];
                    float len = Vector3.Distance(a, b);
                    Vector3 d = (b - a) / Mathf.Max(len, 0.001f), side = new Vector3(-d.z, 0, d.x);
                    for (float t = carry; t < len; t += 0.95f)
                    {
                        Vector3 c = a + d * t;
                        pathSamples.Add(c);
                        int across = R() < 0.35f ? 3 : 2;
                        for (int k = 0; k < across; k++)
                        {
                            float offset = (across == 3 ? (k - 1) * 0.9f : (k - 0.5f) * 1.2f) + (R() - 0.5f) * 0.35f;
                            Vector3 pos = c + side * offset + d * (R() - 0.5f) * 0.3f;
                            float radius = 0.38f + R() * 0.3f;
                            mesh.For(StoneTones[rng.Next(StoneTones.Length)]).Slab(pos + Vector3.up * Lift(pos), radius, 0.09f, 5 + rng.Next(3), R() * 360f, rng);
                        }
                        carry = 0f;
                    }
                    carry = Mathf.Max(0f, carry + 0.95f - len);
                }
            }
            // A round stone plaza where the paths cross in the middle.
            Vector3 plaza = new Vector3(400f, 0, (area.yMin + area.yMax) * 0.5f);
            for (int ring = 0; ring < 4; ring++)
            {
                int around = ring == 0 ? 1 : ring * 7;
                for (int k = 0; k < around; k++)
                {
                    float a = (k + R() * 0.4f) / around * Mathf.PI * 2f;
                    Vector3 pos = plaza + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * ring * 1.6f;
                    mesh.For(StoneTones[rng.Next(StoneTones.Length)]).Slab(pos + Vector3.up * Lift(pos), 0.7f + R() * 0.25f, 0.1f, 6, R() * 360f, rng);
                }
            }

            // Cherry trees: spread out, off the path, clear of easels and the entrance.
            var trees = new List<Vector3>();
            var keepClear = new List<Vector3>(easels) { entrance, plaza };
            for (int attempt = 0; attempt < 6000 && trees.Count < 95; attempt++)
            {
                Vector3 p = new Vector3(area.xMin + R() * area.width, 0, area.yMin + 6f + R() * (area.height - 8f));
                if (p.x < WestEdge(p.z) + 3f || p.x > area.xMax - 4f) continue;
                if (NearAny(p, keepClear, 8.5f) || NearAny(p, trees, 10f) || NearAny(p, pathSamples, PathHalfWidth + 3.8f)) continue;
                trees.Add(p);
            }
            var colliders = new GameObject("TreeTrunks").transform;
            colliders.SetParent(root, false);
            foreach (var t in trees)
            {
                Vector3 ground = t;
                Tree(mesh, ground, rng);
                var trunk = new GameObject("Trunk").AddComponent<CapsuleCollider>();
                trunk.transform.SetParent(colliders, false);
                trunk.transform.position = ground + Vector3.up * 1.4f;
                trunk.radius = 0.3f;
                trunk.height = 2.8f;
            }

            // Fallen petals, mostly under the trees but a few everywhere.
            for (int i = 0; i < 2600; i++)
            {
                Vector3 p;
                if (i < 1800 && trees.Count > 0)
                {
                    Vector3 t = trees[rng.Next(trees.Count)];
                    float a = R() * Mathf.PI * 2f, r = Mathf.Sqrt(R()) * 6f;
                    p = t + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
                }
                else p = new Vector3(area.xMin + R() * area.width, 0, area.yMin + R() * area.height);
                if (p.x < WestEdge(p.z)) continue;
                mesh.For(PetalTones[rng.Next(PetalTones.Length)]).Petal(p + Vector3.up * (Lift(p) + 0.02f + R() * 0.1f), 0.16f + R() * 0.08f, R() * 360f);
            }

            // Rocks and ferns along the path edges and around trees.
            for (int i = 0; i < 70; i++)
            {
                Vector3 c = i % 2 == 0 && pathSamples.Count > 0 ? pathSamples[rng.Next(pathSamples.Count)] : (trees.Count > 0 ? trees[rng.Next(trees.Count)] : plaza);
                float a = R() * Mathf.PI * 2f;
                Vector3 p = c + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (PathHalfWidth + 1.3f + R() * 2.5f);
                if (p.x < WestEdge(p.z) + 1f || NearAny(p, easels, 3.5f) || NearAny(p, pathSamples, PathHalfWidth + 0.6f)) continue;
                if (i % 3 == 0)
                {
                    float s = 0.5f + R() * 0.7f;
                    mesh.For(RockTones[rng.Next(RockTones.Length)]).Blob(p + Vector3.up * s * 0.25f, new Vector3(s, s * 0.6f, s * 0.9f), Quaternion.Euler(0, R() * 360f, 0));
                }
                else Fern(mesh, p, rng);
            }

            mesh.Build(root);
        }

        static void Tree(MeshSet mesh, Vector3 ground, System.Random rng)
        {
            float R() => (float)rng.NextDouble();
            Color bark = Bark[rng.Next(Bark.Length)];
            float height = 2.9f + R() * 1.0f;
            Vector3 top = ground + new Vector3((R() - 0.5f) * 0.5f, height, (R() - 0.5f) * 0.5f);
            mesh.For(bark).Rod(ground, top, 0.34f, 0.22f);

            var tips = new List<Vector3> { top + Vector3.up * 0.8f };
            int branches = 2 + rng.Next(2);
            float start = R() * 360f;
            for (int b = 0; b < branches; b++)
            {
                float a = (start + b * 360f / branches + (R() - 0.5f) * 40f) * Mathf.Deg2Rad;
                Vector3 from = ground + Vector3.up * (height * (0.6f + R() * 0.25f));
                Vector3 tip = from + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (1.6f + R() * 1.0f) + Vector3.up * (1.2f + R() * 0.8f);
                mesh.For(bark).Rod(from, tip, 0.18f, 0.1f);
                tips.Add(tip);
            }
            // Canopy: overlapping low-poly blossom clumps.
            foreach (var tip in tips)
            {
                float s = 1.8f + R() * 0.9f;
                mesh.For(Blossoms[rng.Next(Blossoms.Length)]).Blob(tip, new Vector3(s, s * 0.8f, s), Quaternion.Euler(0, R() * 360f, 0));
            }
            // Some trees hang glowing lanterns under the blossoms.
            if (R() < 0.45f)
            {
                int lanterns = 1 + rng.Next(2);
                for (int k = 0; k < lanterns; k++)
                {
                    Vector3 tip = tips[1 + rng.Next(tips.Count - 1)];
                    Vector3 hang = tip + new Vector3((R() - 0.5f) * 1.6f, -1.3f, (R() - 0.5f) * 1.6f);
                    mesh.For(new Color(0.2f, 0.2f, 0.18f)).Rod(hang + Vector3.up * 0.2f, hang + Vector3.up * 1.2f, 0.02f, 0.02f);
                    mesh.ForGlow(LanternGlow).Box(hang, new Vector3(0.24f, 0.32f, 0.24f), Quaternion.Euler(0, R() * 90f, 0));
                }
            }
        }

        static void Fern(MeshSet mesh, Vector3 p, System.Random rng)
        {
            float R() => (float)rng.NextDouble();
            Color c = FernTones[rng.Next(FernTones.Length)];
            int blades = 5 + rng.Next(3);
            for (int b = 0; b < blades; b++)
            {
                float yaw = b * 360f / blades + R() * 30f;
                Quaternion rot = Quaternion.Euler(0, yaw, 0) * Quaternion.Euler(-55f + R() * 15f, 0, 0);
                mesh.For(c).Box(p + rot * new Vector3(0, 0, 0.3f), new Vector3(0.14f, 0.02f, 0.65f), rot);
            }
        }

        static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;

        // ------------------------------------------------------------------ merged meshes

        /// <summary>One growing mesh per color; flat-shaded (every triangle has its own vertices) for the low-poly look.</summary>
        sealed class MeshSet
        {
            readonly Dictionary<(Color, bool), Part> parts = new Dictionary<(Color, bool), Part>();
            public Part For(Color c) => Get(c, false);
            public Part ForGlow(Color c) => Get(c, true);
            Part Get(Color c, bool glow)
            {
                if (!parts.TryGetValue((c, glow), out var p)) parts[(c, glow)] = p = new Part();
                return p;
            }

            public void Build(Transform root)
            {
                var holder = new GameObject("Garden").transform;
                holder.SetParent(root, false);
                foreach (var kv in parts)
                {
                    var (color, glow) = kv.Key;
                    var mesh = new Mesh { name = "Garden_" + ColorUtility.ToHtmlStringRGB(color), indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                    mesh.SetVertices(kv.Value.Vertices);
                    mesh.SetTriangles(kv.Value.Triangles, 0);
                    mesh.RecalculateNormals();
                    mesh.RecalculateBounds();
                    var go = new GameObject(mesh.name);
                    go.transform.SetParent(holder, false);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var r = go.AddComponent<MeshRenderer>();
                    r.sharedMaterial = glow ? Mat(color, color * 2.2f) : Mat(color);
                    if (glow) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }
        }

        sealed class Part
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<int> Triangles = new List<int>();

            void Tri(Vector3 a, Vector3 b, Vector3 c)
            {
                int i = Vertices.Count;
                Vertices.Add(a); Vertices.Add(b); Vertices.Add(c);
                Triangles.Add(i); Triangles.Add(i + 1); Triangles.Add(i + 2);
            }

            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d) { Tri(a, b, c); Tri(a, c, d); }

            /// <summary>Flat irregular n-sided stone lying on the ground.</summary>
            public void Slab(Vector3 center, float radius, float height, int sides, float yaw, System.Random rng)
            {
                var top = new Vector3[sides];
                var bottom = new Vector3[sides];
                for (int i = 0; i < sides; i++)
                {
                    float a = (yaw + i * 360f / sides) * Mathf.Deg2Rad;
                    float r = radius * (0.75f + (float)rng.NextDouble() * 0.35f);
                    Vector3 o = new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
                    top[i] = center + o + Vector3.up * (height + 0.02f);
                    bottom[i] = center + o * 1.08f + Vector3.up * 0.01f;
                }
                Vector3 mid = center + Vector3.up * (height + 0.03f);
                for (int i = 0; i < sides; i++)
                {
                    int n = (i + 1) % sides;
                    Tri(mid, top[n], top[i]);
                    Quad(top[i], top[n], bottom[n], bottom[i]);
                }
            }

            /// <summary>A tiny flat petal (diamond) lying on the ground.</summary>
            public void Petal(Vector3 p, float size, float yaw)
            {
                Quaternion q = Quaternion.Euler(0, yaw, 0);
                Vector3 f = q * Vector3.forward * size, s = q * Vector3.right * size * 0.55f;
                Quad(p - f, p - s, p + f, p + s);
                Quad(p - f, p + s, p + f, p - s);   // both faces, it's only a few triangles
            }

            public void Box(Vector3 center, Vector3 size, Quaternion rot)
            {
                Vector3 h = size * 0.5f;
                Vector3 V(float x, float y, float z) => center + rot * new Vector3(x * h.x, y * h.y, z * h.z);
                Vector3 a = V(-1, -1, -1), b = V(1, -1, -1), c = V(1, 1, -1), d = V(-1, 1, -1);
                Vector3 e = V(-1, -1, 1), f = V(1, -1, 1), g = V(1, 1, 1), hh = V(-1, 1, 1);
                Quad(a, d, c, b); Quad(f, g, hh, e); Quad(e, hh, d, a); Quad(b, c, g, f); Quad(d, hh, g, c); Quad(e, a, b, f);
            }

            /// <summary>Tapered six-sided branch/trunk from a to b.</summary>
            public void Rod(Vector3 a, Vector3 b, float radiusA, float radiusB)
            {
                const int sides = 6;
                Vector3 axis = (b - a).normalized;
                Vector3 u = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized, v = Vector3.Cross(axis, u);
                for (int i = 0; i < sides; i++)
                {
                    float t0 = i * Mathf.PI * 2f / sides, t1 = (i + 1) * Mathf.PI * 2f / sides;
                    Vector3 o0 = u * Mathf.Cos(t0) + v * Mathf.Sin(t0), o1 = u * Mathf.Cos(t1) + v * Mathf.Sin(t1);
                    Quad(a + o0 * radiusA, b + o0 * radiusB, b + o1 * radiusB, a + o1 * radiusA);
                    Tri(b, b + o1 * radiusB, b + o0 * radiusB);
                }
            }

            /// <summary>Faceted low-poly blob (icosahedron split once), for blossom clumps and rocks.</summary>
            public void Blob(Vector3 center, Vector3 scale, Quaternion rot)
            {
                foreach (var (p, q, r) in IcoFaces)
                    Tri(center + rot * Vector3.Scale(p, scale), center + rot * Vector3.Scale(r, scale), center + rot * Vector3.Scale(q, scale));
            }

            static List<(Vector3, Vector3, Vector3)> icoFaces;
            static List<(Vector3, Vector3, Vector3)> IcoFaces
            {
                get
                {
                    if (icoFaces != null) return icoFaces;
                    float t = (1f + Mathf.Sqrt(5f)) / 2f;
                    var v = new[]
                    {
                        new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                        new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                        new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
                    };
                    for (int i = 0; i < v.Length; i++) v[i] = v[i].normalized;
                    int[] f =
                    {
                        0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                        3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
                    };
                    icoFaces = new List<(Vector3, Vector3, Vector3)>();
                    for (int i = 0; i < f.Length; i += 3)
                    {
                        Vector3 a = v[f[i]], b = v[f[i + 1]], c = v[f[i + 2]];
                        Vector3 ab = (a + b).normalized, bc = (b + c).normalized, ca = (c + a).normalized;
                        icoFaces.Add((a, ab, ca)); icoFaces.Add((b, bc, ab)); icoFaces.Add((c, ca, bc)); icoFaces.Add((ab, bc, ca));
                    }
                    return icoFaces;
                }
            }
        }
    }
}
