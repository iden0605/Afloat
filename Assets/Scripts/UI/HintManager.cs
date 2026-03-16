using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Displays contextual one-time hint toasts during the player's first run.
///
/// Hints queue up so they never overlap.  Each hint is recorded in PlayerPrefs
/// so it shows only once, ever.
///
/// ── Scene setup ──
///   1. Add a new GameObject "HintManager" to the Main scene.
///   2. Attach a UIDocument component → assign the same PanelSettings as WaveUI,
///      Sort Order 20 (renders above all other UI).
///   3. Attach this script. Leave the UIDocument Source Asset empty (UI is built in code).
///
/// ── Resetting hints during development ──
///   Right-click this component in the Inspector → Reset All Hints.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class HintManager : MonoBehaviour
{
    public static HintManager Instance { get; private set; }

    [Tooltip("Seconds each hint stays on screen before disappearing.")]
    [SerializeField] private float displayDuration = 6f;

    [Tooltip("Seconds to wait before showing the next queued hint.")]
    [SerializeField] private float gapBetweenHints = 1f;

    // ── Hint keys ─────────────────────────────────────────────────────────────
    private const string KeySidebar      = "hint_sidebar";
    private const string KeyDrag         = "hint_drag";
    private const string KeyPlaced       = "hint_placed";
    private const string KeyWaveStart    = "hint_wave_start";
    private const string KeyGold         = "hint_gold";
    private const string KeyWaveCleared  = "hint_wave_cleared";
    private const string KeyNextWave     = "hint_next_wave";
    private const string KeyWaveControls = "hint_wave_controls";
    private const string KeyHP           = "hint_hp";
    private const string KeyEvolution    = "hint_evolution";

    // ── Hint data ─────────────────────────────────────────────────────────────

    private enum HintHighlight { None, Sidebar, NextWave, WaveControls }

    private struct HintEntry
    {
        public string        key;
        public string        message;
        public HintHighlight highlight;
    }

    private readonly Queue<HintEntry> _queue    = new();
    private bool                      _isShowing;

    // ── UI elements ───────────────────────────────────────────────────────────
    private VisualElement _toast;
    private Label         _toastLabel;
    private VisualElement _highlight;
    private Coroutine     _pulseCoroutine;

    // ── Gold tracking ─────────────────────────────────────────────────────────
    private int  _goldAtWaveStart;
    private bool _goldHintArmed;

    // ── HP tracking ───────────────────────────────────────────────────────────
    // Track the highest health value seen; the hint fires on the first decrease.
    private int _maxHealthSeen;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnEnable()
    {
        // UIDocument.rootVisualElement is only ready in OnEnable, not Awake.
        // Guard so we don't add duplicate elements if the object is toggled.
        if (_toast == null) BuildUI();

        GoldManager.OnGoldChanged              += OnGoldChanged;
        WaveManager.WaveStarted                += OnWaveStarted;
        WaveManager.WaveCleared                += OnWaveCleared;
        TroopManager.TroopPlaced               += OnTroopPlaced;
        TroopSidebarController.SidebarOpened   += OnSidebarOpened;
        PlayerHealthManager.OnHealthChanged    += OnHealthChanged;

        StartCoroutine(ShowStartHintDelayed());
    }

    void OnDisable()
    {
        GoldManager.OnGoldChanged              -= OnGoldChanged;
        WaveManager.WaveStarted                -= OnWaveStarted;
        WaveManager.WaveCleared                -= OnWaveCleared;
        TroopManager.TroopPlaced               -= OnTroopPlaced;
        TroopSidebarController.SidebarOpened   -= OnSidebarOpened;
        PlayerHealthManager.OnHealthChanged    -= OnHealthChanged;
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    // Hint 1: shown 2 s after scene loads — draws attention to the sidebar toggle
    IEnumerator ShowStartHintDelayed()
    {
        yield return new WaitForSecondsRealtime(2f);
        Enqueue(KeySidebar,
            "Tap the \u2630 button on the left to open your troop menu",
            HintHighlight.Sidebar);
    }

    // Hint 2: sidebar opened — remind player troops cost gold before they drag
    void OnSidebarOpened()
        => Enqueue(KeyDrag,
            "Each troop costs gold to place. Drag a card onto the field to deploy it!");

    // Hint 3: first troop placed — basic attack loop explained, no upgrade mention
    void OnTroopPlaced()
        => Enqueue(KeyPlaced,
            "Your troop will automatically attack any enemy in range. Place more to cover the path!");

    // Hint 4: first wave starts — path danger + introduce Next Wave button
    void OnWaveStarted(int waveIndex)
    {
        if (waveIndex == 0)
            Enqueue(KeyWaveStart,
                "Enemies follow the path \u2014 stop them before they reach the end or you lose health!");

        // Hint 8: wave controls introduced on wave 2 (player has already used Next Wave once)
        if (waveIndex == 1)
            Enqueue(KeyWaveControls,
                "Use the top-right controls: \u23f8 to pause, 2\u00d7 to double speed, and Auto to skip the break between waves.",
                HintHighlight.WaveControls);

        // Arm the gold hint: fire the first time gold increases during a wave
        if (!PlayerPrefs.HasKey(KeyGold))
        {
            _goldAtWaveStart = GoldManager.Instance != null ? GoldManager.Instance.Gold : 0;
            _goldHintArmed   = true;
        }
    }

    // Hint 5: first gold earned in combat — what to do with gold
    void OnGoldChanged(int newTotal)
    {
        if (!_goldHintArmed) return;
        if (newTotal > _goldAtWaveStart)
        {
            _goldHintArmed = false;
            Enqueue(KeyGold,
                "Enemies drop gold when defeated! Spend it between waves to buy more troops.");
        }
    }

    // Hint 6: first wave cleared — reminder to reinforce
    // Hint 7: introduce Next Wave button
    // Hint 9: first evolution unlock (Phase 7, after wave index 34 = WAVE 35)
    void OnWaveCleared(int waveIndex)
    {
        if (waveIndex == 0)
        {
            Enqueue(KeyWaveCleared,
                "Wave cleared! Use the break to place more troops before the next wave begins.");
            Enqueue(KeyNextWave,
                "Press 'Next Wave' when you're ready. Waves won't start automatically until you do!",
                HintHighlight.NextWave);
        }

        if (waveIndex == 34)
            Enqueue(KeyEvolution,
                "Evolutions unlocked! Select a placed troop and spend gold to upgrade it \u2014 once upgraded enough, you can evolve it into a stronger form.");
    }

    // Hint 10: first time an enemy escapes and deals HP damage
    void OnHealthChanged()
    {
        if (PlayerHealthManager.Instance == null) return;
        int current = PlayerHealthManager.Instance.CurrentHealth;

        // Track the peak value so we detect a decrease regardless of Start() order
        if (current > _maxHealthSeen) { _maxHealthSeen = current; return; }

        if (_maxHealthSeen > 0 && current < _maxHealthSeen)
            Enqueue(KeyHP,
                "An enemy escaped! Enemies that reach the end deal damage equal to their remaining health. Reach 0 HP and it's game over!");
    }

    // ── Queue system ──────────────────────────────────────────────────────────

    void Enqueue(string key, string message, HintHighlight highlight = HintHighlight.None)
    {
        if (PlayerPrefs.HasKey(key)) return;

        PlayerPrefs.SetInt(key, 1);
        PlayerPrefs.Save();

        _queue.Enqueue(new HintEntry
        {
            key       = key,
            message   = message,
            highlight = highlight
        });

        if (!_isShowing)
            StartCoroutine(ProcessQueue());
    }

    IEnumerator ProcessQueue()
    {
        _isShowing = true;
        while (_queue.Count > 0)
        {
            yield return StartCoroutine(ShowEntry(_queue.Dequeue()));
            if (_queue.Count > 0)
                yield return new WaitForSecondsRealtime(gapBetweenHints);
        }
        _isShowing = false;
    }

    IEnumerator ShowEntry(HintEntry entry)
    {
        // ── Resolve highlight bounds from the enum ─────────────────────────────
        Rect highlightBounds = entry.highlight switch
        {
            HintHighlight.Sidebar      => TroopSidebarController.Instance?.GetToggleButtonBounds() ?? Rect.zero,
            HintHighlight.NextWave     => WaveUIController.Instance?.GetNextWaveButtonBounds()     ?? Rect.zero,
            HintHighlight.WaveControls => WaveUIController.Instance?.GetControlButtonsBounds()     ?? Rect.zero,
            _                          => Rect.zero
        };

        if (highlightBounds != Rect.zero)
        {
            const float pad = 5f;
            _highlight.style.left    = highlightBounds.x      - pad;
            _highlight.style.top     = highlightBounds.y      - pad;
            _highlight.style.width   = highlightBounds.width  + pad * 2;
            _highlight.style.height  = highlightBounds.height + pad * 2;
            _highlight.style.display = DisplayStyle.Flex;

            if (_pulseCoroutine != null) StopCoroutine(_pulseCoroutine);
            _pulseCoroutine = StartCoroutine(PulseBorder(displayDuration));
        }
        else
        {
            _highlight.style.display = DisplayStyle.None;
        }

        // ── Show the toast ─────────────────────────────────────────────────────
        _toastLabel.text     = entry.message;
        _toast.style.display = DisplayStyle.Flex;

        yield return new WaitForSecondsRealtime(displayDuration);

        _toast.style.display     = DisplayStyle.None;
        _highlight.style.display = DisplayStyle.None;
    }

    // Animates the highlight box border with a pulsing yellow glow
    IEnumerator PulseBorder(float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            float alpha = 0.35f + Mathf.PingPong(t * 2.5f, 1f) * 0.65f;
            var   color = new Color(1f, 0.85f, 0.1f, alpha);
            _highlight.style.borderTopColor    = color;
            _highlight.style.borderRightColor  = color;
            _highlight.style.borderBottomColor = color;
            _highlight.style.borderLeftColor   = color;
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        _pulseCoroutine = null;
    }

    // ── UI construction ───────────────────────────────────────────────────────

    void BuildUI()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;

        // ── Highlight box: absolutely positioned, placed over sidebar toggle ───
        _highlight = new VisualElement();
        _highlight.pickingMode                   = PickingMode.Ignore;
        _highlight.style.position                = Position.Absolute;
        _highlight.style.display                 = DisplayStyle.None;
        _highlight.style.backgroundColor         = new Color(1f, 0.85f, 0.1f, 0.08f);
        _highlight.style.borderTopWidth          = 3;
        _highlight.style.borderRightWidth        = 3;
        _highlight.style.borderBottomWidth       = 3;
        _highlight.style.borderLeftWidth         = 3;
        _highlight.style.borderTopLeftRadius     = 8;
        _highlight.style.borderTopRightRadius    = 8;
        _highlight.style.borderBottomLeftRadius  = 8;
        _highlight.style.borderBottomRightRadius = 8;
        root.Add(_highlight);

        // ── Full-screen anchor centres the toast at the bottom ─────────────────
        var anchor = new VisualElement();
        anchor.pickingMode          = PickingMode.Ignore;
        anchor.style.position       = Position.Absolute;
        anchor.style.left           = 0; anchor.style.right  = 0;
        anchor.style.top            = 0; anchor.style.bottom = 0;
        anchor.style.alignItems     = Align.Center;
        anchor.style.justifyContent = Justify.FlexEnd;
        root.Add(anchor);

        // ── Toast pill ─────────────────────────────────────────────────────────
        _toast = new VisualElement();
        _toast.pickingMode                   = PickingMode.Ignore;
        _toast.style.display                 = DisplayStyle.None;
        _toast.style.backgroundColor         = new Color(0.06f, 0.06f, 0.06f, 0.93f);
        _toast.style.borderTopLeftRadius     = 12;
        _toast.style.borderTopRightRadius    = 12;
        _toast.style.borderBottomLeftRadius  = 12;
        _toast.style.borderBottomRightRadius = 12;
        _toast.style.borderTopWidth          = 1;
        _toast.style.borderRightWidth        = 1;
        _toast.style.borderBottomWidth       = 1;
        _toast.style.borderLeftWidth         = 1;
        _toast.style.borderTopColor          = new Color(1f, 1f, 1f, 0.15f);
        _toast.style.borderRightColor        = new Color(1f, 1f, 1f, 0.15f);
        _toast.style.borderBottomColor       = new Color(1f, 1f, 1f, 0.15f);
        _toast.style.borderLeftColor         = new Color(1f, 1f, 1f, 0.15f);
        _toast.style.paddingTop              = 8;
        _toast.style.paddingBottom           = 8;
        _toast.style.paddingLeft             = 16;
        _toast.style.paddingRight            = 16;
        _toast.style.marginBottom            = 50;
        _toast.style.maxWidth                = 460;

        _toastLabel = new Label();
        _toastLabel.pickingMode              = PickingMode.Ignore;
        _toastLabel.style.color              = Color.white;
        _toastLabel.style.fontSize           = 13;
        _toastLabel.style.unityTextAlign     = TextAnchor.MiddleCenter;
        _toastLabel.style.whiteSpace         = WhiteSpace.Normal;

        _toast.Add(_toastLabel);
        anchor.Add(_toast);
    }

    // ── Dev utility ───────────────────────────────────────────────────────────

    [ContextMenu("Reset All Hints")]
    public void ResetAllHints()
    {
        foreach (var key in new[] { KeySidebar, KeyDrag, KeyPlaced, KeyWaveStart, KeyGold,
                                     KeyWaveCleared, KeyNextWave, KeyWaveControls, KeyHP, KeyEvolution })
            PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();
        Debug.Log("[HintManager] All hints reset.");
    }
}
