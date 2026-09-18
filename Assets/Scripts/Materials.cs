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

    public static Material Gravel(int seed)
    {
        var mat = Lit(new Color(0.52f, 0.40f, 0.24f), 0.045f, 0f);
        ApplyAlbedo(mat, MakeGravelTex(128, seed), 3.4f);
        return mat;
    }

    public static Material FieldGrass(int seed)
    {
        var mat = Lit(new Color(0.30f, 0.38f, 0.14f), 0.08f, 0f);
        ApplyAlbedo(mat, MakeGrassTex(128, seed, true), 5.5f);
        return mat;
    }

    public static Material PathGrass(int seed)
    {
        var mat = Lit(new Color(0.34f, 0.44f, 0.15f), 0.10f, 0f);
        ApplyAlbedo(mat, MakeGrassTex(96, seed, false), 2.2f);
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

    static Texture2D MakeGravelTex(int size, int seed)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear,
            anisoLevel = 4,
            name = "GravelNoise"
        };
        var rng = new System.Random(seed);
        var pix = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float n = Fbm(x, y, size, 5.5f, seed);
                float n2 = Fbm(x + 17, y + 9, size, 13f, seed + 3);
                float dirt = 0.42f + n * 0.22f;
                var col = new Color(
                    0.38f + dirt * 0.34f + n2 * 0.06f,
                    0.28f + dirt * 0.22f,
                    0.14f + dirt * 0.10f,
                    1f);

                double roll = rng.NextDouble();
                if (roll < 0.07)
                {
                    float g = 0.38f + (float)rng.NextDouble() * 0.22f;
                    col = new Color(g, g * 0.92f, g * 0.82f, 1f);
                }
                else if (roll < 0.13)
                {
                    col = new Color(
                        0.28f + (float)rng.NextDouble() * 0.10f,
                        0.20f + (float)rng.NextDouble() * 0.08f,
                        0.11f + (float)rng.NextDouble() * 0.05f,
                        1f);
                }
                pix[y * size + x] = col;
            }
        }
        tex.SetPixels(pix);
        tex.Apply(false, true);
        return tex;
    }

    static Texture2D MakeGrassTex(int size, int seed, bool field)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear,
            anisoLevel = 4,
            name = field ? "FieldGrass" : "PathGrass"
        };
        var rng = new System.Random(seed);
        var pix = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float n = Fbm(x, y, size, field ? 4.2f : 7f, seed);
                float blade = Fbm(x + 31, y + 19, size, 18f, seed + 11);
                var col = new Color(
                    0.18f + n * 0.16f + blade * 0.05f,
                    0.30f + n * 0.22f + blade * 0.10f,
                    0.08f + n * 0.08f,
                    1f);
                if (field && rng.NextDouble() < 0.045)
                {
                    col = new Color(
                        0.36f + (float)rng.NextDouble() * 0.10f,
                        0.26f + (float)rng.NextDouble() * 0.08f,
                        0.12f + (float)rng.NextDouble() * 0.05f,
                        1f);
                }
                pix[y * size + x] = col;
            }
        }
        tex.SetPixels(pix);
        tex.Apply(false, true);
        return tex;
    }

    static float Fbm(int x, int y, int size, float freq, int seed)
    {
        float xf = (x / (float)size) * freq;
        float yf = (y / (float)size) * freq;
        float v = 0f;
        float a = 0.5f;
        float f = 1f;
        for (int o = 0; o < 4; o++)
        {
            v += ValueNoise(xf * f, yf * f, seed + o * 17) * a;
            a *= 0.5f;
            f *= 2.05f;
        }
        return Mathf.Clamp01(v);
    }

    static float ValueNoise(float x, float y, int seed)
    {
        int x0 = Mathf.FloorToInt(x);
        int y0 = Mathf.FloorToInt(y);
        float tx = x - (float)x0;
        float ty = y - (float)y0;
        tx = tx * tx * (3f - 2f * tx);
        ty = ty * ty * (3f - 2f * ty);
        float a = Hash01(x0, y0, seed);
        float b = Hash01(x0 + 1, y0, seed);
        float c = Hash01(x0, y0 + 1, seed);
        float d = Hash01(x0 + 1, y0 + 1, seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
    }

    static float Hash01(int x, int y, int seed)
    {
        int h = x * 374761393 + y * 668265263 + seed * 1442695040;
        h = (h ^ (h >> 13)) * 1274126177;
        h ^= h >> 16;
        return ((h & 0x7fffffff) / 2147483647f);
    }
}
