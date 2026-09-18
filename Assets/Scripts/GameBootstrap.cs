using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class GameBootstrap : MonoBehaviour
{
    FarmWalkerController _player;
    GameHud _hud;
    bool _won;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoStart()
    {
        if (FindFirstObjectByType<GameBootstrap>() != null) return;
        var go = new GameObject("Game");
        go.AddComponent<GameBootstrap>();
    }

    void Start()
    {
        MobileAudioSession.Apply();

        if (MobileControls.ShouldShow)
        {
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;
        }

        var maze = MazeGenerator.Build();
        MazeWorldBuilder.Build(maze);

        var facing = FacingIntoMaze(maze);
        _player = FarmWalkerController.Spawn(maze.StartWorld, facing, maze);

        var gold = PotOfGold.Spawn(maze.GoldWorld);
        gold.OnCollected = HandleWin;

        _hud = GameHud.Create();
        MazeMoodAudio.Install(_player.transform, maze);
        StormWeather.Install(_player.transform);
        PathMudWetness.Install();
        NightSky.Install(_player.transform, gold.transform, maze);
        CrumbBeast.Spawn(maze, _player);
        PlayTone(220f, 0.15f);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R)
            || (MobileControls.Instance != null && MobileControls.Instance.RestartPressed))
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

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
