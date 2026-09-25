using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// M23 (§25.3) pick-up-and-throw self-test — diagnostics only, dormant unless the player is launched
/// with "-throwselftest".
///
/// Why it exists: §25.3's acceptance is a NUMBER (7.0 m of range, one third of the Husk's health, a
/// 1.1 s stagger, 2.6 m of ground bought) and a capture of the arc. A picture cannot show a mass or a
/// stagger length, so the artefact is this report plus the frames. Everything measured here is
/// measured on the REAL CornCob, the REAL Husk and the REAL maze — nothing re-implements the physics.
///
/// Launch:  "Builds/Corn Field Maze.app/Contents/MacOS/Corn Field Maze" -throwselftest
/// Report:  Debug.Log lines prefixed "M23:" (-> Player.log) and Application.persistentDataPath/m23-throw-report.txt
/// </summary>
public sealed class M23ThrowSelfTest : MonoBehaviour
{
    const string Flag = "-throwselftest";
    const string Prefix = "M23: ";

    readonly StringBuilder _report = new StringBuilder();
    readonly List<string> _frames = new List<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void MaybeInstall()
    {
        bool wanted = false;
        foreach (var arg in System.Environment.GetCommandLineArgs())
            if (arg == Flag) wanted = true;
        if (!wanted) return;

        // The touch controls are mobile-only by design. This build is the Mac review surface, so the
        // harness asks them to draw anyway: the capture has to show the cob control Todd will use.
        MobileControls.ForceShowForTest = true;

        var go = new GameObject("M23ThrowSelfTest");
        go.AddComponent<M23ThrowSelfTest>();
    }

    IEnumerator Start()
    {
        // Without this the app stops ticking when it launches unfocused and the run never finishes.
        Application.runInBackground = true;
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
        Emit("begin — contract: cob " + CornCob.Mass + " kg, thrown at " + CornCob.ThrowSpeed + " m/s and " +
             CornCob.ThrowElevationDeg + " deg; level range R = v^2 sin(2θ)/g = " + CornCob.NominalRange.ToString("0.000") +
             " m; a hit takes 1/3 of the Husk's " + Husk.MaxHealth + " and staggers it " + CornCob.HitStagger +
             " s, buying " + CornCob.GroundBought.ToString("0.000") + " m of ground");

        var player = WaitForPlayer(20f);
        if (player == null)
        {
            Emit("FAIL: no FarmWalkerController appeared within 20 s");
            WriteReport();
            yield break;
        }

        // Hand the body over, exactly as M22's harness does and for the same reason: the front end's own
        // Start() shows the title screen, so the force has to be retried until it sticks.
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

        // The game's own Husk would hunt the subject down mid-run, and this test wants a controlled
        // Husk of its own. Clear the field first.
        foreach (var beast in Object.FindObjectsByType<Husk>(FindObjectsSortMode.None))
            Destroy(beast.gameObject);
        yield return new WaitForSeconds(0.5f);

        var maze = player.Maze;
        if (maze == null)
        {
            Emit("FAIL: the player has no maze, so nothing below can be measured");
            WriteReport();
            yield break;
        }

        // ---- the touch control exists on this platform at all ---------------------------------
        if (MobileControls.Instance != null)
        {
            MobileControls.Instance.EnsureBuiltForTest();
            Emit("cob control present: instance=" + (MobileControls.Instance != null) +
                 " visibleWithNothingInReach=" + MobileControls.Instance.ThrowVisible +
                 " (rule §25.3: it appears only in range)");
        }
        else Emit("NOTE: no MobileControls instance on this platform — the touch control cannot be audited here");

        // ---- 1. the cobs in the lanes ----------------------------------------------------------
        var cobs = new List<CornCob>(Object.FindObjectsByType<CornCob>(FindObjectsSortMode.None));
        Emit("cobs lying in the lanes: " + cobs.Count + " (rule: ~1 per " + CornCob.RouteCellsPerCob +
             " route cells)");
        if (cobs.Count == 0)
        {
            Emit("FAIL: no cob was planted, so there is nothing to pick up");
            WriteReport();
            yield break;
        }

        // Audit the placement against the maze directly, re-deriving the route here rather than
        // trusting the planter's own bookkeeping.
        var route = RouteToGold(maze);
        int offPath = 0, deadMouth = 0;
        foreach (var cob in cobs)
        {
            var cell = maze.NearestPathCell(cob.transform.position);
            if (!maze.IsPath(cell.x, cell.y)) offPath++;
            else if (IsDeadEndMouth(maze, route, cell)) deadMouth++;
        }
        Emit("placement audit: " + offPath + " off-path, " + deadMouth + " in a dead-end mouth (both must be 0)");

        // ---- 2. a straight run to throw along --------------------------------------------------
        if (!FindStraightRun(maze, out Vector2Int runCell, out Vector3 along))
        {
            Emit("FAIL: no straight corridor of 3+ cells to throw along, so the range cannot be measured cleanly");
            WriteReport();
            yield break;
        }
        Emit("throwing along a straight run from cell (" + runCell.x + "," + runCell.y + ") toward " + along);

        // ---- 3. pick up -----------------------------------------------------------------------
        var cobShot = cobs[0];
        yield return PlaceAt(player, cobShot.transform.position, 0.5f);
        yield return new WaitForSeconds(0.2f);

        var hands = CobHands.Instance;
        if (hands == null)
        {
            Emit("FAIL: no CobHands on the player — the cob cannot be picked up");
            WriteReport();
            yield break;
        }
        Emit("before pickup: inRange=" + (hands.InRange != null) + " held=" + (hands.Held != null) +
             " controlVisible=" + (MobileControls.Instance != null && MobileControls.Instance.ThrowVisible));

        bool picked = hands.TryPickUp();
        yield return null;
        Emit("pickup: " + (picked ? "PASS" : "FAIL") + " — held=" + (hands.Held != null) +
             " cobState=" + (hands.Held != null ? hands.Held.State.ToString() : "none") +
             " controlVisible=" + (MobileControls.Instance != null && MobileControls.Instance.ThrowVisible) +
             " label=" + (MobileControls.Instance != null ? MobileControls.Instance.ThrowLabelForTest : "(n/a)"));

        if (hands.Held == null)
        {
            Emit("FAIL: nothing is in hand, so the throw cannot be measured");
            WriteReport();
            yield break;
        }

        // ---- 4. the range, in a clean lane ----------------------------------------------------
        if (hands.Held != null) hands.Held.Drop(player.transform.position);
        yield return new WaitForSeconds(0.5f);
        yield return PlaceAt(player, maze.CellToWorld(runCell.x, runCell.y), 0.4f);
        // Put the cob in reach of the new position and pick it up again.
        var held = hands.InRange;
        if (held == null)
        {
            // Walk the cob to the player: this test is about the throw, not about pathing to loot.
            foreach (var c in Object.FindObjectsByType<CornCob>(FindObjectsSortMode.None))
            {
                if (!c.CanBePickedUp) continue;
                c.transform.position = player.transform.position + new Vector3(0.3f, 0.03f, 0f);
                held = c;
                break;
            }
            yield return null;
        }
        if (held != null) hands.TryPickUp();
        yield return null;
        Emit("in hand for the range throw: " + (hands.Held != null));

        yield return Capture("cob-held");   // the aim marker + the THROW control, while holding

        var thrown = hands.Held;
        if (thrown == null)
        {
            Emit("FAIL: nothing in hand at the moment of the throw");
            WriteReport();
            yield break;
        }

        // ---- evidence rig: a side-on view of the arc -------------------------------------------
        // The player's own camera sits behind the thrower, where a 190 mm cob is a smudge at the frame
        // edge. This camera exists only for the take: it is created here and destroyed after, so it is
        // never part of a real run, and the numbers above are still measured on the real throw.
        Vector3 side = Vector3.Cross(Vector3.up, along).normalized;
        Vector3 arcMid = hands.ThrowOrigin + along * 3.5f + Vector3.up * 1.4f;
        var rigGo = new GameObject("CobArcRig");
        var rig = rigGo.AddComponent<Camera>();
        rig.fieldOfView = 55f;
        rig.depth = 100f;              // draws over the player's camera, so the frame is the side view
        rigGo.transform.position = arcMid + side * 6f + Vector3.up * 6.5f;
        rigGo.transform.LookAt(arcMid);
        Emit("arc rig camera at " + Fmt(rigGo.transform.position) + " looking at " + Fmt(arcMid) +
             " (6 m out and 6.5 m up: the crop stands ~3 m, so a camera at lane height is buried in it — " +
             "this looks over the wall into the lane. A cob is " + (CornCob.Length * 1000f) +
             " mm long, so this is the only view where the arc reads)");

        hands.ThrowAlong(along);
        yield return WaitForFlight(thrown, 2.5f, 4, "cob-side-");
        if (rigGo != null) Destroy(rigGo);
        yield return new WaitForSeconds(1.0f);

        float range = thrown.LastLevelRange;
        Vector3 landing = thrown.LastLandingPoint;
        float flatLanded = Vector3.ProjectOnPlane(landing - thrown.LastThrowOrigin, Vector3.up).magnitude;
        float nominal = CornCob.NominalRange;
        bool rangeOk = Mathf.Abs(range - nominal) <= 0.25f;
        Emit("range: level-ground " + range.ToString("0.000") + " m vs nominal " + nominal.ToString("0.000") +
             " m (tolerance 0.25) -> " + (rangeOk ? "PASS" : "FAIL"));
        Emit("range: actual landing point " + flatLanded.ToString("0.000") + " m from the throw origin " +
             "(the hand releases ~1.2 m up, so the cob falls further than the level range — both are reported)");
        Emit("range: throw origin " + Fmt(thrown.LastThrowOrigin) + " landing " + Fmt(landing) +
             " (cells are " + maze.CellSize + " m, so this is " + (range / maze.CellSize).ToString("0.00") + " cells)");
        Emit("range: first contact = " + (string.IsNullOrEmpty(thrown.FirstContact) ? "(none — flew free)" : thrown.FirstContact) +
             "; player collider " + thrown.PlayerIgnoreState);

        // ---- 5. the effect on the Husk --------------------------------------------------------
        if (hands.Held == null)
        {
            foreach (var c in Object.FindObjectsByType<CornCob>(FindObjectsSortMode.None))
            {
                if (!c.CanBePickedUp) continue;
                c.transform.position = player.transform.position + new Vector3(0.3f, 0.03f, 0f);
                yield return null;
                hands.TryPickUp();
                break;
            }
        }
        yield return null;

        // The Husk stands 3.0 m down the lane: inside the 7.0 m arc, so the cob reaches it.
        const float huskDistance = 3.0f;
        Vector3 huskSpot = maze.CellToWorld(runCell.x, runCell.y) + along * huskDistance;
        var husk = Husk.Spawn(maze, player);
        var huskBody = husk.GetComponent<CharacterController>();
        if (huskBody != null) huskBody.enabled = false;
        husk.transform.position = huskSpot;
        if (huskBody != null) huskBody.enabled = true;
        yield return new WaitForSeconds(0.35f);

        var huskRenderer = husk.GetComponentInChildren<Renderer>(true);
        Emit("husk visible height " + (huskRenderer != null ? huskRenderer.bounds.size.y.ToString("0.00") : "?") +
             " m; hit volume " + Husk.HitVolumeHeight + " m tall, " + Husk.HitVolumeRadius + " m radius" +
             " (a column, so the 20° arc connects at close range)");

        float healthBefore = husk.Health;
        string throwFirstContact = "";
        Vector3 huskPosAtHit = Vector3.zero, huskPosAfterStagger = Vector3.zero;
        bool hitHappened = false;
        float staggerSeen = 0f;

        if (hands.Held != null)
        {
            var effectCob = hands.Held;
            hands.ThrowAlong(along);

            float t = 0f;
            while (t < 3f)
            {
                t += Time.deltaTime;
                if (!hitHappened && effectCob.LastHit != null)
                {
                    hitHappened = true;
                    huskPosAtHit = husk.transform.position;
                    yield return Capture("cob-hit");
                }
                if (hitHappened)
                {
                    staggerSeen = Mathf.Max(staggerSeen, husk.LastStaggerSeconds);
                    if (husk.StaggerLeft <= 0f) { huskPosAfterStagger = husk.transform.position; break; }
                }
                yield return null;
            }
            if (huskPosAfterStagger == Vector3.zero) huskPosAfterStagger = husk.transform.position;
            throwFirstContact = effectCob.FirstContact;
        }
        else Emit("NOTE: no cob was available for the effect throw");

        float healthAfter = husk.Health;
        float movedDuringStagger = Vector3.ProjectOnPlane(huskPosAfterStagger - huskPosAtHit, Vector3.up).magnitude;
        float groundBought = Husk.MoveSpeed * staggerSeen;

        Emit("husk hit: " + (hitHappened ? "PASS" : "FAIL") + " — health " + healthBefore + " -> " + healthAfter +
             " (expected " + (healthBefore - Husk.MaxHealth / 3f) + ", i.e. one third of " + Husk.MaxHealth + ")" +
             ", hitsTaken=" + husk.HitsTaken);
        Emit("husk stagger: " + staggerSeen.ToString("0.00") + " s (contract " + CornCob.HitStagger +
             " s) -> " + (Mathf.Abs(staggerSeen - CornCob.HitStagger) <= 0.12f ? "PASS" : "FAIL"));
        Emit("husk movement during the stagger: " +
             (hitHappened ? movedDuringStagger.ToString("0.000") + " m (the stagger is meant to be a full stop)"
                          : "not measured — the cob never reached it; first contact = " +
                            (string.IsNullOrEmpty(throwFirstContact) ? "(none)" : throwFirstContact)));
        Emit("ground bought: " + groundBought.ToString("0.000") + " m = " + Husk.MoveSpeed + " m/s x " +
             staggerSeen.ToString("0.00") + " s (contract " + CornCob.GroundBought.ToString("0.000") + " m) -> " +
             (Mathf.Abs(groundBought - CornCob.GroundBought) <= 0.3f ? "PASS" : "FAIL"));

        // Reversible by design: three hits scatter it, and it re-forms rather than dying (§25.3/§8).
        husk.TakeHit(husk.Health, CornCob.HitStagger, along);
        yield return null;
        Emit("after three hits: scattered=" + husk.Scattered + " (expected True; it re-forms in " +
             Husk.ReformSeconds + " s, it does not die)");

        if (MobileControls.Instance != null)
            Emit("control after the throws: visible=" + MobileControls.Instance.ThrowVisible +
                 " label=" + MobileControls.Instance.ThrowLabelForTest);

        Emit("frames written: " + string.Join(", ", _frames.ToArray()));
        Emit("DONE");
        WriteReport();
    }

    // ------------------------------------------------------------------ helpers

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

        // A CharacterController's collider does not follow a bare transform write; step it out of the
        // way first. Same approach as the M22 harness.
        var body = player.GetComponent<CharacterController>();
        var pos = player.transform.position;
        if (body != null) body.enabled = false;
        player.transform.position = new Vector3(centre.x, pos.y, centre.z);
        if (body != null) body.enabled = true;

        float t = 0f;
        while (t < settle) { t += Time.deltaTime; yield return null; }
    }

    IEnumerator WaitForFlight(CornCob cob, float maxSeconds, int frames, string namePrefix = "cob-arc-")
    {
        // The cadence follows the actual flight (about 0.69 s to the level-ground range), so the arc
        // frames land on the rise, the apex and the descent rather than on a guessed clock.
        float every = 0.12f;
        float t = 0f;
        float next = every;
        int taken = 0;
        while (t < maxSeconds && cob.State == CornCob.CobState.Flying)
        {
            t += Time.deltaTime;
            if (taken < frames && t >= next)
            {
                taken++;
                next += every;
                yield return Capture(namePrefix + taken);
                continue;
            }
            yield return null;
        }
        Emit("flight time to rest: " + t.ToString("0.000") + " s, " + taken + " arc frames taken");
    }

    IEnumerator Capture(string name)
    {
        string path = Path.Combine(Application.persistentDataPath, "m23-" + name + ".png");
        ScreenCapture.CaptureScreenshot(path);
        _frames.Add(name);
        yield return new WaitForEndOfFrame();
        yield return new WaitForSeconds(0.05f);
    }

    static List<Vector2Int> RouteToGold(MazeData maze)
    {
        var route = new List<Vector2Int>();
        var cell = maze.StartCell;
        var seen = new HashSet<Vector2Int>();
        while (seen.Add(cell))
        {
            route.Add(cell);
            if (cell == maze.GoldCell) break;
            var next = maze.NextStepTowardGold(cell);
            if (next == cell) break;
            cell = next;
        }
        return route;
    }

    /// <summary>The dead-end rule of §25.3, re-derived here so the audit does not trust the planter.</summary>
    static bool IsDeadEndMouth(MazeData maze, List<Vector2Int> route, Vector2Int cell)
    {
        var onRoute = new HashSet<Vector2Int>(route);
        var steps = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        foreach (var step in steps)
        {
            var n = cell + step;
            if (!maze.IsPath(n.x, n.y) || onRoute.Contains(n)) continue;
            int exits = 0;
            foreach (var s2 in steps)
                if (maze.IsPath(n.x + s2.x, n.y + s2.y)) exits++;
            if (exits == 1) return true;
        }
        return false;
    }

    /// <summary>A straight run of at least three path cells: 7.0 m needs about two cells of clearance.</summary>
    static bool FindStraightRun(MazeData maze, out Vector2Int start, out Vector3 along, int need = 3)
    {
        start = Vector2Int.zero;
        along = Vector3.forward;
        var route = RouteToGold(maze);
        foreach (var cell in route)
        {
            foreach (var dir in new[] { Vector3.right, Vector3.forward })
            {
                bool ok = true;
                for (int i = 1; i < need; i++)
                {
                    var step = dir == Vector3.right ? new Vector2Int(cell.x + i, cell.y) : new Vector2Int(cell.x, cell.y + i);
                    if (!maze.IsPath(step.x, step.y)) { ok = false; break; }
                }
                if (!ok) continue;
                start = cell;
                along = dir;
                return true;
            }
        }
        return false;
    }

    void WriteReport()
    {
        try
        {
            var path = Path.Combine(Application.persistentDataPath, "m23-throw-report.txt");
            File.WriteAllText(path, _report.ToString());
            Debug.Log(Prefix + "report written to " + path);
        }
        catch (System.Exception e)
        {
            Debug.LogError(Prefix + "could not write the report: " + e.Message);
        }
    }
}
