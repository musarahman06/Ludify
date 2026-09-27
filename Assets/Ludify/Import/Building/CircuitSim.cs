using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ludify.Import
{
    /// <summary>
    /// Runs the circuit on a built PCB exhibit: solves it, lights bulbs/LEDs by power, moves the
    /// current dots along the traces (direction and speed from the solution), animates switch
    /// levers, and provides live voltage/current readouts. Click a switch to toggle it.
    /// </summary>
    public sealed class CircuitSim : MonoBehaviour
    {
        PcbBuilder.Result _pcb;
        readonly Dictionary<string, List<Material>> _glass = new Dictionary<string, List<Material>>();
        readonly Dictionary<string, Color> _glassColor = new Dictionary<string, Color>();
        readonly Dictionary<string, Transform> _levers = new Dictionary<string, Transform>();

        public CircuitSolver Solver => _pcb.Netlist.Solver;

        public void Init(PcbBuilder.Result pcb)
        {
            _pcb = pcb;
            foreach (PartLibrary.BuiltPart part in pcb.Parts.Values)
            {
                foreach (Renderer r in part.Root.GetComponentsInChildren<Renderer>())
                {
                    if (r.name == "Glass")
                    {
                        // Own material instance, so this bulb's brightness doesn't change other exhibits.
                        var m = new Material(r.sharedMaterial);
                        r.sharedMaterial = m;
                        if (!_glass.TryGetValue(part.Data.Id, out var list)) _glass[part.Data.Id] = list = new List<Material>();
                        list.Add(m);
                        _glassColor[part.Data.Id] = m.color;
                    }
                    if (r.name == "Lever") _levers[part.Data.Id] = r.transform;
                }
            }
            OrientLeds();
            Resolve();
        }

        /// <summary>
        /// Which lead of an LED is which can't always be read from a picture. Close every switch, and turn
        /// any LED that stays dark but would light the other way round, then restore the switches.
        /// </summary>
        void OrientLeds()
        {
            var switches = _pcb.Netlist.Solver.Elements.Where(e => e.Kind == "switch").ToList();
            var wasOpen = switches.Select(e => e.IsOpen).ToList();
            foreach (var e in switches) e.IsOpen = false;
            foreach (var led in _pcb.Netlist.Solver.Elements.Where(e => e.IsLed))
            {
                if (!Solver.Solve() || led.LedOn) continue;
                (led.NodeA, led.NodeB) = (led.NodeB, led.NodeA);
                if (!Solver.Solve() || !led.LedOn) (led.NodeA, led.NodeB) = (led.NodeB, led.NodeA);
            }
            for (int i = 0; i < switches.Count; i++) switches[i].IsOpen = wasOpen[i];
        }

        public bool IsSwitch(string partId) =>
            _pcb.Netlist.ElementByPart.TryGetValue(partId, out var e) && e.Kind == "switch";

        public void Toggle(string partId)
        {
            if (!_pcb.Netlist.ElementByPart.TryGetValue(partId, out var e) || e.Kind != "switch") return;
            e.IsOpen = !e.IsOpen;
            Resolve();
        }

        public void Resolve()
        {
            bool ok = Solver.Solve();

            foreach (var (link, top, bottom) in _pcb.Wires)
            {
                double? leaving = _pcb.Netlist.CurrentLeaving(link.From, link.FromTerminal);
                if (leaving == null)
                {
                    double? arriving = _pcb.Netlist.CurrentLeaving(link.To, link.ToTerminal);
                    leaving = arriving.HasValue ? -arriving.Value : (double?)null;
                }
                float rate = ok && leaving.HasValue ? Rate(leaving.Value) : 0f;
                _pcb.Flow.SetRate(top, rate);
                _pcb.Flow.SetRate(bottom, rate);
            }

            foreach (var kv in _pcb.Netlist.ElementByPart)
            {
                CircuitSolver.Element e = kv.Value;
                if (_glass.TryGetValue(kv.Key, out var mats))
                {
                    float brightness = !ok ? 0f
                        : e.Kind == "led" ? Mathf.Clamp01((float)(e.Current / 0.02))
                        : Mathf.Clamp01(Mathf.Sqrt((float)(e.Power / 0.05)));
                    Color baseColor = _glassColor[kv.Key];
                    Color glow = e.Kind == "led" ? baseColor : new Color(1f, 0.8f, 0.35f);
                    foreach (Material m in mats)
                    {
                        m.color = Color.Lerp(baseColor * 0.55f, baseColor, brightness);
                        m.EnableKeyword("_EMISSION");
                        m.SetColor("_EmissionColor", glow * (brightness * 2.5f));
                    }
                }
                if (_levers.TryGetValue(kv.Key, out Transform lever))
                {
                    // Open: lever tilted up off the right contact. Closed: flat across both contacts.
                    lever.localRotation = Quaternion.Euler(0, 0, e.IsOpen ? 25 : 0);
                    lever.localPosition = new Vector3(0, e.IsOpen ? 0.16f : 0.07f, 0);
                }
            }
        }

        /// <summary>Dot speed grows with current (square root, so mA and A are both visible).</summary>
        static float Rate(double amps)
        {
            double a = System.Math.Abs(amps);
            if (a < 1e-5) return 0f;
            float speed = Mathf.Clamp(0.4f + 6f * Mathf.Sqrt((float)a), 0.4f, 3f);
            return amps > 0 ? speed : -speed;
        }

        /// <summary>Live readout for a part's tooltip, e.g. "3.3 V across · 15 mA through".</summary>
        public string Readout(string partId)
        {
            if (!_pcb.Netlist.ElementByPart.TryGetValue(partId, out var e)) return null;
            if (!Solver.Solved) return "Can't simulate this circuit.";
            string amps = CircuitSolver.FormatAmps(e.Current), volts = CircuitSolver.FormatVolts(e.VoltageAcross);
            switch (e.Kind)
            {
                case "battery": return $"{CircuitSolver.FormatVolts(e.Emf)} battery, supplying {amps}";
                case "switch": return (e.IsOpen ? "Open, no current can flow" : $"Closed, {amps} flowing") + "  ·  <b>click to toggle</b>";
                case "bulb": return $"{volts} across  ·  {amps} through  ·  {(e.Power * 1000):0.#} mW";
                case "led": return e.LedOn && System.Math.Abs(e.Current) > 1e-5 ? $"On  ·  {amps} through" : "Off: no current is flowing through it";
                case "capacitor": return $"Charged to {volts} (no DC current flows through)";
                case "meter": return e.Resistance > 1 ? $"Reads {volts}" : $"Reads {amps}";
                default: return $"{volts} across  ·  {amps} through";
            }
        }
    }
}
