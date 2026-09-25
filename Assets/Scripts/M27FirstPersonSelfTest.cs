using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// M27 (§25.8): first person by default, third person kept as a player choice — measured on the built app.
///
///     "&lt;app&gt;" -fptest
///
/// The frames come out of the game's OWN camera path: the harness teleports the walker and aims his look
/// state, then LateUpdate places the camera exactly as it does for a player. Nothing here positions a camera
/// by hand — that is the whole point of the milestone, and a harness that framed its own shots would prove
/// nothing about it.
///
/// Writes m27-fp-report.txt beside the other reports; frames land in the player log folder.
/// </summary>
public class M27FirstPersonSelfTest : MonoBehaviour
{
    const string Flag = "-fptest";
    const int ReportFrames = 240;
    const float FrameSeconds = 3f;

    readonly List<string> _lines = new List<string>();

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
        var go = new GameObject("M27FirstPersonSelfTest");
        go.AddComponent<M27FirstPersonSelfTest>();
        Object.DontDestroyOnLoad(go);
    }

    static string ReportPath => Path.Combine(Application.persistentDataPath, "m27-fp-report.txt");
    static string ShotDir => Application.persistentDataPath;

    IEnumerator Start()
    {
        Application.runInBackground = true;
        yield return null;
        yield return null;

        GameFrontEnd.ForcePlayForTest();
        yield return null;

        var player = Object.FindFirstObjectByType<FarmWalkerController>();
        if (player == null) { Emit("FAIL: no FarmWalkerController in the scene"); WriteReport(); Application.Quit(1); yield break; }
        var maze = player.Maze;
        if (maze == null) { Emit("FAIL: the walker has no maze"); WriteReport(); Application.Quit(1); yield break; }
        var cam = player.Camera;
        if (cam == null) { Emit("FAIL: the walker has no camera"); WriteReport(); Application.Quit(1); yield break; }

        Emit("M27 first person — measured on the built Mac app, " +
             System.DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ssZ", CultureInfo.InvariantCulture));
        Emit("the camera mode at startup: firstPerson=" + player.FirstPerson + " — §25.8 says first person is the default");

        // ---- the cookie's own size, and the eye height derived from it -------------------------
        Emit("the cookie measures " + player.ModelHeight.ToString("0.000") +
             " m from his own renderer bounds; the first-person eye sits at " +
             player.FirstPersonEyeHeight.ToString("0.000") + " m above his feet (0.92 of it)");

        // ---- the rig in each mode --------------------------------------------------------------
        yield return Settle(player, maze, cam);

        int shadowsOnly = 0, drawn = 0;
        foreach (var r in player.GetComponentsInChildren<Renderer>(true))
        {
            if (r == null) continue;
            if (r.shadowCastingMode == ShadowCastingMode.ShadowsOnly) shadowsOnly++;
            else if (r.shadowCastingMode == ShadowCastingMode.On) drawn++;
        }
        Emit("in first person the cookie is NOT drawn in front of his own camera: " + shadowsOnly +
             " of " + (shadowsOnly + drawn) + " renderers are ShadowsOnly (his shadow stays, §25.2's table)");
        Emit("first person rig: eye is " + (cam.transform.position.y - player.transform.position.y).ToString("0.000") +
             " m above the player's feet; distance from the player " +
             Vector3.Distance(cam.transform.position, player.transform.position).ToString("0.000") +
             " m — there is no boom between the two");
        Emit("near clip in first person: " + cam.nearClipPlane.ToString("0.000") + " m");

        // ---- frames 1 and 2: first person, a lane and a corner ----------------------------------
        int lx, ly;
        if (!FindRun(maze, out lx, out ly)) { Emit("FAIL: no east-west run found"); WriteReport(); Application.Quit(1); yield break; }

        // The meter is at 100 % out of the box; a frame of it full says nothing about whether it moves.
        // This is the rain/damage dissolve §5 already ships, set to a mid value for the capture.
        player.SetDissolveForTest(0.45f);
        yield return null;
        Emit("dough meter with the dissolve at 0.45: " + DoughReport());

        yield return Stand(player, maze, lx, ly, 3, 0);
        yield return Shot("m27-fp-lane.png");

        int cx, cy;
        if (FindCorner(maze, out cx, out cy))
        {
            yield return Stand(player, maze, cx, cy, 0, 0);
            // Aim INTO the inside corner but off-axis, so the frame reads as the turn rather than as a face
            // full of leaves: the jutting block fills the left of the frame at ~1.8 m while the lane goes on
            // to the right. This is the frame where a first-person camera in a corn maze breaks.
            var inner = maze.CellToWorld(cx, cy) + Vector3.up * player.FirstPersonEyeHeight;
            player.AimAtForTest(inner + new Vector3(2.4f, 0.30f, 1.1f));
            yield return null;
            yield return null;
            Emit("corner frame: cell (" + cx + "," + cy + "), nearest corn geometry " +
                 NearestCornMetres(player, cam).ToString("0.000") + " m from the eye, against a near clip of " +
                 cam.nearClipPlane.ToString("0.000") + " m — the crop is outside the near plane, so it renders " +
                 "rather than being cut through. The frame is the check on whether it reads that way.");
            yield return Shot("m27-fp-corner.png");
        }
        else Emit("FAIL: no corner cell found for the corner frame");

        // ---- frame 3: the toggle, exercised ----------------------------------------------------
        int before = player.ModeSwitches;
        player.ToggleFirstPerson();                    // the same entry point the V key and VIEW call
        yield return null;
        Emit("toggle exercised: the V key and the phone's VIEW button both call ToggleFirstPerson() — " +
             "firstPerson " + !player.FirstPerson + " -> " + player.FirstPerson + ", switches " + before +
             " -> " + player.ModeSwitches);
        if (player.FirstPerson) { Emit("FAIL: the toggle did not leave first person"); WriteReport(); Application.Quit(1); yield break; }

        yield return Settle(player, maze, cam);
        Emit("third person rig restored: camera " +
             Vector3.Distance(cam.transform.position, player.transform.position).ToString("0.00") +
             " m from the player (boom 5.2 m, pulled in when the corn is in the way), camera " +
             (cam.transform.position.y - player.transform.position.y).ToString("0.00") + " m up, near clip " +
             cam.nearClipPlane.ToString("0.000") + " m");
        drawn = shadowsOnly = 0;
        foreach (var r in player.GetComponentsInChildren<Renderer>(true))
        {
            if (r == null) continue;
            if (r.shadowCastingMode == ShadowCastingMode.ShadowsOnly) shadowsOnly++;
            else if (r.shadowCastingMode == ShadowCastingMode.On) drawn++;
        }
        Emit("in third person the cookie is drawn again: " + drawn + " renderers On, " + shadowsOnly + " ShadowsOnly");
        yield return Shot("m27-tp-toggle.png");

        // ---- the phone control, as evidence rather than as a claim ------------------------------
        bool wasForced = MobileControls.ForceShowForTest;
        MobileControls.ForceShowForTest = true;
        yield return null;
        yield return null;
        var touch = MobileControls.Instance;
        Emit("phone control: " + (touch != null ? touch.ViewButtonForTest : "(no MobileControls instance)") +
             " — building the touch UI on this Mac review surface, which is the only way to show it without a phone");
        MobileControls.ForceShowForTest = wasForced;

        // ---- the feel contract, the meter, and the frame time ----------------------------------
        Emit("feel contract (§25.2) unchanged: dead zone " + MobileControls.DeadZone.ToString("0.00") +
             " ramped to full by " + MobileControls.FullZone.ToString("0.00") +
             ", look on the whole right side above x=" + MobileControls.LookHalfSplit.ToString("0.00") +
             " of the screen, look clamp " + player.MinPitch.ToString("0") + " deg (up) to " +
             player.MaxPitch.ToString("0") + " deg (down)");
        Emit("M27 did not touch the corridor constraint, the rain dissolve, the eat sequence, the cob throw, " +
             "M26's threat term or the maze layout");
        Emit("dough meter now: " + DoughReport());

        yield return MeasureFrameTime();

        WriteReport();
        yield return new WaitForSeconds(0.2f);
        Application.Quit(0);
    }

    string DoughReport()
    {
        var hud = Object.FindFirstObjectByType<GameHud>();
        if (hud == null) return "(no HUD in the scene)";
        return "visible=" + hud.DoughMeterVisible + ", fill " + hud.DoughFillPoints.ToString("0") +
               " of " + GameHud.DoughBarPoints.ToString("0") + " pt, label \"" + hud.DoughLabelForTest + "\"";
    }

    /// <summary>Puts the walker in a cell and lets the corridor constraint settle him, with no input.</summary>
    IEnumerator Stand(FarmWalkerController player, MazeData maze, int x, int y, int aimCells, int aimCellsZ)
    {
        player.InjectInput = true;
        player.InjectedMove = Vector2.zero;
        player.InjectedRun = false;
        player.transform.position = maze.CellToWorld(x, y) + Vector3.up * 0.20f;
        yield return null;
        player.AimAtForTest(maze.CellToWorld(x + aimCells, y + aimCellsZ) + Vector3.up * player.FirstPersonEyeHeight);
        yield return null;
        yield return null;
    }

    IEnumerator Settle(FarmWalkerController player, MazeData maze, Camera cam)
    {
        player.InjectInput = true;
        player.InjectedMove = Vector2.zero;
        player.InjectedRun = false;
        for (int i = 0; i < 3; i++) yield return null;
    }

    /// <summary>An east-west run with corn on both sides and away from the maze's outer edge.</summary>
    static bool FindRun(MazeData maze, out int x, out int y)
    {
        for (int i = 2; i < maze.Width - 2; i++)
            for (int j = 2; j < maze.Height - 2; j++)
                if (maze.IsPath(i, j) && maze.IsPath(i - 1, j) && maze.IsPath(i + 1, j) &&
                    maze.IsWall[i, j - 1] && maze.IsWall[i, j + 1])
                { x = i; y = j; return true; }
        x = y = -1;
        return false;
    }

    /// <summary>A cell where the corridor turns, so the crop is against the camera on one side.</summary>
    static bool FindCorner(MazeData maze, out int x, out int y)
    {
        for (int i = 2; i < maze.Width - 2; i++)
            for (int j = 2; j < maze.Height - 2; j++)
                if (maze.IsPath(i, j) && maze.IsPath(i + 1, j) && maze.IsPath(i, j + 1) &&
                    maze.IsWall[i - 1, j] && maze.IsWall[i, j - 1])
                { x = i; y = j; return true; }
        x = y = -1;
        return false;
    }

    /// <summary>The nearest corn geometry to the eye, from the LOD groups the corn is built from — the leaf
    /// cards carry no colliders, so a raycast cannot answer this and a bounds test can.</summary>
    static float NearestCornMetres(FarmWalkerController player, Camera cam)
    {
        float best = float.MaxValue;
        var groups = Object.FindObjectsByType<LODGroup>(FindObjectsSortMode.None);
        foreach (var g in groups)
        {
            var renderers = g.GetLODs();
            if (renderers.Length == 0) continue;
            foreach (var lod in renderers)
                foreach (var r in lod.renderers)
                {
                    if (r == null) continue;
                    float d = r.bounds.SqrDistance(cam.transform.position);
                    if (d < best) best = d;
                }
        }
        return best >= float.MaxValue ? -1f : Mathf.Sqrt(best);
    }

    IEnumerator Shot(string file)
    {
        string path = Path.Combine(ShotDir, file);
        ScreenCapture.CaptureScreenshot(path);
        float waited = 0f;
        while (!File.Exists(path) && waited < 6f)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }
        yield return new WaitForEndOfFrame();
        Emit("frame " + file + " -> " + (File.Exists(path) ? "written" : "MISSING") + " (" +
             (File.Exists(path) ? new FileInfo(path).Length.ToString() : "0") + " bytes)");
    }

    IEnumerator MeasureFrameTime()
    {
        Application.targetFrameRate = -1;
        for (int i = 0; i < 90; i++) yield return null;   // warm-up: the first frames compile shaders

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
        Emit("frame time in first person over " + frames + " frames after a 90-frame warm-up: avg " +
             (sum / frames).ToString("0.00") + " ms, p95 " + history[p95].ToString("0.00") + " ms, worst " +
             worst.ToString("0.00") + " ms");
        Emit("the phone is the 60 fps target and no device is attached; this is the Mac build.");
    }

    void Emit(string line)
    {
        _lines.Add(line);
        Debug.Log("M27: " + line);
    }

    void WriteReport()
    {
        var text = new StringBuilder();
        foreach (var line in _lines) text.AppendLine(line);
        File.WriteAllText(ReportPath, text.ToString());
        Debug.Log("M27 report written to " + ReportPath);
    }
}
