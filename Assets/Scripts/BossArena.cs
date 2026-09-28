using UnityEngine;

/// <summary>
/// M47: static utility that generates a boss arena as a circular clearing
/// within the maze with pillar walls at the perimeter.
/// </summary>
public static class BossArena
{
    const float ArenaRadius = 5f;
    const int PillarCount = 24;
    const float PillarHeight = 4f;
    const float PillarThickness = 0.5f;

    /// <summary>
    /// Build a circular arena at the maze's gold cell.
    /// Clears walls in a circle, adds pillar walls around the perimeter.
    /// Returns the arena root GameObject.
    /// </summary>
    public static GameObject BuildArena(MazeData maze)
    {
        var root = new GameObject("BossArena");
        Vector3 center = maze.CellToWorld(maze.GoldCell.x, maze.GoldCell.y);

        // Clear a circular region: set IsWall=false for all cells within radius
        float halfCell = maze.CellSize * 0.5f;
        for (int x = 0; x < maze.Width; x++)
        {
            for (int y = 0; y < maze.Height; y++)
            {
                Vector3 cellCenter = maze.CellToWorld(x, y);
                float dist = Vector3.Distance(
                    new Vector3(cellCenter.x, 0f, cellCenter.z),
                    new Vector3(center.x, 0f, center.z));
                if (dist < ArenaRadius - halfCell)
                {
                    maze.IsWall[x, y] = false;
                }
            }
        }

        // Place pillars around the arena perimeter
        var pillarMat = Materials.Lit(new Color(0.38f, 0.28f, 0.18f), 0.08f);
        for (int i = 0; i < PillarCount; i++)
        {
            float angle = (i / (float)PillarCount) * Mathf.PI * 2f;
            Vector3 pillarPos = center + new Vector3(
                Mathf.Cos(angle) * ArenaRadius,
                0f,
                Mathf.Sin(angle) * ArenaRadius);

            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pillar.name = "ArenaPillar";
            pillar.transform.SetParent(root.transform, false);
            pillar.transform.position = pillarPos + Vector3.up * (PillarHeight * 0.5f);
            pillar.transform.localScale = new Vector3(PillarThickness, PillarHeight, PillarThickness);
            pillar.GetComponent<Renderer>().sharedMaterial = pillarMat;
        }

        // Ground plane under the arena
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "ArenaFloor";
        floor.transform.SetParent(root.transform, false);
        floor.transform.position = center + Vector3.up * 0.01f;
        float floorSize = ArenaRadius * 2.2f;
        floor.transform.localScale = new Vector3(floorSize, 0.05f, floorSize);
        var floorMat = Materials.Lit(new Color(0.25f, 0.18f, 0.10f), 0.04f);
        floor.GetComponent<Renderer>().sharedMaterial = floorMat;
        Object.Destroy(floor.GetComponent<Collider>());

        Debug.Log($"[BossArena] Built arena at {center} — radius {ArenaRadius}, {PillarCount} pillars.");
        return root;
    }

    /// <summary>World position of the arena center (gold cell).</summary>
    public static Vector3 ArenaCenter(MazeData maze)
    {
        return maze.CellToWorld(maze.GoldCell.x, maze.GoldCell.y);
    }
}