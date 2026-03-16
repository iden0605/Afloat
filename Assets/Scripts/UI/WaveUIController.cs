using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Updates the wave counter, animated heart HP display, and Next Wave button in the HUD.
///
/// ── Scene setup ──
///   1. Add a GameObject "WaveUI" to the scene.
///   2. Attach a UIDocument component → assign WaveUI.uxml, Sort Order 5.
///   3. Attach this script to the same GameObject.
///   4. Assign the 5 heart sprites (frames 0–4) to Heart Sprites in the Inspector.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class WaveUIController : MonoBehaviour
{
    public static WaveUIController Instance { get; private set; }

    [Header("Heart Animation")]
    [Tooltip("Sprite frames in order: 0, 1, 2, 3, 4")]
    [SerializeField] private Sprite[] heartSprites;

    [Tooltip("Frames per second for the heart animation.")]
    [SerializeField] private float frameRate = 8f;

    // ── Private state ──────────────────────────────────────────────────────────
    private Label         _waveLabel;
    private Label         _healthLabel;
    private VisualElement _heartIcon;
    private Button        _nextWaveBtn;
    private Button        _pauseBtn;
    private Button        _speedBtn;
    private Button        _autoBtn;

    private float _animTime    = 0f;
    private bool  _pingPongDir = true; // true = forward, false = backward

    // Simple pause state managed by the HUD button (no PauseManager required)
    private bool  _hudPaused              = false;
    private float _preHudPauseTimeScale   = 1f;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;

        _waveLabel   = root.Q<Label>("wave-label");
        _healthLabel = root.Q<Label>("health-label");
        _heartIcon   = root.Q("heart-icon");
        _nextWaveBtn = root.Q<Button>("next-wave-btn");

        _pauseBtn    = root.Q<Button>("pause-btn");
        _speedBtn    = root.Q<Button>("speed-btn");
        _autoBtn     = root.Q<Button>("auto-btn");

        if (_nextWaveBtn != null) _nextWaveBtn.clicked += OnNextWaveClicked;
        if (_pauseBtn    != null) _pauseBtn.clicked    += OnPauseClicked;
        if (_speedBtn    != null) _speedBtn.clicked    += OnSpeedClicked;
        if (_autoBtn     != null) _autoBtn.clicked     += OnAutoClicked;

        PlayerHealthManager.OnHealthChanged += RefreshHealth;

        RefreshHealth();
    }

    void OnDisable()
    {
        if (_nextWaveBtn != null) _nextWaveBtn.clicked -= OnNextWaveClicked;
        if (_pauseBtn    != null) _pauseBtn.clicked    -= OnPauseClicked;
        if (_speedBtn    != null) _speedBtn.clicked    -= OnSpeedClicked;
        if (_autoBtn     != null) _autoBtn.clicked     -= OnAutoClicked;

        PlayerHealthManager.OnHealthChanged -= RefreshHealth;
    }

    void Update()
    {
        UpdateWaveLabel();
        UpdateNextWaveButton();
        UpdateControlButtons();
        UpdateHeartAnimation();
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    void UpdateWaveLabel()
    {
        if (WaveManager.Instance == null || _waveLabel == null) return;
        int idx = WaveManager.Instance.CurrentWaveIndex;
        _waveLabel.text = idx < 0 ? "WAVE —" : $"WAVE {idx + 1}";
    }

    void UpdateNextWaveButton()
    {
        if (WaveManager.Instance == null || _nextWaveBtn == null) return;

        bool canStart = !WaveManager.Instance.IsWaveActive
                        && (PlayerHealthManager.Instance == null || !PlayerHealthManager.Instance.IsGameOver);

        _nextWaveBtn.SetEnabled(canStart);

        if (canStart)
            _nextWaveBtn.RemoveFromClassList("hud-next-btn--disabled");
        else
            _nextWaveBtn.AddToClassList("hud-next-btn--disabled");
    }

    void UpdateHeartAnimation()
    {
        if (_heartIcon == null || heartSprites == null || heartSprites.Length == 0) return;

        int frameCount = heartSprites.Length;

        // Advance time
        _animTime += Time.deltaTime * frameRate;

        // Ping-pong: go 0→(n-1) then back (n-1)→0
        int pingPongLength = frameCount * 2 - 2; // e.g. 5 frames → 8 steps (0,1,2,3,4,3,2,1)
        int step = Mathf.FloorToInt(_animTime) % Mathf.Max(1, pingPongLength);

        int frame = step < frameCount ? step : pingPongLength - step;
        frame = Mathf.Clamp(frame, 0, frameCount - 1);

        var sprite = heartSprites[frame];
        if (sprite != null)
            _heartIcon.style.backgroundImage = new StyleBackground(sprite);
    }

    void RefreshHealth()
    {
        if (_healthLabel == null) return;

        _healthLabel.text = PlayerHealthManager.Instance != null
            ? PlayerHealthManager.Instance.CurrentHealth.ToString()
            : "—";
    }

    void UpdateControlButtons()
    {
        bool paused = PauseManager.Instance != null ? PauseManager.Instance.IsPaused : _hudPaused;
        SetCtrlActive(_pauseBtn, paused);
        SetCtrlActive(_speedBtn, WaveManager.Instance != null && WaveManager.Instance.IsDoubleSpeed);
        SetCtrlActive(_autoBtn,  WaveManager.Instance != null && WaveManager.Instance.AutoProceed);
    }

    static void SetCtrlActive(Button btn, bool active)
    {
        if (btn == null) return;
        if (active) btn.AddToClassList("hud-ctrl-btn--active");
        else        btn.RemoveFromClassList("hud-ctrl-btn--active");
    }

    /// <summary>Returns the Next Wave button's screen bounds for hint highlighting.</summary>
    public Rect GetNextWaveButtonBounds()  => _nextWaveBtn?.worldBound ?? Rect.zero;

    /// <summary>Returns a rect that covers the pause/speed/auto control buttons for hint highlighting.</summary>
    public Rect GetControlButtonsBounds()
    {
        if (_pauseBtn == null) return Rect.zero;
        Rect r = _pauseBtn.worldBound;
        if (_speedBtn != null) r = RectUnion(r, _speedBtn.worldBound);
        if (_autoBtn  != null) r = RectUnion(r, _autoBtn.worldBound);
        return r;
    }

    static Rect RectUnion(Rect a, Rect b) =>
        Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin),
                        Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));

    void OnNextWaveClicked() => WaveManager.Instance?.StartNextWave();
    void OnPauseClicked()
    {
        if (PauseManager.Instance != null)
        {
            PauseManager.Instance.QuietToggle();
            return;
        }

        // PauseManager not in scene — manage pause directly
        _hudPaused = !_hudPaused;
        if (_hudPaused)
        {
            _preHudPauseTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
        }
        else
        {
            Time.timeScale = _preHudPauseTimeScale;
        }
    }

    void OnSpeedClicked()
    {
        if (WaveManager.Instance == null) return;
        WaveManager.Instance.SetDoubleSpeed(!WaveManager.Instance.IsDoubleSpeed);
    }

    void OnAutoClicked()
    {
        if (WaveManager.Instance == null) return;
        WaveManager.Instance.AutoProceed = !WaveManager.Instance.AutoProceed;
    }
}
