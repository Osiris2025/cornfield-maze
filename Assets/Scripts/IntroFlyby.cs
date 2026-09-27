using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// M42 — cinematic intro flyby that replaces the text-only introduction.
///
/// Sequence: camera descends from above → stops on Gingy's face → dialogue plays
/// (procedural squeaky voice + synced subtitles + mouth animation) → camera sweeps
/// back → hands control to first-person. Press Space or click to skip.
/// </summary>
public sealed class IntroFlyby : MonoBehaviour
{
    public System.Action OnComplete;

    const string Dialogue =
        "I'm lost! Can you help me find my way out of this maze? I'm scared — " +
        "there is something called The Husk that is chasing me! " +
        "I don't know what he will do if he catches me!";

    Camera _flycam;
    Canvas _subCanvas;
    Text _subtitleText;
    FarmWalkerController _player;
    MouthAnimator _mouth;
    bool _skipped;
    bool _capture;

    const string CaptureFlag = "-introflybycapture";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void MaybeCapture()
    {
        foreach (var a in System.Environment.GetCommandLineArgs())
            if (a == CaptureFlag) { _captureWanted = true; return; }
    }

    static bool _captureWanted;

    public static bool CaptureWanted => _captureWanted;

    public static IntroFlyby Play(FarmWalkerController player, System.Action onComplete)
    {
        var go = new GameObject("IntroFlyby");
        DontDestroyOnLoad(go);
        var flyby = go.AddComponent<IntroFlyby>();
        flyby._player = player;
        flyby.OnComplete = onComplete;
        flyby._capture = _captureWanted;
        _captureWanted = false;
        flyby.StartCoroutine(flyby.Sequence());
        return flyby;
    }

    IEnumerator Sequence()
    {
        // The cookie starts in ShadowsOnly (first-person default). Show him for the flyby.
        if (_player != null)
        {
            _player.SetFirstPerson(false);   // shows cookie via controller's own renderer list
        }

        var playerCam = _player != null ? _player.Camera : Camera.main;
        if (playerCam != null) playerCam.enabled = false;

        _flycam = new GameObject("FlybyCam").AddComponent<Camera>();
        _flycam.transform.SetParent(transform, false);   // destroyed with the flyby on skip
        _flycam.clearFlags = CameraClearFlags.Skybox;
        _flycam.fieldOfView = 55f;
        _flycam.nearClipPlane = 0.1f;
        _flycam.farClipPlane = 500f;
        _flycam.depth = 10f;

        // A fill light so Gingy is visible in the dusk. Destroyed with the flyby.
        var fill = new GameObject("FlybyFill").AddComponent<Light>();
        fill.type = LightType.Point;
        fill.range = 8f;
        fill.intensity = 3.5f;
        fill.color = new Color(1f, 0.92f, 0.78f);
        fill.transform.SetParent(_flycam.transform, false);
        fill.transform.localPosition = Vector3.zero;
        fill.shadows = LightShadows.None;

        BuildSubtitles();

        var cookieModel = _player != null
            ? _player.transform.Find("GingerbreadMesh/Cookie")
            : null;
        if (cookieModel != null)
        {
            _mouth = GingerbreadMesh.BuildMouth(cookieModel);
            if (_mouth != null) _mouth.Visible = false;
        }

        Vector3 playerPos = _player != null ? _player.transform.position : Vector3.zero;
        Vector3 fwd   = _player != null ? _player.transform.forward : Vector3.forward;
        Vector3 right = _player != null ? _player.transform.right : Vector3.right;

        // Positions relative to the cookie's facing direction
        Vector3 camStart      = playerPos + fwd * -8f  + Vector3.up * 12f  + right * 2f;
        Vector3 camFaceCookie = playerPos + fwd * 1.5f + Vector3.up * 1.45f + right * 0.3f;   // ~11° off-centre
        Vector3 camLookCookie = playerPos + Vector3.up * 1.35f;                     // eye level
        Vector3 camSweepOut   = playerPos + fwd * -2f  + Vector3.up * 2.5f  + right * 3f;

        // Phase 1: fly down
        yield return StartCoroutine(MoveCamera(camStart, camFaceCookie, 2.0f, camLookCookie, EaseInOutCubic));

        if (_skipped) { Cleanup(playerCam); yield break; }
        yield return new WaitForSeconds(0.35f);

        if (_capture) Capture("m42-flyby-face");

        // ---- Phase 2: Cookie speaks ----
        if (_mouth != null) _mouth.Visible = true;
        yield return StartCoroutine(SpeakDialogue());
        if (_mouth != null) _mouth.Visible = false;

        if (_skipped) { Cleanup(playerCam); yield break; }
        yield return new WaitForSeconds(0.5f);

        // Phase 3: sweep out
        yield return StartCoroutine(MoveCamera(camFaceCookie, camSweepOut, 1.3f, camLookCookie, EaseInOutCubic));

        Cleanup(playerCam);
    }

    /// <summary>Fires on any exit path — skip, natural completion, or Destroy.</summary>
    void OnDestroy()
    {
        var cam = _player != null ? _player.Camera : Camera.main;
        if (cam != null) cam.enabled = true;
        if (_player != null)
            _player.SetFirstPerson(true);
    }

    void Cleanup(Camera playerCam)
    {
        if (playerCam != null) playerCam.enabled = true;
        OnComplete?.Invoke();
        Destroy(gameObject, 0.3f);
    }

    IEnumerator SpeakDialogue()
    {
        var (clip, wordTimings) = IntroVoice.BuildLine(Dialogue, 0.82f);

        var voiceSource = _flycam.gameObject.AddComponent<AudioSource>();
        voiceSource.spatialBlend = 0f;
        voiceSource.PlayOneShot(clip);

        _subtitleText.text = "";
        float lineLength = clip.length;
        float elapsed = 0f;
        var words = Dialogue.Split(' ');

        while (elapsed < lineLength)
        {
            if (_skipped) yield break;
            elapsed += Time.deltaTime;

            int currentWord = -1;
            for (int i = 0; i < wordTimings.Length; i++)
                if (elapsed >= wordTimings[i].start && elapsed <= wordTimings[i].end)
                    { currentWord = i; break; }

            if (currentWord >= 0 && _mouth != null)
            {
                _mouth.OpenAmount = Mathf.Lerp(_mouth.OpenAmount, 0.75f, 14f * Time.deltaTime);
                int charCount = 0;
                for (int w = 0; w <= currentWord && w < words.Length; w++)
                {
                    if (w > 0) charCount++;
                    charCount += words[w].Length;
                }
                if (charCount > Dialogue.Length) charCount = Dialogue.Length;
                _subtitleText.text = Dialogue.Substring(0, charCount);
            }
            else
            {
                if (_mouth != null)
                    _mouth.OpenAmount = Mathf.Lerp(_mouth.OpenAmount, 0f, 10f * Time.deltaTime);
            }

            yield return null;
        }

        _subtitleText.text = Dialogue;
        if (_mouth != null) _mouth.OpenAmount = 0f;
    }

    IEnumerator MoveCamera(Vector3 from, Vector3 to, float duration, Vector3 lookAt, System.Func<float, float> ease)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (_skipped) yield break;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            _flycam.transform.position = Vector3.Lerp(from, to, ease(t));
            _flycam.transform.LookAt(lookAt);
            yield return null;
        }
        _flycam.transform.position = to;
    }

    void BuildSubtitles()
    {
        _subCanvas = new GameObject("SubtitleCanvas").AddComponent<Canvas>();
        _subCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _subCanvas.sortingOrder = 101;
        _subCanvas.transform.SetParent(transform, false);
        var scaler = _subCanvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        var textGo = new GameObject("SubtitleText", typeof(RectTransform));
        textGo.transform.SetParent(_subCanvas.transform, false);
        var rect = textGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.08f, 0.04f);
        rect.anchorMax = new Vector2(0.92f, 0.17f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        _subtitleText = textGo.AddComponent<Text>();
        _subtitleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _subtitleText.fontSize = 40;
        _subtitleText.alignment = TextAnchor.LowerCenter;
        _subtitleText.color = new Color(1f, 0.95f, 0.78f);
        _subtitleText.text = "";

        var outline = textGo.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.82f);
        outline.effectDistance = new Vector2(2f, -2f);
    }

    void Update()
    {
        if (!_skipped && (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0)))
        {
            _skipped = true;
            if (_mouth != null) _mouth.Visible = false;
        }
    }

    static float EaseInOutCubic(float t) =>
        t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;

    void Capture(string name)
    {
        string dir = Path.Combine(Application.persistentDataPath, "captures");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, name + ".png");
        ScreenCapture.CaptureScreenshot(path);
        Debug.Log("IntroFlyby capture: " + path);
    }
}