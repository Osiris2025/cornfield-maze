using UnityEngine;

/// <summary>
/// M29 (§17): the walked lane, built as a mesh whose margin is ragged in GEOMETRY rather than a stripe.
///
/// The lane is superimposed on the field — the same floor the player already has, with gravel laid over it —
/// and the join is what makes it read as a path rather than a cut line. Two ways to do that were offered:
/// a blend shader, or jittering the outer vertices with the derived mask. This is the second, because:
///
///   * the lane keeps URP/Lit, so it gets the same lighting, normal maps and PathMudWetness wetness path as
///     everything else, with no lit shader to hand-write and maintain;
///   * the raggedness is real geometry, so it also breaks the silhouette at grazing angles and in the
///     shadows, which a fragment-level blend cannot;
///   * it needs no mask lookup per pixel on the phone.
///
/// The cut is a function of the vertex's WORLD position, not of the piece it belongs to. Lane pieces overlap
/// (a cell square meets the connectors that run out of it), so two pieces sharing a margin line must displace
/// it by the same amount or the join would tear: sampling the mask in world space makes that automatic — the
/// same point on the ground gets the same cut whoever builds it. UVs are world-based for the same reason, so
/// the gravel tiles continuously across piece boundaries and no seam appears where two pieces meet.
/// </summary>
public static class GroundLaneMesh
{
    /// <summary>How far the mask may pull a margin inwards, in metres. The lane is 2.08 m wide, so 0.42 m
    /// can never close it.</summary>
    const float MaxCut = 0.42f;

    /// <summary>Metres of ground per repeat of the edge mask. Deliberately not the albedo's 2 m: if the
    /// raggedness repeated at the same scale as the gravel the eye would pick the tiling up twice over.</summary>
    const float MaskMetres = 2.4f;

    /// <summary>Roughly one boundary vertex every 0.25 m along the long axis.</summary>
    const float VertexSpacing = 0.25f;

    static Texture2D _mask;

    /// <summary>Whether the mask was actually readable, i.e. whether the margin is ragged or straight.
    /// Reported rather than assumed — a silent straight lane would still build and still look like a stripe.</summary>
    public static bool MaskLoaded { get; private set; }

    /// <summary>The mask's own statistics, sampled at load. The cut thresholds are derived from these rather
    /// than from constants guessed off the preview: a mask whose values sit between 0.20 and 0.70 would be cut
    /// to nothing by a fixed 0.46-0.86 window, and the lanes would come out perfectly straight (which is
    /// exactly what the first M29 run measured: 0.00 m of cut over 6287 boundary vertices).</summary>
    public static float MaskMin { get; private set; }
    public static float MaskMean { get; private set; }
    public static float MaskMax { get; private set; }
    public static float MaskLowCut { get; private set; }
    public static float MaskHighCut { get; private set; }

    /// <summary>Loads and caches the derived raggedness mask, and works out where its cuts should start.</summary>
    public static bool Prepare()
    {
        if (_mask != null) return MaskLoaded;
        _mask = Resources.Load<Texture2D>("Ground/T_Ground_LaneEdge");
        MaskLoaded = _mask != null && _mask.isReadable;
        if (_mask != null && !_mask.isReadable)
            Debug.LogWarning("M29: Resources/Ground/T_Ground_LaneEdge is not Read/Write enabled — lane margins " +
                             "will be straight. Run CornMaze.EditorTools.CornMazeGroundImport.ApplyAll.");
        if (_mask == null)
            Debug.LogWarning("M29: Resources/Ground/T_Ground_LaneEdge is missing — lane margins will be straight.");
        if (MaskLoaded) SampleThresholds();
        return MaskLoaded;
    }

    static void SampleThresholds()
    {
        const int n = 64;
        var values = new float[n * n];
        float min = 1f, max = 0f, sum = 0f;
        int k = 0;
        for (int j = 0; j < n; j++)
        {
            for (int i = 0; i < n; i++)
            {
                float m = _mask.GetPixelBilinear((i + 0.5f) / n, (j + 0.5f) / n).r;
                values[k++] = m;
                if (m < min) min = m;
                if (m > max) max = m;
                sum += m;
            }
        }
        System.Array.Sort(values);
        MaskMin = min;
        MaskMax = max;
        MaskMean = sum / k;
        // Cut where the mask is above its own median, at full depth by its top tenth: about a third of the
        // margin is pulled back and the deepest cuts are in patches, which is a ragged edge rather than a
        // smaller stripe.
        MaskLowCut = values[Mathf.Clamp((int)(k * 0.55f), 0, k - 1)];
        MaskHighCut = values[Mathf.Clamp((int)(k * 0.90f), 0, k - 1)];
        if (MaskHighCut - MaskLowCut < 0.02f) MaskHighCut = MaskLowCut + 0.02f;
    }

    /// <summary>
    /// A flat lane piece: <paramref name="w"/> along X, <paramref name="d"/> along Z, centred on
    /// <paramref name="centre"/>, with every boundary vertex pulled inwards by the mask.
    /// </summary>
    public static Mesh Build(float w, float d, Vector3 centre)
    {
        int nx = Mathf.Clamp(Mathf.RoundToInt(w / VertexSpacing), 2, 24);
        int nz = Mathf.Clamp(Mathf.RoundToInt(d / VertexSpacing), 2, 24);

        var verts = new Vector3[(nx + 1) * (nz + 1)];
        var uvs = new Vector2[verts.Length];
        var tris = new int[nx * nz * 6];

        float tile = Materials.GroundTileMetres;

        for (int j = 0; j <= nz; j++)
        {
            for (int i = 0; i <= nx; i++)
            {
                float lx = -w * 0.5f + w * i / nx;
                float lz = -d * 0.5f + d * j / nz;
                float wx = centre.x + lx;
                float wz = centre.z + lz;
                float y = 0f;

                bool xMin = i == 0, xMax = i == nx, zMin = j == 0, zMax = j == nz;
                if (xMin || xMax || zMin || zMax)
                {
                    float cut = MaxCut * MaskAt(wx, wz);
                    // Inwards along the axis this vertex is on. At a corner both axes move, which is what
                    // keeps the corner closed instead of peeling it open.
                    if (xMin) lx += cut;
                    if (xMax) lx -= cut;
                    if (zMin) lz += cut;
                    if (zMax) lz -= cut;
                }

                int v = j * (nx + 1) + i;
                verts[v] = new Vector3(lx, y, lz);
                uvs[v] = new Vector2(wx / tile, wz / tile);   // world metres -> repeats, continuous across pieces
            }
        }

        int t = 0;
        for (int j = 0; j < nz; j++)
        {
            for (int i = 0; i < nx; i++)
            {
                int a = j * (nx + 1) + i;        // (i, j)
                int b = a + 1;                   // (i+1, j)
                int c = a + (nx + 1);            // (i, j+1)
                int d2 = c + 1;                  // (i+1, j+1)
                tris[t++] = a; tris[t++] = d2; tris[t++] = b;   // wound so the normal faces up
                tris[t++] = a; tris[t++] = c;  tris[t++] = d2;
            }
        }

        var mesh = new Mesh { name = "LanePiece" };
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateNormals();     // flat +Y everywhere; cheaper than hand-writing it and always right
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// The mask, 0 at "gravel all the way to the edge" and 1 at "the crop has taken this back". Thresholded
    /// and smoothstepped so most of the margin stays at full width and the cuts arrive in patches — an even
    /// ripple along the whole edge would just be a different stripe.
    /// </summary>
    static float MaskAt(float worldX, float worldZ)
    {
        if (!MaskLoaded) return 0f;
        float u = Mathf.Repeat(worldX / MaskMetres, 1f);
        float v = Mathf.Repeat(worldZ / MaskMetres, 1f);
        float m = _mask.GetPixelBilinear(u, v).r;          // the mask is Non-Color; R carries it
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(MaskLowCut, MaskHighCut, m));
    }

    /// <summary>Forgets the cached mask. Only used if the world is rebuilt with different resources.</summary>
    public static void Reset()
    {
        _mask = null;
        MaskLoaded = false;
        MaskMin = MaskMean = MaskMax = MaskLowCut = MaskHighCut = 0f;
    }
}
