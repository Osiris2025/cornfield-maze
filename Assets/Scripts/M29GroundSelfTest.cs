using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// M29 (§17): the photographic ground, measured on the built Mac app.
///
/// The milestone is "wire the derived field/lane textures into the maze", so the evidence has to be the floor
/// that is actually in the world, not the textures on disk:
///
///   * the tiling is checked against the GEOMETRY — each lane mesh's UV span is compared with its own size in
///     metres, so "2 m per repeat" is a measurement rather than a constant someone typed;
///   * the ragged margin is checked against the GEOMETRY — every lane mesh's boundary vertices are compared
///     with its bounding box, and the inward cuts are reported. A straight lane would report zero;
///   * the texture memory is the residency the runtime reports, per map, with its compression format;
///   * three frames at ground level, at night, from a camera standing in a lane.
///
/// Run: "Builds/Corn Field Maze.app/Contents/MacOS/Corn Field Maze" -groundtest
///   ~/Library/Application Support/arl480/Corn Field Maze/m29-ground-report.txt + the PNGs
/// </summary>
public class M29GroundSelfTest : MonoBehaviour
{
    const string Flag = "-groundtest";
    const float MeasureSeconds = 4f;
    const float EyeHeight = 1.62f;          // M27's first-person eye height, so the frame matches what ships
    const float NightWaitCap = 60f;

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
        // A measurement run is launched from a shell while something else owns the screen: without this the
        // player stops ticking when it loses focus and the harness looks hung, which is indistinguishable
        // from done to a runner.
        Application.runInBackground = true;
        var go = new GameObject("M29GroundSelfTest");
        DontDestroyOnLoad(go);
        go.AddComponent<M29GroundSelfTest>();
    }

    IEnumerator Start()
    {
        yield return null;

        GameFrontEnd.ForcePlayForTest();
        var player = Object.FindFirstObjectByType<FarmWalkerController>();
        if (player != null) player.Frozen = false;
        var maze = player != null ? player.Maze : null;

        if (maze == null)
        {
            Emit("FAIL: no maze in the world — nothing to measure");
            WriteReport();
            Application.Quit(1);
            yield break;
        }

        var lanes = new List<MeshFilter>();
        foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
            if (mf.name == "Lane" && mf.sharedMesh != null) lanes.Add(mf);

        MeasureLaneGeometry(lanes);
        MeasureGroundTextures();
        yield return MeasureFrameTime();

        // ---- frames: the two lit ones first, then the lane at night ----------------------------
        var cam = player != null ? player.Camera : Camera.main;
        if (cam == null) { Emit("FAIL: no camera"); WriteReport(); Application.Quit(1); yield break; }
        if (player != null) player.enabled = false;      // stop the rig driving the camera for these frames

        // An east-west run WITH corn on both sides and away from the maze's outer edge: the first run found
        // (2,1) sat in the corner of the maze where nothing screens the view, and the "edge" frame came back as
        // sky and no ground at all.
        int lx = -1, ly = -1;
        for (int x = 2; x < maze.Width - 2; x++)
            for (int y = 2; y < maze.Height - 2; y++)
                if (maze.IsPath(x, y) && maze.IsPath(x - 1, y) && maze.IsPath(x + 1, y) &&
                    maze.IsWall[x, y - 1] && maze.IsWall[x, y + 1] && lx < 0)
                { lx = x; ly = y; }

        if (lx < 0) { Emit("FAIL: no east-west run with corn on both sides found"); WriteReport(); Application.Quit(1); yield break; }

        Vector3 centre = maze.CellToWorld(lx, ly);
        float halfLane = maze.CellSize * 0.52f * 0.5f;
        Emit("lane frame: standing in the east-west run at cell (" + lx + "," + ly + "), corn walls north and " +
             "south, eye height " + EyeHeight.ToString("0.00") + " m, near clip " + cam.nearClipPlane.ToString("0.00") + " m");

        // The field and the margin are close-ups: a night floor with no moon on it is 60 px of near-black and
        // nothing in it can be judged, so these two are taken while the dusk sky is still lighting the ground,
        // and the report says which frame is which.
        Emit("field + edge frames taken at dusk (night01=" + DuskSky.Night01.ToString("0.00") +
             ") because an unlit floor cannot be judged");
        // NOTE ON THE SIGN: a camera's rotation.x positive looks DOWN in Unity's left-handed frame (the flower
        // rig spins the other way, which is why _pitch is negative when it looks up). The first two runs of
        // this harness aimed the close-ups at the SKY with x = -50/-38, and the sky is what they photographed.
        yield return Place(cam, centre + new Vector3(0f, 0.72f, halfLane + 0.85f), Quaternion.Euler(50f, 90f, 0f), "m29-ground-field.png");
        yield return Place(cam, centre + new Vector3(0f, 1.15f, halfLane + 0.30f), Quaternion.Euler(38f, 90f, 0f), "m29-ground-edge.png");

        float waited = 0f;
        while (!DuskSky.IsNight && waited < NightWaitCap)
        {
            waited += Time.unscaledDeltaTime;
            // An idle player is caught in the first seconds and the frame becomes a death close-up.
            foreach (var beast in Object.FindObjectsByType<Husk>(FindObjectsSortMode.None))
                Object.Destroy(beast.gameObject);
            yield return null;
        }
        Emit("lane frame taken at night=" + DuskSky.IsNight + " (night01=" + DuskSky.Night01.ToString("0.00") +
             ") after " + waited.ToString("0.0") + "s of play");
        yield return Place(cam, centre + Vector3.up * EyeHeight, Quaternion.Euler(9f, 90f, 0f), "m29-ground-lane.png");

        WriteReport();
        yield return new WaitForSeconds(0.2f);
        Application.Quit(0);
    }

    /// <summary>
    /// The tiling and the ragged margin, read off the meshes that were built.
    ///
    /// The margin is measured as the WANDER of each boundary line: the vertices sharing the minimum U are the
    /// face the piece's minimum X came from, and if that edge is straight their X values are identical. The
    /// first version of this compared vertices against the mesh's bounding box, which cannot work — the box is
    /// computed from the jittered vertices, so it moves with them and the cut always reads zero. UVs are
    /// computed from the un-jittered world position, so they identify the edge honestly.
    /// </summary>
    void MeasureLaneGeometry(List<MeshFilter> lanes)
    {
        Emit("lane pieces built = " + lanes.Count + " (meshes, not cubes: the margin is geometry)");
        if (lanes.Count == 0) { Emit("FAIL: no lane meshes in the world"); return; }

        float maxWander = 0f, sumWander = 0f;
        int edgesMeasured = 0, raggedEdges = 0;
        float uvSpanX = 0f, uvSpanZ = 0f, sizeX = 0f, sizeZ = 0f;
        bool uvChecked = false;

        foreach (var mf in lanes)
        {
            var mesh = mf.sharedMesh;
            var verts = mesh.vertices;
            var uvs = mesh.uv;
            if (verts.Length == 0) continue;

            float umin = float.MaxValue, umax = float.MinValue, vmin = float.MaxValue, vmax = float.MinValue;
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var v in verts)
            {
                if (v.x < minX) minX = v.x;
                if (v.x > maxX) maxX = v.x;
                if (v.z < minZ) minZ = v.z;
                if (v.z > maxZ) maxZ = v.z;
            }
            foreach (var uv in uvs)
            {
                if (uv.x < umin) umin = uv.x;
                if (uv.x > umax) umax = uv.x;
                if (uv.y < vmin) vmin = uv.y;
                if (uv.y > vmax) vmax = uv.y;
            }

            if (!uvChecked && (maxX - minX) > 3.5f)
            {
                uvChecked = true;
                uvSpanX = umax - umin;
                uvSpanZ = vmax - vmin;
                sizeX = maxX - minX;
                sizeZ = maxZ - minZ;
            }

            // The two long faces of this piece: u == umin and u == umax. Their spread is the raggedness.
            float uMinLo = float.MaxValue, uMinHi = float.MinValue, uMaxLo = float.MaxValue, uMaxHi = float.MinValue;
            float vMinLo = float.MaxValue, vMinHi = float.MinValue, vMaxLo = float.MaxValue, vMaxHi = float.MinValue;
            for (int i = 0; i < verts.Length; i++)
            {
                float x = verts[i].x, z = verts[i].z;
                if (Mathf.Abs(uvs[i].x - umin) < 1e-5f) { if (x < uMinLo) uMinLo = x; if (x > uMinHi) uMinHi = x; }
                if (Mathf.Abs(uvs[i].x - umax) < 1e-5f) { if (x < uMaxLo) uMaxLo = x; if (x > uMaxHi) uMaxHi = x; }
                if (Mathf.Abs(uvs[i].y - vmin) < 1e-5f) { if (z < vMinLo) vMinLo = z; if (z > vMinHi) vMinHi = z; }
                if (Mathf.Abs(uvs[i].y - vmax) < 1e-5f) { if (z < vMaxLo) vMaxLo = z; if (z > vMaxHi) vMaxHi = z; }
            }

            float[] wandering = { uMinHi - uMinLo, uMaxHi - uMaxLo, vMinHi - vMinLo, vMaxHi - vMaxLo };
            foreach (var w in wandering)
            {
                if (float.IsNaN(w) || w < 0f) continue;
                edgesMeasured++;
                sumWander += w;
                if (w > maxWander) maxWander = w;
                if (w > 0.05f) raggedEdges++;
            }
        }

        Emit("ragged margin: a boundary line wanders by up to " + maxWander.ToString("0.00") +
             " m (mean over " + edgesMeasured + " faces " + (sumWander / Mathf.Max(1, edgesMeasured)).ToString("0.00") +
             " m; " + raggedEdges + " of " + edgesMeasured + " faces wander more than 5 cm). " +
             "A straight lane would read 0.00 m and 0 of " + edgesMeasured + ".");
        Emit("metres per repeat: " + Materials.GroundTileMetres.ToString("0.00") + " m (constant) — a " +
             sizeX.ToString("0.00") + " x " + sizeZ.ToString("0.00") + " m piece carries a UV span of " +
             uvSpanX.ToString("0.00") + " x " + uvSpanZ.ToString("0.00") + ", i.e. " +
             (sizeX / Mathf.Max(0.01f, uvSpanX)).ToString("0.00") + " m per repeat measured off the mesh");
        Emit("raggedness mask readable=" + GroundLaneMesh.MaskLoaded +
             " — sampled " + GroundLaneMesh.MaskMin.ToString("0.00") + "/" + GroundLaneMesh.MaskMean.ToString("0.00") +
             "/" + GroundLaneMesh.MaskMax.ToString("0.00") + " (min/mean/max), cut from " +
             GroundLaneMesh.MaskLowCut.ToString("0.00") + " to " + GroundLaneMesh.MaskHighCut.ToString("0.00") +
             "; GroundLaneMesh reads it on the CPU, and unreadable would mean straight lanes");
    }

    /// <summary>The textures actually resident, with the format the runtime chose for them.</summary>
    void MeasureGroundTextures()
    {
        var texes = Resources.LoadAll<Texture2D>("Ground");
        long gpu = 0, cpu = 0;
        var sb = new StringBuilder();
        foreach (var t in texes)
        {
            long bytes = BytesFor(t);
            gpu += bytes;
            long cpuBytes = t.isReadable ? (long)t.width * t.height * 4 : 0;   // the CPU copy Unity keeps
            cpu += cpuBytes;
            long profiler = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(t);
            sb.Append("  ").Append(t.name).Append(" ").Append(t.width).Append("x").Append(t.height)
              .Append(" format=").Append(t.format)
              .Append(" mips=").Append(t.mipmapCount)
              .Append(" wrap=").Append(t.wrapMode)
              .Append(" readable=").Append(t.isReadable)
              .Append(" gpu≈").Append((bytes / 1024f).ToString("0")).Append("KB")
              .Append(" cpu≈").Append((cpuBytes / 1024f).ToString("0")).Append("KB")
              .Append(" profiler=").Append((profiler / 1024f).ToString("0")).AppendLine("KB");
        }
        Emit("ground textures resident = " + texes.Length + " maps, gpu ≈ " + (gpu / (1024f * 1024f)).ToString("0.00") +
             " MB + cpu ≈ " + (cpu / (1024f * 1024f)).ToString("0.00") + " MB");
        Emit(sb.ToString().TrimEnd());
        Emit("gpu/cpu above are computed from each texture's own format, size and mip count, because " +
             "Profiler.GetRuntimeMemorySizeLong reports 0 for the DXT maps in a build (it returned the readable " +
             "one only). The profiler column is printed beside them so the two can be compared.");
        Emit("the mask's CPU copy is the single biggest item and it exists only because GroundLaneMesh reads the " +
             "mask at world-build time. Phone follow-up, worth doing before submission: bake the jitter offsets " +
             "into the lane meshes at design time and drop Read/Write from T_Ground_LaneEdge.");
        Emit("the noise it replaced was three CPU-made RGBA32 tiles (128/128/96 px) — about 0.13 MB resident, " +
             "so the added weight is the difference, and it is the whole cost of the photographic floor.");
        Emit("NOTE: formats above are what the STANDALONE build chose; the phone target's own compression is a " +
             "build-time platform setting and is not measured here.");
    }

    /// <summary>Texture residency from the texture's own description: bytes per pixel by format, times the
    /// mip chain. DXT1 is 4 bits/px, DXT5 and BC5/BC7 are 8, an uncompressed 32-bit map is 32.</summary>
    static long BytesFor(Texture2D t)
    {
        float bpp;
        switch (t.format)
        {
            case TextureFormat.DXT1:
            case TextureFormat.DXT1Crunched:
                bpp = 0.5f; break;
            case TextureFormat.DXT5:
            case TextureFormat.DXT5Crunched:
            case TextureFormat.BC5:
            case TextureFormat.BC7:
                bpp = 1f; break;
            case TextureFormat.R8:
            case TextureFormat.Alpha8:
                bpp = 1f; break;
            case TextureFormat.RGB24:
                bpp = 3f; break;
            default:
                bpp = 4f; break;
        }
        long px = (long)t.width * t.height;
        float mips = t.mipmapCount > 1 ? 1.3333f : 1f;
        return (long)(px * bpp * mips);
    }

    IEnumerator MeasureFrameTime()
    {
        // Warm-up. The frames right after load compile shaders and stream the ground in, and the first run
        // measured "1713 ms/frame" over three frames — a stall reported as a frame time, which is worse than
        // no number at all.
        Application.targetFrameRate = -1;
        for (int i = 0; i < 90; i++) yield return null;

        var history = new List<float>();
        float t = 0f, sum = 0f, worst = 0f;
        int frames = 0;
        while (frames < 300 || t < MeasureSeconds)
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
        Emit("delta against the noise ground: M20 recorded avg 3.14 ms / p95 6.68 ms / worst 14.80 ms with the " +
             "old floor. These are NOT a like-for-like pair — M22-M26 changed the runtime since — but they are " +
             "the same harness on the same machine, so a floor that had become expensive would show here.");
        Emit("NOTE: the 60 fps floor is a PHONE target and no device is attached. This is the Mac build.");
    }

    IEnumerator Place(Camera cam, Vector3 pos, Quaternion rot, string name)
    {
        cam.transform.position = pos;
        cam.transform.rotation = rot;
        yield return new WaitForEndOfFrame();
        yield return CaptureFrame(name);
    }

    IEnumerator CaptureFrame(string name)
    {
        string path = Path.Combine(Application.persistentDataPath, name);
        ScreenCapture.CaptureScreenshot(path);
        yield return new WaitForEndOfFrame();
        int waited = 0;
        while (!File.Exists(path) && waited < 60)
        {
            waited++;
            yield return new WaitForEndOfFrame();
        }
        Emit("frame " + name + " -> " + (File.Exists(path) ? "written after " + waited + " frames" : "MISSING"));
    }

    void Emit(string line)
    {
        _lines.Add(line);
        Debug.Log("M29: " + line);
    }

    void WriteReport()
    {
        try
        {
            var path = Path.Combine(Application.persistentDataPath, "m29-ground-report.txt");
            var sb = new StringBuilder();
            sb.AppendLine("M29 the ground — measured on the built Mac app, " +
                          System.DateTime.Now.ToString("u", CultureInfo.InvariantCulture));
            sb.AppendLine("Blend method: geometry. The lane is a mesh whose boundary vertices are pulled inwards");
            sb.AppendLine("by the derived mask (GroundLaneMesh) — not a blend shader, not a stripe. URP/Lit is");
            sb.AppendLine("kept, so the lane still gets the same lighting, normal maps and PathMudWetness path.");
            sb.AppendLine("Import settings in force (read back from the importers by the editor probe in");
            sb.AppendLine("Builds/ground-import.log; the runtime side is reported below):");
            sb.AppendLine("  albedo sRGB=True Repeat; normal type=NormalMap sRGB=False Repeat flipGreen=False;");
            sb.AppendLine("  _R sRGB=False Repeat; LaneEdge sRGB=False Repeat readable=True max=1024 Compressed.");
            foreach (var l in _lines) sb.AppendLine(l);
            File.WriteAllText(path, sb.ToString());
            Debug.Log("M29: report written to " + path);
        }
        catch (System.Exception e)
        {
            Debug.LogError("M29: could not write the report: " + e.Message);
        }
    }
}
