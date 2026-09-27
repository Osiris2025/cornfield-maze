using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// M43: tracks current level, persists highest unlocked level via PlayerPrefs,
/// survives scene reloads via DontDestroyOnLoad. Reads LevelDef.All for definitions.
/// </summary>
public class LevelManager : MonoBehaviour
{
    const string HighestKey = "corn.highestLevel";
    const string CurrentKey = "corn.currentLevel";

    public static LevelManager Instance { get; private set; }

    LevelDef[] _levels;
    int _current;

    public LevelDef CurrentDef => _levels[_current];
    public int CurrentLevel => _current + 1;
    public bool IsLastLevel => _current >= _levels.Length - 1;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Debug.Log("[LevelManager] Awake — initialising");

        _levels = LevelDef.All;
        Debug.Log($"[LevelManager] Loaded {_levels.Length} level definitions.");

        _current = PlayerPrefs.GetInt(CurrentKey, 0);
        _current = Mathf.Clamp(_current, 0, _levels.Length - 1);
        Debug.Log($"[LevelManager] Starting level {CurrentLevel} ({CurrentDef.levelName})");
    }

    public void Advance()
    {
        int next = _current + 1;
        int highest = PlayerPrefs.GetInt(HighestKey, 1);
        if (next + 1 > highest)
        {
            PlayerPrefs.SetInt(HighestKey, next + 1);
            Debug.Log($"[LevelManager] New highest level unlocked: {next + 1}");
        }
        PlayerPrefs.SetInt(CurrentKey, next);
        PlayerPrefs.Save();
        Debug.Log($"[LevelManager] Advancing to level {next + 1}");
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void RestartLevel()
    {
        PlayerPrefs.SetInt(CurrentKey, _current);
        PlayerPrefs.Save();
        Debug.Log($"[LevelManager] Restarting level {CurrentLevel}");
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}