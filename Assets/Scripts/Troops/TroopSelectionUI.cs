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

    // Next-upgrade delta hints shown beside each stat value
    private Label _atkDeltaLabel;
    private Label _spdDeltaLabel;
    private Label _rngDeltaLabel;

    // Description / effect
    private Label         _descriptionLabel;
    private VisualElement _effectRow;
    private VisualElement _tagsContainer;
    private Label         _effectLabel;

    // Next-upgrade effect preview (inline inside _upgradeInfoRow)
    private VisualElement _nextUpgradeTagsContainer;

    // Level badge in the top bar
    private Label _levelBadgeLabel;

    // Upgrade info
    private VisualElement _upgradeInfoRow;
    private Label         _upgradeTierLabel;
    private Label         _upgradeDescLabel;

    // Evolution — effect preview tags
    private VisualElement _evoEffectsContainer;

    // Evolution section
    private VisualElement _evolutionSection;
    private VisualElement _evolutionPortraitEl;
    private Label         _evolutionNameLabel;
    private Label         _evolutionBoostsLabel;
    private Label         _evolutionReqLabel;
    private Button        _evolveBtn;

    // Live buff display (updates every frame when panel is open)
    private VisualElement _liveBuffSection;
    private Label         _liveConditionalLabel;
    private Label         _liveRampingLabel;
    private Label         _liveFocusLabel;
    private Label         _liveColonyLabel;

    // Action buttons
    private Button _upgradeBtn;
    private Label  _upgradeCostLabel;
    private Label  _sellValueLabel;

    // Cached so Hide() can reach it even after it's been unparented from the troop
    private RangeIndicator _activeIndicator;

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
        GoldManager.OnGoldChanged += OnGoldChanged;
    }

    void OnDisable()
    {
        GoldManager.OnGoldChanged -= OnGoldChanged;
    }

    void OnGoldChanged(int _) => Refresh();

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

        _activeIndicator?.SetVisible(false);
        _activeIndicator = null;
        _target?.GetComponent<SpriteEffectsController>()?.HideOutline();

        _showFrame = Time.frameCount;
        _target    = troop;
        RefreshStatic();
        Refresh();
        _panel.AddToClassList("sel-open");

        var ind = _target.GetComponentInChildren<RangeIndicator>(true);
        if (ind != null)
        {
            var d = _target.Data;
            if (d != null && d.useRectangularRange)
                ind.SetRect(_target.CurrentRange, d.rangeRectWidth / 2f);
            else
                ind.SetRadius(_target.CurrentRange);

            var silhouette = _target.GetComponent<TroopHomeSilhouette>();
            Vector3 indicatorPos = silhouette != null ? silhouette.HomePosition : _target.transform.position;

            // Rect indicators must match the troop's orientation on the path
            Quaternion? indicatorRot = (d != null && d.useRectangularRange)
                ? (Quaternion?)_target.transform.rotation
                : null;

            ind.SetVisible(true, indicatorPos, indicatorRot);
        }
        _activeIndicator = ind;

        _target.GetComponent<SpriteEffectsController>()?.ShowSelectionOutline();
    }

    public void Hide()
    {
        _activeIndicator?.SetVisible(false);
        _activeIndicator = null;
        _target?.GetComponent<SpriteEffectsController>()?.HideOutline();
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
        if (d == null) return;

        if (d.portrait != null)
            _portraitEl.style.backgroundImage = new StyleBackground(d.portrait);
        _troopNameLabel.text = d.troopName;
        _placementLabel.text = PlacementLabel(d.placementType);

        bool hasDesc = !string.IsNullOrEmpty(d.description);
        _descriptionLabel.text = d.description;
        _descriptionLabel.style.display = hasDesc ? DisplayStyle.Flex : DisplayStyle.None;

        // Build skill tags: base effect + any upgrade/evo effects at current level
        _effectRow.style.display = RefreshEffectTags() ? DisplayStyle.Flex : DisplayStyle.None;

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

        // Level badge
        int  totalTiers  = _target.Data?.upgrades?.Length ?? 0;
        bool fullyUpgraded = totalTiers > 0 && _target.UpgradeLevel >= totalTiers;
        _levelBadgeLabel.text = totalTiers > 0
            ? $"T{_target.UpgradeLevel}"
            : "BASE";
        _levelBadgeLabel.EnableInClassList("sel-level-badge--max", fullyUpgraded);

        // Next-upgrade delta hints beside each stat
        if (_target.CanUpgrade)
        {
            var tier = _target.Data.upgrades[_target.UpgradeLevel];
            SetDeltaLabel(_atkDeltaLabel, tier.attackDelta);
            SetDeltaLabel(_spdDeltaLabel, tier.attackSpeedDelta);
            SetDeltaLabel(_rngDeltaLabel, tier.rangeDelta);
        }
        else
        {
            _atkDeltaLabel.style.display = DisplayStyle.None;
            _spdDeltaLabel.style.display = DisplayStyle.None;
            _rngDeltaLabel.style.display = DisplayStyle.None;
        }

        // Effects (rebuild on every refresh — upgrade/evo may change them)
        _effectRow.style.display = RefreshEffectTags() ? DisplayStyle.Flex : DisplayStyle.None;

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
        bool evolutionGate = _target.EvolutionGateActive;
        if (total > 0)
        {
            if (evolutionGate)
                _upgradeTierLabel.text = $"TIER {_target.UpgradeLevel} / {total}  — EVOLVE TO CONTINUE";
            else if (_target.CanUpgrade)
                _upgradeTierLabel.text = $"TIER {_target.UpgradeLevel} / {total}  — NEXT UPGRADE";
            else
                _upgradeTierLabel.text = $"TIER {_target.UpgradeLevel} / {total}  ✓  FULLY UPGRADED";

            if (_target.CanUpgrade)
            {
                string desc = _target.Data.upgrades[_target.UpgradeLevel].description;
                bool hasDesc = !string.IsNullOrEmpty(desc);
                _upgradeDescLabel.text = hasDesc ? desc : "";
                _upgradeDescLabel.style.display = hasDesc ? DisplayStyle.Flex : DisplayStyle.None;
            }
            else
            {
                _upgradeDescLabel.style.display = DisplayStyle.None;
            }

            _upgradeInfoRow.style.display = DisplayStyle.Flex;
        }
        else
        {
            _upgradeInfoRow.style.display = DisplayStyle.None;
        }

        // Upgrade button — disabled if maxed, gate active, OR can't afford
        bool canUpgrade      = _target.CanUpgrade;
        bool canAffordUpgrade = GoldManager.Instance?.CanAfford(_target.NextUpgradeCost) ?? true;
        _upgradeBtn.SetEnabled(canUpgrade && canAffordUpgrade);
        _upgradeBtn.EnableInClassList("sel-btn--unaffordable", canUpgrade && !canAffordUpgrade);
        _upgradeCostLabel.text = evolutionGate ? "EVOLVE\nFIRST" : canUpgrade ? $"{_target.NextUpgradeCost}g" : "MAX";
        _sellValueLabel.text   = $"{_target.SellValue}g";

        // Evolution section — show next evolution in the chain, or "fully evolved"
        if (_target.Data.HasEvolutions)
        {
            var next = _target.NextEvolution; // null when fully evolved

            if (next == null)
            {
                // All evolutions done — show the final evolution's portrait
                var finalEvo = _target.Data.evolutions[^1];
                _evolutionNameLabel.text  = finalEvo.evolutionName;
                SetEvoPortrait(finalEvo.portrait);
                _evolutionBoostsLabel.style.display = DisplayStyle.None;
                _evoEffectsContainer.style.display  = DisplayStyle.None;
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
                SetEvoPortrait(next.portrait);

                // Evolution effect preview tags
                _evoEffectsContainer.Clear();
                bool hasEvoEffects = false;
                if (next.effects != null)
                {
                    foreach (var cfg in next.effects)
                    {
                        if (cfg == null || cfg.effectType == TroopEffectType.None) continue;
                        string tagName = TroopEffectTagName(cfg);
                        if (string.IsNullOrEmpty(tagName)) continue;
                        hasEvoEffects = true;
                        var tag = new Label(tagName);
                        tag.AddToClassList("skill-tag");
                        tag.AddToClassList(TroopEffectTagClass(cfg));
                        tag.AddToClassList("skill-tag--preview");
                        _evoEffectsContainer.Add(tag);
                    }
                }
                _evoEffectsContainer.style.display = hasEvoEffects ? DisplayStyle.Flex : DisplayStyle.None;

                string boosts = FormatEvoBoosts(next);
                _evolutionBoostsLabel.text = boosts;
                _evolutionBoostsLabel.style.display = boosts.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;

                int req = next.upgradesRequired;
                int cur = _target.UpgradeLevel;

                bool canAffordEvo = GoldManager.Instance?.CanAfford(next.evolutionCost) ?? true;

                if (cur >= req)
                {
                    _evolutionReqLabel.text = canAffordEvo
                        ? $"Ready to evolve!  ({next.evolutionCost}g)"
                        : $"Ready to evolve  — need {next.evolutionCost}g";
                    _evolutionReqLabel.RemoveFromClassList("sel-evo-req--lacking");
                    _evolutionReqLabel.RemoveFromClassList("sel-evo-req--done");
                    _evolutionReqLabel.AddToClassList("sel-evo-req--ready");
                    _evolveBtn.SetEnabled(canAffordEvo);
                    _evolveBtn.EnableInClassList("sel-evolve-btn--ready", canAffordEvo);
                    _evolveBtn.EnableInClassList("sel-btn--unaffordable", !canAffordEvo);
                }
                else
                {
                    _evolutionReqLabel.text = $"Requires {req} upgrades  ({req - cur} more to go)";
                    _evolutionReqLabel.RemoveFromClassList("sel-evo-req--ready");
                    _evolutionReqLabel.RemoveFromClassList("sel-evo-req--done");
                    _evolutionReqLabel.AddToClassList("sel-evo-req--lacking");
                    _evolveBtn.SetEnabled(false);
                    _evolveBtn.RemoveFromClassList("sel-evolve-btn--ready");
                    _evolveBtn.RemoveFromClassList("sel-btn--unaffordable");
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

        _levelBadgeLabel = new Label();
        _levelBadgeLabel.AddToClassList("sel-level-badge");

        var closeBtn = new Button(Hide) { text = "✕" };
        closeBtn.AddToClassList("sel-close-btn");

        topBar.Add(titleLabel);
        topBar.Add(_levelBadgeLabel);
        topBar.Add(closeBtn);
        _panel.Add(topBar);

        // ── Scrollable body ───────────────────────────────────────
        var scroll = new ScrollView();
        scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        scroll.verticalScrollerVisibility   = ScrollerVisibility.Auto;
        scroll.AddToClassList("sel-scroll");

        // Portrait + name + placement (horizontal row)
        var portraitSection = new VisualElement();
        portraitSection.AddToClassList("sel-portrait-section");
        portraitSection.AddToClassList("sel-portrait-section--troop");

        _portraitEl = new VisualElement();
        _portraitEl.AddToClassList("sel-portrait");

        var identityCol = new VisualElement();
        identityCol.AddToClassList("sel-identity-col");

        _troopNameLabel = new Label();
        _troopNameLabel.AddToClassList("sel-troop-name");

        _placementLabel = new Label();
        _placementLabel.AddToClassList("sel-placement-badge");

        identityCol.Add(_troopNameLabel);
        identityCol.Add(_placementLabel);

        portraitSection.Add(_portraitEl);
        portraitSection.Add(identityCol);
        scroll.Add(portraitSection);

        // Stats row
        scroll.Add(MakeDivider());
        var statsRow = new VisualElement();
        statsRow.AddToClassList("sel-stats-row");
        _atkLabel = BuildStatItem(statsRow, "ATK", out _atkDeltaLabel);
        _spdLabel = BuildStatItem(statsRow, "SPD", out _spdDeltaLabel);
        _rngLabel = BuildStatItem(statsRow, "RNG", out _rngDeltaLabel);
        scroll.Add(statsRow);

        // Description
        scroll.Add(MakeDivider());
        _descriptionLabel = new Label();
        _descriptionLabel.AddToClassList("sel-description");
        scroll.Add(_descriptionLabel);

        // Special ability — tags row + description text
        _effectRow = new VisualElement();
        _effectRow.AddToClassList("sel-effect-row");
        _tagsContainer = new VisualElement();
        _tagsContainer.AddToClassList("skill-tags-row");
        _effectRow.Add(_tagsContainer);
        _effectLabel = new Label();
        _effectLabel.AddToClassList("sel-effect");
        _effectRow.Add(_effectLabel);
        scroll.Add(_effectRow);

        // Live buff status (War Frenzy / Rampage — updated per-frame)
        _liveBuffSection = new VisualElement();
        _liveBuffSection.AddToClassList("sel-live-buff-section");
        _liveConditionalLabel = new Label();
        _liveConditionalLabel.AddToClassList("sel-live-buff-label");
        _liveRampingLabel = new Label();
        _liveRampingLabel.AddToClassList("sel-live-buff-label");
        _liveFocusLabel = new Label();
        _liveFocusLabel.AddToClassList("sel-live-buff-label");
        _liveFocusLabel.AddToClassList("sel-live-buff-label--focus");
        _liveColonyLabel = new Label();
        _liveColonyLabel.AddToClassList("sel-live-buff-label");
        _liveColonyLabel.AddToClassList("sel-live-buff-label--colony");
        _liveBuffSection.Add(_liveConditionalLabel);
        _liveBuffSection.Add(_liveRampingLabel);
        _liveBuffSection.Add(_liveFocusLabel);
        _liveBuffSection.Add(_liveColonyLabel);
        _liveBuffSection.style.display = DisplayStyle.None;
        scroll.Add(_liveBuffSection);

        // Upgrade info + next-upgrade preview (combined)
        scroll.Add(MakeDivider());
        _upgradeInfoRow = new VisualElement();
        _upgradeInfoRow.AddToClassList("sel-upgrade-info");

        _upgradeTierLabel = new Label();
        _upgradeTierLabel.AddToClassList("sel-tier-label");

        _upgradeDescLabel = new Label();
        _upgradeDescLabel.AddToClassList("sel-upgrade-desc");

        _nextUpgradeTagsContainer = new VisualElement();
        _nextUpgradeTagsContainer.AddToClassList("skill-tags-row");
        _nextUpgradeTagsContainer.AddToClassList("sel-upgrade-preview-tags");

        _upgradeInfoRow.Add(_upgradeTierLabel);
        _upgradeInfoRow.Add(_upgradeDescLabel);
        _upgradeInfoRow.Add(_nextUpgradeTagsContainer);
        scroll.Add(_upgradeInfoRow);

        // ── Evolution section ─────────────────────────────────────
        _evolutionSection = new VisualElement();
        _evolutionSection.AddToClassList("sel-evolution-section");

        var evoHeaderLabel = new Label("EVOLUTION");
        evoHeaderLabel.AddToClassList("sel-evo-header");
        _evolutionSection.Add(evoHeaderLabel);

        // Portrait | name + boosts column (horizontal)
        var evoIdentityRow = new VisualElement();
        evoIdentityRow.AddToClassList("sel-evo-identity-row");

        _evolutionPortraitEl = new VisualElement();
        _evolutionPortraitEl.AddToClassList("sel-evo-portrait");

        var evoTextCol = new VisualElement();
        evoTextCol.AddToClassList("sel-evo-text-col");

        _evolutionNameLabel = new Label();
        _evolutionNameLabel.AddToClassList("sel-evo-name");

        _evolutionBoostsLabel = new Label();
        _evolutionBoostsLabel.AddToClassList("sel-evo-boosts");

        evoTextCol.Add(_evolutionNameLabel);
        evoTextCol.Add(_evolutionBoostsLabel);
        evoIdentityRow.Add(_evolutionPortraitEl);
        evoIdentityRow.Add(evoTextCol);
        _evolutionSection.Add(evoIdentityRow);

        _evoEffectsContainer = new VisualElement();
        _evoEffectsContainer.AddToClassList("skill-tags-row");
        _evoEffectsContainer.AddToClassList("sel-upgrade-preview-tags");
        _evolutionSection.Add(_evoEffectsContainer);

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
        row.Add(sellGroup);
        actionsBar.Add(row);
        _panel.Add(actionsBar);

        _uiDoc.rootVisualElement.Add(_panel);
    }

    Label BuildStatItem(VisualElement parent, string statName, out Label deltaLabel)
    {
        var item = new VisualElement();
        item.AddToClassList("sel-stat-item");

        var valueLabel = new Label();
        valueLabel.AddToClassList("sel-stat-value");

        deltaLabel = new Label();
        deltaLabel.AddToClassList("sel-stat-delta");
        deltaLabel.style.display = DisplayStyle.None;

        var nameLabel = new Label(statName);
        nameLabel.AddToClassList("sel-stat-name");

        item.Add(valueLabel);
        item.Add(deltaLabel);
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

    static void SetDeltaLabel(Label lbl, float delta)
    {
        if (Mathf.Approximately(delta, 0f)) { lbl.style.display = DisplayStyle.None; return; }
        lbl.text = delta > 0f ? $"+{delta:0.#}" : $"{delta:0.#}";
        lbl.style.display = DisplayStyle.Flex;
    }

    // -------------------------------------------------------
    // Update — live buff display
    // -------------------------------------------------------

    void Update()
    {
        if (_target == null || !_panel.ClassListContains("sel-open"))
        {
            if (_liveBuffSection != null)
                _liveBuffSection.style.display = DisplayStyle.None;
            return;
        }
        RefreshLiveBuff();
    }

    void RefreshLiveBuff()
    {
        var behavior   = _target.GetComponent<TroopBehavior>();
        int inRange    = behavior != null ? behavior.EnemiesInRange : 0;
        bool any       = false;

        // War Frenzy — ConditionalAttackBuff
        var condCfg = _target.GetEffectConfig(TroopEffectType.ConditionalAttackBuff);
        if (condCfg != null && condCfg.conditionalAttack > 0)
        {
            any = true;
            int   extra = Mathf.Min(Mathf.Max(0, inRange - 1), (int)condCfg.rampingMaxStacks);
            float bonus = condCfg.conditionalAttack * extra;
            _liveConditionalLabel.text = bonus > 0
                ? $"⚔  WAR FRENZY   +{bonus:0.#} ATK   ({inRange} enemies)"
                : $"⚔  WAR FRENZY   no bonus   ({inRange} in range)";
            _liveConditionalLabel.style.display = DisplayStyle.Flex;
        }
        else
        {
            _liveConditionalLabel.style.display = DisplayStyle.None;
        }

        // Rampage — RampingDoubleBuff
        var rampCfg = _target.GetEffectConfig(TroopEffectType.RampingDoubleBuff);
        if (rampCfg != null)
        {
            any = true;
            int stacks = _target.RampingStackCount;
            int max    = (int)rampCfg.rampingMaxStacks;
            _liveRampingLabel.text = stacks > 0
                ? $"⚡  RAMPAGE   {stacks} / {max} stacks   ({(int)Mathf.Pow(2f, stacks)}× ATK & SPD)"
                : $"⚡  RAMPAGE   0 / {max} stacks   (idle)";
            _liveRampingLabel.style.display = DisplayStyle.Flex;
        }
        else
        {
            _liveRampingLabel.style.display = DisplayStyle.None;
        }

        // Colony / Swarm — AllyProximityBuff & AllySpeedBuff
        var proxyCfg  = _target.GetEffectConfig(TroopEffectType.AllyProximityBuff);
        var swarmCfg  = _target.GetEffectConfig(TroopEffectType.AllySpeedBuff);
        var colonyCfg = proxyCfg ?? swarmCfg;
        if (colonyCfg != null && colonyCfg.allyBonus > 0)
        {
            any = true;
            int allies = _target.NearbyAllyCount;
            bool isSwarm = swarmCfg != null && proxyCfg == null;
            if (isSwarm)
            {
                float spdBonus = colonyCfg.allyBonus * allies;
                _liveColonyLabel.text = allies > 0
                    ? $"🐜  SWARM   {allies} allies nearby   (+{spdBonus:0.##}/s SPD)"
                    : $"🐜  SWARM   no allies in range   (no bonus)";
            }
            else
            {
                float atkBonus = colonyCfg.allyBonus * allies;
                _liveColonyLabel.text = allies > 0
                    ? $"🐜  COLONY   {allies} allies nearby   (+{atkBonus:0.#} ATK)"
                    : $"🐜  COLONY   no allies in range   (no bonus)";
            }
            _liveColonyLabel.EnableInClassList("sel-live-buff-label--colony-active", allies > 0);
            _liveColonyLabel.style.display = DisplayStyle.Flex;
        }
        else
        {
            _liveColonyLabel.style.display = DisplayStyle.None;
        }

        // Focus Strike — ConditionalSpeedBuff
        var focusCfg = _target.GetEffectConfig(TroopEffectType.ConditionalSpeedBuff);
        if (focusCfg != null && focusCfg.conditionalSpeed > 0)
        {
            any = true;
            bool focused    = inRange == 1;
            float rate      = focused ? focusCfg.conditionalSpeed : _target.CurrentAttackSpeed;
            _liveFocusLabel.text = focused
                ? $"🎯  FOCUS STRIKE   {rate:0.##}/s   (focused!)"
                : inRange == 0
                    ? $"🎯  FOCUS STRIKE   {rate:0.##}/s   (no target)"
                    : $"🎯  FOCUS STRIKE   {rate:0.##}/s   ({inRange} in range)";
            _liveFocusLabel.EnableInClassList("sel-live-buff-label--focus-active", focused);
            _liveFocusLabel.style.display = DisplayStyle.Flex;
        }
        else
        {
            _liveFocusLabel.style.display = DisplayStyle.None;
        }

        _liveBuffSection.style.display = any ? DisplayStyle.Flex : DisplayStyle.None;
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
        if (!_target.Upgrade()) return;

        // Refresh range indicator immediately so the circle updates without deselecting
        if (_activeIndicator != null)
        {
            var d = _target.Data;
            if (d != null && d.useRectangularRange)
                _activeIndicator.SetRect(_target.CurrentRange, d.rangeRectWidth / 2f);
            else
                _activeIndicator.SetRadius(_target.CurrentRange);
        }

        // Particle burst at the troop's world position
        UpgradeVFX.Play(_target.transform.position);

        // Brief gold flash on the upgrade button
        _upgradeBtn.AddToClassList("sel-btn--upgraded");
        _upgradeBtn.schedule.Execute(
            () => _upgradeBtn.RemoveFromClassList("sel-btn--upgraded"))
            .StartingIn(380);

        Refresh();
    }

    void OnEvolveClicked()
    {
        if (_target == null || !_target.CanEvolve) return;

        var evo = _target.NextEvolution;
        if (!(GoldManager.Instance?.CanAfford(evo.evolutionCost) ?? true)) return;

        // Determine the "from" portrait: current evolution's portrait, or base portrait
        Sprite fromSprite = _target.EvolutionLevel > 0
            ? (_target.Data.evolutions[_target.EvolutionLevel - 1].portrait ?? _target.Data.portrait)
            : _target.Data.portrait;
        Sprite toSprite = evo.portrait ?? fromSprite;

        // Key uniquely identifies this evolution type across all instances of this troop.
        // e.g. "AntData_AntQueen" — same key means the cutscene already played once.
        string evolutionKey = $"{_target.Data.name}_{evo.evolutionName}";

        Vector3 worldPos = _target.transform.position;
        var     captured = _target;

        Hide(); // close panel while cutscene plays

        EvolveCutscene.Play(fromSprite, toSprite, worldPos, evolutionKey, () =>
        {
            // Evolve() spends gold, swaps prefab, returns the new TroopInstance
            var newInst = captured.Evolve();
            if (newInst != null)
                Show(newInst);
        });
    }

    void OnSellClicked()
    {
        if (_target == null) return;
        _target.Sell(); // adds SellValue gold and destroys the troop
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
        PlacementType.PathOnly     => "Enemy Path",
        _                          => ""
    };

    /// <summary>
    /// Clears and repopulates the skill-tag pills and description text for the current target.
    /// Also populates the next-upgrade preview row.
    /// Returns true if any active or upcoming effects were found.
    /// </summary>
    bool RefreshEffectTags()
    {
        _tagsContainer.Clear();
        _nextUpgradeTagsContainer.Clear();
        if (_target == null) { _nextUpgradeTagsContainer.style.display = DisplayStyle.None; return false; }

        var descSb = new System.Text.StringBuilder();
        bool any = false;

        // ── Active effects (deduplicated by effectType) ────────────────────────
        var seenTypes = new System.Collections.Generic.HashSet<TroopEffectType>();
        foreach (var cfg in _target.ActiveEffects)
        {
            string tagName = TroopEffectTagName(cfg);
            if (string.IsNullOrEmpty(tagName)) continue;

            // Only show the first config per effect type to avoid visual duplicates
            if (!seenTypes.Add(cfg.effectType)) continue;

            any = true;
            var tag = new Label(tagName);
            tag.AddToClassList("skill-tag");
            tag.AddToClassList(TroopEffectTagClass(cfg));
            _tagsContainer.Add(tag);

            string desc = TroopEffectDescription(cfg);
            if (!string.IsNullOrEmpty(desc))
            {
                if (descSb.Length > 0) descSb.Append('\n');
                descSb.Append(desc);
            }
        }

        // ── Axolotl ricochet (custom mechanic, not in the effect system) ─────────
        var axolotlAtk = _target.GetComponent<AxolotlWaterBallAttack>();
        if (axolotlAtk != null)
        {
            any = true;
            int bounces = axolotlAtk.CurrentBounces;
            var tag = new Label($"RICOCHET {bounces}×");
            tag.AddToClassList("skill-tag");
            tag.AddToClassList("skill-tag--teal");
            _tagsContainer.Add(tag);

            if (descSb.Length > 0) descSb.Append('\n');
            descSb.Append($"Ball ricochets to {bounces} enemies per shot, slowing each");
        }

        _effectLabel.text = descSb.ToString();
        _effectLabel.style.display = descSb.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;

        // ── Next-upgrade preview: show effects the player doesn't have yet ──────
        bool hasPreview = false;

        // Ricochet count preview — checked independently of effect tier data
        if (axolotlAtk != null && _target.CanUpgrade)
        {
            int curBounces = axolotlAtk.CurrentBounces;
            int nxtBounces = AxolotlWaterBallAttack.BouncesForUpgradeLevel(_target.UpgradeLevel + 1);
            if (nxtBounces != curBounces)
            {
                hasPreview = true;
                var previewTag = new Label($"RICOCHET {nxtBounces}× ↑");
                previewTag.AddToClassList("skill-tag");
                previewTag.AddToClassList("skill-tag--teal");
                previewTag.AddToClassList("skill-tag--preview");
                _nextUpgradeTagsContainer.Add(previewTag);
            }
        }

        int nextTierIndex = _target.UpgradeLevel; // 0-based: upgrades[UpgradeLevel] is the NEXT tier
        var upgrades = _target.Data?.upgrades;
        if (upgrades != null && nextTierIndex < upgrades.Length)
        {
            var nextEffects = upgrades[nextTierIndex].effects;
            if (nextEffects != null)
            {
                foreach (var cfg in nextEffects)
                {
                    if (cfg == null || cfg.effectType == TroopEffectType.None) continue;

                    string tagName = TroopEffectTagName(cfg);
                    if (string.IsNullOrEmpty(tagName)) continue;

                    if (seenTypes.Contains(cfg.effectType))
                    {
                        // Already active — only preview if values are changing
                        var activeCfg = _target.GetEffectConfig(cfg.effectType);
                        if (activeCfg == null || !EffectConfigValuesChanged(activeCfg, cfg)) continue;
                        tagName += " ↑";
                    }

                    hasPreview = true;
                    var tag = new Label(tagName);
                    tag.AddToClassList("skill-tag");
                    tag.AddToClassList(TroopEffectTagClass(cfg));
                    tag.AddToClassList("skill-tag--preview");
                    _nextUpgradeTagsContainer.Add(tag);
                }
            }
        }
        _nextUpgradeTagsContainer.style.display = hasPreview ? DisplayStyle.Flex : DisplayStyle.None;

        return any || hasPreview;
    }

    static bool EffectConfigValuesChanged(TroopEffectConfig a, TroopEffectConfig b)
    {
        return !Mathf.Approximately(a.conditionalAttack, b.conditionalAttack)
            || !Mathf.Approximately(a.conditionalSpeed,  b.conditionalSpeed)
            || !Mathf.Approximately(a.rampingDuration,   b.rampingDuration)
            || !Mathf.Approximately(a.rampingMaxStacks,  b.rampingMaxStacks)
            || !Mathf.Approximately(a.dotDamage,         b.dotDamage)
            || !Mathf.Approximately(a.dotDuration,       b.dotDuration)
            || !Mathf.Approximately(a.freezeDuration,    b.freezeDuration)
            || !Mathf.Approximately(a.stunDuration,      b.stunDuration)
            || !Mathf.Approximately(a.goldMultiplier,    b.goldMultiplier)
            || !Mathf.Approximately(a.allyBonus,         b.allyBonus);
    }

    static string TroopEffectTagName(TroopEffectConfig cfg) => cfg.effectType switch
    {
        TroopEffectType.DoubleGoldDrop        => "2× GOLD",
        TroopEffectType.BurnOnHit             => "BURN",
        TroopEffectType.PoisonOnHit           => "POISON",
        TroopEffectType.PoisonSplash          => "POISON SPLASH",
        TroopEffectType.FreezeOnHit           => "FREEZE",
        TroopEffectType.StunOnHit             => "STUN",
        TroopEffectType.DazeOnHit             => "DAZE",
        TroopEffectType.ConditionalAttackBuff => "WAR FRENZY",
        TroopEffectType.ConditionalSpeedBuff  => "FOCUS STRIKE",
        TroopEffectType.DoubleEveryFourth     => "DOUBLE HIT",
        TroopEffectType.RampingDoubleBuff     => "RAMPAGE",
        TroopEffectType.AllyProximityBuff     => "COLONY",
        TroopEffectType.AllySpeedBuff         => "PACK RUSH",
        TroopEffectType.PiercingShot          => "PIERCING",
        _                                     => ""
    };

    static string TroopEffectTagClass(TroopEffectConfig cfg) => cfg.effectType switch
    {
        TroopEffectType.DoubleGoldDrop        => "skill-tag--gold",
        TroopEffectType.BurnOnHit             => "skill-tag--fire",
        TroopEffectType.PoisonOnHit           => "skill-tag--poison",
        TroopEffectType.PoisonSplash          => "skill-tag--poison",
        TroopEffectType.FreezeOnHit           => "skill-tag--freeze",
        TroopEffectType.StunOnHit             => "skill-tag--stun",
        TroopEffectType.DazeOnHit             => "skill-tag--daze",
        TroopEffectType.ConditionalAttackBuff => "skill-tag--power",
        TroopEffectType.ConditionalSpeedBuff  => "skill-tag--teal",
        TroopEffectType.DoubleEveryFourth     => "skill-tag--power",
        TroopEffectType.RampingDoubleBuff     => "skill-tag--power",
        TroopEffectType.AllyProximityBuff     => "skill-tag--support",
        TroopEffectType.AllySpeedBuff         => "skill-tag--support",
        TroopEffectType.PiercingShot          => "skill-tag--teal",
        _                                     => "skill-tag--power"
    };

    static string TroopEffectDescription(TroopEffectConfig cfg) => cfg.effectType switch
    {
        TroopEffectType.DoubleGoldDrop        => $"Enemies hit drop {cfg.goldMultiplier:0.#}× gold on death",
        TroopEffectType.BurnOnHit             => cfg.dotDuration > 0
                                                    ? $"Burns for 20% ATK dmg every {cfg.dotInterval:0.#}s over {cfg.dotDuration:0.#}s (stackable)"
                                                    : $"Burns for 20% ATK dmg every {cfg.dotInterval:0.#}s (stackable)",
        TroopEffectType.PoisonOnHit           => cfg.dotDuration > 0
                                                    ? $"Poisons for 20% ATK dmg every {cfg.dotInterval:0.#}s over {cfg.dotDuration:0.#}s (stackable)"
                                                    : $"Poisons for 20% ATK dmg every {cfg.dotInterval:0.#}s (stackable)",
        TroopEffectType.PoisonSplash          => $"Splash attack + poisons for 20% ATK dmg every {cfg.dotInterval:0.#}s",
        TroopEffectType.FreezeOnHit           => $"Slows to {cfg.freezeSlowFactor * 100:0}% speed for {cfg.freezeDuration:0.#}s",
        TroopEffectType.StunOnHit             => $"Stuns enemies for {cfg.stunDuration:0.#}s",
        TroopEffectType.DazeOnHit             => $"Dazes enemies for {cfg.stunDuration:0.#}s — sends them stumbling backwards",
        TroopEffectType.ConditionalAttackBuff => $"+{cfg.conditionalAttack:0.#} ATK per extra enemy in range (max {cfg.rampingMaxStacks})",
        TroopEffectType.ConditionalSpeedBuff  => $"Attack speed → {cfg.conditionalSpeed:0.##}/s when focused on a single enemy",
        TroopEffectType.DoubleEveryFourth     => DoubleHitDesc(cfg),
        TroopEffectType.RampingDoubleBuff     => $"Each hit doubles ATK & SPD for {cfg.rampingDuration:0.#}s (max {cfg.rampingMaxStacks} stacks)",
        TroopEffectType.AllyProximityBuff     => $"+{cfg.allyBonus:0.#} ATK per nearby same-type ally",
        TroopEffectType.AllySpeedBuff         => $"+{cfg.allyBonus:0.#} SPD per nearby same-type ally",
        TroopEffectType.PiercingShot          => cfg.rampingMaxStacks > 1
                                                    ? $"Projectile pierces through {cfg.rampingMaxStacks} enemies"
                                                    : "Projectile pierces through enemies",
        _                                     => ""
    };

    static string DoubleHitDesc(TroopEffectConfig cfg)
    {
        int n = cfg.rampingMaxStacks > 0 ? cfg.rampingMaxStacks : 4;
        string sfx = n switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
        return $"Every {n}{sfx} attack deals ×2 damage";
    }

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

    void SetEvoPortrait(Sprite portrait)
    {
        if (portrait != null)
        {
            _evolutionPortraitEl.style.backgroundImage = new StyleBackground(portrait);
            _evolutionPortraitEl.style.display = DisplayStyle.Flex;
        }
        else
        {
            _evolutionPortraitEl.style.backgroundImage = StyleKeyword.Null;
            _evolutionPortraitEl.style.display = DisplayStyle.None;
        }
    }
}
