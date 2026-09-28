using UnityEngine;

/// <summary>
/// Spawns non-Husk enemies based on level progression.
/// Called by GameBootstrap after Husk spawning.
/// </summary>
public static class EnemySpawner
{
    /// <summary>
    /// Spawn level-appropriate enemies. Husks are spawned separately in GameBootstrap.
    /// </summary>
    public static void SpawnEnemies(MazeData maze, FarmWalkerController player, LevelDef def,
                                     Transform parent)
    {
        int level = def.levelNumber;
        var rng = new System.Random(def.seedBase + level * 7);

        // Level 3+: 1 StaleLoaf
        if (level >= 3)
        {
            int loafCount = level >= 4 ? 2 : 1;
            for (int i = 0; i < loafCount; i++)
            {
                var cell = FindOffRouteCell(maze, player, rng);
                StaleLoaf.Spawn(maze, cell, parent);
            }
        }

        // Level 4+: 1 IcingWisp
        if (level >= 4)
        {
            IcingWisp.Spawn(maze, player, parent);
        }

        // Level 5: 1 SugarAnt cluster
        if (level >= 5)
        {
            var cell = FindOffRouteCell(maze, player, rng);
            SugarAnt.SpawnCluster(maze, cell, parent, player);
        }
    }

    /// <summary>
    /// Find a path cell that is NOT on the gold solution route and NOT within 3 cells of
    /// the player's start, falling back to any path cell.
    /// </summary>
    static Vector2Int FindOffRouteCell(MazeData maze, FarmWalkerController player,
                                       System.Random rng)
    {
        // Build the set of solution-route cells
        var onRoute = new System.Collections.Generic.HashSet<Vector2Int>();
        var cursor = maze.StartCell;
        int safety = maze.Width * maze.Height;
        while (safety-- > 0)
        {
            onRoute.Add(cursor);
            if (cursor == maze.GoldCell) break;
            var next = maze.NextStepTowardGold(cursor);
            if (next == cursor || !maze.IsPath(next.x, next.y)) break;
            cursor = next;
        }

        // Collect candidate cells: path cells, off-route, not near start
        var candidates = new System.Collections.Generic.List<Vector2Int>();
        var startCell = maze.StartCell;
        for (int x = 0; x < maze.Width; x++)
        {
            for (int y = 0; y < maze.Height; y++)
            {
                if (!maze.IsPath(x, y)) continue;
                var cell = new Vector2Int(x, y);
                if (onRoute.Contains(cell)) continue;

                float dx = x - startCell.x;
                float dy = y - startCell.y;
                if (Mathf.Abs(dx) < 3f && Mathf.Abs(dy) < 3f) continue;

                candidates.Add(cell);
            }
        }

        if (candidates.Count > 0)
            return candidates[rng.Next(candidates.Count)];

        // Fallback: any path cell not on route
        candidates.Clear();
        for (int x = 0; x < maze.Width; x++)
        {
            for (int y = 0; y < maze.Height; y++)
            {
                if (!maze.IsPath(x, y)) continue;
                var cell = new Vector2Int(x, y);
                if (onRoute.Contains(cell)) continue;
                candidates.Add(cell);
            }
        }

        if (candidates.Count > 0)
            return candidates[rng.Next(candidates.Count)];

        // Last resort: any path cell
        for (int x = 0; x < maze.Width; x++)
        {
            for (int y = 0; y < maze.Height; y++)
            {
                if (!maze.IsPath(x, y)) continue;
                return new Vector2Int(x, y);
            }
        }
        return maze.StartCell;
    }
}