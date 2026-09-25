using UnityEngine;

/// <summary>
/// Procedural night sky that fades in with the storm. Most stars are random;
/// a faint pointer constellation sits near zenith and aims along the next
/// correct maze step toward the pot of gold.
/// Dome is parented to the main camera at a fixed radius inside farClipPlane.
/// </summary>
public sealed class NightSky : MonoBehaviour
{
    const int FieldCount = 420;
    const int HintCount = 7;
    const float Radius = 58f;
    const float FieldSize = 0.62f;
    const float HintSize = 0.88f;
    const float GoldTipSize = 1.12f;

    Transform _follow;
    Transform _gold;
    MazeData _maze;
    Camera _cam;
    Mesh _mesh;
    MeshRenderer _renderer;
    Material _mat;
    Vector3[] _verts;
    Vector2[] _uvs;
    Color[] _colors;
    int[] _tris;

    Vector3[] _fieldDir;
    Color[] _fieldCol;
    float[] _fieldSize;

    Vector3[] _hintLocal;
    Color[] _hintCol;
    float[] _hintSize;

    public static NightSky Install(Transform player, Transform gold, MazeData maze)
    {
        var existing = Object.FindFirstObjectByType<NightSky>();
        if (existing != null)
            Object.Destroy(existing.gameObject);

        var go = new GameObject("NightSky");
        var sky = go.AddComponent<NightSky>();
        sky._follow = player;
        sky._gold = gold;
        sky._maze = maze;
        return sky;
    }

    void Start()
    {
        BindCamera();
        BuildField();
        BuildHint();
        AllocateMesh();
        _mat = StarMaterial();
        _renderer = gameObject.AddComponent<MeshRenderer>();
        _renderer.sharedMaterial = _mat;
        _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _renderer.receiveShadows = false;
        _renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        _renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        _renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        _renderer.allowOcclusionWhenDynamic = false;
        var filter = gameObject.AddComponent<MeshFilter>();
        _mesh = new Mesh { name = "NightStars" };
        _mesh.MarkDynamic();
        filter.sharedMesh = _mesh;
        Rebuild(1f, Vector3.forward);
        if (_mat.HasProperty("_Visibility"))
            _mat.SetFloat("_Visibility", 0f);
    }

    void LateUpdate()
    {
        if (_cam == null)
            BindCamera();

        // Keep the dome centered on the camera so looking straight up fills the view.
        if (_cam != null)
            transform.position = _cam.transform.position;
        else if (_follow != null)
            transform.position = _follow.position + Vector3.up * 1.2f;

        Vector3 toward = NextPathDir();
        float storm = StormWeather.Intensity;
        float vis = StarVisibility(storm);
        // M25 (§25.5): no stars at dusk. The stars fade in as the sky darkens, layer on layer with the
        // storm — the dusk phase is the reason this sky has a beginning at all.
        vis *= DuskSky.Instance != null ? DuskSky.StarGate : 1f;
        vis *= 1f - StormWeather.Flash * 0.72f;

        if (_mat != null)
        {
            if (_mat.HasProperty("_Visibility"))
                _mat.SetFloat("_Visibility", vis);
            if (_mat.HasProperty("_Twinkle"))
                _mat.SetFloat("_Twinkle", 0.32f + 0.42f * storm);
            if (_mat.HasProperty("_Color"))
                _mat.SetColor("_Color", Color.white);
            if (_mat.HasProperty("_BaseColor"))
                _mat.SetColor("_BaseColor", Color.white);
        }

        if (_renderer != null)
            _renderer.enabled = vis > 0.02f;

        if (vis > 0.02f)
            Rebuild(vis, toward);
    }

    /// <summary>
    /// Fade in early once darkness begins; readable by mid-storm; capped so additive blend does not blow out.
    /// </summary>
    static float StarVisibility(float storm)
    {
        if (storm <= 0.02f) return 0f;
        float u = Mathf.Clamp01((storm - 0.03f) / 0.52f);
        float smooth = u * u * (3f - 2f * u);
        float vis = Mathf.Lerp(0.14f, 0.72f, smooth);
        if (storm > 0.08f)
            vis = Mathf.Max(vis, 0.32f + 0.30f * Mathf.Clamp01((storm - 0.08f) / 0.45f));
        return Mathf.Clamp(vis, 0f, 0.78f);
    }

    void BindCamera()
    {
        _cam = Camera.main;
        if (_cam != null && _cam.farClipPlane < Radius + 40f)
            _cam.farClipPlane = Radius + 40f;
    }

    /// <summary>
    /// Horizontal direction of the next correct corridor step from the player's cell toward gold.
    /// </summary>
    Vector3 NextPathDir()
    {
        Vector3 origin = _follow != null
            ? _follow.position
            : (_cam != null ? _cam.transform.position : transform.position);

        if (_maze != null)
        {
            var cell = _maze.NearestPathCell(origin);
            var next = _maze.NextStepTowardGold(cell);
            if (next != cell)
            {
                Vector3 a = _maze.CellToWorld(cell.x, cell.y);
                Vector3 b = _maze.CellToWorld(next.x, next.y);
                Vector3 d = b - a;
                d.y = 0f;
                if (d.sqrMagnitude > 0.01f)
                    return d.normalized;
            }
        }

        // Fallback: compass toward gold if solution graph is unavailable.
        Vector3 gold = _gold != null ? _gold.position : origin + Vector3.forward * 40f;
        Vector3 flat = gold - origin;
        flat.y = 0f;
        if (flat.sqrMagnitude < 0.04f)
            return Vector3.forward;
        return flat.normalized;
    }

    void BuildField()
    {
        var rng = new System.Random(MazeGenerator.Seed + 904);
        _fieldDir = new Vector3[FieldCount];
        _fieldCol = new Color[FieldCount];
        _fieldSize = new float[FieldCount];
        int i = 0;
        int guard = 0;
        while (i < FieldCount && guard < FieldCount * 8)
        {
            guard++;
            float u = (float)rng.NextDouble();
            float v = (float)rng.NextDouble();
            float az = u * Mathf.PI * 2f;
            // Bias toward upper hemisphere so zenith look-up is dense.
            float elev = Mathf.Asin(Mathf.Clamp01(0.08f + v * 0.92f));
            var dir = new Vector3(
                Mathf.Cos(elev) * Mathf.Sin(az),
                Mathf.Sin(elev),
                Mathf.Cos(elev) * Mathf.Cos(az));
            float dim = 0.42f + (float)rng.NextDouble() * 0.28f;
            float blue = 0.04f + (float)rng.NextDouble() * 0.10f;
            _fieldDir[i] = dir;
            _fieldCol[i] = new Color(0.88f + blue * 0.12f, 0.90f + blue * 0.05f, 1f, dim);
            _fieldSize[i] = FieldSize * (0.65f + (float)rng.NextDouble() * 0.55f);
            i++;
        }
    }

    void BuildHint()
    {
        // Subtle arrow near zenith: bowl + handle; tip is slightly warmer.
        // Local +X is along the tip (next correct path step).
        _hintLocal = new[]
        {
            new Vector3(-0.62f, 0f, -0.22f),
            new Vector3(-0.58f, 0f, 0.20f),
            new Vector3(-0.28f, 0f, 0.30f),
            new Vector3(-0.24f, 0f, -0.26f),
            new Vector3(0.06f, 0f, 0.10f),
            new Vector3(0.38f, 0f, 0.05f),
            new Vector3(0.78f, 0f, 0f)
        };
        _hintCol = new Color[HintCount];
        _hintSize = new float[HintCount];
        for (int i = 0; i < HintCount; i++)
        {
            bool tip = i == HintCount - 1;
            float a = tip ? 0.78f : 0.58f + (float)i * 0.02f;
            _hintCol[i] = tip
                ? new Color(1f, 0.93f, 0.72f, a)
                : new Color(0.92f, 0.95f, 1f, a);
            _hintSize[i] = tip ? GoldTipSize : HintSize * (0.92f + (float)i * 0.025f);
        }
    }

    void AllocateMesh()
    {
        int stars = FieldCount + HintCount;
        _verts = new Vector3[stars * 4];
        _uvs = new Vector2[stars * 4];
        _colors = new Color[stars * 4];
        _tris = new int[stars * 6];
        for (int i = 0; i < stars; i++)
        {
            int v = i * 4;
            int t = i * 6;
            _uvs[v] = new Vector2(0f, 0f);
            _uvs[v + 1] = new Vector2(1f, 0f);
            _uvs[v + 2] = new Vector2(0f, 1f);
            _uvs[v + 3] = new Vector2(1f, 1f);
            _tris[t] = v;
            _tris[t + 1] = v + 2;
            _tris[t + 2] = v + 1;
            _tris[t + 3] = v + 2;
            _tris[t + 4] = v + 3;
            _tris[t + 5] = v + 1;
        }
    }

    void Rebuild(float vis, Vector3 toward)
    {
        Camera cam = _cam != null ? _cam : Camera.main;
        Vector3 camRight = cam != null ? cam.transform.right : Vector3.right;
        Vector3 camUp = cam != null ? cam.transform.up : Vector3.up;

        int n = 0;
        for (int i = 0; i < FieldCount; i++)
        {
            var col = _fieldCol[i];
            WriteStar(n++, _fieldDir[i] * Radius, _fieldSize[i], col, camRight, camUp);
        }

        Vector3 fwd = toward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.001f)
            fwd = Vector3.forward;
        fwd.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, fwd);
        if (right.sqrMagnitude < 0.001f)
            right = Vector3.right;
        right.Normalize();

        // Sit almost at zenith; tip leans slightly toward the next correct cell.
        const float elevDeg = 86f;
        float elev = elevDeg * Mathf.Deg2Rad;
        Vector3 center = (fwd * Mathf.Cos(elev) + Vector3.up * Mathf.Sin(elev)).normalized;
        float spread = 0.20f;

        for (int i = 0; i < HintCount; i++)
        {
            Vector3 local = _hintLocal[i];
            Vector3 dir = (center + fwd * (local.x * spread) + right * (local.z * spread)).normalized;
            var col = _hintCol[i];
            col.a = Mathf.Clamp01(col.a * (0.70f + 0.22f * vis));
            WriteStar(n++, dir * Radius, _hintSize[i], col, camRight, camUp);
        }

        _mesh.Clear();
        _mesh.vertices = _verts;
        _mesh.uv = _uvs;
        _mesh.colors = _colors;
        _mesh.triangles = _tris;
        _mesh.bounds = new Bounds(Vector3.zero, Vector3.one * (Radius * 2.2f));
    }

    void WriteStar(int index, Vector3 center, float size, Color color, Vector3 camRight, Vector3 camUp)
    {
        float h = size * 0.5f;
        Vector3 r = camRight * h;
        Vector3 u = camUp * h;
        int v = index * 4;
        _verts[v] = center - r - u;
        _verts[v + 1] = center + r - u;
        _verts[v + 2] = center - r + u;
        _verts[v + 3] = center + r + u;
        _colors[v] = color;
        _colors[v + 1] = color;
        _colors[v + 2] = color;
        _colors[v + 3] = color;
    }

    static Material StarMaterial()
    {
        var shader = Shader.Find("CornMaze/StarUnlit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        var mat = new Material(shader);
        var tex = SoftDisc(32);
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
        if (mat.HasProperty("_Twinkle")) mat.SetFloat("_Twinkle", 1f);
        if (mat.HasProperty("_Visibility")) mat.SetFloat("_Visibility", 0f);
        mat.DisableKeyword("FOG_LINEAR");
        mat.DisableKeyword("FOG_EXP");
        mat.DisableKeyword("FOG_EXP2");
        mat.DisableKeyword("_FOG_ON");
        if (mat.HasProperty("_SoftParticlesEnabled")) mat.SetFloat("_SoftParticlesEnabled", 0f);
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 1f);
        if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
        mat.renderQueue = 3200;
        return mat;
    }

    static Texture2D SoftDisc(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            name = "StarDisc"
        };
        var pix = new Color[size * size];
        float mid = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - mid) / mid;
                float dy = (y - mid) / mid;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - r);
                a = a * a;
                float core = Mathf.Clamp01(1f - r * 2.2f);
                core *= core;
                pix[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a * 0.50f + core * 0.78f));
            }
        }
        tex.SetPixels(pix);
        tex.Apply(false, true);
        return tex;
    }
}
