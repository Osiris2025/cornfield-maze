using UnityEngine;

public static class Materials
{
    static Shader _lit;

    public static Material Lit(Color color, float smoothness = 0.15f, float metallic = 0f)
    {
        if (_lit == null)
        {
            _lit = Shader.Find("Universal Render Pipeline/Lit");
            if (_lit == null) _lit = Shader.Find("Standard");
            if (_lit == null) _lit = Shader.Find("Sprites/Default");
        }

        var mat = new Material(_lit);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
        return mat;
    }

    // ---- M29: the ground (Todd, 2026-09-25; §17) --------------------------------------------------
    //
    // The floor used to be procedural noise: Gravel at 128 px, FieldGrass at 128 px, PathGrass at 96 px,
    // tiled a handful of times across a 60 m field. A 128 px tile across a 2 m lane can only read as
    // synthetic, which is why the floor looked flat. Todd supplied six photographic PBR sets (Poly Haven,
    // CC0 — licence, checksums and synthetic tells in source-art/ground/PROVENANCE.md) which
    // scripts/ground_build.py blended into the maps under Resources/Ground. Those are what the maze now
    // wears; the noise generators are DELETED rather than left beside them, so nothing can drift back.

    /// <summary>
    /// Metres of maze floor per repeat of a ground map: 1K over 2 m, i.e. 512 px per metre. Matches the
    /// source (each set is authored as a 1K pass over roughly 2 m of ground) and shows litter grain at the
    /// player's feet without the repeat becoming readable across a 4 m cell.
    /// </summary>
    public const float GroundTileMetres = 2f;

    public static Material GroundField()
    {
        return Ground("Ground/T_Ground_Field", "Ground/T_Ground_Field_N",
                      new Color(0.88f, 0.83f, 0.72f), 0.05f);
    }

    public static Material GroundLane()
    {
        return Ground("Ground/T_Ground_Lane", "Ground/T_Ground_Lane_N",
                      new Color(0.94f, 0.92f, 0.88f), 0.11f);
    }

    /// <summary>
    /// A URP/Lit ground material from the derived albedo + normal maps.
    ///
    /// Smoothness is a constant per material, not the sets' `_R` roughness: driving it would mean repacking
    /// roughness into URP's metallic/smoothness slot (or the albedo's alpha via SmoothnessTextureChannel) —
    /// another full-size map in phone memory for a dry/wet variation PathMudWetness already drives at
    /// runtime on the lane. The `_R` maps stay imported and unused on purpose; the report says so.
    /// </summary>
    static Material Ground(string albedo, string normal, Color tint, float smoothness)
    {
        var mat = Lit(tint, smoothness, 0f);
        var a = Resources.Load<Texture2D>(albedo);
        var n = Resources.Load<Texture2D>(normal);
        if (a == null) Debug.LogWarning("M29: ground albedo missing: Resources/" + albedo);
        if (n == null) Debug.LogWarning("M29: ground normal missing: Resources/" + normal);

        if (mat.HasProperty("_BaseMap"))
        {
            if (a != null) mat.SetTexture("_BaseMap", a);
            // The lane meshes carry the metres-per-tile in their UVs (M29), so the material tiles once.
            mat.SetTextureScale("_BaseMap", Vector2.one);
        }
        if (mat.HasProperty("_MainTex") && a != null) mat.SetTexture("_MainTex", a);
        if (n != null)
        {
            if (mat.HasProperty("_BumpMap")) mat.SetTexture("_BumpMap", n);
            if (mat.HasProperty("_NormalMap")) mat.SetTexture("_NormalMap", n);
            if (mat.HasProperty("_BumpScale")) mat.SetFloat("_BumpScale", 1f);
            mat.EnableKeyword("_NORMALMAP");
        }
        return mat;
    }

    public static Material Pebble(Color color)
    {
        return Lit(color, 0.06f, 0.02f);
    }

    public static Material WorkPlaid()
    {
        var mat = Lit(Color.white, 0.11f, 0f);
        ApplyAlbedo(mat, MakePlaidTex(64), 2.1f);
        return mat;
    }

    public static void ApplyAlbedo(Material mat, Texture2D tex, float tile)
    {
        var scale = new Vector2(tile, tile);
        if (mat.HasProperty("_BaseMap"))
        {
            mat.SetTexture("_BaseMap", tex);
            mat.SetTextureScale("_BaseMap", scale);
        }
        if (mat.HasProperty("_MainTex"))
        {
            mat.SetTexture("_MainTex", tex);
            mat.SetTextureScale("_MainTex", scale);
        }
    }

    static Texture2D MakePlaidTex(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Point,
            name = "WorkPlaid"
        };
        var navy = new Color(0.16f, 0.22f, 0.38f, 1f);
        var cream = new Color(0.82f, 0.78f, 0.64f, 1f);
        var rust = new Color(0.48f, 0.20f, 0.16f, 1f);
        var pix = new Color[size * size];
        int band = Mathf.Max(4, size / 8);
        int accent = Mathf.Max(1, size / 32);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool vWide = ((x / band) % 2) == 0;
                bool hWide = ((y / band) % 2) == 0;
                bool vThin = (x % band) < accent || (x % band) > band - accent - 1;
                bool hThin = (y % band) < accent || (y % band) > band - accent - 1;
                Color c = navy;
                if (vWide && hWide) c = cream;
                else if (vWide || hWide) c = Color.Lerp(navy, cream, 0.35f);
                if (vThin || hThin) c = Color.Lerp(c, rust, 0.55f);
                pix[y * size + x] = c;
            }
        }
        tex.SetPixels(pix);
        tex.Apply(false, true);
        return tex;
    }

}
