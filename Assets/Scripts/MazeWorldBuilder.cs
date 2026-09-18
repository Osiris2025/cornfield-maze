using UnityEngine;

public static class MazeWorldBuilder
{
    public static void Build(MazeData maze)
    {
        var root = new GameObject("CornField");
        var cornMats = CornPlant.CreateMaterials();
        var gravelMat = Materials.Gravel(MazeGenerator.Seed + 41);
        PathMudWetness.RegisterGravel(gravelMat);
        var fieldMat = Materials.FieldGrass(MazeGenerator.Seed + 73);
        var grassMat = Materials.PathGrass(MazeGenerator.Seed + 91);
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
        ground.GetComponent<Renderer>().sharedMaterial = fieldMat;

        var rng = new System.Random(MazeGenerator.Seed + 17);
        CornPlant.BuildVariants(rng, out var cornMeshes, out var cornHeights);

        var pathRoot = new GameObject("Paths");
        pathRoot.transform.SetParent(root.transform, false);
        var blockRoot = new GameObject("PathBounds");
        blockRoot.transform.SetParent(root.transform, false);

        float lane = maze.CellSize * 0.52f;
        float thick = 0.07f;

        for (int x = 0; x < maze.Width; x++)
        {
            for (int y = 0; y < maze.Height; y++)
            {
                var center = maze.CellToWorld(x, y);
                if (!maze.IsWall[x, y])
                {
                    PlaceGravel(pathRoot.transform, center + Vector3.up * 0.03f, new Vector3(lane, thick, lane), gravelMat);

                    bool east = maze.IsPath(x + 1, y);
                    bool north = maze.IsPath(x, y + 1);
                    if (east)
                    {
                        PlaceGravel(
                            pathRoot.transform,
                            center + new Vector3(maze.CellSize * 0.5f, 0.03f, 0f),
                            new Vector3(maze.CellSize, thick, lane),
                            gravelMat);
                    }
                    if (north)
                    {
                        PlaceGravel(
                            pathRoot.transform,
                            center + new Vector3(0f, 0.03f, maze.CellSize * 0.5f),
                            new Vector3(lane, thick, maze.CellSize),
                            gravelMat);
                    }

                    ScatterPathDressing(pathRoot.transform, center, maze, x, y, lane, grassMat, pebbleMats, rng);
                    continue;
                }

                AddCornBlock(blockRoot.transform, center, maze.CellSize);

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

    static void PlaceGravel(Transform parent, Vector3 pos, Vector3 scale, Material mat)
    {
        var path = GameObject.CreatePrimitive(PrimitiveType.Cube);
        path.name = "Gravel";
        path.transform.SetParent(parent, false);
        path.transform.position = pos;
        path.transform.localScale = scale;
        path.GetComponent<Renderer>().sharedMaterial = mat;
        Object.Destroy(path.GetComponent<Collider>());
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
