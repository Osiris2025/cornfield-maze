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
        Silent = SilentRequested();   // M40: read it HERE, so the very first Apply() is already silent
        var go = new GameObject("MobileAudioSession");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<MobileAudioSession>();
        Apply();
    }

    /// <summary>M40: a capture run must stay SILENT for its whole life. scripts/shoot.sh passes -silent,
    /// and without this flag every Reactivate() — called on the first keypress, on focus and on resume —
    /// put the volume straight back to 1. A capture that started muted therefore came back ON the moment
    /// Todd pressed a key, which is exactly his "it isnt muted. I hear it".</summary>
    public static bool Silent;

    /// <summary>Is the capture-only -silent switch on this command line?</summary>
    static bool SilentRequested()
    {
        var args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
            if (args[i] == "-silent") return true;
        return false;
    }

    public static void Apply()
    {
        AudioListener.pause = false;
        AudioListener.volume = Silent ? 0f : 1f;
#if UNITY_IOS && !UNITY_EDITOR
        CornMaze_ConfigurePlaybackAudioSession();
#endif
    }

    public static void Reactivate()
    {
        AudioListener.pause = false;
        AudioListener.volume = Silent ? 0f : 1f;   // M40: never undo a capture run's mute
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
