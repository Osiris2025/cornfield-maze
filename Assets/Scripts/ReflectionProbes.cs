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
/// and this scene's sky is a 57 m MESH DOME, not a skybox material. Passes 6 and 7 stop asking a realtime
/// capture to happen and BUILD the environment map instead, from the sky the game actually draws:
///
/// THE SHEEN IS LIMITED BY THE SKY, NOT BY THE WATER. At the acceptance view — eye 1.15 m, water about 4 m
/// down the lane — the view is roughly 74 deg off the water's normal, and Schlick gives a dielectric about
/// 0.23 of reflectance there, not the 0.04 that "grazing" is often taken to mean. Measured against a dark
/// night sky that is still only a few levels of 255, and invisible. So the map holds what a puddle at night
/// actually shows:
///   * the storm's lit cloud band at the horizon — the part a shallow view reflects, and the reason the
///     reflection is brightest toward the horizon and falls off as the view steepens;
///   * the moon disc at DuskSky.MoonDirection;
///   * and a WIDE halo around the moon, because that is what the sky dome itself draws there (visible in every
///     night frame) and a halo spanning tens of degrees is what spreads across a puddle as a sheen, where a
///     point source only ever gives a dot too small to find.
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
    /// A night sky is a gradient — a lit cloud band at the horizon, dark overhead — the moon disc where the mesh
    /// writes it, a wide halo around it, and sparse dim stars. 64 px per face is plenty for a surface that is
    /// 1-2 % of the frame: this map is never seen directly, it is only ever reflected.
    /// </summary>
    static Cubemap BuildNightSkyCube(int size, Vector3 moonDir)
    {
        // The storm's lit cloud band, and the dark overhead. This ratio is the point of the map: a puddle seen
        // down a lane reflects the band, so the band is what has to be bright.
        var horizon = new Color(0.34f, 0.35f, 0.42f);
        var zenith = new Color(0.030f, 0.035f, 0.070f);
        var moonCol = new Color(1f, 0.90f, 0.74f);
        const float HaloWidthDeg = 12f;      // chosen on measurement: at 20 deg the halo spreads the moon's light
                                             // over the sphere and what the water actually reflects DROPS (mean
                                             // |change| 5.68 -> 2.99 of 255), because the water's ripple normal
                                             // scatters a broad source instead of catching it. Narrow and bright.
        const float HaloStrength = 0.45f;
        var dir = moonDir.sqrMagnitude > 0f ? moonDir.normalized : Vector3.up;

        // CALIBRATED, NOT GUESSED. A reflection probe's cubemap feeds the ambient as well as the reflections, so
        // its overall brightness moves the whole ground. Two measured points (cube mean G -> field mean G):
        // 12.23 -> 10.23 and 26.88 -> 14.95, i.e. ambient ~0.322 of the cube's mean plus a 6.29 light-only floor.
        // Matching the 22.32 baseline the ground was approved at needs a cube mean of 49.8 of 255 — and it is the
        // cube's MEAN that drives the ambient, so the map is solved to hit it. Every part of the map except the
        // moon's disc is scaled by the same factor, so adding a halo does not sneak brightness into the ground:
        // the solve accounts for the halo's own contribution to the mean.
        const float AmbientMeanTarget = 49.8f;

        var cube = new Cubemap(size, TextureFormat.RGBA32, true);
        var px = new Color[size * size];

        // Pre-pass over the sphere: the gradient's mean AND the halo's mean, so the scale can be solved rather
        // than guessed. The disc's contribution to the sphere's mean is ~0.1 % and is deliberately ignored.
        double gs = 0, hs = 0; int gn = 0;
        for (int i = 0; i < 64; i++)
        {
            float yy = Mathf.Lerp(-1f, 1f, (i + 0.5f) / 64f);
            float rr = Mathf.Sqrt(Mathf.Max(0f, 1f - yy * yy));
            for (int j = 0; j < 64; j++)
            {
                float ph = Mathf.Lerp(0f, Mathf.PI * 2f, (j + 0.5f) / 64f);
                var d = new Vector3(rr * Mathf.Cos(ph), yy, rr * Mathf.Sin(ph));
                float up = Mathf.Clamp01(yy * 0.5f + 0.5f);
                float tt = Mathf.Pow(Mathf.Clamp01(up * 1.6f), 0.65f);
                gs += Color.Lerp(horizon, zenith, tt).g;
                hs += Mathf.Exp(-Vector3.Angle(d, dir) / HaloWidthDeg) * HaloStrength;
                gn++;
            }
        }
        float gradMean = (float)(gs / gn);
        float haloMean = (float)(hs / gn);
        float scale = (gradMean + haloMean) > 0f ? (AmbientMeanTarget / 255f) / (gradMean + haloMean) : 1f;

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
                    var c = Color.Lerp(horizon, zenith, t) * scale;
                    // The moon: the disc unscaled so it keeps its peak, then the wide halo — the halo is the part
                    // that reads as a sheen spread across a puddle rather than a dot in it.
                    float ang = Vector3.Angle(d, dir);
                    // NOTE: Mathf.SmoothStep(from, to, t) clamps t and returns a value BETWEEN from AND to — it is
                    // not "smooth a 0..1 ramp". Handing it (0, 3.6, ang) returns up to 3.6, so `1 - that` runs to
                    // -2.6, and RGBA32 clamps the negative radiance to black — and a black probe cubemap takes the
                    // whole scene's ambient with it (measured: field 22.32 -> 6.45). Normalise first.
                    float r = Mathf.Clamp01(ang / 3.6f);            // 0 at the moon's centre, 1 at the disc's rim
                    float disc = 1f - (r * r * (3f - 2f * r));      // smooth 1 -> 0 across the disc
                    c += moonCol * (disc * 0.95f);
                    c += moonCol * (Mathf.Exp(-ang / HaloWidthDeg) * HaloStrength * scale);
                    // stars, sparse and dim, only in the top half (a storm is rolling and the field is dark)
                    if (up > 0.45f && rng.NextDouble() < 0.0001) c += new Color(0.55f, 0.60f, 0.75f) * 0.8f * scale;
                    if (c.g < gMin) gMin = c.g;
                    if (c.g > gMax) gMax = c.g;
                    if (t < tMin) tMin = t;
                    if (t > tMax) tMax = t;
                    if (ang < angMin) angMin = ang;
                    if (ang > angMax) angMax = ang;
                    // Radiance is never negative. If the maths above produces a negative channel it is a bug in
                    // the maths, not something to hand the hardware: RGBA32 clamps it to 0, and a black cube map
                    // does not just fail to light the water, it takes the whole scene's ambient with it.
                    px[y * size + x] = new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), 1f);
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
        Debug.Log("M32c environment map: " + size + "px cube, moon dir " + dir + ", scale " + scale.ToString("0.000") +
                  " (gradient mean " + (gradMean * 255f).ToString("0.0") + ", halo mean " + (haloMean * 255f).ToString("0.0") +
                  " of 255) — array +Y mean G " + fillMeanY.ToString("0.00") +
                  ", post-upload +Y mean G " + readMeanY.ToString("0.00") + ", peak " + (mx * 255f).ToString("0") +
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
