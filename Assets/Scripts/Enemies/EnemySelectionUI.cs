using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Right-side slide-in panel shown when the player clicks a live enemy.
/// Reuses the same .sel-panel / .sel-open CSS as TroopSelectionUI so it slides
/// in identically from the right.
///
/// ── Scene setup ──
///   Add this component to the SAME GameObject as TroopSelectionUI.
///   They will share that GameObject's UIDocument (whose root is already full-screen),
///   so no extra GameObject or UIDocument is needed.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class EnemySelectionUI : MonoBehaviour
{
    public static EnemySelectionUI Instance { get; private set; }

    private UIDocument    _uiDoc;
    private EnemyInstance _target;
    private VisualElement _panel;

    // Portrait / header
    private VisualElement _portraitEl;
    private Label         _nameLabel;

    // Live stats
    private Label _hpLabel;
    private Label _spdLabel;
    private Label _goldLabel;

    // Description / effect
    private Label         _descriptionLabel;
    private VisualElement _effectRow;
    private Label         _effectLabel;

    // Prevents LateUpdate from closing the panel on the same frame Show() was called
    private int _showFrame = -1;

    // Outline glow rendered behind the enemy sprite when selected
    private GameObject     _outlineGO;
    private SpriteRenderer _outlineSr;
    private SpriteRenderer _mainSr;      // enemy's primary SpriteRenderer (animated)

    private static readonly Color OutlineColour = new Color(1f, 0.88f, 0.25f, 0.90f);

    // ── Lifecycle ────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _uiDoc = GetComponent<UIDocument>();
    }

    void OnEnable() => BuildPanel();

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>Open the panel for the given enemy. Clicking the same enemy toggles it closed.</summary>
    public void Show(EnemyInstance enemy)
    {
        if (_panel.ClassListContains("sel-open") && _target == enemy)
        {
            Hide();
            return;
        }

        ClearHighlight();

        _showFrame = Time.frameCount;
        _target    = enemy;
        RefreshStatic();
        Refresh();
        _panel.AddToClassList("sel-open");
        ApplyHighlight();
    }

    /// <summary>Close the panel and clear the highlight.</summary>
    public void Hide()
    {
        ClearHighlight();
        _target = null;
        _panel?.RemoveFromClassList("sel-open");
    }

    // ── Refresh ──────────────────────────────────────────────────────────────

    void RefreshStatic()
    {
        if (_target == null || _target.Data == null) return;
        var d = _target.Data;

        if (d.portrait != null)
            _portraitEl.style.backgroundImage = new StyleBackground(d.portrait);

        _nameLabel.text = d.enemyName;

        bool hasDesc = !string.IsNullOrEmpty(d.description);
        _descriptionLabel.text = d.description;
        _descriptionLabel.style.display = hasDesc ? DisplayStyle.Flex : DisplayStyle.None;

        string fx = EffectDescription(d);
        _effectLabel.text = string.IsNullOrEmpty(fx) ? "" : $"*  {fx}";
        _effectRow.style.display = string.IsNullOrEmpty(fx) ? DisplayStyle.None : DisplayStyle.Flex;
    }

    void Refresh()
    {
        if (_target == null) return;
        _hpLabel.text   = $"{_target.CurrentHealth:0.#}/{_target.MaxHealth:0.#}";
        _spdLabel.text  = $"{_target.Data.speed:0.#}";
        _goldLabel.text = $"{_target.Data.goldDrop}g";
    }

    // ── Highlight ────────────────────────────────────────────────────────────

    void ApplyHighlight()
    {
        if (_target == null) return;

        _mainSr = FindMainSpriteRenderer(_target);
        if (_mainSr == null) return;

        // Create a slightly scaled copy of the sprite rendered BEHIND the enemy.
        // This gives a clean outline without touching any existing SpriteRenderer colors.
        _outlineGO = new GameObject("SelectionOutline");
        _outlineGO.transform.SetParent(_target.transform, false);
        _outlineGO.transform.localScale = Vector3.one * 1.14f;

        _outlineSr               = _outlineGO.AddComponent<SpriteRenderer>();
        _outlineSr.sprite        = _mainSr.sprite;
        _outlineSr.color         = OutlineColour;
        _outlineSr.sortingLayerName = _mainSr.sortingLayerName;
        _outlineSr.sortingOrder  = _mainSr.sortingOrder - 1; // behind main sprite
    }

    void ClearHighlight()
    {
        if (_outlineGO != null)
        {
            Destroy(_outlineGO);
            _outlineGO = null;
        }
        _outlineSr = null;
        _mainSr    = null;
    }

    // Returns the enemy's primary SpriteRenderer, skipping health bar quads.
    static SpriteRenderer FindMainSpriteRenderer(EnemyInstance enemy)
    {
        foreach (var sr in enemy.GetComponentsInChildren<SpriteRenderer>())
        {
            // Health bar SpriteRenderers are named HP_BG / HP_Fill by EnemyHealthBar
            var n = sr.gameObject.name;
            if (n == "HP_BG" || n == "HP_Fill") continue;
            return sr;
        }
        return null;
    }

    // ── LateUpdate — live HP refresh + click-outside-to-close ────────────────

    void LateUpdate()
    {
        if (!_panel.ClassListContains("sel-open")) return;

        // Enemy was destroyed (died) — close without touching its colour
        if (_target == null)
        {
            _panel.RemoveFromClassList("sel-open");
            return;
        }

        // Keep outline sprite in sync with the animated main sprite each frame
        if (_outlineSr != null && _mainSr != null)
            _outlineSr.sprite = _mainSr.sprite;

        Refresh();

        if (!Input.GetMouseButtonDown(0)) return;
        if (Time.frameCount == _showFrame) return;

        var   root = _uiDoc.rootVisualElement;
        float px   = (Input.mousePosition.x / Screen.width)                    * root.resolvedStyle.width;
        float py   = ((Screen.height - Input.mousePosition.y) / Screen.height) * root.resolvedStyle.height;

        if (!_panel.worldBound.Contains(new Vector2(px, py)))
            Hide();
    }

    // ── Panel construction ───────────────────────────────────────────────────

    void BuildPanel()
    {
        _panel = new VisualElement();
        // Use the same positioning class as TroopSelectionUI — already styled correctly
        _panel.AddToClassList("sel-panel");

        // ── Top bar ───────────────────────────────────────────────
        var topBar = new VisualElement();
        topBar.AddToClassList("esel-topbar"); // red-tinted variant

        var titleLabel = new Label("ENEMY");
        titleLabel.AddToClassList("esel-sidebar-title"); // red text

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

        // Portrait + name + threat badge
        var portraitSection = new VisualElement();
        portraitSection.AddToClassList("sel-portrait-section");

        _portraitEl = new VisualElement();
        _portraitEl.AddToClassList("esel-portrait");

        _nameLabel = new Label();
        _nameLabel.AddToClassList("sel-troop-name");

        var threatBadge = new Label("THREAT");
        threatBadge.AddToClassList("esel-threat-badge");

        portraitSection.Add(_portraitEl);
        portraitSection.Add(_nameLabel);
        portraitSection.Add(threatBadge);
        scroll.Add(portraitSection);

        // Stats: HP  SPD  GOLD
        scroll.Add(MakeDivider());
        var statsRow = new VisualElement();
        statsRow.AddToClassList("sel-stats-row");

        _hpLabel   = BuildStatItem(statsRow, "HP");
        _hpLabel.AddToClassList("esel-hp-value"); // smaller font to fit "X/Y"
        _spdLabel  = BuildStatItem(statsRow, "SPD");
        _goldLabel = BuildStatItem(statsRow, "GOLD");
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
        _effectLabel.AddToClassList("esel-effect");
        _effectRow.Add(_effectLabel);
        scroll.Add(_effectRow);

        _panel.Add(scroll);

        _uiDoc.rootVisualElement.Add(_panel);
    }

    static Label BuildStatItem(VisualElement parent, string statName)
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

    static string EffectDescription(EnemyData d) => d.effectType switch
    {
        EnemyEffectType.ImmuneMelee     => "Immune to melee attacks",
        EnemyEffectType.ImmuneRanged    => "Immune to ranged attacks",
        EnemyEffectType.DamageReduction => $"Blocks {d.effectValue * 100:0}% of all damage",
        EnemyEffectType.MaxDamagePerHit => $"Max {d.effectValue:0.#} damage per hit",
        EnemyEffectType.DodgeChance     => $"{d.effectValue * 100:0}% chance to dodge attacks",
        EnemyEffectType.SpeedBurst      => d.effectValue > 0
                                            ? $"Doubles speed when HP drops to {d.effectValue:0.#}"
                                            : "Doubles speed when HP drops below 50%",
        EnemyEffectType.SpeedDoubleOnHit => "Doubles speed the first time it is hit",
        EnemyEffectType.SpawnOnDeath    => d.spawnEnemyData != null
                                            ? $"Spawns {d.spawnCount}× {d.spawnEnemyData.enemyName} on death"
                                            : $"Spawns {d.spawnCount} enemies on death",
        _                               => ""
    };
}
