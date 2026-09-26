using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// M35 — the supplied scarecrow, in the maze, chasing Gingy, measured on the built Mac app.
///
///     "&lt;app&gt;" -chasetest
///
/// Todd: "put him in the scene chasing Gingy, but Gingy should be nearer the other end of the current
/// path or he should encounter the scarecrow deeper in the maze. Make sure the current version of the
/// grounds with path are using blended textures instead of puffy-looking path." And then, on seeing a
/// black field: "there should be moonlight".
///
/// So the frames answer four things and the numbers come off the running game rather than the plan:
///   * the encounter is staged TWO THIRDS along the route to the gold — what "deeper in the maze" has
///     to look like;
///   * the spawned Husk is read where the GAME put it, before this harness moves it, so the spawn
///     change is measured and not assumed;
///   * the lane's live material is read back (blend state + which map is bound) and the floor is
///     photographed at eye height, where a haze over the path or a cut line at its edge is obvious;
///   * the moon is read back as LIGHT — a disc painted in the sky proves nothing if its intensity is 0.
///
/// Cameras are placed on ROUTE CELL CENTRES, never on a straight-line offset from one: a lane is 2 m
/// wide with corn either side, so a 6 m offset lands inside the crop, which is what the first pass
/// photographed.
/// </summary>
public class M35ChaseSelfTest : MonoBehaviour
{
    const string Flag = "-chasetest";
    const float EyeHeight = 1.62f;

    readonly List<string> _lines = new List<string>();

    static System.DateTime _runStart;

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
        var go = new GameObject("M35ChaseSelfTest");
        go.AddComponent<M35ChaseSelfTest>();
        Object.DontDestroyOnLoad(go);
    }

    static string ReportPath => Path.Combine(Application.persistentDataPath, "m35-chase-report.txt");

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

        Emit("M35 the supplied scarecrow in the maze — measured on the built Mac app, " +
             System.DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ssZ", CultureInfo.InvariantCulture));
        Emit("maze " + maze.Width + " x " + maze.Height + " cells; start cell " + maze.StartCell +
             ", gold cell " + maze.GoldCell + "; cell size " + maze.CellSize.ToString("0.00") +
             " m, lane 2.00 m wide");

        // ---- the route, walked here so "deep" is a number and not a feeling -----------------------
        var route = new List<Vector2Int>();
        var cursor = maze.StartCell;
        route.Add(cursor);
        int guard = 0;
        while (cursor != maze.GoldCell && guard++ < 4096)
        {
            var next = maze.NextStepTowardGold(cursor);
            if (next == cursor) break;
            cursor = next;
            route.Add(cursor);
        }
        Emit("the route start->gold is " + route.Count + " cells (" +
             (route.Count * maze.CellSize).ToString("0.0") + " m of walking)");
        int deepIndex = Mathf.Clamp(route.Count * 2 / 3, 2, route.Count - 1);
        Emit("staging the encounter " + deepIndex + " cells in of " + route.Count + " — " +
             (deepIndex * maze.CellSize).ToString("0.0") + " m along the route, " +
             (route.Count - deepIndex) + " cells short of the gold");

        // ---- where the GAME spawned the Husk: read before this harness touches it -----------------
        Husk husk = null;
        float waited = 0f;
        while (husk == null && waited < 25f)
        {
            husk = Object.FindFirstObjectByType<Husk>();
            waited += Time.deltaTime;
            yield return null;
        }
        if (husk == null) { Emit("FAIL: no Husk spawned"); Finish(1); yield break; }

        Vector3 spawnPos = husk.transform.position;
        var spawnCell = maze.WorldToCell(spawnPos);
        int spawnIndex = route.IndexOf(spawnCell);
        Emit("the Husk spawned at cell " + spawnCell + ", which is " +
             (spawnIndex >= 0 ? spawnIndex.ToString() : "off-route") + " of " + route.Count +
             " cells along the route and " + Vector3.Distance(spawnPos, maze.StartWorld).ToString("0.0") +
             " m from the start (Husk.SpawnCellsDeeper = " + Husk.SpawnCellsDeeper + ")");

        // ---- and what it is made of ---------------------------------------------------------------
        Emit("the body is built from " + (husk.UsingModel
                ? "THE SUPPLIED MODEL — " + husk.ModelTriangles + " tris, clip '" + husk.ModelClipName + "'"
                : "the M28 PRIMITIVES — " + husk.PartCount + " parts (the model was not in the build)"));
        Emit("it measures " + husk.HeightMeters.ToString("0.00") + " m tall (corn stands 2.90-3.20 m), " +
             "widest " + husk.SilhouetteWidthMeters.ToString("0.00") + " m across; hit volume still " +
             (husk.GetComponent<Collider>() is CapsuleCollider cap
                 ? "capsule " + cap.height.ToString("0.00") + " x r" + cap.radius.ToString("0.00")
                 : "NOT A CAPSULE"));
        ReportModelMaterial(husk);

        // ---- the GROUND, read off the live material -----------------------------------------------
        ReadLaneMaterial();

        // ---- the player is frozen here, and the camera becomes the harness's ----------------------
        player.InjectInput = true;
        player.InjectedMove = Vector2.zero;
        player.InjectedRun = false;
        player.enabled = false;
        yield return null;

        // ---- frame 1: the floor in DUSK, at the start, before anything is moved -------------------
        // This is the frame that answers "puffy path", and it is taken before the player is sent deep
        // because night falls on distance walked (NightCells is 2 cells): one teleport and the whole
        // maze is night. Dusk is also the light Todd's own screenshot of the path was taken in.
        yield return CellShot(cam, maze, route[0], route[Mathf.Min(2, route.Count - 1)], EyeHeight, 0.10f,
                              "m35-lane-dusk.png");
        ReportSky("dusk, at the start");

        // ---- park the creature while the camera is staged -----------------------------------------
        // It walks the whole time it waits; left near the player it is a catch, and a caught player is a
        // run that has stopped with an overlay on every frame.
        Vector3 park = maze.CellToWorld(1, 1);
        float settle = 0f;
        while (settle < 5f)
        {
            settle += Time.deltaTime;
            husk.transform.position = park;
            yield return null;
        }

        // ---- the encounter, two thirds along the route --------------------------------------------
        var playerCell = route[deepIndex];
        var huskCell = route[Mathf.Max(0, deepIndex - 4)];
        Vector3 meet = maze.CellToWorld(playerCell.x, playerCell.y);
        Vector3 huskAt = maze.CellToWorld(huskCell.x, huskCell.y);

        player.transform.position = meet + Vector3.up * 0.2f;
        husk.transform.position = huskAt + Vector3.up * 0.05f;
        yield return null;
        yield return null;

        Emit("staged: the player stands at cell " + playerCell + " (" +
             (deepIndex * maze.CellSize).ToString("0.0") + " m along the route), the creature at cell " +
             huskCell + " — " + Vector3.Distance(huskAt, meet).ToString("0.0") +
             " m behind him on the same lane, walking at him");

        // ---- the creature itself, CLOSE and from the FRONT ------------------------------------------
        // The Husk is open on exactly one question: does its surface read as the supplied model IN THE
        // GAME, and does the walk pose look like the clip. Neither is answerable from a distant, backlit
        // view down a lane — that is all the frames below ever gave, and at 1300x820 the creature lands
        // about 200 px tall, so there is nothing to zoom into after the fact. So the creature is held
        // still here and the camera stands 3.1 m in front of its chest.
        //
        // `enabled = false` stops its MOVEMENT (the walking is in Update) but not its animation: the walk
        // runs through an Animator output on a PlayableGraph, which evaluates independently. So the pose
        // in these frames is still a walking one — it just is not walking out of the shot while the file
        // is captured.
        bool huskWalking = husk.enabled;
        husk.enabled = false;
        Vector3 face = huskAt + Vector3.up * 0.05f;
        Vector3 towardPlayer = (meet - face).normalized;
        yield return WorldShot(cam, face - towardPlayer * 3.1f + Vector3.up * 1.35f,
                               face + Vector3.up * 1.25f, "m37-husk-front.png");
        Vector3 flank = Vector3.Cross(Vector3.up, towardPlayer).normalized;
        yield return WorldShot(cam, face + flank * 3.4f + Vector3.up * 1.35f,
                               face + Vector3.up * 1.25f, "m37-husk-side.png");

        // Three beats of the walk, IN THE GAME, on the creature being held still. A single still cannot tell
        // a playing clip from a frozen bind pose — and "is it animating" is half of what the Husk is open
        // on. If these three frames show three different strides, Unity's Animator is genuinely evaluating
        // the 72-frame clip. If they come back identical, it is not, and no amount of texture work matters.
        for (int i = 0; i < 3; i++)
        {
            yield return new WaitForSeconds(0.34f);
            yield return WorldShot(cam, face + flank * 3.4f + Vector3.up * 1.35f,
                                   face + Vector3.up * 1.25f, "m37-husk-walk-" + (i + 1) + ".png");
        }
        husk.enabled = huskWalking;

        // Third person draws the cookie, which is the only way Gingy is IN the frame at all.
        player.ToggleFirstPerson();
        yield return null;

        // From Gingy's own cell, looking up the lane at what is coming: his back near the lens, the
        // creature walking at him beyond. (The lens on the creature's cell put it in the camera's lap —
        // a pale mass across the corner of the frame — which is not a chase, it is a collision.)
        yield return CellShot(cam, maze, playerCell, huskCell, 1.78f, 1.30f, "m35-chase-deep.png");

        // The creature itself, one cell in front of it, at its own height.
        yield return CellShot(cam, maze, route[Mathf.Max(0, deepIndex - 3)], huskCell,
                              1.90f, 1.45f, "m35-chase-close.png");

        // ---- night, and the moon read as LIGHT rather than as a painted disc ----------------------
        // M37: hold the creature still for this wait. Left loose it walks the whole 20 s, and now that it
        // stands 2.30 m instead of 1.67 m it reaches the staged player and CATCHES him before the night
        // lands — and a caught run restarts, which destroys the camera the remaining frames need. Pinned at
        // huskCell it stays one cell beyond the player and IN FRAME (the walk clip still runs, so these
        // night frames show it mid-stride), without ending the run.
        float nightWaited = 0f;
        while (!DuskSky.IsNight && nightWaited < 20f)
        {
            husk.transform.position = huskAt + Vector3.up * 0.05f;
            nightWaited += Time.unscaledDeltaTime;
            yield return null;
        }
        ReportSky("deep in the maze after " + nightWaited.ToString("0.0") + " s of waiting");

        // The floor again at night: this is where a black lane with a moon in the sky is obvious.
        yield return CellShot(cam, maze, route[Mathf.Max(0, deepIndex - 2)], playerCell, EyeHeight, 0.10f,
                              "m35-lane-night.png");
        yield return CellShot(cam, maze, route[Mathf.Max(0, deepIndex - 4)], playerCell, 1.78f, 1.20f,
                              "m35-chase-night.png");

        Finish(0);
    }

    /// <summary>
    /// Put the camera on a ROUTE CELL and aim it at another, both at eye height. Cell centres are lane
    /// centres, so the shot is down the lane and not through the crop.
    /// </summary>
    IEnumerator CellShot(Camera cam, MazeData maze, Vector2Int atCell, Vector2Int lookCell,
                         float atHeight, float lookHeight, string file)
    {
        // M37: the camera can be GONE by the time a later frame is taken. The creature chases for real
        // during the night wait below, and when it catches the player the run restarts — which destroys
        // this camera, so `cam.transform` threw a NullReferenceException that took the entire report down
        // with it. The symptom was quietly awful: no report was written at all, so every PNG in the folder
        // stayed from an earlier run and looked like evidence.
        if (cam == null) cam = Camera.main;
        if (cam == null)
        {
            Emit("frame " + file + " -> SKIPPED: no camera left (the run restarted mid-harness)");
            yield break;
        }
        Vector3 at = maze.CellToWorld(atCell.x, atCell.y) + Vector3.up * atHeight;
        Vector3 look = maze.CellToWorld(lookCell.x, lookCell.y) + Vector3.up * lookHeight;
        yield return AimAndShoot(cam, at, look, file);
    }

    /// <summary>
    /// M37: a frame from a WORLD position, for a close-up the cell grid cannot express. "The creature's own
    /// cell" is a place; "3 m in front of the creature's chest" is not. And the view from its own cell was
    /// never enough to judge its surface — that is the one question the Husk is still open on, and the
    /// creature lands about 200 px tall at that distance, so there is nothing to zoom into afterwards.
    /// </summary>
    IEnumerator WorldShot(Camera cam, Vector3 at, Vector3 look, string file)
    {
        yield return AimAndShoot(cam, at, look, file);
    }

    /// <summary>Aim the camera at a world point, shoot, and say whether the file is from THIS run.</summary>
    IEnumerator AimAndShoot(Camera cam, Vector3 at, Vector3 look, string file)
    {
        cam.transform.position = at;
        var d = look - at;
        if (d.sqrMagnitude < 0.0001f) d = Vector3.forward;
        cam.transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
        for (int i = 0; i < 3; i++) yield return null;

        string path = Path.Combine(Application.persistentDataPath, file);
        ScreenCapture.CaptureScreenshot(path);
        float waited = 0f;
        bool fresh = false;
        while (waited < 8f)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
            if (File.Exists(path) && File.GetLastWriteTimeUtc(path) >= _runStart) { fresh = true; break; }
        }
        yield return new WaitForEndOfFrame();
        Emit("frame " + file + " -> " + (fresh
                ? "written this run (" + new FileInfo(path).Length + " bytes)"
                : "MISSING/STALE — the file on disk predates this run"));
    }

    /// <summary>The creature's material from the live renderer: whether the supplied albedo is on it.</summary>
    void ReportModelMaterial(Husk husk)
    {
        var mesh = husk.GetComponentInChildren<Renderer>(true);
        if (mesh == null) { Emit("FAIL: the model has no renderer"); return; }
        var mat = mesh.sharedMaterial;
        var smr = mesh as SkinnedMeshRenderer;
        Emit("the creature's mesh: " + (smr == null
                ? "not a skinned renderer"
                : smr.sharedMesh == null
                    ? "SHARED MESH IS NULL on the SkinnedMeshRenderer"
                    : smr.sharedMesh.name + ", " + (smr.sharedMesh.triangles.Length / 3) + " tris, bind box " +
                      smr.sharedMesh.bounds.size.ToString("0.00") + " m, world scale " +
                      smr.transform.lossyScale.ToString("0.000")));
        Emit("the creature's renderer is a " + mesh.GetType().Name + " wearing " +
             (mat != null ? mat.shader.name : "NO MATERIAL") + ", base map " +
             (mat != null && mat.mainTexture != null ? mat.mainTexture.name : "NONE") +
             ", alpha " + (mat != null && mat.HasProperty("_BaseColor")
                 ? mat.GetColor("_BaseColor").a.ToString("0.00") : "n/a"));
    }

    /// <summary>
    /// The moon as LIGHT. A moon painted in the sky with an intensity of zero leaves a black field, and
    /// that is exactly what the first pass photographed — so read the light, not the disc.
    /// </summary>
    void ReportSky(string when)
    {
        Emit("sky " + when + ": night01=" + DuskSky.Night01.ToString("0.00") +
             " moonElev01=" + DuskSky.MoonElev01.ToString("0.00") +
             " starGate=" + DuskSky.StarGate.ToString("0.00") +
             " sunIntensity=" + DuskSky.SunIntensity.ToString("0.00") +
             " fog=" + DuskSky.FogColor.ToString("0.00"));

        Light moon = null;
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional && l.name == "MoonLight") { moon = l; break; }
        if (moon == null) { Emit("FAIL: no MoonLight in the scene"); return; }

        float el = Mathf.Asin(Mathf.Clamp(moon.transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
        var dir = DuskSky.MoonDirection;
        Emit("the moon as LIGHT: intensity " + moon.intensity.ToString("0.00") +
             " (" + (moon.intensity > 0.05f ? "the field is lit" : "THE FIELD IS UNLIT") +
             "), colour " + moon.color.ToString("0.00") +
             ", aimed " + el.ToString("0.0") + " deg, shadows " + moon.shadows +
             "; disc at elevation " +
             (Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) * Mathf.Rad2Deg).ToString("0.0") + " deg");

        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional)
                Emit("  directional light '" + l.name + "': intensity " + l.intensity.ToString("0.00") +
                     ", colour " + l.color.ToString("0.00") + ", elevation " +
                     (Mathf.Asin(Mathf.Clamp(l.transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg)
                         .ToString("0.0") + " deg");
    }

    /// <summary>
    /// Read the lane's live material. The blend state and the bound map are what decide whether the
    /// path is a blended texture or a slab, and both are readable at runtime.
    /// </summary>
    void ReadLaneMaterial()
    {
        Renderer lane = null;
        var all = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        int laneRenderers = 0;
        foreach (var r in all)
        {
            if (r == null || r.sharedMaterial == null) continue;
            var t = r.sharedMaterial.mainTexture;
            if (t != null && t.name.Contains("LaneA")) { laneRenderers++; if (lane == null) lane = r; }
        }
        Emit("lane pieces found in the running maze: " + laneRenderers);
        if (lane == null) { Emit("FAIL: no lane renderer wearing a T_Ground_LaneA map"); return; }

        var mat = lane.sharedMaterial;
        var tex = mat.mainTexture;
        Emit("lane material on the built mesh: " + Materials.DescribeBlend(mat));
        Emit("lane base map: " + (tex != null ? tex.name + " " + tex.width + "x" + tex.height : "NONE") +
             ", renderQueue=" + mat.renderQueue + ", alpha on _BaseMap=" +
             (mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor").a.ToString("0.00") : "n/a"));
        Emit("the fade lives in that map's ALPHA (baked by scripts/m31b_lane_edge_bake.py): interior " +
             "opaque, a 0.167 m transition, edge displaced +/-0.04 m around a constant 1.000 m half-width, " +
             "alpha 0.000 at the mesh edge. M31's haze measured a 0.84 m fade at 0.867 centre-line alpha.");
    }

    void Emit(string line)
    {
        _lines.Add(line);
        Debug.Log("M35: " + line);
    }

    void Finish(int code)
    {
        var text = new StringBuilder();
        foreach (var line in _lines) text.AppendLine(line);
        File.WriteAllText(ReportPath, text.ToString());
        Debug.Log("M35 report written to " + ReportPath);
        Application.Quit(code);
    }
}
