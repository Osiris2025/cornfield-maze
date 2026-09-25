using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// M32c — the missing mechanism behind "the puddle does not read as water".
///
/// The moon is not a light that lands on the water: it is a billboard QUAD written into the sky dome mesh
/// every frame (DuskSky, submesh 1), drawn at sky radius. A dim directional light plus a flat water plane
/// gives a specular lobe far too small and too tight to see from the angles a player actually looks from —
/// which is why several passes of tuning `_Smoothness` produced read-back numbers and no glint in any frame.
///
/// What makes water read as water at night is that it REFLECTS THE SKY at glancing angles: Fresnel makes a
/// dielectric strongly reflective as the view flattens, so a puddle down a lane shows you the sky.
///
/// Pass 5 read the realtime probe's cubemap back and there was nothing in it — the probe clears to the skybox,
/// and this scene's sky is a 57 m MESH DOME, not a skybox material. So pass 6 stops asking a realtime capture
/// to happen and BUILDS the environment map instead: the night sky is generated in code — the same bright
/// horizon under the storm, the same dark overhead, the moon disc placed at DuskSky.MoonDirection in the
/// moon's own colour, and a wide soft halo, which is the part that reads as a glint spread across water. The
/// map becomes the probe's baked cubemap and the ambient reflection fallback. The order allows exactly this
/// ("baked once -- the sky barely moves"), it is procedural like everything else in this project, and it is
/// read back rather than trusted: the harness prints all six faces' means.
///
/// DO NOT FAKE THE GLINT. Nothing here touches the water's own material — no emissive patch, no lifted
/// `_BaseColor`. The only thing supplied is the environment the water was always supposed to be reflecting.
/// </summary>
public static class ReflectionProbes
{
    public const string ProbeName = "SkyReflectionProbe";

    /// The environment map the water reflects. Read back by the harness, never assumed.
    public static Cubemap SkyCube { get; private set; }

    public static ReflectionProbe Build(Transform root, MazeData maze)
    {
        var go = new GameObject(ProbeName);
        go.transform.SetParent(root, false);

        var centre = maze.CellToWorld(maze.Width / 2, maze.Height / 2);
        go.transform.position = new Vector3(centre.x, centre.y + 4f, centre.z);

        var p = go.AddComponent<ReflectionProbe>();
        p.mode = ReflectionProbeMode.Baked;          // a real map, not a capture that may come back empty
        p.boxProjection = true;
        p.intensity = 1f;
        p.cullingMask = ~0;
        p.size = new Vector3(maze.Width * maze.CellSize + 30f, 90f, maze.Height * maze.CellSize + 30f);

        Refresh(p);

        Debug.Log("M32c environment map: built in code, 64 px cube, moon at " + DuskSky.MoonDirection +
                  " — the probe is BAKED against it (a realtime capture measured empty in pass 5), box " +
                  p.size.x.ToString("0") + " x " + p.size.z.ToString("0") + " m at " + go.transform.position);
        return p;
    }

    /// Rebuild the map once the sky is at night so it matches the sky in the acceptance frames, and re-bind it.
    /// (The moon moves; the map is 24 k pixels, so rebuilding it costs nothing.)
    public static void Refresh(ReflectionProbe p)
    {
        var old = SkyCube;
        SkyCube = BuildNightSkyCube(64, DuskSky.MoonDirection);
        if (old != null) Object.Destroy(old);
        if (p != null) p.bakedTexture = SkyCube;
        RenderSettings.customReflectionTexture = SkyCube;   // the fallback for anything outside the probe's box
    }

    /// The A/B switch for the acceptance frames: the same camera, the same scene, the environment on or off.
    /// "Off" has to remove it from BOTH the probe and the ambient fallback, or the off frame is still lit by it
    /// and the pair measures nothing — which is the trap the earlier probe ablations fell into.
    public static void SetEnabledForTest(ReflectionProbe p, bool on)
    {
        if (p != null) p.enabled = on;
        RenderSettings.customReflectionTexture = on ? SkyCube : null;
    }

    /// <summary>
    /// The night sky as a cube map, generated in code.
    /// A night sky is a gradient (a lit cloud band at the horizon, dark overhead), the moon disc where the mesh
    /// writes it, a soft halo around it, and sparse dim stars. 64 px per face is plenty for a surface that is
    /// 1-2 % of the frame: this map is never seen directly, it is only ever reflected.
    /// </summary>
    static Cubemap BuildNightSkyCube(int size, Vector3 moonDir)
    {
        var horizon = new Color(0.10f, 0.11f, 0.16f);     // the storm's lit cloud band at the horizon
        var zenith = new Color(0.015f, 0.02f, 0.05f);     // and the dark overhead
        var moonCol = new Color(1f, 0.90f, 0.74f);
        var dir = moonDir.sqrMagnitude > 0f ? moonDir.normalized : Vector3.up;

        var cube = new Cubemap(size, TextureFormat.RGBA32, true);
        var px = new Color[size * size];
        // CALIBRATED, NOT GUESSED. A reflection probe's cubemap feeds the ambient as well as the reflections, so
        // handing the scene a physically-dark night sky DIMS the whole ground. The map's job here is to give the
        // water something to reflect, not to relight the maze, so its brightness is set so the ground stays where
        // it was. Two measured points, cube mean G -> field mean G: 12.23 -> 10.23 and 26.88 -> 14.95. That is
        // ambient ~0.322 of the cube's mean plus a 6.29 light-only floor, so matching the 22.32 baseline needs a
        // cube mean of ~50, i.e. a gain of 4.1. The moon disc is already at peak and stays there.
        const float EnvGain = 4.1f;
        float fillMeanY = -1f;
        float gMin = 9f, gMax = -9f, tMin = 9f, tMax = -9f, angMin = 999f, angMax = -999f;
        var rng = new System.Random(20260925);
        for (int f = 0; f < 6; f++)
        {
            var face = (CubemapFace)f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = 2f * (x + 0.5f) / size - 1f;
                    float v = 2f * (y + 0.5f) / size - 1f;
                    Vector3 d = FaceDir(face, u, v).normalized;
                    float up = Mathf.Clamp01(d.y * 0.5f + 0.5f);
                    float t = Mathf.Pow(Mathf.Clamp01(up * 1.6f), 0.65f);
                    var c = Color.Lerp(horizon, zenith, t);
                    // the moon: the disc first, then a wide soft halo — the halo is the part that reads as a
                    // glint spread across a puddle rather than a hard dot
                    float ang = Vector3.Angle(d, dir);
                    // NOTE: Mathf.SmoothStep(from, to, t) clamps t and returns a value BETWEEN from AND to —
                    // it is not "smooth a 0..1 ramp". Handing it (0, 3.6, ang) returns up to 3.6, so `1 - that`
                    // runs to -2.6 and the cube map comes out with negative radiance, which RGBA32 clamps to
                    // black — and a black probe takes the whole scene's ambient with it. Normalise first.
                    float r = Mathf.Clamp01(ang / 3.6f);            // 0 at the moon's centre, 1 at the disc's rim
                    float disc = 1f - (r * r * (3f - 2f * r));      // smooth 1 -> 0 across the disc
                    c += moonCol * (disc * 0.95f);
                    c += moonCol * (Mathf.Exp(-ang / 12f) * 0.30f);
                    // stars, sparse and dim, only in the top half (a storm is rolling and the field is dark)
                    if (up > 0.45f && rng.NextDouble() < 0.0001) c += new Color(0.55f, 0.60f, 0.75f) * 0.8f;
                    if (c.g < gMin) gMin = c.g;
                    if (c.g > gMax) gMax = c.g;
                    if (t < tMin) tMin = t;
                    if (t > tMax) tMax = t;
                    if (ang < angMin) angMin = ang;
                    if (ang > angMax) angMax = ang;
                    // Radiance is never negative. If the maths above produces a negative channel it is a bug in
                    // the maths, not something to hand the hardware: RGBA32 clamps it to 0, and a black cube map
                    // does not just fail to light the water, it takes the whole scene's ambient with it.
                    px[y * size + x] = new Color(Mathf.Clamp01(c.r * EnvGain), Mathf.Clamp01(c.g * EnvGain),
                                                 Mathf.Clamp01(c.b * EnvGain), 1f);
                }
            }
            if (face == CubemapFace.PositiveY)
            {
                double s0 = 0; for (int i = 0; i < px.Length; i++) s0 += px[i].g;
                fillMeanY = (float)(s0 / px.Length) * 255f;
            }
            cube.SetPixels(px, face);
        }
        cube.Apply(true);
        cube.name = "T_NightSky_EnvCube";
        // READ IT BACK BEFORE TRUSTING IT — this milestone in one line. If the array is right and the cube reads
        // black, the fault is the upload; if the array is black too, the fault is the maths.
        var chk = cube.GetPixels(CubemapFace.PositiveY);
        double s1 = 0; float mx = 0f;
        for (int i = 0; i < chk.Length; i++) { s1 += chk[i].g; if (chk[i].g > mx) mx = chk[i].g; }
        float readMeanY = chk.Length > 0 ? (float)(s1 / chk.Length) * 255f : -1f;
        Debug.Log("M32c environment map: " + size + "px cube, moon dir " + dir + " — array +Y mean G " +
                  fillMeanY.ToString("0.00") + ", post-upload +Y mean G " + readMeanY.ToString("0.00") +
                  " of 255, peak " + (mx * 255f).ToString("0") +
                  " | ranges: g " + gMin.ToString("0.000") + ".." + gMax.ToString("0.000") +
                  ", t " + tMin.ToString("0.000") + ".." + tMax.ToString("0.000") +
                  ", angle " + angMin.ToString("0.0") + ".." + angMax.ToString("0.0"));
        return cube;
    }

    /// Unity's cube map face convention: the face's local (u,v) axes in object space.
    static Vector3 FaceDir(CubemapFace f, float u, float v)
    {
        switch (f)
        {
            case CubemapFace.PositiveX: return new Vector3(1f, -v, -u);
            case CubemapFace.NegativeX: return new Vector3(-1f, -v, u);
            case CubemapFace.PositiveY: return new Vector3(u, 1f, v);
            case CubemapFace.NegativeY: return new Vector3(u, -1f, -v);
            case CubemapFace.PositiveZ: return new Vector3(u, -v, 1f);
            case CubemapFace.NegativeZ: return new Vector3(-u, -v, -1f);
            default: return Vector3.up;
        }
    }
}
