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

    /// <summary>
    /// M32f — the glint variant, spawned as an additive child of every puddle quad and left OFF. The puddle's own
    /// mesh is reused (it already carries the decal's rut axis in its V), so the streak lies along the water the
    /// same way the decal does. `GlintEnabled` is the shipping switch: false until Todd says otherwise.
    /// </summary>
    public static bool GlintEnabled = false;

    public static int AddGlint(Transform puddlesRoot, Vector3 moonHoriz)
    {
        if (puddlesRoot == null) return 0;
        var mat = Materials.Glint();
        int n = 0;
        foreach (Transform p in puddlesRoot)
        {
            var mf = p.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            // WHICH WAY IS UP IS THE MESH'S OWN BUSINESS. The first two attempts offset along local +Y and scaled
            // local X/Z, which is only correct if the decal's quad lies in the XZ plane — it does not, and a quad
            // squashed along the wrong axis is an edge-on sliver: present, activated, and worth +0.15 of 255. The
            // mesh's own local bounds say which axis is the flat one (the normal) and which in-plane axis is the
            // long one (the rut the streak runs down).
            var lb = mf.sharedMesh.bounds.size;
            float[] e = { lb.x, lb.y, lb.z };
            int flat = 0;
            if (e[1] < e[flat]) flat = 1;
            if (e[2] < e[flat]) flat = 2;
            int a1 = (flat + 1) % 3, a2 = (flat + 2) % 3;
            int longIn = e[a1] >= e[a2] ? a1 : a2;
            int shortIn = longIn == a1 ? a2 : a1;
            var off = Vector3.zero;
            off[flat] = 0.02f / Mathf.Max(1e-4f, Mathf.Abs(p.lossyScale[flat]));
            var sc = Vector3.one;
            sc[longIn] = 0.46f;      // inside the water core, not out into the halo
            sc[shortIn] = 0.30f;
            var go = new GameObject("Glint");
            go.transform.SetParent(p, false);
            go.transform.localPosition = off;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = sc;
            go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            go.SetActive(GlintEnabled);
            n++;
        }
        return n;
    }

    /// <summary>The one switch, driven by the harness so both variants can be framed from the same camera.</summary>
    public static void SetGlintForTest(bool on)
    {
        GlintEnabled = on;
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (r != null && r.gameObject.name == "Glint") r.gameObject.SetActive(on);
    }

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
