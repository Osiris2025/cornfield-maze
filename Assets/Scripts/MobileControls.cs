using UnityEngine;
using UnityEngine.UI;

public sealed class MobileControls : MonoBehaviour
{
    public static MobileControls Instance { get; private set; }

    public Vector2 Move { get; private set; }
    public Vector2 LookDelta { get; private set; }
    public bool Running { get; private set; }
    public bool RestartPressed { get; private set; }

    // ---- M22 (§25.2) the feel contract ------------------------------------------------------
    /// <summary>Stick dead zone: below this the stick reads as centred. Narrow lanes make a
    /// hair-trigger stick feel broken, which is the defect this lands.</summary>
    public const float DeadZone = 0.12f;

    /// <summary>Full deflection: the stick ramps 0 -> 1 between DeadZone and this, so the whole
    /// analog range stays usable instead of jumping to full speed at 0.12.</summary>
    public const float FullZone = 0.30f;

    /// <summary>Screen x fraction where the look region begins. §25.2: a drag anywhere on the right
    /// half is look — there is no fixed look-pad, and this is the boundary that proves it.</summary>
    public const float LookHalfSplit = 0.46f;

    /// <summary>§25.2 look sensitivity in degrees per point. Kept at the shipped 0.14, now exposed
    /// so a settings screen can own it (M8) and so the self-test can read it.</summary>
    public float LookSensitivity = 0.14f;

    /// <summary>§25.2: the invert-Y toggle exists as a setting; the default matches the shipped feel.</summary>
    public bool InvertY;

    /// <summary>
    /// The stick response, as one pure function so the behaviour can be both used and measured.
    /// Radial dead zone, then a linear ramp to full deflection at <see cref="FullZone"/>.
    /// </summary>
    public static Vector2 StickCurve(Vector2 raw, float radius)
    {
        if (radius <= 0.0001f) return Vector2.zero;
        float magnitude = raw.magnitude / radius;
        if (magnitude <= DeadZone || raw.sqrMagnitude < 0.000001f) return Vector2.zero;
        float ramp = Mathf.Clamp01((magnitude - DeadZone) / (FullZone - DeadZone));
        return raw.normalized * ramp;
    }

    /// <summary>
    /// M21 (§25.1): true while the front end is showing a screen that is not the run itself (title,
    /// help, introduction, pause). Hides the stick and the buttons and zeroes their input, so a drag
    /// meant for a menu button can never also drive the player.
    /// </summary>
    public static bool Suppressed;

    public static bool ShouldShow
    {
        get
        {
#if UNITY_IOS || UNITY_ANDROID
            if (!Application.isEditor) return true;
#endif
            return Application.isMobilePlatform;
        }
    }

    Canvas _canvas;
    RectTransform _root;
    RectTransform _stickBase;
    RectTransform _stickKnob;
    RectTransform _runBtn;
    RectTransform _restartBtn;
    Image _runImage;
    int _moveFinger = -1;
    int _lookFinger = -1;
    int _runFinger = -1;
    Vector2 _moveOrigin;
    Vector2 _lookLast;
    float _stickRadiusPx;
    bool _built;

    public static MobileControls Create(Transform hudCanvas)
    {
        var existing = hudCanvas.GetComponent<MobileControls>();
        if (existing != null) return existing;
        var controls = hudCanvas.gameObject.AddComponent<MobileControls>();
        controls._canvas = hudCanvas.GetComponent<Canvas>();
        return controls;
    }

    void Awake()
    {
        Instance = this;
        if (_canvas == null) _canvas = GetComponent<Canvas>();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        if (!ShouldShow) return;
        BuildUi();
    }

    public void ShowRestart(bool show)
    {
        if (_restartBtn != null)
            _restartBtn.gameObject.SetActive(show);
    }

    void BuildUi()
    {
        if (_built) return;
        _built = true;

        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        var circle = CircleSprite(128);
        var disc = SoftDiscSprite(128);

        _root = NewRect("TouchControls", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        _stickBase = NewImage("StickBase", _root, disc, new Color(0f, 0f, 0f, 0.38f), 200f);
        _stickKnob = NewImage("StickKnob", _stickBase, circle, new Color(1f, 1f, 1f, 0.78f), 88f);

        _runBtn = NewImage("Run", _root, circle, new Color(0.12f, 0.12f, 0.12f, 0.55f), 132f);
        _runImage = _runBtn.GetComponent<Image>();
        Label(_runBtn, "RUN", font, 28);

        _restartBtn = NewImage("Restart", _root, SoftDiscSprite(64), new Color(0.18f, 0.14f, 0.05f, 0.82f), new Vector2(280f, 88f));
        _restartBtn.anchorMin = _restartBtn.anchorMax = _restartBtn.pivot = new Vector2(0.5f, 0.28f);
        _restartBtn.anchoredPosition = Vector2.zero;
        Label(_restartBtn, "RESTART", font, 34);
        _restartBtn.gameObject.SetActive(false);

        LayoutPads();
    }

    void LateUpdate()
    {
        RestartPressed = false;
        LookDelta = Vector2.zero;

        if (!ShouldShow || Suppressed)
        {
            Move = Vector2.zero;
            Running = false;
            if (_root != null) _root.gameObject.SetActive(false);
            return;
        }

        if (!_built) BuildUi();
        if (_root != null) _root.gameObject.SetActive(true);
        LayoutPads();
        ReadTouches();
        if (_runImage != null)
            _runImage.color = Running ? new Color(0.92f, 0.74f, 0.18f, 0.78f) : new Color(0.12f, 0.12f, 0.12f, 0.55f);
    }

    void LayoutPads()
    {
        if (_stickBase == null || _canvas == null) return;
        float sf = Mathf.Max(0.01f, _canvas.scaleFactor);
        var safe = Screen.safeArea;
        _stickRadiusPx = Mathf.Min(Screen.width, Screen.height) * 0.16f;

        float left = (safe.xMin + 36f) / sf;
        float bottom = (safe.yMin + 36f) / sf;
        float right = (Screen.width - safe.xMax + 36f) / sf;
        _stickBase.anchorMin = _stickBase.anchorMax = _stickBase.pivot = Vector2.zero;
        _stickBase.anchoredPosition = new Vector2(left + 110f, bottom + 110f);
        _stickBase.sizeDelta = Vector2.one * 200f;

        _runBtn.anchorMin = _runBtn.anchorMax = _runBtn.pivot = new Vector2(1f, 0f);
        _runBtn.anchoredPosition = new Vector2(-(right + 78f), bottom + 86f);
        _runBtn.sizeDelta = Vector2.one * 132f;
    }

    void ReadTouches()
    {
        bool runHeld = false;

        for (int i = 0; i < Input.touchCount; i++)
        {
            var touch = Input.GetTouch(i);
            var pos = touch.position;

            if (touch.phase == TouchPhase.Began)
            {
                if (_restartBtn != null && _restartBtn.gameObject.activeSelf && Inside(_restartBtn, pos))
                {
                    RestartPressed = true;
                    continue;
                }

                if (Inside(_runBtn, pos))
                {
                    _runFinger = touch.fingerId;
                    runHeld = true;
                    continue;
                }

                if (pos.x < Screen.width * LookHalfSplit && _moveFinger < 0)
                {
                    _moveFinger = touch.fingerId;
                    _moveOrigin = pos;
                    Move = Vector2.zero;
                    SetKnob(Vector2.zero);
                    continue;
                }

                if (pos.x >= Screen.width * LookHalfSplit && _lookFinger < 0)
                {
                    _lookFinger = touch.fingerId;
                    _lookLast = pos;
                }
            }
            else if (touch.fingerId == _moveFinger)
            {
                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                {
                    _moveFinger = -1;
                    Move = Vector2.zero;
                    SetKnob(Vector2.zero);
                }
                else
                {
                    var raw = pos - _moveOrigin;
                    float radius = Mathf.Max(48f, _stickRadiusPx);
                    // M22 (§25.2): the raw displacement used to go straight to Move. Now it passes
                    // through the dead zone + ramp, so a hair-trigger touch does not move the player.
                    Move = StickCurve(raw, radius);
                    SetKnob(Move);
                }
            }
            else if (touch.fingerId == _lookFinger)
            {
                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                {
                    _lookFinger = -1;
                }
                else
                {
                    LookDelta = pos - _lookLast;
                    _lookLast = pos;
                }
            }
            else if (touch.fingerId == _runFinger)
            {
                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                    _runFinger = -1;
                else
                    runHeld = true;
            }
        }

        if (_moveFinger < 0)
        {
            Move = Vector2.zero;
            SetKnob(Vector2.zero);
        }

        Running = runHeld || _runFinger >= 0;
    }

    void SetKnob(Vector2 stick)
    {
        if (_stickKnob == null) return;
        _stickKnob.anchoredPosition = stick * 56f;
    }

    bool Inside(RectTransform rect, Vector2 screenPos)
    {
        if (rect == null) return false;
        return RectTransformUtility.RectangleContainsScreenPoint(rect, screenPos, null);
    }

    static RectTransform NewRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = Vector2.zero;
        return rect;
    }

    static RectTransform NewImage(string name, Transform parent, Sprite sprite, Color color, float size)
    {
        return NewImage(name, parent, sprite, color, new Vector2(size, size));
    }

    static RectTransform NewImage(string name, Transform parent, Sprite sprite, Color color, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    static void Label(RectTransform parent, string caption, Font font, int size)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        var text = go.GetComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.text = caption;
        text.raycastTarget = false;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.75f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
    }

    static Sprite CircleSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        float r = size * 0.5f - 1f;
        var center = new Vector2(size * 0.5f, size * 0.5f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                float a = Mathf.Clamp01(r - d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    static Sprite SoftDiscSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        float r = size * 0.5f - 1f;
        var center = new Vector2(size * 0.5f, size * 0.5f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                float ring = Mathf.Abs(d - r * 0.92f);
                float fill = d < r ? 0.55f : 0f;
                float edge = Mathf.Clamp01(1.8f - ring);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Max(fill, edge) * Mathf.Clamp01(r - d + 1f)));
            }
        }
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
