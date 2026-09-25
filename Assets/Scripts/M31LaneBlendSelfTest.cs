using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// M31 (§17): the lane blends into the field by alpha — measured on the built Mac app.
///
///     "&lt;app&gt;" -lanetest
///
/// M29 superimposed the lane as geometry: the margin was cut into the mesh's boundary vertices, so the join
/// was a polygon edge. This harness takes the frames that show whether that is fixed (same framing as M29's,
/// so the pairs can be laid side by side), checks the UV axis the fade depends on, and measures what turning
/// blending on for every lane piece actually costs — overdraw is real on a phone, so it is measured rather
/// than assumed.
/// </summary>
public class M31LaneBlendSelfTest : MonoBehaviour
{
    const string Flag = "-lanetest";
    const float NightWaitCap = 55f;
    const int AbFrames = 200;
    const int AbWarmup = 60;

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
        var go = new GameObject("M31LaneBlendSelfTest");
        go.AddComponent<M31LaneBlendSelfTest>();
        Object.DontDestroyOnLoad(go);
    }

    static string ReportPath => Path.Combine(Application.persistentDataPath, "m31-lane-blend-report.txt");

    IEnumerator Start()
    {
        Application.runInBackground = true;
        _runStart = System.DateTime.UtcNow;
        yield return null;
        yield return null;

        GameFrontEnd.ForcePlayForTest();
        yield return null;

        var player = Object.FindFirstObjectByType<FarmWalkerController>();
        var maze = player != null ? player.Maze : null;
        var cam = player != null ? player.Camera : Camera.main;
        if (maze == null || cam == null)
        {
            Emit("FAIL: no maze or camera");
            Finish(1);
            yield break;
        }

        Emit("M31 the lane blends by alpha — measured on the built Mac app, " +
             System.DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ssZ", CultureInfo.InvariantCulture));

        // ---- the lane pieces themselves -------------------------------------------------------------
        var lanes = new List<MeshFilter>();
        foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
            if (mf.name == "Lane" && mf.sharedMesh != null) lanes.Add(mf);
        if (lanes.Count == 0) { Emit("FAIL: no lane meshes"); Finish(1); yield break; }

        int quads = 0, uvOk = 0, uvBad = 0, tris = 0, squares = 0;
        float vLo = float.MaxValue, vHi = float.MinValue, uSpanMax = 0f, worstUvError = 0f;
        foreach (var mf in lanes)
        {
            var mesh = mf.sharedMesh;
            var verts = mesh.vertices;
            var uvs = mesh.uv;
            tris += mesh.triangles.Length / 3;
            if (verts.Length == 4) quads++;

            float lo = float.MaxValue, hi = float.MinValue, ulo = float.MaxValue, uhi = float.MinValue;
            float xlo = float.MaxValue, xhi = float.MinValue, zlo = float.MaxValue, zhi = float.MinValue;
            for (int i = 0; i < uvs.Length; i++)
            {
                if (uvs[i].y < lo) lo = uvs[i].y;
                if (uvs[i].y > hi) hi = uvs[i].y;
                if (uvs[i].x < ulo) ulo = uvs[i].x;
                if (uvs[i].x > uhi) uhi = uvs[i].x;
                if (verts[i].x < xlo) xlo = verts[i].x;
                if (verts[i].x > xhi) xhi = verts[i].x;
                if (verts[i].z < zlo) zlo = verts[i].z;
                if (verts[i].z > zhi) zhi = verts[i].z;
            }
            float xExtent = xhi - xlo, zExtent = zhi - zlo;
            if (Mathf.Abs(xExtent - zExtent) < 0.01f) squares++;
            float uSpan = uhi - ulo;
            // The metric check, and it has to hold for a square cell too: V crosses the lane exactly 0..1,
            // and U advances one repeat per TileMetres along whichever axis it runs down. Comparing U's span
            // against the piece's own extents proves the axis without the harness needing to know the maze.
            float errX = Mathf.Abs(uSpan - xExtent / GroundLaneMesh.TileMetres);
            float errZ = Mathf.Abs(uSpan - zExtent / GroundLaneMesh.TileMetres);
            float err = Mathf.Min(errX, errZ);
            if (err > worstUvError) worstUvError = err;
            bool ok = Mathf.Abs(lo) < 0.001f && Mathf.Abs(hi - 1f) < 0.001f && err < 0.02f;
            if (ok) uvOk++; else uvBad++;
            if (lo < vLo) vLo = lo;
            if (hi > vHi) vHi = hi;
            if (uSpan > uSpanMax) uSpanMax = uSpan;
        }
        Emit("lane pieces: " + lanes.Count + " meshes, " + tris + " triangles, " + quads + " of " + lanes.Count +
             " are four-vertex quads (M29's cut needed a 625-vertex grid to carry the ragged boundary; the fade " +
             "needs a rectangle), " + squares + " of them square (a cell at a lane's turn)");
        Emit("UV mapping check: " + uvOk + " of " + lanes.Count + " pieces pass — V spans " + vLo.ToString("0.00") +
             ".." + vHi.ToString("0.00") + " across the lane, and U advances one repeat per " +
             GroundLaneMesh.TileMetres.ToString("0.00") + " m along its axis (worst error " +
             worstUvError.ToString("0.000") + " repeats, longest U run " + uSpanMax.ToString("0.00") + ")" +
             (uvBad == 0
                 ? " — every piece passes, which is what makes the fade cross the lane instead of the world"
                 : " — " + uvBad + " FAILED, and the fade would cross the lane at the wrong angle"));

        // ---- the material's blend state, read back off the material -----------------------------------
        var laneMat = lanes[0].GetComponent<MeshRenderer>()?.sharedMaterial;
        Emit("lane material: " + Materials.DescribeBlend(laneMat));
        Emit("that is Alpha Blend on URP/Lit itself — _Surface=1, SrcAlpha/OneMinusSrcAlpha, ZWrite off, " +
             "queue 3000 — so the lane keeps the lit shader: the moon, the normal map and PathMudWetness all " +
             "still land on it. No in-house shader, and no second texture sampled per pixel.");

        if (player != null) player.enabled = false;      // stop the rig driving the camera for these frames

        // Same lane, same framing as M29's frames: an east-west run with corn both sides, away from the
        // outer edge, so the pairs can be compared.
        int lx = -1, ly = -1;
        for (int x = 2; x < maze.Width - 2; x++)
            for (int y = 2; y < maze.Height - 2; y++)
                if (maze.IsPath(x, y) && maze.IsPath(x - 1, y) && maze.IsPath(x + 1, y) &&
                    maze.IsWall[x, y - 1] && maze.IsWall[x, y + 1] && lx < 0)
                { lx = x; ly = y; }
        if (lx < 0) { Emit("FAIL: no east-west run with corn on both sides found"); Finish(1); yield break; }

        Vector3 centre = maze.CellToWorld(lx, ly);
        float halfLane = maze.CellSize * 0.52f * 0.5f;
        Emit("frames: cell (" + lx + "," + ly + "), the same run M29 shot, so m29-ground-edge.png (before) " +
             "and m31-ground-edge.png (after) are the same view of the same lane");

        // Dusk for the two close-ups: an unlit floor is 60 px of near-black and nothing in it can be judged.
        Emit("field + edge frames at dusk (night01=" + DuskSky.Night01.ToString("0.00") + ")");
        yield return Place(cam, centre + new Vector3(0f, 0.72f, halfLane + 0.85f), Quaternion.Euler(50f, 90f, 0f),
                           "m29-ground-field.png");
        yield return Place(cam, centre + new Vector3(0f, 1.15f, halfLane + 0.30f), Quaternion.Euler(38f, 90f, 0f),
                           "m31-ground-edge.png");

        // A corner, because that is where a linear fade has to give up something: the cell's core fades the
        // axis the lane runs along and its other corn-facing edge stays hard. Better to shoot it than to
        // claim it does not happen.
        int cx = -1, cy = -1;
        for (int x = 2; x < maze.Width - 2; x++)
            for (int y = 2; y < maze.Height - 2; y++)
                if (maze.IsPath(x, y) && cx < 0)
                {
                    int arms = (maze.IsPath(x + 1, y) ? 1 : 0) + (maze.IsPath(x - 1, y) ? 1 : 0) +
                               (maze.IsPath(x, y + 1) ? 1 : 0) + (maze.IsPath(x, y - 1) ? 1 : 0);
                    bool xArm = maze.IsPath(x + 1, y) || maze.IsPath(x - 1, y);
                    bool zArm = maze.IsPath(x, y + 1) || maze.IsPath(x, y - 1);
                    if (arms == 2 && xArm && zArm) { cx = x; cy = y; }
                }
        if (cx >= 0)
        {
            Vector3 corner = maze.CellToWorld(cx, cy);
            Emit("corner frame: cell (" + cx + "," + cy + "), two arms on different axes — the cell whose fade " +
                 "has to choose, and the frame says which of its edges is left hard");
            yield return Place(cam, corner + new Vector3(0f, 1.15f, halfLane + 0.30f), Quaternion.Euler(38f, 90f, 0f),
                               "m31-ground-corner.png");
        }

        // Plan view of the lane from directly above. A side-on frame cannot tell a missing lane piece from a
        // dark one, and the night frame showed a bright trapezoid with a straight far edge that neither the
        // overlap fix nor the fade explains. From above, coverage is unambiguous.
        Emit("plan view: camera 14 m above the lane's centre looking straight down (dusk, so the coverage is lit)");
        yield return Place(cam, centre + new Vector3(0f, 14f, 0f), Quaternion.Euler(90f, 0f, 0f), "m31-ground-plan.png");

        // The same view with the lane tinted flat magenta: the colour is the lane's actual coverage, so a
        // brighter patch means two pieces over the same ground. No alpha, no lighting, no texture to misread.
        var laneColour = laneMat != null && laneMat.HasProperty("_BaseColor")
            ? laneMat.GetColor("_BaseColor") : Color.white;
        if (laneMat != null && laneMat.HasProperty("_BaseColor"))
            laneMat.SetColor("_BaseColor", new Color(1f, 0f, 1f, laneColour.a));
        Emit("coverage map: lane tinted flat magenta (no alpha, no texture) — a doubled patch reads as a " +
             "brighter magenta rectangle, a hole reads as field");
        yield return Place(cam, centre + new Vector3(0f, 20f, 0f), Quaternion.Euler(90f, 0f, 0f), "m31-ground-coverage.png");
        if (laneMat != null && laneMat.HasProperty("_BaseColor")) laneMat.SetColor("_BaseColor", laneColour);
        Emit("coverage tint restored: _BaseColor=" + (laneColour.r.ToString("0.00") + "," +
             laneColour.g.ToString("0.00") + "," + laneColour.b.ToString("0.00")));

        // And the number, because a picture of a rectangle is still a judgement: how many lane pieces cover
        // each sample point on the lane, counted from the meshes' own bounds.
        int overlaps = 0, holes = 0, probes = 0, maxCover = 0;
        for (int x = 1; x < maze.Width - 1; x++)
        {
            for (int y = 1; y < maze.Height - 1; y++)
            {
                if (!maze.IsPath(x, y)) continue;
                Vector3 c = maze.CellToWorld(x, y);
                for (int i = -1; i <= 1; i++)
                {
                    for (int j = -1; j <= 1; j++)
                    {
                        var p = new Vector2(c.x + i * 0.8f, c.z + j * 0.8f);
                        int cover = 0;
                        foreach (var mf in lanes)
                        {
                            var b = mf.GetComponent<MeshRenderer>().bounds;
                            if (p.x >= b.min.x && p.x <= b.max.x && p.y >= b.min.z && p.y <= b.max.z) cover++;
                        }
                        probes++;
                        if (cover > maxCover) maxCover = cover;
                        if (cover >= 2) overlaps++;
                        if (cover == 0) holes++;
                    }
                }
            }
        }
        Emit("coverage count over " + probes + " probe points (3x3 per path cell at 0.8 m spacing, counted " +
             "against every lane piece's bounds): max " + maxCover + " pieces over one point, " + overlaps +
             " points covered twice or more, " + holes + " points covered by none. Two pieces over the same " +
             "ground is the doubled-alpha patch; a point with none is a hole in the lane.");

        float waited = 0f;
        while (!DuskSky.IsNight && waited < NightWaitCap)
        {
            waited += Time.unscaledDeltaTime;
            // An idle player is caught in the first seconds and the frame becomes a death close-up.
            foreach (var beast in Object.FindObjectsByType<Husk>(FindObjectsSortMode.None))
                Object.Destroy(beast.gameObject);
            yield return null;
        }
        Emit("lane frame at night=" + DuskSky.IsNight + " (night01=" + DuskSky.Night01.ToString("0.00") +
             ") after " + waited.ToString("0.0") + "s of play");
        yield return Place(cam, centre + Vector3.up * 1.655f, Quaternion.Euler(9f, 90f, 0f), "m29-ground-lane.png");

        // ---- what the blend costs: the same view, the same scene, three ways -------------------------
        // Blended vs forced opaque isolates the blend state and the overdraw. Lane hidden isolates the whole
        // lane (geometry and fill), which is the number that says whether the fade is affordable at all.
        float blended = 0f, opaque = 0f, hidden = 0f, blendedWorst = 0f, opaqueWorst = 0f, hiddenWorst = 0f;
        yield return Measure(AbWarmup, (ms, worst) => { });
        yield return Measure(AbFrames, (ms, worst) => { blended = ms; blendedWorst = worst; });
        Materials.MakeOpaque(laneMat);
        Emit("A/B: lane forced to opaque for the second measurement — " + Materials.DescribeBlend(laneMat));
        yield return Measure(AbWarmup, (ms, worst) => { });
        yield return Measure(AbFrames, (ms, worst) => { opaque = ms; opaqueWorst = worst; });
        Materials.MakeAlphaBlend(laneMat);
        Emit("A/B restored: " + Materials.DescribeBlend(laneMat));
        foreach (var mf in lanes)
        {
            var r = mf.GetComponent<MeshRenderer>();
            if (r != null) r.enabled = false;
        }
        Emit("A/B: all " + lanes.Count + " lane renderers disabled for the third measurement");
        yield return Measure(AbWarmup, (ms, worst) => { });
        yield return Measure(AbFrames, (ms, worst) => { hidden = ms; hiddenWorst = worst; });
        foreach (var mf in lanes)
        {
            var r = mf.GetComponent<MeshRenderer>();
            if (r != null) r.enabled = true;
        }
        Emit("blend cost, same camera and same scene:");
        Emit("  blended (as shipped)  avg " + blended.ToString("0.00") + " ms / worst " + blendedWorst.ToString("0.00") + " ms");
        Emit("  forced opaque         avg " + opaque.ToString("0.00") + " ms / worst " + opaqueWorst.ToString("0.00") + " ms");
        Emit("  lane hidden entirely  avg " + hidden.ToString("0.00") + " ms / worst " + hiddenWorst.ToString("0.00") + " ms");
        Emit("  -> the blend itself costs " + (blended - opaque).ToString("0.00") + " ms a frame, and the lane in " +
             "full (geometry + fill, " + lanes.Count + " pieces) costs " + (blended - hidden).ToString("0.00") +
             " ms. The geometry is identical in the first two, so that difference is the blending and its overdraw.");
        Emit("the phone is the 60 fps target and no device is attached; this is the Mac build, and on this Mac " +
             "the frame is not GPU-bound at all, so a small delta here does NOT mean a small delta on device. " +
             "The number to watch on device is the blended-vs-hidden gap.");

        Finish(0);
    }

    IEnumerator Measure(int frames, System.Action<float, float> done)
    {
        for (int i = 0; i < 20; i++) yield return null;
        float sum = 0f, worst = 0f;
        for (int i = 0; i < frames; i++)
        {
            yield return null;
            float ms = Time.unscaledDeltaTime * 1000f;
            sum += ms;
            if (ms > worst) worst = ms;
        }
        done(sum / Mathf.Max(1, frames), worst);
    }

    IEnumerator Place(Camera cam, Vector3 pos, Quaternion rot, string file)
    {
        cam.transform.position = pos;
        cam.transform.rotation = rot;
        for (int i = 0; i < 3; i++) yield return null;
        string path = Path.Combine(Application.persistentDataPath, file);
        ScreenCapture.CaptureScreenshot(path);
        float waited = 0f;
        while (waited < 6f)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
            if (File.Exists(path) && File.GetLastWriteTimeUtc(path) >= _runStart) break;
        }
        yield return new WaitForEndOfFrame();
        Emit("frame " + file + " -> " + (File.Exists(path) && File.GetLastWriteTimeUtc(path) >= _runStart
            ? "written this run (" + new FileInfo(path).Length + " bytes)" : "MISSING/STALE"));
    }

    static System.DateTime _runStart;

    void Emit(string line)
    {
        _lines.Add(line);
        Debug.Log("M31: " + line);
    }

    void Finish(int code)
    {
        var text = new StringBuilder();
        foreach (var line in _lines) text.AppendLine(line);
        File.WriteAllText(ReportPath, text.ToString());
        Debug.Log("M31 report written to " + ReportPath);
        Application.Quit(code);
    }
}
