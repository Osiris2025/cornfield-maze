using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// M23 — pick up and throw the cob (Furrow + Dough, FSD §25.3).
///
/// Todd's ask: "objects can be picked up and thrown at any threats that do a small amount of damage
/// that in early mazes can help gingy get past the threat to move around it before being eaten."
///
/// That sentence is the design: the cob is NOT a weapon. It buys a walk-around, so its value is
/// measured in METRES OF GROUND AND SECONDS, never in damage. Three hits do add up to the Husk's
/// whole health, so a player who spends everything can scatter it for a while — that is the ceiling,
/// and it is deliberately reversible (see Husk.TakeHit).
///
/// A projectile needs physics and the tree had none: the player is a CharacterController and must
/// stay kinematic, so the cob carries its own kinematic-free Rigidbody and its own collider, and the
/// two are explicitly told to ignore each other in flight.
/// </summary>
public sealed class CornCob : MonoBehaviour
{
    // ---- the spec, as constants, so verification asserts against them and not against vibes ----
    public const float Mass = 0.25f;             // kg, a real dried cob is 0.15-0.30
    public const float ThrowSpeed = 14f;         // m/s
    public const float ThrowElevationDeg = 20f;  // degrees above the horizon, fixed: no aiming stance
    public const float GameGravity = 18f;        // the game's own gravity (FarmWalkerController.Gravity)
    public const float Length = 0.19f;           // m, 190 mm
    public const float Diameter = 0.045f;        // m, 45 mm across
    public const float PickupRadius = 1.2f;      // tap-to-pick, one in hand at a time
    public const float HitDamage = 6f;           // one third of the Husk's 18
    public const float HitStagger = 1.1f;        // the point of the whole mechanic
    /// <summary>
    /// The cob's collider switches on this long after release. At 14 m/s that is 0.7 m of travel, which
    /// puts it clear of the thrower's own CharacterController: §25.3 requires that the player stays
    /// kinematic and the two never interact through physics, and Physics.IgnoreCollision alone did not
    /// hold against the controller. A target inside 0.7 m is already eating you (catch distance 0.62 m),
    /// so nothing is lost.
    /// </summary>
    public const float ArmDelay = 0.05f;
    public const int RouteCellsPerCob = 6;       // ~1 cob per 6 route cells, never in a dead-end mouth

    /// <summary>v²·sin(2θ)/g — 6.999 m. Computed from the constants above, never typed in twice.</summary>
    public static float NominalRange =>
        ThrowSpeed * ThrowSpeed * Mathf.Sin(2f * ThrowElevationDeg * Mathf.Deg2Rad) / GameGravity;

    /// <summary>Ground bought by one hit: the chaser's speed for the length of the stagger. 2.585 m.</summary>
    public static float GroundBought => Husk.MoveSpeed * HitStagger;

    public enum CobState { Lane, Held, Flying, Landed }

    public CobState State { get; private set; } = CobState.Lane;
    public Transform Holder { get; private set; }

    /// <summary>Where the last throw started, and where it came down — the harness measures these.</summary>
    public Vector3 LastThrowOrigin { get; private set; }
    public Vector3 LastLandingPoint { get; private set; }
    public bool LandedFromThrow { get; private set; }
    /// <summary>Horizontal distance travelled at the instant the arc returned to release height. = the range.</summary>
    public float LastLevelRange { get; private set; }
    public Husk LastHit { get; private set; }

    /// <summary>What stopped the last throw. Empty while it is still flying; diagnostics only.</summary>
    public string FirstContact { get; private set; }

    /// <summary>Whether the player's own collider was successfully told to ignore this cob.</summary>
    public string PlayerIgnoreState { get; private set; }

    Rigidbody _rb;
    Collider _col;
    Vector3 _velocity;
    float _releaseHeight;
    float _armAt;
    bool _armed;

    // ------------------------------------------------------------------ construction

    /// <summary>Builds one dried cob: 190 mm long, 45 mm across, from primitives — no new art asset.</summary>
    public static CornCob Create(Transform parent, Vector3 position)
    {
        var root = new GameObject("CornCob");
        root.transform.SetParent(parent, false);
        root.transform.position = position;
        var cob = root.AddComponent<CornCob>();
        cob.BuildBody();
        return cob;
    }

    void BuildBody()
    {
        // Yellow-dent kernel colour, the same cob colour the corn geometry already uses.
        var kernel = Materials.Lit(new Color(0.93f, 0.74f, 0.14f), 0.32f);
        var husk = Materials.Lit(new Color(0.62f, 0.58f, 0.34f), 0.12f);

        // A tapered body with rounded tips. Unity's cylinder is 2 units tall at scale 1, so the
        // body is half the length and the tips finish it off.
        var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        body.name = "CobBody";
        body.transform.SetParent(transform, false);
        body.transform.localScale = new Vector3(Diameter, Length * 0.5f, Diameter);
        body.GetComponent<Renderer>().sharedMaterial = kernel;
        StripCollider(body);

        for (int i = 0; i < 2; i++)
        {
            var tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            tip.name = i == 0 ? "CobTip" : "CobBase";
            tip.transform.SetParent(transform, false);
            tip.transform.localPosition = new Vector3(0f, (i == 0 ? 1f : -1f) * Length * 0.5f, 0f);
            tip.transform.localScale = new Vector3(Diameter, Diameter * 0.7f, Diameter);
            tip.GetComponent<Renderer>().sharedMaterial = i == 0 ? kernel : husk;
            StripCollider(tip);
        }

        _col = gameObject.AddComponent<SphereCollider>();
        ((SphereCollider)_col).radius = Diameter * 0.62f;
        _rb = gameObject.AddComponent<Rigidbody>();
        _rb.mass = Mass;
        _rb.useGravity = false;   // the cob integrates the game's own gravity, see StepBallistic
        _rb.isKinematic = true;
        _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    /// <summary>
    /// Unity primitives arrive with their own colliders. A cob's body and tips are decoration: the one
    /// collider that matters is the SphereCollider on the root, which is the one the throw controls.
    /// Three extra colliders meant the cob bounced off its own tips and could clip the thrower.
    /// </summary>
    static void StripCollider(GameObject go)
    {
        var c = go.GetComponent<Collider>();
        if (c != null) Destroy(c);
    }

    // ------------------------------------------------------------------ pickup / hold / throw

    public bool CanBePickedUp => State == CobState.Lane || State == CobState.Landed;

    public void PickUp(Transform holder)
    {
        if (!CanBePickedUp) return;
        State = CobState.Held;
        Holder = holder;
        _rb.isKinematic = true;
        _rb.linearVelocity = Vector3.zero;
        _col.enabled = false;
        transform.SetParent(holder, false);
        transform.localPosition = new Vector3(0.34f, 1.19f, 0.22f);
        transform.localRotation = Quaternion.Euler(0f, 0f, 74f);
    }

    /// <summary>Puts a held cob back down where the player is standing (picking up a second drops the first).</summary>
    public void Drop(Vector3 groundPoint)
    {
        if (State == CobState.Held && Holder != null)
        {
            Holder = null;
            LayAt(groundPoint);
        }
    }

    public void Throw(Vector3 origin, Vector3 direction)
    {
        if (State != CobState.Held) return;

        transform.SetParent(null, true);
        transform.position = origin + direction.normalized * (Length * 0.5f + 0.05f);
        LastThrowOrigin = transform.position;
        _releaseHeight = transform.position.y;
        LandedFromThrow = false;
        LastLandingPoint = Vector3.zero;
        LastLevelRange = 0f;
        LastHit = null;

        // 14 m/s at 20° above the horizon, exactly. The direction arrives flat and gets the
        // elevation here, in one place, so no caller can throw at a different angle.
        Vector3 flat = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
        if (flat.sqrMagnitude < 0.0001f) flat = Vector3.forward;
        Vector3 aim = (flat * Mathf.Cos(ThrowElevationDeg * Mathf.Deg2Rad) +
                       Vector3.up * Mathf.Sin(ThrowElevationDeg * Mathf.Deg2Rad)).normalized;

        _velocity = aim * ThrowSpeed;
        _armed = false;
        _armAt = Time.fixedTime + ArmDelay;
        _col.enabled = false;
        _rb.isKinematic = false;
        transform.rotation = Quaternion.LookRotation(flat, Vector3.up);
        State = CobState.Flying;

        // The player is a CharacterController and must never be moved by physics. Tell Unity so.
        var player = Holder;
        Holder = null;
        FirstContact = null;
        PlayerIgnoreState = "no holder";
        if (player != null)
        {
            var cc = player.GetComponent<CharacterController>();
            if (cc != null)
            {
                Physics.IgnoreCollision(_col, cc);
                PlayerIgnoreState = "ignore=" + Physics.GetIgnoreCollision(_col, cc) +
                                    " myCollider=" + _col.enabled + " ccCollider=" + cc.enabled;
            }
            else PlayerIgnoreState = "holder has no CharacterController";
        }
    }

    // ------------------------------------------------------------------ flight

    void FixedUpdate()
    {
        if (State != CobState.Flying) return;

        StepBallistic(Time.fixedDeltaTime);

        if (!_armed && Time.fixedTime >= _armAt)
        {
            _armed = true;
            _col.enabled = true;
        }

        // The range we promise is the level-ground range: the distance at the moment the arc comes
        // back down to the height it was released from. A hand releases ~1.2 m up, so the cob then
        // falls a little further — both numbers are reported, the spec's 7.0 m is the first one.
        if (!LandedFromThrow && _velocity.y < 0f && transform.position.y <= _releaseHeight)
        {
            LandedFromThrow = true;
            Vector3 flat = transform.position - LastThrowOrigin;
            flat.y = 0f;
            LastLevelRange = flat.magnitude;
        }

        if (transform.position.y <= 0.02f) Land();
    }

    /// <summary>
    /// Velocity-Verlet with the game's own g. This is exact for a constant acceleration at ANY step
    /// size, so the measured range does not drift with the frame rate — which is what makes the
    /// 7.0 m claim checkable instead of a claim about one particular machine.
    /// </summary>
    void StepBallistic(float dt)
    {
        transform.position += (_velocity + Vector3.down * (0.5f * GameGravity * dt)) * dt;
        _velocity += Vector3.down * (GameGravity * dt);
    }

    void Land()
    {
        Vector3 p = transform.position;
        p.y = 0.03f;
        LastLandingPoint = p;
        LayAt(p);
    }

    void LayAt(Vector3 point)
    {
        State = CobState.Landed;
        transform.SetParent(null, true);
        transform.position = point;
        transform.rotation = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);
        _velocity = Vector3.zero;
        _rb.linearVelocity = Vector3.zero;
        _rb.isKinematic = true;
        _col.enabled = true;
        Invoke(nameof(Sleep), 0.4f);
    }

    void Sleep()
    {
        if (State == CobState.Landed) State = CobState.Lane;   // a spent cob is loot again, not rubbish
    }

    /// <summary>
    /// The Husk's hit volume is a trigger (it must never push the kinematic player about), so the cob
    /// registers the hit here rather than in OnCollisionEnter.
    /// </summary>
    void OnTriggerEnter(Collider other)
    {
        if (State != CobState.Flying) return;
        var husk = other.GetComponentInParent<Husk>();
        if (husk == null) return;
        if (FirstContact == null) FirstContact = "trigger: Husk";
        Strike(husk);
    }

    void Strike(Husk husk)
    {
        husk.TakeHit(HitDamage, HitStagger, _velocity.normalized);
        LastHit = husk;
        Land();
    }

    void OnCollisionEnter(Collision collision)
    {
        if (State != CobState.Flying) return;
        if (FirstContact == null)
            FirstContact = collision.collider.name + " (parent " +
                           (collision.collider.transform.parent != null ? collision.collider.transform.parent.name : "-") +
                           ", point " + collision.contacts[0].point.ToString("0.00") + ")";

        var husk = collision.collider.GetComponentInParent<Husk>();
        if (husk != null) Strike(husk);

        // §25.3 also has a cob cancel an Icing Wisp glob in flight. No Icing Wisp exists in the tree
        // yet (it is a later threat), so this is the hook, not a feature: when a glob appears with
        // "Glob" in its name the cob takes it out of the air.
        if (collision.collider.name.Contains("Glob"))
            Destroy(collision.collider.gameObject);

        // A cob that bounces off the corn comes to rest where it lands; it does not roll away, so
        // the finite ammo stays findable.
        Land();
    }

    // ------------------------------------------------------------------ lanes

    /// <summary>
    /// Lays cobs along the route to the gold: about one every <see cref="RouteCellsPerCob"/> route
    /// cells, and never in the mouth of a dead end — a cob must never be the bait that walks a player
    /// into a trap (§25.3, and §8's fairness law behind it).
    /// </summary>
    public static List<CornCob> PlantLanes(MazeData maze, Transform parent, int seed)
    {
        var cobs = new List<CornCob>();
        if (maze == null) return cobs;

        // Walk the solution route from the start.
        var route = new List<Vector2Int>();
        var cell = maze.StartCell;
        var seen = new HashSet<Vector2Int>();
        while (seen.Add(cell))
        {
            route.Add(cell);
            if (cell == maze.GoldCell) break;
            var next = maze.NextStepTowardGold(cell);
            if (next == cell) break;
            cell = next;
        }

        var rng = new System.Random(seed);
        int sinceLast = 0;
        for (int i = 1; i < route.Count - 1; i++)   // never on the start or the gold cell
        {
            sinceLast++;
            if (sinceLast < RouteCellsPerCob) continue;
            if (TouchesDeadEnd(maze, route, route[i])) continue;
            sinceLast = 0;

            var c = route[i];
            var centre = maze.CellToWorld(c.x, c.y);
            // Offset inside the lane so it reads as dropped, not placed.
            float ox = ((float)rng.NextDouble() - 0.5f) * 0.7f;
            float oz = ((float)rng.NextDouble() - 0.5f) * 0.7f;
            var pos = centre + new Vector3(ox, 0.03f, oz);
            cobs.Add(Create(parent, pos));
        }

        // A small maze can have every route cell next to a branch. The mechanic must still be
        // playable, so guarantee one cob halfway along the route.
        if (cobs.Count == 0 && route.Count > 2)
        {
            var c = route[route.Count / 2];
            cobs.Add(Create(parent, maze.CellToWorld(c.x, c.y) + new Vector3(0f, 0.03f, 0f)));
        }

        return cobs;
    }

    /// <summary>True if this cell is the only mouth of a one-cell dead end hanging off the route.</summary>
    static bool TouchesDeadEnd(MazeData maze, List<Vector2Int> route, Vector2Int cell)
    {
        var onRoute = new HashSet<Vector2Int>(route);
        var steps = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        foreach (var step in steps)
        {
            var n = cell + step;
            if (!maze.IsPath(n.x, n.y) || onRoute.Contains(n)) continue;
            int exits = 0;
            foreach (var s2 in steps)
                if (maze.IsPath(n.x + s2.x, n.y + s2.y)) exits++;
            if (exits == 1) return true;   // n is a dead end and this cell is its mouth
        }
        return false;
    }
}

/// <summary>
/// M23 — the player's hands: what is in range, what is held, and where the cob goes.
///
/// It lives beside the controller rather than inside it on purpose: FarmWalkerController's feel was
/// verified in M22 and the throw has no business editing those numbers. The hands read the same
/// MobileControls surface the controller does, and the touch control and the harness both drive it
/// through the public methods below.
/// </summary>
public sealed class CobHands : MonoBehaviour
{
    public static CobHands Instance { get; private set; }

    public CornCob Held { get; private set; }
    public CornCob InRange { get; private set; }
    public bool Aiming => Held != null;

    /// <summary>The ring shown on the lane where the cob would come down. Aim feedback, nothing more.</summary>
    public GameObject AimMarker { get; private set; }

    public const float AimMarkerRadius = 0.42f;

    Transform _player;
    FarmWalkerController _controller;
    MobileControls _controls;
    Transform _cobRoot;

    /// <summary>Harness hook, the same idea as FarmWalkerController.InjectInput.</summary>
    public bool InjectThrowNow { get; set; }
    public bool InjectPickUpNow { get; set; }

    public static CobHands Install(FarmWalkerController controller, MobileControls controls, Transform cobRoot)
    {
        var go = new GameObject("CobHands");
        go.transform.SetParent(controller.transform, false);
        var hands = go.AddComponent<CobHands>();
        hands._controller = controller;
        hands._player = controller.transform;
        hands._controls = controls;
        hands._cobRoot = cobRoot;
        hands.BuildAimMarker();
        Instance = hands;
        return hands;
    }

    void BuildAimMarker()
    {
        AimMarker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        AimMarker.name = "CobAimMarker";
        AimMarker.transform.localScale = new Vector3(AimMarkerRadius * 2f, 0.012f, AimMarkerRadius * 2f);
        AimMarker.GetComponent<Renderer>().sharedMaterial =
            Materials.Lit(new Color(0.92f, 0.74f, 0.18f), 0.10f);
        var c = AimMarker.GetComponent<Collider>();
        if (c != null) Destroy(c);
        AimMarker.SetActive(false);
    }

    /// <summary>The throw origin: roughly the hand, so the arc starts where the cob is.</summary>
    public Vector3 ThrowOrigin
    {
        get
        {
            Vector3 flat = FlatAim;
            return _player.position + Vector3.up * 1.19f + flat * 0.34f;
        }
    }

    /// <summary>Aim is the camera's flat forward: the look drag aims, and there is no aiming stance.</summary>
    public Vector3 FlatAim
    {
        get
        {
            var cam = _controller != null ? _controller.Camera : Camera.main;
            Vector3 flat = cam != null ? Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up) : Vector3.forward;
            if (flat.sqrMagnitude < 0.0001f) flat = Vector3.forward;
            return flat.normalized;
        }
    }

    void LateUpdate()
    {
        if (_player == null) return;

        CornCob nearest = null;
        float best = CornCob.PickupRadius * CornCob.PickupRadius;
        foreach (var cob in Object.FindObjectsByType<CornCob>(FindObjectsSortMode.None))
        {
            if (!cob.CanBePickedUp) continue;
            float d = (cob.transform.position - _player.position).sqrMagnitude;
            if (d < best) { best = d; nearest = cob; }
        }
        InRange = nearest;

        // Aim feedback: where the cob would land if thrown right now. The range is the analytic one,
        // so the marker does not lie about the physics.
        if (AimMarker != null)
        {
            if (Aiming)
            {
                AimMarker.SetActive(true);
                Vector3 flat = FlatAim;
                AimMarker.transform.position = ThrowOrigin + flat * CornCob.NominalRange;
                AimMarker.transform.position = new Vector3(AimMarker.transform.position.x, 0.02f,
                                                           AimMarker.transform.position.z);
                AimMarker.transform.rotation = Quaternion.identity;
            }
            else AimMarker.SetActive(false);
        }

        if (InjectPickUpNow) { InjectPickUpNow = false; TryPickUp(); }
        if (InjectThrowNow) { InjectThrowNow = false; TryThrow(); }

        // The Mac is the review surface (mission order): the touch control is mobile-only by the
        // existing design, so a desktop key drives the same two actions, or the throw cannot be
        // judged on the machine the frames are taken from. Gated on !ShouldShow so it can never
        // double-fire alongside the touch control on a phone.
        if (!MobileControls.ShouldShow && !MobileControls.Suppressed && Input.GetKeyDown(KeyCode.F))
            TryPickUpOrThrow();

        if (_controls == null) return;
        if (_controls.ThrowPressed) TryPickUpOrThrow();
    }

    /// <summary>The one control: pick up if something is in range, otherwise throw what is held.</summary>
    public void TryPickUpOrThrow()
    {
        if (Held == null) TryPickUp();
        else TryThrow();
    }

    public bool TryPickUp()
    {
        if (InRange == null) return false;
        // One in hand: picking up a second drops the first (§25.3).
        if (Held != null) Held.Drop(_player.position + FlatAim * -0.4f);
        Held = InRange;
        InRange = null;
        Held.PickUp(_player != null ? _player : transform);
        return true;
    }

    public bool TryThrow()
    {
        if (Held == null) return false;
        var cob = Held;
        Held = null;
        cob.Throw(ThrowOrigin, FlatAim);
        return true;
    }

    /// <summary>Called by the harness so it can throw at an exact bearing without a camera.</summary>
    public void ThrowAlong(Vector3 flatDirection)
    {
        if (Held == null) return;
        var cob = Held;
        Held = null;
        cob.Throw(ThrowOrigin, flatDirection);
    }
}
