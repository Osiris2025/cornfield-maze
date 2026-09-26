using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// M21 — the front end: title, help, introduction and pause (FSD §25.1).
///
/// Rules this file obeys, in the house style:
///  · No new art. Panels and type are built in code from the builtin font, exactly like GameHud.
///  · The field is alive behind the front end. The sky, the storm and the field's rustle all run from
///    the first frame — Todd's ask is that the rustle starts before the game does. Only the player's
///    body is held (FarmWalkerController.Frozen) and the Husk is not spawned until Play.
///  · The front end states are NOT paused (Time.timeScale stays 1): the world must live behind the
///    title. Only the pause screen stops time.
///  · Nothing is advertised that does not exist yet. The help screen describes the controls the build
///    actually has — walk, look, run — and gains the cane and the cob when M10 and M23 land.
/// </summary>
public sealed class GameFrontEnd : MonoBehaviour
{
    public const string GameTitle = "CORN FIELD MAZE";
    public const string GameSubtitle = "A gingerbread cookie. A pot of gold. A field that does not want you here.";

    public static GameFrontEnd Instance { get; private set; }

    /// <summary>
    /// False until the player leaves the introduction. Movement, the Husk and the hint text all gate
    /// on this (§25.1), so nothing can start the run behind the menu.
    /// </summary>
    public static bool IsPlaying { get; private set; }

    /// <summary>The introduction plays once per app run; after that Play goes straight into the maze.</summary>
    static bool _introSeen;

    /// <summary>
    /// Set by GameBootstrap immediately before a scene reload ("R" / mobile RESTART), so a restart
    /// lands back in the maze instead of dumping the player on the title (§25.1 pause menu).
    /// </summary>
    static bool _autoPlayNextLoad;

    public static void RequestAutoPlay() { _autoPlayNextLoad = true; }

    enum Stage { Title, Help, Intro, Playing, Paused }

    static readonly string[] IntroPages =
    {
        "You are a gingerbread cookie, and you are late.",
        "Somewhere in this field there is a pot of gold.\nSomething in the corn already knows your name.",
        "The Husk does not run in straight lines.\nIt cannot turn a corner at speed — and neither can you, if you stand still.",
        "Walk the lanes. Take what you can carry.\nDo not stop moving."
    };

    Font _font;
    Stage _stage = Stage.Title;
    int _introPage;

    GameObject _titlePanel;
    GameObject _helpPanel;
    GameObject _introPanel;
    GameObject _pausePanel;
    RectTransform _pauseButton;

    FarmWalkerController _player;
    System.Action _onPlay;

    readonly System.Collections.Generic.List<RectTransform> _safeRoots =
        new System.Collections.Generic.List<RectTransform>();
    Vector2Int _lastScreen;

    // ---- construction ------------------------------------------------------

    public static GameFrontEnd Create(FarmWalkerController player, System.Action onPlay)
    {
        if (Instance != null) return Instance;

        var go = new GameObject("FrontEnd");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;                 // over the HUD and the mobile controls
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        EnsureEventSystem();

        var front = go.AddComponent<GameFrontEnd>();
        front._player = player;
        front._onPlay = onPlay;
        // M22: Instance was declared and read but never assigned, so the singleton was permanently
        // null and nothing outside this method could reach the front end. Assigned here, which is the
        // only place the object is created.
        Instance = front;
        IsPlaying = false;
        return front;
    }

    static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<StandaloneInputModule>();
    }

    /// <summary>
    /// The builtin font, resolved exactly the way GameHud resolves it — with a last-resort borrow from
    /// any Text already in the scene, so the front end can never come up as panels with no words on
    /// them. No font asset is added (§25.1).
    /// </summary>
    static Font ResolveFont()
    {
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (font == null)
        {
            foreach (var existing in FindObjectsByType<Text>(FindObjectsSortMode.None))
                if (existing.font != null) { font = existing.font; break; }
        }
        return font;
    }

    void Start()
    {
        _font = ResolveFont();

        BuildTitle();
        BuildHelp();
        BuildIntro();
        BuildPause();

        if (_autoPlayNextLoad)
        {
            _autoPlayNextLoad = false;
            BeginRun();
        }
        else
        {
            Show(Stage.Title);
        }
    }

    // ---- screens -----------------------------------------------------------

    void BuildTitle()
    {
        _titlePanel = Panel("TitleScreen", 0.86f);
        var safe = Safe(_titlePanel.transform);

        Label(safe, "GameTitle", GameTitle, 104, TextAnchor.MiddleCenter, new Color(1f, 0.83f, 0.32f),
            new Vector2(0f, 300f), new Vector2(1700f, 150f));
        Label(safe, "GameSubtitle", GameSubtitle, 30, TextAnchor.MiddleCenter, new Color(0.94f, 0.90f, 0.82f),
            new Vector2(0f, 185f), new Vector2(1400f, 90f));

        Action(safe, "Play", "PLAY", new Vector2(0f, 20f), () => AdvanceFromTitle());
        Action(safe, "Help", "HELP", new Vector2(0f, -100f), () => Show(Stage.Help));

        Label(safe, "TitleFooter", "the field is already rustling", 24, TextAnchor.MiddleCenter,
            new Color(0.72f, 0.68f, 0.60f), new Vector2(0f, -240f), new Vector2(1200f, 60f));
    }

    void BuildHelp()
    {
        _helpPanel = Panel("HelpScreen", 0.92f);
        var safe = Safe(_helpPanel.transform);

        Label(safe, "HelpTitle", "HOW TO WALK THE FIELD", 56, TextAnchor.MiddleCenter,
            new Color(1f, 0.83f, 0.32f), new Vector2(0f, 380f), new Vector2(1500f, 90f));

        // Left: what the player does. Right: what the field does to them. Two panels, no wall of text.
        var move = Box(safe, "MoveBox", new Vector2(-430f, 40f), new Vector2(760f, 460f));
        Label(move.transform, "MoveHeading", "MOVE", 38, TextAnchor.UpperLeft, new Color(1f, 0.83f, 0.32f),
            new Vector2(38f, -30f), new Vector2(680f, 60f));
        Label(move.transform, "MoveBody",
            MobileControls.ShouldShow
                ? "Left stick — walk the lanes\nDrag the right half of the screen — look\nRUN (hold) — sprint\nPAUSE (top right) — stop and think"
                : "WASD or the arrow keys — walk the lanes\nLeft-drag the mouse — look        Scroll — zoom\nShift — sprint        Esc — pause        R — restart",
            28, TextAnchor.UpperLeft, new Color(0.95f, 0.92f, 0.86f), new Vector2(38f, -110f), new Vector2(680f, 320f));

        var threat = Box(safe, "ThreatBox", new Vector2(430f, 40f), new Vector2(760f, 460f));
        Label(threat.transform, "ThreatHeading", "SURVIVE", 38, TextAnchor.UpperLeft, new Color(1f, 0.83f, 0.32f),
            new Vector2(38f, -30f), new Vector2(680f, 60f));
        Label(threat.transform, "ThreatBody",
            "Find the pot of gold and stay on the gravel paths.\n\n" +
            "The Husk hunts you through the lanes. It is slower than\nyou are, but it never stops. It cannot turn a corner at\nspeed — so keep moving and turn hard.\n\n" +
            "Standing still is how you get eaten.",
            28, TextAnchor.UpperLeft, new Color(0.95f, 0.92f, 0.86f), new Vector2(38f, -110f), new Vector2(680f, 330f));

        Action(safe, "HelpBack", "BACK", new Vector2(-250f, -330f), () => Show(Stage.Title));
        Action(safe, "HelpStart", "START", new Vector2(250f, -330f), () => AdvanceFromTitle());
    }

    void BuildIntro()
    {
        _introPanel = Panel("IntroScreen", 0.94f);
        var safe = Safe(_introPanel.transform);
        _introText = Label(safe, "IntroPage", IntroPages[0], 52, TextAnchor.MiddleCenter,
            new Color(0.97f, 0.94f, 0.88f), new Vector2(0f, 60f), new Vector2(1500f, 500f));
        _introButton = Action(safe, "IntroNext", "NEXT", new Vector2(0f, -320f), AdvanceIntro);
        Label(safe, "IntroHint", MobileControls.ShouldShow ? "tap anywhere to go on" : "click, or press Space", 24,
            TextAnchor.MiddleCenter, new Color(0.70f, 0.66f, 0.58f), new Vector2(0f, -430f), new Vector2(1200f, 50f));
    }

    Text _titleText;
    Text _introText;
    Button _introButton;
    Text _introButtonLabel;

    void BuildPause()
    {
        _pausePanel = Panel("PauseScreen", 0.88f);
        var safe = Safe(_pausePanel.transform);
        Label(safe, "PauseTitle", "PAUSED", 72, TextAnchor.MiddleCenter, new Color(1f, 0.83f, 0.32f),
            new Vector2(0f, 300f), new Vector2(1200f, 100f));

        Action(safe, "Resume", "RESUME", new Vector2(0f, 90f), Resume);
        Action(safe, "PauseHelp", "HELP", new Vector2(0f, -40f), () => Show(Stage.Help));
        Action(safe, "PauseRestart", "RESTART", new Vector2(0f, -170f), Restart);
        Action(safe, "PauseTitleButton", "QUIT TO TITLE", new Vector2(0f, -300f), QuitToTitle);
    }

    /// <summary>
    /// The in-game pause control. It lives on this canvas (not MobileControls) so the front end owns
    /// every screen transition, and it is the phone's only way into the pause menu.
    /// </summary>
    void BuildPauseButton()
    {
        var go = new GameObject("PauseButton", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(transform, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
        rect.sizeDelta = new Vector2(104f, 104f);
        var image = go.GetComponent<Image>();
        image.color = new Color(0.10f, 0.10f, 0.12f, 0.55f);
        var button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(Pause);
        Label(go.transform, "PauseGlyph", "II", 40, TextAnchor.MiddleCenter, new Color(0.95f, 0.92f, 0.86f),
            Vector2.zero, new Vector2(104f, 104f), true);
        _pauseButton = rect;
        go.SetActive(false);
    }

    void Show(Stage stage)
    {
        _stage = stage;
        if (_titlePanel != null) _titlePanel.SetActive(stage == Stage.Title);
        if (_helpPanel != null) _helpPanel.SetActive(stage == Stage.Help);
        if (_introPanel != null) _introPanel.SetActive(stage == Stage.Intro);
        if (_pausePanel != null) _pausePanel.SetActive(stage == Stage.Paused);
        if (_pauseButton != null) _pauseButton.gameObject.SetActive(stage == Stage.Playing);

        bool playing = stage == Stage.Playing;
        IsPlaying = playing;
        if (_player != null) _player.Frozen = !playing;
        MobileControls.Suppressed = !playing;

        // Only the pause screen stops the world. The title, help and introduction must stay alive.
        bool paused = stage == Stage.Paused;
        Time.timeScale = paused ? 0f : 1f;
        AudioListener.pause = paused;
        if (!MobileControls.ShouldShow && !paused && playing) LockCursor(true);
        if (!MobileControls.ShouldShow && (paused || !playing)) LockCursor(false);
    }

    void AdvanceFromTitle()
    {
        if (!_introSeen) { _introPage = 0; ShowIntroPage(); Show(Stage.Intro); }
        else BeginRun();
    }

    void ShowIntroPage()
    {
        if (_introText != null) _introText.text = IntroPages[Mathf.Clamp(_introPage, 0, IntroPages.Length - 1)];
        if (_introButtonLabel != null)
            _introButtonLabel.text = _introPage >= IntroPages.Length - 1 ? "BEGIN" : "NEXT";
    }

    void AdvanceIntro()
    {
        _introPage++;
        if (_introPage >= IntroPages.Length) { _introSeen = true; BeginRun(); return; }
        ShowIntroPage();
    }

    void BeginRun()
    {
        Show(Stage.Playing);
        if (_onPlay != null) _onPlay();
    }

    /// <summary>
    /// M22 diagnostics only: start the run without a human tapping Play, so M22FeelSelfTest measures
    /// the real controller. Called only when the player is launched with "-m22selftest".
    /// </summary>
    public static void ForcePlayForTest()
    {
        if (Instance == null) return;
        Instance.BeginRun();
    }

    // ---- pause -------------------------------------------------------------

    public void Pause()
    {
        if (_stage != Stage.Playing) return;
        Show(Stage.Paused);
    }

    public void Resume()
    {
        if (_stage != Stage.Paused) return;
        Show(_introSeen ? Stage.Playing : Stage.Title);
    }

    void Restart()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        RequestAutoPlay();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void QuitToTitle()
    {
        // Reload the scene so the field starts fresh, then land on the title rather than in the maze.
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>
    /// M36 (Todd, 2026-09-26): the cursor is never captured any more. The look is a left-drag (see
    /// FarmWalkerController.Update) and Todd's ask is that the mouse not be bound to the scene, so
    /// there is no lock left to set. Kept as a named call so the two transitions in Show() still read
    /// as intent rather than as a missing line.
    /// </summary>
    static void LockCursor(bool locked)
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // ---- frame -------------------------------------------------------------

    void Update()
    {
        if (_lastScreen.x != Screen.width || _lastScreen.y != Screen.height) ApplySafeAreas();

        // Desktop conveniences, so the front end is testable on the Mac build as well as the phone.
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (_stage == Stage.Playing) Pause();
            else if (_stage == Stage.Paused) Resume();
            else if (_stage == Stage.Help) Show(_introSeen ? Stage.Playing : Stage.Title);
        }

        bool advance = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)
                       || Input.GetKeyDown(KeyCode.KeypadEnter);
        if (!advance) return;
        if (_stage == Stage.Title) AdvanceFromTitle();
        else if (_stage == Stage.Intro) AdvanceIntro();
        else if (_stage == Stage.Help) AdvanceFromTitle();
    }

    // ---- built-in UI plumbing (same register as GameHud) -------------------

    GameObject Panel(string name, float dim)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        var image = go.GetComponent<Image>();
        image.color = new Color(0.035f, 0.030f, 0.045f, dim);   // near-black, faintly warm
        image.raycastTarget = true;                              // swallows drags meant for the stick
        go.SetActive(false);
        return go;
    }

    GameObject Box(Transform parent, string name, Vector2 anchored, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchored;
        rect.sizeDelta = size;
        var image = go.GetComponent<Image>();
        image.color = new Color(0.09f, 0.07f, 0.05f, 0.80f);
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0.85f, 0.68f, 0.30f, 0.35f);
        outline.effectDistance = new Vector2(2f, -2f);
        return go;
    }

    Text Label(Transform parent, string name, string body, int size, TextAnchor align, Color color,
               Vector2 anchored, Vector2 box, bool centredInParent = false)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = centredInParent ? new Vector2(0.5f, 0.5f) : new Vector2(0.5f, 1f);
        if (align == TextAnchor.MiddleCenter) rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        if (align == TextAnchor.UpperLeft) { rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f); }
        rect.anchoredPosition = anchored;
        rect.sizeDelta = box;
        var text = go.AddComponent<Text>();
        text.text = body;
        text.font = _font;
        text.fontSize = size;
        text.alignment = align;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);
        return text;
    }

    Button Action(Transform parent, string name, string caption, Vector2 anchored, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchored;
        rect.sizeDelta = new Vector2(460f, 92f);
        var image = go.GetComponent<Image>();
        image.color = new Color(0.16f, 0.12f, 0.06f, 0.92f);
        var button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);
        var label = Label(go.transform, "Label", caption, 34, TextAnchor.MiddleCenter,
            new Color(1f, 0.88f, 0.48f), Vector2.zero, new Vector2(460f, 92f), true);
        if (name == "IntroNext") { _introButton = button; _introButtonLabel = label; }
        return button;
    }

    /// <summary>Full-screen panel + a child that respects the phone's notch and home bar (§16).</summary>
    RectTransform Safe(Transform panel)
    {
        var go = new GameObject("Safe", typeof(RectTransform));
        go.transform.SetParent(panel, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        _safeRoots.Add(rect);
        return rect;
    }

    void ApplySafeAreas()
    {
        _lastScreen = new Vector2Int(Screen.width, Screen.height);
        var safe = Screen.safeArea;
        Vector2 min = safe.position;
        Vector2 max = safe.position + safe.size;
        min.x /= Mathf.Max(1f, Screen.width);
        min.y /= Mathf.Max(1f, Screen.height);
        max.x /= Mathf.Max(1f, Screen.width);
        max.y /= Mathf.Max(1f, Screen.height);
        for (int i = 0; i < _safeRoots.Count; i++)
        {
            if (_safeRoots[i] == null) continue;
            _safeRoots[i].anchorMin = min;
            _safeRoots[i].anchorMax = max;
            _safeRoots[i].offsetMin = Vector2.zero;
            _safeRoots[i].offsetMax = Vector2.zero;
        }
        if (_pauseButton != null)
        {
            _pauseButton.anchoredPosition = new Vector2(-(Screen.width - safe.xMax) - 24f, -(Screen.height - safe.yMax) - 24f);
        }
    }
}
