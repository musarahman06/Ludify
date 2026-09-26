using UnityEngine;

/// <summary>
/// Procedural supercharged V8 for <see cref="CarController"/>. No audio files: at startup it synthesises looping
/// engine clips at a few rpm points (cross-plane firing order for the V8 burble) plus a supercharger whine,
/// then crossfades and pitches them from the car's rpm and throttle. Works on every platform, including WebGL.
/// </summary>
[RequireComponent(typeof(CarController))]
public class CarEngineAudio : MonoBehaviour
{
    [Range(0f, 1f)] public float volume = 0.7f;
    [Range(0f, 1f)] public float whineVolume = 0.22f;

    static readonly float[] LayerRpm = { 1000f, 3000f, 5600f };
    static AudioClip[] engineClips;
    static AudioClip whineClip;
    const float WhineClipHz = 1200f;
    const float WhinePerRpm = 0.45f;   // blower whine frequency (Hz) per engine rpm

    CarController car;
    AudioSource[] layers;
    AudioSource whine;
    float smoothedThrottle;

    void Start()
    {
        car = GetComponent<CarController>();
        BuildClips();

        layers = new AudioSource[LayerRpm.Length];
        for (int i = 0; i < layers.Length; i++) layers[i] = CreateSource(engineClips[i]);
        whine = CreateSource(whineClip);
    }

    AudioSource CreateSource(AudioClip clip)
    {
        var src = gameObject.AddComponent<AudioSource>();
        src.clip = clip;
        src.loop = true;
        src.playOnAwake = false;
        src.spatialBlend = 0f;   // the camera follows the driven car, so play it straight to the speakers
        src.dopplerLevel = 0f;
        src.volume = 0f;
        return src;
    }

    void Update()
    {
        if (car == null || layers == null) return;
        float rpm = car.EngineRpm;
        bool running = rpm > 50f;

        smoothedThrottle = Mathf.MoveTowards(smoothedThrottle, car.Throttle, Time.deltaTime * 6f);
        // Load: louder and fuller on throttle, quieter on the overrun.
        float load = 0.45f + 0.55f * smoothedThrottle;
        // Fade out as the engine spins down after getting out.
        float running01 = Mathf.Clamp01(rpm / (car.idleRpm * 0.8f));

        for (int i = 0; i < layers.Length; i++)
        {
            var src = layers[i];
            float w = LayerWeight(rpm, i);
            src.pitch = Mathf.Clamp(Mathf.Max(rpm, 300f) / LayerRpm[i], 0.3f, 3f);
            src.volume = volume * w * load * running01;
            SetPlaying(src, running && w > 0.001f);
        }

        float rev01 = Mathf.Clamp01(rpm / car.redlineRpm);
        whine.pitch = Mathf.Clamp(rpm * WhinePerRpm / WhineClipHz, 0.1f, 3f);
        whine.volume = whineVolume * running01 * rev01 * rev01 * (0.35f + 0.65f * smoothedThrottle);
        SetPlaying(whine, running);
    }

    static void SetPlaying(AudioSource src, bool play)
    {
        if (play && !src.isPlaying) src.Play();
        else if (!play && src.isPlaying) src.Stop();
    }

    /// <summary>Triangular crossfade between neighbouring rpm layers.</summary>
    static float LayerWeight(float rpm, int i)
    {
        float r = Mathf.Clamp(rpm, LayerRpm[0], LayerRpm[LayerRpm.Length - 1]);
        if (i > 0 && r <= LayerRpm[i] && r >= LayerRpm[i - 1])
            return Mathf.InverseLerp(LayerRpm[i - 1], LayerRpm[i], r);
        if (i < LayerRpm.Length - 1 && r >= LayerRpm[i] && r <= LayerRpm[i + 1])
            return 1f - Mathf.InverseLerp(LayerRpm[i], LayerRpm[i + 1], r);
        return 0f;
    }

    // ------------------------------------------------------------------ synthesis

    static void BuildClips()
    {
        if (engineClips != null && engineClips[0] != null) return;
        int sr = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 44100;
        engineClips = new AudioClip[LayerRpm.Length];
        for (int i = 0; i < LayerRpm.Length; i++) engineClips[i] = SynthV8(LayerRpm[i], sr);
        whineClip = SynthWhine(sr);
    }

    /// <summary>
    /// One engine cycle = two crank revolutions = 8 firings. A cross-plane V8 fires its banks unevenly
    /// (L R L L R L R R), which gives the characteristic lumpy burble; each firing is a short damped
    /// exhaust thump plus a burst of noise. The loop holds a whole number of cycles and pulse tails wrap
    /// around, so it loops seamlessly.
    /// </summary>
    static AudioClip SynthV8(float rpm, int sr)
    {
        float cycleHz = rpm / 120f;
        int cycles = Mathf.Max(1, Mathf.RoundToInt(cycleHz * 1.2f));
        int length = Mathf.RoundToInt(cycles * sr / cycleHz);
        float cycleSamples = (float)length / cycles;
        var data = new float[length];
        var rng = new System.Random(1234 + (int)rpm);

        int[] bank = { 0, 1, 0, 0, 1, 0, 1, 1 };
        float[] cylGain = new float[8], cylJitter = new float[8];
        for (int c = 0; c < 8; c++)
        {
            cylGain[c] = (bank[c] == 0 ? 1f : 0.82f) * (0.88f + 0.24f * (float)rng.NextDouble());
            cylJitter[c] = ((float)rng.NextDouble() - 0.5f) * 0.06f;   // fraction of a firing interval
        }

        float firingInterval = cycleSamples / 8f;
        float decay = Mathf.Clamp(firingInterval * 0.9f, sr * 0.004f, sr * 0.03f);
        float thumpHz = 70f + rpm * 0.018f;                // exhaust resonance rises a little with revs
        int pulseLen = Mathf.Min(length, (int)(decay * 6f));

        for (int cyc = 0; cyc < cycles; cyc++)
        {
            for (int c = 0; c < 8; c++)
            {
                float start = cyc * cycleSamples + (c + cylJitter[c]) * firingInterval;
                float gain = cylGain[c] * (0.93f + 0.14f * (float)rng.NextDouble());
                float noiseState = 0f;
                for (int k = 0; k < pulseLen; k++)
                {
                    float t = k / (float)sr;
                    float env = Mathf.Exp(-k / decay);
                    float attack = Mathf.Min(1f, k / (sr * 0.0006f));
                    float thump = Mathf.Sin(2f * Mathf.PI * thumpHz * t) + 0.45f * Mathf.Sin(2f * Mathf.PI * thumpHz * 2.03f * t);
                    noiseState += 0.35f * (((float)rng.NextDouble() * 2f - 1f) - noiseState);   // soft-filtered noise
                    float sample = gain * attack * env * (thump + 0.9f * noiseState * Mathf.Exp(-k / (decay * 0.5f)));
                    int idx = (((int)start + k) % length + length) % length;   // wrap negatives too
                    data[idx] += sample;
                }
            }
        }

        // Low-pass (brighter at high rpm) run twice around the loop so the filter state wraps seamlessly.
        float cutoff = 900f + rpm * 0.35f;
        float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / sr);
        float y = 0f;
        for (int pass = 0; pass < 2; pass++)
            for (int n = 0; n < length; n++)
            {
                y += a * (data[n] - y);
                if (pass == 1) data[n] = y;
            }

        // Soft saturation for a meaty tone, then normalise.
        float peak = 0f;
        for (int n = 0; n < length; n++) { data[n] = Tanh(data[n] * 1.6f); peak = Mathf.Max(peak, Mathf.Abs(data[n])); }
        if (peak > 0f) for (int n = 0; n < length; n++) data[n] *= 0.9f / peak;

        var clip = AudioClip.Create($"V8_{rpm:0}rpm", length, 1, sr, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>Supercharger whine: a slightly buzzy tone, exactly one second of whole cycles so it loops cleanly.</summary>
    static AudioClip SynthWhine(int sr)
    {
        var data = new float[sr];
        for (int n = 0; n < sr; n++)
        {
            float ph = 2f * Mathf.PI * WhineClipHz * n / sr;
            data[n] = 0.6f * Mathf.Sin(ph) + 0.25f * Mathf.Sin(2f * ph) + 0.12f * Mathf.Sin(3f * ph + 0.5f);
        }
        var clip = AudioClip.Create("SuperchargerWhine", sr, 1, sr, false);
        clip.SetData(data, 0);
        return clip;
    }

    static float Tanh(float x)
    {
        float e = Mathf.Exp(2f * Mathf.Clamp(x, -10f, 10f));
        return (e - 1f) / (e + 1f);
    }
}
