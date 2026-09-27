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
                var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _base = probe.GetComponent<Renderer>().sharedMaterial;
                Kill(probe);
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
