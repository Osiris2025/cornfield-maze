using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Original runtime soundtrack and field bed for the corn maze.
/// All clips are synthesized here (oscillators, noise, envelopes). No imported
/// or copyrighted film scores — Hitchcock-like only in gesture (dissonant
/// high-string stabs, anxious pulse), never a copied melody.
/// </summary>
public sealed class MazeMoodAudio : MonoBehaviour
{
    public static float Gust01 { get; private set; }
    public static bool WinThemePlaying { get; private set; }

    const float MusicVol = 0.26f;
    const float ChaseVol = 0.12f;
    const float WindVol = 0.22f;
    const float HowlVol = 0.14f;
    const float RustleVol = 0.16f;
    const float BrushVol = 0.13f;

    Transform _listenerFollow;
    Vector3 _goldWorld;
    Vector3[] _deadEnds;
    Vector3 _lastPos;
    AudioSource _music;
    AudioSource _chase;
    AudioSource _wind;
    AudioSource _howl;
    AudioSource _rustle;
    AudioSource _brush;
    AudioSource _win;
    float _move;
    float _winMix;

    public static MazeMoodAudio Install(Transform player, MazeData maze)
    {
        var existing = FindFirstObjectByType<MazeMoodAudio>();
        if (existing != null)
            Destroy(existing.gameObject);

        var go = new GameObject("MoodAudio");
        var mood = go.AddComponent<MazeMoodAudio>();
        mood._listenerFollow = player;
        mood._goldWorld = maze.GoldWorld;
        mood._deadEnds = FindDeadEnds(maze);
        if (player != null)
            mood._lastPos = player.position;
        return mood;
    }

    public static void PlayWin()
    {
        var mood = FindFirstObjectByType<MazeMoodAudio>();
        if (mood != null)
            mood.BeginWin();
    }

    void BeginWin()
    {
        WinThemePlaying = true;
        if (_win != null && !_win.isPlaying)
        {
            _win.volume = 0f;
            _win.Play();
        }
    }

    public void EnsurePlaying()
    {
        MobileAudioSession.Reactivate();
        RestartIfNeeded(_music);
        RestartIfNeeded(_chase);
        RestartIfNeeded(_wind);
        RestartIfNeeded(_howl);
        RestartIfNeeded(_rustle);
        RestartIfNeeded(_brush);
        if (WinThemePlaying)
            RestartIfNeeded(_win);
    }

    static void RestartIfNeeded(AudioSource src)
    {
        if (src == null || src.clip == null) return;
        src.mute = false;
        if (!src.isPlaying)
            src.Play();
    }

    void Start()
    {
        MobileAudioSession.Apply();
        _music = MakeSource("Music", MusicVol, 0f, true, 64);
        _chase = MakeSource("HuskChase", ChaseVol, 0f, true, 72);
        _wind = MakeSource("Wind", WindVol, 0f, true, 110);
        _howl = MakeSource("Howl", HowlVol, 0f, true, 118);
        _rustle = MakeSource("Rustle", RustleVol, 0.30f, true, 128);
        _brush = MakeSource("Brush", 0f, 0.42f, true, 132);
        _win = MakeSource("WinTheme", 0f, 0f, true, 20);
        if (_listenerFollow != null)
        {
            _rustle.transform.SetParent(_listenerFollow, false);
            _brush.transform.SetParent(_listenerFollow, false);
        }

        _music.clip = MazeMoodSynth.AnxiousDrama(16f);
        _chase.clip = MazeMoodSynth.HuskChase(14f);
        _wind.clip = MazeMoodSynth.Wind(10f);
        _howl.clip = MazeMoodSynth.HollowHowl(12f);
        _rustle.clip = MazeMoodSynth.CornRustle(8f);
        _brush.clip = MazeMoodSynth.CornBrush(5f);
        _win.clip = MazeMoodSynth.GoldStrike(6.4f);

        _music.Play();
        _chase.Play();
        _wind.Play();
        _howl.Play();
        _rustle.Play();
        _brush.Play();
        Gust01 = 0.22f;
        WinThemePlaying = false;
        _winMix = 0f;
    }

    void OnDisable()
    {
        if (WinThemePlaying)
            WinThemePlaying = false;
    }

    void Update()
    {
        float t = Time.time;
        float storm = StormWeather.Intensity;
        float gustA = 0.5f + 0.5f * Mathf.Sin(t * 0.31f);
        float gustB = 0.5f + 0.5f * Mathf.Sin(t * 0.17f + 1.2f);
        Gust01 = Mathf.Clamp01(0.12f + 0.55f * gustA * gustB + 0.42f * storm);

        if (_listenerFollow != null)
        {
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            float speed = (_listenerFollow.position - _lastPos).magnitude / dt;
            _lastPos = _listenerFollow.position;
            _move = Mathf.Lerp(_move, Mathf.Clamp01(speed / 7.4f), 1f - Mathf.Exp(-8f * dt));
        }

        if (WinThemePlaying)
            _winMix = Mathf.MoveTowards(_winMix, 1f, Time.deltaTime / 0.55f);
        float duck = 1f - 0.92f * _winMix;

        float tension = Tension();
        if (_music != null)
            _music.volume = Mathf.Min(0.42f, MusicVol * (1f + 0.30f * tension + 0.55f * storm)) * duck;
        if (_chase != null)
            _chase.volume = Mathf.Min(0.30f, ChaseVol * (0.52f + 0.70f * tension + 1.15f * storm)) * duck;
        if (_wind != null)
        {
            _wind.volume = Mathf.Min(0.40f, WindVol * (0.48f + 0.28f * Gust01) * (1f + 0.12f * tension) * (1f + 1.35f * storm)) * (1f - 0.55f * _winMix);
            _wind.panStereo = Mathf.Sin(t * 0.07f) * 0.30f;
            _wind.pitch = 0.96f + 0.10f * storm;
        }
        if (_howl != null)
        {
            _howl.volume = Mathf.Min(0.34f, HowlVol * (0.36f + 0.40f * Gust01) * (1f + 0.14f * tension) * (1f + 1.7f * storm)) * duck;
            _howl.panStereo = Mathf.Sin(t * 0.045f + 1.1f) * 0.46f;
            _howl.pitch = 0.94f + 0.12f * storm;
        }
        if (_rustle != null)
        {
            float rustle = RustleVol * (0.36f + 0.42f * Gust01) * (1f + 0.40f * _move) * (1f + 1.55f * storm);
            _rustle.volume = Mathf.Min(0.40f, rustle) * (1f - 0.40f * _winMix);
            _rustle.pitch = 1f + 0.028f * _move + 0.06f * storm;
        }
        if (_brush != null)
        {
            _brush.volume = Mathf.Min(0.30f, BrushVol * (_move + 0.35f * storm) * (0.62f + 0.38f * Gust01)) * (1f - 0.35f * _winMix);
            _brush.pitch = 0.97f + 0.08f * _move + 0.05f * storm;
        }
        if (_win != null)
        {
            _win.volume = 0.42f * _winMix;
            if (WinThemePlaying && !_win.isPlaying)
                _win.Play();
        }
    }

    float Tension()
    {
        if (_listenerFollow == null) return 0f;
        var pos = _listenerFollow.position;
        float gold = 1f - Mathf.Clamp01(Vector3.Distance(pos, _goldWorld) / 14f);
        float ends = 0f;
        if (_deadEnds != null)
        {
            for (int i = 0; i < _deadEnds.Length; i++)
                ends = Mathf.Max(ends, 0.7f * (1f - Mathf.Clamp01(Vector3.Distance(pos, _deadEnds[i]) / 7f)));
        }
        return Mathf.Max(gold, ends);
    }

    AudioSource MakeSource(string name, float volume, float spatial, bool loop, int priority)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = loop;
        src.mute = false;
        src.volume = volume;
        src.spatialBlend = spatial;
        src.dopplerLevel = 0f;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = 2f;
        src.maxDistance = 22f;
        src.priority = priority;
        return src;
    }

    static Vector3[] FindDeadEnds(MazeData maze)
    {
        var list = new List<Vector3>(16);
        var steps = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        for (int x = 0; x < maze.Width; x++)
        {
            for (int y = 0; y < maze.Height; y++)
            {
                if (maze.IsWall[x, y]) continue;
                int n = 0;
                for (int i = 0; i < steps.Length; i++)
                {
                    int nx = x + steps[i].x;
                    int ny = y + steps[i].y;
                    if (maze.InBounds(nx, ny) && !maze.IsWall[nx, ny])
                        n++;
                }
                if (n == 1)
                    list.Add(maze.CellToWorld(x, y));
            }
        }
        return list.ToArray();
    }
}

static class MazeMoodSynth
{
    const int Rate = 22050;

    public static AudioClip AnxiousDrama(float seconds)
    {
        int frames = Mathf.RoundToInt(seconds * Rate);
        var data = new float[frames * 2];
        var rng = new Rng(1661);

        // Frequencies chosen so freq * seconds is an integer — the 16s loop meets itself.
        const float D2 = 73.4375f;
        const float A1 = 55f;
        const float A2 = 110f;
        const float F2 = 87.5f;
        const float D3 = 146.875f;
        const float F3 = 174.375f;
        const float Ab3 = 207.5f;
        const float A3 = 220f;
        const float C4 = 261.25f;
        const float E4 = 329.375f;

        for (int i = 0; i < frames; i++)
        {
            float t = i / (float)Rate;
            float u = t / seconds;

            // Anxious pulse (faster than a restful heartbeat) plus a rise that
            // returns to zero at the loop point so the seam stays quiet.
            float heart = 0.66f + 0.34f * Pulse(Mathf.Sin(2f * Mathf.PI * 1.25f * t));
            float breathe = 0.86f + 0.14f * Mathf.Sin(2f * Mathf.PI * u);
            float rise = 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * u);

            float thudEnv = Pulse(Mathf.Sin(2f * Mathf.PI * 1.25f * t));
            float thud = (Organ(t, A1, 0.30f) + Organ(t, D2, 0.14f)) * thudEnv * 0.22f;

            float organ = 0f;
            organ += Organ(t, D2, 0.22f);
            organ += Organ(t, A2, 0.14f);
            organ += Organ(t, F3, 0.09f);
            organ += Organ(t, A1, 0.12f);
            organ *= 0.24f * breathe * heart * (0.90f + 0.16f * rise);

            float strings = 0f;
            float stringEnv = Swell(t, 2.4f, 8.0f, 2.0f, 3.2f);
            strings += SoftSaw(t, D3, 0.16f, 0.003f);
            strings += SoftSaw(t, A3, 0.11f, -0.002f);
            strings += SoftSaw(t, F3, 0.10f, 0.004f);
            strings += SoftSaw(t, Ab3, 0.07f, 0.0015f);
            strings *= 0.22f * stringEnv * (0.82f + 0.28f * rise);

            float choir = 0f;
            float choirEnv = Swell(t, 6.0f, 5.5f, 1.8f, 2.4f);
            choir += ChoirPartial(t, D2, 0.18f);
            choir += ChoirPartial(t, F2, 0.12f);
            choir += ChoirPartial(t, Ab3, 0.08f);
            choir += ChoirPartial(t, D3, 0.07f);
            choir *= 0.15f * choirEnv;

            float brass = SoftSaw(t, A1, 0.22f, 0.001f) + Organ(t, D2, 0.12f);
            brass *= 0.11f * Swell(t, 8.2f, 4.0f, 1.6f, 2.0f);

            float ghost = Mathf.Sin(2f * Mathf.PI * E4 * t) * 0.032f * Swell(t, 3.1f, 1.6f, 0.4f, 0.9f);
            ghost += Mathf.Sin(2f * Mathf.PI * C4 * t) * 0.024f * Swell(t, 10.6f, 1.8f, 0.35f, 0.8f);

            // Original dissonance (E against F), a little more present as tension rises.
            float cluster = (Mathf.Sin(2f * Mathf.PI * E4 * 0.5f * t) + Mathf.Sin(2f * Mathf.PI * F3 * t)) * 0.5f;
            cluster *= 0.048f * Swell(t, 7.0f, 1.4f, 0.25f, 0.55f) * (0.7f + 0.55f * rise);

            float piano = 0f;
            piano += Piano(t, 1.00f, D2, 0.50f);
            piano += Piano(t, 3.00f, D2, 0.34f);
            piano += Piano(t, 5.00f, A2, 0.44f);
            piano += Piano(t, 7.00f, F2, 0.32f);
            piano += Piano(t, 9.00f, D2, 0.46f);
            piano += Piano(t, 11.00f, Ab3 * 0.5f, 0.26f);
            piano += Piano(t, 13.00f, A1, 0.40f);
            piano += Piano(t, 4.50f, A3, 0.14f);
            piano += Piano(t, 10.25f, C4, 0.10f);

            float climb = SoftSaw(t, A3, 0.09f, 0.002f) * rise * 0.09f;

            float mono = organ + thud + strings + choir + brass + ghost + cluster + piano + climb;
            float width = SoftSaw(t, D3 * 1.003f, 0.05f, 0f) * stringEnv * 0.12f;
            float air = (rng.NextSigned() * 0.0035f) * (0.55f + 0.45f * stringEnv);

            data[i * 2] = Soft(mono + width * 0.6f + air);
            data[i * 2 + 1] = Soft(mono - width * 0.6f - air * 0.7f);
        }

        CrossfadeStereo(data, frames, Mathf.RoundToInt(0.12f * Rate));
        return Clip("AnxiousDrama", data, frames, 2);
    }

    /// <summary>
    /// Periodic high-string shrieks: short saw/triangle/noise bursts in irregular
    /// clusters (tense, stab, pause) — original, not a film cue.
    /// </summary>
    public static AudioClip HuskChase(float seconds)
    {
        int frames = Mathf.RoundToInt(seconds * Rate);
        var data = new float[frames * 2];
        var rng = new Rng(1972);

        var onsets = new List<float>(24);
        var freqA = new List<float>(24);
        var freqB = new List<float>(24);
        var freqC = new List<float>(24);
        var amps = new List<float>(24);
        var durs = new List<float>(24);
        var pans = new List<float>(24);

        // High dissonant pairs (minor seconds / close clusters), not a borrowed theme.
        float[] pool =
        {
            1174.7f, 1244.5f, 1318.5f, 1396.9f, 1480f,
            1568f, 1661.2f, 1760f, 1864.7f, 1975.5f, 2093f, 2217.5f
        };

        float[] clusterAt = { 0.62f, 2.38f, 3.92f, 6.48f, 8.86f, 10.78f, 12.98f };
        int[] nStabs = { 4, 2, 5, 3, 1, 4, 2 };

        for (int c = 0; c < clusterAt.Length; c++)
        {
            float t0 = clusterAt[c];
            float gap = 0.072f + (float)rng.NextDouble() * 0.058f;
            int root = (int)((float)rng.NextDouble() * (pool.Length - 3));
            root = Mathf.Clamp(root, 0, pool.Length - 3);
            bool triple = rng.NextDouble() < 0.45;
            for (int s = 0; s < nStabs[c]; s++)
            {
                float jitter = ((float)rng.NextDouble() - 0.5f) * 0.028f;
                onsets.Add(t0 + s * gap + jitter);
                freqA.Add(pool[root]);
                freqB.Add(pool[root + 1]);
                freqC.Add(triple ? pool[root + 2] : 0f);
                amps.Add(0.72f + (float)rng.NextDouble() * 0.28f);
                durs.Add(0.11f + (float)rng.NextDouble() * 0.16f);
                pans.Add(((float)rng.NextDouble() - 0.5f) * 0.72f);
            }
        }

        for (int i = 0; i < frames; i++)
        {
            float t = i / (float)Rate;
            float left = 0f;
            float right = 0f;

            for (int s = 0; s < onsets.Count; s++)
            {
                float age = t - onsets[s];
                if (age < 0f || age > durs[s]) continue;

                const float attack = 0.007f;
                float env = age < attack
                    ? age / attack
                    : Mathf.Exp(-16f * (age - attack));
                env *= env;

                float scoop = 1f + 0.038f * Mathf.Exp(-38f * age);
                float tone = 0f;
                tone += BrightSaw(t, freqA[s] * scoop) * 0.55f;
                tone += Triangleish(t, freqB[s] * scoop) * 0.38f;
                if (freqC[s] > 1f)
                    tone += BrightSaw(t, freqC[s] * scoop) * 0.22f;

                float scrape = 0f;
                if (age < 0.028f)
                    scrape = rng.NextSigned() * (1f - age / 0.028f) * 0.22f;

                float sample = (tone + scrape) * env * amps[s] * 0.22f;
                float pan = pans[s];
                float gL = Mathf.Sqrt(Mathf.Clamp01(0.5f - pan * 0.5f));
                float gR = Mathf.Sqrt(Mathf.Clamp01(0.5f + pan * 0.5f));
                left += sample * gL;
                right += sample * gR;
            }

            data[i * 2] = Soft(left);
            data[i * 2 + 1] = Soft(right);
        }

        CrossfadeStereo(data, frames, Mathf.RoundToInt(0.08f * Rate));
        PeakSoft(data, 0.68f);
        return Clip("HuskChase", data, frames, 2);
    }

    public static AudioClip Wind(float seconds)
    {
        int extra = Mathf.RoundToInt(0.35f * Rate);
        int frames = Mathf.RoundToInt(seconds * Rate);
        int total = frames + extra;
        var left = new float[total];
        var right = new float[total];
        var rng = new Rng(90210);
        float brownL = 0f, brownR = 0f;
        float lpL = 0f, lpR = 0f;
        float prevLpL = 0f, prevLpR = 0f;
        float hpL = 0f, hpR = 0f;
        float pink0 = 0f, pink1 = 0f, pink2 = 0f;
        float resL1 = 0f, resL2 = 0f;
        float resR1 = 0f, resR2 = 0f;

        for (int i = 0; i < total; i++)
        {
            float t = i / (float)Rate;
            float u = (t % seconds) / seconds;
            float gust = 0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * u) * Mathf.Sin(4f * Mathf.PI * u + 0.7f);
            float cut = 300f + 1050f * (0.45f + 0.55f * Mathf.Sin(2f * Mathf.PI * u + 0.4f)) * gust;

            float whiteL = rng.NextSigned();
            float whiteR = rng.NextSigned() * 0.62f + whiteL * 0.38f;
            brownL += (whiteL - brownL) * 0.017f;
            brownR += (whiteR - brownR) * 0.015f;
            pink0 = 0.99765f * pink0 + whiteL * 0.0990460f;
            pink1 = 0.96300f * pink1 + whiteL * 0.2965164f;
            pink2 = 0.57000f * pink2 + whiteL * 1.0526913f;
            float pink = (pink0 + pink1 + pink2 + whiteL * 0.1848f) * 0.11f;

            float inL = brownL * 0.78f + pink * 0.22f;
            float inR = brownR * 0.78f + pink * 0.18f + whiteR * 0.035f;

            float a = Mathf.Exp(-2f * Mathf.PI * cut / Rate);
            lpL = (1f - a) * inL + a * lpL;
            lpR = (1f - a) * inR + a * lpR;

            float ah = Mathf.Exp(-2f * Mathf.PI * 95f / Rate);
            hpL = ah * (hpL + lpL - prevLpL);
            hpR = ah * (hpR + lpR - prevLpR);
            prevLpL = lpL;
            prevLpR = lpR;

            // Hollow moan riding the same gust: slow band-pass sweep, not extra hiss.
            float moanF = 175f + 155f * Mathf.Sin(2f * Mathf.PI * u) + 55f * Mathf.Sin(2f * Mathf.PI * 0.37f * t);
            float howlL = Reson(hpL, moanF, 0.978f, ref resL1, ref resL2);
            float howlR = Reson(hpR, moanF * 1.03f, 0.976f, ref resR1, ref resR2);

            float env = 0.40f + 0.60f * gust;
            left[i] = (hpL * 0.78f + howlL * 0.48f) * env * 1.05f;
            right[i] = (hpR * 0.78f + howlR * 0.48f) * env * 1.05f;
        }

        var data = new float[frames * 2];
        LoopCross(left, right, data, frames, extra);
        return Clip("Wind", data, frames, 2);
    }

    public static AudioClip HollowHowl(float seconds)
    {
        int extra = Mathf.RoundToInt(0.40f * Rate);
        int frames = Mathf.RoundToInt(seconds * Rate);
        int total = frames + extra;
        var left = new float[total];
        var right = new float[total];
        var rng = new Rng(3311);
        float brown = 0f;
        float resA1 = 0f, resA2 = 0f;
        float resB1 = 0f, resB2 = 0f;
        float resC1 = 0f, resC2 = 0f;
        float resD1 = 0f, resD2 = 0f;
        float phase = 0f;

        for (int i = 0; i < total; i++)
        {
            float t = i / (float)Rate;
            float u = (t % seconds) / seconds;
            float sweep = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * u);
            float sweep2 = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * u * 0.5f + 1.7f);
            float moanF = 145f + 210f * sweep + 70f * sweep2;
            float hollowF = moanF * 2.08f;
            float tunnel = 0.55f + 0.45f * sweep;

            float white = rng.NextSigned();
            brown += (white - brown) * 0.022f;

            float bodyL = Reson(brown + white * 0.08f, moanF, 0.986f, ref resA1, ref resA2);
            float bodyR = Reson(brown + white * 0.08f, moanF * 1.018f, 0.984f, ref resB1, ref resB2);
            float airL = Reson(white * 0.35f + brown * 0.4f, hollowF, 0.972f, ref resC1, ref resC2);
            float airR = Reson(white * 0.35f + brown * 0.4f, hollowF * 0.97f, 0.970f, ref resD1, ref resD2);

            phase += 2f * Mathf.PI * moanF / Rate;
            if (phase > 2f * Mathf.PI) phase -= 2f * Mathf.PI;
            float voice = Mathf.Sin(phase) * 0.10f * tunnel;

            left[i] = (bodyL * 0.70f + airL * 0.28f + voice) * 0.85f;
            right[i] = (bodyR * 0.70f + airR * 0.28f + voice * 0.82f) * 0.85f;
        }

        var data = new float[frames * 2];
        LoopCross(left, right, data, frames, extra);
        return Clip("HollowHowl", data, frames, 2);
    }

    public static AudioClip CornRustle(float seconds)
    {
        int extra = Mathf.RoundToInt(0.30f * Rate);
        int frames = Mathf.RoundToInt(seconds * Rate);
        int total = frames + extra;
        var rawL = new float[total];
        var rawR = new float[total];
        var rng = new Rng(441);
        float lpL = 0f, hpL = 0f, prevL = 0f;
        float lpR = 0f, hpR = 0f, prevR = 0f;
        float paperyL = 0f, paperyR = 0f;

        const int grains = 86;
        var on = new float[grains];
        var dur = new float[grains];
        var peak = new float[grains];
        var lo = new float[grains];
        var hi = new float[grains];
        var pan = new float[grains];
        var kind = new int[grains];

        for (int g = 0; g < grains; g++)
        {
            on[g] = (float)rng.NextDouble() * seconds;
            float roll = (float)rng.NextDouble();
            if (roll < 0.22f)
            {
                // Longer leafy gust / husk sheet.
                kind[g] = 1;
                dur[g] = 0.20f + (float)rng.NextDouble() * 0.28f;
                peak[g] = 0.50f + (float)rng.NextDouble() * 0.38f;
                lo[g] = 700f + (float)rng.NextDouble() * 500f;
                hi[g] = 1600f + (float)rng.NextDouble() * 1400f;
            }
            else if (roll < 0.40f)
            {
                // Mid stalk rub.
                kind[g] = 2;
                dur[g] = 0.07f + (float)rng.NextDouble() * 0.11f;
                peak[g] = 0.28f + (float)rng.NextDouble() * 0.32f;
                lo[g] = 450f + (float)rng.NextDouble() * 350f;
                hi[g] = 1400f + (float)rng.NextDouble() * 900f;
            }
            else
            {
                // Dry husk tick / papery crackle.
                kind[g] = 0;
                dur[g] = 0.018f + (float)rng.NextDouble() * 0.055f;
                peak[g] = 0.20f + (float)rng.NextDouble() * 0.42f;
                lo[g] = 1400f + (float)rng.NextDouble() * 900f;
                hi[g] = 2800f + (float)rng.NextDouble() * 2200f;
            }
            pan[g] = ((float)rng.NextDouble() - 0.5f) * 1.15f;
        }

        for (int i = 0; i < total; i++)
        {
            float t = i / (float)Rate;
            float u = (t % seconds) / seconds;
            float gust = 0.40f + 0.60f * Mathf.Max(0f, Mathf.Sin(2f * Mathf.PI * u) * Mathf.Sin(4f * Mathf.PI * u + 0.5f));

            float envL = 0f, envR = 0f;
            float cutL = 2600f, cutR = 2600f;
            float hipL = 800f, hipR = 800f;

            for (int g = 0; g < grains; g++)
            {
                float age = t - on[g];
                if (age < 0f) age += seconds;
                if (age > seconds * 0.5f) continue;
                if (age > dur[g]) continue;

                float x = age / dur[g];
                float atk = kind[g] == 0 ? 0.10f : 0.20f;
                float gEnv = x < atk ? x / atk : 1f - (x - atk) / (1f - atk);
                gEnv = Mathf.Clamp01(gEnv);
                gEnv *= gEnv;
                if (kind[g] == 1)
                    gEnv *= 0.65f + 0.55f * gust;

                float amp = gEnv * peak[g];
                float gL = Mathf.Sqrt(Mathf.Clamp01(0.5f - pan[g] * 0.5f));
                float gR = Mathf.Sqrt(Mathf.Clamp01(0.5f + pan[g] * 0.5f));
                float aL = amp * gL;
                float aR = amp * gR;
                if (aL > envL)
                {
                    envL = aL;
                    cutL = hi[g];
                    hipL = lo[g];
                }
                else
                    envL += aL * 0.22f;
                if (aR > envR)
                {
                    envR = aR;
                    cutR = hi[g];
                    hipR = lo[g];
                }
                else
                    envR += aR * 0.22f;
            }

            float whiteL = rng.NextSigned();
            float whiteR = rng.NextSigned() * 0.55f + whiteL * 0.45f;

            float aLo = Mathf.Exp(-2f * Mathf.PI * cutL / Rate);
            float aRo = Mathf.Exp(-2f * Mathf.PI * cutR / Rate);
            lpL = (1f - aLo) * whiteL + aLo * lpL;
            lpR = (1f - aRo) * whiteR + aRo * lpR;

            float ahL = Mathf.Exp(-2f * Mathf.PI * hipL / Rate);
            float ahR = Mathf.Exp(-2f * Mathf.PI * hipR / Rate);
            hpL = ahL * (hpL + lpL - prevL);
            hpR = ahR * (hpR + lpR - prevR);
            prevL = lpL;
            prevR = lpR;

            // Quiet papery bed — dry, not hissy rain.
            paperyL += (whiteL - paperyL) * 0.08f;
            paperyR += (whiteR - paperyR) * 0.075f;
            float bedL = (hpL - paperyL) * (0.06f + 0.10f * gust);
            float bedR = (hpR - paperyR) * (0.06f + 0.10f * gust);

            float huskTick = 0f;
            if (rng.NextDouble() < 0.0028 + 0.0035 * gust)
                huskTick = rng.NextSigned() * 0.22f;

            rawL[i] = hpL * (0.10f + envL * 0.95f) + bedL + huskTick * 0.10f;
            rawR[i] = hpR * (0.10f + envR * 0.95f) + bedR + huskTick * 0.07f;
        }

        var data = new float[frames * 2];
        LoopCross(rawL, rawR, data, frames, extra);
        return Clip("CornRustle", data, frames, 2);
    }

    /// <summary>
    /// Original celebratory pit-band loop: bright brass, walking bass, major-key
    /// "struck gold" energy. 1930s showtune gesture only — not a reconstruction
    /// of any copyrighted song or recording.
    /// 6.4s = four bars at 150 BPM so the loop meets itself.
    /// </summary>
    public static AudioClip GoldStrike(float seconds)
    {
        int frames = Mathf.RoundToInt(seconds * (float)Rate);
        var data = new float[frames * 2];
        var rng = new Rng(1930);

        // Frequencies chosen so freq * seconds is an integer (seamless 6.4s loop).
        const float Bb1 = 58.125f;
        const float F2 = 87.5f;
        const float G2 = 98.125f;
        const float Bb2 = 116.25f;
        const float C3 = 130.625f;
        const float D3 = 146.875f;
        const float Eb3 = 155.625f;
        const float F3 = 174.375f;
        const float Bb3 = 233.125f;
        const float C4 = 261.25f;
        const float D4 = 293.75f;
        const float Eb4 = 311.25f;
        const float F4 = 348.125f;
        const float G4 = 392.5f;
        const float Bb4 = 466.25f;

        float[] bass =
        {
            Bb2, D3, F2, G2,
            Bb2, F3, Eb3, F2,
            G2, Bb2, C3, D3,
            Eb3, F2, F2, Bb1
        };

        float[] lead =
        {
            Bb3, F4, Bb4, F4,
            D4, F4, D4, Bb3,
            C4, D4, Eb4, F4,
            F4, D4, Bb3, F3
        };

        float beatSec = seconds / 16f;

        for (int i = 0; i < frames; i++)
        {
            float t = i / (float)Rate;
            float u = t / seconds;
            int q = Mathf.Clamp((int)(t / beatSec), 0, 15);
            float age = t - (float)q * beatSec;
            float noteEnv = Mathf.Min(1f, age / 0.018f) * Mathf.Exp(-1.15f * age);
            float swing = (q % 2 == 1) ? 0.92f : 1f;

            float walk = Organ(t, bass[q], 0.42f) * noteEnv * swing * 0.34f;
            walk += Organ(t, bass[q] * 0.5f, 0.18f) * noteEnv * 0.16f;

            float brass = 0f;
            brass += SoftSaw(t, lead[q], 0.22f, 0.0012f);
            brass += SoftSaw(t, lead[q] * 0.5f, 0.12f, -0.0008f);
            brass += Organ(t, lead[q] * 2f, 0.06f);
            brass *= noteEnv * 0.38f;

            float stab = 0f;
            if (q % 4 == 0)
            {
                float root = q == 0 ? Bb3 : (q == 4 ? F3 : (q == 8 ? Eb3 : Bb3));
                float fifth = q == 4 ? C4 : (q == 8 ? Bb3 : F4);
                float sEnv = Mathf.Exp(-4.8f * age) * Mathf.Min(1f, age / 0.012f);
                stab = (SoftSaw(t, root, 0.20f, 0.002f) + Organ(t, fifth, 0.12f) + Organ(t, root * 0.5f, 0.10f)) * sEnv * 0.42f;
            }

            float oomPah = 0f;
            if (q % 2 == 0)
                oomPah = Organ(t, bass[q], 0.16f) * noteEnv * 0.10f;
            else
                oomPah = (Organ(t, F3, 0.10f) + Organ(t, Bb3, 0.08f)) * noteEnv * 0.08f;

            float hat = 0f;
            if (q % 2 == 1 && age < 0.06f)
                hat = rng.NextSigned() * (1f - age / 0.06f) * 0.045f;

            float crash = 0f;
            if (q == 0 && age < 0.22f)
                crash = rng.NextSigned() * Mathf.Exp(-14f * age) * 0.10f;

            float shine = Mathf.Sin(2f * Mathf.PI * G4 * t) * 0.018f * Swell(t, 0.05f, 0.55f, 0.04f, 0.35f);
            shine += Mathf.Sin(2f * Mathf.PI * D4 * t) * 0.014f * Swell(t, 3.25f, 0.40f, 0.04f, 0.28f);

            float lift = 0.88f + 0.12f * Mathf.Sin(2f * Mathf.PI * u);
            float mono = (walk + brass + stab + oomPah + hat + crash + shine) * lift;
            float width = SoftSaw(t, lead[q] * 1.004f, 0.06f, 0f) * noteEnv * 0.16f;
            float air = rng.NextSigned() * 0.004f;

            data[i * 2] = Soft(mono + width + air);
            data[i * 2 + 1] = Soft(mono - width * 0.85f - air * 0.6f);
        }

        CrossfadeStereo(data, frames, Mathf.RoundToInt(0.06f * (float)Rate));
        PeakSoft(data, 0.72f);
        return Clip("GoldStrike", data, frames, 2);
    }

    public static AudioClip CornBrush(float seconds)
    {
        int extra = Mathf.RoundToInt(0.24f * Rate);
        int frames = Mathf.RoundToInt(seconds * Rate);
        int total = frames + extra;
        var rawL = new float[total];
        var rawR = new float[total];
        var rng = new Rng(778);
        float lpL = 0f, hpL = 0f, prevL = 0f;
        float lpR = 0f, hpR = 0f, prevR = 0f;

        const int grains = 64;
        var on = new float[grains];
        var dur = new float[grains];
        var peak = new float[grains];
        var cut = new float[grains];
        var hip = new float[grains];
        var pan = new float[grains];

        for (int g = 0; g < grains; g++)
        {
            on[g] = (float)rng.NextDouble() * seconds;
            dur[g] = 0.022f + (float)rng.NextDouble() * 0.070f;
            peak[g] = 0.30f + (float)rng.NextDouble() * 0.50f;
            cut[g] = 1800f + (float)rng.NextDouble() * 2000f;
            hip[g] = 550f + (float)rng.NextDouble() * 500f;
            pan[g] = ((float)rng.NextDouble() - 0.5f) * 0.85f;
        }

        for (int i = 0; i < total; i++)
        {
            float t = i / (float)Rate;
            float envL = 0f, envR = 0f;
            float cutL = 2200f, cutR = 2200f;
            float hipL = 700f, hipR = 700f;

            for (int g = 0; g < grains; g++)
            {
                float age = t - on[g];
                if (age < 0f) age += seconds;
                if (age > dur[g]) continue;
                float x = age / dur[g];
                float gEnv = x < 0.12f ? x / 0.12f : 1f - (x - 0.12f) / 0.88f;
                gEnv = Mathf.Clamp01(gEnv);
                gEnv *= gEnv;
                float amp = gEnv * peak[g];
                float gL = Mathf.Sqrt(Mathf.Clamp01(0.5f - pan[g] * 0.5f));
                float gR = Mathf.Sqrt(Mathf.Clamp01(0.5f + pan[g] * 0.5f));
                if (amp * gL > envL)
                {
                    envL = amp * gL;
                    cutL = cut[g];
                    hipL = hip[g];
                }
                if (amp * gR > envR)
                {
                    envR = amp * gR;
                    cutR = cut[g];
                    hipR = hip[g];
                }
            }

            float whiteL = rng.NextSigned();
            float whiteR = rng.NextSigned() * 0.5f + whiteL * 0.5f;
            float aLo = Mathf.Exp(-2f * Mathf.PI * cutL / Rate);
            float aRo = Mathf.Exp(-2f * Mathf.PI * cutR / Rate);
            lpL = (1f - aLo) * whiteL + aLo * lpL;
            lpR = (1f - aRo) * whiteR + aRo * lpR;
            float ahL = Mathf.Exp(-2f * Mathf.PI * hipL / Rate);
            float ahR = Mathf.Exp(-2f * Mathf.PI * hipR / Rate);
            hpL = ahL * (hpL + lpL - prevL);
            hpR = ahR * (hpR + lpR - prevR);
            prevL = lpL;
            prevR = lpR;

            rawL[i] = hpL * (0.08f + envL * 1.05f);
            rawR[i] = hpR * (0.08f + envR * 1.05f);
        }

        var data = new float[frames * 2];
        LoopCross(rawL, rawR, data, frames, extra);
        return Clip("CornBrush", data, frames, 2);
    }

    static float Organ(float t, float freq, float amp)
    {
        float s = 0f;
        s += Mathf.Sin(2f * Mathf.PI * freq * t);
        s += 0.45f * Mathf.Sin(2f * Mathf.PI * freq * 2f * t);
        s += 0.22f * Mathf.Sin(2f * Mathf.PI * freq * 3f * t);
        s += 0.10f * Mathf.Sin(2f * Mathf.PI * freq * 4f * t);
        s += 0.06f * Mathf.Sin(2f * Mathf.PI * freq * 6f * t);
        return s * amp;
    }

    static float SoftSaw(float t, float freq, float amp, float detune)
    {
        float f = freq * (1f + detune);
        float s = 0f;
        s += Mathf.Sin(2f * Mathf.PI * f * t);
        s += 0.50f * Mathf.Sin(2f * Mathf.PI * f * 2f * t);
        s += 0.28f * Mathf.Sin(2f * Mathf.PI * f * 3f * t);
        s += 0.16f * Mathf.Sin(2f * Mathf.PI * f * 4f * t);
        s += 0.09f * Mathf.Sin(2f * Mathf.PI * f * 5f * t);
        return s * amp;
    }

    static float BrightSaw(float t, float freq)
    {
        float s = 0f;
        s += Mathf.Sin(2f * Mathf.PI * freq * t);
        s += 0.62f * Mathf.Sin(2f * Mathf.PI * freq * 2f * t);
        s += 0.40f * Mathf.Sin(2f * Mathf.PI * freq * 3f * t);
        s += 0.26f * Mathf.Sin(2f * Mathf.PI * freq * 4f * t);
        s += 0.16f * Mathf.Sin(2f * Mathf.PI * freq * 5f * t);
        s += 0.10f * Mathf.Sin(2f * Mathf.PI * freq * 7f * t);
        return s;
    }

    static float Triangleish(float t, float freq)
    {
        float s = Mathf.Sin(2f * Mathf.PI * freq * t);
        s += 0.111f * Mathf.Sin(2f * Mathf.PI * freq * 3f * t);
        s += 0.040f * Mathf.Sin(2f * Mathf.PI * freq * 5f * t);
        s += 0.020f * Mathf.Sin(2f * Mathf.PI * freq * 7f * t);
        return s;
    }

    static float ChoirPartial(float t, float freq, float amp)
    {
        float s = Mathf.Sin(2f * Mathf.PI * freq * t);
        s += 0.35f * Mathf.Sin(2f * Mathf.PI * freq * 2.01f * t);
        s += 0.18f * Mathf.Sin(2f * Mathf.PI * (freq + 700f) * 0.15f * t) * 0.15f;
        return s * amp;
    }

    static float Piano(float t, float onset, float freq, float amp)
    {
        float age = t - onset;
        if (age < 0f || age > 1.8f) return 0f;
        float attack = 1f - Mathf.Exp(-90f * age);
        float body = Mathf.Exp(-3.1f * age);
        float tone = Mathf.Sin(2f * Mathf.PI * freq * t)
                   + 0.32f * Mathf.Sin(2f * Mathf.PI * freq * 2f * t)
                   + 0.10f * Mathf.Sin(2f * Mathf.PI * freq * 3f * t);
        float hammer = Mathf.Exp(-70f * age) * 0.12f * Mathf.Sin(2f * Mathf.PI * freq * 7f * t);
        return (tone * attack * body + hammer) * amp * 0.34f;
    }

    static float Reson(float x, float freq, float radius, ref float y1, ref float y2)
    {
        float f = Mathf.Clamp(freq, 40f, 6000f);
        float r = Mathf.Clamp(radius, 0.1f, 0.994f);
        float w = 2f * Mathf.PI * f / Rate;
        float y = (1f - r) * x + 2f * r * Mathf.Cos(w) * y1 - r * r * y2;
        y2 = y1;
        y1 = y;
        return y;
    }

    static float Swell(float t, float start, float hold, float attack, float release)
    {
        if (t < start) return 0f;
        float age = t - start;
        if (age < attack) return Smooth(age / attack);
        if (age < attack + hold) return 1f;
        float rel = age - attack - hold;
        if (rel >= release) return 0f;
        return 1f - Smooth(rel / release);
    }

    static float Pulse(float s)
    {
        float x = Mathf.Max(0f, s);
        return x * x * x * x;
    }

    static float Smooth(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * (3f - 2f * x);
    }

    static float Soft(float x)
    {
        if (x > 1f) return 1f;
        if (x < -1f) return -1f;
        return x - x * x * x * 0.12f;
    }

    static void CrossfadeStereo(float[] data, int frames, int fade)
    {
        fade = Mathf.Clamp(fade, 8, frames / 8);
        for (int i = 0; i < fade; i++)
        {
            float w = i / (float)fade;
            int a = i * 2;
            int b = (frames - fade + i) * 2;
            data[a] = data[a] * w + data[b] * (1f - w);
            data[a + 1] = data[a + 1] * w + data[b + 1] * (1f - w);
        }
    }

    static void LoopCross(float[] l, float[] r, float[] stereo, int frames, int extra)
    {
        for (int i = 0; i < frames; i++)
        {
            stereo[i * 2] = l[i];
            stereo[i * 2 + 1] = r[i];
        }
        for (int i = 0; i < extra; i++)
        {
            float w = i / (float)extra;
            stereo[i * 2] = l[i] * w + l[frames + i] * (1f - w);
            stereo[i * 2 + 1] = r[i] * w + r[frames + i] * (1f - w);
        }
        PeakSoft(stereo, 0.70f);
    }

    static void PeakSoft(float[] data, float target)
    {
        float peak = 0.0001f;
        for (int i = 0; i < data.Length; i++)
            peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        float gain = target / peak;
        for (int i = 0; i < data.Length; i++)
            data[i] = Soft(data[i] * gain);
    }

    static AudioClip Clip(string name, float[] data, int frames, int channels)
    {
        var clip = AudioClip.Create(name, frames, channels, Rate, false);
        if (!clip.SetData(data, 0))
            Debug.LogWarning("MazeMoodSynth: SetData failed for " + name);
        clip.LoadAudioData();
        return clip;
    }

    struct Rng
    {
        uint _s;
        public Rng(int seed) { _s = (uint)seed * 747796405u + 2891336453u; }
        public float NextSigned() => (float)NextDouble() * 2f - 1f;
        public double NextDouble()
        {
            _s = _s * 1664525u + 1013904223u;
            return (_s >> 8) * (1.0 / 16777216.0);
        }
    }
}
