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

    /// <summary>Seconds of full-storm rain to reach near-full dissolve (atmospheric, not instant).</summary>
    public const float DissolveRainSeconds = 210f;

    const float LaneHalf = 0.42f;

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

        if (!MobileControls.ShouldShow && Input.GetKeyDown(KeyCode.Escape))
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
            _yaw += look.x * TouchLookSensitivity;
            _pitch = Mathf.Clamp(_pitch - look.y * TouchLookSensitivity, MinPitch, MaxPitch);
        }

        if (_won || _caught)
        {
            AnimateModel(0f, false);
            if (!_eating && _body != null && _body.enabled)
                ApplyGravityOnly();
            return;
        }

        float hx = Input.GetAxisRaw("Horizontal");
        float hz = Input.GetAxisRaw("Vertical");
        if (MobileControls.Instance != null)
        {
            hx = Mathf.Clamp(hx + MobileControls.Instance.Move.x, -1f, 1f);
            hz = Mathf.Clamp(hz + MobileControls.Instance.Move.y, -1f, 1f);
        }
        bool running = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)
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

        var dirs = new[] { Vector3.forward, Vector3.back, Vector3.right, Vector3.left };
        var steps = new[] { new Vector2Int(0, 1), new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(-1, 0) };

        int best = -1;
        int second = -1;
        float bestScore = -1f;
        float secondScore = -1f;
        for (int i = 0; i < 4; i++)
        {
            float score = Vector3.Dot(intent, dirs[i]);
            if (score > bestScore)
            {
                second = best;
                secondScore = bestScore;
                best = i;
                bestScore = score;
            }
            else if (score > secondScore)
            {
                second = i;
                secondScore = score;
            }
        }

        if (best >= 0 && bestScore >= 0.35f && CanStep(cell, steps[best]))
            return dirs[best];
        if (second >= 0 && secondScore >= 0.55f && CanStep(cell, steps[second]))
            return dirs[second];
        return Vector3.zero;
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

        _hips.localPosition = new Vector3(0f, 0.94f + bob, 0f);
        _hips.localRotation = Quaternion.Euler(
            windLean * 0.28f + soggy * 0.35f,
            swing * 6.5f * _walkBlend,
            -swing * 4.0f * _walkBlend + windLean * 0.45f);

        float breathe = Mathf.Sin(Time.time * 1.55f) * 0.012f * (1f - _walkBlend);
        if (_torso != null)
        {
            _torso.localPosition = new Vector3(0f, 0.10f + breathe, 0f);
            _torso.localRotation = Quaternion.Euler(2.2f + windLean * 0.12f + breathe * 8f + soggy * 0.4f, 0f, 0f);
        }

        float thigh = 32f * _walkBlend;
        float arm = 26f * _walkBlend;
        float knee = 38f * _walkBlend;
        float elbow = 14f * _walkBlend;

        SetLocalX(_legL, swing * thigh);
        SetLocalX(_legR, -swing * thigh);
        SetLocalX(_shinL, liftOpp * knee);
        SetLocalX(_shinR, lift * knee);

        SetLocalX(_armL, -swing * arm + 6f);
        SetLocalX(_armR, swing * arm + 6f);
        SetLocalX(_foreL, 8f + lift * elbow);
        SetLocalX(_foreR, 8f + liftOpp * elbow);
    }

    static void SetLocalX(Transform joint, float xDeg)
    {
        if (joint == null) return;
        joint.localRotation = Quaternion.Euler(xDeg, 0f, 0f);
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
    public static Transform Build(Transform parent, out FarmRig rig, out List<Material> mats, out List<bool> icingFlags)
    {
        rig = new FarmRig();
        mats = new List<Material>();
        icingFlags = new List<bool>();

        var model = new GameObject("GingerbreadMesh").transform;
        model.SetParent(parent, false);

        var dough = Track(Materials.Lit(new Color(0.55f, 0.32f, 0.16f), 0.22f), mats, icingFlags, false);
        var doughDark = Track(Materials.Lit(new Color(0.42f, 0.24f, 0.12f), 0.18f), mats, icingFlags, false);
        var icing = Track(Materials.Lit(new Color(0.96f, 0.96f, 0.94f), 0.35f), mats, icingFlags, true);
        var gumRed = Track(Materials.Lit(new Color(0.78f, 0.18f, 0.22f), 0.55f, 0.08f), mats, icingFlags, false);
        var gumGreen = Track(Materials.Lit(new Color(0.22f, 0.62f, 0.28f), 0.55f, 0.08f), mats, icingFlags, false);
        var gumYellow = Track(Materials.Lit(new Color(0.88f, 0.72f, 0.18f), 0.55f, 0.08f), mats, icingFlags, false);
        var smile = Track(Materials.Lit(new Color(0.92f, 0.90f, 0.88f), 0.40f), mats, icingFlags, true);

        var hips = Joint(model, "Hips", new Vector3(0f, 0.94f, 0f));
        rig.Hips = hips;

        Part(hips, PrimitiveType.Cube, "Pelvis", new Vector3(0f, -0.02f, 0f), new Vector3(0.38f, 0.18f, 0.22f), Quaternion.identity, dough);
        // Icing waist outline
        Part(hips, PrimitiveType.Cube, "WaistIcing", new Vector3(0f, 0.08f, 0.12f), new Vector3(0.34f, 0.04f, 0.03f), Quaternion.identity, icing);

        var torso = Joint(hips, "Torso", new Vector3(0f, 0.10f, 0f));
        rig.Torso = torso;
        Part(torso, PrimitiveType.Cube, "Body", new Vector3(0f, 0.28f, 0f), new Vector3(0.44f, 0.52f, 0.26f), Quaternion.identity, dough);
        Part(torso, PrimitiveType.Cube, "ChestIcing", new Vector3(0f, 0.30f, 0.135f), new Vector3(0.32f, 0.04f, 0.02f), Quaternion.identity, icing);
        Part(torso, PrimitiveType.Cube, "ChestIcing2", new Vector3(0f, 0.18f, 0.135f), new Vector3(0.28f, 0.035f, 0.02f), Quaternion.identity, icing);

        Part(torso, PrimitiveType.Sphere, "Button1", new Vector3(0f, 0.42f, 0.14f), Vector3.one * 0.09f, Quaternion.identity, gumRed);
        Part(torso, PrimitiveType.Sphere, "Button2", new Vector3(0f, 0.30f, 0.14f), Vector3.one * 0.09f, Quaternion.identity, gumGreen);
        Part(torso, PrimitiveType.Sphere, "Button3", new Vector3(0f, 0.18f, 0.14f), Vector3.one * 0.09f, Quaternion.identity, gumYellow);

        Part(torso, PrimitiveType.Cylinder, "Neck", new Vector3(0f, 0.56f, 0f), new Vector3(0.11f, 0.05f, 0.11f), Quaternion.identity, dough);
        var head = Part(torso, PrimitiveType.Sphere, "Head", new Vector3(0f, 0.76f, 0f), Vector3.one * 0.34f, Quaternion.identity, dough);
        Part(head, PrimitiveType.Sphere, "EyeL", new Vector3(-0.28f, 0.10f, 0.78f), new Vector3(0.16f, 0.16f, 0.10f), Quaternion.identity, doughDark);
        Part(head, PrimitiveType.Sphere, "EyeR", new Vector3(0.28f, 0.10f, 0.78f), new Vector3(0.16f, 0.16f, 0.10f), Quaternion.identity, doughDark);
        Part(head, PrimitiveType.Sphere, "EyeDotL", new Vector3(-0.28f, 0.10f, 0.88f), new Vector3(0.07f, 0.07f, 0.05f), Quaternion.identity, icing);
        Part(head, PrimitiveType.Sphere, "EyeDotR", new Vector3(0.28f, 0.10f, 0.88f), new Vector3(0.07f, 0.07f, 0.05f), Quaternion.identity, icing);
        // Icing smile
        Part(head, PrimitiveType.Cube, "Smile", new Vector3(0f, -0.22f, 0.82f), new Vector3(0.28f, 0.045f, 0.05f), Quaternion.Euler(0f, 0f, 0f), smile);
        Part(head, PrimitiveType.Cube, "SmileCurveL", new Vector3(-0.14f, -0.16f, 0.82f), new Vector3(0.06f, 0.08f, 0.04f), Quaternion.Euler(0f, 0f, 28f), smile);
        Part(head, PrimitiveType.Cube, "SmileCurveR", new Vector3(0.14f, -0.16f, 0.82f), new Vector3(0.06f, 0.08f, 0.04f), Quaternion.Euler(0f, 0f, -28f), smile);
        // Head icing squiggle
        Part(head, PrimitiveType.Cube, "HeadIcing", new Vector3(0f, 0.55f, 0.15f), new Vector3(0.55f, 0.06f, 0.08f), Quaternion.identity, icing);

        rig.ArmL = Limb(torso, "ArmL", new Vector3(-0.28f, 0.44f, 0f), dough, icing, -1f, out rig.ForeL);
        rig.ArmR = Limb(torso, "ArmR", new Vector3(0.28f, 0.44f, 0f), dough, icing, 1f, out rig.ForeR);

        rig.LegL = Leg(hips, "LegL", new Vector3(-0.11f, -0.04f, 0f), dough, icing, out rig.ShinL);
        rig.LegR = Leg(hips, "LegR", new Vector3(0.11f, -0.04f, 0f), dough, icing, out rig.ShinR);

        return model;
    }

    static Material Track(Material mat, List<Material> mats, List<bool> icingFlags, bool icing)
    {
        mats.Add(mat);
        icingFlags.Add(icing);
        return mat;
    }

    static Transform Limb(Transform parent, string name, Vector3 shoulder, Material dough, Material icing, float side, out Transform forearm)
    {
        var upper = Joint(parent, name, shoulder);
        Part(upper, PrimitiveType.Cube, "Upper", new Vector3(side * 0.02f, -0.14f, 0f), new Vector3(0.13f, 0.30f, 0.14f), Quaternion.identity, dough);
        Part(upper, PrimitiveType.Cube, "UpperIcing", new Vector3(side * 0.02f, -0.08f, 0.075f), new Vector3(0.10f, 0.03f, 0.02f), Quaternion.identity, icing);
        forearm = Joint(upper, name + "Fore", new Vector3(0f, -0.30f, 0f));
        Part(forearm, PrimitiveType.Cube, "Forearm", new Vector3(0f, -0.12f, 0f), new Vector3(0.11f, 0.24f, 0.12f), Quaternion.identity, dough);
        Part(forearm, PrimitiveType.Sphere, "Hand", new Vector3(0f, -0.26f, 0.01f), Vector3.one * 0.12f, Quaternion.identity, dough);
        Part(forearm, PrimitiveType.Cube, "HandIcing", new Vector3(0f, -0.26f, 0.07f), new Vector3(0.08f, 0.025f, 0.02f), Quaternion.identity, icing);
        return upper;
    }

    static Transform Leg(Transform parent, string name, Vector3 hip, Material dough, Material icing, out Transform shin)
    {
        var thigh = Joint(parent, name, hip);
        Part(thigh, PrimitiveType.Cube, "Thigh", new Vector3(0f, -0.22f, 0f), new Vector3(0.17f, 0.42f, 0.18f), Quaternion.identity, dough);
        shin = Joint(thigh, name + "Shin", new Vector3(0f, -0.44f, 0f));
        Part(shin, PrimitiveType.Cube, "Shin", new Vector3(0f, -0.20f, 0f), new Vector3(0.16f, 0.38f, 0.17f), Quaternion.identity, dough);
        Part(shin, PrimitiveType.Cube, "Foot", new Vector3(0f, -0.42f, 0.04f), new Vector3(0.16f, 0.10f, 0.24f), Quaternion.identity, dough);
        Part(shin, PrimitiveType.Cube, "FootIcing", new Vector3(0f, -0.38f, 0.14f), new Vector3(0.12f, 0.03f, 0.02f), Quaternion.identity, icing);
        return thigh;
    }

    static Transform Joint(Transform parent, string name, Vector3 localPos)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = localPos;
        t.localRotation = Quaternion.identity;
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
