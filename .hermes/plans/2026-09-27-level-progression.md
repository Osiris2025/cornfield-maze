# Level Progression System — Implementation Plan

> **For Hermes:** Implement task-by-task. Each task is 2-5 minutes. Build and verify after every task.

**Goal:** Replace the single-maze game with a multi-level progression — each level is a procedurally generated maze with increasing difficulty (larger mazes, more Husks, stronger storm). Winning advances to the next level with a transition screen.

**Architecture:** A `LevelDef` ScriptableObject defines parameters per level. `LevelManager` (singleton) tracks current level, persists progress via `PlayerPrefs`, and tells `GameBootstrap` which `LevelDef` to build. `GameHud` shows a level-transition UI instead of "Restart."

**Tech Stack:** Unity C#, `PlayerPrefs` for persistence, existing `MazeGenerator`/`MazeWorldBuilder`/`GameBootstrap`.

---

## Current State

- `GameBootstrap.Start()` calls `MazeGenerator.Build()` with hardcoded Width/Height/CellSize
- `FarmWalkerController._won` = true triggers `GameHud.ShowWin()` → "Press R to restart"
- Scene reload resets everything — no state survives
- Husk: only one Huskspawned (via `Husk.Spawn` in `BeginRun`)

## What Changes

| Current | After |
|---|---|
| Hardcoded maze params | Per-level `LevelDef` |
| "Press R to restart" | Level transition → "Next Level" |
| One Husk | Configurable Husk count per level |
| No persistence | `PlayerPrefs` stores highest unlocked level |
| Scene reload on death | Restart same level |

---

### Task 1: Create LevelDef ScriptableObject

**Objective:** Define what each level looks like — maze size, cell size, Husk count, storm intensity.

**Files:**
- Create: `Assets/Scripts/LevelDef.cs`
- Create: `Assets/Resources/Levels/Level_01.asset` through `Level_05.asset`

**LevelDef.cs:**

```csharp
using UnityEngine;

[CreateAssetMenu(menuName = "CornMaze/Level Definition")]
public class LevelDef : ScriptableObject
{
    public int levelNumber;
    public string levelName;
    public int mazeWidth;
    public int mazeHeight;
    public float cellSize;
    public int huskCount;
    public float stormStartIntensity;  // 0-1, initial storm strength
    public int seedBase;               // added to level number for deterministic seeds
}
```

**5 level assets** (created via Unity Editor or programmatically in an `Editor/` script):

| Level | Width | Height | CellSize | Husks | Storm |
|-------|-------|--------|----------|-------|-------|
| 1     | 31    | 31     | 4f       | 1     | 0.25  |
| 2     | 39    | 39     | 4f       | 1     | 0.35  |
| 3     | 47    | 47     | 3.5f     | 2     | 0.50  |
| 4     | 55    | 55     | 3.5f     | 2     | 0.65  |
| 5     | 63    | 63     | 3f       | 3     | 0.80  |

**Step 1:** Write `LevelDef.cs`
**Step 2:** Add an editor script or JSON fallback that creates the 5 `.asset` files
**Step 3:** Commit

---

### Task 2: Add LevelDef parameter to MazeGenerator

**Objective:** `MazeGenerator.Build()` currently uses hardcoded `Width=31, Height=31, CellSize=4`. Accept a `LevelDef` parameter.

**Files:**
- Modify: `Assets/Scripts/MazeGenerator.cs`

**Changes:**
- Change `public static MazeData Build()` → `public static MazeData Build(LevelDef def = null)`
- If `def == null`, use the old hardcoded defaults (backward compat)
- Otherwise use `def.mazeWidth`, `def.mazeHeight`, `def.cellSize`
- Pass `def.seedBase + def.levelNumber` as the RNG seed

**Step 1:** Update `MazeGenerator.Build()` signature and body
**Step 2:** Verify Mac build compiles with `bash scripts/verify.sh`
**Step 3:** Commit

---

### Task 3: Create LevelManager singleton

**Objective:** Track current level, load `LevelDef` assets, persist highest unlocked level.

**Files:**
- Create: `Assets/Scripts/LevelManager.cs`

```csharp
using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelManager : MonoBehaviour
{
    const string HighestKey = "corn.highestLevel";
    const string CurrentKey = "corn.currentLevel";

    public static LevelManager Instance { get; private set; }

    LevelDef[] _levels;
    int _current;

    public LevelDef CurrentDef => _levels[_current];
    public int CurrentLevel => _current + 1;
    public int HighestUnlocked => PlayerPrefs.GetInt(HighestKey, 1);
    public bool IsLastLevel => _current >= _levels.Length - 1;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _levels = Resources.LoadAll<LevelDef>("Levels");
        System.Array.Sort(_levels, (a, b) => a.levelNumber.CompareTo(b.levelNumber));

        _current = PlayerPrefs.GetInt(CurrentKey, 0);
        _current = Mathf.Clamp(_current, 0, _levels.Length - 1);
    }

    public void Advance()
    {
        int next = _current + 1;
        int highest = PlayerPrefs.GetInt(HighestKey, 1);
        if (next + 1 > highest)
            PlayerPrefs.SetInt(HighestKey, next + 1);
        PlayerPrefs.SetInt(CurrentKey, next);
        PlayerPrefs.Save();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void RestartLevel()
    {
        PlayerPrefs.SetInt(CurrentKey, _current);
        PlayerPrefs.Save();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
```

**Step 1:** Write `LevelManager.cs`
**Step 2:** Verify Mac build compiles
**Step 3:** Commit

---

### Task 4: Wire GameBootstrap to LevelManager

**Objective:** `GameBootstrap.Start()` reads the current `LevelDef` from `LevelManager` and passes it to `MazeGenerator.Build()`.

**Files:**
- Modify: `Assets/Scripts/GameBootstrap.cs`

**Changes:**
- In `Start()`, after creating `LevelManager` if missing:
  - `var def = LevelManager.Instance.CurrentDef;`
  - `var maze = MazeGenerator.Build(def);`
  - Use `def.huskCount` when spawning Husks in `BeginRun()`
  - Set `StormWeather.Install()` with `def.stormStartIntensity`
- In `BeginRun()`, spawn `def.huskCount` Husks instead of one

```csharp
// In Start():
if (LevelManager.Instance == null)
{
    var lmGo = new GameObject("LevelManager");
    lmGo.AddComponent<LevelManager>();
}
var def = LevelManager.Instance.CurrentDef;
var maze = MazeGenerator.Build(def);
// ... rest as before, store _levelDef = def

// In BeginRun():
for (int i = 0; i < _levelDef.huskCount; i++)
    Husk.Spawn(maze, _player, maze.StartWorld, i * 3f);
```

**Step 1:** Modify `GameBootstrap.cs`
**Step 2:** Verify Mac build compiles
**Step 3:** Commit

---

### Task 5: Update GameHud for level transitions

**Objective:** Replace "Press R to restart" with "Level Complete! → Next Level" or "You Escaped! Play Again?" on final level.

**Files:**
- Modify: `Assets/Scripts/GameHud.cs` — `ShowWin()` method

**Changes:**

```csharp
public void ShowWin()
{
    if (_ended) return;
    _ended = true;
    if (_hint != null) _hint.gameObject.SetActive(false);
    if (_prompt != null) _prompt.gameObject.SetActive(false);
    _win.gameObject.SetActive(true);
    _win.color = new Color(1f, 0.86f, 0.25f);

    var lm = LevelManager.Instance;
    if (lm != null && !lm.IsLastLevel)
    {
        _win.text = $"Level {lm.CurrentLevel} Complete!\n" +
            (MobileControls.ShouldShow
                ? "Tap to continue to Level " + (lm.CurrentLevel + 1)
                : "Press Space to continue to Level " + (lm.CurrentLevel + 1));
    }
    else
    {
        _win.text = "You escaped the corn field!\n" +
            (MobileControls.ShouldShow
                ? "Tap to play again"
                : "Press Space to play again");
    }

    if (MobileControls.Instance != null)
        MobileControls.Instance.ShowRestart(true);
}
```

**Step 1:** Modify `GameHud.cs`  
**Step 2:** Handle the input — when player presses Space / taps after win, call `LevelManager.Instance.Advance()`  
**Step 3:** Verify Mac build compiles  
**Step 4:** Commit

---

### Task 6: Wire win → advance

**Objective:** When the player collects gold, fire the level transition instead of showing "restart."

**Files:**
- Modify: `Assets/Scripts/GameBootstrap.cs` — `HandleWin()` method
- Modify: `Assets/Scripts/GameFrontEnd.cs` — add Space handler for post-win transition

**Changes in `GameBootstrap.HandleWin()`:**
```csharp
void HandleWin()
{
    _player.SetWon(true);
    _hud.ShowWin();
    // Don't auto-advance — wait for player input
}
```

**Changes in `GameFrontEnd.Update()`:**
After `Show()` returns, add a post-win input handler:
```csharp
if (_stage == Stage.Playing && _player.IsWon)
{
    if (Input.GetKeyDown(KeyCode.Space) || (MobileControls.Instance != null && MobileControls.Instance.RestartPressed))
    {
        LevelManager.Instance.Advance();
    }
}
```

**Step 1:** Modify both files  
**Step 2:** Verify Mac build compiles  
**Step 3:** Commit

---

### Task 7: Storm intensity from LevelDef

**Objective:** `StormWeather.Install()` takes a starting intensity from the `LevelDef`.

**Files:**
- Modify: `Assets/Scripts/StormWeather.cs` — `Install()` method

**Changes:**
- `public static StormWeather Install(Transform player, float startIntensity = 0.25f)`
- In `Start()`, set `Intensity = startIntensity`

**Step 1:** Modify `StormWeather.cs`  
**Step 2:** Update `GameBootstrap.Start()` to pass `def.stormStartIntensity`  
**Step 3:** Verify Mac build compiles  
**Step 4:** Commit

---

### Task 8: Husk count from LevelDef

**Objective:** Spawn `def.huskCount` Husks in `BeginRun()`.

Already handled in Task 4. Just verify.

**Step 1:** Confirm `BeginRun()` spawns `_levelDef.huskCount` Husks  
**Step 2:** Verify Mac build compiles  
**Step 3:** Commit

---

### Task 9: Build and test end-to-end

**Objective:** Build Mac standalone, test level 1 → gold → level 2 transition.

**Verification:**
1. `bash scripts/verify.sh` — builds clean
2. Launch the app
3. Complete intro → reach gold → see "Level 1 Complete! Press Space to continue to Level 2"
4. Press Space → new maze with different size loads
5. Verify storm is stronger on level 3+

**Step 1:** Build and capture  
**Step 2:** Check `Player.log` for any errors  
**Step 3:** Commit final state

---

## Files Summary

| File | Action |
|---|---|
| `Assets/Scripts/LevelDef.cs` | CREATE |
| `Assets/Scripts/LevelManager.cs` | CREATE |
| `Assets/Scripts/MazeGenerator.cs` | MODIFY — add LevelDef param |
| `Assets/Scripts/GameBootstrap.cs` | MODIFY — wire LevelManager, Husk count |
| `Assets/Scripts/GameHud.cs` | MODIFY — ShowWin with level text |
| `Assets/Scripts/GameFrontEnd.cs` | MODIFY — post-win Space handler |
| `Assets/Scripts/StormWeather.cs` | MODIFY — startIntensity param |
| `Assets/Resources/Levels/` | CREATE — 5 .asset files |

## Risks

- **LevelDef as ScriptableObject** — `.asset` files require Unity Editor to create; may need an `Editor/` script to generate them programmatically for headless builds
- **DontDestroyOnLoad** with `LevelManager` — must clean up old managers on scene reload or duplicates accumulate
- **Scene reload** loses `DontDestroyOnLoad` objects if the scene is the only one — verify `LevelManager` survives reload
- **PlayerPrefs** works on iOS too — good for progress persistence without a save-file system