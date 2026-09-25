using UnityEngine;

/// <summary>
/// THE HUSK — the thing that chases Gingy. What is left of a corn plant once it is stripped.
/// Original design, not Sesame Street IP.
/// Spawns after a short delay, pathfinds along maze lanes toward the gingerbread player,
/// and is slow enough that staying on the correct route keeps you safe.
/// Wrong turns / backtracking into it can get you eaten.
/// </summary>
public sealed class Husk : MonoBehaviour
{
    public const string DisplayName = "the Husk";
    const float SpawnDelay = 5f;
    /// <summary>Base chaser speed. §25.3 derives the ground a thrown cob buys from this number.</summary>
    public const float MoveSpeed = 2.35f;
    const float RepathSeconds = 0.85f;
    public const float CatchDistance = 0.62f;
    const float LaneHalf = 0.42f;
    const float TurnSpeed = 7f;

    /// <summary>§25.3: a thrown cob takes one third of this — 6 dough of 18.</summary>
    public const float MaxHealth = 18f;
    /// <summary>
    /// After three hits the Husk is scattered, not dead. §25.3 is explicit that the throw buys a
    /// walk-around rather than killing anything, and §8's fairness law forbids a threat being
    /// trivially removable, so a scattered Husk re-forms. The player spends a whole night's ammo to
    /// buy a long walk, and the maze does not become a corridor.
    /// </summary>
    public const float ReformSeconds = 8f;

    /// <summary>
    /// The Husk is built from primitives with their colliders stripped, so it had no hit volume at all
    /// and a thrown cob could fly straight through it. This is a TRIGGER on purpose: §25.3 requires
    /// that the player stays kinematic and that the cob and the player never interact through physics.
    /// A trigger volume never blocks or pushes the CharacterController, so the Husk still catches by
    /// distance, exactly as before.
    ///
    /// It is a COLUMN, 2.6 m tall, and deliberately taller than the model: the cob leaves the hand at
    /// 1.27 m on a 20° launch, so its arc peaks near 1.9 m. A volume that only matched the visible
    /// body would have every throw inside 6 m sail clean over its head, and the mechanic would read as
    /// broken while measuring "correct".
    /// </summary>
    public const float HitVolumeHeight = 2.60f;
    public const float HitVolumeRadius = 0.50f;

    /// <summary>Harness/verification view of the threat: health, and how long its stagger ran.</summary>
    public float Health { get; private set; } = MaxHealth;
    public float StaggerLeft { get; private set; }
    public float LastStaggerSeconds { get; private set; }
    public int HitsTaken { get; private set; }
    public bool Scattered { get; private set; }

    MazeData _maze;
    FarmWalkerController _player;
    Transform _model;
    Transform _jaw;
    Transform _mouthAnchor;
    Vector2Int _goalCell;
    Vector3 _travel = Vector3.forward;
    float _repathTimer;
    bool _active;
    bool _eating;
    bool _caughtPlayer;
    float _reformAt;
    float _flinch;
    // ---- M28 (§25.8): the scarecrow's own materials, built here rather than in Materials.cs ---------
    Material _bodyMat;          // the tattered coat
    Material _mouthMat;         // the dark behind the torn seam
    Material _strawMat;
    Material _strawPaleMat;
    Transform _head;
    Transform _sleeveL;
    Transform _sleeveR;
    Quaternion _headRest;
    Quaternion _sleeveRestL;
    Quaternion _sleeveRestR;
    float _lurchPhase;

    /// <summary>M28: the measured height of the built creature, from its own renderer bounds.</summary>
    public float HeightMeters { get; private set; }
    /// <summary>M28: the widest the silhouette gets — the crossbar. Compared against the hit volume.</summary>
    public float SilhouetteWidthMeters { get; private set; }
    /// <summary>M28: how many primitives it is made of. Evidence that it is built, not posed.</summary>
    public int PartCount { get; private set; }

    /// <summary>§25.8: the lurch. One surge per LurchSeconds, with the speed swinging LurchDepth either
    /// side of MoveSpeed. A full sine averages exactly MoveSpeed, so the ground the player buys with a
    /// thrown cob (§25.3) is unchanged — this is the tell, not a speed change.</summary>
    public const float LurchSeconds = 0.72f;
    public const float LurchDepth = 0.55f;
    const float LurchHop = 0.07f;
    const float LurchRoll = 5.5f;

    public static Husk Spawn(MazeData maze, FarmWalkerController player)
    {
        var go = new GameObject("Husk");
        var hitVolume = go.AddComponent<CapsuleCollider>();
        hitVolume.isTrigger = true;
        hitVolume.height = HitVolumeHeight;
        hitVolume.radius = HitVolumeRadius;
        hitVolume.center = new Vector3(0f, HitVolumeHeight * 0.5f, 0f);
        var beast = go.AddComponent<Husk>();
        beast._maze = maze;
        beast._player = player;

        // Spawn behind the start on a side path when possible (not on the gold-bound first step).
        Vector3 spawn = maze.StartWorld;
        var start = maze.StartCell;
        var goldStep = maze.NextStepTowardGold(start);
        var steps = new[]
        {
            new Vector2Int(0, -1), new Vector2Int(-1, 0), new Vector2Int(1, 0), new Vector2Int(0, 1)
        };
        Vector2Int spawnCell = start;
        bool found = false;
        foreach (var step in steps)
        {
            var c = start + step;
            if (!maze.IsPath(c.x, c.y) || c == goldStep) continue;
            spawnCell = c;
            found = true;
            // Walk one more cell away from start if available.
            foreach (var step2 in steps)
            {
                var c2 = c + step2;
                if (maze.IsPath(c2.x, c2.y) && c2 != start)
                {
                    spawnCell = c2;
                    break;
                }
            }
            break;
        }
        if (!found)
        {
            // Fallback: two cells opposite the gold step.
            var away = start - (goldStep - start);
            if (maze.IsPath(away.x, away.y))
                spawnCell = away;
        }
        spawn = maze.CellToWorld(spawnCell.x, spawnCell.y);
        go.transform.position = spawn + Vector3.up * 0.05f;
        beast.BuildMesh();
        if (beast._model != null)
            beast._model.gameObject.SetActive(false);
        beast._spawnAt = Time.timeSinceLevelLoad + SpawnDelay;
        return beast;
    }

    float _spawnAt;

    void BuildMesh()
    {
        // ---- M28 (§25.8): the scarecrow. Primitives and procedural materials, like everything else
        // (§17) — no third-party model, so no licence question. Named parts, so the report can count them.
        var timber    = Materials.Lit(new Color(0.34f, 0.29f, 0.22f), 0.06f);   // weathered wood
        var timberDry = Materials.Lit(new Color(0.43f, 0.37f, 0.29f), 0.05f);
        _bodyMat      = Materials.Lit(new Color(0.19f, 0.20f, 0.23f), 0.05f);   // the tattered coat
        var coatDark  = Materials.Lit(new Color(0.13f, 0.14f, 0.16f), 0.05f);
        var sack      = Materials.Lit(new Color(0.58f, 0.48f, 0.31f), 0.04f);   // burlap, rough as burlap is
        var sackDark  = Materials.Lit(new Color(0.45f, 0.37f, 0.24f), 0.04f);
        _strawMat     = Materials.Lit(new Color(0.70f, 0.60f, 0.30f), 0.03f);   // dry husks
        _strawPaleMat = Materials.Lit(new Color(0.80f, 0.72f, 0.43f), 0.03f);
        var stitch    = Materials.Lit(new Color(0.05f, 0.04f, 0.04f), 0.02f);
        _mouthMat     = Materials.Lit(new Color(0.15f, 0.05f, 0.04f), 0.05f);   // the dark behind the seam
        var socketMat = Materials.Lit(new Color(0.02f, 0.02f, 0.02f), 0.02f);
        var glowMat   = GlowMat(new Color(0.95f, 0.58f, 0.18f), 3.2f);         // the only light on it

        _model = new GameObject("ScarecrowMesh").transform;
        _model.SetParent(transform, false);

        // The cross. The upright runs from the ground up into the head and the crossbar is set on a skew;
        // both show wherever the coat does not cover them — below the torn hem and past the sleeves.
        Part(_model, PrimitiveType.Cube, "Post", new Vector3(0f, 1.10f, 0f), new Vector3(0.10f, 2.20f, 0.10f),
             Quaternion.Euler(0f, 0f, 1.6f), timber);
        var crossbar = Joint(_model, "Crossbar", new Vector3(0f, 1.62f, 0f));
        crossbar.localRotation = Quaternion.Euler(1.5f, 0f, 3.2f);
        Part(crossbar, PrimitiveType.Cube, "Bar", Vector3.zero, new Vector3(1.46f, 0.085f, 0.085f),
             Quaternion.identity, timberDry);

        // The coat: sack-cloth over the crossbar, and the straw is bursting out of it.
        var coat = Joint(_model, "Coat", new Vector3(0f, 1.24f, 0f));
        coat.localRotation = Quaternion.Euler(0f, 0f, -2.4f);
        Part(coat, PrimitiveType.Cube, "Body", Vector3.zero, new Vector3(0.68f, 0.88f, 0.44f), Quaternion.identity, _bodyMat);
        Part(coat, PrimitiveType.Cube, "Belly", new Vector3(0f, -0.12f, 0.02f), new Vector3(0.58f, 0.34f, 0.42f), Quaternion.identity, coatDark);

        // The hem is torn — nine irregular flaps and the post showing between them, none of them level.
        for (int i = 0; i < 5; i++)
        {
            float x = Mathf.Lerp(-0.28f, 0.28f, i / 4f);
            Part(coat, PrimitiveType.Cube, "Tatter" + i, new Vector3(x, -0.53f - (i % 3) * 0.045f, 0.17f),
                 new Vector3(0.15f, 0.20f + (i % 2) * 0.07f, 0.05f),
                 Quaternion.Euler((i % 3) * 7f, 0f, (i % 2 == 0) ? 5f : -7f), coatDark);
            Part(coat, PrimitiveType.Cube, "TatterBack" + i, new Vector3(x * 0.8f, -0.51f - (i % 2) * 0.05f, -0.17f),
                 new Vector3(0.16f, 0.18f + (i % 3) * 0.05f, 0.05f),
                 Quaternion.Euler(0f, 0f, (i % 2 == 0) ? -6f : 6f), coatDark);
        }

        // Sleeves off the crossbar, ending in straw rather than hands.
        _sleeveL = Joint(coat, "SleeveL", new Vector3(-0.28f, 0.34f, 0f));
        _sleeveL.localRotation = Quaternion.Euler(0f, 0f, 24f);
        Part(_sleeveL, PrimitiveType.Cube, "Arm", new Vector3(-0.20f, 0f, 0f), new Vector3(0.44f, 0.19f, 0.21f), Quaternion.identity, _bodyMat);
        Part(_sleeveL, PrimitiveType.Cube, "Cuff", new Vector3(-0.42f, 0f, 0f), new Vector3(0.10f, 0.22f, 0.24f), Quaternion.identity, coatDark);
        StrawBurst(_sleeveL, new Vector3(-0.45f, 0f, 0f), 6, 0.26f, 40f, 6f);

        _sleeveR = Joint(coat, "SleeveR", new Vector3(0.28f, 0.34f, 0f));
        _sleeveR.localRotation = Quaternion.Euler(0f, 0f, -24f);
        Part(_sleeveR, PrimitiveType.Cube, "Arm", new Vector3(0.20f, 0f, 0f), new Vector3(0.44f, 0.19f, 0.21f), Quaternion.identity, _bodyMat);
        Part(_sleeveR, PrimitiveType.Cube, "Cuff", new Vector3(0.42f, 0f, 0f), new Vector3(0.10f, 0.22f, 0.24f), Quaternion.identity, coatDark);
        StrawBurst(_sleeveR, new Vector3(0.45f, 0f, 0f), 6, 0.26f, 40f, 6f);

        // Straw out of the neck, where the coat does not meet the sack.
        StrawBurst(coat, new Vector3(0f, 0.47f, 0f), 9, 0.30f, 72f, 4f);

        _mouthAnchor = new GameObject("MouthAnchor").transform;
        _mouthAnchor.SetParent(_jaw, false);
        _mouthAnchor.localPosition = new Vector3(0f, 0f, 0.35f);

        // ---- the head: a burlap sack, slightly loose, tilted wrong and staying wrong -----------------
        // This is a scarecrow that has decided to move; the wrongness is the scare.
        _head = Joint(_model, "Head", new Vector3(0f, 2.00f, 0f));
        _head.localRotation = Quaternion.Euler(4f, 11f, 15f);
        Part(_head, PrimitiveType.Cube, "Sack", Vector3.zero, new Vector3(0.38f, 0.44f, 0.35f), Quaternion.Euler(0f, 0f, -3f), sack);
        Part(_head, PrimitiveType.Cube, "SackCrown", new Vector3(0f, 0.20f, 0f), new Vector3(0.30f, 0.12f, 0.28f), Quaternion.Euler(2f, 7f, 6f), sackDark);
        Part(_head, PrimitiveType.Cube, "SackTie", new Vector3(0f, -0.23f, 0f), new Vector3(0.26f, 0.11f, 0.24f), Quaternion.Euler(0f, 0f, 8f), sackDark);

        // Hollow sockets — no eyeballs anywhere on this thing. The faint glow sits just IN FRONT of the
        // socket's face; the first build buried it behind the socket box, where nothing could see it.
        for (int s = 0; s < 2; s++)
        {
            float ex = s == 0 ? -0.105f : 0.105f;
            Part(_head, PrimitiveType.Cube, "Socket" + s, new Vector3(ex, 0.055f, 0.160f),
                 new Vector3(0.125f, 0.095f, 0.06f), Quaternion.identity, socketMat);
            Part(_head, PrimitiveType.Cube, "SocketGlow" + s, new Vector3(ex, 0.050f, 0.196f),
                 new Vector3(0.070f, 0.045f, 0.020f), Quaternion.identity, glowMat);
        }

        // The mouth is a stitched seam, not a grin: thirteen small dark stitches in an irregular line. The
        // first cut used nine large ones with a broad dark maw behind them and read as a toothy grin in the
        // close-up, which is the one thing it must not be.
        for (int i = 0; i < 13; i++)
        {
            float x = Mathf.Lerp(-0.115f, 0.115f, i / 12f);
            float y = -0.070f + ((i % 3) - 1) * 0.009f;
            Part(_head, PrimitiveType.Cube, "Stitch" + i, new Vector3(x, y, 0.178f),
                 new Vector3(0.016f, 0.026f, 0.016f), Quaternion.Euler(0f, 0f, (i % 2 == 0) ? 22f : -26f), stitch);
        }

        // The jaw is the seam itself: a flap of the sack that tears open when it feeds. The eat sequence
        // still drags the player into _mouthAnchor, which sits just inside it, so the fail is the cookie
        // being hauled up into a torn sack.
        _jaw = Joint(_head, "Jaw", new Vector3(0f, -0.10f, 0.06f));
        Part(_jaw, PrimitiveType.Cube, "Maw", new Vector3(0f, -0.01f, 0.05f), new Vector3(0.20f, 0.05f, 0.10f), Quaternion.identity, _mouthMat);

        // The rest poses the lurch swings ON TOP of, so the tilt and the sleeve angles are never lost.
        if (_head != null) _headRest = _head.localRotation;
        if (_sleeveL != null) _sleeveRestL = _sleeveL.localRotation;
        if (_sleeveR != null) _sleeveRestR = _sleeveR.localRotation;

        // What it actually measures, for the report — read off the built renderers, not off the plan.
        var bounds = new Bounds(transform.position, Vector3.zero);
        bool anyBounds = false;
        var renderers = _model.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            if (r == null) continue;
            if (!anyBounds) { bounds = r.bounds; anyBounds = true; }
            else bounds.Encapsulate(r.bounds);
        }
        if (anyBounds)
        {
            HeightMeters = bounds.max.y - transform.position.y;
            SilhouetteWidthMeters = bounds.size.x;
        }
        PartCount = renderers.Length;
    }

    /// <summary>A burst of dry straw — neck, cuffs, hem. Named, so the report can count it.</summary>
    void StrawBurst(Transform parent, Vector3 origin, int count, float length, float spread, float tilt)
    {
        for (int i = 0; i < count; i++)
        {
            float t = count <= 1 ? 0f : i / (float)(count - 1);
            float yaw = Mathf.Lerp(-spread, spread, t) + ((i % 3) - 1) * 9f;
            float pitch = tilt + Mathf.Lerp(8f, 52f, (i % 4) / 3f);
            var rot = Quaternion.Euler(pitch, yaw, Mathf.Lerp(-16f, 16f, ((i * 7) % 5) / 4f));
            var dir = rot * Vector3.up * (length * 0.5f);
            Part(parent, PrimitiveType.Cube, "Straw" + i, origin + dir,
                 new Vector3(0.030f, length, 0.030f), rot,
                 (i % 2 == 0) ? _strawMat : _strawPaleMat);
        }
    }

    /// <summary>§25.8: emissive material for the sockets. The only light this creature emits.</summary>
    static Material GlowMat(Color colour, float intensity)
    {
        var mat = Materials.Lit(colour, 0.2f);
        if (mat.HasProperty("_EmissionColor"))
        {
            mat.SetColor("_EmissionColor", colour * intensity);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        return mat;
    }

    void Update()
    {
        if (!_active)
        {
            if (Time.timeSinceLevelLoad < _spawnAt) return;
            if (_player != null && (_player.IsWon || _player.IsCaught)) return;
            if (_model != null) _model.gameObject.SetActive(true);
            _active = true;
            _repathTimer = 0f;
            Retarget();
        }

        if (Scattered)
        {
            if (Time.timeSinceLevelLoad >= _reformAt) Reform();
            return;
        }

        // ---- M23 §25.3: the stagger is the whole point of the throw -------------------------
        // No pathing, no movement for these 1.1 s: that is the ground the player buys. Nothing here
        // touches the player's own movement, so the flow law (§25.4) is not in play.
        if (StaggerLeft > 0f)
        {
            StaggerLeft -= Time.deltaTime;
            _flinch = 1f;
            ApplyFlinch();
            return;
        }
        if (_flinch > 0f)
        {
            _flinch = Mathf.Max(0f, _flinch - Time.deltaTime * 2.6f);
            ApplyFlinch();
        }

        if (_eating) return;
        if (_player == null || _player.IsWon || _player.IsCaught)
            return;

        _repathTimer -= Time.deltaTime;
        if (_repathTimer <= 0f)
        {
            _repathTimer = RepathSeconds;
            Retarget();
        }

        // ---- M28 (§25.8): the walk is a LURCH, not a glide ------------------------------------------
        // One surge per LurchSeconds, speed swinging LurchDepth either side of MoveSpeed. A full sine
        // averages exactly MoveSpeed, so the ground a thrown cob buys (§25.3, CornCob.GroundBought) is
        // unchanged: it covers the same distance in the same time and looks like it is hauling itself
        // along a post. This is the tell, not a speed change.
        _lurchPhase += Time.deltaTime / LurchSeconds * Mathf.PI * 2f;
        float surge = 1f + LurchDepth * Mathf.Sin(_lurchPhase);

        Vector3 target = _maze.CellToWorld(_goalCell.x, _goalCell.y);
        Vector3 pos = transform.position;
        Vector3 to = target - pos;
        to.y = 0f;
        if (to.sqrMagnitude > 0.04f)
        {
            Vector3 dir = to.normalized;
            _travel = dir;
            var look = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, TurnSpeed * Time.deltaTime);
            Vector3 next = pos + dir * (MoveSpeed * surge * Time.deltaTime);
            next = ConstrainToPath(next);
            next.y = pos.y;
            transform.position = next;
        }
        else
        {
            // Arrived at waypoint — repath immediately.
            Retarget();
        }

        // The pose: a hop that lands as the surge dies, a roll, and a head that swings a beat behind.
        if (_model != null && _flinch <= 0f && !_eating)
        {
            _model.localPosition = new Vector3(0f, Mathf.Max(0f, Mathf.Sin(_lurchPhase)) * LurchHop, 0f);
            _model.localRotation = Quaternion.Euler(Mathf.Cos(_lurchPhase) * 7f, 0f, Mathf.Sin(_lurchPhase) * LurchRoll);
        }
        if (_head != null && !_eating)
            _head.localRotation = _headRest * Quaternion.Euler(0f, 0f, Mathf.Sin(_lurchPhase - 0.9f) * 11f);
        if (_sleeveL != null && !_eating)
            _sleeveL.localRotation = _sleeveRestL * Quaternion.Euler(0f, 0f, Mathf.Sin(_lurchPhase + 1.6f) * 13f);
        if (_sleeveR != null && !_eating)
            _sleeveR.localRotation = _sleeveRestR * Quaternion.Euler(0f, 0f, Mathf.Sin(_lurchPhase - 1.6f) * 13f);

        // The stitched seam barely moves until it feeds.
        if (_jaw != null && !_eating)
            _jaw.localRotation = Quaternion.Euler(2f + Mathf.Sin(Time.time * 1.7f) * 2f, 0f, 0f);

        TryCatch();
    }

    /// <summary>A thrown cob lands: one third of the health, and the stagger that is the real prize.</summary>
    public void TakeHit(float damage, float staggerSeconds, Vector3 fromDirection)
    {
        if (Scattered || _eating) return;

        Health = Mathf.Max(0f, Health - damage);
        StaggerLeft = staggerSeconds;
        LastStaggerSeconds = staggerSeconds;
        HitsTaken++;
        _flinch = 1f;

        // Knocked back off the line of the throw, so the hit reads as a hit.
        if (fromDirection.sqrMagnitude > 0.0001f)
        {
            Vector3 push = fromDirection;
            push.y = 0f;
            if (push.sqrMagnitude > 0.0001f) transform.position += push.normalized * 0.22f;
        }

        if (Health <= 0f) Scatter();
    }

    void ApplyFlinch()
    {
        if (_model == null) return;
        // At _flinch = 0 this is the identity pose, so the flinch eases itself out with no reset path.
        _model.localRotation = Quaternion.Euler(-20f * _flinch, 0f, 15f * _flinch);
        _model.localPosition = new Vector3(0f, -0.06f * _flinch, 0f);
        if (_jaw != null) _jaw.localRotation = Quaternion.Euler(2f + 20f * _flinch, 0f, 0f);
    }

    /// <summary>Three hits' worth: scattered, not dead, and it comes back (see ReformSeconds).</summary>
    void Scatter()
    {
        Scattered = true;
        StaggerLeft = 0f;
        _reformAt = Time.timeSinceLevelLoad + ReformSeconds;
        if (_model != null) _model.gameObject.SetActive(false);
    }

    void Reform()
    {
        Scattered = false;
        Health = MaxHealth;
        HitsTaken = 0;
        _flinch = 0f;
        if (_model != null)
        {
            _model.gameObject.SetActive(true);
            _model.localRotation = Quaternion.identity;
        }
        Retarget();
    }

    void Retarget()
    {
        if (_maze == null || _player == null) return;
        var from = _maze.NearestPathCell(transform.position);
        var to = _maze.NearestPathCell(_player.transform.position);
        _goalCell = _maze.NextStepToward(from, to);
        if (_goalCell == from && from != to)
        {
            // Same cell but not on player yet — aim at player cell.
            _goalCell = to;
        }
    }

    void TryCatch()
    {
        if (_caughtPlayer || _player == null) return;
        var myCell = _maze.NearestPathCell(transform.position);
        var theirCell = _maze.NearestPathCell(_player.transform.position);
        Vector3 flat = _player.transform.position - transform.position;
        flat.y = 0f;
        bool sameCell = myCell == theirCell;
        bool close = flat.magnitude <= CatchDistance;
        if (!sameCell && !close) return;

        _caughtPlayer = true;
        _eating = true;
        StartCoroutine(EatPlayer());
    }

    System.Collections.IEnumerator EatPlayer()
    {
        // Mouth opens wide, then snaps shut as the cookie shrinks in.
        float t = 0f;
        while (t < 0.35f)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / 0.35f);
            if (_jaw != null)
                _jaw.localRotation = Quaternion.Euler(Mathf.Lerp(10f, 52f, u), 0f, 0f);
            yield return null;
        }

        if (_player != null)
            _player.BeginEaten(_mouthAnchor != null ? _mouthAnchor : transform);

        t = 0f;
        while (t < 0.55f)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / 0.55f);
            if (_jaw != null)
                _jaw.localRotation = Quaternion.Euler(Mathf.Lerp(52f, 4f, u * u), 0f, 0f);
            yield return null;
        }

        _eating = false;
    }

    Vector3 ConstrainToPath(Vector3 pos)
    {
        if (_maze == null) return pos;
        var cell = _maze.WorldToCell(pos);
        if (!_maze.IsPath(cell.x, cell.y))
        {
            cell = _maze.NearestPathCell(pos);
            var snap = _maze.CellToWorld(cell.x, cell.y);
            pos.x = snap.x;
            pos.z = snap.z;
            return pos;
        }

        var center = _maze.CellToWorld(cell.x, cell.y);
        bool east = _maze.IsPath(cell.x + 1, cell.y);
        bool west = _maze.IsPath(cell.x - 1, cell.y);
        bool north = _maze.IsPath(cell.x, cell.y + 1);
        bool south = _maze.IsPath(cell.x, cell.y - 1);
        bool ew = east || west;
        bool ns = north || south;
        float half = _maze.CellSize * 0.5f - 0.38f;

        bool alongX = Mathf.Abs(_travel.x) >= Mathf.Abs(_travel.z) && _travel.sqrMagnitude > 0.01f;
        if (ew && !ns) alongX = true;
        else if (ns && !ew) alongX = false;
        else if (_travel.sqrMagnitude <= 0.01f)
            alongX = Mathf.Abs(pos.x - center.x) >= Mathf.Abs(pos.z - center.z);

        if (alongX)
        {
            pos.z = center.z;
            if (!west) pos.x = Mathf.Max(pos.x, center.x - half);
            if (!east) pos.x = Mathf.Min(pos.x, center.x + half);
            pos.z = Mathf.Clamp(pos.z, center.z - LaneHalf, center.z + LaneHalf);
        }
        else
        {
            pos.x = center.x;
            if (!south) pos.z = Mathf.Max(pos.z, center.z - half);
            if (!north) pos.z = Mathf.Min(pos.z, center.z + half);
            pos.x = Mathf.Clamp(pos.x, center.x - LaneHalf, center.x + LaneHalf);
        }

        return pos;
    }

    static Transform Joint(Transform parent, string name, Vector3 localPos)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = localPos;
        return t;
    }

    static Transform Part(Transform parent, PrimitiveType type, string name, Vector3 localPos, Vector3 scale, Quaternion localRot, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        go.transform.localRotation = localRot;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        Object.Destroy(go.GetComponent<Collider>());
        return go.transform;
    }
}
