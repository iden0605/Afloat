using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Reads the TroopData list and builds the sidebar cards at runtime.
/// To add a new troop: create a TroopData asset and drag it into the Troops list here.
/// Must be on the same GameObject as the UIDocument.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class TroopSidebarController : MonoBehaviour
{
    [Tooltip("Combat troops shown in the top section. Order matches display order.")]
    [SerializeField] private List<TroopData> troops = new();

    [Tooltip("Powers (e.g. Lily Pad) shown in the bottom section. Order matches display order.")]
    [SerializeField] private List<TroopData> powers = new();

    private VisualElement _sidebar;
    private Button        _toggleBtn;
    private bool          _isOpen;

    // Detail overlay elements (built once, repopulated on show)
    private VisualElement _detailOverlay;
    private VisualElement _detailPortrait;
    private Label         _detailName;
    private Label         _detailCostBadge;
    private Label         _detailPlacementBadge;
    private Label         _detailAtkLabel;
    private Label         _detailSpdLabel;
    private Label         _detailRngLabel;
    private VisualElement _detailDescRow;
    private Label         _detailDesc;
    private VisualElement _detailEffectRow;
    private Label         _detailEffect;
    private VisualElement _detailUpgradesContainer;

    void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;

        _sidebar   = root.Q("sidebar-container");
        _toggleBtn = root.Q<Button>("toggle-btn");
        _toggleBtn.clicked += ToggleSidebar;

        BuildCards(root.Q("troop-list"));
        BuildDetailOverlay(root);
    }

    void OnDisable()
    {
        if (_toggleBtn != null) _toggleBtn.clicked -= ToggleSidebar;
    }

    // -------------------------------------------------------
    // Card building
    // -------------------------------------------------------

    void BuildCards(VisualElement list)
    {
        list.Clear();

        foreach (var data in troops)
        {
            if (data == null) continue;
            list.Add(MakeCard(data));
        }

        if (powers.Count > 0)
        {
            var divider = new VisualElement();
            divider.AddToClassList("sidebar-divider");
            list.Add(divider);

            var header = new Label("POWERS");
            header.AddToClassList("sidebar-section-label");
            list.Add(header);

            foreach (var data in powers)
            {
                if (data == null) continue;
                list.Add(MakeCard(data));
            }
        }
    }

    VisualElement MakeCard(TroopData data)
    {
        var card = new VisualElement();
        card.AddToClassList("troop-card");

        // Portrait thumbnail
        var portrait = new VisualElement();
        portrait.AddToClassList("troop-portrait");
        if (data.portrait != null)
            portrait.style.backgroundImage = new StyleBackground(data.portrait);

        // Troop name
        var nameLabel = new Label(data.troopName);
        nameLabel.AddToClassList("troop-name");

        // Cost label
        var costLabel = new Label($"{data.baseCost}g");
        costLabel.AddToClassList("troop-cost");

        // Info button — opens detail overlay
        var captured = data;
        var infoBtn = new Button(() => ShowDetailOverlay(captured)) { text = "?" };
        infoBtn.AddToClassList("troop-info-btn");

        card.Add(portrait);
        card.Add(nameLabel);
        card.Add(costLabel);
        card.Add(infoBtn);

        card.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button != 0) return;
            // Don't start drag if the info button was clicked
            if (evt.target is Button) return;
            evt.StopPropagation();
            TroopDragController.Instance.BeginNewDrag(captured);
        });

        return card;
    }

    // -------------------------------------------------------
    // Detail overlay — built once, shown per card click
    // -------------------------------------------------------

    void BuildDetailOverlay(VisualElement root)
    {
        // Full-screen backdrop
        _detailOverlay = new VisualElement();
        _detailOverlay.AddToClassList("detail-overlay");
        _detailOverlay.AddToClassList("detail-overlay--hidden");

        // Click backdrop to close
        _detailOverlay.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.target == _detailOverlay) HideDetailOverlay();
        });

        // Panel
        var panel = new VisualElement();
        panel.AddToClassList("detail-panel");

        // ── Header: portrait + name + close ─────────────────────
        var header = new VisualElement();
        header.AddToClassList("detail-header");

        _detailPortrait = new VisualElement();
        _detailPortrait.AddToClassList("detail-portrait");

        var titleBlock = new VisualElement();
        titleBlock.AddToClassList("detail-title-block");

        _detailName = new Label();
        _detailName.AddToClassList("detail-name");

        var badgesRow = new VisualElement();
        badgesRow.AddToClassList("detail-badges-row");

        _detailCostBadge = new Label();
        _detailCostBadge.AddToClassList("detail-badge");
        _detailCostBadge.AddToClassList("detail-badge--gold");

        _detailPlacementBadge = new Label();
        _detailPlacementBadge.AddToClassList("detail-badge");

        badgesRow.Add(_detailCostBadge);
        badgesRow.Add(_detailPlacementBadge);

        titleBlock.Add(_detailName);
        titleBlock.Add(badgesRow);

        var closeBtn = new Button(HideDetailOverlay) { text = "✕" };
        closeBtn.AddToClassList("detail-close-btn");

        header.Add(_detailPortrait);
        header.Add(titleBlock);
        header.Add(closeBtn);
        panel.Add(header);

        // ── Stats row ────────────────────────────────────────────
        panel.Add(MakeDetailDivider());

        var statsRow = new VisualElement();
        statsRow.AddToClassList("detail-stats-row");
        _detailAtkLabel = BuildDetailStatItem(statsRow, "ATK");
        _detailSpdLabel = BuildDetailStatItem(statsRow, "SPD");
        _detailRngLabel = BuildDetailStatItem(statsRow, "RNG");
        panel.Add(statsRow);

        // ── Description ──────────────────────────────────────────
        _detailDescRow = new VisualElement();
        _detailDescRow.AddToClassList("detail-desc-row");
        _detailDesc = new Label();
        _detailDesc.AddToClassList("detail-description");
        _detailDescRow.Add(_detailDesc);
        panel.Add(_detailDescRow);

        // ── Special ability ───────────────────────────────────────
        _detailEffectRow = new VisualElement();
        _detailEffectRow.AddToClassList("detail-effect-row");
        _detailEffect = new Label();
        _detailEffect.AddToClassList("detail-effect");
        _detailEffectRow.Add(_detailEffect);
        panel.Add(_detailEffectRow);

        // ── Upgrades ─────────────────────────────────────────────
        _detailUpgradesContainer = new VisualElement();
        _detailUpgradesContainer.AddToClassList("detail-upgrades-container");
        panel.Add(_detailUpgradesContainer);

        _detailOverlay.Add(panel);
        root.Add(_detailOverlay);
    }

    void ShowDetailOverlay(TroopData data)
    {
        // Portrait
        if (data.portrait != null)
            _detailPortrait.style.backgroundImage = new StyleBackground(data.portrait);

        // Name + badges
        _detailName.text          = data.troopName;
        _detailCostBadge.text     = $"{data.baseCost}g";
        _detailPlacementBadge.text = PlacementLabel(data.placementType);

        // Stats (base values shown since no instance available here)
        _detailAtkLabel.text = $"{data.attack:0.#}";
        _detailSpdLabel.text = $"{data.attackSpeed:0.#}/s";
        _detailRngLabel.text = $"{data.range:0.#}u";

        // Description
        bool hasDesc = !string.IsNullOrEmpty(data.description);
        _detailDesc.text = data.description;
        _detailDescRow.style.display = hasDesc ? DisplayStyle.Flex : DisplayStyle.None;

        // Effect
        string fx = EffectDescription(data.effectType);
        _detailEffect.text = string.IsNullOrEmpty(fx) ? "" : $"*  {fx}";
        _detailEffectRow.style.display = string.IsNullOrEmpty(fx) ? DisplayStyle.None : DisplayStyle.Flex;

        // Upgrades
        _detailUpgradesContainer.Clear();
        bool hasUpgrades = data.upgrades != null && data.upgrades.Length > 0;
        if (hasUpgrades)
        {
            var upHeader = new Label("UPGRADES");
            upHeader.AddToClassList("detail-upgrades-header");
            _detailUpgradesContainer.Add(upHeader);
            _detailUpgradesContainer.Add(MakeDetailDivider());

            for (int i = 0; i < data.upgrades.Length; i++)
            {
                var tier = data.upgrades[i];
                var row  = new VisualElement();
                row.AddToClassList("detail-upgrade-row");

                var tierLabel = new Label($"T{i + 1}");
                tierLabel.AddToClassList("detail-upgrade-tier");

                var descLabel = new Label(FormatUpgradeTier(tier));
                descLabel.AddToClassList("detail-upgrade-desc");

                var costLabel = new Label($"{tier.cost}g");
                costLabel.AddToClassList("detail-upgrade-cost");

                row.Add(tierLabel);
                row.Add(descLabel);
                row.Add(costLabel);
                _detailUpgradesContainer.Add(row);
            }
        }
        _detailUpgradesContainer.style.display = hasUpgrades ? DisplayStyle.Flex : DisplayStyle.None;

        _detailOverlay.RemoveFromClassList("detail-overlay--hidden");
    }

    void HideDetailOverlay()
    {
        _detailOverlay.AddToClassList("detail-overlay--hidden");
    }

    // Builds a detail stat item (value / name) and returns the value label
    Label BuildDetailStatItem(VisualElement parent, string statName)
    {
        var item = new VisualElement();
        item.AddToClassList("detail-stat-item");

        var valueLabel = new Label();
        valueLabel.AddToClassList("detail-stat-value");

        var nameLabel = new Label(statName);
        nameLabel.AddToClassList("detail-stat-name");

        item.Add(valueLabel);
        item.Add(nameLabel);
        parent.Add(item);
        return valueLabel;
    }

    static VisualElement MakeDetailDivider()
    {
        var d = new VisualElement();
        d.AddToClassList("sel-divider");
        return d;
    }

    // -------------------------------------------------------
    // Toggle
    // -------------------------------------------------------

    void ToggleSidebar()
    {
        _isOpen = !_isOpen;
        if (_isOpen)
        {
            _sidebar.AddToClassList("open");
            _toggleBtn.text = "\u2715"; // ✕
        }
        else
        {
            _sidebar.RemoveFromClassList("open");
            _toggleBtn.text = "\u2630"; // ☰
        }
    }

    // -------------------------------------------------------
    // Helpers
    // -------------------------------------------------------

    static string PlacementLabel(PlacementType t) => t switch
    {
        PlacementType.LandOnly     => "Land",
        PlacementType.WaterOnly    => "Water",
        PlacementType.LandAndWater => "Land & Water",
        _                          => ""
    };

    static string EffectDescription(TroopEffectType t) => t switch
    {
        TroopEffectType.DoubleGoldDrop        => "Enemies hit drop double gold",
        TroopEffectType.ConditionalAttackBuff => "Attack triples with multiple enemies in range",
        TroopEffectType.ConditionalSpeedBuff  => "Attack speed increases with only 1 enemy in range",
        TroopEffectType.AllyProximityBuff     => "Gains +0.5 ATK per nearby ally of the same type",
        _                                     => ""
    };

    static string FormatUpgradeTier(TroopData.UpgradeTier tier)
    {
        var parts = new List<string>();
        if (tier.attackDelta      != 0) parts.Add($"ATK {(tier.attackDelta > 0 ? "+" : "")}{tier.attackDelta:0.#}");
        if (tier.attackSpeedDelta != 0) parts.Add($"SPD {(tier.attackSpeedDelta > 0 ? "+" : "")}{tier.attackSpeedDelta:0.#}");
        if (tier.rangeDelta       != 0) parts.Add($"RNG {(tier.rangeDelta > 0 ? "+" : "")}{tier.rangeDelta:0.#}");

        string stats = string.Join(", ", parts);
        if (string.IsNullOrEmpty(tier.description)) return stats.Length > 0 ? stats : "Upgrade";
        return stats.Length > 0 ? $"{tier.description}  ({stats})" : tier.description;
    }
}
