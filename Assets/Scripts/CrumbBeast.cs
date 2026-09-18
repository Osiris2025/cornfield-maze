using UnityEngine;

/// <summary>
/// Original hungry cookie-chasing beast (not Sesame Street IP).
/// Spawns after a short delay, pathfinds along maze lanes toward the gingerbread player,
/// and is slow enough that staying on the correct route keeps you safe.
/// Wrong turns / backtracking into it can get you eaten.
/// </summary>
public sealed class CrumbBeast : MonoBehaviour
{
    public const string DisplayName = "Crumb Beast";
    const float SpawnDelay = 5f;
    const float MoveSpeed = 2.35f;
    const float RepathSeconds = 0.85f;
    const float CatchDistance = 0.62f;
    const float LaneHalf = 0.42f;
    const float TurnSpeed = 7f;

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
    Material _bodyMat;
    Material _mouthMat;
    Material _crumbMat;

    public static CrumbBeast Spawn(MazeData maze, FarmWalkerController player)
    {
        var go = new GameObject("CrumbBeast");
        var beast = go.AddComponent<CrumbBeast>();
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

        _model = new GameObject("CrumbBeastMesh").transform;
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
