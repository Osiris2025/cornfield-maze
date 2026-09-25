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
    Vector3[] _verts;
    Vector2[] _uvs;
    Color[] _colors;
    int[] _tris;
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
        _moonLight.shadows = LightShadows.None;   // one shadow-casting light is enough on the phone
        moonGo.transform.rotation = Quaternion.Euler(2f, _moonAzimuthDeg, 0f);

        BuildClouds();
        AllocateMesh();
        _mat = SkyMaterial();
        var renderer = gameObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = _mat;
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
        _moonElev01 = Mathf.Clamp01(PlaySeconds / RiseSeconds);
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

    float MoonAngularRadiusDeg()
    {
        // §25.5: deliberately oversized for the Halloween read — roughly 7 degrees across, not 0.5.
        return 3.5f;
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

        // Moon light: the field is lit by the moon once it is up, and a crossing cloud takes that away
        // for a beat — that interruption is the "brilliant" part of §25.5.
        float moonUp = Mathf.Clamp01(moonEl / 18f);
        if (_moonLight != null)
        {
            _moonLight.color = Color.Lerp(new Color(1f, 0.66f, 0.38f), new Color(0.76f, 0.82f, 1f), _moonElev01);
            _moonLight.intensity = moonUp * (0.30f + 0.22f * n) * (1f - _occlusion * 0.92f);
            _moonLight.transform.rotation = Quaternion.Euler(Mathf.Max(1f, moonEl), _moonAzimuthDeg, 0f);
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
        // moon + warm band + clouds + 1 sun glow disc
        _quadCount = 1 + 1 + ScrapCount + StripCount + ScudCount;
        _verts = new Vector3[_quadCount * 4];
        _uvs = new Vector2[_quadCount * 4];
        _colors = new Color[_quadCount * 4];
        _tris = new int[_quadCount * 6];
        for (int i = 0; i < _quadCount; i++)
        {
            int v = i * 4, t = i * 6;
            _uvs[v] = new Vector2(0f, 0f);
            _uvs[v + 1] = new Vector2(1f, 0f);
            _uvs[v + 2] = new Vector2(0f, 1f);
            _uvs[v + 3] = new Vector2(1f, 1f);
            _tris[t] = v; _tris[t + 1] = v + 2; _tris[t + 2] = v + 1;
            _tris[t + 3] = v + 2; _tris[t + 4] = v + 3; _tris[t + 5] = v + 1;
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
        float size = Mathf.Tan(MoonAngularRadiusDeg() * Mathf.Deg2Rad) * Radius * 2f;
        // Oblate near the horizon: atmospheric flattening, exaggerated for the Halloween read.
        float oblate = Mathf.Lerp(0.62f, 1f, Mathf.Clamp01(moonEl / 10f));
        Color moonCol = Color.Lerp(new Color(1f, 0.58f, 0.26f), new Color(1f, 0.90f, 0.74f), Mathf.Clamp01(moonEl / 16f));
        moonCol.a = Mathf.Lerp(0.92f, 0.80f, Mathf.Clamp01(moonEl / 10f)) * (1f - _occlusion * 0.85f)
                    * Mathf.Clamp01(0.25f + _moonElev01 * 2f);
        WriteQuad(n++, _moonDir * Radius, size, size * oblate, moonCol, camRight, camUp, 0f);

        // ---- the warm band low in the west at dusk ----------------------------------------
        Vector3 bandDir = DirOf(250f, 5.5f);
        Color band = new Color(1f, 0.56f, 0.26f, (1f - _night01) * (1f - _night01) * 0.55f);
        WriteQuad(n++, bandDir * Radius, 62f, 15f, band, camRight, camUp, 1f);

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
            WriteQuad(n++, d * Radius, w, h, col, camRight, camUp, 2f);
        }

        _mesh.Clear();
        _mesh.vertices = _verts;
        _mesh.uv = _uvs;
        _mesh.colors = _colors;
        _mesh.triangles = _tris;
        _mesh.bounds = new Bounds(Vector3.zero, Vector3.one * (Radius * 2.4f));
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
        float u0 = regionU / 3f, u1 = (regionU + 1f) / 3f;
        int v0 = index * 4;
        _uvs[v0] = new Vector2(u0, 0f);
        _uvs[v0 + 1] = new Vector2(u1, 0f);
        _uvs[v0 + 2] = new Vector2(u0, 1f);
        _uvs[v0 + 3] = new Vector2(u1, 1f);
    }

    /// <summary>
    /// One material for moon, band and clouds: each quad samples its own region of an atlas built here,
    /// so the whole sky is one draw call. Region 0 = moon disc, 1 = soft horizontal band, 2 = torn cloud.
    /// </summary>
    static Material SkyMaterial()
    {
        var shader = Shader.Find("CornMaze/StarUnlit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        var mat = new Material(shader);

        var tex = SkyAtlas(128);
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
        if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)BlendMode.One);
        if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
        mat.renderQueue = 3210;   // after the stars (3200) so cloud shapes sit over them
        return mat;
    }

    /// <summary>128x128 atlas: 3 columns — moon, warm band, torn cloud. Built in code, no assets.</summary>
    static Texture2D SkyAtlas(int size)
    {
        var tex = new Texture2D(size * 3, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            name = "DuskSkyAtlas"
        };

        var pix = new Color[size * 3 * size];
        var rng = new System.Random(MazeGenerator.Seed + 991);

        // column 0: moon disc with a soft limb
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x - (size - 1) * 0.5f) / ((size - 1) * 0.5f);
                float dy = (y - (size - 1) * 0.5f) / ((size - 1) * 0.5f);
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01((1f - r) / 0.10f);
                // a few darker maria so it is a moon, not a blob
                float maria = Mathf.Clamp01(0.72f + 0.28f * Mathf.PerlinNoise(dx * 2.4f + 5f, dy * 2.4f + 5f));
                pix[y * size * 3 + x] = new Color(maria, maria, maria, a);
            }

        // column 1: soft horizontal band
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dy = Mathf.Abs((y - (size - 1) * 0.5f)) / ((size - 1) * 0.5f);
                float a = Mathf.Clamp01(1f - dy);
                a = a * a * a;
                pix[y * size * 3 + size + x] = new Color(1f, 1f, 1f, a);
            }

        // column 2: torn cloud — fbm with a hard ragged edge
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
                pix[y * size * 3 + size * 2 + x] = new Color(1f, 1f, 1f, a);
            }

        tex.SetPixels(pix);
        tex.Apply(false, true);
        return tex;
    }
}
