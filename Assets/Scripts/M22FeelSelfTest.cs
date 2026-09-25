using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// M22 (§25.2) movement-feel self-test — diagnostics only, and dormant unless the player is launched
/// with "-m22selftest". It drives the REAL FarmWalkerController inside the REAL maze: nothing here
/// re-implements the movement rules, so what it measures is what a player gets.
///
/// Why it exists: §25.2 says the contract is "measured, not asserted". A frame cannot show a dead
/// zone or a lane bound, so the artefact for this milestone is these numbers plus the capture.
///
/// Launch:  "Builds/Corn Field Maze.app/Contents/MacOS/Corn Field Maze" -m22selftest
/// Report:  Debug.Log lines prefixed "M22:" (-> Player.log) and Application.persistentDataPath/m22-feel-report.txt
/// </summary>
public sealed class M22FeelSelfTest : MonoBehaviour
{
    const string Flag = "-m22selftest";
    const string Prefix = "M22: ";

    readonly StringBuilder _report = new StringBuilder();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void MaybeInstall()
    {
        bool wanted = false;
        foreach (var arg in System.Environment.GetCommandLineArgs())
            if (arg == Flag) wanted = true;
        if (!wanted) return;

        var go = new GameObject("M22FeelSelfTest");
        go.AddComponent<M22FeelSelfTest>();
    }

    IEnumerator Start()
    {
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
        Emit("begin (contract: stick dead zone 0.12 ramped to full by 0.30; a deliberate sideways push must move the player; the lane bound must hold)");

        var player = WaitForPlayer(20f);
        if (player == null)
        {
            Emit("FAIL: no FarmWalkerController appeared within 20 s");
            WriteReport();
            yield break;
        }

        // Hand the body over. This deliberately does NOT depend on the front end being up: the test only
        // needs a live, unfrozen body, and these are the same two switches the front end itself flips.
        // It retries because the front end's own Start() shows the title screen, and if that lands after
        // the first force it would hold the body again.
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
             " isPlaying=" + GameFrontEnd.IsPlaying + " frontEndPresent=" + (GameFrontEnd.Instance != null));
        if (player.Frozen)
            Emit("FAIL: the body never unfroze — every measurement below is meaningless");

        yield return new WaitForSeconds(1.0f);

        // The Husk is not part of this test and would eat the subject.
        foreach (var beast in Object.FindObjectsByType<Husk>(FindObjectsSortMode.None))
            Object.Destroy(beast.gameObject);

        var maze = player.Maze;
        if (maze == null)
        {
            Emit("FAIL: the controller has no maze");
            WriteReport();
            yield break;
        }

        // ---- 1. the stick curve, sampled off the real function -----------------------------
        float[] samples = { 0.05f, 0.1199f, 0.12f, 0.21f, 0.2999f, 0.30f, 0.60f, 1.00f };
        var curve = new StringBuilder();
        foreach (var m in samples)
        {
            var v = MobileControls.StickCurve(Vector2.right * m, 1f);
            curve.Append(m.ToString("0.0000")).Append("->").Append(v.magnitude.ToString("0.000")).Append("  ");
        }
        Emit("stick curve (mag -> output magnitude): " + curve.ToString().TrimEnd());

        float belowDeadZone = MobileControls.StickCurve(Vector2.right * 0.1199f, 1f).magnitude;
        float aboveDeadZone = MobileControls.StickCurve(Vector2.right * 0.30f, 1f).magnitude;
        Emit(belowDeadZone <= 0.0001f && aboveDeadZone >= 0.999f
            ? "PASS dead zone: input below 0.12 produces no move and input at 0.30 produces full move"
            : "FAIL dead zone: belowDeadZone=" + belowDeadZone.ToString("0.000") + " aboveDeadZone=" + aboveDeadZone.ToString("0.000"));

        // ---- find a straight corridor ------------------------------------------------------
        if (!FindStraightCorridor(maze, player.transform.position, out var cell, out var along, out var lateral))
        {
            Emit("FAIL: no straight corridor found to measure in");
            WriteReport();
            yield break;
        }

        var centre = maze.CellToWorld(cell.x, cell.y);
        Emit("measuring in cell " + cell.x + "," + cell.y + " centre " + centre.ToString("0.00") +
             " along=" + along + " lateral=" + lateral + " laneHalf=" + FarmWalkerController.LaneHalf.ToString("0.00"));

        // ---- 2. a deliberate sideways push must MOVE the player ----------------------------
        yield return PlaceAt(player, centre, settle: 0.6f);
        float lateralBefore = LateralOffset(player, centre, lateral);
        yield return Inject(player, lateral, 0.8f);
        float lateralAfter = LateralOffset(player, centre, lateral);
        float sidestep = Mathf.Abs(lateralAfter - lateralBefore);
        Emit(sidestep > 0.10f
            ? "PASS sidestep: a deliberate sideways push moved the player " + sidestep.ToString("0.000") + " m (was 0.000 m before M22 - the push was refused)"
            : "FAIL sidestep: sideways push moved the player only " + sidestep.ToString("0.000") + " m");

        // ---- 3. the lane bound must hold ---------------------------------------------------
        float maxOffset = Mathf.Abs(lateralAfter);
        yield return Inject(player, lateral, 1.5f, onSample: () => { maxOffset = Mathf.Max(maxOffset, Mathf.Abs(LateralOffset(player, centre, lateral))); });
        Emit(maxOffset <= FarmWalkerController.LaneHalf + 0.02f
            ? "PASS lane bound: held at " + maxOffset.ToString("0.000") + " m, lane half is " + FarmWalkerController.LaneHalf.ToString("0.000") + " m - the player never leaves the lane"
            : "FAIL lane bound: reached " + maxOffset.ToString("0.000") + " m, beyond the " + FarmWalkerController.LaneHalf.ToString("0.000") + " m lane half");

        // ---- 4. releasing the stick must not YANK the player to the centreline --------------
        // Measured as the CHANGE in offset across the release, not the offset itself: the player is
        // meant to STAY where they pushed to. The old code snapped it back to the centreline.
        float beforeRelease = LateralOffset(player, centre, lateral);
        yield return Inject(player, Vector2.zero, 1.0f);
        float afterRelease = LateralOffset(player, centre, lateral);
        float yank = Mathf.Abs(afterRelease - beforeRelease);
        Emit(yank < 0.05f
            ? "PASS no yank: released at " + beforeRelease.ToString("0.000") + " m off-centre and stayed there (" +
              afterRelease.ToString("0.000") + " m) — the old auto-centring would have snapped it to the centreline"
            : "FAIL no yank: the player was pulled " + yank.ToString("0.000") + " m back toward the centreline after release");

        // ---- 5. measured sprint speed ------------------------------------------------------
        yield return PlaceAt(player, centre, settle: 0.6f);
        float walk = 0f;
        yield return Inject(player, along, 1.0f, run: false, onSpeed: s => walk = s);
        yield return PlaceAt(player, centre, settle: 0.6f);
        float sprint = 0f;
        yield return Inject(player, along, 1.0f, run: true, onSpeed: s => sprint = s);
        Emit("measured speed: walk=" + walk.ToString("0.00") + " m/s (constant is " + 4.4f.ToString("0.00") +
             ") sprint=" + sprint.ToString("0.00") + " m/s (constant is " + 7.4f.ToString("0.00") + ")");
        Emit(Mathf.Abs(walk - 4.4f) < 0.9f && Mathf.Abs(sprint - 7.4f) < 1.2f
            ? "PASS speed: walk and sprint match the shipped constants within tolerance"
            : "FAIL speed: measured walk/sprint diverge from the constants");

        // ---- 6. the touch surface the contract depends on ----------------------------------
        var controls = MobileControls.Instance;
        Emit(controls != null
            ? "touch surface: look sensitivity=" + controls.LookSensitivity.ToString("0.00") + " deg/pt (constant 0.14), invertY=" + controls.InvertY +
              ", look-drag region=x >= " + (Screen.width * MobileControls.LookHalfSplit).ToString("0") + " px of " + Screen.width + " (drag anywhere on the right half, no fixed pad)"
            : "touch surface: MobileControls not present on this platform (desktop) - look reads mouse");

        Emit("end");
        WriteReport();
    }

    static FarmWalkerController WaitForPlayer(float timeout)
    {
        float deadline = Time.realtimeSinceStartup + timeout;
        while (Time.realtimeSinceStartup < deadline)
        {
            var p = Object.FindFirstObjectByType<FarmWalkerController>();
            if (p != null) return p;
        }
        return null;
    }

    static bool FindStraightCorridor(MazeData maze, Vector3 near, out Vector2Int chosen, out Vector3 along, out Vector3 lateral)
    {
        chosen = default;
        along = Vector3.right;
        lateral = Vector3.forward;

        float best = float.MaxValue;
        bool found = false;
        for (int x = 0; x < maze.Width; x++)
        {
            for (int y = 0; y < maze.Height; y++)
            {
                if (!maze.IsPath(x, y)) continue;
                bool east = maze.IsPath(x + 1, y), west = maze.IsPath(x - 1, y);
                bool north = maze.IsPath(x, y + 1), south = maze.IsPath(x, y - 1);
                bool straightX = east && west && !north && !south;
                bool straightZ = north && south && !east && !west;
                if (!straightX && !straightZ) continue;

                var c = maze.CellToWorld(x, y);
                float d = (c - near).sqrMagnitude;
                if (d >= best) continue;
                best = d;
                chosen = new Vector2Int(x, y);
                along = straightX ? Vector3.right : Vector3.forward;
                lateral = straightX ? Vector3.forward : Vector3.right;
                found = true;
            }
        }
        return found;
    }

    static IEnumerator PlaceAt(FarmWalkerController player, Vector3 centre, float settle)
    {
        player.InjectInput = true;
        player.InjectedMove = Vector2.zero;
        player.InjectedRun = false;

        // Teleport the way the controller itself does it: a CharacterController's internal collider
        // does not follow a bare transform write, so the body must be stepped out of the way first.
        var body = player.GetComponent<CharacterController>();
        var pos = player.transform.position;
        if (body != null) body.enabled = false;
        player.transform.position = new Vector3(centre.x, pos.y, centre.z);
        if (body != null) body.enabled = true;

        // Let the body's rotation settle so the injected input maps onto the corridor axis cleanly.
        float t = 0f;
        while (t < settle) { t += Time.deltaTime; yield return null; }
    }

    static IEnumerator Inject(FarmWalkerController player, Vector3 worldDir, float seconds, bool run = false,
                              System.Action onSample = null, System.Action<float> onSpeed = null)
    {
        float yaw = player.transform.eulerAngles.y;
        var local = Quaternion.Euler(0f, -yaw, 0f) * worldDir;   // input is body-relative; this asks for a WORLD direction
        player.InjectInput = true;
        player.InjectedMove = new Vector2(local.x, local.z);
        player.InjectedRun = run;

        float t = 0f;
        var start = player.transform.position;
        while (t < seconds)
        {
            t += Time.deltaTime;
            if (onSample != null) onSample();
            yield return null;
        }

        float dt = Mathf.Max(0.0001f, t);
        var end = player.transform.position;
        var delta = end - start;
        delta.y = 0f;
        if (onSpeed != null) onSpeed(delta.magnitude / dt);

        player.InjectedMove = Vector2.zero;
        player.InjectedRun = false;
    }

    static float LateralOffset(FarmWalkerController player, Vector3 centre, Vector3 lateral)
    {
        var d = player.transform.position - centre;
        d.y = 0f;
        return Vector3.Dot(d, lateral);
    }

    void WriteReport()
    {
        try
        {
            var path = Path.Combine(Application.persistentDataPath, "m22-feel-report.txt");
            File.WriteAllText(path, _report.ToString());
            Debug.Log(Prefix + "report written to " + path);
        }
        catch (System.Exception e)
        {
            Debug.LogError(Prefix + "could not write the report: " + e.Message);
        }
    }
}
