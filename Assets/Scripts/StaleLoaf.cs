using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stale Loaf — a stationary blocker that body-checks anything entering its cell.
/// Appears from level 3+. Three cane hits to break (HP 102), drops 4 coins on death.
/// Dough cost on contact: 12.
/// </summary>
public sealed class StaleLoaf : MonoBehaviour
{
    public const float MaxHealth = 102f;
    public const float HitCooldown = 1.5f;
    public const int CoinDrop = 4;
    public const float DoughCost = 12f;
    public const float TriggerRadius = 1.0f;

    public float Health { get; private set; } = MaxHealth;

    float _lastHitTime = float.MinValue;
    FarmWalkerController _player;

    static readonly List<StaleLoaf> _all = new List<StaleLoaf>();

    public static IReadOnlyList<StaleLoaf> All => _all;

    /// <summary>Spawn a StaleLoaf at the given path cell.</summary>
    public static StaleLoaf Spawn(MazeData maze, Vector2Int cell, Transform parent)
    {
        var go = new GameObject("StaleLoaf");
        go.transform.SetParent(parent, false);
        go.transform.position = maze.CellToWorld(cell.x, cell.y);

        var loaf = go.AddComponent<StaleLoaf>();
        loaf.BuildVisual();
        return loaf;
    }

    void BuildVisual()
    {
        // Crust: slightly darker brown outer box
        var crustMat = Materials.Lit(new Color(0.45f, 0.30f, 0.18f), 0.06f);
        var crust = GameObject.CreatePrimitive(PrimitiveType.Cube);
        crust.name = "Crust";
        crust.transform.SetParent(transform, false);
        crust.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        crust.transform.localScale = new Vector3(1.5f, 1.2f, 0.8f);
        crust.GetComponent<Renderer>().sharedMaterial = crustMat;
        Destroy(crust.GetComponent<Collider>());

        // Body: lighter brown inner box
        var bodyMat = Materials.Lit(new Color(0.58f, 0.42f, 0.28f), 0.08f);
        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(transform, false);
        body.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        body.transform.localScale = new Vector3(1.3f, 1.0f, 0.65f);
        body.GetComponent<Renderer>().sharedMaterial = bodyMat;
        Destroy(body.GetComponent<Collider>());

        // Score line across the top
        var scoreMat = Materials.Lit(new Color(0.35f, 0.22f, 0.12f), 0.04f);
        var score = GameObject.CreatePrimitive(PrimitiveType.Cube);
        score.name = "Score";
        score.transform.SetParent(transform, false);
        score.transform.localPosition = new Vector3(0f, 1.21f, 0f);
        score.transform.localScale = new Vector3(1.2f, 0.04f, 0.05f);
        score.GetComponent<Renderer>().sharedMaterial = scoreMat;
        Destroy(score.GetComponent<Collider>());

        // Trigger volume
        var trigger = gameObject.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = TriggerRadius;
        trigger.center = new Vector3(0f, 0.6f, 0f);
    }

    void OnEnable()
    {
        _all.Add(this);
    }

    void OnDisable()
    {
        _all.Remove(this);
    }

    void OnTriggerStay(Collider other)
    {
        if (_player == null)
        {
            var fwc = other.GetComponent<FarmWalkerController>();
            if (fwc != null) _player = fwc;
        }

        if (_player == null || other.GetComponent<FarmWalkerController>() == null) return;
        if (Health <= 0f) return;

        float now = Time.time;
        if (now - _lastHitTime < HitCooldown) return;
        _lastHitTime = now;

        if (_player != null && !_player.IsWon && !_player.IsCaught)
        {
            _player.TakeBite(DoughCost, (_player.transform.position - transform.position).normalized);
        }
    }

    /// <summary>Take a cane hit. Returns true if the loaf is destroyed.</summary>
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
        StartCoroutine(Crumble());
    }

    System.Collections.IEnumerator Crumble()
    {
        // Disable the trigger so it can't hit the player while crumbling
        var trigger = GetComponent<SphereCollider>();
        if (trigger != null) trigger.enabled = false;

        float t = 0f;
        float duration = 0.5f;
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