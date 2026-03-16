using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Displays the player's current gold total in a badge centred at the top of the screen.
///
/// Reacts to GoldManager.OnGoldChanged:
///   • Gold gained  → badge pops up to 1.35× scale then snaps back (300 ms)
///   • Gold spent   → label flashes red then fades back to gold (550 ms)
///
/// ── Scene setup ──
///   1. Create a GameObject in the scene (e.g. "GoldHUD").
///   2. Add a UIDocument component — assign the same PanelSettings used by your other UI.
///      Set its Sort Order higher than all other UIDocuments (e.g. 50) so it draws on top.
///   3. Add this GoldHUD component to the same GameObject.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class GoldHUD : MonoBehaviour
{
    public static GoldHUD Instance { get; private set; }

    [Header("Visuals")]
    [Tooltip("Padding inside the gold badge (px).")]
    [SerializeField] private int paddingH = 20;
    [SerializeField] private int paddingV = 0;
    [Tooltip("Top margin from the screen edge (px).")]
    [SerializeField] private int topMargin = 0;
    [Tooltip("Corner radius of the badge (px).")]
    [SerializeField] private int cornerRadius = 14;
    [Tooltip("Height of the badge (px).")]
    [SerializeField] private int badgeHeight = 60;
    [Tooltip("Font size of the gold label (px).")]
    [SerializeField] private int fontSize = 26;

    private VisualElement _badge;
    private Label         _goldLabel;
    private int           _previousGold;
    private bool          _initialized;

    // Running animation handles — paused/replaced on each new trigger
    private IVisualElementScheduledItem _scaleAnim;
    private IVisualElementScheduledItem _colorAnim;

    private static readonly Color GoldColour  = new Color(1.00f, 0.88f, 0.30f);
    private static readonly Color SpendColour = new Color(1.00f, 0.28f, 0.22f);

    // ── Timer badge ───────────────────────────────────────────────────────────
    private VisualElement _timerBadge;
    private Label         _timerLabel;

    // Elapsed seconds during the current wave
    private float  _elapsedSeconds = 0f;
    private int    _lastWaveIndex  = -1;
    private Button _skipBtn;

    private static readonly Color TimerNormalColour   = new Color(0.55f, 0.92f, 1.00f);
    private static readonly Color TimerCountdownColour = new Color(1.00f, 0.82f, 0.30f);
    private static readonly Color TimerFlashColour     = new Color(1.00f, 0.22f, 0.18f);
    private static readonly Color TimerFlashHighColour = new Color(1.00f, 0.85f, 0.85f);

    private static readonly Color TimerBorderNormal = new Color(0.20f, 0.62f, 0.88f, 0.55f);
    private static readonly Color TimerBorderRed    = new Color(0.90f, 0.18f, 0.14f, 0.80f);

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        Instance = this;
    }

    /// <summary>
    /// World-space centre of the gold badge.
    /// Used by GoldCoin to know where to fly.
    /// </summary>
    public Vector3 BadgeWorldPosition
    {
        get
        {
            if (_badge == null) return Vector3.zero;

            // UIToolkit worldBound: origin top-left, y axis downward
            var rect = _badge.worldBound;
            float sx = rect.center.x;
            float sy = Screen.height - rect.center.y; // flip to Unity screen space (y up)

            var cam = Camera.main;
            if (cam == null) return Vector3.zero;

            float z = Mathf.Abs(cam.transform.position.z);
            return cam.ScreenToWorldPoint(new Vector3(sx, sy, z));
        }
    }

    void OnEnable()
    {
        BuildHUD();
        GoldManager.OnGoldChanged += UpdateDisplay;

        // Sync immediately in case GoldManager already fired Start() before we subscribed
        if (GoldManager.Instance != null)
            UpdateDisplay(GoldManager.Instance.Gold);
    }

    void OnDisable()
    {
        GoldManager.OnGoldChanged -= UpdateDisplay;
    }

    // ── Build ─────────────────────────────────────────────────────────────────

    void BuildHUD()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        root.Clear();
        _initialized = false;

        // Full-screen invisible container — centres its child horizontally
        var screen = new VisualElement();
        screen.style.position       = Position.Absolute;
        screen.style.top            = 0;
        screen.style.left           = 0;
        screen.style.right          = 0;
        screen.style.flexDirection  = FlexDirection.Row;
        screen.style.justifyContent = Justify.Center;
        screen.style.alignItems     = Align.FlexStart;
        screen.pickingMode          = PickingMode.Ignore;
        root.Add(screen);

        // Gold badge
        _badge = new VisualElement();
        _badge.style.marginTop                   = 0;
        _badge.style.paddingTop                  = paddingV;
        _badge.style.paddingBottom               = paddingV;
        _badge.style.paddingLeft                 = paddingH;
        _badge.style.paddingRight                = paddingH;
        _badge.style.backgroundColor             = new StyleColor(new Color(0.10f, 0.08f, 0.04f, 0.88f));
        _badge.style.borderTopLeftRadius         = cornerRadius;
        _badge.style.borderTopRightRadius        = cornerRadius;
        _badge.style.borderBottomLeftRadius      = cornerRadius;
        _badge.style.borderBottomRightRadius     = cornerRadius;
        _badge.style.borderTopWidth              = 1f;
        _badge.style.borderBottomWidth           = 1f;
        _badge.style.borderLeftWidth             = 1f;
        _badge.style.borderRightWidth            = 1f;
        _badge.style.borderTopColor              = new StyleColor(new Color(0.85f, 0.65f, 0.10f, 0.55f));
        _badge.style.borderBottomColor           = new StyleColor(new Color(0.85f, 0.65f, 0.10f, 0.55f));
        _badge.style.borderLeftColor             = new StyleColor(new Color(0.85f, 0.65f, 0.10f, 0.55f));
        _badge.style.borderRightColor            = new StyleColor(new Color(0.85f, 0.65f, 0.10f, 0.55f));
        _badge.style.height                      = badgeHeight;
        _badge.style.flexDirection               = FlexDirection.Row;
        _badge.style.alignItems                  = Align.Center;
        _badge.pickingMode                       = PickingMode.Ignore;
        screen.Add(_badge);

        // "G " prefix acts as the icon
        var prefix = new Label("G ");
        prefix.style.fontSize                  = fontSize;
        prefix.style.color                     = new StyleColor(new Color(0.85f, 0.65f, 0.25f, 0.85f));
        prefix.style.unityFontStyleAndWeight   = FontStyle.Bold;
        prefix.style.marginTop                 = 0;
        prefix.style.marginBottom              = 0;
        prefix.style.paddingTop                = 0;
        prefix.style.paddingBottom             = 0;
        prefix.style.unityTextAlign            = TextAnchor.MiddleCenter;
        prefix.pickingMode                     = PickingMode.Ignore;
        _badge.Add(prefix);

        _goldLabel = new Label("0");
        _goldLabel.style.fontSize                = fontSize;
        _goldLabel.style.color                   = new StyleColor(GoldColour);
        _goldLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        _goldLabel.style.marginTop               = 0;
        _goldLabel.style.marginBottom            = 0;
        _goldLabel.style.paddingTop              = 0;
        _goldLabel.style.paddingBottom           = 0;
        _goldLabel.style.unityTextAlign          = TextAnchor.MiddleCenter;
        _goldLabel.pickingMode                   = PickingMode.Ignore;
        _badge.Add(_goldLabel);

        // ── Timer badge (sits immediately to the right of the gold badge) ─────
        var timerGap = new VisualElement();
        timerGap.style.width     = 8;
        timerGap.pickingMode     = PickingMode.Ignore;
        screen.Add(timerGap);

        _timerBadge = new VisualElement();
        _timerBadge.style.marginTop              = 0;
        _timerBadge.style.paddingTop             = paddingV;
        _timerBadge.style.paddingBottom          = paddingV;
        _timerBadge.style.paddingLeft            = paddingH;
        _timerBadge.style.paddingRight           = paddingH;
        _timerBadge.style.backgroundColor        = new StyleColor(new Color(0.04f, 0.08f, 0.12f, 0.88f));
        _timerBadge.style.borderTopLeftRadius    = cornerRadius;
        _timerBadge.style.borderTopRightRadius   = cornerRadius;
        _timerBadge.style.borderBottomLeftRadius  = cornerRadius;
        _timerBadge.style.borderBottomRightRadius = cornerRadius;
        _timerBadge.style.borderTopWidth         = 1f;
        _timerBadge.style.borderBottomWidth      = 1f;
        _timerBadge.style.borderLeftWidth        = 1f;
        _timerBadge.style.borderRightWidth       = 1f;
        SetBadgeBorderColor(_timerBadge, TimerBorderNormal);
        _timerBadge.style.height                 = badgeHeight;
        _timerBadge.style.flexDirection          = FlexDirection.Row;
        _timerBadge.style.alignItems             = Align.Center;
        _timerBadge.pickingMode                  = PickingMode.Ignore;
        screen.Add(_timerBadge);

        _timerLabel = new Label("—:——");
        _timerLabel.style.fontSize               = fontSize;
        _timerLabel.style.color                  = new StyleColor(TimerNormalColour);
        _timerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        _timerLabel.style.marginTop              = 0;
        _timerLabel.style.marginBottom           = 0;
        _timerLabel.style.paddingTop             = 0;
        _timerLabel.style.paddingBottom          = 0;
        _timerLabel.style.unityTextAlign         = TextAnchor.MiddleCenter;
        _timerLabel.pickingMode                  = PickingMode.Ignore;
        _timerBadge.Add(_timerLabel);

        // Skip button — visible only during countdown
        _skipBtn = new Button(() => WaveManager.Instance?.SkipCountdown()) { text = "▶▶" };
        _skipBtn.style.marginLeft              = 10;
        _skipBtn.style.paddingTop              = 0;
        _skipBtn.style.paddingBottom           = 0;
        _skipBtn.style.paddingLeft             = 8;
        _skipBtn.style.paddingRight            = 8;
        _skipBtn.style.height                  = 30;
        _skipBtn.style.fontSize                = 13;
        _skipBtn.style.color                   = new StyleColor(new Color(1f, 0.85f, 0.30f));
        _skipBtn.style.backgroundColor         = new StyleColor(new Color(0.25f, 0.20f, 0.04f, 0.80f));
        _skipBtn.style.borderTopLeftRadius     = 6;
        _skipBtn.style.borderTopRightRadius    = 6;
        _skipBtn.style.borderBottomLeftRadius  = 6;
        _skipBtn.style.borderBottomRightRadius = 6;
        _skipBtn.style.borderTopWidth          = 1f;
        _skipBtn.style.borderBottomWidth       = 1f;
        _skipBtn.style.borderLeftWidth         = 1f;
        _skipBtn.style.borderRightWidth        = 1f;
        SetBadgeBorderColor(_skipBtn, new Color(0.85f, 0.65f, 0.10f, 0.50f));
        _skipBtn.style.unityTextAlign          = TextAnchor.MiddleCenter;
        _skipBtn.style.display                 = DisplayStyle.None;
        _timerBadge.Add(_skipBtn);
    }

    // ── Update display ────────────────────────────────────────────────────────

    void UpdateDisplay(int newGold)
    {
        if (_goldLabel == null) return;

        _goldLabel.text = newGold.ToString();

        // Skip animation on the very first call (initial display, not a real change)
        if (!_initialized)
        {
            _previousGold = newGold;
            _initialized  = true;
            return;
        }

        if (newGold > _previousGold)
            PlayGainPop();
        else if (newGold < _previousGold)
            PlaySpendFlash();

        _previousGold = newGold;
    }

    // ── Gain animation: badge pops up in scale then snaps back ───────────────

    void PlayGainPop()
    {
        // Cancel any running colour animation and reset label colour
        _colorAnim?.Pause();
        _goldLabel.style.color = new StyleColor(GoldColour);

        // Cancel any running scale animation and reset before starting fresh
        _scaleAnim?.Pause();
        _badge.transform.scale = Vector3.one;

        float start    = Time.realtimeSinceStartup;
        float duration = 0.30f; // seconds
        float peak     = 1.35f;

        _scaleAnim = _badge.schedule.Execute(() =>
        {
            float t = Mathf.Clamp01((Time.realtimeSinceStartup - start) / duration);

            // Triangle curve: rise to peak at 40% of duration, fall back to 1 by 100%
            float s = t < 0.4f
                ? Mathf.Lerp(1f, peak, t / 0.4f)
                : Mathf.Lerp(peak, 1f, (t - 0.4f) / 0.6f);

            _badge.transform.scale = new Vector3(s, s, 1f);

            if (t >= 1f)
            {
                _badge.transform.scale = Vector3.one;
                _scaleAnim.Pause();
            }
        }).Every(16); // ~60 fps
    }

    // ── Timer update ──────────────────────────────────────────────────────────

    void Update()
    {
        if (_timerLabel == null || WaveManager.Instance == null) return;

        var wm          = WaveManager.Instance;
        bool waveActive = wm.IsWaveActive;
        bool counting   = wm.IsCountingDown;
        int  waveIdx    = wm.CurrentWaveIndex;

        // Reset elapsed timer whenever a new wave begins
        if (waveIdx != _lastWaveIndex)
        {
            _elapsedSeconds = 0f;
            _lastWaveIndex  = waveIdx;
        }

        if (_skipBtn != null)
            _skipBtn.style.display = counting ? DisplayStyle.Flex : DisplayStyle.None;

        if (counting)
        {
            float remaining = wm.CountdownRemaining;
            _timerLabel.text = FormatTime(remaining, ceiling: true);

            if (remaining <= 3f && remaining > 0f)
            {
                // Flash between red and a pale highlight at ~4 Hz
                float flash = (Mathf.Sin(Time.unscaledTime * Mathf.PI * 4f) + 1f) * 0.5f;
                _timerLabel.style.color = new StyleColor(Color.Lerp(TimerFlashColour, TimerFlashHighColour, flash));

                // Wobble left-right at ~6 Hz
                float wobble = Mathf.Sin(Time.unscaledTime * Mathf.PI * 6f) * 5f;
                _timerBadge.transform.position = new Vector3(wobble, 0f, 0f);

                SetBadgeBorderColor(_timerBadge, TimerBorderRed);
            }
            else
            {
                _timerLabel.style.color = new StyleColor(TimerCountdownColour);
                _timerBadge.transform.position = Vector3.zero;
                SetBadgeBorderColor(_timerBadge, TimerBorderNormal);
            }
        }
        else if (waveActive)
        {
            _elapsedSeconds += Time.deltaTime;
            _timerLabel.text = FormatTime(_elapsedSeconds, ceiling: false);
            _timerLabel.style.color = new StyleColor(TimerNormalColour);
            _timerBadge.transform.position = Vector3.zero;
            SetBadgeBorderColor(_timerBadge, TimerBorderNormal);
        }
        else if (waveIdx < 0)
        {
            // Before the first wave starts
            _timerLabel.text = "—:——";
            _timerLabel.style.color = new StyleColor(TimerNormalColour);
            _timerBadge.transform.position = Vector3.zero;
            SetBadgeBorderColor(_timerBadge, TimerBorderNormal);
        }
        // All waves complete — leave last elapsed time displayed
    }

    // Formats seconds as M:SS.  ceiling=true rounds up (for countdown display).
    static string FormatTime(float seconds, bool ceiling)
    {
        float s = ceiling ? Mathf.Ceil(seconds) : Mathf.Floor(seconds);
        s = Mathf.Max(0f, s);
        int total = (int)s;
        return $"{total / 60}:{total % 60:00}";
    }

    static void SetBadgeBorderColor(VisualElement el, Color col)
    {
        var sc = new StyleColor(col);
        el.style.borderTopColor    = sc;
        el.style.borderBottomColor = sc;
        el.style.borderLeftColor   = sc;
        el.style.borderRightColor  = sc;
    }

    // ── Spend animation: label flashes red then fades back to gold ───────────

    void PlaySpendFlash()
    {
        // Cancel any running scale animation and reset badge size
        _scaleAnim?.Pause();
        _badge.transform.scale = Vector3.one;

        // Cancel any previous colour fade
        _colorAnim?.Pause();

        _goldLabel.style.color = new StyleColor(SpendColour);

        float start    = Time.realtimeSinceStartup;
        float duration = 0.55f; // seconds

        _colorAnim = _goldLabel.schedule.Execute(() =>
        {
            float t = Mathf.Clamp01((Time.realtimeSinceStartup - start) / duration);
            _goldLabel.style.color = new StyleColor(Color.Lerp(SpendColour, GoldColour, t));

            if (t >= 1f)
            {
                _goldLabel.style.color = new StyleColor(GoldColour);
                _colorAnim.Pause();
            }
        }).Every(16);
    }
}
