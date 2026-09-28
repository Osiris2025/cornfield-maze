using UnityEngine;

/// <summary>
/// Singleton wallet that persists coin count across levels via PlayerPrefs (FSD §10).
/// Lazy-initialises itself on first access so it is available before GameBootstrap runs.
/// </summary>
public sealed class CoinWallet : MonoBehaviour
{
    const string PrefsKey = "corn.coins";

    static CoinWallet _instance;
    public static CoinWallet Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("CoinWallet");
                _instance = go.AddComponent<CoinWallet>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    public int Balance { get; private set; }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        Balance = PlayerPrefs.GetInt(PrefsKey, 0);
    }

    /// <summary>Add <paramref name="amount"/> coins and persist.</summary>
    public void Add(int amount)
    {
        Balance += amount;
        PlayerPrefs.SetInt(PrefsKey, Balance);
        PlayerPrefs.Save();
        Debug.Log($"[CoinWallet] +{amount} → {Balance}");
    }

    /// <summary>
    /// Spend <paramref name="amount"/> coins. Returns false if the balance is too low.
    /// </summary>
    public bool Spend(int amount)
    {
        if (Balance < amount) return false;
        Balance -= amount;
        PlayerPrefs.SetInt(PrefsKey, Balance);
        PlayerPrefs.Save();
        Debug.Log($"[CoinWallet] -{amount} → {Balance}");
        return true;
    }

    /// <summary>Resets the wallet to zero — for test harness use only.</summary>
    public void ResetForTest()
    {
        Balance = 0;
        PlayerPrefs.SetInt(PrefsKey, 0);
        PlayerPrefs.Save();
        Debug.Log("[CoinWallet] reset to 0 (test)");
    }
}