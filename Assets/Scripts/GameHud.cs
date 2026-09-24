using UnityEngine;
using UnityEngine.UI;

public sealed class GameHud : MonoBehaviour
{
    Text _hint;
    Text _win;
    float _hintTimer = 24f;
    bool _ended;
    bool _held;   // M21: the front end holds the hint until the player leaves the introduction (§25.1)

    public static GameHud Create()
    {
        var go = new GameObject("HUD");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        var hud = go.AddComponent<GameHud>();
        MobileControls.Create(go.transform);
        return hud;
    }

    void Start()
    {
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        bool mobile = MobileControls.ShouldShow;
        int hintSize = mobile ? 26 : 30;
        var hintBox = mobile ? new Vector2(1500, 260) : new Vector2(1600, 320);
        _hint = MakeText("Hint", new Vector2(0, mobile ? -24 : -40), hintBox, hintSize, TextAnchor.UpperCenter, font);
        if (mobile)
        {
            _hint.text =
                "You are a gingerbread cookie. Find the pot of gold in the corn maze.\n" +
                "Stay on the gravel paths. Left stick walks the lanes. Drag the right side to look. Hold RUN to sprint.\n" +
                "When the sky darkens, look straight up — a faint star arrow overhead points along the next correct turn.\n" +
                "Watch out for the Husk. It hunts the lanes and it cannot corner at speed — keep moving and turn hard.";
        }
        else
        {
            _hint.text =
                "You are a gingerbread cookie. Find the pot of gold in the corn maze.\n" +
                "Stay on the gravel paths. WASD / arrows follow the lanes.  Mouse  look    Shift  run    Esc  cursor\n" +
#if UNITY_EDITOR
                "Editor: Game tab → Maximize On Play, then Play.  Built Mac app: F11 / Cmd+F (or the green button) for fullscreen.\n" +
#else
                "Fullscreen: F11 or Cmd+F, or the green traffic-light button. Esc only frees the mouse — it does not leave fullscreen.\n" +
#endif
                "When the sky darkens, look straight up — a faint star arrow overhead points along the next correct turn.\n" +
                "Watch out for the Husk — it hunts the lanes and cannot corner at speed. R restarts.";
        }

        _win = MakeText("Win", Vector2.zero, new Vector2(1400, 280), 56, TextAnchor.MiddleCenter, font);
        _win.color = new Color(1f, 0.86f, 0.25f);
        _win.text = "";
        _win.gameObject.SetActive(false);

        // Hold() is called by GameBootstrap the moment the HUD is created — before this Start runs — so
        // the hint it could not reach yet is retired here instead (§25.1: it must not show behind the
        // title screen, and its 24 s must not be spent there).
        if (_held && _hint != null) _hint.gameObject.SetActive(false);
    }

    Text MakeText(string name, Vector2 anchored, Vector2 size, int fontSize, TextAnchor align, Font font)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        if (name == "Win")
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        }
        rect.anchoredPosition = anchored;
        rect.sizeDelta = size;
        var text = go.AddComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.alignment = align;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);
        return text;
    }

    /// <summary>Held by the front end: the 24 s hint must not run out behind the title screen (§25.1).</summary>
    public void Hold()
    {
        _held = true;
        if (_hint != null) _hint.gameObject.SetActive(false);
    }

    /// <summary>The run has started — show the hint and start its clock.</summary>
    public void Begin()
    {
        _held = false;
        _hintTimer = 24f;
        if (_hint == null) return;
        var c = _hint.color;
        c.a = 1f;
        _hint.color = c;
        _hint.gameObject.SetActive(true);
    }

    void Update()
    {
        if (_held || _ended || _hint == null || !_hint.gameObject.activeSelf) return;
        _hintTimer -= Time.deltaTime;
        if (_hintTimer < 3f)
        {
            var c = _hint.color;
            c.a = Mathf.Clamp01(_hintTimer / 3f);
            _hint.color = c;
        }
        if (_hintTimer <= 0f)
            _hint.gameObject.SetActive(false);
    }

    public void ShowWin()
    {
        if (_ended) return;
        _ended = true;
        if (_hint != null) _hint.gameObject.SetActive(false);
        _win.gameObject.SetActive(true);
        _win.color = new Color(1f, 0.86f, 0.25f);
        _win.text = MobileControls.ShouldShow
            ? "You found the pot of gold!\nTap Restart to wander the maze again"
            : "You found the pot of gold!\nPress R to wander the maze again";
        if (MobileControls.Instance != null)
            MobileControls.Instance.ShowRestart(true);
    }

    public void ShowCaught()
    {
        if (_ended) return;
        _ended = true;
        if (_hint != null) _hint.gameObject.SetActive(false);
        _win.gameObject.SetActive(true);
        _win.color = new Color(1f, 0.55f, 0.35f);
        _win.text = MobileControls.ShouldShow
            ? "Caught by the Husk!\nTap Restart to try again"
            : "Caught by the Husk!\nPress R to try again";
        if (MobileControls.Instance != null)
            MobileControls.Instance.ShowRestart(true);
    }
}
