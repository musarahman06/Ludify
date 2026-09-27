using System;
using System.Collections.Generic;
using Ludify.Import;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Bottom-centre conversation panel: speaker name, their line, and up to 3 reply buttons (keys 1-3 or click;
/// Esc picks the last option). The player stands still while it's open.
/// </summary>
public class DialogueBox : MonoBehaviour
{
    public bool IsOpen => panel != null && panel.gameObject.activeSelf;

    RectTransform panel;
    TextMeshProUGUI speaker, line;
    readonly List<Button> buttons = new List<Button>();
    readonly List<Action> actions = new List<Action>();
    PlayerController pausedPlayer;
    int openedFrame;

    public static DialogueBox Create()
    {
        var canvas = UiKit.CreateCanvas("DialogueCanvas", 60);
        var box = canvas.gameObject.AddComponent<DialogueBox>();
        box.Build(canvas.transform);
        return box;
    }

    void Build(Transform canvas)
    {
        var bg = UiKit.Image("Panel", canvas, UiKit.PanelColor, UiKit.RoundedSprite, raycast: true);
        bg.type = Image.Type.Sliced;
        panel = UiKit.Place(bg.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(980f, 250f));

        var accent = UiKit.Image("Accent", panel, UiKit.AccentColor);
        UiKit.Place(accent.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -3f), new Vector2(940f, 4f));

        speaker = UiKit.Text("Speaker", panel, "", 30, TextAlignmentOptions.TopLeft, new Color(1f, 0.82f, 0.3f));
        UiKit.Place(speaker.rectTransform, new Vector2(0f, 1f), new Vector2(30f, -18f), new Vector2(900f, 40f));
        speaker.rectTransform.pivot = new Vector2(0f, 1f);
        speaker.fontStyle = FontStyles.Bold;

        line = UiKit.Text("Line", panel, "", 27, TextAlignmentOptions.TopLeft);
        UiKit.Place(line.rectTransform, new Vector2(0f, 1f), new Vector2(30f, -62f), new Vector2(920f, 110f));
        line.rectTransform.pivot = new Vector2(0f, 1f);

        for (int i = 0; i < 3; i++)
        {
            int index = i;
            var b = UiKit.Button("Option" + i, panel, "", 24, () => Choose(index));
            UiKit.Place((RectTransform)b.transform, new Vector2(0f, 0f), new Vector2(30f + i * 310f, 20f), new Vector2(295f, 56f));
            ((RectTransform)b.transform).pivot = new Vector2(0f, 0f);
            buttons.Add(b);
        }
        panel.gameObject.SetActive(false);
    }

    public void Show(string who, string text, params (string label, Action action)[] options)
    {
        speaker.text = who;
        line.text = text;
        actions.Clear();
        for (int i = 0; i < buttons.Count; i++)
        {
            bool used = i < options.Length;
            buttons[i].gameObject.SetActive(used);
            if (!used) continue;
            UiKit.SetButtonLabel(buttons[i], $"{i + 1}.  {options[i].label}");
            actions.Add(options[i].action);
        }
        panel.gameObject.SetActive(true);
        openedFrame = Time.frameCount;

        var player = FindAnyObjectByType<PlayerController>();
        if (player != null && player.enabled) { player.enabled = false; pausedPlayer = player; }
    }

    public void Close()
    {
        panel.gameObject.SetActive(false);
        if (pausedPlayer != null) { pausedPlayer.enabled = true; pausedPlayer = null; }
    }

    void Choose(int index)
    {
        if (!IsOpen || index >= actions.Count) return;
        var action = actions[index];
        Close();
        action?.Invoke();   // may open the next line of dialogue
    }

    void Update()
    {
        if (!IsOpen || Time.frameCount == openedFrame) return;
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) Choose(0);
        else if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) Choose(1);
        else if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) Choose(2);
        else if (kb.escapeKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame) Choose(actions.Count - 1);
    }
}
