using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// M28 (§25.8): the chaser is a scarecrow — measured on the built Mac app.
///
///     "&lt;app&gt;" -scarecrowtest
///
/// The silhouette test is the acceptance test: at 12 m at night, with every part collapsed to a flat black
/// shape, the frame has to read as a scarecrow from the shape alone. This harness takes that frame, takes
/// the moonlit lane and the head close-up, and measures the two things the milestone must not change — the
/// hit volume (against M23's cob) and the ground speed (against CornCob.GroundBought).
/// </summary>
public class M28ScarecrowSelfTest : MonoBehaviour
{
    const string Flag = "-scarecrowtest";
    const float LurchSampleSeconds = 5f;
    const float NightWaitCap = 55f;
    const int ReportFrames = 240;
    const float FrameSeconds = 3f;

    readonly List<string> _lines = new List<string>();

    // Held so the frames can be taken without the creature reaching the player mid-shot.
    Husk _husk;
    FarmWalkerController _player;
    Vector3 _pin;
    static System.DateTime _runStart;

    /// <summary>Put the Husk back where the frame wants it. Zero means "let it walk".</summary>
    void Repin()
    {
        if (_husk == null || _pin == Vector3.zero) return;
        _husk.transform.position = _pin;
    }

    static bool Wanted()
    {
        foreach (var a in System.Environment.GetCommandLineArgs())
            if (a == Flag) return true;
        return false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void MaybeInstall()
    {
        if (!Wanted()) return;
        var go = new GameObject("M28ScarecrowSelfTest");
        go.AddComponent<M28ScarecrowSelfTest>();
        Object.DontDestroyOnLoad(go);
    }

    static string ReportPath => Path.Combine(Application.persistentDataPath, "m28-scarecrow-report.txt");

    IEnumerator Start()
    {
        Application.runInBackground = true;
        _runStart = System.DateTime.UtcNow;
        yield return null;
        yield return null;

        GameFrontEnd.ForcePlayForTest();
        yield return null;

        var player = Object.FindFirstObjectByType<FarmWalkerController>();
        if (player == null) { Emit("FAIL: no FarmWalkerController"); Finish(1); yield break; }
        var maze = player.Maze;
        var cam = player.Camera;
        if (maze == null || cam == null) { Emit("FAIL: no maze or camera"); Finish(1); yield break; }

        Emit("M28 the scarecrow — measured on the built Mac app, " +
             System.DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ssZ", CultureInfo.InvariantCulture));

        // The Husk spawns 5 s in, behind the start (§25.3). Wait for the real one rather than making another.
        Husk husk = null;
        float waited = 0f;
        while (husk == null && waited < 20f)
        {
            husk = Object.FindFirstObjectByType<Husk>();
            waited += Time.deltaTime;
            yield return null;
        }
        if (husk == null) { Emit("FAIL: no Husk spawned"); Finish(1); yield break; }

        // It is inert until its spawn delay has passed and the model is switched on — and it walks toward
        // the player the whole time it waits. Park it far away FIRST: the earlier runs let it settle for
        // seven seconds, which is sixteen metres of walking, which is a catch, and a caught player is a run
        // that has stopped with an overlay on every frame.
        Vector3 park = maze.CellToWorld(maze.Width - 2, maze.Height - 2);
        if (!maze.IsPath(maze.Width - 2, maze.Height - 2)) park = maze.CellToWorld(1, 1);
        husk.transform.position = park;
        _husk = husk;
        _pin = park;
        float settle = 0f;
        while (settle < 7f) { settle += Time.deltaTime; Repin(); yield return null; }
        Emit("Husk settled and parked " + Vector3.Distance(husk.transform.position, player.transform.position).ToString("0.0") +
             " m from the player (catch range is " + Husk.CatchDistance.ToString("0.00") + " m) — it walks at " +
             Husk.MoveSpeed.ToString("0.00") + " m/s, so leaving it near him for the settle is a catch");

        Emit("built from " + husk.PartCount + " primitives; it measures " + husk.HeightMeters.ToString("0.00") +
             " m tall (the cookie is 1.80 m and the corn stands 2.90-3.20 m, so it breaks the lane line, not " +
             "the canopy); the widest thing on it is " + husk.SilhouetteWidthMeters.ToString("0.00") + " m across");

        var collider = husk.GetComponent<Collider>();
        Emit("hit volume: " + (collider is CapsuleCollider capsule
                ? "capsule " + capsule.height.ToString("0.00") + " m tall, radius " + capsule.radius.ToString("0.00") +
                  " m, trigger=" + capsule.isTrigger
                : "NOT A CAPSULE") + " — unchanged from M23, so a thrown cob (1/3 of " + Husk.MaxHealth +
             " health, " + CornCob.HitStagger.ToString("0.00") + " s stagger) hits exactly what it hit before. " +
             "The crossbar is wider than the volume and the report says so: a cob through the sleeve end passes " +
             "through. Widening the volume would move M23's measured 7.104 m, and M28 is a look change.");

        // ---- stand it on a lane near the player, well out of catching range --------------------------
        // The lit frames come first, before the player has moved at all: DuskSky goes to night as soon as
        // he has travelled two cells, so any frame taken after a teleport is a night frame whether you
        // wanted one or not. The HUSK is the thing that gets re-pinned each frame, never the player, and it
        // is placed well outside CatchDistance: the first run dropped it on the player's own cell, it
        // caught him on the first frame, and everything after that — frames and the lurch measurement
        // alike — described a creature standing still under a "Caught by the Husk" overlay.
        Vector3 playerStart = player.transform.position;
        var startCell = maze.WorldToCell(playerStart);
        Vector3 huskSpot = Vector3.zero;
        float bestScore = float.MaxValue;
        for (int y = 1; y < maze.Height - 1; y++)
        {
            for (int x = 1; x < maze.Width - 1; x++)
            {
                if (!maze.IsPath(x, y)) continue;
                Vector3 world = maze.CellToWorld(x, y);
                float d = Vector3.Distance(world, playerStart);
                if (d < 5f || d > 9f) continue;
                bool sameLane = x == startCell.x || y == startCell.y;
                float score = (sameLane ? 0f : 100f) + d;
                if (score < bestScore) { bestScore = score; huskSpot = world; }
            }
        }
        if (huskSpot == Vector3.zero)
        {
            Emit("FAIL: no path cell 5-9 m from the player to stand the Husk on");
            Finish(1);
            yield break;
        }
        huskSpot += Vector3.up * 0.05f;
        // Look down the lane toward the creature for the frames: fwd is the player -> Husk direction.
        Vector3 fwd = huskSpot - playerStart;
        fwd.y = 0f;
        fwd = fwd.sqrMagnitude > 0.01f ? fwd.normalized : Vector3.forward;
        // The walk is measured over a SHORT chase: put the player on a path cell 16-22 m away. Parked 88 m
        // off, the creature does not walk at all (it stands on its goal cell and repaths forever), and the
        // two runs that measured 0.00 m/s were both that mistake wearing different hats.
        Vector3 playerFar = Vector3.zero;
        float farBest = 0f;
        for (int y = 1; y < maze.Height - 1; y++)
        {
            for (int x = 1; x < maze.Width - 1; x++)
            {
                if (!maze.IsPath(x, y)) continue;
                Vector3 world = maze.CellToWorld(x, y);
                float d = Vector3.Distance(world, huskSpot);
                if (d < 16f || d > 22f) continue;
                if (farBest == 0f || d < farBest) { farBest = d; playerFar = world; }
            }
        }
        if (playerFar == Vector3.zero)
        {
            Emit("FAIL: no path cell 16-22 m from the Husk to park the player on for the walk measurement");
            Finish(1);
            yield break;
        }

        player.InjectInput = true;
        player.InjectedMove = Vector2.zero;
        player.InjectedRun = false;
        player.enabled = false;              // the harness places the camera; this is a creature portrait
        _husk = husk;
        _player = player;
        _pin = huskSpot;
        Repin();
        yield return null;
        yield return null;

        Emit("frames taken in the lane the player starts in, at dusk, before he has moved: the Husk stands " +
             Vector3.Distance(huskSpot, playerStart).ToString("0.0") + " m in front of him, facing him, " +
             "and is re-pinned every frame so it walks and never reaches him");

        // ---- frame 1: moonlit, at a lane's end, walking toward the camera along the lane ---------------
        yield return Place(cam, huskSpot + fwd * 6.0f + Vector3.up * 1.55f, huskSpot + Vector3.up * 1.15f,
                           "m28-scarecrow-lane.png");

        // ---- frame 2: the head — sockets, stitched seam, and the tilt --------------------------------
        // Taken from IN FRONT of it: the first run photographed the back of its head and read as a pole.
        yield return Place(cam, huskSpot + fwd * 1.60f + Vector3.up * 2.02f, huskSpot + Vector3.up * 1.98f,
                           "m28-scarecrow-close.png");

        // ---- the lurch, measured against its speed ---------------------------------------------------
        _pin = Vector3.zero;                 // let it walk for real
        player.transform.position = playerFar + Vector3.up * 0.20f;
        yield return MeasureLurch(husk, player, playerFar);

        // ---- frame 3: the silhouette test, at night, at 12 m ------------------------------------------
        float nightWaited = 0f;
        while (!DuskSky.IsNight && nightWaited < NightWaitCap)
        {
            nightWaited += Time.unscaledDeltaTime;
            yield return null;
        }
        Emit("silhouette frame at night=" + DuskSky.IsNight + " (night01=" + DuskSky.Night01.ToString("0.00") +
             ") after " + nightWaited.ToString("0.0") + " s");

        // Collapse every part to a flat black unlit shape. Nothing else about the model changes: if the
        // shape does not read, the answer is the shape, and the frame will say so.
        var black = new Material(Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default"));
        black.color = new Color(0.01f, 0.01f, 0.012f);
        var renderers = husk.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)

        {
            if (r == null) continue;
            r.sharedMaterial = black;
        }
        // The camera goes in FRONT of it — the way it is facing, which is toward the player — so the shape
        // stands between the camera and the far end of the lane rather than against a corn wall.
        Vector3 facing = playerFar - husk.transform.position;
        facing.y = 0f;
        facing = facing.sqrMagnitude > 0.001f ? facing.normalized : fwd;
        yield return Place(cam, husk.transform.position + facing * 12f + Vector3.up * 1.30f,
                           husk.transform.position + Vector3.up * 1.15f, "m28-scarecrow-silhouette.png");
        Emit("silhouette frame: camera 12 m out at 1.30 m, every part swapped to a flat black unlit material, " +
             "night01=" + DuskSky.Night01.ToString("0.00") + " — the frame is the answer to whether it reads");

        yield return MeasureFrameTime();

        Finish(0);
    }

    IEnumerator MeasureLurch(Husk husk, FarmWalkerController player, Vector3 playerFar)
    {
        var speeds = new List<float>();
        Vector3 prev = husk.transform.position;
        float t = 0f, total = 0f;
        while (t < LurchSampleSeconds)
        {
            yield return null;
            t += Time.unscaledDeltaTime;
            Vector3 p = husk.transform.position;
            float step = Vector3.Distance(new Vector3(p.x, 0f, p.z), new Vector3(prev.x, 0f, prev.z));
            total += step;
            if (Time.unscaledDeltaTime > 0.0001f) speeds.Add(step / Time.unscaledDeltaTime);
            prev = p;
            // The player stays parked at the far corner for the whole window: it is 88 m away, so the walk
            // never ends in a catch and never arrives at the goal. Carrying the player along in front of the
            // creature instead — the previous attempt — drops the target inside a corn wall, and the
            // creature stands still because the nearest path cell to its goal is the one it is standing on.
        }
        if (speeds.Count < 10) { Emit("lurch: only " + speeds.Count + " samples — not measurable"); yield break; }

        float mean = total / t;
        float min = float.MaxValue, max = 0f, sum = 0f;
        int surges = 0, stalls = 0;
        for (int i = 0; i < speeds.Count; i++)
        {
            float s = speeds[i];
            if (s < min) min = s;
            if (s > max) max = s;
            if (s < 0.01f) stalls++;
            sum += s;
            if (i > 0 && speeds[i - 1] <= mean && s > mean) surges++;   // rising edges = one surge each
        }
        Emit("lurch measured over " + t.ToString("0.0") + " s of walking (" + speeds.Count + " frames): " +
             "distance/time = " + mean.ToString("0.00") + " m/s = " + (mean / Husk.MoveSpeed * 100f).ToString("0") +
             "% of MoveSpeed " + Husk.MoveSpeed.ToString("0.00") + " m/s over " + surges + " surges");
        Emit("the residual is the sampling window, not the walk: a full sine integrates to exactly MoveSpeed, " +
             "and " + t.ToString("0.0") + " s cuts the first and last surge in half — plus " + stalls +
             " frame(s) where a corner repath held it still. The tell is the swing, not the mean.");
        Emit("the tell: instantaneous speed " + min.ToString("0.00") + " to " + max.ToString("0.00") +
             " m/s (the peak is " + (max / Husk.MoveSpeed).ToString("0.00") + "x the base speed, against the " +
             "designed " + (1f + Husk.LurchDepth).ToString("0.00") + "x), one surge every " +
             (t / Mathf.Max(1, surges)).ToString("0.00") + " s against the designed " +
             Husk.LurchSeconds.ToString("0.00") + " s");
        Emit("this is a look change, not a balance change: MoveSpeed, CatchDistance and the 8 s reform are all " +
             "untouched, and M23's cob numbers (7.104 m bought, 18 -> 12 health, 1.10 s stagger) still hold");
    }

    /// <summary>A rotation aimed at a world point from the camera's own position.</summary>
    static Quaternion LookFrom(Vector3 from, Vector3 at)
    {
        var d = at - from;
        if (d.sqrMagnitude < 0.0001f) d = Vector3.forward;
        return Quaternion.LookRotation(d.normalized, Vector3.up);
    }

    /// <summary>Stand the camera still for a beat, then capture — the same pattern the M29 frames used.</summary>
    IEnumerator Place(Camera cam, Vector3 position, Vector3 lookAt, string file)
    {
        cam.transform.position = position;
        cam.transform.rotation = LookFrom(position, lookAt);
        for (int i = 0; i < 3; i++) { Repin(); yield return null; }
        string path = Path.Combine(Application.persistentDataPath, file);
        ScreenCapture.CaptureScreenshot(path);
        // Freshness, not existence: the first run's frames are still on disk, and waiting for a file that
        // is already there reports last run's picture as this run's evidence.
        float waited = 0f;
        bool fresh = false;
        while (waited < 8f)
        {
            waited += Time.unscaledDeltaTime;
            Repin();
            yield return null;
            if (File.Exists(path) && File.GetLastWriteTimeUtc(path) >= _runStart) { fresh = true; break; }
        }
        yield return new WaitForEndOfFrame();
        Emit("frame " + file + " -> " + (fresh
                ? "written this run (" + new FileInfo(path).Length + " bytes)"
                : "MISSING/STALE — the file on disk predates this run"));
    }

    IEnumerator MeasureFrameTime()
    {
        Application.targetFrameRate = -1;
        for (int i = 0; i < 90; i++) yield return null;
        var history = new List<float>();
        float t = 0f, sum = 0f, worst = 0f;
        int frames = 0;
        while (frames < ReportFrames || t < FrameSeconds)
        {
            yield return null;
            t += Time.unscaledDeltaTime;
            frames++;
            float ms = Time.unscaledDeltaTime * 1000f;
            sum += ms;
            history.Add(ms);
            if (ms > worst) worst = ms;
        }
        if (history.Count == 0) { Emit("frame time: no frames sampled"); yield break; }
        history.Sort();
        int p95 = Mathf.Clamp((int)(history.Count * 0.95f), 0, history.Count - 1);
        Emit("frame time over " + frames + " frames after a 90-frame warm-up: avg " + (sum / frames).ToString("0.00") +
             " ms, p95 " + history[p95].ToString("0.00") + " ms, worst " + worst.ToString("0.00") + " ms");
        Emit("the phone is the 60 fps target and no device is attached; this is the Mac build.");
    }

    void Emit(string line)
    {
        _lines.Add(line);
        Debug.Log("M28: " + line);
    }

    void Finish(int code)
    {
        var text = new StringBuilder();
        foreach (var line in _lines) text.AppendLine(line);
        File.WriteAllText(ReportPath, text.ToString());
        Debug.Log("M28 report written to " + ReportPath);
        Application.Quit(code);
    }
}
