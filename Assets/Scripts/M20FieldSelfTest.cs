using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// M20 (FSD §24 / §18): measures the corn field that is actually in the maze — how many block instances
/// went in, how many triangles the renderer is really being asked for through the LOD chain, what the
/// frame time actually is, and whether the camera ends up parked inside the crop (the §25.2 boom defect).
///
/// Dormant unless the player is launched with -m20selftest, exactly like M22FeelSelfTest, so it can never
/// affect a normal run. Evidence lands in:
///   ~/Library/Application Support/arl480/Corn Field Maze/m20-field-report.txt
/// </summary>
public class M20FieldSelfTest : MonoBehaviour
{
    const string Flag = "-m20selftest";
    const float MeasureSeconds = 6f;
    const float CanopyTop = 3.1f;

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
        // A measurement run is launched from a shell while something else owns the screen. Without this
        // the player stops ticking the moment it is not the frontmost app and the coroutine below never
        // resumes — the harness looks hung, and "hung" is indistinguishable from "done" to a runner.
        Application.runInBackground = true;
        var go = new GameObject("M20FieldSelfTest");
        DontDestroyOnLoad(go);
        go.AddComponent<M20FieldSelfTest>();
    }

    IEnumerator Start()
    {
        yield return null;

        var player = Object.FindFirstObjectByType<FarmWalkerController>();
        var maze = player != null ? player.Maze : null;

        // ---- what actually went into the world ------------------------------------------------
        var groups = Object.FindObjectsByType<LODGroup>(FindObjectsSortMode.None);
        int cornGroups = 0, icoGroups = 0;
        foreach (var g in groups)
        {
            if (g.name.StartsWith("Corn_c")) cornGroups++;
            else icoGroups++;
        }

        int wallCells = 0;
        if (maze != null)
            for (int x = 0; x < maze.Width; x++)
                for (int y = 0; y < maze.Height; y++)
                    if (maze.IsWall[x, y]) wallCells++;

        Emit("corn block instances placed = " + cornGroups + " (expected 4 x wall cells = " + (wallCells * 4) + ")");
        Emit("wall cells = " + wallCells + ", other LOD groups = " + icoGroups);

        // ---- what the renderer is actually asked for, LOD chain included ----------------------
        var renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        int active = 0, tris = 0;
        var mats = new HashSet<Material>();
        foreach (var r in renderers)
        {
            if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
            active++;
            foreach (var m in r.sharedMaterials) if (m != null) mats.Add(m);

            Mesh mesh = r is SkinnedMeshRenderer smr ? smr.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh;
            if (mesh != null)
                for (int s = 0; s < mesh.subMeshCount; s++) tris += (int)(mesh.GetIndexCount(s) / 3);
        }
        Emit("active renderers = " + active + ", unique materials = " + mats.Count +
             ", triangles RESIDENT (every enabled renderer, ALL LOD levels — this is not the drawn cost) = " + tris);

        // The §24.2 arithmetic, measured in the running world rather than in a document.
        int lod0Tris = 0;
        foreach (var g in groups)
        {
            if (!g.name.StartsWith("Corn_c")) continue;
            var lods = g.GetLODs();
            if (lods.Length == 0) continue;
            foreach (var r in lods[0].renderers)
            {
                Mesh m = r.GetComponent<MeshFilter>()?.sharedMesh;
                if (m == null) continue;
                for (int s = 0; s < m.subMeshCount; s++) lod0Tris += (int)(m.GetIndexCount(s) / 3);
            }
        }
        Emit("corn LOD0 triangles for every block = " + lod0Tris + " (expected 1144 x 8955 = " + (1144 * 8955) + ")");

        // ---- frame cost ------------------------------------------------------------------------
        float worst = 0f, sum = 0f;
        int frames = 0;
        var history = new List<float>();
        float t = 0f;
        while (t < MeasureSeconds)
        {
            yield return null;
            t += Time.unscaledDeltaTime;
            frames++;
            float ms = Time.unscaledDeltaTime * 1000f;
            sum += ms;
            if (ms > worst) worst = ms;
            history.Add(ms);
        }
        history.Sort();
        int p95i = Mathf.Clamp((int)(history.Count * 0.95f), 0, history.Count - 1);
        Emit("frame time over " + frames + " frames: avg " + (sum / Mathf.Max(1, frames)).ToString("0.00") +
             " ms, p95 " + history[p95i].ToString("0.00") + " ms, worst " + worst.ToString("0.00") + " ms");
        Emit("NOTE: this is the MAC build. The 60 fps floor is a PHONE target and no device is attached — " +
             "this number is context, not that claim.");

        // ---- the boom: is the camera parked inside the crop? -----------------------------------
        if (player != null && maze != null && player.Camera != null)
        {
            int sampled = 0, inCorn = 0;
            float tt = 0f;
            while (tt < 3f)
            {
                yield return null;
                tt += Time.unscaledDeltaTime;
                var p = player.Camera.transform.position;
                sampled++;
                if (p.y <= CanopyTop)
                {
                    var c = maze.WorldToCell(p);
                    if (c.x >= 0 && c.y >= 0 && c.x < maze.Width && c.y < maze.Height &&
                        maze.IsWall[c.x, c.y]) inCorn++;
                }
            }
            Emit("camera samples = " + sampled + ", camera inside the crop = " + inCorn +
                 (inCorn == 0 ? "  PASS (boom never parks in the leaves)" : "  FAIL"));
        }
        else
        {
            Emit("camera check SKIPPED: player or maze missing");
        }

        // Frames straight from the engine. The OS window capture needs System Events permission and a
        // window name that is unique on the machine — neither is reliable for a shell runner at 2 am.
        yield return CaptureFrame("m20-field-title.png");
        GameFrontEnd.ForcePlayForTest();
        yield return new WaitForSeconds(1.5f);
        yield return CaptureFrame("m20-field-play.png");

        WriteReport();

        // Quit: this is a measurement harness, not a session. Without this the app sits on the title
        // screen forever and a runner cannot tell "done" from "hung".
        yield return new WaitForSeconds(0.2f);
        Application.Quit(0);
    }

    IEnumerator CaptureFrame(string name)
    {
        string path = Path.Combine(Application.persistentDataPath, name);
        ScreenCapture.CaptureScreenshot(path);
        yield return new WaitForEndOfFrame();

        // The file lands a frame or two after the request; a check that runs immediately reports MISSING
        // on a frame that was captured perfectly well. Poll briefly instead of guessing.
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
        Debug.Log("M20: " + line);
    }

    void WriteReport()
    {
        try
        {
            var path = Path.Combine(Application.persistentDataPath, "m20-field-report.txt");
            var sb = new StringBuilder();
            sb.AppendLine("M20 corn field — measured on the built Mac app, " + System.DateTime.Now.ToString("u", CultureInfo.InvariantCulture));
            foreach (var l in _lines) sb.AppendLine(l);
            File.WriteAllText(path, sb.ToString());
            Debug.Log("M20: report written to " + path);
        }
        catch (System.Exception e)
        {
            Debug.LogError("M20: could not write the report: " + e.Message);
        }
    }
}
