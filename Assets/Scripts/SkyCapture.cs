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
///   ~/Library/Application Support/arl480/Corn Field Maze/m25b-moon-report.txt and moon-t0.png / moon-t40.png
///   (M25's own report, sky-report.txt / sky-t0.png / sky-t40.png, is the committed artifact from that pass.)
/// </summary>
public class SkyCapture : MonoBehaviour
{
    const string Flag = "-skycapture";
    const float SecondFrameAt = 40f;   // §25.5: the moon crosses the horizon in the first 40 seconds
    const float ClearShotAt = 22f;     // M25b: when the moon is high enough to clear the corn from a lane

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
        yield return AimAtMoon(player);
        yield return CaptureFrame("moon-t0.png");
        Say("M25b view: " + DuskSky.MoonViewReport());
        Say("t=" + DuskSky.PlaySeconds.ToString("0.0") + "s DUSK  night01=" + DuskSky.Night01.ToString("0.00") +
            " sunColour=" + DuskSky.SunColor.ToString("0.00") + " sunIntensity=" + DuskSky.SunIntensity.ToString("0.00") +
            " sunElevation=" + SunElevationDegrees().ToString("0.0") + "deg" +
            " moonElev=" + MoonElevationDegrees().ToString("0.0") + "deg" +
            " moonAngularSize=" + (MoonAngularDegrees()).ToString("0.0") + "deg" +
            " starGate=" + DuskSky.StarGate.ToString("0.00"));

        // ---- sample the whole rise so occlusion is measured, not assumed --------------------
        string geoHow;
        float expectedPx = ExpectedDiscPixels(out geoHow);
        Say("M25b moon: " + DuskSky.MoonTextureReport());
        // M36: Todd's requirement is that the moon lights the scene with proper shadows, so the harness
        // reports the LIGHT's own state next to the texture's — mode, strength and intensity off the
        // component, not off the code that set it.
        Say("M25b light: " + DuskSky.MoonLightReport());
        Say("M25b geometry: discAngularSize=" + MoonAngularDegrees().ToString("0.00") + "deg" +
            " quadAngularSize=" + (MoonAngularDegrees() / DuskSky.DiscFraction).ToString("0.00") + "deg" +
            " discFraction=" + DuskSky.DiscFraction.ToString("0.00") +
            " expectedDiscPx=" + expectedPx.ToString("0.0") + "px  (" + geoHow + ")");
        int beats = 0;
        int husksSeen = 0;
        bool wasOccluded = false;
        bool clearShotTaken = false;
        float maxOcclusion = 0f, occludedSeconds = 0f;
        float nightAt = -1f;
        while (DuskSky.PlaySeconds < SecondFrameAt)
        {
            yield return null;

            // M25b: the moon clears the crop at roughly 10 degrees of elevation, which §25.5's rise reaches
            // about 15 seconds in; below that the 2.9-3.2 m corn is in front of it and no camera in a lane
            // can show it. This is the frame where the moon is first actually visible.
            if (!clearShotTaken && DuskSky.PlaySeconds >= ClearShotAt)
            {
                clearShotTaken = true;
                yield return AimAtMoon(player);
                yield return CaptureFrame("moon-clear.png");
            }

            // The sky is what this harness is observing. The Husk hunted a motionless player down inside
            // the 40 s and the second frame came out as a death close-up. Keep the chaser out of the take.
            foreach (var beast in Object.FindObjectsByType<Husk>(FindObjectsSortMode.None))
            {
                husksSeen++;
                Object.Destroy(beast.gameObject);
            }

            float occ = DuskSky.MoonOcclusion;
            if (occ > maxOcclusion) maxOcclusion = occ;
            bool occluded = occ > 0.5f;
            if (occluded) occludedSeconds += Time.deltaTime;
            if (occluded && !wasOccluded) beats++;
            wasOccluded = occluded;
            if (nightAt < 0f && DuskSky.IsNight) nightAt = DuskSky.PlaySeconds;
        }

        // ---- t ~ 40: the moon is up ---------------------------------------------------------
        yield return AimAtMoon(player);
        yield return CaptureFrame("moon-t40.png");
        Say("M25b view: " + DuskSky.MoonViewReport());
        Say("M25b light: " + DuskSky.MoonLightReport());
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
        Say("moonrise frames: husk instances observed alive during the rise = " + husksSeen +
            " (proves the renamed class spawns and is found by type at runtime)");
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
        // The moon is the first quad written.
        // M25b: the quad is 1/DiscFraction larger than the moon, because the photograph's disc only fills
        // the middle of it. Report the DISC — multiply back — or this line reads 11 degrees and looks like a
        // §25.5 regression when the moon on screen is the size it always was.
        float halfWidth = (verts[1] - verts[0]).magnitude * 0.5f * DuskSky.DiscFraction;
        float radius = verts[0].magnitude;
        return Mathf.Atan(halfWidth / Mathf.Max(0.001f, radius)) * 2f * Mathf.Rad2Deg;
    }

    /// <summary>
    /// M25b: the disc's diameter in pixels of the 2556x1179 review frame, from the camera's own projection.
    /// This is the number the measurement of the captured PNG has to agree with — an internal check on its
    /// own is worth nothing, the whole point is the frame.
    ///
    /// The review frame is a centred crop of the window to 2556:1179, scaled to exactly that size. The
    /// window's half-height is fovY/2, so tan(fovY/2) / halfHeight is the tan-per-window-pixel, and the
    /// crop's half-height is then mapped onto FinalH/2.
    /// </summary>
    static float ExpectedDiscPixels(out string how)
    {
        const float finalW = 2556f, finalH = 1179f;
        var cam = Camera.main;
        if (cam == null) { how = "no camera"; return float.NaN; }
        float cropH = Mathf.Min(Screen.height, Screen.width / (finalW / finalH));
        float tanPerWinPx = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / (Screen.height * 0.5f);
        float tanHalfDisc = Mathf.Tan(DuskSky.DiscAngularRadiusDeg * Mathf.Deg2Rad);
        float pxInCrop = 2f * tanHalfDisc / tanPerWinPx;
        float px = pxInCrop * (finalH / cropH);
        how = "fovY=" + cam.fieldOfView.ToString("0.0") + "deg, window=" + Screen.width + "x" + Screen.height +
              ", crop=" + cropH.ToString("0") + "px tall -> " + finalW.ToString("0") + "x" + finalH.ToString("0") +
              ", tan/win-px=" + tanPerWinPx.ToString("0.0000000");
        return px;
    }

    /// <summary>M25b: the camera's forward elevation in degrees — how far up the view actually points.</summary>
    static float CamForwardElevationDeg()
    {
        var cam = Camera.main;
        if (cam == null) return float.NaN;
        return Mathf.Asin(Mathf.Clamp(cam.transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
    }

    /// <summary>M25b: the moon's elevation as seen from the camera, in degrees.</summary>
    static float MoonElevationFromCameraDeg()
    {
        var cam = Camera.main;
        if (cam == null) return float.NaN;
        Vector3 d = (DuskSky.MoonWorldCentre - cam.transform.position).normalized;
        return Mathf.Asin(Mathf.Clamp(d.y, -1f, 1f)) * Mathf.Rad2Deg;
    }

    /// <summary>
    /// M25b: put the moon in the frame.
    ///
    /// The yaw is a straight correction from the measured offset. The PITCH is not: the camera hangs behind a
    /// rig that also moves the boom, so the camera's forward elevation does not follow the controller's pitch
    /// one-for-one (setting pitch = the moon's elevation moved the camera the WRONG way and left the moon
    /// 81 deg out of frame). So the pitch is solved by bisection against the camera's own forward elevation —
    /// measured, not assumed. Every attempt is reported, including the residual, so a frame that does not
    /// contain the moon cannot pass as evidence that it does.
    /// </summary>
    IEnumerator AimAtMoon(FarmWalkerController player)
    {
        for (int i = 0; i < 3; i++)
        {
            player.AimAtForTest(DuskSky.MoonWorldCentre);
            yield return null;
            yield return null;
            float yawOff = DuskSky.MoonYawOffsetDeg;
            if (Mathf.Abs(yawOff) < 0.5f) break;
            player.NudgeLookForTest(yawOff, 0f);
            yield return null;
        }

        float target = MoonElevationFromCameraDeg();
        float lo = player.MinPitch, hi = player.MaxPitch;
        player.NudgeLookForTest(0f, lo - player.PitchForTest);
        yield return null; yield return null;
        float elevLo = CamForwardElevationDeg();
        player.NudgeLookForTest(0f, hi - player.PitchForTest);
        yield return null; yield return null;
        float elevHi = CamForwardElevationDeg();
        bool rising = elevHi > elevLo;      // which way the camera goes when the pitch rises

        for (int i = 0; i < 14; i++)
        {
            float mid = (lo + hi) * 0.5f;
            player.NudgeLookForTest(0f, mid - player.PitchForTest);
            yield return null; yield return null;
            float elev = CamForwardElevationDeg();
            if (Mathf.Abs(elev - target) < 0.5f) break;
            bool needHigher = (elev < target) == rising;
            if (needHigher) lo = mid; else hi = mid;
        }

        foreach (var beast in Object.FindObjectsByType<Husk>(FindObjectsSortMode.None))
            Object.Destroy(beast.gameObject);
        yield return null;
        Say("M25b aim: residual yaw=" + DuskSky.MoonYawOffsetDeg.ToString("0.00") +
            "deg pitch=" + DuskSky.MoonPitchOffsetDeg.ToString("0.00") +
            "deg | cameraElev=" + CamForwardElevationDeg().ToString("0.0") +
            "deg moonElev=" + MoonElevationFromCameraDeg().ToString("0.0") +
            "deg | pitch range " + elevLo.ToString("0.0") + ".." + elevHi.ToString("0.0") + "deg");
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
            var path = Path.Combine(Application.persistentDataPath, "m25b-moon-report.txt");
            var sb = new StringBuilder();
            sb.AppendLine("M25b the moon — a photograph in the sky, measured on the built Mac app, " +
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
