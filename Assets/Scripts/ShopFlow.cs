using UnityEngine;

/// <summary>
/// M12: controls shop visibility. The public API <see cref="Show"/> creates the shop
/// overlay, and <see cref="IsOpen"/> lets GameBootstrap gate its own Space/R handling.
/// The shop panel is destroyed after the continue callback fires (FSD §11, §14).
/// </summary>
public sealed class ShopFlow : MonoBehaviour
{
    /// <summary>True while the shop overlay is visible.</summary>
    public static bool IsOpen { get; private set; }

    /// <summary>
    /// Show the upgrade shop. Creates the CoinWallet singleton if needed,
    /// builds the shop panel, and wires <paramref name="onContinue"/> to its
    /// CONTINUE button. The panel is destroyed when that callback fires.
    /// </summary>
    public static void Show(System.Action onContinue)
    {
        if (IsOpen) return;

        // Ensure the wallet exists.
        var _ = CoinWallet.Instance;

        IsOpen = true;

        var go = new GameObject("ShopFlow");
        var flow = go.AddComponent<ShopFlow>();

        var panel = ShopPanel.Create(() =>
        {
            IsOpen = false;
            if (flow != null) Destroy(flow.gameObject);
            if (onContinue != null) onContinue();
        });
    }
}