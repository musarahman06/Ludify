using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.AI.Navigation;
using UnityEngine;
using static Ludify.Import.ModelKit;

namespace Ludify.Gallery
{
    /// <summary>
    /// Turns the north-east city blocks into an outdoor cherry-blossom gallery at runtime (no scene edits):
    /// the buildings and roads there are switched off, the ground is painted pink, a stone path winds from the
    /// entrance sign past every exhibit easel, and cherry trees with lanterns fill the rest (<see cref="GalleryGarden"/>).
    /// </summary>
    public static class GalleryArea
    {
        /// <summary>World XZ area of the gallery (the blocks circled on the map).</summary>
        public static readonly Rect Area = new Rect(290f, 297f, 210f, 203f);

        /// <summary>Building groups whose buildings inside <see cref="Area"/> are replaced.</summary>
        static readonly string[] BuildingGroups = { "InnerOutskirtsBuildings", "DowntownBuildings", "CityOutskirtsBuildings" };

        /// <summary>Road groups whose pieces inside <see cref="Area"/> are replaced by the stone path.</summary>
        static readonly string[] RoadGroups = { "Roads_InnerOutskirts", "Roads_Downtown", "Roads_CityOutskirts", "RoadConnectors", "RoadProps" };

        const int MaxExhibits = 16;
        static readonly Color SignColor = new Color(0.5f, 0.33f, 0.22f);        // wood, like the easels
        static readonly Color SignBlossom = new Color(0.96f, 0.62f, 0.75f);

        public sealed class Layout
        {
            public readonly List<Pedestal> Pedestals = new List<Pedestal>();
            public Vector3 Entrance;
            public Quaternion EntranceFacing;
        }

        public static bool Contains(Vector3 p) => Area.Contains(new Vector2(p.x, p.z));

        public static Layout Build(Transform root)
        {
            List<Vector3> lots = HideBuildings();
            HideRoads(root);
            var layout = new Layout();
            if (lots.Count == 0)
            {
                Debug.LogWarning("[Gallery] No buildings found in the gallery area; nothing to replace.");
                return layout;
            }

            GalleryGarden.PaintGround(Area);

            // Entrance on the south edge, in the middle; the stone path runs north from it through the whole gallery.
            Vector3 gate = Ground(new Vector3(400f, 0f, Area.yMin + 5f));
            var paths = GalleryGarden.PathNetwork(Area, gate);
            var easels = new List<Vector3>();
            foreach (var (position, rotation) in GalleryGarden.EaselSpots(paths, gate, MaxExhibits))
            {
                layout.Pedestals.Add(Pedestal.Create(root, Ground(position), rotation));
                easels.Add(position);
            }
            GalleryGarden.Decorate(root, Area, paths, easels, gate);

            BuildEntranceSign(root, gate, out layout.Entrance, out layout.EntranceFacing);
            return layout;
        }

        /// <summary>Switches off buildings inside the area. Returns their lot centres.</summary>
        static List<Vector3> HideBuildings()
        {
            var lots = new List<Vector3>();
            foreach (string groupName in BuildingGroups)
            {
                GameObject group = GameObject.Find(groupName);
                if (group == null) continue;
                foreach (Transform building in group.transform)
                {
                    Renderer[] renderers = building.GetComponentsInChildren<Renderer>(true);
                    if (renderers.Length == 0) continue;
                    Bounds b = renderers[0].bounds;
                    foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
                    if (!Contains(b.center)) continue;
                    building.gameObject.SetActive(false);
                    lots.Add(new Vector3(b.center.x, 0, b.center.z));
                }
            }
            return lots;
        }

        /// <summary>Switches off the asphalt roads (and their props) inside the area; the stone path replaces them.</summary>
        static void HideRoads(Transform root)
        {
            foreach (string groupName in RoadGroups)
            {
                GameObject group = GameObject.Find(groupName);
                if (group == null) continue;
                foreach (Transform piece in group.transform)
                {
                    Renderer[] renderers = piece.GetComponentsInChildren<Renderer>(true);
                    if (renderers.Length == 0) continue;
                    Bounds b = renderers[0].bounds;
                    foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
                    if (Contains(b.center)) piece.gameObject.SetActive(false);
                    else if (b.max.x > Area.xMin && b.min.x < Area.xMax && b.max.z > Area.yMin && b.min.z < Area.yMax)
                    {
                        // Runs on outside the gallery too (e.g. the long east spine): just cover the part inside.
                        float x0 = Mathf.Max(b.min.x, Area.xMin), x1 = Mathf.Min(b.max.x, Area.xMax);
                        float z0 = Mathf.Max(b.min.z, Area.yMin), z1 = Mathf.Min(b.max.z, Area.yMax);
                        if (x1 - x0 > 5f && z1 - z0 > 5f) GalleryGarden.CoverRoad(root, Rect.MinMaxRect(x0, z0, x1, z1), b.max.y);
                    }
                }
            }
        }

        /// <summary>
        /// City Life may have set up before the gallery (start-up order isn't guaranteed): drop the
        /// hidden buildings from its hint list and rebuild the NPC NavMesh so residents walk through
        /// the gallery instead of around invisible buildings.
        /// </summary>
        public static void FixUpCityLife()
        {
            CityColorizer.Buildings.RemoveAll(b => Contains(b.Bounds.center));
            // CityNav blocks out every building's volume; drop the blockers for buildings we removed.
            GameObject blockers = GameObject.Find("_CityLife/BuildingBlockers");
            if (blockers != null)
                foreach (Transform blocker in blockers.transform.Cast<Transform>().ToList())
                    if (Contains(blocker.position)) Object.DestroyImmediate(blocker.gameObject);
            GameObject cityLife = GameObject.Find("_CityLife");
            NavMeshSurface surface = cityLife != null ? cityLife.GetComponent<NavMeshSurface>() : null;
            if (surface != null) surface.BuildNavMesh();
        }

        static void BuildEntranceSign(Transform root, Vector3 ground, out Vector3 entrance, out Quaternion facing)
        {
            var sign = new GameObject("GallerySign").transform;
            sign.SetParent(root, false);
            sign.position = ground;
            Prim(PrimitiveType.Cube, sign, new Vector3(-3.2f, 2.2f, 0), new Vector3(0.4f, 4.4f, 0.4f), SignColor);
            Prim(PrimitiveType.Cube, sign, new Vector3(3.2f, 2.2f, 0), new Vector3(0.4f, 4.4f, 0.4f), SignColor);
            Prim(PrimitiveType.Cube, sign, new Vector3(0, 4.1f, 0), new Vector3(7.2f, 1.4f, 0.3f), SignColor);
            foreach (float side in new[] { -1f, 1f })
            {
                var text = new GameObject("SignText").AddComponent<TextMeshPro>();
                text.transform.SetParent(sign, false);
                text.transform.localPosition = new Vector3(0, 4.1f, 0.17f * side);
                text.transform.localRotation = Quaternion.Euler(0, side > 0 ? 180 : 0, 0);
                text.text = "<b>LUDIFY ART GALLERY</b>\n<size=55%>Turn any image into a 3D exhibit</size>";
                text.fontSize = 5;
                text.alignment = TextAlignmentOptions.Center;
                text.rectTransform.sizeDelta = new Vector2(7f, 1.3f);
                text.color = Color.white;
            }
            foreach (var (x, y, s) in new[] { (-3.4f, 4.9f, 1.1f), (3.4f, 4.9f, 1.1f), (-1.6f, 5.0f, 0.8f), (1.9f, 5.05f, 0.9f) })
                Prim(PrimitiveType.Sphere, sign, new Vector3(x, y, 0), new Vector3(s * 1.4f, s, s * 1.2f), SignBlossom, name: "Blossom");
            // Only the posts are solid, so people can walk through the gate.
            foreach (float x in new[] { -3.2f, 3.2f })
            {
                var post = sign.gameObject.AddComponent<BoxCollider>();
                post.center = new Vector3(x, 2.2f, 0);
                post.size = new Vector3(0.4f, 4.4f, 0.4f);
            }

            entrance = ground + new Vector3(0, 0, -4f);   // just south of the sign
            facing = Quaternion.identity;                  // looking north into the gallery
        }

        static Vector3 Ground(Vector3 p)
        {
            var origin = new Vector3(p.x, 100f, p.z);
            foreach (RaycastHit hit in Physics.RaycastAll(origin, Vector3.down, 200f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
                if (hit.collider.GetComponentInParent<Pedestal>() == null) return hit.point;
            Terrain t = Terrain.activeTerrain;
            return t != null ? new Vector3(p.x, t.SampleHeight(p) + t.transform.position.y, p.z) : p;
        }
    }
}
