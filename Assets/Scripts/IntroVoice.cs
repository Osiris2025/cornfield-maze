using UnityEngine;

/// <summary>
/// Procedural squeaky voice for the gingerbread cookie's intro dialogue. No external assets — the
/// "voice" is a pitch-shifted pulse wave with vibrato, subtitled in sync with playback.
/// </summary>
public static class IntroVoice
{
    const int SampleRate = 22050;
    const float BasePitch = 380f;      // squeaky base frequency
    const float VibratoRate = 7f;
    const float VibratoDepth = 14f;

    /// <summary>Generate a clip for one line of dialogue. Returns the clip and a timing array of
    /// (startTime, endTime) per word for subtitle sync.</summary>
    public static (AudioClip clip, (float start, float end)[] wordTimings) BuildLine(
        string text, float speed = 1f)
    {
        var words = text.Split(' ');
        float totalDuration = text.Length * 0.058f / speed;  // rough: ~58ms per char
        int totalSamples = Mathf.CeilToInt(SampleRate * totalDuration);

        var clip = AudioClip.Create("intro_line", totalSamples, 1, SampleRate, false);
        var data = new float[totalSamples];

        float wordLength = totalDuration / Mathf.Max(1, words.Length);
        var wordTimings = new (float start, float end)[words.Length];

        for (int i = 0; i < totalSamples; i++)
        {
            float t = i / (float)SampleRate;
            float freq = BasePitch + Mathf.Sin(2f * Mathf.PI * VibratoRate * t) * VibratoDepth;
            float phase = (i * freq / SampleRate) % 1f;
            float sample = phase < 0.5f ? 0.8f : -0.8f;
            float syllableT = (t % 0.16f) / 0.16f;
            float envelope = syllableT < 0.1f ? syllableT / 0.1f :
                             syllableT > 0.85f ? (1f - syllableT) / 0.15f : 1f;
            float fadeIn = Mathf.Clamp01(t / 0.04f);
            float fadeOut = Mathf.Clamp01((totalDuration - t) / 0.06f);
            data[i] = sample * envelope * fadeIn * fadeOut * 0.32f;
        }

        clip.SetData(data, 0);
        clip.LoadAudioData();

        for (int w = 0; w < words.Length; w++)
            wordTimings[w] = (w * wordLength, (w + 1) * wordLength);

        return (clip, wordTimings);
    }
}