using UnityEngine;

/// <summary>
/// Subtle gravel mud progression as storm rain accumulates — darkens and wets path material.
/// </summary>
public sealed class PathMudWetness : MonoBehaviour
{
    /// <summary>Seconds of full-intensity rain to reach max mud tint.</summary>
    const float MudRainSeconds = 150f;

    /// <summary>
    /// M32c: the most smoothness this script may hand the path surface, ever. The lane is matte by rule — any
    /// highlight on it is a defect — and a per-frame writer that can raise smoothness is a way for the rule to be
    /// broken without anything in the material read-back changing. 0.20 keeps the wet path well inside it whether
    /// URP reads `_Smoothness` alone or multiplies it by the map's alpha.
    /// </summary>
    public const float MatteSmoothCeiling = 0.20f;

    static Material _gravel;
    static Color _baseColor = new Color(0.52f, 0.40f, 0.24f);
    static float _baseSmooth = 0.045f;
    static float _exposure;
    static float _mud;

    public static void RegisterGravel(Material gravel)
    {
        _gravel = gravel;
        if (gravel == null) return;
        if (gravel.HasProperty("_BaseColor"))
            _baseColor = gravel.GetColor("_BaseColor");
        else if (gravel.HasProperty("_Color"))
            _baseColor = gravel.GetColor("_Color");
        if (gravel.HasProperty("_Smoothness"))
            _baseSmooth = gravel.GetFloat("_Smoothness");
        else if (gravel.HasProperty("_Glossiness"))
            _baseSmooth = gravel.GetFloat("_Glossiness");
        _exposure = 0f;
        _mud = 0f;
    }

    public static PathMudWetness Install()
    {
        var existing = Object.FindFirstObjectByType<PathMudWetness>();
        if (existing != null)
            return existing;
        var go = new GameObject("PathMudWetness");
        return go.AddComponent<PathMudWetness>();
    }

    void Update()
    {
        float storm = StormWeather.Intensity;
        if (storm > 0.02f)
            _exposure += storm * Time.deltaTime;

        float target = Mathf.Clamp01(_exposure / MudRainSeconds);
        // Keep progression subtle — never turn paths into solid brown soup.
        target *= 0.72f;
        _mud = Mathf.MoveTowards(_mud, target, Time.deltaTime * 0.25f);
        Apply(_mud);
    }

    static void Apply(float mud)
    {
        if (_gravel == null) return;
        var dry = _baseColor;
        var wet = new Color(0.28f, 0.18f, 0.10f, dry.a);
        var mudTint = new Color(0.34f, 0.22f, 0.11f, dry.a);
        var col = Color.Lerp(dry, Color.Lerp(wet, mudTint, 0.55f), mud);
        if (_gravel.HasProperty("_BaseColor")) _gravel.SetColor("_BaseColor", col);
        if (_gravel.HasProperty("_Color")) _gravel.SetColor("_Color", col);

        // M32c: the standing rule is that the ground is MATTE and any highlight on the lane is a defect — M32
        // measured the lane's smoothness at 0.137 with 0.0 % of the surface above 0.40. This script was lerping
        // the lane's `_Smoothness` toward 0.42 as the storm ran, so the path could walk past that rule on its own
        // while the material read-back still showed the map's 0.137 — two different numbers for one surface.
        // Wet dirt darkens; it does not grow a highlight.
        float smooth = Mathf.Min(Mathf.Lerp(_baseSmooth, 0.42f, mud), MatteSmoothCeiling);
        if (_gravel.HasProperty("_Smoothness")) _gravel.SetFloat("_Smoothness", smooth);
        if (_gravel.HasProperty("_Glossiness")) _gravel.SetFloat("_Glossiness", smooth);
    }

    /// <summary>
    /// What this script is doing to the path right now, for the harness to print. Read, not assumed: the whole
    /// reason the ceiling exists is that this script was quietly walking the lane past the matte rule.
    /// </summary>
    public static string DebugReport()
    {
        float now = Mathf.Min(Mathf.Lerp(_baseSmooth, 0.42f, _mud), MatteSmoothCeiling);
        float full = Mathf.Min(Mathf.Lerp(_baseSmooth, 0.42f, 1f), MatteSmoothCeiling);
        return "PathMudWetness: MatteSmoothCeiling " + MatteSmoothCeiling.ToString("0.00") +
               ", base _Smoothness at Register " + _baseSmooth.ToString("0.000") +
               ", mud now " + _mud.ToString("0.000") + " (max reachable 0.72)" +
               " -> writes _Smoothness " + now.ToString("0.000") + " now, " + full.ToString("0.000") +
               " at full mud " + (full <= MatteSmoothCeiling + 1e-4f
                   ? "(the ceiling holds: rain cannot walk the lane past the matte rule)"
                   : "(THE CEILING FAILS — the lane can exceed " + MatteSmoothCeiling.ToString("0.00") + ")");
    }

    void OnDisable()
    {
        _exposure = 0f;
        _mud = 0f;
    }
}
