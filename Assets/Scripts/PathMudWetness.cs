using UnityEngine;

/// <summary>
/// Subtle gravel mud progression as storm rain accumulates — darkens and wets path material.
/// </summary>
public sealed class PathMudWetness : MonoBehaviour
{
    /// <summary>Seconds of full-intensity rain to reach max mud tint.</summary>
    const float MudRainSeconds = 150f;

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

        float smooth = Mathf.Lerp(_baseSmooth, 0.42f, mud);
        if (_gravel.HasProperty("_Smoothness")) _gravel.SetFloat("_Smoothness", smooth);
        if (_gravel.HasProperty("_Glossiness")) _gravel.SetFloat("_Glossiness", smooth);
    }

    void OnDisable()
    {
        _exposure = 0f;
        _mud = 0f;
    }
}
