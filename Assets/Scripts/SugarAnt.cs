using UnityEngine;

/// <summary>
/// Sugar Ant — swarm enemy. Spawns in clusters of 5, pathfinds through wall cells (corn rows)
/// toward the player. Dies in 1 hit (HP = 1). Dough cost per bite: 5. Drops 1 coin each.
/// Appears from level 5+.
/// </summary>
public sealed class SugarAnt : MonoBehaviour
{
    public const float MoveSpeed = 3.10f;
    public const float BiteDistance = 0.5f;
    public const float BiteCooldown = 1.2f;
    public const int DoughCost = 5;
    public const int CoinDrop = 1;
    public const int MaxHealth = 1;
    public const float AntRadius = 0.15f;

    public int Health { get; private set; } = MaxHealth;

    FarmWalkerController _player;
    MazeData _maze;
    float _lastBiteTime = float.MinValue;
    bool _active;

    /// <summary>Spawn a cluster of 5 ants near the given start cell.</summary>
    public static void SpawnCluster(MazeData maze, Vector2Int startCell, Transform parent,
                                    FarmWalkerController player)
    {
        Vector3 centre = maze.CellToWorld(startCell.x, startCell.y);
        for (int i = 0; i < 5; i++)
        {
            float offsetX = Random.Range(-0.6f, 0.6f);
            float offsetZ = Random.Range(-0.6f, 0.6f);
            var go = new GameObject("SugarAnt_" + i);
            go.transform.SetParent(parent, false);
            go.transform.position = centre + new Vector3(offsetX, 0.05f, offsetZ);

            var ant = go.AddComponent<SugarAnt>();
            ant._player = player;
            ant._maze = maze;
            ant.BuildVisual();
            ant._active = true;
        }
    }

    void BuildVisual()
    {
        // Small dark sphere for the body
        var bodyMat = Materials.Lit(new Color(0.15f, 0.08f, 0.03f), 0.05f);
        var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        body.name = "Body";
        body.transform.SetParent(transform, false);
        body.transform.localPosition = Vector3.zero;
        body.transform.localScale = Vector3.one * (AntRadius * 2f);
        body.GetComponent<Renderer>().sharedMaterial = bodyMat;
        Destroy(body.GetComponent<Collider>());

        // Tiny legs (6 little cylinders)
        var legMat = Materials.Lit(new Color(0.12f, 0.06f, 0.02f), 0.03f);
        for (int i = 0; i < 6; i++)
        {
            float angle = i * 60f * Mathf.Deg2Rad;
            float lx = Mathf.Cos(angle) * 0.1f;
            float lz = Mathf.Sin(angle) * 0.1f;
            var leg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            leg.name = "Leg_" + i;
            leg.transform.SetParent(transform, false);
            leg.transform.localPosition = new Vector3(lx, -0.12f, lz);
            leg.transform.localScale = new Vector3(0.02f, 0.06f, 0.02f);
            leg.transform.localRotation = Quaternion.Euler(90f + (i % 2 == 0 ? 25f : -25f), 0f, 0f);
            leg.GetComponent<Renderer>().sharedMaterial = legMat;
            Destroy(leg.GetComponent<Collider>());
        }

        // Trigger for player contact
        var trigger = gameObject.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = BiteDistance;
        trigger.center = Vector3.zero;
    }

    void Update()
    {
        if (!_active || Health <= 0 || _player == null || _player.IsWon || _player.IsCaught)
            return;

        Vector3 toPlayer = _player.transform.position - transform.position;
        toPlayer.y = 0f;
        float dist = toPlayer.magnitude;

        // Move toward player through walls
        if (dist > BiteDistance * 0.8f)
        {
            Vector3 dir = toPlayer.normalized;
            transform.position += dir * (MoveSpeed * Time.deltaTime);
        }

        // Bite if close enough
        if (dist <= BiteDistance)
        {
            float now = Time.time;
            if (now - _lastBiteTime >= BiteCooldown && !_player.IsWon && !_player.IsCaught)
            {
                _lastBiteTime = now;
                _player.TakeSwipeDamage(DoughCost);
            }
        }
    }

    /// <summary>Take a hit. SugarAnts die in one hit.</summary>
    public bool TakeHit(int damage)
    {
        if (Health <= 0) return true;

        Health = Mathf.Max(0, Health - damage);
        if (Health <= 0)
        {
            Die();
            return true;
        }
        return false;
    }

    void Die()
    {
        CoinWallet.Instance.Add(CoinDrop);
        StartCoroutine(Burst());
    }

    System.Collections.IEnumerator Burst()
    {
        float t = 0f;
        float duration = 0.25f;
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