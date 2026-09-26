using System.Collections.Generic;
using System.Linq;
using Ludify.Import;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Knowledge Time Trial: getting into a car on the circuit starts a 3- or 5-lap race (player's choice). Once per lap,
/// at a random point, the game drops into slow motion and asks a question from the imported lecture. Each question is
/// only ever asked once (tracked per lecture in <see cref="UsedQuestions"/>). Right answers give a speed boost;
/// wrong answers (or running out of time) slow the car and add 3 s. Best lap and best total are saved.
/// Created at runtime by <see cref="RuntimeWorldColliders"/>; the HUD is drawn by <see cref="TimeTrialHud"/>.
/// </summary>
public class TimeTrialManager : MonoBehaviour
{
    public enum State { Idle, AwaitingImport, ChoosingLaps, Countdown, Racing, Question, Finished }

    public static readonly int[] LapOptions = { 3, 5 };
    public const float CountdownSeconds = 3f;
    public const float AnswerSeconds = 15f;          // real time
    const float ResultSeconds = 2.2f;                // real time the right answer stays on screen
    const float SlowMotionScale = 0.1f;
    const float TimeEaseSeconds = 0.25f;             // real time to ease in/out of slow motion
    const float BoostMultiplier = 1.8f, BoostSeconds = 3f;
    const float PenaltySlowSeconds = 2f, PenaltyTimeSeconds = 3f;
    const int MaxStepPoints = 15;                    // bigger jumps in track progress = shortcut
    const float FlashSeconds = 1.6f;

    // ---- state read by the HUD ----
    public State CurrentState { get; private set; } = State.Idle;
    public CarController Car { get; private set; }
    public int Lap { get; private set; }
    public int LapCount { get; private set; } = 3;
    public float CurrentLapTime => IsTiming ? Time.time - lapStartTime : 0f;
    public float TotalTime => (CurrentState == State.Finished ? finishTime : IsTiming ? Time.time - raceStartTime : 0f) + PenaltyTotal;
    public float PenaltyTotal { get; private set; }
    public float LastLap { get; private set; }
    public float BestLapThisRace { get; private set; }
    public IReadOnlyList<float> LapTimes => lapTimes;
    public float CountdownRemaining => CountdownSeconds - (Time.time - countdownStart);
    public int Asked { get; private set; }
    public int CorrectCount { get; private set; }
    public bool HasQuiz => bank != null;
    public string Topic => bank?.Topic;
    /// <summary>Questions from this lecture that have never been asked (each question is only ever used once).</summary>
    public int QuestionsLeft => bank == null ? 0 : IsTiming || CurrentState == State.Countdown ? pool.Count : UnusedQuestions().Count;
    public int QuestionsTotal => bank?.Questions?.Count ?? 0;
    public TimeTrialRecords.Record Records => TimeTrialRecords.Get(RecordKey);
    public TimeTrialRecords.Record RaceRecords => TimeTrialRecords.Get(TimeTrialRecords.RaceKey(RecordKey, LapCount));
    public bool WrongWay => wrongWayTimer > 1f;
    public string FlashText => Time.unscaledTime < flashUntil ? flashText : null;
    public Color FlashColor { get; private set; } = Color.white;
    public string ImportError { get; private set; }
    public bool NewRecordTotal { get; private set; }
    public bool NewRecordLap { get; private set; }

    // question
    public McQuestion Question { get; private set; }
    public float AnswerTimeLeft => Mathf.Max(0f, AnswerSeconds - (Time.unscaledTime - questionStart));
    public bool Answered { get; private set; }
    public int ChosenIndex { get; private set; } = -1;
    public bool LastAnswerCorrect { get; private set; }

    bool IsTiming => CurrentState == State.Racing || CurrentState == State.Question;
    string RecordKey => bank?.Id;

    TrackPath track;
    QuestionBank bank;
    readonly List<McQuestion> pool = new List<McQuestion>();
    readonly List<float> lapTimes = new List<float>();
    float countdownStart, raceStartTime, lapStartTime, finishTime;
    int lastProgress, checkpoints, questionTrigger;
    bool questionAskedThisLap;
    float wrongWayTimer;
    float questionStart, resultUntil;
    float targetScale = 1f, baseFixedDelta;
    string flashText;
    float flashUntil;

    void Awake()
    {
        baseFixedDelta = Time.fixedDeltaTime;
        track = TrackPath.Load();
    }

    void OnEnable()
    {
        VehicleInteraction.CarEntered += OnCarEntered;
        VehicleInteraction.CarExited += OnCarExited;
        ImportButtonOverlay.LessonImported += OnLessonImported;
        ImportButtonOverlay.ImportFailed += OnImportFailed;
    }

    void OnDisable()
    {
        VehicleInteraction.CarEntered -= OnCarEntered;
        VehicleInteraction.CarExited -= OnCarExited;
        ImportButtonOverlay.LessonImported -= OnLessonImported;
        ImportButtonOverlay.ImportFailed -= OnImportFailed;
        ResetToIdle();
        Time.timeScale = 1f;
        Time.fixedDeltaTime = baseFixedDelta;
    }

    // ------------------------------------------------------------------ entering / leaving

    void OnCarEntered(CarController car)
    {
        if (track == null) return;
        Car = car;
        ImportButtonOverlay.Enabled = false;
        PlaceOnStartLine();

        ImportError = null;
        bank = QuestionBankStore.LoadAll().FirstOrDefault(b => b.Questions != null && b.Questions.Count > 0);
        CurrentState = bank != null ? State.ChoosingLaps : State.AwaitingImport;
        Car.HoldForStart = true;
    }

    void OnCarExited(CarController car)
    {
        if (car == Car) ResetToIdle();
    }

    void ResetToIdle()
    {
        if (Car != null)
        {
            Car.HoldForStart = false;
            Car.ClearEffects();
        }
        Car = null;
        CurrentState = State.Idle;
        targetScale = 1f;
        VehicleInteraction.BlockExit = false;
        ImportButtonOverlay.Enabled = true;
    }

    void PlaceOnStartLine()
    {
        // Two points (4 m) behind the line, centred: clear of the decorative grid cars.
        int i = track.StartIndex - 2;
        Vector3 pos = track.PointAt(i) + Vector3.up * 0.35f;
        Car.PlaceAt(pos, Quaternion.LookRotation(track.TangentAt(i), Vector3.up));
        track.ResetSearch();
    }

    // ------------------------------------------------------------------ import prompt (called by the HUD)

    public void ImportLecture()
    {
        ImportError = null;
        ImportButtonOverlay.RequestImport();
    }

    public void RaceWithoutQuestions()
    {
        bank = null;
        CurrentState = State.ChoosingLaps;
    }

    /// <summary>Called by the lap-choice buttons (or keys 3 / 5).</summary>
    public void ChooseLaps(int laps)
    {
        if (CurrentState != State.ChoosingLaps) return;
        LapCount = laps;
        StartCountdown();
    }

    void OnLessonImported(QuestionBank imported)
    {
        if (imported?.Questions == null || imported.Questions.Count == 0) return;
        if (CurrentState == State.AwaitingImport || CurrentState == State.ChoosingLaps)
        {
            bank = imported;
            ImportError = null;
            CurrentState = State.ChoosingLaps;
        }
    }

    void OnImportFailed(string message)
    {
        if (CurrentState == State.AwaitingImport || CurrentState == State.ChoosingLaps) ImportError = message;
    }

    List<McQuestion> UnusedQuestions()
    {
        if (bank?.Questions == null) return new List<McQuestion>();
        var used = UsedQuestions.For(bank.Id);
        return bank.Questions.Where(q => !used.Contains(q.Id ?? q.Prompt)).ToList();
    }

    // ------------------------------------------------------------------ race flow

    void StartCountdown()
    {
        CurrentState = State.Countdown;
        countdownStart = Time.time;
        Car.HoldForStart = true;
        Car.ClearEffects();
        lapTimes.Clear();
        Lap = 0;
        PenaltyTotal = 0f;
        LastLap = BestLapThisRace = 0f;
        Asked = CorrectCount = 0;
        NewRecordTotal = NewRecordLap = false;
        wrongWayTimer = 0f;

        // Fresh, shuffled pool of never-asked questions for this race.
        pool.Clear();
        pool.AddRange(UnusedQuestions());
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }
    }

    void Go()
    {
        CurrentState = State.Racing;
        Car.HoldForStart = false;
        raceStartTime = lapStartTime = Time.time;
        Lap = 1;
        lastProgress = track.ProgressFromStart(track.NearestIndex(Car.transform.position));
        StartLap();
        Flash("GO!", new Color(0.4f, 1f, 0.5f));
    }

    void StartLap()
    {
        checkpoints = 0;
        questionAskedThisLap = false;
        // One question per lap, somewhere between 20% and 85% of the way round.
        questionTrigger = Random.Range(Mathf.RoundToInt(track.Count * 0.2f), Mathf.RoundToInt(track.Count * 0.85f));
    }

    void Update()
    {
        UpdateTimeScale();
        if (Car == null) return;
        var kb = Keyboard.current;

        switch (CurrentState)
        {
            case State.AwaitingImport:
                Car.HoldForStart = true;
                break;
            case State.ChoosingLaps:
                Car.HoldForStart = true;
                if (kb != null)
                {
                    if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) ChooseLaps(3);
                    else if (kb.digit5Key.wasPressedThisFrame || kb.numpad5Key.wasPressedThisFrame) ChooseLaps(5);
                }
                break;
            case State.Countdown:
                if (Time.time - countdownStart >= CountdownSeconds) Go();
                break;
            case State.Racing:
                UpdateProgress();
                break;
            case State.Question:
                UpdateQuestion(kb);
                break;
            case State.Finished:
                Car.HoldForStart = true;
                if (kb != null && kb.rKey.wasPressedThisFrame)
                {
                    PlaceOnStartLine();
                    CurrentState = State.ChoosingLaps;
                }
                break;
        }
    }

    void UpdateProgress()
    {
        int n = track.Count;
        int progress = track.ProgressFromStart(track.NearestIndex(Car.transform.position));
        int delta = progress - lastProgress;
        if (delta > n / 2) delta -= n;
        if (delta < -n / 2) delta += n;
        bool smooth = Mathf.Abs(delta) <= MaxStepPoints;

        // Wrong-way warning.
        if (delta < 0 && Car.ForwardSpeed > 2f) wrongWayTimer += Time.deltaTime;
        else wrongWayTimer = Mathf.Max(0f, wrongWayTimer - Time.deltaTime * 2f);

        // Checkpoints at 25/50/75% must be driven through in order.
        if (checkpoints < 3)
        {
            int next = n * (checkpoints + 1) / 4;
            if (lastProgress < next && progress >= next && progress < n * 0.95f)
            {
                if (smooth) checkpoints++;
                else Flash("Shortcut! Checkpoint missed", new Color(1f, 0.5f, 0.3f));
            }
        }

        // Question trigger.
        if (pool.Count > 0 && !questionAskedThisLap && smooth && lastProgress < questionTrigger && progress >= questionTrigger)
        {
            AskQuestion();
            lastProgress = progress;
            return;
        }

        // Crossing the start/finish line going forward.
        if (delta > 0 && lastProgress > n * 0.9f && progress < n * 0.1f && checkpoints >= 3) CompleteLap();

        lastProgress = progress;
    }

    void CompleteLap()
    {
        float lapTime = Time.time - lapStartTime;
        lapTimes.Add(lapTime);
        LastLap = lapTime;
        if (BestLapThisRace <= 0f || lapTime < BestLapThisRace) BestLapThisRace = lapTime;
        if (TimeTrialRecords.SubmitLap(RecordKey, lapTime))
        {
            NewRecordLap = true;
            Flash($"NEW BEST LAP  {TimeTrialRecords.Format(lapTime)}", new Color(1f, 0.85f, 0.3f));
        }
        else
        {
            Flash($"Lap {Lap}  {TimeTrialRecords.Format(lapTime)}", Color.white);
        }

        if (Lap >= LapCount) { Finish(); return; }
        Lap++;
        lapStartTime = Time.time;
        StartLap();
    }

    void Finish()
    {
        finishTime = Time.time - raceStartTime;
        CurrentState = State.Finished;
        Car.HoldForStart = true;
        Car.ClearEffects();
        NewRecordTotal = TimeTrialRecords.SubmitRace(RecordKey, LapCount, finishTime + PenaltyTotal, CorrectCount, Asked);
    }

    // ------------------------------------------------------------------ questions

    void AskQuestion()
    {
        Question = pool[pool.Count - 1];
        pool.RemoveAt(pool.Count - 1);
        UsedQuestions.MarkUsed(bank.Id, Question.Id ?? Question.Prompt);   // never asked again, even in later races
        questionAskedThisLap = true;
        CurrentState = State.Question;
        questionStart = Time.unscaledTime;
        Answered = false;
        ChosenIndex = -1;
        targetScale = SlowMotionScale;
        VehicleInteraction.BlockExit = true;
        Asked++;
    }

    void UpdateQuestion(Keyboard kb)
    {
        if (!Answered)
        {
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) Answer(0);
                else if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) Answer(1);
                else if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) Answer(2);
                else if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame) Answer(3);
            }
            if (!Answered && AnswerTimeLeft <= 0f) Answer(-1);
            return;
        }

        if (Time.unscaledTime >= resultUntil) Resume();
    }

    /// <summary>Called by keys 1–4 or the HUD buttons; -1 means time ran out.</summary>
    public void Answer(int choice)
    {
        if (CurrentState != State.Question || Answered) return;
        Answered = true;
        ChosenIndex = choice;
        LastAnswerCorrect = choice >= 0 && Question.IsCorrect(choice);
        if (LastAnswerCorrect) CorrectCount++;
        float extra = string.IsNullOrEmpty(Question.Explanation) ? 0f : 1.3f;
        resultUntil = Time.unscaledTime + ResultSeconds + (LastAnswerCorrect ? 0f : extra);
    }

    void Resume()
    {
        CurrentState = State.Racing;
        targetScale = 1f;
        VehicleInteraction.BlockExit = false;
        if (LastAnswerCorrect)
        {
            Car.ApplyBoost(BoostMultiplier, BoostSeconds);
            Flash("BOOST!", new Color(0.35f, 0.9f, 1f));
        }
        else
        {
            Car.ApplyPenalty(PenaltySlowSeconds);
            PenaltyTotal += PenaltyTimeSeconds;
            Flash($"+{PenaltyTimeSeconds:0}s penalty", new Color(1f, 0.4f, 0.35f));
        }
    }

    // ------------------------------------------------------------------ helpers

    void UpdateTimeScale()
    {
        float scale = Mathf.MoveTowards(Time.timeScale, targetScale, Time.unscaledDeltaTime * (1f - SlowMotionScale) / TimeEaseSeconds);
        if (!Mathf.Approximately(scale, Time.timeScale))
        {
            Time.timeScale = scale;
            Time.fixedDeltaTime = baseFixedDelta * Mathf.Max(scale, 0.01f);
        }
    }

    void Flash(string text, Color color)
    {
        flashText = text;
        FlashColor = color;
        flashUntil = Time.unscaledTime + FlashSeconds;
    }

    void OnGUI() => TimeTrialHud.Draw(this);
}
