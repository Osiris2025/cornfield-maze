using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

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

    // ---- M35: Todd's supplied scarecrow replaces the primitives ----------------------------------
    // He dropped a rigged GLB (24 bones, one 72-frame "Unsteady_Walk" clip) in ~/Downloads and said
    // "I have a better scary scarecrow — put him in the scene". It is imported to FBX beside its
    // albedo by scripts/m35_scarecrow_import.py, and it SUPERSEDES the M28 primitive build as the
    // creature's body. The primitives stay in this file as the fallback: if the model is missing from
    // the build the chaser still exists and still catches, which is the thing the level depends on.
    public const string ModelResourcePath = "Scarecrow/scarecrow";
    public const string ModelAlbedoPath = "Scarecrow/scarecrow_albedo";
    /// <summary>Folder the walk clips are loaded from — the same one the model lives in.</summary>
    public const string ModelClipFolder = "Scarecrow";
    /// <summary>Height the model is scaled to stand. It has to break the lane line (corn is 2.90-3.20 m).</summary>
    public const float ModelHeightMeters = 2.30f;

    /// <summary>True when the supplied mesh built the body rather than the primitives.</summary>
    public bool UsingModel { get; private set; }
    /// <summary>Triangles in the body as built — off the live renderers, not off the plan.</summary>
    public int ModelTriangles { get; private set; }
    /// <summary>The walk clip actually playing, or "" when the model's animation did not survive import.</summary>
    public string ModelClipName { get; private set; } = "";

    /// <summary>
    /// M35: how far INTO the maze the meeting is meant to happen. The Husk used to spawn a cell or two
    /// behind the start, so the opening move of every run was a creature already on the player's
    /// doorstep. Todd: "Gingy should be nearer the other end of the current path or he should encounter
    /// the scarecrow deeper in the maze". It now spawns this many cells along the route to the gold, so
    /// the first stretch is quiet and the encounter lands deep.
    /// </summary>
    public const int SpawnCellsDeeper = 9;

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
    PlayableGraph _walkGraph;         // M35: the supplied walk clip, played straight off the model

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

        // ---- M35: spawn DEEP on the route, not on the player's doorstep ------------------------------
        // Walk the gold route SpawnCellsDeeper cells in and stand there. This is the change Todd asked
        // for: the encounter happens deep in the maze instead of at the start. Everything else about the
        // chase is untouched — same speed, same repath, same catch range, so §25.3's balance holds and
        // the player still meets it on the way to the gold rather than being ambushed at the gate.
        var start = maze.StartCell;
        Vector2Int spawnCell = start;
        int walked = 0;
        var cursor = start;
        while (walked < SpawnCellsDeeper)
        {
            var next = maze.NextStepTowardGold(cursor);
            if (next == cursor || !maze.IsPath(next.x, next.y) || next == maze.GoldCell) break;
            cursor = next;
            walked++;
            spawnCell = cursor;
        }
        if (walked == 0)
        {
            // Nowhere to walk (the start already adjoins the gold): fall back to the old side-step.
            var goldStep = maze.NextStepTowardGold(start);
            var steps = new[]
            {
                new Vector2Int(0, -1), new Vector2Int(-1, 0), new Vector2Int(1, 0), new Vector2Int(0, 1)
            };
            foreach (var step in steps)
            {
                var c = start + step;
                if (!maze.IsPath(c.x, c.y) || c == goldStep) continue;
                spawnCell = c;
                break;
            }
        }
        Vector3 spawn = maze.CellToWorld(spawnCell.x, spawnCell.y);
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
        // ---- M35: the creature gets its body here ------------------------------------------------
        // The supplied mesh wins when it is present. The M28 primitives below are kept as the fallback
        // and are what runs if Resources has no Scarecrow — a missing art file must not delete the
        // chaser, because the level's threat is the thing the maze is built around.
        _model = new GameObject("ScarecrowMesh").transform;
        _model.SetParent(transform, false);
        if (TryBuildModel(_model))
            return;

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
        if (TryBounds(_model, out var bounds))
        {
            HeightMeters = bounds.max.y - transform.position.y;
            SilhouetteWidthMeters = bounds.size.x;
        }
        PartCount = _model.GetComponentsInChildren<Renderer>(true).Length;
    }

    /// <summary>
    /// Bounds of a built hierarchy, in world space, off whatever the parts actually ARE. A rigged FBX
    /// imports as a SkinnedMeshRenderer, and a skinned renderer's own bounds are only written when it
    /// is drawn — read while it is off-screen they come back as a point, which is how a 2.30 m creature
    /// first measured 0.00 m and would have been left unscaled. So: live bounds when they are real,
    /// the mesh's own AABB times the scale when they are not.
    /// </summary>
    static bool TryBounds(Transform root, out Bounds bounds)
    {
        bounds = new Bounds(root.position, Vector3.zero);
        bool any = false;
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (r == null) continue;
            var b = r.bounds;
            if (b.size.sqrMagnitude < 1e-6f && r is SkinnedMeshRenderer smr && smr.sharedMesh != null)
            {
                var m = smr.sharedMesh.bounds;
                var s = smr.transform.lossyScale;
                b = new Bounds(
                    smr.transform.TransformPoint(m.center),
                    new Vector3(m.size.x * Mathf.Abs(s.x), m.size.y * Mathf.Abs(s.y),
                                m.size.z * Mathf.Abs(s.z)));
            }
            if (!any) { bounds = b; any = true; }
            else bounds.Encapsulate(b);
        }
        return any;
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

    /// <summary>
    /// M35: build the body from Todd's supplied scarecrow, or return false so the primitives take over.
    ///
    /// Three things are handled here rather than in the asset, because they cannot be authored from a
    /// batch build and the glTF -> FBX hop does not carry them reliably:
    ///   * SCALE — measured off the model's own live renderers and divided into the stated height. The
    ///     conversion carries the armature's 0.01 scale, so a constant baked in here would be silently
    ///     wrong the day the importer's unit handling changes. Measure, do not assume.
    ///   * MATERIAL — one URP/Lit material with the supplied albedo bound from the PNG shipped beside
    ///     the FBX. The FBX's own material export is not what this project's night lighting is tuned for.
    ///   * ANIMATION — the 72-frame walk is played straight off the model with a Playables graph. An
    ///     AnimatorController asset is the other route and it cannot be authored headlessly; a clip
    ///     played directly needs none.
    /// </summary>
    bool TryBuildModel(Transform parent)
    {
        var prefab = Resources.Load<GameObject>(ModelResourcePath);
        if (prefab == null)
        {
            Debug.LogWarning("Husk: no model at Resources/" + ModelResourcePath +
                             " — building the M28 primitives instead");
            return false;
        }

        var inst = Instantiate(prefab, parent, false);
        inst.name = "ScarecrowModel";

        float native = MeasuredHeight(inst);
        if (native > 0.05f)
            inst.transform.localScale = Vector3.one * (ModelHeightMeters / native);

        var albedo = Resources.Load<Texture2D>(ModelAlbedoPath);
        var mat = Materials.Lit(Color.white, 0.05f);
        if (albedo != null) Materials.ApplyAlbedo(mat, albedo, 1f);

        // A rigged FBX comes in as a SkinnedMeshRenderer, not a MeshFilter — counting only MeshFilters
        // reported the creature as 0 tris while it was standing in the lane, drawn and animated.
        int tris = 0;
        var counted = new HashSet<Mesh>();
        foreach (var mf in inst.GetComponentsInChildren<MeshFilter>(true))
            if (mf.sharedMesh != null && counted.Add(mf.sharedMesh))
                tris += mf.sharedMesh.triangles.Length / 3;
        foreach (var smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            if (smr.sharedMesh != null && counted.Add(smr.sharedMesh))
                tris += smr.sharedMesh.triangles.Length / 3;
        foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            r.sharedMaterial = mat;

        var animator = inst.GetComponentInChildren<Animator>();
        if (animator == null) animator = inst.AddComponent<Animator>();
        animator.applyRootMotion = false;

        var clips = Resources.LoadAll<AnimationClip>(ModelClipFolder);
        if (clips != null && clips.Length > 0)
        {
            var clip = clips[0];
            _walkGraph = PlayableGraph.Create("HuskWalk");
            var play = AnimationClipPlayable.Create(_walkGraph, clip);
            AnimationPlayableOutput.Create(_walkGraph, "walk", animator).SetSourcePlayable(play);
            _walkGraph.Play();
            ModelClipName = clip.name;
            // Desync the phase: a chaser that steps in lock with the world's clock reads as clockwork.
            play.SetTime(Time.timeSinceLevelLoad % Mathf.Max(0.01f, clip.length));
        }
        else
        {
            Debug.LogWarning("Husk: model loaded but no AnimationClip under Resources/" + ModelClipFolder +
                             " — it will stand in its bind pose");
        }

        UsingModel = true;
        ModelTriangles = tris;
        Debug.Log("Husk: built from the supplied model — " + native.ToString("0.000") +
                  " units native, scaled x" + (ModelHeightMeters / Mathf.Max(0.05f, native)).ToString("0.000") +
                  ", " + tris + " tris, clip '" + ModelClipName + "'");
        return true;
    }

    /// <summary>World height of a built object off its own parts — not off its origin or its scale.</summary>
    static float MeasuredHeight(GameObject go)
    {
        return TryBounds(go.transform, out var b) ? b.size.y : 0f;
    }

    void OnDestroy()
    {
        if (_walkGraph.IsValid()) _walkGraph.Destroy();
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
