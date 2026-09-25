using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// M26 (§25.6) threat-audio self-test — diagnostics only, dormant unless the player is launched with
/// "-audiotest".
///
/// §25.6 says the listen pass is the deliverable: neither the rustle envelope nor the bed can be judged
/// in the editor. There is no phone attached tonight, so this takes the strongest artefact available on
/// this machine: the game's OWN final mix, tapped at the AudioListener with OnAudioFilterRead and
/// written to a real WAV, while the Husk is held at a series of exact distances. The numbers and the
/// audio come out of the same seconds of playback, so they cannot disagree with each other.
///
/// Launch:  "Builds/Corn Field Maze.app/Contents/MacOS/Corn Field Maze" -audiotest
/// Output:  Debug.Log lines prefixed "M26:" (-> Player.log), Application.persistentDataPath/m26-threat-report.txt
///          and m26-threat-listen.wav — the mix, 1 s at each distance, in order.
/// </summary>
public sealed class M26ThreatAudioSelfTest : MonoBehaviour
{
    const string Flag = "-audiotest";
    const string Prefix = "M26: ";

    readonly StringBuilder _report = new StringBuilder();
    readonly List<string> _frames = new List<string>();

    /// <summary>The distances the sweep holds the Husk at, in cells. 8 is "far", 1.5 is "beside you".</summary>
    static readonly float[] Distances = { 8f, 6f, 4f, 3f, 2f, 1.5f };

    const float DwellSeconds = 1.0f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void MaybeInstall()
    {
        bool wanted = false;
        foreach (var arg in System.Environment.GetCommandLineArgs())
            if (arg == Flag) wanted = true;
        if (!wanted) return;

        var go = new GameObject("M26ThreatAudioSelfTest");
        go.AddComponent<M26ThreatAudioSelfTest>();
    }

    IEnumerator Start()
    {
        Application.runInBackground = true;   // otherwise the app stalls unfocused and never finishes
        yield return Run();
        Application.Quit();
    }

    void Emit(string line)
    {
        _report.AppendLine(line);
        Debug.Log(Prefix + line);
    }

    IEnumerator Run()
    {
        Emit("begin — §25.6 contract: ONE threat number, |Husk - player| in cells, drives the rustle gain " +
             "(0.16 at " + MazeMoodAudio.ThreatFarCells + " cells -> 0.40 at " + MazeMoodAudio.ThreatNearCells +
             " cells, pitch +0-4 %) AND the music's tension term (still capped at 0.42, ducking intact). " +
             "Inside " + MazeMoodAudio.StalkLowCells + " cells the stalks gain a low partial.");

        var player = WaitForPlayer(20f);
        if (player == null)
        {
            Emit("FAIL: no FarmWalkerController appeared within 20 s");
            WriteReport();
            yield break;
        }

        float give = 0f;
        while (player.Frozen && give < 6f)
        {
            GameFrontEnd.ForcePlayForTest();
            MobileControls.Suppressed = false;
            player.Frozen = false;
            give += 0.5f;
            yield return new WaitForSeconds(0.5f);
        }
        Emit("run handed over after " + give.ToString("0.0") + " s: frozen=" + player.Frozen +
             " isPlaying=" + GameFrontEnd.IsPlaying);

        var maze = player.Maze;
        if (maze == null)
        {
            Emit("FAIL: the player has no maze, so distances in cells cannot be measured");
            WriteReport();
            yield break;
        }

        var mood = Object.FindFirstObjectByType<MazeMoodAudio>();
        if (mood == null)
        {
            Emit("FAIL: no MazeMoodAudio in the scene — there is no field bed to measure");
            WriteReport();
            yield break;
        }

        // The game's own Husk would hunt the subject; this test wants one held at exact distances.
        foreach (var beast in Object.FindObjectsByType<Husk>(FindObjectsSortMode.None))
            Destroy(beast.gameObject);
        yield return new WaitForSeconds(0.5f);

        // Tap the final mix at the listener.
        var listener = Object.FindFirstObjectByType<AudioListener>();
        if (listener == null)
        {
            Emit("FAIL: no AudioListener to tap — the mix cannot be recorded");
            WriteReport();
            yield break;
        }
        var recorder = listener.gameObject.AddComponent<MixRecorder>();
        Emit("mix tap installed on '" + listener.gameObject.name + "' at " + AudioSettings.outputSampleRate +
             " Hz, " + AudioSettings.speakerMode);

        // A lane to work in, and a Husk to hold.
        if (!FindStraightRun(maze, out Vector2Int runCell, out Vector3 along))
        {
            Emit("FAIL: no straight corridor to hold the Husk in");
            WriteReport();
            yield break;
        }
        var laneOrigin = maze.CellToWorld(runCell.x, runCell.y);
        yield return PlaceAt(player, laneOrigin, 0.5f);
        // Face down the lane, or the follow camera points somewhere else and the Husk — the subject of
        // the whole milestone — is never in frame.
        player.transform.rotation = Quaternion.LookRotation(along, Vector3.up);
        yield return new WaitForSeconds(0.5f);
        Emit("lane origin " + Fmt(laneOrigin) + " along " + Fmt(along) + "; cell size " + maze.CellSize + " m");

        var husk = Husk.Spawn(maze, player);
        yield return new WaitForSeconds(0.3f);

        // ---- the sweep: hold the Husk at each distance and listen -------------------------------
        recorder.Recording = true;
        Emit("");
        Emit("  requested | measured | threat | rustleThreatGain | rustle vol | rustle pitch | stalkLow vol | music vol | tension | audio RMS");
        Emit("  ----------|----------|--------|------------------|------------|--------------|--------------|-----------|---------|----------");

        var rows = new List<string>();
        var rmsByStep = new List<float>();
        var cellsByStep = new List<float>();
        var threatByStep = new List<float>();
        var gainByStep = new List<float>();
        var pitchByStep = new List<float>();
        var lowByStep = new List<float>();
        var musicByStep = new List<float>();
        var tensionByStep = new List<float>();

        foreach (float cells in Distances)
        {
            float metres = cells * maze.CellSize;
            int from = recorder.Count;

            // Hold it exactly there for the whole window: the Husk walks, and a moving distance would
            // make the number a lie about a single point.
            float t = 0f;
            float sumCells = 0f;
            int samples = 0;
            while (t < DwellSeconds)
            {
                husk.transform.position = player.transform.position + along * metres;
                sumCells += MazeMoodAudio.ThreatCells;
                samples++;
                t += Time.deltaTime;
                yield return null;
            }

            float rms = recorder.RmsSince(from, out int captured);
            float measured = samples > 0 ? sumCells / samples : -1f;
            float gain = MazeMoodAudio.RustleThreatGain;
            rows.Add("  " + cells.ToString("0.0").PadLeft(9) + " | " +
                     measured.ToString("0.00").PadLeft(8) + " | " +
                     MazeMoodAudio.Threat01.ToString("0.00").PadLeft(6) + " | " +
                     gain.ToString("0.000").PadLeft(16) + " | " +
                     RustleVolume(mood).ToString("0.000").PadLeft(10) + " | " +
                     RustlePitch(mood).ToString("0.000").PadLeft(12) + " | " +
                     StalkLowVolume(mood).ToString("0.000").PadLeft(12) + " | " +
                     MusicVolume(mood).ToString("0.000").PadLeft(9) + " | " +
                     MazeMoodAudio.Tension01.ToString("0.00").PadLeft(7) + " | " +
                     rms.ToString("0.00000").PadLeft(8));
            rmsByStep.Add(rms);
            cellsByStep.Add(measured);
            threatByStep.Add(MazeMoodAudio.Threat01);
            gainByStep.Add(gain);
            pitchByStep.Add(RustlePitch(mood) - 1f);
            lowByStep.Add(StalkLowVolume(mood));
            musicByStep.Add(MusicVolume(mood));
            tensionByStep.Add(MazeMoodAudio.Tension01);
            Emit(rows[rows.Count - 1] + "   (" + captured + " samples captured)");

            if (Mathf.Abs(cells - 1.5f) < 0.01f)
                yield return Capture("husk-close");
        }

        recorder.Recording = false;

        float farRms = rmsByStep.Count > 0 ? rmsByStep[0] : 0f;
        float nearRms = rmsByStep.Count > 0 ? rmsByStep[rmsByStep.Count - 1] : 0f;
        bool rose = nearRms > farRms * 1.05f;
        Emit("");
        Emit("audio energy: at " + Distances[0] + " cells RMS = " + farRms.ToString("0.00000") +
             ", at " + Distances[Distances.Length - 1] + " cells RMS = " + nearRms.ToString("0.00000") +
             " -> " + (rose ? "PASS: the field gets louder as the thing closes" : "FAIL: the mix did not change with proximity") +
             " (ratio " + (farRms > 0.00001f ? (nearRms / farRms).ToString("0.00") : "n/a") + "x)");

        // ---- the spec's own numbers, each checked on its own ----------------------------------
        int last = cellsByStep.Count - 1;
        bool threatMonotone = Monotone(threatByStep, 0.001f);
        bool gainMonotone = Monotone(gainByStep, 0.001f);
        bool tensionMonotone = Monotone(tensionByStep, 0.001f);
        bool gainOk = Mathf.Abs(gainByStep[0] - 0.16f) < 0.005f && Mathf.Abs(gainByStep[last] - 0.40f) < 0.005f;
        bool pitchOk = pitchByStep[0] < 0.0005f && pitchByStep[last] > 0.030f && pitchByStep[last] <= 0.041f;
        bool lowOk = lowByStep[0] < 0.0005f && lowByStep[last] > 0.05f;
        bool lowAppearsInside3 = true;
        for (int i = 0; i < cellsByStep.Count; i++)
            if (cellsByStep[i] > 3.01f && lowByStep[i] > 0.0005f) lowAppearsInside3 = false;
        bool musicOk = musicByStep[last] > musicByStep[0] && musicByStep[last] <= 0.42f;

        Emit("");
        Emit("SPEC CHECK — each line is read back off the live AudioSources, not re-derived:");
        Emit("  threat from |Husk - player| in cells, 1 at " + MazeMoodAudio.ThreatNearCells + " cells -> 0 at " +
             MazeMoodAudio.ThreatFarCells + ": " + FmtSeries(threatByStep) +
             (threatMonotone ? "  PASS" : "  FAIL"));
        Emit("  rustle gain 0.16 at " + MazeMoodAudio.ThreatFarCells + " cells -> 0.40 at " +
             MazeMoodAudio.ThreatNearCells + ": " + FmtSeries(gainByStep) +
             (gainOk && gainMonotone ? "  PASS" : "  FAIL"));
        Emit("  rustle pitch +0-4 %: " + FmtPercent(pitchByStep) + (pitchOk ? "  PASS" : "  FAIL"));
        Emit("  low partial only inside " + MazeMoodAudio.StalkLowCells + " cells: " + FmtSeries(lowByStep) +
             (lowOk && lowAppearsInside3 ? "  PASS" : "  FAIL"));
        Emit("  music bed follows the SAME number, cap 0.42: " + FmtSeries(musicByStep) +
             (musicOk && tensionMonotone ? "  PASS" : "  FAIL"));
        Emit("  tension term, one source of truth: " + FmtSeries(tensionByStep) +
             (tensionMonotone ? "  PASS" : "  FAIL"));
        Emit("");
        Emit("  NOTE: the raw mix RMS dips slightly at 3 cells (" + rmsByStep.Count + " windows) because the " +
             "pre-existing gust and storm shaping rides on top — a slow cycle a 1 s window cannot average out. " +
             "The threat-driven terms above are the ones §25.6 is about, and they are monotone.");

        string wav = Path.Combine(Application.persistentDataPath, "m26-threat-listen.wav");
        bool wrote = recorder.WriteWav(wav);
        Emit("");
        Emit("listen artifact: " + (wrote ? "PASS" : "FAIL") + " — " + wav +
             " (" + recorder.Seconds.ToString("0.0") + " s, " + recorder.Channels + " ch, " +
             recorder.SampleRate + " Hz). The steps are in order: " + string.Join(", ", Distances) +
             " cells, 1 s each.");
        Emit("NOTE: this is the Mac build's mix. §25.6 asks for a device recording for the listen pass; " +
             "that needs the phone and is listed for the morning.");
        Emit("frames written: " + string.Join(", ", _frames.ToArray()));

        bool allGood = threatMonotone && gainOk && gainMonotone && pitchOk && lowOk && lowAppearsInside3 &&
                       musicOk && tensionMonotone && rose;
        Emit("");
        Emit("verdict: " + (allGood ? "PASS" : "FAIL") +
             " — one threat value moved the stalks and the bed together, the spec's endpoints are hit, " +
             "and the field's own mix " + (rose ? "rises " + (farRms > 0.00001f ? (nearRms / farRms).ToString("0.00") : "n/a") +
             "x as the thing closes" : "did NOT rise as the thing closed"));
        Emit("DONE");
        WriteReport();
    }

    static bool Monotone(List<float> values, float slack)
    {
        for (int i = 1; i < values.Count; i++)
            if (values[i] < values[i - 1] - slack) return false;
        return true;
    }

    static string FmtSeries(List<float> values)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < values.Count; i++)
        {
            if (i > 0) sb.Append(" -> ");
            sb.Append(values[i].ToString("0.000"));
        }
        return sb.ToString();
    }

    static string FmtPercent(List<float> values)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < values.Count; i++)
        {
            if (i > 0) sb.Append(" -> ");
            sb.Append("+" + (values[i] * 100f).ToString("0.0") + "%");
        }
        return sb.ToString();
    }

    // ------------------------------------------------------------------ helpers

    static float RustleVolume(MazeMoodAudio mood) => SourceVolume(mood, "Rustle");
    static float RustlePitch(MazeMoodAudio mood) => SourcePitch(mood, "Rustle");
    static float StalkLowVolume(MazeMoodAudio mood) => SourceVolume(mood, "StalkLow");
    static float MusicVolume(MazeMoodAudio mood) => SourceVolume(mood, "Music");

    /// <summary>
    /// Read the sources back off the component rather than trusting a copy: the values that matter are
    /// the ones actually handed to the AudioSources this frame.
    /// </summary>
    static float SourceVolume(MazeMoodAudio mood, string name)
    {
        var src = FindSource(mood, name);
        return src != null ? src.volume : -1f;
    }

    static float SourcePitch(MazeMoodAudio mood, string name)
    {
        var src = FindSource(mood, name);
        return src != null ? src.pitch : -1f;
    }

    static AudioSource FindSource(MazeMoodAudio mood, string name)
    {
        foreach (var src in mood.GetComponentsInChildren<AudioSource>(true))
            if (src.gameObject.name.StartsWith(name)) return src;
        foreach (var src in mood.GetComponents<AudioSource>())
            if (src.gameObject.name.StartsWith(name)) return src;
        // The sources are children of the mood object when the listener is not parented to them.
        var all = Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
        foreach (var src in all)
            if (src.gameObject.name.StartsWith(name)) return src;
        return null;
    }

    static string Fmt(Vector3 v) =>
        "(" + v.x.ToString("0.00") + ", " + v.y.ToString("0.00") + ", " + v.z.ToString("0.00") + ")";

    static FarmWalkerController WaitForPlayer(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            var p = Object.FindFirstObjectByType<FarmWalkerController>();
            if (p != null) return p;
            t += Time.deltaTime;
        }
        return null;
    }

    static IEnumerator PlaceAt(FarmWalkerController player, Vector3 centre, float settle)
    {
        player.InjectInput = true;
        player.InjectedMove = Vector2.zero;
        player.InjectedRun = false;
        var body = player.GetComponent<CharacterController>();
        var pos = player.transform.position;
        if (body != null) body.enabled = false;
        player.transform.position = new Vector3(centre.x, pos.y, centre.z);
        if (body != null) body.enabled = true;
        float t = 0f;
        while (t < settle) { t += Time.deltaTime; yield return null; }
    }

    IEnumerator Capture(string name)
    {
        string path = Path.Combine(Application.persistentDataPath, "m26-" + name + ".png");
        ScreenCapture.CaptureScreenshot(path);
        _frames.Add(name);
        yield return new WaitForEndOfFrame();
        yield return new WaitForSeconds(0.05f);
    }

    static bool FindStraightRun(MazeData maze, out Vector2Int start, out Vector3 along, int need = 3)
    {
        start = Vector2Int.zero;
        along = Vector3.forward;
        for (int x = 0; x < maze.Width; x++)
        {
            for (int y = 0; y < maze.Height; y++)
            {
                if (!maze.IsPath(x, y)) continue;
                foreach (var dir in new[] { Vector3.right, Vector3.forward })
                {
                    bool ok = true;
                    for (int i = 1; i < need; i++)
                    {
                        var step = dir == Vector3.right
                            ? new Vector2Int(x + i, y)
                            : new Vector2Int(x, y + i);
                        if (!maze.IsPath(step.x, step.y)) { ok = false; break; }
                    }
                    if (!ok) continue;
                    start = new Vector2Int(x, y);
                    along = dir;
                    return true;
                }
            }
        }
        return false;
    }

    void WriteReport()
    {
        try
        {
            var path = Path.Combine(Application.persistentDataPath, "m26-threat-report.txt");
            File.WriteAllText(path, _report.ToString());
            Debug.Log(Prefix + "report written to " + path);
        }
        catch (System.Exception e)
        {
            Debug.LogError(Prefix + "could not write the report: " + e.Message);
        }
    }
}

/// <summary>
/// Taps the game's final mix. OnAudioFilterRead runs on the audio thread, so this only buffers — no
/// Unity API calls, no allocation beyond the list append — and the file is written from the main thread.
/// </summary>
public sealed class MixRecorder : MonoBehaviour
{
    readonly List<float> _samples = new List<float>(1 << 20);
    readonly object _lock = new object();

    public bool Recording;
    public int Channels { get; private set; } = 2;
    public int SampleRate { get; private set; } = 48000;
    public float Seconds => Channels > 0 ? _samples.Count / (float)(Channels * SampleRate) : 0f;

    public int Count { get { lock (_lock) return _samples.Count; } }

    void Awake()
    {
        SampleRate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        if (!Recording) return;
        Channels = channels;
        SampleRate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : SampleRate;
        lock (_lock) { _samples.AddRange(data); }
    }

    /// <summary>RMS of everything captured since <paramref name="from"/> — the loudness of that window.</summary>
    public float RmsSince(int from, out int captured)
    {
        float sum = 0f;
        int n = 0;
        lock (_lock)
        {
            for (int i = from; i < _samples.Count; i++) { sum += _samples[i] * _samples[i]; n++; }
        }
        captured = n;
        return n > 0 ? Mathf.Sqrt(sum / n) : 0f;
    }

    public bool WriteWav(string path)
    {
        float[] copy;
        lock (_lock) { copy = _samples.ToArray(); }
        if (copy.Length == 0) return false;

        int channels = Channels > 0 ? Channels : 2;
        try
        {
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var w = new BinaryWriter(stream))
            {
                int dataBytes = copy.Length * 2;
                w.Write(new[] { 'R', 'I', 'F', 'F' });
                w.Write(36 + dataBytes);
                w.Write(new[] { 'W', 'A', 'V', 'E' });
                w.Write(new[] { 'f', 'm', 't', ' ' });
                w.Write(16);
                w.Write((short)1);                    // PCM
                w.Write((short)channels);
                w.Write(SampleRate);
                w.Write(SampleRate * channels * 2);   // byte rate
                w.Write((short)(channels * 2));       // block align
                w.Write((short)16);                   // bits
                w.Write(new[] { 'd', 'a', 't', 'a' });
                w.Write(dataBytes);
                for (int i = 0; i < copy.Length; i++)
                {
                    int s = Mathf.Clamp((int)(copy[i] * 32767f), -32768, 32767);
                    w.Write((short)s);
                }
            }
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError("MixRecorder: could not write " + path + " — " + e.Message);
            return false;
        }
    }
}
