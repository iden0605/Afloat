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
    [SerializeField] private int paddingH = 10;
    [SerializeField] private int paddingV = 4;
    [Tooltip("Top margin from the screen edge (px).")]
    [SerializeField] private int topMargin = 8;
    [Tooltip("Corner radius of the badge (px).")]
    [SerializeField] private int cornerRadius = 6;

    private VisualElement _badge;
    private Label         _goldLabel;
    private int           _previousGold;
    private bool          _initialized;

    // Running animation handles — paused/replaced on each new trigger
    private IVisualElementScheduledItem _scaleAnim;
    private IVisualElementScheduledItem _colorAnim;

    private static readonly Color GoldColour  = new Color(1.00f, 0.88f, 0.30f);
    private static readonly Color SpendColour = new Color(1.00f, 0.28f, 0.22f);

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
        _badge.style.marginTop                   = topMargin;
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
        _badge.style.flexDirection               = FlexDirection.Row;
        _badge.style.alignItems                  = Align.Center;
        _badge.pickingMode                       = PickingMode.Ignore;
        screen.Add(_badge);

        // "G " prefix acts as the icon
        var prefix = new Label("G ");
        prefix.style.fontSize                  = 11;
        prefix.style.color                     = new StyleColor(new Color(0.85f, 0.65f, 0.25f, 0.85f));
        prefix.style.unityFontStyleAndWeight   = FontStyle.Bold;
        prefix.pickingMode                     = PickingMode.Ignore;
        _badge.Add(prefix);

        _goldLabel = new Label("0");
        _goldLabel.style.fontSize                = 13;
        _goldLabel.style.color                   = new StyleColor(GoldColour);
        _goldLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        _goldLabel.pickingMode                   = PickingMode.Ignore;
        _badge.Add(_goldLabel);
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
