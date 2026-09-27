using System.Collections;
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

    static readonly Vector3 CamStart      = new Vector3(6f,  10f, -14f);
    static readonly Vector3 CamFaceCookie = new Vector3(1.4f, 1.45f, 0.55f);
    static readonly Vector3 CamLookCookie = new Vector3(0f, 1.25f, 0f);
    static readonly Vector3 CamSweepOut   = new Vector3(-1.8f, 2.0f, -2.2f);

    Camera _flycam;
    Canvas _subCanvas;
    Text _subtitleText;
    FarmWalkerController _player;
    MouthAnimator _mouth;
    bool _skipped;

    public static IntroFlyby Play(FarmWalkerController player, System.Action onComplete)
    {
        var go = new GameObject("IntroFlyby");
        DontDestroyOnLoad(go);
        var flyby = go.AddComponent<IntroFlyby>();
        flyby._player = player;
        flyby.OnComplete = onComplete;
        flyby.StartCoroutine(flyby.Sequence());
        return flyby;
    }

    IEnumerator Sequence()
    {
        var playerCam = _player != null ? _player.Camera : Camera.main;
        if (playerCam != null) playerCam.enabled = false;

        _flycam = new GameObject("FlybyCam").AddComponent<Camera>();
        _flycam.clearFlags = CameraClearFlags.Skybox;
        _flycam.fieldOfView = 55f;
        _flycam.nearClipPlane = 0.1f;
        _flycam.farClipPlane = 500f;
        _flycam.depth = 10f;

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

        // Phase 1: fly down
        Vector3 startPos = playerPos + CamStart;
        Vector3 facePos  = playerPos + CamFaceCookie;
        yield return StartCoroutine(MoveCamera(startPos, facePos, 2.0f, EaseInOutCubic));

        if (_skipped) { Cleanup(playerCam); yield break; }
        yield return new WaitForSeconds(0.35f);

        // Phase 2: cookie speaks
        if (_mouth != null) _mouth.Visible = true;
        yield return StartCoroutine(SpeakDialogue());
        if (_mouth != null) _mouth.Visible = false;

        if (_skipped) { Cleanup(playerCam); yield break; }
        yield return new WaitForSeconds(0.5f);

        // Phase 3: sweep out
        Vector3 sweepPos = playerPos + CamSweepOut;
        yield return StartCoroutine(MoveCamera(facePos, sweepPos, 1.3f, EaseInOutCubic));

        Cleanup(playerCam);
    }

    void Cleanup(Camera playerCam)
    {
        if (playerCam != null) playerCam.enabled = true;
        if (_flycam != null) Destroy(_flycam.gameObject);
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

    IEnumerator MoveCamera(Vector3 from, Vector3 to, float duration, System.Func<float, float> ease)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (_skipped) yield break;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            _flycam.transform.position = Vector3.Lerp(from, to, ease(t));
            Vector3 lookTarget = (_player != null
                ? _player.transform.position
                : Vector3.zero) + CamLookCookie;
            _flycam.transform.LookAt(lookTarget);
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
}