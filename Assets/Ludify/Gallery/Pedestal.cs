using Ludify.Import;
using TMPro;
using UnityEngine;
using static Ludify.Import.ModelKit;

namespace Ludify.Gallery
{
    /// <summary>
    /// A large stone pedestal. Empty: a glowing "+" hologram invites you to add an image.
    /// Filled: the 3D exhibit stands on top and the front plaque explains it next to a thumbnail
    /// of the original image. The front faces local −Z.
    /// </summary>
    public sealed class Pedestal : MonoBehaviour
    {
        public const float TopHeight = 1.5f;
        const float Width = 2.4f;

        static readonly Color Marble = new Color(0.9f, 0.88f, 0.84f);
        static readonly Color Stone = new Color(0.62f, 0.6f, 0.57f);
        static readonly Color PlaqueColor = new Color(0.16f, 0.13f, 0.1f);
        static readonly Color Holo = new Color(0.35f, 0.8f, 1f);

        public ExhibitRecord Record { get; private set; }
        public bool IsEmpty => Record == null;
        /// <summary>What the inspect camera orbits (centre of the exhibit).</summary>
        public Transform Focus { get; private set; }
        public Vector3 Front => transform.position - transform.forward * (Width / 2 + 1.2f);

        Transform _exhibit, _holder, _hologram, _thumbnail;

        /// <summary>The built exhibit (null when empty).</summary>
        public Transform Exhibit => _exhibit;
        public ExhibitInfo Info => _exhibit != null ? _exhibit.GetComponent<ExhibitInfo>() : null;
        /// <summary>Set by the viewer while it holds the exhibit, so the idle motion stops.</summary>
        public bool InViewer;

        const float SpinDegreesPerSecond = 8f;
        const float CircuitTilt = 32f, CircuitLift = 1.1f;
        TextMeshPro _plaqueText, _holoText;
        string _status;

        public static Pedestal Create(Transform parent, Vector3 position, Quaternion rotation)
        {
            var go = new GameObject("Pedestal");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            var pedestal = go.AddComponent<Pedestal>();
            pedestal.Build();
            return pedestal;
        }

        void Build()
        {
            Transform t = transform;
            Prim(PrimitiveType.Cube, t, new Vector3(0, 0.125f, 0), new Vector3(Width + 0.5f, 0.25f, Width + 0.5f), Stone, name: "Plinth");
            Prim(PrimitiveType.Cube, t, new Vector3(0, 0.25f + 0.55f, 0), new Vector3(Width, 1.1f, Width), Marble, name: "Body");
            Prim(PrimitiveType.Cube, t, new Vector3(0, TopHeight - 0.075f, 0), new Vector3(Width + 0.25f, 0.15f, Width + 0.25f), Stone, name: "Cap");
            var box = gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0, TopHeight / 2, 0);
            box.size = new Vector3(Width + 0.5f, TopHeight, Width + 0.5f);

            // Plaque on the front face: thumbnail on the left, text on the right.
            float z = -Width / 2 - 0.02f;
            Prim(PrimitiveType.Cube, t, new Vector3(0, 0.8f, z), new Vector3(2.1f, 0.95f, 0.04f), PlaqueColor, name: "Plaque");
            _thumbnail = Prim(PrimitiveType.Quad, t, new Vector3(-0.68f, 0.8f, z - 0.025f), new Vector3(0.6f, 0.6f, 1f), Color.white, name: "Thumbnail");
            _thumbnail.gameObject.SetActive(false);

            var textGo = new GameObject("PlaqueText");
            textGo.transform.SetParent(t, false);
            textGo.transform.localPosition = new Vector3(0.28f, 0.8f, z - 0.03f);
            _plaqueText = textGo.AddComponent<TextMeshPro>();
            _plaqueText.rectTransform.sizeDelta = new Vector2(1.4f, 0.82f);
            _plaqueText.enableAutoSizing = true;
            _plaqueText.fontSizeMin = 0.3f;
            _plaqueText.fontSizeMax = 1.6f;
            _plaqueText.alignment = TextAlignmentOptions.MidlineLeft;
            _plaqueText.color = new Color(1f, 0.93f, 0.78f);
            _plaqueText.textWrappingMode = TextWrappingModes.Normal;

            Focus = new GameObject("Focus").transform;
            Focus.SetParent(t, false);
            Focus.localPosition = new Vector3(0, TopHeight + 1.2f, 0);

            BuildHologram();
            ShowEmpty();
        }

        void BuildHologram()
        {
            _hologram = new GameObject("Hologram").transform;
            _hologram.SetParent(transform, false);
            _hologram.localPosition = new Vector3(0, TopHeight + 1.1f, 0);
            Prim(PrimitiveType.Cube, _hologram, Vector3.zero, new Vector3(0.9f, 0.18f, 0.18f), Holo, emission: Holo * 2f);
            Prim(PrimitiveType.Cube, _hologram, Vector3.zero, new Vector3(0.18f, 0.9f, 0.18f), Holo, emission: Holo * 2f);
            Prim(PrimitiveType.Cylinder, _hologram, new Vector3(0, -1.05f, 0), new Vector3(1.6f, 0.01f, 1.6f), Holo, emission: Holo);
            _holoText = Label(_hologram, "", new Vector3(0, 0.85f, 0), 0.3f, Holo);
        }

        void Update()
        {
            // Idle display: circuit boards hover tilted toward the viewer; other exhibits turn slowly.
            if (_holder != null && !InViewer)
            {
                ExhibitInfo info = Info;
                if (info != null && info.IsCircuit)
                    _holder.localPosition = new Vector3(0, TopHeight + CircuitLift + Mathf.Sin(Time.time * 1.2f) * 0.05f, 0);
                else
                    _holder.localRotation = Quaternion.Euler(0, Time.time * SpinDegreesPerSecond, 0);
            }
            if (_hologram == null || !_hologram.gameObject.activeSelf) return;
            _hologram.localRotation = Quaternion.Euler(0, Time.time * 40f, 0);
            _hologram.localPosition = new Vector3(0, TopHeight + 1.1f + Mathf.Sin(Time.time * 2f) * 0.08f, 0);
        }

        /// <summary>Short progress text on an empty pedestal ("Gemini is studying the image…"); null clears it.</summary>
        public void SetStatus(string status)
        {
            _status = status;
            if (IsEmpty) _holoText.text = status ?? "Add an image  [I]";
        }

        public void SetExhibit(ExhibitRecord record, Texture2D image)
        {
            ClearExhibitObject();
            Record = record;
            SceneModel model = record.Model;

            GameObject exhibit = model != null && model.IsModel
                ? ModelBuilder.Build(model)
                : ModelBuilder.BuildImagePanel(image, model?.Title ?? "Image");
            _holder = new GameObject("Display").transform;
            _holder.SetParent(transform, false);
            _holder.localPosition = new Vector3(0, TopHeight, 0);
            _exhibit = exhibit.transform;
            _exhibit.SetParent(_holder, false);
            _exhibit.localPosition = Vector3.zero;
            ExhibitInfo built = exhibit.GetComponent<ExhibitInfo>();
            if (built != null && built.IsCircuit)
            {
                // Tip the board toward the front (−Z), pivoting on its centre, so you see the top, not the edge.
                _exhibit.localRotation = Quaternion.Euler(-CircuitTilt, 0, 0);
                _holder.localPosition = new Vector3(0, TopHeight + CircuitLift, 0);
            }

            _hologram.gameObject.SetActive(false);
            string how = model == null || !model.IsModel ? "Picture" : model.Mode == "concept" ? "Concept model" : "Traced from image";
            _plaqueText.text = $"<b>{Escape(model?.Title ?? "Image")}</b>\n<size=65%><i>{Escape(model?.Subject)}  ·  {how}</i></size>\n<size=75%>{Escape(model?.Explanation)}</size>";

            _thumbnail.gameObject.SetActive(image != null);
            if (image != null)
            {
                var r = _thumbnail.GetComponent<Renderer>();
                var m = new Material(r.sharedMaterial) { mainTexture = image, color = Color.white };
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", image);
                r.sharedMaterial = m;
                float aspect = (float)image.width / image.height;
                _thumbnail.localScale = aspect >= 1 ? new Vector3(0.62f, 0.62f / aspect, 1) : new Vector3(0.62f * aspect, 0.62f, 1);
            }
        }

        public void ShowEmpty()
        {
            ClearExhibitObject();
            Record = null;
            _hologram.gameObject.SetActive(true);
            _thumbnail.gameObject.SetActive(false);
            _plaqueText.text = "<b>Empty pedestal</b>\n<size=75%>Walk up and press <b>I</b> to add an image. Gemini will turn it into a 3D exhibit.</size>";
            SetStatus(_status);
        }

        void ClearExhibitObject()
        {
            if (_holder != null) Kill(_holder.gameObject);
            else if (_exhibit != null) Kill(_exhibit.gameObject);
            _exhibit = null;
            _holder = null;
            InViewer = false;
        }

        static string Escape(string s) => string.IsNullOrEmpty(s) ? "" : s.Replace("<", "‹").Replace(">", "›");
    }
}
