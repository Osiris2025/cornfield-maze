using UnityEngine;

/// <summary>
/// M43: defines what a single level looks like — maze dimensions, Husk count, storm intensity.
/// Not a ScriptableObject so it works headlessly in builds without .asset files.
/// </summary>
[System.Serializable]
public class LevelDef
{
    public int levelNumber;
    public string levelName;
    public int mazeWidth;
    public int mazeHeight;
    public float cellSize;
    public int huskCount;
    public float stormStartIntensity;
    public int seedBase;

    static LevelDef[] _all;

    public static LevelDef[] All
    {
        get
        {
            if (_all == null) _all = BuildDefaults();
            return _all;
        }
    }

    static LevelDef[] BuildDefaults()
    {
        return new[]
        {
            new LevelDef { levelNumber = 1, levelName = "First Steps",      mazeWidth = 31, mazeHeight = 31, cellSize = 4.0f, huskCount = 1, stormStartIntensity = 0.25f, seedBase = 100 },
            new LevelDef { levelNumber = 2, levelName = "Darkening Sky",    mazeWidth = 39, mazeHeight = 39, cellSize = 4.0f, huskCount = 1, stormStartIntensity = 0.35f, seedBase = 200 },
            new LevelDef { levelNumber = 3, levelName = "The Corn Thickens", mazeWidth = 47, mazeHeight = 47, cellSize = 3.5f, huskCount = 2, stormStartIntensity = 0.50f, seedBase = 300 },
            new LevelDef { levelNumber = 4, levelName = "Howling Wind",     mazeWidth = 55, mazeHeight = 55, cellSize = 3.5f, huskCount = 2, stormStartIntensity = 0.65f, seedBase = 400 },
            new LevelDef { levelNumber = 5, levelName = "The Last Row",     mazeWidth = 63, mazeHeight = 63, cellSize = 3.0f, huskCount = 3, stormStartIntensity = 0.80f, seedBase = 500 },
        };
    }
}