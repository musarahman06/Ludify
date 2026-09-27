using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Ludify.Import.ModelKit;

namespace Ludify.Import
{
    /// <summary>
    /// Moves a body around what it orbits (planet → star, moon → planet) on a visible orbit ring.
    /// Speed follows Kepler's third law (closer = faster). Uses unscaled time so it runs in the viewer.
    /// </summary>
    public sealed class Orbiter : MonoBehaviour
    {
        Transform _centre, _ring;
        float _radius, _angle, _degreesPerSecond;

        public static void Build(PartLibrary.BuiltPart body, PartLibrary.BuiltPart centre, Transform content, float innermostRadius)
        {
            Vector3 d = body.Root.localPosition - centre.Root.localPosition;
            d.y = 0;
            float radius = Mathf.Max(d.magnitude, 0.5f);
            var orbiter = body.Root.gameObject.AddComponent<Orbiter>();
            orbiter._centre = centre.Root;
            orbiter._radius = radius;
            orbiter._angle = Mathf.Atan2(d.z, d.x) * Mathf.Rad2Deg;
            // T ∝ r^1.5 → angular speed ∝ r^-1.5; the innermost orbit takes ~12 s.
            orbiter._degreesPerSecond = 30f * Mathf.Pow(Mathf.Max(innermostRadius, 0.5f) / radius, 1.5f);

            float thickness = Mathf.Clamp(0.06f / radius, 0.004f, 0.08f);            // ~0.06 layout units wide
            orbiter._ring = MeshObject("OrbitRing", RingMesh(1f - thickness), content, centre.Root.localPosition,
                                       Quaternion.identity, Vector3.one * radius, Glass(new Color(0.8f, 0.9f, 1f, 0.6f)));
        }

        void Update()
        {
            if (_centre == null) return;
            _angle += _degreesPerSecond * Time.unscaledDeltaTime;
            float a = _angle * Mathf.Deg2Rad;
            Vector3 c = _centre.localPosition;
            transform.localPosition = c + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * _radius;
            transform.localRotation = Quaternion.Euler(0, -_angle * 3f, 0);   // spin on its own axis too
            if (_ring != null) _ring.localPosition = c;
            ExhibitPart part = GetComponent<ExhibitPart>();
            if (part != null) part.BasePosition = transform.localPosition;
        }
    }

    /// <summary>
    /// Turns meshing gears: the first gear drives, and each neighbour turns the opposite way at
    /// (driver teeth ÷ its teeth) × the driver's speed, which is exactly how real gear trains work.
    /// </summary>
    public sealed class GearTrain : MonoBehaviour
    {
        const float DriverDegreesPerSecond = 40f;
        readonly List<(Transform wheel, float speed)> _gears = new List<(Transform, float)>();

        public static void Build(Transform content, Dictionary<string, PartLibrary.BuiltPart> parts, List<SceneLink> meshLinks)
        {
            var gears = parts.Values.Where(p => p.Data.Kind == "gear").ToList();
            if (gears.Count == 0) return;
            var neighbours = gears.ToDictionary(g => g, g => new List<PartLibrary.BuiltPart>());
            foreach (SceneLink l in meshLinks)
                if (parts.TryGetValue(l.From, out var a) && parts.TryGetValue(l.To, out var b) && neighbours.ContainsKey(a) && neighbours.ContainsKey(b))
                {
                    neighbours[a].Add(b);
                    neighbours[b].Add(a);
                }

            var speed = new Dictionary<PartLibrary.BuiltPart, float>();
            foreach (PartLibrary.BuiltPart start in gears)
            {
                if (speed.ContainsKey(start)) continue;
                speed[start] = DriverDegreesPerSecond;               // each separate train gets its own driver
                var queue = new Queue<PartLibrary.BuiltPart>();
                queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    PartLibrary.BuiltPart g = queue.Dequeue();
                    foreach (PartLibrary.BuiltPart n in neighbours[g])
                    {
                        if (speed.ContainsKey(n)) continue;
                        float ratio = PartLibrary.GearTeeth(g.Data.Value) / (float)PartLibrary.GearTeeth(n.Data.Value);
                        speed[n] = -speed[g] * ratio;
                        queue.Enqueue(n);
                    }
                }
            }

            var train = content.gameObject.AddComponent<GearTrain>();
            foreach (var kv in speed)
            {
                Transform wheel = kv.Key.Root.Find("Wheel");
                if (wheel != null) train._gears.Add((wheel, kv.Value));
            }
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            foreach (var (wheel, speed) in _gears)
                if (wheel != null) wheel.localRotation *= Quaternion.Euler(0, 0, speed * dt);
        }
    }
}
