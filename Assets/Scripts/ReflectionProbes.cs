using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// M32c — the missing mechanism behind "the puddle does not read as water".
///
/// The moon is not a light that lands on the water: it is a billboard QUAD written into the sky dome mesh
/// every frame (DuskSky, submesh 1), drawn at sky radius. A dim directional light plus a flat water plane
/// gives a specular lobe far too small and too tight to see from the angles a player actually looks from —
/// which is why four passes of tuning `_Smoothness` produced read-back numbers and no glint in any frame.
///
/// What makes water read as water at night is that it REFLECTS THE SKY at glancing angles: Fresnel makes a
/// dielectric strongly reflective as the view flattens, so a puddle down a lane shows you the sky — including
/// the moon quad, because that is world-positioned geometry and a probe sees it in the right direction. The
/// parallax error at sky radius is nil.
///
/// Nothing was feeding the ground an environment map. This is that probe: one realtime probe over the lane
/// area, refreshed through scripting (the sky barely moves), box-projected so it covers the maze, at a
/// resolution a phone can afford. It is refreshed once after the sky reaches night, and it can be switched
/// off for the A/B frame.
/// </summary>
public static class ReflectionProbes
{
    public const string ProbeName = "SkyReflectionProbe";

    public static ReflectionProbe Build(Transform root, MazeData maze)
    {
        var go = new GameObject(ProbeName);
        go.transform.SetParent(root, false);

        var centre = maze.CellToWorld(maze.Width / 2, maze.Height / 2);
        go.transform.position = new Vector3(centre.x, centre.y + 4f, centre.z);

        var p = go.AddComponent<ReflectionProbe>();
        p.mode = ReflectionProbeMode.Realtime;
        p.refreshMode = ReflectionProbeRefreshMode.ViaScripting;   // one render, not one per frame
        // 128 is enough: the thing being captured is a smooth sky dome with one bright disc in it. A phone
        // cannot afford a cube map per frame, and it does not need one.
        p.resolution = 128;
        p.boxProjection = true;
        p.intensity = 1f;
        p.nearClipPlane = 0.1f;
        p.farClipPlane = 2500f;
        p.clearFlags = ReflectionProbeClearFlags.Skybox;
        p.cullingMask = ~0;
        p.size = new Vector3(maze.Width * maze.CellSize + 30f, 90f, maze.Height * maze.CellSize + 30f);
        p.RenderProbe();
        Debug.Log("M32c reflection probe: realtime, 128px, box " + p.size.x.ToString("0") + " x " + p.size.z.ToString("0") +
                  " m at " + go.transform.position + ", drawn once through scripting");
        return p;
    }

    /// Called after the sky has reached night: the probe has to be captured with the moon up, not at dusk.
    public static void Refresh(ReflectionProbe p)
    {
        if (p != null) p.RenderProbe();
    }

    /// The A/B switch for the acceptance frames: the same camera, the same scene, the probe on or off.
    public static void SetEnabledForTest(ReflectionProbe p, bool on)
    {
        if (p != null) p.enabled = on;
    }
}