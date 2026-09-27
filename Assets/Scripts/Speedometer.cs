using UnityEngine;

/// <summary>
/// Small analogue speedometer drawn with IMGUI in the bottom-right corner: mph dial and needle, digital speed,
/// gear, and a rev bar. Textures are generated in code, so no UI assets are needed.
/// </summary>
public static class Speedometer
{
    const float MaxMph = 240f;
    const float StartAngle = -135f;
    const float Sweep = 270f;

    static Texture2D disc, pixel;
    static GUIStyle bigStyle, smallStyle, labelStyle;
    static int styleForHeight;

    public static void Draw(CarController car)
    {
        EnsureResources();

        float size = Mathf.Clamp(Screen.height * 0.2f, 120f, 190f);
        float margin = 16f;
        var rect = new Rect(Screen.width - size - margin, Screen.height - size - margin, size, size);
        var c = rect.center;
        float r = size * 0.5f;

        var oldColor = GUI.color;
        var oldMatrix = GUI.matrix;

        // Dial face.
        GUI.color = new Color(0.05f, 0.06f, 0.08f, 0.78f);
        GUI.DrawTexture(rect, disc);

        // Ticks every 10 mph, longer every 40.
        for (int v = 0; v <= MaxMph; v += 10)
        {
            bool major = v % 40 == 0;
            float len = r * (major ? 0.16f : 0.09f);
            float w = major ? 2.5f : 1.5f;
            GUI.color = new Color(1f, 1f, 1f, major ? 0.95f : 0.6f);
            GUIUtility.RotateAroundPivot(AngleFor(v), c);
            GUI.DrawTexture(new Rect(c.x - w * 0.5f, c.y - r * 0.9f, w, len), pixel);
            GUI.matrix = oldMatrix;

            if (major)
            {
                float a = AngleFor(v) * Mathf.Deg2Rad;
                var p = c + new Vector2(Mathf.Sin(a), -Mathf.Cos(a)) * r * 0.6f;
                GUI.color = new Color(1f, 1f, 1f, 0.8f);
                GUI.Label(new Rect(p.x - 20f, p.y - 10f, 40f, 20f), v.ToString(), labelStyle);
            }
        }

        // Rev bar with shift light colour near the redline.
        float rev = Mathf.Clamp01(car.EngineRpm / car.redlineRpm);
        var bar = new Rect(c.x - r * 0.45f, c.y + r * 0.62f, r * 0.9f, Mathf.Max(3f, size * 0.025f));
        GUI.color = new Color(1f, 1f, 1f, 0.15f);
        GUI.DrawTexture(bar, pixel);
        GUI.color = car.EngineRpm >= car.upshiftRpm * 0.97f ? new Color(1f, 0.25f, 0.2f) : new Color(0.35f, 0.85f, 1f);
        GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * rev, bar.height), pixel);

        // Digital speed and gear.
        float mph = Mathf.Abs(car.SpeedMph);
        GUI.color = Color.white;
        GUI.Label(new Rect(rect.x, c.y + r * 0.05f, size, r * 0.35f), mph.ToString("0"), bigStyle);
        GUI.Label(new Rect(rect.x, c.y + r * 0.34f, size, r * 0.22f),
            $"mph   {(car.Gear < 0 ? "R" : car.Gear.ToString())}", smallStyle);

        // Needle.
        float needleW = Mathf.Max(2f, size * 0.018f);
        GUI.color = new Color(1f, 0.35f, 0.15f);
        GUIUtility.RotateAroundPivot(AngleFor(Mathf.Min(mph, MaxMph)), c);
        GUI.DrawTexture(new Rect(c.x - needleW * 0.5f, c.y - r * 0.82f, needleW, r * 0.82f), pixel);
        GUI.matrix = oldMatrix;
        GUI.DrawTexture(new Rect(c.x - size * 0.04f, c.y - size * 0.04f, size * 0.08f, size * 0.08f), disc);

        GUI.color = oldColor;
    }

    static float AngleFor(float mph) => StartAngle + Sweep * Mathf.Clamp01(mph / MaxMph);

    static void EnsureResources()
    {
        if (disc == null)
        {
            const int n = 128;
            disc = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var px = new Color32[n * n];
            float rad = n * 0.5f - 1f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f));
                    byte a = (byte)(255f * Mathf.Clamp01(rad - d + 0.5f));   // anti-aliased edge
                    px[y * n + x] = new Color32(255, 255, 255, a);
                }
            disc.SetPixels32(px);
            disc.Apply();
        }
        if (pixel == null)
        {
            pixel = new Texture2D(1, 1) { hideFlags = HideFlags.DontSave };
            pixel.SetPixel(0, 0, Color.white);
            pixel.Apply();
        }
        if (bigStyle == null || styleForHeight != Screen.height)
        {
            styleForHeight = Screen.height;
            float size = Mathf.Clamp(Screen.height * 0.2f, 120f, 190f);
            bigStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = Mathf.RoundToInt(size * 0.17f) };
            smallStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(size * 0.08f) };
            labelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(size * 0.075f) };
            foreach (var s in new[] { bigStyle, smallStyle, labelStyle }) s.normal.textColor = Color.white;
        }
    }
}
