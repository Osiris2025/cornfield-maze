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
        var mat = Ground("Ground/T_Ground_Field", "Ground/T_Ground_Field_N",
                         new Color(0.88f, 0.83f, 0.72f), PreM32FieldSmoothness, "Ground/T_Ground_Field_M");
        MakeMatte(mat);
        return mat;
    }

    /// <summary>
    /// M32c (§17, and the standing rule): a MATTE surface, structurally. Any highlight on the lane or the field
    /// is a defect, so the ground does not get a specular lobe or an environment reflection AT ALL rather than a
    /// low smoothness value that a later edit could quietly raise — pass 3 measured the near lane at 92.5 of 255
    /// against 33.6 for the lane beyond it and 22.3 for the field, and a highlight that survives an albedo cut of
    /// ten percent and a gloss cut to zero is not something to tune, it is something to remove.
    /// The water is the only surface in this game that reflects; it is built by GroundPuddle(), which does not
    /// pass through here.
    /// </summary>
    public static void MakeMatte(Material mat)
    {
        if (mat == null) return;
        if (mat.HasProperty("_SpecularHighlights")) mat.SetFloat("_SpecularHighlights", 0f);
        mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        if (mat.HasProperty("_EnvironmentReflections")) mat.SetFloat("_EnvironmentReflections", 0f);
        mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
    }

    /// <summary>
    /// The harness's control: put the lobe and the environment reflection back on a matte surface, at mirror
    /// smoothness, so the fix can be measured instead of asserted — and so the reflection probe's capture is
    /// tested on a surface that is certain to use it. If a mirror-smooth lane at a grazing angle stays dark, the
    /// probe's cubemap holds no sky and the report says so rather than blaming the water.
    /// </summary>
    public static void UnmakeMatteForTest(Material mat)
    {
        if (mat == null) return;
        if (mat.HasProperty("_SpecularHighlights")) mat.SetFloat("_SpecularHighlights", 1f);
        mat.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
        if (mat.HasProperty("_EnvironmentReflections")) mat.SetFloat("_EnvironmentReflections", 1f);
        mat.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
    }

    /// <summary>
    /// M32: the smoothness the field and lane carried before the maps were wired — a constant per material.
    /// The reflection report's A/B restores exactly this, so it is defined once, here, and used both by the
    /// material and by the harness that measures the difference. Not a guess at "before": this is the number
    /// the previous commit passed in.
    /// </summary>
    public const float PreM32FieldSmoothness = 0.05f;
    public const float PreM32LaneSmoothness = 0.11f;

    /// <summary>
    /// M31 (§17): the lane, blended into the field by alpha rather than cut out of it.
    ///
    /// The fade is baked into the albedo's alpha (T_Ground_LaneA, baked by scripts/m31_lane_alpha_bake.py
    /// from the strip Todd shipped) and the material is set to Alpha Blend, so URP/Lit's own base-map alpha
    /// drives opacity. That is what keeps the lit shader on the lane: it still gets the moon, the normal map
    /// and the PathMudWetness wetness path. No in-house shader, no second texture sampled per pixel.
    /// </summary>
    public static Material GroundLane()
    {
        // M32c: the lane read as a pale lit ribbon rather than a path through a night field — measured 1.16x the
        // field's mean brightness in the shallow acceptance frame, against the order's 15 % ceiling (the other
        // frame measured 0.89x, lane already darker). A uniform tint scale is the honest knob: it changes the
        // lane's albedo and nothing else — footprint, alpha blend, normal map, smoothness and the HUD are all
        // untouched — and the result is measured again in the frame rather than assumed. Wet dirt is darker than
        // dry in any case, and it is raining in this level.
        const float LaneTintScale = 0.90f;
        var mat = Ground("Ground/T_Ground_LaneA", "Ground/T_Ground_Lane_N",
                         new Color(0.94f * LaneTintScale, 0.92f * LaneTintScale, 0.88f * LaneTintScale),
                         PreM32LaneSmoothness, "Ground/T_Ground_Lane_M");
        MakeAlphaBlend(mat);
        MakeMatte(mat);
        return mat;
    }

    /// <summary>
    /// M32b (Todd: "maybe there should be a sheen in puddles, but not over all"): the one surface in the maze
    /// allowed to reflect.
    ///
    /// Alpha Blend, because this one IS transparency — the coverage lives in the derived albedo's alpha
    /// (`T_Ground_PuddleA`: RGB = the wet earth, A = coverage, max 0.86 so the lane's grain still reads
    /// faintly through the water) and URP reads the base map's alpha as opacity.
    ///
    /// The sheen is the material, not the light. `T_Ground_Puddle_M` is RGB metallic 0 with **A = smoothness**:
    /// ~0.88 inside the water, and ~0.10 across the damp halo, so the ring around the waterline stays matte
    /// and the highlight cannot bleed past the water into the lane. The normal map is the near-flat water plane
    /// with its silt lip — flat on purpose, because still water is a mirror and the highlight wants to be a
    /// clean shape rather than a speckled one.
    /// </summary>
    public static Material GroundPuddle()
    {
        var mat = Ground("Ground/T_Ground_PuddleA", "Ground/T_Ground_Puddle_N",
                         Color.white, 0.10f, "Ground/T_Ground_Puddle_M");
        // M32c CLOSE-OUT — Todd's call, 2026-09-25: "accept the puddles as dark wet patches". The frame he was
        // shown and accepted is `artifacts/review/world/m32g-water-opaque.png`, and that frame was shot with the
        // water OPAQUE and alpha-tested, so this is the shipping water — the look he judged is the look that
        // ships. REVERTING IS ONE LINE: `WaterOpaque = false` restores the alpha blend below.
        if (WaterOpaque)
        {
            MakeOpaqueWater(mat);
            return mat;
        }
        MakeAlphaBlend(mat);
        return mat;
    }

    /// <summary>
    /// M32c close-out — which water ships. `true` = the variant in the frame Todd accepted (opaque, alpha-tested so
    /// the coverage mask still shapes the pool and the damp halo is clipped away); `false` = the pre-M32g alpha
    /// blend, which keeps the halo. Both are one line apart and both are committed; the frame is the tie-breaker.
    /// </summary>
    public static bool WaterOpaque = true;

    /// <summary>
    /// Turns a URP/Lit material into a fading one. These are URP's own property names and keywords, which is
    /// why the stock shader can do this: `_SrcBlend`/`_DstBlend` are the pass's blend factors, `_Surface`
    /// and `_SURFACE_TYPE_TRANSPARENT` are what URP's Lit reads to drop ZWrite and switch to forward
    /// transparency, and the queue has to move or the lane sorts behind the field it is drawn over.
    /// </summary>
    public static void MakeAlphaBlend(Material mat)
    {
        if (mat == null) return;
        mat.SetOverrideTag("RenderType", "Transparent");
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);        // 0 opaque, 1 transparent
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);            // 0 alpha
        if (mat.HasProperty("_SrcBlend"))
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (mat.HasProperty("_DstBlend"))
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
        if (mat.HasProperty("_AlphaClip")) mat.SetFloat("_AlphaClip", 0f);
        if (mat.HasProperty("_BaseColor"))
        {
            var c = mat.GetColor("_BaseColor");
            c.a = 1f;                       // the fade lives in the map, not in the tint
            mat.SetColor("_BaseColor", c);
        }
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    /// <summary>The same material forced back to opaque — used by the M31 harness to measure what the blend
    /// actually costs, by rendering the identical lane with and without it.</summary>
    public static void MakeOpaque(Material mat)
    {
        if (mat == null) return;
        mat.SetOverrideTag("RenderType", "Opaque");
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 0f);
        if (mat.HasProperty("_SrcBlend"))
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
        if (mat.HasProperty("_DstBlend"))
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
        if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 1f);
        mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
    }

    /// <summary>
    /// M32g TEST 1 — OPAQUE WATER. The suspect is that URP's transparent pass is what loses the probe's specular
    /// cube; if so, opaque water reflects it. Alpha-test at 0.45 keeps the coverage mask doing the shape work (the
    /// water core is 0.86 and survives, the damp halo at 0.1-0.2 is cut), so the pool stays a pool rather than
    /// becoming the decal's rectangle.
    /// </summary>
    public static void MakeOpaqueWater(Material mat)
    {
        if (mat == null) return;
        MakeOpaque(mat);
        if (mat.HasProperty("_AlphaClip")) mat.SetFloat("_AlphaClip", 1f);
        if (mat.HasProperty("_AlphaCutoff")) mat.SetFloat("_AlphaCutoff", 0.45f);
        mat.EnableKeyword("_ALPHATEST_ON");
    }

    /// <summary>Harness alias — the M32g test frame calls this name, and the frame it produced is the one Todd
    /// accepted, so `MakeOpaqueWater` is now the shipping path (`WaterOpaque`).</summary>
    public static void MakeOpaqueWaterForTest(Material mat) { MakeOpaqueWater(mat); }

    /// <summary>Reads the blend state back off a material, so the report can print what is actually set
    /// rather than what the builder intended.</summary>
    public static string DescribeBlend(Material mat)
    {
        if (mat == null) return "NO MATERIAL";
        float surface = mat.HasProperty("_Surface") ? mat.GetFloat("_Surface") : -1f;
        float src = mat.HasProperty("_SrcBlend") ? mat.GetFloat("_SrcBlend") : -1f;
        float dst = mat.HasProperty("_DstBlend") ? mat.GetFloat("_DstBlend") : -1f;
        float zwrite = mat.HasProperty("_ZWrite") ? mat.GetFloat("_ZWrite") : -1f;
        return "renderQueue=" + mat.renderQueue + " renderType=" + mat.GetTag("RenderType", false) +
               " _Surface=" + surface + " (0 opaque, 1 transparent)" +
               " _SrcBlend=" + src + " _DstBlend=" + dst +
               " _ZWrite=" + zwrite +
               " keyword _SURFACE_TYPE_TRANSPARENT=" + mat.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT") +
               " baseMap=" + (mat.HasProperty("_BaseMap") && mat.GetTexture("_BaseMap") != null
                   ? mat.GetTexture("_BaseMap").name : "none");
    }

    /// <summary>
    /// A URP/Lit ground material from the derived albedo + normal maps.
    ///
    /// M32 (Todd): "can you use the PBR files for reflections off the moonlight?" — the sets ship roughness
    /// as a separate `_R` map and URP/Lit cannot take a standalone roughness texture, so the builder emits
    /// URP's own metallic/smoothness map instead: `_M` is RGB = metallic 0 (ground is a dielectric; there is
    /// no metal in it) and **A = smoothness**, straight from the set's roughness.
    ///
    /// The keyword matters and is not guesswork: URP's own material upgrader sets `_METALLICSPECGLOSSMAP`
    /// from the presence of `_MetallicGlossMap` (UniversalRenderPipelineMaterialUpgrader.cs), and URP reads
    /// smoothness as `metallicGloss.a * _Smoothness`, so the scalar goes to 1 and the map carries the value.
    /// `_SmoothnessTextureChannel` stays 0 = metallic alpha, with `_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A`
    /// explicitly OFF — the LANE's albedo alpha is its fade (M31), and reading that as smoothness would make
    /// the fade edges mirror-shiny.
    ///
    /// The normal map stays bound on purpose: it is what breaks the highlight into grain. A reflective floor
    /// without normals is a mirror blob, which is worse than no reflection.
    ///
    /// `preM32Smoothness` is the constant this material carried before the maps were wired; it is the value
    /// the A/B restores.
    /// </summary>
    static Material Ground(string albedo, string normal, Color tint, float preM32Smoothness, string glossMap)
    {
        var mat = Lit(tint, preM32Smoothness, 0f);
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

        var g = glossMap == null ? null : Resources.Load<Texture2D>(glossMap);
        if (g == null)
        {
            Debug.LogWarning("M32: ground metallic/smoothness map missing: Resources/" + glossMap +
                             " — this material falls back to a constant smoothness of " + preM32Smoothness);
        }
        BindReflection(mat, g, preM32Smoothness);

        // M32c: environment reflections ON, set explicitly rather than assumed. URP/Lit's toggle is the keyword
        // `_ENVIRONMENTREFLECTIONS_OFF`; a material that carries it samples no environment at all, which would
        // make the new sky probe useless on exactly the surface that is supposed to reflect it. Same class of
        // bug as `alphaSource=None` throwing away the smoothness: wired is not wired until it is read back.
        mat.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        if (mat.HasProperty("_EnvironmentReflections")) mat.SetFloat("_EnvironmentReflections", 1f);
        if (mat.HasProperty("_SpecularHighlights")) mat.SetFloat("_SpecularHighlights", 1f);
        return mat;
    }

    /// <summary>
    /// M32: bind (or clear) the metallic/smoothness map and set the scalar to match. Kept public because it
    /// is also the harness's A/B switch — same view, same scene, one variable — and because a material with a
    /// metallic map bound but the wrong keyword renders as if it had none, which is exactly the kind of
    /// "wired" that is not wired.
    /// </summary>
    public static void BindReflection(Material mat, Texture2D gloss, float constantSmoothness)
    {
        if (mat == null) return;
        if (mat.HasProperty("_MetallicGlossMap")) mat.SetTexture("_MetallicGlossMap", gloss);
        if (gloss != null)
        {
            if (mat.HasProperty("_SmoothnessTextureChannel")) mat.SetFloat("_SmoothnessTextureChannel", 0f);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 1f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
            mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            mat.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
        }
        else
        {
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", constantSmoothness);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
            mat.DisableKeyword("_METALLICSPECGLOSSMAP");
            mat.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
        }
    }

    /// <summary>
    /// M32f — THE FALLBACK, BUILT SO TODD HAS A CHOICE RATHER THAN AN ARGUMENT. A transparent surface in URP is
    /// hard to get a probe's cube onto (the environment arrives on the water as ambient, which is view-independent
    /// and therefore not a reflection), so this is the other way to get a sheen: a controlled, NON-DIELECTRIC
    /// glint — the standard game cheat. Additive, shaped, and independent of Fresnel, so it reads at any angle.
    /// Subtle on purpose: a hint of moon in water, not a mirror. It is a child of the puddle quad and OFF by
    /// default, so removing it is one switch (`PuddleDecals.GlintEnabled`).
    /// </summary>
    public static Material Glint()
    {
        // URP **Particles/Unlit**, not Lit and not URP/Unlit. This shader is PROVEN to resolve in this player build:
        // the rain uses it (StormWeather) and the rain renders in every frame. `CornMaze/StarUnlit` is the one that
        // does not resolve, and URP/Unlit may be stripped — a material that silently falls back to nothing is how
        // the first two attempts at this glint measured +0.02 and +0.03 of 255 and looked like a subtlety.
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null) shader = Lit(Color.white, 0.05f, 0f).shader;
        var mat = new Material(shader);
        var glintTex = MakeGlintTex(128);
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", glintTex);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", glintTex);
        if (mat.HasProperty("_EmissionMap")) mat.SetTexture("_EmissionMap", glintTex);
        // Unlit and additive: the highlight does not depend on the moon light, on Fresnel, or on the view angle,
        // which is the entire reason this variant exists. SUBTLE — a hint of moon in water, not a mirror.
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.34f, 0.39f, 0.52f, 1f));
        if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", new Color(0.34f, 0.39f, 0.52f, 1f));
        if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);   // ADD
        if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 1f);          // 1 = additive in the Particles family
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.DisableKeyword("_ALPHAMODULATE_ON");
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        // Drawn AFTER the water decal so transparent sorting cannot hide it under the water it belongs to.
        mat.renderQueue = 3100;
        return mat;
    }
    static Texture2D MakeGlintTex(int n)
    {
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        var px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f;      // across the lane
                float v = (y + 0.5f) / n * 2f - 1f;      // down the rut
                float wide = Mathf.Clamp01(1f - Mathf.Sqrt(u * u * 7.0f + v * v * 0.50f));
                float core = Mathf.Clamp01(1f - Mathf.Sqrt(u * u * 16f + v * v * 1.10f));
                float a = wide * wide * wide * 0.55f + core * core * 0.60f;
                px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
            }
        t.SetPixels(px);
        t.Apply();
        return t;
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
