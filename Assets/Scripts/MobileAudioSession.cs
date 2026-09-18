using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// iOS Ring/Silent switch silences Ambient-category apps. Games need Playback.
/// Configures the native AVAudioSession and keeps the listener unmuted across
/// background/foreground transitions.
/// </summary>
public sealed class MobileAudioSession : MonoBehaviour
{
    static MobileAudioSession _instance;
    bool _touched;

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    static extern void CornMaze_ConfigurePlaybackAudioSession();

    [DllImport("__Internal")]
    static extern void CornMaze_ReactivateAudioSession();
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (_instance != null) return;
        var go = new GameObject("MobileAudioSession");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<MobileAudioSession>();
        Apply();
    }

    public static void Apply()
    {
        AudioListener.pause = false;
        AudioListener.volume = 1f;
#if UNITY_IOS && !UNITY_EDITOR
        CornMaze_ConfigurePlaybackAudioSession();
#endif
    }

    public static void Reactivate()
    {
        AudioListener.pause = false;
        AudioListener.volume = 1f;
#if UNITY_IOS && !UNITY_EDITOR
        CornMaze_ReactivateAudioSession();
#endif
    }

    void OnApplicationPause(bool pause)
    {
        if (pause) return;
        Reactivate();
        ResumeGameAudio();
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus) return;
        Reactivate();
        ResumeGameAudio();
    }

    void Update()
    {
        if (_touched) return;
        if (!Input.anyKey && Input.touchCount == 0) return;
        _touched = true;
        Reactivate();
        ResumeGameAudio();
    }

    static void ResumeGameAudio()
    {
        var mood = FindFirstObjectByType<MazeMoodAudio>();
        if (mood != null)
            mood.EnsurePlaying();
        var storm = FindFirstObjectByType<StormWeather>();
        if (storm != null)
            storm.EnsurePlaying();
    }
}
