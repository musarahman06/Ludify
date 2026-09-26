using System.Collections.Generic;
using Ludify.Gameplay.World;
using UnityEditor;
using UnityEngine;

namespace Ludify.Gameplay.Editor
{
    /// <summary>
    /// Re-lays the city's buildings into the blocks between roads. The original buildings were placed on a
    /// point grid before the road network existed, so most of them sat on a street. This tool clears each
    /// building group and packs new Kenney buildings into the free space: off the roads (plus a sidewalk
    /// margin), away from the river banks, and outside reserved plots (the future Gallery Theatre).
    /// Deterministic (fixed seed) so re-running gives the same city.
    /// </summary>
    public static class CityLayoutTool
    {
        const float SidewalkMargin = 1.5f;
        const float RiverClearance = 30f;
        const float MapEdgeInset = 3f;

        /// <summary>Downtown plot kept empty for the Gallery Theatre (milestone 8), next to the B2 bridge landing.</summary>
        public static readonly Rect GalleryPlot = Rect.MinMaxRect(338f, 246f, 382f, 290f);

        class ZoneFill
        {
            public string Group;
            public Rect Area;
            public string[] Prefabs;
            public string[] SkyscraperPrefabs;
            public float SkyscraperChance;
            public string[] Materials;
            public float Spacing;
            public float Step;
            public int MaxCount;
            public int Seed;
        }

        [MenuItem("Ludify/World/0. Re-layout City Buildings")]
        public static void Run()
        {
            const string commercial = "Assets/Kenney/CityCommercial/Models/";
            const string suburban = "Assets/Kenney/CitySuburban/Models/";
            var lowRise = new List<string>();
            for (char c = 'a'; c <= 'n'; c++) lowRise.Add(commercial + "building-" + c + ".fbx");
            var towers = new List<string>();
            for (char c = 'a'; c <= 'e'; c++) towers.Add(commercial + "building-skyscraper-" + c + ".fbx");
            var houses = new List<string>();
            for (char c = 'a'; c <= 'u'; c++) houses.Add(suburban + "building-type-" + c + ".fbx");
            string[] commercialMats = { "Assets/Kenney/CityCommercial/Mat_VariationA.mat", "Assets/Kenney/CityCommercial/Mat_VariationB.mat" };
            string[] suburbanMats = { "Assets/Kenney/CitySuburban/Mat_VariationA.mat", "Assets/Kenney/CitySuburban/Mat_VariationB.mat", "Assets/Kenney/CitySuburban/Mat_VariationC.mat" };

            var fills = new[]
            {
                new ZoneFill { Group = "DowntownBuildings", Area = Rect.MinMaxRect(300, 175, 500, 300), Prefabs = lowRise.ToArray(),
                    SkyscraperPrefabs = towers.ToArray(), SkyscraperChance = 0.45f, Materials = commercialMats, Spacing = 1.5f, Step = 1f, MaxCount = 400, Seed = 11 },
                new ZoneFill { Group = "InnerOutskirtsBuildings", Area = Rect.MinMaxRect(300, 300, 500, 485), Prefabs = lowRise.ToArray(),
                    Materials = commercialMats, Spacing = 5f, Step = 1.5f, MaxCount = 400, Seed = 12 },
                new ZoneFill { Group = "CityOutskirtsBuildings", Area = Rect.MinMaxRect(300, 0, 500, 175), Prefabs = lowRise.ToArray(),
                    Materials = commercialMats, Spacing = 8f, Step = 1.5f, MaxCount = 400, Seed = 13 },
                new ZoneFill { Group = "SuburbHouses", Area = Rect.MinMaxRect(10, 300, 225, 485), Prefabs = houses.ToArray(),
                    Materials = suburbanMats, Spacing = 6f, Step = 1.5f, MaxCount = 400, Seed = 14 },
            };

            var blocked = CollectRoadRects();
            var report = new List<string>();
            foreach (var fill in fills) report.Add($"{fill.Group}: {Populate(fill, blocked)}");

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("[CityLayoutTool] " + string.Join(", ", report));
        }

        static List<Rect> CollectRoadRects()
        {
            var rects = new List<Rect>();
            foreach (var groupName in new[] { "RoadNetwork", "RoadConnectors" })
            {
                var group = GameObject.Find(groupName);
                if (group == null) continue;
                foreach (var r in group.GetComponentsInChildren<Renderer>()) rects.Add(Expand(XZ(r.bounds), SidewalkMargin));
            }
            foreach (var bridge in WorldLayout.Bridges)
            {
                var deck = GameObject.Find(bridge.DeckObjectName);
                if (deck == null) continue;
                foreach (var r in deck.GetComponentsInChildren<Renderer>()) rects.Add(Expand(XZ(r.bounds), SidewalkMargin));
            }
            rects.Add(GalleryPlot);
            return rects;
        }

        static int Populate(ZoneFill fill, List<Rect> blocked)
        {
            var group = GameObject.Find(fill.Group) ?? new GameObject(fill.Group);
            for (int i = group.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(group.transform.GetChild(i).gameObject);

            var rng = new System.Random(fill.Seed);
            var footprintCache = new Dictionary<string, Rect>();
            var placed = new List<Rect>();

            var area = Rect.MinMaxRect(
                Mathf.Max(fill.Area.xMin, MapEdgeInset), Mathf.Max(fill.Area.yMin, MapEdgeInset),
                Mathf.Min(fill.Area.xMax, WorldLayout.MapSize - MapEdgeInset), Mathf.Min(fill.Area.yMax, WorldLayout.MapSize - MapEdgeInset));

            // Scanline packing: walk the zone row by row so each block fills from its corner outward,
            // trying a few different buildings at every spot. Spacing (not the cap) sets the density.
            for (float z = area.yMin; z <= area.yMax && placed.Count < fill.MaxCount; z += fill.Step)
            for (float x = area.xMin; x <= area.xMax && placed.Count < fill.MaxCount; x += fill.Step)
            {
                var c = new Vector2(x, z);
                string path = null; int quarter = 0; Rect foot = default; bool fits = false;
                for (int attempt = 0; attempt < 4 && !fits; attempt++)
                {
                    bool tower = fill.SkyscraperPrefabs != null && rng.NextDouble() < fill.SkyscraperChance;
                    var pool = tower ? fill.SkyscraperPrefabs : fill.Prefabs;
                    path = pool[rng.Next(pool.Length)];
                    quarter = rng.Next(4);
                    if (!footprintCache.TryGetValue(path, out var local)) footprintCache[path] = local = LocalFootprint(path);
                    foot = RotatedFootprint(local, quarter, c);
                    fits = Contains(area, foot) && !NearRiver(foot) && !Overlaps(blocked, foot)
                        && !Overlaps(placed, Expand(foot, fill.Spacing * 0.5f));
                }
                if (!fits) continue;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group.transform);
                go.transform.SetPositionAndRotation(new Vector3(c.x, 0f, c.y), Quaternion.Euler(0f, quarter * 90f, 0f));
                var mat = AssetDatabase.LoadAssetAtPath<Material>(fill.Materials[rng.Next(fill.Materials.Length)]);
                foreach (var r in go.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = mat;
                go.isStatic = true;
                placed.Add(Expand(foot, fill.Spacing * 0.5f));
            }
            return placed.Count;
        }

        /// <summary>XZ footprint of a prefab at the origin with no rotation.</summary>
        static Rect LocalFootprint(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var inst = Object.Instantiate(prefab);
            inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var rends = inst.GetComponentsInChildren<Renderer>();
            var b = rends[0].bounds;
            foreach (var r in rends) b.Encapsulate(r.bounds);
            Object.DestroyImmediate(inst);
            return XZ(b);
        }

        static Rect RotatedFootprint(Rect local, int quarter, Vector2 pos)
        {
            // Rotate the rect's corners by quarter*90° around the pivot, then translate.
            var corners = new[] { new Vector2(local.xMin, local.yMin), new Vector2(local.xMax, local.yMin), new Vector2(local.xMin, local.yMax), new Vector2(local.xMax, local.yMax) };
            float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
            foreach (var p in corners)
            {
                // Unity yaw rotates (x, z) -> (x cos + z sin, -x sin + z cos)
                float ang = quarter * 90f * Mathf.Deg2Rad;
                float rx = p.x * Mathf.Cos(ang) + p.y * Mathf.Sin(ang);
                float rz = -p.x * Mathf.Sin(ang) + p.y * Mathf.Cos(ang);
                minX = Mathf.Min(minX, rx); maxX = Mathf.Max(maxX, rx);
                minZ = Mathf.Min(minZ, rz); maxZ = Mathf.Max(maxZ, rz);
            }
            return Rect.MinMaxRect(minX + pos.x, minZ + pos.y, maxX + pos.x, maxZ + pos.y);
        }

        static bool NearRiver(Rect foot)
        {
            foreach (var p in new[] { new Vector2(foot.xMin, foot.yMin), new Vector2(foot.xMax, foot.yMin), new Vector2(foot.xMin, foot.yMax), new Vector2(foot.xMax, foot.yMax), foot.center })
                if (Mathf.Abs(p.x - WorldLayout.RiverCenterX(p.y)) < RiverClearance) return true;
            return false;
        }

        static bool Overlaps(List<Rect> rects, Rect r)
        {
            foreach (var o in rects) if (o.Overlaps(r)) return true;
            return false;
        }

        static bool Contains(Rect outer, Rect inner) =>
            inner.xMin >= outer.xMin && inner.xMax <= outer.xMax && inner.yMin >= outer.yMin && inner.yMax <= outer.yMax;

        static Rect XZ(Bounds b) => Rect.MinMaxRect(b.min.x, b.min.z, b.max.x, b.max.z);
        static Rect Expand(Rect r, float m) => Rect.MinMaxRect(r.xMin - m, r.yMin - m, r.xMax + m, r.yMax + m);
    }
}
