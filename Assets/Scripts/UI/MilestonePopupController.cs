using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Drives phase-end milestone pop-ups after specific waves clear.
/// Pause-safe: all animation uses unscaledDeltaTime / WaitForSecondsRealtime.
///
/// ── Scene setup ──
///   1. Add a GameObject "MilestonePopupUI" to the Main scene.
///   2. Attach UIDocument → assign MilestonePopup.uxml, Sort Order 20.
///   3. Attach this script to the same GameObject.
///   4. Assign your drawn panel background sprite to Panel Background (optional; dark fallback if left empty).
///   5. Create one MilestoneData asset per phase (Right-click → Create → Click n Claw → Milestone Data).
///   6. Drag all MilestoneData assets into the Milestones list in Inspector (any order is fine).
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class MilestonePopupController : MonoBehaviour
{
    [Header("Milestone Definitions")]
    [Tooltip("All MilestoneData assets, one per phase. Order doesn't matter.")]
    [SerializeField] private List<MilestoneData> milestones = new();

    [Header("Visuals")]
    [Tooltip("Your hand-drawn panel background sprite. " +
             "Leave empty to use the dark CSS fallback until the art is ready.")]
    [SerializeField] private Sprite panelBackground;

    // ── Timing ────────────────────────────────────────────────────────────────

    private const float BackdropFade   = 0.25f;
    private const float PanelSlide     = 0.38f;
    private const float TitleSlide     = 0.30f;
    private const float WaveLabelFade  = 0.25f;
    private const float DividerExpand  = 0.22f;
    private const float HeaderFade     = 0.20f;
    private const float CardSlide      = 0.34f;
    private const float CardStagger    = 0.14f;  // delay between each card
    private const float NameFade       = 0.22f;
    private const float BadgeFade      = 0.18f;
    private const float StatsFade      = 0.18f;
    private const float AbilityFade    = 0.18f;
    private const float ButtonFade     = 0.25f;

    // ── UI refs ───────────────────────────────────────────────────────────────

    private VisualElement _root;
    private VisualElement _backdrop;
    private VisualElement _panel;
    private Label         _phaseLabel;
    private Label         _waveLabel;
    private VisualElement _divider;
    private Label         _alliesHeader;
    private VisualElement _cardsContainer;
    private Label         _victoryMsg;
    private VisualElement _buttons;
    private Button        _continueBtn;
    private Button        _keepGoingBtn;
    private Button        _menuBtn;

    // ── State ─────────────────────────────────────────────────────────────────

    private Dictionary<int, MilestoneData> _milestoneMap = new();
    private bool _popupActive;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void OnEnable()
    {
        var doc = GetComponent<UIDocument>();
        var root = doc.rootVisualElement;

        _root           = root.Q("ms-root");
        _backdrop       = root.Q("ms-backdrop");
        _panel          = root.Q("ms-panel");
        _phaseLabel     = root.Q<Label>("ms-phase-label");
        _waveLabel      = root.Q<Label>("ms-wave-label");
        _divider        = root.Q("ms-divider");
        _alliesHeader   = root.Q<Label>("ms-allies-header");
        _cardsContainer = root.Q("ms-cards-container");
        _victoryMsg     = root.Q<Label>("ms-victory-msg");
        _buttons        = root.Q("ms-buttons");
        _continueBtn    = root.Q<Button>("ms-continue-btn");
        _keepGoingBtn   = root.Q<Button>("ms-keep-going-btn");
        _menuBtn        = root.Q<Button>("ms-menu-btn");

        // Apply panel art if provided
        if (panelBackground != null && _panel != null)
            _panel.style.backgroundImage = new StyleBackground(panelBackground);

        // Build fast-lookup map  wave-index → milestone
        _milestoneMap.Clear();
        foreach (var m in milestones)
            if (m != null)
                _milestoneMap[m.triggerAfterWave] = m;

        // Wire buttons
        if (_continueBtn  != null) _continueBtn.clicked  += Dismiss;
        if (_keepGoingBtn != null) _keepGoingBtn.clicked += OnKeepGoing;
        if (_menuBtn      != null) _menuBtn.clicked      += OnMainMenu;

        // Start hidden
        if (_root != null) _root.style.display = DisplayStyle.None;

        WaveManager.WaveCleared += OnWaveCleared;
    }

    void OnDisable()
    {
        if (_continueBtn  != null) _continueBtn.clicked  -= Dismiss;
        if (_keepGoingBtn != null) _keepGoingBtn.clicked -= OnKeepGoing;
        if (_menuBtn      != null) _menuBtn.clicked      -= OnMainMenu;

        WaveManager.WaveCleared -= OnWaveCleared;
    }

    // ── Wave cleared handler ───────────────────────────────────────────────────

    void OnWaveCleared(int waveIndex)
    {
        if (_popupActive) return;
        if (!_milestoneMap.TryGetValue(waveIndex, out var data)) return;

        StartCoroutine(ShowPopup(data, waveIndex + 1));
    }

    // ── Main pop-up coroutine ─────────────────────────────────────────────────

    IEnumerator ShowPopup(MilestoneData data, int waveNumber)
    {
        _popupActive = true;
        Time.timeScale = 0f;

        // ── Populate text content ────────────────────────────────────────────
        _phaseLabel.text = data.phaseName;
        _waveLabel.text  = data.isVictory
            ? "ALL 50 WAVES DEFEATED!"
            : $"WAVE {waveNumber} CLEARED!";

        // Which UI sections are visible depends on victory vs. normal
        bool isVictory = data.isVictory;
        _alliesHeader.style.display   = isVictory ? DisplayStyle.None : DisplayStyle.Flex;
        _cardsContainer.style.display = isVictory ? DisplayStyle.None : DisplayStyle.Flex;
        _victoryMsg.style.display     = isVictory ? DisplayStyle.Flex : DisplayStyle.None;
        _continueBtn.style.display    = isVictory ? DisplayStyle.None : DisplayStyle.Flex;
        _keepGoingBtn.style.display   = isVictory ? DisplayStyle.Flex : DisplayStyle.None;
        _menuBtn.style.display        = isVictory ? DisplayStyle.Flex : DisplayStyle.None;

        // Build ally cards (normal mode only)
        List<VisualElement> cards = new();
        if (!isVictory)
        {
            _cardsContainer.Clear();
            foreach (var troop in data.unlockedTroops)
                if (troop != null)
                    cards.Add(BuildTroopCard(troop));
            foreach (var evo in data.evolutionUnlocks)
                cards.Add(BuildEvolutionCard(evo));

            foreach (var card in cards)
                _cardsContainer.Add(card);
        }

        // ── Reset all animated elements to start state ───────────────────────
        SetOpacity(_backdrop, 0f);
        ResetElement(_panel,       translateX: 0f, translateY: 60f, opacity: 0f);
        ResetElement(_phaseLabel,  translateX: -24f, translateY: 0f, opacity: 0f);
        ResetElement(_waveLabel,   translateX: -24f, translateY: 0f, opacity: 0f);
        SetWidth(_divider, 0f);
        SetOpacity(_divider, 0f);
        SetOpacity(_alliesHeader, 0f);
        SetOpacity(_victoryMsg,   0f);
        SetOpacity(_buttons,  0f);
        ResetElement(_buttons, translateX: 0f, translateY: 10f, opacity: 0f);
        foreach (var card in cards)
        {
            ResetElement(card, translateX: 36f, translateY: 0f, opacity: 0f);
            ResetChildElement(card, "ms-card-name",    translateX: 0f, translateY: 8f, opacity: 0f);
            ResetChildElement(card, "ms-attack-badge", translateX: 0f, translateY: 8f, opacity: 0f);
            ResetChildElement(card, "ms-stats-row",    translateX: 0f, translateY: 8f, opacity: 0f);
            ResetChildElement(card, "ms-card-ability", translateX: 0f, translateY: 8f, opacity: 0f);
        }

        // ── Show root ────────────────────────────────────────────────────────
        _root.style.display = DisplayStyle.Flex;

        // ── 1. Backdrop fade in ───────────────────────────────────────────────
        yield return AnimateOpacity(_backdrop, 0f, 0.82f, BackdropFade);

        // ── 2. Panel slide up + fade in ───────────────────────────────────────
        yield return AnimatePanelIn(_panel, PanelSlide);

        // ── 3. Phase title slides from left ───────────────────────────────────
        yield return new WaitForSecondsRealtime(0.05f);
        yield return AnimateSlide(_phaseLabel, -24f, 0f, 1f, 0f, 1f, TitleSlide, EaseOutQuad);

        // ── 4. Wave cleared label ─────────────────────────────────────────────
        yield return new WaitForSecondsRealtime(0.06f);
        yield return AnimateSlide(_waveLabel,  -24f, 0f, 1f, 0f, 1f, WaveLabelFade, EaseOutQuad);

        // ── 5. Divider expands ────────────────────────────────────────────────
        yield return new WaitForSecondsRealtime(0.06f);
        yield return AnimateDivider(_divider, DividerExpand);

        if (isVictory)
        {
            // ── Victory path ──────────────────────────────────────────────────
            yield return new WaitForSecondsRealtime(0.08f);
            yield return AnimateOpacity(_victoryMsg, 0f, 1f, 0.30f);
        }
        else
        {
            // ── 6. Section header ─────────────────────────────────────────────
            yield return new WaitForSecondsRealtime(0.04f);
            yield return AnimateOpacity(_alliesHeader, 0f, 1f, HeaderFade);

            // ── 7. Ally cards staggered in ────────────────────────────────────
            foreach (var card in cards)
            {
                StartCoroutine(AnimateCardIn(card));
                yield return new WaitForSecondsRealtime(CardStagger);
            }

            // Wait for the last card to finish animating
            float lastCardDuration = CardSlide
                + NameFade + 0.06f
                + BadgeFade + 0.05f
                + StatsFade + 0.05f
                + AbilityFade;
            yield return new WaitForSecondsRealtime(lastCardDuration);
        }

        // ── 8. Buttons appear ─────────────────────────────────────────────────
        yield return new WaitForSecondsRealtime(0.08f);
        yield return AnimateSlide(_buttons, 0f, 0f, 10f, 0f, 1f, ButtonFade, EaseOutQuad);
    }

    // ── Per-card animation (runs in parallel, staggered by caller) ────────────

    IEnumerator AnimateCardIn(VisualElement card)
    {
        // Card slides in from right
        yield return AnimateSlide(card, 36f, 0f, 0f, 0f, 1f, CardSlide, EaseOutCubic);

        var name    = card.Q(className: "ms-card-name");
        var badge   = card.Q(className: "ms-attack-badge");
        var stats   = card.Q(className: "ms-stats-row");
        var ability = card.Q(className: "ms-card-ability");

        yield return new WaitForSecondsRealtime(0.06f);
        if (name != null)
            yield return AnimateSlide(name, 0f, 0f, 8f, 0f, 1f, NameFade, EaseOutQuad);

        yield return new WaitForSecondsRealtime(0.05f);
        if (badge != null)
            yield return AnimateSlide(badge, 0f, 0f, 8f, 0f, 1f, BadgeFade, EaseOutQuad);

        yield return new WaitForSecondsRealtime(0.05f);
        if (stats != null)
            yield return AnimateSlide(stats, 0f, 0f, 8f, 0f, 1f, StatsFade, EaseOutQuad);

        yield return new WaitForSecondsRealtime(0.05f);
        if (ability != null)
            yield return AnimateSlide(ability, 0f, 0f, 8f, 0f, 1f, AbilityFade, EaseOutQuad);
    }

    // ── Button handlers ───────────────────────────────────────────────────────

    void Dismiss()
    {
        StopAllCoroutines();
        _root.style.display = DisplayStyle.None;
        _popupActive = false;
        // Restore time scale, preserving any active double-speed state.
        // If AutoProceed is on, WaveManager's frozen AutoStartNext coroutine will
        // automatically resume and start the next wave once timeScale is > 0.
        Time.timeScale = WaveManager.Instance != null ? WaveManager.Instance.SpeedLevel : 1;
    }

    void OnKeepGoing()
    {
        // TODO: trigger unlimited/endless wave mode here when implemented.
        // For now, simply dismiss and let the player interact with a wave-complete game state.
        Dismiss();
    }

    void OnMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }

    // ── Card builders ─────────────────────────────────────────────────────────

    VisualElement BuildTroopCard(TroopData troop)
    {
        var card = new VisualElement();
        card.AddToClassList("ms-card");

        // Portrait
        var portrait = new VisualElement();
        portrait.AddToClassList("ms-card-portrait");
        if (troop.portrait != null)
            portrait.style.backgroundImage = new StyleBackground(troop.portrait);
        card.Add(portrait);

        // Info column
        var info = new VisualElement();
        info.AddToClassList("ms-card-info");

        var nameLabel = new Label(troop.troopName);
        nameLabel.AddToClassList("ms-card-name");
        info.Add(nameLabel);

        // Attack-type badge
        var badge = new Label(troop.attackType.ToString().ToUpper());
        badge.AddToClassList("ms-attack-badge");
        badge.AddToClassList(troop.attackType switch
        {
            AttackType.Melee  => "ms-attack-badge--melee",
            AttackType.Ranged => "ms-attack-badge--ranged",
            AttackType.Splash => "ms-attack-badge--splash",
            _                 => "ms-attack-badge--utility",
        });
        info.Add(badge);

        // Stats row
        var statsRow = new VisualElement();
        statsRow.AddToClassList("ms-stats-row");
        statsRow.Add(MakeStatChip($"ATK  {troop.attack:0.#}"));
        statsRow.Add(MakeStatChip($"SPD  {troop.attackSpeed:0.#}"));
        statsRow.Add(MakeStatChip($"RNG  {troop.range:0.#}"));
        info.Add(statsRow);

        // Special ability text
        string abilityText = EffectToString(troop.baseEffect);
        if (!string.IsNullOrEmpty(abilityText))
        {
            var ability = new Label(abilityText);
            ability.AddToClassList("ms-card-ability");
            info.Add(ability);
        }

        card.Add(info);
        return card;
    }

    VisualElement BuildEvolutionCard(EvolutionUnlockEntry evo)
    {
        var card = new VisualElement();
        card.AddToClassList("ms-card");

        // Portrait
        var portrait = new VisualElement();
        portrait.AddToClassList("ms-card-portrait");
        if (evo.portrait != null)
            portrait.style.backgroundImage = new StyleBackground(evo.portrait);
        card.Add(portrait);

        // Info column
        var info = new VisualElement();
        info.AddToClassList("ms-card-info");

        var nameLabel = new Label(evo.displayName);
        nameLabel.AddToClassList("ms-card-name");
        info.Add(nameLabel);

        // "EVOLUTION" badge
        var badge = new Label("EVOLUTION");
        badge.AddToClassList("ms-attack-badge");
        badge.AddToClassList("ms-attack-badge--utility");
        info.Add(badge);

        // Subtitle as stat chip
        if (!string.IsNullOrEmpty(evo.subtitle))
        {
            var statsRow = new VisualElement();
            statsRow.AddToClassList("ms-stats-row");
            statsRow.Add(MakeStatChip(evo.subtitle));
            info.Add(statsRow);
        }

        // Ability description
        if (!string.IsNullOrEmpty(evo.abilityDescription))
        {
            var ability = new Label(evo.abilityDescription);
            ability.AddToClassList("ms-card-ability");
            info.Add(ability);
        }

        card.Add(info);
        return card;
    }

    static VisualElement MakeStatChip(string text)
    {
        var chip = new Label(text);
        chip.AddToClassList("ms-stat-chip");
        return chip;
    }

    // ── Effect → readable string ──────────────────────────────────────────────

    static string EffectToString(TroopEffectConfig cfg)
    {
        if (cfg == null) return null;
        return cfg.effectType switch
        {
            TroopEffectType.BurnOnHit            => "Burns enemies on hit",
            TroopEffectType.PoisonOnHit          => "Poisons enemies on hit",
            TroopEffectType.PoisonSplash         => "Poisons the area on attack",
            TroopEffectType.FreezeOnHit          => $"Slows enemies for {cfg.freezeDuration:0.#}s",
            TroopEffectType.StunOnHit            => $"Stuns enemies for {cfg.stunDuration:0.#}s",
            TroopEffectType.DoubleGoldDrop       => "Enemies drop double gold",
            TroopEffectType.ConditionalAttackBuff=> "Stronger when facing crowds",
            TroopEffectType.ConditionalSpeedBuff => "Faster against single targets",
            TroopEffectType.DoubleEveryFourth    => "Every 4th hit deals double damage",
            TroopEffectType.RampingDoubleBuff    => "Stacks power with each hit",
            TroopEffectType.AllyProximityBuff    => "Stronger near allies",
            TroopEffectType.AllySpeedBuff        => "Faster near allies",
            _                                    => null,
        };
    }

    // ── Animation helpers ─────────────────────────────────────────────────────

    /// <summary>Fades the panel in while sliding it up from translateY = startY → 0.
    /// Uses EaseOutBack so it slightly overshoots for a bouncy feel.</summary>
    IEnumerator AnimatePanelIn(VisualElement el, float duration)
    {
        const float startY   = 60f;
        const float startOpa = 0f;

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float p   = EaseOutBack(Mathf.Clamp01(t / duration));
            float opa = Mathf.Clamp01(t / duration * 2f); // fade finishes at half the duration
            el.style.translate = new StyleTranslate(new Translate(new Length(0), new Length(startY * (1f - p))));
            el.style.opacity   = opa;
            yield return null;
        }
        el.style.translate = new StyleTranslate(new Translate(new Length(0), new Length(0)));
        el.style.opacity   = 1f;
    }

    /// <summary>Animates both translate and opacity simultaneously.</summary>
    IEnumerator AnimateSlide(VisualElement el,
                              float fromX, float toX,
                              float fromY, float toY,
                              float toOpacity,
                              float duration,
                              System.Func<float, float> ease)
    {
        float startOpacity = el.resolvedStyle.opacity;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float p  = ease(Mathf.Clamp01(t / duration));
            float op = Mathf.Lerp(startOpacity, toOpacity, Mathf.Clamp01(t / duration));
            el.style.translate = new StyleTranslate(new Translate(
                new Length(Mathf.Lerp(fromX, toX, p)),
                new Length(Mathf.Lerp(fromY, toY, p))));
            el.style.opacity = op;
            yield return null;
        }
        el.style.translate = new StyleTranslate(new Translate(new Length(toX), new Length(toY)));
        el.style.opacity   = toOpacity;
    }

    IEnumerator AnimateOpacity(VisualElement el, float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            el.style.opacity = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
            yield return null;
        }
        el.style.opacity = to;
    }

    IEnumerator AnimateDivider(VisualElement el, float duration)
    {
        el.style.opacity = 1f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float p = EaseOutQuad(Mathf.Clamp01(t / duration));
            el.style.width = new StyleLength(Length.Percent(p * 100f));
            yield return null;
        }
        el.style.width = new StyleLength(Length.Percent(100f));
    }

    // ── USS reset helpers ─────────────────────────────────────────────────────

    static void ResetElement(VisualElement el, float translateX, float translateY, float opacity)
    {
        el.style.translate = new StyleTranslate(new Translate(new Length(translateX), new Length(translateY)));
        el.style.opacity   = opacity;
    }

    static void ResetChildElement(VisualElement parent, string className, float translateX, float translateY, float opacity)
    {
        var el = parent.Q(className: className);
        if (el != null) ResetElement(el, translateX, translateY, opacity);
    }

    static void SetOpacity(VisualElement el, float opacity)
    {
        if (el != null) el.style.opacity = opacity;
    }

    static void SetWidth(VisualElement el, float percent)
    {
        if (el != null) el.style.width = new StyleLength(Length.Percent(percent));
    }

    // ── Easing functions ──────────────────────────────────────────────────────

    static float EaseOutQuad(float t)  => 1f - (1f - t) * (1f - t);
    static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);

    /// <summary>Slight overshoot — makes the panel entrance feel snappy and alive.</summary>
    static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
}
