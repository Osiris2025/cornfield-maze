using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// M25 (FSD §25.5): evidence for the dusk phase. Takes the two frames the milestone asks for — t~0 and
/// t~40 s — straight from the engine, and records what the sky was actually doing at each of them, so the
/// claim "the moon rose and clouds stole it" is a measurement and not a memory of one.
///
/// Dormant unless the player is launched with -skycapture. Evidence lands in:
///   ~/Library/Application Support/arl480/Corn Field Maze/sky-report.txt and sky-t0.png / sky-t40.png
/// </summary>
public class SkyCapture : MonoBehaviour
{
    const string Flag = "-skycapture";
    const float SecondFrameAt = 40f;   // §25.5: the moon crosses the horizon in the first 40 seconds

    readonly StringBuilder _report = new StringBuilder();

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
        Application.runInBackground = true;
        var go = new GameObject("SkyCapture");
        DontDestroyOnLoad(go);
        go.AddComponent<SkyCapture>();
    }

    IEnumerator Start()
    {
        yield return null;

        // Wait for the world, then hand the body over the same way Play does — the dusk clock starts when
        // the player is handed control, so this is the real t=0.
        FarmWalkerController player = null;
        float waited = 0f;
        while (player == null && waited < 20f)
        {
            yield return null;
            waited += Time.unscaledDeltaTime;
            player = Object.FindFirstObjectByType<FarmWalkerController>();
        }
        if (player == null) { Say("FAIL: no player appeared"); Finish(); yield break; }

        GameFrontEnd.ForcePlayForTest();
        player.Frozen = false;
        yield return null;

        // ---- t ~ 0: dusk --------------------------------------------------------------------
        yield return CaptureFrame("sky-t0.png");
        Say("t=" + DuskSky.PlaySeconds.ToString("0.0") + "s DUSK  night01=" + DuskSky.Night01.ToString("0.00") +
            " sunColour=" + DuskSky.SunColor.ToString("0.00") + " sunIntensity=" + DuskSky.SunIntensity.ToString("0.00") +
            " sunElevation=" + SunElevationDegrees().ToString("0.0") + "deg" +
            " moonElev=" + MoonElevationDegrees().ToString("0.0") + "deg" +
            " moonAngularSize=" + (MoonAngularDegrees()).ToString("0.0") + "deg" +
            " starGate=" + DuskSky.StarGate.ToString("0.00"));

        // ---- sample the whole rise so occlusion is measured, not assumed --------------------
        int beats = 0;
        bool wasOccluded = false;
        float maxOcclusion = 0f, occludedSeconds = 0f;
        float nightAt = -1f;
        while (DuskSky.PlaySeconds < SecondFrameAt)
        {
            yield return null;

            // The sky is what this harness is observing. The Husk hunted a motionless player down inside
            // the 40 s and the second frame came out as a death close-up. Keep the chaser out of the take.
            foreach (var beast in Object.FindObjectsByType<CrumbBeast>(FindObjectsSortMode.None))
                Object.Destroy(beast.gameObject);

            float occ = DuskSky.MoonOcclusion;
            if (occ > maxOcclusion) maxOcclusion = occ;
            bool occluded = occ > 0.5f;
            if (occluded) occludedSeconds += Time.deltaTime;
            if (occluded && !wasOccluded) beats++;
            wasOccluded = occluded;
            if (nightAt < 0f && DuskSky.IsNight) nightAt = DuskSky.PlaySeconds;
        }

        // ---- t ~ 40: the moon is up ---------------------------------------------------------
        yield return CaptureFrame("sky-t40.png");
        Say("t=" + DuskSky.PlaySeconds.ToString("0.0") + "s NIGHT night01=" + DuskSky.Night01.ToString("0.00") +
            " sunColour=" + DuskSky.SunColor.ToString("0.00") + " sunIntensity=" + DuskSky.SunIntensity.ToString("0.00") +
            " sunElevation=" + SunElevationDegrees().ToString("0.0") + "deg" +
            " moonElev=" + MoonElevationDegrees().ToString("0.0") + "deg" +
            " moonAngularSize=" + (MoonAngularDegrees()).ToString("0.0") + "deg" +
            " starGate=" + DuskSky.StarGate.ToString("0.00"));
        Say("cloud occultation over the rise: beats=" + beats + " (a beat = a cloud covering more than half " +
            "the moon), occludedTime=" + occludedSeconds.ToString("0.0") + "s, max=" + maxOcclusion.ToString("0.00"));
        Say("night arrived at t=" + (nightAt < 0f ? "never" : nightAt.ToString("0.0") + "s") +
            " (rule: 40 s OR " + DuskSky.NightCells + " cells in, whichever comes first)");
        Say(beats > 0
            ? "PASS: a cloud crossed the moon during the rise — the field loses its light for a beat"
            : "FAIL: no cloud crossed the moon in the first 40 s, so the occlusion beat never fired");

        Finish();
    }

    /// <summary>A directional light points along its forward, so the body it stands for is the other way.</summary>
    static float ElevationOf(Transform t)
    {
        return Mathf.Asin(Mathf.Clamp(-t.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
    }

    static float SunElevationDegrees()
    {
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional && l.name != "MoonLight" && l.name != "Lightning")
                return ElevationOf(l.transform);
        return float.NaN;
    }

    static float MoonElevationDegrees()
    {
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.name == "MoonLight") return ElevationOf(l.transform);
        return float.NaN;
    }

    static float MoonAngularDegrees()
    {
        // §25.5: deliberately oversized. Read it back off the dome rather than trusting the constant.
        var sky = DuskSky.Instance;
        if (sky == null) return float.NaN;
        var filter = sky.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null) return float.NaN;
        var verts = filter.sharedMesh.vertices;
        if (verts.Length < 4) return float.NaN;
        // The moon is the first quad written; its own width is the quad's local height above the horizon.
        float halfWidth = (verts[1] - verts[0]).magnitude * 0.5f;
        float radius = verts[0].magnitude;
        return Mathf.Atan(halfWidth / Mathf.Max(0.001f, radius)) * 2f * Mathf.Rad2Deg;
    }

    IEnumerator CaptureFrame(string name)
    {
        string path = Path.Combine(Application.persistentDataPath, name);
        ScreenCapture.CaptureScreenshot(path);
        yield return new WaitForEndOfFrame();
        int waited = 0;
        while (!File.Exists(path) && waited < 90) { waited++; yield return new WaitForEndOfFrame(); }
        Say("frame " + name + " -> " + (File.Exists(path) ? "written" : "MISSING"));
    }

    void Say(string line)
    {
        _report.AppendLine(line);
        Debug.Log("SKY: " + line);
    }

    void Finish()
    {
        try
        {
            var path = Path.Combine(Application.persistentDataPath, "sky-report.txt");
            var sb = new StringBuilder();
            sb.AppendLine("M25 dusk / moonrise — measured on the built Mac app, " +
                          System.DateTime.Now.ToString("u", CultureInfo.InvariantCulture));
            sb.Append(_report);
            File.WriteAllText(path, sb.ToString());
            Debug.Log("SKY: report written to " + path);
        }
        catch (System.Exception e)
        {
            Debug.LogError("SKY: could not write the report: " + e.Message);
        }
        Application.Quit(0);
    }
}
