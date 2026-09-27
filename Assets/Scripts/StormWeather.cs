using UnityEngine;

/// <summary>
/// Calm for ~20s, then a gradual turn into a dark, hard-blowing field storm.
/// Lightning flashes first; procedural thunder follows after a distance delay.
/// </summary>
public sealed class StormWeather : MonoBehaviour
{
    public static float Intensity { get; private set; }
    public static float GustPush { get; private set; }
    public static float Flash { get; private set; }
    public static Vector3 WindDir { get; private set; } = new Vector3(0.82f, 0f, 0.57f);

    const float CalmSeconds = 20f;
    const float RampSeconds = 34f;

    Transform _follow;
    Light _sun;
    Light _lightning;
    float _storm;
    Material _skyInstance;
    ParticleSystem _rain;
    AudioSource _thunder;
    AudioSource _rumble;
    AudioSource _stormWind;
    AudioSource _rainNormal;     // M42: real rain & thunder ogg
    AudioSource _rainHard;       // M42: real hard rain wav
    AudioClip[] _thunders;

    Color _fogCalm;
    Color _fogStorm = new Color(0.045f, 0.05f, 0.07f);
    Color _skyCalm;
    Color _skyStorm = new Color(0.028f, 0.032f, 0.048f);
    Color _eqCalm;
    Color _eqStorm = new Color(0.055f, 0.06f, 0.07f);
    Color _gndCalm;
    Color _gndStorm = new Color(0.018f, 0.016f, 0.014f);
    Color _sunCalm;
    Color _sunStorm = new Color(0.42f, 0.48f, 0.58f);
    float _sunIntCalm = 1.35f;
    float _fogDensCalm = 0.028f;
    float _fogDensStorm = 0.072f;

    Color _ambSkyNow;
    Color _ambEqNow;
    Color _ambGndNow;
    Color _fogNow;
    float _flash;

    float _nextStrike;
    bool _striking;
    System.Random _rng = new System.Random(4409);

    public static StormWeather Install(Transform player)
    {
        var existing = Object.FindFirstObjectByType<StormWeather>();
        if (existing != null)
            Object.Destroy(existing.gameObject);

        var go = new GameObject("StormWeather");
        var storm = go.AddComponent<StormWeather>();
        storm._follow = player;
        return storm;
    }

    void OnDisable()
    {
        Intensity = 0f;
        GustPush = 0f;
        Flash = 0f;
    }

    public void EnsurePlaying()
    {
        MobileAudioSession.Reactivate();
        if (_rumble != null && _rumble.clip != null)
        {
            _rumble.mute = false;
            if (!_rumble.isPlaying)
                _rumble.Play();
        }
        if (_stormWind != null && _stormWind.clip != null)
        {
            _stormWind.mute = false;
            if (!_stormWind.isPlaying)
                _stormWind.Play();
        }
        if (_rainNormal != null && _rainNormal.clip != null)
        {
            _rainNormal.mute = false;
            if (!_rainNormal.isPlaying)
                _rainNormal.Play();
        }
    }

    void Start()
    {
        MobileAudioSession.Apply();
        Intensity = 0f;
        WindDir = new Vector3(0.82f, 0f, 0.57f).normalized;

        _fogCalm = RenderSettings.fogColor;
        _skyCalm = RenderSettings.ambientSkyColor;
        _eqCalm = RenderSettings.ambientEquatorColor;
        _gndCalm = RenderSettings.ambientGroundColor;
        _fogDensCalm = RenderSettings.fogDensity;
        _ambSkyNow = _skyCalm;
        _ambEqNow = _eqCalm;
        _ambGndNow = _gndCalm;
        _fogNow = _fogCalm;

        // The SUN, by the same rule DuskSky uses to find it — not "the first directional light", which
        // is what this was. DuskSky adds a MoonLight and the storm adds a Lightning, so the first hit
        // can be the MOON: the storm then drove the moon with the sun's palette and its own dimming,
        // and the field went dark at the exact moment the moon was supposed to light it.
        _sun = null;
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional && l.name == "Sun") { _sun = l; break; }
        if (_sun == null)
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional && l.name != "MoonLight" && l.name != "Lightning")
                { _sun = l; break; }
        if (_sun != null)
        {
            _sunCalm = _sun.color;
            _sunIntCalm = _sun.intensity;
        }

        var bolt = new GameObject("Lightning");
        bolt.transform.SetParent(transform, false);
        _lightning = bolt.AddComponent<Light>();
        _lightning.type = LightType.Directional;
        _lightning.intensity = 0f;
        _lightning.color = new Color(0.82f, 0.90f, 1f);
        _lightning.shadows = LightShadows.None;

        _thunder = MakeSource("Thunder", 0f, 0f, false, 40);
        _rumble = MakeSource("StormRumble", 0f, 0f, true, 52);
        _stormWind = MakeSource("StormWind", 0f, 0f, true, 56);
        _rumble.clip = StormSynth.RumbleLoop(10f);
        _stormWind.clip = StormSynth.HardWind(9f);
        _rainNormal = MakeSource("RainNormal", 0f, 0f, true, 50);
        _rainHard   = MakeSource("RainHard",   0f, 0f, true, 48);
        _rainNormal.clip = Resources.Load<AudioClip>("Audio/rain_and_thunder");
        _rainHard.clip   = Resources.Load<AudioClip>("Audio/hard_rain");
        _rainNormal.Play();
        _rumble.Play();
        _stormWind.Play();

        _thunders = new[]
        {
            StormSynth.Thunder(6.4f, 901, 0.22f),
            StormSynth.Thunder(5.8f, 1444, 0.48f),
            StormSynth.Thunder(5.2f, 2201, 0.72f),
            StormSynth.Thunder(4.8f, 3110, 0.95f)
        };

        BuildRain();
        _nextStrike = CalmSeconds + 8f + (float)_rng.NextDouble() * 6f;
    }

    void Update()
    {
        float storm = Storm01();
        Intensity = storm;

        float t = Time.time;
        float gustA = 0.5f + 0.5f * Mathf.Sin(t * 0.47f);
        float gustB = 0.5f + 0.5f * Mathf.Sin(t * 0.19f + 2.1f);
        float gustC = 0.5f + 0.5f * Mathf.Sin(t * 1.15f + 0.4f);
        float gust = Mathf.Clamp01(0.35f + 0.40f * gustA * gustB + 0.35f * gustC * storm);
        GustPush = storm * (0.45f + 0.80f * gust);

        Flash = _flash;
        _storm = storm;
        DriveRain(storm);
        DriveWindAudio(storm, gust);

        if (_follow != null && _rain != null)
            _rain.transform.position = _follow.position + Vector3.up * 12f + WindDir * (0.55f * storm);

        if (!_striking && storm > 0.22f && t >= _nextStrike)
            StartCoroutine(LightningBurst(storm));
    }

    static float Storm01()
    {
        float age = Time.timeSinceLevelLoad - CalmSeconds;
        if (age <= 0f) return 0f;
        float u = Mathf.Clamp01(age / RampSeconds);
        return u * u * (3f - 2f * u);
    }

    /// <summary>
    /// M25: the sky is applied in LateUpdate so it always runs AFTER DuskSky.Update has published this
    /// frame's dusk->night palette. Two components writing the same light from Update is a race, and the
    /// loser is whichever one Unity happens to run second.
    /// </summary>
    void LateUpdate()
    {
        ApplySky(_storm);
    }

    void ApplySky(float storm)
    {
        float punch = _flash;

        // M25 (§25.5): the calm sky is no longer a constant. DuskSky ramps dusk -> night, so the storm
        // blends away from whatever the sky is RIGHT NOW. Without this, the storm would drag the sky
        // back to the dusk amber it captured at scene load and the moonrise would never arrive.
        bool dusk = DuskSky.Instance != null;
        Color fogCalm = dusk ? DuskSky.FogColor : _fogCalm;
        Color skyCalm = dusk ? DuskSky.AmbientSky : _skyCalm;
        Color eqCalm = dusk ? DuskSky.AmbientEquator : _eqCalm;
        Color gndCalm = dusk ? DuskSky.AmbientGround : _gndCalm;

        var fog = Color.Lerp(fogCalm, _fogStorm, storm);
        fog = Color.Lerp(fog, new Color(0.78f, 0.84f, 0.95f), punch * 0.55f);
        RenderSettings.fogColor = fog;
        RenderSettings.fogDensity = Mathf.Lerp(_fogDensCalm, _fogDensStorm, storm);

        _ambSkyNow = Color.Lerp(skyCalm, _skyStorm, storm);
        _ambEqNow = Color.Lerp(eqCalm, _eqStorm, storm);
        _ambGndNow = Color.Lerp(gndCalm, _gndStorm, storm);
        RenderSettings.ambientSkyColor = Color.Lerp(_ambSkyNow, new Color(0.72f, 0.80f, 1f), punch);
        RenderSettings.ambientEquatorColor = Color.Lerp(_ambEqNow, new Color(0.55f, 0.62f, 0.78f), punch * 0.7f);
        RenderSettings.ambientGroundColor = Color.Lerp(_ambGndNow, new Color(0.28f, 0.30f, 0.36f), punch * 0.35f);

        if (_sun != null)
        {
            bool duskOwnsSun = DuskSky.Instance != null;
            Color sunCalm = duskOwnsSun ? DuskSky.SunColor : _sunCalm;
            float sunIntCalm = duskOwnsSun ? DuskSky.SunIntensity : _sunIntCalm;

            _sun.color = Color.Lerp(sunCalm, _sunStorm, storm);
            float dim = Mathf.Lerp(sunIntCalm, 0.16f, storm);
            _sun.intensity = dim + punch * 0.15f;

            // M25: when DuskSky is present it owns the sun's DIRECTION (dusk has the sun below the
            // horizon; the old fixed 38° elevation would light the field like noon). The storm keeps
            // the colour and intensity.
            if (!duskOwnsSun)
            {
                var rot = Quaternion.Slerp(
                    Quaternion.Euler(38f, 155f, 0f),
                    Quaternion.Euler(12f, 168f, 0f),
                    storm);
                _sun.transform.rotation = rot;
            }
        }

        if (RenderSettings.skybox != null)
        {
            if (_skyInstance == null)
            {
                _skyInstance = new Material(RenderSettings.skybox);
                RenderSettings.skybox = _skyInstance;
            }
            float exposure = Mathf.Lerp(1.05f, 0.12f, storm) + punch * 0.55f;
            if (_skyInstance.HasProperty("_Exposure"))
                _skyInstance.SetFloat("_Exposure", exposure);
            if (_skyInstance.HasProperty("_AtmosphereThickness"))
                _skyInstance.SetFloat("_AtmosphereThickness", Mathf.Lerp(1.0f, 0.55f, storm));
        }
    }

    void DriveRain(float storm)
    {
        if (_rain == null) return;
        var emission = _rain.emission;
        float rate = storm < 0.08f ? 0f : Mathf.Lerp(0f, 720f, Mathf.InverseLerp(0.08f, 1f, storm));
        emission.rateOverTime = rate;

        // Mostly vertical streaks — light sideways drift only; wind audio/gusts stay stormy elsewhere.
        var vel = _rain.velocityOverLifetime;
        vel.enabled = true;
        float sideways = 1.35f * storm;
        vel.x = new ParticleSystem.MinMaxCurve(WindDir.x * sideways);
        vel.z = new ParticleSystem.MinMaxCurve(WindDir.z * sideways);
        vel.y = new ParticleSystem.MinMaxCurve(-18f - 10f * storm, -24f - 12f * storm);

        var force = _rain.forceOverLifetime;
        force.enabled = true;
        force.x = new ParticleSystem.MinMaxCurve(WindDir.x * 1.8f * storm);
        force.z = new ParticleSystem.MinMaxCurve(WindDir.z * 1.8f * storm);
        force.y = new ParticleSystem.MinMaxCurve(-2.5f * storm);
    }

    void DriveWindAudio(float storm, float gust)
    {
        float winDuck = MazeMoodAudio.WinThemePlaying ? 0.42f : 1f;
        if (_rumble != null)
        {
            _rumble.volume = Mathf.Min(0.34f, storm * (0.14f + 0.20f * gust)) * winDuck;
            _rumble.pitch = 0.88f + 0.08f * gust;
        }
        if (_stormWind != null)
        {
            _stormWind.volume = Mathf.Min(0.38f, storm * (0.14f + 0.24f * gust)) * winDuck;
            _stormWind.panStereo = Mathf.Sin(Time.time * 0.11f) * 0.34f;
            _stormWind.pitch = 0.94f + 0.10f * gust;
        }
        if (_rainNormal != null)
        {
            _rainNormal.volume = Mathf.Min(0.40f, storm * 0.45f) * winDuck;
            _rainNormal.pitch = 1f;
        }
        if (_rainHard != null)
        {
            // Hard rain kicks in above 0.7 storm intensity
            float hardFeather = Mathf.Clamp01((storm - 0.7f) / 0.3f);
            _rainHard.volume = Mathf.Min(0.32f, hardFeather * 0.38f) * winDuck;
            _rainHard.pitch = 1f;
        }
    }

    System.Collections.IEnumerator LightningBurst(float storm)
    {
        _striking = true;
        float closeness = 0.18f + (float)_rng.NextDouble() * (0.35f + 0.47f * storm);
        closeness = Mathf.Clamp01(closeness);

        float yaw = 40f + (float)_rng.NextDouble() * 260f;
        float pitch = 8f + (float)_rng.NextDouble() * 38f;
        _lightning.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

        yield return DoFlash(1f, 0.045f + (float)_rng.NextDouble() * 0.05f);
        if (_rng.NextDouble() < 0.68)
        {
            yield return new WaitForSeconds(0.045f + (float)_rng.NextDouble() * 0.14f);
            yield return DoFlash(0.42f + (float)_rng.NextDouble() * 0.18f, 0.030f + (float)_rng.NextDouble() * 0.04f);
        }

        float delay = Mathf.Lerp(2.45f, 0.32f, closeness);
        yield return new WaitForSeconds(delay);
        PlayThunder(closeness);

        float gap = Mathf.Lerp(11.5f, 3.4f, storm) + (float)_rng.NextDouble() * Mathf.Lerp(6f, 2.2f, storm);
        _nextStrike = Time.time + gap;
        _striking = false;
    }

    System.Collections.IEnumerator DoFlash(float strength, float hold)
    {
        _flash = strength;
        Flash = _flash;
        if (_lightning != null)
            _lightning.intensity = 4.8f * strength;
        float t = 0f;
        while (t < hold)
        {
            t += Time.deltaTime;
            yield return null;
        }
        float fade = 0.045f;
        t = 0f;
        while (t < fade)
        {
            t += Time.deltaTime;
            float u = 1f - Mathf.Clamp01(t / fade);
            _flash = strength * u;
            Flash = _flash;
            if (_lightning != null)
                _lightning.intensity = 4.8f * strength * u;
            yield return null;
        }
        _flash = 0f;
        Flash = 0f;
        if (_lightning != null)
            _lightning.intensity = 0f;
    }

    void PlayThunder(float closeness)
    {
        if (_thunder == null || _thunders == null || _thunders.Length == 0) return;
        int idx = closeness < 0.35f ? 0 : closeness < 0.58f ? 1 : closeness < 0.80f ? 2 : 3;
        float winDuck = MazeMoodAudio.WinThemePlaying ? 0.38f : 1f;
        _thunder.pitch = 0.90f + (float)_rng.NextDouble() * 0.10f;
        _thunder.volume = Mathf.Lerp(0.34f, 0.78f, closeness) * (0.62f + 0.38f * Intensity) * winDuck;
        _thunder.PlayOneShot(_thunders[idx]);
    }

    void BuildRain()
    {
        var go = new GameObject("Rain");
        go.transform.SetParent(transform, false);
        _rain = go.AddComponent<ParticleSystem>();

        var main = _rain.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = 0.85f;
        main.startSpeed = 14f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.032f);
        main.startColor = new Color(0.62f, 0.68f, 0.74f, 0.42f);
        main.maxParticles = 2200;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 1.15f;

        var emission = _rain.emission;
        emission.rateOverTime = 0f;

        var shape = _rain.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(26f, 1.2f, 26f);

        var renderer = _rain.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 3.8f;
        renderer.velocityScale = 0.08f;
        renderer.material = RainMaterial();

        _rain.Play();
    }

    static Material RainMaterial()
    {
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null) shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        var mat = new Material(shader);
        var tex = new Texture2D(4, 16, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            name = "RainStreak"
        };
        var pix = new Color[4 * 16];
        for (int y = 0; y < 16; y++)
        {
            float v = y / 15f;
            float a = Mathf.Sin(v * Mathf.PI);
            a *= a;
            var c = new Color(0.78f, 0.84f, 0.90f, a);
            for (int x = 0; x < 4; x++)
                pix[y * 4 + x] = c;
        }
        tex.SetPixels(pix);
        tex.Apply(false, true);
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
        return mat;
    }

    AudioSource MakeSource(string name, float volume, float spatial, bool loop, int priority)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = loop;
        src.mute = false;
        src.volume = volume;
        src.spatialBlend = spatial;
        src.dopplerLevel = 0f;
        src.priority = priority;
        return src;
    }
}

static class StormSynth
{
    const int Rate = 22050;

    public static AudioClip Thunder(float seconds, int seed, float closeness)
    {
        int frames = Mathf.RoundToInt(seconds * (float)Rate);
        var data = new float[frames * 2];
        var rng = new Rng(seed);
        float brown = 0f;
        float lp = 0f;
        float subA1 = 0f, subA2 = 0f;
        float subB1 = 0f, subB2 = 0f;
        float subC1 = 0f, subC2 = 0f;
        float subD1 = 0f, subD2 = 0f;
        float mid1 = 0f, mid2 = 0f;
        float crackLp = 0f;
        float crackHp = 0f;
        float prevCrack = 0f;

        closeness = Mathf.Clamp01(closeness);
        float crackLen = Mathf.Lerp(0.07f, 0.20f, closeness);
        float sub32 = Mathf.Lerp(32f, 36f, closeness);
        float sub45 = Mathf.Lerp(42f, 48f, closeness);
        float sub58 = Mathf.Lerp(54f, 62f, closeness);
        float sub74 = Mathf.Lerp(70f, 80f, closeness);

        for (int i = 0; i < frames; i++)
        {
            float t = i / (float)Rate;
            float white = rng.NextSigned();
            brown += (white - brown) * 0.010f;

            float cut = Mathf.Lerp(55f, 160f, closeness) + 70f * Mathf.Exp(-1.6f * t);
            float a = Mathf.Exp(-2f * Mathf.PI * cut / (float)Rate);
            lp = (1f - a) * (brown * 0.88f + white * 0.06f) + a * lp;

            float attack = 1f - Mathf.Exp(-14f * t);
            float decayLong = Mathf.Exp(-0.38f * t);
            float decayMid = Mathf.Exp(-0.72f * t);
            float decayShort = Mathf.Exp(-1.35f * t);

            float layer32 = Reson(brown + lp * 0.35f, sub32, 0.992f, ref subA1, ref subA2) * decayLong * 0.62f;
            float layer45 = Reson(lp, sub45, 0.990f, ref subB1, ref subB2) * decayLong * 0.48f;
            float layer58 = Reson(lp, sub58, 0.986f, ref subC1, ref subC2) * decayMid * 0.36f;
            float layer74 = Reson(lp, sub74, 0.982f, ref subD1, ref subD2) * decayShort * 0.24f;
            float layerMid = Reson(lp, Mathf.Lerp(88f, 110f, closeness), 0.976f, ref mid1, ref mid2) * decayMid * 0.16f;

            float sineSub = Mathf.Sin(2f * Mathf.PI * sub32 * t) * decayLong * 0.28f * attack;
            sineSub += Mathf.Sin(2f * Mathf.PI * sub45 * t) * decayMid * 0.18f * attack;
            sineSub += Mathf.Sin(2f * Mathf.PI * 38f * t) * Mathf.Exp(-0.48f * t) * 0.16f;

            float crack = 0f;
            if (t < crackLen)
            {
                float cEnv = 1f - t / crackLen;
                cEnv = cEnv * cEnv;
                float ca = Mathf.Exp(-2f * Mathf.PI * 2200f / (float)Rate);
                crackLp = (1f - ca) * white + ca * crackLp;
                float ah = Mathf.Exp(-2f * Mathf.PI * 400f / (float)Rate);
                crackHp = ah * (crackHp + crackLp - prevCrack);
                prevCrack = crackLp;
                float snap = (white * 0.40f + crackHp * 0.60f) * cEnv;
                crack = snap * Mathf.Lerp(0.55f, 1.05f, closeness);
            }

            float boom2 = 0f;
            if (t > 0.28f)
            {
                float age = t - 0.28f;
                boom2 = Mathf.Sin(2f * Mathf.PI * sub45 * t) * Mathf.Exp(-1.1f * age) * 0.22f * attack;
                boom2 += brown * Mathf.Exp(-0.85f * age) * 0.10f;
            }

            float boom3 = 0f;
            if (t > 0.85f)
            {
                float age = t - 0.85f;
                boom3 = Mathf.Sin(2f * Mathf.PI * sub32 * t) * Mathf.Exp(-0.55f * age) * 0.16f;
            }

            float roll = 0f;
            if (t > 0.16f)
            {
                float age = t - 0.16f;
                float pulses = Mathf.Sin(2f * Mathf.PI * (1.6f + closeness) * age);
                roll = Mathf.Max(0f, pulses) * Mathf.Exp(-0.85f * age) * 0.18f * lp;
            }

            float body = (lp * 0.42f + layer32 + layer45 + layer58 + layer74 + layerMid + sineSub + boom2 + boom3 + roll) * attack;
            body *= 0.62f + 0.38f * closeness;
            float mono = body + crack;
            float width = brown * 0.10f * decayMid;
            data[i * 2] = Soft(mono + width);
            data[i * 2 + 1] = Soft(mono - width * 0.75f);
        }

        LimitPeak(data, 0.82f);
        return Clip("Thunder", data, frames, 2);
    }

    public static AudioClip RumbleLoop(float seconds)
    {
        int extra = Mathf.RoundToInt(0.40f * (float)Rate);
        int frames = Mathf.RoundToInt(seconds * (float)Rate);
        int total = frames + extra;
        var left = new float[total];
        var right = new float[total];
        var rng = new Rng(7711);
        float brown = 0f;
        float y1 = 0f, y2 = 0f;
        float z1 = 0f, z2 = 0f;

        for (int i = 0; i < total; i++)
        {
            float t = i / (float)Rate;
            float u = (t % seconds) / seconds;
            float swell = 0.62f + 0.38f * Mathf.Sin(2f * Mathf.PI * u);
            float white = rng.NextSigned();
            brown += (white - brown) * 0.008f;
            float low = Reson(brown, 34f + 14f * swell, 0.991f, ref y1, ref y2);
            float mid = Reson(brown, 62f, 0.984f, ref z1, ref z2);
            float sine = Mathf.Sin(2f * Mathf.PI * 36f * t) * 0.18f * swell;
            left[i] = (low * 0.78f + mid * 0.22f + sine) * swell;
            right[i] = (low * 0.72f + mid * 0.26f + sine * 0.85f) * swell;
        }

        var data = new float[frames * 2];
        for (int i = 0; i < frames; i++)
        {
            data[i * 2] = left[i];
            data[i * 2 + 1] = right[i];
        }
        for (int i = 0; i < extra; i++)
        {
            float w = i / (float)extra;
            data[i * 2] = left[i] * w + left[frames + i] * (1f - w);
            data[i * 2 + 1] = right[i] * w + right[frames + i] * (1f - w);
        }
        PeakSoft(data, 0.58f);
        return Clip("StormRumble", data, frames, 2);
    }

    public static AudioClip HardWind(float seconds)
    {
        int extra = Mathf.RoundToInt(0.35f * (float)Rate);
        int frames = Mathf.RoundToInt(seconds * (float)Rate);
        int total = frames + extra;
        var left = new float[total];
        var right = new float[total];
        var rng = new Rng(8801);
        float brown = 0f, lp = 0f, hp = 0f, prev = 0f;
        float r1 = 0f, r2 = 0f;

        for (int i = 0; i < total; i++)
        {
            float t = i / (float)Rate;
            float u = (t % seconds) / seconds;
            float gust = 0.50f + 0.50f * Mathf.Sin(2f * Mathf.PI * u) * Mathf.Sin(4f * Mathf.PI * u + 0.9f);
            float white = rng.NextSigned();
            brown += (white - brown) * 0.018f;
            float cut = 240f + 1400f * gust;
            float a = Mathf.Exp(-2f * Mathf.PI * cut / (float)Rate);
            lp = (1f - a) * (brown * 0.7f + white * 0.3f) + a * lp;
            float ah = Mathf.Exp(-2f * Mathf.PI * 110f / (float)Rate);
            hp = ah * (hp + lp - prev);
            prev = lp;
            float moan = Reson(hp, 155f + 210f * gust, 0.982f, ref r1, ref r2);
            float env = 0.45f + 0.55f * gust;
            left[i] = (hp * 0.72f + moan * 0.55f) * env;
            right[i] = (hp * 0.68f + moan * 0.60f) * env;
        }

        var data = new float[frames * 2];
        for (int i = 0; i < frames; i++)
        {
            data[i * 2] = left[i];
            data[i * 2 + 1] = right[i];
        }
        for (int i = 0; i < extra; i++)
        {
            float w = i / (float)extra;
            data[i * 2] = left[i] * w + left[frames + i] * (1f - w);
            data[i * 2 + 1] = right[i] * w + right[frames + i] * (1f - w);
        }
        PeakSoft(data, 0.64f);
        return Clip("HardWind", data, frames, 2);
    }

    static float Reson(float x, float freq, float radius, ref float y1, ref float y2)
    {
        float f = Mathf.Clamp(freq, 24f, 4000f);
        float r = Mathf.Clamp(radius, 0.1f, 0.994f);
        float w = 2f * Mathf.PI * f / (float)Rate;
        float y = (1f - r) * x + 2f * r * Mathf.Cos(w) * y1 - r * r * y2;
        y2 = y1;
        y1 = y;
        return y;
    }

    static float Soft(float x)
    {
        if (x > 1f) return 1f;
        if (x < -1f) return -1f;
        return x - x * x * x * 0.12f;
    }

    static void PeakSoft(float[] data, float target)
    {
        float peak = 0.0001f;
        for (int i = 0; i < data.Length; i++)
            peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        float gain = target / peak;
        for (int i = 0; i < data.Length; i++)
            data[i] = Soft(data[i] * gain);
    }

    static void LimitPeak(float[] data, float ceiling)
    {
        float peak = 0.0001f;
        for (int i = 0; i < data.Length; i++)
            peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        if (peak <= ceiling)
        {
            for (int i = 0; i < data.Length; i++)
                data[i] = Soft(data[i]);
            return;
        }
        float gain = ceiling / peak;
        for (int i = 0; i < data.Length; i++)
            data[i] = Soft(data[i] * gain);
    }

    static AudioClip Clip(string name, float[] data, int frames, int channels)
    {
        var clip = AudioClip.Create(name, frames, channels, Rate, false);
        if (!clip.SetData(data, 0))
            Debug.LogWarning("StormSynth: SetData failed for " + name);
        clip.LoadAudioData();
        return clip;
    }

    struct Rng
    {
        uint _s;
        public Rng(int seed) { _s = (uint)seed * 747796405u + 2891336453u; }
        public float NextSigned() => (float)NextDouble() * 2f - 1f;
        public double NextDouble()
        {
            _s = _s * 1664525u + 1013904223u;
            return (_s >> 8) * (1.0 / 16777216.0);
        }
    }
}
