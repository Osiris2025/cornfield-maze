using UnityEngine;

/// <summary>
/// Game Center leaderboard integration — single leaderboard (cornmaze.highest_node).
/// Architecture: ObjC plugin at Assets/Plugins/iOS/CornGameCenter.mm handles native calls;
/// this static wrapper exposes them to C# and is a no-op on non-iOS.
/// FSD §15.
/// </summary>
public static class CornGameCenter
{
    const string LeaderboardId = "cornmaze.highest_node";
    const string BestScoreKey = "corn.gc.bestScore";

    static bool _authenticated;
    static bool _authFailed;
    static bool _everTriedAuth;
    static bool _initialized;

    // ---- public API -----------------------------------------------------------------

    /// <summary>
    /// Authenticate once, shortly after the first level starts — never on the title
    /// screen and never blocking play. Subsequent calls are no-ops.
    /// </summary>
    public static void AuthenticateIfNeeded()
    {
        EnsureInitialized();
        if (_authenticated || _authFailed || _everTriedAuth) return;
        _everTriedAuth = true;
#if UNITY_IOS && !UNITY_EDITOR
        _CornGC_Authenticate();
#endif
    }

    /// <summary>
    /// Submit the highest score. Only submits when the new score beats the locally stored
    /// best; Game Center keeps the best on its side too. Silent on failure.
    /// </summary>
    public static void SubmitScore(int score)
    {
        EnsureInitialized();
        int best = PlayerPrefs.GetInt(BestScoreKey, 0);
        if (score <= best) return;
        PlayerPrefs.SetInt(BestScoreKey, score);
        PlayerPrefs.Save();
#if UNITY_IOS && !UNITY_EDITOR
        _CornGC_SubmitScore(score, LeaderboardId);
#endif
    }

    /// <summary>Show the leaderboard UI. If not authenticated, the system sign-in prompt appears.</summary>
    public static void ShowLeaderboardUI()
    {
        EnsureInitialized();
#if UNITY_IOS && !UNITY_EDITOR
        _CornGC_ShowLeaderboardUI(LeaderboardId);
#endif
    }

    // ---- ObjC callbacks (called via UnitySendMessage on the "CornGameCenter" GO) ----

    internal static void OnAuthSuccess()
    {
        _authenticated = true;
        Debug.Log("[CornGameCenter] Authenticated");
    }

    internal static void OnAuthError(string reason)
    {
        _authFailed = true;
        Debug.LogWarning($"[CornGameCenter] Auth failed: {reason}");
    }

    // ---- init -----------------------------------------------------------------------

    static void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;
        var go = new GameObject("CornGameCenter");
        go.AddComponent<CornGameCenterReceiver>();
        Object.DontDestroyOnLoad(go);
    }

    // ---- native imports (no-op stubs for non-iOS) ------------------------------------

#if UNITY_IOS && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("__Internal")]
    static extern void _CornGC_Authenticate();

    [System.Runtime.InteropServices.DllImport("__Internal")]
    static extern void _CornGC_SubmitScore(int score, string leaderboardId);

    [System.Runtime.InteropServices.DllImport("__Internal")]
    static extern void _CornGC_ShowLeaderboardUI(string leaderboardId);
#else
    static void _CornGC_Authenticate() { }
    static void _CornGC_SubmitScore(int score, string leaderboardId) { }
    static void _CornGC_ShowLeaderboardUI(string leaderboardId) { }
#endif
}

/// <summary>Receiver MonoBehaviour so ObjC UnitySendMessage callbacks land on a real GameObject.</summary>
sealed class CornGameCenterReceiver : MonoBehaviour
{
    // ReSharper disable once UnusedMember.Local — called from ObjC via UnitySendMessage
    void OnAuthSuccess(string _) => CornGameCenter.OnAuthSuccess();

    // ReSharper disable once UnusedMember.Local — called from ObjC via UnitySendMessage
    void OnAuthError(string reason) => CornGameCenter.OnAuthError(reason);
}