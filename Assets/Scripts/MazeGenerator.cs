using System.Collections.Generic;
using UnityEngine;

public sealed class MazeData
{
    public bool[,] IsWall;
    public int Width;
    public int Height;
    public float CellSize;
    public Vector2Int StartCell;
    public Vector2Int GoldCell;

    /// <summary>For each path cell, the neighbor one step closer to gold (BFS parent from gold).</summary>
    Dictionary<Vector2Int, Vector2Int> _towardGold;

    public Vector3 CellToWorld(int x, int y)
    {
        return new Vector3(x * CellSize, 0f, y * CellSize);
    }

    public Vector3 StartWorld => CellToWorld(StartCell.x, StartCell.y);
    public Vector3 GoldWorld => CellToWorld(GoldCell.x, GoldCell.y);

    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    public bool IsPath(int x, int y) => InBounds(x, y) && !IsWall[x, y];

    public Vector2Int WorldToCell(Vector3 world)
    {
        int x = Mathf.RoundToInt(world.x / CellSize);
        int z = Mathf.RoundToInt(world.z / CellSize);
        return new Vector2Int(x, z);
    }

    public Vector2Int NearestPathCell(Vector3 world)
    {
        var c = WorldToCell(world);
        if (IsPath(c.x, c.y)) return c;

        float best = float.MaxValue;
        var pick = StartCell;
        int x0 = Mathf.Clamp(c.x - 3, 0, Width - 1);
        int x1 = Mathf.Clamp(c.x + 3, 0, Width - 1);
        int y0 = Mathf.Clamp(c.y - 3, 0, Height - 1);
        int y1 = Mathf.Clamp(c.y + 3, 0, Height - 1);
        for (int x = x0; x <= x1; x++)
        {
            for (int y = y0; y <= y1; y++)
            {
                if (IsWall[x, y]) continue;
                float d = (CellToWorld(x, y) - world).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    pick = new Vector2Int(x, y);
                }
            }
        }
        if (best < float.MaxValue) return pick;

        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                if (IsWall[x, y]) continue;
                float d = (CellToWorld(x, y) - world).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    pick = new Vector2Int(x, y);
                }
            }
        }
        return pick;
    }

    public void BuildGoldGuidance()
    {
        _towardGold = new Dictionary<Vector2Int, Vector2Int>(Width * Height / 2);
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(GoldCell);
        _towardGold[GoldCell] = GoldCell;
        var steps = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            foreach (var step in steps)
            {
                var next = cell + step;
                if (!IsPath(next.x, next.y) || _towardGold.ContainsKey(next))
                    continue;
                _towardGold[next] = cell;
                queue.Enqueue(next);
            }
        }
    }

    /// <summary>Next path cell on the solution toward gold from <paramref name="from"/>.</summary>
    public Vector2Int NextStepTowardGold(Vector2Int from)
    {
        if (_towardGold == null)
            BuildGoldGuidance();
        if (_towardGold != null && _towardGold.TryGetValue(from, out var next))
            return next;
        return from;
    }

    /// <summary>BFS next step from <paramref name="from"/> toward <paramref name="goal"/> along paths.</summary>
    public Vector2Int NextStepToward(Vector2Int from, Vector2Int goal)
    {
        if (from == goal || !IsPath(from.x, from.y) || !IsPath(goal.x, goal.y))
            return from;

        var queue = new Queue<Vector2Int>();
        var parent = new Dictionary<Vector2Int, Vector2Int>();
        queue.Enqueue(from);
        parent[from] = from;
        var steps = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        bool found = false;
        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            if (cell == goal)
            {
                found = true;
                break;
            }
            foreach (var step in steps)
            {
                var next = cell + step;
                if (!IsPath(next.x, next.y) || parent.ContainsKey(next))
                    continue;
                parent[next] = cell;
                queue.Enqueue(next);
            }
        }
        if (!found) return from;

        var cursor = goal;
        while (parent.TryGetValue(cursor, out var p) && p != from && p != cursor)
            cursor = p;
        return cursor;
    }
}

public static class MazeGenerator
{
    // Seed 1661: long solution path plus several deep dead ends (one ~25 cells).
    public const int Seed = 1661;
    public const int Width = 25;
    public const int Height = 21;
    public const float CellSize = 4f;
    const float StraightBias = 0.58f;

    public static MazeData Build()
    {
        var rng = new System.Random(Seed);
        var wall = new bool[Width, Height];
        for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
                wall[x, y] = true;

        var start = new Vector2Int(1, Height - 2);
        wall[start.x, start.y] = false;

        var stack = new Stack<Vector2Int>();
        stack.Push(start);
        Vector2Int lastDir = Vector2Int.zero;
        var dirs = new[]
        {
            new Vector2Int(0, 2),
            new Vector2Int(0, -2),
            new Vector2Int(2, 0),
            new Vector2Int(-2, 0)
        };

        while (stack.Count > 0)
        {
            var cell = stack.Peek();
            var neighbors = new List<(Vector2Int next, Vector2Int dir)>();
            foreach (var dir in dirs)
            {
                var next = cell + dir;
                if (next.x > 0 && next.x < Width - 1 && next.y > 0 && next.y < Height - 1 && wall[next.x, next.y])
                    neighbors.Add((next, dir));
            }

            if (neighbors.Count == 0)
            {
                stack.Pop();
                continue;
            }

            (Vector2Int next, Vector2Int dir) pick;
            if (lastDir != Vector2Int.zero)
            {
                var straight = neighbors.FindAll(n => n.dir == lastDir);
                if (straight.Count > 0 && rng.NextDouble() < StraightBias)
                    pick = straight[rng.Next(straight.Count)];
                else
                    pick = neighbors[rng.Next(neighbors.Count)];
            }
            else
            {
                pick = neighbors[rng.Next(neighbors.Count)];
            }

            wall[pick.next.x, pick.next.y] = false;
            wall[cell.x + pick.dir.x / 2, cell.y + pick.dir.y / 2] = false;
            stack.Push(pick.next);
            lastDir = pick.dir;
        }

        var gold = FarthestCell(wall, start);

        var maze = new MazeData
        {
            IsWall = wall,
            Width = Width,
            Height = Height,
            CellSize = CellSize,
            StartCell = start,
            GoldCell = gold
        };
        maze.BuildGoldGuidance();
        return maze;
    }

    static Vector2Int FarthestCell(bool[,] wall, Vector2Int start)
    {
        var queue = new Queue<Vector2Int>();
        var dist = new Dictionary<Vector2Int, int>();
        queue.Enqueue(start);
        dist[start] = 0;
        var farthest = start;
        var best = 0;
        var steps = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            if (dist[cell] > best)
            {
                best = dist[cell];
                farthest = cell;
            }

            foreach (var step in steps)
            {
                var next = cell + step;
                if (next.x < 0 || next.y < 0 || next.x >= Width || next.y >= Height)
                    continue;
                if (wall[next.x, next.y] || dist.ContainsKey(next))
                    continue;
                dist[next] = dist[cell] + 1;
                queue.Enqueue(next);
            }
        }

        return farthest;
    }
}
