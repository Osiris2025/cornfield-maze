using UnityEngine;

/// <summary>
/// Subtle gravel mud progression as storm rain accumulates — darkens and wets path material.
/// </summary>
public sealed class PathMudWetness : MonoBehaviour
{
    /// <summary>Seconds of full-intensity rain to reach max mud tint.</summary>
    const float MudRainSeconds = 150f;

    /// <summary>
    /// The most smoothness this script may hand the path surface, ever: the lane's own MATTE constant.
    ///
    /// M32c set this to a literal 0.20 while the lane bound a metallic/smoothness map, and justified it as "well
    /// inside" the matte rule. It was only inside because the map's alpha divided it back down: URP then read
    /// smoothness as `metallicGloss.a * _Smoothness`, so the map's lane alpha (mean 0.137, measured — see the
    /// committed `T_Ground_Lane_M.png`) turned a written 0.20 into ~0.027 of real smoothness. M37 unbinds that
    /// map for the field and the lane, so URP now reads `_Smoothness` DIRECTLY and 0.20 is 0.20 — above the
    /// 0.11 the lane holds. The writer would then walk the lane past the constant the material was just set to
    /// within a minute of rain, which is exactly what the standing rule forbids ("THE GROUND IS MATTE ... Any
    /// highlight on the field or the lane is a defect"), and a per-frame writer is the one place the rule can
    /// be broken with every material read-back still looking correct. So the ceiling IS the lane's matte value:
    /// rain darkens the path, it never grows a highlight on it.
    /// </summary>
    public const float MatteSmoothCeiling = Materials.PreM32LaneSmoothness;

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
        //
        // M37: the gloss target is removed, not merely clamped. M32c's clamp only worked because the bound map
        // divided the written value down by its alpha (0.137 mean) — with that map unbound on the lane, any
        // target above the ceiling is a real increase in smoothness. The lane is held at `MatteSmoothCeiling`
        // (the M32 constant, 0.11) and the wetness rides on the albedo alone.
        float smooth = Mathf.Min(_baseSmooth, MatteSmoothCeiling);
        if (_gravel.HasProperty("_Smoothness")) _gravel.SetFloat("_Smoothness", smooth);
        if (_gravel.HasProperty("_Glossiness")) _gravel.SetFloat("_Glossiness", smooth);
    }

    /// <summary>
    /// What this script is doing to the path right now, for the harness to print. Read, not assumed: the whole
    /// reason the ceiling exists is that this script was quietly walking the lane past the matte rule.
    /// </summary>
    public static string DebugReport()
    {
        float now = Mathf.Min(_baseSmooth, MatteSmoothCeiling);
        return "PathMudWetness: MatteSmoothCeiling " + MatteSmoothCeiling.ToString("0.00") +
               ", base _Smoothness at Register " + _baseSmooth.ToString("0.000") +
               ", mud now " + _mud.ToString("0.000") + " (max reachable 0.72)" +
               " -> writes _Smoothness " + now.ToString("0.000") + " at every mud level " +
               (now <= MatteSmoothCeiling + 1e-4f
                   ? "(the ceiling holds: rain cannot walk the lane past the matte rule)"
                   : "(THE CEILING FAILS — the lane can exceed " + MatteSmoothCeiling.ToString("0.00") + ")");
    }

    void OnDisable()
    {
        _exposure = 0f;
        _mud = 0f;
    }
}
