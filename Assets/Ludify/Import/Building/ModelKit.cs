using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Ludify.Import
{
    /// <summary>
    /// Low-level helpers for building exhibits from Unity primitives: cached materials (based on the
    /// render pipeline's default material, so no shader lookups that could fail in builds),
    /// collider-free primitives, a cone mesh and camera-facing labels.
    /// </summary>
    public static class ModelKit
    {
        static Material _base;
        static Mesh _cone;
        static readonly Dictionary<(Color, Color), Material> Materials = new Dictionary<(Color, Color), Material>();

        public static Material Mat(Color color, Color? emission = null)
        {
            Color glow = emission ?? Color.black;
            if (Materials.TryGetValue((color, glow), out Material cached) && cached != null) return cached;

            if (_base == null)
            {
                // A saved material keeps the emission shader variant in builds (unused variants get stripped).
                _base = Resources.Load<Material>("LudifyExhibitOpaque");
                if (_base == null)
                {
                    var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    _base = probe.GetComponent<Renderer>().sharedMaterial;
                    Kill(probe);
                }
            }
            var m = new Material(_base) { name = "Exhibit " + ColorUtility.ToHtmlStringRGB(color), color = color };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (glow != Color.black)
            {
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                m.SetColor("_EmissionColor", glow);
            }
            Materials[(color, glow)] = m;
            return m;
        }

        static Material _transparentBase;
        static readonly Dictionary<Color, Material> Transparents = new Dictionary<Color, Material>();

        /// <summary>See-through material (e.g. a cell membrane) visible from inside and outside. Alpha comes from the colour.</summary>
        public static Material Glass(Color color)
        {
            if (Transparents.TryGetValue(color, out Material cached) && cached != null) return cached;
            if (_transparentBase == null) _transparentBase = Resources.Load<Material>("LudifyExhibitTransparent");
            Material m = _transparentBase != null ? new Material(_transparentBase) : new Material(Mat(color));
            m.name = "Exhibit glass " + ColorUtility.ToHtmlStringRGBA(color);
            m.color = color;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            Transparents[color] = m;
            return m;
        }

        /// <summary>A mesh object without a collider.</summary>
        public static Transform MeshObject(string name, Mesh mesh, Transform parent, Vector3 position, Quaternion rotation, Vector3 scale, Material material)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            Transform t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = position;
            t.localRotation = rotation;
            t.localScale = scale;
            return t;
        }

        static Mesh _pyramid, _prism;
        static readonly Dictionary<int, Mesh> Rings = new Dictionary<int, Mesh>();

        /// <summary>Square pyramid, base 1×1 at y = −0.5, apex at y = +0.5.</summary>
        public static Mesh PyramidMesh => _pyramid ?? (_pyramid = Faceted("Pyramid",
            new[] { new Vector3(-.5f, -.5f, -.5f), new Vector3(.5f, -.5f, -.5f), new Vector3(.5f, -.5f, .5f), new Vector3(-.5f, -.5f, .5f), new Vector3(0, .5f, 0) },
            new[] { 0, 4, 1, 1, 4, 2, 2, 4, 3, 3, 4, 0, 0, 1, 2, 0, 2, 3 }));

        /// <summary>Triangular prism along Z (length 1), triangle base 1 wide, 1 tall.</summary>
        public static Mesh PrismMesh => _prism ?? (_prism = Faceted("Prism",
            new[] { new Vector3(-.5f, -.5f, -.5f), new Vector3(.5f, -.5f, -.5f), new Vector3(0, .5f, -.5f),
                    new Vector3(-.5f, -.5f, .5f), new Vector3(.5f, -.5f, .5f), new Vector3(0, .5f, .5f) },
            new[] { 0, 2, 1, 3, 4, 5, 0, 1, 4, 0, 4, 3, 1, 2, 5, 1, 5, 4, 2, 0, 3, 2, 3, 5 }));

        /// <summary>Flat ring (annulus) in the XZ plane, outer radius 1, visible from both sides.</summary>
        public static Mesh RingMesh(float innerRatio)
        {
            int key = Mathf.RoundToInt(innerRatio * 1000);
            if (Rings.TryGetValue(key, out Mesh cached) && cached != null) return cached;
            const int segments = 96;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments;
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                verts.Add(dir * innerRatio);
                verts.Add(dir);
            }
            for (int i = 0; i < segments; i++)
            {
                int v = i * 2;
                tris.AddRange(new[] { v, v + 1, v + 3, v, v + 3, v + 2 });   // top
                tris.AddRange(new[] { v, v + 3, v + 1, v, v + 2, v + 3 });   // bottom
            }
            var mesh = new Mesh { name = "Ring" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            return Rings[key] = mesh;
        }

        /// <summary>Flat-shaded mesh from shared corner positions (each triangle gets its own vertices).</summary>
        static Mesh Faceted(string name, Vector3[] corners, int[] triangles)
        {
            // Convex shapes: make every face point away from the centre, whatever order it was listed in.
            Vector3 centre = Vector3.zero;
            foreach (Vector3 c in corners) centre += c;
            centre /= corners.Length;
            var verts = new Vector3[triangles.Length];
            var tris = new int[triangles.Length];
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = corners[triangles[i]], b = corners[triangles[i + 1]], c = corners[triangles[i + 2]];
                bool outward = Vector3.Dot(Vector3.Cross(b - a, c - a), (a + b + c) / 3 - centre) < 0;   // Unity: clockwise = front
                verts[i] = a; verts[i + 1] = outward ? b : c; verts[i + 2] = outward ? c : b;
                tris[i] = i; tris[i + 1] = i + 1; tris[i + 2] = i + 2;
            }
            var mesh = new Mesh { name = name, vertices = verts, triangles = tris };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A primitive without a collider, positioned in its parent's local space.</summary>
        public static Transform Prim(PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Color color,
                                     Quaternion? rotation = null, Color? emission = null, string name = null)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            Kill(go.GetComponent<Collider>());
            go.name = name ?? type.ToString();
            Transform t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = position;
            t.localRotation = rotation ?? Quaternion.identity;
            t.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = Mat(color, emission);
            return t;
        }

        /// <summary>Cylinder whose axis runs from a to b (local space of parent).</summary>
        public static Transform Rod(Transform parent, Vector3 a, Vector3 b, float diameter, Color color, Color? emission = null)
        {
            Vector3 d = b - a;
            float length = d.magnitude;
            if (length < 1e-4f) return null;
            return Prim(PrimitiveType.Cylinder, parent, (a + b) / 2, new Vector3(diameter, length / 2, diameter), color,
                        Quaternion.FromToRotation(Vector3.up, d / length), emission, "Rod");
        }

        public static Transform Cone(Transform parent, Vector3 position, Quaternion rotation, Vector3 scale, Color color)
        {
            var go = new GameObject("Cone", typeof(MeshFilter), typeof(MeshRenderer));
            go.GetComponent<MeshFilter>().sharedMesh = ConeMesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = Mat(color);
            Transform t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = position;
            t.localRotation = rotation;
            t.localScale = scale;
            return t;
        }

        /// <summary>Unit cone: base radius 0.5 at y = -0.5, tip at y = +0.5.</summary>
        static Mesh ConeMesh
        {
            get
            {
                if (_cone != null) return _cone;
                const int sides = 24;
                var verts = new List<Vector3>();
                var tris = new List<int>();
                for (int i = 0; i < sides; i++)
                {
                    float a0 = i * Mathf.PI * 2 / sides, a1 = (i + 1) * Mathf.PI * 2 / sides;
                    Vector3 p0 = new Vector3(Mathf.Cos(a0) * 0.5f, -0.5f, Mathf.Sin(a0) * 0.5f);
                    Vector3 p1 = new Vector3(Mathf.Cos(a1) * 0.5f, -0.5f, Mathf.Sin(a1) * 0.5f);
                    int v = verts.Count;
                    verts.Add(p0); verts.Add(new Vector3(0, 0.5f, 0)); verts.Add(p1);        // side
                    tris.Add(v); tris.Add(v + 1); tris.Add(v + 2);
                    verts.Add(p1); verts.Add(new Vector3(0, -0.5f, 0)); verts.Add(p0);      // base
                    tris.Add(v + 3); tris.Add(v + 4); tris.Add(v + 5);
                }
                _cone = new Mesh { name = "Cone" };
                _cone.SetVertices(verts);
                _cone.SetTriangles(tris, 0);
                _cone.RecalculateNormals();
                _cone.RecalculateBounds();
                return _cone;
            }
        }

        /// <summary>Floating text that always faces the camera. Height in parent-local units.</summary>
        public static TextMeshPro Label(Transform parent, string text, Vector3 position, float height, Color? color = null)
        {
            var go = new GameObject("Label: " + text);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = 10;                                  // 10 pt ≈ 1 unit of line height at scale 1
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color ?? Color.white;
            tmp.outlineWidth = 0.25f;
            tmp.outlineColor = new Color32(0, 0, 0, 255);
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.rectTransform.sizeDelta = new Vector2(20, 2);
            go.transform.localScale = Vector3.one * height;
            go.AddComponent<Billboard>();
            return tmp;
        }

        /// <summary>Destroy that also works outside Play mode (editor tools, tests).</summary>
        public static void Kill(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o);
        }

        public static Color ParseColor(string value, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(value)) return fallback;
            string v = value.Trim().ToLowerInvariant().Replace(" ", "");
            if (v == "copper") return new Color(0.85f, 0.45f, 0.2f);
            if (v == "gold") return new Color(1f, 0.8f, 0.2f);
            if (v == "brown") return new Color(0.45f, 0.28f, 0.15f);
            if (v == "orange") return new Color(1f, 0.55f, 0.1f);
            if (v == "purple" || v == "violet") return new Color(0.55f, 0.3f, 0.8f);
            if (v == "pink") return new Color(1f, 0.55f, 0.7f);
            return ColorUtility.TryParseHtmlString(v, out Color c) ? c : fallback;
        }
    }

    /// <summary>Keeps a label turned toward the main camera.</summary>
    public sealed class Billboard : MonoBehaviour
    {
        void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam != null) transform.rotation = cam.transform.rotation;
        }
    }
}
