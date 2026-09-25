using System.Collections.Generic;
using UnityEngine;

public static class MazeWorldBuilder
{
    public static void Build(MazeData maze)
    {
        var root = new GameObject("CornField");
        // M29: the photographic ground. The lane keeps the name the wetness system registered against, so
        // PathMudWetness still drives it; the field's repeat is carried per-renderer (the field is one cube).
        var gravelMat = Materials.GroundLane();
        PathMudWetness.RegisterGravel(gravelMat);
        var fieldMat = Materials.GroundField();
        // The lane dressing quads are boxes a few centimetres across, so they need their own texture scale:
        // the lane material tiles once per 2 m of world UV, and a 16 cm tuft with that mapping would show a
        // single gravel stone blown up to the size of a fist.
        var grassMat = new Material(gravelMat);
        if (grassMat.HasProperty("_BaseMap")) grassMat.SetTextureScale("_BaseMap", new Vector2(0.12f, 0.12f));
        var pebbleMats = new[]
        {
            Materials.Pebble(new Color(0.42f, 0.36f, 0.26f)),
            Materials.Pebble(new Color(0.50f, 0.44f, 0.34f)),
            Materials.Pebble(new Color(0.34f, 0.28f, 0.20f))
        };

        float fieldW = maze.Width * maze.CellSize + 16f;
        float fieldH = maze.Height * maze.CellSize + 16f;
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Field";
        ground.transform.SetParent(root.transform, false);
        ground.transform.position = new Vector3((maze.Width - 1) * maze.CellSize * 0.5f, -0.25f, (maze.Height - 1) * maze.CellSize * 0.5f);
        ground.transform.localScale = new Vector3(fieldW, 0.5f, fieldH);
        var fieldRenderer = ground.GetComponent<Renderer>();
        fieldRenderer.sharedMaterial = fieldMat;
        // M29: a 60 m field wants 30 repeats of a 2 m map, not the 5.5 the old noise used. The cube's faces
        // are 0..1 UV, so the repeat goes in the shader's _BaseMap_ST via a property block — per renderer,
        // with no material instance per cell and nothing allocated per frame.
        float fieldTu = fieldW / Materials.GroundTileMetres;
        float fieldTv = fieldH / Materials.GroundTileMetres;
        var fieldMpb = new MaterialPropertyBlock();
        fieldMpb.SetVector("_BaseMap_ST", new Vector4(fieldTu, fieldTv, 0f, 0f));
        fieldMpb.SetVector("_BumpMap_ST", new Vector4(fieldTu, fieldTv, 0f, 0f));
        fieldRenderer.SetPropertyBlock(fieldMpb);

        var rng = new System.Random(MazeGenerator.Seed + 17);
        Material[] cornMats = null;
        Mesh[] cornMeshes = null;
        float[] cornHeights = null;

        var pathRoot = new GameObject("Paths");
        pathRoot.transform.SetParent(root.transform, false);
        var blockRoot = new GameObject("PathBounds");
        blockRoot.transform.SetParent(root.transform, false);
        var cornRoot = new GameObject("CornBlocks");
        cornRoot.transform.SetParent(root.transform, false);

        // M20 (§24.2): the field is TILED from the six shipped 2 m blocks — 4 per 4 m wall cell, block
        // index and 90° rotation hashed from the cell coordinate, so the whole field costs six meshes
        // and four rotations and nothing is unique per slot.
        var cornPrefabs = LoadCornBlocks();
        if (cornPrefabs == null)
            Debug.LogWarning("M20: corn block prefabs are missing from Resources/Corn — falling back to " +
                             "the old primitive scatter. Run CornMaze.EditorTools.CornMazeCornSetup.SetupAll.");

        // M29: the lane pieces are flat meshes now, so there is no lane thickness constant any more.
        float lane = maze.CellSize * 0.52f;

        for (int x = 0; x < maze.Width; x++)
        {
            for (int y = 0; y < maze.Height; y++)
            {
                var center = maze.CellToWorld(x, y);
                if (!maze.IsWall[x, y])
                {
                    // M31: the cell's own piece exists to fade the edges that face corn. A straight run needs
                    // one piece — the fade runs across the lane, and the two edges along it are covered by the
                    // connectors. A corner or a dead end needs both, because it has exposed edges on both
                    // axes; a four-way junction needs neither, because all four of its sides are covered by
                    // the connectors running out of it. Placing one square piece for every cell (M29) meant a
                    // north-south cell got its fade running down the lane instead of across it.
                    bool east = maze.IsPath(x + 1, y);
                    bool west = maze.IsPath(x - 1, y);
                    bool north = maze.IsPath(x, y + 1);
                    bool south = maze.IsPath(x, y - 1);
                    bool needAcrossZ = !north || !south;    // a north or south edge faces corn
                    bool needAcrossX = !east || !west;      // an east or west edge faces corn
                    // One core piece per cell, fading across the axis whose edge faces corn. When both do —
                    // a corner or a dead end — it fades the axis the lane runs along, and the other exposed
                    // edge keeps its hard line: a linear strip cannot fade two adjacent sides at once, and
                    // the alternative (two overlapping pieces) was measured as a brighter plate with a
                    // straight edge, which is the exact defect this milestone removes.
                    bool axisX;
                    if (needAcrossZ && !needAcrossX) axisX = true;        // only north/south face corn
                    else if (needAcrossX && !needAcrossZ) axisX = false;  // only east/west face corn
                    else if (needAcrossZ && needAcrossX)
                        // Both axes have an exposed edge: a corner or a dead end. Fade the axis the lane runs
                        // along. At a corner the arms are on both axes, so it is a coin toss — taken as the
                        // east-west axis, and the frame of a corner says which edge is left hard.
                        axisX = (east || west) && !(north || south);
                    else axisX = false;                                   // four-way junction: all sides covered
                    PlaceLane(pathRoot.transform, center + Vector3.up * 0.03f, lane, lane, gravelMat, axisX);

                    // Connectors ABUT the core piece: from this cell's edge to the next cell's edge. M29 ran
                    // them centre to centre, so every connector overlapped the two core pieces it sat between
                    // — two alpha layers over the same ground, which the M31 frame caught as a bright
                    // rectangle with a hard edge in the middle of the lane.
                    float span = maze.CellSize - lane;
                    if (east)
                    {
                        PlaceLane(
                            pathRoot.transform,
                            center + new Vector3(maze.CellSize * 0.5f, 0.03f, 0f),
                            span, lane,
                            gravelMat, true);
                    }
                    if (north)
                    {
                        PlaceLane(
                            pathRoot.transform,
                            center + new Vector3(0f, 0.03f, maze.CellSize * 0.5f),
                            lane, span,
                            gravelMat, false);
                    }

                    ScatterPathDressing(pathRoot.transform, center, maze, x, y, lane, grassMat, pebbleMats, rng);
                    continue;
                }

                AddCornBlock(blockRoot.transform, center, maze.CellSize);

                if (cornPrefabs != null)
                {
                    PlantCornBlocks(cornRoot.transform, x, y, center, cornPrefabs);
                    continue;
                }

                // Fallback (§24.2 keeps CornPlant.cs as the fallback path until this swap lands): the old
                // per-cell scatter, 0.4-0.6 plants/m². Only runs if the block prefabs are absent.
                if (cornMats == null)
                {
                    cornMats = CornPlant.CreateMaterials();
                    CornPlant.BuildVariants(rng, out cornMeshes, out cornHeights);
                }
                int stalks = 6 + rng.Next(4);
                for (int i = 0; i < stalks; i++)
                {
                    float ox = ((float)rng.NextDouble() - 0.5f) * maze.CellSize * 0.78f;
                    float oz = ((float)rng.NextDouble() - 0.5f) * maze.CellSize * 0.78f;
                    int variant = rng.Next(cornMeshes.Length);
                    CornPlant.Spawn(root.transform, center + new Vector3(ox, 0f, oz), cornMeshes[variant], cornHeights[variant], cornMats, rng);
                }
            }
        }

        SetupAtmosphere();
    }

    /// <summary>
    /// M20: the six shipped blocks, loaded once. Every slot instantiates one of these six — nothing is
    /// unique per slot, which is what keeps the field at six meshes and four rotations (§24.2).
    /// </summary>
    public static GameObject[] LoadCornBlocks()
    {
        var prefabs = new List<GameObject>();
        for (int i = 1; i <= 6; i++)
        {
            var prefab = Resources.Load<GameObject>("Corn/CornBlock_2m_" + i.ToString("00"));
            if (prefab == null) return null;
            prefabs.Add(prefab);
        }
        return prefabs.ToArray();
    }

    /// <summary>
    /// Four 2 m blocks tile one 4 m wall cell (§24.2). The block index and the 90° rotation are hashed
    /// from the cell coordinate AND the quadrant, so placement is deterministic from the seed and the
    /// wall never repeats an obvious pattern. The blocks deliberately overhang their 2 m square — the
    /// leaves are what closes the join between neighbours.
    /// </summary>
    static void PlantCornBlocks(Transform parent, int cellX, int cellY, Vector3 centre, GameObject[] prefabs)
    {
        const float BlockSize = 2.0f;
        for (int q = 0; q < 4; q++)
        {
            int sx = (q & 1) == 0 ? -1 : 1;
            int sz = (q & 2) == 0 ? -1 : 1;

            uint h = Hash((uint)(cellX * 73856093) ^ (uint)(cellY * 19349663) ^ (uint)(q * 83492791) ^ (uint)MazeGenerator.Seed);

            var go = Object.Instantiate(prefabs[h % (uint)prefabs.Length], parent);
            go.name = "Corn_c" + cellX + "_" + cellY + "_q" + q;
            go.transform.position = centre + new Vector3(sx * BlockSize * 0.5f, 0f, sz * BlockSize * 0.5f);
            go.transform.rotation = Quaternion.Euler(0f, ((h / (uint)prefabs.Length) % 4u) * 90f, 0f);
        }
    }

    static uint Hash(uint v)
    {
        v ^= v >> 16;
        v *= 2246822519u;
        v ^= v >> 13;
        v *= 3266489917u;
        v ^= v >> 16;
        return v;
    }

    /// <summary>
    /// M31: a lane piece — a quad laid over the field, blended into it by the alpha fade in the lane
    /// material (see GroundLaneMesh: the geometric cut M29 used was still a cut line, just an irregular
    /// one). No collider: the field cube underneath is the floor the player walks on and the lane sits
    /// 3 cm above it.
    /// </summary>
    static void PlaceLane(Transform parent, Vector3 pos, float w, float d, Material mat, bool uAlongX)
    {
        var path = new GameObject("Lane");
        path.transform.SetParent(parent, false);
        path.transform.position = pos;
        path.AddComponent<MeshFilter>().sharedMesh = GroundLaneMesh.Build(w, d, pos, uAlongX);
        path.AddComponent<MeshRenderer>().sharedMaterial = mat;
    }

    static void ScatterPathDressing(
        Transform parent,
        Vector3 center,
        MazeData maze,
        int x,
        int y,
        float lane,
        Material grassMat,
        Material[] pebbleMats,
        System.Random rng)
    {
        bool east = maze.IsPath(x + 1, y);
        bool west = maze.IsPath(x - 1, y);
        bool north = maze.IsPath(x, y + 1);
        bool south = maze.IsPath(x, y - 1);

        float half = lane * 0.48f;
        if (!east || !west || !north || !south)
        {
            int tufts = 1 + rng.Next(2);
            for (int i = 0; i < tufts; i++)
            {
                var edge = EdgePoint(center, half, east, west, north, south, rng);
                var tuft = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tuft.name = "GrassTuft";
                tuft.transform.SetParent(parent, false);
                tuft.transform.position = edge + Vector3.up * 0.045f;
                tuft.transform.localScale = new Vector3(
                    0.16f + (float)rng.NextDouble() * 0.14f,
                    0.05f,
                    0.12f + (float)rng.NextDouble() * 0.12f);
                tuft.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                tuft.GetComponent<Renderer>().sharedMaterial = grassMat;
                Object.Destroy(tuft.GetComponent<Collider>());
            }
        }

        if (rng.NextDouble() < 0.55)
        {
            int n = 1 + rng.Next(2);
            for (int i = 0; i < n; i++)
            {
                float ox = ((float)rng.NextDouble() - 0.5f) * lane * 0.62f;
                float oz = ((float)rng.NextDouble() - 0.5f) * lane * 0.62f;
                var pebble = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                pebble.name = "Pebble";
                pebble.transform.SetParent(parent, false);
                pebble.transform.position = center + new Vector3(ox, 0.055f, oz);
                float s = 0.045f + (float)rng.NextDouble() * 0.07f;
                pebble.transform.localScale = new Vector3(s, s * 0.55f, s * 0.85f);
                pebble.transform.rotation = Quaternion.Euler(
                    (float)rng.NextDouble() * 30f,
                    (float)rng.NextDouble() * 360f,
                    (float)rng.NextDouble() * 20f);
                pebble.GetComponent<Renderer>().sharedMaterial = pebbleMats[rng.Next(pebbleMats.Length)];
                Object.Destroy(pebble.GetComponent<Collider>());
            }
        }
    }

    static Vector3 EdgePoint(Vector3 center, float half, bool east, bool west, bool north, bool south, System.Random rng)
    {
        int side = rng.Next(4);
        for (int k = 0; k < 4; k++)
        {
            int s = (side + k) % 4;
            if (s == 0 && !north) return center + new Vector3(((float)rng.NextDouble() - 0.5f) * half, 0f, half);
            if (s == 1 && !south) return center + new Vector3(((float)rng.NextDouble() - 0.5f) * half, 0f, -half);
            if (s == 2 && !east) return center + new Vector3(half, 0f, ((float)rng.NextDouble() - 0.5f) * half);
            if (s == 3 && !west) return center + new Vector3(-half, 0f, ((float)rng.NextDouble() - 0.5f) * half);
        }
        return center + new Vector3(((float)rng.NextDouble() - 0.5f) * half, 0f, ((float)rng.NextDouble() - 0.5f) * half);
    }

    static void AddCornBlock(Transform parent, Vector3 center, float cellSize)
    {
        var go = new GameObject("CornBlock");
        go.transform.SetParent(parent, false);
        go.transform.position = center;
        var box = go.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, 1.35f, 0f);
        box.size = new Vector3(cellSize * 0.92f, 2.7f, cellSize * 0.92f);
    }

    static void SetupAtmosphere()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.028f;
        RenderSettings.fogColor = new Color(0.78f, 0.70f, 0.42f);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.90f, 0.78f, 0.45f);
        RenderSettings.ambientEquatorColor = new Color(0.55f, 0.48f, 0.28f);
        RenderSettings.ambientGroundColor = new Color(0.22f, 0.18f, 0.10f);

        var light = Object.FindFirstObjectByType<Light>();
        if (light == null)
        {
            var go = new GameObject("Sun");
            light = go.AddComponent<Light>();
            light.type = LightType.Directional;
        }

        light.color = new Color(1f, 0.86f, 0.55f);
        light.intensity = 1.35f;
        light.transform.rotation = Quaternion.Euler(38f, 155f, 0f);
        light.shadows = LightShadows.Soft;
    }
}
