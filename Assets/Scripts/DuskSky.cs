using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// M25 (FSD §25.5): dusk, the Halloween sky and the moonrise.
///
/// The player's first frame of play is DUSK — sun just below the horizon, a warm band low in the west,
/// the field reading as silhouette — and the moon rises while they are still in the first maze:
///
///   t = 0        dusk. Sun below the horizon, warm band west, stars not yet out.
///   0 - 40 s     the moon climbs off the horizon: deliberately oversized, harvest-orange, slightly
///                oblate while it is low.
///   throughout   ragged cloud scraps, long strips and occasional fast low scud cross the moon's path.
///                Every cloud that crosses steals the moon's light off the field for a beat, because
///                occlusion is computed from the cloud and moon directions rather than hoped for.
///   ~2 cells in  night. Handover to the §23.5 sky; §23.5's own rule (moon is a bearing early, a liar
///                later) is untouched here.
///
/// This is the single owner of the dusk->night ramp. It exposes statics for everything else to read, so
/// the storm (§18), the stars (NightSky) and any later system cannot disagree about what time it is.
/// </summary>
public sealed class DuskSky : MonoBehaviour
{
    // ---- what the rest of the game reads ------------------------------------------------
    /// <summary>0 = dusk, 1 = full night.</summary>
    public static float Night01 { get; private set; }
    /// <summary>0 = moon on the horizon, 1 = fully risen (~28° up).</summary>
    public static float MoonElev01 { get; private set; }
    /// <summary>0 = moon clear, 1 = a cloud is stealing it. Drives the light dip.</summary>
    public static float MoonOcclusion { get; private set; }
    public static bool IsNight { get; private set; }
    /// <summary>Stars are gated by this: nothing at dusk, full by night.</summary>
    public static float StarGate { get; private set; }
    /// <summary>The calm (no-storm) sky the storm blends away from.</summary>
    public static Color SunColor { get; private set; } = new Color(1f, 0.86f, 0.55f);
    public static float SunIntensity { get; private set; } = 1.35f;
    public static Color FogColor { get; private set; }
    public static Color AmbientSky { get; private set; }
    public static Color AmbientEquator { get; private set; }
    public static Color AmbientGround { get; private set; }
    /// <summary>Seconds since the run was handed over to the player.</summary>
    public static float PlaySeconds { get; private set; }

    /// <summary>§25.5: the rise takes 40 s. Night takes over at whichever comes first, this or 2 cells.</summary>
    public const float RiseSeconds = 40f;
    public const float NightCells = 2f;
    /// <summary>
    /// How far into the night the moon stays below the horizon. The moon rides the NIGHT's clock
    /// (see ApplyPalette), and this is the lag before it clears the corn: dusk is still the sun's.
    /// </summary>
    public const float MoonLag01 = 0.30f;

    /// <summary>
    /// M36 (Todd, 2026-09-26): "the moon MUST glow and give off light just like a regular halloween
    /// moon — it must light up the scene with proper shadows." Absolute requirement.
    ///
    /// The pair that used to sit inline in DriveLights was 0.30 -> 0.52, then multiplied by
    /// (1 - occlusion * 0.92). BuildClouds deliberately lays three strips straight across the moon's
    /// climb, so for most of the rise the moon's own light came out at ~0.03 — a field lit by nothing.
    /// The moon is the light source of the night now, and these are named so the number is arguable in
    /// one place instead of buried in an expression.
    /// </summary>
    public const float MoonIntensityFloor = 0.70f;
    public const float MoonIntensityPeak = 1.45f;

    /// <summary>M36: the most of the moon's light a cloud may take. A cloud dims the moon; it never
    /// switches it off, which is what (1 - occlusion * 0.92) used to do.</summary>
    public const float MoonCloudVeilMin = 0.67f;

    /// <summary>M36: how far the additive glow spills past the disc, as a multiple of the disc's own
    /// quad. The disc itself is sized A / DiscFraction, so 2.6 puts the halo's soft edge a good way out
    /// while its brightest part stays under the disc and never washes the maria.</summary>
    public const float HaloScale = 2.6f;

    /// <summary>M36: peak alpha of the additive halo. Deliberately modest — it is added to the sky,
    /// not composited over it, so a large number reads as a blown-out white blob, not a glow.</summary>
    public const float HaloStrength = 0.55f;

    /// <summary>
    /// M25b (§25.5): the moon's DISC is this fraction of T_Moon_Full.png's width — scripts/moon_build.py's
    /// DISC_FRACTION, and the two must move together. A quad that should subtend angle A is therefore sized
    /// A / DiscFraction: only the middle 62 % of the quad is the disc and the rest is the baked amber glow.
    /// Forget this factor and the moon silently shrinks to 62 % of the size §25.5 asks for.
    /// </summary>
    public const float DiscFraction = 0.62f;

    const float Radius = 57f;          // just inside NightSky's 58 so the clouds draw over the stars
    const int ScrapCount = 18;
    const int StripCount = 6;
    const int ScudCount = 5;

    Transform _follow;
    MazeData _maze;
    Camera _cam;
    Vector3 _startCell;

    float _night01, _moonElev01, _occlusion;
    bool _started;

    Light _sun;
    Light _moonLight;

    Mesh _mesh;
    Material _mat;
    Material _moonMat;
    Vector3[] _verts;
    Vector2[] _uvs;
    Color[] _colors;
    int _quadCount;

    // moon
    float _moonAzimuthDeg;
    Vector3 _moonDir = Vector3.forward;

    // clouds: azimuth, elevation, angular radius, drift deg/s, opacity, vertical squash, kind
    struct Cloud
    {
        public float Az, El, Rad, Drift, Opacity, Squash;
        public int Kind;             // 0 scrap, 1 strip, 2 scud
        public Vector3 Dir => DirOf(Az, El);
    }
    Cloud[] _clouds;

    public static DuskSky Instance { get; private set; }

    public static DuskSky Install(Transform player, MazeData maze)
    {
        if (Instance != null)
            Object.Destroy(Instance.gameObject);

        var go = new GameObject("DuskSky");
        var sky = go.AddComponent<DuskSky>();
        sky._follow = player;
        sky._maze = maze;
        Instance = sky;
        return sky;
    }

    void Awake()
    {
        Night01 = 0f;
        MoonElev01 = 0f;
        MoonOcclusion = 0f;
        IsNight = false;
        StarGate = 0f;
        PlaySeconds = 0f;

        // Sun and moon rise/set in opposite ends of the sky, which is what makes a moonrise at dusk read.
        const float sunAzimuth = 250f;   // west
        _moonAzimuthDeg = 74f;          // east

        _sun = FindSunLight();
        if (_sun != null)
            SunColor = _sun.color;

        var moonGo = new GameObject("MoonLight");
        moonGo.transform.SetParent(transform, false);
        _moonLight = moonGo.AddComponent<Light>();
        _moonLight.type = LightType.Directional;
        _moonLight.color = new Color(1f, 0.86f, 0.70f);
        _moonLight.intensity = 0f;
        // M36 (Todd, 2026-09-26): "the moon MUST glow and give off light just like a regular halloween
        // moon — it must light up the scene with proper shadows." Absolute requirement, so the old
        // phone-first `LightShadows.None` is gone: it was the single reason the corn could never cast a
        // shadow off the moon. Nothing downstream needed changing — the URP asset already ships
        // m_MainLightShadowsSupported 1, a 2048 main shadowmap, 4 cascades and m_SoftShadowsSupported 1.
        _moonLight.shadows = LightShadows.Soft;
        _moonLight.shadowStrength = 0.9f;
        _moonLight.shadowBias = 0.05f;
        _moonLight.shadowNormalBias = 0.4f;
        _moonLight.shadowNearPlane = 0.2f;
        moonGo.transform.rotation = Quaternion.Euler(2f, _moonAzimuthDeg, 0f);

        BuildClouds();
        AllocateMesh();
        _mat = SkyMaterial();
        _moonMat = MoonMaterial();
        var renderer = gameObject.AddComponent<MeshRenderer>();
        // Submesh 0 = warm band + torn clouds (the atlas). Submesh 1 = the moon, its own texture.
        renderer.sharedMaterials = new[] { _mat, _moonMat };
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.allowOcclusionWhenDynamic = false;
        var filter = gameObject.AddComponent<MeshFilter>();
        _mesh = new Mesh { name = "DuskSky" };
        _mesh.MarkDynamic();
        filter.sharedMesh = _mesh;

        // Keep the §23.5 fog/ambient the tree already had as the dusk end of the ramp.
        FogColor = RenderSettings.fogColor;
        AmbientSky = RenderSettings.ambientSkyColor;
        AmbientEquator = RenderSettings.ambientEquatorColor;
        AmbientGround = RenderSettings.ambientGroundColor;

        Rebuild();
    }

    /// <summary>
    /// The world's sun. NOT FindFirstObjectByType&lt;Light&gt;() — by install time the storm has already added
    /// its lightning light, so "the first light" is the wrong one and dusk would drive the lightning's
    /// direction while the sun stayed at the fixed noon elevation MazeWorldBuilder gave it.
    /// </summary>
    static Light FindSunLight()
    {
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional && l.name == "Sun") return l;
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional && l.name != "MoonLight" && l.name != "Lightning") return l;
        return null;
    }

    static Vector3 DirOf(float azDeg, float elDeg)
    {
        float az = azDeg * Mathf.Deg2Rad;
        float el = elDeg * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(el) * Mathf.Sin(az), Mathf.Sin(el), Mathf.Cos(el) * Mathf.Cos(az));
    }

    void Update()
    {
        if (_cam == null) _cam = Camera.main;
        if (_cam != null) transform.position = _cam.transform.position;

        // The clock runs from the moment the player is actually handed the body, not from scene load —
        // otherwise the whole rise plays behind the title screen. Dusk holds at t=0 until then, which is
        // the §25.5 beat: the title screen is already dusk.
        if (!_started && _follow != null)
        {
            var walker = _follow.GetComponent<FarmWalkerController>();
            if (walker == null || !walker.Frozen)
            {
                _started = true;
                PlaySeconds = 0f;
                _startCell = _follow.position;
            }
        }
        if (_started)
            PlaySeconds += Time.deltaTime;

        float byTime = Mathf.Clamp01(PlaySeconds / RiseSeconds);
        float byCells = 0f;
        if (_maze != null && _follow != null)
        {
            var cell = _maze.WorldToCell(_follow.position);
            var from = _maze.WorldToCell(_startCell);
            byCells = Mathf.Clamp01(Mathf.Max(Mathf.Abs(cell.x - from.x), Mathf.Abs(cell.y - from.y)) / NightCells);
        }
        _night01 = Mathf.Max(byTime, byCells);
        // The moon rides the NIGHT's clock, not one of its own. Night arrives by time OR by distance
        // (NightCells is 2 cells — a few paces), so on two separate clocks the field goes fully dark
        // in the first seconds of a walk and stays unlit: the moon is painted in the sky and casting
        // nothing on the maze, which is what a run into the deep corn looked like. One clock, so night
        // always has its moon — still climbing through it, just never absent from it.
        _moonElev01 = Mathf.Clamp01((_night01 - MoonLag01) / (1f - MoonLag01));
        IsNight = _night01 >= 0.999f;

        Night01 = _night01;
        MoonElev01 = _moonElev01;
        // Dusk -> night: the sky is not dark at t=0, which is the whole point of a dusk phase.
        StarGate = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((_night01 - 0.18f) / 0.72f));

        float moonEl = Mathf.Lerp(-1.5f, 28f, _moonElev01);
        _moonDir = DirOf(_moonAzimuthDeg, moonEl);

        // ---- occlusion: computed, not hoped for ------------------------------------------
        float occlusion = 0f;
        for (int i = 0; i < _clouds.Length; i++)
        {
            ref Cloud c = ref _clouds[i];
            c.Az += c.Drift * Time.deltaTime;
            if (c.Az > 360f) c.Az -= 360f;
            if (c.Az < 0f) c.Az += 360f;

            float dAz = Mathf.Abs(Mathf.DeltaAngle(c.Az, _moonAzimuthDeg)) * Mathf.Cos(moonEl * Mathf.Deg2Rad);
            float dEl = Mathf.Abs(c.El - moonEl);
            // A strip is a strip: its elevation extent is Rad * Squash, not Rad. Testing a squashed cloud
            // as if it were a circle is why the first run's occlusion never passed 0.32 — the geometry
            // said "not covering" while the picture said "covering".
            float ex = Mathf.Max(0.5f, c.Rad);
            float ey = Mathf.Max(0.5f, c.Rad * c.Squash);
            float moonR = MoonAngularRadiusDeg();
            float ox = Mathf.Clamp01(1f - (dAz - moonR) / ex);
            float oy = Mathf.Clamp01(1f - (dEl - moonR) / ey);
            float overlap = ox * oy;
            if (overlap > 0f)
                occlusion = Mathf.Max(occlusion, overlap * c.Opacity);
        }
        _occlusion = occlusion;
        MoonOcclusion = occlusion;

        DriveLights(moonEl);
        Rebuild();
    }

    /// <summary>M25b: the moon's direction on the dome, for the frame-space checks.</summary>
    public static Vector3 MoonDirection => Instance != null ? Instance._moonDir : Vector3.up;

    /// <summary>
    /// M25b: what the moon is actually drawn with, read off the live renderer — not what we intended.
    /// The milestone exists because a generated disc read as a blob, so the evidence has to name the texture.
    /// </summary>
    public static string MoonTextureReport()
    {
        var sky = Instance;
        if (sky == null) return "FAIL: no DuskSky instance";
        var mr = sky.GetComponent<MeshRenderer>();
        if (mr == null || mr.sharedMaterials.Length < 2 || mr.sharedMaterials[1] == null)
            return "FAIL: the sky renderer has no submesh-1 (moon) material";
        var mat = mr.sharedMaterials[1];
        var tex = mat.HasProperty("_BaseMap") ? mat.GetTexture("_BaseMap") : mat.mainTexture;
        if (tex == null) return "FAIL: the moon material has no texture";
        // Which shader is ACTUALLY drawing this: Shader.Find can fail in a player build if nothing else
        // references the shader, and then the material is the fallback and every blend instruction in the
        // C# is a no-op. Name it rather than assume it.
        var atlas = mr.sharedMaterials[0];
        return "moonShader=" + mat.shader.name +
               " atlasShader=" + (atlas != null ? atlas.shader.name : "?") +
               " texture=" + tex.name + " " + tex.width + "x" + tex.height +
               " submeshes=" + mr.sharedMaterials.Length +
               " dstBlend=" + (mat.HasProperty("_DstBlend") ? mat.GetFloat("_DstBlend").ToString("0") : "none") +
               " queue=" + mat.renderQueue;
    }

    /// <summary>
    /// M36 (Todd, 2026-09-26): what the moon's LIGHT actually is, read off the live Light rather than
    /// what DriveLights intended. Shadows are exactly the kind of setting that reports as "on" in a diff
    /// while being off in the build — the old line read `LightShadows.None` with a comment saying one
    /// shadow-casting light was enough on the phone, so the intent was never in doubt and the picture
    /// was still shadowless. So this names the mode, the strength, the intensity and the cloud veil,
    /// off the component itself.
    /// </summary>
    public static string MoonLightReport()
    {
        var sky = Instance;
        if (sky == null) return "FAIL: no DuskSky instance";
        var l = sky._moonLight;
        if (l == null) return "FAIL: there is no moon light";
        return "shadows=" + l.shadows +
               " shadowStrength=" + l.shadowStrength.ToString("0.00") +
               " intensity=" + l.intensity.ToString("0.000") +
               " colour=(" + l.color.r.ToString("0.00") + "," + l.color.g.ToString("0.00") + "," +
               l.color.b.ToString("0.00") + ")" +
               " elevation=" + l.transform.eulerAngles.x.ToString("0.0") + "deg" +
               " azimuth=" + l.transform.eulerAngles.y.ToString("0.0") + "deg" +
               " enabled=" + l.enabled +
               " cloudVeil=" + Mathf.Lerp(1f, MoonCloudVeilMin, Mathf.Clamp01(sky._occlusion)).ToString("0.000");
    }

    /// <summary>
    /// M25b: M25b: where the moon is drawn, in world terms — its quad centre, and its angular offset from
    /// the camera's forward direction. The evidence needs this because the moon is 28 degrees up at the end
    /// of the rise and a third-person camera pitched at the lane can have it above the frame entirely; then
    /// "we photographed the moon" would be a claim about a frame that does not contain it.
    /// </summary>
    public static Vector3 MoonWorldCentre
    {
        get
        {
            var sky = Instance;
            var cam = Camera.main;
            if (sky == null || cam == null) return Vector3.zero;
            return cam.transform.position + MoonDirection * Radius;
        }
    }

    /// <summary>Signed yaw and pitch between the camera's forward direction and the moon, in degrees.</summary>
    public static float MoonYawOffsetDeg
    {
        get
        {
            var cam = Camera.main;
            if (cam == null) return float.NaN;
            Vector3 toMoon = (MoonWorldCentre - cam.transform.position).normalized;
            Vector3 fwd = cam.transform.forward;
            return Vector3.SignedAngle(new Vector3(fwd.x, 0f, fwd.z), new Vector3(toMoon.x, 0f, toMoon.z), Vector3.up);
        }
    }

    /// <summary>How far the moon is above the camera's forward direction, in degrees.</summary>
    public static float MoonPitchOffsetDeg
    {
        get
        {
            var cam = Camera.main;
            if (cam == null) return float.NaN;
            Vector3 toMoon = (MoonWorldCentre - cam.transform.position).normalized;
            float fwdY = Mathf.Clamp(cam.transform.forward.y, -1f, 1f);
            return Mathf.Asin(Mathf.Clamp(toMoon.y, -1f, 1f)) * Mathf.Rad2Deg -
                   Mathf.Asin(fwdY) * Mathf.Rad2Deg;
        }
    }

    /// <summary>Signed yaw and pitch between the camera's forward direction and the moon, in degrees.</summary>
    public static string MoonViewReport()
    {
        var cam = Camera.main;
        if (cam == null) return "FAIL: no camera";
        float yaw = MoonYawOffsetDeg, pitch = MoonPitchOffsetDeg;
        Vector3 vp = cam.WorldToViewportPoint(MoonWorldCentre);
        bool onScreen = vp.z > 0f && vp.x > 0f && vp.x < 1f && vp.y > 0f && vp.y < 1f;
        return "cameraYaw=" + cam.transform.eulerAngles.y.ToString("0.0") + "deg" +
               " moonOffset yaw=" + yaw.ToString("0.0") + "deg pitch=" + pitch.ToString("0.0") + "deg" +
               " viewport=" + (onScreen ? "(" + vp.x.ToString("0.000") + "," + vp.y.ToString("0.000") + ")" : "OFF-SCREEN") +
               " moonWorldY=" + MoonWorldCentre.y.ToString("0.0");
    }

    /// <summary>M25b: the disc's angular radius in degrees — deliberately oversized (§25.5), as a constant
    /// so the frame-space check can use the same number the dome was built from.</summary>
    public const float DiscAngularRadiusDeg = 3.5f;

    float MoonAngularRadiusDeg()
    {
        // §25.5: deliberately oversized for the Halloween read — roughly 7 degrees across, not 0.5.
        return DiscAngularRadiusDeg;
    }

    void DriveLights(float moonEl)
    {
        float n = _night01;

        // The sun is BELOW the horizon from the first frame of play: dusk, not daylight.
        float sunEl = Mathf.Lerp(-2.0f, -13f, n);
        Color duskWarm = new Color(1.00f, 0.62f, 0.30f);
        Color duskCold = new Color(0.34f, 0.40f, 0.58f);
        SunColor = Color.Lerp(duskWarm, duskCold, n);
        SunIntensity = Mathf.Lerp(0.55f, 0.05f, n);

        if (_sun != null)
        {
            // Direction only. The colour and the intensity belong to whoever applies the sky palette
            // (StormWeather does, blending calm -> storm), so the two never write the same property.
            _sun.transform.rotation = Quaternion.Euler(sunEl, 250f, 0f);
        }

        // Warm band low in the west at dusk, gone by night.
        FogColor = Color.Lerp(new Color(0.86f, 0.58f, 0.34f), new Color(0.09f, 0.11f, 0.19f), n);
        AmbientSky = Color.Lerp(new Color(0.72f, 0.52f, 0.34f), new Color(0.10f, 0.13f, 0.24f), n);
        AmbientEquator = Color.Lerp(new Color(0.44f, 0.32f, 0.20f), new Color(0.07f, 0.09f, 0.16f), n);
        AmbientGround = Color.Lerp(new Color(0.18f, 0.13f, 0.08f), new Color(0.03f, 0.04f, 0.07f), n);

        // ---- the moon as LIGHT (M36) --------------------------------------------------------
        // Todd, 2026-09-26: "the moon MUST glow and give off light just like a regular halloween
        // moon — it must light up the scene with proper shadows." Three faults sat in the four lines
        // that used to be here, and this block is the fix for all three:
        //   1. NO SHADOWS  — LightShadows.None was set at build time (see Install), so the corn could
        //      not cast a shadow from the moon under any intensity. Fixed there, not here.
        //   2. TOO DIM      — 0.30 -> 0.52 peak, and moonUp held it down until 18 deg of elevation.
        //      A night field lit by its own moon has to be readable, not merely not-black.
        //   3. THE CLOUDS ATE IT — (1 - occlusion * 0.92) let one passing strip remove 92 % of the
        //      light, and BuildClouds deliberately lays three strips across the moon's climb. The
        //      moon's light came out ~0.03 for most of the rise: the unlit field Todd rejected.
        // A cloud now veils the moon to at worst MoonCloudVeilMin; it can never switch it off.
        float moonUp = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(moonEl / 12f));
        if (_moonLight != null)
        {
            _moonLight.color = Color.Lerp(new Color(1f, 0.72f, 0.44f), new Color(0.88f, 0.92f, 1f), _moonElev01);
            float veil = Mathf.Lerp(1f, MoonCloudVeilMin, Mathf.Clamp01(_occlusion));
            _moonLight.intensity = Mathf.Lerp(MoonIntensityFloor, MoonIntensityPeak, moonUp) * veil;
            // The lowest the light may sit. Below ~7 deg a directional light's shadows run to infinity
            // across the field and stop reading as shadows at all, so the lamp never goes flatter.
            _moonLight.transform.rotation = Quaternion.Euler(Mathf.Max(7f, moonEl), _moonAzimuthDeg, 0f);
        }
    }

    void BuildClouds()
    {
        var rng = new System.Random(MazeGenerator.Seed + 4477);
        var list = new List<Cloud>();

        for (int i = 0; i < ScrapCount; i++)
            list.Add(new Cloud
            {
                Az = (float)rng.NextDouble() * 360f,
                El = 4f + (float)rng.NextDouble() * 26f,
                Rad = 5f + (float)rng.NextDouble() * 7f,      // ragged scraps
                Drift = (0.35f + (float)rng.NextDouble() * 0.5f) * ((float)rng.NextDouble() < 0.5f ? -1f : 1f),
                Opacity = 0.35f + (float)rng.NextDouble() * 0.4f,
                Squash = 0.4f + (float)rng.NextDouble() * 0.25f,
                Kind = 0
            });

        for (int i = 0; i < StripCount; i++)
            list.Add(new Cloud
            {
                Az = (float)rng.NextDouble() * 360f,
                El = 8f + (float)rng.NextDouble() * 18f,
                Rad = 11f + (float)rng.NextDouble() * 9f,     // long strips crossing the moon's path
                Drift = (0.22f + (float)rng.NextDouble() * 0.35f) * ((float)rng.NextDouble() < 0.5f ? -1f : 1f),
                Opacity = 0.5f + (float)rng.NextDouble() * 0.35f,
                Squash = 0.16f + (float)rng.NextDouble() * 0.10f,
                Kind = 1
            });

        for (int i = 0; i < ScudCount; i++)
            list.Add(new Cloud
            {
                Az = (float)rng.NextDouble() * 360f,
                El = 2f + (float)rng.NextDouble() * 7f,
                Rad = 4f + (float)rng.NextDouble() * 4f,      // occasional fast low scud
                Drift = (1.5f + (float)rng.NextDouble() * 1.4f) * ((float)rng.NextDouble() < 0.5f ? -1f : 1f),
                Opacity = 0.45f + (float)rng.NextDouble() * 0.3f,
                Squash = 0.22f + (float)rng.NextDouble() * 0.12f,
                Kind = 2
            });

        // Three strips laid ACROSS the moon's path, at the elevations the moon climbs through during the
        // first 40 s (it rises -1.5° -> 28°). The spec asks for "a few long strips crossing the moon's
        // path" — leaving that to luck is how the first run produced zero occultation beats.
        int placed = 0;
        for (int i = 0; i < list.Count && placed < 3; i++)
        {
            if (list[i].Kind != 1) continue;
            var c = list[i];
            c.Az = _moonAzimuthDeg + (placed == 1 ? 2.5f : (placed == 2 ? -2f : 0f));
            c.El = 4f + placed * 8f;            // 4°, 12°, 20° — the moon passes each one on the way up
            c.Rad = 13f + placed * 1.5f;
            c.Squash = 0.20f;
            c.Opacity = 0.72f + placed * 0.06f; // over the beat threshold on its own
            c.Drift = 0.4f + placed * 0.12f;
            list[i] = c;
            placed++;
        }

        // A few scraps sit in the same band, so the moon also gets ragged cover, not only clean strips.
        int scrap = 0;
        for (int i = 0; i < list.Count && scrap < 5; i++)
        {
            if (list[i].Kind != 0) continue;
            var c = list[i];
            c.Az = _moonAzimuthDeg - 10f + scrap * 5f;
            c.El = 2f + scrap * 6f;
            c.Rad = 6f + scrap * 1.2f;
            c.Opacity = 0.62f;
            c.Drift = 0.5f + scrap * 0.15f;
            list[i] = c;
            scrap++;
        }

        _clouds = list.ToArray();
    }

    void AllocateMesh()
    {
        // M36: moon + the moon's additive glow + warm band + clouds
        _quadCount = 2 + 1 + ScrapCount + StripCount + ScudCount;
        _verts = new Vector3[_quadCount * 4];
        _uvs = new Vector2[_quadCount * 4];
        _colors = new Color[_quadCount * 4];
        for (int i = 0; i < _quadCount; i++)
        {
            int v = i * 4;
            _uvs[v] = new Vector2(0f, 0f);
            _uvs[v + 1] = new Vector2(1f, 0f);
            _uvs[v + 2] = new Vector2(0f, 1f);
            _uvs[v + 3] = new Vector2(1f, 1f);
        }
    }

    void Rebuild()
    {
        Camera cam = _cam != null ? _cam : Camera.main;
        Vector3 camRight = cam != null ? cam.transform.right : Vector3.right;
        Vector3 camUp = cam != null ? cam.transform.up : Vector3.up;

        int n = 0;

        // ---- the moon ---------------------------------------------------------------------
        float moonEl = Mathf.Lerp(-1.5f, 28f, _moonElev01);
        // M25b: the photograph IS the moon. Sized A / DiscFraction so the DISC still subtends §25.5's ~7
        // degrees and the extra 61 % of quad is the glow baked around it; UVs 0..1, not an atlas region.
        float size = Mathf.Tan(MoonAngularRadiusDeg() * Mathf.Deg2Rad) * Radius * 2f / DiscFraction;
        // Oblate near the horizon: atmospheric flattening, exaggerated for the Halloween read.
        float oblate = Mathf.Lerp(0.62f, 1f, Mathf.Clamp01(moonEl / 10f));
        Color moonCol = Color.Lerp(new Color(1f, 0.58f, 0.26f), new Color(1f, 0.90f, 0.74f), Mathf.Clamp01(moonEl / 16f));
        // M36 (Todd, 2026-09-26): "the moon MUST glow… just like a regular halloween moon." The old
        // alpha was Lerp(0.92, 0.80, …) * (1 - occlusion * 0.85) * Clamp01(0.25 + moonElev01 * 2):
        // 0.23 at the horizon, and any cloud near it drove that to 0.15. A moon you cannot see. A
        // halloween moon is the brightest thing in its frame, so the disc is effectively opaque and a
        // cloud may take only a little of it — matching the light's own veil, so the picture and the
        // illumination agree instead of disagreeing.
        float moonVeil = Mathf.Lerp(1f, 0.82f, Mathf.Clamp01(_occlusion));
        moonCol.a = Mathf.Lerp(0.99f, 0.94f, Mathf.Clamp01(moonEl / 10f)) * moonVeil;
        WriteQuad(n++, _moonDir * Radius, size, size * oblate, moonCol, camRight, camUp, FullUv);

        // ---- the moon's glow, spilling past its limb --------------------------------------
        // A wide soft falloff rather than a fatter disc: atlas region 2 is a radial ramp, drawn at
        // HaloScale × the disc so the drop-off lives outside the limb and the maria stay clean. Written
        // here, before the disc, because submesh 0 draws first — the disc then lands on top and the halo
        // reads as spill around it.
        //
        // MEASURED, NOT INTENDED: the harness reports `atlasShader=Sprites/Default dstBlend=none` in the
        // shipped build, so the additive `_DstBlend` this material asks for is a no-op — Sprites/Default
        // has no such property and never did. The halo is therefore alpha-composited, not added, and on a
        // near-black sky those read almost the same; it is NOT a true additive bloom, and calling it one
        // here would be the same class of lie as the `LightShadows.None` line that started this.
        {
            float haloSize = size * HaloScale;
            Color halo = Color.Lerp(new Color(1f, 0.52f, 0.20f), new Color(1f, 0.86f, 0.62f),
                                    Mathf.Clamp01(moonEl / 16f));
            halo.a = HaloStrength * moonVeil * Mathf.Clamp01(0.35f + _moonElev01);
            WriteQuad(n++, _moonDir * Radius, haloSize, haloSize * oblate, halo, camRight, camUp, 2f);
        }

        // ---- the warm band low in the west at dusk ----------------------------------------
        Vector3 bandDir = DirOf(250f, 5.5f);
        Color band = new Color(1f, 0.56f, 0.26f, (1f - _night01) * (1f - _night01) * 0.55f);
        WriteQuad(n++, bandDir * Radius, 62f, 15f, band, camRight, camUp, 0f);

        // ---- clouds ------------------------------------------------------------------------
        for (int i = 0; i < _clouds.Length; i++)
        {
            var c = _clouds[i];
            Vector3 d = c.Dir;
            float w = Mathf.Tan(c.Rad * Mathf.Deg2Rad) * Radius * 2f;
            float h = w * c.Squash;

            // Torn, front-lit edges: warm while the dusk light lasts, cooler as night takes over, and
            // brighter on the side facing the moon.
            float moonSide = Mathf.Clamp01(Vector3.Dot(d, _moonDir) * 0.5f + 0.5f);
            Color warmEdge = Color.Lerp(new Color(0.42f, 0.36f, 0.40f), new Color(1f, 0.72f, 0.42f), moonSide * 0.6f);
            Color coldEdge = Color.Lerp(new Color(0.30f, 0.34f, 0.44f), new Color(0.86f, 0.90f, 1f), moonSide * 0.5f);
            Color col = Color.Lerp(warmEdge, coldEdge, _night01);
            col.a = c.Opacity * Mathf.Lerp(0.55f, 1f, _night01);
            // fast scud gets a touch of speed blur by being drawn dimmer
            if (c.Kind == 2) col.a *= 0.75f;
            WriteQuad(n++, d * Radius, w, h, col, camRight, camUp, 1f);
        }

        _mesh.Clear();
        _mesh.vertices = _verts;
        _mesh.uv = _uvs;
        _mesh.colors = _colors;
        // M25b: the moon is submesh 1 now, so quad 0's six indices move out of the atlas submesh. Left in,
        // the atlas material would draw the old perlin disc straight over the photograph.
        // NOTE: fill the submeshes with SetTriangles, never `mesh.triangles` — that shorthand sets
        // subMeshCount back to 1, and the following SetTriangles(…, 1) then fails with "Submesh index is
        // out of bounds" and the moon is silently never drawn. That is exactly what the first build did.
        _mesh.subMeshCount = 2;
        _mesh.SetTriangles(SkyTriangles(), 0);
        _mesh.SetTriangles(MoonTriangles, 1);
        _mesh.bounds = new Bounds(Vector3.zero, Vector3.one * (Radius * 2.4f));
    }

    /// <summary>
    /// The moon's own quad: vertices 0-3, same winding as the atlas quads. Static because the moon is
    /// always quad 0 — AllocateMesh and Rebuild both assume it.
    /// </summary>
    static readonly int[] MoonTriangles = { 0, 2, 1, 2, 3, 1 };

    const float RegionCount = 3f;      // atlas columns: 0 = warm band, 1 = torn cloud, 2 = moon glow
    const float FullUv = -1f;          // regionU meaning "sample the whole texture, 0..1"

    /// <summary>Every quad except the moon (quad 0), for the atlas material.</summary>
    int[] SkyTriangles()
    {
        var tris = new int[(_quadCount - 1) * 6];
        int t = 0;
        for (int q = 1; q < _quadCount; q++)
        {
            int v = q * 4;
            tris[t++] = v; tris[t++] = v + 2; tris[t++] = v + 1;
            tris[t++] = v + 2; tris[t++] = v + 3; tris[t++] = v + 1;
        }
        return tris;
    }

    void WriteQuad(int index, Vector3 centre, float width, float height, Color color,
                   Vector3 camRight, Vector3 camUp, float regionU)
    {
        float hw = width * 0.5f, hh = height * 0.5f;
        Vector3 r = camRight * hw;
        Vector3 u = camUp * hh;
        int v = index * 4;
        _verts[v] = centre - r - u;
        _verts[v + 1] = centre + r - u;
        _verts[v + 2] = centre - r + u;
        _verts[v + 3] = centre + r + u;
        for (int i = 0; i < 4; i++) _colors[v + i] = color;
        float u0 = regionU < 0f ? 0f : regionU / RegionCount, u1 = regionU < 0f ? 1f : (regionU + 1f) / RegionCount;
        int v0 = index * 4;
        _uvs[v0] = new Vector2(u0, 0f);
        _uvs[v0 + 1] = new Vector2(u1, 0f);
        _uvs[v0 + 2] = new Vector2(u0, 1f);
        _uvs[v0 + 3] = new Vector2(u1, 1f);
    }

    /// <summary>
    /// The band and the torn clouds: one atlas, one draw call, additive because both are glows.
    /// Region 0 = soft horizontal band, 1 = torn cloud. (M25b: the moon used to be region 0 of this atlas;
    /// it is its own material now — see MoonMaterial.)
    /// </summary>
    static Material SkyMaterial()
    {
        return UnlitMaterial(SkyAtlas(128), BlendMode.One, 3210);
    }

    /// <summary>
    /// M25b: the moon's own material, sampling the photograph at full resolution.
    ///
    /// Why it left the atlas: the disc covers ~200 px of a 2556 px frame, and a 128 px atlas column would
    /// resample the 1024 px photograph down to 128 — throwing away exactly the maria and crater detail this
    /// pass exists for. Growing the atlas to suit the moon would mean a 1024 px column for two regions that
    /// are pure gradients. So the sky is two draw calls instead of one, and neither the band nor the clouds
    /// changed a pixel.
    ///
    /// Blending, measured rather than assumed: in the SHIPPED build the sky's materials are drawn by
    /// Sprites/Default, because Shader.Find("CornMaze/StarUnlit") does not resolve in a player (nothing else
    /// references the shader, so it is not in the build) and Sprites/Default is the explicit fallback here.
    /// Sprites/Default is SrcAlpha/OneMinusSrcAlpha, so the moon composites — its maria darken the disc
    /// instead of adding grey over the sky, which is the behaviour this milestone wants. The _SrcBlend /
    /// _DstBlend pair below is stated anyway: StarUnlit now honours it with SrcAlpha/One as the default, so
    /// the intent survives if the shader is ever included. DuskSky.MoonTextureReport() prints the shader
    /// actually in use, so this is a fact in the report, not an assumption in a comment.
    /// </summary>
    static Material MoonMaterial()
    {
        var tex = Resources.Load<Texture2D>("Sky/T_Moon_Full");
        var mat = UnlitMaterial(tex, BlendMode.OneMinusSrcAlpha, 3209);
        if (tex == null)
            Debug.LogWarning("[DuskSky] M25b: Resources/Sky/T_Moon_Full is missing — the moon has no disc");
        return mat;
    }

    /// <summary>Shared setup for the sky's unlit quads: no fog, no z-write, the given blend and queue.</summary>
    static Material UnlitMaterial(Texture2D tex, BlendMode dstBlend, int queue)
    {
        var shader = Shader.Find("CornMaze/StarUnlit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        var mat = new Material(shader);

        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
        if (mat.HasProperty("_Visibility")) mat.SetFloat("_Visibility", 1f);
        if (mat.HasProperty("_Twinkle")) mat.SetFloat("_Twinkle", 0f);
        mat.DisableKeyword("FOG_LINEAR");
        mat.DisableKeyword("FOG_EXP");
        mat.DisableKeyword("FOG_EXP2");
        mat.DisableKeyword("_FOG_ON");
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 1f);
        if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)dstBlend);
        if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
        mat.renderQueue = queue;
        return mat;
    }

    /// <summary>
    /// 128 px atlas, THREE columns — warm band, torn cloud, moon glow. Built in code, no assets.
    /// M25b: the moon's column is gone. It was a perlin patch pretending to be a disc; the moon is a
    /// photograph now (MoonMaterial), and nothing here paints it any more, so the blob cannot creep back.
    /// M36: a third column arrives that is not the moon either — it is the RADIAL FALLOFF the additive
    /// halo quad needs. It could not be done with column 0: that is a horizontal band, and WriteQuad maps
    /// it along the quad's own axis, so it would have drawn a soft bar across the sky instead of a glow.
    /// </summary>
    static Texture2D SkyAtlas(int size)
    {
        const int cols = 3;
        var tex = new Texture2D(size * cols, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            name = "DuskSkyAtlas"
        };

        var pix = new Color[size * cols * size];
        var rng = new System.Random(MazeGenerator.Seed + 991);

        // column 0: soft horizontal band
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dy = Mathf.Abs((y - (size - 1) * 0.5f)) / ((size - 1) * 0.5f);
                float a = Mathf.Clamp01(1f - dy);
                a = a * a * a;
                pix[y * size * cols + x] = new Color(1f, 1f, 1f, a);
            }

        // column 1: torn cloud — fbm with a hard ragged edge
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x - (size - 1) * 0.5f) / ((size - 1) * 0.5f);
                float dy = (y - (size - 1) * 0.5f) / ((size - 1) * 0.5f);
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float n = 0f, amp = 0.6f, freq = 2.2f;
                for (int o = 0; o < 3; o++)
                {
                    n += amp * Mathf.PerlinNoise((float)rng.NextDouble() * 0.01f + dx * freq + o * 3.1f,
                                                 (float)rng.NextDouble() * 0.01f + dy * freq + o * 1.7f);
                    amp *= 0.5f;
                    freq *= 2.1f;
                }
                float body = Mathf.Clamp01(1f - r * 1.15f);
                float a = Mathf.Clamp01(body * (0.55f + n));
                a = Mathf.SmoothStep(0f, 1f, a);
                pix[y * size * cols + size + x] = new Color(1f, 1f, 1f, a);
            }

        // column 2 (M36): radial glow for the moon's halo. A soft, wide falloff rather than a hard
        // disc — the moon's own quad supplies the hard edge, this only supplies the spill. Squared
        // twice so the centre is dense and the tail is long, which is what a bright object in haze
        // actually looks like; a linear ramp reads as a flat grey ball behind the moon.
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x - (size - 1) * 0.5f) / ((size - 1) * 0.5f);
                float dy = (y - (size - 1) * 0.5f) / ((size - 1) * 0.5f);
                float r = Mathf.Min(1f, Mathf.Sqrt(dx * dx + dy * dy));
                float a = 1f - r;
                a = a * a * a * a;
                pix[y * size * cols + size * 2 + x] = new Color(1f, 1f, 1f, a);
            }

        tex.SetPixels(pix);
        tex.Apply(false, true);
        return tex;
    }
}
