using Ludify.Import;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Job tracker (right side, under the city quest tracker) with a progress bar and timer, plus the job "[E]" prompt.
/// Separate from CityHud so the city quest code doesn't overwrite it each frame.
/// </summary>
public class JobHud : MonoBehaviour
{
    TextMeshProUGUI title, body, prompt;
    RectTransform panel;
    Image barFill;

    public static JobHud Create()
    {
        var canvas = UiKit.CreateCanvas("JobHudCanvas", 29);
        var hud = canvas.gameObject.AddComponent<JobHud>();
        hud.Build(canvas.transform);
        return hud;
    }

    void Build(Transform canvas)
    {
        var bg = UiKit.Image("JobTracker", canvas, UiKit.PanelColor, UiKit.RoundedSprite);
        bg.type = Image.Type.Sliced;
        panel = UiKit.Place(bg.rectTransform, new Vector2(1f, 1f), new Vector2(-24f, -380f), new Vector2(420f, 190f));

        title = UiKit.Text("Title", panel, "", 24, TextAlignmentOptions.TopLeft, new Color(0.55f, 0.95f, 0.5f));
        UiKit.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(18f, -12f), new Vector2(384f, 34f));
        title.rectTransform.pivot = new Vector2(0f, 1f);
        title.fontStyle = FontStyles.Bold;

        var barBg = UiKit.Image("Bar", panel, new Color(1f, 1f, 1f, 0.12f), UiKit.RoundedSprite);
        barBg.type = Image.Type.Sliced;
        UiKit.Place(barBg.rectTransform, new Vector2(0f, 1f), new Vector2(18f, -50f), new Vector2(384f, 12f));
        barBg.rectTransform.pivot = new Vector2(0f, 1f);
        barFill = UiKit.Image("Fill", barBg.transform, new Color(0.45f, 0.9f, 0.4f), UiKit.RoundedSprite);
        barFill.type = Image.Type.Sliced;
        var fr = barFill.rectTransform;
        fr.anchorMin = Vector2.zero; fr.anchorMax = new Vector2(0f, 1f); fr.pivot = new Vector2(0f, 0.5f);
        fr.offsetMin = Vector2.zero; fr.offsetMax = Vector2.zero;

        body = UiKit.Text("Body", panel, "", 20, TextAlignmentOptions.TopLeft);
        UiKit.Place(body.rectTransform, new Vector2(0f, 1f), new Vector2(18f, -70f), new Vector2(384f, 115f));
        body.rectTransform.pivot = new Vector2(0f, 1f);
        panel.gameObject.SetActive(false);

        prompt = UiKit.Text("JobPrompt", canvas, "", 30, TextAlignmentOptions.Center);
        UiKit.Place(prompt.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 250f), new Vector2(900f, 50f));
        prompt.fontStyle = FontStyles.Bold;
        prompt.outlineWidth = 0.2f;
        prompt.outlineColor = new Color32(0, 0, 0, 200);
    }

    public void SetPrompt(string text) => prompt.text = text ?? "";

    public void SetTracker(string jobTitle, string text, float progress01)
    {
        bool show = !string.IsNullOrEmpty(jobTitle);
        if (panel.gameObject.activeSelf != show) panel.gameObject.SetActive(show);
        if (!show) return;
        title.text = jobTitle;
        body.text = text;
        var fr = barFill.rectTransform;
        fr.anchorMax = new Vector2(Mathf.Clamp01(progress01), 1f);
    }
}
