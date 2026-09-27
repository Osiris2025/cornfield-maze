using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class GameBootstrap : MonoBehaviour
{
    FarmWalkerController _player;
    GameHud _hud;
    MazeData _maze;
    LevelDef _levelDef;
    bool _won;
    bool _beastSpawned;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoStart()
    {
        if (FindFirstObjectByType<GameBootstrap>() != null) return;
        var go = new GameObject("Game");
        go.AddComponent<GameBootstrap>();
    }

    /// <summary>Is this command-line switch present? For the capture-only flags scripts/shoot.sh passes.</summary>
    static bool HasArg(string flag)
    {
        var args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
            if (args[i] == flag) return true;
        return false;
    }

    void Start()
    {
        MobileAudioSession.Apply();

        // ---- a CAPTURE run is silent ------------------------------------------------------------------
        // scripts/shoot.sh launches the player hidden so no window lands on Todd's screen, but a HIDDEN app
        // still PLAYS: he heard the soundtrack carrying on for a minute after the window vanished. The
        // script passes -silent; honour it by muting the listener for the whole run. A launch without the
        // flag — his own double-click — is completely untouched.
        if (HasArg("-silent"))
        {
            MobileAudioSession.Silent = true;   // M40: stops Reactivate() putting the volume back to 1
            AudioListener.volume = 0f;
            Debug.Log("Corn Field Maze: -silent capture run — audio muted for the whole run.");
        }

        if (MobileControls.ShouldShow)
        {
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;
        }

        // M43: ensure LevelManager exists before anything reads it
        if (LevelManager.Instance == null)
        {
            var lmGo = new GameObject("LevelManager");
            lmGo.AddComponent<LevelManager>();
        }
        _levelDef = LevelManager.Instance.CurrentDef;
        Debug.Log($"[GameBootstrap] Level {_levelDef.levelNumber}: {_levelDef.levelName} " +
            $"({_levelDef.mazeWidth}x{_levelDef.mazeHeight}, {_levelDef.huskCount} husk(s))");

        var maze = MazeGenerator.Build(_levelDef);
        MazeWorldBuilder.Build(maze);

        var facing = FacingIntoMaze(maze);
        _player = FarmWalkerController.Spawn(maze.StartWorld, facing, maze);

        var gold = PotOfGold.Spawn(maze.GoldWorld);
        gold.OnCollected = HandleWin;

        _hud = GameHud.Create();
        MazeMoodAudio.Install(_player.transform, maze);
        StormWeather.Install(_player.transform, _levelDef.stormStartIntensity);
        PathMudWetness.Install();
        NightSky.Install(_player.transform, gold.transform, maze);
        // M25 (§25.5): the dusk -> night ramp. Installed AFTER StormWeather so the storm grabs the Sun
        // light first (DuskSky adds its own moon light and would otherwise be found as "the" light).
        DuskSky.Install(_player.transform, maze);

        // M23 (§25.3): the cobs lying in the lanes, and the player's hands. The hands ride on the
        // player and read the same touch surface the controller does; the cob control is theirs to
        // show. Seed is derived from the maze so the same maze always lays the same cobs.
        var cobRoot = new GameObject("Cobs");
        CornCob.PlantLanes(maze, cobRoot.transform, maze.Width * 31 + maze.Height * 7 + maze.StartCell.x);
        CobHands.Install(_player, MobileControls.Instance, cobRoot.transform);

        // M21 (§25.1): the front end owns the first moments, and everything above is already running —
        // the sky, the storm and the field's rustle — so the title screen is neither silent nor static.
        // Only the body is held, the HUD is held, and the Husk waits for the player to start.
        _maze = maze;
        _player.Frozen = true;
        _hud.Hold();
        GameFrontEnd.Create(_player, BeginRun);
    }

    /// <summary>Called by the front end when the player leaves the introduction (M21, §25.1).</summary>
    void BeginRun()
    {
        if (_player != null) _player.Frozen = false;
        if (_hud != null) _hud.Begin();
        if (_beastSpawned || _maze == null || _player == null) return;
        _beastSpawned = true;
        for (int i = 0; i < _levelDef.huskCount; i++)
            Husk.Spawn(_maze, _player);
        PlayTone(220f, 0.15f);
    }

    void Update()
    {
        // M43: Space after win → advance level; R → restart via LevelManager
        if (GameFrontEnd.IsPlaying)
        {
            bool rKey = Input.GetKeyDown(KeyCode.R) ||
                (MobileControls.Instance != null && MobileControls.Instance.RestartPressed);
            bool spaceKey = Input.GetKeyDown(KeyCode.Space);

            if (rKey || (spaceKey && _won))
            {
                GameFrontEnd.RequestAutoPlay();
                if (_won)
                    LevelManager.Instance.Advance();
                else
                    LevelManager.Instance.RestartLevel();
            }
        }

        if (MobileControls.ShouldShow) return;

        if (Input.GetKeyDown(KeyCode.F11)
            || ((Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand)
                 || Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                && Input.GetKeyDown(KeyCode.F)))
        {
            Screen.fullScreen = !Screen.fullScreen;
        }
    }

    void HandleWin()
    {
        if (_won) return;
        if (_player != null && _player.IsCaught) return;
        _won = true;
        if (_player != null) _player.SetWon(true);
        if (_hud != null) _hud.ShowWin();
        MazeMoodAudio.PlayWin();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    static float FacingIntoMaze(MazeData maze)
    {
        var start = maze.StartCell;
        var steps = new[]
        {
            (new Vector2Int(0, 1), 0f),
            (new Vector2Int(1, 0), 90f),
            (new Vector2Int(0, -1), 180f),
            (new Vector2Int(-1, 0), 270f)
        };
        foreach (var (step, yaw) in steps)
        {
            var next = start + step;
            if (maze.InBounds(next.x, next.y) && !maze.IsWall[next.x, next.y])
                return yaw;
        }
        return 0f;
    }

    static void PlayTone(float freq, float seconds)
    {
        var go = new GameObject("Tone");
        var source = go.AddComponent<AudioSource>();
        int samples = Mathf.CeilToInt(44100 * seconds);
        var clip = AudioClip.Create("tone", samples, 1, 44100, false);
        var data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = i / 44100f;
            data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * Mathf.Exp(-3f * t) * 0.28f;
        }
        clip.SetData(data, 0);
        clip.LoadAudioData();
        source.playOnAwake = false;
        source.mute = false;
        source.spatialBlend = 0f;
        source.PlayOneShot(clip);
        Destroy(go, seconds + 0.2f);
    }
}
