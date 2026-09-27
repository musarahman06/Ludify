using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;

namespace Ludify.Import
{
    /// <summary>
    /// One part of an exhibit: its data (name, value, Gemini's one-line info), a trigger collider for
    /// hovering/clicking in the viewer (off otherwise, so it never gets in the player's way), a
    /// highlight, and its offset for the exploded view.
    /// </summary>
    public sealed class ExhibitPart : MonoBehaviour
    {
        public ScenePart Data;
        public Vector3 BasePosition;
        /// <summary>Where the part moves (content space, added to BasePosition) when exploded.</summary>
        public Vector3 ExplodeOffset;

        BoxCollider _collider;
        Renderer[] _renderers;
        Material[][] _original;
        bool _highlighted;

        public static ExhibitPart Attach(Transform root, ScenePart data)
        {
            var part = root.gameObject.AddComponent<ExhibitPart>();
            part.Data = data;
            part.BasePosition = root.localPosition;
            return part;
        }

        /// <summary>Sizes the hover collider to the part's visible geometry. Call after the exhibit is scaled.</summary>
        public void FitCollider()
        {
            _renderers = GetComponentsInChildren<Renderer>().Where(r => r.GetComponent<TMP_Text>() == null && r.name != "Electron").ToArray();
            if (_renderers.Length == 0) return;
            Bounds world = _renderers[0].bounds;
            foreach (Renderer r in _renderers) world.Encapsulate(r.bounds);
            _collider = gameObject.AddComponent<BoxCollider>();
            _collider.isTrigger = true;
            _collider.center = transform.InverseTransformPoint(world.center);
            Vector3 s = transform.lossyScale;
            _collider.size = new Vector3(world.size.x / Mathf.Max(s.x, 1e-4f), world.size.y / Mathf.Max(s.y, 1e-4f), world.size.z / Mathf.Max(s.z, 1e-4f)) * 1.1f;
            _collider.enabled = false;
        }

        public void SetInteractive(bool on)
        {
            if (_collider != null) _collider.enabled = on;
            if (!on) SetHighlight(false);
        }

        public void SetHighlight(bool on)
        {
            if (on == _highlighted || _renderers == null) return;
            _highlighted = on;
            if (on)
            {
                // Skip pieces that were destroyed since the part was built (keeps _original aligned for restoring).
                _renderers = _renderers.Where(r => r != null).ToArray();
                _original = _renderers.Select(r => r.sharedMaterials).ToArray();
                foreach (Renderer r in _renderers)
                {
                    var mats = r.materials;   // instances, so other exhibits aren't affected
                    foreach (Material m in mats)
                    {
                        m.EnableKeyword("_EMISSION");
                        m.SetColor("_EmissionColor", new Color(0.35f, 0.3f, 0.1f));
                    }
                    r.materials = mats;
                }
            }
            else if (_original != null)
            {
                for (int i = 0; i < _renderers.Length; i++)
                {
                    if (_renderers[i] == null) continue;
                    foreach (Material m in _renderers[i].materials) Destroy(m);
                    _renderers[i].sharedMaterials = _original[i];
                }
                _original = null;
            }
        }

        public string Title
        {
            get
            {
                string name = string.IsNullOrWhiteSpace(Data.Label) ? Nice(Data.Kind) : Data.Label.Trim();
                string value = Data.Value?.Trim();
                return string.IsNullOrEmpty(value) || value == name ? name : $"{name}  ·  {value}";
            }
        }

        public string Describe(CircuitSim sim)
        {
            var sb = new StringBuilder();
            sb.Append("<b>").Append(Title).Append("</b>");
            if (!string.IsNullOrWhiteSpace(Data.Label)) sb.Append("  <size=80%><color=#8a7866>").Append(Nice(Data.Kind)).Append("</color></size>");
            if (!string.IsNullOrWhiteSpace(Data.Info)) sb.Append('\n').Append(Data.Info.Trim());
            string live = sim != null ? sim.Readout(Data.Id) : null;
            if (!string.IsNullOrEmpty(live)) sb.Append("\n<color=#2f7fb8>").Append(live).Append("</color>");
            return sb.ToString();
        }

        static string Nice(string kind)
        {
            switch (kind)
            {
                case "led": return "LED";
                case "meter": return "Meter";
                case "node": return "Junction";
                default: return string.IsNullOrEmpty(kind) ? "Part" : char.ToUpperInvariant(kind[0]) + kind.Substring(1);
            }
        }
    }

    /// <summary>Everything the viewer needs to know about a built exhibit.</summary>
    public sealed class ExhibitInfo : MonoBehaviour
    {
        public SceneModel Model;
        public Transform Content;
        public bool IsCircuit;
        /// <summary>Moving system (orbits, gears): no exploded view.</summary>
        public bool Animated;
        public CircuitSim Sim;
        public readonly List<ExhibitPart> Parts = new List<ExhibitPart>();

        float _explode, _explodeTarget;

        public bool Exploded => _explodeTarget > 0.5f;

        public void SetInteractive(bool on)
        {
            foreach (ExhibitPart p in Parts) p.SetInteractive(on);
        }

        public void SetExploded(bool on) => _explodeTarget = on ? 1f : 0f;

        void Update()
        {
            if (Mathf.Approximately(_explode, _explodeTarget)) return;
            _explode = Mathf.MoveTowards(_explode, _explodeTarget, Time.unscaledDeltaTime * 2.5f);
            float t = Mathf.SmoothStep(0, 1, _explode);
            foreach (ExhibitPart p in Parts)
                if (p != null) p.transform.localPosition = p.BasePosition + p.ExplodeOffset * t;
        }
    }
}
