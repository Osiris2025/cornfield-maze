# M42 — Intro Flyby with Gingy Dialogue

> **For Hermes:** Implement task-by-task. Do not run Unity — only edit C# files.

**Goal:** Replace the 4-page text intro with a cinematic camera flyby: the camera sweeps through the corn to Gingy, who speaks with a squeaky voice (subtitled), then sweeps back to first-person view.

**Architecture:** A new `IntroFlyby` MonoBehaviour owns the camera animation (manual coroutine + AnimationCurves), a procedural squeaky voice (AudioClip.Create with formant synthesis), Canvas subtitles, and simple cookie facial animation (a procedural mouth GameObject + eye scaling). `GameFrontEnd` keeps its title screen but delegates the intro stage to `IntroFlyby`. No new packages; no new art assets except code-generated meshes.

**Tech Stack:** Unity C# (MonoBehaviour, coroutines, AnimationCurve, AudioClip.Create, Canvas/Text)

---

## Task 1: Create the procedural squeaky voice generator

**Objective:** Write a static utility that generates a dialogue AudioClip from text using simple formant synthesis — the cookie should sound high-pitched, nervous, squeaky.

**Files:**
- Create: `Assets/Scripts/IntroVoice.cs`

**Implementation:**

```csharp
using UnityEngine;

/// <summary>
/// Procedural squeaky voice for the gingerbread cookie's intro dialogue. No external assets — the
/// "voice" is a pitch-shifted buzz with formant-like filtering, subtitled in sync with playback.
/// </summary>
public static class IntroVoice
{
    const int SampleRate = 22050;
    const float BasePitch = 380f;      // squeaky base frequency
    const float VibratoRate = 8f;
    const float VibratoDepth = 15f;

    /// <summary>Generate a clip for one line of dialogue. Returns the clip and a timing array of
    /// (startTime, endTime) per word for subtitle sync.</summary>
    public static (AudioClip clip, (float start, float end)[] wordTimings) BuildLine(
        string text, float speed = 1f)
    {
        // Split into words for subtitle timing
        var words = text.Split(' ');
        float totalDuration = text.Length * 0.065f / speed;  // rough: ~65ms per char
        int totalSamples = Mathf.CeilToInt(SampleRate * totalDuration);

        var clip = AudioClip.Create("intro_line", totalSamples, 1, SampleRate, false);
        var data = new float[totalSamples];

        float wordLength = totalDuration / Mathf.Max(1, words.Length);
        var wordTimings = new (float start, float end)[words.Length];

        for (int i = 0; i < totalSamples; i++)
        {
            float t = i / (float)SampleRate;
            // Vibrato
            float freq = BasePitch + Mathf.Sin(2f * Mathf.PI * VibratoRate * t) * VibratoDepth;
            // Simple pulse wave (buzzy, like a small creature)
            float phase = (i * freq / SampleRate) % 1f;
            float sample = phase < 0.5f ? 0.8f : -0.8f;
            // Amplitude envelope: fade in/out per syllable
            float syllableT = (t % 0.18f) / 0.18f;
            float envelope = syllableT < 0.1f ? syllableT / 0.1f :
                             syllableT > 0.85f ? (1f - syllableT) / 0.15f : 1f;
            // Overall fade at ends
            float fadeIn = Mathf.Clamp01(t / 0.05f);
            float fadeOut = Mathf.Clamp01((totalDuration - t) / 0.08f);
            data[i] = sample * envelope * fadeIn * fadeOut * 0.35f;
        }

        clip.SetData(data, 0);
        clip.LoadAudioData();

        // Word timings
        for (int w = 0; w < words.Length; w++)
        {
            wordTimings[w] = (w * wordLength, (w + 1) * wordLength);
        }

        return (clip, wordTimings);
    }
}
```

**Verification:** No build — pure code file. Confirm it compiles by checking syntax.

---

## Task 2: Create the cookie mouth GameObject generator

**Objective:** `GingerbreadMesh` gets a static method that creates a simple mouth mesh (ellipse) parented to the cookie's head bone, with open/close states driven by a `MouthAnimator` component.

**Files:**
- Modify: `Assets/Scripts/FarmWalkerController.cs` (GingerbreadMesh class, ~line 1275)
- Create: `Assets/Scripts/MouthAnimator.cs`

**Step 1: Add MouthAnimator (new file)**

```csharp
using UnityEngine;

/// <summary>
/// Drives a simple mouth mesh on the gingerbread cookie for the intro flyby dialogue.
/// The mouth is a thin dark ellipse that scales on Y to open/close while speaking.
/// </summary>
public sealed class MouthAnimator : MonoBehaviour
{
    public float OpenAmount { get; set; }   // 0 = closed, 1 = full open
    public bool Visible { get; set; }

    Vector3 _baseScale;
    Vector3 _basePos;
    Renderer _rend;

    void Awake()
    {
        _rend = GetComponent<Renderer>();
        _baseScale = transform.localScale;
        _basePos = transform.localPosition;
    }

    void LateUpdate()
    {
        if (_rend != null) _rend.enabled = Visible;
        float s = Mathf.Lerp(0.15f, 1f, OpenAmount);
        transform.localScale = new Vector3(_baseScale.x, _baseScale.y * s, _baseScale.z);
        // Mouth opens downward from top edge
        transform.localPosition = _basePos + Vector3.down * (_baseScale.y * (1f - s) * 0.5f);
    }
}
```

**Step 2: Add BuildMouth to GingerbreadMesh (append to FarmWalkerController.cs)**

After the `Bone` helper (around line 1352), add:

```csharp
public static MouthAnimator BuildMouth(Transform cookieInstance)
{
    var head = Bone(cookieInstance, "spine02");
    if (head == null) return null;

    var go = new GameObject("CookieMouth");
    go.transform.SetParent(head, false);
    go.transform.localPosition = new Vector3(0f, -0.045f, 0.028f);
    go.transform.localRotation = Quaternion.identity;
    go.transform.localScale = new Vector3(0.032f, 0.008f, 0.002f);

    var mf = go.AddComponent<MeshFilter>();
    mf.sharedMesh = MouthMesh();

    var mr = go.AddComponent<MeshRenderer>();
    mr.sharedMaterial = MouthMaterial();
    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    mr.receiveShadows = false;

    return go.AddComponent<MouthAnimator>();
}

static Mesh MouthMesh()
{
    // Simple 8-vert ellipse
    var mesh = new Mesh { name = "Mouth" };
    int segs = 8;
    var verts = new Vector3[segs + 1];
    var tris = new int[segs * 3];
    verts[0] = Vector3.zero;
    for (int i = 0; i < segs; i++)
    {
        float a = i / (float)segs * Mathf.PI * 2f;
        verts[i + 1] = new Vector3(Mathf.Cos(a) * 0.5f, Mathf.Sin(a) * 0.5f, 0f);
        int j = i * 3;
        tris[j] = 0;
        tris[j + 1] = i + 1;
        tris[j + 2] = (i + 1) % segs + 1;
    }
    mesh.vertices = verts;
    mesh.triangles = tris;
    mesh.RecalculateBounds();
    return mesh;
}

static Material MouthMaterial()
{
    var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
    mat.color = new Color(0.08f, 0.04f, 0.02f);   // dark brown, like baked cookie crease
    return mat;
}
```

**Verification:** No build. Check syntax.

---

## Task 3: Create the IntroFlyby MonoBehaviour

**Objective:** A new component that owns the entire intro sequence: camera animation, dialogue, subtitles, mouth sync, and transition back to gameplay.

**Files:**
- Create: `Assets/Scripts/IntroFlyby.cs`

**Implementation:**

```csharp
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class IntroFlyby : MonoBehaviour
{
    public System.Action OnComplete;

    // The single line of dialogue
    const string Dialogue =
        "I'm lost! Can you help me find my way out of this maze? I'm scared — there is something called The Husk that is chasing me! I don't know what he will do if he catches me!";

    Camera _flycam;
    AudioSource _voiceSource;
    Canvas _subCanvas;
    Text _subtitleText;
    FarmWalkerController _player;

    MouthAnimator _mouth;
    Transform _cookieEyes;

    // Flyby path control points (relative to maze start position)
    static readonly Vector3 CamStart = new Vector3(8f, 12f, -15f);     // above and behind
    static readonly Vector3 CamFaceCookie = new Vector3(1.8f, 1.55f, 0.6f);  // close on face
    static readonly Vector3 CamSweepOut = new Vector3(-2f, 2.2f, -2.5f);    // pull back and around

    public static IntroFlyby Play(FarmWalkerController player, System.Action onComplete)
    {
        var go = new GameObject("IntroFlyby");
        var flyby = go.AddComponent<IntroFlyby>();
        flyby._player = player;
        flyby.OnComplete = onComplete;
        flyby.StartCoroutine(flyby.Sequence());
        return flyby;
    }

    IEnumerator Sequence()
    {
        // Disable the player's camera and build our flyby camera
        var playerCam = _player.Camera;
        if (playerCam != null) playerCam.enabled = false;

        _flycam = new GameObject("FlybyCam").AddComponent<Camera>();
        _flycam.clearFlags = CameraClearFlags.Skybox;
        _flycam.fieldOfView = 55f;
        _flycam.nearClipPlane = 0.1f;
        _flycam.farClipPlane = 500f;

        // Build subtitles canvas
        BuildSubtitles();

        // Build mouth if possible
        var cookieModel = _player.transform.Find("GingerbreadMesh/Cookie");
        if (cookieModel != null)
        {
            _mouth = GingerbreadMesh.BuildMouth(cookieModel);
            if (_mouth != null) _mouth.Visible = false;
        }

        // Find eye bones for scared expression
        _cookieEyes = GingerbreadMesh.Bone(cookieModel, "spine02"); // head bone — scale eyes from here
        // TODO: actual eye bone names depend on the FBX rig

        // ---- Phase 1: Fly down to the cookie --------------------------------
        Vector3 startPos = _player.transform.position + CamStart;
        Vector3 facePos = _player.transform.position + CamFaceCookie;
        yield return StartCoroutine(MoveCamera(startPos, facePos, 1.8f,
            EaseInOutCubic));

        yield return new WaitForSeconds(0.3f);

        // ---- Phase 2: Cookie speaks ------------------------------—
        if (_mouth != null) _mouth.Visible = true;
        yield return StartCoroutine(SpeakDialogue());

        yield return new WaitForSeconds(0.4f);

        // ---- Phase 3: Sweep out and transition to first person --------------------
        if (_mouth != null) _mouth.Visible = false;
        Vector3 sweepPos = _player.transform.position + CamSweepOut;
        yield return StartCoroutine(MoveCamera(facePos, sweepPos, 1.2f,
            EaseInOutCubic));

        // Transition to first-person
        if (plasyerCam != null)
        {
            playerCam.enabled = true;
            // First person is the default (M27) — just ensure it
            playerCam.transform.localPosition = new Vector3(0f, 0.62f, 0f);
            playerCam.transform.localRotation = Quaternion.identity;
        }

        Destroy(_flycam.gameObject);
        Destroy(gameObject, 0.2f);
        OnComplete?.Invoke();
    }

    IEnumerator SpeakDialogue()
    {
        var (clip, wordTimings) = IntroVoice.BuildLine(Dialogue, 0.85f);

        _voiceSource = _flycam.gameObject.AddComponent<AudioSource>();
        _voiceSource.spatialBlend = 0f;
        _voiceSource.PlayOneShot(clip);
        _voiceSource.PlayOneShot(clip);

        _subtitleText.text = "";
        float lineLength = clip.length;
        float elapsed = 0f;

        // Mouth opens/closes per word
        while (elapsed < LineLength)
        {
            elapsed += Time.deltaTime;

            // Find current word
            int currentWord = -1;
            for (int i = 0; i < wordTimings.Length; i++)
                if (elapsed >= wordTimings[i].start && elapsed <= wordTimings[i].end)
                    { currentWord = i; break; }

            if (currentWord >= 0)
            {
                _mouth.OpenAmount = Mathf.Lerp(_mouth.OpenAmount, 0.8f, 12f * Time.deltaTime);
                // Show substring up to current word
                int charCount = 0;
                for (int w = 0; w <= currentWord && w < Dialogue.Split(' ').Length; w++)
                    charCount += Dialogue.Split(' ')[w].Length + (w > 0 ? 1 : 0);
                if (charCount >= Dialogue.Length) charCount = Dialogue.Length;
                _subtitleText.text = Dialogue.Substring(0, charCount);
            }
            else
            {
                _mouth.OpenAmount = Mathf.Lerp(_mouth.OpenAmount, 0f, 8f * Time.deltaTime);
            }

            yield return null;
        }

        _subtitleText.text = Dialogue;
        _mouth.OpenAmount = 0f;
    }

    IEnumerator MoveCamera(Vector3 from, Vector3 to, float duration, System.Func<float, float> ease)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            _flycam.transform.position = Vector3.Lerp(from, to, ease(t));
            // Look at the cookie during the whole move
            Vector3 lookTarget = _player.transform.position + Vector3.up * 1.3f;
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
        rect.anchorMin = new Vector2(0.1f, 0.05f);
        rect.anchorMax = new Vector2(0.9f, 0.18f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        _subtitleText = textGo.AddComponent<Text>();
        _subtitleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _subtitleText.fontSize = 42;
        _subtitleText.alignment = TextAnchor.LowerCenter;
        _subtitleText.color = new Color(1f, 0.95f, 0.80f);
        _subtitleText.text = "";

        var outline = textGo.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
        outline.effectDistance = new Vector2(2f, -2f);
    }

    static float EaseInOutCubic(float t) =>
        t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
}
```

**Verification:** No build. Check syntax.

---

## Task 4: Wire IntroFlyby into GameFrontEnd

**Objective:** Replace the text-only `IntroPages` with the new flyby. `GameFrontEnd.AdvanceFromTitle()` starts `IntroFlyby.Play()` instead of showing text pages. The skip/advance still works (Space or click skips the flyby).

**Files:**
- Modify: `Assets/Scripts/GameFrontEnd.cs`

**Changes:**

1. Remove `IntroPages` array and `_introPage`/`_introText`/`_introButton`/`_introButtonLabel` fields (keep `_introPanel` for the skip prompt)
2. In `AdvanceFromTitle()`: start the flyby
3. Add a skip handler

```csharp
// Replace the IntroPages field (line 47-53) with:
IntroFlyby _flyby;

// Replace AdvanceFromTitle (line 272-276):
void AdvanceFromTitle()
{
    if (!_introSeen)
    {
        _introSeen = true;
        _flyby = IntroFlyby.Play(_player, () =>
        {
            _flyby = null;
            BeginRun();
        });
    }
    else BeginRun();
}

// In Update(), add skip for intro flyby (alongside existing Space handler):
// Add this inside Update() right after the existing advance checks:
if (_flyby != null && (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0)))
{
    // Skip the flyby
    Destroy(_flyby.gameObject);
    _flyby = null;
    BeginRun();
}
```

**Verification:** Check syntax. No build.

---

## Task 5: Add self-test flag and commit

**Objective:** Add a `-introflyby` flag to test the intro in isolation, and commit all changes.

**Files:**
- Modify: `Assets/Scripts/IntroFlyby.cs` — add the flag gating
- Create: `docs/plans/2026-09-27-m42-intro-flyby.md` — this plan

**Commit:**
```bash
git add Assets/Scripts/IntroFlyby.cs Assets/Scripts/MouthAnimator.cs Assets/Scripts/IntroVoice.cs Assets/Scripts/GameFrontEnd.cs Assets/Scripts/FarmWalkerController.cs docs/plans/2026-09-27-m42-intro-flyby.md
git commit -m "feat(M42): intro flyby camera + Gingy dialogue with procedural squeaky voice and subtitles"
```

---

## Risks & Open Questions

1. **Mouth position on the FBX**: The `BuildMouth` positions use `spine02` (the head bone) with hardcoded offsets tuned for a gingerbread man face. If `spine02` is actually the upper chest, the mouth will be in the wrong place — verify on the real model.

2. **Voice quality**: The procedural voice will sound like a buzzy chipmunk, not a real voice. This is per house rules (no external audio assets), but it may not meet Todd's expectation for "squeaky little voice." He may want to source or record a real voice clip later — the architecture supports swapping `IntroVoice.BuildLine` for an `AudioClip` load.

3. **Camera path through corn**: The flyby starts above the corn and descends. If the start position is inside a wall cell, the camera will clip through corn stalks. The `CamStart` offset assumes the player starts in a lane facing into the maze — verify with the actual `maze.StartWorld` position.

4. **Expression (scared eyes)**: The plan scales the eye bones for a "wide-eyed scared" look, but the exact eye bone names depend on the FBX rig. If the FBX has no dedicated eye bones, this becomes a stretch-bone-on-the-head approximation.