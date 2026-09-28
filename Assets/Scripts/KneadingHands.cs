using UnityEngine;

/// <summary>
/// M47 — B1: THE KNEADING HANDS (300 HP).
/// Boss fight with slap and pincer attacks, three phases.
/// </summary>
public sealed class KneadingHands : BossBase
{
    // Hands
    GameObject _handL;
    GameObject _handR;
    Renderer[] _handRenderers;
    Material _handMat;
    Material _handMatOrig;

    // Arena bounds
    Vector3 _arenaCenter;
    float _arenaRadius = 5f;

    // Attack state
    float _slapCooldown;
    float _pincerCooldown;
    float _slapCooldownTime = 2.5f;
    float _pincerCooldownTime = 6f;
    bool _slapActive;
    bool _pincerActive;
    float _slapTimer;
    float _pincerTimer;

    // Slap from left side
    Vector3 _slapStart;
    Vector3 _slapTarget;
    bool _slapTelegraphed;
    bool _slapStruck;

    // Pincer
    Vector3 _pincerLStart;
    Vector3 _pincerLTarget;
    Vector3 _pincerRStart;
    Vector3 _pincerRTarget;
    bool _pincerTelegraphed;
    bool _pincerClosed;

    float HandHalfSize = 0.65f;
    float StrikeRadius = 1.8f;

    protected override void Start()
    {
        maxHP = 300f;
        base.Start();

        _arenaCenter = transform.position;
        BuildHands();
    }

    void BuildHands()
    {
        // Add a collider so the cob can hit the boss
        var col = gameObject.AddComponent<SphereCollider>();
        col.radius = 1.2f;
        col.center = new Vector3(0f, 1f, 0f);

        _handMat = Materials.Lit(new Color(0.52f, 0.38f, 0.24f), 0.12f);
        _handMatOrig = new Material(_handMat);

        _handL = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _handL.name = "HandL";
        _handL.transform.SetParent(transform, false);
        _handL.transform.localPosition = new Vector3(-2f, 1.5f, 1f);
        _handL.transform.localScale = Vector3.one * HandHalfSize;
        _handL.GetComponent<Renderer>().sharedMaterial = _handMat;
        Object.Destroy(_handL.GetComponent<Collider>());

        _handR = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _handR.name = "HandR";
        _handR.transform.SetParent(transform, false);
        _handR.transform.localPosition = new Vector3(2f, 1.5f, 1f);
        _handR.transform.localScale = Vector3.one * HandHalfSize;
        _handR.GetComponent<Renderer>().sharedMaterial = _handMat;
        Object.Destroy(_handR.GetComponent<Collider>());

        _handRenderers = new Renderer[]
        {
            _handL.GetComponent<Renderer>(),
            _handR.GetComponent<Renderer>()
        };
    }

    protected override void UpdateBehaviour()
    {
        if (_dead) return;

        float dt = Time.deltaTime;
        float telegraph = currentPhase >= 3 ? 0.6f : telegraphSeconds;

        // Clamp boss to arena
        Vector3 flat = transform.position;
        flat.y = 0f;
        Vector3 arenaFlat = _arenaCenter;
        arenaFlat.y = 0f;
        if (Vector3.Distance(flat, arenaFlat) > _arenaRadius * 0.85f)
        {
            Vector3 toward = (arenaFlat - flat).normalized;
            transform.position += toward * 2f * dt;
        }

        // Cooldown ticks
        _slapCooldown -= dt;
        _pincerCooldown -= dt;

        // P1: Slap only. P2: Pincer only. P3: both.
        bool canSlap = (currentPhase == 1 || currentPhase >= 3) && _slapCooldown <= 0f;
        bool canPincer = (currentPhase == 2 || currentPhase >= 3) && _pincerCooldown <= 0f;

        // Start attacks
        if (!_slapActive && !_pincerActive)
        {
            if (canSlap)
            {
                StartSlap(telegraph);
            }
            else if (canPincer)
            {
                StartPincer(telegraph);
            }
        }

        // Tick active attacks
        if (_slapActive) TickSlap(dt);
        if (_pincerActive) TickPincer(dt);

        // Bob hands
        float bob = Mathf.Sin(Time.time * 2.5f) * 0.12f;
        if (_handL != null) _handL.transform.localPosition = new Vector3(_handL.transform.localPosition.x, 1.5f + bob, _handL.transform.localPosition.z);
        if (_handR != null) _handR.transform.localPosition = new Vector3(_handR.transform.localPosition.x, 1.5f + bob * 0.8f, _handR.transform.localPosition.z);
    }

    // ---- SLAP ----
    void StartSlap(float telegraph)
    {
        _slapActive = true;
        _slapTelegraphed = false;
        _slapStruck = false;
        _slapTimer = 0f;

        // Telegraph flash
        StartCoroutine(TelegraphFlash(_handRenderers, telegraph));

        // Hand rears back
        if (_handL != null) _handL.transform.localPosition = new Vector3(-2f, 2.8f, -1.5f);

        if (_player != null)
        {
            Vector3 toPlayer = (_player.transform.position - transform.position).normalized;
            _slapTarget = transform.position + toPlayer * 3.5f;
        }
        else
        {
            _slapTarget = transform.position + transform.forward * 3.5f;
        }
    }

    void TickSlap(float dt)
    {
        _slapTimer += dt;
        float telegraphDur = currentPhase >= 3 ? 0.6f : telegraphSeconds;

        if (!_slapTelegraphed && _slapTimer >= telegraphDur)
        {
            _slapTelegraphed = true;
            // Slam forward
            if (_handL != null)
                _handL.transform.position = _slapTarget + Vector3.up * 0.6f;
        }

        if (_slapTelegraphed && !_slapStruck && _slapTimer >= telegraphDur + 0.15f)
        {
            _slapStruck = true;
            // Check if player is in strike zone
            if (_player != null && !_player.IsWon && !_player.IsCaught)
            {
                float dist = Vector3.Distance(
                    new Vector3(_player.transform.position.x, 0f, _player.transform.position.z),
                    new Vector3(_slapTarget.x, 0f, _slapTarget.z));
                if (dist < StrikeRadius)
                {
                    _player.TakeBite(heavyDamage, (_player.transform.position - transform.position).normalized);
                }
            }
        }

        if (_slapTimer >= telegraphDur + 0.5f)
        {
            // Retract hand
            if (_handL != null)
                _handL.transform.localPosition = new Vector3(-2f, 1.5f, 1f);
            _slapActive = false;
            _slapCooldown = _slapCooldownTime;
        }
    }

    // ---- PINCER ----
    void StartPincer(float telegraph)
    {
        _pincerActive = true;
        _pincerTelegraphed = false;
        _pincerClosed = false;
        _pincerTimer = 0f;

        StartCoroutine(TelegraphFlash(_handRenderers, telegraph));

        _pincerLStart = transform.position + Vector3.left * _arenaRadius;
        _pincerLTarget = transform.position + Vector3.left * 0.8f;
        _pincerRStart = transform.position + Vector3.right * _arenaRadius;
        _pincerRTarget = transform.position + Vector3.right * 0.8f;

        if (_handL != null) _handL.transform.position = _pincerLStart + Vector3.up * 1.5f;
        if (_handR != null) _handR.transform.position = _pincerRStart + Vector3.up * 1.5f;
    }

    void TickPincer(float dt)
    {
        _pincerTimer += dt;
        float telegraphDur = currentPhase >= 3 ? 0.6f : telegraphSeconds;

        if (!_pincerTelegraphed && _pincerTimer >= telegraphDur)
        {
            _pincerTelegraphed = true;
        }

        if (_pincerTelegraphed && !_pincerClosed)
        {
            float sweepProgress = Mathf.Clamp01((_pincerTimer - telegraphDur) / 1.2f);
            if (_handL != null)
                _handL.transform.position = Vector3.Lerp(_pincerLStart, _pincerLTarget, sweepProgress) + Vector3.up * 1.5f;
            if (_handR != null)
                _handR.transform.position = Vector3.Lerp(_pincerRStart, _pincerRTarget, sweepProgress) + Vector3.up * 1.5f;

            // Contact damage while hands sweep
            if (_player != null && !_player.IsWon && !_player.IsCaught)
            {
                float distL = _handL != null ? Vector3.Distance(
                    new Vector3(_player.transform.position.x, 0f, _player.transform.position.z),
                    new Vector3(_handL.transform.position.x, 0f, _handL.transform.position.z)) : float.MaxValue;
                float distR = _handR != null ? Vector3.Distance(
                    new Vector3(_player.transform.position.x, 0f, _player.transform.position.z),
                    new Vector3(_handR.transform.position.x, 0f, _handR.transform.position.z)) : float.MaxValue;
                if (distL < StrikeRadius || distR < StrikeRadius)
                {
                    _player.TakeBite(contactDamage, (_player.transform.position - transform.position).normalized);
                }
            }

            if (sweepProgress >= 1f)
            {
                _pincerClosed = true;
            }
        }

        if (_pincerTimer >= telegraphDur + 1.8f)
        {
            if (_handL != null) _handL.transform.localPosition = new Vector3(-2f, 1.5f, 1f);
            if (_handR != null) _handR.transform.localPosition = new Vector3(2f, 1.5f, 1f);
            _pincerActive = false;
            _pincerCooldown = _pincerCooldownTime;
        }
    }

    // ---- BossBase overrides ----
    protected override void DoHeavyAttack()
    {
        // Driven by UpdateBehaviour via slap/pincer
    }

    /// <summary>
    /// M47: receive cob hits. The cob only targets Husk via GetComponentInParent,
    /// so the boss handles its own hit detection via OnCollisionEnter with the cob.
    /// </summary>
    void OnCollisionEnter(Collision collision)
    {
        if (_dead) return;
        var cob = collision.collider.GetComponentInParent<CornCob>();
        if (cob == null) return;

        // CornCob.HitDamage is 6f, but the boss is balanced for ~15 cob throws.
        // Use 20 damage per cob hit (~15 throws for 300 HP).
        TakeDamage(20f);
        Debug.Log($"[KneadingHands] Hit by cob — HP: {currentHP}/{maxHP}");
    }

    protected override void OnPhaseChange(int newPhase)
    {
        base.OnPhaseChange(newPhase);
        // Phase 3: faster attacks (telegraph handled in UpdateBehaviour)
        if (newPhase >= 3)
        {
            _slapCooldownTime = 1.8f;
            _pincerCooldownTime = 4.5f;
        }

        // Stop any active attack on phase change
        _slapActive = false;
        _pincerActive = false;
        if (_handL != null) _handL.transform.localPosition = new Vector3(-2f, 1.5f, 1f);
        if (_handR != null) _handR.transform.localPosition = new Vector3(2f, 1.5f, 1f);
    }
}