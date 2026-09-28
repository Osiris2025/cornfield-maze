using UnityEngine;

/// <summary>
/// Icing Wisp — ranged enemy. Holds a lane 6-10 cells from the player, drifts slowly,
/// and spits damaging globs down the corridor. Dies in 2 cane hits (HP 68).
/// Dough cost on glob hit: 8. Drops 4 coins on death. Appears from level 4+.
/// </summary>
public sealed class IcingWisp : MonoBehaviour
{
    public const float MaxHealth = 68f;
    public const float DriftSpeed = 1.20f;
    public const float FireInterval = 2.2f;
    public const int CoinDrop = 4;
    public const int DoughCost = 8;
    public const float WispRadius = 0.35f;
    public const float InnerGlowRadius = 0.15f;
    public const float MinDistanceCells = 6f;
    public const float MaxDistanceCells = 10f;

    // Glob projectile constants
    public const float GlobSpeed = 6f;
    public const float GlobMaxRange = 10f;
    public const float GlobRadius = 0.12f;

    public float Health { get; private set; } = MaxHealth;

    FarmWalkerController _player;
    MazeData _maze;
    float _fireTimer;
    Vector2Int _currentCell;

    /// <summary>
    /// Spawn an IcingWisp on a path cell 6-10 cells from the player.
    /// Falls back to a path cell near the gold if no cell in range is found.
    /// </summary>
    public static IcingWisp Spawn(MazeData maze, FarmWalkerController player, Transform parent)
    {
        var playerCell = maze.NearestPathCell(player.transform.position);
        var spawnCell = FindSpawnCell(maze, playerCell);

        var go = new GameObject("IcingWisp");
        go.transform.SetParent(parent, false);
        go.transform.position = maze.CellToWorld(spawnCell.x, spawnCell.y) + Vector3.up * 1.5f;

        var wisp = go.AddComponent<IcingWisp>();
        wisp._player = player;
        wisp._maze = maze;
        wisp._currentCell = spawnCell;
        wisp._fireTimer = Random.Range(0.5f, FireInterval);
        wisp.BuildVisual();
        return wisp;
    }

    static Vector2Int FindSpawnCell(MazeData maze, Vector2Int playerCell)
    {
        var candidates = new System.Collections.Generic.List<(Vector2Int cell, float dist)>();

        for (int x = 0; x < maze.Width; x++)
        {
            for (int y = 0; y < maze.Height; y++)
            {
                if (!maze.IsPath(x, y)) continue;
                float dx = x - playerCell.x;
                float dy = y - playerCell.y;
                float cellDist = Mathf.Sqrt(dx * dx + dy * dy);
                if (cellDist >= MinDistanceCells && cellDist <= MaxDistanceCells)
                {
                    candidates.Add((new Vector2Int(x, y), cellDist));
                }
            }
        }

        if (candidates.Count > 0)
        {
            // Pick a random cell from candidates
            int idx = Random.Range(0, candidates.Count);
            return candidates[idx].cell;
        }

        // Fallback: a path cell near the gold
        var goldCell = maze.GoldCell;
        var dirs = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        foreach (var dir in dirs)
        {
            var c = goldCell + dir;
            if (maze.IsPath(c.x, c.y)) return c;
        }
        return goldCell;
    }

    void BuildVisual()
    {
        // Outer translucent sphere
        var outerMat = Materials.Lit(new Color(0.55f, 0.75f, 1.0f), 0.3f);
        if (outerMat.HasProperty("_EmissionColor"))
        {
            outerMat.SetColor("_EmissionColor", new Color(0.2f, 0.4f, 0.9f) * 1.2f);
            outerMat.EnableKeyword("_EMISSION");
            outerMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        var outer = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        outer.name = "OuterGlow";
        outer.transform.SetParent(transform, false);
        outer.transform.localPosition = Vector3.zero;
        outer.transform.localScale = Vector3.one * (WispRadius * 2f);
        outer.GetComponent<Renderer>().sharedMaterial = outerMat;
        Destroy(outer.GetComponent<Collider>());

        // Inner bright core
        var innerMat = Materials.Lit(new Color(0.8f, 0.9f, 1.0f), 0.5f);
        if (innerMat.HasProperty("_EmissionColor"))
        {
            innerMat.SetColor("_EmissionColor", Color.white * 2.5f);
            innerMat.EnableKeyword("_EMISSION");
            innerMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        var inner = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        inner.name = "InnerCore";
        inner.transform.SetParent(transform, false);
        inner.transform.localPosition = Vector3.zero;
        inner.transform.localScale = Vector3.one * (InnerGlowRadius * 2f);
        inner.GetComponent<Renderer>().sharedMaterial = innerMat;
        Destroy(inner.GetComponent<Collider>());
    }

    void Update()
    {
        if (_player == null || _player.IsWon || _player.IsCaught || Health <= 0f)
            return;

        // Drift slowly toward the player along path cells
        var playerCell = _maze.NearestPathCell(_player.transform.position);
        if (playerCell != _currentCell)
        {
            var next = _maze.NextStepToward(_currentCell, playerCell);
            if (next != _currentCell)
            {
                _currentCell = next;
            }
        }

        Vector3 target = _maze.CellToWorld(_currentCell.x, _currentCell.y) + Vector3.up * 1.5f;
        Vector3 to = target - transform.position;
        to.y = 0f;
        if (to.magnitude > 0.05f)
        {
            transform.position = Vector3.MoveTowards(transform.position, target, DriftSpeed * Time.deltaTime);
        }

        // Floating bob
        float bob = Mathf.Sin(Time.time * 2.1f) * 0.12f;
        transform.position += Vector3.up * bob * Time.deltaTime * 2f;

        // Fire globs
        _fireTimer -= Time.deltaTime;
        if (_fireTimer <= 0f)
        {
            _fireTimer = FireInterval;
            FireGlob();
        }
    }

    void FireGlob()
    {
        if (_player == null) return;

        Vector3 toPlayer = _player.transform.position - transform.position;
        toPlayer.y = 0f;
        Vector3 dir = toPlayer.normalized;

        var globGo = new GameObject("IcingGlob");
        globGo.transform.position = transform.position + dir * 0.5f;

        var globMat = Materials.Lit(new Color(0.6f, 0.8f, 1.0f), 0.4f);
        if (globMat.HasProperty("_EmissionColor"))
        {
            globMat.SetColor("_EmissionColor", new Color(0.3f, 0.5f, 1.0f) * 1.5f);
            globMat.EnableKeyword("_EMISSION");
            globMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "GlobMesh";
        sphere.transform.SetParent(globGo.transform, false);
        sphere.transform.localPosition = Vector3.zero;
        sphere.transform.localScale = Vector3.one * (GlobRadius * 2f);
        sphere.GetComponent<Renderer>().sharedMaterial = globMat;
        Destroy(sphere.GetComponent<Collider>());

        var glob = globGo.AddComponent<IcingGlob>();
        glob.Init(_player, dir, GlobSpeed, GlobMaxRange, DoughCost);
    }

    /// <summary>Take a hit. Two hits to kill.</summary>
    public bool TakeHit(int damage)
    {
        if (Health <= 0f) return true;

        Health = Mathf.Max(0f, Health - damage);
        if (Health <= 0f)
        {
            Die();
            return true;
        }
        return false;
    }

    void Die()
    {
        CoinWallet.Instance.Add(CoinDrop);
        StartCoroutine(Fizzle());
    }

    System.Collections.IEnumerator Fizzle()
    {
        float t = 0f;
        float duration = 0.4f;
        Vector3 startScale = transform.localScale;
        while (t < duration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);
            transform.localScale = startScale * (1f - u);
            yield return null;
        }
        Destroy(gameObject);
    }
}

/// <summary>
/// Glob projectile fired by IcingWisp. Moves in a straight line, damages player on contact.
/// </summary>
internal sealed class IcingGlob : MonoBehaviour
{
    FarmWalkerController _player;
    Vector3 _direction;
    float _speed;
    float _maxRange;
    int _doughCost;
    Vector3 _origin;
    bool _hit;

    public void Init(FarmWalkerController player, Vector3 direction, float speed, float maxRange, int doughCost)
    {
        _player = player;
        _direction = direction.normalized;
        _speed = speed;
        _maxRange = maxRange;
        _doughCost = doughCost;
        _origin = transform.position;
    }

    void Update()
    {
        if (_hit || _player == null)
        {
            Destroy(gameObject);
            return;
        }

        transform.position += _direction * (_speed * Time.deltaTime);

        // Check distance traveled
        if (Vector3.Distance(transform.position, _origin) > _maxRange)
        {
            Destroy(gameObject);
            return;
        }

        // Check player hit
        Vector3 toPlayer = _player.transform.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.magnitude < 0.6f && !_player.IsWon && !_player.IsCaught)
        {
            _hit = true;
            _player.TakeSwipeDamage(_doughCost);
            Destroy(gameObject);
        }
    }
}