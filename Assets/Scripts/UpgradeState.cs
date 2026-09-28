using UnityEngine;

/// <summary>
/// M12: tracks upgrade tiers across the five upgrades and persists them via PlayerPrefs.
/// Static data-only class — no MonoBehaviour needed (FSD §11).
/// </summary>
public static class UpgradeState
{
    public enum UpgradeId { Dough, Seal, Feet, Cane, Rush }

    const string PrefsPrefix = "corn.upgrade.";

    static readonly string[] PrefsKeys =
    {
        PrefsPrefix + "dough",
        PrefsPrefix + "seal",
        PrefsPrefix + "feet",
        PrefsPrefix + "cane",
        PrefsPrefix + "rush"
    };

    public static readonly int[] DoughCosts = { 30, 80, 180 };
    public static readonly int[] SealCosts  = { 30, 80, 180 };
    public static readonly int[] FeetCosts  = { 30, 80, 180 };
    public static readonly int[] CaneCosts  = { 30, 80, 180 };
    public static readonly int[] RushCosts  = { 30, 80, 180 };

    static int[] CostsFor(UpgradeId id)
    {
        switch (id)
        {
            case UpgradeId.Dough: return DoughCosts;
            case UpgradeId.Seal:  return SealCosts;
            case UpgradeId.Feet:  return FeetCosts;
            case UpgradeId.Cane:  return CaneCosts;
            default:              return RushCosts;
        }
    }

    /// <summary>Number of tiers purchased for this upgrade (0–3).</summary>
    public static int GetTier(UpgradeId id)
    {
        return PlayerPrefs.GetInt(PrefsKeys[(int)id], 0);
    }

    /// <summary>Cost of the next tier, or -1 if already maxed.</summary>
    public static int NextCost(UpgradeId id)
    {
        int tier = GetTier(id);
        if (tier >= 3) return -1;
        return CostsFor(id)[tier];
    }

    /// <summary>True if all three tiers are purchased.</summary>
    public static bool IsMaxed(UpgradeId id)
    {
        return GetTier(id) >= 3;
    }

    /// <summary>True if the wallet can afford the next tier.</summary>
    public static bool CanAfford(UpgradeId id)
    {
        int cost = NextCost(id);
        if (cost < 0) return false;
        return CoinWallet.Instance.Balance >= cost;
    }

    /// <summary>
    /// Purchase the next tier for this upgrade. Spends coins via the wallet,
    /// increments the tier counter, and persists immediately. Returns false if
    /// the upgrade is maxed or the player cannot afford it.
    /// </summary>
    public static bool Buy(UpgradeId id)
    {
        if (IsMaxed(id)) return false;
        int cost = NextCost(id);
        if (!CoinWallet.Instance.Spend(cost)) return false;
        int tier = GetTier(id) + 1;
        PlayerPrefs.SetInt(PrefsKeys[(int)id], tier);
        PlayerPrefs.Save();
        Debug.Log($"[UpgradeState] {UpgradeName(id)} → tier {tier} ({cost} coins spent)");
        return true;
    }

    /// <summary>Human-readable upgrade name.</summary>
    public static string UpgradeName(UpgradeId id)
    {
        switch (id)
        {
            case UpgradeId.Dough: return "Thicker Dough";
            case UpgradeId.Seal:  return "Icing Seal";
            case UpgradeId.Feet:  return "Faster Feet";
            case UpgradeId.Cane:  return "Candy Cane Reach";
            default:              return "Sugar Rush";
        }
    }

    /// <summary>One-line description of what the upgrade does.</summary>
    public static string UpgradeDescription(UpgradeId id)
    {
        switch (id)
        {
            case UpgradeId.Dough: return "More cookie. +30/+30/+40 max dough per tier (100→200).";
            case UpgradeId.Seal:  return "Rain hurts less. −15% dissolve rate per tier (stacks to −45%).";
            case UpgradeId.Feet:  return "Walk faster. +5% move speed per tier.";
            case UpgradeId.Cane:  return "Hit harder. +8/+10/+12 cane damage, +10° arc per tier.";
            default:              return "Rush lasts longer. +1.0/+1.0/+2.0 s Sugar Rush per tier.";
        }
    }

    /// <summary>Sum of all coins ever spent on upgrades (for debug / shop display).</summary>
    public static int TotalSpent()
    {
        int total = 0;
        for (int id = 0; id < 5; id++)
        {
            int tier = GetTier((UpgradeId)id);
            int[] costs = CostsFor((UpgradeId)id);
            for (int t = 0; t < tier; t++)
                total += costs[t];
        }
        return total;
    }
}