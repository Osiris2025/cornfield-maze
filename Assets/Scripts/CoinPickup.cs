using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A gold-rimmed sweet disc lying in the maze lanes — auto-collected on contact (FSD §10).
/// Placed off the solution route so they reward exploring dead ends.
/// </summary>
public sealed class CoinPickup : MonoBehaviour
{
    public int Value = 1;
    public System.Action<CoinPickup> OnCollected;

    float _bobPhase;
    const float BobAmplitude = 0.06f;
    const float BobSpeed = 1.8f;
    const float SpinSpeed = 60f;

    // ---- factory ---------------------------------------------------------------

    /// <summary>Builds one coin disc at <paramref name="position"/>.</summary>
    public static CoinPickup Create(Vector3 position, int value)
    {
        var root = new GameObject("Coin");
        root.transform.position = position;
        var coin = root.AddComponent<CoinPickup>();
        coin.Value = value;
        coin.BuildBody();
        return coin;
    }

    void BuildBody()
    {
        // Golden-yellow disc, metallic.
        var bodyMat = Materials.Lit(new Color(0.95f, 0.68f, 0.12f), 0.55f, 0.7f);
        var rimMat  = Materials.Lit(new Color(0.78f, 0.52f, 0.10f), 0.62f, 0.7f);

        // Disc body: 0.35 m diameter, 0.04 m thick. Unity cylinder is 2 units tall at scale 1,
        // so Y = half thickness = 0.02.
        var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        body.name = "CoinBody";
        body.transform.SetParent(transform, false);
        body.transform.localPosition = Vector3.zero;
        body.transform.localScale = new Vector3(0.35f, 0.02f, 0.35f);
        body.GetComponent<Renderer>().sharedMaterial = bodyMat;
        Destroy(body.GetComponent<Collider>());

        // Slightly darker outer rim.
        var rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rim.name = "CoinRim";
        rim.transform.SetParent(transform, false);
        rim.transform.localPosition = Vector3.zero;
        rim.transform.localScale = new Vector3(0.37f, 0.018f, 0.37f);
        rim.GetComponent<Renderer>().sharedMaterial = rimMat;
        Destroy(rim.GetComponent<Collider>());

        // Trigger: 0.45 m radius → 0.9 m pickup diameter (FSD §10).
        var trigger = gameObject.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 0.45f;

        _bobPhase = Random.Range(0f, Mathf.PI * 2f);
    }

    // ---- behaviour ------------------------------------------------------------

    void Update()
    {
        // Float and spin.
        float bob = Mathf.Sin(Time.time * BobSpeed + _bobPhase) * BobAmplitude;
        var p = transform.position;
        p.y = 0.03f + bob;
        transform.position = p;
        transform.Rotate(0f, SpinSpeed * Time.deltaTime, 0f);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") || other.GetComponentInParent<FarmWalkerController>() != null)
        {
            CoinWallet.Instance.Add(Value);
            OnCollected?.Invoke(this);
            Destroy(gameObject);
        }
    }

    // ---- maze planting --------------------------------------------------------

    /// <summary>
    /// Places <c>6 + levelNumber</c> coins on path cells that are NOT on the solution route
    /// (FSD §10). No two coins share a cell. Seeded RNG so the same level always lays the
    /// same coins.
    /// </summary>
    public static List<CoinPickup> PlantCoins(MazeData maze, Transform parent, int levelNumber)
    {
        var coins = new List<CoinPickup>();
        if (maze == null) return coins;

        int count = 6 + levelNumber;
        var route = CornCob.RouteToGold(maze);
        var onRoute = new HashSet<Vector2Int>(route);

        // Gather all path cells NOT on the solution route.
        var candidates = new List<Vector2Int>();
        for (int x = 0; x < maze.Width; x++)
        {
            for (int y = 0; y < maze.Height; y++)
            {
                if (!maze.IsPath(x, y)) continue;
                var cell = new Vector2Int(x, y);
                if (onRoute.Contains(cell)) continue;
                if (cell == maze.StartCell || cell == maze.GoldCell) continue;
                candidates.Add(cell);
            }
        }

        // Seeded RNG: same level, same coin layout every time.
        var rng = new System.Random(maze.Width * 31 + maze.Height * 7 + levelNumber * 13);
        int placed = 0;
        while (placed < count && candidates.Count > 0)
        {
            int idx = rng.Next(candidates.Count);
            var cell = candidates[idx];
            candidates.RemoveAt(idx);

            var centre = maze.CellToWorld(cell.x, cell.y);
            float ox = ((float)rng.NextDouble() - 0.5f) * 0.8f;
            float oz = ((float)rng.NextDouble() - 0.5f) * 0.8f;
            var pos = centre + new Vector3(ox, 0.03f, oz);
            var coin = Create(pos, 1);
            coin.transform.SetParent(parent, false);
            coins.Add(coin);
            placed++;
        }

        return coins;
    }
}