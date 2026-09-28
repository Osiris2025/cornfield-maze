using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// M12: full-screen shop overlay — dark panel with upgrade rows, BUY buttons,
/// coin display, and a CONTINUE button. Built entirely in code, no art assets (FSD §11, §16).
/// </summary>
public sealed class ShopPanel : MonoBehaviour
{
    System.Action _onContinue;
    Font _font;

    Text _coinLabel;
    Text[] _tierLabels = new Text[5];
    Text[] _costLabels = new Text[5];
    Button[] _buyButtons = new Button[5];

    public static ShopPanel Create(System.Action onContinue)
    {
        var go = new GameObject("ShopPanel");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 110; // over HUD, under front-end pause
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();

        var panel = go.AddComponent<ShopPanel>();
        panel._onContinue = onContinue;
        return panel;
    }

    void Start()
    {
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        BuildBackground();
        BuildTitle();
        BuildCoinDisplay();
        BuildUpgradeRows();
        BuildContinueButton();
        Refresh();
    }

    void Update()
    {
        // Refresh coin display and button states every frame so it stays live.
        if (_coinLabel != null)
            _coinLabel.text = "Coins: " + CoinWallet.Instance.Balance;

        for (int i = 0; i < 5; i++)
            RefreshRow((UpgradeState.UpgradeId)i);
    }

    // ---- layout helpers --------------------------------------------------

    Text MakeLabel(string name, string body, int size, TextAnchor align, Color color,
                   Vector2 anchored, Vector2 box, Transform parent = null)
    {
        if (parent == null) parent = transform;
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = (align == TextAnchor.MiddleCenter)
            ? new Vector2(0.5f, 0.5f)
            : new Vector2(0.5f, 1f);
        if (align == TextAnchor.MiddleLeft)
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0.5f);
        if (align == TextAnchor.MiddleRight)
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = anchored;
        rect.sizeDelta = box;
        var text = go.AddComponent<Text>();
        text.text = body;
        text.font = _font;
        text.fontSize = size;
        text.alignment = align;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);
        return text;
    }

    Button MakeButton(string name, string caption, Vector2 anchored, Vector2 size,
                      Transform parent = null)
    {
        if (parent == null) parent = transform;
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchored;
        rect.sizeDelta = size;
        var image = go.GetComponent<Image>();
        image.color = new Color(0.16f, 0.12f, 0.06f, 0.92f);
        var button = go.GetComponent<Button>();
        button.targetGraphic = image;
        MakeLabel("Label", caption, 28, TextAnchor.MiddleCenter,
            new Color(1f, 0.88f, 0.48f), Vector2.zero, size, go.transform);
        return button;
    }

    // ---- sections --------------------------------------------------------

    void BuildBackground()
    {
        var bg = new GameObject("Dim", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(transform, false);
        var rect = bg.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        var image = bg.GetComponent<Image>();
        image.color = new Color(0.035f, 0.030f, 0.045f, 0.92f);
        image.raycastTarget = true;
    }

    void BuildTitle()
    {
        MakeLabel("ShopTitle", "UPGRADE SHOP", 64, TextAnchor.MiddleCenter,
            new Color(1f, 0.83f, 0.32f),
            new Vector2(0f, 400f), new Vector2(1200f, 100f));
    }

    void BuildCoinDisplay()
    {
        _coinLabel = MakeLabel("CoinDisplay", "Coins: 0", 34, TextAnchor.MiddleCenter,
            new Color(1f, 0.86f, 0.25f),
            new Vector2(0f, 320f), new Vector2(600f, 50f));
    }

    void BuildUpgradeRows()
    {
        // Five rows starting at y = 220, stepping down 100 each
        float startY = 220f;
        float stepY = 100f;

        for (int i = 0; i < 5; i++)
        {
            var id = (UpgradeState.UpgradeId)i;
            float y = startY - i * stepY;

            // Row background
            var row = new GameObject("Row_" + i, typeof(RectTransform), typeof(Image));
            row.transform.SetParent(transform, false);
            var rowRect = row.GetComponent<RectTransform>();
            rowRect.anchorMin = rowRect.anchorMax = rowRect.pivot = new Vector2(0.5f, 0.5f);
            rowRect.anchoredPosition = new Vector2(0f, y);
            rowRect.sizeDelta = new Vector2(1000f, 80f);
            row.GetComponent<Image>().color = new Color(0.09f, 0.07f, 0.05f, 0.70f);
            row.GetComponent<Image>().raycastTarget = false;

            // Name — left-aligned in row
            MakeLabel("Name_" + i, UpgradeState.UpgradeName(id), 26, TextAnchor.MiddleLeft,
                new Color(0.95f, 0.92f, 0.86f),
                new Vector2(-460f, 0f), new Vector2(320f, 40f), row.transform);

            // Tier label — centre
            _tierLabels[i] = MakeLabel("Tier_" + i, "Tier 0/3", 22, TextAnchor.MiddleCenter,
                new Color(0.72f, 0.68f, 0.60f),
                new Vector2(-80f, 0f), new Vector2(140f, 34f), row.transform);

            // Cost label — right of tier
            _costLabels[i] = MakeLabel("Cost_" + i, "", 22, TextAnchor.MiddleCenter,
                new Color(1f, 0.86f, 0.25f),
                new Vector2(80f, 0f), new Vector2(140f, 34f), row.transform);

            // BUY button — far right
            _buyButtons[i] = MakeButton("BuyBtn_" + i, "BUY",
                new Vector2(350f, 0f), new Vector2(140f, 54f), row.transform);
            int captured = i;
            _buyButtons[i].onClick.AddListener(() =>
            {
                if (UpgradeState.Buy((UpgradeState.UpgradeId)captured))
                    RefreshRow((UpgradeState.UpgradeId)captured);
            });
        }
    }

    void BuildContinueButton()
    {
        var btn = MakeButton("ContinueBtn", "CONTINUE",
            new Vector2(0f, -360f), new Vector2(340f, 80f));
        btn.onClick.AddListener(() =>
        {
            if (_onContinue != null)
            {
                var cb = _onContinue;
                _onContinue = null;
                // Destroy self before invoking callback so the panel is gone
                // before the scene reload or whatever the callback does.
                Destroy(gameObject);
                cb();
            }
        });
    }

    // ---- per-frame refresh -----------------------------------------------

    void RefreshRow(UpgradeState.UpgradeId id)
    {
        int i = (int)id;
        int tier = UpgradeState.GetTier(id);
        bool maxed = UpgradeState.IsMaxed(id);
        int cost = UpgradeState.NextCost(id);

        if (_tierLabels[i] != null)
            _tierLabels[i].text = "Tier " + tier + "/3";

        if (_costLabels[i] != null)
        {
            if (maxed)
                _costLabels[i].text = "MAXED";
            else
                _costLabels[i].text = cost + " coins";
        }

        if (_buyButtons[i] != null)
        {
            bool canBuy = !maxed && UpgradeState.CanAfford(id);
            _buyButtons[i].interactable = canBuy;
            var img = _buyButtons[i].GetComponent<Image>();
            if (img != null)
            {
                if (maxed)
                    img.color = new Color(0.12f, 0.09f, 0.05f, 0.55f);
                else if (!canBuy)
                    img.color = new Color(0.10f, 0.08f, 0.04f, 0.50f);
                else
                    img.color = new Color(0.16f, 0.12f, 0.06f, 0.92f);
            }
            // Update button label colour
            var labels = _buyButtons[i].GetComponentsInChildren<Text>();
            foreach (var l in labels)
            {
                if (maxed || !canBuy)
                    l.color = new Color(0.50f, 0.45f, 0.40f);
                else
                    l.color = new Color(1f, 0.88f, 0.48f);
            }
        }
    }

    void Refresh()
    {
        for (int i = 0; i < 5; i++)
            RefreshRow((UpgradeState.UpgradeId)i);
    }
}