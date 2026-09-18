using UnityEngine;

public sealed class CornSway : MonoBehaviour
{
    float _phase;
    float _speed;
    float _amount;
    Quaternion _rest;

    public void Init(float phase, float speed, float amount)
    {
        _phase = phase;
        _speed = speed;
        _amount = amount;
        _rest = transform.localRotation;
    }

    void LateUpdate()
    {
        float mood = 1f + 0.4f * Mathf.Max(0f, MazeMoodAudio.Gust01 - 0.4f);
        float storm = 1f + StormWeather.Intensity * 1.9f + StormWeather.GustPush * 0.85f;
        float wave = Mathf.Sin(Time.time * (_speed * (1f + StormWeather.Intensity * 0.75f)) + _phase);
        float amp = _amount * mood * storm;
        float yaw = StormWeather.GustPush * 3.8f * Mathf.Sin(Time.time * 0.7f + _phase);
        transform.localRotation = _rest * Quaternion.Euler(wave * amp, yaw, wave * amp * 0.45f);
    }
}
