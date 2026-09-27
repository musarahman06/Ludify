using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.AI.Navigation;
using UnityEngine;
using static Ludify.Import.ModelKit;

namespace Ludify.Gallery
{
    /// <summary>
    /// Turns the north-east city blocks into an outdoor gallery at runtime (no scene edits):
    /// the buildings there are switched off, each lot becomes a lawn, every other lot gets a
    /// pedestal, and an entrance sign marks the south side. Roads stay as walking paths.
    /// </summary>
    public static class GalleryArea
    {
        /// <summary>World XZ area of the gallery (the blocks circled on the map).</summary>
        public static readonly Rect Area = new Rect(290f, 297f, 210f, 203f);

        /// <summary>Building groups whose buildings inside <see cref="Area"/> are replaced.</summary>
        static readonly string[] BuildingGroups = { "InnerOutskirtsBuildings", "DowntownBuildings", "CityOutskirtsBuildings" };

        const float LawnSize = 20f;
        static readonly Color Lawn = new Color(0.36f, 0.62f, 0.3f);
        static readonly Color SignColor = new Color(0.45f, 0.25f, 0.6f);

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
            var layout = new Layout();
            if (lots.Count == 0)
            {
                Debug.LogWarning("[Gallery] No buildings found in the gallery area; nothing to replace.");
                return layout;
            }

            // Lots sit on a grid; index them so we can place a pedestal on every other one (checkerboard).
            float[] xs = Cluster(lots.Select(l => l.x)), zs = Cluster(lots.Select(l => l.z));
            Vector3 entranceLot = lots.OrderBy(l => l.z).ThenBy(l => Mathf.Abs(l.x - Area.center.x)).First();

            foreach (Vector3 lot in lots)
            {
                Vector3 ground = Ground(lot);
                Prim(PrimitiveType.Cube, root, ground + Vector3.up * 0.03f, new Vector3(LawnSize, 0.06f, LawnSize), Lawn, name: "Lawn");

                if (Near(lot, entranceLot)) continue;
                int ix = Nearest(xs, lot.x), iz = Nearest(zs, lot.z);
                if ((ix + iz) % 2 != 0) continue;
                // Pedestals face south, toward the entrance and the rest of the city.
                layout.Pedestals.Add(Pedestal.Create(root, ground + Vector3.up * 0.06f, Quaternion.identity));
            }

            BuildEntranceSign(root, Ground(entranceLot), out layout.Entrance, out layout.EntranceFacing);
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

        /// <summary>Distinct grid lines (values within 6 m merge).</summary>
        static float[] Cluster(IEnumerable<float> values)
        {
            var lines = new List<float>();
            foreach (float v in values.OrderBy(v => v))
                if (lines.Count == 0 || v - lines[lines.Count - 1] > 6f) lines.Add(v);
            return lines.ToArray();
        }

        static int Nearest(float[] lines, float v)
        {
            int best = 0;
            for (int i = 1; i < lines.Length; i++)
                if (Mathf.Abs(lines[i] - v) < Mathf.Abs(lines[best] - v)) best = i;
            return best;
        }

        static bool Near(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < 1f;
    }
}
