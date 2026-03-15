using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class TroopSelectionUI : MonoBehaviour
{
    public static TroopSelectionUI Instance { get; private set; }

    private UIDocument    _uiDoc;
    private TroopInstance _target;
    private VisualElement _panel;

    // Portrait section
    private VisualElement _portraitEl;
    private Label         _troopNameLabel;
    private Label         _placementLabel;

    // Stats
    private Label _atkLabel;
    private Label _spdLabel;
    private Label _rngLabel;

    // Description / effect
    private Label         _descriptionLabel;
    private VisualElement _effectRow;
    private Label         _effectLabel;

    // Upgrade info
    private VisualElement _upgradeInfoRow;
    private Label         _upgradeTierLabel;
    private Label         _upgradeDescLabel;

    // Evolution section
    private VisualElement _evolutionSection;
    private Label         _evolutionNameLabel;
    private Label         _evolutionDescLabel;
    private Label         _evolutionBoostsLabel;
    private Label         _evolutionReqLabel;
    private Button        _evolveBtn;

    // Action buttons
    private Button _upgradeBtn;
    private Label  _upgradeCostLabel;
    private Label  _sellValueLabel;

    // Prevents LateUpdate from closing the panel on the same frame Show() was called
    private int _showFrame = -1;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _uiDoc = GetComponent<UIDocument>();
    }

    void OnEnable()
    {
        BuildPanel();
    }

    // -------------------------------------------------------
    // Public API
    // -------------------------------------------------------

    public void Show(TroopInstance troop)
    {
        // Toggle: clicking the same troop while open closes the sidebar
        if (_panel.ClassListContains("sel-open") && _target == troop)
        {
            Hide();
            return;
        }

        _target?.GetComponentInChildren<RangeIndicator>(true)?.SetVisible(false);

        _showFrame = Time.frameCount;
        _target    = troop;
        RefreshStatic();
        Refresh();
        _panel.AddToClassList("sel-open");

        var ind = _target.GetComponentInChildren<RangeIndicator>(true);
        if (ind != null) { ind.SetRadius(_target.CurrentRange); ind.SetVisible(true); }
    }

    public void Hide()
    {
        _target?.GetComponentInChildren<RangeIndicator>(true)?.SetVisible(false);
        _target = null;
        _panel?.RemoveFromClassList("sel-open");
    }

    // -------------------------------------------------------
    // Refresh
    // -------------------------------------------------------

    // Called once when a new troop is selected — populates content that never changes per-instance
    void RefreshStatic()
    {
        if (_target == null) return;
        var d = _target.Data;

        if (d.portrait != null)
            _portraitEl.style.backgroundImage = new StyleBackground(d.portrait);
        _troopNameLabel.text = d.troopName;
        _placementLabel.text = PlacementLabel(d.placementType);

        bool hasDesc = !string.IsNullOrEmpty(d.description);
        _descriptionLabel.text = d.description;
        _descriptionLabel.style.display = hasDesc ? DisplayStyle.Flex : DisplayStyle.None;

        string fx = EffectDescription(d.effectType);
        _effectLabel.text = string.IsNullOrEmpty(fx) ? "" : $"*  {fx}";
        _effectRow.style.display = string.IsNullOrEmpty(fx) ? DisplayStyle.None : DisplayStyle.Flex;

        // Evolution section visible only when evolutions exist
        _evolutionSection.style.display = d.HasEvolutions ? DisplayStyle.Flex : DisplayStyle.None;
    }

    // Called after every upgrade or evolve — updates stats, buttons, and evolution state
    public void Refresh()
    {
        if (_target == null) return;

        // Stats
        _atkLabel.text = $"{_target.CurrentAttack:0.#}";
        _spdLabel.text = $"{_target.CurrentAttackSpeed:0.#}/s";
        _rngLabel.text = $"{_target.CurrentRange:0.#}u";

        // Name: show current evolution name if evolved, otherwise base troop name
        // Portrait always stays as the original TroopData portrait (set once in RefreshStatic)
        if (_target.EvolutionLevel > 0)
        {
            var currentEvo = _target.Data.evolutions[_target.EvolutionLevel - 1];
            if (!string.IsNullOrEmpty(currentEvo.evolutionName))
                _troopNameLabel.text = currentEvo.evolutionName;
        }

        // Upgrade tier info
        int total = _target.Data.upgrades?.Length ?? 0;
        if (total > 0)
        {
            _upgradeTierLabel.text = $"Tier {_target.UpgradeLevel} / {total}";
            _upgradeDescLabel.text = _target.CanUpgrade
                ? "Next: " + FormatUpgradeTier(_target.Data.upgrades[_target.UpgradeLevel])
                : "Fully upgraded";
            _upgradeInfoRow.style.display = DisplayStyle.Flex;
        }
        else
        {
            _upgradeInfoRow.style.display = DisplayStyle.None;
        }

        // Upgrade button
        bool canUpgrade = _target.CanUpgrade;
        _upgradeBtn.SetEnabled(canUpgrade);
        _upgradeCostLabel.text = canUpgrade ? $"{_target.NextUpgradeCost}g" : "MAX";
        _sellValueLabel.text   = $"{_target.SellValue}g";

        // Evolution section — show next evolution in the chain, or "fully evolved"
        if (_target.Data.HasEvolutions)
        {
            var next = _target.NextEvolution; // null when fully evolved

            if (next == null)
            {
                // All evolutions done
                _evolutionNameLabel.text  = _target.Data.evolutions[^1].evolutionName;
                _evolutionDescLabel.style.display   = DisplayStyle.None;
                _evolutionBoostsLabel.style.display = DisplayStyle.None;
                _evolutionReqLabel.text = "✓  Fully evolved";
                _evolutionReqLabel.RemoveFromClassList("sel-evo-req--lacking");
                _evolutionReqLabel.RemoveFromClassList("sel-evo-req--ready");
                _evolutionReqLabel.AddToClassList("sel-evo-req--done");
                _evolveBtn.SetEnabled(false);
                _evolveBtn.RemoveFromClassList("sel-evolve-btn--ready");
            }
            else
            {
                // Show info for the next evolution
                _evolutionNameLabel.text = next.evolutionName;

                bool hasDesc = !string.IsNullOrEmpty(next.description);
                _evolutionDescLabel.text = next.description;
                _evolutionDescLabel.style.display = hasDesc ? DisplayStyle.Flex : DisplayStyle.None;

                string boosts = FormatEvoBoosts(next);
                _evolutionBoostsLabel.text = boosts;
                _evolutionBoostsLabel.style.display = boosts.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;

                int req = next.upgradesRequired;
                int cur = _target.UpgradeLevel;

                if (cur >= req)
                {
                    _evolutionReqLabel.text = "Ready to evolve!";
                    _evolutionReqLabel.RemoveFromClassList("sel-evo-req--lacking");
                    _evolutionReqLabel.RemoveFromClassList("sel-evo-req--done");
                    _evolutionReqLabel.AddToClassList("sel-evo-req--ready");
                    _evolveBtn.SetEnabled(true);
                    _evolveBtn.AddToClassList("sel-evolve-btn--ready");
                }
                else
                {
                    _evolutionReqLabel.text = $"Requires {req} upgrades  ({req - cur} more to go)";
                    _evolutionReqLabel.RemoveFromClassList("sel-evo-req--ready");
                    _evolutionReqLabel.RemoveFromClassList("sel-evo-req--done");
                    _evolutionReqLabel.AddToClassList("sel-evo-req--lacking");
                    _evolveBtn.SetEnabled(false);
                    _evolveBtn.RemoveFromClassList("sel-evolve-btn--ready");
                }
            }
        }
    }

    // -------------------------------------------------------
    // Panel construction
    // -------------------------------------------------------

    void BuildPanel()
    {
        _panel = new VisualElement();
        _panel.AddToClassList("sel-panel");

        // ── Top bar ───────────────────────────────────────────────
        var topBar = new VisualElement();
        topBar.AddToClassList("sel-topbar");

        var titleLabel = new Label("SELECTED");
        titleLabel.AddToClassList("sel-sidebar-title");

        var closeBtn = new Button(Hide) { text = "✕" };
        closeBtn.AddToClassList("sel-close-btn");

        topBar.Add(titleLabel);
        topBar.Add(closeBtn);
        _panel.Add(topBar);

        // ── Scrollable body ───────────────────────────────────────
        var scroll = new ScrollView();
        scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        scroll.verticalScrollerVisibility   = ScrollerVisibility.Auto;
        scroll.AddToClassList("sel-scroll");

        // Portrait + name + placement
        var portraitSection = new VisualElement();
        portraitSection.AddToClassList("sel-portrait-section");

        _portraitEl = new VisualElement();
        _portraitEl.AddToClassList("sel-portrait");

        _troopNameLabel = new Label();
        _troopNameLabel.AddToClassList("sel-troop-name");

        _placementLabel = new Label();
        _placementLabel.AddToClassList("sel-placement-badge");

        portraitSection.Add(_portraitEl);
        portraitSection.Add(_troopNameLabel);
        portraitSection.Add(_placementLabel);
        scroll.Add(portraitSection);

        // Stats row
        scroll.Add(MakeDivider());
        var statsRow = new VisualElement();
        statsRow.AddToClassList("sel-stats-row");
        _atkLabel = BuildStatItem(statsRow, "ATK");
        _spdLabel = BuildStatItem(statsRow, "SPD");
        _rngLabel = BuildStatItem(statsRow, "RNG");
        scroll.Add(statsRow);

        // Description
        scroll.Add(MakeDivider());
        _descriptionLabel = new Label();
        _descriptionLabel.AddToClassList("sel-description");
        scroll.Add(_descriptionLabel);

        // Special ability
        _effectRow = new VisualElement();
        _effectRow.AddToClassList("sel-effect-row");
        _effectLabel = new Label();
        _effectLabel.AddToClassList("sel-effect");
        _effectRow.Add(_effectLabel);
        scroll.Add(_effectRow);

        // Upgrade info
        scroll.Add(MakeDivider());
        _upgradeInfoRow = new VisualElement();
        _upgradeInfoRow.AddToClassList("sel-upgrade-info");

        _upgradeTierLabel = new Label();
        _upgradeTierLabel.AddToClassList("sel-tier-label");

        _upgradeDescLabel = new Label();
        _upgradeDescLabel.AddToClassList("sel-upgrade-desc");

        _upgradeInfoRow.Add(_upgradeTierLabel);
        _upgradeInfoRow.Add(_upgradeDescLabel);
        scroll.Add(_upgradeInfoRow);

        // ── Evolution section ─────────────────────────────────────
        _evolutionSection = new VisualElement();
        _evolutionSection.AddToClassList("sel-evolution-section");

        var evoHeaderRow = new VisualElement();
        evoHeaderRow.AddToClassList("sel-evo-header-row");

        var evoHeaderLabel = new Label("EVOLUTION");
        evoHeaderLabel.AddToClassList("sel-evo-header");

        _evolutionNameLabel = new Label();
        _evolutionNameLabel.AddToClassList("sel-evo-name");

        evoHeaderRow.Add(evoHeaderLabel);
        evoHeaderRow.Add(_evolutionNameLabel);
        _evolutionSection.Add(evoHeaderRow);

        _evolutionDescLabel = new Label();
        _evolutionDescLabel.AddToClassList("sel-evo-desc");
        _evolutionSection.Add(_evolutionDescLabel);

        _evolutionBoostsLabel = new Label();
        _evolutionBoostsLabel.AddToClassList("sel-evo-boosts");
        _evolutionSection.Add(_evolutionBoostsLabel);

        _evolutionReqLabel = new Label();
        _evolutionReqLabel.AddToClassList("sel-evo-req");
        _evolutionSection.Add(_evolutionReqLabel);

        _evolveBtn = new Button(OnEvolveClicked) { text = "EVOLVE" };
        _evolveBtn.AddToClassList("sel-evolve-btn");
        _evolutionSection.Add(_evolveBtn);

        scroll.Add(MakeDivider());
        scroll.Add(_evolutionSection);

        _panel.Add(scroll);

        // ── Actions bar — pinned to bottom ────────────────────────
        var actionsBar = new VisualElement();
        actionsBar.AddToClassList("sel-actions");

        var row = new VisualElement();
        row.AddToClassList("sel-row");

        // Upgrade
        var upgradeGroup = new VisualElement();
        upgradeGroup.AddToClassList("sel-group");
        _upgradeBtn = new Button(OnUpgradeClicked) { text = "▲" };
        _upgradeBtn.AddToClassList("sel-btn");
        _upgradeBtn.AddToClassList("sel-upgrade");
        _upgradeCostLabel = new Label("??g");
        _upgradeCostLabel.AddToClassList("sel-sub");
        upgradeGroup.Add(_upgradeBtn);
        upgradeGroup.Add(_upgradeCostLabel);

        // Move
        var moveGroup = new VisualElement();
        moveGroup.AddToClassList("sel-group");
        var moveBtn = new Button(OnMoveClicked) { text = "+" };
        moveBtn.AddToClassList("sel-btn");
        moveBtn.AddToClassList("sel-move");
        moveGroup.Add(moveBtn);

        // Sell
        var sellGroup = new VisualElement();
        sellGroup.AddToClassList("sel-group");
        var sellBtn = new Button(OnSellClicked) { text = "$" };
        sellBtn.AddToClassList("sel-btn");
        sellBtn.AddToClassList("sel-sell");
        _sellValueLabel = new Label("??g");
        _sellValueLabel.AddToClassList("sel-sub");
        sellGroup.Add(sellBtn);
        sellGroup.Add(_sellValueLabel);

        row.Add(upgradeGroup);
        row.Add(moveGroup);
        row.Add(sellGroup);
        actionsBar.Add(row);
        _panel.Add(actionsBar);

        _uiDoc.rootVisualElement.Add(_panel);
    }

    Label BuildStatItem(VisualElement parent, string statName)
    {
        var item = new VisualElement();
        item.AddToClassList("sel-stat-item");

        var valueLabel = new Label();
        valueLabel.AddToClassList("sel-stat-value");

        var nameLabel = new Label(statName);
        nameLabel.AddToClassList("sel-stat-name");

        item.Add(valueLabel);
        item.Add(nameLabel);
        parent.Add(item);
        return valueLabel;
    }

    static VisualElement MakeDivider()
    {
        var d = new VisualElement();
        d.AddToClassList("sel-divider");
        return d;
    }

    // -------------------------------------------------------
    // LateUpdate — click-outside-to-close
    // -------------------------------------------------------

    void LateUpdate()
    {
        if (!_panel.ClassListContains("sel-open")) return;
        if (!Input.GetMouseButtonDown(0)) return;
        if (Time.frameCount == _showFrame) return;

        var   root = _uiDoc.rootVisualElement;
        float px   = (Input.mousePosition.x / Screen.width)                    * root.resolvedStyle.width;
        float py   = ((Screen.height - Input.mousePosition.y) / Screen.height) * root.resolvedStyle.height;

        if (!_panel.worldBound.Contains(new Vector2(px, py)))
            Hide();
    }

    // -------------------------------------------------------
    // Button callbacks
    // -------------------------------------------------------

    void OnUpgradeClicked()
    {
        if (_target == null) return;
        _target.Upgrade();
        Refresh();
    }

    void OnEvolveClicked()
    {
        if (_target == null) return;
        var newInst = _target.Evolve();
        if (newInst != null)
            Show(newInst); // refreshes UI and moves range indicator to new instance
    }

    void OnMoveClicked()
    {
        if (_target == null) return;
        var t = _target;
        Hide();
        TroopDragController.Instance.BeginMoveDrag(t);
    }

    void OnSellClicked()
    {
        if (_target == null) return;
        Debug.Log($"[TroopSelectionUI] Sold {_target.Data.troopName} for {_target.SellValue}g");
        _target.Sell();
        Hide();
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

    static string FormatEvoBoosts(EvolutionData evo)
    {
        var parts = new List<string>();
        if (evo.attackBoost      != 0) parts.Add($"ATK +{evo.attackBoost:0.#}");
        if (evo.attackSpeedBoost != 0) parts.Add($"SPD +{evo.attackSpeedBoost:0.#}");
        if (evo.rangeBoost       != 0) parts.Add($"RNG +{evo.rangeBoost:0.#}");
        return string.Join("   ", parts);
    }
}
