using Ludify.Import;
using TMPro;
using UnityEngine;
using static Ludify.Import.ModelKit;

namespace Ludify.Gallery
{
    /// <summary>
    /// An exhibit spot in the cherry-blossom gallery: a wooden easel whose canvas shows the original image, and a low
    /// round wooden platform beside it where the 3D exhibit stands. Empty: a blank canvas and a glowing "+" hologram
    /// over the platform invite you to add an image. A small wooden card under the canvas explains the exhibit.
    /// The front faces local −Z. (Kept the name "Pedestal" so the rest of the gallery code is unchanged.)
    /// </summary>
    public sealed class Pedestal : MonoBehaviour
    {
        /// <summary>Height of the model platform's top.</summary>
        public const float TopHeight = 0.46f;
        const float Width = 2.4f;
        /// <summary>Where the model stands and where the easel stands (local, beside each other).</summary>
        static readonly Vector3 ModelSpot = new Vector3(1.35f, 0f, 0f), EaselSpot = new Vector3(-1.1f, 0f, 0.05f);

        static readonly Color Wood = new Color(0.55f, 0.38f, 0.24f);
        static readonly Color WoodDark = new Color(0.42f, 0.28f, 0.18f);
        static readonly Color WoodLight = new Color(0.68f, 0.5f, 0.33f);
        static readonly Color Cream = new Color(0.96f, 0.93f, 0.86f);
        static readonly Color PlaqueColor = new Color(0.3f, 0.2f, 0.13f);
        static readonly Color Holo = new Color(0.35f, 0.8f, 1f);

        public ExhibitRecord Record { get; private set; }
        public bool IsEmpty => Record == null;
        /// <summary>What the inspect camera orbits (centre of the exhibit).</summary>
        public Transform Focus { get; private set; }
        public Vector3 Front => transform.position - transform.forward * (Width / 2 + 1.2f);

        Transform _exhibit, _holder, _hologram, _thumbnail;
        TextMeshPro _canvasHint;

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
            // A low wooden deck shared by the easel and the model platform.
            Prim(PrimitiveType.Cube, t, new Vector3(0.15f, 0.06f, 0.1f), new Vector3(4.7f, 0.12f, 2.5f), WoodDark, name: "Deck");

            // Model platform: round and low, so the exhibit sits at eye level next to its picture.
            Prim(PrimitiveType.Cylinder, t, ModelSpot + new Vector3(0, 0.22f, 0), new Vector3(2.2f, 0.2f, 2.2f), Wood, name: "Platform");
            Prim(PrimitiveType.Cylinder, t, ModelSpot + new Vector3(0, TopHeight - 0.02f, 0), new Vector3(2.3f, 0.02f, 2.3f), WoodLight, name: "PlatformTop");

            // Easel: base block, A-frame legs, back leg and a top rail.
            Vector3 e = EaselSpot;
            Prim(PrimitiveType.Cube, t, e + new Vector3(0, 0.22f, 0.1f), new Vector3(1.5f, 0.2f, 0.9f), WoodLight, name: "EaselBase");
            Rod(t, e + new Vector3(-0.6f, 0.3f, -0.15f), e + new Vector3(-0.38f, 2.78f, 0.1f), 0.08f, Wood);
            Rod(t, e + new Vector3(0.6f, 0.3f, -0.15f), e + new Vector3(0.38f, 2.78f, 0.1f), 0.08f, Wood);
            Rod(t, e + new Vector3(0, 0.3f, 0.55f), e + new Vector3(0, 2.55f, 0.12f), 0.07f, Wood);
            Rod(t, e + new Vector3(-0.78f, 2.74f, 0.1f), e + new Vector3(0.78f, 2.74f, 0.1f), 0.06f, WoodDark);
            Prim(PrimitiveType.Cube, t, e + new Vector3(0, 1.08f, -0.13f), new Vector3(1.72f, 0.06f, 0.22f), WoodDark, name: "Ledge");

            // The canvas leans back a little; the picture sits on its front face.
            var canvas = new GameObject("Canvas").transform;
            canvas.SetParent(t, false);
            canvas.localPosition = e + new Vector3(0, 1.74f, -0.04f);
            canvas.localRotation = Quaternion.Euler(-7f, 0, 0);
            Prim(PrimitiveType.Cube, canvas, new Vector3(0, 0, 0.03f), new Vector3(1.64f, 1.26f, 0.05f), Wood, name: "Frame");
            Prim(PrimitiveType.Cube, canvas, Vector3.zero, new Vector3(1.52f, 1.14f, 0.04f), Cream, name: "CanvasBoard");
            _thumbnail = Prim(PrimitiveType.Quad, canvas, new Vector3(0, 0, -0.025f), new Vector3(1.4f, 1.02f, 1f), Color.white, name: "Picture");
            _thumbnail.gameObject.SetActive(false);
            _canvasHint = Label(canvas, "Your image\nhere", new Vector3(0, 0, -0.03f), 0.22f, new Color(0.55f, 0.45f, 0.4f));

            var box = gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0.15f, 0.6f, 0.1f);
            box.size = new Vector3(4.7f, 1.2f, 2.5f);

            // Wooden card under the canvas: what the exhibit is.
            Vector3 card = e + new Vector3(0, 0.62f, -0.48f);
            Prim(PrimitiveType.Cube, t, card, new Vector3(1.5f, 0.52f, 0.04f), PlaqueColor, name: "Plaque");
            var textGo = new GameObject("PlaqueText");
            textGo.transform.SetParent(t, false);
            textGo.transform.localPosition = card + new Vector3(0, 0, -0.03f);
            _plaqueText = textGo.AddComponent<TextMeshPro>();
            _plaqueText.rectTransform.sizeDelta = new Vector2(1.4f, 0.46f);
            _plaqueText.enableAutoSizing = true;
            _plaqueText.fontSizeMin = 0.2f;
            _plaqueText.fontSizeMax = 1.2f;
            _plaqueText.alignment = TextAlignmentOptions.Midline;
            _plaqueText.color = new Color(1f, 0.93f, 0.78f);
            _plaqueText.textWrappingMode = TextWrappingModes.Normal;

            Focus = new GameObject("Focus").transform;
            Focus.SetParent(t, false);
            Focus.localPosition = ModelSpot + new Vector3(0, TopHeight + 1.2f, 0);

            BuildHologram();
            ShowEmpty();
        }

        void BuildHologram()
        {
            _hologram = new GameObject("Hologram").transform;
            _hologram.SetParent(transform, false);
            _hologram.localPosition = ModelSpot + new Vector3(0, TopHeight + 1.1f, 0);
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
                    _holder.localPosition = ModelSpot + new Vector3(0, TopHeight + CircuitLift + Mathf.Sin(Time.time * 1.2f) * 0.05f, 0);
                else
                    _holder.localRotation = Quaternion.Euler(0, Time.time * SpinDegreesPerSecond, 0);
            }
            if (_hologram == null || !_hologram.gameObject.activeSelf) return;
            _hologram.localRotation = Quaternion.Euler(0, Time.time * 40f, 0);
            _hologram.localPosition = ModelSpot + new Vector3(0, TopHeight + 1.1f + Mathf.Sin(Time.time * 2f) * 0.08f, 0);
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
            _holder.localPosition = ModelSpot + new Vector3(0, TopHeight, 0);
            _exhibit = exhibit.transform;
            _exhibit.SetParent(_holder, false);
            _exhibit.localPosition = Vector3.zero;
            ExhibitInfo built = exhibit.GetComponent<ExhibitInfo>();
            if (built != null && built.IsCircuit)
            {
                // Tip the board toward the front (−Z), pivoting on its centre, so you see the top, not the edge.
                _exhibit.localRotation = Quaternion.Euler(-CircuitTilt, 0, 0);
                _holder.localPosition = ModelSpot + new Vector3(0, TopHeight + CircuitLift, 0);
            }

            _hologram.gameObject.SetActive(false);
            string how = model == null || !model.IsModel ? "Picture" : model.Mode == "concept" ? "Concept model" : "Traced from image";
            _plaqueText.text = $"<b>{Escape(model?.Title ?? "Image")}</b>\n<size=65%><i>{Escape(model?.Subject)}  ·  {how}</i></size>\n<size=75%>{Escape(model?.Explanation)}</size>";

            _thumbnail.gameObject.SetActive(image != null);
            _canvasHint.gameObject.SetActive(image == null);
            if (image != null)
            {
                var r = _thumbnail.GetComponent<Renderer>();
                var m = new Material(r.sharedMaterial) { mainTexture = image, color = Color.white };
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", image);
                r.sharedMaterial = m;
                float aspect = (float)image.width / image.height;
                // Fit inside the canvas (1.4 × 1.02 m).
                const float w = 1.4f, h = 1.02f;
                _thumbnail.localScale = aspect >= w / h ? new Vector3(w, w / aspect, 1) : new Vector3(h * aspect, h, 1);
            }
        }

        public void ShowEmpty()
        {
            ClearExhibitObject();
            Record = null;
            _hologram.gameObject.SetActive(true);
            _thumbnail.gameObject.SetActive(false);
            _canvasHint.gameObject.SetActive(true);
            _plaqueText.text = "<b>Empty easel</b>\n<size=75%>Press <b>I</b> to add an image. Gemini turns it into a 3D exhibit.</size>";
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
