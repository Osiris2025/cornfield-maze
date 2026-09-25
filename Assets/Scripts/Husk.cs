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
    const float CatchDistance = 0.62f;
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
    float _bobPhase;
    bool _active;
    bool _eating;
    bool _caughtPlayer;
    float _reformAt;
    float _flinch;
    Material _bodyMat;
    Material _mouthMat;
    Material _crumbMat;

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
        _bodyMat = Materials.Lit(new Color(0.38f, 0.52f, 0.72f), 0.28f);
        _mouthMat = Materials.Lit(new Color(0.55f, 0.12f, 0.16f), 0.35f);
        _crumbMat = Materials.Lit(new Color(0.55f, 0.34f, 0.16f), 0.15f);
        var eyeWhite = Materials.Lit(new Color(0.92f, 0.93f, 0.90f), 0.4f);
        var pupil = Materials.Lit(new Color(0.08f, 0.08f, 0.10f), 0.1f);
        var tooth = Materials.Lit(new Color(0.95f, 0.93f, 0.85f), 0.45f);

        _model = new GameObject("HuskMesh").transform;
        _model.SetParent(transform, false);

        // Round goofy body — original design, not trademark blue-fur character.
        Part(_model, PrimitiveType.Sphere, "Body", new Vector3(0f, 0.55f, 0f), Vector3.one * 1.15f, Quaternion.identity, _bodyMat);
        Part(_model, PrimitiveType.Sphere, "Belly", new Vector3(0f, 0.42f, 0.28f), new Vector3(0.85f, 0.70f, 0.70f), Quaternion.identity,
            Materials.Lit(new Color(0.48f, 0.58f, 0.70f), 0.32f));

        // Big hungry mouth (separate jaw for eat anim).
        _jaw = Joint(_model, "Jaw", new Vector3(0f, 0.42f, 0.35f));
        Part(_jaw, PrimitiveType.Sphere, "Mouth", new Vector3(0f, -0.02f, 0.18f), new Vector3(0.72f, 0.42f, 0.55f), Quaternion.identity, _mouthMat);
        Part(_jaw, PrimitiveType.Cube, "ToothL", new Vector3(-0.16f, 0.12f, 0.28f), new Vector3(0.10f, 0.14f, 0.08f), Quaternion.identity, tooth);
        Part(_jaw, PrimitiveType.Cube, "ToothR", new Vector3(0.16f, 0.12f, 0.28f), new Vector3(0.10f, 0.14f, 0.08f), Quaternion.identity, tooth);

        _mouthAnchor = new GameObject("MouthAnchor").transform;
        _mouthAnchor.SetParent(_jaw, false);
        _mouthAnchor.localPosition = new Vector3(0f, 0f, 0.35f);

        // Expressive eyes — simple discs, not googly IP props.
        var eyeL = Part(_model, PrimitiveType.Sphere, "EyeL", new Vector3(-0.28f, 0.78f, 0.42f), Vector3.one * 0.28f, Quaternion.identity, eyeWhite);
        var eyeR = Part(_model, PrimitiveType.Sphere, "EyeR", new Vector3(0.28f, 0.78f, 0.42f), Vector3.one * 0.28f, Quaternion.identity, eyeWhite);
        Part(eyeL, PrimitiveType.Sphere, "PupilL", new Vector3(0f, -0.05f, 0.55f), Vector3.one * 0.45f, Quaternion.identity, pupil);
        Part(eyeR, PrimitiveType.Sphere, "PupilR", new Vector3(0f, -0.05f, 0.55f), Vector3.one * 0.45f, Quaternion.identity, pupil);

        // Cookie crumb freckles on the snout.
        for (int i = 0; i < 6; i++)
        {
            float ang = (float)i * 55f * Mathf.Deg2Rad;
            float r = 0.22f + (i % 3) * 0.06f;
            Part(_model, PrimitiveType.Sphere, "Crumb" + i,
                new Vector3(Mathf.Cos(ang) * r, 0.55f + (i % 2) * 0.08f, 0.55f + Mathf.Sin(ang) * 0.08f),
                Vector3.one * (0.06f + (i % 3) * 0.02f),
                Quaternion.identity, _crumbMat);
        }

        // Stubby feet along the path.
        Part(_model, PrimitiveType.Sphere, "FootL", new Vector3(-0.28f, 0.10f, 0.10f), new Vector3(0.28f, 0.16f, 0.34f), Quaternion.identity, _bodyMat);
        Part(_model, PrimitiveType.Sphere, "FootR", new Vector3(0.28f, 0.10f, 0.10f), new Vector3(0.28f, 0.16f, 0.34f), Quaternion.identity, _bodyMat);
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
            Vector3 next = pos + dir * (MoveSpeed * Time.deltaTime);
            next = ConstrainToPath(next);
            next.y = pos.y;
            transform.position = next;
        }
        else
        {
            // Arrived at waypoint — repath immediately.
            Retarget();
        }

        _bobPhase += Time.deltaTime * 5.5f;
        if (_model != null)
            _model.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(_bobPhase)) * 0.04f, 0f);

        // Idle jaw bob.
        if (_jaw != null && !_eating)
            _jaw.localRotation = Quaternion.Euler(8f + Mathf.Sin(Time.time * 3.2f) * 6f, 0f, 0f);

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
        if (_jaw != null) _jaw.localRotation = Quaternion.Euler(8f + 22f * _flinch, 0f, 0f);
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
