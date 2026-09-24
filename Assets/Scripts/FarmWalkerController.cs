using System.Collections.Generic;
using UnityEngine;

public sealed class FarmWalkerController : MonoBehaviour
{
    public float WalkSpeed = 4.4f;
    public float RunSpeed = 7.4f;
    public float Gravity = 18f;
    public float TurnSpeed = 12f;
    public float MouseSensitivity = 2.1f;
    public float TouchLookSensitivity = 0.14f;
    public float CameraDistance = 5.2f;
    public float CameraHeight = 2.1f;
    // Pitch: negative = camera low / look toward zenith; positive = overhead / look down.
    public float MinPitch = -87f;
    public float MaxPitch = 48f;

    /// <summary>
    /// M21 (§25.1): held by the front end — the title, help, introduction and pause screens. The
    /// camera still tracks and the player may still look around the field; only the body, the walk
    /// cycle and the movement input stop. Freezing through this flag rather than disabling the
    /// controller is what keeps the field alive behind the title screen.
    /// </summary>
    public bool Frozen;

    // ---- M22 (§25.2) diagnostics hook -------------------------------------------------------
    /// <summary>When set, the controller reads this instead of the keyboard and the touch stick, so
    /// M22FeelSelfTest can measure the REAL movement path rather than a re-implementation of it.</summary>
    public bool InjectInput;
    public Vector2 InjectedMove;
    public bool InjectedRun;

    /// <summary>The corridor axis chosen this frame — which axis the lane bound applies to.</summary>
    Vector3 _alongDir;

    /// <summary>The maze this walker is in (exposed for measurement).</summary>
    public MazeData Maze => _maze;

    /// <summary>Seconds of full-storm rain to reach near-full dissolve (atmospheric, not instant).</summary>
    public const float DissolveRainSeconds = 210f;

    /// <summary>
    /// M22 (§25.2): the lane half-width. A BOUND, not a rail — the player may stand anywhere across
    /// the lane. Public so the movement self-test measures against this number instead of a copy of it.
    /// </summary>
    public const float LaneHalf = 0.42f;

    /// <summary>Distance over which the lane bound eases the player to a stop, so a sideways push
    /// slows into the edge rather than hitting an invisible wall.</summary>
    const float LaneFeather = 0.08f;

    MazeData _maze;
    CharacterController _body;
    Transform _model;
    Transform _camRig;
    Camera _camera;
    Transform _hips;
    Transform _torso;
    Transform _armL;
    Transform _armR;
    Transform _foreL;
    Transform _foreR;
    Transform _legL;
    Transform _legR;
    Transform _shinL;
    Transform _shinR;
    float _yaw;
    float _pitch = 22f;
    float _vertical;
    float _walkPhase;
    float _walkBlend;
    Vector3 _travel;
    bool _won;
    bool _caught;
    bool _eating;
    float _rainExposure;
    float _dissolve;
    List<Material> _cookieMats;
    List<Color> _cookieBaseCols;
    List<bool> _icingFlags;
    // The FBX rig carries a real rest pose (its bones are NOT identity at rest), so the walk is
    // authored as a swing composed ONTO each joint's rest rotation, about the character's own
    // sideways axis. Writing an absolute rotation would snap the skeleton out of its pose.
    Transform[] _joints;
    Quaternion[] _jointRest;
    Vector3[] _jointSwingAxis;
    Vector3[] _jointYawAxis;
    Transform _crumb;

    public float DissolveAmount => _dissolve;
    public bool IsCaught => _caught;
    public bool IsWon => _won;

    public static FarmWalkerController Spawn(Vector3 position, float facingYaw, MazeData maze)
    {
        var root = new GameObject("GingerbreadWalker");
        root.tag = "Player";
        root.transform.position = position + Vector3.up * 0.08f;
        root.transform.rotation = Quaternion.Euler(0f, facingYaw, 0f);

        var controller = root.AddComponent<CharacterController>();
        controller.center = new Vector3(0f, 0.90f, 0f);
        controller.height = 1.80f;
        controller.radius = 0.30f;
        controller.stepOffset = 0.08f;
        controller.slopeLimit = 40f;
        controller.minMoveDistance = 0f;

        var player = root.AddComponent<FarmWalkerController>();
        player._body = controller;
        player._yaw = facingYaw;
        player._maze = maze;
        player._model = GingerbreadMesh.Build(root.transform, out var rig, out var mats, out var icing);
        player._cookieMats = mats;
        player._icingFlags = icing;
        player._cookieBaseCols = new List<Color>(mats.Count);
        for (int i = 0; i < mats.Count; i++)
            player._cookieBaseCols.Add(ReadColor(mats[i]));
        player._hips = rig.Hips;
        player._torso = rig.Torso;
        player._armL = rig.ArmL;
        player._armR = rig.ArmR;
        player._foreL = rig.ForeL;
        player._foreR = rig.ForeR;
        player._legL = rig.LegL;
        player._legR = rig.LegR;
        player._shinL = rig.ShinL;
        player._shinR = rig.ShinR;
        player.CaptureJointRestPose();
        player.CreateCamera();
        return player;
    }

    void CreateCamera()
    {
        foreach (var existing in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            Destroy(existing.gameObject);

        var camGo = new GameObject("FollowCamera");
        _camera = camGo.AddComponent<Camera>();
        _camera.nearClipPlane = 0.12f;
        _camera.farClipPlane = 180f;
        _camera.fieldOfView = 62f;
        camGo.tag = "MainCamera";
        camGo.AddComponent<AudioListener>();

        var urpType = System.Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
        if (urpType != null && camGo.GetComponent(urpType) == null)
            camGo.AddComponent(urpType);

        _camRig = new GameObject("CameraRig").transform;
        _camRig.SetParent(transform, false);
        camGo.transform.SetParent(_camRig, false);
        if (MobileControls.ShouldShow)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    public void SetWon(bool won) => _won = won;

    public void BeginEaten(Transform beastMouth)
    {
        if (_caught || _won) return;
        _caught = true;
        _eating = true;
        if (_body != null) _body.enabled = false;
        StartCoroutine(EatSequence(beastMouth));
    }

    System.Collections.IEnumerator EatSequence(Transform beastMouth)
    {
        float t = 0f;
        Vector3 start = transform.position;
        Vector3 startScale = _model != null ? _model.localScale : Vector3.one;
        while (t < 0.85f)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / 0.85f);
            float ease = u * u;
            if (beastMouth != null)
            {
                Vector3 target = beastMouth.position;
                transform.position = Vector3.Lerp(start, target, ease);
            }
            if (_model != null)
                _model.localScale = Vector3.Lerp(startScale, startScale * 0.08f, ease);
            ApplyDissolve(Mathf.Lerp(_dissolve, 1f, ease));
            yield return null;
        }

        if (_model != null)
            _model.gameObject.SetActive(false);

        // Tiny leftover crumb so the fail is readable, not a soft-lock vanish.
        if (_crumb == null)
        {
            var crumbGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            crumbGo.name = "CookieCrumb";
            crumbGo.transform.SetParent(transform, false);
            crumbGo.transform.localPosition = new Vector3(0f, 0.08f, 0f);
            crumbGo.transform.localScale = Vector3.one * 0.12f;
            crumbGo.GetComponent<Renderer>().sharedMaterial =
                Materials.Lit(new Color(0.42f, 0.24f, 0.12f), 0.08f);
            Object.Destroy(crumbGo.GetComponent<Collider>());
            _crumb = crumbGo.transform;
        }

        _eating = false;
        var hud = Object.FindFirstObjectByType<GameHud>();
        if (hud != null) hud.ShowCaught();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Update()
    {
        UpdateDissolveFromRain();

        // M21 (§25.1): while the run is live, Esc belongs to the front end (it opens the pause menu).
        if (!GameFrontEnd.IsPlaying && !MobileControls.ShouldShow && Input.GetKeyDown(KeyCode.Escape))
        {
            bool locked = Cursor.lockState == CursorLockMode.Locked;
            Cursor.lockState = locked ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = locked;
        }

        if (!MobileControls.ShouldShow && Cursor.lockState == CursorLockMode.Locked)
        {
            _yaw += Input.GetAxis("Mouse X") * MouseSensitivity;
            _pitch = Mathf.Clamp(_pitch - Input.GetAxis("Mouse Y") * MouseSensitivity, MinPitch, MaxPitch);
        }

        if (MobileControls.Instance != null)
        {
            var look = MobileControls.Instance.LookDelta;
            // M22 (§25.2): the sensitivity is exposed (it was a private-feeling constant) and the
            // invert-Y toggle is honoured. The default is unchanged at 0.14 deg/pt.
            float sens = MobileControls.Instance.LookSensitivity > 0f ? MobileControls.Instance.LookSensitivity : TouchLookSensitivity;
            float invert = MobileControls.Instance.InvertY ? -1f : 1f;
            _yaw += look.x * sens;
            _pitch = Mathf.Clamp(_pitch - look.y * sens * invert, MinPitch, MaxPitch);
        }

        if (_won || _caught)
        {
            AnimateModel(0f, false);
            if (!_eating && _body != null && _body.enabled)
                ApplyGravityOnly();
            return;
        }

        if (Frozen)
        {
            // The front end is up. The look block above has already run, so the camera keeps tracking
            // and the player can still look around; the body and the walk cycle are what stop.
            AnimateModel(0f, false);
            if (_body != null && _body.enabled) ApplyGravityOnly();
            return;
        }

        float hx = Input.GetAxisRaw("Horizontal");
        float hz = Input.GetAxisRaw("Vertical");
        if (InjectInput)
        {
            hx = InjectedMove.x;
            hz = InjectedMove.y;
        }
        if (MobileControls.Instance != null)
        {
            hx = Mathf.Clamp(hx + MobileControls.Instance.Move.x, -1f, 1f);
            hz = Mathf.Clamp(hz + MobileControls.Instance.Move.y, -1f, 1f);
        }
        bool running = InjectedRun || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)
            || (MobileControls.Instance != null && MobileControls.Instance.Running);
        float speed = running ? RunSpeed : WalkSpeed;
        // Soften when very soggy, but still able to finish if the player is quick.
        speed *= Mathf.Lerp(1f, 0.82f, Mathf.Clamp01((_dissolve - 0.55f) / 0.45f));

        var move = CorridorMove(hx, hz);
        if (move.sqrMagnitude > 0.01f)
        {
            _travel = move;
            var target = Quaternion.LookRotation(move, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, TurnSpeed * Time.deltaTime);
        }

        if (_body.isGrounded) _vertical = -1f;
        else _vertical -= Gravity * Time.deltaTime;

        Vector3 gust = PathGust(move);
        Vector3 before = transform.position;
        Vector3 intended = before + (move * speed + gust) * Time.deltaTime;
        Vector3 constrained = ConstrainToPath(intended);
        Vector3 horiz = constrained - before;
        horiz.y = 0f;
        _body.Move(horiz + Vector3.up * _vertical * Time.deltaTime);

        var after = transform.position;
        var onPath = ConstrainToPath(after);
        if (Mathf.Abs(onPath.x - after.x) > 0.0008f || Mathf.Abs(onPath.z - after.z) > 0.0008f)
        {
            _body.enabled = false;
            transform.position = new Vector3(onPath.x, after.y, onPath.z);
            _body.enabled = true;
        }

        AnimateModel(move.magnitude * speed, running);
    }

    void UpdateDissolveFromRain()
    {
        if (_eating || _caught) return;
        float storm = StormWeather.Intensity;
        if (storm > 0.02f)
            _rainExposure += storm * Time.deltaTime;

        float target = Mathf.Clamp01(_rainExposure / DissolveRainSeconds);
        // Ease so early rain only softens icing; late storm soaks the cookie.
        float eased = target * target * (3f - 2f * target);
        _dissolve = Mathf.MoveTowards(_dissolve, eased, Time.deltaTime * 0.35f);
        ApplyDissolve(_dissolve);
    }

    void ApplyDissolve(float amount)
    {
        amount = Mathf.Clamp01(amount);
        if (_model != null)
        {
            float squash = Mathf.Lerp(1f, 0.78f, amount);
            float sink = Mathf.Lerp(1f, 0.88f, amount);
            _model.localScale = new Vector3(squash * 1.04f, sink, squash * 1.04f);
        }

        if (_cookieMats == null) return;
        for (int i = 0; i < _cookieMats.Count; i++)
        {
            var mat = _cookieMats[i];
            if (mat == null) continue;
            Color baseCol = _cookieBaseCols[i];
            bool icing = _icingFlags != null && i < _icingFlags.Count && _icingFlags[i];

            Color soggy = icing
                ? Color.Lerp(baseCol, new Color(0.72f, 0.74f, 0.76f, baseCol.a), amount * 0.85f)
                : Color.Lerp(baseCol, new Color(0.28f, 0.16f, 0.08f, baseCol.a), amount * 0.55f);

            // Icing washes off first; body fades later but leaves a crumb of opacity.
            float alpha = icing
                ? Mathf.Lerp(1f, 0.05f, Mathf.Clamp01(amount * 1.35f))
                : Mathf.Lerp(1f, 0.22f, amount);
            soggy.a = alpha;
            WriteColor(mat, soggy);
            SetTransparent(mat, alpha < 0.98f);
            if (mat.HasProperty("_Smoothness"))
                mat.SetFloat("_Smoothness", Mathf.Lerp(0.18f, 0.55f, amount));
            if (mat.HasProperty("_Glossiness"))
                mat.SetFloat("_Glossiness", Mathf.Lerp(0.18f, 0.55f, amount));
        }
    }

    static Color ReadColor(Material mat)
    {
        if (mat.HasProperty("_BaseColor")) return mat.GetColor("_BaseColor");
        if (mat.HasProperty("_Color")) return mat.GetColor("_Color");
        return Color.white;
    }

    static void WriteColor(Material mat, Color c)
    {
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
    }

    static void SetTransparent(Material mat, bool on)
    {
        if (!on) return;
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
        if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = 3000;
    }

    void LateUpdate()
    {
        if (_camRig == null || _camera == null) return;

        float lookUp = Mathf.Clamp01((-_pitch) / 87f);
        float pivotY = 1.15f + lookUp * 0.95f;
        _camRig.position = transform.position + Vector3.up * pivotY;

        float lean = StormWeather.GustPush * 1.6f;
        float roll = Mathf.Sin(Time.time * 1.35f) * lean;
        float nod = Mathf.Sin(Time.time * 0.82f + 0.7f) * lean * 0.35f;
        _camRig.rotation = Quaternion.Euler(_pitch + nod, _yaw, roll);

        float boom = Mathf.Lerp(CameraDistance, CameraDistance * 0.62f, lookUp);
        float heightOff = (CameraHeight - 1.15f) + lookUp * 0.55f;
        var desired = _camRig.position - _camRig.forward * boom + Vector3.up * heightOff;
        float castDist = Vector3.Distance(_camRig.position, desired);
        if (castDist > 0.05f
            && Physics.SphereCast(_camRig.position, 0.18f, desired - _camRig.position, out var hit, castDist, ~0, QueryTriggerInteraction.Ignore))
        {
            desired = hit.point + hit.normal * 0.22f;
            float floorY = transform.position.y + 0.55f + lookUp * 0.85f;
            if (desired.y < floorY)
                desired.y = floorY;
        }

        _camera.transform.position = desired;
        float lookAtY = 1.40f + lookUp * 1.15f;
        _camera.transform.LookAt(transform.position + Vector3.up * lookAtY);
    }

    Vector3 CorridorMove(float hx, float hz)
    {
        if (_maze == null) return Vector3.zero;
        var intent = Quaternion.Euler(0f, _yaw, 0f) * new Vector3(hx, 0f, hz);
        if (intent.sqrMagnitude < 0.01f) return Vector3.zero;
        intent.Normalize();

        var cell = _maze.WorldToCell(transform.position);
        if (!_maze.IsPath(cell.x, cell.y))
            cell = _maze.NearestPathCell(transform.position);

        var centre = _maze.CellToWorld(cell.x, cell.y);
        var dirs = new[] { Vector3.forward, Vector3.back, Vector3.right, Vector3.left };
        var steps = new[] { new Vector2Int(0, 1), new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(-1, 0) };

        // M22 (§25.2): the corridor is still the rail — we pick the best direction that is actually
        // OPEN — but a push that no corridor answers is no longer thrown away. It becomes a sideways
        // step WITHIN the lane. Before this, a deliberate sideways push scored ~0 against every
        // corridor direction, CanStep refused the wall, and the move came back Vector3.zero: the
        // player could not step across their own lane at all.
        int best = -1;
        float bestScore = 0.35f;
        for (int i = 0; i < 4; i++)
        {
            if (!CanStep(cell, steps[i])) continue;
            float score = Vector3.Dot(intent, dirs[i]);
            if (score > bestScore) { bestScore = score; best = i; }
        }

        // Full analog along the corridor: the along component keeps the stick's magnitude, so a
        // half-pushed stick is half speed. (It used to return a unit vector — full speed at 0.35.)
        var along = best >= 0 ? dirs[best] * bestScore : Vector3.zero;

        // Whatever the corridor did not take is the player's own lateral intent, held inside the lane.
        var residual = intent - along;
        residual.y = 0f;
        var lateral = Vector3.zero;
        float lateralAmount = residual.magnitude;
        if (lateralAmount > 0.001f)
        {
            var lateralDir = residual / lateralAmount;
            var offset = transform.position - centre;
            offset.y = 0f;
            // How much lane is left in the direction the player is pushing? The bound eases them to a
            // stop at the edge rather than overruling the push.
            float room = LaneHalf - Mathf.Max(0f, Vector3.Dot(offset, lateralDir));
            lateral = lateralDir * (lateralAmount * Mathf.Clamp01(room / LaneFeather));
        }

        var move = along + lateral;
        if (along.sqrMagnitude > 0.0001f) _alongDir = along.normalized;
        return move;
    }

    bool CanStep(Vector2Int cell, Vector2Int step)
    {
        if (_maze == null) return false;
        var next = new Vector2Int(cell.x + step.x, cell.y + step.y);
        if (_maze.IsPath(next.x, next.y)) return true;

        var center = _maze.CellToWorld(cell.x, cell.y);
        var pos = transform.position;
        if (step.x != 0)
            return Mathf.Abs(pos.x - center.x) > 0.12f && Mathf.Sign(center.x - pos.x) == Mathf.Sign((float)step.x);
        return Mathf.Abs(pos.z - center.z) > 0.12f && Mathf.Sign(center.z - pos.z) == Mathf.Sign((float)step.y);
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

        bool alongX = _alongDir.sqrMagnitude > 0.01f
            ? Mathf.Abs(_alongDir.x) >= Mathf.Abs(_alongDir.z)
            : (Mathf.Abs(_travel.x) >= Mathf.Abs(_travel.z) && _travel.sqrMagnitude > 0.01f);
        if (ew && !ns) alongX = true;
        else if (ns && !ew) alongX = false;
        else if (_travel.sqrMagnitude <= 0.01f && _alongDir.sqrMagnitude <= 0.01f)
            alongX = Mathf.Abs(pos.x - center.x) >= Mathf.Abs(pos.z - center.z);

        // M22 (§25.2): the lane is a BOUND, not a rail. The old code snapped the player onto the
        // centreline (pos.z = center.z / pos.x = center.x) and only then clamped to the lane — that is
        // the auto-centring that fought the player's own input. Now the perpendicular axis is clamped
        // at the lane edge and nothing more, so a deliberate sideways push decides where in the lane
        // the player stands. The corridor walls are still held by the colliders on the corn blocks.
        if (alongX)
        {
            if (!west) pos.x = Mathf.Max(pos.x, center.x - half);
            if (!east) pos.x = Mathf.Min(pos.x, center.x + half);
            pos.z = Mathf.Clamp(pos.z, center.z - LaneHalf, center.z + LaneHalf);
        }
        else
        {
            if (!south) pos.z = Mathf.Max(pos.z, center.z - half);
            if (!north) pos.z = Mathf.Min(pos.z, center.z + half);
            pos.x = Mathf.Clamp(pos.x, center.x - LaneHalf, center.x + LaneHalf);
        }

        return pos;
    }

    Vector3 PathGust(Vector3 move)
    {
        float push = StormWeather.GustPush;
        if (push < 0.02f || _maze == null) return Vector3.zero;
        var wind = StormWeather.WindDir;
        wind.y = 0f;
        if (wind.sqrMagnitude < 0.01f) return Vector3.zero;
        wind.Normalize();

        Vector3 axis = move.sqrMagnitude > 0.01f
            ? move
            : (Mathf.Abs(_travel.x) >= Mathf.Abs(_travel.z) ? new Vector3(Mathf.Sign(_travel.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(_travel.z)));
        if (axis.sqrMagnitude < 0.01f)
        {
            var cell = _maze.WorldToCell(transform.position);
            bool ew = _maze.IsPath(cell.x + 1, cell.y) || _maze.IsPath(cell.x - 1, cell.y);
            bool ns = _maze.IsPath(cell.x, cell.y + 1) || _maze.IsPath(cell.x, cell.y - 1);
            axis = ew && !ns ? Vector3.right : Vector3.forward;
        }

        float along = Vector3.Dot(wind, axis);
        return axis * (along * push * 0.55f);
    }

    void ApplyGravityOnly()
    {
        if (_body.isGrounded) _vertical = -1f;
        else _vertical -= Gravity * Time.deltaTime;
        var p = transform.position;
        var locked = ConstrainToPath(p);
        Vector3 delta = new Vector3(locked.x - p.x, _vertical * Time.deltaTime, locked.z - p.z);
        _body.Move(delta);
    }

    void AnimateModel(float speed, bool running)
    {
        if (_model == null || _hips == null) return;

        bool stepping = speed > 0.15f && _body.isGrounded && !_caught;
        float blendGoal = stepping ? 1f : 0f;
        _walkBlend = Mathf.MoveTowards(_walkBlend, blendGoal, Time.deltaTime * (stepping ? 7f : 6f));

        if (stepping)
            _walkPhase += Time.deltaTime * (running ? 12.5f : 8.4f);

        float swing = Mathf.Sin(_walkPhase);
        float lift = Mathf.Max(0f, Mathf.Sin(_walkPhase));
        float liftOpp = Mathf.Max(0f, -Mathf.Sin(_walkPhase));
        float bob = Mathf.Abs(swing) * 0.055f * _walkBlend;
        float windLean = StormWeather.GustPush * (stepping ? 4.2f : 3.4f);
        float soggy = _dissolve * 6f;

        if (_model != null)
            _model.localPosition = new Vector3(0f, bob * 0.35f - _dissolve * 0.04f, 0f);

        float breathe = Mathf.Sin(Time.time * 1.55f) * 0.012f * (1f - _walkBlend);
        float thigh = 32f * _walkBlend;
        float arm = 26f * _walkBlend;
        float knee = 38f * _walkBlend;
        float elbow = 14f * _walkBlend;

        // Rotations only, composed onto the rest pose. No bone localPosition is ever written: the
        // FBX's rest positions ARE the skeleton.
        Pose(0, windLean * 0.28f + soggy * 0.35f, swing * 6.5f * _walkBlend);      // hips: pitch + sway
        Pose(1, 2.2f + windLean * 0.12f + breathe * 8f + soggy * 0.4f, 0f);        // torso
        Pose(2, -swing * arm + 6f, 0f);                                             // upper arm L
        Pose(3, swing * arm + 6f, 0f);                                              // upper arm R
        Pose(4, 8f + lift * elbow, 0f);                                             // forearm L
        Pose(5, 8f + liftOpp * elbow, 0f);                                          // forearm R
        Pose(6, swing * thigh, 0f);                                                // leg L (thigh)
        Pose(7, -swing * thigh, 0f);                                               // leg R (thigh)
        Pose(8, liftOpp * knee, 0f);                                               // knee L
        Pose(9, lift * knee, 0f);                                                  // knee R
    }

    void CaptureJointRestPose()
    {
        _joints = new[] { _hips, _torso, _armL, _armR, _foreL, _foreR, _legL, _legR, _shinL, _shinR };
        _jointRest = new Quaternion[_joints.Length];
        _jointSwingAxis = new Vector3[_joints.Length];
        _jointYawAxis = new Vector3[_joints.Length];

        var modelRight = _model != null ? _model.right : Vector3.right;
        var modelUp = _model != null ? _model.up : Vector3.up;
        for (int i = 0; i < _joints.Length; i++)
        {
            var joint = _joints[i];
            if (joint == null)
            {
                _jointRest[i] = Quaternion.identity;
                _jointSwingAxis[i] = Vector3.right;
                _jointYawAxis[i] = Vector3.up;
                continue;
            }

            _jointRest[i] = joint.localRotation;
            var parent = joint.parent;
            // The swing axis, expressed in the joint's PARENT frame, because localRotation is
            // relative to the parent. This keeps the swing on the character's sideways axis whatever
            // baked splay the rig's bones carry.
            _jointSwingAxis[i] = parent != null ? parent.InverseTransformDirection(modelRight).normalized : Vector3.right;
            _jointYawAxis[i] = parent != null ? parent.InverseTransformDirection(modelUp).normalized : Vector3.up;
        }
        Debug.Log("FarmWalkerController: captured the cookie's rest pose for " + _joints.Length + " joints.");
    }

    void Pose(int index, float pitchDeg, float yawDeg)
    {
        if (_joints == null || index < 0 || index >= _joints.Length) return;
        var joint = _joints[index];
        if (joint == null) return;
        var rot = Quaternion.AngleAxis(pitchDeg, _jointSwingAxis[index]);
        if (yawDeg != 0f)
            rot = Quaternion.AngleAxis(yawDeg, _jointYawAxis[index]) * rot;
        joint.localRotation = rot * _jointRest[index];
    }
}

public struct FarmRig
{
    public Transform Hips;
    public Transform Torso;
    public Transform ArmL;
    public Transform ArmR;
    public Transform ForeL;
    public Transform ForeR;
    public Transform LegL;
    public Transform LegR;
    public Transform ShinL;
    public Transform ShinR;
}

/// <summary>Stylized gingerbread cookie person — brown dough, white icing, gumdrop buttons.</summary>
public static class GingerbreadMesh
{
    /// <summary>
    /// The model is 5.3897 units tall at scale 1 against a 1.80 player capsule, so it is scaled
    /// down deliberately: 1.80 / 5.3897 = 0.334. Stated here so the number is not a mystery.
    /// </summary>
    public const float CookieScale = 0.335f;

    const string ModelResource = "GingerbreadMan/gb_man";
    const string DoughResource = "GingerbreadMan/mat_cookie_dough";
    const string IcingResource = "GingerbreadMan/mat_cookie_icing";
    const string IcingMeshToken = "decoration";

    public static Transform Build(Transform parent, out FarmRig rig, out List<Material> mats, out List<bool> icingFlags)
    {
        rig = new FarmRig();
        mats = new List<Material>();
        icingFlags = new List<bool>();

        var model = new GameObject("GingerbreadMesh").transform;
        model.SetParent(parent, false);
        model.localPosition = Vector3.zero;
        model.localRotation = Quaternion.identity;
        // Scale 1 on the wrapper: ApplyDissolve squashes this transform, so the cookie's size lives
        // on the instantiated model below and the squash stays relative.
        model.localScale = Vector3.one;

        var prefab = Resources.Load<GameObject>(ModelResource);
        if (prefab == null)
        {
            Debug.LogError("GingerbreadMesh: the supplied cookie model is missing from Resources ('" +
                           ModelResource + "'). There is no primitive fallback — the primitive cookie is " +
                           "the asset Todd rejected.");
            return model;
        }

        var instance = Object.Instantiate(prefab, model, false);
        instance.name = "Cookie";
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one * CookieScale;

        var doughSrc = Resources.Load<Material>(DoughResource);
        var icingSrc = Resources.Load<Material>(IcingResource);

        foreach (var smr in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            // The icing is its OWN mesh (gb_man_decoration) — that is exactly what lets the rain
            // dissolve wash the icing off FIRST. One material instance per mesh so the tint and the
            // fade stay independent, and the loaded assets are never mutated.
            bool isIcing = smr.name.Contains(IcingMeshToken);
            var src = isIcing ? icingSrc : doughSrc;
            if (src == null)
            {
                Debug.LogError("GingerbreadMesh: material asset missing for '" + smr.name + "' (" +
                               (isIcing ? IcingResource : DoughResource) + ") — the cookie would render untextured.");
                continue;
            }

            var mat = Object.Instantiate(src);
            mat.name = (isIcing ? "cookie_icing_" : "cookie_dough_") + smr.name;
            smr.sharedMaterial = mat;
            smr.updateWhenOffscreen = true;
            mats.Add(mat);
            icingFlags.Add(isIcing);
        }

        // The FarmRig contract, mapped onto the FBX bones. Legs read waist > hip > chin > foot in the
        // file; by geometry hip.* is the top of the leg, chin.* is the knee and foot.* the ankle.
        rig.Hips = Bone(instance.transform, "spine01");
        rig.Torso = Bone(instance.transform, "spine02");
        rig.ArmL = Bone(instance.transform, "upper_arm.L");
        rig.ArmR = Bone(instance.transform, "upper_arm.R");
        rig.ForeL = Bone(instance.transform, "forearm.L");
        rig.ForeR = Bone(instance.transform, "forearm.R");
        rig.LegL = Bone(instance.transform, "hip.L");
        rig.LegR = Bone(instance.transform, "hip.R");
        rig.ShinL = Bone(instance.transform, "chin.L");
        rig.ShinR = Bone(instance.transform, "chin.R");

        ReportJointMap(rig, mats, icingFlags);
        return model;
    }

    static Transform Bone(Transform root, string boneName)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == boneName) return t;
        Debug.LogError("GingerbreadMesh: bone '" + boneName + "' is not in the cookie rig.");
        return null;
    }

    static void ReportJointMap(FarmRig rig, List<Material> mats, List<bool> icingFlags)
    {
        Debug.Log("GINGERBREAD-JOINTS: Hips=" + Name(rig.Hips) + " Torso=" + Name(rig.Torso) +
                  " ArmL=" + Name(rig.ArmL) + " ArmR=" + Name(rig.ArmR) +
                  " ForeL=" + Name(rig.ForeL) + " ForeR=" + Name(rig.ForeR) +
                  " LegL=" + Name(rig.LegL) + " LegR=" + Name(rig.LegR) +
                  " ShinL=" + Name(rig.ShinL) + " ShinR=" + Name(rig.ShinR) +
                  " | meshes=" + mats.Count + " icingFlags=[" + string.Join(",", icingFlags.ToArray()) + "]");
    }

    static string Name(Transform t) => t == null ? "<missing>" : t.name;
}


