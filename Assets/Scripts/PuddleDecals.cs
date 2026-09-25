using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// M32b — puddles: the only reflective surface in the maze.
///
/// Todd's ruling put water in charge of the sheen ("maybe there should be a sheen in puddles, but not over
/// all"), so the ground is matte by measurement (M32) and every drop of reflectivity in the game lives here.
///
/// Placement rule: **decals in lane low spots — a handful per level, elongated along the lane, never tiled,
/// never on the field.** Concretely:
///   * only cells on a STRAIGHT run (exactly two neighbours, opposite each other), so a puddle never sits in a
///     junction where it would read as a wall of water across the way out;
///   * six a level, taken every Nth candidate so they spread over the maze instead of clustering wherever the
///     generator happened to leave a straight run;
///   * 2.6 x 1.5 m and rotated into the lane's axis, so the short side stays inside the 2.08 m lane and the
///     puddle cannot reach the corn;
///   * one quad each, a few degrees off the lane's axis so the family does not read as one rotated stamp.
///
/// Removing them is deleting one object: everything is under a single parent named "Puddles".
/// </summary>
public static class PuddleDecals
{
    public const int PuddlesPerLevel = 4;
    // M32c: fewer and larger. Four at 4.4 x 1.8 m instead of six at 3.4 x 1.7 m — a 3.4 m rut seen from a
    // shallow angle foreshortens to roughly its own width and reads as a round painted spot, which is what
    // the frame showed. Water sits in a rut, so the rut should be long enough to still read as one when the
    // view flattens. The short side stays inside the 2.08 m lane, so the decal still cannot reach the corn.
    public const float PuddleLong = 4.4f;    // along the lane
    public const float PuddleWide = 1.8f;    // across it — inside the 2.08 m lane
    const float Lift = 0.006f;        // above the lane's own 0.03 m, so the decal does not z-fight it
    const float YawJitter = 8f;       // degrees
    const int SeedOffset = 4177;      // the puddle build's own seed (scripts/puddle_build.py)

    public static GameObject Build(Transform root, MazeData maze, Material mat)
    {
        var parent = new GameObject("Puddles");
        parent.transform.SetParent(root, false);

        var candidates = new List<Vector3>();
        var alongX = new List<bool>();
        for (int y = 0; y < maze.Height; y++)
        {
            for (int x = 0; x < maze.Width; x++)
            {
                if (!maze.IsPath(x, y)) continue;
                bool e = maze.IsPath(x + 1, y), w = maze.IsPath(x - 1, y);
                bool n = maze.IsPath(x, y + 1), s = maze.IsPath(x, y - 1);
                bool straightEW = e && w && !n && !s;
                bool straightNS = n && s && !e && !w;
                if (!straightEW && !straightNS) continue;
                candidates.Add(maze.CellToWorld(x, y));
                alongX.Add(straightEW);
            }
        }

        var rng = new System.Random(MazeGenerator.Seed + SeedOffset);
        int stride = Mathf.Max(1, candidates.Count / PuddlesPerLevel);
        int placed = 0;
        for (int i = 0; i < candidates.Count && placed < PuddlesPerLevel; i += stride)
        {
            var pos = candidates[i] + Vector3.up * (0.03f + Lift);
            var go = new GameObject("Puddle");
            go.transform.SetParent(parent.transform, false);
            go.transform.position = pos;
            float yaw = (float)(rng.NextDouble() * (YawJitter * 2.0) - YawJitter);
            // The decal's mesh puts its LONG axis on local X (GroundLaneMesh.Build takes the long side as its
            // width and lays it on X). So an east-west lane needs NO rotation and a north-south lane needs 90:
            // the first version had this inverted, which laid every east-west puddle across the lane — 3.4 m
            // across a 2.08 m lane, sticking into the corn.
            go.transform.rotation = Quaternion.Euler(0f, (alongX[i] ? 0f : 90f) + yaw, 0f);
            go.AddComponent<MeshFilter>().sharedMesh =
                GroundLaneMesh.Build(PuddleLong, PuddleWide, pos, alongX[i]);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            placed++;
        }

        Debug.Log("M32b puddles: " + placed + " placed under one parent (\"Puddles\"), out of " +
                  candidates.Count + " straight-run candidates, stride " + stride +
                  " — " + PuddleLong + " x " + PuddleWide + " m, elongated along the lane and inside the " +
                  "2.08 m lane width, never tiled, never on the field");
        return parent;
    }
}
