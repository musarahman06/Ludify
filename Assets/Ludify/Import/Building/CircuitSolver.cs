using System;
using System.Collections.Generic;
using System.Globalization;

namespace Ludify.Import
{
    /// <summary>
    /// DC circuit solver (nodal analysis). Every two-terminal element is modelled as a conductance
    /// with an optional current source (Norton form), so batteries, resistors, bulbs, switches,
    /// meters and LEDs all reduce to one linear system solved by Gaussian elimination.
    /// Terminal A = the part's local −X lead, B = +X lead. Current is positive flowing A → B inside the part.
    /// Pure C#, no Unity dependencies.
    /// </summary>
    public sealed class CircuitSolver
    {
        const double Open = 1e-9;             // conductance of an open switch / capacitor / leak to ground (S)
        const double LedDrop = 2.0, LedResistance = 50.0;

        public sealed class Element
        {
            public string Id;
            public string Kind;
            public int NodeA, NodeB;
            /// <summary>Ohms (resistors, bulbs, internal resistance of batteries).</summary>
            public double Resistance;
            /// <summary>Battery EMF in volts: V(B) − V(A) with no load (B is the + terminal).</summary>
            public double Emf;
            public bool IsOpen;                 // switches (toggled at runtime), capacitors at DC
            public bool IsLed;
            public bool LedOn;

            // Results
            public double Current;              // A → B through the element, amps
            public double VoltageAcross;        // V(A) − V(B)
            public double Power => Math.Abs(Current * VoltageAcross);
        }

        public readonly List<Element> Elements = new List<Element>();
        public int NodeCount;
        public int Ground;
        public double[] NodeVoltages = new double[0];
        public bool Solved { get; private set; }

        /// <summary>Solves the circuit. Returns false (and zero results) if it can't be solved.</summary>
        public bool Solve()
        {
            Solved = false;
            if (NodeCount < 2 || Elements.Count == 0) { Clear(); return false; }

            // LEDs are non-linear: guess, solve, fix any LED whose state is inconsistent, repeat.
            foreach (Element e in Elements) if (e.IsLed) e.LedOn = true;
            for (int iter = 0; iter < 12; iter++)
            {
                if (!SolveLinear()) { Clear(); return false; }
                bool changed = false;
                foreach (Element e in Elements)
                {
                    if (!e.IsLed) continue;
                    if (e.LedOn && e.Current < 0) { e.LedOn = false; changed = true; }
                    else if (!e.LedOn && e.VoltageAcross > LedDrop) { e.LedOn = true; changed = true; }
                }
                if (!changed) break;
            }
            Solved = true;
            return true;
        }

        void Clear()
        {
            foreach (Element e in Elements) { e.Current = 0; e.VoltageAcross = 0; }
        }

        bool SolveLinear()
        {
            // Unknowns: voltages of every node except ground.
            int n = NodeCount;
            var index = new int[n];
            int size = 0;
            for (int i = 0; i < n; i++) index[i] = i == Ground ? -1 : size++;
            var G = new double[size, size];
            var rhs = new double[size];

            void Stamp(int a, int b, double g, double j)
            {
                // Element current A→B = g·(Va − Vb) + j. KCL: sum of currents leaving each node = 0.
                int ia = index[a], ib = index[b];
                if (ia >= 0) { G[ia, ia] += g; rhs[ia] -= j; }
                if (ib >= 0) { G[ib, ib] += g; rhs[ib] += j; }
                if (ia >= 0 && ib >= 0) { G[ia, ib] -= g; G[ib, ia] -= g; }
            }

            foreach (Element e in Elements)
            {
                if (e.NodeA == e.NodeB) continue;
                (double g, double j) = Norton(e);
                Stamp(e.NodeA, e.NodeB, g, j);
            }
            // Tiny leak from every node to ground keeps floating parts of the circuit solvable.
            for (int i = 0; i < n; i++) if (index[i] >= 0) G[index[i], index[i]] += Open;

            double[] v = Gauss(G, rhs);
            if (v == null) return false;

            NodeVoltages = new double[n];
            for (int i = 0; i < n; i++) NodeVoltages[i] = index[i] >= 0 ? v[index[i]] : 0;
            foreach (Element e in Elements)
            {
                double va = NodeVoltages[e.NodeA], vb = NodeVoltages[e.NodeB];
                (double g, double j) = Norton(e);
                e.VoltageAcross = va - vb;
                e.Current = e.NodeA == e.NodeB ? 0 : g * (va - vb) + j;
            }
            return true;
        }

        static (double g, double j) Norton(Element e)
        {
            if (e.IsOpen) return (Open, 0);
            if (e.Kind == "battery")
            {
                // Inside the battery current flows from − (A) to + (B): i = (Va − Vb + E) / r.
                double g = 1.0 / Math.Max(e.Resistance, 1e-3);
                return (g, g * e.Emf);
            }
            if (e.IsLed)
            {
                if (!e.LedOn) return (Open, 0);
                double g = 1.0 / LedResistance;        // i = (Va − Vb − Vf) / R
                return (g, -g * LedDrop);
            }
            return (1.0 / Math.Max(e.Resistance, 1e-4), 0);
        }

        /// <summary>Gaussian elimination with partial pivoting. Returns null if singular.</summary>
        static double[] Gauss(double[,] a, double[] b)
        {
            int n = b.Length;
            var m = (double[,])a.Clone();
            var x = (double[])b.Clone();
            for (int col = 0; col < n; col++)
            {
                int pivot = col;
                for (int r = col + 1; r < n; r++) if (Math.Abs(m[r, col]) > Math.Abs(m[pivot, col])) pivot = r;
                if (Math.Abs(m[pivot, col]) < 1e-15) return null;
                if (pivot != col)
                {
                    for (int c = 0; c < n; c++) (m[col, c], m[pivot, c]) = (m[pivot, c], m[col, c]);
                    (x[col], x[pivot]) = (x[pivot], x[col]);
                }
                for (int r = col + 1; r < n; r++)
                {
                    double f = m[r, col] / m[col, col];
                    if (f == 0) continue;
                    for (int c = col; c < n; c++) m[r, c] -= f * m[col, c];
                    x[r] -= f * x[col];
                }
            }
            var result = new double[n];
            for (int r = n - 1; r >= 0; r--)
            {
                double s = x[r];
                for (int c = r + 1; c < n; c++) s -= m[r, c] * result[c];
                result[r] = s / m[r, r];
            }
            return result;
        }

        // ---- Values from Gemini's text ("9V", "220Ω", "4.7k", "1 MΩ") ----

        public static double ParseVolts(string value, double fallback)
        {
            double? v = ParseNumber(value, "v", "volt", "volts");
            return v.HasValue && v.Value > 0 ? v.Value : fallback;
        }

        public static double ParseOhms(string value, double fallback)
        {
            if (string.IsNullOrWhiteSpace(value)) return fallback;
            string s = value.ToLowerInvariant();
            if (s.Contains("v") && !s.Contains("ω") && !s.Contains("ohm")) return fallback;   // "6V" bulb rating, not a resistance
            double? v = ParseNumber(value, "ω", "ohm", "ohms", "r");
            return v.HasValue && v.Value > 0 ? v.Value : fallback;
        }

        static double? ParseNumber(string value, params string[] units)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            string s = value.Trim().ToLowerInvariant().Replace(" ", "").Replace("Ω", "ω");
            foreach (string u in units) if (s.EndsWith(u)) { s = s.Substring(0, s.Length - u.Length); break; }
            double mult = 1;
            if (s.EndsWith("k")) { mult = 1e3; s = s.TrimEnd('k'); }
            else if (s.EndsWith("m") && units[0] == "ω") { mult = 1e6; s = s.TrimEnd('m'); }
            else if (s.EndsWith("m")) { mult = 1e-3; s = s.TrimEnd('m'); }
            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double n) ? n * mult : (double?)null;
        }

        public static string FormatAmps(double a)
        {
            a = Math.Abs(a);
            if (a < 1e-6) return "0 A";
            if (a < 1e-3) return (a * 1e6).ToString("0", CultureInfo.InvariantCulture) + " µA";
            if (a < 1) return (a * 1e3).ToString("0.#", CultureInfo.InvariantCulture) + " mA";
            return a.ToString("0.##", CultureInfo.InvariantCulture) + " A";
        }

        public static string FormatVolts(double v)
        {
            v = Math.Abs(v);
            if (v < 1e-3) return "0 V";
            if (v < 1) return (v * 1e3).ToString("0", CultureInfo.InvariantCulture) + " mV";
            return v.ToString("0.##", CultureInfo.InvariantCulture) + " V";
        }
    }
}
