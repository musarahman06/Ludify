using Ludify.Import;
using UnityEngine;

/// <summary>
/// IMGUI overlay for <see cref="TimeTrialManager"/>: lap/time panel (top-left), countdown, flash messages,
/// the slow-motion question card, the "import a lecture" prompt and the finish screen.
/// Laid out at an 800-px design height and scaled to the screen, like the import button.
/// </summary>
public static class TimeTrialHud
{
    const float DesignHeight = 800f;

    static Texture2D pixel;
    static GUIStyle big, lapStyle, row, rowValue, title, body, button, centeredButton, flash, small;

    static readonly Color PanelColor = new Color(0.05f, 0.06f, 0.09f, 0.82f);
    static readonly Color Accent = new Color(1f, 0.82f, 0.3f);
    static readonly Color Good = new Color(0.35f, 0.9f, 0.5f);
    static readonly Color Bad = new Color(1f, 0.4f, 0.35f);

    public static void Draw(TimeTrialManager tt)
    {
        if (tt.CurrentState == TimeTrialManager.State.Idle || tt.Car == null) return;
        if (LessonFilePicker.IsOpen) return;   // the file browser draws its own UI on top
        EnsureStyles();

        float scale = Screen.height / DesignHeight;
        var previous = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        float width = Screen.width / scale;

        switch (tt.CurrentState)
        {
            case TimeTrialManager.State.AwaitingImport:
                DrawImportPrompt(tt, width);
                break;
            case TimeTrialManager.State.ChoosingLaps:
                DrawLapChoice(tt, width);
                break;
            case TimeTrialManager.State.Countdown:
                DrawLapPanel(tt);
                DrawCountdown(tt, width);
                break;
            case TimeTrialManager.State.Racing:
                DrawLapPanel(tt);
                break;
            case TimeTrialManager.State.Question:
                DrawLapPanel(tt);
                DrawQuestion(tt, width);
                break;
            case TimeTrialManager.State.Finished:
                DrawFinish(tt, width);
                break;
        }

        if (tt.WrongWay && tt.CurrentState == TimeTrialManager.State.Racing)
            Shadowed(new Rect(0, 220, width, 50), "WRONG WAY", flash, Bad);

        string f = tt.FlashText;
        if (f != null && tt.CurrentState != TimeTrialManager.State.Question)
            Shadowed(new Rect(0, 150, width, 50), f, flash, tt.FlashColor);

        GUI.matrix = previous;
    }

    // ------------------------------------------------------------------ panels

    static void DrawLapPanel(TimeTrialManager tt)
    {
        var records = tt.Records;
        var rect = new Rect(16, 16, 300, tt.HasQuiz ? 262 : 246);
        Panel(rect);

        float x = rect.x + 16, y = rect.y + 10, w = rect.width - 32;
        int lap = Mathf.Max(tt.Lap, 1);
        GUI.Label(new Rect(x, y, w, 44), $"LAP {lap}/{tt.LapCount}", lapStyle);
        y += 48;

        Row(ref y, x, w, "Lap", TimeTrialRecords.Format(tt.CurrentLapTime), Color.white);
        string total = TimeTrialRecords.Format(tt.TotalTime);
        if (tt.PenaltyTotal > 0f) total += $"  (+{tt.PenaltyTotal:0}s)";
        Row(ref y, x, w, "Total", total, Color.white);
        Row(ref y, x, w, "Last lap", TimeTrialRecords.Format(tt.LastLap), Color.white);
        Row(ref y, x, w, "Best lap", TimeTrialRecords.Format(tt.BestLapThisRace), Accent);
        Row(ref y, x, w, "PB lap", TimeTrialRecords.Format(records.BestLap), new Color(0.75f, 0.8f, 0.9f));
        Row(ref y, x, w, $"PB {tt.LapCount} laps", TimeTrialRecords.Format(tt.RaceRecords.BestTotal), new Color(0.75f, 0.8f, 0.9f));

        y += 4;
        string quiz = tt.HasQuiz ? $"Quiz  {tt.CorrectCount}/{tt.Asked} correct  •  {tt.QuestionsLeft} left" : "No quiz (racing without questions)";
        GUI.color = tt.HasQuiz ? Good : new Color(0.7f, 0.7f, 0.75f);
        GUI.Label(new Rect(x, y, w, 22), quiz, small);
        GUI.color = Color.white;
        if (tt.HasQuiz && !string.IsNullOrEmpty(tt.Topic))
        {
            GUI.color = new Color(0.7f, 0.75f, 0.85f);
            GUI.Label(new Rect(x, y + 18, w, 22), Truncate(tt.Topic, 38), small);
            GUI.color = Color.white;
        }
    }

    static void DrawCountdown(TimeTrialManager tt, float width)
    {
        int n = Mathf.CeilToInt(tt.CountdownRemaining);
        if (n < 1) return;
        Shadowed(new Rect(0, 250, width, 160), n.ToString(), big, n == 1 ? Accent : Color.white);
    }

    static void DrawQuestion(TimeTrialManager tt, float width)
    {
        var q = tt.Question;
        if (q == null) return;

        float w = Mathf.Min(680f, width - 40f);
        float promptH = body.CalcHeight(new GUIContent(q.Prompt), w - 40f);
        string explanation = tt.Answered && !tt.LastAnswerCorrect ? q.Explanation : null;
        float explH = string.IsNullOrEmpty(explanation) ? 0f : small.CalcHeight(new GUIContent(explanation), w - 40f) + 8f;
        float h = 70f + promptH + 4 * 52f + 20f + explH + 30f;
        var rect = new Rect((width - w) * 0.5f, Mathf.Max(90f, (DesignHeight - h) * 0.5f), w, h);
        Panel(rect);

        float x = rect.x + 20f, y = rect.y + 14f, iw = w - 40f;
        GUI.color = Accent;
        GUI.Label(new Rect(x, y, iw, 26), $"SLOW-MO QUESTION  •  LAP {tt.Lap}", title);
        GUI.color = Color.white;

        // Timer bar (real seconds).
        float t = tt.Answered ? 0f : tt.AnswerTimeLeft / TimeTrialManager.AnswerSeconds;
        var bar = new Rect(x, y + 30, iw, 6);
        Fill(bar, new Color(1f, 1f, 1f, 0.15f));
        Fill(new Rect(bar.x, bar.y, bar.width * t, bar.height), t > 0.3f ? Good : Bad);
        if (!tt.Answered) GUI.Label(new Rect(x, y + 38, iw, 20), $"{Mathf.CeilToInt(tt.AnswerTimeLeft)}s   (keys 1-4 or click)", small);
        y += 62f;

        GUI.Label(new Rect(x, y, iw, promptH), q.Prompt, body);
        y += promptH + 10f;

        for (int i = 0; i < 4 && i < q.Choices.Length; i++)
        {
            var r = new Rect(x, y, iw, 44);
            var bg = new Color(0.22f, 0.26f, 0.34f);
            if (tt.Answered)
            {
                if (i == q.CorrectIndex) bg = new Color(0.18f, 0.55f, 0.3f);
                else if (i == tt.ChosenIndex) bg = new Color(0.65f, 0.2f, 0.18f);
            }
            GUI.backgroundColor = bg;
            if (GUI.Button(r, $"  {i + 1}.  {q.Choices[i]}", button) && !tt.Answered) tt.Answer(i);
            GUI.backgroundColor = Color.white;
            y += 52f;
        }

        if (tt.Answered)
        {
            string verdict = tt.ChosenIndex < 0 ? "Out of time!  +3s penalty"
                : tt.LastAnswerCorrect ? "Correct!  Speed boost incoming" : "Not quite.  +3s penalty";
            GUI.color = tt.LastAnswerCorrect ? Good : Bad;
            GUI.Label(new Rect(x, y + 2, iw, 26), verdict, title);
            GUI.color = Color.white;
            if (!string.IsNullOrEmpty(explanation))
                GUI.Label(new Rect(x, y + 30, iw, explH), explanation, small);
        }
    }

    static void DrawImportPrompt(TimeTrialManager tt, float width)
    {
        float w = Mathf.Min(560f, width - 40f), h = 270f;
        var rect = new Rect((width - w) * 0.5f, (DesignHeight - h) * 0.5f, w, h);
        Panel(rect);
        float x = rect.x + 24f, y = rect.y + 20f, iw = w - 48f;

        GUI.color = Accent;
        GUI.Label(new Rect(x, y, iw, 30), "IMPORT A LECTURE TO RACE", title);
        GUI.color = Color.white;
        y += 38f;
        const string intro = "The time trial asks questions from your own notes. Import a lecture (PDF, PowerPoint, " +
            "Word or text) and answer them in slow motion for speed boosts.";
        float introH = body.CalcHeight(new GUIContent(intro), iw);
        GUI.Label(new Rect(x, y, iw, introH), intro, body);
        y += introH + 24f;

        if (ImportButtonOverlay.IsBusy)
        {
            GUI.Label(new Rect(x, y, iw, 50), ImportButtonOverlay.StatusMessage ?? "Importing…", body);
            return;
        }

        if (!string.IsNullOrEmpty(tt.ImportError))
        {
            GUI.color = Bad;
            GUI.Label(new Rect(x, y - 16, iw, 40), Truncate(tt.ImportError, 140), small);
            GUI.color = Color.white;
        }

        float bw = (iw - 16f) * 0.5f;
        GUI.backgroundColor = new Color(0.25f, 0.55f, 0.9f);
        if (GUI.Button(new Rect(x, y + 30, bw, 52), "Import lecture", button)) tt.ImportLecture();
        GUI.backgroundColor = new Color(0.3f, 0.33f, 0.4f);
        if (GUI.Button(new Rect(x + bw + 16f, y + 30, bw, 52), "Race without questions", button)) tt.RaceWithoutQuestions();
        GUI.backgroundColor = Color.white;
    }

    static void DrawLapChoice(TimeTrialManager tt, float width)
    {
        float w = Mathf.Min(560f, width - 40f), iw = w - 48f;
        string info;
        bool exhausted = tt.HasQuiz && tt.QuestionsLeft == 0;
        if (!tt.HasQuiz) info = "Racing without questions.";
        else if (exhausted) info = $"You've answered every question from \"{Truncate(tt.Topic ?? "this lecture", 40)}\". Import a new lecture for fresh questions.";
        else info = $"Questions from \"{Truncate(tt.Topic ?? "your lecture", 40)}\": {tt.QuestionsLeft} of {tt.QuestionsTotal} unused. One per lap, never repeated.";
        float infoH = body.CalcHeight(new GUIContent(info), iw);
        bool fewLeft = tt.HasQuiz && !exhausted && tt.QuestionsLeft < TimeTrialManager.LapOptions[1];
        float h = 60f + infoH + 10f + (string.IsNullOrEmpty(tt.ImportError) ? 0f : 36f) + 66f
                  + (fewLeft ? 26f : 0f) + (exhausted || !tt.HasQuiz ? 50f : 0f) + 10f;

        var rect = new Rect((width - w) * 0.5f, (DesignHeight - h) * 0.5f, w, h);
        Panel(rect);
        float x = rect.x + 24f, y = rect.y + 20f;

        GUI.color = Accent;
        GUI.Label(new Rect(x, y, iw, 30), "CHOOSE RACE LENGTH", title);
        GUI.color = Color.white;
        y += 40f;

        if (ImportButtonOverlay.IsBusy)
        {
            GUI.Label(new Rect(x, y, iw, 50), ImportButtonOverlay.StatusMessage ?? "Importing…", body);
            return;
        }

        GUI.color = exhausted ? Accent : Color.white;
        GUI.Label(new Rect(x, y, iw, infoH), info, body);
        GUI.color = Color.white;
        y += infoH + 10f;

        if (!string.IsNullOrEmpty(tt.ImportError))
        {
            GUI.color = Bad;
            GUI.Label(new Rect(x, y, iw, 36), Truncate(tt.ImportError, 120), small);
            GUI.color = Color.white;
            y += 36f;
        }

        float bw = (iw - 16f) * 0.5f;
        for (int i = 0; i < TimeTrialManager.LapOptions.Length; i++)
        {
            int laps = TimeTrialManager.LapOptions[i];
            GUI.backgroundColor = new Color(0.25f, 0.55f, 0.9f);
            if (GUI.Button(new Rect(x + i * (bw + 16f), y, bw, 56), $"{laps} LAPS   (key {laps})", centeredButton)) tt.ChooseLaps(laps);
        }
        GUI.backgroundColor = Color.white;
        y += 66f;

        if (fewLeft)
            GUI.Label(new Rect(x, y, iw, 22), $"Only {tt.QuestionsLeft} questions left: some laps won't have one.", small);

        if (exhausted || !tt.HasQuiz)
        {
            GUI.backgroundColor = new Color(0.3f, 0.33f, 0.4f);
            if (GUI.Button(new Rect(x, y, iw, 40), "Import a new lecture", button)) tt.ImportLecture();
            GUI.backgroundColor = Color.white;
        }
    }

    static void DrawFinish(TimeTrialManager tt, float width)
    {
        float w = Mathf.Min(520f, width - 40f), h = 356f;
        var rect = new Rect((width - w) * 0.5f, (DesignHeight - h) * 0.5f, w, h);
        Panel(rect);
        float x = rect.x + 24f, y = rect.y + 18f, iw = w - 48f;

        GUI.Label(new Rect(x, y, iw, 50), "FINISHED", lapStyle);
        y += 56f;
        var records = tt.Records;
        Row(ref y, x, iw, "Total time", TimeTrialRecords.Format(tt.TotalTime), tt.NewRecordTotal ? Accent : Color.white);
        if (tt.PenaltyTotal > 0f) Row(ref y, x, iw, "Penalties", $"+{tt.PenaltyTotal:0}s", Bad);
        Row(ref y, x, iw, "Best lap", TimeTrialRecords.Format(tt.BestLapThisRace), tt.NewRecordLap ? Accent : Color.white);
        if (tt.HasQuiz) Row(ref y, x, iw, "Questions", $"{tt.CorrectCount}/{tt.Asked} correct", Good);
        if (tt.HasQuiz) Row(ref y, x, iw, "Coins earned", $"+{tt.CoinsEarned}", new Color(1f, 0.82f, 0.25f));
        Row(ref y, x, iw, $"PB {tt.LapCount} laps", TimeTrialRecords.Format(tt.RaceRecords.BestTotal), new Color(0.75f, 0.8f, 0.9f));
        Row(ref y, x, iw, "PB lap", TimeTrialRecords.Format(records.BestLap), new Color(0.75f, 0.8f, 0.9f));

        y += 8f;
        if (tt.NewRecordTotal || tt.NewRecordLap)
        {
            GUI.color = Accent;
            GUI.Label(new Rect(x, y, iw, 28), tt.NewRecordTotal ? "NEW RECORD!" : "NEW BEST LAP!", title);
            GUI.color = Color.white;
            y += 30f;
        }
        GUI.Label(new Rect(x, y, iw, 22), "R  race again      X  get out", small);
    }

    // ------------------------------------------------------------------ helpers

    static void Row(ref float y, float x, float w, string label, string value, Color valueColor)
    {
        GUI.Label(new Rect(x, y, w * 0.45f, 26), label, row);
        GUI.color = valueColor;
        GUI.Label(new Rect(x + w * 0.4f, y, w * 0.6f, 26), value, rowValue);
        GUI.color = Color.white;
        y += 26f;
    }

    static void Panel(Rect r)
    {
        Fill(r, PanelColor);
        Fill(new Rect(r.x, r.y, r.width, 3), Accent);
    }

    static void Fill(Rect r, Color c)
    {
        var old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, pixel);
        GUI.color = old;
    }

    static void Shadowed(Rect r, string text, GUIStyle style, Color color)
    {
        var old = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.7f);
        GUI.Label(new Rect(r.x + 3, r.y + 3, r.width, r.height), text, style);
        GUI.color = color;
        GUI.Label(r, text, style);
        GUI.color = old;
    }

    static string Truncate(string s, int max) => s.Length <= max ? s : s.Substring(0, max - 1) + "…";

    static void EnsureStyles()
    {
        if (pixel == null)
        {
            pixel = new Texture2D(1, 1) { hideFlags = HideFlags.DontSave };
            pixel.SetPixel(0, 0, Color.white);
            pixel.Apply();
        }
        if (big != null) return;

        GUIStyle Label(int size, FontStyle fs, TextAnchor anchor, bool wrap = false)
        {
            var s = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = fs, alignment = anchor, wordWrap = wrap };
            s.normal.textColor = Color.white;
            return s;
        }

        big = Label(140, FontStyle.Bold, TextAnchor.MiddleCenter);
        lapStyle = Label(34, FontStyle.Bold, TextAnchor.MiddleLeft);
        row = Label(17, FontStyle.Normal, TextAnchor.MiddleLeft);
        row.normal.textColor = new Color(0.72f, 0.76f, 0.84f);
        rowValue = Label(19, FontStyle.Bold, TextAnchor.MiddleRight);
        title = Label(20, FontStyle.Bold, TextAnchor.MiddleLeft);
        body = Label(19, FontStyle.Normal, TextAnchor.UpperLeft, wrap: true);
        small = Label(15, FontStyle.Normal, TextAnchor.UpperLeft, wrap: true);
        flash = Label(34, FontStyle.Bold, TextAnchor.MiddleCenter);
        button = new GUIStyle(GUI.skin.button) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, wordWrap = true };
        button.normal.textColor = button.hover.textColor = button.active.textColor = Color.white;
        centeredButton = new GUIStyle(button) { alignment = TextAnchor.MiddleCenter };
    }
}
