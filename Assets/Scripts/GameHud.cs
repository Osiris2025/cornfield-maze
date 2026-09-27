using UnityEngine;
using UnityEngine.UI;

public sealed class GameHud : MonoBehaviour
{
    Text _hint;
    Text _win;

    /// <summary>
    /// M38 (Todd, 2026-09-26): "i have no idea how to attack.   I think jumping should be a thing too".
    /// The 24 s <see cref="_hint"/> cannot answer that — it is gone before the player has found a cob —
    /// so the attack and the jump also get a permanent, quiet line that stays up for the whole run.
    /// </summary>
    Text _prompt;
    float _hintTimer = 24f;
    bool _ended;
    bool _held;   // M21: the front end holds the hint until the player leaves the introduction (§25.1)

    // ---- M27 (§25.8) §5's dough meter -------------------------------------------------------
    GameObject _doughRoot;
    Image _doughFill;
    Text _doughLabel;
    FarmWalkerController _player;

    /// <summary>The walker, found once and cached — and re-found if the run restarts, which destroys and
    /// respawns him.</summary>
    FarmWalkerController Player
    {
        get
        {
            if (_player == null) _player = Object.FindFirstObjectByType<FarmWalkerController>();
            return _player;
        }
    }

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
                "PICK UP / THROW takes or throws a cob.\n" +
                "VIEW switches between first person and third person.\n" +
                "When the sky darkens, look straight up — a faint star arrow overhead points along the next correct turn.\n" +
                "Watch out for the Husk. It hunts the lanes and it cannot corner at speed — keep moving and turn hard.";
        }
        else
        {
            _hint.text =
                "You are a gingerbread cookie. Find the pot of gold in the corn maze.\n" +
                "Stay on the gravel paths. WASD / arrows follow the lanes.  Mouse  look    V  view    Shift  run    Esc  cursor\n" +
                "Left-click or F  take or throw a cob    Space  jump    R  restart\n" +
#if UNITY_EDITOR
                "Editor: Game tab → Maximize On Play, then Play.  Built Mac app: F11 / Cmd+F (or the green button) for fullscreen.\n" +
#else
                "Fullscreen: F11 or Cmd+F, or the green traffic-light button. Esc only frees the mouse — it does not leave fullscreen.\n" +
#endif
                "When the sky darkens, look straight up — a faint star arrow overhead points along the next correct turn.\n" +
                "Watch out for the Husk — it hunts the lanes and cannot corner at speed. R restarts.";
        }

        // M38: the persistent control prompt. Bottom edge, centred (§ see MakeText for why the anchor
        // is the bottom edge and not an offset from the top, which would drift with aspect ratio).
        // Quiet on purpose: 22 pt (20 on the phone) and 0.55 alpha, under the hint's 30/26, so it reads
        // as a prompt and not as a banner. Todd hates clutter, so there is no animation and no fade.
        _prompt = MakeText("ControlPrompt", new Vector2(0f, 60f), new Vector2(1200f, 34f), mobile ? 20 : 22,
            TextAnchor.LowerCenter, font);
        _prompt.color = new Color(0.93f, 0.90f, 0.84f, 0.55f);
        _prompt.raycastTarget = false;   // it must never eat a left-click meant for the attack
        // The phone prompt names only what a phone has: there is no touch jump button, so the jump is
        // left off this line rather than advertised and missing.
        _prompt.text = mobile
            ? "PICK UP / THROW — take or throw a cob"
            : "Left-click or F — take or throw a cob        Space — jump";

        BuildDoughMeter(font);

        _win = MakeText("Win", Vector2.zero, new Vector2(1400, 280), 56, TextAnchor.MiddleCenter, font);
        _win.color = new Color(1f, 0.86f, 0.25f);
        _win.text = "";
        _win.gameObject.SetActive(false);

        // Hold() is called by GameBootstrap the moment the HUD is created — before this Start runs — so
        // the hint it could not reach yet is retired here instead (§25.1: it must not show behind the
        // title screen, and its 24 s must not be spent there).
        if (_held && _hint != null) _hint.gameObject.SetActive(false);
        if (_held && _doughRoot != null) _doughRoot.SetActive(false);
        // M38: the prompt is a run control line, so it waits behind the title with the rest of the run.
        if (_held && _prompt != null) _prompt.gameObject.SetActive(false);
    }

    /// <summary>
    /// §5's dough meter — the only health UI, at §19's "top-left, 220 pt wide". It is a convenience in
    /// third person and a necessity in first: §5's health model is the cookie's BODY, and once his eyes
    /// are the camera the player cannot see it (M27). It reads DoughIntegrity and nothing else, so there
    /// is still exactly one health model.
    /// </summary>
    void BuildDoughMeter(Font font)
    {
        _doughRoot = new GameObject("DoughMeter", typeof(RectTransform));
        _doughRoot.transform.SetParent(transform, false);
        var rootRect = _doughRoot.GetComponent<RectTransform>();
        rootRect.anchorMin = rootRect.anchorMax = new Vector2(0f, 1f);
        rootRect.pivot = new Vector2(0f, 1f);
        rootRect.anchoredPosition = new Vector2(26f, -26f);
        rootRect.sizeDelta = new Vector2(220f, 22f);

        MakeImage("Back", _doughRoot.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                  Vector2.zero, new Vector2(220f, 22f), new Color(0f, 0f, 0f, 0.55f));
        _doughFill = MakeImage("Fill", _doughRoot.transform, new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                               new Vector2(3f, -11f), new Vector2(214f, 16f), DoughColour(1f));
        _doughLabel = MakeText("DoughLabel", new Vector2(0, 0), new Vector2(520, 30), 22, TextAnchor.UpperLeft, font);
        var labelRect = _doughLabel.rectTransform;
        labelRect.SetParent(_doughRoot.transform, false);
        labelRect.anchorMin = labelRect.anchorMax = new Vector2(0f, 1f);
        labelRect.pivot = new Vector2(0f, 1f);
        labelRect.anchoredPosition = new Vector2(0f, -26f);
        labelRect.sizeDelta = new Vector2(520f, 30f);
        _doughLabel.text = "DOUGH 100%  whole";
    }

    static Image MakeImage(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 offset,
                           Vector2 size, Color colour)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = offset;
        rect.sizeDelta = size;
        var image = go.AddComponent<Image>();
        image.color = colour;
        image.raycastTarget = false;
        return image;
    }

    /// <summary>§5's three states as colours: warm dough while whole, floury pale while softening, dark
    /// and baked once it crumbles. The state word is in the label too, so the read is not colour-only.</summary>
    static Color DoughColour(float integrity01)
    {
        string state = FarmWalkerController.DoughStateFor(integrity01);
        if (state == "whole") return new Color(0.90f, 0.66f, 0.34f, 0.95f);
        if (state == "softening") return new Color(0.95f, 0.88f, 0.72f, 0.95f);
        return new Color(0.62f, 0.24f, 0.14f, 0.95f);
    }

    /// <summary>The metre in the report is the width of this bar in pixels at the reference resolution —
    /// 214 pt, i.e. the whole meter on one line of the HUD, readable at a glance mid-run.</summary>
    public const float DoughBarPoints = 214f;

    public float DoughFillPoints => _doughFill != null ? _doughFill.rectTransform.sizeDelta.x : 0f;
    public string DoughLabelForTest => _doughLabel != null ? _doughLabel.text : "(none)";
    public bool DoughMeterVisible => _doughRoot != null && _doughRoot.activeSelf;

    /// <summary>
    /// M38 (test only): the persistent control prompt, the same idea as DoughLabelForTest. This prompt is
    /// the whole answer to Todd's "i have no idea how to attack", so a capture run has to be able to prove
    /// it is on screen and says the right thing instead of trusting the build.
    /// </summary>
    public string ControlPromptForTest => _prompt != null ? _prompt.text : "(none)";
    public bool ControlPromptVisible => _prompt != null && _prompt.gameObject.activeInHierarchy;

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
        // M38: the control prompt is pinned to the BOTTOM edge. Anchoring it there (rather than
        // offsetting it down from the top anchor) is what keeps it on the bottom edge at any height or
        // aspect ratio; a y offset from the top would slide off a tall canvas and into the win text.
        else if (name == "ControlPrompt")
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0f);
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
        if (_doughRoot != null) _doughRoot.SetActive(false);
        if (_prompt != null) _prompt.gameObject.SetActive(false);
    }

    /// <summary>The run has started — show the hint and the dough meter, and start the hint's clock.</summary>
    public void Begin()
    {
        _held = false;
        _hintTimer = 24f;
        if (_doughRoot != null) _doughRoot.SetActive(true);
        // M38: the prompt does not fade — it is the answer to "how do I attack", asked at any point.
        if (_prompt != null) _prompt.gameObject.SetActive(true);
        if (_hint == null) return;
        var c = _hint.color;
        c.a = 1f;
        _hint.color = c;
        _hint.gameObject.SetActive(true);
    }

    void Update()
    {
        UpdateDoughMeter();
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

    void UpdateDoughMeter()
    {
        if (_doughFill == null) return;
        var player = Player;
        float integrity = player != null ? Mathf.Clamp01(player.DoughIntegrity / 100f) : 1f;
        var rect = _doughFill.rectTransform;
        var size = rect.sizeDelta;
        float want = DoughBarPoints * integrity;
        if (Mathf.Abs(size.x - want) > 0.5f)
        {
            size.x = want;
            rect.sizeDelta = size;
        }
        _doughFill.color = DoughColour(integrity);
        if (_doughLabel != null)
        {
            string state = FarmWalkerController.DoughStateFor(integrity);
            string text = "DOUGH " + Mathf.RoundToInt(integrity * 100f) + "%  " + state;
            if (_doughLabel.text != text) _doughLabel.text = text;
        }
    }

    public void ShowWin()
    {
        if (_ended) return;
        _ended = true;
        if (_hint != null) _hint.gameObject.SetActive(false);
        if (_prompt != null) _prompt.gameObject.SetActive(false);   // the run is over; drop the prompt
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
        if (_prompt != null) _prompt.gameObject.SetActive(false);   // the run is over; drop the prompt
        _win.gameObject.SetActive(true);
        _win.color = new Color(1f, 0.55f, 0.35f);
        _win.text = MobileControls.ShouldShow
            ? "Caught by the Husk!\nTap Restart to try again"
            : "Caught by the Husk!\nPress R to try again";
        if (MobileControls.Instance != null)
            MobileControls.Instance.ShowRestart(true);
    }
}
