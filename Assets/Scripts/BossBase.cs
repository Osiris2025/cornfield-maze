using UnityEngine;

/// <summary>
/// M47: abstract boss base class. Shared grammar for all bosses:
/// contact damage 25, heavy telegraphed attack for 40,
/// phase breaks at 66% and 33% HP, health bar at top of screen,
/// cannot leave the arena.
/// </summary>
public abstract class BossBase : MonoBehaviour
{
    [Header("Stats")]
    public float maxHP = 300f;
    public float contactDamage = 25f;
    public float heavyDamage = 40f;
    public float telegraphSeconds = 0.8f;

    public float currentHP { get; protected set; }
    public int currentPhase { get; protected set; } = 1;

    protected FarmWalkerController _player;
    protected bool _dead;

    /// <summary>
    /// M47: callback invoked when the boss dies. Set by GameBootstrap
    /// to wire PotOfGold.OnCollected → HandleWin.
    /// </summary>
    public System.Action onDeathWin;

    // Health bar
    GameObject _healthBarRoot;
    UnityEngine.UI.Image _healthFill;

    protected virtual void Start()
    {
        currentHP = maxHP;
        _player = FindFirstObjectByType<FarmWalkerController>();
        BuildHealthBar();
    }

    /// <summary>
    /// Apply damage. Returns true if the boss died this hit.
    /// Checks phase breaks at 66% and 33%.
    /// </summary>
    public virtual bool TakeDamage(float amount)
    {
        if (_dead) return true;

        currentHP = Mathf.Max(0f, currentHP - amount);

        int oldPhase = currentPhase;
        float pct = currentHP / maxHP;
        if (pct <= 0f)
            currentPhase = 4; // dead
        else if (pct <= 0.33f)
            currentPhase = 3;
        else if (pct <= 0.66f)
            currentPhase = 2;
        else
            currentPhase = 1;

        if (currentPhase != oldPhase && !_dead)
        {
            OnPhaseChange(currentPhase);
            PlayPhaseSting();
        }

        UpdateHealthBar();

        if (currentHP <= 0f && !_dead)
        {
            _dead = true;
            Die();
            return true;
        }

        return false;
    }

    /// <summary>Called when the phase changes. Override for behaviour swaps.</summary>
    protected virtual void OnPhaseChange(int newPhase)
    {
        Debug.Log($"[{GetType().Name}] Phase {newPhase} — HP: {currentHP}/{maxHP}");
    }

    /// <summary>Execute the boss's heavy/telegraphed attack. Override per-boss.</summary>
    protected abstract void DoHeavyAttack();

    /// <summary>Per-frame behaviour update. Called from Update.</summary>
    protected abstract void UpdateBehaviour();

    void Update()
    {
        if (_dead) return;
        UpdateBehaviour();
    }

    /// <summary>Death sequence: play sting, spawn PotOfGold, award coins, destroy.</summary>
    protected virtual void Die()
    {
        Debug.Log($"[{GetType().Name}] Defeated!");

        // Spawn the pot of gold at arena center, wired to HandleWin
        var pot = PotOfGold.Spawn(transform.position);
        if (onDeathWin != null)
            pot.OnCollected = onDeathWin;

        CoinWallet.Instance.Add(50);

        // Clean up
        if (_healthBarRoot != null)
            Destroy(_healthBarRoot);

        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (_healthBarRoot != null)
            Destroy(_healthBarRoot);
    }

    /// <summary>
    /// Build a thin dough-grey health bar at the top of the screen.
    /// Uses a screen-space Canvas so it's always visible during the boss fight.
    /// </summary>
    void BuildHealthBar()
    {
        _healthBarRoot = new GameObject("BossHealthBar");

        var canvas = _healthBarRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var canvasScaler = _healthBarRoot.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasScaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1920, 1080);

        _healthBarRoot.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // Background bar
        var bgGo = new GameObject("BarBackground");
        bgGo.transform.SetParent(_healthBarRoot.transform, false);
        var bgImage = bgGo.AddComponent<UnityEngine.UI.Image>();
        bgImage.color = new Color(0.15f, 0.13f, 0.10f, 0.85f); // dark dough
        var bgRect = bgGo.GetComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0.15f, 0.96f);
        bgRect.anchorMax = new Vector2(0.85f, 0.975f);
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        // Fill bar
        var fillGo = new GameObject("BarFill");
        fillGo.transform.SetParent(bgGo.transform, false);
        _healthFill = fillGo.AddComponent<UnityEngine.UI.Image>();
        _healthFill.color = new Color(0.55f, 0.43f, 0.30f, 0.95f); // dough-grey
        var fillRect = fillGo.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        fillRect.pivot = new Vector2(0f, 0.5f);
    }

    /// <summary>Update the health bar fill amount.</summary>
    void UpdateHealthBar()
    {
        if (_healthFill != null)
        {
            float fill = Mathf.Clamp01(currentHP / maxHP);
            _healthFill.rectTransform.anchorMax = new Vector2(fill, 1f);

            // Colour shifts toward red/crumbling as HP drops
            if (fill < 0.33f)
                _healthFill.color = new Color(0.78f, 0.25f, 0.18f, 0.95f);
            else if (fill < 0.66f)
                _healthFill.color = new Color(0.72f, 0.52f, 0.25f, 0.95f);
            else
                _healthFill.color = new Color(0.55f, 0.43f, 0.30f, 0.95f);
        }
    }

    /// <summary>Play an audible sting on phase change (procedural tone).</summary>
    void PlayPhaseSting()
    {
        var go = new GameObject("PhaseSting");
        var source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;

        int samples = Mathf.CeilToInt(44100 * 0.35f);
        var clip = AudioClip.Create("phaseSting", samples, 1, 44100, false);
        var data = new float[samples];
        float freq = currentPhase == 3 ? 320f : 260f;
        for (int i = 0; i < samples; i++)
        {
            float t = i / 44100f;
            data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * Mathf.Exp(-5f * t) * 0.32f;
        }
        clip.SetData(data, 0);
        clip.LoadAudioData();
        source.PlayOneShot(clip);
        Destroy(go, 0.55f);
    }

    /// <summary>
    /// Flash this boss's colour to red and back over the telegraph duration.
    /// Call from derived classes when a heavy attack telegraph begins.
    /// </summary>
    protected System.Collections.IEnumerator TelegraphFlash(Renderer[] renderers, float duration)
    {
        if (renderers == null || renderers.Length == 0) yield break;

        // Store original colours
        var originals = new Color[renderers.Length];
        var mats = new Material[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            mats[i] = renderers[i].sharedMaterial;
            originals[i] = mats[i] != null && mats[i].HasProperty("_BaseColor")
                ? mats[i].GetColor("_BaseColor")
                : (mats[i] != null && mats[i].HasProperty("_Color") ? mats[i].GetColor("_Color") : Color.white);
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float lerp = Mathf.PingPong(t / (duration * 0.5f), 1f);
            Color flash = Color.Lerp(Color.white, new Color(0.85f, 0.15f, 0.08f), lerp);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null || mats[i] == null) continue;
                if (mats[i].HasProperty("_BaseColor")) mats[i].SetColor("_BaseColor", flash);
                if (mats[i].HasProperty("_Color")) mats[i].SetColor("_Color", flash);
            }
            yield return null;
        }

        // Restore originals
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null || mats[i] == null) continue;
            if (mats[i].HasProperty("_BaseColor")) mats[i].SetColor("_BaseColor", originals[i]);
            if (mats[i].HasProperty("_Color")) mats[i].SetColor("_Color", originals[i]);
        }
    }
}